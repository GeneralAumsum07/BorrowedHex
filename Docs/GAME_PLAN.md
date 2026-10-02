# Borrowed Hex — Game Design and Implementation Plan

> Confirmed game title: Borrowed Hex. This document is the shared implementation brief for Codex and Claude. Use the `superpowers:executing-plans` workflow when implementation is authorized, and implement the phases in order. Checkboxes describe work to perform; they do not claim that work is complete. Unity CLI is the primary Unity workflow. Do not automatically commit, push, publish, or contact other agents or services.

**Date:** 2 October 2026  
**Status:** Planning document; implementation has not started.  
**Goal:** Build a short, replayable action game about a rogue magician who temporarily steals enemy attacks and returns them when their borrowed lifetime expires.  
**Architecture:** A 2.5D game: camera-facing 2D pixel-art characters inside a 3D Unity arena, viewed through a fixed elevated camera, with movement and combat constrained to one flat gameplay plane. Use placeholder character views until artist assets are supplied. Separate combat simulation, run state, progression, and presentation. Author configurable definitions as ScriptableObjects; keep mutable run state and persistent player data separate.  
**Tech stack:** Recommended project editor: Unity 6.3 LTS `6000.3.25f1`; currently installed editor: `6000.3.5f1`. Use a 3D URP project, C#, Unity Input System, uGUI, Unity Test Framework, and Unity CLI. Verify editor/template/package availability before creation and pin the selected version for both agents and both targets.  
**Spec:** Sections 1–8 of this document; phased implementation is in section 9. Read both before implementing a phase.

## 1. Scope, decisions, and priorities

### Agreed direction

- Theme: **Everything is temporary**.
- Player fantasy: a nimble rogue magician who borrows dangerous magic rather than owning a conventional weapon.
- Core interaction: intercept an enemy attack, carry it briefly, reposition, and return it when its timer expires.
- Two stored attack packets, each with an independent lifetime.
- Temporary, interacting upgrades acquired during a run.
- Permanent passive skills purchased with skill points and gated by mastery level.
- Achievements, personal records, and meaningful progression even after unsuccessful runs.
- A short main mode and an endless survival mode with scaling encounters and recurring bosses.
- Customizable capture styles and, eventually, cosmetic presentation.
- Confirmed 2.5D presentation: 2D pixel-art character sprites in a 3D environment, inspired by the supplied Octopath Traveler reference. Establish the camera, flat combat plane, and separate character-view/collider setup in the earliest phases; final art and lighting polish remain deferred.
- **Enemy drafting is excluded.** Players do not choose which enemy factions enter the arena.
- An artist and sound designer handle asset production. This implementation uses functional placeholders and does not prioritize final assets or polish.

### Implementation defaults

The conversation established the design direction but did not fix every parameter. Values in this document are explicit starting defaults, not claims that they have been playtested. Put them in configuration rather than scattering constants through scripts. Record any later tuning changes with their reason.

- Confirmed delivery targets: Windows desktop and browser Web builds. Initial controls remain keyboard and mouse; mobile browser/touch support is not implied by the Web target.
- The user will create the repository. Do not create a competing repository or assume its URL/path; use the supplied checkout when available.
- Deadline scheduling is deferred at the user's request. Do not repeatedly ask for the deadline; the user will provide it later.
- Confirmed game title: **Borrowed Hex**. Proposed new Unity project folder name: `BorrowedHex`. Preserve any repository path the user supplies instead of renaming it automatically.
- One arena and one boss are sufficient for the first complete gameplay version.
- No multiplayer, online accounts, online leaderboards, monetization, cloud saves, procedural level generation, or additional platforms in the baseline scope.
- No direct player gun or unlimited basic damaging spell. Damage comes from borrowed attacks and upgrades derived from them.
- Health and timers must remain understandable without relying on final art or sound.

### Priority rule

Make **movement → incoming attacks → capture → timed return → enemy defeat** enjoyable before building progression. A skill tree cannot compensate for awkward interception or weak counterattacks. Preserve a playable build after each phase.

## 2. Player experience and controls

### Core loop

1. Read an incoming enemy attack.
2. Move into a useful interception position.
3. Activate a short directional catch window.
4. Carry the captured attack packet for three seconds.
5. Reposition and aim while its timer shrinks.
6. Automatically return the attack toward the current aim when it expires.
7. Use the result to defeat enemies, maintain a scoring chain, and set up another interception.

The player should make decisions about interception, ammunition selection, positioning, and timing. Captured attacks retain recognizable identities: bullets return as bullets; rockets return as rockets.

### Initial controls

| Action | Binding | Rule |
|---|---|---|
| Move | WASD | Camera-relative directions on the arena plane; normalize diagonal input |
| Aim | Mouse | Project cursor onto the gameplay plane; preserve last valid aim if projection fails |
| Catch | Left mouse button | A press opens one catch window; holding does not repeatedly activate it |
| Dash | Space | Dash toward movement input, or aim when no movement input exists |
| Release early | Right mouse button | Fire the selected packet now along the current aim (owner direction, D32) |
| Switch slot | Q | Cycle the packet-slot selection; wraps (owner direction, D33) |
| Pause | Escape | Pause gameplay; resume through the same menu |
| Confirm UI choice | Mouse/keyboard navigation | UI input never activates combat actions |
| Restart | Results button | Clear run state and create a new run identifier |

### Initial player tuning

| Parameter | Default |
|---|---:|
| Movement speed | 6 arena units/second |
| Maximum health | 3 hit points |
| Damage from a normal enemy hit | 1 hit point |
| Post-hit invulnerability | 0.65 seconds |
| Dash distance / duration | 2.9 units / 0.18 seconds |
| Dash cooldown | 1.2 seconds |
| Dash invulnerability | First 0.12 seconds |
| Stored packet slots | 2 |
| Lifetime of each packet | 3.0 gameplay seconds |
| Catch range / total cone angle | 2.8 units / 90 degrees |
| Catch window | 0.25 seconds |
| Catch recovery after window closes | 0.65 seconds |
| Capacity per packet | 12 energy units |

Dash movement must respect walls. Visual recoil, sprite animation, and camera effects must not displace collision geometry. Zero input, cursor outside the window, and focus loss must not produce invalid movement or direction values.

## 3. Capture, storage, and return rules

### Packet lifecycle

- A catch attempt consumes recovery even if it catches nothing, but an empty attempt occupies no storage slot.
- The first successful interception creates a packet. Its three-second lifetime begins at that interception.
- Additional eligible projectiles intercepted during the same catch window join that packet without resetting its timer.
- Each packet contains immutable attack snapshots: payload type, base damage, speed, size, relative spread, energy cost, original enemy identifier, and whether that shot was a perfect catch.
- Store data, not references to the original projectile GameObjects. The enemy or projectile can disappear without invalidating a packet.
- A packet moves with the player visually. Its countdown is independent of its orbit animation.
- At expiry, release all stored payloads from the player's current position toward the last valid current aim. Preserve their relative spread rather than reversing their historic trajectories.
- Release exactly once. Free the slot immediately; an echo upgrade must not keep the slot occupied.
- Two packets never merge automatically. Show their types, fullness, and remaining lifetime separately.

### Eligibility and capacity

