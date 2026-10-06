# Borrowed Hex — Lore Implementation Plan

**Date:** 4 October 2026  
**Status:** Planned; runtime implementation has not started.  
**Goal:** Tell the complete Borrowed Hex narrative through short lore screens, character dialogue, and the existing gameplay presentation.  
**Architecture:** A presentation-owned narrative director observes the deterministic simulation, holds a dedicated pause reason during story sequences, and hands control back to the existing run flow. Story discoveries persist in the player profile. The final encounter transition is reordered so the anomaly revelation occurs after arrival in the Sanctum and before the final upgrade screen.  
**Tech stack:** Unity 6.3 LTS `6000.3.25f1`, C#, existing uGUI helpers, Unity Input System, and the existing profile storage.  
**Narrative source:** [LORE_DOCUMENT.md](LORE_DOCUMENT.md). Gameplay references: [GAME_PLAN.md](GAME_PLAN.md) and its later [REWORK_PLAN.md](REWORK_PLAN.md) revisions.

> **Execution:** Implement the tasks sequentially using `superpowers:executing-plans`. Checkboxes describe future work. Do not commit, push, or publish without a separate request.

## 1. Experience and scope

A player's first successful short run should deliver a complete narrative. Essential revelations do not require earlier deaths, achievements, collectibles, or mastery levels.

The player should understand:

- Their familiar world became the Afterlease through the Collector's keeping.
- The Collector began as a mortal physician, Avel Sere.
- The rogue's completed death and interrupted conversion explain their borrowed life and dependence on enemy magic.
- The unresolved entry is an anomaly that prevents the Collector from sealing the keeping permanently.
- Killing the rogue's body renews the anomaly; recovering the torn entry is necessary to resolve it.
- The rogue must release the other entries before closing their own.

Target approximately **2–3 minutes of reading**, excluding combat. The player controls reading speed; this is a pacing target, not a timeout.

### Guaranteed presentation

**Black lore screens:** Full black background with centered text. One passage per page, no scrolling, and at most **28 words** in the passage. Text types out at **35 text elements per second**. The completed passage stays until the player advances. Advancing clears the text and starts the next page. These screens carry history, discoveries, written messages, and transitions.

**Character dialogue:** Freeze the gameplay view and hide combat HUD elements. Show a black box across the bottom with the speaker's name, sprite, and one short line of at most **18 words**. Dialogue types out to a simple, quiet text tick. Use the existing magician and Collector sprites; Mara appears through written corrections, so her story does not require a new character asset.

**Sound:** Use a synthetic text tick for dialogue, in the spirit of Undertale's text presentation. There is no voice acting, speech synthesis, spoken narration, or recording milestone. Black lore screens remain silent as their passages type out. Existing gameplay sound remains independent of this feature.

**Optional illustrations:** Still illustrations may replace the black backgrounds of existing pages. They are an enhancement after the complete text version works, with no changes to triggers or controls.

### Story placement

| Beat | Trigger | Delivery | Purpose |
|---|---|---|---|
| The stolen life | First short-run start | Black lore screens | Establish the world, execution, escape, borrowed life, and objective |
| The last kindness | First encounter cleared, before its upgrade choice | Black lore screens | Reveal Sere's origins and Mara's refusal |
| The forgotten answer | Second encounter cleared, before its upgrade choice | Black lore screens | Show the human cost of compulsory keeping |
| The anomaly | Sanctum pull/reveal finished, before the final upgrade screen | Black lore screens | Reveal the permanent binding and the rogue's obstruction |
| The confrontation | Final upgrade choice resolved, already in the Sanctum | Bottom dialogue | Make the conflict personal before combat |
| The open window | Short-run victory, before results | Lore screens and bottom dialogue | Resolve the Ledger, Collector, world, and protagonist |
| The recall | Death or time expiry | Brief results caption | Explain another attempt without delaying restart |

**Required final transition:**

`Encounter 3 cleared → Sanctum pull/reveal → anomaly revelation → final upgrade choice → confrontation → boss combat`

The anomaly must not play before the pull, over the pull animation, after the upgrade screen, or after boss combat starts. A previously seen anomaly scene can be skipped immediately at its designated handoff; the upgrade choice still follows the completed arrival.

