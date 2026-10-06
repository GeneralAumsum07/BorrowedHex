"""Deterministic, authored pixel drafts for Borrowed Hex's four enemy families.

Run from any working directory with Python + Pillow. Every silhouette, plate,
cloth panel, binding and mutation below is drawn explicitly. No image model,
downloaded character, stochastic texture or interpolation is used. The existing
Collector is only placed on the review board to compare native pixel density.

The drawings use a 96px drafting space, translated into the Collector's 160x128
canvas. A shared ground anchor lets a later animator preserve hitbox placement.
"""

from __future__ import annotations

import hashlib
import json
import math
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont, ImageChops, ImageFilter


ROOT = Path(__file__).resolve().parents[1]
PROJECT = ROOT.parents[2]
SIZE = (160, 128)
OFFSET = (32, 27)
GROUND = 115
OUTLINE = (16, 16, 27, 255)

# Each ramp models one material, rather than adding unrelated colours as noise.
# Cool shadow hues connect the cast to the Collector; warm accents distinguish
# the casting, chasing, chorus and siege roles at their real gameplay size.
RAMPS = {
    "wine": ["231d30", "382438", "553044", "773b4b", "a5585b"],
    "wine_e": ["21182c", "342235", "4e2a3d", "713a49", "a45558"],
    "blue": ["171f30", "223044", "31455b", "456274", "6b8c93"],
    "blue_e": ["171c2b", "253040", "344855", "46666c", "689796"],
    "cloth": ["191b2c", "25283a", "34374e", "4a4d63", "686b7e"],
    "leather": ["251f29", "3a2d32", "55403b", "765548", "9a7860"],
    "bone": ["393542", "5c5257", "887b77", "b4a18c", "d8c9a9"],
    "bone_e": ["2c2836", "50434c", "807071", "b09785", "d6bea1"],
    "iron": ["191b29", "30323f", "484b55", "686d73", "92999a"],
    "stone": ["191c2a", "2a303c", "424b55", "5e6870", "859296"],
    "wood": ["251d27", "392831", "533838", "754c43", "9b7054"],
    "brass": ["332c30", "5b4540", "8b6850", "b38e62", "d8b879"],
    "amber": ["6d352f", "a85135", "d17a41", "efa94f", "ffe3a0"],
    "cyan": ["244855", "326c78", "50999b", "8ac7be", "d3e7cb"],
    "red": ["452534", "75323c", "aa4547", "d36c58", "f7b98e"],
    "void": ["11111e", "181728", "212033", "2c2940", "39354a"],
}


def rgba(hex_string):
    return tuple(int(hex_string[i:i + 2], 16) for i in (0, 2, 4)) + (255,)


P = {name: [rgba(c) for c in ramp] for name, ramp in RAMPS.items()}


class Painter:
    """Small palette brush with explicit forms and clustered, quantized light."""

    def __init__(self):
        self.im = Image.new("RGBA", (96, 96))
        self.layers = []

    def shape(self, points, material, mode="round", light=0.0, label=""):
        mask = Image.new("L", self.im.size)
        d = ImageDraw.Draw(mask)
        d.polygon(points, fill=255)
        bounds = mask.getbbox()
        if not bounds:
            return
        left, top, right, bottom = bounds
        width, height = max(1, right-left), max(1, bottom-top)
        px, mp = self.im.load(), mask.load()
        pal = P[material]
        for y in range(top, bottom):
            for x in range(left, right):
                if not mp[x, y]:
                    continue
                u = (x-left) / width
                v = (y-top) / height
                # Discrete material planes preserve deliberate pixel clusters.
                # Fabric has broad folds; stone has faceted planes. Neither gets
                # random speckling, which would obscure silhouettes at 1x scale.
                if mode == "cloth":
                    field = 1.8 + 0.95*math.cos((u*3.4-v*0.24)*math.pi*2) - 0.5*u - 0.3*v
                elif mode == "flat":
                    field = 2.9 - 1.6*u - 0.65*v
                elif mode == "stone":
                    field = 2.8 - 1.35*u - 0.8*v + (0.45 if u < .42 and v < .45 else 0)
                else:
                    field = 2.7 - 1.8*abs(u-.36) - .6*v
                index = max(0, min(4, int(field+light)))
                px[x, y] = pal[index]
        if label:
            self.layers.append((label, mask.copy()))

    def ellipse(self, box, material, light=0.0):
        mask = Image.new("L", self.im.size)
        ImageDraw.Draw(mask).ellipse(box, fill=255)
        left, top, right, bottom = box
        px, mp = self.im.load(), mask.load()
        for y in range(max(0, top), min(96, bottom+1)):
            for x in range(max(0, left), min(96, right+1)):
                if mp[x, y]:
                    u = (x-left)/max(1, right-left)
                    v = (y-top)/max(1, bottom-top)
                    k = int(3.3-2.2*abs(u-.3)-1.4*abs(v-.28)+light)
                    px[x, y] = P[material][max(0, min(4, k))]

    def line(self, points, material, shade=2, width=1):
        color = P[material][shade] if material in P else material
        ImageDraw.Draw(self.im).line(points, fill=color, width=width)

    def dot(self, x, y, material, shade=2):
        self.im.putpixel((x, y), P[material][shade])

    def rect(self, box, material, shade=2):
        ImageDraw.Draw(self.im).rectangle(box, fill=P[material][shade])

    def finish(self):
        # A 4-connected one-pixel edge avoids the inflated diagonal corners of a
        # square dilation. It belongs outside the silhouette, never replaces
        # authored highlights or internal one-pixel features.
        a = self.im.getchannel("A")
        expanded = a.copy()
        for dx, dy in [(-1, 0), (1, 0), (0, -1), (0, 1)]:
            shifted = Image.new("L", a.size)
            shifted.paste(a, (dx, dy))
            expanded = ImageChops.lighter(expanded, shifted)
        edged = Image.new("RGBA", self.im.size, OUTLINE)
        edged.putalpha(expanded)
        edged.alpha_composite(self.im)
        canvas = Image.new("RGBA", SIZE)
        canvas.alpha_composite(edged, OFFSET)
        return canvas


