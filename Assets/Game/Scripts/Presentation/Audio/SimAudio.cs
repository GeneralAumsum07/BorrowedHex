using BorrowedHex.Combat;
using BorrowedHex.Core;
using BorrowedHex.Enemies;
using BorrowedHex.Runs;
using UnityEngine;

namespace BorrowedHex.Presentation.Audio
{
    /// <summary>
    /// Translates one run's SimEvents into cues from Assets/SFX/manifest.json, and drives the
    /// state-based layers (music, ambience, charge, heartbeat) from what it observes each frame.
    ///
    /// The sim stays silent and unaware of audio: everything here listens to events the sim
    /// already raises for presentation, exactly like CombatFeedback. Each run is a new sim, so
    /// Bind is called per run; the old sim is discarded whole and its events never fire again.
    /// </summary>
    public sealed class SimAudio
    {
        // Below this many life seconds the heartbeat plays. Inferred: the HUD has no "danger"
        // threshold to share, and 10 s is roughly one encounter's worth of drain.
        public const float DangerSeconds = 10f;

        ArenaSim sim;
        RunState lastState;
        int lastEncounter, lastWave;
        bool inDanger;
        RunEndReason? ended;
        readonly System.Collections.Generic.HashSet<int> overcharged = new System.Collections.Generic.HashSet<int>();