- Only hostile, explicitly capturable attacks can be intercepted.
- Player-returned attacks, echoes, friendly explosions, and the player's own upgrade effects cannot be recaptured.
- A projectile must be inside the active cone/range and approaching the player-facing capture region. A shot moving away cannot be collected from behind.
- Bullet cost: 1 energy unit. Heavy shot cost: 3. Rocket cost: 4.
- A projectile that does not fit completely is not consumed. It continues travelling and can hurt the player.
- A full packet stops collecting; fullness must be visibly indicated.
- With both slots occupied, a new catch cannot create a packet unless the Overflow upgrade is active. Existing packets continue their timers normally.
- If an eligible capture and player impact coincide, successful interception wins. Otherwise ordinary collision and damage apply. Use one authoritative projectile-resolution path to prevent double consumption.
- Resolve already-expired packets before processing new catches on the same simulation tick, so a freed slot is usable immediately.

### Perfect catch

A shot is perfect when, at interception, its projected path would reach the player's hitbox within 0.10 seconds. Use its current velocity and the player's collision shape, not screen-space distance. A projectile whose path misses the player cannot qualify.

Perfect shots receive a baseline 15% return-damage bonus. Keep this flag per payload, not for the whole packet. The Final Second upgrade adds to this bonus. This system is added after basic capture works; it is not a prerequisite for the first counterattack prototype.

### Clock, pause, and lifecycle

One gameplay clock governs movement cooldowns, projectile lifetime, packet expiry, enemy telegraphs, upgrades, and run duration. Pause, upgrade selection, results, and application focus loss stop that clock. UI animations may use an unscaled clock.

Death cancels every pending packet, echo, explosion, enemy action, and delayed spawn. None may execute in the results screen or the next run. Restart clears event subscriptions and returns pooled objects to a neutral state.

### Ammunition starvation

Enemy compositions must normally keep ranged ammunition available. Also implement a visible arcane lantern as a safety source: if enemies remain but there are no ranged enemies, hostile projectiles, or stored packets for two seconds, it emits a pair of slow capturable bolts every two seconds until the condition ends. Lantern attacks are hostile but generate no capture XP or score by themselves. This prevents a melee-only remainder from making the game unwinnable without giving the player a direct gun.

> **Superseded after the Phase 4 playtest (owner direction, D26).** The arcane lantern is removed: it read as random
> and out of place. A melee-only remainder is answered by the **parry** instead: a catch window open while facing a
> Pursuer when its strike resolves intercepts the strike and redirects it at the attacker as a riposte (2 damage,
> pierce 1). It stays a borrowed attack, not a direct gun: a riposte exists only when an enemy commits a strike.

## 4. Enemies and borrowed attack types

### Initial roster

| Enemy | Behaviour | Attack supply | Purpose |
|---|---|---|---|
| Bolt Acolyte | Keeps distance, telegraphs, aims, then fires | Three ordinary bolts | Teaches capture and return |
| Pursuer | Approaches the player and performs a telegraphed close strike | None in the baseline | Pressures positioning and gives crowd targets |
| Scatter Caster | Repositions between attacks | Five-shot fan | Supplies fuller packets and tests interception angles |
| Siege Familiar | Slow, clearly telegraphed attacks | One explosive rocket | Supplies crowd-clearing ammunition |
| Elite variant | Uses its base enemy's behaviour with one explicit modifier | Same payload identity | Increases difficulty without an entirely new enemy |

Start with three health for an acolyte, two for a pursuer, five for a scatter caster, and eight for a siege familiar. Ordinary returned bolts deal one damage; rockets deal five within a 1.6-unit explosion radius. Incoming versions still deal one player hit. Treat these as tuning defaults.

Enemy movement uses the flat arena and simple steering. Avoid introducing navigation complexity before the chosen arena requires it. Enemies cannot spawn on the player; show a spawn warning before they become dangerous. Clearly telegraph melee wind-up, firing direction, and rocket launch.

### Weapon implementation

The player has a **payload system**, not a conventional weapon inventory. Share attack definitions between enemy emission and player return, with faction and modifier context determining who can be hurt. Returning an attack does not create a different unrelated weapon.

Build ordinary bolts first, then fan volleys, then rockets and explosions. Heavy shots, fireballs, laser recordings, shockwaves, and stealing charge momentum are expansion candidates, not first-version requirements. Never mark a boss attack capturable until a return behaviour exists for it.

### First boss: the Collector

The Collector alternates three readable patterns: a bolt stream, a fan volley, and a rocket attack. Include short repositioning gaps but no extended period with neither targets nor ammunition. The boss remains damageable in the baseline version.

Start with 120 health, then tune so a new character with no permanent skills can win within the short-mode boss window. Test this assumption before adding mastery progression. A repeated endless boss gains one predefined pattern variation, such as a second fan angle, rather than only more health. Charges and melee attacks are avoidable hazards, not automatically stealable abilities.

## 5. Temporary run upgrades

All upgrades below expire when the run ends. Short mode gives three choices, one at each encounter transition. Offer three distinct eligible upgrades and let the player pick one. Randomness is seeded for reproducible debugging. Short mode does not offer a previously selected upgrade again.

| ID / upgrade | Rank-one behaviour | Endless rank scaling |
|---|---|---|
| `piercing_return` — Piercing Return | Returned non-explosive projectiles pass through one extra enemy | One extra unique target per rank; maximum rank 3 |
| `echo_volley` — Echo Volley | Repeat the release after 0.20 seconds at 25% damage | Echo damage 25%, 40%, 55%; one echo, not recursive copies |
| `heavy_orbit` — Heavy Orbit | Stored packets damage nearby enemies for 1 every 0.35 seconds | Radius 1.0, 1.2, 1.4 units; each enemy has its own hit interval |
| `parting_gift` — Parting Gift | Release creates a 1-damage blast around the player | Damage 1, 2, 3; radius 1.5, 1.75, 2.0 units |
| `final_second` — Final Second | Add 20 percentage points to perfect-shot damage bonus | Additional bonus 20%, 40%, 60%, added to baseline 15% |
| `overflow` — Overflow | A catch with both slots full releases the oldest packet at 65% power | Immediate release power 65%, 80%, 95% |

Overflow triggers at most once per catch activation and only upon a successful interception that needs a new slot. It does not eject a packet on an empty click or repeatedly release while appending shots. Natural expiry always uses normal power. Apply the overflow multiplier to release-derived damage, including its echo and parting blast.

Projectiles with piercing keep a set of already-hit actor IDs. Explosions damage an actor once per explosion. Echoes inherit provenance and cannot produce their own echoes. Orbit damage does not count as a returned-volley hit for combo building. Compute modifiers in a documented order; do not mutate the original attack-definition asset.

Intended build examples: Walking Arsenal combines orbit damage and parting blasts; Return Specialist combines perfect catches and piercing; Overflow Engine combines forced releases and echoes. These are playtest hypotheses, not balance guarantees.

## 6. Run modes, score, and replayability

### Short mode

- Three 40-second encounters followed by a boss window of at most 60 seconds: maximum 180 active gameplay seconds.
- Upgrade choices at 40, 80, and 120 seconds pause gameplay. Existing packets and enemies are preserved through the choice; all timers remain frozen.
- Stop ordinary spawn scheduling at the boss transition. Despawn remaining ordinary enemies and their unclaimed projectiles without score or XP; preserve already captured packets. Introduce the boss with a visible warning.
- Encounter-one roster: acolytes and a few pursuers. Encounter two adds scatter casters. Encounter three adds siege familiars and mixed formations.
- Defeating the boss wins immediately. Death loses immediately. A living boss at 180 seconds produces a `TimeExpired` loss. Present the objective clearly from the start.
- Maximum simultaneously active ordinary enemies: 12. Queued spawns wait when the limit is reached rather than silently increasing pressure.
- Use authored formations with seeded variation, not random placements that can surround the player without warning.

### Score and records

Initial kill values: pursuer/acolyte 10, scatter caster 20, siege familiar 25, elite 1.5 times its base value, boss 250. Add two points per unused active second on a short-mode victory.

