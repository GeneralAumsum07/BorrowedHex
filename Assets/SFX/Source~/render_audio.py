"""Offline, deterministic sound design and original scoring for Borrowed Hex.

Run with the sibling requirements installed, from any working directory:
    python render_audio.py --batch 1
    python render_audio.py --batch 2
    ...
    python render_audio.py --verify

No Unity runtime code, assets, scene bindings, mixers, or profile settings are
modified. Source~ is intentionally ignored by Unity. A stable seed for each
asset makes local revisions reproducible without turning variants into clones.
"""

import argparse
import csv
import hashlib
import json
import math
from pathlib import Path
import uuid

import numpy as np
import pyloudnorm as pyln
from scipy import signal
import soundfile as sf

from catalog import ALIASES, CUES, MUSIC

ROOT = Path(__file__).resolve().parents[1]
SR = 48_000
DIALOGUE_SR = 22_050  # Lore Task 5 deliberately specifies a lower source rate.
TAU = 2 * np.pi
NAMESPACE = uuid.UUID('8ea36c76-4eed-4ea9-8394-8bc8f8b4f201')
REPORT = ROOT / 'Source~' / 'delivery.json'
METER = pyln.Meter(SR)


def rng_for(key):
    return np.random.default_rng(int.from_bytes(hashlib.sha256(key.encode()).digest()[:8], 'little'))


def timeline(seconds):
    return np.arange(round(seconds * SR), dtype=np.float64) / SR


def db(value):
    return 20 * np.log10(max(float(value), 1e-12))