        public void Bind(ArenaSim s)
        {
            sim = s;
            lastState = s.State;
            lastEncounter = s.Encounter;
            lastWave = s.Wave;
            inDanger = false;
            ended = null;
            overcharged.Clear();
            GameAudio.StopAllLoops();
            var e = s.Events;

            // --- core borrowing (E) ---
            e.CatchActivated += _ => GameAudio.Play(CatchCue(sim.Setup.StyleId), 0.8f, 0.08f);
            e.ShotCaptured += (p, shot, at, r) =>
            {
                switch (r)
                {
                    case CaptureResult.CreatedPacket: GameAudio.Play("capture_success"); break;
                    case CaptureResult.Appended: GameAudio.Play("capture_append"); break;
                    case CaptureResult.Overflowed: GameAudio.Play("overflow_replace"); break;
                    case CaptureResult.Fused: GameAudio.Play("fusion_merge"); break;
                }
                if (p != null && p.IsFull) GameAudio.Play("packet_capacity_full", 0.8f, 0.2f);
            };
            e.CaptureRejected += (at, r) =>
            {
                // Rate-limited (README: "rate-limit repeated rejections").
                string cue = r == CaptureResult.PacketFull ? "catch_capacity_rejected"
                    : r == CaptureResult.SlotsFull ? "catch_slots_occupied"
                    : r == CaptureResult.HandFull ? "catch_hand_occupied" : null;
                if (cue != null) GameAudio.Play(cue, 0.8f, 0.3f);
            };
            e.PacketReleased += (p, root) => GameAudio.Play(ReleaseCue(p), 1f, 0.08f);
            e.PacketOvercharged += (p, root) => GameAudio.Play("overcharge_release");
            e.PacketBackfired += _ => GameAudio.Play("packet_backfire");
            e.ReleaseRefused += _ => GameAudio.Play("release_unprimed", 0.8f, 0.25f);
            e.SlotSwapped += _ => GameAudio.Play("slot_swap", 0.7f);
            e.PacketsFused += (a, b) => GameAudio.Play("fusion_merge");

            // --- upgrades (I) ---
            e.EchoFired += _ => GameAudio.Play("echo_volley_release", 0.8f, 0.08f);
            e.QuickDrawFired += _ => GameAudio.Play("quick_draw");
            e.OverflowFired += _ => GameAudio.Play("overflow_replace");
            e.PartingGiftBurst += (at, r) => GameAudio.Play("parting_gift");
            e.UpgradePaid += (o, secs) => GameAudio.Play("upgrade_buy_life_payment");
            e.KillChainChanged += (len, bonus) => { var c = ChainCue(len); if (c != null) GameAudio.Play(c, 0.8f); };

            // --- player (D) ---
            e.Dashed += (from, dir) => GameAudio.Play("dash_launch", 0.8f);
            e.PlayerHit += (amount, source) => GameAudio.Play(HitCue(source), 1f, 0.08f);
            e.PlayerDied += () => GameAudio.Play("player_death");
            e.LifeStolen += (secs, at) => GameAudio.Play("life_stolen", 0.6f, 0.2f);
            // Kills give life back constantly; quiet and spaced so it reads as texture.
            e.LifeClockChanged += (delta, at) => { if (delta > 0.01f) GameAudio.Play("life_restored_kill", 0.45f, 0.18f); };

            // --- enemies and the Collector (G, H) ---
            e.EnemySpawned += en => GameAudio.Play(en.IsBoss ? "collector_manifest" : Family(en.Category) + "_spawn_warning", 0.7f, 0.15f);
            e.EnemyTelegraph += en =>
            {
                GameAudio.Play(WindupCue(en), 1f, 0.1f);
                if (en.Elite) GameAudio.Play("elite_attack_layer", 0.7f, 0.1f);
            };
            e.EnemyFired += en => GameAudio.Play(FireCue(en), 0.9f, 0.08f);
            e.EnemyDamaged += (en, d) => GameAudio.Play(en.IsBoss ? "collector_hurt" : Family(en.Category) + "_hurt", 0.8f, 0.07f);
            e.EnemyKilled += (en, d) =>
            {
                // The boss death is a sequence (rupture, then the defeat stinger at results):
                // only the rupture here, so the moments do not all land at once (README).
                GameAudio.Play(en.IsBoss ? "collector_death_rupture" : Family(en.Category) + "_death", 1f, 0.05f);
                if (en.Elite) GameAudio.Play("elite_death_accent", 0.8f);
            };
            e.StrikeParried += (en, at) => GameAudio.Play(en.IsBoss ? "parry_collector" : "parry_pursuer");
            e.EnemyOverstayed += en =>
            {
                GameAudio.Play("overstay_warning", 0.8f, 0.3f);
                if (en.Elite) GameAudio.Play("elite_evolution", 0.8f, 0.3f);
            };

            // --- projectiles (F) ---
            e.ProjectileEnded += (p, reason) =>
            {
                string fam = ProjectileFamily(p);
                string cue = reason == ProjectileEndReason.HitWall ? fam + "_wall_impact"
                    : reason == ProjectileEndReason.HitActor ? fam + "_impact"
                    : reason == ProjectileEndReason.Expired ? fam + "_dissipate" : null;
                if (cue != null) GameAudio.Play(cue, reason == ProjectileEndReason.Expired ? 0.5f : 0.8f, 0.04f);
            };
            e.Explosion += (at, radius, faction) => GameAudio.Play("siege_rocket_explosion", 1f, 0.06f);

            // --- cover (J) ---
            e.PillarDamaged += (pillar, lost) => GameAudio.Play(Material() + "_wear", 0.6f, 0.4f);
            e.PillarCrumbled += pillar => GameAudio.Play(Material() + "_collapse", 1f, 0.1f);

            // --- run flow (B) ---
            e.RunEnded += summary =>
            {
                ended = summary.Reason;
                GameAudio.StopAllLoops();
                GameAudio.Play(summary.Reason == RunEndReason.Victory ? "short_run_victory"
                    : summary.Reason == RunEndReason.Death ? "death_defeat"
                    : summary.Reason == RunEndReason.TimeExpired ? "time_expiry_defeat" : "run_retired");
            };
        }

