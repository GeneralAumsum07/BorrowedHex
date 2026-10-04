"""Validate Lore Task 5's asset contract without implementing narrative playback."""

import json
from pathlib import Path
import numpy as np
import soundfile as sf

ROOT = Path(__file__).resolve().parents[1]
tick = ROOT/'Narrative/dialogue_tick_v01.wav'
info = sf.info(tick)
x,sr = sf.read(tick,dtype='float64')
assert sr == 22050 and info.channels == 1 and info.subtype == 'PCM_24'
assert len(x) == round(.025*sr), 'Tick duration differs from nominal 25 ms'
assert abs(np.max(np.abs(x))-.08) < .00003, 'Quiet source amplitude changed'
assert x[0] == x[-1] == 0, 'Tick endpoints must not click'

# Compare the un-enveloped interior to the plan's actual oscillator frequency,
# rather than merely testing a spectral-bin label on this very short sample.
ramp_frames = round(.005*sr)
t = np.arange(len(x))/sr
interior = slice(ramp_frames,len(x)-ramp_frames)
assert np.max(np.abs(x[interior]-.08*np.sin(2*np.pi*640*t[interior]))) < 3e-7
for edge in [slice(1,ramp_frames),slice(len(x)-ramp_frames,len(x)-1)]:
    assert np.sqrt(np.mean(x[edge]**2)) < .045, 'Five-millisecond ramp is missing'
assert np.isfinite(x).all()

meta = Path(str(tick)+'.meta').read_text(encoding='utf-8')
assert 'sampleRateSetting: 0' in meta, 'Preserve the specified source rate'
assert 'normalize: 0' in meta, 'Do not normalize away the quiet source amplitude'

shared = [
    'UI/focus_hover_v01.wav','UI/button_press_v01.wav','UI/confirm_v01.wav',
    'UI/back_cancel_v01.wav','UI/action_rejected_v01.wav','UI/toggle_on_v01.wav',
    'UI/toggle_off_v01.wav','UI/setting_increase_v01.wav','UI/setting_decrease_v01.wav',
    'Music/sanctum_intro.wav','Music/collector_battle.wav','Music/victory_results.wav',
    'Music/failure_results.wav','Collector/collector_manifest_v01.wav',
    'Stingers/short_run_victory_v01.wav','Stingers/death_defeat_v01.wav',
    'Stingers/time_expiry_defeat_v01.wav',
]
for name in shared:
    assert (ROOT/name).is_file() and Path(str(ROOT/name)+'.meta').is_file(), name
    assert sf.info(ROOT/name).frames > 0, name
assert len(list((ROOT/'Narrative').glob('*.wav'))) == 1, 'Use one fixed tick across all speakers'

report = {'status':'passed','tick':'Narrative/dialogue_tick_v01.wav',
    'sample_rate_hz':sr,'frames':len(x),'duration_ms':round(len(x)/sr*1000,6),
    'frequency_hz':640,'channels':info.channels,'peak_amplitude':float(np.max(np.abs(x))),
    'ramp_ms_nominal':5,'shared_assets_checked':len(shared),
    'runtime_playback_checked':False,'runtime_integration':'None'}
(ROOT/'Source~'/'lore_audio_qa.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
print(json.dumps(report,indent=2))
