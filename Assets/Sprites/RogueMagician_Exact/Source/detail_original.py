"""Add pixel detail without changing any original cell, outline or material area.

The supplied sheet is 1280x640 with four 320x640 cells. Its ten-pixel source grid
is detailed on a five-pixel grid, then exported at the exact original size.
Every brush is clipped to an original material mask. Alpha is copied verbatim.
"""

from __future__ import annotations

import hashlib
import json
import math
from pathlib import Path

from PIL import Image, ImageChops, ImageDraw


ROOT=Path(__file__).resolve().parents[1]
ORIGINAL=ROOT.parent/"Byog_Game_Jam_Assets.png"
SCALE=5
NATIVE=(64,128)
CELL=(320,640)
VIEWS=("Left","Front","Right","Back")
BLUE={(16,63,118):3,(11,50,96):2,(7,41,82):1}
PURPLE={(118,66,138):3,(108,56,128):2,(159,97,182):5}
BRASS={(168,134,94):3,(150,119,81):2,(219,182,140):5}
RAMPS={
    "cloak":["081d39","0b2952","0b3260","103f76","185286","2b669a","507da6"],
    "hair":["34213f","4b2b57","643780","76428a","8e579f","9f61b6","bd85cd"],
    "hat":["392329","4e2c2b","573027","663931","805249","a16754","c88b66"],
    "skin":["472a2c","57312d","60362e","7c4d3d","a56d50","c38a64","e8b88b"],
    "brass":["5d453b","80623f","967751","a8865e","c3a276","dbb889","ecd2a1"],
}


def rgba(colour):return tuple(int(colour[i:i+2],16) for i in (0,2,4))+(255,)
PALETTE={key:[rgba(c) for c in ramp] for key,ramp in RAMPS.items()}


def material(colour,x,y,view):
    rgb=colour[:3]
    if rgb in BLUE:return "cloak",BLUE[rgb]
    if rgb in PURPLE:return "hair",PURPLE[rgb]
    if rgb in BRASS:return "brass",BRASS[rgb]
    if rgb==(0,0,0):return "eye",0
    # The face and hat share browns in the supplied art. Existing colour and a
    # small face region distinguish them; neither region's boundary is moved.
    face_boxes={"Front":(22,30,40,50),"Right":(30,34,44,51),"Left":(10,34,26,51)}
    box=face_boxes.get(view)
    skin=box and box[0]<=x<box[2] and box[1]<=y<box[3]
    if skin:return "skin",2
    shades={(96,54,46):2,(102,57,49):3,(128,82,73):4,(87,47,39):1,
            (100,49,39):2,(96,50,42):2,(118,60,49):3,(178,102,87):5}
    return "hat",shades.get(rgb,3)


def masks_for(source,view):
    masks={key:Image.new("L",NATIVE) for key in (*RAMPS,"eye")}
    pixels=source.load()
    for y in range(128):
        for x in range(64):
            if pixels[x,y][3]:masks[material(pixels[x,y],x,y,view)[0]].putpixel((x,y),255)
    return masks


def stroke(image,mask,points,colour,width=1):
    layer=Image.new("RGBA",NATIVE)
    ImageDraw.Draw(layer).line(points,fill=colour,width=width)
    # Only existing pixels of this material can receive the detailing brush.
    layer.putalpha(ImageChops.multiply(layer.getchannel("A"),mask))
    image.alpha_composite(layer)


def front_face(out,masks,source,closed=False):
    # Preserve the supplied stepped cheeks/chin and every original face colour.
    # New shading clusters changed the apparent face shape despite the matching
    # outer sprite alpha; copying source pixels avoids that perceptual change.
    face_mask=ImageChops.lighter(masks["skin"],masks["eye"])
    out.paste(source,(0,0),face_mask)
    if closed:
        # Blink/death poses close the original eyes inside their own marks.
        # Skin, cheek contour and chin remain copied from the original draft.
        for y in range(128):
            for x in range(64):
                if masks["eye"].getpixel((x,y)):
                    out.putpixel((x,y),(96,54,46,255))
        for eye_x in (26,34):
            stroke(out,face_mask,[(eye_x,37),(eye_x+1,37)],(64,39,36,255))