def bone_segment(p, start, end, width=3, evolved=False):
    """Draw bone as a dark shaft with a bevel and actual joint separation."""
    mat = "bone_e" if evolved else "bone"
    x0, y0 = start
    x1, y1 = end
    p.line([start, end], mat, 0, width+2)
    p.line([start, end], mat, 2, width)
    p.line([(x0-1, y0-1), (x1-1, y1-1)], mat, 3, max(1, width-1))
    for x, y in [start, end]:
        p.ellipse((x-2, y-2, x+1, y+1), mat)
        p.dot(x-1, y-1, mat, 4)


def hand(p, x, y, direction=1, evolved=False):
    mat = "bone_e" if evolved else "bone"
    p.shape([(x-2,y-2),(x+2,y-2),(x+3,y+2),(x,y+4),(x-2,y+2)],mat)
    # Unequal finger lengths give a grasping pose without the mitten silhouette
    # that results from representing the whole hand by one rectangle.
    for i, length in enumerate([4, 5, 4]):
        xx = x-1+i
        p.line([(xx,y), (xx+direction*(2+i),y-length), (xx+direction*(3+i),y-length+1)],mat,3)
    p.line([(x-1,y+2),(x-4,y),(x-3,y-2)],mat,2)


def ember(p, x, y, mat="amber", large=False):
    # Hard-edged flame clusters, not translucent blur, preserve alpha and palette.
    s = 2 if large else 1
    p.shape([(x-3*s,y+2*s),(x-4*s,y-s),(x-2*s,y-5*s),
             (x,y-2*s),(x+s,y-8*s),(x+3*s,y-3*s),(x+3*s,y+2*s),(x,y+4*s)],mat,"flat")
    p.line([(x-1,y+2),(x-1,y-1),(x+1,y-4)],mat,3,2)
    p.line([(x,y+1),(x,y-2)],mat,4)


def binding(p, x, y, length, material="brass"):
    p.line([(x,y),(x,y+length)],material,1,2)
    for yy in range(y, y+length-1, 5):
        p.line([(x-1,yy),(x+1,yy+1),(x,yy+2)],material,3)


def mask_face(p, x, y, back=False, side=False, evolved=False, glow="amber"):
    mat = "bone_e" if evolved else "bone"
    if back:
        return
    if side:
        p.shape([(x-4,y-6),(x+1,y-7),(x+5,y-3),(x+6,y+1),
                 (x+3,y+2),(x+3,y+6),(x-1,y+7),(x-4,y+3)],mat,"flat")
        p.line([(x+1,y-2),(x+4,y-2),(x+3,y)],"void",0)
        p.dot(x+3,y-2,glow,3)
        p.line([(x+2,y+2),(x+1,y+4),(x+3,y+4)],"void",1)
        p.line([(x-2,y+5),(x+1,y+6)],mat,1)
        return
    p.shape([(x-5,y-6),(x-2,y-8),(x+2,y-8),(x+5,y-5),
             (x+4,y+3),(x+2,y+7),(x-2,y+7),(x-4,y+3)],mat,"flat")
    p.line([(x-3,y-2),(x-1,y-1)],"void",0,2)
    p.line([(x+1,y-1),(x+3,y-2)],"void",0,2)
    p.dot(x-2,y-2,glow,3)
    p.dot(x+2,y-2,glow,3)
    p.line([(x,y),(x-1,y+2),(x+1,y+2)],"void",0)
    p.line([(x-2,y+4),(x+2,y+4)],mat,0)
    p.dot(x,y+5,mat,3)


def robe(p, mat, back=False, evolved=False, collar_y=43, width=16):
    hem = 87
    p.shape([(48-width+4,collar_y),(48+width-4,collar_y),
             (48+width-1,61),(48+width+5,hem-4),(48+width+1,hem),
             (55,hem-2),(48,hem),(41,hem-2),(48-width-3,hem),
             (48-width-5,hem-4),(48-width+1,63)],mat,"cloth",label="robe")
    p.shape([(45,collar_y+5),(51,collar_y+5),(54,83),(48,87),(42,84)],mat,"flat",-.3)
    # Directional seams follow the drape; long highlight runs are interrupted
    # where the fabric turns, instead of describing every fold with a black line.
    for pts in [[(37,57),(34,70),(31,80)],[(42,59),(39,77),(38,84)],
                [(51,58),(54,76),(56,83)],[(57,58),(60,72),(65,83)]]:
        p.line(pts,mat,1)
        p.line([(x-1,y) for x,y in pts[:-1]],mat,3)
    p.line([(29,83),(36,85),(42,84)],"brass",1)
    p.line([(54,84),(62,85),(68,82)],"brass",1)
    for x, y in [(33,83),(39,84),(58,84),(65,83)]:
        p.line([(x,y-2),(x+1,y-1),(x,y)],"brass",3)
    if back:
        p.line([(48,collar_y+5),(48,70),(46,81)],mat,0)
        p.line([(47,collar_y+7),(47,62)],mat,3)
        p.shape([(40,48),(48,53),(56,48),(54,61),(48,65),(42,61)],mat,"flat")
        p.line([(43,55),(48,58),(52,55)],"brass",2)
    else:
        p.shape([(43,51),(46,51),(44,78),(39,83),(40,72)],"bone","flat",-.8)
        p.shape([(51,51),(54,51),(58,82),(53,80),(51,65)],"bone","flat",-.8)
        for x, yy in [(43,57),(43,63),(42,70),(54,58),(54,65),(55,73)]:
            p.line([(x-1,yy),(x+1,yy),(x,yy+2)],"wood",1)


