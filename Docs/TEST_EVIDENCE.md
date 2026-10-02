# Borrowed Hex — Test Evidence

Actual outcomes only. Planned-but-unrun checks are not listed as passed.

## Phase 0

```text
Phase/task: Phase 0 — project and CLI foundation
Files changed: Assets/Game/** (assemblies, Core, Data, Presentation, Editor), Docs/**
Actual CLI commands:
  unity status / unity command recompile / unity command run_tests --mode editor
  unity command eval "BorrowedHex.EditorTools.ProjectBootstrap.Run(...)" (x2, idempotence)
  unity command build --target StandaloneWindows64 --outputPath Builds/Windows/BorrowedHex.exe
  unity command build --target WebGL --outputPath Builds/Web
Test report: EditMode CoreTests 10/10 passed
Manual behaviour observed:
  - Bootstrap twice → exactly one Main Camera, Sun, Arena (9 children), GameRoot
  - Windows build launched outside editor; Player.log has no errors
  - Web build served via python http.server on 127.0.0.1; page loads, canvas blank;
    console: GL_INVALID_OPERATION "Mismatch between texture format and sampler type"
Known issues: Web rendering fix (D6) not yet re-verified in a Web build
Next uncompleted task: Phase 1
```

## Phase 1

```text
Phase/task: Phase 1 — player movement, aiming, dash, health
Files changed: Scripts/Player/*, Scripts/Runs/*, Scripts/Data/PlayerTuning.cs,
  Scripts/Presentation/{GameRoot,ArenaView,CharacterView,PixelSprites}.cs, Scripts/UI/*,
  Tests/EditMode/PlayerTests.cs, Tests/PlayMode/GameRootPlayModeTests.cs
Actual CLI commands:
  unity command recompile; unity command run_tests --mode editor
  unity command run_tests --mode playmode --async_tests true (+ poll Temp/pipeline_test_status.json)
  unity command eval (enter play mode, drive sim steps); unity command capture_game_view
Test report:
  EditMode 20/20 passed (CoreTests 10, PlayerTests 10: diagonal speed, zero input, dash
    distance, dash stops at wall, cooldown, aim kept when projection fails, repeated hits
    during invulnerability cost 1, two lethal hits → one death, dash i-frame window, paused sim)
  PlayMode 2/2 passed: focus loss pauses combat (clock frozen; menu stays open on regain);
    click on HUD pause button opens the menu and is NOT a catch, click on arena IS a catch
Manual behaviour observed:
  - Camera capture in play mode: magician sprite stands on the floor with blob shadow and
    aim ring, after 40 driven steps at (3.83, -2.85); arena framing as Phase 0
  - Screen capture shows HUD (3 pips, dash bar, clock, Reset, Pause) and pause menu layout.
    Note: the unfocused editor's Game view did not repaint, so that capture is the first frame.
Known issues: Web rendering fix (D6) still awaiting a Web build; live keyboard/mouse feel
  not exercised by a human yet
Next uncompleted task: Phase 2
```

## Phase 2

```text
Phase/task: Phase 2 — incoming projectiles and one enemy source
Files changed: Scripts/Combat/*, Scripts/Enemies/*, Scripts/Data/CombatTuning.cs,
  Scripts/Runs/{ArenaSim.Combat,ArenaSim.Projectiles,ArenaSim.Enemies,SimEvents.Combat,RunSetup}.cs,
  Scripts/Presentation/{ArenaView,PixelSprites,GameRoot}.cs, Scripts/UI/GameplayHud.cs,
  Tests/EditMode/ProjectileCollisionTests.cs
Actual CLI commands: recompile; run_tests editor; run_tests playmode --async_tests;
  eval (play mode, drive sim to a locked telegraph); capture_game_view --source camera
Test report: EditMode 30/30 (ProjectileCollisionTests 10: fast shot crossing player in one
  step hits; pillar blocks later player impact; no damage after despawn; hostile ignores
  enemies; returned ignores player and keeps source/root-release provenance; pooled reuse is
  fully reset; pierce hits each enemy once; expiry; acolyte warning → telegraph → 3 bolts;
  death cancels scheduled work and clears shots). PlayMode 2/2.
Manual behaviour observed:
  - Live run left unattended: the acolyte telegraphed and killed a stationary player
  - Captures: locked red aim line from acolyte to player, lantern pair of amber bolts,
    projectile glows at true hitbox size; player sprite correctly occluded behind a pillar
Known issues: Web build not yet re-verified (D6); capture is still disabled (Phase 3)
Next uncompleted task: Phase 3
```


