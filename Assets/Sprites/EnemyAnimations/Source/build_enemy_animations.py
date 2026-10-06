"""Articulate the approved enemy pixel drawings and export full animation sheets.

The drawing brushes are recorded into semantic layers, then moved through a
joint hierarchy. This retains the authored palette and native pixel clusters
while allowing heads, arms, legs, cloth and carried coffins to move separately.
All source drafts stay read-only. No image-generation model is used.
"""

from __future__ import annotations

import hashlib
import importlib.util
import json
import math
import sys
from dataclasses import dataclass
from pathlib import Path

from PIL import Image, ImageChops, ImageDraw


ROOT = Path(__file__).resolve().parents[1]
SPRITES = ROOT.parent
sys.dont_write_bytecode = True
spec = importlib.util.spec_from_file_location("approved_v2", SPRITES / "EnemyDrafts_V2/Source/build_enemy_drafts_v2.py")
v2 = importlib.util.module_from_spec(spec)
spec.loader.exec_module(v2)
art = v2.art
SIZE = (160,128)
STATES = {"Idle": (8,8,True), "Moving": (8,12,True), "Attack": (12,12,False), "Death": (12,10,False)}
VIEWS = art.VIEWS
IDENTITY = (1.,0.,0.,0.,1.,0.)
WORK_SIZE = (256,192)
WORK_OFFSET = (80,36)


def multiply(a,b):
    return (a[0]*b[0]+a[1]*b[3], a[0]*b[1]+a[1]*b[4], a[0]*b[2]+a[1]*b[5]+a[2],
            a[3]*b[0]+a[4]*b[3], a[3]*b[1]+a[4]*b[4], a[3]*b[2]+a[4]*b[5]+a[5])


def shift(x,y):
    return (1.,0.,x,0.,1.,y)


def around(pivot,angle=0,sx=1,sy=1):
    c,s = math.cos(math.radians(angle)),math.sin(math.radians(angle))
    return multiply(shift(*pivot),multiply((c*sx,-s*sy,0,s*sx,c*sy,0),shift(-pivot[0],-pivot[1])))


def apply(m,point):
    x,y = point
    return (m[0]*x+m[1]*y+m[2],m[3]*x+m[4]*y+m[5])


def inverse(m):
    det = m[0]*m[4]-m[1]*m[3]
    a,b,d,e = m[4]/det,-m[1]/det,-m[3]/det,m[0]/det
    return (a,b,-a*m[2]-b*m[5],d,e,-d*m[2]-e*m[5])


def ease(t):
    t = max(0,min(1,t))
    return t*t*(3-2*t)


def distance(a,b):
    return (a[0]-b[0])**2+(a[1]-b[1])**2


def joints(name,view,evolved):
    side,back = view == "Side",view == "Back"
    if name == "Acolyte":
        arms = ({"R": ((46,53),(49,64),(64,58))} if side else
                {"L": ((35,50),(31,61),(35,65)) if back else ((35,50),(32,62),(43,58)),
                 "R": ((60,49),(64,59),(69,54)) if back else ((60,49),(64,59),(67,53))})
        if evolved:
            arms.update({"XL": ((33,49),(26,61),(40,66)), "XR": ((51,49),(62,41),(64,39))} if side else
                        {"XL": ((30,50),(21,45),(21,34)), "XR": ((66,50),(75,45),(75,34))})
        return arms,{},(48,39) if not side else (50,39)
    if name == "Scatter":
        arms = ({"R": ((46,53),(57,69),(68,61)) if evolved else ((46,53),(49,65),(62,59))} if side else
                {"L": ((33,54),(29,68),(26,77)) if evolved else ((33,54),(30,68),(43,64)),
                 "R": ((62,54),(67,68),(72,76)) if evolved else ((62,54),(66,68),(53,64))})
        return arms,{},(48,36) if not side else (52,36)
    if name == "Pursuer":
        if side:
            arms = ({"L": ((39,55),(26,68),(20,85)), "R": ((52,53),(67,64),(76,82))} if evolved else
                    {"L": ((36,58),(31,69),(29,78)), "R": ((53,53),(64,64),(63,76))})
            legs = {"L": ((35,75),(29,82),(35,86)),"R": ((43,74),(48,81),(54,86))}
        else:
            arms = ({"L": ((35,49),(23,61),(17,82)), "R": ((60,50),(73,63),(79,83))} if evolved else
                    {"L": ((35,49),(28,59),(29,73)), "R": ((59,50),(66,61),(64,73))})
            legs = {"L": ((41,75),(36,81),(35,86)),"R": ((55,74),(60,80),(59,86))}
        return arms,legs,(59 if evolved else 55,40) if side else (48,36 if evolved else 37)
    if side:
        return {"R": ((51,51),(59,64),(66,74))}, {"L": ((44,69),(39,77),(38,86)),"R": ((55,72),(55,80),(56,86))},(58,38)
    return {"L": ((23,47),(23,61),(23,74)),"R": ((71,48),(71,63),(72,75))}, {"L": ((35,62),(34,75),(33,86)),"R": ((60,62),(62,75),(62,86))},(48,38)