The existing tutorial retains its lessons. Practice and endless mode do not play the short-run story or its ending.

## 2. Screen-ready narrative

Each numbered passage is a separate page or dialogue line. These are the initial implementation scripts, adapted from the lore document. Labels identifying a written source are separate from the passage.

### A. The stolen life — opening

Black screens:

1. “The Collector executed you. Your heart is beating again—but every beat spends time that isn't yours.”
2. “The baker whispered warmth into his ovens. The healer borrowed another morning. You made coins disappear. The children waited, smiling, for their return.”
3. “Later, you carried letters through wards where nobody left. Some letters escaped. One register didn’t survive your visit. The next entry was your own.”
4. “You caught the binding meant to claim you and stole its remaining time. Your true name stayed in his Ledger.”
5. “A physician refused to let his patients die. He kept their bodies, their homes, and finally the world.”
6. “Your world remained. Its health did not. Those who stayed too long became something worse.”
7. “Every heartbeat spends that time. Your own spellmaking died with your life. Borrow your enemies' magic. Reach the Ledger. Take your name back.”

Fade into the existing arena. Do not add a new playable opening or replace the tutorial.

### B. The last kindness — after encounter one

Black screens:

1. “Before he was The Collector, Avel Sere was a mortal physician. During a famine, he borrowed years to keep his patients alive.”
2. “His sister Mara recorded their last wishes. When her time came, she asked him to open the window.”
3. “He closed it, cut the ending from her healing spell, and made her stay.”
4. “Beside her name, she wrote: Enough. He crossed it out.”

On the final page, emphasize **Enough** with a restrained text color and a crossing-out. No illustration is required. After completion or skip, show the existing upgrade choice.

### C. The forgotten answer — after encounter two

Black screens:

1. “Deep in the House, patients lie beneath clean blankets. Beside each bed is a record of consent dated centuries ago.”
2. “I asked for time to see my son. He came. We spoke. He went home. What is the rest of this for?”
3. “In the Ledger's margins, Mara writes: He remembers everything about us except the last thing we said.”

Label the second page **An unfiled request** and the third **Mara's correction**. Treat them as writing, without a speaking portrait or text tick. After completion or skip, show the upgrade choice.

### D. The anomaly — after the Sanctum pull, before upgrades

Wait until the existing pull, camera relocation, and Sanctum reveal finish. Keep the simulation frozen and the final upgrade panel hidden. Then show these black screens:

1. “The Collector means to seal every renewal into one unbroken circle. Nothing within his keeping would ever be permitted to leave.”
2. “But you are neither ordinarily alive nor one of his bound dead. Your unfinished entry keeps that circle open.”
3. “Killing you only begins the failed renewal again. He must reclaim your torn entry. Mara's corrections show where the buried exits remain.”

This is the revelation that the rogue's existence obstructs his plan. When the sequence finishes or is skipped, show the final upgrade screen **in the Sanctum**. Choosing or continuing proceeds to the confrontation; it does not initiate another pull.

### E. The confrontation — after the final upgrade choice

Use the bottom dialogue box over the frozen Sanctum:

1. **Collector:** “There you are. Your bed is still made.”
2. **Collector:** “You have already spent several of my patients. Shall I tell you their names?”
3. **Rogue:** “You kept their signatures. Did you keep their answers?”
4. **Collector:** “They were tired.”
5. **Rogue:** “They told you what they wanted.”
6. **Collector:** “Until your entry is settled, nothing can be finished.”
7. **Rogue:** “Then I'm opening their entries before I close mine.”

Type these lines with the dialogue tick. There is no dialogue choice: the rogue's resolve is established by the narrative, and the game has one principal ending.

After the final line or a scene skip, clear the dialogue interface, restore the combat HUD, and begin the existing boss fight with its normal spawn warning. Do not repeat the Sanctum reveal or its title.

### F. The open window — after victory

Black screens:

1. “The Collector falls. His Ledger remains. While its renewals endure, even he can be recalled.”
2. “You place the torn transfer beside your true name. Closing only your entry would complete his circle.”
3. “You leave your name open and restore the dismissals he buried.”
4. “The dead can leave. Those with time remaining can spend it freely. Exhausted buildings fall. Places capable of growth can change again.”
5. “Mara's entry closes. Beside it, one word remains: Enough.”
6. “The world does not become young. What survives can finally have a future.”

