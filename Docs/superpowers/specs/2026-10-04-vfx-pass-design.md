# VFX pass: design

Date: 2026-10-04. Status: approved in conversation, section by section; awaiting review of this written spec.

Prerequisite already shipped: per-shooter projectile ranges (commit `6c1909a`). Shots now expire inside the arena, which this pass gives a visible fizzle.

## 1. Intent and decisions

The owner asked for projectile sprites (not the magician or enemies), enemy hit effects, camera shake, impact frames, effects for the temporary upgrades and the skill-tree nodes, callout text such as "Parry!" and "Overcharge!", secondary effects and trails.

Owner decisions, in the order they were made:

| # | Question | Decision |
|---|---|---|
| D1 | Readability vs spectacle | **Readable baseline; spectacle only on rare moments.** |
| D2 | Projectile identity | **Shape follows the source school; colour follows the state.** |
| D3 | Upgrade / skill visuals | **Moments get effects; passives get cues only while their action happens; nothing permanent.** |
| D4 | Callout text | **Rare moments only**: Parry!, Perfect! / Last Second!, Overcharge!, Backfire!, Fusion!, Chain xN! (N >= 3). No damage numbers. |
| D5 | Asset sourcing | **Local packs in `~/Downloads/itch_downloads` only.** A gap found in playtest is requested per file (name, source, size, licence) before any download. |
| D6 | Enemy hit reaction | **Visual only**: flash, squash, spark, 0.1-unit sprite recoil. Sim positions never move (no knockback). |
| D7 | Architecture | **A feedback director plus a pure cue table.** |

Final Second, as the code defines it, raises the perfect-catch bonus (`ArenaSim.PerfectBonusNow`). It has no moment of its own. With Final Second held, a perfect catch shows **Last Second!** *instead of* **Perfect!**, never both.

## 2. Architecture

```
ArenaSim events --> CombatFeedback (MonoBehaviour, presentation only)
                        | asks
                        v
                    FeedbackPolicy (pure static: event + context -> Cue)
                        |  Cue { sheet, size, colour, ground, shake amp/dur,
                        |        hitStop seconds, callout text/colour, recoil, impactFrame }
                        v
      WorldEffects.Spawn | CameraShake.Kick | GameRoot.HitStop(s) | CalloutText | CharacterView.Recoil
```

| Unit | Status | Responsibility | Depends on |
|---|---|---|---|
| `FeedbackPolicy` | new, pure static | The cue table and the tier rules (section 4). No Unity objects. | Sim types only |
| `CombatFeedback` | new MonoBehaviour | Subscribes to `ArenaSim` events, asks the policy, executes the cue, applies `DisplayOptions.ReduceFlashes`, and tracks per-shot state for pierce detection | Policy and the outputs below |
| `ProjectileSkins` | new | Chooses each shot's sheet, halo colour and trail from (source school, faction, kind, overcharged, echo), and caches sprites | `WorldArtLibrary` |
| `CalloutText` | new | Pooled world-space `TextMesh` pop-ups in `m5x7` (point filtered); at most 3 live; a repeated text refreshes the live one | none |
| `GameRoot.HitStop(float)` | changed | Replaces the fixed `HitStopSeconds` with a parameter, following the "extend, never sum, cap" rule | Existing code |
| `CharacterView.Recoil(dir)` | changed | Visual-only nudge plus squash; settles in 0.1 s | Existing code |
| `ArenaView` shot view | changed | The disc becomes a halo; the sprite and `TrailRenderer` are added, pooled with the view | `ProjectileSkins` |

**Moves:**
- `GameRoot.OnOvercharged`'s shake and hit-stop move into the cue table.
- `ArenaView` keeps the enemy hit flash and the hitbox shadow.
- The boss effects in `WorldPresentation` (Teleport, Vortex, Shockwave, Wide Cleave, Heavy Hit, death) stay where they are.

**Sim changes** (additive signals only; no rule changes). Revised while planning, after reading the code:
- New event `QuickDrawFired(Vector2 at)`, raised when a release got the Quick Draw bonus. A new event, rather than a flag on `PacketReleased`, leaves that event's existing subscribers untouched.
- New event `OverflowFired(Vector2 at)`. Overflow's forced release raises only `PacketReleased`, and the catch that follows reports `CreatedPacket`, so nothing else identifies it.
- New event `PartingGiftBurst(Vector2 at, float radius)`, raised immediately before the gift's existing `Explosion`. Parting Gift bursts on every release, so without this event its `Explosion` would get the Tier 1 rocket shake on every release.
- `DamageEvent` gains `SourceCategory`, copied from the shot, so a hit spark can be tinted by school.
- Pierce needs no sim change. `CombatFeedback` sees a second `EnemyDamaged` with the same (`RootReleaseId`, `ShotId`, echo-or-not). The echo flag is part of the key because an echo copies its original's `ShotId`.

