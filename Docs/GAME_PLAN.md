# Borrowed Hex — Game Design and Implementation Plan

> The shared design and implementation brief for Borrowed Hex. Checkboxes describe work to perform; they do not claim
> that work is complete. Rulings made during implementation live in `Docs/DECISIONS.md` (D1–D56 so far), measured
> results in `Docs/TEST_EVIDENCE.md`, and phase-by-phase progress in `Docs/IMPLEMENTATION_STATUS.md`. Where this
> document and a later decision disagree, the later decision wins until this document is updated.

**Date:** 2 October 2026 (rewritten for the "borrowed time" core rework)  
**Status:** Phases 0–5 are implemented (short run, enemy roster, the Collector, score). The core rework in section 3
and Phase 6 is specified here and not started. Values marked *proposal* are starting points that have not been
playtested; the owner's answers to the rework questions are in section 13.  
**Goal:** A fast, short, replayable action game about a rogue magician who steals enemy attacks. Every stolen hex is
decaying in the magician's hands, the run clock is the magician's life, and nothing in the arena lasts.  
**Architecture:** 2.5D: camera-facing 2D characters inside a 3D Unity arena under a fixed elevated camera, with all
movement and combat on one flat plane. Combat is a deterministic plain-C# simulation (`ArenaSim`, D2) with analytic
collision on the XZ plane (D3); MonoBehaviours read input and draw. Tuning lives in configuration (`GameConfig` and its
tuning classes), not in scattered constants.  
**Tech stack:** Unity 6.3 LTS `6000.3.25f1`, 3D URP, C#, Unity Input System, uGUI, Unity Test Framework, Unity CLI.
Targets: Windows desktop and Web.

## 1. Scope, theme, and priorities

### The theme: "Everything is temporary"

The jam theme is the design's spine, not a coat of paint. Each rule below exists to make something temporary and to
make the player feel it:

| What is temporary | How the player feels it | Section |
|---|---|---|
| The stolen hex | A held packet decays; if it runs out unfired it **backfires** on the magician | 3 |
| The magician's attention | Only the **selected** packet decays; the other is frozen. Swapping (Q) chooses which hex is burning | 3 |
| Power | A hex grows stronger the longer it decays, so the best shot is the latest one the player dares | 3 |
| Life | The **run clock is health**: kills add seconds, hits and backfires take them away | 6 |
| Cover | Pillars **decay over time** and crumble | 4 |
| Upgrades | A chosen upgrade lasts **one encounter** only | 5 |
| Enemies' patience | An enemy left alive too long **turns into something worse** | 4 |

### Agreed direction

- Player fantasy: a nimble rogue magician who borrows dangerous magic rather than owning a weapon. There is no direct
  gun and no free damaging spell; damage comes from borrowed attacks (caught projectiles and parried strikes).
- Core interaction: catch an enemy attack, carry it while it decays and gains power, and fire it before it backfires.
- Two packet slots. Swapping between them is a core decision, not a convenience (section 3).
- Encounter-length temporary upgrades; permanent passive skills bought with mastery points.
- Achievements, personal records, and progression that still advances after a lost run.
- A short main mode and an endless mode with recurring bosses.
- Capture styles and, later, cosmetics.
- 2.5D presentation inspired by Octopath Traveler. An artist and a sound designer produce the assets; this plan uses
  functional placeholders and does not wait for them.
- Enemy drafting is excluded: players do not choose which enemies appear.

### Out of scope

Multiplayer, online accounts or leaderboards, monetization, cloud saves, procedural levels, controller and touch
support, extra arenas, and platforms beyond Windows and Web.

### Priority rule

Make **movement → incoming attacks → catch → decay, swap and fire → enemy defeat** fast and readable before building
progression. A skill tree cannot rescue an unclear core. Keep a playable build after every phase.

## 2. Player experience and controls

### Core loop

1. Read an incoming enemy attack, a melee wind-up, or a crumbling pillar.
2. Move to a useful interception position.
3. Open a short directional catch window (or parry a melee strike).
4. The caught hex lands in a slot. If that slot is selected it starts **decaying and gaining power**; otherwise it
   waits, frozen.
5. Swap (Q) to choose which hex burns, reposition, and aim.
6. Fire (right mouse) as late as you dare: power peaks just before expiry, and expiry backfires.
7. Kills buy back seconds of life; use them to keep the chain going.

The decisions the player makes every few seconds: what to catch, which hex to burn now, which to bank, where to stand,
and how long to hold.

### Controls

| Action | Binding | Rule |
|---|---|---|
| Move | WASD | Camera-relative on the arena plane; diagonals normalized |
| Aim | Mouse | Cursor projected onto the gameplay plane; the last valid aim is kept if projection fails |
| Catch / parry | Left mouse | A press opens one catch window; holding does not re-open it |
| Fire | Right mouse | Fire the **selected** packet now, along the current aim, at its current power. An empty selected slot does nothing (D33) |
| Swap | Q | Toggle the selected slot. Freezes the packet you leave and resumes the one you select |
| Dash | Space | Toward movement input, or toward the aim with no input |
| Pause | Escape, or P (browser-safe fallback) | Pauses gameplay; resume from the same menu |
| UI | Mouse / keyboard navigation | UI input never triggers combat actions |
| Restart | Results button | Clears run state and creates a new run ID |

Firing is now the main verb. Packets no longer fire themselves on expiry (section 3).

### Player tuning

| Parameter | Default |
|---|---:|
| Movement speed | 6 units/s |
| Life | The run clock, section 6 (replaces the 5-heart bar of D51; see section 13, Q1) |
| Post-hit invulnerability | 0.65 s (0.5 s after body contact, D51) |
| Dash distance / duration / cooldown | 2.9 units / 0.18 s / 1.2 s |
| Dash invulnerability | First 0.12 s; hostile shots pass through an invulnerable player (D12) |
| Packet slots | 2 |
| Packet lifetime | 3.0 s of **decaying** time (frozen time does not count) |
| Catch range / cone | 2.8 units / 90 degrees |
| Catch window / recovery | 0.25 s / 0.65 s |
| Packet capacity | 12 energy units |