def ramp(x, attack=.004, release=.025, sample_rate=SR):
    """Raised-cosine endpoints prevent hard gates, including very short UI ticks."""
    x = x.copy()
    a = min(round(attack*sample_rate), len(x)//2)
    r = min(round(release*sample_rate), len(x)//2)
    if a:
        w = np.sin(np.linspace(0,np.pi/2,a))**2
        x[:a] *= w[:,None] if x.ndim == 2 else w
    if r:
        w = np.sin(np.linspace(np.pi/2,0,r))**2
        x[-r:] *= w[:,None] if x.ndim == 2 else w
    return x


def filtered(x, low=0, high=8000):
    if low and high:
        sos = signal.butter(2,[low,high],btype='bandpass',fs=SR,output='sos')
    else:
        sos = signal.butter(2,low or high,btype='highpass' if low else 'lowpass',fs=SR,output='sos')
    return signal.sosfilt(sos,x,axis=0).astype(np.float32)


def noise(t,rng,low=100,high=6000):
    x = filtered(rng.standard_normal(len(t)).astype(np.float32),low,high)
    return x / max(np.std(x),1e-6)


def sweep(t,f0,f1,decay,brightness=.0):
    # Analytic integration avoids discontinuous frequency jumps and pitch aliasing.
    phase = TAU*(f1*t+(f0-f1)*decay*(1-np.exp(-t/decay)))
    return np.sin(phase)+brightness*np.sin(phase*2)*.35


def modes(t,root,rng,material='magic',decay=.28):
    ratios = {
        'magic':[1,1.5,2,3,4.01,6.02],
        'metal':[1,2.76,5.40,8.93,13.34],
        'stone':[1,1.61,2.39,3.84,5.17],
        # Rock has a denser, less bell-like modal response than masonry/columns.
        # Keep this name aligned with the approved material_rock_* catalog.
        'rock':[1,1.38,2.17,3.43,4.91],
        'wood':[1,2.14,3.69,5.1],
        'ceramic':[1,2.32,4.25,6.57,9.22],
        'crystal':[1,2,3.02,4.03,6.06,8.12],
    }[material]
    x = np.zeros(len(t),dtype=np.float64)
    for i,ratio in enumerate(ratios):
        f = root*ratio*rng.uniform(.985,1.015)
        if f > 15_000:
            continue
        x += np.sin(TAU*f*t)*np.exp(-t/(decay/(1+i*.18)))/(1+i)**1.25
    return x


def echo(x,delays=(.041,.083,.131),gains=(.16,.09,.045),tail=.24):
    """Finite early reflections keep rapid gameplay cues readable, not washed out."""
    y = np.pad(x,(0,round(tail*SR)))
    for delay,gain in zip(delays,gains):
        d = round(delay*SR)
        n = min(len(x),len(y)-d)
        y[d:d+n] += filtered(x[:n],high=5200)*gain
    return ramp(y,.002,.035)


def put(dst,x,seconds,gain=1,pan=0,wrap=False):
    """Place an instrument. Wrapped note tails make the music genuinely periodic."""
    if dst.ndim == 2 and x.ndim == 1:
        theta = (np.clip(pan,-1,1)+1)*np.pi/4
        x = np.column_stack((x*np.cos(theta),x*np.sin(theta)))
    start = round(seconds*SR)
    if wrap:
        start %= len(dst)
        consumed = 0
        while consumed < len(x):
            n = min(len(dst)-start,len(x)-consumed)
            dst[start:start+n] += x[consumed:consumed+n]*gain
            consumed += n
            start = 0
    else:
        if start >= len(dst) or start < 0:
            return
        n = min(len(x),len(dst)-start)
        dst[start:start+n] += x[:n]*gain


def finite_grains(t,rng,root,material,count=14):
    x = np.zeros(len(t),dtype=np.float32)
    for i in range(count):
        at = rng.uniform(0,max(.001,t[-1]-.04))
        tt = timeline(rng.uniform(.035,.16))
        grain = modes(tt,root*rng.uniform(.6,2.8),rng,material,.035)
        grain += noise(tt,rng,800,9000)*.18
        put(x,ramp(grain,.001,.014),at,rng.uniform(.08,.25))
    return x


def short_sound(cue,variant):
    key = f'{cue.group}/{cue.name}/{variant}'
    rng = rng_for(key)
    # UI and dialogue pitches stay stable; timbre changes carry their variations.
    stable = cue.group in ['UI','Narrative','Training'] or cue.recipe.startswith('stinger')
    root = cue.tone*(1 if stable else rng.uniform(.965,1.035))
    dur = cue.seconds*(1 if stable or cue.group == 'World' else rng.uniform(.965,1.035))
    t = timeline(dur)
    u = t/max(dur,.001)
    r = cue.recipe
    if r == 'dialogue':
        # Preserve the authored lore specification exactly: no variation or reverb.
        # Generate at its native rate rather than resampling the library's 48 kHz
        # master: the envelope's five milliseconds must use this same timebase.
        t = np.arange(round(.025*DIALOGUE_SR),dtype=np.float64)/DIALOGUE_SR
        return ramp(.08*np.sin(TAU*640*t),.005,.005,sample_rate=DIALOGUE_SR)
    if r.startswith('stinger'):
        return stinger(r,dur,root,rng)

    if r in ['air','dash','dash_grasp','grasp','slash','scrape','melee_windup']:
        air = noise(t,rng,350 if r != 'scrape' else 90,9000)
        arch = np.sin(np.pi*u)**(1.2 if r in ['grasp','melee_windup'] else .65)
        x = .35*air*arch
        if r in ['grasp','dash_grasp','dash']:
            x += .30*sweep(t,root*1.8,root*.25,dur*.35)*arch
            x += .14*modes(t,root,rng,'magic',dur*.6)
        if r == 'slash':
            x += .3*sweep(t,root*4,root*.4,dur*.15)*np.exp(-u*5)
        if r == 'scrape':
            x *= .35+.65*np.abs(np.sin(TAU*root*.17*t))
            x += finite_grains(t,rng,root,'stone',9)*.7
        if r == 'melee_windup':
            x += .45*sweep(t,root*.7,root*1.6,dur*.3)*u**1.3

    elif r in ['capture','perfect','seal','ready','swap','freeze','form','boss_form','heal','siphon','ignite']:
        env = np.exp(-u*(4 if r in ['capture','ready','seal'] else 2.2))
        x = modes(t,root,rng,'crystal' if r in ['perfect','ready','ignite'] else 'magic',dur*.38)*env*.35
        direction = (root*.3,root*1.6) if r in ['capture','form','boss_form','heal','siphon','ignite'] else (root*1.6,root*.5)
        x += sweep(t,*direction,dur*.2)*np.sin(np.pi*u)**.8*.35
        x += noise(t,rng,1200,9500)*np.exp(-u*8)*.07
        if r == 'perfect':
            x += .40*modes(t,root*.5,rng,'metal',dur*.65)
            x += .25*np.sin(TAU*root*1.5*t)*np.exp(-u*3)
        if r == 'swap':
            x *= .5+.5*np.cos(TAU*9*t)
        if r == 'freeze':
            x = x[::-1].copy()*.8 + modes(t,root,rng,'crystal',dur*.25)*.2
        if r in ['boss_form','siphon']:
            x += .3*sweep(t,root*1.2,root*.45,dur*.22)*np.exp(-u*2)
        if r == 'ignite':
            x += .22*noise(t,rng,300,5000)*np.exp(-u*6)

    elif r in ['deny','ui_focus','ui_press','ui_confirm','ui_back','paper_open','paper_close','paper_tick']:
        if r.startswith('paper'):
            x = .38*noise(t,rng,1400,8000)*np.sin(np.pi*u)**.8
            x *= .5+.5*np.sin(TAU*(37+root*.02)*t)**2
            if r == 'paper_close':
                x *= np.exp(-u*3)
        elif r == 'deny':
            # Preserve fixed UI pitch, but vary the beating/upper-partial texture.
            # Without these seeded parameters every UI rejection variation was
            # byte-identical because the stable-pitch path consumed no randomness.
            x = .45*np.sin(TAU*root*t)+rng.uniform(.14,.18)*np.sin(TAU*root*1.05946*t)
            x *= np.exp(-u*4)*(.55+.45*np.cos(TAU*rng.uniform(22,26)*t))
        elif r in ['ui_confirm','ui_back']:
            x = np.zeros(len(t),dtype=np.float32)
            for i,f in enumerate([root,root*(1.5 if r == 'ui_confirm' else .75)]):
                tt = timeline(dur*.58)
                ping = modes(tt,f,rng,'wood',dur*.16)
                put(x,ramp(ping,.002,.015),i*dur*.32,.5)
        else:
            x = .65*modes(t,root,rng,'wood',dur*.18)
            x += .03*noise(t,rng,1800,8000)*np.exp(-u*18)

    elif r in ['bolt','boss_bolt','scatter','rocket','riposte','magic_hit','wall_hit','pierce','resist','echo','quickdraw','orbit_hit']:
        decay = dur*.20
        x = .4*sweep(t,root*2.8,root*.7,decay,.35)*np.exp(-u*4)
        x += .20*modes(t,root,rng,'metal' if r in ['riposte','pierce','resist'] else 'magic',decay)
        if r == 'scatter':
            x = np.zeros(len(t),dtype=np.float32)
            for i in range(5):
                tt = timeline(dur*.70)
                grain = sweep(tt,root*rng.uniform(1.5,2.8),root*.7,decay)*np.exp(-tt/decay)
                grain += .18*noise(tt,rng,800,9000)*np.exp(-tt/(decay*.35))
                put(x,ramp(grain,.001,.015),i*.007,.32)
        if r in ['magic_hit','wall_hit','orbit_hit']:
            x += .30*noise(t,rng,120,6000)*np.exp(-u*11)
        if r == 'wall_hit':
            x += .35*modes(t,170,rng,'stone',.045)
        if r in ['rocket','boss_bolt']:
            x += .45*sweep(t,root*1.3,root*.35,decay)*np.exp(-u*5)
            x += .30*noise(t,rng,80,2600)*np.exp(-u*4)
        if r in ['riposte','pierce','quickdraw']:
            x += noise(t,rng,1800,11000)*np.sin(np.pi*u)**1.1*.20
        if r == 'resist':
            x = filtered(x,high=1800)*(.55+.45*np.cos(TAU*47*t))
        if r == 'echo':
            x = filtered(x,high=4500)

    elif r in ['explosion','slam','backfire','overcharge','hurt','boss_hurt','enemy_hurt','stagger','parry','gift','overflow','fusion','evolve']:
        lowroot = min(root,120) if r in ['explosion','slam','backfire'] else root
        x = .6*sweep(t,lowroot*2.4,lowroot*.5,.035)*np.exp(-u*6)
        x += .25*noise(t,rng,60,5500)*np.exp(-u*8)
        x += .15*modes(t,root,rng,'metal' if r == 'parry' else 'stone',dur*.3)
        if r in ['explosion','slam','gift']:
            x += .45*noise(t,rng,45,1500)*np.exp(-u*4)
            x += finite_grains(t,rng,330,'stone',18)*.4
        if r in ['backfire','evolve']:
            x += .20*np.sin(TAU*root*t+6*np.sin(TAU*root*1.41*t))*np.exp(-u*3)
            x += .2*noise(t,rng,1000,9000)*np.exp(-u*6)
        if r == 'parry':
            x += .9*modes(t,root,rng,'metal',dur*.8)
        if r == 'overcharge':
            for ratio in [1,1.5,2,3]:
                x += .23*sweep(t,root*ratio*1.2,root*ratio,dur*.05)*np.exp(-u*3)
        if r == 'fusion':
            x = .30*sweep(t,root*.4,root*2,dur*.3)*np.sin(np.pi*u)**.8
            x += .30*sweep(t,root*3,root,dur*.3)*np.sin(np.pi*u)**.8
            tt = timeline(dur*.65)
            put(x,ramp(modes(tt,root,rng,'crystal',dur*.3),.004,.04),dur*.35,.55)
        if r == 'overflow':
            tt = timeline(dur*.65)
            put(x,ramp(sweep(tt,root*.4,root*2,.05)*np.exp(-tt/.12)),dur*.28,.35)
        if r in ['hurt','boss_hurt','enemy_hurt','stagger']:
            # Nonverbal material/binding rupture; no recorded or synthesized speech.
            x = np.tanh(x*1.5)
            x += .2*modes(t,root,rng,'wood',.065)

    elif r in ['windup','slam_warning','summon','vortex','rise','teleport_out','teleport_in','warning','unstable','glitch','death','unbind','dissipate']:
        arch = np.sin(np.pi*u)**.8
        upward = r in ['windup','summon','vortex','rise','teleport_in']
        x = .35*sweep(t,root*(.45 if upward else 2.2),root*(2.6 if upward else .30),dur*.45)*arch
        x += .12*noise(t,rng,300,6500)*arch
        if r in ['windup','summon','rise']:
            x *= u**1.2
            x += .2*np.sin(TAU*root*1.5*t)*u**1.6
        if r == 'slam_warning':
            x = .45*sweep(t,root,root*.55,dur*.4)*arch
            x += .28*noise(t,rng,45,400)*arch
        if r in ['warning','unstable']:
            x *= .45+.55*np.sin(TAU*(5 if r == 'warning' else 17)*t)**2
        if r == 'glitch':
            # Smoothed granular gates, not discontinuous sample-and-hold clicks.
            x *= (.3+.7*np.sin(TAU*23*t)**8)
            x += finite_grains(t,rng,root,'crystal',8)*.6
        if r in ['death','unbind','dissipate']:
            x *= np.exp(-u*2.5)
            x += .24*modes(t,root,rng,'magic',dur*.35)

    elif r in ['footstep','cloth_step','rubble'] or r.startswith('material_'):
        if r.startswith('material_'):
            _,material,op = r.split('_')
        else:
            material,op = ('wood' if r == 'cloth_step' else 'stone'), ('settle' if r == 'rubble' else 'step')
        if op == 'form':
            x = finite_grains(t,rng,root,material,22)[::-1].copy()
            x += .25*modes(t,root,rng,material,dur*.3)*np.sin(np.pi*u)**.7
        elif op in ['collapse','settle']:
            x = finite_grains(t,rng,root,material,36 if op == 'collapse' else 22)
            x *= np.exp(-u*2)
            if op == 'collapse':
                x += .35*noise(t,rng,60,1300 if material in ['stone','wood'] else 8000)*np.exp(-u*8)
                x += .4*sweep(t,85,38,.045)*np.exp(-u*10)
        else:
            x = .5*modes(t,root,rng,material,.025 if op == 'step' else .055)
            x += .28*noise(t,rng,90,2200 if r == 'footstep' else 6500)*np.exp(-u*15)
            if r == 'cloth_step':
                x = filtered(x,high=3100)
    else:
        raise ValueError(f'Unauthored recipe: {r}')

    x = ramp(x,.003 if r not in ['windup','rise','summon'] else .015,.035)
    # UI stays dry; it must not leave a lingering tail when navigating rapidly.
    if cue.group == 'UI':
        return x.astype(np.float32)
    return echo(x,tail=.28 if dur < 1 else .40).astype(np.float32)


def periodic_noise(n,rng,low,high):
    """FFT band shaping gives environmental beds a periodic boundary by construction."""
    f = np.fft.rfftfreq(n,1/SR)
    spectrum = np.fft.rfft(rng.standard_normal(n))
    shape = (1-np.exp(-(f/max(low,1))**4))*np.exp(-(f/high)**4)
    spectrum *= shape
    spectrum[0] = 0
    x = np.fft.irfft(spectrum,n).astype(np.float32)
    return x / max(np.std(x),1e-6)


def periodic_tone(t,hz):
    duration = len(t)/SR
    return np.sin(TAU*round(hz*duration)/duration*t)


def loop_sound(cue):
    rng = rng_for(cue.group+'/'+cue.name)
    t = timeline(cue.seconds)
    u = t/cue.seconds
    r,root = cue.recipe,cue.tone
    if r.startswith('amb_'):
        stereo = cue.name in ['courtyard','graveyard','cave','sanctum']
        low,high = {'courtyard':(70,1100),'graveyard':(100,750),'cave':(45,600),
                    'sanctum':(35,360),'brazier':(200,7500),'blue_flame':(250,5000),'candle':(500,9000)}[cue.name]
        def channel():
            x = periodic_noise(len(t),rng,low,high)*(.50+.14*np.sin(TAU*u*3)+.10*np.sin(TAU*u*7))
            if cue.name in ['sanctum','blue_flame']:
                x += .18*periodic_tone(t,root)+.08*periodic_tone(t,root*1.5)
            if cue.name in ['brazier','blue_flame','candle']:
                for _ in range(45 if cue.name != 'candle' else 12):
                    tt = timeline(rng.uniform(.015,.065))
                    pop = ramp(noise(tt,rng,1500,10000)*np.exp(-tt/.009),.001,.012)
                    put(x,pop,rng.uniform(0,cue.seconds),rng.uniform(.08,.23),wrap=True)
            return x
        if stereo:
            base = channel()
            side = channel()*.30
            x = np.column_stack((base+side,base-side))
            if cue.name == 'cave':
                for i in range(9):
                    tt = timeline(.48)
                    drip = ramp(modes(tt,rng.uniform(1200,2400),rng,'crystal',.13),.002,.04)
                    at = rng.uniform(0,cue.seconds)
                    put(x,drip,at,.19,rng.uniform(-.8,.8),True)
                    put(x,drip,at+.19,.055,rng.uniform(-.8,.8),True)
            if cue.name == 'graveyard':
                for _ in range(5):
                    tt = timeline(1.2)
                    creak = noise(tt,rng,180,850)*np.sin(np.pi*tt/1.2)**2
                    creak *= .5+.5*np.sin(TAU*31*tt)
                    put(x,creak,rng.uniform(0,cue.seconds),.055,rng.uniform(-.8,.8),True)
            return x
        return channel()
    if r == 'heartbeat_loop':
        x = np.zeros(len(t),dtype=np.float32)
        for at,gain in [(0,1),(.23,.65)]:
            tt = timeline(.24)
            beat = ramp(sweep(tt,76,44,.025)*np.exp(-tt/.06),.006,.025)
            put(x,beat,at,gain,wrap=True)
        return x
    x = periodic_noise(len(t),rng,250 if r != 'rocket_loop' else 80,5000)*.10
    for ratio,gain in [(1,.35),(1.5,.13),(2,.08)]:
        x += periodic_tone(t,root*ratio)*gain
    modcycles = 9 if r == 'charge_loop' else 3 if r == 'orbit_loop' else 7
    x *= .65+.2*np.sin(TAU*u*modcycles)+.1*np.sin(TAU*u*2)
    if r == 'rocket_loop':
        x += periodic_noise(len(t),rng,80,1900)*.26
    return x


def midi(note):
    return 440*2**((note-69)/12)


def instrument(note,seconds,kind,rng):
    """Bowed/modal instruments are authored here; no external samples or soundfonts."""
    t = timeline(seconds)
    f = midi(note)
    x = np.zeros(len(t),dtype=np.float64)
    if kind in ['celesta','plucked','low_piano','bell']:
        ratios = [1,2,3,4.006,5.02,7.04] if kind != 'bell' else [1,2.76,4.25,5.4,8.93]
        for i,ratio in enumerate(ratios):
            rate = (1.8 if kind == 'bell' else 2.7 if kind == 'celesta' else 4.8)/(seconds*(1+i*.28))
            # Longer fundamental and shorter upper partials produce a real struck envelope.
            rate = (1.6 if kind in ['celesta','bell'] else 3.5)*(1+i*.55)/seconds
            x += np.sin(TAU*f*ratio*t)*np.exp(-t*rate)/(1+i)**1.55
        x += noise(t,rng,1200,6500)*np.exp(-t/.012)*.035
        return ramp(x,.003,.05).astype(np.float32)
    if kind in ['bowed','pad','bass']:
        attack = .12 if kind == 'bowed' else .5 if kind == 'pad' else .012
        vib = .004*np.sin(TAU*4.4*t+rng.uniform(0,TAU))
        phase = TAU*f*t + vib*np.sin(TAU*f*t)*.08
        for h in range(1,9 if kind == 'bowed' else 5):
            if f*h < 8000:
                x += np.sin(phase*h)/(h**(1.3 if kind == 'bowed' else 2.1))
        if kind == 'pad':
            x += .20*np.sin(TAU*f*1.002*t) + .20*np.sin(TAU*f*.998*t)
        env = np.minimum(t/attack,1)*np.minimum((seconds-t)/(.20 if kind != 'pad' else .8),1)
        return ramp(x*env,.004,.02).astype(np.float32)
    raise ValueError(kind)


def drum(kind,rng):
    seconds = {'low':.65,'frame':.23,'tick':.09,'metal':.42}[kind]
    t = timeline(seconds)
    if kind == 'low':
        x = sweep(t,110,43,.025)*np.exp(-t/.17)
        x += .25*noise(t,rng,80,1200)*np.exp(-t/.028)
    elif kind == 'frame':
        x = .5*modes(t,170,rng,'wood',.075)
        x += .3*noise(t,rng,400,5500)*np.exp(-t/.04)
    elif kind == 'tick':
        x = noise(t,rng,4300,11000)*np.exp(-t/.012)*.45
    else:
        x = modes(t,1100,rng,'metal',.13)*.25
    return ramp(x,.002,.025).astype(np.float32)


def room(x,wet=.17,wrap=False):
    """Stereo multi-tap diffusion. Return-to-start tails are wrapped for looping scores."""
    out = x.copy()
    taps = [.079,.113,.173,.239,.317,.419,.557,.733,1.019,1.373,1.817]
    dry = filtered(x,high=5500)
    for i,delay in enumerate(taps):
        d = round(delay*SR)
        gain = wet*np.exp(-delay/1.0)*(.65 if i < 4 else .4)
        source = dry[:,::-1] if i%2 else dry
        if wrap:
            out += np.roll(source,d,axis=0)*gain
        elif d < len(out):
            out[d:] += source[:-d]*gain
    return out


def stinger(recipe,seconds,root,rng):
    t = timeline(seconds+.8)
    x = np.zeros((len(t),2),dtype=np.float32)
    note = round(69+12*np.log2(root/440))
    offsets = {'stinger_start':[0,7,12], 'stinger_clear':[0,3,7,12],
        'stinger_boss':[0,1,7,-12], 'stinger_victory':[0,4,7,12,16],
        'stinger_failure':[7,3,0,-1,-12], 'stinger_neutral':[0,7,2]}[recipe]
    for i,offset in enumerate(offsets):
        at = i*seconds*.12
        kind = 'bell' if recipe == 'stinger_boss' else 'celesta' if recipe != 'stinger_failure' else 'low_piano'
        put(x,instrument(note+offset,max(.4,seconds-at+.5),kind,rng),at,.35,(-.3 if i%2 else .3))
    put(x,instrument(note-12,seconds+.65,'pad',rng),0,.18,0)
    if recipe in ['stinger_boss','stinger_victory']:
        put(x,drum('low',rng),0,.40)
    return ramp(room(x,.21),.01,.15)


def music_track(spec):
    id,name,bpm,bars,role,priority,notes = spec
    rng = rng_for('Music/'+name)
    if role == 'intro':
        x = np.zeros((round(7.8*SR),2),dtype=np.float32)
        tt = timeline(1.6)
        rise = sweep(tt,65,520,.7)*np.sin(np.pi*tt/1.6)**2
        put(x,ramp(rise,.05,.1),0,.35)
        for i,note in enumerate([62,64,65,69,70,74,76,77]):
            # LitPillars is floor((age - 2.2) / .4), so the FIRST light appears
            # after one interval (2.6), and the eighth coincides with the title.
            put(x,instrument(note,1.3,'celesta',rng),2.6+i*.4,.16,(-.5 if i%2 else .5))
        put(x,instrument(38,2.4,'bowed',rng),5.4,.35)
        put(x,instrument(50,2.4,'bell',rng),5.4,.19)
        return ramp(room(x,.17),.02,.15)
    beat = 60/bpm
    total = bars*4*beat
    x = np.zeros((round(total*SR),2),dtype=np.float32)
    stem = role.endswith('_stem')
    boss = role in ['boss','boss_stem']
    combat = role in ['courtyard','graveyard','cave','boss'] or stem
    progression = [[50,53,57,60],[46,50,53,57],[48,52,55,58],[45,49,52,55]]
    if role == 'victory':
        progression = [[50,54,57,61],[47,50,54,57],[43,47,50,54],[45,49,52,57]]
    elif role == 'graveyard':
        progression = [[50,53,57,60],[46,50,53,57],[43,46,50,53],[45,49,52,55]]
    elif role == 'cave':
        progression = [[50,53,57,60],[51,55,58,62],[46,50,53,57],[45,49,52,55]]
    elif boss:
        progression = [[38,41,45,49],[34,38,41,45],[39,43,46,50],[33,37,40,43]]

    # Three/four movements establish, answer, then develop a recognisable eight-note motif.
    motif = [74,77,76,69,72,70,69,65]
    answer = [69,72,74,77,76,72,70,69]
    if role == 'victory':
        motif = [74,78,76,69,73,71,69,66]
        answer = [69,73,74,78,76,73,71,69]
    if boss:
        motif = [74,70,69,73,65,64,61,62]
        answer = [69,70,73,74,77,73,70,69]
    for bar in range(bars):
        at = bar*4*beat
        movement = bar//8
        chord = progression[(bar//2)%4]
        root = chord[0]
        if not stem:
            if bar%2 == 0:
                for j,note in enumerate(chord):
                    put(x,instrument(note,8*beat+.7,'pad',rng),at,.045 if combat else .065,(j-1.5)*.35,True)
            put(x,instrument(root-12 if not boss else root,3.6*beat,'bass',rng),at,.19 if combat else .065,0,True)
            # Pizzicato and modal bell ostinati change density by movement and arena.
            steps = 8 if combat else 4 if role in ['menu','upgrade'] else 2
            for j in range(steps):
                n = chord[[0,2,1,3,2,1,0,2][j%8]]+(12 if not boss else 24)
                if role == 'failure':
                    n -= 12
                put(x,instrument(n,beat*.85,'plucked' if combat else 'low_piano',rng),
                    at+j*4*beat/steps,.075 if combat else .06,(-.35 if j%2 else .35),True)
            # Phrase breath: skip every fourth bar, and delay combat's melody until bar 8.
            if bar%4 != 3 and (not combat or movement >= 1):
                seq = motif if (bar//4)%2 == 0 else answer
                positions = [0,.75,1.5,2.75] if combat else [0,1.5,2.75]
                for j,pos in enumerate(positions):
                    note = seq[(bar%4*2+j)%8]
                    if role in ['upgrade','failure','graveyard']:
                        note -= 12
                    kind = 'bell' if role == 'graveyard' else 'bowed' if boss else 'celesta'
                    put(x,instrument(note,beat*(.7 if boss else 1.6),kind,rng),
                        at+pos*beat,.09 if boss else .12,(-.20 if j%2 else .20),True)

        if combat:
            gain = .25 if not stem else .12
            for pos in ([0,1.5,2,3.5] if boss else [0,2]):
                put(x,drum('low',rng),at+pos*beat,gain*(.92+rng.uniform(-.06,.06)),0,True)
            for pos in ([1,2.75,3] if boss else [1,3]):
                put(x,drum('frame',rng),at+pos*beat,gain*.65,rng.uniform(-.25,.25),True)
            for j in range(8):
                put(x,drum('tick',rng),at+j*beat*.5+.004*rng.uniform(-1,1),
                    .032 if not stem else .021,(-.5 if j%2 else .5),True)
            if movement >= 2 or stem:
                for pos in [.75,2.5,3.75]:
                    put(x,drum('metal',rng),at+pos*beat,.042,(-.65 if bar%2 else .65),True)
            if stem:
                for j in range(8):
                    note = chord[(j+bar)%4]+(24 if not boss else 36)
                    put(x,instrument(note,beat*.32,'plucked',rng),at+j*.5*beat,.038,
                        (-.4 if j%2 else .4),True)
            # End-of-phrase fill: keeps 24/32-bar scores from being a repeated one-bar loop.
            if bar%8 == 7:
                for j in range(6):
                    put(x,drum('frame',rng),at+(2.5+j*.25)*beat,.12 if not stem else .06,
                        -.5+j*.2,True)
    return room(x,.15 if combat else .22,True)


def master(x,peak_db,loop=False,music=False,dialogue=False,stem=False):
    x = np.asarray(x,dtype=np.float32)
    assert np.isfinite(x).all()
    if dialogue:
        return x
    if loop:
        x -= np.mean(x,axis=0)
    else:
        x = filtered(x,low=28,high=18000)
    x = np.tanh(x*.85)/.85
    if music:
        measured = METER.integrated_loudness(x)
        target = -27 if stem else -18
        x *= 10**((target-measured)/20)
    else:
        x *= 10**(peak_db/20)/max(np.max(np.abs(x)),1e-8)
    # Four-times oversampled peak check leaves headroom between digital samples too.
    truepeak = np.max(np.abs(signal.resample_poly(x,4,1,axis=0)))
    cap = 10**((-2 if music else -3)/20)
    if truepeak > cap:
        x *= cap/truepeak
    if not loop:
        x = ramp(x,.002,.03)
    return x.astype(np.float32)


def metadata(path,loop=False):
    # Never replace a GUID assigned by Unity. The deterministic GUID only seeds new assets.
    meta = Path(str(path)+'.meta')
    if meta.exists():
        return
    rel = path.relative_to(ROOT).as_posix()
    guid = uuid.uuid5(NAMESPACE,rel).hex
    if path.is_dir():
        body = f'fileFormatVersion: 2\nguid: {guid}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {{}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n'
    elif path.suffix == '.wav':
        long = path.parent.name in ['Music','Ambience']
        # Masters stay PCM24 on disk. Long Unity imports are streamed Vorbis; short
        # cues are PCM so timing-critical attacks avoid codec startup latency.
        body = f'''fileFormatVersion: 2
guid: {guid}
AudioImporter:
  externalObjects: {{}}
  serializedVersion: 7
  defaultSettings:
    serializedVersion: 2
    loadType: {2 if long else 0}
    sampleRateSetting: 0
    sampleRateOverride: 48000
    compressionFormat: {1 if long else 0}
    quality: 0.8
    conversionMode: 0
  platformSettingOverrides: {{}}
  forceToMono: 0
  normalize: 0
  preloadAudioData: {0 if long else 1}
  loadInBackground: {1 if long else 0}
  ambisonic: 0
  3D: {0 if long else 1}
  userData: {'Loop entire clip; no embedded leading/trailing silence.' if loop else 'One-shot; natural tail included.'}
  assetBundleName: 
  assetBundleVariant: 
'''
    else:
        body = f'fileFormatVersion: 2\nguid: {guid}\nDefaultImporter:\n  externalObjects: {{}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n'
    meta.write_text(body,encoding='utf-8')


def export(path,x,loop,details,sample_rate=SR):
    path.parent.mkdir(parents=True,exist_ok=True)
    metadata(path.parent)
    sf.write(path,x,sample_rate,subtype='PCM_24')
    metadata(path,loop)
    record = {'path':path.relative_to(ROOT).as_posix(),'sample_rate':sample_rate,
        'frames':len(x),'duration_seconds':round(len(x)/sample_rate,6),'channels':1 if x.ndim==1 else x.shape[1],
        'format':'PCM_24','loop':loop,'peak_dbfs':round(db(np.max(np.abs(x))),3),
        **details}
    return record


def inventory(records):
    present = {r['path']:r for r in records}
    result = []
    for c in CUES:
        paths = [f'{c.group}/{c.name}_v{i:02d}.wav' for i in range(1,c.variants+1)]
        result.append({'approval_id':c.id,'name':c.name,'priority':c.priority,
            'loop':c.loop,'note':c.note,'paths':paths,
            'status':'rendered' if all(p in present for p in paths) else 'pending'})
    for m in MUSIC:
        result.append({'approval_id':m[0],'name':m[1],'priority':m[5],
            'loop':m[4]!='intro','note':m[6],'paths':[f'Music/{m[1]}.wav'],
            'status':'rendered' if f'Music/{m[1]}.wav' in present else 'pending'})
    for id,targets in ALIASES.items():
        paths = [r['path'] for r in records if any(r['path'].startswith(target+'_v') for target in targets)]
        result.append({'approval_id':id,'name':'reuse_existing_cues','priority':0,
            'loop':False,'note':'Explicit shared cue; no new recording needed.','paths':paths,
            'status':'rendered' if paths else 'pending'})
    return result


def save_reports(records):
    records = sorted(records,key=lambda r:r['path'])
    REPORT.write_text(json.dumps(records,indent=2)+'\n',encoding='utf-8')
    manifest = ROOT/'manifest.json'
    manifest.write_text(json.dumps({'schema_version':1,'default_sample_rate':SR,
        'sample_rate_overrides':{'Narrative/dialogue_tick_v01.wav':DIALOGUE_SR},'master_format':'24-bit PCM WAV',
        'provenance':'Original procedural synthesis and composition; no external audio samples, voices, or model outputs.',
        'runtime_integration':'None. Audio assets and offline source only.',
        'auditory_review':'Not yet approved through human listening. Technical QA is separate from artistic approval.',
        'inventory':inventory(records)},indent=2)+'\n',encoding='utf-8')
    metadata(manifest)
    metadata(ROOT/'README.md')
    with (ROOT/'Source~'/'asset_measurements.csv').open('w',newline='',encoding='utf-8') as f:
        keys = ['approval_id','priority','path','sample_rate','duration_seconds','channels','loop','peak_dbfs']
        writer = csv.DictWriter(f,fieldnames=keys,extrasaction='ignore')
        writer.writeheader()
        writer.writerows(records)


def preview(batch,records):
    """Review-only montage: silence separates effects; cue times are written beside it."""
    examples = []
    seen = set()
    for r in records:
        if r['priority'] != batch or r.get('loop') or r['path'].startswith('Music/'):
            continue
        family = r['path'].rsplit('_v',1)[0]
        if family in seen:
            continue
        seen.add(family)
        examples.append(r)
    timeline_rows = []
    clips = []
    at = 0
    for r in examples:
        x,source_rate = sf.read(ROOT/r['path'],dtype='float32',always_2d=True)
        if source_rate != SR:
            # Review montages share a 48 kHz clock. Conversion here prevents the
            # 22.05 kHz tick from playing at the wrong pitch in that montage only.
            divisor = math.gcd(SR,source_rate)
            x = signal.resample_poly(x,SR//divisor,source_rate//divisor,axis=0)
        if x.shape[1] == 1:
            x = np.repeat(x,2,axis=1)*.707
        timeline_rows.append({'seconds':round(at,3),'cue':r['path']})
        clips.extend([x,np.zeros((round(.32*SR),2),dtype=np.float32)])
        at += len(x)/SR+.32
    if clips:
        directory = ROOT/'Source~'/'Review'
        directory.mkdir(exist_ok=True)
        sf.write(directory/f'batch_{batch:02d}_audition.wav',np.concatenate(clips),SR,subtype='PCM_24')
        (directory/f'batch_{batch:02d}_timeline.json').write_text(json.dumps(timeline_rows,indent=2)+'\n',encoding='utf-8')


def review_highlights():
    """Short listening-review reel, separate from the importable master library."""
    paths = ['CoreMagic/catch_snatcher_v01.wav','CoreMagic/capture_success_v01.wav',
        'CoreMagic/capture_perfect_v01.wav','CoreMagic/slot_swap_v01.wav',
        'Projectiles/parry_pursuer_v01.wav','CoreMagic/overcharge_release_v01.wav',
        'CoreMagic/packet_backfire_v01.wav','Projectiles/siege_rocket_launch_v01.wav',
        'Projectiles/siege_rocket_explosion_v01.wav','UI/button_press_v01.wav']
    clips,times = [],[]
    at = 0
    for path in paths:
        x,_ = sf.read(ROOT/path,dtype='float32',always_2d=True)
        if x.shape[1] == 1:
            x = np.repeat(x,2,axis=1)*.707
        times.append({'seconds':round(at,3),'cue':path})
        clips.extend([x,np.zeros((round(.45*SR),2),dtype=np.float32)])
        at += len(x)/SR+.45
    directory = ROOT/'Source~'/'Review'
    directory.mkdir(exist_ok=True)
    sf.write(directory/'core_highlights.wav',np.concatenate(clips),SR,subtype='PCM_24')
    (directory/'core_highlights_timeline.json').write_text(json.dumps(times,indent=2)+'\n',encoding='utf-8')


def verify(partial=False):
    records = json.loads(REPORT.read_text(encoding='utf-8'))
    failures = []
    hashes = {}
    for i,r in enumerate(records):
        path = ROOT/r['path']
        try:
            info = sf.info(path)
            x,sr = sf.read(path,dtype='float32',always_2d=True)
            peak = float(np.max(np.abs(x)))
            truepeak = float(np.max(np.abs(signal.resample_poly(x,4,1,axis=0))))
            expected_rate = DIALOGUE_SR if r['recipe'] == 'dialogue' else SR
            assert sr == expected_rate == r['sample_rate'] and info.subtype == 'PCM_24' and info.frames == r['frames']
            assert np.isfinite(x).all() and peak > .0001 and truepeak < .795
            assert np.max(np.abs(np.mean(x,axis=0))) < .004
            if r['loop']:
                jump = float(np.max(np.abs(x[-1]-x[0])))
                slope = float(np.quantile(np.abs(np.diff(x,axis=0)),.999))
                assert jump <= max(.003,slope*2), f'Loop boundary jump {jump:.5f} > neighbourhood {slope:.5f}'
                r['loop_boundary_jump'] = round(jump,7)
            else:
                assert np.max(np.abs(x[[0,-1]])) < .00002, 'Nonzero one-shot boundary'
            digest = hashlib.sha256(path.read_bytes()).hexdigest()
            assert digest not in hashes, f'Duplicate PCM file: {hashes.get(digest)}'
            hashes[digest] = r['path']
            r['sha256'] = digest
            assert Path(str(path)+'.meta').exists(), 'Missing Unity GUID'
            r['true_peak_dbtp'] = round(db(truepeak),3)
            r['rms_dbfs'] = round(db(np.sqrt(np.mean(x*x))),3)
            if r['path'].startswith('Music/'):
                r['integrated_lufs'] = round(float(METER.integrated_loudness(x)),3)
        except Exception as exc:
            failures.append({'path':r['path'],'error':str(exc)})
        if (i+1)%100 == 0:
            print(f'Validated {i+1}/{len(records)} assets',flush=True)
    # Check the entire approved catalog, including aliases and every expected variation.
    missing = [item for item in inventory(records) if item['status'] != 'rendered']
    actual = {p.relative_to(ROOT).as_posix() for p in ROOT.rglob('*.wav') if 'Source~' not in p.parts}
    expected = {r['path'] for r in records}
    if actual != expected:
        failures.append({'error':f'Asset/report mismatch: unlisted {actual-expected}; missing {expected-actual}'})
    guids = []
    for path in ROOT.rglob('*.meta'):
        if 'Source~' in path.parts:
            continue
        for line in path.read_text(encoding='utf-8').splitlines():
            if line.startswith('guid: '):
                guids.append(line[6:])
    if len(guids) != len(set(guids)):
        failures.append({'error':'Duplicate Unity GUIDs'})
    summary = {'assets':len(records),'approved_ids':len({item['approval_id'] for item in inventory(records)}),
        'pending_inventory_entries':len(missing),'failures':failures,
        'size_mib':round(sum((ROOT/r['path']).stat().st_size for r in records)/2**20,2),
        'checks':'Decode, specified PCM24 source rate (48 kHz; dialogue tick 22.05 kHz), channel/frame counts, finite data, non-silence, DC, four-times true peak, one-shot endpoints, loop seam, SHA256 uniqueness, catalog coverage, metadata GUID uniqueness.',
        'limitation':'No human listening approval or in-engine playback verification. No audio integration was performed.'}
    (ROOT/'Source~'/'qa_report.json').write_text(json.dumps(summary,indent=2)+'\n',encoding='utf-8')
    save_reports(records)
    if not partial and not failures and not missing:
        review_highlights()
    print(json.dumps(summary,indent=2),flush=True)
    return 1 if failures or (missing and not partial) else 0


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--batch',type=int,choices=range(1,7))
    parser.add_argument('--only',help='Rerender one named family or music track within its batch.')
    parser.add_argument('--verify',action='store_true')
    parser.add_argument('--partial',action='store_true',help='Allow pending catalog items while validating an intermediate delivery batch.')
    args = parser.parse_args()
    if args.verify:
        raise SystemExit(verify(args.partial))
    if args.batch is None:
        parser.error('Choose --batch 1..6 or --verify')
    records = json.loads(REPORT.read_text(encoding='utf-8')) if REPORT.exists() else []
    new = []
    for c in CUES:
        if c.priority != args.batch or (args.only and c.name != args.only):
            continue
        for v in range(1,c.variants+1):
            x = loop_sound(c) if c.loop else short_sound(c,v)
            x = master(x,c.peak_db,c.loop,dialogue=c.recipe=='dialogue')
            path = ROOT/c.group/f'{c.name}_v{v:02d}.wav'
            source_rate = DIALOGUE_SR if c.recipe == 'dialogue' else SR
            new.append(export(path,x,c.loop,{'approval_id':c.id,'priority':c.priority,'recipe':c.recipe,'variant':v},source_rate))
        print(f'{c.id:3s} {c.group}/{c.name}: {c.variants} variations',flush=True)
    for m in MUSIC:
        if m[5] != args.batch or (args.only and m[1] != args.only):
            continue
        print(f'Composing {m[0]} {m[1]}...',flush=True)
        x = music_track(m)
        loop = m[4] != 'intro'
        x = master(x,-2,loop,True,stem=m[4].endswith('_stem'))
        new.append(export(ROOT/'Music'/f'{m[1]}.wav',x,loop,
            {'approval_id':m[0],'priority':m[5],'bpm':m[2],'bars':m[3],'recipe':m[4],'variant':1}))
    if not new:
        parser.error('No assets match the requested batch/name')
    replaced = {r['path'] for r in new}
    records = [r for r in records if r['path'] not in replaced]+new
    save_reports(records)
    preview(args.batch,records)
    print(f'Batch {args.batch}: {len(new)} files exported. Total {len(records)}.',flush=True)


if __name__ == '__main__':
    main()