@dataclass
class Layer:
    tag: str
    image: Image.Image


class Recorder(art.Painter):
    """Capture drawing operations with their semantic helper/call-site context."""

    current = None
    context = None

    def __init__(self):
        super().__init__()
        self.recorded = []
        Recorder.current = self
        self.name,self.view,self.evolved = Recorder.context
        self.arms,self.legs,self.head = joints(*Recorder.context)
        self.emitters = {}

    def classify(self,layer,label=""):
        bbox = layer.getbbox()
        if not bbox: return "torso"
        x,y = (bbox[0]+bbox[2])/2,(bbox[1]+bbox[3])/2
        frames,frame = {},sys._getframe(2)
        while frame:
            frames.setdefault(frame.f_code.co_name,frame.f_locals)
            frame = frame.f_back
        if "bone_segment" in frames:
            call = frames["bone_segment"]
            start,end = call["start"],call["end"]
            candidates = []
            for prefix,chain in (("arm",self.arms),("leg",self.legs)):
                for key,(a,b,c) in chain.items():
                    candidates += [(distance(start,a)+distance(end,b),f"{prefix}{key}_upper"),
                                   (distance(start,b)+distance(end,c),f"{prefix}{key}_lower")]
            return min(candidates)[1]
        if "hand" in frames:
            call = frames["hand"]
            return "arm" + min(self.arms,key=lambda k: distance((call["x"],call["y"]),self.arms[k][2])) + "_lower"
        if "ember" in frames:
            call = frames["ember"]
            pos = call["x"],call["y"]
            key = "C" if self.name == "Scatter" and self.view != "Side" else min(self.arms,key=lambda k: distance(pos,self.arms[k][2]))
            self.emitters[key] = pos
            return "spell"+key
        if "mask_face" in frames or "skull" in frames: return "head"
        if "coffin" in frames: return "coffin"
        if "ribcage" in frames: return "torso"
        if "robe" in frames or label == "robe": return "robe"
        if "choir_collar" in frames: return "collarL" if x < 48 else "collarR"
        if label == "hood": return "head"
        # Helper-independent seam lines belong to the same rigid part as the
        # material they describe; isolated details must not float over a joint.
        if bbox[3] <= 50 and abs(x-self.head[0]) < 14 and y <= 44:
            if self.name != "Siege" or (bbox[1] >= 25 and bbox[0] >= 36 and bbox[2] <= 68):
                return "head"
        if self.name in ("Acolyte","Scatter"):
            if self.view == "Side":
                if "binding" in frames or y >= 68: return "robe"
                if bbox[1] >= 50 and bbox[3] <= 68 and x >= 42: return "armR_upper"
            else:
                if 48 <= y <= 69 and (x <= 35 or x >= 61): return "armL_upper" if x < 48 else "armR_upper"
                if y >= 70 or "binding" in frames: return "robe"
            return "torso"
        if self.name == "Pursuer":
            if y >= 83 and 28 < x < 69 and bbox[3] >= 87:
                return "leg" + min(self.legs,key=lambda k: distance((x,y),self.legs[k][2])) + "_foot"
            # Long evolved claws extend below the hip but remain attached to
            # forearms, rather than being mistaken for feet by a y-only split.
            if y > 76 and (x < 27 or x > 70): return "armL_lower" if x < 48 else "armR_lower"
            if 77 <= y <= 83: return "leg" + min(self.legs,key=lambda k: distance((x,y),self.legs[k][1])) + "_lower"
            return "torso"
        if self.name == "Siege":
            if y > 80 and bbox[1] >= 80:
                return "leg" + min(self.legs,key=lambda k: distance((x,y),self.legs[k][2])) + "_foot"
            if y >= 69 and ((self.view != "Side" and (x < 29 or x > 65)) or (self.view == "Side" and x > 60)):
                return "armL_lower" if self.view != "Side" and x < 48 else "armR_lower"
            if y >= 66: return "leg" + min(self.legs,key=lambda k: distance((x,y),self.legs[k][1])) + "_upper"
            if "rock_plate" in frames and ((x < 33 or x > 64) if self.view != "Side" else (y < 65 and x > 50)):
                return "armL_upper" if self.view != "Side" and x < 48 else "armR_upper"
            if y < 42 and (x < 36 or x > 66): return "coffin"
        return "torso"

    def capture(self,method,*args,**kwargs):
        previous = self.im
        self.im = Image.new("RGBA",(96,96))
        getattr(super(),method)(*args,**kwargs)
        layer = self.im
        tag = self.classify(layer,kwargs.get("label",""))
        self.im = previous
        self.im.alpha_composite(layer)
        if layer.getbbox():
            # Adjacent operations may be merged without changing draw order.
            # Non-adjacent parts retain explicit overlap relationships.
            if self.recorded and self.recorded[-1].tag == tag:
                self.recorded[-1].image.alpha_composite(layer)
            else:
                self.recorded.append(Layer(tag,layer))

    def shape(self,*args,**kwargs): self.capture("shape",*args,**kwargs)
    def ellipse(self,*args,**kwargs): self.capture("ellipse",*args,**kwargs)
    def line(self,*args,**kwargs): self.capture("line",*args,**kwargs)
    def dot(self,*args,**kwargs): self.capture("dot",*args,**kwargs)
    def rect(self,*args,**kwargs): self.capture("rect",*args,**kwargs)