Dash respects walls and standing pillars. Visual recoil, sprite animation and camera effects never move collision
geometry. Zero input, a cursor outside the window, and focus loss never produce invalid movement or aim.

## 3. Capture, decay, swapping, and firing

This section is the heart of the rework. Items marked *new* change the implemented Phase 3–5 behaviour.

### Catching

- A catch attempt costs recovery even if it catches nothing; an empty attempt uses no slot.
- The first successful interception in a window creates a packet. Further eligible projectiles caught in the same
  window join it, within capacity, without resetting anything.
- A packet stores immutable attack snapshots (payload type, base damage, speed, size, relative spread, energy cost,
  original source ID, perfect-catch flag), never references to live projectile objects.
- Only hostile, capturable attacks can be caught. Returned attacks, echoes, explosions and upgrade effects cannot.
- A projectile must be inside the active cone and range and approaching the player. A shot moving away cannot be
  collected from behind; side and rear impacts during an open window still hurt (D16).
- Energy costs: bolt 1, heavy shot 3, rocket 4. A projectile that does not fit is not consumed; it keeps flying and
  can hurt the player. A full packet stops collecting and shows it.
- If a capture and a player impact coincide, the capture wins. One authoritative resolution path prevents double
  consumption (D15).

### Which slot a catch fills — *new*

1. If the **selected** slot is empty, the new packet goes there and starts decaying at once.
2. Otherwise, if the other slot is empty, the new packet goes there and arrives **frozen**.
3. If both slots are full, the catch catches nothing (the projectile is not consumed), unless an upgrade says
   otherwise (Overflow, Fusion; section 5).

Selection never moves by itself, including when the selected packet fires or backfires (D33 kept). This means a player
can deliberately bank a frozen hex in the other slot. That is intended: banking is safe, but a banked hex gains no power
and earns no seconds until the player commits to it.

### Decay — *new*

- **Only the selected packet decays.** Its lifetime (3.0 s) counts down and its power rises only while it is selected
  and the gameplay clock is running.
- Swapping away freezes the packet exactly where it is: remaining lifetime and accumulated power both stop. Swapping
  back resumes both from the same point.
- Pause, upgrade choices, the boss intro and focus loss freeze every packet, as before.

### Power — *new*

A packet's power multiplier rises with its decaying time `d` (seconds, 0 to 3.0):

`power = 1.0 + 0.35 * d` → 1.0 when caught, 1.7 at 2.0 s, 2.05 just before expiry. *Proposal* (section 13, Q3).

- Power multiplies the damage of every payload in the packet, including explosion damage. It does not change speed,
  size, spread, explosion radius or pierce count.
- The HUD shows power continuously for both slots, and marks the last 0.5 s of lifetime as a danger zone.
- The perfect-catch bonus (15% per perfect payload) multiplies on top of power.

### Backfire — *new*

A packet that reaches the end of its lifetime unfired **backfires**:

- The packet is destroyed; no projectiles are fired.
- The player loses **10 seconds** of run clock (*proposal*, Q2) and takes the normal post-hit invulnerability. The
  loss resets the combo multiplier like any hit.
- A burst at the player's position shows it; backfires do not damage enemies.
- A backfire is a hit for statistics (`damage taken`) and for the Untouchable achievement.
- Dash invulnerability does not prevent a backfire: the hex is in the player's hands.

A frozen packet can never backfire, because only a decaying packet's lifetime runs.

### Firing

- Right mouse fires the selected packet from the player's current position along the current aim, keeping the
  payloads' relative spread. The slot frees immediately.
- A release fires exactly once. Echo effects never keep a slot occupied.
- Fired payloads carry their original source ID, so a hex can kill the enemy that cast it (Return Policy).

### Per-enemy hexes — *new*

Each source gives a borrowed hex its own character when fired, so what the player caught matters as much as when they
fire it. Values are *proposals* (Q4).

| Caught from | Returns as | Rule |
|---|---|---|
| Bolt Acolyte | Piercing bolt | Each returned bolt passes through one extra enemy (two targets) |
| Scatter Caster | Shotgun | The fan fires with its spread compressed to ±12 degrees; each pellet deals 1.5 and travels at most 6 units |
| Siege Familiar | Rocket | Unchanged: a 1.6-unit burst for 5, hitting each actor once (D22, D23) |
| The Collector | Heavy bolt | Each returned boss bolt deals 2 instead of 1 |
| Pursuer (parry) | Riposte | Unchanged: flies at the attacker, 2 damage, pierce 1 (D26, D27). A riposte is not a packet: it never decays, swaps or backfires |

A packet that mixes payloads (from one catch window crossing two sources) fires each payload with its own rule.

### Parry (replaces the arcane lantern)

The arcane lantern is removed (D26). A melee-only remainder is answered by the parry: during the first half of the
catch window a thin gold parry band (1.15 from the player, 0.12 wide, spanning the cone) is shown. If it touches the
gold rim of a Pursuer's strike circle while that rim is shown (0.1–0.35 s into the wind-up, D46), the strike is
cancelled, the Pursuer staggers, and a riposte fires at it. The Collector's sweep can be parried the same way through
its gold arc (D47); its slam never can. Parries ignore packet slots and capacity (D28).

### Clock and lifecycle

One gameplay clock governs cooldowns, projectiles, packet decay, telegraphs, upgrades, enemy overstay timers and the
run clock. Pause, upgrade choices, the boss intro, results and focus loss stop it. UI animation may use real time.