def acolyte(view, evolved):
    p = Painter()
    mat = "wine_e" if evolved else "wine"
    back, side = view == "Back", view == "Side"
    if side:
        # Profile robe and hood are authored separately: flipping a frontal pose
        # would leave the hood opening, shoulders and hem facing the viewer.
        p.shape([(36,45),(51,43),(57,58),(59,76),(66,85),(60,88),
                 (43,87),(29,88),(31,80),(33,64)],mat,"cloth",label="robe")
        p.shape([(35,46),(40,54),(37,72),(33,84),(29,87)],mat,"flat")
        p.line([(37,58),(36,72),(34,81)],mat,3)
        p.line([(49,61),(51,75),(58,84)],mat,1)
        p.line([(32,85),(42,86),(54,85),(62,85)],"brass",1)
        p.shape([(36,40),(35,33),(40,26),(46,23),(52,28),(56,38),
                 (56,46),(46,49)],mat,"flat",label="hood")
        p.shape([(48,31),(54,34),(57,40),(54,47),(48,44)],"void")
        mask_face(p,52,39,side=True,evolved=evolved)
        p.line([(39,30),(44,27),(48,28)],mat,3)
        p.shape([(37,46),(48,48),(53,54),(41,53)],mat,"flat")
        p.line([(43,50),(49,52)],"brass",3)
        p.shape([(42,52),(49,54),(53,65),(47,67),(43,62)],mat)
        bone_segment(p,(49,64),(61,59),2,evolved)
        hand(p,64,58,1,evolved)
        ember(p,67,52,"amber")
        p.line([(48,55),(50,64)],"brass",1)
        binding(p,43,56,17)
        if evolved:
            p.shape([(33,46),(39,49),(35,57),(29,53)],mat)
            bone_segment(p,(32,52),(26,61),2,True)
            bone_segment(p,(26,61),(38,66),2,True)
            hand(p,40,66,-1,True)
            bone_segment(p,(51,49),(62,41),2,True)
            hand(p,64,39,1,True)
            ember(p,67,32,"amber")
            p.line([(31,78),(27,82),(29,87)],mat,1)
        return p.finish()

    robe(p,mat,back,evolved)
    # Normal hands are tucked close to the chest. The overstayed's added hands
    # occupy the outer outline, so the mutation remains visible in a silhouette.
    if evolved:
        for sign in [-1,1]:
            p.shape([(48+sign*12,46),(48+sign*19,47),(48+sign*20,53),
                     (48+sign*15,57)],mat)
            bone_segment(p,(48+sign*18,50),(48+sign*27,45),2,True)
            bone_segment(p,(48+sign*27,45),(48+sign*27,36),2,True)
            hand(p,48+sign*27,34,sign,True)
            ember(p,48+sign*29,27,"amber")
            p.line([(48+sign*19,51),(48+sign*22,48)],"brass",2)
    p.shape([(35,45),(30,49),(29,61),(34,64),(39,54)],mat)
    p.shape([(60,45),(65,49),(67,58),(62,61),(56,52)],mat)
    p.line([(31,52),(31,58),(34,60)],mat,3)
    p.line([(61,50),(63,55)],mat,3)
    if back:
        bone_segment(p,(31,61),(35,65),2,evolved)
        bone_segment(p,(64,59),(68,55),2,evolved)
        hand(p,69,54,1,evolved)
        ember(p,70,48,"amber")
    else:
        bone_segment(p,(32,62),(43,59),2,evolved)
        hand(p,43,58,1,evolved)
        bone_segment(p,(64,59),(66,54),2,evolved)
        hand(p,67,53,1,evolved)
        ember(p,68,46,"amber")
    # An oblique hood rim, a deep cavity and a small bone face reproduce the
    # Collector's hierarchy: cloth silhouette first, face and magic second.
    p.shape([(37,43),(35,36),(38,29),(44,25),(49,23),(56,29),
             (61,37),(60,45),(52,49),(43,47)],mat,"flat",label="hood")
    if back:
        p.shape([(40,31),(47,27),(55,31),(58,40),(51,46),(44,44)],mat,"cloth")
        p.line([(46,29),(48,33),(50,40),(49,44)],mat,1)
        p.line([(41,33),(40,37)],mat,3)
        p.shape([(35,43),(44,48),(53,48),(61,43),(58,52),(49,55),(38,51)],mat)
        p.line([(40,48),(47,51),(55,48)],"brass",2)
    else:
        p.shape([(40,34),(45,29),(51,29),(56,34),(56,42),(50,47),(44,45),(40,41)],"void")
        mask_face(p,48,38,evolved=evolved)
        p.line([(37,36),(39,31),(44,27)],mat,3)
        p.line([(39,42),(42,46),(48,49),(56,46),(59,42)],mat,1)
        p.shape([(37,45),(46,50),(49,53),(54,49),(61,45),(59,52),(49,56),(39,52)],mat,"flat")
        p.shape([(47,51),(50,52),(51,55),(48,57),(46,54)],"brass")
        p.dot(48,53,"amber",4)
        binding(p,38,54,13)
        binding(p,59,54,12)
    if evolved:
        p.line([(35,71),(36,77),(33,81)],mat,0)
        p.line([(58,72),(62,76),(63,81)],mat,0)
        p.line([(47,79),(45,86)],mat,0)
    return p.finish()


def skull(p, x, y, side=False, back=False, evolved=False):
    mat = "bone_e" if evolved else "bone"
    if side:
        p.shape([(x-6,y-6),(x-1,y-9),(x+5,y-7),(x+8,y-3),
                 (x+8,y+1),(x+5,y+3),(x+5,y+7),(x-1,y+8),(x-5,y+3)],mat,"flat")
        p.line([(x+1,y-2),(x+6,y-2),(x+4,y+1),(x+1,y)],"void",0,2)
        if not evolved:
            p.dot(x+4,y-1,"red",4)
        else:
            p.dot(x+1,y,"red",3)
        p.line([(x+5,y+2),(x+7,y+3)],"void",0)
        p.line([(x,y+5),(x+4,y+5)],mat,0)
        for xx in range(x+1,x+5,2):
            p.dot(xx,y+6,mat,3)
        p.line([(x-5,y-4),(x-3,y-6),(x,y-6)],mat,4)
        return
    p.shape([(x-6,y-6),(x-2,y-9),(x+3,y-8),(x+7,y-4),
             (x+7,y+2),(x+4,y+4),(x+3,y+8),(x-3,y+8),
             (x-4,y+4),(x-7,y+2)],mat,"flat")
    if back:
        p.line([(x-4,y-5),(x,y-7),(x+3,y-5)],mat,4)
        p.line([(x+1,y-6),(x,y-2),(x+2,y+1),(x,y+4)],mat,1)
        p.line([(x-4,y+3),(x-2,y+5),(x+2,y+5)],mat,1)
    else:
        p.shape([(x-5,y-2),(x-1,y-1),(x-1,y+2),(x-4,y+2)],"void")
        p.shape([(x+1,y-1),(x+5,y-2),(x+4,y+2),(x+1,y+2)],"void")
        if evolved:
            # The face recedes into the skull rather than just gaining horns.
            p.shape([(x-3,y+2),(x+3,y+2),(x+2,y+6),(x-2,y+6)],"void")
            p.dot(x-2,y+1,"red",3)
            p.dot(x+2,y+1,"red",3)
        else:
            p.dot(x-3,y,"red",4)
            p.dot(x+3,y,"red",4)
            p.line([(x,y+1),(x-1,y+3),(x+1,y+3)],"void",0)
            p.line([(x-3,y+5),(x+3,y+5)],mat,0)
            for xx in [x-2,x,x+2]:
                p.dot(xx,y+6,mat,3)
        p.line([(x-5,y-5),(x-2,y-7),(x+1,y-7)],mat,4)