def record(name,view,evolved):
    Recorder.context = name,view,evolved
    old_painter,old_palette = art.Painter,art.P
    art.Painter,art.P = Recorder,v2.DARK_PALETTE if evolved else v2.ORIGINAL_PALETTE
    try:
        neutral = art.FAMILIES[name][0](view,evolved)
        rig = Recorder.current
    finally:
        art.Painter,art.P = old_painter,old_palette
    accepted_path = SPRITES / "EnemyDrafts_V2" / (name+("_Evolved" if evolved else ""))
    stem = name+("_Evolved" if evolved else "")+"_"+view
    accepted = Image.open(accepted_path / (stem+("_Base" if evolved else "")+".png")).convert("RGBA")
    assert neutral.tobytes() == accepted.tobytes(), (name,view,"Recording changed approved artwork")
    return rig


def pose(rig,state,index):
    count,fps,_ = STATES[state]
    t = index/(count-1)
    phase = 2*math.pi*index/count
    wave = math.sin(phase)
    side,back = rig.view == "Side",rig.view == "Back"
    family = rig.name
    upper,lower,thigh,knee = {},{},{},{}
    dx,dy,lean,head_angle,coffin_angle,cloth = 0.,0.,0.,0.,0.,0.
    collar_open,flash = 0.,0.
    if state == "Idle":
        dy = -.7*wave
        head_angle = 1.8*math.sin(phase+.7)
        coffin_angle = .8*math.sin(phase-1)
        cloth = .9*math.sin(phase-.9)
        for key in rig.arms:
            upper[key] = 2*math.sin(phase+(1 if key.endswith("R") else -1))
            lower[key] = -1.5*wave
    elif state == "Moving":
        dy = -abs(math.sin(phase))* (2 if family != "Siege" else 1)
        lean = (7 if family == "Pursuer" else 3) if side else 1.2*wave
        head_angle = -lean*.5 + 2*math.sin(phase+.8)
        coffin_angle = 2.6*math.sin(phase-1)
        cloth = 2.6*math.sin(phase-1.2)
        for key in rig.arms:
            sign = 1 if key.endswith("R") else -1
            amplitude = 13 if family == "Pursuer" else 6 if family == "Siege" else 5
            upper[key] = sign*amplitude*wave
            lower[key] = -sign*7*math.sin(phase-.5)
        for key in rig.legs:
            sign = 1 if key == "R" else -1
            thigh[key] = sign*(17 if family == "Pursuer" else 11)*wave
            knee[key] = -max(0,sign*wave)*(24 if family == "Pursuer" else 14)
    elif state == "Attack":
        # Anticipation, a brief release, then recovery have different timing.
        # A sine loop would read as waving rather than a deliberate attack.
        wind = ease(index/4) if index <= 4 else 1-ease((index-4)/4)
        strike = [0,0,0,0,.12,.65,1,.72,.35,.12,0,0][index]
        recover = ease((index-7)/4)
        if family == "Pursuer":
            dx = (-2*wind+8*strike) if side else .8*wind
            dy = (3*strike*(-1 if back else 1)) if not side else 1.3*strike
            lean = (-7*wind+15*strike) if side else -3*wind+5*strike
            head_angle = -lean*.4
            for key in rig.arms:
                sign = 1 if key.endswith("R") else -1
                # Sweep the long claws outward and upward. The opposite sign
                # drives a hanging forearm into the floor during a frontal hit.
                upper[key] = (-22*wind-35*strike) if side else sign*(22*wind-40*strike)
                lower[key] = (28*wind-20*strike) if side else sign*(-28*wind+20*strike)
        elif family == "Siege":
            dy = 2.3*wind-2*strike
            lean = -5*wind+8*strike if side else 2*strike
            coffin_angle = -5*wind+6*strike
            head_angle = 4*wind-8*strike
            for key in rig.arms:
                sign = 1 if key.endswith("R") else -1
                upper[key] = sign*(-8*wind+12*strike)
                lower[key] = sign*(12*wind-18*strike)
            flash = strike
        else:
            dy = -1.1*wind+1.5*strike
            lean = -3*wind+4*strike if side else 1.5*strike
            head_angle = 4*wind-7*strike
            collar_open = (.055*wind+.09*strike) if family == "Scatter" else 0
            cloth = -1.7*wind+2.5*strike
            for key in rig.arms:
                sign = 1 if key.endswith("R") else -1
                upper[key] = sign*(-17*wind-25*strike)*(1.3 if key.startswith("X") else 1)
                lower[key] = sign*(-20*wind+12*strike)
            flash = strike
        # Recovery returns all parts exactly to the approved pose at the end.
        if index == count-1:
            dx=dy=lean=head_angle=coffin_angle=cloth=collar_open=flash=0
            upper = {key:0 for key in rig.arms}
            lower = upper.copy()
    elif state == "Death":
        fall = ease((index-2)/7)
        recoil = math.sin(min(1,index/3)*math.pi)*4
        lean = (78 if not back else -78)*fall - recoil
        dx = -recoil*.6 if side else 0
        head_angle = 20*fall-6*recoil
        coffin_angle = -10*fall
        cloth = 3*math.sin(t*math.pi)
        for key in rig.arms:
            sign = 1 if key.endswith("R") else -1
            upper[key] = sign*(16*fall+recoil)
            lower[key] = -sign*35*fall
        for key in rig.legs:
            sign = 1 if key == "R" else -1
            thigh[key],knee[key] = sign*18*fall,-sign*25*fall
    root = multiply(shift(dx,dy),around((48,88) if state == "Death" else (48,67),lean))
    # Mild chest breathing does not scale the head, legs or individual bones.
    matrices = {"torso":root,"robe":root,
                "head":multiply(root,around(rig.head,head_angle)),
                "coffin":multiply(root,around((40 if side else 48,58),coffin_angle)),
                "collarL":multiply(root,around((48,47),-collar_open*35,1+collar_open,1)),
                "collarR":multiply(root,around((48,47),collar_open*35,1+collar_open,1))}
    for key,(shoulder,elbow,wrist) in rig.arms.items():
        up = multiply(root,around(shoulder,upper.get(key,0)))
        low = multiply(up,around(elbow,lower.get(key,0)))
        if state != "Death" and rig.name == "Pursuer" and rig.evolved:
            # The stretched hands must clear the ground even while the chest
            # pitches forward. Adjust the elbow, keeping the feet and root fixed.
            boxes = [layer.image.getbbox() for layer in rig.recorded if layer.tag == "arm"+key+"_lower"]
            corners = [(x,y) for box in boxes for x in (box[0],box[2]) for y in (box[1],box[3])]
            if corners and max(apply(low,p)[1] for p in corners) > 91:
                for correction in range(1,91):
                    candidates = [multiply(up,around(elbow,lower.get(key,0)+sign*correction)) for sign in (-1,1)]
                    valid = [m for m in candidates if max(apply(m,p)[1] for p in corners) <= 91]
                    if valid:
                        low = valid[0]
                        break
        matrices["arm"+key+"_upper"],matrices["arm"+key+"_lower"] = up,low
        if key in rig.emitters:
            emitter = rig.emitters[key]
            flame = around(emitter,0,1+.08*wave,1+.1*math.sin(phase+1))
            matrices["spell"+key] = multiply(low,flame)
    matrices["spellC"] = multiply(root,around(rig.emitters.get("C",(48,60)),0,1+.1*wave,1+.12*wave))
    for key,(hip,joint,foot) in rig.legs.items():
        up = multiply(root,around(hip,thigh.get(key,0)))
        low = multiply(up,around(joint,knee.get(key,0)))
        matrices["leg"+key+"_upper"],matrices["leg"+key+"_lower"] = up,low
        matrices["leg"+key+"_foot"] = multiply(low,around(foot,-thigh.get(key,0)*.5))
    return matrices,cloth,flash,phase,t


