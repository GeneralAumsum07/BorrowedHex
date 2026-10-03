# Arena Visual Redesign: Design Spec

Date: 2026-10-04 · Status: **DRAFT**. Section 1 is approved; sections 2–5 await review.

## 0. Intent

**Owner's request (verbatim intent):** redesign the arenas and their textures so the game reads
as 2.5D, as close as possible to *Rogue's Odyssey* (duggoman.itch.io/rogues-odyssey), while
keeping BorrowedHex's dark, nightmarish theme. Encounters 1 and 2 currently look worst. Nothing
outside the arena may be visible (the grey void and skyline past the walls).

**Decisions the owner has made (2026-10-04):**

| # | Decision | Choice |
|---|---|---|
| 1 | Ground style | **Painted**: smooth, no pixel grid. Pixel art stays on actors and VFX only. |
| 2 | Hiding the outside | **Enclosure**: tall cliffs, walls and trees ring each arena. |
| 3 | 3D source | **Free kits plus walls built in code**: Kenney Graveyard Kit 5.0 and Quaternius Ultimate Nature Pack (both CC0) for props; the enclosure mass is built in code. |
| 4 | Scope | **All four arenas** (Courtyard, Graveyard, Cave, Sanctum), with the most detail on encounters 1 and 2. |
| 5 | Architecture | **Option 1, runtime-built from code recipes**: no hand-placed prefabs. |
| 6 | Lighting | Real lighting (moonlight, warm point lights, soft shadows) replaces the flat quantized shading. |

**Assumptions:**
- Gameplay is untouched. `ArenaLayout` collision rects, arena sizes, cover positions and
  `ArenaSim` stay authoritative. This is presentation only.
- The live arena morph, the boss-room reveal, the cover decay dissolve, actor occlusion and the
  travel curtain all keep working.

**Evidence behind the diagnosis** (from reading the code, 2026-10-04):
- **Why the floors look noisy.** `WorldPixelSurfaces.Create` builds 512² floor and 128² wall
  textures by tiling transparent overlay atlases with modulo sampling and point filtering. The
  tan rims of the atlas blobs repeat as boxes.
- **Why the void shows.** `WorldCameraPolicy` pitches the camera only 25° (offset
  (0, 7.607, -16.313), FOV 40), so the frustum sees far past the north wall. What fills that
  view is `WorldBackdrop` (a 600×600 cube) and side-view skyline sprites.

**Out of scope:** gameplay, layouts, UI/HUD, actor and boss sprites, VFX sprites, audio.

---

## 1. Camera and enclosure (APPROVED 2026-10-04)

### Camera (`WorldCameraPolicy`)
- **Pitch:** 25° → **50°**.
- **Distance:** about 20 units from the focus point, so the offset is roughly (0, 15.3, -12.9).
- **FOV:** stays at 40° vertical.
- **Follow limits:** `ClampFocus` margins are unchanged (x ±6 inside the walls, y +5/-5).

### Visibility arithmetic
Computed by hand for a 16:9 screen; the test in §5 is the authority.

| Edge | How far the view reaches when the focus is clamped | What it needs |
|---|---|---|
| North | about 9 units past the north wall | An enclosure **about 5 units tall** blocks the line of sight. |
| East and west | the top corners reach about 14 units past the walls | Side enclosure heights rise toward the north. |
| South | about 2 units past the south wall | A low edge is enough. |

### Enclosure ring
A visual band 4–8 units deep, just outside `bounds`, with no collision. It is graded by height:

| Band | Height | Content |
|---|---|---|
| North | 6–9 units | The theme's statement wall: crypt façades, cloister wall, cliff face or altar colonnade. |
| East and west | about 2 units at the south end, rising to about 7 at the north | Rock walls, dead trees, broken walls and iron fences in front of taller masses. |
| South | at most 1.2 units | A low fence, dead-brush hedge or broken wall; beyond it, dark ground and foliage tops. Anything that still overlaps an actor uses the existing `_Occlusion` cutout. |

**Safety net.** Behind the ring is a dark ground plane under black exponential fog, so any
remaining gap reads as darkness, never as grey void.

**Removed:**
- `WorldBackdrop` (the 600² cube)
- the "Sky" and "Horizon" background sprites
- the 12 side scenery billboards
- the `_NorthLimit` apron clipping