Return to the frozen arena for a short typed dialogue exchange:

- **Collector:** “The lamp. Please. Don't leave me in the dark.”
- **Rogue:** “Is there someone I should send for?”

Finish on black screens:

7. “You open the window. You stay until the lamp goes out. Then you close your own entry.”
8. “There will be no further recall. The unused time leaves your hands. There is light beyond the window.”

Then show the existing results panel. The text version does not require animated beds, windows, a Ledger prop, or a dying Collector pose. The dialogue portrait is an interface image independent of the actor removed by terminal cleanup.

### G. Failure and brief combat reactions

On death or time expiry, show this alongside the results:

> The unfinished renewal recalls you. Another advance waits to be stolen.

It must not delay **Play again** or the restart key.

Optional rogue reactions:

| Event | Line |
|---|---|
| First successful capture | “Was that meant for me?” |
| First backfire | “Held on too long.” |
| First paid upgrade | “I'll need less time if this works.” |

Combat reactions are instant, silent captions above the occupied HUD area. They never pause combat, type out, play dialogue ticks, or require input. These reactions are polish after the complete main story is integrated.

## 3. Implementation tasks

Use the existing C# simulation, uGUI helpers, input routing, and profile storage. Add no external dialogue framework, Timeline dependency, or video playback system.

### Task 1 — Narrative content and selection

**New responsibilities:**

- `NarrativeCatalog`: immutable scene/page content, stable scene IDs, speaker IDs, page presentation kind, and optional illustration keys. Keep the guaranteed English script in code so optional assets cannot remove it.
- `NarrativeDirector`: scene eligibility, pending playback, per-run duplicate prevention, completion/skip decisions, and replay selection.
- `NarrativeProfileState`: unlocked and seen scenes, story completion, and narrative settings.

Use these stable IDs: `prologue`, `last_kindness`, `forgotten_answer`, `anomaly`, `confrontation`, `open_window`.

The director exposes `IsPlaying`, `BlocksFlow`, and `HasPendingBossScene`. Its scene-completion callback distinguishes completion, explicit skip, automatic familiar-scene skip, and cancellation. Cancellation never marks a partially viewed scene as seen.

- [ ] Implement the catalog using section 2's exact passages and presentation kinds.
- [ ] Select opening, encounter, Sanctum-arrival, confrontation, and ending scenes at their specified triggers.
- [ ] Identify deliveries by run ID and scene ID. A pause/resume transition cannot deliver the same scene again.
- [ ] Keep unlocked, seen, and story-completed status separate.
- [ ] Test fresh-profile ordering, familiar-scene skipping, explicit skip, cancellation, and duplicate notifications.

**Acceptance:** A first successful short run reaches all six sequences in order without a prior death or progression unlock.

### Task 2 — Lore screens, portraits, and typed dialogue

Create `UI/NarrativePanel` using the existing canvas and UI helpers. It supports centered lore pages and bottom character dialogue.

- [ ] Keep lore text within a central safe area. Place dialogue within the bottom quarter, with an unobstructed name, sprite, and navigation controls.
- [ ] Respect the existing interface scale. Use `PixelSprites.Get` for guaranteed magician and Collector portraits; optional portrait overrides preserve aspect ratio.
- [ ] Type out black lore passages at **35 text elements per second**, using the same reveal system as character dialogue. Pressing advance during typing reveals the remaining passage; a second fresh press advances. Lore text has no fade-in or fade-out; changing pages clears the previous passage and starts typing the next.
- [ ] Type character dialogue at **35 text elements per second**, starting after the panel's opening fade. Keep the full line's layout fixed using a transparent unrevealed suffix, so wrapping and alignment do not change as letters appear. Reveal whole text elements, never half of a Unicode character.
- [ ] With Reduce flashes enabled, remove panel, scene, and illustration fades. Provide a separate **Instant story text** setting for players who want complete lore passages and dialogue lines immediately.
- [ ] Use Enter, Space, or left click to advance. A press during typing reveals the remainder silently and stays on the current line. A subsequent fresh press advances.
- [ ] Require a fresh input after opening/changing a page; the initiating click or a held key cannot consume it. Clear latched gameplay input before returning to combat.
- [ ] Add a **Skip scene** button. Escape opens the pause menu instead of skipping the narrative.
- [ ] Freeze narrative animation/typing during menus and focus loss. Returning cannot fast-forward text or emit a burst of sound.
- [ ] Test both layouts, input behavior, scale limits, instant text, and scene cancellation during typing.

