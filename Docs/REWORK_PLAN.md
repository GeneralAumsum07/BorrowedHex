# Borrowed Hex — Game Rework Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make swapping hands mandatory under pressure, make the hold-and-fire decision a skill shot, and shorten the death-to-retry loop. The changes are the hand rule, priming, school resistance, Overcharge, upgrades bought with life, instant restart, kill chains, lifesteal with a fully unlockable skill tree, life shown x10, and score/XP from skill shots.

**Architecture:** Every rule lives in the plain-C# sim (`ArenaSim` partials, `PacketStore`, `CaptureController`), so EditMode tests can pin it. Presentation (HUD, view, hit-stop, shake, results) only listens to new `SimEvents`. Tuning goes into the existing `GameConfig` sections and `Assets/Game/Data/GameConfig.asset`. No new assemblies.

**Tech Stack:** Unity 6000.3.25f1, C#, NUnit EditMode and PlayMode tests run through the live editor (`.superpowers/*.sh`), and the new Input System.

**Spec:** §0 of this document. It holds the owner decisions from 3 Oct 2026, quoted, plus my rulings. The game's base spec is `Docs/GAME_PLAN.md`. Where the two conflict, this document wins for the items listed in §0.1. `GAME_PLAN.md` is not rewritten; Task 12 adds pointers from it to this document.

---

## 0. Spec

### 0.1 What the owner decided (verbatim, 3 Oct 2026)

> "We will implement both Rule A and Rule B as well as your secondary idea - enemies take 0.75x damage from their own attacks and 1.33x from other sources. Keep the priming time 0.4 s for now. Along with this, from your previous response, we will implement points 1, 2 (but only for temporary upgrades, it will cost 25% of your current health), 3 (leave the "one thing you nearly did" out for now), 4 and as for point 5, I will discuss more in the next message."

The proposals that quote refers to:

| # | Feature | Proposal as written in the review |
|---|---|---|
| A | Hand rule | "You catch only with your selected hand, and only if it's empty. No automatic banking." Q pockets (freezes) the held hex. |
| B | Priming | "A fresh hex is unstable and can't be fired for its first ~0.5 s." The owner set it to **0.4 s**. |
| S | School resistance | "Enemies take 0.75x damage from their own attacks and 1.33x from other sources." |
| 1 | Overcharge | "Make the last ~0.35 s before a hex expires a crit zone, with big feedback: freeze-frame, shake, a distinct sound, gold numbers. Releasing in that window counts as a 'perfect release'. Releasing a moment too late still backfires." This also covers the power-curve problem the review raised ("Power rises linearly… no sweet spot"). |
| 2 | Keep it | "When an upgrade expires, offer 'Keep it'." The owner limited it to temporary upgrades (no pillars), at a cost of **25% of current health**. |
| 3 | Instant restart | "After a death, the results should be readable in about 2 s, with one key to retry." The near-miss line is **out**. |
| 4 | Kill chains | "Consecutive kills inside a short window add escalating time (+3, +4, +6…), shown as a streak counter by the life bar." |
| 5 | Skill-tree reframe | **Not in this plan.** The owner will discuss it next. |

### 0.1b Owner answers to the first draft (verbatim, 3 Oct 2026)

> "For point 2., yes reverse it and for 4. no, they don't get the 33% bonus and for 6.,no, the point is to sacrifice your own health to keep an existing upgrade, you can still get more on top. point 7 - the GAME_PLAN.md should point to the new doc. I'm fine with the other points"

That changes R5, R6 and R13 below and settles every question except Q7. The draft's numbering ran 1–7 over R6, R7, R5, R10, R13 and the GAME_PLAN question, so the owner's "2" is the riposte (R6), "4" is upgrade damage (R5), "6" is Keep (R13) and "7" is GAME_PLAN.

### 0.1c Owner revision (verbatim, 3 Oct 2026)

> "We should keep the "max upgrades at a time" limit to 4 for now, you should be able to keep stacking them, we can add increasing life sacrifices. For example - You have 1 upgrade and want to keep it and pick another = you lose 25% of current health. You have 2 upgrades and want to keep them and pick another = you lose 40% of current health. You have 3 upgrades and want to keep them and pick another = you lose 50% of current health. After that there are no more choices - you are locked with those 4 upgrades till the end of the run.
> We should also add lifesteal skill tree nodes - you gain a tiny portion of the damage you deal back as health.
> You rely on borrowed power - and it comes with a price."

This replaced R13 (stacking up to four, Task 8) and added R17 (lifesteal, Task 10).

### 0.1d Owner answers to the second draft (verbatim, 3 Oct 2026)

> "On your calls - 1. No, you cannot discard your upgrades. If you have 0 upgrades, you can pick 1 by sacrificing 15% of current health or continue without upgrades. If you have 1 - you have two choices - swap for free or keep your current one AND gain another - but you sacrifice 25% of your health and so on...
> Points 3 and 4- You can rank up cards, no matter how many upgrades you hold - and the sacrificing cost depends on the number of cards you hold - for example, you have 2 cards and your choice of cards has one card that is a rank up of one of your existing cards and other 2 are new cards, then you can choose to either upgrade the card or pick a new one - both will need the same 40% health sacrifice OR you can choose to not pick anything and continue without sacrificing health
> Point 9 - remove the limiter on max number of nodes - everything can be unlocked.
> Point 11 - A number should pop up indicating the lifesteal healing - you can multiply all health and drain in the game by 10 or any other number to avoid small decimals
> Go with your calls on other points
> The answer to the open Q7 - overcharge and kill chains should add to score (the combo multiplier should also add score, and also, higher score should earn you more XP too)"

Follow-up answers, from the owner's picks on four multiple-choice questions:
- At four held: **rank-ups only, for 50%**.
- A swap: **the player chooses** which held card goes.
- Life ×10: **display only**.
- Skill tree: **no equip step, mastery cap 13**.

This rewrites R13 (Task 8) and extends R17 (Task 10). It adds R18 (an unlockable tree, Task 10), R19 (score and XP, the new Task 11) and R20 (life ×10, Task 8), and answers Q7. The docs task becomes Task 12.

### 0.2 Rulings (owner-confirmed where marked)

Each ruling gets a `DECISIONS.md` row in the task that implements it. "Cost if wrong" says what changes if the owner overrules it.

| ID | Ruling | Why | Cost if wrong |
|---|---|---|---|
| R1 | **Priming counts gameplay time since capture** (`Clock.Now - CapturedAt`), not selected time (`DecayedTime`). A pocketed hex keeps priming. | Rule B's job is to force the juggle: catch, pocket, fire the other hex, swap back, fire. If a pocketed hex stopped priming, that juggle would cost a second 0.4 s wait and punish the swap the rule is meant to create. The game clock pauses during menus and choices, so priming pauses too. | A one-line change in `ArenaSim.IsPrimed`, plus two tests flip. |
| R2 | Several shots caught **in one catch window** still append to that window's packet, even after Q moves the selection mid-window. Rule A applies when a window would **start** a packet. | One window has always meant one packet (CaptureController). Splitting a volley across hands mid-window would be invisible and confusing. | `CaptureController.TryCapture` would check `store.HandFree` before appending too. |
| R3 | A **full hand plus a full pocket** still reports `SlotsFull`. A full hand with an empty pocket reports the new `HandFull`. | Existing stats and tests keep their meaning, and the HUD can say "press Q" only when Q would actually help. | Enum naming only. |
| R4 | **Overflow triggers on a full hand** (pocket empty or full). Its forced release **ignores priming**. **Fusion still needs both slots full.** | Overflow reads as "catching with your hand full fires what you hold". Fusion merges the *other* packet, so it needs one. A forced release is a side effect of the catch, not a fire command (same reasoning as D79 for Quick Draw). | With Overflow held, one hand is enough for that encounter. That is the upgrade working as intended, but it does blunt Rule A for that encounter. |
| R5 | School resistance compares the **attack's school** (`AttackSnapshot.SourceCategory`) with the **victim's category**. Same school: ×0.75. Any other enemy's school: ×1.33. **Upgrade damage (Orbit, Parting Gift) is neutral, ×1.0.** It is told apart by `DamageCategory`, not by school. | **Owner, 3 Oct:** upgrades "don't get the 33% bonus". The ×0.75/×1.33 rule is about turning enemies' magic against each other, and an upgrade is not enemy magic. | One condition in `SchoolMultiplier`. |
| R6 | **A riposte counts as another source (×1.33 against everything, Pursuers included).** `FireRiposte` is **not** changed: its snapshot keeps the default `SourceCategory` (`ActorCategory.Player`), which never matches an enemy's category. | **Owner, 3 Oct:** reversed from the draft, so parry stays the hard counter to Pursuers. | A test pins it (`School_Riposte_CountsAsAnotherSource`). |
| R7 | **The boss ignores school resistance** (×1.0 from everything). | In the boss fight the boss's own returned shots are almost the only damage source (ordinary enemies are despawned at the transition, section 6). ×0.75 would make the fight 33% longer, and the bots have never reached the boss, so it is untested. | One condition in `SchoolMultiplier`. **Flagged as question Q3.** |
| R8 | The power curve becomes **eased (convex) up to the zone, then ×1.5 inside the last 0.35 s**. The values are `peakPower 1.8`, `powerCurveExponent 2`, `overchargeWindow 0.35`, `overchargeMultiplier 1.5`. They are **placeholders to tune by play**. The old curve was 1.0 rising linearly to 2.05. The new one is ≈1.02 at 0.4 s, 1.26 at 1.5 s, 1.8 at 2.65 s, and **2.7 in the zone**. | The curve rewards waiting instead of a shallow slope, and makes the zone the obvious goal. | Values only, in `GameConfig.asset`. My inference: early and mid-hold fires get weaker, which partly cancels the ×1.33 cross-school buff. Needs playtesting. |
| R9 | An **Overflow release in the zone is still overcharged** (power and gold), because Overflow fires "at its current power". **Quick Draw stacks** with Overcharge. | Overcharge belongs to the hex's state, not to the fire command. | Two tests change. |
| R10 | **A pocketed hex frozen inside the Overcharge zone stays overcharged** until it is selected again and fired or expires. | This follows from "only the selected hex decays". It is a skill play (bank a crit), and the risk returns the moment it is selected (≤0.35 s to fire). | If it turns out degenerate in play, make `IsOvercharged` require the packet to be selected. **Flagged as question Q4.** |
| R11 | **"Gold numbers"** means two things. First, a gold `OVERCHARGE ×2.7` label at the player. Second, gold damage numbers on enemies hit by an overcharged volley. The game has no damage numbers today, so the gold numbers only appear for overcharged hits. Projectiles from an overcharged release are gold. | This matches the review's intent without adding numbers to every hit. | Presentation only. |
| R12 | **Freeze-frame and shake respect the existing "Reduce flashes" setting.** It is the only accessibility toggle there is. | Motion-sensitive players need a way off. | A new settings toggle if the owner wants them separate. |
| R13 | **Upgrades for life (owner).** Nothing expires. At each choice: continue for free; swap a new card in for free (you pick which one goes); or pay a share of **current** life to add a card or rank a held one up: **15% / 25% / 40% / 50%** with 0 / 1 / 2 / 3 held. **Four held** locks the set: only rank-ups, for 50%, no swaps. My sub-rulings are R13a–k in Task 8: rank-ups come from the normal draw, and max-rank cards leave the pool; the price is paid on confirming; the price is not a hit and can't kill; each upgrade keeps its own rank; Overflow beats Fusion; short mode tops out at 3 held (inferred); the default focus is Continue. | **Owner, 3 Oct (third revision):** quoted in §0.1d. | Four held upgrades is a large, untested power spike. Supersedes GAME_PLAN §5's "everything is temporary". |
| R17 | **Lifesteal (owner).** A fourth skill-tree branch, **Blood Price**, with three stacking nodes that return life-seconds per point of damage landed. Placeholders: 0.10 / +0.10 / +0.15 (1 / 1 / 1.5 on screen). No overkill; every player-caused source counts; capped at the starting seconds; off in the tutorial; **a muted-red pop for every heal** (owner). Full detail is in Task 10 (R17a–f). | **Owner, 3 Oct:** "you gain a tiny portion of the damage you deal back as health"; "A number should pop up indicating the lifesteal healing". | Values untested by play. Pops on every hit may crowd busy fights. |
| R18 | **The whole skill tree can be unlocked (owner).** There is no equip step: owning a node makes it active. The mastery cap goes from 10 to 13, so 12 points buy all 12 nodes. Detail: Task 10 (R18a–c). | **Owner, 3 Oct:** "remove the limiter on max number of nodes - everything can be unlocked"; the owner chose cap 13 and no equip. | The tree stops being a build choice (flagged and accepted). Profiles at level 10 re-earn levels 11–13. |
| R19 | **Score from skill shots, XP from score (owner, Q7).** An Overcharged release scores 20 × combo, and a chain kill scores {0, 5, 10, 15}[N−1] × combo (placeholders). XP gains score ÷ 50. Detail: Task 11 (R19a–e). | **Owner, 3 Oct:** quoted in §0.1d. The combo already multiplied kill score; it now multiplies the new bonuses too (my reading). | Values only. Records from before aren't comparable. |
| R20 | **Life is shown ×10, display only (owner).** The sim and tuning keep seconds; `LifeDisplay` rounds seconds × 10 for every number the player reads. A hit reads −100 and the start is 1800. Detail: Task 8 (D100). | **Owner, 3 Oct:** "multiply all health and drain in the game by 10 … to avoid small decimals"; the owner chose display only. | Tuning values in seconds no longer match the numbers on screen. |
| R14 | **Restart key is R**, active **only on the results screen**. Enter on the focused "Play again" button already works and stays. The results screen shows a big headline line (score, time, kills) with the detailed stats smaller underneath. | R in live combat would throw runs away by accident. The pause menu already has Restart. | One condition in `GameRoot.HandleRestartKey`. |
| R15 | **Kill chain:** a kill within `chainWindow` (2.5 s, placeholder) of the previous kill extends the chain. The bonus seconds by chain length come from `chainBonusSeconds = {0, 1, 3, 5}`, and the last entry repeats. For Acolytes that gives +3, +4, +6, +8, +8…, which matches the owner's "+3, +4, +6…". Kills in the same tick each count (a rocket wiping three is a chain of three). Boss kills neither extend nor break a chain. Taking a hit does **not** break it. Life is still capped at the run's starting seconds. | This is the proposal as written. Breaking the chain on hits was not asked for. | Values only, plus one line for break-on-hit. |
| R16 | **Tutorial:** the Slots lesson now teaches "your hand is full, press Q to pocket it, then catch again", and the Capture lesson mentions the 0.4 s priming. The goal (hold two hexes, swap, fire from both) is unchanged. | Rule A changes how the goal is reached, not what it is. | Prompt text only. |

### 0.3 Questions for the owner

Resolved on 3 Oct 2026 (see §0.1b):
- Priming continues while pocketed (R1).
- A riposte counts as another source (R6, reversed).
- The boss ignores school resistance (R7).
- A pocketed hex can bank an Overcharge (R10).
- Upgrade damage is neutral (R5, changed).
- Upgrades are never discarded. You pay 15/25/40/50% to add a card or rank one up, swap for free, and four held means rank-ups only (R13, third revision).
- Lifesteal nodes, with a pop on every heal (R17), and the whole tree can be unlocked (R18).
- Overcharge and chains add score, and score earns XP (R19, answering Q7).
- Life is shown ×10, display only (R20).
- `GAME_PLAN.md` only points here (Task 12).

Nothing is open. Q7 was answered on 3 Oct (§0.1d, R19).

---

## Global Constraints

- Unity 6000.3.25f1; scripts under `Assets/Game/Scripts`, tests under `Assets/Game/Tests/{EditMode,PlayMode}`.
- Recompile: `bash .superpowers/uc.sh` must print `failed: False`.
- EditMode: `timeout 900 bash .superpowers/rt.sh editor [Filter]`. PlayMode: `timeout 900 bash .superpowers/rtp.sh` (Bash tool timeout ≥ 600000 ms).
- Baseline before Task 1: **EditMode 319/319, PlayMode 11/11.** Every task ends with the **full** EditMode suite green.
- Priming time **0.4 s**. School multipliers **0.75 own / 1.33 other**. Upgrade prices **15% / 25% / 40% / 50% of current life** (by upgrades held), **four** held at most. Life is shown **×10** (display only). Exact values, from the owner.
- Comment code heavily and explain *why*. Match the existing comment voice: decision IDs in parentheses, e.g. `(D89)`.
- New `DECISIONS.md` rows start at **D89**. Planned: D89 hand rule, D90 priming, D91 Overflow/Fusion under the hand rule, D92 school resistance, D93 power curve and Overcharge, D94 Overcharge feedback, D95 kill chains, D96 upgrades for life, D97 instant restart and compact results, D98 tutorial update, D99 lifesteal, D100 life shown x10, D101 fully unlockable skill tree, D102 score and XP from skill shots.
- Commit locally after each task: `git add Assets/Game Docs`, then `git commit -q -F - <<'EOF' … EOF`. **Never push.** **No co-author trailer, no "Generated with", no tool credit.**
- Never stage `Assets/Sprites*`, `Assets/Settings/Mobile_RPAsset.asset`, `ProjectSettings/ProjectSettings.asset` or `Assets/_Recovery*`. `git add Assets/Game Docs` already avoids them.
- C# edits keep CRLF line endings. Python edit scripts in the scratchpad (`edit_lib.py` `edit(path, [(old, new)])`) preserve them. Run them with `python script.py < /dev/null`.
- The rest of point 5 (the skill-tree reframe) is still out of scope. Task 10 adds the Blood Price branch and removes the equip limit (owner). It touches `SkillTree`, `Loadout`, `ProgressionTuning`, `SkillTreePanel`, `PlayerProfile`, `ProfileService` (`MaxLevel`, validation) and one line in each of `StylePanel` and `GameRoot.Progression`. Do not rename or retune the existing nine nodes. Task 11 adds one XP rate to `Mastery`.

## Review Focus

These are the five inputs most likely to bite a player that no feature test naturally covers. Each has a pinning test in the task named.

1. **Catch and Q pressed on the same tick.** Cycle runs before catch in `TickPlayer`, so the catch must land in the newly selected (empty) hand, not be rejected. Pinned in Task 1 (`HandRule_CycleAndCatchOnOneTick_CatchLandsInTheNewHand`).
2. **Right-click on an unprimed hex.** It must fire nothing, keep the hex, raise exactly one `ReleaseRefused` per press, and leave the hex firable once primed. Pinned in Task 2 (`Priming_UnprimedRelease_IsRefusedOnce_ThenFiresWhenPrimed`).
3. **A pocketed hex frozen inside the Overcharge zone.** It stays overcharged and fires overcharged right after being selected again (R10). Pinned in Task 5 (`Overcharge_PocketedInTheZone_StaysOvercharged`).
4. **The choice screen when taking nothing, or when nothing new can be offered.** Continue must cost nothing and take nothing. A card at max rank must never be offered, and the panel must still fill three cards from what's left. At four held, every card must be a rank-up and swaps must be refused. Pinned in Task 8 (`Continue_IsFree_AndTakesNothing`, `MaxRankCards_AreNeverOffered` and `FourHeld_OffersOnlyRankUps_AtHalfYourLife`). That Enter on a freshly opened choice continues for free (default focus, R13k) is a manual check in Task 12.
5. **R pressed during live combat.** It must do nothing. Pinned in Task 9 (PlayMode `RestartKey_OnlyActsOnTheResultsScreen`).

---

## File map

| File | Responsibility | Tasks |
|---|---|---|
| `Scripts/Combat/CapturedPacket.cs` | `PacketStore.Create` gets the hand-only rule. Adds `HandFree` and `CreateInSlot`. `FirePower(PowerCurve)`. | 1, 5 |
| `Scripts/Combat/CaptureController.cs` | `CaptureResult.HandFull`; the capture gate uses `HandFree`. | 1 |
| `Scripts/Combat/PowerCurve.cs` (**new**) | Eased ramp plus Overcharge zone. A pure function, unit-testable. | 5 |
| `Scripts/Combat/AttackSnapshot.cs` | `Overcharged` flag on `AttackSnapshot` and `DamageEvent`. | 5 |
| `Scripts/Data/PlayerTuning.cs` (`CaptureTuning`) | `primeSeconds`. Curve fields replace `powerPerSecond`. | 2, 5 |
| `Scripts/Data/CombatTuning.cs` | `ownSchoolDamage`, `otherSchoolDamage`, `chainWindow`, `chainBonusSeconds`. | 4, 7 |
| `Scripts/Data/UpgradeTuning.cs` | `maxHeld`, `maxRank`, `takeCostByHeld`. | 8 |
| `Scripts/Core/LifeDisplay.cs` (**new**) | Life shown ×10 (display only). | 8, 10 |
| `Scripts/Data/RunTuning.cs` (`ShortModeTuning`) | `overchargeScore`, `chainScore`. | 11 |
| `Assets/Game/Data/GameConfig.asset` | Serialized values for the above (`combat:` and `capture:` blocks). | 2, 4, 5, 7 |
| `Scripts/Player/PlayerStats.cs` | `PrimeSeconds`. `Power` (a `PowerCurve`) replaces `PowerPerSecond`. `LifePerDamage`. | 2, 5, 10 |
| `Scripts/Runs/ArenaSim.Capture.cs` | The priming gate in `TryReleaseEarly`; Overcharge in `ReleaseService`. | 2, 5 |
| `Scripts/Runs/ArenaSim.Upgrades.cs` | `TryFullHandUpgrade` (renamed); the held list, `RankOf`, rank-up offers, prices, swaps, the lock at four. | 1, 8 |
| `Scripts/Runs/ArenaSim.Enemies.cs` | `SchoolMultiplier` and `StealLife` in `DamageEnemy`. | 4, 10 |
| `Scripts/Progression/SkillTree.cs`, `Loadout.cs`, `Data/ProgressionTuning.cs` | Blood Price branch: three lifesteal nodes; equip rules removed. | 10 |
| `Scripts/Progression/PlayerProfile.cs`, `ProfileService.cs` | `equippedNodes` removed; `MaxLevel` 13; validation. | 10 |
| `Scripts/Progression/Mastery.cs` | `ScorePerXp`, `XpBreakdown.ScoreXp`. | 11 |
| `Scripts/UI/SkillTreePanel.cs` | Four columns; a click only buys. | 10 |
| `Scripts/Runs/KillChain.cs` (**new**) | Chain length, window and bonus lookup. Pure C#. | 7 |
| `Scripts/Runs/ArenaSim.Run.cs` | `RewardKillTime` adds the chain bonus. | 7 |
| `Scripts/Runs/SimEvents.Combat.cs` / `SimEvents.Run.cs` | `ReleaseRefused`, `PacketOvercharged`, `KillChainChanged`, `UpgradePaid`, `LifeStolen`. | 2, 5, 7, 8, 10 |
| `Scripts/Runs/RunScore.cs` | `Overcharges`, `BestChain`, `SecondsSacrificed`, `LifeStolen`, `ScoreFromOvercharges`/`ScoreFromChains`, plus summary fields. | 5, 7, 8, 10, 11 |
| `Scripts/Runs/EncounterUpgrades.cs` | Overflow and Fusion card text. | 1 |
| `Scripts/Runs/TutorialDirector.cs` | Slots and Capture prompts. | 3 |
| `Scripts/UI/PacketIndicator.cs` | UNSTABLE, OVERCHARGE and HAND FULL states. | 1, 2, 5 |
| `Scripts/UI/GameplayHud.cs` | Chain counter by the life bar; every held upgrade in the upgrade label. | 7, 8 |
| `Scripts/UI/RunFlowPanels.cs` | Take/Add/Swap/Rank-up buttons and prices; swap target; Continue; compact results; `[R]` label; health stolen; score breakdown. | 8, 9, 10, 11 |
| `Scripts/Presentation/ArenaView.cs` | Gold shots, gold numbers, the OVERCHARGE label, the refusal pop; ×10 pops; the lifesteal pop. | 2, 6, 8, 10 |
| `Scripts/Presentation/CameraShake.cs` (**new**) | Unscaled-time shake on the main camera. | 6 |
| `Scripts/Presentation/GameRoot.cs` | Hit-stop; R restart; choice wiring. | 6, 8, 9 |
| `Scripts/Presentation/GameRoot.Progression.cs`, `Scripts/UI/StylePanel.cs` | Read owned nodes; the XP breakdown shows score. | 10, 11 |
| `Scripts/Player/PlayerInputReader.cs` | `Restart` UI action (R). | 9 |
| Tests (EditMode) | `SlotTests`, `UpgradeTests`, `CaptureTests`, `BorrowedTimeTests`, `ParryTests`, `AchievementTests`, `MasteryTests`, `RunLifecycleTests`, `TutorialTests`, `IntegrationTests`, `StyleTests`, `ProfileTests`, `PlayerTests` (`TestSims`), plus new `ReworkTests.cs`. | 1–11 |
| Tests (PlayMode) | `GameRootPlayModeTests`. | 9, 10 |
| Docs | `DECISIONS.md` (one row per task), `IMPLEMENTATION_STATUS.md`, `TEST_EVIDENCE.md`, `HANDOFF.md`, `GAME_PLAN.md` (pointers only). | each task + 12 |

