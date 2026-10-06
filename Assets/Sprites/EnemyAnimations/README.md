# Complete enemy animation sheets

Eight characters: Acolyte, Pursuer, Scatter, Siege, and their evolved forms.
Every character has front, back and right-side **idle, moving, attack and death**
animations. Use horizontal flipping for left. Both approved draft folders remain
unchanged. All drawing and animation use deterministic pixel code.

| Animation | Frames | FPS | Playback |
| --- | ---: | ---: | --- |
| Idle | 8 | 8 | Loop |
| Moving | 8 | 12 | Loop |
| Attack | 12 | 12 | Play once; release pose at frame 6 |
| Death | 12 | 10 | Play once; hold the corpse |

There are **96 sequences and 960 body frames**. The evolved forms also have
**480 matching aura frames**. Body animation includes separate head and limb
motion, trailing robe hems, coffin movement, anticipation, release, recovery,
and a jointed collapse. Their clothing/gear retain the approved darker palette.
Aura masks follow every new body frame, including attacks and death; the aura
weakens during death but remains faintly visible on the final corpse.

Each character's `*_Complete.png` is **1920 × 1536**, with twelve 160 × 128 cells
per row. The row order is:

1. Front: idle, moving, attack, death.
2. Back: idle, moving, attack, death.
3. Side: idle, moving, attack, death.

Idle/moving rows use the first eight columns; their final four columns are
transparent padding and are excluded from slicing and playback. Attack/death
use all twelve columns. `*_Front_Idle.png` and corresponding names provide
separate horizontal strips for every view/state. The manifest records all frame
counts, timings, sheet rows, visible bounds and raw-pixel hashes.

All frames use **32 PPU**, Point filtering, no mipmaps and no compression. Pivot
is `(0.5, 13/128)` for the common ground anchor `(80,115)` in image coordinates.
Master sheets are imported as Multiple sprites, with 120 named slices each.
`Clips/` holds Unity `.anim` files bound to a SpriteRenderer on the same object;
idle/moving loop, attack/death play once. Each discrete sprite gets its full
frame duration, including the final pose. Frame 6 is a timing marker in the manifest, not a
gameplay callback; the sheets do not change damage or projectile timing.

Evolved master sheets and strips also include `_Base` and `_Aura` exports, with
matching clips. Use the composite clips for a single-renderer character. For
separate aura control, play the Base and Aura clips in sync on two aligned
renderers, with the aura behind the body. Composite sprites already contain
their aura. No controllers, scenes, prefabs or gameplay code have been changed.

`Previews/Enemies_Front.gif`, `Enemies_Back.gif`, and `Enemies_Side.gif` show all
characters and states animated at integer 2× zoom. Attacks and deaths restart
in these previews for review; the delivered clips play once.

Regenerate the images with
`python Assets/Sprites/EnemyAnimations/Source/build_enemy_animations.py`.
The source records semantic layers from the preserved approved drawings, applies
an articulated joint hierarchy with nearest-neighbour transforms, and exports
the matching aura masks. `Source/import_enemy_animations.cs.txt` creates sprite
slices and clips through the running Unity Editor's Pipeline evaluator. Copy
it to a temporary `.cs` path and evaluate with a sufficient timeout.

`Source/approved_drafts_snapshot.json` records hashes of both preserved draft
folders. Export checks include opaque body transparency, complete frame bounds,
distinct body poses per sequence, and exact preservation of the approved art
when recording its unanimated layers.
