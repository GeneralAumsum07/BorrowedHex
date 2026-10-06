"""Revise the accepted deterministic pixel drafts without overwriting them.

The original drawing source remains in EnemyDrafts. Only selected material
ramps change for evolved poses; silhouettes and standard characters stay exact.
The glow is a separate RGBA layer so future motion can use the crisp body art.
"""

from __future__ import annotations

import hashlib
import importlib.util
import json
import sys
from pathlib import Path

from PIL import Image, ImageChops, ImageDraw, ImageFilter


ROOT = Path(__file__).resolve().parents[1]
APPROVED = ROOT.parent / "EnemyDrafts"
SOURCE = APPROVED / "Source/build_enemy_drafts.py"
spec = importlib.util.spec_from_file_location("approved_enemy_art", SOURCE)
art = importlib.util.module_from_spec(spec)
# Reading the approved source must not create a bytecode cache in its folder.
previous_bytecode_policy = sys.dont_write_bytecode
try:
    sys.dont_write_bytecode = True
    spec.loader.exec_module(art)
finally:
    sys.dont_write_bytecode = previous_bytecode_policy

SIZE = art.SIZE
AURA_RGB = (174, 143, 222)
# Opacity is deliberately low. Spell colours remain the bright focal points;
# the evolved state is communicated by a soft halo around the full silhouette.
AURA_BANDS = (34, 24, 16, 10, 5)
PULSE_STRENGTHS = (.80, .86, 1.00, 1.10, 1.16, 1.10, 1.00, .86)
MATERIAL_BRIGHTNESS = {
    "wine_e": .86,
    "blue_e": .86,
    "cloth": .88,
    "leather": .88,
    "wood": .88,
    "iron": .88,
    "brass": .89,
    # The siege familiar's stone is also its body. A smaller adjustment keeps
    # its large facets readable while its carried gear receives the full change.
    "stone": .94,
    "wine": .86,
    "blue": .86,
}
ORIGINAL_PALETTE = art.P
DARK_PALETTE = {
    material: [tuple(round(channel * MATERIAL_BRIGHTNESS.get(material, 1))
                     for channel in colour[:3]) + (255,) for colour in ramp]
    for material, ramp in ORIGINAL_PALETTE.items()
}


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def snapshot_approved():
    # A persisted inventory lets us prove the previous draft was preserved. It
    # excludes Unity metadata, which the editor can create during asset imports.
    files = {p.relative_to(APPROVED).as_posix(): digest(p)
             for p in APPROVED.rglob("*") if p.is_file() and p.suffix != ".meta"
             and "__pycache__" not in p.parts}
    snapshot_path = ROOT / "Source/approved_draft_snapshot.json"
    if snapshot_path.exists():
        assert json.loads(snapshot_path.read_text()) == files, "Approved draft has changed"
    else:
        snapshot_path.write_text(json.dumps(files, indent=2) + "\n")
    return files


def draw_body(draw, view, evolved):
    # The imported module owns the palette referenced by its material brushes.
    # Restore it even on failure so standard poses can never inherit this tint.
    art.P = DARK_PALETTE if evolved else ORIGINAL_PALETTE
    try:
        return draw(view, evolved)
    finally:
        art.P = ORIGINAL_PALETTE


def aura_layer(body, strength=1):
    silhouette = body.getchannel("A")
    alpha = Image.new("L", SIZE)
    previous = silhouette
    for radius, opacity in enumerate(AURA_BANDS, 1):
        # Integer morphology preserves the sprite's pixel rhythm. Each ring
        # gets its own opacity instead of blurring or tinting the body pixels.
        expanded = silhouette.filter(ImageFilter.MaxFilter(radius * 2 + 1))
        ring = ImageChops.subtract(expanded, previous)
        alpha.paste(round(opacity * strength), mask=ring)
        previous = expanded
    layer = Image.new("RGBA", SIZE, AURA_RGB + (0,))
    layer.putalpha(alpha)
    return layer


def compose(body, aura):
    result = aura.copy()
    result.alpha_composite(body)
    return result