def drape(image,amount):
    # Scanline offsets let the hem trail the body without smearing pixel detail.
    # Their weight starts below the shoulders, keeping the collar attached.
    if abs(amount) < .3: return image
    result = Image.new("RGBA",image.size)
    for y in range(96):
        weight = ease((y-54)/33)
        result.paste(image.crop((0,y,96,y+1)),(round(amount*weight),y))
    return result


def outline(image):
    alpha = image.getchannel("A")
    expanded = alpha.copy()
    for dx,dy in ((-1,0),(1,0),(0,-1),(0,1)):
        moved = Image.new("L",image.size)
        moved.paste(alpha,(dx,dy))
        expanded = ImageChops.lighter(expanded,moved)
    result = Image.new("RGBA",image.size,art.OUTLINE)
    result.putalpha(expanded)
    result.alpha_composite(image)
    return result


def attack_flash(canvas,rig,matrices,flash,index):
    if flash <= .05 or rig.name == "Pursuer": return
    d = ImageDraw.Draw(canvas)
    mat = "cyan" if rig.name == "Scatter" else "amber"
    palette = art.P[mat]
    if rig.name == "Siege":
        origin = (64,57) if rig.view == "Side" else (48,59)
        transformed = apply(matrices["torso"],origin)
    elif rig.name == "Scatter" and rig.view != "Side":
        transformed = apply(matrices["torso"],(48,50))
    else:
        key = "R" if "R" in rig.emitters else next(iter(rig.emitters))
        transformed = apply(matrices["spell"+key],rig.emitters[key])
    x,y = round(transformed[0]+WORK_OFFSET[0]),round(transformed[1]+WORK_OFFSET[1])
    radius = round((7 if rig.name == "Siege" else 5)*flash)
    d.polygon([(x-radius,y),(x,y-radius),(x+radius,y),(x,y+radius)],fill=palette[2])
    d.polygon([(x-radius+2,y),(x,y-radius+2),(x+radius-2,y),(x,y+radius-2)],fill=palette[3])
    d.point((x,y),fill=palette[4])
    if rig.name == "Scatter":
        # A fan of short, separated vocal sparks communicates scatter without
        # baking the gameplay projectile or its hitbox into the actor sprite.
        for n in (-1,0,1):
            tx,ty = (x+radius+3,y+n*5) if rig.view == "Side" else (x+n*6,y+radius+2)
            d.line((tx-1,ty,tx+1,ty),fill=palette[3],width=1)


