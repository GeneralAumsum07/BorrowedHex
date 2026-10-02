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

### Phase 6 — Borrowed time — in progress (2 Oct 2026)

- First chunk implemented: selected-first catches, frozen unselected packets, power from
  selected time, expiry backfire, clock as health, capped kill-time rewards, seconds-based
  damage, clock/packet HUD and floating time feedback; summary statistics (D57–D60).
- Regression tests migrated from hearts/automatic return to seconds/manual fire. The
  177 existing and new packet/clock EditMode checks pass. Five separately authored next-chunk
  tests currently fail for missing per-enemy returns, pillar decay and overstay, as expected.
- Remaining: per-enemy hexes, temporary arena, evolved views, complete bot/build validation,
  and the owner's short-run playtest gate. Do not start Phase 7 before that gate.

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


### Phase 3 — Catch, carry, and three-second return — done (human gate pending)

- `CapturedPacket` / `PacketStore`: packets with fixed expiry (`CapturedAt + lifetime`),
  capacity in units, payload snapshots; a store of N independent slots that expires packets
  with a 1e-9 tolerance so accumulated 1/60 steps still release on the intended tick.
- `CaptureController`: one directional window per activation, then recovery. First success
  creates the packet; later successes in the same window append within capacity. Rejections:
  not eligible (returned / echo / non-capturable), window closed, packet full, slots full.
  An empty activation costs recovery but no slot.
- `CaptureGeometry`: a shot is catchable only while inside the live aim cone and range and
  approaching the player. The resolver samples each tick's swept segment (≤0.1 units, plus
  the exact player-impact time) so capture competes in the same earliest-contact ordering as
  walls and actors; a rejected shot keeps flying and re-resolves without capture.
- `ArenaSim.Capture` + `ReleaseService`: expired packets release at step 3 at the current
  player position and aim (aim is updated first), each payload keeping its spread offset and
  original source actor; one root release ID per packet. Death cancels packets and releases.
- Presentation: cone sector shown only while the window is open, orbit dots per stored shot
  (separate ring and spin per packet, blinking in the last 0.5 s), capture/reject/release
  pops; HUD `PacketIndicator` with one shrinking countdown bar and `Kind n/12 t` label per
  slot and a catch-readiness bar.


### Phase 4 — Enemy roster and distinct borrowed weapons — done (human gate pending)

- Data: one `EnemyTuning` shape for every ordinary enemy (`acolyte`, `pursuer`, `scatter`,
  `siege` on `CombatTuning`, looked up with `For(category)`), including kill values.
- Brains: `RangedCaster` (Acolyte, Scatter Caster with repositioning, Siege Familiar with a
  rocket) and `Pursuer` (seek, telegraphed strike circle resolved once at the end of the
  wind-up, recover). Shared `EnemySteering` (band keeping, seek, reposition target).
- `ExplosionResolver`: rockets burst on their first wall or actor contact. Returned bursts
  damage each active enemy in radius once and never the player; hostile rockets deal one
  player hit and burst visually only.
- Enemy bookkeeping: `Killed` flag and a single kill event per enemy; `DespawnEnemy` raises
  `EnemyDespawned`, never a kill; `KillValue` with the 1.5x elite factor.
- Lantern starvation: enemies alive but no ranged enemy, no non-lantern hostile shot and no
  stored packet for 2 s, then a pair every 2 s until it ends.
- `EnemySpawnService`: seven authored formations (one deliberately melee-only); the sandbox
  cycles them; dev button "+ Formation".
- Presentation: per-shot telegraph lines (a fan shows all five), heavier orange rocket
  telegraph, Pursuer strike disc, rocket colour, burst ring at the true damage radius.


### Post-Phase 4 playtest changes (owner direction) — done (human gate pending)

- Arcane lantern removed entirely: sim state, starvation tick, attack definition, sprite,
  view, dev button, tests and the config field.
- Parry (`ArenaSim.Parry.cs`, called from `Pursuer`): catch window open + attacker inside the
  capture cone + a strike that would hit → no damage, a riposte (`AttackKind.Riposte`,
  returned, 2 damage, pierce 1, not capturable, no packet energy) flies from the player at the
  attacker; `StrikeParried` event; a surviving attacker is staggered for a full cooldown.
  Presentation: gold riposte, gold ring closing on the strike circle, attacker flash.