def check_image(im, binary=False):
    assert im.size == SIZE and im.mode == "RGBA"
    bounds = im.getbbox()
    assert bounds and 0 < bounds[0] < bounds[2] < SIZE[0]
    assert 0 < bounds[1] < bounds[3] < SIZE[1]
    if binary:
        assert set(im.getchannel("A").get_flattened_data()) == {0, 255}
    return bounds


def review_board(frames):
    background, panel, ink, muted = "#151722", "#1c1f2b", "#eee3d0", "#a3a7b6"
    board = Image.new("RGB", (2100, 1970), background)
    d = ImageDraw.Draw(board)
    d.text((52,32), "BORROWED HEX · DRAFT 02", font=art.font(38,True), fill=ink)
    d.text((54,85), "Darker evolved clothing and gear · faint violet aura", font=art.font(20), fill=muted)
    d.text((54,122), "Front, back, right side · approved anatomy and silhouettes", font=art.font(17), fill=muted)
    collector = Image.open(art.PROJECT / "Assets/Game/Resources/WorldArt/Necromancer.png").convert("RGBA").crop((0,0,160,128))
    collector = collector.crop(collector.getbbox()).resize((126,156), Image.Resampling.NEAREST)
    board.paste(collector, (1860,22), collector)
    d.text((1820,186), "Existing Collector · 3×", font=art.font(15), fill=muted)
    for row, (name, (_, description)) in enumerate(art.FAMILIES.items()):
        y = 270 + row * 400
        d.rounded_rectangle((40,y,2060,y+384), radius=13, fill=panel)
        d.text((62,y+17), name.upper(), font=art.font(24,True), fill=ink)
        d.text((62,y+53), description, font=art.font(15), fill=muted)
        for group, evolved in enumerate((False, True)):
            x0 = 180 + group * 920
            title = "EVOLVED · DARKER GEAR + AURA" if evolved else "STANDARD · PRESERVED"
            d.text((x0+75,y+17), title, font=art.font(15,True), fill="#c1a4e6" if evolved else muted)
            for col, view in enumerate(art.VIEWS):
                im = frames[name,evolved,view]
                # This review window includes the five-pixel glow on all sides.
                # Every character and Collector use the same integer 3× zoom.
                bounds = im.getbbox()
                assert bounds[0] >= 32 and bounds[1] >= 30 and bounds[2] <= 130 and bounds[3] <= 124
                window = im.crop((32,30,130,124)).resize((294,282), Image.Resampling.NEAREST)
                x = x0 + col * 298
                board.paste(window, (x,y+78), window)
                d.text((x+120,y+360), view.upper(), font=art.font(13), fill=muted)
        d.line((1072,y+76,1072,y+354), fill="#333646", width=1)
    d.text((54,1900), "160 × 128 transparent frames · shared ground pivot · aura exported separately with an 8-frame opacity loop", font=art.font(17), fill=muted)
    d.text((54,1930), "Static body drafts. Aura loops are an animation reference; movement, attacks and death are not yet authored.", font=art.font(15), fill=muted)
    board.save(ROOT / "EnemyDrafts_V2_Review.png")