def ribcage(p, x, y, width=10, height=15, back=False, split=False, evolved=False):
    mat = "bone_e" if evolved else "bone"
    p.shape([(x-width,y),(x+width,y),(x+width-2,y+height-4),
             (x+4,y+height),(x-4,y+height),(x-width+2,y+height-4)],"void")
    if back:
        p.line([(x,y-1),(x,y+height)],mat,2,2)
        for yy in range(y,y+height,3):
            p.rect((x-1,yy,x+1,yy+1),mat,3)
        for sign in [-1,1]:
            p.shape([(x+sign*2,y+2),(x+sign*(width-1),y),
                     (x+sign*(width-2),y+8),(x+sign*4,y+9)],mat,"flat")
        return
    for i in range(4):
        yy = y+i*3
        reach = width-i
        for sign in [-1,1]:
            mid = 3 if split else 1
            pts = [(x+sign*mid,yy+2),(x+sign*(reach-2),yy+3),
                   (x+sign*reach,yy),(x+sign*(reach-1),yy-1)]
            p.line(pts,mat,2,2)
            p.line([(xx,yyy-1) for xx,yyy in pts[:3]],mat,3)
    if not split:
        p.line([(x,y),(x,y+height-3)],mat,2,2)
    else:
        p.line([(x,y-2),(x-1,y+4),(x+1,y+9),(x,y+height)],"cyan",3)
        for yy in range(y,y+height,4):
            p.dot(x,yy,"cyan",4)


def pursuer(view, evolved):
    p = Painter()
    back, side = view == "Back", view == "Side"
    mat = "bone_e" if evolved else "bone"
    if side:
        # The evolved profile changes the ratio of arm length to trunk. A tall
        # skull and forward chest counterweight make its crouch plausible.
        hx, hy = (59,40) if evolved else (55,41)
        p.shape([(31,54),(39,42),(49,42),(59,50),(55,66),
                 (44,71),(34,66)],"leather","flat")
        p.shape([(32,53),(40,45),(48,45),(51,51),(41,57),(35,64)],mat,"flat")
        p.line([(34,53),(40,49),(47,49)],mat,4)
        p.line([(40,53),(45,58),(51,61)],mat,1)
        ribcage(p,48,54,8,12,evolved=evolved)
        p.shape([(32,65),(43,65),(49,71),(43,76),(30,77),(28,72)],"leather")
        p.line([(30,69),(40,68),(45,72)],"red",1,2)
        bone_segment(p,(35,75),(29,82),3,evolved)
        bone_segment(p,(29,82),(35,86),2,evolved)
        p.shape([(31,85),(37,85),(43,88),(31,88)],"leather","flat")
        bone_segment(p,(43,74),(48,81),3,evolved)
        bone_segment(p,(48,81),(54,86),2,evolved)
        p.shape([(52,84),(56,85),(61,88),(50,88)],"leather","flat")
        if evolved:
            bone_segment(p,(52,53),(67,64),3,True)
            bone_segment(p,(67,64),(76,82),2,True)
            p.shape([(72,81),(78,82),(83,86),(77,86),(74,88),(71,87)],mat)
            for x,y in [(77,85),(80,84),(74,86)]:
                p.line([(x,y),(x+5,y+1),(x+6,y+3)],mat,3)
            bone_segment(p,(39,55),(26,68),2,True)
            bone_segment(p,(26,68),(20,85),2,True)
            p.line([(18,85),(17,88),(23,88)],mat,3,2)
            p.shape([(31,47),(33,38),(36,45),(36,34),(39,44),(42,38),(43,48)],mat)
        else:
            bone_segment(p,(53,53),(64,64),3)
            bone_segment(p,(64,64),(63,76),2)
            hand(p,64,78,1)
            bone_segment(p,(36,58),(31,69),2)
            bone_segment(p,(31,69),(29,78),2)
            hand(p,30,79,-1)
        skull(p,hx,hy,side=True,evolved=evolved)
        p.line([(35,59),(40,62)],"brass",1)
        binding(p,40,70,9)
        p.shape([(34,72),(38,73),(37,80),(33,79)],"wine","flat")
        return p.finish()

    hy = 36 if evolved else 37
    # A hunched neck and unequal shoulder heights keep this from reading as an
    # upright skeleton soldier. Both forms retain the same bindings and scars.
    p.shape([(34,48),(39,43),(47,43),(57,46),(62,53),(58,65),
             (52,71),(41,71),(34,64)],"leather","flat")
    ribcage(p,48,51,11,16,back=back,evolved=evolved)
    p.shape([(37,67),(44,66),(49,69),(55,66),(60,70),(58,75),
             (49,77),(38,76),(35,72)],"leather","flat")
    p.line([(38,71),(45,70),(51,72),(57,70)],"red",1,2)
    p.rect((46,70,50,73),"brass",1)
    p.dot(47,70,"brass",3)
    bone_segment(p,(41,75),(36,81),3,evolved)
    bone_segment(p,(36,81),(35,86),2,evolved)
    bone_segment(p,(55,74),(60,80),3,evolved)
    bone_segment(p,(60,80),(59,86),2,evolved)
    p.shape([(31,85),(38,84),(42,87),(40,88),(29,88)],"leather")
    p.shape([(56,84),(63,85),(66,88),(53,88)],"leather")
    for x in [35,59]:
        p.line([(x-2,80),(x+2,81)],"bone",2)
    if evolved:
        arms = [((35,49),(23,61),(17,82)),((60,50),(73,63),(79,83))]
    else:
        arms = [((35,49),(28,59),(29,73)),((59,50),(66,61),(64,73))]
    for shoulder, elbow, wrist in arms:
        bone_segment(p,shoulder,elbow,3,evolved)
        bone_segment(p,elbow,wrist,2,evolved)
        wx,wy = wrist
        sign = -1 if wx < 48 else 1
        hand(p,wx,wy+2,sign,evolved)
        if evolved:
            for i in range(3):
                p.line([(wx+sign*(i-1),wy+3),(wx+sign*(i+2),86),(wx+sign*(i+4),88)],mat,3)
    if back:
        p.shape([(33,47),(40,44),(43,50),(37,54),(33,53)],mat,"flat")
        p.shape([(54,46),(61,49),(60,55),(53,52)],mat,"flat")
        p.line([(41,50),(45,54),(45,64)],"leather",1,2)
        p.line([(52,51),(50,58),(49,66)],"leather",1,2)
        binding(p,48,61,12)
    else:
        p.line([(39,46),(43,48),(47,48)],mat,3)
        p.line([(52,48),(57,48),(60,51)],mat,3)
        p.line([(36,53),(44,60),(48,63)],"leather",1,2)
        p.line([(37,53),(45,60)],"leather",3)
        p.shape([(38,64),(42,65),(41,75),(37,73)],"wine","flat")
        binding(p,57,64,9)
    if evolved:
        for x,y,h in [(36,44,8),(41,44,10),(54,44,9),(59,47,6)]:
            p.shape([(x-2,y+1),(x-1,y-h),(x+1,y-h+3),(x+2,y+2)],mat,"flat")
        p.line([(48,43),(48,49)],mat,2,3)
    skull(p,48,hy,back=back,evolved=evolved)
    if not back:
        p.line([(43,hy-7),(47,hy-5),(49,hy-3)],mat,1)
    return p.finish()