def detail(source,view):
    masks=masks_for(source,view)
    out=source.copy();pixels=source.load();op=out.load()
    for y in range(128):
        for x in range(64):
            colour=pixels[x,y]
            if not colour[3]:continue
            mat,index=material(colour,x,y,view)
            if mat=="eye":continue
            # Quantized plane shading subdivides the existing flat fields while
            # retaining their original dark folds and highlight clusters.
            if mat=="cloak":
                offset=int(.8*math.cos((x*.10+y*.012)*math.pi)-.3)
            elif mat=="hair":
                offset=int(.75*math.sin((x-y*.28)*.55))
            elif mat=="hat":
                offset=1 if x<29 and y<30 else 0
            elif mat=="skin":
                offset=1 if x<31 else 0
            else:offset=0
            # An interior bevel describes the edge without adding a single
            # opaque pixel outside the old silhouette or moving its boundary.
            if x+1>=64 or not pixels[x+1,y][3]:offset-=1
            if y+1>=128 or not pixels[x,y+1][3]:offset-=1
            op[x,y]=PALETTE[mat][max(0,min(6,index+offset))]
    if view=="Front":
        folds=[[(28,52),(33,63),(34,78),(30,96),(32,110)],[(21,61),(19,76),(24,88),(18,106)],
               [(40,59),(44,75),(43,88),(51,109)],[(27,87),(26,96),(17,108)],[(38,77),(36,96),(39,111)]]
    elif view=="Back":
        folds=[[(25,54),(23,69),(29,82),(32,102),(31,111)],[(42,57),(40,72),(36,85),(40,106)],
               [(20,76),(23,91),(16,107)],[(46,83),(45,96),(49,109)],[(30,77),(34,85),(31,92)]]
    elif view=="Right":
        folds=[[(39,54),(34,66),(24,82),(29,96),(44,108)],[(43,66),(39,76),(33,87)],
               [(30,83),(23,94),(19,106)],[(41,87),(48,99),(52,110)],[(25,96),(28,107),(38,111)]]
    else:
        folds=[[(64-x,y) for x,y in points] for points in
               [[(39,54),(34,66),(24,82),(29,96),(44,108)],[(43,66),(39,76),(33,87)],
                [(30,83),(23,94),(19,106)],[(41,87),(48,99),(52,110)],[(25,96),(28,107),(38,111)]]]
    for points in folds:
        stroke(out,masks["cloak"],points,PALETTE["cloak"][1])
        stroke(out,masks["cloak"],[(x-1,y) for x,y in points[:-1]],PALETTE["cloak"][4])
        if len(points)>3:stroke(out,masks["cloak"],[(x-2,y) for x,y in points[1:3]],PALETTE["cloak"][5])
    # Short seam stitches and a narrow inner hem are costume detailing only.
    stroke(out,masks["cloak"],[(7,110),(21,111),(33,110),(46,112),(57,111)],PALETTE["cloak"][4])
    for x in range(10,57,6):stroke(out,masks["cloak"],[(x,108),(x+1,109)],PALETTE["cloak"][5])
    for y in range(67,103,7):stroke(out,masks["cloak"],[(33,y),(34,y+1)],PALETTE["cloak"][4])
    hair_paths=[[(15,36),(13,43),(18,51),(23,55)],[(21,37),(20,44),(24,51)],
                [(42,37),(45,44),(40,52),(37,56)],[(48,40),(49,46),(45,52)]]
    if view=="Back":hair_paths += [[(28,38),(26,45),(31,54)],[(35,38),(38,45),(33,55)]]
    for points in hair_paths:
        stroke(out,masks["hair"],points,PALETTE["hair"][1])
        stroke(out,masks["hair"],[(x-1,y-1) for x,y in points[:-1]],PALETTE["hair"][5])
    for points in [[(29,13),(28,19),(34,24),(38,28)],[(19,25),(26,22),(31,25)],
                   [(39,24),(43,29),(44,32)],[(14,31),(23,29),(30,31)]]:
        stroke(out,masks["hat"],points,PALETTE["hat"][1])
        stroke(out,masks["hat"],[(x,y-1) for x,y in points[:-1]],PALETTE["hat"][4])
    for x,y in [(23,23),(25,24),(27,25),(29,25),(31,26)]:
        stroke(out,masks["hat"],[(x,y),(x+1,y)],PALETTE["hat"][3])
    stroke(out,masks["brass"],[(4,30),(17,28),(30,29),(43,28),(59,32)],PALETTE["brass"][5])
    for x in range(7,57,6):stroke(out,masks["brass"],[(x,33),(x+1,33)],PALETTE["brass"][1])
    # Recessed eyes retain every original eye pixel. Small highlights are placed
    # within the eye marks, never beside them or on a redesigned face.
    eye_box=masks["eye"].getbbox()
    if view=="Front":
        front_face(out,masks,source)
    elif eye_box:
        for x in range(eye_box[0],eye_box[2]):
            if masks["eye"].getpixel((x,eye_box[1])):
                out.putpixel((x,eye_box[1]),(170,145,123,255))
                break
        stroke(out,masks["skin"],[(eye_box[0],eye_box[3]+3),(eye_box[2],eye_box[3]+3)],PALETTE["skin"][1])
    # Restore alpha from the original instead of deriving it from detail layers.
    out.putalpha(source.getchannel("A"))
    assert out.getchannel("A").tobytes()==source.getchannel("A").tobytes()
    return out