        /// <summary>Per-frame layers: music, ambience and the state loops. Called after the sim ticked.</summary>
        public void Tick(bool mainMenu, bool tutorial, bool practice)
        {
            if (sim == null) return;
            var state = sim.State;
            if (state != lastState) OnState(lastState, state);
            lastState = state;

            if (sim.Encounter != lastEncounter)
            {
                if (sim.Encounter > lastEncounter) GameAudio.Play("encounter_clear");
                lastEncounter = sim.Encounter;
            }
            if (sim.Setup.Mode == GameMode.Endless && sim.Wave != lastWave)
            {
                if (sim.Wave > lastWave) GameAudio.Play("endless_wave_clear");
                lastWave = sim.Wave;
            }

            GameAudio.SetMusic(MusicCue(mainMenu, tutorial, practice, state));
            GameAudio.SetAmbience(AmbienceCue());

            // State loops only while the clock runs: a pause menu must not keep charging.
            bool live = !mainMenu && !sim.Clock.IsPaused && state != RunState.Results;
            bool holding = false;
            if (sim.Packets != null)
                foreach (var p in sim.Packets.Packets)
                {
                    if (p.Status != PacketStatus.Stored) continue;
                    holding = true;
                    // Edge, not level: the window cue plays once as each packet enters Overcharge.
                    if (p.IsOvercharged(sim.Stats.Power)) { if (overcharged.Add(p.PacketId)) GameAudio.Play("overcharge_window_enter", 0.9f); }
                }
            GameAudio.Loop("packet_charge", live && holding, 0.35f);

            bool danger = live && !sim.Setup.LifeLocked && sim.LifeSeconds < DangerSeconds;
            if (danger && !inDanger) GameAudio.Play("life_danger_enter");
            inDanger = danger;
            GameAudio.Loop("low_life_heartbeat", danger, 0.6f);
        }

        void OnState(RunState from, RunState to)
        {
            if (to == RunState.Combat && from == RunState.Ready) GameAudio.Play("run_start");
            else if (to == RunState.UpgradeChoice) GameAudio.Play("upgrade_offers");
            // The Sanctum score is cut to the reveal's 7.8 s, so it plays when the reveal does:
            // at SanctumArrival in a short run (lore plan Task 3), at BossIntro in endless.
            // Outside the world arenas the plain reveal stinger carries BossIntro. Never both (README).
            else if (to == RunState.SanctumArrival)
            {
                if (sim.ArenaStage == 3) GameAudio.Play("sanctum_intro", 0.9f);
            }
            else if (to == RunState.BossIntro)
            {
                if (sim.ArenaStage != 3) GameAudio.Play("boss_reveal", 0.9f);
                else if (!sim.IsShortRun) GameAudio.Play("sanctum_intro", 0.9f);
            }
            else if (to == RunState.BossCombat) GameAudio.Play("boss_combat_start");
        }

        string MusicCue(bool mainMenu, bool tutorial, bool practice, RunState state)
        {
            if (mainMenu) return "main_menu";
            if (tutorial || practice) return "training";
            switch (state)
            {
                case RunState.UpgradeChoice: return "upgrade_selection";
                // The intro score (a one-shot) carries the reveal; the loop waits for the fight.
                case RunState.BossIntro: return null;
                case RunState.SanctumArrival: return null;
                case RunState.BossCombat: return "collector_battle";
                case RunState.Results: return ended == RunEndReason.Victory ? "victory_results" : "failure_results";
            }
            switch (Stage())
            {
                case 1: return "graveyard_combat";
                case 2: return "cave_combat";
                default: return "courtyard_combat";
            }
        }

        string AmbienceCue()
        {
            switch (sim.ArenaStage)
            {
                case 1: return "graveyard";
                case 2: return "cave";
                case 3: return "sanctum";
                default: return "courtyard";
            }
        }

        // The Sanctum (stage 3) has no combat score of its own: the Cave score carries until the boss.
        int Stage() => Mathf.Clamp(sim.ArenaStage, 0, 2);

        // Inferred materials: Courtyard and Sanctum are stone, Graveyard ceramic urns, Cave rock.
        string Material() => sim.ArenaStage == 1 ? "ceramic" : sim.ArenaStage == 2 ? "rock" : "stone";