def render(rig,state,index):
    matrices,cloth,flash,phase,t = pose(rig,state,index)
    work = Image.new("RGBA",WORK_SIZE)
    for layer in rig.recorded:
        image = drape(layer.image,cloth) if layer.tag == "robe" else layer.image
        m = multiply(shift(*WORK_OFFSET),matrices.get(layer.tag,matrices["torso"]))
        transformed = image.transform(WORK_SIZE,Image.Transform.AFFINE,inverse(m),Image.Resampling.NEAREST)
        work.alpha_composite(transformed)
    attack_flash(work,rig,matrices,flash,index)
    work = outline(work)
    bbox = work.getbbox()
    # Draw falling parts on an oversized scratch canvas before grounding them.
    # Cropping first would silently amputate limbs during the collapse.
    if state == "Death":
        left,top = 48,bbox[3]-115
        if bbox[0] < left+7: left = bbox[0]-7
        if bbox[2] > left+153: left = bbox[2]-153
    else:
        left,top = 48,9
    body = work.crop((left,top,left+160,top+128))
    # Canonical transparent pixels make raw frame hashes stable when a frame is
    # composited into a sheet. Hidden RGB must not depend on a scratch-layer fill.
    canonical_body = Image.new("RGBA",SIZE)
    canonical_body.alpha_composite(body)
    body = canonical_body
    bounds = body.getbbox()
    assert bounds and bounds[0] > 5 and bounds[1] > 5 and bounds[2] < 155 and bounds[3] < 123, (rig.name,rig.view,state,index,bounds)
    assert set(body.getchannel("A").get_flattened_data()) == {0,255}
    aura = Image.new("RGBA",SIZE)
    if rig.evolved:
        strength = .94+.14*math.sin(phase+.5)
        if state == "Death": strength *= max(.2,1-.8*ease((index-4)/7))
        aura = v2.aura_layer(body,strength)
    canonical_aura = Image.new("RGBA",SIZE)
    canonical_aura.alpha_composite(aura)
    aura = canonical_aura
    composite = Image.new("RGBA",SIZE)
    composite.alpha_composite(v2.compose(body,aura) if rig.evolved else body)
    cb = composite.getbbox()
    assert cb[0] > 0 and cb[1] > 0 and cb[2] < 160 and cb[3] < 128
    return body,aura,composite