## Phase 3

```text
Phase/task: Phase 3 — catch, carry, and three-second return
Files changed: Scripts/Combat/{CapturedPacket,CaptureController,ProjectileActor}.cs,
  Scripts/Runs/{ArenaSim,ArenaSim.Capture,ArenaSim.Combat,ArenaSim.Projectiles,SimEvents.Combat}.cs,
  Scripts/Presentation/{ArenaView,PixelSprites}.cs, Scripts/UI/{GameplayHud,PacketIndicator}.cs,
  Tests/EditMode/CaptureTests.cs
Actual CLI commands: recompile; run_tests editor; run_tests playmode --async_tests;
  eval (play mode: drive the acolyte, catch its volley, advance to release; scripted
  catch loop; read HUD labels); capture_game_view --source camera / --source screen
Test report: EditMode 51/51 (CaptureRuleTests 8, CaptureIntegrationTests 13). PlayMode 2/2.
  Boundary vectors: t=1.0 packet not released at 3.99, released once at 4.0; append at 1.20
  keeps expiry 4.0; rocket cost 4 rejected at 10/12 and the packet is unchanged; shooter
  removed before release does not break release and attribution survives.
  Checks: same-tick capture prevents damage; exact capture/impact tie captures;
  uncaptured, out-of-cone and rear shots still hurt; two slots expire independently; ten
  paused seconds leave expiry untouched; returned shots are not recaptured; full-packet
  rejection leaves the shot flying and it hurts; death cancels packets and their releases;
  expiry frees a slot that a catch can use on the same tick.
  Mutation check: swapping the capture/actor tie order makes the tie test fail (expected 3 HP,
  was 2); restored code passes. This check also exposed a dead body-contact clause in
  CaptureGeometry, removed under D16.
Manual behaviour observed (play mode, scripted input, not a human):
  - Catch toward the acolyte took all 3 bolts into one packet at 3/3 HP; cone and 3 orbit
    dots drawn (shots/phase3_catch.png)
  - Release fired on the expiry tick exactly (expires 5.8167, released 5.8167) as 3 returned
    shots toward the current aim (shots/phase3_release.png); that volley hit a pillar
  - Scripted stationary catch loop: 6 catches, 16 shots captured, 0 damage taken, acolyte
    killed by 3 returned hits in 19.3 s; 8 of 11 returned shots ended on walls
  - HUD labels after one catch: "Bolt  1/12  2.5s", "empty", catch bar "..."
Known issues: screen captures stay stale while the Game view is unfocused (layout verified,
  live values read via eval); restart cancellation relies on the one-sim-per-run design
  (old sim and view are discarded), with no separate test; Web build still to re-verify (D6)
Human gate (not yet done): a person must confirm that repeated catches feel deliberate and
  worth repeating. The scripted loop suggests many returns are lost to pillars and strafing.
Next uncompleted task: Phase 4
```


## Phase 4