def build():
    original=Image.open(ORIGINAL).convert("RGBA")
    assert original.size==(1280,640)
    hashes={p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in (ORIGINAL,Path(str(ORIGINAL)+".meta"))}
    sheet=Image.new("RGBA",original.size)
    records=[]
    for col,view in enumerate(VIEWS):
        source=original.crop((col*320,0,(col+1)*320,640))
        small=source.resize(NATIVE,Image.Resampling.NEAREST)
        result=detail(small,view).resize(CELL,Image.Resampling.NEAREST)
        # Dimensions, alpha bytes, opaque bounds and pixel count are exact;
        # this guarantees all original stepped silhouette edges are preserved.
        assert result.size==source.size
        assert result.getchannel("A").tobytes()==source.getchannel("A").tobytes()
        assert result.getbbox()==source.getbbox()
        result.save(ROOT/f"RogueMagician_{view}_Detailed.png")
        sheet.alpha_composite(result,(col*320,0))
        records.append({"view":view,"file":f"RogueMagician_{view}_Detailed.png","originalColumn":col,
                        "bounds":source.getbbox(),"alphaSHA256":hashlib.sha256(source.getchannel("A").tobytes()).hexdigest()})
    assert sheet.getchannel("A").tobytes()==original.getchannel("A").tobytes()
    sheet.save(ROOT/"RogueMagician_Detailed_OriginalDimensions.png")
    manifest={"status":"detailing only; exact original silhouettes","sheetSize":[1280,640],"frameSize":CELL,
              "originalOrder":VIEWS,"detailPixelSize":5,"pixelsPerUnit":320,"originalFileHashes":hashes,
              "sheetFile":"RogueMagician_Detailed_OriginalDimensions.png","frames":records,
              "originalMaskPreserved":True,"materialAreasPreserved":True}
    (ROOT/"manifest.json").write_text(json.dumps(manifest,indent=2)+"\n")
    for p in (ORIGINAL,Path(str(ORIGINAL)+".meta")):assert hashlib.sha256(p.read_bytes()).hexdigest()==hashes[p.name]
    print(json.dumps({"sheet":list(sheet.size),"cells":list(CELL),"allFourAlphaMasksMatch":True,"originalPreserved":True,"output":str(ROOT)},indent=2))


if __name__=="__main__":build()