**Acceptance:** Every passage is readable without scrolling; completing a typed line never accidentally advances the next page or performs a combat action.

### Task 3 — Sanctum arrival and run-flow handoffs

**Verified integration point:** In the current `ArenaSim.Run` flow, encounter three opens `UpgradeChoice`; resolving that choice calls `BeginBossIntro`, which selects the Sanctum and spawns the boss. `WorldPresentation` currently calls `CompleteBossIntro` directly when the pull finishes. These handoffs must change to enforce the requested arrival-before-upgrades order.

Add a `GameRoot.Narrative` partial to own UI construction, subscriptions, cancellation, and narrative handoffs. Append `PauseReason.Narrative` and `RunState.SanctumArrival` to their enums so existing values keep their meanings.

**Opening and the first two encounters:**

- [ ] Show the opening after building the short-run simulation but before its first gameplay tick.
- [ ] At encounter-one/two clear, hold the narrative pause and hide the upgrade controls until the relevant scene completes or skips. Retain the already generated offers.

**Final encounter and Sanctum:**

- [ ] At encounter-three clear, generate the final upgrade offers once at the same point they are generated today, preserving draw order. Prepare the Sanctum and boss once, then enter `SanctumArrival` with gameplay frozen.
- [ ] Move the existing clear/select/spawn work into a shared preparation path. Keep captured packets, scoring, and the existing terminal rules intact. Prepare cover once; choosing an upgrade cannot reinitialize the Sanctum afterward.
- [ ] Let the existing world presentation perform its pull and reveal. Do not apply a narrative hold that prevents that travel from completing.
- [ ] At arrival completion, notify `GameRoot`; do not start boss combat. The handler selects scene D, with upgrade controls still hidden.
- [ ] After scene D completes or skips, call a guarded `CompleteSanctumArrival()` to enter the final `UpgradeChoice`. Preserve the boss pause so no combat begins between story and upgrades.
- [ ] On final upgrade confirmation/Continue, advance the encounter counter once and enter `BossIntro` without selecting the arena, spawning the boss, or replaying travel again.
- [ ] Deliver the confrontation there. Start boss combat only after it ends/skips and all remaining menu/focus holds clear. Preserve the normal boss spawn warning.
- [ ] Suppress the conventional boss banner when the Sanctum travel already displayed its title. For a headless/classic arena without world presentation, complete arrival immediately, deliver the anomaly before upgrades, and use the conventional banner once at the confrontation.

**Other modes and lifecycle:**

- [ ] Preserve endless-mode travel and boss cycles; they do not receive the short-run arrival/upgrade reorder or narrative ending.
- [ ] Prevent menus, focus loss, and re-entered states from re-triggering arrival, drawing new offers, or spawning another boss.
- [ ] Make `GameRoot` the presentation owner of the scored short run's boss-start decision; the world observer reports readiness rather than bypassing narrative gates.
- [ ] While story blocks flow, disable gameplay and underlying panel input. Drop accumulated simulation time so resuming never fast-forwards life, enemy timers, or hex decay.

**Victory and teardown:**

- [ ] Preserve existing `RunEnded` finalization: freeze the summary and save score, records, and rewards exactly once.
- [ ] Hold results behind the ending. Block results buttons and the restart key until the ending completes or skips.
- [ ] Keep death/time-expiry results immediately restartable; their recall caption is non-blocking.
- [ ] On restart/menu return/simulation replacement, remove old subscriptions, cancel typing, stop text sound, hide panels, and discard pending scenes. Remove only narrative-owned pause reasons.

