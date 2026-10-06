# Enemy visual drafts

Eight original designs: Acolyte, Pursuer, Scatter, Siege, and an evolved version
of each. These are static turnaround drafts, not completed animation cycles.

Each character folder contains three transparent **160 × 128** PNG frames and
a **480 × 128** turnaround sheet. View order is **front, back, right side**;
flip the right-facing pose horizontally for left. Ground anchor is `(80, 115)`
in top-left image coordinates. Individual poses are imported into Unity as
single sprites with Point filtering, 32 pixels per unit, no mipmaps, no texture
compression, and the shared ground pivot.

Open `EnemyDrafts_Review.png` for the paired comparison board. All characters and
the existing Collector reference are displayed at the same integer 3× scale.
`EnemyDrafts_Atlas.png` is a transparent 480 × 1024 atlas, with rows in this order:
Acolyte, Acolyte evolved, Pursuer, Pursuer evolved, Scatter, Scatter evolved,
Siege, Siege evolved. The atlas and turnaround sheets are not automatically
sliced; use the individual sprites directly or slice with 160 × 128 cells.

The mutations follow the enemy descriptions in `Docs/LORE_DOCUMENT.md`:

- Acolyte: extra casting hands outside the original pair.
- Pursuer: elongated reaching limbs, exposed spine, and recessed face.
- Scatter: broken funerary collar and a split throat/rib seam.
- Siege: a cracked coffin enclosed by growing ribs and grasping bones.

The artwork is authored with explicit pixel shapes and material shading in
`Source/build_enemy_drafts.py`, using Python and Pillow; no image-generation
model is used. Run that script to regenerate the PNGs and manifest. The review
board also uses local canvas-design fonts and the project's Collector sprite.
The new character sprites do not contain pixels copied from the Collector.

`manifest.json` records pose bounds, palettes, raw-pixel hashes, sheet rectangles,
and import settings. The exporter checks transparency and frame boundaries.
No gameplay assets have been assigned to these drafts.