def choir_collar(p, evolved=False, back=False):
    mat = "bone_e" if evolved else "bone"
    # The chorister has a radiating funerary collar; the acolyte's compact hood
    # never uses this shape. Evolution opens the same collar into a broken fan.
    for sign in [-1,1]:
        p.shape([(48+sign*4,44),(48+sign*9,40),(48+sign*15,39),
                 (48+sign*20,45),(48+sign*16,51),(48+sign*7,50)],"blue_e" if evolved else "blue","flat")
        for i in range(3):
            x = 48+sign*(9+i*4)
            y = 42+i
            p.line([(x,y),(x+sign*2,y+5),(x-sign*1,y+7)],mat,2)
            p.dot(x,y,mat,4)
    p.line([(35,48),(43,52),(48,53),(55,51),(62,47)],"brass",2)
    if evolved:
        p.shape([(28,45),(25,39),(30,40),(32,44),(33,34),(36,40),
                 (40,44),(39,49)],mat,"flat")
        p.shape([(56,43),(60,37),(61,33),(64,41),(68,39),(72,41),
                 (67,49),(61,49)],mat,"flat")


def scatter(view, evolved):
    p = Painter()
    back, side = view == "Back", view == "Side"
    mat = "blue_e" if evolved else "blue"
    bone = "bone_e" if evolved else "bone"
    if side:
        p.shape([(34,44),(49,43),(55,55),(57,67),(63,85),(58,88),
                 (43,87),(28,88),(30,77)],mat,"cloth",label="robe")
        p.line([(36,57),(34,69),(32,83)],mat,3)
        p.line([(49,62),(52,76),(57,84)],mat,1)
        p.shape([(33,43),(37,39),(48,41),(56,46),(51,52),(38,50)],mat,"flat")
        for x,y in [(37,42),(43,43),(49,45)]:
            p.line([(x,y),(x+3,y+5)],bone,3)
        p.shape([(39,33),(40,25),(45,22),(51,24),(56,30),(56,41),
                 (52,46),(42,44)],mat,"flat",label="hood")
        p.shape([(49,29),(55,31),(58,36),(56,44),(51,44)],"void")
        mask_face(p,53,36,side=True,glow="cyan",evolved=evolved)
        p.line([(43,27),(47,25)],mat,3)
        if evolved:
            p.shape([(50,42),(57,44),(59,61),(54,69),(50,60)],"void")
            p.line([(54,45),(55,51),(54,59),(55,65)],"cyan",3,2)
            for y in range(50,66,4):
                p.line([(51,y),(58,y-2),(62,y),(60,y+3)],bone,2,2)
                p.dot(59,y-1,bone,4)
            p.shape([(36,39),(33,31),(37,32),(41,41)],bone)
            p.shape([(51,42),(55,34),(57,36),(57,44)],bone)
            bone_segment(p,(43,55),(57,69),2,True)
            bone_segment(p,(57,69),(67,63),2,True)
            hand(p,68,61,1,True)
            ember(p,70,55,"cyan")
        else:
            p.shape([(42,51),(49,53),(53,63),(47,67),(43,61)],mat)
            bone_segment(p,(49,65),(61,59),2)
            hand(p,62,59,1)
            ember(p,65,53,"cyan")
            p.line([(52,44),(53,49)],"cyan",2)
        binding(p,39,54,18)
        p.line([(31,85),(38,86),(48,86),(60,85)],"brass",1)
        p.line([(43,71),(46,82)],mat,0)
        return p.finish()

    robe(p,mat,back,evolved,width=18)
    choir_collar(p,evolved,back)
    p.shape([(32,50),(27,55),(26,67),(32,70),(39,59),(38,52)],mat)
    p.shape([(63,50),(69,55),(70,66),(64,70),(56,59),(57,52)],mat)
    p.line([(29,58),(29,66),(32,67)],mat,3)
    p.line([(64,56),(66,63)],mat,3)
    if evolved:
        bone_segment(p,(29,68),(25,76),2,True)
        bone_segment(p,(67,68),(72,75),2,True)
        hand(p,26,77,-1,True)
        hand(p,72,76,1,True)
        if not back:
            ribcage(p,48,53,13,20,split=True,evolved=True)
            p.shape([(42,46),(54,46),(51,55),(45,55)],"void")
            p.line([(47,46),(46,50),(48,54)],"cyan",3,2)
            p.line([(49,46),(50,50),(49,54)],"cyan",2)
            p.shape([(43,76),(48,74),(54,76),(53,79),(47,81),(43,78)],"brass","flat")
            for x in [44,48,52]:
                p.line([(x,79),(x,83)],"brass",2)
            p.dot(48,77,"cyan",4)
        else:
            ribcage(p,48,53,12,19,back=True,evolved=True)
            p.line([(33,60),(39,67),(41,75)],mat,0,2)
            p.line([(63,59),(58,67),(56,76)],mat,0,2)
            p.shape([(43,74),(53,74),(54,82),(48,85),(42,81)],mat,"flat")
    else:
        bone_segment(p,(30,68),(43,64),2)
        bone_segment(p,(66,68),(53,64),2)
        hand(p,43,64,1)
        hand(p,53,64,-1)
        if not back:
            p.shape([(42,50),(54,50),(53,62),(48,66),(43,61)],"bone","flat",-.5)
            p.line([(46,53),(46,59),(48,61),(50,59),(50,53)],"cyan",2)
            p.line([(48,51),(48,59)],"cyan",3)
            p.ellipse((45,66,51,72),"brass")
            p.rect((47,68,49,70),"cyan",3)
            ember(p,48,60,"cyan")
        else:
            p.line([(42,51),(48,56),(54,51)],"brass",2)
            p.line([(48,55),(48,68)],"brass",1)
    p.shape([(39,40),(37,33),(39,27),(45,23),(51,23),(58,28),
             (60,35),(57,44),(49,48),(41,44)],mat,"flat",label="hood")
    if back:
        p.line([(44,27),(41,31),(41,36)],mat,3)
        p.line([(48,26),(50,33),(49,42)],mat,1)
        if evolved:
            p.line([(47,34),(45,37),(48,40),(47,44)],"cyan",1)
    else:
        p.shape([(42,29),(53,28),(56,34),(54,43),(50,47),(44,44),(41,36)],"void")
        mask_face(p,48,35,evolved=evolved,glow="cyan")
        # The chorus mask is deliberately longer and has a vertical mouth seam,
        # distinguishing it from the acolyte's short skull-like faceplate.
        p.shape([(44,39),(52,39),(51,46),(48,49),(45,46)],bone,"flat")
        p.line([(48,40),(48,47)],"void",0)
        p.dot(48,44,"cyan",3)
        p.line([(40,30),(44,26),(49,26)],mat,3)
    return p.finish()