Death (the run clock reaching zero) cancels every pending packet, echo, explosion, enemy action and spawn. Nothing
executes in the results screen or the next run. Restart clears subscriptions and resets pooled objects.

### Ordering inside one tick

Aim updates first, then packet decay and backfires, then catches (so a slot freed by a backfire is usable the same
tick, D17), then projectile resolution, then terminal checks in the order death, victory, time expiry (D45).

## 4. Enemies, pillars, and overstaying

### Roster

| Enemy | Behaviour | Supplies | Health |
|---|---|---|---:|
| Bolt Acolyte | Keeps distance, telegraphs, fires three bolts | Piercing bolts | 3 |
| Pursuer | Routes around pillars (D37) to the player; telegraphed strike with a parry rim | Ripostes (parry) | 2 |
| Scatter Caster | Repositions between attacks; five-shot fan | Shotgun | 5 |
| Siege Familiar | Slow; telegraphed rocket | Rocket | 8 |
| Overstayed (elite) | Any of the above after overstaying, see below | Same hex | 1.5× |

Incoming attacks cost the player run-clock seconds (section 6). Enemies cannot spawn on the player and show a spawn
warning before they become dangerous; only bodies past their warning hurt. Melee wind-ups, firing directions and rocket
launches are clearly telegraphed. Pursuer speed is 4.2 (D56), still under the player's 6.

### Overstaying — *new*

An ordinary enemy that stays alive too long turns into something worse (owner-confirmed, Q5):

- Each ordinary enemy has an **overstay timer** of 25 active seconds, starting when its spawn warning ends.
- During its last 5 seconds a shrinking ring around the enemy warns the player.
- When it runs out, the enemy becomes **overstayed**, once only: health is restored to 1.5× its base maximum, its
  attack interval drops to 0.75×, and it moves 15% faster.
- Its sprite **evolves into a more horrifying form** (owner direction). Until the artist supplies evolved sprites, the
  placeholder view swaps to a larger, darker, spikier variant with an outline, so the change reads at a glance. The
  view adapter takes one "overstayed" sprite per enemy type, so final art drops in without code changes.
- An overstayed enemy is worth 1.5× score and counts as an elite for XP. Letting enemies ripen is a deliberate
  risk-for-reward option, not an exploit: it costs time and safety.
- The Collector does not overstay; the run clock is its pressure.

### Pillars crumble — *new*

The arena keeps its four pillars, but they are temporary cover. Owner direction (Q6): pillars **degrade and break
with time on their own**, and come back each encounter.

- Each pillar has **12 durability** and loses 1 every few active seconds on its own. Each pillar's interval is seeded
  between 5 and 8 s (*proposal*), so they fall at different times (roughly 60–95 s each) rather than all at once.
- **Only time** wears a pillar down (owner direction): hits, rockets and slams never damage it. The same rule holds for
  any future world decay.
- The pillar shows its damage in stages (cracks at 8 and 4).
- At 0 it crumbles: it stops blocking movement, projectiles and line of sight, and leaves visible rubble with no
  collision. Enemy routing (`EnemySteering.Waypoint`, recomputed each tick) and the Collector's line-of-sight check
  simply stop seeing it.
- All pillars are restored at the start of each encounter and at the boss transition, so every encounter starts with
  the same arena. Their decay pauses with the gameplay clock.

### Later: a constantly decaying world (deferred until assets exist)

The owner's broader vision is a world that is visibly decaying all the time: the environment changes and degrades
during a fight, and each encounter takes place in a different environment. It needs environment art, so it is **not
part of Phases 6–13**. Pillars are the first, asset-free piece of it. When environment assets arrive, plan it as its
own phase: per-encounter arenas, more decaying props, and floor and lighting changes over time. Keep the pillar
durability code general (any obstacle can decay) so it extends to other props.

### The Collector (boss)

50 health. Four patterns: bolt stream (12 shots), fan volley (9 bolts), sweep (parryable through its gold arc) and
ground slam (never parryable). Announced by a name banner that pauses the clock (D41).

Pattern choice by position (D48, revised by D52–D55), in priority order: after 2 melee patterns in a row, ranged (fan
within 7.5, else stream); no line of sight → slam; within 2.4 → slam; within 5.0 → sweep (a seeded 35% chance it slams
instead); within 7.5 → fan; else stream. It never starts the same pattern more than twice in a row (a third pick
becomes its partner: fan↔stream, slam↔sweep). Ranged patterns walk to a firing spot swung 25–55 degrees around the
player; the reposition gap is up to 1.5 s.

Teleport: when the player is at least 6 units away and 5 s have passed since the last one, each pattern start rolls a
seeded 48% chance to blink 2.6 units behind the player (0.6 s wind-up). The attack after a teleport is always melee, a
seeded 50/50 slam or sweep, and its wind-up starts on arrival. No teleport at the melee cap. Move speed 3.6; wind-ups
sweep 0.77 s, slam 0.8 s.

Returned boss bolts are heavy bolts (section 3). Crumbling pillars change the fight: a hidden player draws slams, and
pillars fall on their own schedule, so cover disappears mid-fight. A repeated endless boss gains one predefined pattern variation rather than only health.

## 5. Encounter upgrades

### Rules — *new*

- After each encounter is cleared, the game pauses and offers **three distinct upgrades**; the player picks one.
- The chosen upgrade lasts **for the next encounter only** (the one picked after encounter 3 lasts for the boss
  fight). It expires when that encounter is cleared, and the next choice replaces it.
- The player therefore holds at most one encounter upgrade at a time. Permanent passives (section 7) are what combine.
- Offers are seeded for reproducible debugging. The same upgrade may be offered again in later encounters.
- An upgrade's effect applies to packets fired while it is active. A packet caught under one upgrade and fired after
  it expired does not keep the effect.

### Upgrade pool

