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