def coffin(p, x, y, width, height, side=False, evolved=False):
    half = width//2
    # Real bevelled coffin proportions, visible slats and iron bands make the
    # siege familiar recognisable from behind without relying on a glow colour.
    p.shape([(x-half+3,y),(x+half-3,y),(x+half,y+7),
             (x+half-2,y+height-4),(x+half-5,y+height),
             (x-half+5,y+height),(x-half+2,y+height-4),(x-half,y+7)],"wood","flat")
    p.line([(x-half+3,y+3),(x-half+2,y+10),(x-half+4,y+height-5)],"wood",3)
    p.line([(x+half-3,y+5),(x+half-4,y+height-4)],"wood",0,2)
    for xx in range(x-half+6,x+half-2,5):
        p.line([(xx,y+3),(xx-1,y+height-3)],"wood",1)
        p.line([(xx+1,y+12),(xx+2,y+16),(xx+1,y+22)],"wood",3)
    for yy in [y+9,y+height-11]:
        p.line([(x-half+1,yy),(x+half-1,yy)],"iron",0,4)
        p.line([(x-half+1,yy-1),(x+half-1,yy-1)],"iron",2)
        for xx in [x-half+3,x+half-3]:
            p.dot(xx,yy,"brass",3)
    if not side:
        p.line([(x,y+5),(x,y+height-4)],"iron",1,2)
        p.shape([(x-3,y+15),(x+3,y+15),(x+4,y+20),(x,y+24),(x-4,y+20)],"brass","flat")
        p.rect((x-1,y+18,x+1,y+20),"void",0)
        p.dot(x,y+17,"brass",4)
    if evolved:
        p.line([(x-2,y+2),(x,y+10),(x-2,y+18),(x+2,y+25),
                (x,y+height-3)],"void",0,3)
        p.line([(x-1,y+4),(x+1,y+11),(x-1,y+18),(x+2,y+24)],"amber",2)
        for yy in [y+12,y+27]:
            p.line([(x-2,yy),(x+2,yy-1)],"amber",4)


def rock_plate(p, points, evolved=False):
    p.shape(points,"stone","stone")
    # A broken facet and restrained edge bevel describe rough stone; patterned
    # checkerboard noise would confuse it with chainmail or woven cloth.
    if len(points)>3:
        p.line(points[:3],"stone",3)
        x = sum(q[0] for q in points)//len(points)
        y = sum(q[1] for q in points)//len(points)
        p.line([(x-2,y-3),(x+1,y),(x-1,y+3),(x+2,y+5)],"stone",0)
        p.line([(x-3,y-3),(x,y)],"stone",2)