```text
Phase/task: Phase 4 — enemy roster and distinct borrowed weapons
Files changed: Scripts/Data/CombatTuning.cs; Scripts/Enemies/{EnemyActor,EnemySteering,
  RangedCaster,Pursuer,Lantern,EnemySpawnService}.cs (BoltAcolyte.cs removed, folded into
  RangedCaster); Scripts/Combat/ExplosionResolver.cs; Scripts/Runs/{ArenaSim.Enemies,
  ArenaSim.Projectiles,ArenaSim.Combat,SimEvents.Combat}.cs; Scripts/Presentation/
  {ArenaView,GameRoot}.cs; Tests/EditMode/Phase4Tests.cs
Actual CLI commands: recompile; run_tests editor; run_tests playmode --async_tests; eval
  (play mode: mixed formation telegraphs; catch and return a siege rocket);
  capture_game_view --source camera
Test report: EditMode 68/68 (AttackPayloadTests 7, EnemyEncounterTests 10). PlayMode 2/2.
  Checks: returned rocket damages enemies in radius but not a player standing in it; burst
  damages each actor once incl. the direct-hit target; rocket bursts on a pillar; hostile
  rocket = one player hit, no area damage; killing a shooter with its own snapshot keeps
  source attribution; returned fan keeps its five offsets exactly; captured rocket returns as
  a rocket costing 4; enemies never spawn within minSpawnDistance (40 seeds x all
  formations); spawn warning harmless and immune; pursuer hits a player who stays, misses one
  who leaves; scatter fires 5, siege fires 1 rocket; melee-only remainder makes the lantern
  fire at 2/4/6 s and stop when a ranged enemy appears; a stored packet postpones it;
  despawn is not a kill; overkill raises one kill; elite kill value 1.5x.
  Mutation checks: counting lantern bolts as ammunition fails the lantern test; giving
  hostile bursts area damage fails the hostile-rocket test. Restored code passes.
Manual behaviour observed (play mode, scripted input, not a human):
  - Scatter Caster telegraph shows all five aim lines; Pursuer strike disc on the player;
    Siege telegraph drawn as a heavier orange line (shots/phase4_tele.png)
  - Siege rocket caught (capacity 4), returned, burst for 5 damage and killed a pursuer
    (shots/phase4_rocket.png)
  - The burst ring itself was not visible in the capture: pops age on real frame time and had
    expired by capture time. Visual NOT verified.
Known issues: elites have kill value only, no behaviour modifier yet (Phase 11/12);
  "melee-only remainder is solvable" is covered at the rule level (lantern supply + capture
  tests), not by an end-to-end scripted kill
Human gate (not yet done): verify each enemy's attack gives a distinct tactical opportunity
Next uncompleted task: Phase 5
```


## Post-Phase 4: lantern removed, parry, playtest controls

```text
Phase/task: owner playtest feedback before Phase 5
Files changed: Scripts/Core/Enums.cs; Scripts/Data/{CombatTuning,GameConfig}.cs;
  Data/GameConfig.asset; Scripts/Enemies/{Pursuer,AttackEmitter,EnemySpawnService}.cs
  (Lantern.cs removed); Scripts/Runs/{ArenaSim,ArenaSim.Combat,ArenaSim.Enemies,
  SimEvents.Combat,RunSetup}.cs; Scripts/Runs/ArenaSim.Parry.cs (new);
  Scripts/Presentation/{ArenaView,PixelSprites,GameRoot}.cs; Scripts/UI/GameplayHud.cs;
  Tests/EditMode/ParryTests.cs (new); Tests/EditMode/Phase4Tests.cs (lantern tests removed)
Actual CLI commands: recompile; run_tests editor; run_tests playmode --async_tests; eval
  (play mode, dev buttons invoked); capture_game_view --source camera
Test report: EditMode 78/78 (ParryTests 8, SandboxControlTests 4; 2 lantern tests removed).
  PlayMode 2/2.
  Mutation checks: dropping the cone check fails FacingAway_TheStrikeStillHurts; dropping the
  would-hit check fails AStrikeThatWouldMiss_IsNotParried; blocking parry on full slots fails
  ParryWorks_EvenWithBothPacketSlotsFull. Restored code passes.
Manual behaviour observed (play mode, scripted input, not a human):
  - Dev buttons invoked by name: Auto-spawn toggled to OFF (label updated), Clear arena,
    + Pursuer summoned exactly one Pursuer
  - Aiming at it and pressing catch 0.1 s before the strike: 1 parry, health 3/3, Pursuer
    killed by the riposte (shots/parry.png: gold riposte leaving the cone; no lantern)
Known issues: parry has no dedicated sound or tutorial prompt yet (Phase 12 polish)
Human gate (not yet done): does parry timing feel fair, and does the Pack formation now read
  as "parry them" without being told?
Next uncompleted task: Phase 5
```


## Post-Phase 4: fixed slots, early release, slot cycling