**Acceptance:** The last transition always follows `clear → pull → anomaly → upgrades → confrontation → fight`, with no life spent while reading and no duplicated arena, boss, offer, or reward.

### Task 4 — Persistence, settings, and Story replay

Add an additive narrative field to the existing player profile. Keep the current schema version because existing fields retain their meaning; initialize a missing narrative group when loading an older save.

Defaults:

- No scenes unlocked or seen; story completion false.
- **Skip familiar scenes:** on.
- **Instant story text:** off; applies to both lore screens and character dialogue.
- **Dialogue sound volume:** 35%.
- **Combat reactions:** on.

- [ ] Unlock a scene when its eligible story trigger is reached. Mark it seen on completion or explicit skip, not on interruption.
- [ ] Set story completion on an eligible short-run victory even if the ending is skipped.
- [ ] Do not award story unlocks from tutorial, practice, endless, or debug/cheated runs. Development previews use in-memory narrative state.
- [ ] Let an abandoned ordinary run retain discoveries already reached, without awarding run rewards.
- [ ] Add **Story** to the main menu, listing unlocked scenes in narrative order. Undiscovered scenes remain locked; the ending cannot be previewed before victory.
- [ ] Replay the same content without creating a scored run, changing completion, or awarding rewards.
- [ ] Add the four narrative settings above to the existing settings UI. Save at settings changes and story boundaries, not on every typed letter.
- [ ] Test legacy saves, skipped scenes, interrupted scenes, replay, and excluded run modes.

**Acceptance:** Returning players can retry quickly and reread previously skipped discoveries without revealing future scenes.

### Task 5 — Dialogue text sound and combat reactions

Create `DialogueTextAudio`, dedicated to the typewriter tick. It contains no speech playback or voice-clip catalog.

- [ ] Generate a soft **25 ms**, mono **640 Hz** sine tick at **22,050 Hz** sample rate, with **5 ms** attack/release ramps and **0.08** peak amplitude. This is the guaranteed sound; an optional supplied tick asset may replace it. No recording or asset download is required.
- [ ] Use one consistent tick across speakers, at fixed pitch and the saved dialogue-sound volume. Do not add vocal impersonations or randomized pitch effects.
- [ ] Tick on newly revealed letters/numbers, not spaces or punctuation. Limit ticks to one every **0.06 seconds** so the sound stays restrained.
- [ ] Do not play ticks for black lore pages, instant-text mode, scene skips, or instant completion of a partially typed line. Lore typing stays silent.
- [ ] Stop active ticks when the narrative pauses, loses focus, changes scene, or is cancelled. Do not replay missed ticks afterward.
- [ ] Implement section G's optional combat reactions as silent, instant captions. Allow each at most once per run with a shared **20-second cooldown**; discard reactions that cannot appear promptly.
- [ ] Hide combat reactions during blocking story and use a compact caption position above the existing HUD, without a full dialogue box.
- [ ] Test volume zero, throttling, whitespace, instant completion, pause/resume, and per-run reaction limits.

**Acceptance:** Dialogue has a gentle typing rhythm. Muting the tick changes neither the available story nor its controls. No spoken content is part of the release.

### Task 6 — Optional illustrations and final polish

Optional illustration groups:

1. The torn entry and waking hand.
2. Sere beside Mara's closed window.
3. The ward of ancient consent.
4. The unfinished circle in the Ledger.
5. The Collector and Last Lamp.
6. The opened window and released world.

- [ ] Attach still-image overrides to existing pages; use simple fades and no mandatory camera animation.
- [ ] Fall back to black when an illustration is absent. Reduced-flash settings remove transitions.
- [ ] Keep narrative input, timing, and eligibility identical with or without pictures.
- [ ] Use the existing pillars, enemy transformations, arena changes, and Sanctum reveal to convey decay; add no environmental mechanics in this plan.

**Acceptance:** Removing every optional image leaves a complete, playable narrative with speaker sprites and text sound intact.

## 4. Verification and acceptance

### Review focus