A packet's first successful returned hit increases the combo multiplier by 0.25, from 1.0 to a cap of 3.0, and refreshes a five-second combo timer. Further hits from that packet or its echo do not increase it again. Score kills using the current multiplier. Taking damage resets the multiplier. Combo time freezes during menus. Catching alone gives no score.

Track total score, duration, bosses defeated, kills by attack type, packets released/hit, perfect shots, damage taken, and best release's distinct kills. A release and its echo share one root release identifier. Use that identifier to count a best volley without double-counting enemies.

Personal records are separated by mode and capture style, with build version, seed, mastery level, and equipped passive IDs attached. Permanent stats affect comparisons; do not present unlike loadouts as a fair global competitive leaderboard. Provisional rank thresholds live in configuration and must be tuned from actual sessions.

### Endless mode

Endless uses the same combat, upgrades, enemies, boss, score events, and profile. It adds a different encounter scheduler rather than duplicating the game.

- Normal waves last 30 active seconds. Provide an upgrade after every two waves.
- After six normal waves, stop regular spawns, clean up ordinary enemies/projectiles without rewards, and start a boss encounter. The boss encounter ends on boss defeat or player death, not on a fixed timer.
- Defer the wave-six upgrade until that boss is defeated. Repeat the six-wave/boss cycle; restore one health after each defeated boss, clamped to maximum health.
- Endless upgrades may increase owned upgrades to rank 3. Once all six upgrades reach their caps, stop offering choices; do not create an unbounded damage multiplier.
- Permit retiring between waves; keep earned progression and record the result as `Retired`, distinct from death or victory. This is not save-and-resume of an unfinished run.
- Escalate combinations and elite frequency first, then bounded movement/attack frequency. Keep active ordinary enemies capped at 18, bullets at a defined pool limit, and telegraphs above a readable minimum.
- Initial scaling per completed six-wave cycle: health +15%, movement +5% up to +25%, attack interval -5% down to 70% of baseline. Boss health +20% per cycle. Revisit these values after baseline combat tests.
- If the projectile budget is full, delay an emission with a visible/readable continuation; never drop dangerous bullets invisibly or recycle live shots beneath the player.

Replayability comes from build experimentation, different capture styles, mixed formations, increasing mastery, personal records, and surviving harder encounters. Achievements and the tree provide finite milestones; they are not the only reason to replay.

## 7. Permanent progression, achievements, and customization

### Mastery and points

Mastery is account-wide and capped at level 10 for the first version. Start at level 1 with zero points. Each level gained awards one skill point, for nine total points. Unlock all nine passive nodes through play; equip at most three at once so completed progression still permits different loadouts.

XP required to advance from level `L` to `L+1` is `100 + 50 * (L - 1)`. Carry excess XP through multiple level gains. At level 10, preserve lifetime statistics without issuing further spendable points.

Initial run XP:

```text
2 * normal enemy kills
+ 5 * elite enemy kills
+ 35 * boss kills
+ 5 * completed normal encounters
+ min(20, perfect shots that subsequently damage an enemy)
```

Count each perfect shot once for XP, even if it pierces several targets. An elite kill uses the elite value instead of also receiving the normal value. Short-mode timed encounters count when the player survives their transition; endless normal waves count at their transition. Despawning an enemy never counts as a kill. Capture quantity, idle time, and lantern emissions give no XP on their own.

Award this earned XP on death, victory, time expiry, or retirement. Finalize each run exactly once using a unique run ID. No achievement awards additional skill points in the baseline, so the tree's economy remains consistent. Points and mastery belong to the profile; run upgrades belong only to the current run.

### Nine-node skill tree

Each node costs one point. Tier-one requires mastery level 2; tier-two level 4 and its branch's tier-one node; tier-three level 7 and its branch's tier-two node. Prerequisites must be owned, not necessarily equipped.

| Branch / tier | Stable ID | Permanent passive when equipped |
|---|---|---|
| Precision 1 | `precision_angle` | Add 15 degrees to total catch angle |
| Precision 2 | `precision_capacity` | Add 2 energy units to each packet's capacity |
| Precision 3 | `precision_recovery` | Reduce post-catch recovery by 0.10 seconds |
| Mobility 1 | `mobility_speed` | Increase movement speed by 5% |
| Mobility 2 | `mobility_dash_recovery` | Reduce dash cooldown by 0.10 seconds |
| Mobility 3 | `mobility_dash_distance` | Add 0.30 units to dash distance; keep dash duration unchanged |
| Resilience 1 | `resilience_grace` | Add 0.15 seconds of post-hit invulnerability |
| Resilience 2 | `resilience_health` | Add one maximum health; start runs at that maximum |
| Resilience 3 | `resilience_dash_grace` | Add 0.04 seconds of dash invulnerability, capped at dash duration |

Buying, equipping, and respeccing occur between runs. Free respec refunds all purchased nodes and unequips them, preserving mastery, achievements, records, and lifetime statistics. Validate point balance, mastery gate, prerequisite ownership, duplicate purchase, and the three-equipped-node limit in domain logic, not only disabled UI buttons.

### Achievements

Implement achievements from gameplay events with stable IDs. Persist cumulative counters and completion once; do not repeatedly grant rewards when a completed condition occurs again. Functional badges suffice until cosmetics are supplied.

| ID / name | Exact first-version condition |
|---|---|
| `return_policy` — Return Policy | A returned payload kills the same actor identified as its original source |
| `first_borrow` — First Borrow | A captured payload damages any enemy |
| `crowd_control` — Crowd Control | One root release, including its echo, kills five distinct enemies |
| `perfect_timing` — Perfect Timing | Five perfect shots damage enemies in one run |
| `untouchable` — Untouchable | Finish a normal short-mode encounter without losing health during it |
| `mixed_bag` — Mixed Bag | Defeat enemies using both returned ordinary bolts and returned rockets in one run |
| `final_notice` — Final Notice | Win short mode |
| `second_encore` — Second Encore | Defeat two bosses in one endless run |
| `persistent_student` — Persistent Student | Reach mastery level 5 |
| `fully_trained` — Fully Trained | Reach mastery level 10 |

Death must not erase an achievement already earned during a run. Rewards are badges initially and cosmetic IDs when assets are available; core functionality does not depend on receiving those assets.

### Capture styles

Build Snatcher first. Add the other two only after the basic loop, modifiers, and progression work. All styles are available when their implementation phase is complete; no extra unlock grind is required.

| Style | Catch behaviour | Trade-off |
|---|---|---|
| Snatcher | Baseline cone, range, window, and recovery | Balanced and precise |
| Collector | 140-degree cone, 0.40-second window, 1.00-second post-window recovery | Broad interception with more commitment |
| Daredevil | Catch action performs a dash and captures along its forward swept path during that dash | Aggressive interception tied to dash availability |

For Daredevil, Catch and Dash share the dash cooldown. Space performs the same capturing dash; pressing both on the same tick produces one action. Its packet lifetime and capacity remain unchanged. Permanent dash skills apply; catch-recovery reductions do not shorten its shared dash cooldown. Precision angle affects the swept capture cone.

Store selected style and cosmetic IDs in the profile. Invalid or removed IDs fall back to Snatcher/default appearance. Cosmetic selections must never affect hitboxes or hide expiration indicators.

## 8. Unity architecture and execution conventions

### Verified environment

Read-only checks on 2 October 2026 established:

- Unity CLI executable: `C:/Users/Rachit/AppData/Local/Unity/bin/unity.exe`.
- CLI version: `1.0.0-beta.8`.
- Installed editor: `6000.3.5f1`, under `C:/Program Files/Unity/Hub/Editor/6000.3.5f1/Editor/Unity.exe`.
- Installed 3D URP template ID: `com.unity.template.urp-blank`.
- The CLI reported no connected Pipeline-enabled editor. This does not prove that no other editor process is open.
- This chat's directory contains no existing Unity project to modify. The project and every file path below are proposed, not existing implementation.

An additional read-only check on 2 October 2026 found `6000.3.25f1` as the latest 6.3 LTS patch in the CLI release catalog. Its official release page records 24 September 2026. This is the recommended project version; it has not been installed during planning. Unity 6.3 LTS is supported until December 2027. There is no identified game requirement that needs a newer Update-release branch.

Recheck availability at execution time. Install/choose the recommended editor and its Web Build Support module before project creation, using the user's actual installation authorization. Rediscover templates for that exact version: the template ID above was verified against the currently installed older patch. Inspect template-installed packages before adding dependencies. Keep Windows and Web build profiles on the same pinned editor.

### Proposed project layout

Paths below are relative to the future Unity project root.

```text
Assets/Game/
  Scenes/                 Bootstrap.unity, Arena.unity
  Scripts/
    Core/                 clock, run context, IDs, event records
    Player/               input, movement, aim, health, dash
    Combat/               projectiles, payloads, capture, packets, release, pooling
    Enemies/              steering, telegraphs, attack emitters, boss
    Runs/                 encounter director, mode rules, statistics, score
    Upgrades/             definitions, eligibility, modifier application
    Progression/          profile, mastery, skill tree, achievements, records
    UI/                   HUD, menus, choices, results, progression screens
    Presentation/         sprite view and replaceable feedback adapters
  Data/                   immutable definitions and tuning assets
  Prefabs/                functional player, enemy, projectile, and UI prefabs
  Editor/                 repeatable scene/content bootstrap and build entry point
  Tests/EditMode/         domain rules, clocks, modifiers, profile validation
  Tests/PlayMode/         collision, capture, restart, scene and mode integration
Docs/
  IMPLEMENTATION_STATUS.md
  DECISIONS.md
  TEST_EVIDENCE.md
```

Use a small runtime assembly, editor-only assembly, and separate test assemblies. Do not build a generic dependency-injection framework, networking layer, or ECS conversion for this jam.

### Coordinate and physics model

Gameplay uses world XZ with Y fixed at the ground plane. Visual sprite children can face the camera independently. Mouse aim is a camera ray projected onto the gameplay plane. Use one 3D collision model consistently; do not mix Physics2D with 3D actor collision.

Sweep fast projectiles and dash movement across their travelled segment rather than relying only on final-frame overlaps. Include initial-overlap handling: a sphere cast alone does not cover every already-overlapping case. Resolve obstacles, capture, and actor impacts in travel order, with the capture-versus-player-hit rule from section 3.

Use explicit layers/factions for player body, enemy body, obstacles, capture region, hostile attacks, and returned attacks. Separate visual height from logical hit position so angled camera art cannot create misleading collisions.

### Shared contracts to establish before parallel implementation

These are proposed contracts. Define them centrally and update this document if an implementation changes them.

```csharp
public enum GameMode { Short, Endless }
public enum RunEndReason { Victory, Death, TimeExpired, Retired }
public enum AttackKind { Bolt, HeavyShot, Rocket }
public enum AttackFaction { Hostile, Returned }

public interface IGameplayClock
{
    double Now { get; }
    bool IsPaused { get; }
    void Advance(float deltaSeconds);
    void SetPaused(bool paused);
}
```

Domain data responsibilities:

- `AttackDefinition`: immutable ID, kind, movement/collision parameters, base damage, energy cost, explosion parameters, and capturability.
- `AttackSnapshot`: copied definition values plus source actor ID, shot ID, spread offset, and perfect-catch flag; no live GameObject dependency.
- `CapturedPacket`: packet ID, capture timestamp, expiry timestamp, capacity usage, payload snapshots, and lifecycle status.
- `RunContext`: run ID, mode, seed, build version, style ID, passive IDs, effective starting stats, and gameplay clock.
- `DamageEvent`: unique damage ID, target actor ID, original source actor ID, shot ID, root release ID, attack kind, amount, and effect category.
- `KillEvent`: unique victim ID, damage provenance, reward eligibility, and actor category.
- `RunSummary`: end reason, frozen statistics, XP earned, achievements earned, and loadout metadata.
- `PlayerProfile`: schema version, mastery XP/level, available points, purchased/equipped node IDs, achievement counters/completions, records, selected style, and last finalized run IDs.

Central APIs:

```text
CaptureController.TryActivate(aimDirection) -> bool
CaptureController.TryCapture(projectileSnapshot, impactContext) -> capture result
PacketStore.Advance(gameplayTime) -> packets that expired this tick
ReleaseService.Release(packet, origin, aim, powerMultiplier) -> root release ID
RunController.Begin(mode, seed, loadout) -> run ID
RunController.End(reason) -> immutable RunSummary, once only
ProfileRepository.Load() -> validated profile and recovery status
ProfileRepository.Save(profile) -> success or explicit failure
ProgressionService.FinalizeRun(summary, profile) -> updated profile, idempotently
SkillTreeService.TryPurchase(nodeId, profile) -> result with rejection reason
```

Use typed events for score, achievements, and presentation. Combat must continue working if a sound or animation listener is absent. Avoid scene-wide searches each frame and uncontrolled static event subscriptions.

### Unity CLI workflow

During this drafting task, no project was created and no installation or editor settings were changed. At build time, start with read-only discovery:

```powershell
unity --version
unity editors --installed --format json
unity templates list --editor 6000.3.25f1 --installed --format json
unity auth status --format json
unity license status --format json
unity status --format json
```

After the user supplies the repository checkout and the recommended editor is installed, verify its 3D URP template and create the project inside the agreed repository layout. The example parent below is only illustrative; replace it with the actual repository's chosen project parent. Run the creation command only if template discovery confirms the specified ID for that editor:

```powershell
$gameParent = 'C:/Users/Rachit/Documents/Codex/2026-10-01/i-x20'
unity projects create BorrowedHex --path $gameParent --editor-version 6000.3.25f1 --template com.unity.template.urp-blank --no-cloud --no-initial-commit
```

Do not pass a VCS provider, create a remote, accept license terms, sign in, or install a different editor as an incidental planning step. Use existing authorization during execution and report real blockers clearly.

Before scene/prefab/asset mutations, run `unity status`, then discover `unity command` or `unity list`. Use the commands actually exposed by that editor. Install the Pipeline package through the documented CLI workflow if needed; inspect local help for its exact arguments. If connectivity fails while an editor is running, inspect `unity pipeline list` and compile logs for Safe Mode before treating the editor as absent.

Create scenes, prefabs, ScriptableObjects, and Input Action assets through Unity editor APIs or discovered live commands. C# source files can use normal file editing. Never hand-author GUID/fileID YAML for scenes or prefabs while a live editor is reachable. Scene bootstrap code must be repeatable without duplicating objects or overwriting unrelated content.

### Persistence

Use one validated, versioned JSON profile schema with platform-specific storage adapters. ScriptableObject definition assets are not player save files.

