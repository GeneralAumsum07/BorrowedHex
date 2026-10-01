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