---

## Task 1: Hand rule (Rule A)

**Files:**
- Modify: `Assets/Game/Scripts/Combat/CapturedPacket.cs` (`PacketStore` doc comment, `Create`, new `HandFree`, `CreateInSlot`)
- Modify: `Assets/Game/Scripts/Combat/CaptureController.cs` (`CaptureResult`, `TryCapture`)
- Modify: `Assets/Game/Scripts/Runs/ArenaSim.Upgrades.cs` (`TrySlotsFullUpgrade` → `TryFullHandUpgrade`)
- Modify: `Assets/Game/Scripts/Runs/ArenaSim.Capture.cs` (the call site in `TryCaptureProjectile`)
- Modify: `Assets/Game/Scripts/Runs/EncounterUpgrades.cs` (Overflow and Fusion text)
- Modify: `Assets/Game/Scripts/UI/PacketIndicator.cs` (HAND FULL flash)
- Modify: `Assets/Game/Tests/EditMode/PlayerTests.cs` (`TestSims.Seed`, `TestSims.Pocket`)
- Modify: `Assets/Game/Tests/EditMode/{SlotTests,UpgradeTests,CaptureTests,BorrowedTimeTests,ParryTests,AchievementTests,MasteryTests,RunLifecycleTests,IntegrationTests,StyleTests}.cs` (migration)
- Create: `Assets/Game/Tests/EditMode/ReworkTests.cs`
- Modify: `Docs/DECISIONS.md` (D89, D91)

**Interfaces:**
- Produces:
  - `PacketStore.HandFree : bool`: the selected slot holds no packet and is not locked.
  - `PacketStore.CreateInSlot(int slot, int packetId, int activationId, double now, float lifetime, int capacity) : CapturedPacket` (null if that slot is occupied, locked or out of range).
  - `CaptureResult.HandFull`.
  - `ArenaSim.TryFullHandUpgrade(ref AttackSnapshot) : CaptureResult?`.
  - `TestSims.Seed(PacketStore, int packetId, int activationId, double now, float lifetime, int capacity) : CapturedPacket` (first free slot).
  - `TestSims.Pocket(ArenaSim)` (one tick with Q).

- [ ] **Step 1: Add the test helpers**

In `Tests/EditMode/PlayerTests.cs`, extend `TestSims`:

```csharp
    public static class TestSims
    {
        static GameConfig config;
        public static GameConfig Config => config != null ? config : (config = GameConfig.CreateDefault());

        public static ArenaSim Sandbox(int seed = 1) => new ArenaSim(Config, RunSetup.ForSandbox(seed));

        /// <summary>
        /// Seed a packet straight into the first free slot, bypassing the hand rule (D89). Tests
        /// that need "two packets held" use this; tests about WHICH slot a catch fills must go
        /// through a real catch (or PacketStore.Create) instead.
        /// </summary>
        public static CapturedPacket Seed(PacketStore store, int packetId, int activationId, double now, float lifetime, int capacity)
        {
            for (int slot = 0; slot < store.SlotCount; slot++)
            {
                var p = store.CreateInSlot(slot, packetId, activationId, now, lifetime, capacity);
                if (p != null) return p;
            }
            return null;
        }

        /// <summary>Rule A (D89): one tick with Q pressed, pocketing the held hex so the hand is free.</summary>
        public static void Pocket(ArenaSim sim) => sim.Tick(PlayerCommand.Moving(Vector2.zero).WithCycle(), 1f / 60f);
    }
```

Add `using BorrowedHex.Combat;` to the top of `PlayerTests.cs` if it is missing.

- [ ] **Step 2: Write the failing tests**

Create `Tests/EditMode/ReworkTests.cs`:

```csharp
using System.Collections.Generic;
using BorrowedHex.Combat;
using BorrowedHex.Core;
using BorrowedHex.Player;
using BorrowedHex.Runs;
using NUnit.Framework;
using UnityEngine;

namespace BorrowedHex.Tests
{
    /// <summary>
    /// The 3 Oct 2026 rework (Docs/REWORK_PLAN.md): hand rule, priming, school resistance,
    /// Overcharge, kill chains, keeping an upgrade. One fixture so the rework's rules are
    /// readable in one place.
    /// </summary>
    public class ReworkTests
    {
        const float Dt = 1f / 60f;
        static readonly Vector2 AimEast = new Vector2(5f, 0f);
        static PlayerCommand Hold => PlayerCommand.Moving(Vector2.zero).WithAim(AimEast);

        static ArenaSim Sim()
        {
            var sim = TestSims.Sandbox();
            sim.Player.Position = Vector2.zero;
            return sim;
        }

        static void Shot(ArenaSim sim, Vector2 from, string id = AttackIds.Bolt)
        {
            var s = AttackSnapshot.From(sim.Attacks.Get(id), 999, sim.Ids.Next(), 0f);
            sim.SpawnProjectile(s, AttackFaction.Hostile, from, sim.Player.Position - from);
        }

        static void Run(ArenaSim sim, int ticks, PlayerCommand? cmd = null)
        {
            for (int i = 0; i < ticks; i++) sim.Tick(cmd ?? Hold, Dt);
        }

        /// <summary>Catch one bolt, then let the catch recovery finish so the next press is live.</summary>
        static void CatchOne(ArenaSim sim)
        {
            Shot(sim, new Vector2(1.2f, 0f));
            sim.Tick(Hold.WithCatch(), Dt);
            Run(sim, Mathf.CeilToInt(sim.Stats.CaptureRecovery / Dt) + 2);
        }

        // ---- Rule A: the hand rule (D89) -----------------------------------------------------

        [Test]
        public void HandRule_FullHand_RejectsTheCatch_EvenWithAnEmptyPocket()
        {
            var sim = Sim();
            CatchOne(sim);
            Assert.IsNotNull(sim.Packets.InSlot(0));
            CaptureResult? got = null;
            sim.Events.CaptureRejected += (_, r) => got = r;
            Shot(sim, new Vector2(1.2f, 0f));
            sim.Tick(Hold.WithCatch(), Dt);
            Assert.AreEqual(CaptureResult.HandFull, got);
            Assert.IsNull(sim.Packets.InSlot(1), "no automatic banking into the other slot");
        }

        [Test]
        public void HandRule_QPocketsTheHex_ThenTheFreeHandCatches()
        {
            var sim = Sim();
            CatchOne(sim);
            var first = sim.Packets.InSlot(0);
            TestSims.Pocket(sim);
            Assert.AreEqual(1, sim.Packets.SelectedSlot);
            CatchOne(sim);
            Assert.AreSame(first, sim.Packets.InSlot(0), "the pocketed hex is untouched");
            Assert.IsNotNull(sim.Packets.InSlot(1));
        }

        [Test]
        public void HandRule_CycleAndCatchOnOneTick_CatchLandsInTheNewHand()
        {
            // Review Focus 1: cycle runs before catch in TickPlayer, so Q + click together is
            // "pocket, then catch" — the most natural panic input must not be punished.
            var sim = Sim();
            CatchOne(sim);
            Shot(sim, new Vector2(1.2f, 0f));
            sim.Tick(Hold.WithCycle().WithCatch(), Dt);
            Assert.IsNotNull(sim.Packets.InSlot(1));
            Assert.AreEqual(1, sim.Packets.SelectedSlot);
        }

        [Test]
        public void HandRule_BothSlotsFull_StillReportsSlotsFull()
        {
            var sim = Sim();
            CatchOne(sim);
            TestSims.Pocket(sim);
            CatchOne(sim);
            CaptureResult? got = null;
            sim.Events.CaptureRejected += (_, r) => got = r;
            Shot(sim, new Vector2(1.2f, 0f));
            sim.Tick(Hold.WithCatch(), Dt);
            Assert.AreEqual(CaptureResult.SlotsFull, got);
        }

        [Test]
        public void HandRule_ParryStillWorks_WithAFullHand()
        {
            // A parry makes no hex, so a full hand must not stop the catch window opening.
            var sim = Sim();
            CatchOne(sim);
            Assert.IsTrue(sim.Capture.IsReady(sim.Clock.Now));
            sim.Tick(Hold.WithCatch(), Dt);
            Assert.IsTrue(sim.Capture.IsWindowOpen(sim.Clock.Now));
        }

        [Test]
        public void Store_Create_UsesOnlyTheSelectedSlot()
        {
            var store = new PacketStore(2);
            Assert.IsNotNull(store.Create(1, 1, 0, 3f, 12));
            Assert.IsNull(store.Create(2, 2, 0, 3f, 12), "selected slot occupied: no fallback");
            store.CycleSelection();
            Assert.AreEqual(1, store.Create(3, 3, 0, 3f, 12).Slot);
        }

        [Test]
        public void Overflow_TriggersOnAFullHand_WithThePocketEmpty()
        {
            var sim = Sim();
            sim.ForceUpgrade(UpgradeId.Overflow);
            CatchOne(sim);
            var first = sim.Packets.InSlot(0);
            CapturedPacket released = null;
            sim.Events.PacketReleased += (p, _) => released = p;
            Shot(sim, new Vector2(1.2f, 0f));
            sim.Tick(Hold.WithCatch(), Dt);
            Assert.AreSame(first, released);
            Assert.IsNotNull(sim.Packets.InSlot(0));
            Assert.AreNotSame(first, sim.Packets.InSlot(0));
            Assert.IsNull(sim.Packets.InSlot(1));
        }

        [Test]
        public void Fusion_NeedsBothSlotsFull_AFullHandAloneIsRejected()
        {
            var sim = Sim();
            sim.ForceUpgrade(UpgradeId.Fusion);
            CatchOne(sim);
            CaptureResult? got = null;
            sim.Events.CaptureRejected += (_, r) => got = r;
            Shot(sim, new Vector2(1.2f, 0f));
            sim.Tick(Hold.WithCatch(), Dt);
            Assert.AreEqual(CaptureResult.HandFull, got);
        }
    }
}
```

- [ ] **Step 3: Compile and run to confirm the failures**