- Windows: JSON under `Application.persistentDataPath`, using temporary files and backup/replace. Preserve corrupt files for diagnosis, try a valid backup, and report recovery.
- Web: browser-backed PlayerPrefs stores the compact JSON profile; Unity uses IndexedDB for Web PlayerPrefs with a 1 MB limit. Keep each serialized snapshot under 64 KiB, retain two generation-numbered snapshot keys, and load the newest valid snapshot. Bound record/run-receipt history. Call `PlayerPrefs.Save()` at explicit save points and test persistence by refreshing/reopening the browser. Do not use desktop filesystem rename assumptions for this adapter.
- Report unavailable/full browser storage and offer in-memory play with a visible progress-saving warning. Browser saves are local to that browser/site; no cross-device or Windows/Web save synchronization is promised.

At run end, apply XP, achievements, and records together in one finalized profile transaction. Duplicate result callbacks cannot award them twice. Save after skill purchases, respec, loadout changes, and finalization. Show save failures with a retry option and retain the in-memory summary.

Application suspension or quitting during combat on Windows should attempt to finalize earned progress as a retired run. Browser focus loss pauses; do not bank rewards merely for switching tabs. A browser-close/unload callback is not a reliable save mechanism: save completed results and progression changes while the page is active. A hard crash or closing an unfinished browser run is not promised to recover every unsaved second. Resuming a live run is outside scope. Tests use isolated storage and never the player's real save.

### Windows and Web compatibility

Keep all combat/progression code platform-neutral. Use the WebGL 2 compatibility path initially and a basic 3D URP renderer; no required WebGPU, compute-only effects, or native plugin dependencies. Use platform adapters only for storage, application exit, and browser interaction. Browser Quit returns to the game's menu rather than attempting to close the tab.

Build a browser prototype in Phase 0 and repeat browser smoke checks after major combat phases. Serve the build over HTTP using Unity's build/run server or a configured local static server, not a `file://` URL. Match compression settings to server support; begin with uncompressed development builds when hosting headers are unknown. Test resizing, canvas focus, tab switching, input-map separation, and storage reload on desktop Chrome/Edge and Firefox. Explicitly verify a browser-safe pause action because Escape can also exit fullscreen; provide a visible Pause button and `P` as a fallback binding on both platforms. Publishing/uploading remains a separately requested action.

### Review focus

1. Capture and damage on the same tick: one outcome, never both consumed and damaging.
2. Expiry during pause/death/restart: timers freeze appropriately and effects never leak across runs.
3. Upgrade combinations: provenance remains correct; echoes and overflow cannot recurse indefinitely.
4. Progress finalization and damaged saves: rewards are idempotent and recoverable data is preserved.
5. Endless saturation: enemy/projectile limits preserve readable combat and do not steal live objects from pools.

## 9. Phased implementation checklist

Complete the phases in ascending order. Each phase includes a playable deliverable and an acceptance gate. Add only tests that exercise meaningful rules or integration failures; do not test decorative placeholder details. For combat/progression domain rules, write the failing test first, implement the rule, and run it again. Record actual outcomes rather than marking planned checks as passed.

### Phase 0 — Project and repeatable CLI foundation

**Depends on:** This brief.  
**Deliverable:** A local Unity project that opens, compiles, runs an empty arena, and produces Windows and Web prototypes.

**Proposed files:** `Assets/Game/Editor/ProjectBootstrap.cs`, `Assets/Game/Editor/BuildGame.cs`, runtime/editor/test `.asmdef` files, `Assets/Game/Scenes/Bootstrap.unity`, `Arena.unity`, and `Docs/IMPLEMENTATION_STATUS.md`.

- [ ] Recheck editor, CLI, template, auth/license availability, destination, and project-specific instructions. If an existing project is supplied, read its `CLAUDE.md`/`AGENTS.md` and preserve its established layout.
- [ ] Create/open the project through Unity CLI using the pinned 3D URP template. Record the actual package versions and editor version in the status document.
- [ ] Establish the 2.5D foundation: a fixed elevated camera looking onto a flat 3D floor, arena boundaries, basic lighting, and gameplay collision layers through editor APIs. Keep all gameplay positions on XZ; no vertical combat or platforming.
- [ ] Establish the gameplay clock and run ID generator. Ensure UI can pause the clock independently of presentation.
- [ ] Create repeatable editor bootstrap/build methods; put Bootstrap then Arena in the build scene list. A second bootstrap invocation must not duplicate scene objects.
- [ ] Confirm/install Web Build Support for the selected editor. Add Windows and Web build profiles with the same scenes; make early builds for both targets and launch the Web build through HTTP.
- [ ] Resolve compilation, scene references, input-package setup, and browser canvas focus before gameplay work.

**Checks:** Bootstrap twice yields one arena/camera; compile succeeds; Windows and HTTP-served Web builds launch; browser resize/focus works; a paused clock remains unchanged after advancement requests; run IDs differ on restart. Record license or CLI failures as infrastructure failures rather than game-test failures.

### Phase 1 — Player movement, aiming, dash, health

**Depends on:** Phase 0.  
**Deliverable:** A controllable placeholder magician in a bounded arena.

**Proposed files:** `PlayerInputReader.cs`, `PlayerMotor.cs`, `AimResolver.cs`, `DashController.cs`, `PlayerHealth.cs` under `Scripts/Player/`; `PlayerTuning.cs` under `Data/`; `PlayerMovementTests.cs`, `PlayerHealthTests.cs`.

- [ ] Create Gameplay and UI Input Action maps with the bindings from section 2.
- [ ] Implement normalized camera-relative movement and mouse-to-plane aim. Keep visual facing independent from movement.
- [ ] Create `Scripts/Presentation/CharacterView.cs` with a camera-facing 2D stand-in as a child of the player's 3D collision root. Final sprites are not required; verify screen-space appearance and ground anchoring now so 2.5D is built in from the start.
- [ ] Implement collision-respecting dash distance, duration, cooldown, and invulnerability. Capture is not attached to dash yet.
- [ ] Implement health loss and post-hit invulnerability through one damage entry point. Death raises one event.
- [ ] Add functional health/dash displays, pause/resume, and an arena reset button for development.

**Checks:** Diagonal speed equals straight speed; dash stops at a wall; no aim projection preserves the last valid direction; repeated hits during invulnerability cost one health; two lethal callbacks emit one death; UI clicks do not dash or catch; focus loss pauses combat.

### Phase 2 — Incoming projectiles and one enemy source

**Depends on:** Phase 1.  
**Deliverable:** An acolyte telegraphs and shoots at the player; hits and avoidance work.

**Proposed files:** `AttackDefinition.cs`, `AttackSnapshot.cs`, `ProjectileActor.cs`, `ProjectileResolver.cs`, `ProjectilePool.cs` under `Combat/`; `AttackEmitter.cs`, `BoltAcolyte.cs` under `Enemies/`; `ProjectileCollisionTests.cs`.

- [ ] Define immutable attack data and copied snapshot data. Assign unique actor/shot IDs within a run.
- [ ] Build a projectile lifecycle: spawn, swept movement, obstacle/player impact, expiry, and return to pool.
- [ ] Reset every mutable field on reuse, including faction, lifetime, hit history, and provenance.
- [ ] Create one stationary or simple-steering acolyte with a readable aiming telegraph and three-bolt volley.
- [ ] Add the test/lantern emitter using the same projectile code. Keep capture disabled until the next phase.

**Checks:** A fast shot crossing the player during one timestep still hits; wall collision blocks a later actor impact; a projectile cannot damage twice after despawning; hostile and returned factions obey their own collision rules; reused objects contain no old IDs or piercing targets.

### Phase 3 — Catch, carry, and three-second return

**Depends on:** Phase 2.  
**Deliverable:** The complete signature mechanic against one enemy.

**Proposed files:** `CaptureController.cs`, `CaptureGeometry.cs`, `CapturedPacket.cs`, `PacketStore.cs`, `ReleaseService.cs`, `PacketIndicator.cs`; `CaptureRuleTests.cs`, `PacketLifetimeTests.cs`, `CaptureIntegrationTests.cs`.

