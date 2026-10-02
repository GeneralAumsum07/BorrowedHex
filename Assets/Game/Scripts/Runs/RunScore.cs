using System.Collections.Generic;
using BorrowedHex.Combat;
using BorrowedHex.Core;
using BorrowedHex.Enemies;
using UnityEngine;

namespace BorrowedHex.Runs
{
    /// <summary>
    /// Section 6 score, combo and run statistics for ONE run. It is built by the sim and
    /// subscribes only to that sim's own events, so a restart (new sim) cannot leave a
    /// listener behind or double-count into the next run.
    ///
    /// Rules:
    ///   - kill = kill value (elite 1.5x already folded in at spawn) x CURRENT multiplier;
    ///   - a release's FIRST successful returned hit adds +0.25 (cap 3.0) and refreshes a 5 s
    ///     timer; further hits from the same root release (pierce, echo, explosion) do not;
    ///   - taking damage resets the multiplier; the timer runs on the gameplay clock, so it is
    ///     frozen in menus and choices for free;
    ///   - catching scores nothing; despawns score nothing (no kill event is raised).
    /// Event order inside DamageEnemy is Damaged then Killed, so a killing blow that is also a
    /// release's first hit raises the multiplier BEFORE the kill is scored. Documented here
    /// because it decides one combo step on every first-hit kill.
    /// </summary>
    public sealed class RunScore
    {
        readonly ArenaSim sim;

        public int Score { get; private set; }
        public float Multiplier { get; private set; } = 1f;
        public double ComboExpiresAt { get; private set; }
        public int VictoryBonus { get; private set; }

        public int Kills { get; private set; }
        public int BossesDefeated { get; private set; }
        public int PacketsReleased { get; private set; }
        /// <summary>Released packets that damaged at least one enemy.</summary>
        public int PacketsHit { get; private set; }
        public int PerfectShots { get; private set; }
        public int DamageTaken { get; private set; }
        public int Backfires { get; private set; }
        public int Swaps { get; private set; }
        public int PillarsCrumbled { get; private set; }
        public int EnemiesOverstayed { get; private set; }
        public float SecondsGained { get; private set; }
        float totalFirePower;
        public float AverageFirePower => PacketsReleased == 0 ? 0f : totalFirePower / PacketsReleased;
        /// <summary>Most distinct enemies killed by one root release (a release and its echo count together).</summary>
        public int BestVolleyKills { get; private set; }
        public readonly Dictionary<AttackKind, int> KillsByKind = new Dictionary<AttackKind, int>();

        readonly HashSet<int> releasedRoots = new HashSet<int>();
        readonly HashSet<int> hitRoots = new HashSet<int>();
        readonly HashSet<int> comboRoots = new HashSet<int>();
        readonly Dictionary<int, HashSet<int>> killsByRoot = new Dictionary<int, HashSet<int>>();

        public RunScore(ArenaSim sim)
        {
            this.sim = sim;
            var ev = sim.Events;
            ev.EnemyDamaged += OnEnemyDamaged;
            ev.EnemyKilled += OnEnemyKilled;
            ev.PlayerHit += OnPlayerHit;
            ev.PacketReleased += OnPacketReleased;
            ev.PacketBackfired += _ => Backfires++;
            ev.SlotSwapped += _ => Swaps++;
            ev.PillarCrumbled += _ => PillarsCrumbled++;
            ev.EnemyOverstayed += _ => EnemiesOverstayed++;
        }

        public float HitRate => PacketsReleased == 0 ? 0f : (float)PacketsHit / PacketsReleased;

        /// <summary>Combo timer: called once per tick from the run flow.</summary>
        public void Tick(double now)
        {
            if (Multiplier > 1f && now >= ComboExpiresAt - 1e-9) Multiplier = 1f;
        }

        void OnEnemyDamaged(EnemyActor e, DamageEvent d)
        {
            int root = d.RootReleaseId;
            // Root 0 = not from a player release (nothing today; future contact/orbit damage).
            if (root == 0) return;
            if (releasedRoots.Contains(root) && hitRoots.Add(root)) PacketsHit++;
            // Ruling (D38): a riposte has its own root and builds combo like a packet does —
            // it is the parry's "release", and melee-only waves would otherwise be combo-dead.
            if (comboRoots.Add(root))
            {
                var sm = sim.Config.shortMode;
                Multiplier = Mathf.Min(sm.comboMax, Multiplier + sm.comboStep);
                ComboExpiresAt = sim.Clock.Now + sm.comboTimer;
            }
        }