```text
Phase/task: owner pace feedback before Phase 5
Files changed: Scripts/Player/{PlayerActor,PlayerInputReader}.cs; Scripts/Combat/
  {CapturedPacket,CaptureController}.cs; Scripts/Runs/{ArenaSim,ArenaSim.Capture}.cs;
  Scripts/UI/PacketIndicator.cs; Scripts/Presentation/ArenaView.cs;
  Tests/EditMode/SlotTests.cs (new); Docs/GAME_PLAN.md (controls table)
Actual CLI commands: recompile; run_tests editor; run_tests playmode --async_tests; eval
  (play mode); capture_game_view --source camera
Test report: EditMode 89/89 (SlotTests 11). PlayMode 2/2.
  Mutation checks: slot = packet count (old shifting) fails SlotsKeepTheirPosition; release
  after catch fails ReleaseFreesASlotForACatchOnTheSameTick; closing the window on early
  release fails ReleasingDuringItsOwnWindow; no empty-slot fallback fails
  RightClick_OnAnEmptySelectedSlot. Mutating WithCatch fails
  CommandBuilders_DoNotMutateTheReceiver. Restored code passes.
Manual behaviour observed (play mode, scripted commands, not a human):
  - Bound actions read back: Release=<Mouse>/rightButton, CycleSlot=<Keyboard>/q
  - HUD after 2 bolts then a rocket and Q: "1  Bolt x2 | 2/12 1.5s", "> 2  Rocket x1 | 4/12 2.2s";
    after right mouse: slot 2 "empty", slot 1 unchanged; the rocket flies east
    (shots/early_release.png: returned rocket, two unselected bolt dots)
  - The hint line and outline were read as component state; the overlay itself was not
    screenshotted (camera capture excludes UI)
Known issues: right-click context-menu suppression in the browser is inferred from the Web
  loader listing "contextmenu" among handled events; NOT verified in a Web build
Human gate (not yet done): does early release + Q make combat feel faster?
Next uncompleted task: Phase 5
```


```
Second playtest fixes: pillar routing, binding slot selection, harder parry
Actual CLI commands: recompile; run_tests editor; run_tests playmode --async_tests; eval
  (play mode); capture_game_view --source camera
Test report: EditMode 99/99. PlayMode 2/2.
  RED first: Pursuer_RoutesAroundAPillar (stuck at (-6.00, 4.50)),
  RangedEnemy_TooFar_RoutesAroundAPillarToo (stuck at (-6.00, 4.55)),
  RightClick_OnAnEmptySelectedSlot_FiresNothing (fired the other slot).
  Routing went through two failed designs before passing, each found by the same tests:
  a single best corner parked on the corner; a 0.9x sight radius let the planner cut a
  corner the full body could not fit (tick trace: stuck at (-6.985, 4.502)).
  Added Pursuer_ReachesThePlayer_FromSquareBehindEveryPillarFace (16 cases).
  Mutation checks: straight-line MoveToward fails both pursuer routing tests; parry gated on
  the full catch window fails PressedTooLateForTheParry; parry without the band check fails
  FacingAway and InsideTheStrike_BandsApart. ParryGeometry closed form agrees with dense
  arc sampling on >3000 random cases. Restored code passes.
Manual behaviour observed (play mode, scripted commands, not a human):
  - Pursuer wound up next to the player, catch pressed: gold parry band visible inside the
    cyan cone, touching the gold rim of the strike circle (shots/parry_band.png,
    shots/parry_band_zoom.png)
  - After the parry window, with the catch window still open: band hidden, cone shown
  - In that run the catch was pressed ~0.18 s before the strike, so the strike landed
    (hp 2/3, no parry): consistent with the 0.125 s parry window
  - Capture-harness note: Destroy is deferred while the editor is unfocused, so a scripted
    Restart briefly left the old ArenaView in the frame; removed by hand for the capture.
    Not a game bug (the next real frame destroys it)
Known issues: GameRoot.Update throws NullReferenceException every frame if scripts recompile
  during Play mode (Sim is lost on domain reload); editor-only, does not affect builds
Human gate (not yet done): is the parry now hard but learnable? Do pursuers still get stuck?
Next uncompleted task: Phase 5
```


## Phase 5: short run, Collector boss, score

