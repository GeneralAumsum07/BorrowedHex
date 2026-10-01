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