- [ ] Implement one short directional catch window with recovery and atomic projectile interception.
- [ ] Create a packet only on first success; append eligible shots during that window within capacity.
- [ ] Add a second independent packet slot. Draw orbit placeholders and separate shrinking countdown indicators.
- [ ] Release expired payloads at the current player position/aim with retained spread and original-source attribution.
- [ ] Implement full-packet rejection, full-slot rejection, friendly-shot rejection, and expiry-before-capture tick ordering.
- [ ] Cancel pending work on death/restart and freeze packet timers during pause.
- [ ] Playtest repeated catches and returns with no progression or upgrades. Tune interception readability before continuing.

**Required boundary vectors:** A packet first captured at `t=1.0` does not release at `t=3.99` and releases once at `t=4.0`; appending at `t=1.20` keeps expiry at `4.0`; a rocket of cost 4 cannot fit in a packet with 10 of 12 units occupied; deleting the original shooter does not break release.

**Checks:** Successful same-tick capture prevents that shot's player damage; uncaptured shots still hurt; two slots expire independently; pause does not consume lifetime; returned shots cannot be recaptured; restarting cancels old releases. Human gate: a player can intentionally catch and return multiple consecutive volleys, and the result feels worth repeating.

### Phase 4 — Enemy roster and distinct borrowed weapons

**Depends on:** Phase 3.  
**Deliverable:** A mixed encounter with positioning pressure and multiple useful ammunition types.

**Proposed files:** `EnemyHealth.cs`, `EnemySteering.cs`, `Pursuer.cs`, `ScatterCaster.cs`, `SiegeFamiliar.cs`, `ExplosionResolver.cs`, `EnemySpawnService.cs`, enemy/payload definitions and prefabs; `AttackPayloadTests.cs`, `EnemyEncounterTests.cs`.

- [ ] Add enemy health, unique kills, reward eligibility, steering, and telegraphed contact strikes.
- [ ] Add five-shot fans that preserve their spread on return.
- [ ] Add rockets with a single-impact explosion and per-target deduplication.
- [ ] Add spawn warning markers and a safe minimum distance from the player.
- [ ] ~~Enable the lantern's ammunition-starvation condition from section 3.~~ Replaced by the parry (D26).
- [ ] Author small mixed formations instead of adding more enemy types.

**Checks:** A returned rocket damages enemies but not the player; its explosion hits an actor once; killing a shooter with its own snapshot preserves source attribution; a melee-only remainder remains solvable; an enemy cannot spawn on the player; a returned fan remains recognizable. Verify each enemy's attack provides a distinct tactical opportunity.

### Phase 5 — Complete short run, boss, statistics, score

**Depends on:** Phase 4.  
**Deliverable:** A beginning-to-end short game with death, victory, time expiry, results, and restart. Upgrade transitions may initially present a Continue button.

**Proposed files:** `RunController.cs`, `RunContext.cs`, `ShortModeRules.cs`, `EncounterDirector.cs`, `CollectorBoss.cs`, `RunStatistics.cs`, `ScoreService.cs`, `RunSummary.cs`, `GameplayHud.cs`, `ResultsPanel.cs`; `RunLifecycleTests.cs`, `ScoreTests.cs`, `ShortRunTests.cs`.

- [ ] Implement explicit states: Ready, Combat, UpgradeChoice, BossIntro, BossCombat, Paused, Results. Returning from pause restores the prior state.
- [ ] Schedule the three encounters and boss transition using active gameplay time, with capped spawning and authored formations.
- [ ] Implement the boss's three attacks through existing payload definitions.
- [ ] Introduce typed hit/kill/release events, score/combo rules, and root release grouping.
- [ ] Freeze a summary once at end; display score, best volley, hit rate, damage, duration, and reason.
- [ ] Reset the entire run and support immediate replay. Profile-related summary fields are calculated in memory until saving is added.

**Checks:** Transitions occur at 40/80/120 seconds with timers paused during choices; a surviving boss at 180 ends the run; death and simultaneous boss defeat follow one documented terminal ordering (player death takes precedence if both are resolved in one tick); boss death otherwise wins; cleanup despawns give no rewards; restarting repeatedly never duplicates event listeners. A zero-passive character must be able to defeat the boss.

### Phase 6 — Temporary interacting upgrades and build identity

**Depends on:** Phase 5.  
**Deliverable:** Three choices per short run that noticeably change combat.

**Proposed files:** `RunUpgradeDefinition.cs`, `RunUpgradeState.cs`, `UpgradeOfferService.cs`, `CombatModifierPipeline.cs`, `UpgradeChoicePanel.cs`; `UpgradeEligibilityTests.cs`, `ModifierCombinationTests.cs`.

- [ ] Implement the six upgrades and rank-one behaviour from section 5.
- [ ] Offer three distinct eligible choices; show exact effects. Exclude already-owned short-mode upgrades.
- [ ] Introduce perfect-catch geometry and bonus before enabling Final Second offers.
- [ ] Implement release modifier ordering: copied base payload → perfect-shot bonus → overflow power → echo power, where applicable. Explosion/orbit effects have their own stated damage rules.
- [ ] Prevent recursive echoes, repeated piercing hits, and more than one overflow eviction per catch activation.
- [ ] Ensure all upgrade state disappears on the next run.

**Checks:** Seeded offers reproduce under the same content version; natural expiry does not receive the overflow penalty; an empty full-slot catch does not evict anything; Echo plus Overflow yields one primary and one weaker echo; pause freezes echo delay; Heavy Orbit respects per-enemy intervals. Playtest at least the three intended build families from section 5.

### Phase 7 — Functional menus and reliable player profile

**Depends on:** Phase 6.  
**Deliverable:** Launch, play, finish, close, and reopen with valid persistent records. No final UI art is required.

**Proposed files:** `PlayerProfile.cs`, `ProfileRepository.cs`, `ProfileValidator.cs`, `ProgressionService.cs`, `MainMenu.cs`, `SettingsPanel.cs`, `ProfileRecoveryPanel.cs`; `ProfilePersistenceTests.cs`, `RunFinalizationTests.cs`.

- [ ] Implement versioned profile JSON, a default new-player profile, and Windows-file/Web-PlayerPrefs storage adapters.
- [ ] Validate numeric ranges, known IDs, point balance, and finalized run receipts; save through temporary/backup files.
- [ ] Route terminal run summaries through a single idempotent finalization service.
- [ ] Save records/loadout metadata now; connect mastery and achievement fields in the following phases.
- [ ] Add menu actions for Play Short, Settings, Quit, and future mode/style panels. Keep unavailable functionality visibly disabled rather than pretending it exists.
- [ ] Add sensitivity, platform-appropriate fullscreen/window behaviour, and functional readability options. Browser Quit returns to the menu. Do not build a cosmetic menu before assets arrive.

**Checks:** Load/save round-trip preserves values; finalizing the same run twice adds no duplicate rewards; invalid primary save uses valid backup and reports recovery; invalid Windows files are preserved and fresh-profile recovery is explicit; failed save retains the summary for retry. Verify Web progress survives refresh on the same origin and unavailable browser storage is reported. Switching menu screens does not resume combat accidentally.

### Phase 8 — Mastery, permanent passive tree, and loadouts

**Depends on:** Phase 7.  
**Deliverable:** Players earn progression after success or failure, spend points on gated nodes, and select passive loadouts.