---

## 2. Floors, materials and lighting (DRAFT, needs review)

### 2.1 Shader `BorrowedHex/PaintedWorld`
A new URP forward shader. It **keeps PixelWorld's entire property interface**, so that
`WorldGeometry`'s effect code keeps working unchanged.

**Properties kept:**
- `_BaseMap`, `_FromMap`, `_BaseColor`
- `_Morph`, `_Visible`, `_Retiring`
- `_GlitchTime`, `_Glitches`
- `_Reveal`, `_Emission`
- `_FogAmount`, `_Occlusion`

**Removed:** `_NorthLimit`.

**Lighting:**
- **Diffuse:** wrapped Lambert (soft terminator, no specular), giving the painted look.
- **Main light:** with shadows (`_MAIN_LIGHT_SHADOWS`, soft where the pipeline asset allows it).
- **Additional lights:** per-object.
- **Ambient:** from `RenderSettings.ambientLight`.
- **Order of operations:** `_Reveal` multiplies the lit colour, then `_Emission` is added.

**Kept as they are:** the decay dissolve (world-space grain hash on `_Visible`/`_Retiring`) and
the morph patch mix with glitch bands.

**Shadow pass:** a `ShadowCaster` pass that respects the same coverage clip. A dissolving prop
must not cast a solid shadow.

**PixelWorld stays** for the pixel sprites that still use it (VFX ground decals and anything
not migrated). It is not deleted in this work.

### 2.2 Painted floors
- **Baking.** An editor tool, `PaintedFloorBaker` (menu *BorrowedHex → Bake Painted Floors*),
  ports the approved swatch generator (fbm value noise, domain-warped Voronoi flagstones,
  bilinear decals). It writes **one PNG per theme**.
- **Size.** Each PNG covers the arena once, with no tiling or repetition: roughly 40 px per unit,
  so about 2048×1536 for 48×36. The images are generated from our own code, so they are
  committed.
- **Import settings.** Bilinear, mipmaps, Read/Write enabled, because the morph snapshot reads
  pixels.
- **Look per theme.**
  - **Courtyard:** dark slate flagstones, black-moss cracks, creeping moss, pale weeds.
  - **Graveyard:** dark moss with bald grave-earth patches, worn footpaths and pale weeds.
  - **Cave:** violet-grey rock floor with damp dark pools and mineral flecks.
  - **Sanctum:** worn basalt tiles with faint inlaid lines. The existing ritual seal stays on top.
- **Outer ground.** The darker ground under and past the ring is a small tiling texture of the
  same style.
- **Floor chunks.** The floor mesh is split into **4×3 chunks**. Both URP assets cap
  `m_AdditionalLightsPerObjectLimit` at 4, so one floor mesh would receive only 4 point lights.
  With chunks, each one gets its own nearest four (verified in `PC_RPAsset` and
  `Mobile_RPAsset`, 2026-10-04).
- **Morph snapshot cost.** Today `Snapshot()` runs `GetPixels32` and a per-pixel lerp on the CPU.
  On a 2048² texture that is about 3M pixels, a visible hitch. Instead:
  - when the outgoing `_Morph >= 1`, the snapshot **is** the outgoing `_BaseMap`, with no CPU
    work;
  - only an interrupted morph bakes on the CPU, and then at a 512² cap.

### 2.3 Kit materials
- **Kenney.** Every model samples one 10 KB `colormap.png`. A baked **night colormap**
  (desaturated and darkened) replaces it, and per-theme `_BaseColor` tints finish the job.
- **Quaternius.** Models carry flat material colours and no textures. A name → night-colour
  table (bark, dead wood, moss rock, plain rock, leaves) assigns `_BaseColor` per submesh.
- **Instances.** All of them become `PaintedWorld` material instances owned by the arena, and are
  released on Dispose, as today.

### 2.4 Lighting rig (per theme, set in `ApplyTheme`)
- **Moon.** The existing `sun` becomes a cool blue-violet moon at low intensity, with shadows on.
  The pitch is chosen so props cast readable shadows toward the camera's side.