```text
Phase/task: Phase 5 (owner changes: Collector without rockets, sweep + slam, 50 hp, banner)
Files changed: Scripts/Data/RunTuning.cs (new); Scripts/Enemies/{CollectorBoss (new),
  EnemyActor,EnemySpawnService}.cs; Scripts/Runs/{ArenaSim,ArenaSim.Combat,
  ArenaSim.Enemies}.cs, ArenaSim.Run.cs, RunScore.cs, SimEvents.Run.cs (new);
  Scripts/UI/{GameplayHud,PauseMenu}.cs, RunFlowPanels.cs (new);
  Scripts/Presentation/{ArenaView,GameRoot}.cs;
  Tests/EditMode/{RunLifecycleTests,ScoreTests,ShortRunTests}.cs (new)
Actual CLI commands: recompile; run_tests editor; run_tests playmode; eval (play mode);
  capture_game_view (screen and --source camera)
Test report: EditMode 129/129 (RunLifecycle 11, Score 10, ShortRun 9). PlayMode 2/2.
  Honest ordering note: this phase's tests were written AFTER the sim code, not RED first.
  To make up for that, mutation checks were run on the rules most likely to be wrong:
  victory checked before death fails the same-tick terminal-order test; `<=` on the enemy
  cap fails the cap test; removing the once-per-release combo rule fails the combo test.
  Restored code passes.
  Clock tests use a 1e-4 tolerance: 2400 steps of 1f/60f sum to 40.000002, so the tick
  count is asserted exactly and the time only approximately.
  Zero-passive boss check (ShortRunTests): a scripted bot that only catches and returns
  (no dash, no parry) beats the boss with 45.9 s of the 60 s window used, taking 13 hits.
  Health is set to 999 in that test so it measures offence only; the hits taken show the
  boss is still dangerous. With the first tuning the bot ended with the boss on 15/50 (D39).
Manual behaviour observed (play mode, scripted ticks, not a human):
  - Boss banner "- BOSS - / THE COLLECTOR / Defeat it before the time runs out" over the
    frozen arena; HUD shows 1:00, "DEFEAT THE COLLECTOR" and the boss bar (shots/p5-banner-ui.png)
  - All four telegraphs (shots/p5-patterns.png): stream aim line with bolts in flight;
    sweep wedge with the blade; nine fan aim lines; slam ring with the fill growing to it
  - Results after a scripted boss kill: VICTORY, score 364 = 250 kill + 114 bonus (57 unused
    seconds x 2), centred stats, Play again + Practice sandbox (shots/p5-results.png)
  - First results capture found two defects, both fixed: the boss banner stayed behind the
    results when the run ended inside its fade; the stats were left-aligned
  - The player sprite is missing in the boss shots because the idle scripted player was hit
    repeatedly and blinks while invulnerable; not a rendering fault
Not verified by eye: the upgrade Continue panel, the mode-switch buttons, a death or
  time-expiry results screen (all covered by sim tests, not screenshots)
Known issues: the editor stops rendering frames while unfocused, so scripted captures need
  EditorApplication.Step(); focus loss also opens the pause menu (D10) during scripted runs
Human gate (not yet done): is the 3x40 s + 60 s run paced well; is the Collector readable
  and beatable without upgrades; is the banner long enough?
Next uncompleted task: Phase 6
```

## Post-Phase 5: rim window, hearts, kill-all encounters, Collector by position

```text
Phase/task: owner playtest changes after Phase 5 (D46-D51)
Files changed: Scripts/Data/{CombatTuning,PlayerTuning,RunTuning}.cs; Data/GameConfig.asset
  (maxHealth 3 → 10); Scripts/Player/PlayerStats.cs; Scripts/Combat/ParryGeometry.cs;
  Scripts/Enemies/{AttackEmitter,CollectorBoss,EnemyActor,Pursuer}.cs;
  Scripts/Runs/{ArenaSim,ArenaSim.Enemies,ArenaSim.Parry,ArenaSim.Run,RunScore}.cs;
  Scripts/UI/{GameplayHud,RunFlowPanels}.cs; Scripts/Presentation/ArenaView.cs;
  Tests/EditMode/{CaptureTests,ParryTests,Phase4Tests,ProjectileCollisionTests,
  RunLifecycleTests,ShortRunTests}.cs
Actual CLI commands: recompile; run_tests editor; run_tests playmode; eval (play mode);
  capture_game_view
Test report: EditMode 153/153, PlayMode 2/2.
  Honest ordering note: the sim changes were written first; the 30 existing tests they broke
  were then updated, and the suite grew by a net 24 tests (rim timing incl. too-early/too-late presses,
  contact damage, half hearts incl. the shipped asset, kill-all flow and shared clock,
  pattern choice incl. line of sight and the melee cap, sweep parry and arc geometry, slam
  never parried, teleport behind/cooldown/never-when-close). Not RED first.
  Two of my own test assumptions were wrong and corrected, not the code: the parry band
  meets the gold arc over a range (~2.4-3.2 units), not at one distance; and the teleport
  wind-up lengthens the worst bolt drought to ~7.2 s (bound now computed from tuning).
  Zero-passive bot (offence only, 999 health): boss defeated 64.0 s into the fight; with
  instant encounter clears the run clock read 92.9 s at the win. The old bar (60 s boss
  window) would now FAIL by 4 s: the faster, teleporting boss is harder to hit.
Manual behaviour observed (play mode, scripted ticks, not a human):
  - HUD: 3.5 of 5 heart pips, "ENCOUNTER 1/3 — KILL ALL ENEMIES (10 LEFT)", 2:59 (shots/p6-hud.png)
  - Pursuer wind-up with the gold rim up, then 0.2 s later the same wind-up with no rim,
    0.13 s before the strike (shots/p6-rim-open.png, p6-rim-closed.png)
  - Collector sweep wind-up with the gold parry arc inside the wedge (shots/p6-sweep-arc.png)
  - Teleport wind-up: boss faded, purple arrival ring behind the player (shots/p6-teleport.png)
  - A player standing still in encounter 1 died in ~9 s to five one-heart hits (strikes and
    bolts); no contact hits in that log, since pursuers stop short of the body
Not verified by eye: the CLEARED upgrade title, the hearts wording on the results screen
Human gate (not yet done): is the rim window fair while the strike still tracks; is 3:00
  enough for three cleared encounters plus the boss; do 2-heart boss bolts feel right?
Next uncompleted task: Phase 6
```