1. **Sanctum completion starts combat early:** arrival must hand off to the anomaly, then upgrades, then confrontation.
2. **A dialogue advance leaks into combat or upgrades:** require fresh presses and flush latched gameplay commands.
3. **Focus/menu overlap clears the wrong hold:** story, travel, upgrade, and boss reasons retain independent ownership.
4. **Victory ending duplicates rewards:** finalize once before presentation; completion, skip, and replay never finalize again.
5. **Instant completion emits a burst of sound:** revealing the rest of a line must be silent and must not advance the next page.

### EditMode checks

- Fresh-profile scene order and all essential revelations available in one successful short run.
- No duplicate scene eligibility after pause/resume or repeated arrival notifications.
- Correct seen/unlocked/completed states for completion, skip, cancellation, and replay.
- Third encounter prepares the Sanctum and draws the final offers exactly once.
- Arrival completion enters final upgrades; their resolution enters boss intro without another selection, spawn, or cover reset.
- Earlier encounter upgrades and endless cycles retain their existing behavior.
- Legacy profile defaults and excluded-mode/debug unlock rules.
- Catalog word limits, valid speaker/presentation combinations, and text-reaction cooldowns.
- Lore and dialogue typewriters revealing at the configured rate, fresh-press behavior, dialogue tick throttle, and silence on instant completion.

### PlayMode checks

- Assert visible order: third clear, pull/reveal, anomaly, upgrades, confrontation, boss combat. Test both a new profile and familiar-scene skipping.
- Assert no upgrade panel, combat input, or boss attack during the pull and anomaly.
- Assert a single boss, single final offer draw, and one Sanctum travel; upgrades do not cause another reveal.
- Assert life, held hexes, enemies, and pillar decay stay frozen during blocking narrative.
- Assert lore passages type out without text fades or sound; changing pages clears the old passage and starts the new one.
- Assert completing a lore passage or dialogue line reveals it without advancing; the next fresh press advances without catch/release/dash input leakage. Instant story text applies to both layouts.
- Assert focus/menu pauses stop typing and tick audio, with no catch-up burst on return.
- Assert the ending holds results while score/rewards finalize once; skipping/replaying does not duplicate them.
- Assert failure results remain immediately restartable.
- Assert teardown removes old narrative state, subscriptions, captions, and active tick playback.
- Assert speaker fallbacks, absent illustrations, maximum UI scale, and reduced-flash settings remain usable.

Use the existing project helpers: `bash .superpowers/uc.sh` for compilation, `bash .superpowers/rt.sh editor [Filter]` for focused EditMode tests, and `bash .superpowers/rtp.sh [Filter]` for PlayMode tests. Run focused checks during each task, then the full suites after integration. Do not treat historical test counts as new evidence.

### Manual checks

Validate Windows and Web builds with a fresh profile and full victory; several failures followed by victory; all scenes skipped; Story replay; text sound muted; Instant story text enabled for both layouts; optional images absent; maximum UI scale; Reduce flashes; keyboard-only and mouse-only controls; and focus loss during Sanctum arrival, typing, and the ending.

A first-time reader should be able to answer:

1. Was the Afterlease originally the protagonist's world?
2. Who was the Collector before his necromancy?
3. Why does the rogue borrow spells and life?
4. Why must the Collector resolve the anomaly rather than merely kill its body?
5. Why does the rogue release the other entries first?

## 5. Delivery order and constraints

Deliver in this order:

1. Catalog, narrative selection, and focused tests.
2. Typed black lore screens and typed character dialogue.
3. Complete short-run integration, including the revised Sanctum handoff and ending.
4. Persistence, settings, familiar-scene skipping, and Story replay.
5. Dialogue text sound and optional silent combat reactions.
6. Optional illustrations.

The guaranteed release uses text screens, existing speaker sprites, and generated dialogue ticks. Illustrations are optional; voice acting is excluded entirely.

Keep the lore document as the narrative authority. Adapt its prose for brief screens without changing the established history, anomaly, motives, or ending. This task creates the implementation plan only; it does not change the lore document or runtime code.

Do not add branching endings, collectible gates, a new campaign mode, or deaths required to unlock essential revelations. Subsequent scored short runs replay the unresolved premise; endless mode continues the unresolved keeping. Neither retroactively reverses the completed narrative ending.

Preserve unrelated in-progress work. Implementation remains native and sequential. Commits, pushes, publication, and external asset acquisition require their own user request.