- **Fire.** Warm, flickering point lights on Kenney `fire-basket`, `lantern-candle` and
  `lightpost` props, about 8 per arena.
  - **Placement rule:** no floor chunk is touched by more than 4 of them.
  - **Flicker:** Perlin noise on intensity, in unscaled time, so it holds under pause.
- **Ambient.** Very dark and per theme; the fog colour is near-black and per theme.
- **Boss reveal.** The Sanctum keeps the existing reveal, which drives `sun.intensity` and the
  fog. The reveal also fades in the fire lights, so the room still starts dark.

---

## 3. Props and dressing (DRAFT, needs review)

### 3.1 Model import
- **Import tool.** `WorldModelImporter` (an editor menu) copies the **subset of FBX files the
  recipes use** from `Downloads/itch_downloads` into `Assets/Game/Resources/WorldModels/{Kenney,Quaternius}/`.
  It copies each kit's `License.txt` alongside.
- **Committed, unlike WorldArt.** These models are CC0, so they are committed, unlike the
  git-ignored `Resources/WorldArt`. A fresh checkout renders the real arenas without the
  owner's downloads.

### 3.2 Recipes (`ArenaDressing`)
- **Format.** Pure data in C#: for each theme, a list of *(model, band, count, scale range,
  height role)* rules plus fixed hero pieces (for example, a north crypt façade centred on the
  north wall).
- **Determinism.** Placement is deterministic per (theme, seed), so every run looks identical.
- **No Unity objects.** The recipes are pure data, so they are EditMode-testable.

### 3.3 Backing mass (built in code)
- **Shape.** Behind the kit props sits a continuous **wall ribbon**: an extruded, vertex-jittered
  strip along each side. It follows the §1 height grade and carries a painted rock, hedge or
  masonry texture.
- **Why it exists.** It guarantees the no-void property however the props happen to scatter.
- **Morph.** Stages 0–2 share the same 48×36 bounds, so the ribbon keeps **one geometry**.
  During a morph, its texture cross-fades through `_Morph` rather than dissolving. The silhouette
  stays solid, so no holes open onto the void.

### 3.4 Cover props (gameplay pillars)
Billboard sprites become 3D kit models, **scaled so their footprint fills the collision rect**
(x/z) with a height per kind:

| DecayPropKind | Model | Rubble when crumbled |
|---|---|---|
| Tomb | Kenney `crypt-small` / `gravestone-wide` | `gravestone-debris` |
| Urn | Kenney `urn-round` | `debris` |
| Column | Kenney `column-large` | `debris` |
| Rock | Quaternius `Rock_Moss_*` | Quaternius `Rock_*` (small) |
| DeadTree | Quaternius `CommonTree_Dead_*` | `trunk` |
| Obelisk | Kenney `pillar-obelisk` (+ the existing rune crown) | `debris` |
| Crystal | Code-built low-poly crystal cluster with emissive glow (neither kit has one) | shards |
| Ruin / other | Kenney `stone-wall-damaged` | `debris` |

The decay dissolve, retiring, reveal and occlusion keep using the same property-block calls.

### 3.5 Per-theme dressing

| Theme | North (hero) | East / west | South (≤1.2) | Lights |
|---|---|---|---|---|
| **Courtyard** (enc 1) | Ruined cloister: `stone-wall`, `column-large`, broken arches, dead `pine-crooked` behind | `stone-wall-damaged`, `brick-wall`, dead trees | `stone-wall-damaged` stubs, `iron-fence-border` | `fire-basket` braziers |
| **Graveyard** (enc 2) | `crypt-large`, `crypt-a/b`, `Willow_Dead_*`, `pine-crooked` | `iron-fence` runs, gravestone rows, `CommonTree_Dead_*`, `lightpost` | `iron-fence-damaged`, `grave-border` | `lantern-candle`, `lightpost` |
| **Cave** (enc 3) | Tall cliff ribbon with large `Rock_*` boulders | Cliff ribbon, stalagmite rocks | Low rock rim | Code-built crystals (cold blue), 2–3 torches |
| **Sanctum** (boss) | `altar-stone`, `column-large` colonnade, `crypt-large-door` gate | Columns, `pillar-obelisk`, `candle-multiple` | `border-pillar`, `grave-border` | `fire-basket`, candles (fade in with the reveal) |