- Playtest controls (sandbox/debug only): `+ Acolyte`, `+ Pursuer`, `+ Scatter`, `+ Siege`
  (`ArenaSim.SummonEnemy`), `Auto-spawn: ON/OFF` (`ArenaSim.AutoSpawn`, remembered across
  Reset), `Clear arena` (`ArenaSim.ClearArena`: despawn, never kills; hostile shots removed;
  stored packets kept).


### Post-Phase 4 pace changes (owner direction) — done (human gate pending)

- Fixed packet slots: each packet keeps one slot index from capture until release; a new
  packet takes the lowest free slot; a held slot never receives later catches.
- Right mouse fires the selected packet early (no penalty); Q cycles the selection; an empty
  selected slot does nothing (D33 revised after playtest; the fallback is gone). Release precedes catch in the tick,
  so the freed slot is usable on the same tick.
- HUD: panels pinned to slots, full contents ("Rocket x1  Bolt x2"), gold outline and ">" on
  the selected slot, control hint line. Orbit rings keyed by slot; selected dots larger.
- `PlayerCommand.With*` builders no longer mutate the receiver.


### Second playtest fixes (owner direction) — done (human gate pending)

- Bug: pursuers stuck behind pillars. Enemies now steer by a per-tick shortest path over
  padded pillar corners whenever the straight line is blocked (`EnemySteering.Waypoint`, D37):
  pursuer seek, a ranged enemy closing in, and Scatter repositioning.
- Bug: right mouse on an empty selected slot fired the other slot. The selection is now
  binding; an empty selected slot does nothing (D33 revised).
- Parry made harder (D36): thin gold parry band inside the cone during the first half of the
  catch window only; thin gold rim on the Pursuer's strike circle during its wind-up; parry
  only when band and rim touch in that window, otherwise the strike lands anywhere inside
  the circle. New tuning: `capture.parryRingRadius` 1.15, `parryRingWidth` 0.2,
  `parryWindowScale` 0.5; `pursuer.strikeEdgeWidth` 0.15.
- Open owner question (D36): should a band touching the strike's NEAR rim from just outside
  the strike count as a parry? It currently does.

### Phase 5 — Complete short run, boss, statistics, score — done (human gate pending)

- Run states Ready, Combat, UpgradeChoice, BossIntro, BossCombat, Paused, Results, with
  pause restoring the prior state (`ArenaSim.Run.cs`). Transitions at 40/80/120 s of active
  time, boss window to 180 s, terminal order death > victory > time expiry (D45).
- Encounter director: per-encounter formation pools, a signature formation first, seeded
  picks, 12-enemy cap with a bounded queue (D44).
- The Collector (`CollectorBoss.cs`, D38/D39): 50 health; bolt stream, sweeping melee, fan
  volley, ground slam. Name banner on entry (D41); HUD boss health bar.
- Score and combo (`RunScore.cs`): kill value x multiplier, +0.25 per release's first hit up
  to 3.0, 5 s timer on gameplay time, damage resets it; riposte counts (D40). Victory bonus
  +2 per unused second. Frozen `RunSummary` drives the results panel.
- HUD: countdown clock, objective line, score with multiplier. Upgrade transitions show a
  Continue panel until Phase 6 (D42). Short run is the default; sandbox switch in the pause
  menu and on results (D43).
- Not done here: upgrade cards (Phase 6), saving the summary (Phase 7).

### Post-Phase 5 playtest changes (owner direction) — done (human gate pending)

- Pursuer parry rim: short window just after the wind-up starts, gone before the strike;
  the rim is the parry window (D46).
- Health: 5 hearts in half-heart units; ordinary hits 1 heart, boss hits 2, contact 0.5/1
  heart with a 0.5 s blink (D51). HUD pips fill by halves; results show hearts.
- Short run: kill-all encounters with a pre-drawn formation plan (4/5/5), shared 3:00 clock,
  objective "KILL ALL ENEMIES (N LEFT)" (D50).
- Collector: position-based pattern choice with a melee cap (D48), parryable sweep through a
  gold arc and unparryable slam (D47), teleport behind a far player, faster movement and
  attacks (D49).