def build():
    approved_snapshot = snapshot_approved()
    frames, records = {}, []
    atlas = Image.new("RGBA", (480,1024))
    for family_row, (name, (draw, description)) in enumerate(art.FAMILIES.items()):
        for evolved in (False, True):
            character = name + ("_Evolved" if evolved else "")
            folder = ROOT / character
            folder.mkdir(parents=True, exist_ok=True)
            sheet = Image.new("RGBA", (480,128))
            base_sheet = Image.new("RGBA", (480,128))
            aura_sheet = Image.new("RGBA", (480,128))
            poses = []
            for col, view in enumerate(art.VIEWS):
                stem = f"{character}_{view}"
                body = draw_body(draw,view,evolved)
                body_bounds = check_image(body, binary=True)
                original = Image.open(APPROVED / character / f"{stem}.png").convert("RGBA")
                assert body.getchannel("A").tobytes() == original.getchannel("A").tobytes()
                if not evolved:
                    assert body.tobytes() == original.tobytes(), (character,view,"Standard changed")
                aura = aura_layer(body) if evolved else Image.new("RGBA",SIZE)
                im = compose(body,aura) if evolved else body
                bounds = check_image(im)
                # Glows must not cover bone highlights, flames, or any opaque
                # body pixel. This also makes the separate layer easy to reuse.
                assert ImageChops.multiply(aura.getchannel("A"),body.getchannel("A")).getbbox() is None
                assert im.crop(body_bounds).getbbox()
                frames[name,evolved,view] = im
                im.save(folder / f"{stem}.png")
                sheet.alpha_composite(im,(col*160,0))
                pose = {"view":view,"file":f"{character}/{stem}.png",
                        "sheetRectTopLeft":[col*160,0,160,128],"bodyBounds":body_bounds,
                        "visibleBounds":bounds,"sha256":hashlib.sha256(im.tobytes()).hexdigest()}
                if evolved:
                    body.save(folder / f"{stem}_Base.png")
                    aura.save(folder / f"{stem}_Aura.png")
                    base_sheet.alpha_composite(body,(col*160,0))
                    aura_sheet.alpha_composite(aura,(col*160,0))
                    pulse = Image.new("RGBA", (160*len(PULSE_STRENGTHS),128))
                    for frame,strength in enumerate(PULSE_STRENGTHS):
                        layer = aura_layer(body,strength)
                        check_image(layer)
                        pulse.alpha_composite(layer,(frame*160,0))
                    pulse.save(folder / f"{stem}_AuraPulse.png")
                    pose.update(baseFile=f"{character}/{stem}_Base.png",
                                auraFile=f"{character}/{stem}_Aura.png",
                                auraPulseFile=f"{character}/{stem}_AuraPulse.png")
                poses.append(pose)
            sheet.save(folder / f"{character}_Turnaround.png")
            if evolved:
                base_sheet.save(folder / f"{character}_Base_Turnaround.png")
                aura_sheet.save(folder / f"{character}_Aura_Turnaround.png")
            atlas.alpha_composite(sheet, (0,(family_row*2+int(evolved))*128))
            records.append({"character":character,"category":name,"evolved":evolved,
                            "evolutionDesign":description,"turnaround":f"{character}/{character}_Turnaround.png",
                            "poses":poses})
    atlas.save(ROOT / "EnemyDrafts_V2_Atlas.png")
    review_board(frames)
    manifest = {"revision":2,"approvedDraft":"../EnemyDrafts",
                "status":"static body drafts with separate aura loop reference",
                "drawingMethod":"deterministic Python/Pillow; original silhouettes preserved",
                "frameSize":SIZE,"pixelsPerUnit":32,"groundAnchorTopLeft":[80,115],
                "unityPivot":[.5,13/128],"viewOrder":art.VIEWS,"flipSideForLeft":True,
                "filterMode":"Point","mipmaps":False,"compression":"None",
                "evolvedMaterialBrightness":MATERIAL_BRIGHTNESS,
                "aura":{"colourRGB":AURA_RGB,"outerRadiusPixels":5,"bandAlpha":AURA_BANDS,
                        "pulseStrengths":PULSE_STRENGTHS,"pulseFrames":8,"pulseFPS":8,
                        "appliesTo":"all evolved animation states; preview attached to static poses",
                        "compositing":"straight-alpha normal blending, aura behind base sprite"},
                "characters":records}
    (ROOT / "manifest.json").write_text(json.dumps(manifest,indent=2)+"\n")
    for name,expected in approved_snapshot.items():
        assert digest(APPROVED / name) == expected, "Approved file was overwritten: " + name
    print(json.dumps({"output":str(ROOT),"characters":8,"directionalPoses":24,
                      "auraLayers":12,"auraPulseSheets":12,"approvedDraftPreserved":True},indent=2))


if __name__ == "__main__":
    build()