def inventory():
    snapshot = {}
    for name in ("EnemyDrafts","EnemyDrafts_V2"):
        for path in (SPRITES/name).rglob("*"):
            if path.is_file() and path.suffix != ".meta" and "__pycache__" not in path.parts:
                snapshot[path.relative_to(SPRITES).as_posix()] = hashlib.sha256(path.read_bytes()).hexdigest()
    return snapshot


def preview(all_frames,view):
    # One sheet of moving examples lets the owner inspect every character and
    # state together; previews are not used as game textures or sprite sources.
    names = list(all_frames)
    background = (21,23,34)
    animation = []
    for tick in range(72):
        board = Image.new("RGB",(2680,1310),background)
        d = ImageDraw.Draw(board)
        d.text((28,20),f"BORROWED HEX · ENEMY ANIMATIONS · {view.upper()}",font=art.font(27,True),fill="#eee3d0")
        d.text((30,58),"Idle and movement loop. Attack and death restart here for review. All sprites shown at 2×.",font=art.font(16),fill="#a3a7b6")
        for col,name in enumerate(names):
            x = 28+col*332
            label = name.replace("_Evolved"," +")
            d.text((x+98,90),label,font=art.font(17,True),fill="#c1a4e6" if "Evolved" in name else "#eee3d0")
            for row,(state,(count,fps,_)) in enumerate(STATES.items()):
                y = 129+row*284
                frame_index = int(tick/12*fps)%count
                im = all_frames[name][view,state][frame_index]
                window = im.resize((320,256),Image.Resampling.NEAREST)
                # Columns have ample spacing around standing sprites. A corpse
                # is allowed to be wider; neighbouring cells remain independent.
                board.paste(window,(x,y),window)
                d.text((x+130,y+258),state.upper(),font=art.font(12),fill="#a3a7b6")
        d.text((30,1284),"160 × 128 game frames · 32 PPU · evolved aura follows the current body silhouette · left uses side flipping",font=art.font(15),fill="#a3a7b6")
        animation.append(board.quantize(colors=256,method=Image.Quantize.MEDIANCUT,dither=Image.Dither.NONE))
    path = ROOT / "Previews" / f"Enemies_{view}.gif"
    path.parent.mkdir(exist_ok=True)
    animation[0].save(path,save_all=True,append_images=animation[1:],duration=[83,83,84]*24,loop=0,disposal=2,optimize=False)
    animation[6].convert("RGB").save(ROOT / "Previews" / f"Enemies_{view}_Overview.png")