## 3. Projectiles

### 3.1 School shapes (local sheets)

| Source school (`AttackSnapshot.SourceCategory`) | Sheet | Note |
|---|---|---|
| Acolyte | Pixel VFX Essentials / Projectiles / Magic Missile | Baseline bolt |
| ScatterCaster | Pixel VFX Essentials / Projectiles / Ice Shard Shot | Thin and pointed, so a fan reads as spread |
| SiegeFamiliar | Pixel VFX Essentials / Projectiles / Fireball Shot | Plus Fire Trail puffs |
| Collector (boss) | Pixel VFX Essentials / Projectiles / Homing Orb | Reads as "the boss" |
| Player / unknown | Pixel VFX Essentials / Magic / Arcane Orb | Fallback |
| Riposte (any school) | `Resources/WorldArt/Dark Slash` | Already imported |

A shot keeps its school sprite after capture and return (D2).

### 3.2 State, size and facing

- **The halo is the honest signal.** The existing glow disc stays at the true hitbox size (`radius * 1.3`, as now), at about 45% alpha, in the existing colours:

  | State | Colour | Constant |
  |---|---|---|
  | Hostile | red-orange | `HostileColor` |
  | Returned | cyan | `ReturnedColor` |
  | Riposte | yellow | `RiposteColor` |
  | Rocket | orange | `RocketColor` |
  | Overcharged | gold | `OverchargeGold` |

- The school sprite sits on top of the halo, untinted.
- Sprite size is about `radius * 2.6`. The sprite turns to face its direction of travel.
- Echo shots draw their sprite at 60% alpha.
- The ground shadow is unchanged.

### 3.3 Trails

- Every shot gets a `TrailRenderer` in its state colour: width equal to the hitbox radius, fading to transparent.
- Time is 0.12 s for returned shots and 0.08 s for hostile ones.
- Rockets add Fire Trail puffs. Overcharged shots add Spark Trail.
- Trails are pooled with the shot view and cleared when the view is reused, so a reused view never draws a streak from its previous shot.

### 3.4 How a shot ends (Tier 0: no shake, no hit-stop, no callout)

| `ProjectileEndReason` | Effect |
|---|---|
| Expired | Small Pop in the state colour, 0.6x size |
| HitWall | Block Spark |
| HitActor | Section 4 |
| Captured | Existing capture visuals plus a small cyan Star Burst |
| Cleared | none |

**Muzzle flash:** `EnemyFired` from an ordinary enemy spawns a small Casting puff at the muzzle.

## 4. Impacts, shake, hit-stop, impact frames, callouts

### 4.1 Tiers

| Tier | Event | Effect | Shake amp / dur | Hit-stop | Callout |
|---|---|---|---|---|---|
| 0 | A returned, echo, orbit or Parting Gift hit on an enemy | School-coloured Hit Spark (Pierce Spark on a pierced second enemy). White flash, squash and 0.1-unit recoil along the shot | none | none | none |
| 1 | Ordinary kill | Smoke Burst plus Weak Hit | 0.05 / 0.10 | none | none |
| 1 | Player hit | Existing flash plus a red Heavy Hit | 0.12 / 0.20 | none (it would delay the next dodge) | none |
| 1 | Rocket explosion | Blast, sized to the explosion radius | 0.10 / 0.20 | none | none |
| 1 | Boss hit | Existing Heavy Hit and hurt pose | 0.06 / 0.10 | none | none |
| 2 | Parry (`StrikeParried`) | Parry Flash | 0.15 / 0.20 | 0.09 s | Parry! (yellow) |
| 2 | Perfect catch (`ShotCaptured` with `shot.Perfect`) | Critical Star | none | 0.05 s | Perfect!, or Last Second! with Final Second held (cyan) |
| 2 | Overcharge (`PacketOvercharged`) | Overload | 0.18 / 0.25 | 0.07 s | Overcharge! xP (gold), where P is the fire power. This replaces `ArenaView`'s existing "OVERCHARGE xP" label, so the multiplier stays visible and the label isn't doubled |
| 2 | Backfire (`PacketBackfired`) | Big Boom | 0.20 / 0.25 | none | Backfire! (red) |
| 2 | Fusion (`PacketsFused`) | Star Burst plus Zap Ring | none | none | Fusion! (violet) |
| 2 | Kill chain of 3 or more (`KillChainChanged`) | Star Burst on the kill | none | none | Chain xN! (orange) |
| 2 | Boss kill | Existing death effects | 0.35 / 0.50 | 0.15 s | none |

### 4.2 Rules (enforced by `FeedbackPolicy`, pinned by tests)

