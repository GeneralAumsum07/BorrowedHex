# Original magician — detail only

The supplied PNG and metadata remain unchanged. The detailed sheet preserves the
exact **1280 × 640** dimensions, four **320 × 640** cells, and original direction
order: **left, front, right, back**. Every original alpha-mask pixel and opaque
bound is identical. Hat, hair, face, cloak and brim material regions also remain
in their original positions; the new brushwork only adds detail inside them.

`RogueMagician_Detailed_OriginalDimensions.png` is the complete detailed reference
sheet. Individual `*_Detailed.png` files use the exact original cell size. Detail
is authored on a five-pixel grid, subdividing the original ten-pixel blocks.
This adds shading, fabric folds, seams, hair highlights, hat creases and brim
detail without enlarging the outline or altering the proportions.

`Source/detail_original.py` regenerates the images with deterministic Pillow
code. It verifies every alpha mask byte-for-byte against the original sheet and
records the original PNG/metadata hashes in `manifest.json`.

## Animations

Animated poses are allowed to move the cloak, hair and hat while preserving the
original character design and **320 × 640** frame size. No limbs are revealed.
The first idle frame is identical to the detailed reference in each direction.

Each direction folder contains a **3840 × 3200** master sheet with twelve
320 × 640 columns and five rows. Rows are **Idle, Moving, Dash, Hurt, Death**.
Unused columns remain transparent; the manifest records each row's frame count.
Individual animation strips provide the same keys without unused cells.

| State | Frames | FPS | Loop |
|---|---:|---:|---|
| Idle | 8 | 8 | Yes |
| Moving | 8 | 12 | Yes |
| Dash | 8 | 24 | No |
| Hurt | 6 | 15 | No |
| Death | 12 | 10 | No |

All four supplied views are retained: left, front, right and back. Right can be
flipped for left movement if desired. There are 20 sequences and 168 frames.
Idle uses breathing, hair motion and a blink; moving uses concealed steps with
cloak/hem motion; dash includes anticipation, launch and recovery; hurt uses
recoil and a brief flash; death closes the eyes and slumps the costume down.

Unity master textures use point filtering, no mipmaps, no compression, full-rect
meshes and a shared ground pivot at **(160, 570)** from the frame's top-left.
The initial import uses **320 PPU** to account for the original tenfold upscale;
adjust this per project scale. Sliced sprites and `.anim` clips are ready under
the direction masters and `Clips/`. Idle/moving loop; dash/hurt/death play once.
Clips target the same GameObject's SpriteRenderer; gameplay wiring is separate.

`Previews/` contains animated GIFs, scaled down for review. The exported PNG
frames retain the exact original size. Preview pauses after one-shot states are
not part of the game animations.

Regenerate PNGs/GIFs with `python Source/animate_original.py`. Use the saved Unity
importer snippet with Pipeline's `eval_file` to refresh slices/clips after any
frame-count changes. Both sources affect only this asset directory.