def siege(view, evolved):
    p = Painter()
    back, side = view == "Back", view == "Side"
    bone = "bone_e" if evolved else "bone"
    if side:
        coffin(p,38,14 if evolved else 20,18,51,True,evolved)
        p.shape([(30,20),(31,57),(38,69),(43,65),(39,22)],"iron","flat")
        p.line([(32,25),(33,51),(38,61)],"iron",3)
        p.shape([(40,42),(52,38),(63,46),(67,61),(57,73),(43,73),
                 (35,59)],"stone","stone")
        rock_plate(p,[(42,47),(51,44),(57,50),(54,62),(45,64),(40,59)])
        p.shape([(54,47),(61,48),(64,62),(58,67),(54,60)],"void")
        for i in range(4):
            yy = 51+i*4
            p.line([(55,yy),(62,yy+1),(62,yy+3)],bone,2,2)
            p.dot(56,yy-1,bone,4)
        p.line([(60,53),(61,58),(59,63)],"amber",3)
        p.dot(61,56,"amber",4)
        rock_plate(p,[(39,66),(48,66),(51,75),(46,83),(34,83),(33,75)])
        rock_plate(p,[(48,72),(59,69),(63,77),(62,85),(48,86),(45,81)])
        p.shape([(32,82),(44,81),(48,85),(47,88),(29,88)],"iron","flat")
        p.shape([(48,84),(62,83),(67,86),(66,88),(47,88)],"iron","flat")
        rock_plate(p,[(46,48),(55,47),(62,54),(63,64),(57,68),(49,62)])
        bone_segment(p,(59,64),(66,74),4,evolved)
        p.shape([(62,74),(68,73),(73,77),(71,83),(64,84),(60,80)],"stone","stone")
        for x in [64,67,70]:
            p.line([(x,77),(x+1,81)],bone,2)
        p.shape([(43,41),(45,32),(52,27),(60,29),(66,37),(65,44),
                 (56,48),(48,47)],"stone","stone")
        skull(p,58,38,side=True,evolved=evolved)
        p.dot(62,37,"amber",4)
        if evolved:
            for pts in [[(29,21),(26,27),(27,51),(34,60)],
                        [(37,17),(39,22),(39,45),(43,52)],
                        [(43,35),(45,24),(50,31),(50,43)]]:
                p.line(pts,bone,1,4)
                p.line([(x-1,y-1) for x,y in pts],bone,3,2)
            p.shape([(35,54),(29,52),(30,46),(27,42),(29,38),(33,40),
                     (35,46),(40,45),(42,49)],bone)
            p.line([(30,40),(31,44),(33,47)],bone,4)
            p.shape([(44,68),(42,60),(40,55),(45,57),(48,64),(51,61),
                     (52,69)],bone)
        else:
            p.line([(34,36),(42,47),(44,65)],"leather",1,3)
            p.line([(33,36),(41,47)],"leather",3)
        return p.finish()

    coffin(p,48,12 if evolved else 19,28 if evolved else 24,53,False,evolved)
    # Coffin is behind the torso in front view and exposed in back view. Keeping
    # that layer order explicit avoids a false front-facing chest in the rear.
    rock_plate(p,[(31,59),(41,57),(43,73),(39,84),(27,83),(25,73)])
    rock_plate(p,[(53,58),(65,58),(70,73),(68,84),(56,85),(52,74)])
    p.shape([(26,82),(39,81),(43,85),(42,88),(23,88)],"iron","flat")
    p.shape([(55,82),(68,83),(74,87),(72,88),(53,88)],"iron","flat")
    for x,y in [(31,73),(62,73)]:
        p.line([(x-4,y),(x+4,y-1)],bone,2,3)
        p.line([(x-3,y-1),(x+3,y-2)],bone,4)
    p.shape([(29,42),(38,38),(56,38),(67,43),(66,60),(58,72),
             (39,72),(29,61)],"stone","stone")
    p.shape([(29,43),(39,40),(45,46),(40,56),(30,55)],bone,"flat")
    p.shape([(54,41),(65,43),(69,51),(65,58),(56,55),(51,47)],bone,"flat")
    rock_plate(p,[(23,45),(30,43),(34,51),(29,63),(19,62),(17,56)])
    rock_plate(p,[(66,44),(73,47),(78,57),(75,65),(66,62),(62,53)])
    bone_segment(p,(23,61),(23,74),4,evolved)
    bone_segment(p,(71,63),(72,75),4,evolved)
    rock_plate(p,[(18,72),(25,71),(31,77),(28,83),(17,83),(14,78)])
    rock_plate(p,[(67,73),(75,73),(80,79),(77,85),(66,84),(63,80)])
    for x,y in [(18,78),(22,77),(26,78),(68,80),(72,79),(76,80)]:
        p.line([(x,y),(x,y+3)],bone,2)
    if back:
        coffin(p,48,13 if evolved else 20,26 if evolved else 24,53,False,evolved)
        p.line([(31,47),(38,55),(42,69)],"leather",1,3)
        p.line([(65,46),(58,56),(54,69)],"leather",1,3)
        p.line([(32,46),(39,55)],"leather",3)
        if evolved:
            for sign in [-1,1]:
                for yy in [28,40,51,62]:
                    p.line([(48+sign*13,yy-3),(48+sign*18,yy+1),
                            (48+sign*11,yy+5),(48+sign*3,yy+7)],bone,1,4)
                    p.line([(48+sign*13,yy-4),(48+sign*17,yy),
                            (48+sign*11,yy+4),(48+sign*3,yy+6)],bone,3,2)
            p.shape([(40,46),(35,42),(33,37),(34,33),(37,34),(38,40),
                     (43,40),(45,43)],bone)
            p.line([(35,34),(36,38)],bone,4)
            p.shape([(56,56),(63,52),(66,54),(65,60),(60,62),(56,60)],bone)
        else:
            p.line([(36,27),(39,25),(43,26)],"iron",2)
            p.line([(54,62),(58,61),(60,58)],"iron",2)
    else:
        ribcage(p,48,49,13,21,split=False,evolved=evolved)
        # The familiar's core remains behind bones. Its orange cue is a furnace,
        # unlike the acolyte's free flame or scatter's cyan vocal fissure.
        for yy in [53,58,63]:
            p.line([(44,yy),(47,yy+1),(51,yy),(53,yy+1)],"amber",2)
            p.dot(47,yy,"amber",4)
        p.line([(48,50),(48,67)],bone,2,2)
        p.shape([(39,68),(47,70),(56,68),(57,73),(48,76),(38,73)],"iron","flat")
        p.rect((45,70,50,73),"brass",2)
        p.dot(46,70,"brass",4)
        p.shape([(37,34),(42,27),(50,25),(57,30),(61,39),
                 (57,47),(48,50),(40,45)],"stone","stone")
        skull(p,48,38,evolved=evolved)
        p.dot(45,38,"amber",4)
        p.dot(51,38,"amber",4)
        if evolved:
            for sign in [-1,1]:
                p.shape([(48+sign*13,34),(48+sign*21,31),(48+sign*25,36),
                         (48+sign*19,40),(48+sign*13,41)],bone,"flat")
                p.line([(48+sign*19,37),(48+sign*22,32),
                        (48+sign*18,27),(48+sign*16,20)],bone,2,3)
                p.line([(48+sign*18,36),(48+sign*21,32),
                        (48+sign*17,27)],bone,4)
                p.line([(48+sign*14,55),(48+sign*19,62),
                        (48+sign*13,68)],bone,2,3)
                p.line([(48+sign*15,54),(48+sign*20,61)],bone,3)
            p.line([(48,30),(46,36),(48,42)],"void",0,2)
    return p.finish()