## Post-Phase 5: Collector variety and movement (D52)

```text
Files changed: Scripts/Data/RunTuning.cs; Scripts/Enemies/CollectorBoss.cs;
  Tests/EditMode/ShortRunTests.cs
Test report: EditMode 160/160, PlayMode 2/2.
  New: Choose_SameAttackTwiceInARow_SwitchesToItsPartner;
  ThePlayerStandingStill_AtAnyRange_NeverSeesOneAttackThreeTimesInARow (1.5/3/6/9 units);
  RangedPatterns_StrafeToAFiringSpot_OffTheStraightLine; Tuning_TeleportFromSixUnits_...
  RED: first as a compile failure (new Choose overload and fields). Then, with the rule
  disabled (maxSameInARow 99), the range test failed with
  "FanVolley,FanVolley,FanVolley,FanVolley,FanVolley,..." at 6 units, the owner's bug; restored, green.
  The strafe test first failed on a spot where both swung spots overlapped pillars and the
  boss fell back to straight back; fixed by trying the swing range ends before giving up.
  Zero-passive bot: boss defeated 35.6 s into the fight (run clock 64.6 s), down from 64.0 s.
  Why it is faster is not established (inference: more fans/streams mean more bolts to capture).
Human gate: does the boss now feel varied and mobile enough?
```

## Post-Phase 5: more melee, melee after teleports (D53)

```text
Files changed: Scripts/Data/RunTuning.cs; Scripts/Enemies/CollectorBoss.cs;
  Tests/EditMode/ShortRunTests.cs
Test report: EditMode 162/162, PlayMode 2/2.
  RED first (behavioural, code unchanged): Choose_WiderMeleeBands_SlamTo2_4_SweepTo5 (2.3 units gave
  Sweep), TheAttackRightAfterATeleport_IsAlwaysMelee_AndBothKindsShowUp (every post-teleport
  attack was Sweep, never Slam), tuning 0.35 != 0.4. Then green.
  Changed expectation: the pillar test's in-sight control at 5 units is now Sweep, not FanVolley.
  First run attempt timed out: the PC was locked and the editor processed no commands.
  Zero-passive bot: 35.6 s into the boss fight, identical to before D53. Inference only: its
  distances and seed path may not touch the changed bands; not investigated.
```

## Post-Phase 5: faster boss and slam, slams in the open (D54)

```text
Files changed: Scripts/Data/RunTuning.cs; Scripts/Enemies/CollectorBoss.cs;
  Tests/EditMode/ShortRunTests.cs
Test report: EditMode 164/164, PlayMode 2/2.
  RED first: APlayerInSweepRange_InTheOpen_StillGetsSlammedSometimes saw 0 slams;
  Tuning_FasterBoss_AndFasterSlam saw 3.0. Then green (the test requires >= 15% of melee
  choices at 3.5 units to be slams, and fewer than all).
  Zero-passive bot: 37.8 s into the boss fight (run clock 66.7 s), was 35.6 s.
```

## Post-Phase 5: instant melee after teleport, speed 3.6, teleport 48% (D55)

