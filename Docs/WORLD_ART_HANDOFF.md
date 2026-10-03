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
and prepares 36 selected textures. **Install World Presentation** adds the observer
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
- Old cover breaks on staggered deadlines, incoming cover grows from fragments
  and becomes solid only when its footprint is clear of the player, enemies and
  retiring cover. Interrupted transitions cannot leave abandoned collision behind.
- Floor textures transform in irregular patches with sparse shifting glitch bands;
  Reduce Flashes removes those bands. Floor tint, sunlight, fog and surrounding
  scenery change gradually. Pausing freezes the transformation.
- The 40×34 Sanctum is physically separate. A four-second vortex and rising pull
  carry the player/camera there while WorldTransition holds combat and life time.
  Manual/menu/focus pauses also hold this cinematic. Endless return uses the same
  safe route; restart releases the abandoned pause and effects.
- The perspective camera points down 30°, uses a 40° field of view and an 18-unit
  offset, follows smoothly with forward framing, and clamps near arena edges.
  Continuous textured terrain covers the frustum and route between spaces.
- Weathered pavers, cracked earth, foundations, coping, buttresses, stairs and
  cover are real meshes. Eight courtyard props, twelve graveyard props, ten cave
  rocks and eight Sanctum obelisks replace the placeholder four-column layout in
  world mode. Tombs, ruined walls, trees, rocks and obelisks show wear and rubble.
  Generated scenery adds no physical colliders: analytic simulation rectangles
  remain authoritative for movement, spawning, shots and boss sight.
- Necromancer clips follow the Collector's existing pattern/stage, with hurt,
  death, teleport departure/arrival, ranged casts, sweep and slam effects.
- Braziers use animated fire and restrained drifting fog. Collapse produces dust
  and rock bursts. Additional imported sheets are available to extend effects;
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
- EditMode: **346/350 passed**; all six world-layout/morph tests and all thirteen
  art-policy tests passed.
- PlayMode: **22/22 passed**, including follow/frustum, live morph,
  interrupted formation, Sanctum/return travel, pause, restart, component cleanup,
  missing local art, and terminal boss effects.
- Fresh scoped code review cleared after correcting occupied-cover recovery,
  Sanctum return, interpolation on landing and interrupted scenery continuity.
- Camera-only Editor previews: `Temp/redesign-courtyard.png` and
  `Temp/redesign-mid-morph.png` and `Temp/redesign-sanctum.png`. These exclude
  the screen-space overlay UI.
- Earlier first-pass output: `Builds/ArtIntegration/Web` (0 build errors, one
  expected Pipeline-runtime-disabled warning; browser console clean). This output
  **does not contain the redesign**. No additional player build was started.
- SHA256 comparison: all fifteen pre-redesign protected files unchanged. Earlier
  externally edited Lore was preserved and separately backed up, not reverted.

The protected unfinished tutorial tests currently have four failures: the scripted
full playthrough, evolution lesson, two-parry lesson and pocketing/Q lesson. They
exercise the plain simulation without this presentation layer. Their files and
the other protected work were left untouched.

The downloaded kit remains locally imported and sprite replacements can use the
existing view interfaces. A new WebGL performance measurement is still needed;
Editor correctness and the prior Web build do not establish redesigned WebGL
frame rate. Raw art is excluded from Git. The implementation is committed on main
without pushing, per the owner's latest instruction.
