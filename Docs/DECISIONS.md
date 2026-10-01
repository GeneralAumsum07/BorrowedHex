# Borrowed Hex — Decisions

Rulings made during implementation where the plan (`Docs/GAME_PLAN.md`) was silent,
ambiguous, or conflicted with what the project actually needed. Each entry records what was
decided, why, and what it costs if it turns out wrong.

| # | Phase | Decision | Why | Cost if wrong |
|---|---|---|---|---|
| D1 | 0 | Work directly on `main`, committing and pushing per phase | Explicit user instruction (overrides the plan's "no automatic commits") | None — user-directed |
| D2 | 0 | Combat simulation is plain C# (`ArenaSim`), MonoBehaviours only read input and draw views | Makes timing, capture, provenance and terminal ordering testable in EditMode and deterministic | Views need a sync layer; slightly more plumbing than component-per-entity |
| D3 | 0 | Collision is analytic on the XZ plane (swept circles vs circles/boxes), not PhysX | One consistent model, exact swept tests incl. initial overlap, cheap on WebGL; arena is flat boxes + circles anyway | Non-box obstacles would need new primitives |
| D4 | 0 | Unity "collision layers" are logical factions in code, not Unity physics layers | No PhysX collision is used, so physics layers would be unused configuration | None |
| D5 | 0 | `PlayerSettings.companyName = "BorrowedHex"` | Fixes the persistent save path identity before saves exist (section 12 check) | Changing it later moves Windows save location; migrate if renamed |
| D6 | 0 | Lit materials do not receive shadows; the sun casts none; characters get blob shadows | URP Lit shadow-receiving draws fail on WebGL (Chrome 152/ANGLE: "Mismatch between texture format and sampler type") and the arena rendered blank. Matches Unity forum report 863894 | Loses real cast shadows on the floor; revisit when final art/lighting arrives |
| D7 | 0 | Scene content is built by `ProjectBootstrap` (editor APIs, idempotent); runtime-only entities (player, enemies, projectiles, UI) are spawned in code rather than authored as prefabs | Keeps the CLI workflow repeatable and avoids hand-authored YAML; placeholder art is generated anyway | Artists replace views via `CharacterView`/view adapters rather than editing prefabs |
| D8 | 0 | The plan's phases are executed in order; each phase is one or more commits and a status entry in `IMPLEMENTATION_STATUS.md` instead of the superpowers task scripts | The plan uses "Phase N" sections, not the task-brief format the scripts parse | None |