Run: `bash .superpowers/uc.sh`
Expected: `failed: True` with errors naming `CreateInSlot`, `HandFull` and `TestSims.Seed`/`HandFree` (they don't exist yet).

- [ ] **Step 4: Implement the store and controller**

In `CapturedPacket.cs`, update the `PacketStore` summary: replace the phrase "and a new packet takes the selected slot first, then another free slot" with "and a new packet takes ONLY the selected slot, and only if it is empty (D89)". Then replace `Create`:

```csharp
        /// <summary>
        /// Rule A (D89): the selected slot is the hand. A catch lands there or nowhere — there is
        /// no fallback to the other slot any more, because banking a hex is the player's Q
        /// decision, not the store's. Before D89 the store auto-banked, which made one slot enough.
        /// </summary>
        public bool HandFree => InSlot(SelectedSlot) == null && !IsLocked(SelectedSlot);

        public CapturedPacket Create(int packetId, int activationId, double now, float lifetime, int capacity)
            => HandFree ? CreateInSlot(SelectedSlot, packetId, activationId, now, lifetime, capacity) : null;

        /// <summary>
        /// Put a packet in a specific slot. Gameplay goes through <see cref="Create"/>; this exists
        /// for tests and sandbox seeding that need "two hexes held" without playing two catches.
        /// </summary>
        public CapturedPacket CreateInSlot(int slot, int packetId, int activationId, double now, float lifetime, int capacity)
        {
            if (slot < 0 || slot >= SlotCount || InSlot(slot) != null || IsLocked(slot)) return null;
            var p = new CapturedPacket
            {
                PacketId = packetId,
                Slot = slot,
                ActivationId = activationId,
                CapturedAt = now,
                Lifetime = lifetime,
                AdvancedAt = now,
                Capacity = capacity,
            };
            packets.Add(p);
            return p;
        }
```

In `CaptureController.cs`, add `HandFull` as the **last** member of `CaptureResult`, with a comment: `// D89: the selected hand holds a hex but the other slot is free — press Q first.`. In `TryCapture`, replace:

```csharp
            if (store.FreeSlots <= 0) return CaptureResult.SlotsFull;
```

with:

```csharp
            // Rule A (D89): only an empty selected hand can start a packet. Both slots full keeps
            // its old name (stats, tests); a full hand with a free pocket is the new HandFull, the
            // one case where Q would have saved the catch, so the HUD can say exactly that.
            if (!store.HandFree) return store.FreeSlots <= 0 ? CaptureResult.SlotsFull : CaptureResult.HandFull;
```

- [ ] **Step 5: Re-gate Overflow and Fusion (D91)**

In `ArenaSim.Upgrades.cs`, rename `TrySlotsFullUpgrade` to `TryFullHandUpgrade` and replace its summary and first guard:

```csharp
        /// <summary>
        /// Before the normal capture: if this catch would be refused because the hand is full
        /// (D91), Overflow fires the held hex to make room — pocket empty or not — and Fusion merges,
        /// but Fusion still needs BOTH slots full since it absorbs the other packet. Used at most
        /// once per activation. Returns the result if it fully handled the shot (Fusion), or null
        /// to continue with the normal path.
        /// </summary>
        CaptureResult? TryFullHandUpgrade(ref AttackSnapshot shot)
        {
            if (Capture.ActivePacket != null || Packets.HandFree || Capture.SlotsFullUpgradeUsed) return null;
```

Inside the Fusion branch, add this as the first line: `if (Packets.FreeSlots > 0) return null;   // a free pocket: nothing to merge (D91)`. In the Overflow branch, extend the existing comment with `// Forced release: priming does not apply (D91), like Quick Draw does not (D79).`. In `ArenaSim.Capture.cs`, `TryCaptureProjectile`: `var special = TryFullHandUpgrade(ref shot);`. The property `CaptureController.SlotsFullUpgradeUsed` keeps its name: it is in tests and the meaning ("once per activation") holds.

In `EncounterUpgrades.cs` `Describe`:

```csharp
                case UpgradeId.Overflow:
                    return "Catching with your hand full fires the hex you hold at once and catches the new shot in its place.";
                case UpgradeId.Fusion:
                    return $"Catching with both slots full merges the catch and your other packet into the selected one (+{Pct(t.fusionPowerScale - 1f)} power). The other slot stays locked until it fires.";
```

- [ ] **Step 6: HUD flash for HAND FULL**

In `PacketIndicator.cs`, subscribe in `Bind` and flash the selected panel's highlight red for 0.3 s on `CaptureResult.HandFull`:

```csharp
        float handFullUntil;

        public void Bind(ArenaSim s)
        {
            if (sim != null) sim.Events.CaptureRejected -= OnRejected;
            sim = s;
            handFullUntil = 0f;
            sim.Events.CaptureRejected += OnRejected;
        }

        // D89: the one rejection Q would have prevented gets its own cue on the hand itself,
        // so "why didn't that catch?" reads as "my hand was full", not as a missed click.
        void OnRejected(UnityEngine.Vector2 at, CaptureResult r)
        {
            if (r == CaptureResult.HandFull) handFullUntil = Time.unscaledTime + 0.3f;
        }

        void OnDestroy() { if (sim != null) sim.Events.CaptureRejected -= OnRejected; }
```

In `LateUpdate`, where `p.Highlight` is set for the selected panel, use `RejectRed` (add `static readonly Color RejectRed = new Color(1f, 0.3f, 0.3f);`) while `selected && Time.unscaledTime < handFullUntil`. In the selected panel's `state` string, use `"HAND FULL — Q"` instead of `"DECAYING"` during the flash.

- [ ] **Step 7: Migrate the existing tests**

1. In every test file, replace direct seeding with the helper. Do **not** change `BorrowedTimeTests.CatchFillsSelectedEmptySlotFirst`, which tests `Create` itself.
   - `sim.Packets.Create(` → `TestSims.Seed(sim.Packets, `
   - `store.Create(` → `TestSims.Seed(store, `
   
   The affected files are AchievementTests:121, BorrowedTimeTests:18 and 38, CaptureTests:25/38 and the creates in 47–110, MasteryTests:272, ParryTests:240, RunLifecycleTests:119/227 and UpgradeTests:24.
2. Any test that **catches twice** and expects the second catch in slot 1 (via a `CatchVolley` helper or similar) needs `TestSims.Pocket(sim);` before the second catch. After that, the selected slot is **1**. If the test then right-clicks expecting slot 0 to fire, add another `TestSims.Pocket(sim);` before the release.
   - Known cases: `SlotTests.OwnerExample_…` (rename it to `OwnerExample_SlotOneKeepsItsVolley_PocketedCatchesGoToSlotTwo`), `SlotTests.SlotsKeepTheirPosition_WhenTheOtherReleases`, `SlotTests.ReleaseFreesASlotForACatchOnTheSameTick` and `SlotTests.Cycle_SelectsSlotTwo_…`.
   - Run the suite to find the rest.
3. `UpgradeTests.WithoutOverflowOrFusion_FullSlotsRejectTheCatch` stays as it is: both slots are seeded and selected slot 0 is full, so the result is still `SlotsFull` (R3).
4. In `IntegrationTests` (the soak bot, around lines 62–69), catch only with a free hand. Otherwise pocket:

```csharp
                if (incoming != null && sim.Capture.IsReady(now))
                {
                    // D89: a full hand cannot catch — pocket first (Q and catch on one tick is
                    // "pocket, then catch", the cycle runs first).
                    cmd = cmd.WithAim(incoming.Position).WithCatch();
                    if (!sim.Packets.HandFree && sim.Packets.FreeSlots > 0) cmd = cmd.WithCycle();
                    return rng.NextDouble() < 0.5 ? cmd.WithDash() : cmd;
                }
```

5. Leave `TutorialTests` failures for Task 3 **only if** they are in the Slots lesson. Note them in the ledger. Everything else must be green.

- [ ] **Step 8: Run the tests**

Run: `bash .superpowers/uc.sh`, then `timeout 900 bash .superpowers/rt.sh editor`
Expected: `failed: False`. All `ReworkTests` pass. The only failures allowed are `TutorialTests` Slots-lesson tests (listed in the ledger for Task 3).

- [ ] **Step 9: Decisions and commit**

Append to `Docs/DECISIONS.md`. Use the same table format as D88: ID | phase | decision | why | cost.

```
| D89 | Rework | Hand rule (Rule A). A catch starts a packet ONLY in the selected slot, and only if that slot is empty and unlocked. There is no automatic banking into the other slot. A full hand with a free other slot rejects the catch as HandFull (a red flash on the hand, "HAND FULL — Q"); both slots full is still SlotsFull. Q pockets the held hex (it freezes, as before). The catch window still opens with a full hand, so parry is unaffected. Shots caught later in the same window still append to that window's packet even if Q was pressed mid-window | Owner decision 3 Oct 2026: swapping must be necessary, not optional. Auto-banking (GAME_PLAN §3 "Which slot a catch fills", rule 2) made one slot enough | Harder for players who never press Q; the tutorial Slots lesson is rewritten (D98) |
| D91 | Rework | Overflow triggers on a FULL HAND (the other slot may be empty) and its forced release ignores priming. Fusion still needs both slots full. Card text updated | Overflow's fantasy is "catching with your hand full fires what you hold"; Fusion needs an other packet to merge | With Overflow held, one hand is enough for that encounter |
```

```bash
git add Assets/Game Docs
git commit -q -F - <<'EOF'
Make the selected hand the only place a catch can land

A catch now starts a packet only in the selected slot, and only when it is
empty: no more automatic banking into the other slot. A full hand rejects the
catch as HandFull so the HUD can say "press Q"; Overflow fires on a full hand,
Fusion still needs both slots full (D89, D91). Tests seed packets through
TestSims.Seed and pocket with TestSims.Pocket where they used to rely on
auto-banking.
EOF
```

---

## Task 2: Priming (Rule B)

**Files:**
- Modify: `Assets/Game/Scripts/Data/PlayerTuning.cs` (`CaptureTuning.primeSeconds`)
- Modify: `Assets/Game/Data/GameConfig.asset` (`capture:` block)
- Modify: `Assets/Game/Scripts/Player/PlayerStats.cs` (`PrimeSeconds`)
- Modify: `Assets/Game/Scripts/Runs/ArenaSim.Capture.cs` (`IsPrimed`, `TryReleaseEarly`)
- Modify: `Assets/Game/Scripts/Runs/SimEvents.Combat.cs` (`ReleaseRefused`)
- Modify: `Assets/Game/Scripts/UI/PacketIndicator.cs` (UNSTABLE state)
- Modify: `Assets/Game/Scripts/Presentation/ArenaView.cs` (refusal pop)
- Modify: `Assets/Game/Tests/EditMode/ReworkTests.cs`, `SlotTests.cs`, `IntegrationTests.cs`
- Modify: `Docs/DECISIONS.md` (D90)

**Interfaces:**
- Consumes: `PacketStore.HandFree` (Task 1).
- Produces:
  - `PlayerStats.PrimeSeconds : float`.
  - `ArenaSim.IsPrimed(CapturedPacket) : bool`.
  - `SimEvents.ReleaseRefused : Action<CapturedPacket>`.

- [ ] **Step 1: Write the failing tests** (append to `ReworkTests`)

```csharp
        // ---- Rule B: priming (D90) ------------------------------------------------------------

        [Test]
        public void Priming_UnprimedRelease_IsRefusedOnce_ThenFiresWhenPrimed()
        {
            // Review Focus 2: the refused press keeps the hex, raises ONE refusal, fires nothing.
            var sim = Sim();
            Shot(sim, new Vector2(1.2f, 0f));
            sim.Tick(Hold.WithCatch(), Dt);
            var p = sim.Packets.InSlot(0);
            Assert.IsNotNull(p);
            int refused = 0, released = 0;
            sim.Events.ReleaseRefused += _ => refused++;
            sim.Events.PacketReleased += (_, __) => released++;
            sim.Tick(Hold.WithRelease(), Dt);
            Assert.AreEqual(1, refused);
            Assert.AreEqual(0, released);
            Assert.AreSame(p, sim.Packets.InSlot(0));
            // 0.4 s after capture it fires.
            while (sim.Clock.Now - p.CapturedAt < 0.4 - 1e-6) sim.Tick(Hold, Dt);
            sim.Tick(Hold.WithRelease(), Dt);
            Assert.AreEqual(1, released);
            Assert.AreEqual(1, refused, "no refusal for the primed press");
        }

        [Test]
        public void Priming_ContinuesWhilePocketed()
        {
            // R1: priming is time since capture, so the juggle (catch, pocket, fire the other,
            // swap back, fire) never pays the 0.4 s twice.
            var sim = Sim();
            Shot(sim, new Vector2(1.2f, 0f));
            sim.Tick(Hold.WithCatch(), Dt);
            var p = sim.Packets.InSlot(0);
            TestSims.Pocket(sim);
            Run(sim, 30);   // 0.5 s pocketed: DecayedTime barely moved
            Assert.Less(p.DecayedTime, 0.1);
            Assert.IsTrue(sim.IsPrimed(p));
            TestSims.Pocket(sim);   // back to slot 0
            int released = 0;
            sim.Events.PacketReleased += (_, __) => released++;
            sim.Tick(Hold.WithRelease(), Dt);
            Assert.AreEqual(1, released);
        }

        [Test]
        public void Priming_OverflowForcedRelease_IgnoresIt()
        {
            var sim = Sim();
            sim.ForceUpgrade(UpgradeId.Overflow);
            Shot(sim, new Vector2(1.2f, 0f));
            sim.Tick(Hold.WithCatch(), Dt);
            var first = sim.Packets.InSlot(0);
            Run(sim, Mathf.CeilToInt(sim.Stats.CaptureRecovery / Dt) + 2);
            // Still inside 0.4 s? Recovery is 0.65 s, so force freshness for the check:
            first.CapturedAt = sim.Clock.Now;
            Assert.IsFalse(sim.IsPrimed(first));
            CapturedPacket released = null;
            sim.Events.PacketReleased += (pk, _) => released = pk;
            Shot(sim, new Vector2(1.2f, 0f));
            sim.Tick(Hold.WithCatch(), Dt);
            Assert.AreSame(first, released, "Overflow fires an unprimed hex (D91)");
        }
```

- [ ] **Step 2: Run them to confirm the failure**

Run: `bash .superpowers/uc.sh`
Expected: `failed: True`. The errors name `ReleaseRefused` and `IsPrimed`.

- [ ] **Step 3: Implement**

`PlayerTuning.cs` `CaptureTuning`, after `packetSlots`:

```csharp
        [Tooltip("Rule B (D90): seconds after capture before a hex can be fired. Owner value 0.4.")]
        public float primeSeconds = 0.4f;
```

`GameConfig.asset`: in the `capture:` block, add `    primeSeconds: 0.4` after `    packetSlots: 2`.

`PlayerStats.cs`: add the field `public float PrimeSeconds;` next to `PacketSlots`, and `PrimeSeconds = c.primeSeconds,` in `FromConfig`.

`SimEvents.Combat.cs`:

```csharp
        /// <summary>D90: fire was pressed on an unprimed (fresh) hex; nothing was fired.</summary>
        public event Action<CapturedPacket> ReleaseRefused;
        internal void RaiseReleaseRefused(CapturedPacket p) => ReleaseRefused?.Invoke(p);
```

`ArenaSim.Capture.cs`, above `TryReleaseEarly`:

```csharp
        /// <summary>
        /// Rule B (D90): a fresh hex is unstable for PrimeSeconds after its capture. Measured on
        /// the gameplay clock since capture, NOT selected time (R1): a pocketed hex keeps priming,
        /// so catching, pocketing, firing the other hex and swapping back is a fluid rhythm.
        /// The clock freezes in menus and choices, so priming does too.
        /// </summary>
        public bool IsPrimed(CapturedPacket p) => p != null && Clock.Now - p.CapturedAt >= Stats.PrimeSeconds - 1e-6;
```

Inside `TryReleaseEarly`, after the null check:

```csharp
            // An unstable hex refuses the fire command and stays in the hand: with Rule A that
            // means a second incoming shot can only be answered by Q (D89/D90). Overflow's
            // forced release does not come through here, so it ignores priming (D91).
            if (!IsPrimed(packet)) { Events.RaiseReleaseRefused(packet); return false; }
```

`PacketIndicator.cs`: for a packet that is not primed, use the state text `"UNSTABLE"`. The fill/back colour is dimmed (alpha 0.5) until `sim.IsPrimed(pk)`.

`ArenaView.cs`, with the other event subscriptions:

```csharp
            // D90: a refused fire gets a small grey fizzle at the player, so "I clicked and
            // nothing happened" reads as "too fresh", not as a dropped input.
            sim.Events.ReleaseRefused += _ => SpawnPop(sim.Player.Position, new Color(0.7f, 0.7f, 0.75f), 0.2f, 0.6f, 0.15f);
```

- [ ] **Step 4: Migrate the tests broken by priming**

- `SlotTests.ReleasingDuringItsOwnWindow_LaterCatchesInThatWindowStartAFreshSlot`: the catch window (0.25 s) is shorter than priming (0.4 s), so this can no longer happen. Rename it to `ReleaseDuringItsOwnWindow_IsRefused_HexIsStillUnstable` with this body:

```csharp
            var sim = Sim();
            Shot(sim, new Vector2(1.2f, 0f));
            sim.Tick(Hold.WithCatch(), Dt);
            var first = sim.Packets.InSlot(0);
            Assert.IsTrue(sim.Capture.IsWindowOpen(sim.Clock.Now));
            sim.Tick(Hold.WithRelease(), Dt);
            Assert.AreEqual(PacketStatus.Collecting, first.Status, "0.4 s priming outlasts the 0.25 s window (D90)");
            Assert.AreSame(first, sim.Packets.InSlot(0));
```

- Any other test that fires within 0.4 s of a capture: add `Run(sim, 25)` (≈0.42 s) before the release, or fire a `TestSims.Seed` packet whose `CapturedAt` is in the past. Find them by running the suite.
- `IntegrationTests` bot: release only when primed. Otherwise press Q if the pocket holds a primed hex:

```csharp
                var held = sim.Packets.ReleaseCandidate();
                if (held != null && !sim.IsPrimed(held))
                {
                    // D90: too fresh to fire — swap to the pocketed hex if that one is ready.
                    int other = (sim.Packets.SelectedSlot + 1) % sim.Packets.SlotCount;
                    var pocket = sim.Packets.InSlot(other);
                    if (pocket != null && sim.IsPrimed(pocket)) return cmd.WithAim(target).WithCycle();
                    return cmd.WithAim(target);
                }
                return cmd.WithAim(target).WithRelease();
```

- [ ] **Step 5: Run the tests**

Run: `bash .superpowers/uc.sh`, then `timeout 900 bash .superpowers/rt.sh editor`
Expected: `failed: False`. All pass, except the Slots-lesson `TutorialTests` carried from Task 1, if any.

- [ ] **Step 6: Decision and commit**

```
| D90 | Rework | Priming (Rule B). A hex cannot be fired until 0.4 s (`capture.primeSeconds`) of gameplay time have passed since its capture. Fire on an unprimed hex does nothing but raise ReleaseRefused (grey fizzle, UNSTABLE on the slot). Priming counts time since capture, so it continues while the hex is pocketed (R1). Overflow's forced release ignores it | Owner decision 3 Oct 2026, value 0.4 s. Without it, "fire immediately" always freed the hand and made Rule A optional | If priming should freeze in the pocket, IsPrimed reads DecayedTime instead (one line, two tests) |
```

```bash
git add Assets/Game Docs
git commit -q -F - <<'EOF'
Make a fresh hex unstable for 0.4 s before it can be fired

Right mouse on a hex caught less than primeSeconds ago fires nothing and
raises ReleaseRefused; the slot shows UNSTABLE. Priming counts gameplay time
since capture, so it continues while pocketed. Overflow's forced release is
not a fire command and ignores it (D90).
EOF
```

---

## Task 3: Tutorial for the hand rule and priming

**Files:**
- Modify: `Assets/Game/Scripts/Runs/TutorialDirector.cs` (Capture and Slots prompts)
- Modify: `Assets/Game/Tests/EditMode/TutorialTests.cs` (TutorialBot)
- Modify: `Docs/DECISIONS.md` (D98)

**Interfaces:**
- Consumes: `PacketStore.HandFree`, `ArenaSim.IsPrimed`.
- Produces: none.

- [ ] **Step 1: Write the failing test** (append to `TutorialTests`)

```csharp
        [Test]
        public void SlotsLesson_TeachesPocketingWithQ()
        {
            var sim = new ArenaSim(TestSims.Config, RunSetup.ForTutorial(1));
            var bot = new TutorialBot();
            for (int i = 0; i < 60 * 240 && sim.Tutorial.Step != TutorialStep.Slots; i++) sim.Tick(bot.Next(sim), Dt);
            Assert.AreEqual(TutorialStep.Slots, sim.Tutorial.Step);
            StringAssert.Contains("Q", sim.Tutorial.Prompt);
            StringAssert.Contains("pocket", sim.Tutorial.Prompt.ToLowerInvariant());
        }
```

- [ ] **Step 2: Run it to confirm the failure**

Run: `timeout 900 bash .superpowers/rt.sh editor BorrowedHex.Tests.TutorialTests`
Expected: the new test FAILS (the prompt says "Catch shots until both slots are filled"), and the full-playthrough test may fail in the Slots lesson.

- [ ] **Step 3: Rewrite the prompts**

In `TutorialDirector.cs`, Capture step, second prompt:

```csharp
                            ? "Caught! The spell is yours now. A fresh hex is unstable for a moment — then aim at the Acolyte and press RIGHT MOUSE to fire it back."
```

Slots step:

```csharp
                case TutorialStep.Slots:
                    // D98: under the hand rule (D89) the second slot only fills if the player
                    // pockets the first hex with Q, so the lesson teaches that verb by name.
                    Prompt = !bothSlotsHeld
                        ? "You can only catch with an EMPTY hand. Holding a hex? Press Q to pocket it (it freezes), then catch again. Fill both slots."
                        : !swapped
                            ? "Both slots full. RIGHT MOUSE fires the hex in your hand. Press Q to swap to the other one."
                            : slotsFiredFrom.Count < 2
                                ? "Now fire from both slots: RIGHT MOUSE, then Q, then RIGHT MOUSE again."
                                : "Use everything you have learned to defeat both enemies.";
```

Leave the `Progress` lines unchanged.

- [ ] **Step 4: Update the TutorialBot**

In `TutorialTests.TutorialBot.Next`, the catch decision currently reads `bool wantCatch = t.Step == TutorialStep.Slots ? slots.FreeSlots > 0 : !holding;`. Replace the catch block with:

```csharp
                var slots = sim.Packets;
                bool holding = slots.Packets.Count > 0;
                bool wantCatch = t.Step == TutorialStep.Slots ? slots.FreeSlots > 0 : !holding;
                if (incoming != null && wantCatch && sim.Capture.IsReady(now) && now - lastCatch > 0.3)
                {
                    lastCatch = now;
                    var c = PlayerCommand.Moving(move).WithAim(incoming.Position).WithCatch();
                    // D89: a full hand cannot catch; Q on the same tick pockets first.
                    return slots.HandFree ? c : c.WithCycle();
                }
```

In the two release branches, guard each `return cmd.WithRelease();` with priming:

```csharp
                        if (!sim.IsPrimed(slots.InSlot(slots.SelectedSlot))) return cmd;   // D90
```

Add the same guard before the general `if (holding && target != null …)` release at the end:

```csharp
                if (holding && target != null && now - lastRelease > 0.4 && now - lastCatch > 0.35
                    && sim.IsPrimed(slots.ReleaseCandidate()))
```

- [ ] **Step 5: Run the tests**

Run: `timeout 900 bash .superpowers/rt.sh editor`
Expected: the full EditMode suite PASSES, including the full tutorial playthrough.

- [ ] **Step 6: Decision and commit**

```
| D98 | Rework | Tutorial for D89/D90: the Capture lesson says a fresh hex is unstable for a moment; the Slots lesson tells the player they can only catch with an empty hand and to press Q to pocket the held hex before catching again. Goals unchanged (two hexes held, a swap, fire from both) | The old prompt ("catch until both slots are filled") described auto-banking, which no longer exists | Prompt text only |
```

```bash
git add Assets/Game Docs
git commit -q -F - <<'EOF'
Teach pocketing with Q and the unstable fresh hex in the tutorial

The Slots lesson now says a catch needs an empty hand and that Q pockets the
held hex; the Capture lesson mentions priming. The scripted tutorial player
pockets before catching and waits for priming before firing (D98).
EOF
```

---

## Task 4: School resistance

**Files:**
- Modify: `Assets/Game/Scripts/Data/CombatTuning.cs` (after `ripostePierce`)
- Modify: `Assets/Game/Data/GameConfig.asset` (`combat:` block, after `ripostePierce: 1`)
- Modify: `Assets/Game/Scripts/Runs/ArenaSim.Enemies.cs` (`DamageEnemy`, new `SchoolMultiplier`)
- Modify: `Assets/Game/Tests/EditMode/ReworkTests.cs`
- Modify: `Docs/DECISIONS.md` (D92)

**Interfaces:**
- Produces: `ArenaSim.SchoolMultiplier(EnemyActor victim, DamageCategory category, in AttackSnapshot shot) : float` (internal).

- [ ] **Step 1: Write the failing tests** (append to `ReworkTests`; add `using BorrowedHex.Enemies;`)

```csharp
        // ---- School resistance (D92) -----------------------------------------------------------

        static EnemyActor Spawned(ArenaSim sim, ActorCategory cat)
        {
            var e = sim.SpawnEnemy(cat, new Vector2(4f, 0f));
            e.ActiveAt = 0;
            e.Health = e.MaxHealth = 100f;
            return e;
        }

        static AttackSnapshot From(ActorCategory school)
            => new AttackSnapshot { DefinitionId = "test", Kind = AttackKind.Bolt, SourceCategory = school };

        [TestCase(ActorCategory.Acolyte, ActorCategory.Acolyte, 0.75f)]
        [TestCase(ActorCategory.Acolyte, ActorCategory.SiegeFamiliar, 1.33f)]
        [TestCase(ActorCategory.SiegeFamiliar, ActorCategory.SiegeFamiliar, 0.75f)]
        public void School_OwnAttacksResisted_OthersAmplified(ActorCategory victim, ActorCategory school, float factor)
        {
            var sim = Sim();
            var e = Spawned(sim, victim);
            sim.DamageEnemy(e, 10f, DamageCategory.ReturnedProjectile, From(school), 1);
            Assert.AreEqual(100f - 10f * factor, e.Health, 1e-3f);
        }

        [TestCase(DamageCategory.Orbit)]
        [TestCase(DamageCategory.PartingGift)]
        public void School_UpgradeDamageIsNeutral(DamageCategory category)
        {
            // R5 (owner): upgrades get neither the resistance nor the x1.33.
            var sim = Sim();
            var e = Spawned(sim, ActorCategory.Acolyte);
            sim.DamageEnemy(e, 10f, category, From(ActorCategory.Player), 0);
            Assert.AreEqual(90f, e.Health, 1e-3f);
        }

        [Test]
        public void School_Riposte_CountsAsAnotherSource()
        {
            // R6 (owner): parry stays the Pursuer counter. The riposte snapshot is built exactly
            // as FireRiposte builds it, so a later change there that tags it with the Pursuer's
            // category fails this test.
            var sim = Sim();
            var e = Spawned(sim, ActorCategory.Pursuer);
            var riposte = AttackSnapshot.From(sim.Attacks.Get(AttackIds.Riposte), 999, sim.Ids.Next(), 0f);
            sim.DamageEnemy(e, 10f, DamageCategory.ReturnedProjectile, riposte, 1);
            Assert.AreEqual(100f - 10f * 1.33f, e.Health, 1e-3f);
        }

        [Test]
        public void School_BossIgnoresIt()
        {
            var sim = Sim();
            var e = Spawned(sim, ActorCategory.Boss);
            sim.DamageEnemy(e, 10f, DamageCategory.ReturnedProjectile, From(ActorCategory.Boss), 1);
            Assert.AreEqual(90f, e.Health, 1e-3f, "R7");
        }

        [Test]
        public void School_DamageEventReportsTheScaledAmount()
        {
            var sim = Sim();
            var e = Spawned(sim, ActorCategory.Acolyte);
            float got = 0f;
            sim.Events.EnemyDamaged += (_, d) => got = d.Amount;
            sim.DamageEnemy(e, 10f, DamageCategory.ReturnedProjectile, From(ActorCategory.Acolyte), 1);
            Assert.AreEqual(7.5f, got, 1e-4f);
        }
```

**Before writing:** check the real spawn API name with `grep -n "public EnemyActor Spawn" Assets/Game/Scripts/Runs/ArenaSim.Enemies.cs`. Then use it in `Spawned`; if it differs, copy `P4.Parked` from `Phase4Tests.cs`. Set `ActiveAt = 0` so `IsActive` passes.

- [ ] **Step 2: Run them to confirm the failure**

Run: `timeout 900 bash .superpowers/rt.sh editor BorrowedHex.Tests.ReworkTests`
Expected: the School tests FAIL (health drops by exactly 10).

- [ ] **Step 3: Implement**

`CombatTuning.cs`, after `ripostePierce`:

```csharp
        [Tooltip("D92: damage multiplier when an enemy is hit by its own school's attack (owner value 0.75).")]
        public float ownSchoolDamage = 0.75f;
        [Tooltip("D92: damage multiplier from any other school, ripostes included; upgrade damage is neutral (owner value 1.33).")]
        public float otherSchoolDamage = 1.33f;
```

`GameConfig.asset`, after `    ripostePierce: 1` in `combat:`:

```
    ownSchoolDamage: 0.75
    otherSchoolDamage: 1.33
```

`ArenaSim.Enemies.cs`: in `DamageEnemy`, right after the guard line, add `amount *= SchoolMultiplier(e, category, shot);` with this method below it:

```csharp
        /// <summary>
        /// School resistance (D92, owner rule): an enemy shrugs off its own school's magic (x0.75)
        /// and takes x1.33 from any other school. "School" is the attack's SourceCategory, which a
        /// returned payload keeps from its caster, so a bolt caught from ANY Acolyte is Acolyte
        /// school. A riposte keeps the default Player category, so it is "another source" and
        /// parry stays the Pursuer counter (R6, owner). Upgrade damage (Orbit, Parting Gift) is
        /// neutral: the rule is about turning enemy magic on enemies, and upgrades are not enemy
        /// magic (R5, owner). The boss is exempt (R7): in its fight its own returned shots are
        /// nearly the only damage there is, so resistance would only lengthen it.
        /// Applied here, the single entry point, so the DamageEvent, kill check and score all see
        /// the same scaled number.
        /// </summary>
        internal float SchoolMultiplier(EnemyActor victim, DamageCategory category, in AttackSnapshot shot)
        {
            if (victim.IsBoss || category == DamageCategory.Orbit || category == DamageCategory.PartingGift) return 1f;
            var c = Config.combat;
            return shot.SourceCategory == victim.Category ? c.ownSchoolDamage : c.otherSchoolDamage;
        }
```

`ArenaSim.Parry.cs` is **not** touched (R6): the riposte must keep its default `SourceCategory`.

Confirm that `EnemyActor` exposes `Category` and `IsBoss` (both are used elsewhere in `ArenaSim`).

- [ ] **Step 4: Run the full suite**

Run: `bash .superpowers/uc.sh`, then `timeout 900 bash .superpowers/rt.sh editor`
Expected: `ReworkTests` pass. Existing tests that assert exact damage numbers may now fail: `Phase4Tests`, `ParryTests` riposte damage, Orbit `hp - 3f`, `AchievementTests`, `ScoreTests`, `ShortRunTests`. For each one, multiply the expected damage by the multiplier the hit actually gets. For example, a riposte on a Pursuer is ×1.33, and an Acolyte bolt back on an Acolyte is ×0.75. Orbit and Parting Gift tests should **not** change (×1.0). Read the multiplier from `sim.Config.combat` in the test rather than writing the literal, so a retune can't break the test silently. **Kill counts may change** where a hit that used to kill no longer does (×0.75). In that case, raise the hit count in the test, and say so in the ledger.

Expected after fixes: all pass.

- [ ] **Step 5: Decision and commit**

```
| D92 | Rework | School resistance. Every enemy except the boss takes x0.75 damage from its own school (the attack's SourceCategory equals the victim's category) and x1.33 from any other school. Ripostes count as another source, so parry stays the Pursuer counter (R6, owner). Upgrade damage (Orbit, Parting Gift) is neutral, x1.0 (R5, owner). Applied once inside DamageEnemy, so events, kills and score see the scaled amount. The boss is exempt (R7). Tuning: combat.ownSchoolDamage / otherSchoolDamage | Owner decision 3 Oct 2026. It gives the pocketed hex a target ("a rocket for the Acolyte, a bolt for the Siege") | Return Policy (kill a caster with its own shot) gets harder at x0.75 but stays possible |
```

```bash
git add Assets/Game Docs
git commit -q -F - <<'EOF'
Make enemies resist their own school and take more from others

DamageEnemy scales every hit by 0.75 when the attack's source category matches
the victim's and by 1.33 otherwise. Ripostes count as another source, upgrade
damage is neutral and the boss is exempt (D92). Tests that pinned exact damage now read the
multipliers from config.
EOF
```

---

## Task 5: Power curve and Overcharge (sim)

**Files:**
- Create: `Assets/Game/Scripts/Combat/PowerCurve.cs`
- Modify: `Assets/Game/Scripts/Data/PlayerTuning.cs` (`CaptureTuning`: remove `powerPerSecond`, add four fields)
- Modify: `Assets/Game/Data/GameConfig.asset` (`capture:` block)
- Modify: `Assets/Game/Scripts/Player/PlayerStats.cs` (`Power` replaces `PowerPerSecond`)
- Modify: `Assets/Game/Scripts/Combat/CapturedPacket.cs` (`Power`/`FirePower` signatures)
- Modify: `Assets/Game/Scripts/Combat/AttackSnapshot.cs` (`Overcharged` on both structs)
- Modify: `Assets/Game/Scripts/Runs/ArenaSim.Capture.cs` (`TryReleaseEarly`, `ReleaseService.Release`)
- Modify: `Assets/Game/Scripts/Runs/ArenaSim.Upgrades.cs` (Overflow `FirePower`)
- Modify: `Assets/Game/Scripts/Runs/ArenaSim.Enemies.cs` (`DamageEvent.Overcharged`)
- Modify: `Assets/Game/Scripts/Runs/SimEvents.Combat.cs` (`PacketOvercharged`)
- Modify: `Assets/Game/Scripts/Runs/RunScore.cs` (`Overcharges`, `FirePower` call, summary field)
- Modify: `Assets/Game/Scripts/UI/PacketIndicator.cs` (`FirePower` call)
- Modify: `Assets/Game/Tests/EditMode/{ReworkTests,StyleTests,UpgradeTests}.cs`
- Modify: `Docs/DECISIONS.md` (D93)

**Interfaces:**
- Produces:
  - `struct PowerCurve { float PeakPower, Exponent, OverchargeWindow, OverchargeMultiplier; float Evaluate(double decayed, float lifetime); bool IsOvercharged(double decayed, float lifetime); }`
  - `PlayerStats.Power : PowerCurve`
  - `CapturedPacket.Power(PowerCurve) : float`, `CapturedPacket.FirePower(PowerCurve) : float`, `CapturedPacket.IsOvercharged(PowerCurve) : bool`
  - `AttackSnapshot.Overcharged`, `DamageEvent.Overcharged`
  - `SimEvents.PacketOvercharged : Action<CapturedPacket, int root>`
  - `RunScore.Overcharges : int`, `RunSummary.Overcharges : int`

- [ ] **Step 1: Write the failing tests** (append to `ReworkTests`)

```csharp
        // ---- Power curve and Overcharge (D93) ---------------------------------------------------

        static PowerCurve Curve => new PowerCurve(1.8f, 2f, 0.35f, 1.5f);

        [TestCase(0.0, 1.0f)]
        [TestCase(2.65, 1.8f)]
        [TestCase(1.325, 1.2f)]   // halfway through the ramp: 1 + 0.8 * 0.25
        public void Curve_RampIsEasedUpToTheZone(double decayed, float expected)
        {
            Assert.AreEqual(expected, Curve.Evaluate(decayed, 3f), 1e-3f);
        }

        [TestCase(2.649, false)]
        [TestCase(2.65, true)]
        [TestCase(2.99, true)]
        [TestCase(3.0, false)]   // expiry is a backfire, never a crit
        public void Curve_ZoneIsTheLast035Seconds(double decayed, bool zone)
        {
            Assert.AreEqual(zone, Curve.IsOvercharged(decayed, 3f));
        }

        [Test]
        public void Curve_ZoneMultipliesThePeak()
        {
            Assert.AreEqual(2.7f, Curve.Evaluate(2.8, 3f), 1e-3f);
        }

        static CapturedPacket Held(ArenaSim sim, double decayed)
        {
            var p = TestSims.Seed(sim.Packets, sim.Ids.Next(), 0, sim.Clock.Now - 5.0, 3f, 12);
            p.Payloads.Add(AttackSnapshot.From(sim.Attacks.Get(AttackIds.Bolt), 999, sim.Ids.Next(), 0f));
            p.CapacityUsed = 1;
            p.Status = PacketStatus.Stored;
            p.DecayedTime = decayed;
            return p;
        }

        [Test]
        public void Overcharge_ReleaseInTheZone_IsGoldAndRaisesTheEventOnce()
        {
            var sim = Sim();
            var p = Held(sim, 2.75);
            int overcharges = 0;
            sim.Events.PacketOvercharged += (_, __) => overcharges++;
            var spawned = new List<ProjectileActor>();
            sim.Events.ProjectileSpawned += s => spawned.Add(s);
            sim.Tick(Hold.WithRelease(), Dt);
            Assert.AreEqual(1, overcharges);
            Assert.AreEqual(1, spawned.Count);
            Assert.IsTrue(spawned[0].Shot.Overcharged);
            Assert.AreEqual(sim.Stats.Power.Evaluate(p.DecayedTime, 3f), spawned[0].PowerMultiplier, 1e-3f);
            Assert.AreEqual(1, sim.Score.Overcharges);
        }

        [Test]
        public void Overcharge_ReleaseBeforeTheZone_IsNot()
        {
            var sim = Sim();
            Held(sim, 1.0);
            int overcharges = 0;
            sim.Events.PacketOvercharged += (_, __) => overcharges++;
            sim.Tick(Hold.WithRelease(), Dt);
            Assert.AreEqual(0, overcharges);
        }

        [Test]
        public void Overcharge_TooLate_StillBackfires()
        {
            var sim = Sim();
            Held(sim, 2.995);
            int backfires = 0, released = 0;
            sim.Events.PacketBackfired += _ => backfires++;
            sim.Events.PacketReleased += (_, __) => released++;
            sim.Tick(Hold.WithRelease(), Dt);   // expiry runs before input (D17)
            Assert.AreEqual(1, backfires);
            Assert.AreEqual(0, released);
        }

        [Test]
        public void Overcharge_PocketedInTheZone_StaysOvercharged()
        {
            // Review Focus 3 / R10: only the selected hex decays, so a hex pocketed inside the
            // zone is a banked crit until it is selected again.
            var sim = Sim();
            var p = Held(sim, 2.7);
            TestSims.Pocket(sim);
            Run(sim, 120);
            Assert.IsTrue(p.IsOvercharged(sim.Stats.Power));
            TestSims.Pocket(sim);
            int overcharges = 0;
            sim.Events.PacketOvercharged += (_, __) => overcharges++;
            sim.Tick(Hold.WithRelease(), Dt);
            Assert.AreEqual(1, overcharges);
        }

        [Test]
        public void Overcharge_DamageEventCarriesTheFlag()
        {
            var sim = Sim();
            var e = Spawned(sim, ActorCategory.SiegeFamiliar);
            var shot = From(ActorCategory.Acolyte);
            shot.Overcharged = true;
            bool flagged = false;
            sim.Events.EnemyDamaged += (_, d) => flagged = d.Overcharged;
            sim.DamageEnemy(e, 1f, DamageCategory.ReturnedProjectile, shot, 1);
            Assert.IsTrue(flagged);
        }
```

- [ ] **Step 2: Run them to confirm the failure**

Run: `bash .superpowers/uc.sh`
Expected: `failed: True`. The errors name `PowerCurve`, `Overcharged`, `PacketOvercharged` and `Overcharges`.

- [ ] **Step 3: Create `PowerCurve.cs`**

```csharp
using UnityEngine;

namespace BorrowedHex.Combat
{
    /// <summary>
    /// D93: how much a hex hits for, by how long it has decayed. Replaces the old linear
    /// 1 + 0.35 * t (1.00 -> 2.05), which had no sweet spot: holding 2.4 s or 2.9 s felt the same.
    ///
    /// Two parts:
    ///  - an eased ramp from 1 to PeakPower that ends where the Overcharge zone starts. Convex
    ///    (Exponent > 1), so most of the gain comes late: an early fire is honest but weak.
    ///  - the Overcharge zone, the last OverchargeWindow seconds before expiry: power x
    ///    OverchargeMultiplier, a "perfect release". One tick too late is still a backfire,
    ///    because expiry is resolved before input (D17).
    /// A pure value type: the sim, the HUD and the tests all evaluate the same numbers.
    /// </summary>
    public readonly struct PowerCurve
    {
        public readonly float PeakPower;
        public readonly float Exponent;
        public readonly float OverchargeWindow;
        public readonly float OverchargeMultiplier;

        public PowerCurve(float peakPower, float exponent, float overchargeWindow, float overchargeMultiplier)
        {
            PeakPower = peakPower;
            Exponent = exponent;
            OverchargeWindow = overchargeWindow;
            OverchargeMultiplier = overchargeMultiplier;
        }

        public bool IsOvercharged(double decayed, float lifetime) =>
            OverchargeWindow > 0f && decayed >= lifetime - OverchargeWindow - 1e-9 && decayed < lifetime;

        public float Evaluate(double decayed, float lifetime)
        {
            // The ramp reaches its peak exactly where the zone begins, so the zone is a clean
            // step up, readable as "now!", not a slope the player has to estimate.
            float rampEnd = Mathf.Max(1e-4f, lifetime - OverchargeWindow);
            float u = Mathf.Clamp01((float)(decayed / rampEnd));
            float p = 1f + (PeakPower - 1f) * Mathf.Pow(u, Exponent);
            return IsOvercharged(decayed, lifetime) ? p * OverchargeMultiplier : p;
        }
    }
}
```

- [ ] **Step 4: Tuning, stats, packet**

`CaptureTuning`: delete the `powerPerSecond` field and its tooltip, and add:

```csharp
        [Tooltip("D93: power reached at the START of the Overcharge zone (placeholder, tune by play).")]
        public float peakPower = 1.8f;
        [Tooltip("D93: ramp shape; 1 = linear, 2 = most of the gain comes late.")]
        public float powerCurveExponent = 2f;
        [Tooltip("D93: the last N seconds before expiry are the Overcharge (perfect release) zone.")]
        public float overchargeWindow = 0.35f;
        [Tooltip("D93: power multiplier inside the Overcharge zone.")]
        public float overchargeMultiplier = 1.5f;
```

`GameConfig.asset`: replace the line `    powerPerSecond: 0.35` with:

```
    peakPower: 1.8
    powerCurveExponent: 2
    overchargeWindow: 0.35
    overchargeMultiplier: 1.5
```

`PlayerStats`: replace `public float PowerPerSecond;` with:

```csharp
        /// <summary>D93: decay-to-power curve with the Overcharge zone (replaces PowerPerSecond).</summary>
        public BorrowedHex.Combat.PowerCurve Power;
```

In `FromConfig`, replace `PowerPerSecond = c.powerPerSecond,` with `Power = new BorrowedHex.Combat.PowerCurve(c.peakPower, c.powerCurveExponent, c.overchargeWindow, c.overchargeMultiplier),`. `Clone()` is a `MemberwiseClone`, so the struct copies correctly.

`CapturedPacket`: replace `Power` and `FirePower`:

```csharp
        public float Power(PowerCurve curve) => curve.Evaluate(DecayedTime, Lifetime);
        /// <summary>D93: inside the last OverchargeWindow seconds of its own (selected) decay.</summary>
        public bool IsOvercharged(PowerCurve curve) => curve.IsOvercharged(DecayedTime, Lifetime);
```

```csharp
        public float FirePower(PowerCurve curve) => Power(curve) * PowerScale;
```

`AttackSnapshot.cs`: in `AttackSnapshot`, next to `Perfect`, add:

```csharp
        /// <summary>D93: fired from an overcharged release (gold in the view). Set on the copy at release.</summary>
        public bool Overcharged;
```

In `DamageEvent`, add `public bool Overcharged;`. In `ArenaSim.Enemies.DamageEnemy`, add `Overcharged = shot.Overcharged,` to the `DamageEvent` initializer.

- [ ] **Step 5: Release path**

`ArenaSim.Capture.cs`, `TryReleaseEarly`: `packet.FirePower(Stats.Power) * QuickDrawMultiplier(Clock.Now)`. Quick Draw stacks with Overcharge (R9).

`ArenaSim.Upgrades.cs`, Overflow: `selected.FirePower(Stats.Power)` (R9: the zone counts).

`SimEvents.Combat.cs`:

```csharp
        /// <summary>D93: a release fired inside the Overcharge zone (a perfect release): packet, root release ID.</summary>
        public event Action<CapturedPacket, int> PacketOvercharged;
        internal void RaisePacketOvercharged(CapturedPacket p, int root) => PacketOvercharged?.Invoke(p, root);
```

`ReleaseService.Release`: after `var volley = BuildVolley(sim, packet.Payloads);`, add:

```csharp
            // D93: whether this release is a perfect one is read from the hex's state NOW
            // (its own decayed time), so Overflow in the zone counts too (R9). The flag goes on
            // the volley's copies, so the echo (which reuses the volley) is gold as well.
            bool overcharged = packet.IsOvercharged(sim.Stats.Power);
            if (overcharged)
                for (int i = 0; i < volley.Count; i++) { var r = volley[i]; r.Shot.Overcharged = true; volley[i] = r; }
```

After `sim.Events.RaisePacketReleased(packet, root);`, add `if (overcharged) sim.Events.RaisePacketOvercharged(packet, root);`.

`RunScore.cs`: add `public int Overcharges { get; private set; }`. Subscribe in the constructor (alongside the existing `PacketReleased` subscription): `sim.Events.PacketOvercharged += (_, __) => Overcharges++;`. In `OnPacketReleased`, change to `p.FirePower(sim.Stats.Power)`. In `RunSummary`, add `public readonly int Overcharges;` and `Overcharges = s.Overcharges;` in the constructor.

`PacketIndicator.cs`: `pk.FirePower(sim.Stats.Power)`. For the selected packet with `pk.IsOvercharged(sim.Stats.Power)`, set the state to `"OVERCHARGE — FIRE!"` and the fill to gold `new Color(1f, 0.84f, 0.2f)`. A frozen overcharged packet shows `"FROZEN  OVERCHARGED"` (R10).

- [ ] **Step 6: Fix callers and existing tests**

Run: `bash .superpowers/uc.sh`
Expected: compile errors only in tests, at `StyleTests.cs:90` and `UpgradeTests.cs:375`. Fix them:
- `StyleTests:90`: `Assert.AreEqual(b.Power.Evaluate(1.0, 3f), s.Power.Evaluate(1.0, 3f), 1e-6f, style.Id);`
- `UpgradeTests:375`: `float power = selected.Power(sim.Stats.Power) * 1.25f;`

Run: `timeout 900 bash .superpowers/rt.sh editor`
Expected: all pass. Tests that asserted a specific old linear power value fail. Recompute them from `sim.Stats.Power.Evaluate(...)` rather than writing a literal.

- [ ] **Step 7: Decision and commit**

```
| D93 | Rework | Power curve and Overcharge. Power = 1 + (peakPower - 1) * u^exponent, with u = decayed / (lifetime - overchargeWindow), clamped. Inside the last overchargeWindow seconds of a hex's own decay, power x overchargeMultiplier: a perfect release (PacketOvercharged, Overcharges stat, gold shots). Placeholder values peakPower 1.8, exponent 2, window 0.35 s, multiplier 1.5 (≈1.02 at 0.4 s, 1.26 at 1.5 s, 1.8 at 2.65 s, 2.7 in the zone; the old line was 1.0 to 2.05). Overflow in the zone and Quick Draw both stack (R9). A pocketed hex frozen in the zone stays overcharged (R10). Replaces capture.powerPerSecond | Review point 1 approved by the owner 3 Oct 2026: the hold decision becomes a skill shot | Values are untested by play; early fires are weaker than before |
```

```bash
git add Assets/Game Docs
git commit -q -F - <<'EOF'
Replace linear hex power with an eased curve and an Overcharge zone

Power now eases from 1.0 to a peak at the start of the last 0.35 s of a hex's
decay, and is multiplied by 1.5 inside that zone. A release there is a perfect
release: it raises PacketOvercharged, is counted in the run stats, and its
shots (and echo) carry the Overcharged flag for the view (D93).
EOF
```

---

## Task 6: Overcharge feedback (presentation)

**Files:**
- Create: `Assets/Game/Scripts/Presentation/CameraShake.cs`
- Modify: `Assets/Game/Scripts/Presentation/GameRoot.cs` (hit-stop, shake wiring)
- Modify: `Assets/Game/Scripts/Presentation/ArenaView.cs` (gold shots, gold numbers, OVERCHARGE label)
- Modify: `Assets/Game/Tests/PlayMode/GameRootPlayModeTests.cs`
- Modify: `Docs/DECISIONS.md` (D94)

**Interfaces:**
- Consumes: `SimEvents.PacketOvercharged`, `AttackSnapshot.Overcharged`, `DamageEvent.Overcharged` (Task 5).
- Produces:
  - `CameraShake.Kick(float amplitude, float duration)`
  - `GameRoot.HitStopActive : bool` (for the test)

- [ ] **Step 1: Write the failing PlayMode test**

```csharp
        [UnityTest]
        public IEnumerator Overcharge_FreezesTheFrameBriefly_WithoutLosingGameplayTime()
        {
            yield return StartShortRun();
            // Hold a hex deep in the zone and fire it through the real frame loop.
            var sim = root.Sim;
            var p = sim.Packets.CreateInSlot(sim.Packets.SelectedSlot, sim.Ids.Next(), 0, sim.Clock.Now - 5.0, 3f, 12);
            p.Payloads.Add(BorrowedHex.Combat.AttackSnapshot.From(sim.Attacks.Get(BorrowedHex.Core.AttackIds.Bolt), 999, sim.Ids.Next(), 0f));
            p.CapacityUsed = 1;
            p.Status = BorrowedHex.Combat.PacketStatus.Stored;
            p.DecayedTime = 2.75;
            sim.Events.PacketOvercharged += (_, __) => { };
            root.DebugFireSelected();
            yield return null;
            Assert.IsTrue(root.HitStopActive || BorrowedHex.UI.DisplayOptions.ReduceFlashes);
            double frozenAt = sim.Clock.Now;
            yield return null;
            if (root.HitStopActive) Assert.AreEqual(frozenAt, sim.Clock.Now, 1e-9, "no steps during hit-stop");
            yield return new WaitForSecondsRealtime(0.2f);
            Assert.IsFalse(root.HitStopActive);
            Assert.Greater(sim.Clock.Now, frozenAt, "gameplay resumes");
        }
```

`DebugFireSelected()` is a GameRoot test hook that queues one release command for the next step. Find `DisplayOptions` with `grep -rn "class DisplayOptions" Assets/Game/Scripts` and use its namespace.

- [ ] **Step 2: Run it to confirm the failure**

Run: `bash .superpowers/uc.sh`
Expected: `failed: True` (`HitStopActive`, `DebugFireSelected` and `CameraShake` are missing).

- [ ] **Step 3: Implement `CameraShake.cs`**

```csharp
using UnityEngine;

namespace BorrowedHex.Presentation
{
    /// <summary>
    /// D94: a short, decaying positional shake on the camera, in unscaled time so it plays
    /// through hit-stop. The base position is captured when a shake STARTS, not in Awake, so a
    /// camera rig that positions the camera after boot is respected. Pure presentation: the sim
    /// never reads the camera.
    /// </summary>
    public sealed class CameraShake : MonoBehaviour
    {
        Vector3 basePos;
        float until, amplitude, duration;
        bool shaking;

        public void Kick(float amp, float dur)
        {
            if (!shaking) basePos = transform.localPosition;
            shaking = true;
            amplitude = Mathf.Max(amp, amplitude * Remaining01());
            duration = Mathf.Max(0.01f, dur);
            until = Time.unscaledTime + duration;
        }

        float Remaining01() => shaking ? Mathf.Clamp01((until - Time.unscaledTime) / duration) : 0f;

        void LateUpdate()
        {
            if (!shaking) return;
            float k = Remaining01();
            if (k <= 0f) { transform.localPosition = basePos; shaking = false; amplitude = 0f; return; }
            // Quadratic falloff: a hard kick that settles fast reads as impact, not as wobble.
            Vector2 j = Random.insideUnitCircle * amplitude * k * k;
            transform.localPosition = basePos + new Vector3(j.x, j.y, 0f);
        }
    }
}
```

- [ ] **Step 4: Hit-stop and wiring in `GameRoot`**

Fields and hooks:

```csharp
        // D94: Overcharge freeze-frame. Real (unscaled) time during which no sim step runs. The
        // sim clock simply does not advance, so nothing about gameplay changes: it is the same
        // run, shown with a 70 ms beat on the perfect release.
        const float HitStopSeconds = 0.07f;
        float hitStopUntil;
        CameraShake shake;
        public bool HitStopActive => Time.unscaledTime < hitStopUntil;

        void OnOvercharged(BorrowedHex.Combat.CapturedPacket p, int root)
        {
            // R12: both effects honour Reduce flashes, the existing motion/flash accessibility switch.
            if (DisplayOptions.ReduceFlashes) return;
            hitStopUntil = Time.unscaledTime + HitStopSeconds;
            if (shake == null && cam != null) shake = cam.GetComponent<CameraShake>() ?? cam.gameObject.AddComponent<CameraShake>();
            if (shake != null) shake.Kick(0.18f, 0.25f);
        }

        bool debugFire;
        /// <summary>PlayMode test hook: the next sim step carries a release command.</summary>
        public void DebugFireSelected() => debugFire = true;
```

In `BeginRun`, after `Sim.Events.RunEnded += OnRunEnded;`, add `Sim.Events.PacketOvercharged += OnOvercharged;` and `hitStopUntil = 0f;`. The old sim is discarded with its subscriptions, so nothing leaks.

In `Update`, right after the `if (Sim.Clock.IsPaused) { … }` block:

```csharp
            if (HitStopActive)
            {
                // Same rule as a pause: real time spent frozen is dropped, never fast-forwarded.
                accumulator = 0f;
                View.Render(1f);
                return;
            }
```

In the step loop, after `var cmd = Input.ConsumeCommand(cam);`, add `if (debugFire) { cmd = cmd.WithRelease(); debugFire = false; }`.

- [ ] **Step 5: Gold in `ArenaView`**

- Add `static readonly Color OverchargeGold = new Color(1f, 0.84f, 0.2f);`.
- In `RenderProjectiles`, put this first in the colour chain: `Color c = p.Shot.Overcharged ? OverchargeGold : p.Shot.Kind == AttackKind.Riposte ? …`.
- Generalise `SpawnTimeNumber` into `SpawnNumber(string text, Color color, Vector2 at)`, with the body unchanged apart from the text and colour. Keep `SpawnTimeNumber` as a call to it.
- Add these subscriptions:

```csharp
            // D94: the perfect release — a big gold label at the player and a wide gold ring.
            sim.Events.PacketOvercharged += (pk, _) =>
            {
                SpawnPop(sim.Player.Position, OverchargeGold, 0.4f, 2.2f, 0.3f);
                SpawnNumber($"OVERCHARGE x{pk.FirePower(sim.Stats.Power):0.0}", OverchargeGold, sim.Player.Position);
            };
            // R11: gold damage numbers, only for overcharged hits (the game shows no others).
            sim.Events.EnemyDamaged += (e, d) => { if (d.Overcharged) SpawnNumber($"{d.Amount:0.#}", OverchargeGold, e.Position); };
```

- [ ] **Step 6: Run the tests**

Run: `bash .superpowers/uc.sh`, then `timeout 900 bash .superpowers/rtp.sh` (Bash timeout 600000), then `timeout 900 bash .superpowers/rt.sh editor`
Expected: PlayMode 12/12, and EditMode all pass.

- [ ] **Step 7: Decision and commit**

```
| D94 | Rework | Overcharge feedback: 70 ms freeze-frame (no sim steps; the gameplay clock does not advance, so it changes nothing about the run), a 0.25 s decaying camera shake, gold returned shots, a gold "OVERCHARGE xN" label at the player and gold damage numbers on overcharged hits only (R11). Freeze and shake are off under Reduce flashes (R12). The HUD slot turns gold with "OVERCHARGE — FIRE!" while the selected hex is in the zone. No sound yet: the project has no audio pipeline | Review point 1: the perfect release must feel like an event | Sound is a TBD once audio exists |
```

```bash
git add Assets/Game Docs
git commit -q -F - <<'EOF'
Give the Overcharge release a freeze-frame, shake and gold feedback

A perfect release holds the frame for 70 ms without advancing the gameplay
clock, kicks a short camera shake, and turns its shots, a label at the player
and its damage numbers gold. Freeze and shake honour Reduce flashes (D94).
EOF
```

---

## Task 7: Kill chains

**Files:**
- Create: `Assets/Game/Scripts/Runs/KillChain.cs`
- Modify: `Assets/Game/Scripts/Data/CombatTuning.cs`, `Assets/Game/Data/GameConfig.asset`
- Modify: `Assets/Game/Scripts/Runs/ArenaSim.Run.cs` (`InitRun`, `RewardKillTime`)
- Modify: `Assets/Game/Scripts/Runs/SimEvents.Run.cs` (`KillChainChanged`)
- Modify: `Assets/Game/Scripts/Runs/RunScore.cs` (`BestChain` and summary)
- Modify: `Assets/Game/Scripts/UI/GameplayHud.cs` (chain label)
- Modify: `Assets/Game/Tests/EditMode/ReworkTests.cs`
- Modify: `Docs/DECISIONS.md` (D95)

**Interfaces:**
- Produces:
  - `KillChain(float window, float[] bonusSeconds)` with `Register(double now) : float`, `Length : int`, `ExpiresAt : double`, `Best : int`, `IsActive(double now) : bool`, `BonusFor(int length) : float`
  - `ArenaSim.Chain : KillChain`
  - `SimEvents.KillChainChanged : Action<int length, float bonusSeconds>`
  - `RunScore.BestChain`, `RunSummary.BestChain`

- [ ] **Step 1: Write the failing tests** (append to `ReworkTests`)

```csharp
        // ---- Kill chains (D95) -----------------------------------------------------------------

        [Test]
        public void Chain_BonusEscalates_ThenRepeatsTheLast()
        {
            var c = new KillChain(2.5f, new[] { 0f, 1f, 3f, 5f });
            Assert.AreEqual(0f, c.Register(0.0));
            Assert.AreEqual(1f, c.Register(1.0));
            Assert.AreEqual(3f, c.Register(2.0));
            Assert.AreEqual(5f, c.Register(3.0));
            Assert.AreEqual(5f, c.Register(4.0));
            Assert.AreEqual(5, c.Length);
        }

        [Test]
        public void Chain_BreaksWhenTheWindowPasses()
        {
            var c = new KillChain(2.5f, new[] { 0f, 1f, 3f, 5f });
            c.Register(0.0);
            c.Register(2.5);           // exactly on the edge still chains
            Assert.AreEqual(2, c.Length);
            Assert.AreEqual(0f, c.Register(5.1));
            Assert.AreEqual(1, c.Length);
            Assert.AreEqual(2, c.Best);
        }

        [Test]
        public void Chain_AcolyteKillsGiveThreeFourSix()
        {
            // The owner's example: +3, +4, +6 for consecutive Acolyte kills.
            var sim = Sim();
            sim.DebugSetLife(60.0);
            var gains = new List<float>();
            sim.Events.LifeClockChanged += (d, _) => { if (d > 0) gains.Add(d); };
            for (int i = 0; i < 3; i++)
            {
                var e = Spawned(sim, ActorCategory.Acolyte);
                sim.DamageEnemy(e, 1000f, DamageCategory.ReturnedProjectile, From(ActorCategory.SiegeFamiliar), 1);
                Run(sim, 30);
            }
            CollectionAssert.AreEqual(new[] { 3f, 4f, 6f }, gains);
            Assert.AreEqual(3, sim.Score.BestChain);
        }
```

`DebugSetLife(double)` is a public sandbox helper you add in Step 3. Sandbox runs start at the cap, so gains would otherwise clamp to zero.

- [ ] **Step 2: Run them to confirm the failure**

Run: `bash .superpowers/uc.sh`
Expected: `failed: True` (`KillChain`, `DebugSetLife` and `BestChain` are missing).

- [ ] **Step 3: Implement**

`KillChain.cs`:

```csharp
namespace BorrowedHex.Runs
{
    /// <summary>
    /// D95: consecutive kills inside a short window buy back escalating time. Pure C#, driven by
    /// the gameplay clock, so pauses and choices freeze the window. Several kills in one tick
    /// each count, so a rocket that wipes three enemies is a chain of three.
    /// </summary>
    public sealed class KillChain
    {
        readonly float window;
        readonly float[] bonus;

        public int Length { get; private set; }
        public int Best { get; private set; }
        public double ExpiresAt { get; private set; }

        public KillChain(float window, float[] bonusSeconds)
        {
            this.window = window;
            bonus = bonusSeconds ?? new float[0];
        }

        public bool IsActive(double now) => Length > 0 && now <= ExpiresAt + 1e-9;

        /// <summary>Count a kill at <paramref name="now"/>; returns the bonus seconds it earns.</summary>
        public float Register(double now)
        {
            Length = IsActive(now) ? Length + 1 : 1;
            ExpiresAt = now + window;
            if (Length > Best) Best = Length;
            return BonusFor(Length);
        }

        /// <summary>Bonus for the Nth kill of a chain; past the table, the last entry repeats.</summary>
        public float BonusFor(int length)
        {
            if (length <= 0 || bonus.Length == 0) return 0f;
            return bonus[System.Math.Min(length, bonus.Length) - 1];
        }
    }
}
```

`CombatTuning.cs`, after the school fields:

```csharp
        [Tooltip("D95: seconds after a kill during which the next kill extends the chain (placeholder).")]
        public float chainWindow = 2.5f;
        [Tooltip("D95: extra seconds for the 1st, 2nd, 3rd... kill of a chain; the last entry repeats (placeholder).")]
        public float[] chainBonusSeconds = { 0f, 1f, 3f, 5f };
```

`GameConfig.asset`, `combat:` block, after `otherSchoolDamage: 1.33`:

```
    chainWindow: 2.5
    chainBonusSeconds:
    - 0
    - 1
    - 3
    - 5
```

`SimEvents.Run.cs`, following the file's pattern:

```csharp
        /// <summary>D95: a kill extended or started a chain: its length and the bonus seconds it earned.</summary>
        public event Action<int, float> KillChainChanged;
        internal void RaiseKillChainChanged(int length, float bonus) => KillChainChanged?.Invoke(length, bonus);
```

`ArenaSim.Run.cs`: add `public KillChain Chain { get; private set; }`. In `InitRun`, before the `EnemyKilled` subscription: `Chain = new KillChain(Config.combat.chainWindow, Config.combat.chainBonusSeconds);`. Rewrite `RewardKillTime`:

```csharp
        void RewardKillTime(EnemyActor enemy, Combat.DamageEvent damage)
        {
            // A kill cannot revive a clock already depleted by this tick's hit or ticking. Boss
            // kills neither extend nor break a chain (R15): their reward is the run's ending.
            if (Summary != null || !Player.Alive || lifeSeconds <= 0 || enemy.IsBoss) return;
            float chainBonus = Chain.Register(Clock.Now);
            Events.RaiseKillChainChanged(Chain.Length, chainBonus);
            float reward = Config.combat.For(enemy.Category).killSeconds * (enemy.Elite ? 1.5f : 1f) + chainBonus;
            double before = lifeSeconds;
            lifeSeconds = System.Math.Min(Stats.StartingSeconds, lifeSeconds + reward);
            float gained = (float)(lifeSeconds - before);
            Score.RecordTimeGained(gained);
            if (gained > 0) Events.RaiseLifeClockChanged(gained, enemy.Position);
        }

        /// <summary>Sandbox/test helper: set the life clock directly (capped like any gain).</summary>
        public void DebugSetLife(double seconds) => lifeSeconds = System.Math.Max(0, System.Math.Min(Stats.StartingSeconds, seconds));
```

`RunScore.cs`: add `public int BestChain => sim.Chain?.Best ?? 0;`. In `RunSummary`, add `public readonly int BestChain;` and `BestChain = s.BestChain;`.

- [ ] **Step 4: HUD counter**

In `GameplayHud.Build`, after the life bar:

```csharp
            // D95: the chain counter sits right of the life bar, because chains are life.
            chainLabel = Ui.Label("Chain", root, "", 22, TextAnchor.MiddleLeft);
            Ui.Place(chainLabel.rectTransform, new Vector2(0.5f, 1), new Vector2(26 + 200 + 95, -38), new Vector2(170, 30));
            chainLabel.color = Ui.Accent;
```

Add the field `Text chainLabel;`. In `LateUpdate`, after the heart beat:

```csharp
            // Shown from the 2nd kill (a "chain" of one is just a kill), fading out as the window
            // runs down so the player can see how long they have to keep it going.
            var chain = sim.Chain;
            bool showChain = chain != null && chain.Length >= 2 && chain.IsActive(now);
            chainLabel.text = showChain ? $"CHAIN x{chain.Length}  +{chain.BonusFor(chain.Length + 1):0.#} next" : "";
            if (showChain)
            {
                float left = Mathf.Clamp01((float)((chain.ExpiresAt - now) / Mathf.Max(0.01f, sim.Config.combat.chainWindow)));
                chainLabel.color = new Color(Ui.Accent.r, Ui.Accent.g, Ui.Accent.b, 0.35f + 0.65f * left);
            }
```

- [ ] **Step 5: Run the tests**

Run: `bash .superpowers/uc.sh`, then `timeout 900 bash .superpowers/rt.sh editor`
Expected: all pass. A `ScoreTests` or `ShortRunTests` test that asserts exact `SecondsGained` over consecutive kills may fail. Add the chain bonus to its expectation via `sim.Chain.BonusFor(n)`.

- [ ] **Step 6: Decision and commit**

```
| D95 | Rework | Kill chains. A kill within combat.chainWindow (2.5 s) of the previous one extends the chain; the Nth kill adds combat.chainBonusSeconds[N-1] (0, 1, 3, 5, last repeats) on top of its normal kill seconds: Acolytes give +3, +4, +6, +8. Same-tick kills each count. Boss kills neither extend nor break it; hits do not break it (R15). Capped by the run's starting seconds like every gain. HUD: "CHAIN xN +M next" right of the life bar from the 2nd kill, fading as the window runs out. Stat: BestChain | Review point 4 approved by the owner: aggression is the healing; a slow, careful player otherwise bleeds out | Values are placeholders |
```

```bash
git add Assets/Game Docs
git commit -q -F - <<'EOF'
Add kill chains that buy back escalating time

Consecutive kills within 2.5 s earn 0, +1, +3, +5 extra seconds on top of
their kill reward (Acolytes: +3, +4, +6, +8). A counter beside the life bar
shows the chain and fades as its window runs out; the best chain is kept in
the run summary (D95).
EOF
```

---

## Task 8: Upgrades for life: pay to add or rank up, swap for free, four at most

**Files:**
- Create: `Assets/Game/Scripts/Core/LifeDisplay.cs` (the ×10 display scale, D100)
- Modify: `Assets/Game/Scripts/Data/UpgradeTuning.cs` (`maxHeld`, `maxRank`, `takeCostByHeld`)
- Modify: `Assets/Game/Scripts/Runs/ArenaSim.Upgrades.cs` (the held list, `Has`/`RankOf`, offers that include rank-ups, `ChooseUpgrade(index, replace)`, the price, no more expiry)
- Modify: `Assets/Game/Scripts/Runs/SimEvents.Run.cs` (`UpgradePaid`)
- Modify: `Assets/Game/Scripts/Runs/RunScore.cs` (`SecondsSacrificed`, `UpgradesPaidFor`, `MostUpgradesHeld`, summary)
- Modify: `Assets/Game/Scripts/UI/RunFlowPanels.cs` (per-card Take/Swap/Rank-up buttons and prices, the swap-target row, "Continue without an upgrade", the held line, results lines in display units)
- Modify: `Assets/Game/Scripts/UI/GameplayHud.cs` (the upgrade label lists every held upgrade)
- Modify: `Assets/Game/Scripts/Presentation/ArenaView.cs` (`SpawnTimeNumber` in display units)
- Modify: `Assets/Game/Scripts/Presentation/GameRoot.cs` (wiring)
- Modify: `Assets/Game/Tests/EditMode/ReworkTests.cs`, `UpgradeTests.cs` (the expiry test becomes a persistence test), `IntegrationTests.cs:134`
- Modify: `Docs/DECISIONS.md` (D96, D100)

**The rule (owner, 3 Oct 2026, third revision; quoted in §0.1d):** upgrades are never discarded. At every choice you may **continue without an upgrade** for free. Taking a card costs a share of current life set by how many you hold **before** the pick:

| Held | New card: add it | New card: swap it in | Rank up a held card | Continue |
|---|---|---|---|---|
| 0 | **15%** → hold 1 | (nothing to swap) | (nothing to rank) | free |
| 1 | **25%** → hold 2 | **free**, replaces it | **25%** | free |
| 2 | **40%** → hold 3 | **free**, you pick which one goes | **40%** | free |
| 3 | **50%** → hold 4 (locked) | **free**, you pick which one goes | **50%** | free |
| 4 (locked) | no new cards | no swaps | **50%** | free |

My rulings on what the quote leaves open:
- **R13a. Nothing expires any more.** "You cannot discard your upgrades": what you hold stays until you swap it out. This supersedes GAME_PLAN §5's "everything is temporary" (pointer added in Task 12).
- **R13b. A rank-up card is just an offer of something you hold.** The pool is all 7 upgrades minus those already at `maxRank` (3, the length of every rank table). When a held upgrade is drawn, its card reads "Rank up" and costs the same as adding (owner). The new rank is `min(maxRank, max(heldRank + 1, offerRank))`. In endless a late cycle offers rank 3 cards, so a rank-up never gives less than a fresh card of the same cycle would.
- **R13c. At four held, offers are rank-ups only** (owner: "Rank-ups only, 50%"). The pool is the four held upgrades below `maxRank`. If none is left, there are no cards and the panel shows only Continue (and Retire in endless).
- **R13d. Swapping: you choose which card goes** (owner). With 1 held the target is obvious and there is no second step. With 2 or 3 held, "Swap" opens a row of your held cards; clicking one completes the swap, and Back cancels. The swapped-in card arrives at the offer's rank, and the swapped-out one is gone.
- **R13e. The price is paid on the click that confirms**, read from the panel at that moment. The clock is paused during the choice, so the price shown is the price paid.
- **R13f. The price is not a hit.** No invulnerability, no combo reset, it doesn't count toward "health lost to hits", and it doesn't break Untouchable. It can't kill you either: the most it takes is 50% of what you have.
- **R13g. Each upgrade keeps its own rank.** Every effect reads `RankOf(itsId)`.
- **R13h. Overflow beats Fusion** if both are held. It's checked first and frees the hand, so Fusion has nothing to merge.
- **R13i. Short mode has three choices, so it tops out at 3 held** (inferred from `shortMode.encounterCount = 3`). The lock at four is an endless goal.
- **R13j. `UpgradeTuning`:** `maxHeld = 4`, `maxRank = 3`, `takeCostByHeld = { 0.15, 0.25, 0.40, 0.50 }` (index = held count, capped at the last entry, so 4 held also costs 0.50).
- **R13k. The default focus on the panel is "Continue without an upgrade",** so a stray Enter never spends life.

**Life reads ×10 (D100, owner: "multiply all health and drain … by 10", display only):** the sim keeps counting seconds. Everything the player reads shows `seconds × 10`, rounded to a whole number: the gain/loss pops, the price on the cards, and the results lines. The bar itself has no number (D66), so it doesn't change. A hit (10 s) reads −100, and the start is 1800. This lands here because Task 8 is the first task to print a life amount; Tasks 9 and 10 use it.

**Interfaces:**
- Produces:
  - `BorrowedHex.Core.LifeDisplay`: `const float Scale = 10f`, `static int Points(double seconds)` (rounded), `static string Signed(double seconds)` ("+12" / "-100").
  - `ArenaSim.HeldUpgrades : IReadOnlyList<UpgradeOffer>` (0–4 entries, distinct ids, oldest first). It replaces `ActiveUpgrade`; `ExpiredUpgrade` is deleted.
  - `ArenaSim.UpgradesLocked : bool`, true once `HeldUpgrades.Count >= maxHeld`.
  - `ArenaSim.Has(UpgradeId)`, `ArenaSim.RankOf(UpgradeId) : int` (0 if not held). `RankOf` replaces the private `ActiveRank`.
  - `ArenaSim.IsRankUp(UpgradeOffer) : bool` (the offer's id is held).
  - `ArenaSim.TakeCost : float`, the life (seconds) any paid pick costs right now; `ArenaSim.TakeCostFraction : float`.
  - `ArenaSim.CanSwap : bool` (1–3 held, not locked).
  - `ArenaSim.ChooseUpgrade(int index, int replace = -1) : bool`. A rank-up card ranks up and pays. A new card with `replace >= 0` swaps it in for free. A new card with `replace < 0` adds it and pays (refused when locked).
  - `ArenaSim.ContinueFromUpgrade()` (exists) is the free "Continue without an upgrade".
  - `ArenaSim.DebugHold(params UpgradeId[])` (test/sandbox).
  - `SimEvents.UpgradePaid : Action<UpgradeOffer, float seconds>`.
  - `RunScore.SecondsSacrificed : float`, `RunScore.UpgradesPaidFor : int`, `RunScore.MostUpgradesHeld : int`, all mirrored on `RunSummary`.
  - `RunFlowPanels.Create(…)`: `onChoose` becomes `Action<int, int>` (card index, replace index or −1), plus a new `Action onContinue`.

- [ ] **Step 1: Write the failing tests** (append to `ReworkTests`; add `using System.Linq;`)

Use `P5.ClearEncounter(sim)` (it already exists; see `ShortRunTests.cs:49`). Add `static ArenaSim AtFirstChoice()`: a short-run sim (copy `ShortRunTests`' setup), then `P5.ClearEncounter(sim)`. For three and four held, use an endless sim as `static ArenaSim EndlessAtChoice()` (copy the setup and the first wave clear from `EndlessTests.cs:94-110`). `NextChoice(sim)` is `P5.ClearEncounter(sim)` in short mode, and `P5.TickWhile(sim, RunState.Combat, 40000)` in endless (as `EndlessTests` does).

```csharp
        // ---- Upgrades for life (D96) -----------------------------------------------------------

        static void NextChoice(ArenaSim sim)
        {
            if (sim.IsEndlessRun) P5.TickWhile(sim, RunState.Combat, 40000); else P5.ClearEncounter(sim);
            Assert.AreEqual(RunState.UpgradeChoice, sim.State);
            // Refill (Task 7's helper): three paid picks plus the waves between them would
            // otherwise drain a test run to death before the case under test is reached.
            sim.DebugSetLife(sim.Stats.StartingSeconds);
        }

        /// <summary>Index of the first offer that is NOT a rank-up (a new card), or -1.</summary>
        static int NewCard(ArenaSim sim)
        {
            for (int i = 0; i < sim.Offers.Count; i++) if (!sim.IsRankUp(sim.Offers[i])) return i;
            return -1;
        }

        [Test]
        public void Continue_IsFree_AndTakesNothing()
        {
            var sim = AtFirstChoice();
            float life = sim.LifeSeconds;
            Assert.IsTrue(sim.ContinueFromUpgrade());
            Assert.AreEqual(0, sim.HeldUpgrades.Count);
            Assert.AreEqual(life, sim.LifeSeconds, 1e-4f);
        }

        [TestCase(0, 0.15f)]
        [TestCase(1, 0.25f)]
        [TestCase(2, 0.40f)]
        [TestCase(3, 0.50f)]
        public void Adding_CostsMoreTheMoreYouHold(int held, float fraction)
        {
            var sim = EndlessAtChoice();
            for (int i = 0; i < held; i++) { Assert.IsTrue(sim.ChooseUpgrade(NewCard(sim))); NextChoice(sim); }
            Assert.AreEqual(held, sim.HeldUpgrades.Count);
            float life = sim.LifeSeconds;
            Assert.AreEqual(life * fraction, sim.TakeCost, 1e-3f);
            int hits = sim.Score.DamageTaken;
            Assert.IsTrue(sim.ChooseUpgrade(NewCard(sim)));
            Assert.AreEqual(life * (1f - fraction), sim.LifeSeconds, 1e-3f);
            Assert.AreEqual(held + 1, sim.HeldUpgrades.Count);
            Assert.AreEqual(hits, sim.Score.DamageTaken, "a price, not a hit (R13f)");
        }

        [Test]
        public void Upgrades_NeverExpire_BetweenEncounters()
        {
            var sim = AtFirstChoice();
            var pick = sim.Offers[0];
            Assert.IsTrue(sim.ChooseUpgrade(0));
            NextChoice(sim);
            Assert.IsTrue(sim.Has(pick.Id), "R13a: nothing is discarded");
            Assert.IsTrue(sim.ContinueFromUpgrade());
            Assert.IsTrue(sim.Has(pick.Id));
        }

        [Test]
        public void Swapping_IsFree_AndReplacesTheCardYouChose()
        {
            // R13d: with two held, the player names the one that goes.
            var sim = EndlessAtChoice();
            sim.ChooseUpgrade(NewCard(sim)); NextChoice(sim);
            sim.ChooseUpgrade(NewCard(sim)); NextChoice(sim);
            var keep = sim.HeldUpgrades[0].Id;
            var drop = sim.HeldUpgrades[1].Id;
            int i = NewCard(sim);
            var incoming = sim.Offers[i].Id;
            float life = sim.LifeSeconds;
            Assert.IsTrue(sim.CanSwap);
            Assert.IsTrue(sim.ChooseUpgrade(i, replace: 1));
            Assert.AreEqual(life, sim.LifeSeconds, 1e-4f, "swaps are free");
            Assert.IsTrue(sim.Has(keep));
            Assert.IsFalse(sim.Has(drop));
            Assert.IsTrue(sim.Has(incoming));
            Assert.AreEqual(2, sim.HeldUpgrades.Count);
        }

        [Test]
        public void RankUp_CostsTheSameAsAdding_AndRaisesOnlyThatCard()
        {
            var sim = Sim();
            sim.DebugHold(UpgradeId.PiercingReturn, UpgradeId.EchoVolley);
            sim.DebugOpenChoice(new UpgradeOffer(UpgradeId.PiercingReturn, 1), new UpgradeOffer(UpgradeId.Overflow, 1));
            Assert.IsTrue(sim.IsRankUp(sim.Offers[0]));
            float life = sim.LifeSeconds;
            Assert.AreEqual(life * 0.40f, sim.TakeCost, 1e-3f, "two held: 40% either way");
            Assert.IsTrue(sim.ChooseUpgrade(0));
            Assert.AreEqual(2, sim.RankOf(UpgradeId.PiercingReturn));
            Assert.AreEqual(1, sim.RankOf(UpgradeId.EchoVolley));
            Assert.AreEqual(2, sim.HeldUpgrades.Count, "a rank-up adds no card");
            Assert.AreEqual(life * 0.60f, sim.LifeSeconds, 1e-3f);
        }

        [Test]
        public void FourHeld_OffersOnlyRankUps_AtHalfYourLife()
        {
            // R13c (owner): locked at four, but rank-ups stay on the table for 50%.
            var sim = EndlessAtChoice();
            sim.ContinueFromUpgrade();
            sim.DebugHold(UpgradeId.Overflow, UpgradeId.Fusion, UpgradeId.EchoVolley, UpgradeId.HeavyOrbit);
            Assert.IsTrue(sim.UpgradesLocked);
            NextChoice(sim);
            Assert.IsTrue(sim.Offers.Count > 0);
            Assert.IsTrue(sim.Offers.All(sim.IsRankUp), "no new cards once locked");
            Assert.IsFalse(sim.CanSwap);
            Assert.AreEqual(sim.LifeSeconds * 0.50f, sim.TakeCost, 1e-3f);
            Assert.IsFalse(sim.ChooseUpgrade(0, replace: 0), "no swaps once locked");
            Assert.IsTrue(sim.ChooseUpgrade(0));
            Assert.AreEqual(4, sim.HeldUpgrades.Count);
        }

        [Test]
        public void MaxRankCards_AreNeverOffered()
        {
            var sim = AtFirstChoice();
            sim.ContinueFromUpgrade();
            sim.ForceUpgrade(UpgradeId.FinalSecond, 3);
            for (int k = 0; k < 2; k++)
            {
                NextChoice(sim);
                Assert.IsFalse(sim.Offers.Any(o => o.Id == UpgradeId.FinalSecond), "already at max rank");
                Assert.AreEqual(sim.Config.upgrades.offerCount, sim.Offers.Count);
                sim.ContinueFromUpgrade();
            }
        }

        [Test]
        public void HeldUpgrades_EachUseTheirOwnRank()
        {
            var sim = Sim();
            sim.ForceUpgrade(UpgradeId.PiercingReturn, 2);
            sim.DebugHold(UpgradeId.EchoVolley);
            Assert.AreEqual(2, sim.RankOf(UpgradeId.PiercingReturn));
            Assert.AreEqual(1, sim.RankOf(UpgradeId.EchoVolley));
            Assert.AreEqual(0, sim.RankOf(UpgradeId.Overflow));
        }

        [TestCase(10.0, 100)]
        [TestCase(0.1, 1)]
        [TestCase(0.04, 0)]
        [TestCase(180.0, 1800)]
        public void LifeDisplay_ShowsTenPointsPerSecond(double seconds, int points)
            => Assert.AreEqual(points, LifeDisplay.Points(seconds));
```

`DebugOpenChoice(offers)` is a sandbox hook you add in Step 4. It pauses, sets `State = UpgradeChoice` and replaces `Offers`.

- [ ] **Step 2: Run them to confirm the failure**

Run: `bash .superpowers/uc.sh`
Expected: `failed: True`. The errors name `HeldUpgrades`, `TakeCost`, `IsRankUp`, `CanSwap`, `UpgradesLocked`, `RankOf`, `DebugHold`, `DebugOpenChoice` and `LifeDisplay`.

- [ ] **Step 3: `LifeDisplay`, tuning and events**

`Core/LifeDisplay.cs`:

```csharp
using UnityEngine;

namespace BorrowedHex.Core
{
    /// <summary>
    /// D100 (owner): life is shown x10 so small gains (lifesteal, D99) read as whole numbers.
    /// DISPLAY ONLY: the sim, the tuning and every test keep counting seconds, so one constant
    /// changes what the player reads without touching balance. Every number the player sees
    /// about life goes through here, so the scale can never be applied twice or forgotten.
    /// </summary>
    public static class LifeDisplay
    {
        public const float Scale = 10f;

        public static int Points(double seconds) => Mathf.RoundToInt((float)(seconds * Scale));

        /// <summary>"+12" / "-100" for the floating pops; 0 reads as "0".</summary>
        public static string Signed(double seconds)
        {
            int p = Points(seconds);
            return p > 0 ? "+" + p : p.ToString();
        }
    }
}
```

`UpgradeTuning.cs`:

```csharp
        [Tooltip("D96: most upgrades held at once. At this many, only rank-ups are offered (owner value 4).")]
        public int maxHeld = 4;
        [Tooltip("D96: highest rank. Must match the length of the rank tables below (3).")]
        public int maxRank = 3;
        [Tooltip("D96: fraction of CURRENT life a paid pick (add or rank up) costs, by how many upgrades you hold BEFORE it (0, 1, 2, 3+). Owner values 0.15, 0.25, 0.40, 0.50; the last repeats.")]
        public float[] takeCostByHeld = { 0.15f, 0.25f, 0.40f, 0.50f };
```

`SimEvents.Run.cs`:

```csharp
        /// <summary>D96: life was paid for an upgrade (an add or a rank-up): the card and the seconds paid.</summary>
        public event Action<UpgradeOffer, float> UpgradePaid;
        internal void RaiseUpgradePaid(UpgradeOffer offer, float seconds) => UpgradePaid?.Invoke(offer, seconds);
```

- [ ] **Step 4: The held list in `ArenaSim.Upgrades.cs`**

Rewrite the class summary: "Encounter upgrades (section 5, reworked by D96). Upgrades are never discarded: at each choice the player may continue for free, swap a new card in for free, or pay a share of current life to add a card or rank one up. Four held locks the set; after that only rank-ups are offered. Every effect is read at the moment it acts (release, catch, orbit tick), never baked into a packet."

Replace `ActiveUpgrade`, `ExpiredUpgrade`, `Has` and `ActiveRank` with:

```csharp
        /// <summary>Every upgrade in force, oldest first (0..maxHeld, distinct ids).</summary>
        public IReadOnlyList<UpgradeOffer> HeldUpgrades => held;
        readonly List<UpgradeOffer> held = new List<UpgradeOffer>(4);

        /// <summary>D96: at maxHeld no new card can join; only rank-ups are offered (owner).</summary>
        public bool UpgradesLocked => held.Count >= Mathf.Max(1, UT.maxHeld);

        // Each effect reads the rank of ITS OWN upgrade, so a rank-3 endless pick and a rank-1
        // older upgrade never borrow each other's rank.
        public bool Has(UpgradeId id) => RankOf(id) > 0;

        public int RankOf(UpgradeId id)
        {
            for (int i = 0; i < held.Count; i++) if (held[i].Id == id) return held[i].Rank;
            return 0;
        }

        public bool IsRankUp(UpgradeOffer offer) => Has(offer.Id);

        /// <summary>Swaps need something to replace, and stop once the set is locked (owner).</summary>
        public bool CanSwap => State == RunState.UpgradeChoice && held.Count > 0 && !UpgradesLocked;

        /// <summary>
        /// D96: "You rely on borrowed power, and it comes with a price." What an add or a
        /// rank-up costs right now, as a share of CURRENT life set by how many you hold. The
        /// clock is paused during a choice, so the price on the panel is the price paid.
        /// </summary>
        public float TakeCostFraction
        {
            get
            {
                var t = UT.takeCostByHeld;
                return t == null || t.Length == 0 ? 0f : t[Mathf.Min(held.Count, t.Length - 1)];
            }
        }

        public float TakeCost => (float)(lifeSeconds * TakeCostFraction);
```

Replace every `ActiveRank` with `RankOf` and the id its `Has(...)` guard tests:
- `PerfectBonusNow` → `RankOf(UpgradeId.FinalSecond)`
- `UpgradePierce` → `RankOf(UpgradeId.PiercingReturn)`
- the echo → `RankOf(UpgradeId.EchoVolley)`
- `PartingGift` (both uses) → `RankOf(UpgradeId.PartingGift)`
- `TickOrbit` and `OrbitRadiusNow` → `RankOf(UpgradeId.HeavyOrbit)`

`OpenUpgradeChoice(int rank)`: nothing expires. Draw from upgrades that can still improve:

```csharp
        void OpenUpgradeChoice(int rank)
        {
            Offers.Clear();
            var pool = new List<UpgradeId>();
            foreach (var id in UpgradeInfo.Pool)
            {
                int r = RankOf(id);
                if (r >= UT.maxRank) continue;           // R13b: nothing left to rank up
                if (r == 0 && UpgradesLocked) continue;  // R13c: locked = rank-ups only
                pool.Add(id);
            }
            int n = Mathf.Min(UT.offerCount, pool.Count);
            for (int i = 0; i < n; i++)
            {
                int k = upgradeRandom.NextInt(0, pool.Count);
                var id = pool[k];
                // R13b: a rank-up is never weaker than a fresh card of this cycle would be.
                int r = Has(id) ? Mathf.Min(UT.maxRank, Mathf.Max(RankOf(id) + 1, rank)) : rank;
                Offers.Add(new UpgradeOffer(id, r));
                pool.RemoveAt(k);
            }
        }
```

The pool is unchanged when nothing is held, so first-choice offers for a seed stay the same. Later offers change only when something held is at max rank, or the set is locked. Re-pin any test that pins those, and note each one in the ledger.

Delete `ExpireUpgrade()`. `ForceUpgrade(id, rank)` keeps its meaning for existing tests: `held.Clear(); orbitNextHit.Clear(); held.Add(new UpgradeOffer(id, Mathf.Max(1, rank)));`. `ClearUpgrade()` becomes `{ held.Clear(); orbitNextHit.Clear(); }`.

`ChooseUpgrade`:

```csharp
        /// <summary>
        /// D96. Take offer <paramref name="index"/> and leave the choice:
        /// - a held upgrade's card ranks it up and costs TakeCost;
        /// - a new card with <paramref name="replace"/> >= 0 swaps out that held upgrade, free;
        /// - a new card otherwise joins the set and costs TakeCost (refused once locked).
        /// ContinueFromUpgrade is the free "take nothing".
        /// </summary>
        public bool ChooseUpgrade(int index, int replace = -1)
        {
            if (State != RunState.UpgradeChoice || index < 0 || index >= Offers.Count) return false;
            var pick = Offers[index];
            bool rankUp = IsRankUp(pick);
            bool swap = !rankUp && replace >= 0;
            if (swap && (!CanSwap || replace >= held.Count)) return false;
            if (!rankUp && !swap && UpgradesLocked) return false;
            float cost = swap ? 0f : TakeCost;   // read BEFORE the set changes
            if (!ContinueFromUpgrade()) return false;

            if (rankUp) held[held.FindIndex(o => o.Id == pick.Id)] = pick;
            else if (swap)
            {
                if (held[replace].Id == UpgradeId.HeavyOrbit) orbitNextHit.Clear();
                held[replace] = pick;
            }
            else held.Add(pick);

            if (cost > 0f)
            {
                // A price, not a hit (R13f): no invulnerability, no combo reset, not health lost
                // to hits. At most half of a positive number, so it can never kill.
                double before = lifeSeconds;
                lifeSeconds = System.Math.Max(0, lifeSeconds - cost);
                float paid = (float)(before - lifeSeconds);
                Score.RecordUpgradePaid(paid);
                Events.RaiseUpgradePaid(pick, paid);
                Events.RaiseLifeClockChanged(-paid, Player.Position);
            }
            Score.RecordHeld(held.Count);
            Events.RaiseUpgradeChosen(pick);
            return true;
        }

        /// <summary>Sandbox/test helper: add rank-1 upgrades on top of what is held, up to maxHeld.</summary>
        public void DebugHold(params UpgradeId[] ids)
        {
            foreach (var id in ids)
                if (!Has(id) && !UpgradesLocked) held.Add(new UpgradeOffer(id, 1));
        }

        /// <summary>Test helper: open a choice with exactly these offers.</summary>
        public void DebugOpenChoice(params UpgradeOffer[] offers)
        {
            Offers.Clear();
            Offers.AddRange(offers);
            Clock.SetPauseReason(PauseReason.UpgradeChoice, true);
            SetState(RunState.UpgradeChoice);
        }
```

`RaiseLifeClockChanged(-paid, …)` also flashes the HUD bar red, as a hit does. That's intended: the sacrifice should be felt. Check `GameplayHud.FlashClock`; if it only flashes on negative deltas, nothing else is needed.

**Before writing:** confirm `ContinueFromUpgrade` in the sandbox (no run) behaves as the tests need. `DebugOpenChoice` puts a sandbox sim into `UpgradeChoice`. `ContinueFromUpgrade` then increments `Encounter` and calls `RestorePillars`/`SetState(Combat)`; check that this is harmless in a sandbox. If not, give `RankUp_…` an endless sim like the other tests, and note it in the ledger.

In `TrySlotsFullUpgrade` (named `TryFullHandUpgrade` after Task 1), add `// R13h: with both held, Overflow wins; it frees the hand, so Fusion has nothing to merge.` above the Overflow branch.

- [ ] **Step 5: Score, HUD, pops and panel**

`RunScore.cs`:

```csharp
        public float SecondsSacrificed { get; private set; }
        public int UpgradesPaidFor { get; private set; }
        public int MostUpgradesHeld { get; private set; }
        internal void RecordUpgradePaid(float seconds) { SecondsSacrificed += seconds; UpgradesPaidFor++; }
        internal void RecordHeld(int count) { if (count > MostUpgradesHeld) MostUpgradesHeld = count; }
```

Add all three to `RunSummary` (fields plus constructor lines).

`ArenaView.SpawnTimeNumber`: `text.text = LifeDisplay.Signed(delta);`, and return early (before creating the GameObject) when `LifeDisplay.Points(delta) == 0`, so a gain too small to show doesn't leave a "0". Update the D66 comment: "No 's' suffix (D66), shown x10 (D100)".

`GameplayHud.LateUpdate`, replacing the `sim.ActiveUpgrade` block at line ~180:

```csharp
            // D96: up to four upgrades are held for the whole run; "LOCKED" once no new card can join.
            var held = sim.HeldUpgrades;
            if (held.Count == 0) upgradeLabel.text = "";
            else
            {
                var parts = new string[held.Count];
                for (int i = 0; i < held.Count; i++)
                    parts[i] = UpgradeInfo.Name(held[i].Id).ToUpperInvariant() + (held[i].Rank > 1 ? $" {held[i].Rank}" : "");
                upgradeLabel.text = (sim.UpgradesLocked ? "LOCKED: " : "UPGRADES: ") + string.Join("  ·  ", parts);
            }
```

Check that the label's rect fits four names. If it doesn't, widen it or drop the font a size, and note which you did in the ledger.

`RunFlowPanels` (read the current card-building code from line ~150 first):
- `onChoose` becomes `Action<int, int>`. Add `Action onContinue`.
- Each card keeps its name, description and rank. Its buttons follow the table above:
  - a rank-up card: one button, `Rank up to {rank} — {LifeDisplay.Points(sim.TakeCost)} life`, which calls `onChoose(i, -1)`.
  - a new card with 0 held: `Take — {cost} life`.
  - a new card with 1–3 held: `Add — {cost} life` and `Swap — free`.
  - a new card while locked: no buttons, with the caption "Locked: four held".
- **Swap target:**
  - With 1 held, "Swap — free" calls `onChoose(i, 0)` directly.
  - With 2 or 3 held, it hides the cards and shows "Replace which?", a button per held upgrade (`onChoose(i, j)`) and a Back button that restores the cards.
- Price text: `<color=#FF6B6B>{LifeDisplay.Points(sim.TakeCost)} life ({Mathf.RoundToInt(sim.TakeCostFraction*100)}%)</color>`. When adding would make four: `   <b>Your last card: after this, only rank-ups.</b>`.
- A **"Continue without an upgrade"** button under the cards, wired to `onContinue`. It is the default focus (R13k).
- A caption above the cards: "You rely on borrowed power — and it comes with a price."
- The old "Expired:" line becomes "Holding: " + the held names (with ranks), or empty when nothing is held.
- In `FillResults`, change `Time lost to hits: {s.DamageTaken}s   Time gained: {s.SecondsGained:0.#}s` to `Health lost to hits: {LifeDisplay.Points(s.DamageTaken)}   Health gained: {LifeDisplay.Points(s.SecondsGained)}`, and add `$"Health sacrificed: {LifeDisplay.Points(s.SecondsSacrificed)} ({s.UpgradesPaidFor} upgrade{(s.UpgradesPaidFor == 1 ? "" : "s")})   Most held: {s.MostUpgradesHeld}\n"`. Task 9 restructures these lines.

`TutorialPanel.cs:69` says "every hit costs time." It stays as is: it describes the drain, not a number.

`GameRoot.cs:64`:
`Flow = RunFlowPanels.Create(canvas, (i, r) => Sim.ChooseUpgrade(i, r), () => Sim.ContinueFromUpgrade(), Restart, ShowMainMenu, () => Sim.RetireRun());`
Put `onContinue` second in the factory's parameter list.

Existing tests:
- `UpgradeTests.cs:98-106`: the expiry test becomes `Upgrade_PersistsAfterItsEncounter`. Assert `sim.Has(pick.Id)` after the next clear, and delete the `ExpiredUpgrade` line.
- `UpgradeTests.cs:100,120` and `IntegrationTests.cs:134`: `sim.ActiveUpgrade.Value.Id` → `sim.HeldUpgrades.Last().Id` (an add) or `Assert.IsTrue(sim.Has(pick.Id))`.
- `IntegrationTests.cs:132`: the soak now pays life at every pick, and it could pick a swap. Leave `ChooseUpgrade(i)` (an add or a rank-up). If the soak now dies before finishing, choose `ContinueFromUpgrade()` every other choice, and ledger it.
- Any test that checks life right after `ChooseUpgrade` now sees the price. Fix each with `ContinueFromUpgrade()` where the pick doesn't matter, or by expecting the price. Ledger each one.

- [ ] **Step 6: Run the tests**

Run: `bash .superpowers/uc.sh`, then `timeout 900 bash .superpowers/rt.sh editor`, then `timeout 900 bash .superpowers/rtp.sh`
Expected: all pass, apart from the re-pinned seeds and adjusted tests, each one ledgered.

- [ ] **Step 7: Decisions and commit**

```
| D96 | Rework | Upgrades for life. Nothing expires. At each choice: continue free; swap a new card in for one you hold, free (you pick which goes; not once locked); or pay a share of CURRENT life to add a card or rank a held one up: 15% (0 held), 25% (1), 40% (2), 50% (3 or 4) (upgrades.takeCostByHeld). Four held (upgrades.maxHeld) locks the set: only rank-ups are offered. The pool skips upgrades at maxRank (3); a held upgrade drawn is a rank-up to max(rank+1, the cycle's offer rank). Each effect reads its own rank; Overflow beats Fusion. The price is paid on confirming, is not a hit and can never kill. Short mode has three choices, so it holds at most 3. Default focus is Continue. Stats: SecondsSacrificed, UpgradesPaidFor, MostUpgradesHeld. Supersedes section 5's "everything is temporary" | Owner decisions 3 Oct 2026 (§0.1c, §0.1d): "You cannot discard your upgrades… swap for free or keep your current one AND gain another"; "you can rank up cards, no matter how many upgrades you hold… the same 40% health sacrifice"; at four, rank-ups only for 50%; the player picks the swapped-out card | Four held upgrades is a large, untested power spike; existing tests that read life after a pick change |
| D100 | Rework | Life shows x10. Display only: the sim, tuning and tests keep seconds. LifeDisplay.Points rounds seconds x 10 for the pops, card prices and results lines; a pop that rounds to 0 is not shown | Owner decision 3 Oct 2026: "multiply all health and drain in the game by 10 or any other number to avoid small decimals"; display-only chosen by the owner | A tuning value in seconds no longer matches the number on screen; designers must remember the x10 |
```

```bash
git add Assets/Game Docs
git commit -q -F - <<'EOF'
Make upgrades last the run: pay life to add or rank up, swap for free

Upgrades no longer expire. At each choice the player can continue for free,
swap a new upgrade in for one they hold, or pay 15/25/40/50% of current life
(by how many they hold) to add one or rank one up. Four held locks the set to
rank-ups only. Life numbers on screen now read x10 so small changes show as
whole numbers (D96, D100).
EOF
```

---

## Task 9: Instant restart and a compact results screen

**Files:**
- Modify: `Assets/Game/Scripts/Player/PlayerInputReader.cs` (`Restart` action, `ConsumeRestart`)
- Modify: `Assets/Game/Scripts/Presentation/GameRoot.cs` (`HandleRestartKey`)
- Modify: `Assets/Game/Scripts/UI/RunFlowPanels.cs` (headline plus details, `[R]` label)
- Modify: `Assets/Game/Tests/PlayMode/GameRootPlayModeTests.cs`
- Modify: `Docs/DECISIONS.md` (D97)

**Interfaces:**
- Produces:
  - `PlayerInputReader.ConsumeRestart() : bool`
  - `GameRoot.HandleRestartKey(bool pressed) : bool`
  - `RunFlowPanels.AgainLabel : string` (for the test)

- [ ] **Step 1: Write the failing PlayMode tests**

```csharp
        [UnityTest]
        public IEnumerator RestartKey_OnlyActsOnTheResultsScreen()
        {
            yield return StartShortRun();
            var first = root.Sim;
            // Review Focus 5: R in live combat does nothing.
            Assert.IsFalse(root.HandleRestartKey(true));
            Assert.AreSame(first, root.Sim);

            // Die: the results screen comes up, then R starts a fresh run of the same kind.
            first.DamagePlayer(100000, 0);
            for (int i = 0; i < 10 && first.State != RunState.Results; i++) yield return null;
            Assert.AreEqual(RunState.Results, first.State);
            Assert.IsTrue(root.HandleRestartKey(true));
            Assert.AreNotSame(first, root.Sim);
            Assert.AreEqual(GameMode.Short, root.Sim.Setup.Mode);
            StringAssert.Contains("[R]", root.Flow.AgainLabel);
        }

        [UnityTest]
        public IEnumerator RestartKey_ThroughTheKeyboard()
        {
            yield return StartShortRun();
            var kb = InputSystem.AddDevice<Keyboard>();
            try
            {
                var first = root.Sim;
                first.DamagePlayer(100000, 0);
                for (int i = 0; i < 10 && first.State != RunState.Results; i++) yield return null;
                InputSystem.QueueStateEvent(kb, new KeyboardState(Key.R));
                yield return null;
                InputSystem.QueueStateEvent(kb, new KeyboardState());
                yield return null;
                Assert.AreNotSame(first, root.Sim);
            }
            finally { InputSystem.RemoveDevice(kb); }
        }
```

Check that `DamagePlayer(int, int)` ends a short run: `ArenaSim.cs:122` takes the amount in seconds, so 100000 empties the clock and death goes to `EndRun(Death)` on the next tick.

- [ ] **Step 2: Run them to confirm the failure**

Run: `bash .superpowers/uc.sh`
Expected: `failed: True` (`HandleRestartKey` and `AgainLabel` are missing).

- [ ] **Step 3: Input**

`PlayerInputReader.Build`, after the pause action:

```csharp
            // D97: one key to retry from the results screen. Lives in the UI map because the
            // gameplay map is off on the results screen. GameRoot decides when it counts.
            restart = Ui.AddAction("Restart", InputActionType.Button, "<Keyboard>/r");
```

Add the field `InputAction restart;` and `bool restartLatched;`. In `Update`, add `if (restart.WasPressedThisFrame()) restartLatched = true;` next to the pause line. Then add:

```csharp
        public bool ConsumeRestart()
        {
            bool r = restartLatched;
            restartLatched = false;
            return r;
        }
```

- [ ] **Step 4: GameRoot**

```csharp
        /// <summary>
        /// D97 (R14): R restarts ONLY from the results screen. In combat it would throw runs away
        /// by accident; the pause menu already has Restart. The profile was finalized inside the
        /// tick that ended the run, before the results appeared, so restarting at once is safe.
        /// </summary>
        public bool HandleRestartKey(bool pressed)
        {
            if (!pressed || InMainMenu || Sim == null || Sim.State != RunState.Results) return false;
            Restart();
            return true;
        }
```

In `Update`, right after `bool pausePressed = Input.ConsumePause();`, add `bool restartPressed = Input.ConsumeRestart();`. After the `InMainMenu` block returns, before the `pausePressed` handling, add `if (HandleRestartKey(restartPressed)) return;`.

- [ ] **Step 5: Compact results**

Goal: the outcome is readable at a glance. The detailed stats stay, but smaller and dimmer. In `RunFlowPanels.Build`, after `resultsTitle`:

```csharp
            // D97: one big headline (score, time, kills) under the title, so the outcome reads in
            // a glance; the detailed stats move below it, smaller and dimmer, for whoever wants them.
            resultsHeadline = Ui.Sized(Ui.Label("Headline", rp.transform, "", 40), 52);
            resultsHeadline.color = Ui.Ink;
```

Change the `resultsBody` font from 28 to 21, set its colour to `new Color(1, 1, 1, 0.65f)`, and keep its height. Rename the Play again label: `Ui.Button("PlayAgain", rp.transform, "Play again  [R]", onPlayAgain)`. Add `public string AgainLabel => againButton.GetComponentInChildren<Text>().text;`.

In `FillResults`:

```csharp
            int secs = Mathf.FloorToInt(s.Duration);
            resultsHeadline.text = $"{s.Score}   ·   {secs / 60}:{secs % 60:00}   ·   {s.Kills} kill{(s.Kills == 1 ? "" : "s")}";
            string bonus = s.VictoryBonus > 0 ? $"   (time bonus +{s.VictoryBonus})" : "";
            resultsBody.text =
                $"{ReasonText(s)}{bonus}\n" +
                $"Best volley: {s.BestVolleyKills}   Best chain: x{s.BestChain}   Overcharges: {s.Overcharges}\n" +
                $"Hit rate: {Mathf.RoundToInt(s.HitRate * 100f)}%  ({s.PacketsHit}/{s.PacketsReleased})   Average power: x{s.AverageFirePower:0.00}\n" +
                $"Health lost to hits: {LifeDisplay.Points(s.DamageTaken)}   Health gained: {LifeDisplay.Points(s.SecondsGained)}   Backfires: {s.Backfires}   Swaps: {s.Swaps}\n" +
                $"Health sacrificed: {LifeDisplay.Points(s.SecondsSacrificed)} ({s.UpgradesPaidFor} upgrade{(s.UpgradesPaidFor == 1 ? "" : "s")})   Most held: {s.MostUpgradesHeld}";
```

This removes the duplicate Duration/Kills/Score lines; they are now in the headline.

- [ ] **Step 6: Run the tests**

Run: `bash .superpowers/uc.sh`, then `timeout 900 bash .superpowers/rtp.sh`, then `timeout 900 bash .superpowers/rt.sh editor`
Expected: PlayMode 14/14 (11 baseline + 1 from Task 6 + 2 here), and EditMode all pass.

- [ ] **Step 7: Decision and commit**

```
| D97 | Rework | Instant restart and compact results. R on the results screen starts a fresh run of the same kind at once (Enter on the focused "Play again [R]" still works). R does nothing anywhere else (R14). Results: title, a big headline (score · time · kills), then dimmer, smaller details including best chain, Overcharges and kept upgrades. No near-miss line (owner: out for now) | Review point 3 approved by the owner: every second between death and the next run lowers the replay rate | "Readable in about 2 s" is a design target, not something a test can verify; it needs a human look |
```

```bash
git add Assets/Game Docs
git commit -q -F - <<'EOF'
Retry from the results screen with R and tighten the results layout

R restarts the same kind of run from the results screen only. The results
lead with one headline line (score, time, kills) and show the detailed stats
smaller underneath, now including best chain, Overcharges and kept upgrades
(D97).
EOF
```

---

## Task 10: Lifesteal (Blood Price) and a fully unlockable skill tree

**Owner, 3 Oct 2026:**
- "We should also add lifesteal skill tree nodes - you gain a tiny portion of the damage you deal back as health. You rely on borrowed power - and it comes with a price."
- Later the same day: "remove the limiter on max number of nodes - everything can be unlocked"; "A number should pop up indicating the lifesteal healing".
- The owner chose: no equip step, and a mastery level cap of 13.

My rulings on the details the quotes leave open:
- **R17a. A fourth branch, "Blood Price", holds three lifesteal nodes (tiers 1–3).** They're gated like the others: mastery levels 0, 2, 4 and 7 by tier, and each node needs the one below it. The three nodes **stack**.
- **R17b. The rate is life-seconds per point of damage dealt.** Placeholders, to tune by play: Leech 0.10, Siphon +0.10, Blood Debt +0.15, so 0.35 with all three. On screen (×10, D100) that's 1, 1 and 1.5 health per damage. For scale: an Acolyte has 3 health, so killing one with Leech returns 3 on screen, about 10 with all three, next to the kill's own 30.
- **R17c. Only damage that lands counts.** Overkill is excluded: the stolen amount uses `min(hit, health left)`.
- **R17d. Every source of damage the player causes counts**, the same set `DamageEnemy` handles: returned shots, explosions, echoes, ripostes, Orbit and Parting Gift. School resistance applies first. The boss counts too.
- **R17e. Life is capped at the run's starting seconds**, like every other gain. Lifesteal is off in the tutorial and once the run has ended.
- **R17f. A pop for every heal (owner).** The pop is a muted red "+N" (display units), above the enemy that was hit, so it's distinct from the kill pop's colour. A heal that rounds to 0 on screen shows nothing. The results report "Health stolen", kept apart from "Health gained".
- **R18a. Owning a node makes it active. The equip step and its 3-slot cap are gone** (owner). `PlayerProfile.equippedNodes` is deleted. Old saves still load, because `JsonUtility` ignores keys it doesn't know. The run and the Style preview read `ownedNodes`.
- **R18b. The mastery level cap rises from 10 to 13** (owner), which gives 12 points for 12 nodes. Levels 11–13 continue `CostToAdvance` (600, 650 and 700 XP). A profile already at level 10 starts earning again from 0 XP into level 11; banked `totalXp` is not converted (cost if wrong: veterans re-earn three levels).
- **R18c. The tree's choice is now order, not exclusion.** Every maxed profile ends up with the same 12 nodes. This was flagged to the owner and accepted.

**Files:**
- Modify: `Assets/Game/Scripts/Data/ProgressionTuning.cs` (a `[Header("Blood Price")]` section with three fields)
- Modify: `Assets/Game/Scripts/Player/PlayerStats.cs` (`LifePerDamage`)
- Modify: `Assets/Game/Scripts/Progression/SkillTree.cs` (`SkillBranch.BloodPrice`, three nodes, `Describe`; delete `WhyCannotEquip`/`TryEquip`/`Unequip`; `Respec` no longer touches equips; the validator line for duplicate equips goes)
- Modify: `Assets/Game/Scripts/Progression/Loadout.cs` (`Apply` cases)
- Modify: `Assets/Game/Scripts/Progression/ProfileService.cs` (`MaxLevel = 13`; delete `MaxEquipped`, the `equippedNodes ??=` line, and the two equip checks in `Validate`)
- Modify: `Assets/Game/Scripts/Progression/PlayerProfile.cs` (delete `equippedNodes`)
- Modify: `Assets/Game/Scripts/Presentation/GameRoot.Progression.cs:50` and `Assets/Game/Scripts/UI/StylePanel.cs:98` (read `ownedNodes`)
- Modify: `Assets/Game/Scripts/Runs/ArenaSim.Enemies.cs` (`DamageEnemy` steals life)
- Modify: `Assets/Game/Scripts/Runs/SimEvents.Run.cs` (`LifeStolen`)
- Modify: `Assets/Game/Scripts/Runs/RunScore.cs` (`LifeStolen` total plus summary)
- Modify: `Assets/Game/Scripts/Presentation/ArenaView.cs` (the lifesteal pop)
- Modify: `Assets/Game/Scripts/UI/SkillTreePanel.cs` (four columns; a click only buys; no equip count)
- Modify: `Assets/Game/Scripts/UI/RunFlowPanels.cs` (a results line)
- Modify tests:
  - `MasteryTests.cs`: node count, gating, the level-cap test and the equip test
  - `ProfileTests.cs:35,54`
  - `StyleTests.cs:115`
  - `GameRootPlayModeTests.cs:214-216`
  - `ReworkTests.cs`
- Modify: `Docs/DECISIONS.md` (D99, D101)

**Interfaces:**
- Consumes: `SchoolMultiplier` (Task 4); `DebugSetLife` (Task 7); `Spawned`/`From` (Task 4's test helpers); `LifeDisplay` (Task 8).
- Produces:
  - `SkillBranch.BloodPrice`
  - `SkillTree.BloodLeech`, `SkillTree.BloodSiphon`, `SkillTree.BloodDebt` (ids `blood_leech`, `blood_siphon`, `blood_debt`)
  - `PlayerStats.LifePerDamage : float`
  - `SimEvents.LifeStolen : Action<float seconds, Vector2 at>`
  - `RunScore.LifeStolen : float` and `RunSummary.LifeStolen : float`
  - `Mastery.MaxLevel == 13`. `Mastery.MaxEquipped`, `SkillTree.TryEquip`/`Unequip`/`WhyCannotEquip` and `PlayerProfile.equippedNodes` no longer exist.

- [ ] **Step 1: Write the failing tests**

Append to `ReworkTests`:

```csharp
        // ---- Lifesteal (D99) -------------------------------------------------------------------

        static ArenaSim WithLifesteal(float perDamage)
        {
            var stats = PlayerStats.FromConfig(TestSims.Config);
            stats.LifePerDamage = perDamage;
            var setup = RunSetup.ForSandbox(1);
            setup.Stats = stats;   // ArenaSim reads Setup.Stats ?? PlayerStats.FromConfig
            var sim = new ArenaSim(TestSims.Config, setup);
            sim.Player.Position = Vector2.zero;
            return sim;
        }

        [Test]
        public void Lifesteal_ReturnsAShareOfTheDamageThatLands()
        {
            var sim = WithLifesteal(0.1f);
            sim.DebugSetLife(60.0);
            float stolen = 0f;
            sim.Events.LifeStolen += (s, _) => stolen += s;
            var e = Spawned(sim, ActorCategory.SiegeFamiliar);   // 100 health in the helper
            sim.DamageEnemy(e, 10f, DamageCategory.ReturnedProjectile, From(ActorCategory.Acolyte), 1);
            // 10 x 1.33 (another school) = 13.3 damage, x 0.1 = 1.33 s (13 on screen).
            Assert.AreEqual(60f + 1.33f, sim.LifeSeconds, 1e-3f);
            Assert.AreEqual(1.33f, sim.Score.LifeStolen, 1e-3f);
            Assert.AreEqual(1.33f, stolen, 1e-3f, "R17f: the pop is driven by this event");
        }

        [Test]
        public void Lifesteal_IgnoresOverkill()
        {
            // R17c: a 13.3-damage hit on 2 health left steals for 2.
            var sim = WithLifesteal(0.1f);
            sim.DebugSetLife(60.0);
            var e = Spawned(sim, ActorCategory.SiegeFamiliar);
            e.Health = 2f;
            sim.DamageEnemy(e, 10f, DamageCategory.ReturnedProjectile, From(ActorCategory.Acolyte), 1);
            Assert.AreEqual(60.2f, sim.LifeSeconds, 1e-3f);
        }

        [Test]
        public void Lifesteal_NeverExceedsTheCap()
        {
            var sim = WithLifesteal(10f);
            var e = Spawned(sim, ActorCategory.SiegeFamiliar);
            sim.DamageEnemy(e, 10f, DamageCategory.ReturnedProjectile, From(ActorCategory.Acolyte), 1);
            Assert.AreEqual(sim.Stats.StartingSeconds, sim.LifeSeconds, 1e-4f);
        }

        [Test]
        public void Lifesteal_OffByDefault()
        {
            var sim = Sim();
            sim.DebugSetLife(60.0);
            var e = Spawned(sim, ActorCategory.SiegeFamiliar);
            sim.DamageEnemy(e, 10f, DamageCategory.ReturnedProjectile, From(ActorCategory.Acolyte), 1);
            Assert.AreEqual(60f, sim.LifeSeconds, 1e-4f);
        }
```

In `MasteryTests`, replace `AFourthEquippedNode_IsRejected_AndOnlyOwnedNodesEquip` (line ~173) with:

```csharp
        [Test]
        public void EveryNode_CanBeOwned_AndOwnedNodesAreActive()
        {
            // D101 (owner): no equip step and no cap. Level 13 = 12 points = all 12 nodes.
            Assert.AreEqual(13, Mastery.MaxLevel);
            Assert.AreEqual(12, SkillTree.Nodes.Count);
            var p = Profile(Mastery.MaxLevel, Mastery.MaxLevel - 1);
            // Buy in tier order so every prerequisite is already owned.
            foreach (var n in SkillTree.Nodes.OrderBy(n => n.Tier))
                Assert.IsTrue(SkillTree.TryBuy(p, n.Id, out var why), why);
            Assert.AreEqual(0, p.mastery.points);
            Assert.IsTrue(ProfileService.Validate(p, out var bad), bad);
            var s = Loadout.Resolve(TestSims.Config, p.ownedNodes);
            Assert.Greater(s.LifePerDamage, 0f, "owned = active");
        }

        [Test]
        public void BloodPrice_NodesStack_AndGateLikeTheOtherBranches()
        {
            var t = TestSims.Config.progression;
            var s = Loadout.Resolve(TestSims.Config, new[] { SkillTree.BloodLeech, SkillTree.BloodSiphon, SkillTree.BloodDebt });
            Assert.AreEqual(t.bloodLeech + t.bloodSiphon + t.bloodDebt, s.LifePerDamage, 1e-6f);
            var p = Profile(6, 1, SkillTree.BloodLeech, SkillTree.BloodSiphon);
            Assert.IsFalse(SkillTree.TryBuy(p, SkillTree.BloodDebt, out var why));
            StringAssert.Contains("mastery 7", why);
        }
```

`Profile(level, points, params string[] owned)` is the file's existing helper; check its signature first. Add `using System.Linq;` if it's missing.

Other existing tests:
- `MasteryTests.cs:61-67` (the level-cap test): `Assert.AreEqual(10, m.level)` → `Mastery.MaxLevel`; `Assert.AreEqual(9, m.points, …)` → `Mastery.MaxLevel - 1`, with the message "no point beyond the cap".
- `Respec_RefundsEveryOwnedNode_AndUnequipsThem` → rename to `Respec_RefundsEveryOwnedNode`, and delete its two `equippedNodes` lines.
- `ProfileTests.cs:35,54` and `MasteryTests.cs:213-216`: delete the `equippedNodes` lines.
- `ProfileTests.cs:76`: the JSON case stays. An unknown `"equippedNodes"` key is now ignored, and the case still fails on the unknown node `"a"`.
- `StyleTests.cs:115`: `p.equippedNodes.Add` → `p.ownedNodes.Add`. Read the test first, and keep its mastery level and points consistent with one more owned node.
- `GameRootPlayModeTests.cs:214-216`: delete the second click ("equip"). Expect `storage.Writes == 1`, and `CollectionAssert.Contains(root.Profile.Profile.ownedNodes, SkillTree.PrecisionAngle)`.

Fix the "nine-node" doc comments in `MasteryTests.cs`, `SkillTreePanel.cs` and `SkillTree.cs` to "twelve-node".

- [ ] **Step 2: Run them to confirm the failure**

Run: `bash .superpowers/uc.sh`
Expected: `failed: True`. The errors name `LifePerDamage`, `BloodLeech` and `LifeStolen`.

- [ ] **Step 3: Implement lifesteal**

`ProgressionTuning.cs`, after the Resilience block:

```csharp
        [Header("Blood Price")]
        [Tooltip("blood_leech: life SECONDS returned per point of damage that lands (shown x10 on screen, D100). Placeholder.")]
        public float bloodLeech = 0.10f;
        [Tooltip("blood_siphon: added to the above; the nodes stack (placeholder).")]
        public float bloodSiphon = 0.10f;
        [Tooltip("blood_debt: added to the above; the nodes stack (placeholder).")]
        public float bloodDebt = 0.15f;
```

`PlayerStats.cs`:

```csharp
        /// <summary>D99: life seconds returned per point of damage that lands on an enemy (0 = none).</summary>
        public float LifePerDamage;
```

`SkillTree.cs`: add `BloodPrice` to `SkillBranch`, the three constants, and the nodes:

```csharp
            // D99: borrowed power has a price, and this branch is the other side of it: the
            // damage you deal pays your life back. The names are placeholders like the rest (D80).
            new SkillNode(BloodLeech, "Leech", SkillBranch.BloodPrice, 1),
            new SkillNode(BloodSiphon, "Siphon", SkillBranch.BloodPrice, 2),
            new SkillNode(BloodDebt, "Blood Debt", SkillBranch.BloodPrice, 3),
```

`Describe` (display units, D100):

```csharp
                case BloodLeech: return $"Heal {t.bloodLeech * LifeDisplay.Scale:0.#} per damage you deal";
                case BloodSiphon: return $"+{t.bloodSiphon * LifeDisplay.Scale:0.#} healed per damage (stacks)";
                case BloodDebt: return $"+{t.bloodDebt * LifeDisplay.Scale:0.#} healed per damage (stacks)";
```

Check how `Describe` prints `resilienceTime` (seconds). If it says "+N s", change it to display units too (`+{t.resilienceTime * LifeDisplay.Scale:0} health`), so the tree never mixes units.

`Loadout.Apply`:

```csharp
                // D99: the three Blood Price nodes stack.
                case SkillTree.BloodLeech: s.LifePerDamage += t.bloodLeech; break;
                case SkillTree.BloodSiphon: s.LifePerDamage += t.bloodSiphon; break;
                case SkillTree.BloodDebt: s.LifePerDamage += t.bloodDebt; break;
```

`SimEvents.Run.cs`:

```csharp
        /// <summary>D99: lifesteal returned this many life seconds (after the cap) at this position.</summary>
        public event Action<float, Vector2> LifeStolen;
        internal void RaiseLifeStolen(float seconds, Vector2 at) => LifeStolen?.Invoke(seconds, at);
```

`ArenaSim.Enemies.cs`, `DamageEnemy`: replace `e.Health = Mathf.Max(0f, e.Health - amount);` with:

```csharp
            // D99: lifesteal counts only damage that lands (R17c); a huge hit on a nearly dead
            // enemy heals for what was left, not for the hit.
            float landed = Mathf.Min(amount, e.Health);
            e.Health = Mathf.Max(0f, e.Health - amount);
            StealLife(landed, e.Position);
```

Below `DamageEnemy`:

```csharp
        /// <summary>
        /// D99: give back Stats.LifePerDamage life per point of landed damage, capped at the
        /// run's starting seconds like every gain. Nothing in the tutorial (life does not drain
        /// there), after the run ended, or with a dead player. Raises LifeStolen, NOT
        /// LifeClockChanged: the view gives it its own colour (R17f), and the HUD must not treat
        /// it as a kill reward.
        /// </summary>
        void StealLife(float landed, Vector2 at)
        {
            if (Stats.LifePerDamage <= 0f || landed <= 0f || Setup.Tutorial || Summary != null
                || !Player.Alive || lifeSeconds <= 0) return;
            double before = lifeSeconds;
            lifeSeconds = System.Math.Min(Stats.StartingSeconds, lifeSeconds + landed * Stats.LifePerDamage);
            float gained = (float)(lifeSeconds - before);
            if (gained <= 0f) return;
            Score.RecordLifeStolen(gained);
            Events.RaiseLifeStolen(gained, at);
        }
```

Check the real name of the tutorial flag on `RunSetup` (`grep -n "Tutorial" Assets/Game/Scripts/Runs/RunSetup.cs`), and use it.

`RunScore.cs`: add `public float LifeStolen { get; private set; }` and `internal void RecordLifeStolen(float s) => LifeStolen += s;`. In `RunSummary`, add `public readonly float LifeStolen;` and `LifeStolen = s.LifeStolen;`. It doesn't count toward `SecondsGained`.

`ArenaView.cs`: next to `sim.Events.LifeClockChanged += SpawnTimeNumber;` (line ~138) add `sim.Events.LifeStolen += SpawnStolenNumber;`. Factor `SpawnTimeNumber`'s body into `SpawnNumber(string text, Color color, Vector2 at)`; `SpawnTimeNumber` and the new method both call it:

```csharp
        // D99/R17f: lifesteal gets its own muted red so a heal from hitting reads differently
        // from a kill's reward; shown x10 (D100), and skipped when it rounds to nothing.
        static readonly Color StolenColor = new Color(1f, 0.45f, 0.5f);
        void SpawnStolenNumber(float seconds, Vector2 at)
        {
            if (LifeDisplay.Points(seconds) <= 0) return;
            SpawnNumber(LifeDisplay.Signed(seconds), StolenColor, at);
        }
```

Unsubscribe it wherever the view unsubscribes `SpawnTimeNumber`, if it does.

`RunFlowPanels.FillResults`: add `Health stolen: {LifeDisplay.Points(s.LifeStolen)}` to the line with "Health gained" (Task 9 laid those lines out).

- [ ] **Step 4: Open the tree (D101)**

- `ProfileService.cs`, the `Mastery` partial: `MaxLevel = 13`. Comment: "D101 (owner): 12 points for the 12 nodes; everything can be unlocked." Delete `MaxEquipped`.
- `ProfileService.Validate`: delete the `equippedNodes.Count > MaxEquipped` check and the "equipped but not owned" loop. Delete `p.equippedNodes ??= …` (line ~120).
- `PlayerProfile.cs`: delete `equippedNodes`, with the comment "D101: owning a node makes it active. Old saves' equippedNodes key is ignored by JsonUtility."
- `SkillTree.cs`: delete `WhyCannotEquip`, `TryEquip` and `Unequip`. In `Respec`, delete `p.equippedNodes.Clear()`. In the validator helper (line ~159), delete the duplicate-equipped check. Update the class summary: "buying and respeccing; an owned node is active (D101)".
- `GameRoot.Progression.cs:50`: `setup.PassiveIds = new List<string>(Profile.Profile.ownedNodes);`
- `StylePanel.cs:98`: `Loadout.Resolve(c, p.ownedNodes, style.Id)`.
- `SkillTreePanel.cs`:
  - `Click`: only `SkillTree.TryBuy(profile, id, out why)` for unowned nodes. An owned node's click does nothing, with no status.
  - The header loses the "N / 3 equipped" part.
  - Card states: owned = `<color=#8CF0A8>ACTIVE</color>`; otherwise as now.
  - The `Equipped` colour becomes the owned colour.
  - The note text becomes "Click a node to buy it. Owned nodes are always active. Changes apply from the next run."
  - Four columns, panel still 1560 wide:

```csharp
            // Four branches since D99: 380 px apart, 360 wide, centred.
            string[] branchNames = { "PRECISION", "MOBILITY", "RESILIENCE", "BLOOD PRICE" };
            for (int b = 0; b < branchNames.Length; b++)
            {
                float x = (b - 1.5f) * 380f;
                // ...unchanged label code, width 360...
            }
```

For node cards use `float x = ((int)n.Branch - 1.5f) * 380f;` and width 360 instead of 460. Colour the Blood Price header `new Color(1f, 0.55f, 0.55f)`, a muted red, so the branch reads as the price side.

Run `grep -rn "equippedNodes\|MaxEquipped\|TryEquip\|Unequip\|WhyCannotEquip" Assets/Game` afterwards: it must return nothing.

- [ ] **Step 5: Run the tests**

Run: `bash .superpowers/uc.sh`, then `timeout 900 bash .superpowers/rt.sh editor`, then `timeout 900 bash .superpowers/rtp.sh`
Expected: all pass. Check the skill tree once in the editor at 1920×1080: the four columns mustn't overlap and the card text mustn't clip. Record that in `TEST_EVIDENCE.md` as a human check.

- [ ] **Step 6: Decisions and commit**

```
| D99 | Rework | Lifesteal. A fourth skill-tree branch, Blood Price, with three stacking nodes (Leech 0.10, Siphon +0.10, Blood Debt +0.15 life seconds per point of damage = 1 / 1 / 1.5 on screen; placeholders), gated like the others (mastery 0/2/4/7, each needs the one below). Only damage that lands counts (no overkill), from every player-caused source, after school resistance; capped at the starting seconds; off in the tutorial. Every heal pops a muted-red "+N" (x10, D100) unless it rounds to 0; the results show "Health stolen", apart from "Health gained" | Owner decisions 3 Oct 2026: "you gain a tiny portion of the damage you deal back as health. You rely on borrowed power - and it comes with a price."; "A number should pop up indicating the lifesteal healing" | Values untested by play; pops on every hit may crowd busy fights |
| D101 | Rework | The skill tree is fully unlockable. No equip step: owning a node makes it active (PlayerProfile.equippedNodes removed; old saves load, the key is ignored). Mastery cap 10 -> 13, so 12 points buy all 12 nodes; levels 11-13 continue CostToAdvance. Supersedes section 7's three equip slots | Owner decision 3 Oct 2026: "remove the limiter on max number of nodes - everything can be unlocked"; owner chose no equip and cap 13 | The tree stops being a build choice: every maxed profile has the same 12 nodes (flagged, accepted); profiles at level 10 re-earn levels 11-13 from 0 XP |
```

```bash
git add Assets/Game Docs
git commit -q -F - <<'EOF'
Add Blood Price lifesteal and make the whole skill tree unlockable

Three stacking nodes return a share of each hit's landed damage as life,
capped at the starting clock and excluding overkill, with a pop for every
heal. Owned nodes are now always active, the equip limit is gone and the
mastery cap is 13 so all twelve nodes can be bought (D99, D101).
EOF
```

---

## Task 11: Score from Overcharge and kill chains, and XP from score

**Owner, 3 Oct 2026 (answer to Q7):** "overcharge and kill chains should add to score (the combo multiplier should also add score, and also, higher score should earn you more XP too)".

What the code already does (evidence): each kill scores `KillValue × Multiplier`, where `Multiplier` is the combo, from 1.0 up to 3.0 in steps of 0.25 per first-hit release (`RunScore.cs:141`). So the combo already adds score to kills. XP today comes only from kills, overstays, bosses, encounters and perfect hits (`Mastery.cs:39`). Score earns none.

My rulings:
- **R19a. An Overcharged release scores `shortMode.overchargeScore` (20, placeholder) × the combo multiplier**, once per release. An echo isn't a release, so it doesn't count. For scale, an ordinary kill is 10–25.
- **R19b. The Nth kill of a chain scores `shortMode.chainScore[N-1]` × the combo**: `{ 0, 5, 10, 15 }` (placeholders; the last repeats). It mirrors the chain's time bonus (`chainBonusSeconds` 0, 1, 3, 5): the first kill of a chain is just a kill. Boss kills neither extend nor score a chain (R15).
- **R19c. "The combo multiplier should also add score"** is read as: the combo multiplies the new bonuses too, as it already does for kills (inferred; there is no separate combo-score award). If you meant a bonus for reaching a combo level, that's a follow-up.
- **R19d. XP from score: 1 XP per 50 points (`Mastery.ScorePerXp = 50`), uncapped**, the same as kill XP. A typical short victory (~2,000 points) earns about +40 XP, next to roughly 150–200 from the existing sources. The divisor is a `Mastery` constant like the other XP rates, because saved levels are validated against them.
- **R19e.** The results screen breaks the score down: `Overcharge +N   Chains +N`. The mastery breakdown adds `score N` to its "+X XP" line.

**Files:**
- Modify: `Assets/Game/Scripts/Data/RunTuning.cs` (`ShortModeTuning.overchargeScore`, `chainScore`)
- Modify: `Assets/Game/Scripts/Runs/RunScore.cs` (`ScoreFromOvercharges`, `ScoreFromChains`, the two subscriptions, summary)
- Modify: `Assets/Game/Scripts/Progression/Mastery.cs` (`ScorePerXp`, `XpBreakdown.ScoreXp`, `RunXp`)
- Modify: `Assets/Game/Scripts/Presentation/GameRoot.Progression.cs` (the XP breakdown line)
- Modify: `Assets/Game/Scripts/UI/RunFlowPanels.cs` (the results breakdown)
- Modify: `Assets/Game/Tests/EditMode/ReworkTests.cs`, `MasteryTests.cs`
- Modify: `Docs/DECISIONS.md` (D102)

**Interfaces:**
- Consumes:
  - `SimEvents.PacketOvercharged : Action<CapturedPacket, int>` (Task 5)
  - `SimEvents.KillChainChanged : Action<int length, float bonus>` (Task 7)
  - the `Held` helper (Task 5) and the `Spawned`/`From` helpers (Task 4)
  - `DebugSetLife` (Task 7)
- Produces:
  - `RunScore.ScoreFromOvercharges : int`, `RunScore.ScoreFromChains : int`, both on `RunSummary`
  - `Mastery.ScorePerXp : const int`, `XpBreakdown.ScoreXp : int`
  - `Mastery.RunXp(int normalKills, int overstayedKills, int bossKills, int encounters, int perfectHits, int score = 0)`

- [ ] **Step 1: Write the failing tests**

Append to `ReworkTests`:

```csharp
        // ---- Score and XP (D102) ---------------------------------------------------------------

        [Test]
        public void Overcharge_AddsScore()
        {
            var sim = Sim();
            Held(sim, 2.75);
            int before = sim.Score.Score;
            float combo = sim.Score.Multiplier;   // nothing hit yet: 1
            sim.Tick(Hold.WithRelease(), Dt);
            int expected = Mathf.RoundToInt(sim.Config.shortMode.overchargeScore * combo);
            Assert.AreEqual(expected, sim.Score.ScoreFromOvercharges);
            Assert.AreEqual(before + expected, sim.Score.Score);
        }

        [Test]
        public void Chains_AddScore_TimesTheCombo()
        {
            var sim = Sim();
            sim.DebugSetLife(60.0);
            var t = sim.Config.shortMode.chainScore;
            int expected = 0;
            for (int i = 0; i < 3; i++)
            {
                var e = Spawned(sim, ActorCategory.Acolyte);
                sim.DamageEnemy(e, 1000f, DamageCategory.ReturnedProjectile, From(ActorCategory.SiegeFamiliar), 1);
                // Read the combo AFTER the hit: the kill (and its chain step) is scored at it.
                expected += Mathf.RoundToInt(t[Mathf.Min(i, t.Length - 1)] * sim.Score.Multiplier);
                Run(sim, 30);
            }
            Assert.AreEqual(expected, sim.Score.ScoreFromChains);
            Assert.Greater(sim.Score.ScoreFromChains, 0);
        }
```

Append to `MasteryTests`:

```csharp
        [Test]
        public void Score_EarnsXp()
        {
            // D102 (owner): a higher score earns more XP.
            var none = Mastery.RunXp(10, 2, 1, 3, 50);
            var some = Mastery.RunXp(10, 2, 1, 3, 50, score: 1234);
            Assert.AreEqual(1234 / Mastery.ScorePerXp, some.ScoreXp);
            Assert.AreEqual(none.Total + some.ScoreXp, some.Total);
        }
```

The existing `RunXp` test at `MasteryTests.cs:74-75` keeps passing, because `score` defaults to 0.

- [ ] **Step 2: Run them to confirm the failure**

Run: `bash .superpowers/uc.sh`
Expected: `failed: True`. The errors name `overchargeScore`, `chainScore`, `ScoreFromOvercharges`, `ScoreFromChains`, `ScorePerXp` and `ScoreXp`.

- [ ] **Step 3: Implement**

`RunTuning.cs`, `ShortModeTuning`, after `comboTimer`:

```csharp
        [Tooltip("D102: score for an Overcharged release, times the combo multiplier (placeholder).")]
        public int overchargeScore = 20;
        [Tooltip("D102: score for the Nth kill of a chain (index N-1), times the combo; the last repeats. Mirrors combat.chainBonusSeconds (placeholder).")]
        public int[] chainScore = { 0, 5, 10, 15 };
```

`RunScore.cs`, in the constructor next to the existing subscriptions:

```csharp
            // D102 (owner): the skill shots score, and the combo multiplies them like it does kills.
            sim.Events.PacketOvercharged += (_, __) => AddBonus(sim.Config.shortMode.overchargeScore, ref scoreFromOvercharges);
            sim.Events.KillChainChanged += (length, _) =>
            {
                var t = sim.Config.shortMode.chainScore;
                if (t == null || t.Length == 0 || length < 1) return;
                AddBonus(t[Mathf.Min(length, t.Length) - 1], ref scoreFromChains);
            };
```

And the members:

```csharp
        int scoreFromOvercharges, scoreFromChains;
        public int ScoreFromOvercharges => scoreFromOvercharges;
        public int ScoreFromChains => scoreFromChains;

        void AddBonus(int basePoints, ref int bucket)
        {
            if (basePoints <= 0) return;
            int pts = Mathf.RoundToInt(basePoints * Multiplier);
            Score += pts;
            bucket += pts;
        }
```

Add both to `RunSummary`, as fields and constructor lines.

**Check the order:** `KillChainChanged` is raised from `RewardKillTime`, which runs on `EnemyKilled`. If `RunScore` subscribes to `KillChainChanged` before the sim's `Chain` exists, that's fine: the event lives on `sim.Events`. Run the chain test and read the result; don't assume it.

`Mastery.cs`:

```csharp
        /// <summary>D102 (owner): every this-many points of score is 1 XP. A rule, not tuning (see the class note).</summary>
        public const int ScorePerXp = 50;
```

- `XpBreakdown`: add `public int ScoreXp;`.
- `RunXp(int…)` gains `int score = 0`, sets `ScoreXp = System.Math.Max(0, score) / ScorePerXp`, and adds it to `Total`.
- `RunXp(RunSummary s)` passes `s.Score`.

`GameRoot.Progression.cs` (line ~76): `if (x.ScoreXp > 0) parts.Add($"score {x.ScoreXp}");`

`RunFlowPanels.FillResults`: under the score line (Task 9's layout), add `Overcharge +{s.ScoreFromOvercharges}   Chains +{s.ScoreFromChains}`, shown only when either is above 0.

- [ ] **Step 4: Run the tests**

Run: `bash .superpowers/uc.sh`, then `timeout 900 bash .superpowers/rt.sh editor`, then `timeout 900 bash .superpowers/rtp.sh`
Expected: all pass. Any test that pins an exact final score (`grep -rn "Score.Score\|\.Score)" Assets/Game/Tests`) may change if its run overcharges or chains. Re-pin it, and note each one in the ledger.

- [ ] **Step 5: Decision and commit**

```
| D102 | Rework | Score and XP. An Overcharged release scores shortMode.overchargeScore (20) x combo; the Nth kill of a chain scores shortMode.chainScore[N-1] ({0,5,10,15}, last repeats) x combo; both placeholders. The combo already multiplied kill score; it now multiplies these too (the reading of "the combo multiplier should also add score"). XP gains score / Mastery.ScorePerXp (50), uncapped. Results show the Overcharge and chain score; the XP breakdown shows "score N". Answers Q7 | Owner decision 3 Oct 2026: "overcharge and kill chains should add to score (the combo multiplier should also add score, and also, higher score should earn you more XP too)" | Values untested; endless runs with big scores earn proportionally more XP; records set before this change are not comparable |
```

```bash
git add Assets/Game Docs
git commit -q -F - <<'EOF'
Score Overcharge and kill chains, and turn score into mastery XP

An Overcharged release and each chain kill now add score, multiplied by the
combo like kills already are. Every 50 points of a run's score adds 1 XP, so a
higher score levels mastery faster. Results and the XP breakdown show the new
sources (D102).
EOF
```

---

## Task 12: Soak, docs, handoff

**Files:**
- Modify: `Docs/GAME_PLAN.md` (a banner at the top plus one pointer line in each superseded subsection; no rule text is rewritten)
- Modify: `Docs/IMPLEMENTATION_STATUS.md`, `Docs/TEST_EVIDENCE.md`, `Docs/HANDOFF.md`

**Interfaces:** none (documentation and verification).

- [ ] **Step 1: Full verification**

Run: `bash .superpowers/uc.sh`, then `timeout 900 bash .superpowers/rt.sh editor`, then `timeout 900 bash .superpowers/rtp.sh`
Expected: `failed: False`, EditMode all pass (319 + new), PlayMode 14/14. Record the exact counts.

- [ ] **Step 2: Read the soak numbers**

`IntegrationTests` prints per-seed lines (reach, swaps, upgrades). Copy the swaps-per-run and survival times from `.superpowers/last-tests.json` into `TEST_EVIDENCE.md`, next to the last recorded soak. Label them as **bot evidence, not player evidence**: the bot's Q use is scripted, so a rise in swaps proves the rule is wired, not that players will swap.

- [ ] **Step 3: Point GAME_PLAN.md at this document** (owner, 3 Oct 2026: "the GAME_PLAN.md should point to the new doc")

Don't rewrite any rule text in `GAME_PLAN.md`. Add a banner right under its title:

```markdown
> **Rework of 3 Oct 2026.** Several rules below are superseded by [REWORK_PLAN.md](REWORK_PLAN.md)
> (decisions D89-D102): the hand rule, priming, school resistance, the power curve and Overcharge,
> Overflow/Fusion triggers, kill chains, upgrades that last the run and are bought with life, the results
> screen / R restart, the Blood Price (lifesteal) branch, a fully unlockable skill tree, life shown x10,
> and score/XP from Overcharge and chains.
> Where a section below carries a "Superseded" note, REWORK_PLAN.md wins.
```

Then add one italic pointer line at the top of each affected subsection:
- `*Superseded in part: see REWORK_PLAN.md D89 (hand rule) and D90 (priming).*` → §3 "Which slot a catch fills" and §3 Catching.
- `*Superseded: see REWORK_PLAN.md D93 (power curve and Overcharge).*` → §3 Power.
- `*Superseded in part: see REWORK_PLAN.md D90 (unprimed hexes cannot fire).*` → §3 Firing.
- `*Extended: see REWORK_PLAN.md D92 (school resistance).*` → §3 per-enemy hexes.
- `*Superseded in part: see REWORK_PLAN.md D91 (Overflow/Fusion) and D96 (upgrades no longer expire; bought with life, up to four).*` → §5.
- `*Superseded in part: see REWORK_PLAN.md D99 (Blood Price lifesteal branch) and D101 (no equip limit, mastery cap 13).*` → §7 skill tree and mastery.
- `*Extended: see REWORK_PLAN.md D102 (score from Overcharge and chains; XP from score).*` → §6 score and §7 XP.
- `*Extended: see REWORK_PLAN.md D100 (life shown x10).*` → §6 the run clock is life.
- `*Extended: see REWORK_PLAN.md D95 (kill chains).*` → §6 the run clock is life.
- `*Superseded in part: see REWORK_PLAN.md D97 (results screen, R restart).*` → §6 score and records.
- `*Extended: see REWORK_PLAN.md D97 (R restarts from the results screen).*` → §2 controls.

Find each heading with `grep -n "^#" Docs/GAME_PLAN.md` before editing. If a heading named above doesn't exist under that name, put the pointer on the closest section that holds that rule, and note it in the ledger.

- [ ] **Step 4: Status, evidence, handoff**

- `IMPLEMENTATION_STATUS.md`: add a "Rework (3 Oct 2026)" block listing each feature, its D-number and its test names.
- `TEST_EVIDENCE.md`: changed files, new tests and counts, plus **Not verified** (including the balance of four held upgrades, the upgrade prices, the lifesteal values and pop density, the score/XP values, and the four-column tree layout):
  - feel of the 0.4 s priming
  - Overcharge values
  - shake and freeze strength
  - the "2 s readable" target
  - the boss fight with school resistance off for the boss
  - **manual check (Review Focus 4):** pressing Enter on a freshly opened upgrade choice continues for free
- `HANDOFF.md`: update the EditMode/PlayMode counts. Under "Open questions", list the placeholder values that need a play pass: upgrade prices, lifesteal rates, Overcharge/chain score, and XP per score.

- [ ] **Step 5: Commit**

```bash
git add Assets/Game Docs
git commit -q -F - <<'EOF'
Record the rework in the game plan, status, evidence and handoff

GAME_PLAN gets a banner and per-section pointers to REWORK_PLAN.md for the
rules the rework supersedes (D89-D102). Status, evidence and handoff carry the
new test counts, the soak numbers and the values still to tune by play.
EOF
```

---

## Self-review (done while writing)

- **Spec coverage:**
  - Rule A → Task 1.
  - Rule B → Task 2.
  - School resistance → Task 4.
  - Point 1 (Overcharge plus curve) → Tasks 5 and 6.
  - Point 2 (upgrades bought with life; swap, add, rank up; four max) → Task 8.
  - Point 3 (R plus compact results, no near-miss) → Task 9.
  - Point 4 (kill chains plus counter) → Task 7.
  - Tutorial knock-on → Task 3.
  - Lifesteal and an unlockable tree → Task 10.
  - Life ×10 → Task 8 (`LifeDisplay`), used by Tasks 9 and 10.
  - Q7 (score/XP) → Task 11.
  - Docs → each task, plus Task 12.
  - Point 5's skill-tree reframe is still to be discussed. Only the lifesteal nodes and the equip limit the owner asked about are in scope (Task 10).
- **Known soft spots, not placeholders:**
  - Task 1 Step 7 and Task 4 Step 4 migrate existing tests by rule ("insert a pocket", "scale the expected damage") rather than listing every line. The exact set is only known by running the suite after the change. Each fix has to be recorded in the ledger.
  - Task 8 Step 1 builds `AtFirstChoice`/`EndlessAtChoice` from the existing `ShortRunTests`/`EndlessTests` routes, and reuses `P5.ClearEncounter`, rather than inventing a new route to a choice.
  - Task 8 changes what a pick costs, so existing tests that read life after `ChooseUpgrade` change. The exact set is found by running the suite. Each fix is ledgered.
- **Owner revisions (3 Oct):**
  - R5, R6, R13 and R17–R20 changed or were added. Tasks 4, 8 and 10 were rewritten, Task 11 is new, and the docs task is Task 12.
  - Task 8's names (`HeldUpgrades`, `UpgradesLocked`, `RankOf`, `IsRankUp`, `TakeCost`/`TakeCostFraction`, `CanSwap`, `ChooseUpgrade(int, int)`, `DebugHold`, `DebugOpenChoice`, `SecondsSacrificed`, `UpgradesPaidFor`, `MostUpgradesHeld`, `LifeDisplay`) are used in Task 8, in Task 9's results lines and in Task 10 (`LifeDisplay`).
  - Task 10 consumes `SchoolMultiplier` (Task 4), `DebugSetLife` (Task 7), the `Spawned`/`From` helpers (Task 4) and `LifeDisplay` (Task 8).
  - Task 11 consumes `PacketOvercharged` and `Held` (Task 5), `KillChainChanged` and `DebugSetLife` (Task 7), and Task 9's results layout.
  - The task order already satisfies every one of these.
- **Type consistency:**
  - `HandFree`, `CreateInSlot` and `TestSims.Seed`/`Pocket` (Task 1) are used in Tasks 2, 3, 5 and 6.
  - `IsPrimed` (Task 2) is used in Task 3.
  - `PowerCurve`/`Stats.Power`/`FirePower(PowerCurve)` (Task 5) are used in Tasks 6 and 9.
  - `Spawned`/`From` (Task 4) are used in Tasks 5 and 7.
  - `Chain`/`BestChain` (Task 7), `Overcharges` (Task 5) and `SecondsSacrificed`/`UpgradesPaidFor`/`MostUpgradesHeld` (Task 8) are used in Task 9's results.
- **Review Focus:** every line has a named test in its owning task.