def build():
    before = inventory()
    all_frames,characters = {},[]
    total = 0
    for name in art.FAMILIES:
        for evolved in (False,True):
            character = name+("_Evolved" if evolved else "")
            folder = ROOT / character
            folder.mkdir(parents=True,exist_ok=True)
            masters = [Image.new("RGBA",(1920,1536)) for _ in range(3)]
            sequences,frames_for_preview = [],{}
            for view_index,view in enumerate(VIEWS):
                rig = record(name,view,evolved)
                for state_index,(state,(count,fps,loop)) in enumerate(STATES.items()):
                    row = view_index*4+state_index
                    strips = [Image.new("RGBA",(160*count,128)) for _ in range(3)]
                    hashes,bounds,preview_frames = [],[],[]
                    for frame in range(count):
                        rendered = render(rig,state,frame)
                        for layer_index,im in enumerate(rendered):
                            strips[layer_index].alpha_composite(im,(frame*160,0))
                            masters[layer_index].alpha_composite(im,(frame*160,row*128))
                        body,aura,composite = rendered
                        hashes.append(hashlib.sha256(composite.tobytes()).hexdigest())
                        bounds.append(composite.getbbox())
                        preview_frames.append(composite)
                    # A completed cycle must contain actual pose changes, not
                    # repeated exports of one image with a pulsing aura alone.
                    body_hashes = {hashlib.sha256(strips[0].crop((i*160,0,(i+1)*160,128)).tobytes()).hexdigest() for i in range(count)}
                    assert len(body_hashes) >= (5 if state in ("Idle","Moving") else 9), (character,view,state,"Insufficient articulated poses",len(body_hashes))
                    stem = f"{character}_{view}_{state}"
                    strips[2].save(folder / f"{stem}.png")
                    if evolved:
                        strips[0].save(folder / f"{stem}_Base.png")
                        strips[1].save(folder / f"{stem}_Aura.png")
                    sequence = {"view":view,"state":state,"frames":count,"fps":fps,"loop":loop,
                                "masterRow":row,"file":f"{character}/{stem}.png",
                                "frameHashes":hashes,"visibleBounds":bounds}
                    if state == "Attack": sequence["releaseFrame"] = 6
                    if state == "Death": sequence["holdFinalFrame"] = True
                    if evolved: sequence.update(baseFile=f"{character}/{stem}_Base.png",auraFile=f"{character}/{stem}_Aura.png")
                    sequences.append(sequence)
                    frames_for_preview[view,state] = preview_frames
                    total += count
                print(f"Rendered {character} {view}: idle, movement, attack, death",flush=True)
            master = f"{character}/{character}_Complete.png"
            masters[2].save(ROOT / master)
            record_data = {"character":character,"category":name,"evolved":evolved,"masterFile":master,"sequences":sequences}
            if evolved:
                masters[0].save(folder / f"{character}_Complete_Base.png")
                masters[1].save(folder / f"{character}_Complete_Aura.png")
                record_data.update(baseMasterFile=f"{character}/{character}_Complete_Base.png",auraMasterFile=f"{character}/{character}_Complete_Aura.png")
            characters.append(record_data)
            all_frames[character] = frames_for_preview
    manifest = {"status":"complete enemy animation sprite sheets","frameSize":SIZE,"pixelsPerUnit":32,
                "pivot":[.5,13/128],"masterSize":[1920,1536],"masterColumns":12,
                "rowOrder":[f"{view}_{state}" for view in VIEWS for state in STATES],
                "flipSideForLeft":True,"filterMode":"Point","mipmaps":False,"compression":"None",
                "attackReleaseFrame":6,"deathHoldsCorpse":True,
                "aura":"separate RGBA layer plus composite sheets; mask follows each articulated pose",
                "characters":characters,"totalBodyFrames":total}
    (ROOT / "manifest.json").write_text(json.dumps(manifest,indent=2)+"\n")
    (ROOT / "Source/approved_drafts_snapshot.json").write_text(json.dumps(before,indent=2)+"\n")
    assert inventory() == before, "An approved draft was modified"
    if "--no-previews" not in sys.argv:
        for view in VIEWS:
            preview(all_frames,view)
            print(f"Saved animated {view} preview",flush=True)
    print(json.dumps({"characters":8,"sequences":96,"bodyFrames":total,"evolvedAuraFrames":total//2,"output":str(ROOT)},indent=2))


if __name__ == "__main__":
    build()
