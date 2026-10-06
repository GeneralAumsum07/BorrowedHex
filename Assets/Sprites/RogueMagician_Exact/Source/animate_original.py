"""Animate the detailed original instead of drawing a replacement character.

All poses start from the original four material masks. Resting poses therefore
have the exact supplied silhouette; cloth and head layers move for animation.
The native 64x128 working canvas exports losslessly into 320x640 original cells.
"""

from __future__ import annotations

import hashlib
import json
import math
import sys
from pathlib import Path

from PIL import Image, ImageChops, ImageDraw

sys.dont_write_bytecode = True
import detail_original as art


ROOT = art.ROOT
SIZE = art.NATIVE
STATES = {
    "Idle": (8, 8, True),
    "Moving": (8, 12, True),
    "Dash": (8, 24, False),
    "Hurt": (6, 15, False),
    "Death": (12, 10, False),
}
ROWS = list(STATES)


def masked(image, mask):
    result = image.copy()
    result.putalpha(ImageChops.multiply(result.getchannel("A"), mask))
    return result


def shift(image, x=0, y=0):
    bounds = image.getbbox()
    if bounds:
        x = max(1 - bounds[0], min(63 - bounds[2], x))
        y = max(1 - bounds[1], min(127 - bounds[3], y))
    canvas = Image.new("RGBA", SIZE)
    canvas.alpha_composite(image, (round(x), round(y)))
    return canvas


def affine(image, sx=1, sy=1, lean=0, dx=0, dy=0, pivot=(32, 113)):
    # Work in character coordinates, with a stable ground pivot. Inverting the
    # transform lets nearest-neighbour sampling retain sharp five-pixel blocks.
    px, py = pivot
    bounds = image.getbbox()
    if bounds:
        # Keep the entire material layer inside the original cell. Character
        # margins are much narrower than on the rejected replacement sheet;
        # limiting travel is preferable to trimming a brim or a hem off-screen.
        width = (bounds[2] - bounds[0]) * sx
        height = (bounds[3] - bounds[1]) * sy
        if width + abs(lean) * (bounds[3] - bounds[1]) > 62:
            lean = math.copysign(max(0, 62 - width) / (bounds[3] - bounds[1]), lean)
        xs = [px + sx * (x - px) + lean * (y - py)
              for x in (bounds[0], bounds[2]) for y in (bounds[1], bounds[3])]
        ys = [py + sy * (y - py) for y in (bounds[1], bounds[3])]
        dx = max(1 - min(xs), min(63 - max(xs), dx))
        dy = max(1 - min(ys), min(127 - max(ys), dy))
    invx, invy = 1 / sx, 1 / sy
    return image.transform(SIZE, Image.Transform.AFFINE,
        (invx, -lean * invx * invy,
         px - invx * (px + dx) + lean * invx * invy * (py + dy),
         0, invy, py - invy * (py + dy)),
        Image.Resampling.NEAREST)


def fabric(image, phase, strength=1, flutter=0):
    # The shoulders stay joined to the head. Only the lower cloak receives
    # lateral fold motion and an uneven hem lift; no limbs are ever exposed.
    out = Image.new("RGBA", SIZE)
    ip, op = image.load(), out.load()
    bounds = image.getbbox()
    hair = bounds and bounds[3] < 61
    top, length = (35, 24) if hair else (53, 60)
    for y in range(128):
        h = max(0, min(1, (y - top) / length))
        wave = strength * h * h * (math.sin(phase - h * 2.1) + .3 * math.sin(phase * 2 + h * 4))
        occupied = [x for x in range(64) if ip[x, y][3]]
        if occupied:
            wave = max(1 - min(occupied), min(62 - max(occupied), wave))
        for x in range(64):
            hem = flutter * h ** 3 * (.5 + .5 * math.sin(x * .32 + phase))
            xx, yy = round(x - wave), round(y + hem)
            if 0 <= xx < 64 and 0 <= yy < 128:
                op[x, y] = ip[xx, yy]
    return out


def tint(image, colour, amount):
    # Hit feedback recolours opaque pixels without adding a halo or turning
    # transparent pixels into a rectangle around the character.
    result = Image.blend(image, Image.new("RGBA", SIZE, colour + (255,)), amount)
    result.putalpha(image.getchannel("A"))
    return result


def smooth(t):
    t = max(0, min(1, t))
    return t * t * (3 - 2 * t)