```text
Files changed: Scripts/Data/RunTuning.cs; Scripts/Enemies/CollectorBoss.cs;
  Tests/EditMode/ShortRunTests.cs
Test report: EditMode 165/165, PlayMode 2/2.
  RED first: AfterATeleport_TheMeleeWindUpStartsOnArrival_NoWalkFirst (stage was Reposition on
  arrival); tuning tests saw 3.3 and 0.4. Then green.
  Zero-passive bot: 73.0 s into the boss fight (run clock 102.0 s), was 37.8 s. Not investigated
  (inference: more teleports and instant melee give the offence-only bot fewer free windows).
```

## Phase 6, first chunk: packet decay and the life clock (2 Oct 2026)

- Live Unity CLI recompile completed without compiler errors.
- New packet/clock tests: 11 cases; initial RED 8 failures/3 already-passing boundaries,
  then GREEN 11/11. Required selected-time boundary (catch 1, freeze 2-5, expiry 7),
  selected-first assignment, power 1.0/1.7, frozen power, fire at 2.99 versus backfire
  at 3.0 through immunity, 10-second damage, 298 + 3 capped at 300, Death versus TimeExpired.
- Full EditMode run: 182 cases, 177 passed. All five failures are the new, uncommitted
  TemporaryArenaTests for the next chunk (acolyte pierce, shotgun, heavy boss return,
  crumbling pillars, overstay); the 166 migrated baseline plus 11 packet/clock cases pass.
- Shipped GameConfig updated through live Editor eval and SaveAssets, preserving its
  existing arena, presentation and tuning. Unity serialized formerly implicit defaults.
- Human playtest and final build validation remain pending until the rest of Phase 6.
- PlayMode: 2/2 passed (focus-loss pause and UI click isolation).

## Phase 6, completed core (2 Oct 2026)

- EditMode: 189/189 passed; PlayMode: 2/2 passed. Live CLI recompile completed.
- The first five per-enemy/arena tests failed before implementation, then passed.
  Additional boundaries cover a real two-enemy piercing return, six-unit shotgun travel,
  source destruction before release, exact 72-second pillar crumble, pause, bolt impacts
  never wearing cover, restoration through the boss transition, elite kill time and boss
  exclusion from overstay. Existing packet-capacity rejection and lifecycle tests remain green.
- Restoring a pillar under the player initially failed the new overlap test. Restoration
  now moves the player to a clear side; the same regression passes.
- Independent source review found a missing ordinary pillar-wear event. The new wear test
  failed (expected four durability lost, observed zero event loss), then passed after
  reporting actual wear before crumble. Pauses and already-crumbled pillars emit no wear.
- Retained D45 ordering is pinned explicitly: final-tick boss defeat beats ordinary time
  expiry with zero remaining-time bonus; same-tick damage death still beats boss defeat.
- Updated zero-passive bot: victory 39.7 seconds into the boss fight, active run elapsed
  68.7 seconds, 24 packets fired, 18 packets hit, zero backfires. The fixture grants hit
  immunity and clears encounters instantly: it measures offense, not survival or a human
  full-run clear. Compared with the earlier D55 bot's 73.0 seconds, this is faster; the
  separate contributions of return identity, power and pillar decay were not isolated.
- Live PlayMode inspection (scripted, not human): large clock, both packet contents,
  power/countdowns and DECAYING/FROZEN labels; pillar durability 12/8/4/0 displayed intact,
  one crack, additional cracks and rubble. The evolved acolyte had a distinct pink horned
  outline. The taller panels initially overlapped their control hint; spacing was corrected.
- Shipped config serialized through the live Editor, including return rules, kill seconds,
  seeded pillar rates and overstay modifiers. Optional evolved art slots remain empty and
  use generated placeholders.
- Windows development build succeeded through the live CLI: zero errors; one warning
  that Pipeline has no runtime config and is disabled in players (the game does not need
  runtime Pipeline). Player launched outside the Editor, responded, and its log contained
  no exception/error matches.
- Web development build succeeded through the live CLI in 428.9 seconds: zero errors,
  two warnings (runtime Pipeline disabled; queued uncompiled Editor changes). The latter
  followed Inspector tooltip/comment edits; no Editor import/post-processing code changed.
  The built player was served over localhost and visibly rendered the arena, actors,
  telegraphs, clock and packet HUD, with the hint clear of the panels. Browser console:
  no errors; one Unity persistent-data synchronization deprecation warning. This is a
  loading/rendering smoke check, not a Web performance or survival verdict.
- Human gate remains pending: play a short run and judge whether swapping matters and
  the pace is fast. Phase 7 has not started.
