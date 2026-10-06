"""Audit exported images against the supplied art and animation manifest."""

import hashlib
import json
from pathlib import Path

from PIL import Image

root = Path(__file__).resolve().parents[1]
manifest = json.loads((root / "manifest.json").read_text())
original = Image.open(root.parent / "Byog_Game_Jam_Assets.png").convert("RGBA")
reference = Image.open(root / manifest["sheetFile"]).convert("RGBA")
assert reference.size == original.size == (1280, 640)
original_alpha = original.getchannel("A").tobytes()
reference_alpha = reference.getchannel("A").tobytes()
changed_mask_pixels = sum(a != b for a, b in zip(original_alpha, reference_alpha))
assert changed_mask_pixels == 0
for file, expected in manifest["originalFileHashes"].items():
    assert hashlib.sha256((root.parent / file).read_bytes()).hexdigest() == expected, file

checked = 0
for col, view in enumerate(manifest["originalOrder"]):
    single = Image.open(root / f"RogueMagician_{view}_Detailed.png").convert("RGBA")
    assert single.size == (320, 640)
    assert single.tobytes() == reference.crop((col * 320, 0, (col + 1) * 320, 640)).tobytes()
    if view == "Front":
        source_front = original.crop((col * 320, 0, (col + 1) * 320, 640))
        # Exact eye size, shape, spacing and colour come from the original art.
        original_eyes = {i for i, pixel in enumerate(source_front.get_flattened_data()) if pixel == (0,0,0,255)}
        detailed_eyes = {i for i, pixel in enumerate(single.get_flattened_data()) if pixel == (0,0,0,255)}
        assert original_eyes == detailed_eyes
        face_colours = {(96,54,46),(102,57,49),(128,82,73),(87,47,39),
                        (100,49,39),(96,50,42),(118,60,49),(178,102,87),(0,0,0)}
        for y in range(150,250):
            for x in range(110,200):
                pixel = source_front.getpixel((x,y))
                if pixel[3] and pixel[:3] in face_colours:
                    assert single.getpixel((x,y)) == pixel, (x,y)

for sequence in manifest["sequences"]:
    strip = Image.open(root / sequence["stripFile"]).convert("RGBA")
    master = Image.open(root / sequence["masterFile"]).convert("RGBA")
    count, row = sequence["frames"], sequence["masterRow"]
    assert strip.size == (count * 320, 640)
    assert master.size == (3840, 3200)
    hashes = set()
    for index in range(count):
        frame = strip.crop((index * 320, 0, (index + 1) * 320, 640))
        assert frame.size == (320, 640)
        assert frame.tobytes() == master.crop((index * 320, row * 640, (index + 1) * 320, (row + 1) * 640)).tobytes()
        bounds = frame.getbbox()
        assert bounds and bounds[0] > 0 and bounds[1] > 0 and bounds[2] < 320 and bounds[3] < 640
        hashes.add(hashlib.sha256(frame.tobytes()).hexdigest())
        checked += 1
        if sequence["state"] == "Idle" and index == 0:
            reference_frame = Image.open(root / f"RogueMagician_{sequence['view']}_Detailed.png").convert("RGBA")
            assert frame.tobytes() == reference_frame.tobytes()
    assert len(hashes) > 1, "Animation contains only duplicate frames: " + sequence["stripFile"]
    if count < 12:
        assert not master.crop((count * 320, row * 640, 3840, (row + 1) * 640)).getbbox()
    assert (root / sequence["clipFile"]).is_file(), sequence["clipFile"]
    preview = Image.open(root / "Previews" / f"RogueMagician_{sequence['state']}.gif")
    assert preview.n_frames > 1

report = {"referenceSheetSize":list(reference.size), "animationFrameSize":[320,640],
    "changedReferenceOutlinePixels":changed_mask_pixels, "verifiedFrames":checked,
    "verifiedSequences":len(manifest["sequences"]), "restFramesIdenticalToReference":True,
    "sourcePNGAndMetadataUnchanged":True, "allPosesInsideFrame":True,
    "masterSlicesMatchStateStrips":True}
report["frontEyesMatchOriginalExactly"] = True
report["frontFacePixelsMatchOriginalExactly"] = True
(root / "verification.json").write_text(json.dumps(report, indent=2) + "\n")
print(json.dumps(report, indent=2))