        void OnEnemyKilled(EnemyActor e, DamageEvent d)
        {
            Score += Mathf.RoundToInt(e.KillValue * Multiplier);
            Kills++;
            if (e.IsBoss) BossesDefeated++;
            // Kills by HEX type: only returned payloads count. Orbit and Parting Gift kills are
            // upgrade damage, not a borrowed hex, and would otherwise pose as bolts or rockets.
            if (d.Category == DamageCategory.ReturnedProjectile || d.Category == DamageCategory.Echo
                || d.Category == DamageCategory.Explosion)
            {
                KillsByKind.TryGetValue(d.Kind, out int k);
                KillsByKind[d.Kind] = k + 1;
            }
            if (d.RootReleaseId != 0)
            {
                if (!killsByRoot.TryGetValue(d.RootReleaseId, out var set))
                    killsByRoot[d.RootReleaseId] = set = new HashSet<int>();
                // A set of actor IDs, so a pierce + echo that both "kill" the same enemy (the
                // second can't, but explosions and later upgrades might) never counts twice.
                set.Add(e.ActorId);
                BestVolleyKills = Mathf.Max(BestVolleyKills, set.Count);
            }
        }

        void OnPlayerHit(int amount, int source)
        {
            DamageTaken += amount;
            Multiplier = 1f;
            ComboExpiresAt = 0;
        }

        void OnPacketReleased(CapturedPacket p, int root)
        {
            PacketsReleased++;
            totalFirePower += p.FirePower(sim.Stats.PowerPerSecond);
            releasedRoots.Add(root);
            foreach (var s in p.Payloads) if (s.Perfect) PerfectShots++;
        }

        internal void RecordTimeGained(float seconds) => SecondsGained += seconds;

        /// <summary>Section 6: +2 per unused active second on a short-mode victory (whole seconds).</summary>
        internal void AddVictoryBonus(double unusedSeconds)
        {
            int secs = Mathf.Max(0, (int)System.Math.Floor(unusedSeconds + 1e-6));
            VictoryBonus = secs * sim.Config.shortMode.victoryBonusPerSecond;
            Score += VictoryBonus;
        }
    }

    /// <summary>
    /// The end-of-run record, frozen once (Phase 5). Plain copied values: nothing here points
    /// back into the sim, so the results screen and (Phase 7) the save system read a value
    /// that can never change under them.
    /// </summary>
    public sealed class RunSummary
    {
        public readonly RunEndReason Reason;
        public readonly string RunId;
        public readonly int Seed;
        public readonly GameMode Mode;
        public readonly string StyleId;
        public readonly float Duration;
        public readonly int Score;
        public readonly int VictoryBonus;
        public readonly int Kills;
        public readonly int BossesDefeated;
        public readonly int PacketsReleased;
        public readonly int PacketsHit;
        public readonly float HitRate;
        public readonly int PerfectShots;
        public readonly int DamageTaken;
        public readonly int Backfires;
        public readonly int Swaps;
        public readonly int PillarsCrumbled;
        public readonly int EnemiesOverstayed;
        public readonly float AverageFirePower;
        public readonly float SecondsGained;
        public readonly int BestVolleyKills;
        public readonly IReadOnlyDictionary<AttackKind, int> KillsByKind;
        public readonly int EncountersCompleted;

        RunSummary(ArenaSim sim, RunEndReason reason, double now)
        {
            var s = sim.Score;
            Reason = reason;
            RunId = sim.RunId;
            Seed = sim.Setup.Seed;
            Mode = sim.Setup.Mode;
            StyleId = sim.Setup.StyleId;
            Duration = (float)now;
            Score = s.Score;
            VictoryBonus = s.VictoryBonus;
            Kills = s.Kills;
            BossesDefeated = s.BossesDefeated;
            PacketsReleased = s.PacketsReleased;
            PacketsHit = s.PacketsHit;
            HitRate = s.HitRate;
            PerfectShots = s.PerfectShots;
            DamageTaken = s.DamageTaken;
            Backfires = s.Backfires;
            Swaps = s.Swaps;
            PillarsCrumbled = s.PillarsCrumbled;
            EnemiesOverstayed = s.EnemiesOverstayed;
            AverageFirePower = s.AverageFirePower;
            SecondsGained = s.SecondsGained;
            BestVolleyKills = s.BestVolleyKills;
            KillsByKind = new Dictionary<AttackKind, int>(s.KillsByKind);
            // Section 7: an encounter counts at its transition, which since D50 means it was
            // cleared (every member killed).
            EncountersCompleted = sim.TransitionsReached;
        }

        internal static RunSummary Freeze(ArenaSim sim, RunEndReason reason, double now) => new RunSummary(sim, reason, now);
    }
}
