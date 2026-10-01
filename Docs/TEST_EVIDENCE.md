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