| ID / name | Effect for one encounter |
|---|---|
| `piercing_return` — Piercing Return | Returned non-explosive payloads pass through one extra enemy (stacks with the acolyte's own pierce) |
| `echo_volley` — Echo Volley | Each release repeats after 0.20 s at 25% damage; one echo, never recursive |
| `heavy_orbit` — Heavy Orbit | Held packets (selected or frozen) damage enemies within 1.0 units for 1 every 0.35 s, per enemy |
| `parting_gift` — Parting Gift | Each release also creates a 1-damage blast of radius 1.5 around the player |
| `final_second` — Final Second | Adds 20 percentage points to the perfect-catch bonus |
| `overflow` — Overflow | A catch with both slots full fires the **selected** packet at once at its current power and puts the new packet in its place |
| `fusion` — Fusion *(new; idea 6)* | A catch with both slots full merges the new catch and the frozen packet into the selected packet (capacity ignored, +25% power); the other slot stays locked until the merged packet fires |

Fusion is the owner's merge idea and stays a temporary encounter upgrade. The owner's swap-fire idea, Quick Draw,
is a permanent skill-tree node instead (section 7).

Modifier order for a release: copied base payload → per-enemy hex rule → perfect-catch bonus → power multiplier
(including Overflow's current power and Fusion's +25%) → Quick Draw (permanent node) → echo fraction. Explosions and orbit damage state
their own rules. Never mutate the attack-definition asset. Echoes inherit provenance and cannot echo. Orbit damage is
not a returned hit for combo purposes. Piercing payloads keep a set of hit actor IDs; an explosion hits each actor once.

## 6. Run modes, the life clock, and score

### The run clock is life — *new*

The clock that limits the run is also the magician's health (owner idea 7; replaces the hearts of D51, Q1).

- A short run starts with **300 s** (D56). The clock counts down in active gameplay time and is capped at 300 s.
- **Kills add seconds** (*proposal*, Q8): acolyte and pursuer +3 s, scatter caster +5 s, siege familiar +6 s,
  overstayed enemies 1.5× their base. Despawned enemies give nothing.
- **Hits take seconds**, at 5 s per former half heart: an ordinary enemy hit −10 s, a boss attack (including its
  bolts) −20 s, body contact −5 s (ordinary) or −10 s (boss), a backfire −10 s.
- Damage numbers float up from the player ("−10") and the clock flashes; kill gains float up from the kill ("+3").
- When the clock reaches zero the run ends. If the last change was damage or a backfire the end reason is `Death`;
  if it ran out by ticking it is `TimeExpired`. Both are losses; the distinction is for statistics.
- The clock is the largest element of the HUD.

### Short mode

- Three encounters, then the Collector, under the one shared clock (D50).
- Each encounter draws a fixed, seeded list of authored formations up front (4, 5 and 5 formations) and ends when every
  member is dead. Encounter one: acolytes and a few pursuers; two adds scatter casters; three adds siege familiars.
- After each encounter, the upgrade choice pauses everything; packets and pillars are kept through the choice, then
  pillars are restored as the next encounter starts.
- At the boss transition, ordinary spawning stops; there are no leftover enemies because encounters are kill-all.
  Captured packets are preserved. The banner announces the boss.
- Defeating the boss wins immediately; the clock reaching zero loses.
- At most 12 ordinary enemies at once; queued spawns wait rather than stacking pressure (D44).

### Score and records

Kill values: pursuer and acolyte 10, scatter caster 20, siege familiar 25, overstayed 1.5× base, boss 250. A victory
adds 2 points per second left on the clock.

A packet's first returned hit (or a riposte, D40) raises the combo multiplier by 0.25, from 1.0 to a cap of 3.0, and
refreshes a 5 s combo timer. Further hits from the same release or its echo do not raise it again. Taking damage or
backfiring resets it. Catching alone gives no score.

Tracked: score, duration, bosses defeated, kills by hex type, packets fired, backfires, swaps, average fire power,
perfect shots, damage taken in seconds, seconds gained from kills, pillars crumbled, enemies that overstayed, and the
best single release's distinct kills (a release and its echo share one root release ID).

Personal records are kept per mode and capture style, with build version, seed, mastery level and equipped passives.
They are not presented as a fair global leaderboard.

### Endless mode

Same combat, enemies, boss, score and profile, under a different scheduler.

- The clock starts at 300 s, capped at 300 s, and is life exactly as in short mode (Q9).
- Normal waves last 30 active seconds. An upgrade choice comes every two waves and lasts until the next choice.
- After six waves, regular spawns stop, ordinary enemies and their projectiles are cleaned up without rewards, and the
  boss appears. The wave-six choice is deferred until the boss falls. Defeating a boss restores 30 s of clock.
- Offered upgrades have a rank equal to the cycle number, capped at 3 (rank scaling: Piercing +1 target per rank; Echo
  25/40/55%; Orbit radius 1.0/1.2/1.4; Parting Gift damage 1/2/3 and radius 1.5/1.75/2.0; Final Second +20/40/60;
  Overflow and Fusion unchanged).
- Retiring between waves keeps earned progression and ends the run as `Retired`.
- Per completed cycle: enemy health +15%, movement +5% (to +25%), attack interval −5% (to 70%), boss health +20%,
  overstay timer −2 s (to a minimum of 15 s). At most 18 ordinary enemies. A full projectile budget delays an emission
  visibly; live shots are never dropped or recycled under the player.

## 7. Permanent progression, achievements, and customization

### Mastery and points

Account-wide mastery, capped at level 10. Level 1 starts with no points; each level gained gives one point (nine
total). Advancing from level `L` costs `100 + 50 * (L - 1)` XP; excess carries over. At level 10, statistics keep
counting without new points.

Run XP:

```text
2 * normal enemy kills
+ 5 * overstayed enemy kills
+ 35 * boss kills
+ 5 * cleared encounters (short) or completed waves (endless)
+ min(20, perfect shots that damage an enemy)
```

XP is awarded on every ending (victory, death, time expiry, retirement) and finalized exactly once per run ID. No
achievement awards points. Riposte capture XP remains an open question from D29.

### Nine-node skill tree

Each node costs one point. Tier one needs mastery 2; tier two needs mastery 4 and its branch's tier-one node; tier
three needs mastery 7 and its branch's tier-two node. Owned (not equipped) prerequisites suffice. At most three nodes
are equipped at once. Buying, equipping and free respec happen between runs; validation lives in domain logic.

| Branch / tier | ID | Passive when equipped |
|---|---|---|
| Precision 1 | `precision_angle` | +15 degrees catch cone |
| Precision 2 | `precision_capacity` | +2 energy per packet |
| Precision 3 | `quick_draw` — Quick Draw | Firing within 0.3 s after a swap deals +30% damage (*replaces* `precision_recovery`) |
| Mobility 1 | `mobility_speed` | +5% movement speed |
| Mobility 2 | `mobility_dash_recovery` | −0.10 s dash cooldown |
| Mobility 3 | `mobility_dash_distance` | +0.30 dash distance, same duration |
| Resilience 1 | `resilience_grace` | +0.15 s post-hit invulnerability |
| Resilience 2 | `resilience_time` | +20 s starting clock and cap (*replaces* `resilience_health`) |
| Resilience 3 | `resilience_dash_grace` | +0.04 s dash invulnerability, capped at dash duration |

Quick Draw (owner's swap-fire idea, Q7) takes the Precision tier-three slot so the tree stays at nine nodes, matching
the nine points mastery can give. Precision fits it: the branch is about catching well, and Quick Draw rewards
handling the two slots well. The catch-recovery passive it replaces is the least theme-relevant node.

### Achievements

| ID / name | Condition |
|---|---|
| `return_policy` — Return Policy | A returned payload kills the actor that originally cast it |
| `first_borrow` — First Borrow | A returned payload damages any enemy |
| `crowd_control` — Crowd Control | One root release, with its echo, kills five distinct enemies |
| `perfect_timing` — Perfect Timing | Five perfect shots damage enemies in one run |
| `untouchable` — Untouchable | Clear a short-mode encounter without a hit or a backfire |
| `mixed_bag` — Mixed Bag | Kill enemies with returned bolts and returned rockets in one run |
| `final_notice` — Final Notice | Win short mode |
| `second_encore` — Second Encore | Defeat two bosses in one endless run |
| `persistent_student` — Persistent Student | Reach mastery 5 |
| `fully_trained` — Fully Trained | Reach mastery 10 |

Completion persists once; death never erases an achievement earned during the run. Rewards are badges now and
cosmetic IDs once assets exist.

### Capture styles

| Style | Catch | Trade-off |
|---|---|---|
| Snatcher | Baseline cone, range, window, recovery | Balanced, precise |
| Collector | 140-degree cone, 0.40 s window, 1.00 s recovery | Broad, more commitment |
| Daredevil | The catch is a dash that captures along its swept path | Aggressive, tied to dash availability |

Daredevil's catch and dash share the dash cooldown; pressing both on one tick produces one action. All styles keep two
slots, the 3.0 s decay lifetime, the frozen-unselected rule and the backfire. The selected style and cosmetic IDs are
saved; unknown IDs fall back to Snatcher and the default look. Cosmetics never change hitboxes or hide timers.

## 8. Unity architecture and conventions

### Environment

The project is at `C:/Users/Rachit/BorrowedHex` on Unity `6000.3.25f1` (3D URP), in a Git repository with LFS. Work
happens on `main`, committed and pushed as work proceeds (owner instruction, D1). Exact package versions are recorded
in `Docs/IMPLEMENTATION_STATUS.md`.

### Layout

```text
Assets/Game/
  Scenes/          Bootstrap.unity, Arena.unity (built by ProjectBootstrap, D7)
  Scripts/
    Core/          clock, run context, IDs, events
    Player/        input reader, motor, aim
    Combat/        projectiles, capture, packets, release, parry geometry
    Enemies/       steering, ranged casters (one parameterised brain, D20), pursuer, Collector
    Runs/          ArenaSim partials for the run, encounters, score
    Data/          GameConfig and tuning classes
    UI/            HUD, menus, choices, results
    Presentation/  views and feedback adapters
  Editor/          bootstrap and build entry points
  Tests/EditMode/  rules, timing, boss behaviour, tuning
  Tests/PlayMode/  integration and the scripted catch-and-return bot
Docs/              GAME_PLAN, DECISIONS, IMPLEMENTATION_STATUS, TEST_EVIDENCE
```

### Simulation model

- `ArenaSim` is plain C# at a fixed 60 Hz with frame time clamped to 0.1 s (D11); views interpolate and poll state
  (D14). Everything that decides an outcome is testable in EditMode and deterministic from a seed.
- Collision is analytic on XZ: swept circles against circles and boxes, including initial overlap (D3). Factions are
  logical, not Unity physics layers (D4).
- Health and damage are floats (D13), so power multipliers and bonuses never round away.

### Contracts the rework touches

These change in Phase 6 and must be updated together with their tests:

- **Packet**: adds `DecayedTime` (seconds decayed so far), derived `Power`, and a frozen/decaying state that follows
  selection. Expiry is measured in decayed time, not capture time.
- **Packet store**: slot assignment by the selected-first rule (section 3); `Advance` decays only the selected packet
  and reports backfires instead of releases.
- **Release**: takes the packet's power; applies the per-enemy hex rule per payload.
- **Run clock**: becomes life; damage and backfires subtract, kills add, capped at the starting value; the end reason
  depends on the last change.
- **Obstacles**: pillars gain durability and a crumbled state; collision, line of sight and enemy routing read only
  standing pillars.
- **Enemies**: an overstay timer and an overstayed flag that applies the modifiers once.
- **Events**: backfire, swap, pillar damaged/crumbled, enemy overstayed, clock gained/lost — for the HUD, sound and
  statistics. Combat must keep working with no listeners.

### Persistence

One versioned, validated JSON profile. Windows: under `Application.persistentDataPath` with temporary file plus
backup and replace; a corrupt file is preserved and a valid backup tried. Web: the compact JSON in PlayerPrefs
(IndexedDB, 1 MB limit), each snapshot under 64 KiB, two generation-numbered keys, `PlayerPrefs.Save()` at explicit
save points. Unavailable storage is reported and play continues in memory with a warning. XP, achievements and records
are applied in one idempotent finalization per run ID. Focus loss pauses; a browser close is not a reliable save point.
Resuming an unfinished run is out of scope. Tests never touch the real save.

### Windows and Web

Platform-neutral combat and progression; adapters only for storage, quitting and browser interaction. WebGL 2, basic
URP, no compute-only effects or native plugins (lit materials do not receive shadows on WebGL, D6). Serve Web builds
over HTTP. Check resizing, canvas focus, tab switching, the Escape/fullscreen interaction, and save reload.

### Review focus

1. Catch and damage on the same tick: one outcome, never both.
2. Decay, freeze and backfire across swaps, pause, choices, death and restart: a frozen packet never decays, never
   backfires, and never leaks into the next run.
3. Power and the per-enemy hex rules: damage is computed once per payload in the documented order.
4. The life clock: no double subtraction from one hit, correct end reason, cap respected.
5. Pillar crumbling: routing and line of sight never see a crumbled pillar; restoration never traps an actor inside.
6. Overstay: applied exactly once; never to the boss.
7. Profile finalization is idempotent; damaged saves are recoverable.

## 9. Phased implementation checklist

Complete phases in order; each ends with a playable build and an acceptance gate. Write the failing test first for
every rule, then implement it. Record real outcomes in `TEST_EVIDENCE.md`, not planned ones. Phases 6–12 of the
previous revision of this plan are now Phases 7–13.

### Phases 0–5 — done

Recorded in `Docs/IMPLEMENTATION_STATUS.md` and `DECISIONS.md` D1–D56: project and CLI foundation; player movement,
aim, dash, health; projectiles and the acolyte; catch, carry and return; the enemy roster, rockets, pursuers with
parry and routing; the complete short run with kill-all encounters, the Collector and its position-driven patterns,
statistics, score, results and restart. At the start of Phase 6: 166/166 EditMode and 2/2 PlayMode tests pass.

### Phase 6 — Borrowed time (the core rework)

**Depends on:** Phase 5 and the owner's answers to section 13 (or acceptance of the proposals).  
**Deliverable:** The short run plays under the new core: frozen unselected packets, decay with power, backfire,
per-enemy hexes, the life clock, crumbling pillars and overstaying enemies.

Order matters: the packet rules first (they change every fight), then the clock, then the arena.

- [ ] Packets decay only while selected; swapping freezes and resumes. Slot assignment by the selected-first rule.
- [ ] Remove automatic release on expiry; add the backfire (clock loss, invulnerability, combo reset, burst, event).
- [ ] Power from decayed time, applied at release to every payload's damage; HUD power and danger-zone display for both
      slots, with a clear frozen state.
- [ ] Per-enemy hex rules: piercing acolyte bolts, shotgun scatter pellets, heavy boss bolts.
- [ ] Life clock: remove hearts; hits and backfires subtract, kills add, cap at the start value, end reason from the
      last change; floating gain/loss numbers; the clock as the main HUD element.
- [ ] Pillar durability that decays over time only (seeded per-pillar rate), damage stages, crumbling
      (collision, line of sight and routing ignore it), restoration at each encounter start and at the boss transition.
- [ ] Overstay timer, warning ring, one-time overstayed modifiers, an "evolved" placeholder view per enemy type; elite
      score/XP values.
- [ ] Update the scripted catch-and-return bot to swap, fire before expiry, and never backfire on purpose. It must
      still beat the boss.
- [ ] Record each rule change in `DECISIONS.md` and the measured bot result in `TEST_EVIDENCE.md`.

**Required boundary vectors:**

- Selected packet caught at `t=1.0`, swapped away at `t=2.0`, swapped back at `t=5.0`: it backfires once at `t=7.0`,
  not at `t=4.0`, and never while frozen.
- Power at fire: 0 s decayed → 1.0; 2.0 s → 1.7; a frozen packet's power does not change while frozen.
- A packet fired at 2.99 s decayed does not backfire; one at 3.0 s backfires once and fires nothing.
- Both slots full, a catch catches nothing and the projectile keeps flying (no upgrade).
- Clock at 8 s and a 10 s hit → run ends `Death`; clock at 0.01 s ticking → `TimeExpired`.
- Clock at 298 s and a +3 s kill → 300 s, not 301.
- A pillar at 1 durability is hit by a returned bolt: the bolt stops and the pillar keeps its durability.
- An untouched pillar with a 6 s interval crumbles at exactly 72 s of active time; pausing does not advance it.
- An enemy alive 25 s past its warning becomes overstayed once; at 50 s it is still overstayed once.

**Checks:** swapping changes which packet decays; pause freezes everything; a death or restart leaves no pending
backfire; acolyte bolts hit two enemies in a line; shotgun pellets vanish at 6 units; the boss never overstays.
Human gate: the owner plays a short run and judges whether swapping now matters and the pace is fast.

### Phase 7 — Encounter upgrades

**Depends on:** Phase 6.  
**Deliverable:** A choice of three after each encounter, lasting one encounter.

- [ ] Upgrade definitions and the one-encounter lifetime; the choice panel shows exact effects and what is expiring.
- [ ] The seven upgrades of section 5, including Overflow and Fusion under the frozen-slot rules.
- [ ] Perfect-catch geometry and bonus before Final Second can be offered.
- [ ] Modifier order as section 5; no recursive echoes, no repeated pierce hits, at most one Overflow or Fusion per
      catch activation.
- [ ] Upgrade state is cleared when its encounter ends and on every new run.

**Checks:** seeded offers reproduce; an upgrade picked after encounter 3 applies to the boss and nothing after it; a
packet caught under Echo and fired after the encounter ends gets no echo; Fusion's locked slot unlocks when the merged
packet fires or backfires; a merged packet backfires once.

### Phase 8 — Menus and the player profile

**Depends on:** Phase 7.  
**Deliverable:** Launch, play, finish, close and reopen with valid saved records.

- [ ] Versioned profile JSON, default profile, Windows file and Web PlayerPrefs adapters, validation and recovery.
- [ ] One idempotent finalization path for run summaries.
- [ ] Main menu (Play Short, Settings, Quit; unavailable modes visibly disabled), replacing the D43 switch.
- [ ] Settings: sensitivity, fullscreen/window behaviour, readability options. Browser Quit returns to the menu.

**Checks:** round-trip preserves values; finalizing twice adds nothing; an invalid primary save falls back to a valid
backup; Web progress survives a refresh; menus never resume combat by accident.

### Phase 9 — Mastery, skill tree, loadouts

**Depends on:** Phase 8.

- [ ] XP formula and thresholds from section 7, excess carry, level-10 cap, one point per level gained.
- [ ] Nine nodes (with `resilience_time` and `quick_draw`), purchase validation, three equipped, free respec.
- [ ] Resolve style plus equipped passives once at run start; encounter upgrades stay separate.

**Checks:** 99 XP stays level 1, 100 reaches level 2 with one point; multiple levels from one run; locked or
unaffordable nodes rejected; a fourth equipped node rejected; respec refunds correctly; a lost run still grants XP.

### Phase 10 — Achievements and records

**Depends on:** Phase 9.

- [ ] Section 7's ten achievements from combat and run events; per-run, per-encounter, cumulative and profile scopes.
- [ ] Records per mode and style with metadata; a short comparison with the previous best on the results screen.

**Checks:** piercing and echoes never count a victim twice; Return Policy needs the original caster; Untouchable fails
on a backfire; repeat completion gives nothing more.

### Phase 11 — Capture styles

**Depends on:** Phase 10.

- [ ] Move baseline catch values into Snatcher's definition unchanged; add Collector and Daredevil.
- [ ] Show effective stats before a run; save the selection.

**Checks:** a same-tick catch and dash is one Daredevil action; its sweep never catches through a wall or a standing
pillar; all styles keep the frozen-slot and backfire rules.

### Phase 12 — Endless mode

**Depends on:** Phase 11.

- [ ] 30 s waves, a choice every two waves lasting until the next, a boss after six waves, deferred wave-six choice,
      +30 s on a boss kill.
- [ ] Ranked offers by cycle; cycle scaling including the shorter overstay timer; elites from overstaying only.
- [ ] Retirement, finalization, survival records; a long debug-assisted session that cannot submit rewards.

**Checks:** boss cadence over two cycles; boss time does not advance wave scheduling; pause freezes scaling; enemy and
projectile caps hold; a late death produces a complete valid summary.

### Phase 13 — Integration, tuning, and build handoff

**Depends on:** Phase 12.

- [ ] Focused EditMode and PlayMode suites green; fresh and advanced profiles; every style, upgrade and mode; pause,
      focus loss, death, retirement, restart, corrupted-save recovery.
- [ ] Tune the clock economy (start, gains, losses), power curve, backfire cost, overstay timer and pillar durability
      from real sessions and the bot.
- [ ] 60 FPS at 1080p on the actual target machine in the placeholder build, recorded with its hardware.
- [ ] Windows and HTTP-served Web builds, each played through a full short run and an endless boss cycle.
- [ ] Debug reward tools disabled in player builds; the handoff lists build location, version, controls, tests and
      known issues.

### Checkpoints

- **After Phase 6:** stop and playtest the new core before building upgrades on it. If swapping still does not matter,
  or backfires feel unfair rather than tense, revise section 3 first.
- **After Phase 7:** the short game is complete and is the fallback jam build.
- **After Phase 13:** the full design works: both modes, three styles, seven encounter upgrades, the tree, mastery,
  achievements and records.

## 10. Verification

Run from the project root. Do not launch a second editor on a locked project; use the live editor's test runner or
close it before batch runs.

```powershell
unity projects verify . --format json
unity test . --editor-version 6000.3.25f1 --mode EditMode --output ./TestResults/editmode.xml --timeout 600
unity test . --editor-version 6000.3.25f1 --mode PlayMode --output ./TestResults/playmode.xml --timeout 600
unity build . --editor-version 6000.3.25f1 --target StandaloneWindows64 --output-path ./Builds/Windows/BorrowedHex.exe --allow-dirty-build --timeout 600
unity build . --editor-version 6000.3.25f1 --target WebGL --output-path ./Builds/Web --allow-dirty-build --timeout 600
```

Test exit code 8 means tests ran and failed; other non-zero codes are infrastructure failures, not a verdict. A locked
PC stops the editor processing CLI commands; a hung run is checked for that before anything else. Passing tests do
not prove a mechanic is fun; the human gates do.

Evidence per change in `TEST_EVIDENCE.md`: files changed, commands run, test results, behaviour observed, tuning
changed and why, known issues, next task.

## 11. Coordination

- One agent owns a phase or task at a time; never edit the same scene, prefab, definition asset or project settings
  concurrently. A shared editor session has one mutation owner.
- Read completed evidence before starting a task, and confirm the interfaces it needs exist. Do not assume this plan
  describes implemented code; check the code.
- Changes to packet timing, provenance, the clock or the profile schema update this document and their tests together.
- Comment code to explain timing, ordering, provenance and other non-obvious decisions.
- Commits are in the owner's name only, with no tool attribution of any kind.
- One review workflow per change; CodeRabbit only when the owner asks for it by name.
- The sprite sheet and its `.meta` files under `Assets/Sprites/` belong to a teammate; do not commit or edit them
  unless asked.

## 12. Art and sound handoff — deferred

Direction: a rogue magician in an Octopath-inspired 2.5D world. Fixed elevated camera, flat combat plane,
camera-facing characters with ground anchors, unobstructed projectile paths, no blur or heavy bloom over combat.
Placeholders now; adapters listen to events and missing adapters never break gameplay.

The theme should be visible everywhere:

- **Decaying hex:** the selected packet grows brighter, faster and more unstable as power rises, with a distinct
  danger state in its last 0.5 s. **Frozen hex:** stilled, desaturated, crystallised.
- **Backfire:** an unmistakable burst on the magician.
- **The life clock:** the dominant HUD element; it visibly drains on hits and swells on kills.
- **Pillars:** crack in stages, then crumble to rubble.
- **Overstaying enemies:** a warning ring, then the sprite evolves into a more horrifying form (one evolved sprite
  per enemy type).
- **The world (later):** pillars crack and crumble now; later, environments that visibly degrade during a fight and
  differ per encounter (section 4).
- **Encounter upgrades:** shown as fading or burning out, distinct from permanent techniques (a journal or diagram).

Colour cues are always backed by shape, motion or timers. The artist receives sprite facing and scale, animation
state names, projectile identity rules and these state conventions; the sound designer receives event names and
timing. Assets are not generated, purchased or commissioned as part of these phases.

## 13. Open questions for the owner

Answered by the owner on 2 October 2026: Q5, Q6 and Q7 as stated in the table; for the rest the owner accepted the
working defaults. Values marked *proposal* in the text are still untested tuning and are revisited in Phase 13.

| # | Question | Resolution |
|---|---|---|
| Q1 | Does the life clock **replace** the 5 hearts (D51)? | Default accepted: replace; the clock is the only life bar |
| Q2 | How much does a backfire cost? | Default accepted: −10 s, the same as an ordinary hit |
| Q3 | Power curve: how strong is a hex fired at the last moment compared with one fired at once? | Default accepted: linear, 1.0 → 2.05 over the 3 s |
| Q4 | Per-enemy hexes: are piercing acolyte bolts, a shotgun from scatter casters (±12°, 1.5 per pellet, 6-unit range), rockets as now, and 2-damage boss bolts the right identities? | Default accepted |
| Q5 | What does "turn into something worse" mean? | Owner: yes to the default (25 s; 1.5× health, faster, 1.5× score, once, not the boss), and the sprite evolves into something more horrifying |
| Q6 | Pillar durability, and do pillars come back? | Owner: pillars degrade and break with time only (never from hits), and come back each encounter. A constantly decaying world with per-encounter environments is deferred until assets exist |
| Q7 | Quick Draw and Fusion: upgrades or tree nodes? | Owner: Quick Draw is a skill-tree node; Fusion is a temporary encounter upgrade |
| Q8 | Starting clock and kill rewards? | Default accepted: 300 s start and cap; +3 / +5 / +6 s per kill |
| Q9 | Endless: same 300 s life clock? | Default accepted: yes, +30 s per boss kill |
| — | Deadline | Deferred by the owner; not to be asked again until supplied |

Confirmed earlier: title **Borrowed Hex**; Windows and Web with keyboard and mouse; project folder `BorrowedHex`.

## 14. References

- Local Unity CLI skill: `C:/Users/Rachit/.agents/skills/unity-cli/SKILL.md` and its `references/`.
- [Unity 6.3 ScriptableObject](https://docs.unity3d.com/6000.3/Documentation/Manual/class-ScriptableObject.html)
- [Unity Input System actions](https://docs.unity3d.com/Packages/com.unity.inputsystem@1.11/manual/Actions.html)
- [Unity 6.3 persistentDataPath](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Application-persistentDataPath.html)
- [Unity 6.3 URP introduction](https://docs.unity3d.com/6000.3/Documentation/Manual/urp/urp-introduction.html)
- [Unity 6000.3.25f1 release notes](https://unity.com/releases/editor/whats-new/6000.3.25f1) and [Unity release support](https://unity.com/releases/unity-6/support)
- [Unity Web build folder](https://docs.unity3d.com/6000.3/Documentation/Manual/webgl-building.html)
- [Unity PlayerPrefs](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/PlayerPrefs.html)

## 15. Completion checklist

- [x] Movement, aim and dash are reliable; the 2.5D foundation is in place.
- [x] Enemy attacks can be caught and fired with identity, spread, faction and source intact.
- [x] Four enemy types, the parry, and the Collector make a complete short run with results and restart.
- [ ] Only the selected packet decays; swapping freezes and resumes; power rises with decay; expiry backfires.
- [ ] Each enemy's hex behaves distinctly when fired.
- [ ] The run clock is life: kills add, hits and backfires subtract.
- [ ] Pillars decay over time and crumble, and are restored each encounter; overstaying enemies evolve once.
- [ ] Encounter upgrades last one encounter and interact safely.
- [ ] Mastery, the gated tree, respec and loadouts work.
- [ ] Achievements and records are accurate and saved.
- [ ] Three capture styles are selectable.
- [ ] Endless mode scales, repeats the boss and supports retirement.
- [ ] Losing still earns valid progression, never twice.
- [ ] Pause, focus loss, menus and restart respect every timing rule.
- [ ] Windows and HTTP-served Web builds, browser save reload and save recovery are verified with evidence.
- [ ] Art and sound can be integrated without rewriting combat rules.