class OriginalRig:
    def __init__(self, view, original):
        self.view = view
        self.source = original.resize(SIZE, Image.Resampling.NEAREST)
        self.detailed = art.detail(self.source, view)
        self.masks = art.masks_for(self.source, view)
        self.cloak = masked(self.detailed, self.masks["cloak"])
        self.hair = masked(self.detailed, self.masks["hair"])
        head_mask = ImageChops.lighter(self.masks["skin"], self.masks["eye"])
        self.face = masked(self.detailed, head_mask)
        hat_mask = ImageChops.lighter(self.masks["hat"], self.masks["brass"])
        self.hat = masked(self.detailed, hat_mask)
        # The authored side views remain independent and unchanged at rest.
        # Runtime left/right flipping is supported, but both originals are kept.
        self.direction = -1 if view == "Left" else 1

    def head(self, phase=0, hair_sway=0, hat_sway=0, dead=False, blink=False):
        upper = Image.new("RGBA", SIZE)
        # Hair is behind the face and brim, retaining the original overlap.
        upper.alpha_composite(fabric(self.hair, phase, hair_sway))
        face = self.face.copy()
        if dead or blink:
            if self.view == "Front":
                art.front_face(face,self.masks,self.source,closed=True)
            else:
                # Closing the existing eye marks changes only their interior pixels.
                pixels, mask = face.load(), self.masks["eye"].load()
                for y in range(128):
                    for x in range(64):
                        if mask[x, y]:
                            pixels[x, y] = (68, 39, 35, 255) if dead else (96, 54, 46, 255)
        upper.alpha_composite(face)
        hat = self.hat
        if hat_sway:
            # A small shear around the brim bends the cloth tip, while preserving
            # the original brim width, hat height, and original painted detail.
            hat = affine(hat, lean=hat_sway, pivot=(32, 34))
        upper.alpha_composite(hat)
        return upper

    def pose(self, state, frame, count):
        phase = math.tau * frame / count
        t = frame / (count - 1)
        cloak, head = self.cloak, self.head()
        if state == "Idle":
            if frame == 0:
                # This key is exactly the approved detailed original. It is an
                # explicit anchor, rather than an approximately matching redraw.
                return self.detailed.copy()
            breath = math.sin(phase)
            cloak = fabric(cloak, phase, .8, .3)
            cloak = affine(cloak, sy=1 + .012 * breath)
            head = shift(self.head(phase, .5, .035 * breath, blink=frame == 6),
                         0, -.7 * breath)
        elif state == "Moving":
            # Two concealed steps per cycle. The changing hem/folds carry the
            # walk, with a smaller delayed hat/hair response instead of legs.
            bob = 1.8 * abs(math.sin(phase))
            sway = math.sin(phase) * 1.3
            cloak = fabric(cloak, phase, 2.4, 2.2)
            cloak = affine(cloak, lean=.027 * math.sin(phase), dx=sway, sy=1 + bob / 61)
            head = shift(self.head(phase - .6, 1.2, .065 * math.sin(phase - .5)), sway, -bob)
        elif state == "Dash":
            # Anticipation -> launch -> travel -> recovery. Lean and trailing
            # cloth indicate the direction, without enlarging the canvas.
            drive = [0, -.3, .7, 1, 1, .8, .35, 0][frame]
            dx = [0, -1, 0, 3, 4, 3, 1, 0][frame] * self.direction
            lean = -.17 * drive * self.direction if self.view in ("Left", "Right") else .05 * drive
            cloak = fabric(cloak, phase + .7, 5 * drive, 2 * max(0, drive))
            cloak = affine(cloak, lean=lean, dx=dx, sy=1 - .035 * max(0, drive))
            head = affine(self.head(phase, 2 * max(0, drive), .14 * drive * self.direction),
                          lean=lean, dx=dx, dy=2 * max(0, drive))
        elif state == "Hurt":
            recoil = [0, 1, .8, -.3, .1, 0][frame]
            cloak = fabric(cloak, phase, 1.8 * recoil)
            cloak = affine(cloak, lean=.075 * recoil * self.direction, dy=1.5 * abs(recoil))
            head = affine(self.head(phase, recoil, -.09 * recoil, blink=frame in (1, 2)),
                          lean=.09 * recoil * self.direction, dy=1.5 * abs(recoil))
            hit = [0, .48, .16, 0, 0, 0][frame]
            cloak, head = tint(cloak, (217, 170, 157), hit), tint(head, (217, 170, 157), hit)
        else:
            # A staged loss of balance and cloth collapse. This deforms the
            # supplied costume into a resting pile; it never draws replacement
            # anatomy, stretches the canvas, or reveals a hand or foot.
            fall = smooth((t - .09) / .78)
            slump = smooth((t - .2) / .63)
            direction = self.direction
            cloak = fabric(cloak, phase * .5, 2 * fall, 0)
            cloak = affine(cloak, sx=1 + .03 * slump, sy=1 - .69 * slump,
                           lean=.12 * fall * direction, dx=1.5 * fall * direction)
            head = self.head(phase, .8 * fall, .17 * fall * direction, dead=t > .1)
            head = affine(head, lean=.24 * fall * direction, dx=2.5 * fall * direction,
                          dy=43 * slump, sy=1 - .13 * slump, pivot=(32, 53))
            # At the end, the hat loses its upright posture. Rotate around its
            # own brim before laying it into the cloak; avoid rotating empty
            # canvas space, which would displace the character's ground anchor.
            if t > .7:
                hat = affine(self.hat, lean=.17 * fall * direction, pivot=(32, 34))
                hat = hat.rotate(-18 * direction * smooth((t - .7) / .3),
                                      Image.Resampling.NEAREST, center=(32, 34))
                # Keep the same collapse transform across this transition;
                # only the additional hat tilt changes, avoiding a pose pop.
                upper = Image.new("RGBA", SIZE)
                upper.alpha_composite(self.hair)
                closed_face = masked(self.head(dead=True),
                                     ImageChops.lighter(self.masks["skin"], self.masks["eye"]))
                upper.alpha_composite(closed_face)
                upper.alpha_composite(hat)
                head = affine(upper, sy=1 - .13 * slump, dy=43 * slump,
                              lean=.24 * fall * direction, dx=2.5 * fall * direction, pivot=(32, 53))
            cloak = tint(cloak, (10, 22, 40), .18 * slump)
        result = Image.new("RGBA", SIZE)
        result.alpha_composite(cloak)
        result.alpha_composite(head)
        return result