1. **No Tier 0 cue** carries shake, hit-stop, a callout or an impact frame.
2. **Shake:** the strongest active shake wins and shakes never add up. This is existing `CameraShake.Kick` behaviour.
3. **Hit-stop:** a new request extends the current one only up to its own length, never sums with it, and the total is capped at 0.15 s.
4. **Callouts:** at most 3 live; a repeat of the same text refreshes the existing one. Each appears above the event's position, rises about 0.6 units, pops in over 0.08 s and is gone by 0.6 s.
5. **`ReduceFlashes`** drops shake, hit-stop, impact frames and the screen flash. Sprites, trails and callouts remain.

### 4.3 Impact frames (Parry, Perfect catch, Overcharge, boss kill)

- During that event's hit-stop, the actors involved render as solid white silhouettes for 2 rendered frames.
- A full-screen flash at 20% alpha plays in the event's colour for the same 2 frames.

## 5. Upgrade and skill-tree visuals

### 5.1 Upgrade moments (Tier 0 unless section 4 says otherwise)

| Upgrade | Signal | Effect |
|---|---|---|
| Piercing Return | Second `EnemyDamaged` with the same `ShotId` | Pierce Spark plus a brief Chain Lightning arc between the two enemies |
| Echo Volley | `EchoFired` | A half-alpha afterimage of the original sprite at the muzzle; echo shots at 60% alpha |
| Heavy Orbit | `EnemyDamaged`, `DamageCategory.Orbit` | Shock Hit |
| Parting Gift | `PartingGiftBurst` (it bursts around the player on each release, not on a kill) | Zap Ring at the player, sized to the gift radius; Tier 0 school-tinted sparks on each enemy it hits |
| Overflow | `OverflowFired` | Overload at the player |
| Fusion | `PacketsFused` | Tier 2 (section 4) |
| Final Second | A perfect catch while held | Last Second! (section 1) |

### 5.2 Skill-node moments

- **Quick Draw:** while the post-swap window is open, a small Charge Up glint shows on the player's hand. `QuickDrawFired` adds Spark Burst at the muzzle.
- **Blood Price (leech, siphon, debt):** `LifeStolen` adds a small red Heal sheet on the player, alongside the existing number pop.

### 5.3 Passive cues (only while the action is happening)

| Passive | Cue |
|---|---|
| Dash distance | A dash afterimage trail of 3 fading silhouettes along the dash. A longer dash shows a longer trail |
| Dash recovery | Notify Ping on the player when `DashReadyAt` passes |
| Post-hit and dash grace | A faint Shield Bubble shimmer for the whole invulnerable window |
| Catch angle | none: the cone is already drawn at its true width |
| Capacity, move speed, starting time | none |

## 6. Assets

Sheets are copied from the local packs into `Assets/Game/Resources/WorldArt` and imported the same way as the existing sheets there.

`m5x7.ttf` is imported under `Assets/Game/Resources` with point filtering.

**Licences:**

| Pack | Licence |
|---|---|
| Free Pixel Effects Pack | Public domain |
| Foozle Pixel Magic Effects | CC0 |
| Pixel VFX Essentials | [IceMaan on itch.io](https://icemaan.itch.io/pixel-vfx-essentials): any project, commercial included; modification allowed; attribution welcome, not required; no resale or redistribution as an asset pack, no other asset stores, no AI training. Sheets stay git-ignored |
| m5x7 | [Daniel Linssen on itch.io](https://managore.itch.io/m5x7): "free to use but attribution appreciated". File redistribution is not addressed, so it stays git-ignored |

## 7. Testing

- **EditMode:**
  - Every rule in 4.2.
  - Each Tier 2 event maps to exactly the callout in 4.1, and Last Second! replaces Perfect! when Final Second is held.
  - The `ProjectileSkins` mapping for each school, state and echo.
  - Pierce detection.
  - The sim raises `QuickDrawFired` only inside the post-swap window, `OverflowFired` only on Overflow's forced release, and `PartingGiftBurst` once per gift; `DamageEvent.SourceCategory` matches the shot.
- **PlayMode:**
  - Raising each event spawns its effect.
  - Live callouts stay at 3 or fewer.
  - Shot views and trails are reused across 200 shots without the pool growing past the peak live count.
  - Reused trails start empty.
- **Visual check:** a capture of each Tier 2 moment and of the projectile shapes, sent to the owner.
- **Gate:** the full gate stays green, as in every prior pass.

## 8. Out of scope

- Knockback (D6).
- New player or enemy character art.
- Damage numbers.
- Particle systems and VFX Graph.
- Assets from outside the local packs (D5).
- Changes to the boss's existing effects.

## 9. Tuning

Every size, colour, shake, hit-stop and callout value above is a first pass. They live in the `FeedbackPolicy` table, so a retune touches one file.
