# Borrowed Hex — Build Handoff

State as of 3 Oct 2026, after Phase 13. Plan: `Docs/GAME_PLAN.md`. Rulings: `Docs/DECISIONS.md` (D1–D85).
Evidence: `Docs/TEST_EVIDENCE.md`.

## Builds

| Target | Location | How it was made | Verified |
|---|---|---|---|
| Windows (release) | `Builds/Windows/BorrowedHex.exe` | `unity command build --target StandaloneWindows64` (no Development flag) | Built, 0 errors; boots headless (null graphics) through both scenes with no exception logged |
| Web (release) | `Builds/Web/` (`index.html`) | `unity command build --target WebGL`, uncompressed | Served over HTTP on 127.0.0.1; menu renders without dev buttons; an endless run starts, spawns and counts down; no console errors |

- Product name **Borrowed Hex**, version **0.1.0**, company BorrowedHex (ProjectSettings).
- `Builds/` is git-ignored: the builds exist only on this machine.
- Menu entries (D85): `Borrowed Hex/Build/Windows` and `/Web` are release builds. `Windows (development)` and
  `Web (development)` write to `Builds/WindowsDev/` and `Builds/WebDev/` and keep the dev-only tools.
- Serving the Web build: any static server works because the build is uncompressed, e.g.
  `python -m http.server 8765` inside `Builds/Web`. Opening `index.html` from disk does not work (browsers block it).
- Observed: in the Web build the run's clock stayed at 0:30 until the canvas was clicked once. Inferred cause: the
  page had no focus, so the focus-loss pause held the run. Click the canvas before playing.

## Save data

- Windows and the editor: `profile.json` plus the backup `profile.bak.json` under Unity's `persistentDataPath`
  (normally `%USERPROFILE%\AppData\LocalLow\BorrowedHex\Borrowed Hex\`).
- Web: PlayerPrefs (browser storage for that origin, so a different port means a different profile).
- Recovery from a corrupted profile (via the backup) is covered by `ProfileTests`.

## Controls

| Action | Binding |
|---|---|
| Move | WASD |
| Aim | Mouse |
| Catch / parry | Left mouse |
| Fire selected packet | Right mouse |
| Swap slot | Q |
| Dash | Space |
| Pause | Escape, or P (browser-safe) |
| Menus | Mouse; arrow keys and Enter |
| Play again (results screen) | R (D97) |

## Modes and debug tools

- **Play**: the short run (encounters, choices, the Collector).
- **Tutorial**: six lessons (move, dash, capture, two slots, parry, evolution); the clock is frozen, enemies only
  evolve in the last lesson, and nothing is saved (D86, D87).
- **Endless**: 30 s waves, a choice every two waves, the boss every six, Retire from any choice (D84).
- **Practice sandbox**: never submits rewards.
- Dev-only, shown in the editor and development builds only (`Application.isEditor || Debug.isDebugBuild`):
  **Endless (debug)** and **Skip wave**. Any run that uses them is marked Debug and never saved.

## Tests

- EditMode: **410/410** (live editor, `bash .superpowers/rt.sh editor`).
- PlayMode: **32/32** (`bash .superpowers/rtp.sh`).
- `Tests/EditMode/IntegrationTests.cs` soaks 18 whole runs: {short, endless} × {Snatcher, Collector, Daredevil} ×
  {fresh, advanced loadout, assisted}, through random pauses and focus losses, checking the enemy cap, life bounds,
  the endless projectile budget and clock freezes every tick, then finalizing each run once.

## Known issues and TBDs

- **Open questions — rework placeholders needing a play pass** (`Docs/REWORK_PLAN.md`, D89-D102). None of these was
  set by play:
  - Upgrade prices: 15% / 25% / 40% / 50% of current life by cards held (`upgrades.takeCostByHeld`), and whether four
    held upgrades is too strong (D96).
  - Lifesteal rates: Leech 0.10, Siphon +0.10, Blood Debt +0.15 life seconds per damage, and whether a pop on every
    hit crowds busy fights (D99).
  - Overcharge score 20 and chain score {0, 5, 10, 15}, both times the combo (D102).
  - XP per score: 1 XP per 50 points, uncapped (D102). In the soak, an assisted 20-minute endless run earned about
    2,580 XP; reaching mastery 13 takes 4,500 in total. That is bot evidence: is that rate acceptable?
  - Also untuned: priming 0.4 s (D90), the Overcharge curve and zone (D93), freeze/shake strength (D94), chain
    window and bonuses (D95).

- **TBD — tuning.** No value has been tuned from real sessions. The soak bot is far weaker than a person
  (it never reaches a boss unassisted), so its run lengths say nothing about balance. Question for Rachit: who
  plays the tuning sessions, and which numbers matter first (start clock, kill gain, backfire cost, overstay)?
- **TBD — performance.** 60 FPS at 1080p on the target machine has not been measured. Question: which machine is
  the target, and is a Windows build on it enough, or is Web the target?
- **Not done — human playthroughs.** No full short run or full endless boss cycle has been played by hand in either
  build; the automated runs prove the flow, not the feel.
- **Layout.** At an 800×600 Web canvas the main-menu title overlaps its tagline. Larger windows not checked by eye.
- **Placeholders awaiting a decision:** the heart icon (swap for `Byog_Heart_Icon.png`?), a mouse-sensitivity
  setting (D74), skill-node names (D80), Daredevil's 1.0 catch radius (D83), and in D84 the 80-shot projectile
  budget, the encore fan as the repeated boss's variation, and survivors staying through an endless choice.
- **Untracked teammate files:** `Assets/Sprites.meta` and `Assets/Sprites/*.png.meta` were left uncommitted.
