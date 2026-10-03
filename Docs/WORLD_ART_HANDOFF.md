# Decaying world and 2.5D presentation

3 October 2026. The Arena scene now contains an independent `WorldPresentation`
observer and an opt-in world layout. Three larger environments morph in place as
the run progresses; the boss inhabits a separate Sanctum. Camera-facing pixel
sprites sit in solid 3D architecture under a closer following perspective camera.
Player/ordinary-enemy art, music and UI redesign remain outside this change.

## Local art and reproducible import

In the open Unity Editor, use **Borrowed Hex → Art → Import Local Downloads**.
The source folder is `%USERPROFILE%/Downloads/itch_downloads`, with the downloaded
archives already extracted. `WorldArtImporter.cs` contains the exact source paths
and prepares 43 selected textures. **Install World Presentation** adds the observer
to a clean, saved Arena scene; it does not rerun the project bootstrap.

The raw textures and their metadata are deliberately ignored under
`Assets/Game/Resources/WorldArt`. This repository is public, and several packs allow
distribution inside a game while prohibiting distribution as standalone assets.
The existing first-pass Web build includes the art. No Windows build was made.
A fresh checkout retains generated
geometry and the existing boss placeholder until its owner imports the downloads.

| Download | Selected content | License evidence / handling |
| --- | --- | --- |
| CreativeKind Necromancer | Seven animation rows: idle, run, three attacks, hurt, death | [Creator page](https://creativekind.itch.io/necromancer-free); keep the source sheet local, use it in the game |
| Szadi art World and Dungeon | Cracked ground, cave ground/walls, dead tree, rock, obelisk, tomb and damaged tomb states | `OpenWorldandcavedung_1.0/public-license.txt`: personal/commercial use and edits permitted; asset-pack sale prohibited |
| Anokolisa Graveyard | Sky, silhouettes, tiles, brush | `Graveyard scenery and backgrounds/Final/Social/Autor_note.txt`: commercial use permitted; credits optional, with a special request not to credit modified shapes/colours |
| Ruined Temple background | Distant ruined temple | `RTB_v1.0/_license.txt`: commercial games and modification permitted; asset resale prohibited |
| Pixel VFX Essentials | Seventeen selected fire, dust, ambient, slash, hit, magic and explosion sheets | Keep purchased/downloaded source local; original package remains in Downloads |
| CodeManu Free Pixel Effects | Vortex, Midnight, Casting | [Creator page](https://codemanu.itch.io/pixelart-effect-pack); downloaded README states public-domain use and 100×100 cells |

The importer uses point filtering, no mipmaps, uncompressed RGBA, a 4096 maximum
size and **NPOT scaling disabled**. The Necromancer must remain **2720×896**:
160×128 cells, row lengths **8 / 8 / 13 / 13 / 17 / 5 / 10**. Effects use 32×32 cells
except the three CodeManu sheets, which use 100×100. Trailing transparent padding
is trimmed while internal transparent frames retain their timing.

## What is wired

- Courtyard, graveyard and cave share a 48×36 outer footprint but have distinct
  cover layouts. At encounter boundaries (or each pair of Endless waves), the
  environment morphs over 24 gameplay seconds while combat continues. Player
  position, aim and packets stay in place; ordinary transitions add no pause.
- Old cover retires through gradual pixel coverage, incoming artwork materializes
  and becomes solid only when its footprint is clear of the player, enemies and
  retiring cover. Interrupted transitions cannot leave abandoned collision behind.
- Floors, walls and backgrounds overlap different downloaded atlas textures in
  irregular patches with sparse shifting glitch bands; Reduce Flashes removes
  those bands. Cover never scales or plays a shatter animation. Interrupted morphs
  preserve the currently displayed texture and coverage. Pausing freezes morphs.
- Courtyard walls enclose encounter 1, breached graveyard walls expose the scenery
  in encounter 2, and only low wall remnants remain in encounter 3. Backgrounds
  morph from the graveyard skyline into the darker ruined temple. Masonry and
  ground use native pixels from the Graveyard, World/Dungeon and Temple packs.
- The 40×34 Sanctum is physically separate. A 7.8-second entrance lifts the player
  vertically, relocates under a dark curtain, and lights its eight retained pillars
  one by one. The Collector and boss title appear after all eight lights; combat
  resumes after the title hold. WorldTransition holds combat, timer and life drain.
  Manual/menu/focus pauses also hold this cinematic. Endless return uses the same
  safe route; restart releases the abandoned pause and effects.
- The perspective camera points down 25°, uses a 40° field of view and an 18-unit
  offset, follows smoothly with forward framing, and clamps near arena edges.
  Continuous textured terrain covers the frustum and route between spaces.
- Pixel terrain, foundations, coping and enclosing architecture are real meshes.
  Ordinary cover uses trimmed, full-size downloaded sprites: three types per arena.
  Eight courtyard props, twelve graveyard tomb/tree/urn props, ten wasteland
  rock/crystal/tree props and eight textured Sanctum obelisks replace placeholders.
  Generated scenery adds no physical colliders: analytic simulation rectangles
  remain authoritative for movement, spawning, shots and boss sight.
- Necromancer clips use the lowest opaque pixel across each animation row as the
  grounded pivot, preventing the robe from clipping below the floor. Clips follow
  the Collector's existing pattern/stage, with hurt,
  death, teleport departure/arrival, ranged casts, sweep and slam effects.
- Cover directly between the camera and the player/Collector uses a pixel cutout
  to keep actors readable at 25°. Geometry, collision and decay remain intact.
- Braziers use animated fire and restrained drifting fog. Cover leaves static
  remains; collapse dust and rock bursts have been removed. Additional sheets can extend effects;
  importing a sheet does not mean every animation in its pack is already used.
- Gameplay animations freeze with simulation time. Only death flourishes advance
  during results; manual/menu/focus pauses freeze those too. Pools are bounded,
  restarts release old listeners/effects, and component removal restores the boss,
  camera, lighting and original arena renderers before disposing generated assets.

The camera framing and lighting are applied at runtime and restored on removal.
The original placeholder arena is disabled as a group while this presentation is
active. World mode is enabled on the scene's GameRoot; tutorial and default legacy
simulation setups retain their existing layout. Shared GameConfig assets are not
mutated by selecting a stage.
Source camera/render/player settings are not rewritten. Mesh shadows remain off
to preserve the project's existing WebGL shadow-sampler workaround.

Owner's build preference: show the work before starting any future build. The
already-started Web build may remain; no Windows build is authorized at this stage.

## Validation

Unity 6000.3.25f1, connected Editor via Unity CLI:

- Compilation completed without errors.
- EditMode: **407/407 passed**, including five pixel revision tests covering camera
  angle, varied props, structural decay, ordered reveal and every boss clip's grounding.
- PlayMode: the full suite checks follow/frustum, native floor/wall texture overlap,
  interrupted formation, vertical Sanctum travel and return, pause, restart,
  component cleanup, missing art, terminal effects and the complete real boss intro.
  The intro check confirms eight sequential lights/title ordering and unchanged
  simulation time and health throughout the cinematic. The full suite passed
  **32/32**; focused checks rerun after the last visual corrections also passed.
- Scoped review corrected retirement from partially faded cover, overlay cleanup
  before run binding, and preservation of the departure horizon during the rise.
- Camera-only Editor previews: `Temp/pixel-courtyard.png`,
  `Temp/pixel-graveyard.png`, `Temp/pixel-wasteland.png`, `Temp/pixel-sanctum.png`. These exclude
  the screen-space overlay UI.
- Earlier first-pass output: `Builds/ArtIntegration/Web` (0 build errors, one
  expected Pipeline-runtime-disabled warning; browser console clean). This output
  **does not contain the redesign**. No additional player build was started.
- SHA256 comparison: all **41** files protected at the start of this revision
  unchanged, including Claude's pending mechanics, UI, tests and project settings.

The downloaded kit remains locally imported and sprite replacements can use the
existing view interfaces. A new WebGL performance measurement is still needed;
Editor correctness and the prior Web build do not establish redesigned WebGL
frame rate. Raw art is excluded from Git. The implementation is committed on main
without pushing, per the owner's latest instruction.