**Inside the play rect,** only flat floor detail and non-blocking pieces under **0.3 units**
(tufts, candles at wall feet) are allowed. Anything taller would look like cover that does not
collide.

**Excluded from all recipes:** pumpkins, hay, the Kenney characters, and the snow, autumn,
cactus and palm variants.

---

## 4. Integration (DRAFT, needs review)

### 4.1 Public API stays fixed
`WorldGeometry`'s public API is unchanged:
- `CurrentTheme`, `Stone`, `FloorTexture`, `FloorFrom`
- `RenderCover`, `RenderRetiring`, `BeginMorphFrom`, `RenderMorph`, `RenderSceneryFormation`
- `RenderReveal`, `FinishReveal`, `RenderActorOcclusion`
- `FlameAnchor`, `SetTheme`, `SetVisible`, `Dispose`

`WorldPresentation` changes only in these ways:
- `WorldBackdrop` is removed, along with its `Render`/`SetReveal`/`Dispose` calls.
- `ApplyTheme` gets the lighting rig.

### 4.2 Splitting `WorldGeometry.cs`
The 361-line file is split by responsibility:

| Unit | Responsibility |
|---|---|
| `WorldGeometry` | Orchestration and the effect API (as today) |
| `PaintedFloor` | Floor chunks, outer ground, snapshot short-circuit |
| `ArenaEnclosure` | Backing ribbon and band placement from recipes |
| `ArenaDressing` | Recipes and deterministic placement (pure, no Unity objects) |
| `WorldModelLibrary` | Loads FBX prefabs from Resources, assigns PaintedWorld materials, caches them |
| `CoverModels` | DecayPropKind → model, fit to the collision rect |

### 4.3 Removed
- `WorldBackdrop.cs`
- `WorldPixelSurfaces.cs`
- `WorldBoundaryPolicy.cs`: its "consumed walls" height variation is superseded by the §1 grade.
  Its test is replaced by the ring tests.

### 4.4 Travel and failure handling
- **Travel.** The camera rises 2 units at the departure point before the opaque curtain cuts to
  the Sanctum. The no-void test includes the risen camera.
- **Missing model.** If a model fails to load, `Debug.LogError` names it and that placement is
  skipped. A test (§5) fails if any recipe model is missing, so this never ships silently.
- **Performance.** A few hundred low-poly renderers per arena, plus property blocks (which
  already exist today and opt out of the SRP Batcher).
  - **Target:** 60 fps on the PC quality level during the gate run.
  - **Mobile:** not measured in this work (TBD below).

---

## 5. Testing (DRAFT, needs review)

**EditMode**
- **`ArenaDressing` placement**
  - Deterministic for a given seed.
  - Nothing taller than 0.3 units inside any play rect except cover.
  - The south band stays at or under 1.2 units; the north band reaches at least 6.
  - Every recipe model name resolves in `Resources/WorldModels`.
- **`CoverModels` fit.** Every DecayPropKind's model footprint lies within its collision rect,
  and covers at least 80% of it.
- **Light budget.** No floor chunk receives more than 4 point lights for any theme's recipe.

**PlayMode**
- **`NoVoidVisible`**
  - **Setup:** all 4 themes × 4 clamped camera extremes, plus the risen travel camera.
  - **Rays:** a 16×9 grid at 16:9 and a 21×9 grid at 21:9.
  - **Pass:** every ray hits floor, enclosure or a prop before 60 units. A ray that reaches the
    fog fails.
- **Morph hitch.** `BeginMorphFrom` completes in under 50 ms when the outgoing morph was complete.
- **Existing tests.** All existing `WorldPresentationPlayModeTests`, `WorldArenaLayoutTests` and
  the full suite stay green.

**Visual sign-off.** The gate captures one screenshot per arena, and the owner reviews them in
the visual companion before the work is called done.

---

## Open questions (TBD)
1. **Mobile.** Is mobile a release target for this build? If yes, a mobile performance budget
   and a device to test on are needed. Shadows and fire lights may need a cheaper path on the
   `Mobile` quality level.
2. **Committing the kit models.** Do you want the CC0 kit models committed, as §3.1 proposes, or
   kept local like WorldArt with an importer? Committing them makes a fresh checkout look right;
   keeping them local keeps the repo lighter (size unmeasured, estimated a few MB for the subset).