FAMILIES = {
    "Acolyte": (acolyte,"Extra casting hands; the original pair remains near the body."),
    "Pursuer": (pursuer,"Lengthened reaching limbs, exposed spine and a face recessed into the skull."),
    "Scatter": (scatter,"Broken funerary collar and an open throat/rib seam carrying the chorus."),
    "Siege": (siege,"Coffin cracked and enclosed by growing ribs, bone braces and grasping bones."),
}
VIEWS = ["Front", "Back", "Side"]


def font(size, bold=False):
    # Font choice is for the review board only, never baked into game sprites.
    folder = Path("C:/Users/Rachit/.agents/skills/canvas-design/canvas-fonts")
    path = folder / ("BricolageGrotesque-Bold.ttf" if bold else "BricolageGrotesque-Regular.ttf")
    return ImageFont.truetype(str(path),size)


def review_board(frames):
    bg, panel, ink, muted = "#151722", "#1c1f2b", "#eee3d0", "#a3a7b6"
    board = Image.new("RGB",(1900,1830),bg)
    d = ImageDraw.Draw(board)
    d.text((52,32),"BORROWED HEX",font=font(38,True),fill=ink)
    d.text((54,83),"Enemy drafts  /  native pixel art  /  front, back, right side",font=font(19),fill=muted)
    # One reference pose is enough to show real native pixel density. It is not
    # used as a texture, traced or incorporated into the new character artwork.
    collector_path = PROJECT / "Assets/Game/Resources/WorldArt/Necromancer.png"
    collector = Image.open(collector_path).convert("RGBA").crop((0,0,160,128))
    collector = collector.crop(collector.getbbox()).resize((126,156),Image.Resampling.NEAREST)
    board.paste(collector,(1660,22),collector)
    d.text((1621,186),"Existing Collector · 3×",font=font(15),fill=muted)
    # Each family gets a single paired row, making mutations easy to compare
    # without confusing the view order or comparing differently scaled images.
    row_top, row_height = 238, 373
    for row,(name,(_,description)) in enumerate(FAMILIES.items()):
        y = row_top+row*row_height
        d.rounded_rectangle((40,y,1860,y+355),radius=13,fill=panel)
        d.text((62,y+17),name.upper(),font=font(24,True),fill=ink)
        d.text((62,y+50),description,font=font(15),fill=muted)
        for group,evolved in enumerate([False,True]):
            x0 = 210+group*840
            state = "EVOLVED / OVERSTAYED" if evolved else "STANDARD"
            color = "#ce986d" if evolved else muted
            d.text((x0+46,y+17),state,font=font(15,True),fill=color)
            for col,view in enumerate(VIEWS):
                im = frames[(name,evolved,view)]
                # Crop the same review window for every drawing. Per-character
                # zooming would make the player misjudge real in-game scale.
                # Integer enlargement preserves equally sized display pixels;
                # labels sit below the art instead of crossing its ground line.
                window = im.crop((37,36,123,119)).resize((258,249),Image.Resampling.NEAREST)
                x = x0+col*264
                board.paste(window,(x,y+79),window)
                d.text((x+102,y+333),view.upper(),font=font(13),fill=muted)
        d.line([(1018,y+72),(1018,y+326)],fill="#333646",width=1)
    d.text((54,1760),"Transparent game frames: 160 × 128 px  ·  all sprites shown at 3×  ·  ground anchor: (80, 115)  ·  flip side for left",font=font(17),fill=muted)
    d.text((54,1790),"Draft poses for visual review. Animation cycles have not been authored yet.",font=font(15),fill=muted)
    board.save(ROOT / "EnemyDrafts_Review.png")


def build():
    frames = {}
    records = []
    for name,(draw,description) in FAMILIES.items():
        for evolved in [False,True]:
            character = name + ("_Evolved" if evolved else "")
            folder = ROOT / character
            folder.mkdir(parents=True,exist_ok=True)
            sheet = Image.new("RGBA",(SIZE[0]*3,SIZE[1]))
            pose_records = []
            for col,view in enumerate(VIEWS):
                im = draw(view,evolved)
                frames[(name,evolved,view)] = im
                im.save(folder / f"{character}_{view}.png")
                sheet.alpha_composite(im,(col*SIZE[0],0))
                bbox = im.getbbox()
                assert bbox and bbox[0]>0 and bbox[1]>0 and bbox[2]<SIZE[0] and bbox[3]<SIZE[1], (character,view,bbox)
                alpha = set(im.getchannel("A").get_flattened_data())
                assert alpha == {0,255}, (character,view,"non-binary alpha")
                colors = {px[:3] for px in im.get_flattened_data() if px[3]}
                pose_records.append({"view":view,"file":f"{character}/{character}_{view}.png",
                                     "sheetRectTopLeft":[col*SIZE[0],0,*SIZE],"opaqueBounds":bbox,
                                     "paletteSize":len(colors),"sha256":hashlib.sha256(im.tobytes()).hexdigest()})
            sheet.save(folder / f"{character}_Turnaround.png")
            records.append({"character":character,"category":name,"evolved":evolved,
                            "evolutionDesign":description,"turnaround":f"{character}/{character}_Turnaround.png",
                            "poses":pose_records})
    review_board(frames)
    # A native-resolution atlas provides one file to inspect or batch-slice.
    atlas = Image.new("RGBA",(480,1024))
    for row,rec in enumerate(records):
        sheet = Image.open(ROOT / rec["turnaround"])
        atlas.alpha_composite(sheet,(0,row*128))
    atlas.save(ROOT / "EnemyDrafts_Atlas.png")
    manifest = {"status":"static visual drafts; no animation cycles", "drawingMethod":"authored deterministic Python/Pillow pixel drawing",
                "frameSize":SIZE,"pixelsPerUnit":32,"groundAnchorTopLeft":[80,GROUND],
                "unityPivot":[.5,(SIZE[1]-GROUND)/SIZE[1]],"viewOrder":VIEWS,"flipSideForLeft":True,
                "filterMode":"Point","mipmaps":False,"compression":"None","characters":records}
    (ROOT / "manifest.json").write_text(json.dumps(manifest,indent=2)+"\n",encoding="utf-8")
    print(json.dumps({"characters":len(records),"directionalPoses":len(frames),
                      "output":str(ROOT),"paletteRange":[min(p["paletteSize"] for r in records for p in r["poses"]),
                                                            max(p["paletteSize"] for r in records for p in r["poses"])]},indent=2))


if __name__ == "__main__":
    build()
