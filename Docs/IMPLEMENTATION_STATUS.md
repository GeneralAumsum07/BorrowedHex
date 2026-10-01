# Borrowed Hex — Implementation Status

Plan: `Docs/GAME_PLAN.md` (copy of the shared brief). Decisions: `Docs/DECISIONS.md`.
Test evidence: `Docs/TEST_EVIDENCE.md`.

## Environment (verified 2 Oct 2026)

- Editor: Unity `6000.3.25f1` (Web Build Support installed), project at the repo root.
- Unity CLI `1.0.0-beta.8`; live editor driven through `com.unity.pipeline` `0.8.0-exp.1`
  (`unity command recompile / run_tests / eval / build`).
- Packages of note: URP `17.3.0`, Input System `1.20.0` (Input System only), uGUI `2.0.0`,
  Test Framework `1.6.0`.

## Phase log

### Phase 0 — Project and repeatable CLI foundation — done

- Assemblies: `BorrowedHex` (runtime), `BorrowedHex.Editor`, `BorrowedHex.Tests.EditMode`,
  `BorrowedHex.Tests.PlayMode`.
- Core: `GameplayClock` (multi-reason pause), `IdGenerator`, `RunIdFactory`, `SeededRandom`,
  `GameplayScheduler`, `Geometry2D` (swept XZ collision).
- `ProjectBootstrap.Run(resetConfig)` builds `Bootstrap.unity` + `Arena.unity`, the config
  asset, base materials, build scene list, and player settings. Verified idempotent: two
  runs → one camera, one light, one arena (floor + 4 walls + 4 pillars), one GameRoot.
- `BuildGame.BuildWindows/BuildWeb` entry points; builds also run through the live
  `unity command build`.
- Windows build: succeeded, launched outside the editor, no errors in the player log.
- Web build: succeeded and loaded over HTTP, but rendered blank (URP Lit shadow sampler
  error on WebGL) → ruling D6. Re-verification scheduled with the next Web build.

### Phase 1 — Player movement, aiming, dash, health — done

- Sim: `PlayerActor`/`PlayerCommand`, `PlayerMotor` (normalised camera-relative move, axis
  slide against walls, swept dash that stops at walls, dash cooldown + i-frames capped to the
  dash duration), `PlayerStats` resolved once per run, `ArenaSim.DamagePlayer` as the single
  damage entry point (post-hit invulnerability; death raised once), `SimEvents`.
- Input: `PlayerInputReader` builds Gameplay (WASD, pointer aim, LMB catch, Space dash) and
  UI (point/click/scroll/navigate/submit, Esc/P pause) maps in code; presses are latched and
  consumed once per fixed step; a press starting over UI is never a catch. `AimResolver`
  projects the cursor to y=0 and fails (keeping the last aim) outside the window.
- Presentation: `GameRoot` steps the sim at a fixed 60 Hz (frame clamp 0.1 s, accumulator
  dropped while paused); `ArenaView` interpolates between steps; `CharacterView` is a
  camera-facing pixel sprite on a ground-anchored root with a blob shadow; `PixelSprites`
  generates placeholder art for every actor kind.
- UI: code-built uGUI kit (`Ui`), `GameplayHud` (health pips, dash cooldown bar, run clock,
  pause button, dev reset), `PauseMenu` (resume / restart / quit on desktop).
- File naming deviates from the plan's proposal (no separate `DashController`/`PlayerHealth`
  MonoBehaviours) — see D9.

### Phase 2 — Incoming projectiles and one enemy source — done

- Data: `CombatTuning` (attack table: bolt, lantern bolt, rocket; acolyte; lantern; spawn
  warning and minimum spawn distance) on `GameConfig`.
- Combat: immutable `AttackDefinition` + per-run `AttackCatalog`; value-type `AttackSnapshot`
  (provenance: source actor, shot ID, spread offset, perfect flag) and `DamageEvent`;
  pooled `ProjectileActor` with a full `Reset()`; `ProjectilePool`.
- `ArenaSim.Projectiles`: single resolution path. Each tick sweeps the travelled segment;
  earliest contact wins, ties broken capture < wall < actor. Hostile shots hit only the
  player (and pass through while invulnerable), returned shots only enemies (pierce with a
  per-projectile hit set). Death clears projectiles and cancels scheduled work.
- Enemies: `EnemyActor`, `BoltAcolyte` brain (distance band + strafe, stop-and-aim telegraph
  with aim lock, three-bolt volley at −8/0/+8°), spawn warning (harmless and immune),
  seeded spawn-point search away from the player, pairwise separation; `AttackEmitter` is the
  single hostile firing path; lantern fires a ±10° pair of slow bolts on demand.
- Presentation: enemy sprites with spawn-warning ring and ground aim-line telegraph,
  projectile glows sized from the logical radius with ground shadows, lantern prop. Sandbox
  keeps one practice acolyte alive; dev buttons spawn an acolyte / fire the lantern.