**Proposed files:** `MasteryService.cs`, `SkillNodeDefinition.cs`, `SkillTreeService.cs`, `PlayerStatResolver.cs`, `SkillTreePanel.cs`, `MasteryPanel.cs`; `MasteryTests.cs`, `SkillPurchaseTests.cs`, `LoadoutStatTests.cs`.

- [ ] Implement the level thresholds and XP formula from section 7, including excess XP and the level-10 cap.
- [ ] Grant one point per gained level, including multiple gains from one summary.
- [ ] Implement all nine node definitions and validate every purchase in the service.
- [ ] Add three active passive slots, prerequisite ownership rules, and free respec.
- [ ] Resolve base style stats plus equipped permanent passives once at run start. Run upgrades remain separate modifiers.
- [ ] Display XP progress, locked-tier requirements, point cost, rejection reasons, and equipped skills with placeholder UI.

**Checks:** Level 1 at 99 XP stays level 1; reaching 100 grants level 2 and one point; multiple thresholds grant the correct total; duplicate finalization grants nothing further; a locked or unaffordable node is rejected; owned-but-unequipped prerequisites suffice; a fourth equipped node is rejected; respec preserves mastery/records and refunds the correct points. A death with earned XP still advances the profile.

### Phase 9 — Achievements and personal achievement feedback

**Depends on:** Phase 8.  
**Deliverable:** Ten real achievements with accurate counters and results feedback.

**Proposed files:** `AchievementDefinition.cs`, `AchievementService.cs`, `PersonalRecordService.cs`, `AchievementPanel.cs`, achievement badge/toast placeholder; `AchievementConditionTests.cs`, `RecordGroupingTests.cs`.

- [ ] Implement section 7's exact conditions from existing combat/run events.
- [ ] Distinguish per-run, per-encounter, cumulative, and profile-level conditions.
- [ ] Persist completion once and present earned badges on results; avoid covering active combat with large pop-ups.
- [ ] Record best runs by mode/style with mastery/loadout/build-version metadata.
- [ ] Add a concise result comparison: previous best, current result, and one accurate near-completed achievement.

**Checks:** Piercing/echoes cannot count the same victim twice; a returned attack killing a different actor does not earn Return Policy; no-hit encounter tracking resets at the correct boundary; death preserves completed achievements; repeat completion has no duplicate reward. Second Encore remains inactive until the endless implementation provides real boss events.

### Phase 10 — Capture-style customization

**Depends on:** Phase 9.  
**Deliverable:** Three selectable capture styles with genuine differences and saved passive loadouts.

**Proposed files:** `CaptureStyleDefinition.cs`, `CaptureStyleResolver.cs`, `DaredevilCapture.cs`, `LoadoutPanel.cs`, `CharacterView.cs`; `CaptureStyleTests.cs`, `DaredevilIntegrationTests.cs`.

- [ ] Move baseline capture parameters into Snatcher's definition without changing its behaviour.
- [ ] Implement Collector's wider, longer, slower-recovering catch.
- [ ] Implement Daredevil's swept capturing dash and shared action cooldown.
- [ ] Apply equipped permanent passives consistently and show effective stats before starting.
- [ ] Save selection and provide a basic view adapter so supplied sprites can replace shapes later.

**Checks:** Same-frame Catch and Dash trigger one Daredevil action; its sweep catches attacks along the travelled route but not through a wall; all styles retain two slots and three-second lifetime; Collector's recovery is not accidentally Snatcher's; switching styles occurs only outside a run. A missing cosmetic produces a visible default, not an invisible player.

### Phase 11 — Endless scheduler, recurring bosses, and scaling

**Depends on:** Phase 10.  
**Deliverable:** Endless mode reuses the finished game and produces survival records and progression.

**Proposed files:** `EndlessModeRules.cs`, `EndlessEncounterDirector.cs`, `DifficultyScaling.cs`, repeat boss-pattern data; `EndlessScheduleTests.cs`, `UpgradeRankTests.cs`, `EndlessStressTests.cs`.

- [ ] Implement 30-second waves, choices every two waves, and a boss after every six normal waves.
- [ ] Defer a wave-six choice until boss defeat; restore one health and resume the next cycle.
- [ ] Add upgrade ranks up to three and an eligible-offer fallback when fewer than three choices remain.
- [ ] Skip exhausted choices when the build is complete. Keep scaling enemies within the stated readability limits.
- [ ] Add elites and one repeat-boss pattern variation. Reuse attack definitions and enemy code.
- [ ] Enable retiring between waves, earned-progress finalization, and survival records.
- [ ] Run a scripted/debug-assisted long session to verify object budgets, timers, integer ranges, and restart after many cycles. Debug sessions cannot submit profile rewards or records.

**Checks:** Boss cadence is correct over at least two cycles; boss time does not accidentally advance normal-wave scheduling; capped upgrades are never offered; pause freezes scaling timers; enemy/projectile limits are respected; retirement banks earned progress once. A late death creates a complete valid summary without overflow or duplicate boss rewards.

### Phase 12 — Gameplay integration, tuning, and build handoff

**Depends on:** Phase 11.  
**Deliverable:** A stable, fully playable placeholder build ready for team testing and later asset integration.

**Proposed files:** existing tuning definitions, `BuildGame.cs`, regression tests, `Docs/TEST_EVIDENCE.md`, `Docs/DECISIONS.md`, and final functional controls/readme.

- [ ] Run focused domain and PlayMode suites; fix unresolved failures rather than repeatedly running unrelated suites.
- [ ] Test fresh profile and advanced profile, all styles, each upgrade, intended combinations, both modes, pause, focus loss, death, retirement, restart, and corrupted-save recovery.
- [ ] Verify a new character can enjoy the core loop and clear short mode without grinding. Tune XP and permanent passives after combat difficulty is stable.
- [ ] Tune fairness/readability of spawn warnings, catch geometry, projectile speed, boss telegraphs, and saturation limits.
- [ ] Inspect sustained endless performance on the actual target machine. Initial target: 60 FPS at 1080p in the placeholder build; record hardware and measured conditions instead of claiming universal performance.
- [ ] Produce Windows and Web development builds. Launch Windows outside the editor and Web through HTTP; exercise a full short attempt and an endless boss cycle in each. Check browser resize, pause/fullscreen interactions, tab focus, save/reload, and measured performance.
- [ ] Remove/disable reward-bearing debug tools in the player build. Verify saves use the intended application/company identity and persist after relaunch.
- [ ] Write actual build location, version, controls, tests passed/failed, known issues, and next work into the handoff.

**Checks:** Clean compilation; working standalone input/UI; correct scene list; no fatal console errors during the exercised runs; independent new runs; valid progression after relaunch. Placeholder visuals and absent sound are acceptable; unreadable attacks or broken feedback indicators are not.

## 10. Verification commands and evidence

Run commands from the Unity project root once it exists. Create report/output directories before running them. Avoid launching a second editor on a locked project; use the discovered live test runner or coordinate an editor close before batch tests/builds.

```powershell
unity projects verify . --format json
unity test . --editor-version 6000.3.25f1 --mode EditMode --output ./TestResults/editmode.xml --timeout 600
unity test . --editor-version 6000.3.25f1 --mode PlayMode --output ./TestResults/playmode.xml --timeout 600
unity build . --editor-version 6000.3.25f1 --target StandaloneWindows64 --output-path ./Builds/Windows/BorrowedHex.exe --allow-dirty-build --timeout 600
unity build . --editor-version 6000.3.25f1 --target WebGL --output-path ./Builds/Web --allow-dirty-build --timeout 600
```