def gif_export(frames, path, duration):
    # GIFs are review previews, not engine sprites. A dark background makes the
    # transparent sprite edge legible and avoids GIF's binary-alpha artifacts.
    preview = []
    for frame in frames:
        canvas = Image.new("RGB", (640, 352), (19, 23, 31))
        canvas.paste(frame.resize((640, 320), Image.Resampling.NEAREST), (0, 32),
                     frame.resize((640, 320), Image.Resampling.NEAREST).getchannel("A"))
        d = ImageDraw.Draw(canvas)
        for col, view in enumerate(art.VIEWS):
            d.text((col * 160 + 8, 9), view.upper(), fill=(219, 213, 198))
        preview.append(canvas.quantize(128, dither=Image.Dither.NONE))
    preview[0].save(path, save_all=True, append_images=preview[1:], duration=duration,
                    loop=0, disposal=2, optimize=False)


def build():
    art.build()
    original = Image.open(art.ORIGINAL).convert("RGBA")
    manifest = json.loads((ROOT / "manifest.json").read_text())
    (ROOT / "Previews").mkdir(exist_ok=True)
    sequences, all_frames = [], {}
    for col, view in enumerate(art.VIEWS):
        rig = OriginalRig(view, original.crop((col * 320, 0, (col + 1) * 320, 640)))
        folder = ROOT / view
        folder.mkdir(exist_ok=True)
        master = Image.new("RGBA", (12 * 320, 5 * 640))
        for row, (state, (count, fps, loop)) in enumerate(STATES.items()):
            frames = [rig.pose(state, frame, count).resize(art.CELL, Image.Resampling.NEAREST)
                      for frame in range(count)]
            strip = Image.new("RGBA", (count * 320, 640))
            for frame, image in enumerate(frames):
                strip.alpha_composite(image, (frame * 320, 0))
                master.alpha_composite(image, (frame * 320, row * 640))
                # Transparent margins are checked, not assumed. Any clipped
                # moving pixel is a bug, despite the correct file dimensions.
                bounds = image.getbbox()
                assert bounds and min(bounds[:2]) > 0 and bounds[2] < 320 and bounds[3] < 640, (view, state, frame, bounds)
            file = f"{view}/RogueMagician_{view}_{state}.png"
            strip.save(ROOT / file)
            all_frames[view, state] = frames
            sequences.append({"view":view, "state":state, "frames":count,
                "fps":fps, "loop":loop, "masterRow":row, "stripFile":file,
                "masterFile":f"{view}/RogueMagician_{view}_Complete.png",
                "clipFile":f"Clips/RogueMagician_{view}_{state}.anim"})
        master.save(folder / f"RogueMagician_{view}_Complete.png")
        rest = all_frames[view, "Idle"][0]
        assert rest.tobytes() == Image.open(ROOT / f"RogueMagician_{view}_Detailed.png").convert("RGBA").tobytes()
    for state, (count, fps, _) in STATES.items():
        review = []
        for frame in range(count):
            full = Image.new("RGBA", (1280, 640))
            for col, view in enumerate(art.VIEWS):
                full.alpha_composite(all_frames[view, state][frame], (320 * col, 0))
            review.append(full)
        # A pause after one-shot states makes the settled death and recovery
        # pose reviewable. It does not add extra keys to the actual game clips.
        duration = [round(1000 / fps)] * count
        if state in ("Dash", "Hurt", "Death"): duration[-1] += 600
        gif_export(review, ROOT / "Previews" / f"RogueMagician_{state}.gif", duration)
    manifest.update({"status":"detailed original with animated poses",
        "masterSize":[3840,3200], "masterColumns":12,
        "masterRowOrder":ROWS, "pivot":[.5,70 / 640],
        "groundPixel":[160,570], "sequences":sequences,
        "restPoseMatchesDetailedOriginal":True,
        "animationFramesMayChangeOutline":True,
        "totalAnimationFrames":sum(s["frames"] for s in sequences)})
    (ROOT / "manifest.json").write_text(json.dumps(manifest, indent=2) + "\n")
    for file, checksum in manifest["originalFileHashes"].items():
        assert hashlib.sha256((ROOT.parent / file).read_bytes()).hexdigest() == checksum
    print(json.dumps({"sequences":len(sequences),"frames":manifest["totalAnimationFrames"],
        "originalCellSize":list(art.CELL),"referenceMasksExact":True,"unclipped":True}, indent=2))


if __name__ == "__main__":
    build()
