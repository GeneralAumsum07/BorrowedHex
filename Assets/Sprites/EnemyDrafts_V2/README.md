# Enemy draft 02

The approved draft remains unchanged in `../EnemyDrafts`. This revision preserves
all anatomy, poses and silhouettes, and leaves the four standard designs exact.
Evolved cloth is about 14% darker, leather/wood/metal 12% darker, and brass 11%
darker. The siege familiar's stone is 6% darker. Bones, masks, faces and spell
accents keep their approved colours.

`EnemyDrafts_V2_Review.png` shows all eight designs in front, back and right-side
views. `EnemyDrafts_V2_Atlas.png` keeps the original 160 × 128 frame convention,
three columns, and paired standard/evolved rows in Acolyte, Pursuer, Scatter,
Siege order. Left uses horizontal flipping. Pivot remains `(80, 115)` in
top-left pixel coordinates; Unity uses `(0.5, 13/128)` at 32 pixels per unit.

Each evolved pose includes:

- `*_Front.png`, `*_Back.png`, `*_Side.png`: composite preview with faint aura.
- `*_Base.png`: crisp darker body with binary transparency.
- `*_Aura.png`: violet RGBA aura only; faint five-pixel fade outside the body.
- `*_AuraPulse.png`: eight 160 × 128 cells, left to right, for a one-second aura
  opacity loop at 8 FPS. These do not animate the creature's body.

The aura layers use straight alpha and normal blending. Render them behind the
matching base pose at the same pivot. The intended later animation treatment is
to follow each evolved body frame in idle, movement, attack and death, carrying
the gentle pulse continuously across state changes. The aura mask must follow
the moving silhouette; a single idle mask should not be reused for every body
animation frame. No animation controller or runtime rendering has been changed.

Individual body/composite/aura PNGs use Point filtering with no mipmaps or texture
compression. Atlas, turnaround and pulse sheets are grid references; they are
not automatically sliced. New aura assets use partial transparency by design.

Regenerate with `python Assets/Sprites/EnemyDrafts_V2/Source/build_enemy_drafts_v2.py`.
The script imports the preserved draft's original drawing source, applies
material palettes selectively, and exports the aura separately. It uses no
image-generation model. `Source/approved_draft_snapshot.json` records hashes of
the approved draft; the exporter verifies they are unchanged.