The installed CLI help confirms these command shapes. Recheck help if the CLI changes. `--allow-dirty-build` is appropriate for authorized local builds because commits are not automatic; it is not permission to publish. Test exit 8 means tests ran and failed; other nonzero infrastructure failures must not be described as a completed test verdict. Read JSON `success` and error fields rather than assuming an empty editor-instance list is success.

Keep evidence with each phase:

```text
Phase/task:
Agent holding edit ownership:
Files changed:
Actual CLI commands:
Test report paths and outcomes:
Manual behaviour observed:
Balance values changed and reason:
Known issues:
Next uncompleted task:
```

For rule tests, use the concrete boundary vectors and conditions in section 9. For integration, verify collisions and state transitions in PlayMode. Do not claim a mechanic is fun solely because automated tests pass; observe whether a human can predict and deliberately execute it.

## 11. Claude/Codex coordination and handoff

Both agents follow this document and the actual project's original instructions. Do not rename Claude configuration paths, model IDs, binaries, or project files. Durable project decisions belong in the project documentation, not unverified imported memory.

- One agent owns a phase/task at a time unless explicit parallel work is assigned. Do not simultaneously edit the same scene, prefab, definition asset, or project settings.
- Record ownership before edits and release it with a written handoff. A shared Unity editor session also has a single mutation owner.
- Prefer separate checkouts for overlapping source work when an actual repository exists. Keep editor-generated `.meta` files with their assets; preserve GUIDs.
- Before implementing a task, read completed phase evidence and confirm its required interfaces exist. Do not invent earlier implementation from this plan.
- Establish shared contracts before dividing source files. Changes to packet provenance, timing, or profile schema require updating dependent tests and this brief.
- Use descriptive code comments to explain timing, provenance, ordering, and other non-obvious decisions.
- Use one appropriate review workflow for a completed change; do not run CodeRabbit unless requested by name.
- No automatic commits, pushes, remote creation, publishing, external messages, or self-attribution in Git/GitHub content.

### First playable checkpoint

After Phase 3, stop adding systems long enough to test interception and release. If that interaction is unclear or unsatisfying, revise movement, capture geometry, and feedback before spending time on the skill tree.

### First complete checkpoint

After Phase 6, the short game must already be playable end to end. This is the fallback jam build if later progression work runs into deadline pressure. Permanent progression remains required for the expanded design, but must not delay having a complete core game.

### Expanded gameplay checkpoint

After Phase 12, the agreed expanded design is functional: short/endless modes, three capture styles, six temporary upgrades, nine permanent nodes, mastery, achievements, and personal records. Cosmetics are presentation hooks, not missing gameplay dependencies.

## 12. Art and sound handoff — deferred, not a current workstream

The accepted direction is a rogue magician in an Octopath-inspired 2.5D world. Preserve an elevated fixed camera, flat logical combat plane, camera-facing character view, clear shadows/ground anchors, and unobstructed projectile paths. Detailed scenery belongs around the playable centre. Avoid blur or excessive bloom over combat.

Use primitive shapes, plain materials, simple rings/bars, and default UI now. Keep visual/audio adapters listening to capture, packet-full, expiry, return, perfect catch, hit, kill, dash, boss transition, upgrade, and achievement events. Missing adapters cannot break gameplay.

When the user requests asset integration, provide the artist with sprite facing/scale, animation state names, projectile identity requirements, and countdown conventions. Provide the sound designer with event names and timing. Do not generate, purchase, or commission assets as part of this plan's implementation phases.

The eventual presentation should show unstable borrowed power becoming more intense as it approaches expiration. Keep permanent techniques visually grounded in a journal/diagram and run upgrades visibly transient. Shape, motion, and timer indicators must support colour cues rather than relying on colour alone.

## 13. Decisions to confirm before they become blockers

Updated with the user's answers on 2 October 2026. Remaining questions are production choices, not unfinished mechanics. Do not invent user preferences or repeatedly ask about explicitly deferred scheduling.

| Question | Current working default | When an answer is needed |
|---|---|---|
| Deadline? | Explicitly deferred; user will inform later | Only revisit when the user supplies scheduling information |
| Repository/project location? | User is creating the repository; no existing Unity project reported | Obtain checkout path/URL before Phase 0 creates anything |
| Delivery platforms? | Confirmed Windows and Web; keyboard/mouse baseline | Already resolved; test both from Phase 0 |
| Editor/tool restrictions? | Recommend 6.3 LTS 6000.3.25f1; no special restrictions reported | Verify/install the selected editor before project creation |
| Final title? | Confirmed: Borrowed Hex; new project folder BorrowedHex | Resolved; use for application identity before save files/builds are distributed |
| Controller, mobile touch, or extra arenas? | Outside baseline scope | Only if requested |

### Confirmed title

The user selected **Borrowed Hex**. Use this display title in menus and build metadata, and `BorrowedHex` for a new Unity project folder and executable. Do not rename an existing user-created repository or checkout automatically. The Markdown filename is retained so existing document links continue to work.

## 14. Reference material

The design rules above are project decisions. Technical references support the Unity workflow, not a claim that any implementation already exists.

- Local Unity CLI skill: `C:/Users/Rachit/.agents/skills/unity-cli/SKILL.md`; command references under its `references/` directory. Installed CLI help and read-only discovery were checked during drafting.
- [Unity 6.3 ScriptableObject documentation](https://docs.unity3d.com/6000.3/Documentation/Manual/class-ScriptableObject.html) — definition assets and shared immutable authoring data.
- [Unity Input System actions](https://docs.unity3d.com/Packages/com.unity.inputsystem@1.11/manual/Actions.html) — action-map approach; use the installed package's matching docs during implementation.
- [Unity 6.3 Physics.SphereCast](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Physics.SphereCast.html) — swept collision and initial-overlap caveat.
- [Unity 6.3 persistentDataPath](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Application-persistentDataPath.html) — player-save location.
- [Unity 6.3 URP introduction](https://docs.unity3d.com/6000.3/Documentation/Manual/urp/urp-introduction.html) — selected rendering pipeline.
- [Unity 6000.3.25f1 release notes](https://unity.com/releases/editor/whats-new/6000.3.25f1) and [Unity release support](https://unity.com/releases/unity-6/support) — recommended patch and support window, checked 2 October 2026.
- [Unity Web build folder](https://docs.unity3d.com/6000.3/Documentation/Manual/webgl-building.html) — output structure and local-file restrictions.
- [Unity PlayerPrefs](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/PlayerPrefs.html) — Web IndexedDB-backed preferences and storage limit.

## 15. Completion checklist

- [ ] Player movement, aiming, dash, and health are reliable.
- [ ] The 2.5D foundation is present: camera-facing character views in a 3D arena, fixed elevated camera, flat XZ gameplay, and separate visuals/colliders.
- [ ] Enemy attacks can be caught and returned after exactly three gameplay seconds.
- [ ] Attack identity, spread, faction, and original-source attribution survive capture.
- [ ] Multiple enemy types create useful ammunition and positioning pressure.
- [ ] Short mode has a complete boss-ending loop with results and restart.
- [ ] Temporary upgrades interact safely and reset between runs.
- [ ] Mastery, gated permanent nodes, free respec, and passive loadouts work.
- [ ] Achievements and records are accurate and saved.
- [ ] Three capture styles are functional and selectable.
- [ ] Endless mode scales, schedules recurring bosses, and supports retirement.
- [ ] Death earns valid progression; repeated finalization never duplicates it.
- [ ] Pause, focus loss, menus, and restart preserve the intended timing rules.
- [ ] Windows and HTTP-served Web builds, browser save/reload, and save recovery are verified with recorded evidence.
- [ ] Art and sound can be integrated later without rewriting combat rules.