        static string CatchCue(string style) =>
            style == "collector" ? "catch_collector_style" : style == "daredevil" ? "catch_daredevil" : "catch_snatcher";

        static string Family(ActorCategory c)
        {
            switch (c)
            {
                case ActorCategory.Pursuer: return "pursuer";
                case ActorCategory.ScatterCaster: return "scatter_caster";
                case ActorCategory.SiegeFamiliar: return "siege_familiar";
                default: return "acolyte";
            }
        }

        static string ProjectileFamily(ProjectileActor p)
        {
            switch (p.Shot.Kind)
            {
                case AttackKind.HeavyShot: return "scatter_volley";
                case AttackKind.Rocket: return "siege_rocket";
                case AttackKind.Riposte: return "riposte";
                default: return "acolyte_bolt";
            }
        }

        static string ReleaseCue(CapturedPacket p)
        {
            if (p == null) return "acolyte_bolt_returned_launch";
            // A packet carrying more than one kind of shot gets the mixed cue.
            for (int i = 1; i < p.Payloads.Count; i++)
                if (p.Payloads[i].Kind != p.Payloads[0].Kind) return "mixed_packet_release";
            switch (p.DominantKind)
            {
                case AttackKind.HeavyShot: return "scatter_volley_returned_launch";
                case AttackKind.Rocket: return "siege_rocket_returned_launch";
                case AttackKind.Riposte: return "riposte_launch";
                default: return "acolyte_bolt_returned_launch";
            }
        }

        string HitCue(int source)
        {
            // A Pursuer's strike is melee; anything else that hurts is a shot (or a collision).
            foreach (var en in sim.Enemies)
                if (en.ActorId == source)
                    return en.Category == ActorCategory.Pursuer || (en.IsBoss && en.Boss != null && CollectorBoss.IsMelee(en.Boss.Pattern))
                        ? "player_hit_melee" : "player_hit_projectile";
            return "player_hit_projectile";
        }

        static string WindupCue(EnemyActor en)
        {
            if (en.IsBoss && en.Boss != null)
                switch (en.Boss.Pattern)
                {
                    case BossPattern.Sweep: return "collector_sweep_windup";
                    case BossPattern.FanVolley: return "collector_fan_windup";
                    case BossPattern.Slam: return "collector_slam_warning";
                    case BossPattern.Summon: return "collector_summon_windup";
                    default: return "collector_stream_windup";
                }
            switch (en.Category)
            {
                case ActorCategory.Pursuer: return "pursuer_windup";
                case ActorCategory.ScatterCaster: return "scatter_windup";
                case ActorCategory.SiegeFamiliar: return "siege_windup";
                default: return "acolyte_windup";
            }
        }

        static string FireCue(EnemyActor en)
        {
            if (en.IsBoss && en.Boss != null)
                switch (en.Boss.Pattern)
                {
                    case BossPattern.Sweep: return "collector_sweep";
                    case BossPattern.FanVolley: return "collector_fan_release";
                    case BossPattern.Slam: return "collector_slam";
                    case BossPattern.Summon: return "collector_summon_release";
                    default: return "collector_bolt_launch";
                }
            switch (en.Category)
            {
                case ActorCategory.Pursuer: return "pursuer_strike";
                case ActorCategory.ScatterCaster: return "scatter_volley_launch";
                case ActorCategory.SiegeFamiliar: return "siege_rocket_launch";
                default: return "acolyte_bolt_launch";
            }
        }

        static string ChainCue(int len)
        {
            // A chain of one is just a kill. Two to five climb; past five every fifth is a milestone.
            if (len <= 0) return "chain_break";
            if (len == 1) return null;
            if (len == 2) return "chain_start";
            if (len <= 5) return "chain_advance_0" + len;
            return len % 5 == 0 ? "chain_milestone" : "chain_advance_05";
        }
    }
}
