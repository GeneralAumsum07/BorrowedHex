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
        /// <summary>D93: perfect releases (fired inside the Overcharge zone).</summary>
        public int Overcharges { get; private set; }
        /// <summary>D95: the longest kill chain this run (owned by the sim's KillChain).</summary>
        public int BestChain => sim.Chain?.Best ?? 0;
        // D96: life paid for upgrades. Kept apart from DamageTaken: a price is not a hit (R13f).
        public float SecondsSacrificed { get; private set; }
        public int UpgradesPaidFor { get; private set; }
        public int MostUpgradesHeld { get; private set; }
        internal void RecordUpgradePaid(float seconds) { SecondsSacrificed += seconds; UpgradesPaidFor++; }
        // D99: life returned by lifesteal. Kept apart from SecondsGained (kill rewards and chains),
        // so the results can show what the Blood Price nodes actually paid back.
        public float LifeStolen { get; private set; }
        internal void RecordLifeStolen(float seconds) => LifeStolen += seconds;
        // A high-water mark: a swap keeps the count, so only adds can raise it.
        internal void RecordHeld(int count) { if (count > MostUpgradesHeld) MostUpgradesHeld = count; }
        public int Swaps { get; private set; }
        public int PillarsCrumbled { get; private set; }
        public int EnemiesOverstayed { get; private set; }
        /// <summary>Kills of enemies that had overstayed (section 7 XP term; the boss never overstays).</summary>
        public int OverstayedKills { get; private set; }
        /// <summary>Distinct perfect shots that damaged an enemy (section 7 XP term, Perfect Timing).</summary>
        public int PerfectHits => perfectHitShots.Count;
        readonly HashSet<int> perfectHitShots = new HashSet<int>();

        // ---- Achievement facts (section 7), gathered where the events are, frozen in the summary.
        /// <summary>A returned payload (not a riposte) damaged an enemy.</summary>
        public bool FirstBorrow { get; private set; }
        /// <summary>A returned payload killed the very actor that cast it.</summary>
        public bool ReturnPolicy { get; private set; }
        /// <summary>Short-mode encounters cleared with no hit and no backfire.</summary>
        public int UntouchableEncounters { get; private set; }
        int hitsThisEncounter;
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
            ev.PacketOvercharged += (_, __) => Overcharges++;   // D93
            ev.PacketBackfired += _ => Backfires++;
            ev.SlotSwapped += _ => Swaps++;
            ev.PillarCrumbled += _ => PillarsCrumbled++;
            ev.EnemyOverstayed += _ => EnemiesOverstayed++;
            ev.RunStateChanged += OnRunStateChanged;
            // D102 (owner): the skill shots score, and the combo multiplies them like it does kills.
            // Overcharge: once per release (an echo is not a release, so it raises nothing here).
            ev.PacketOvercharged += (_, __) => AddBonus(sim.Config.shortMode.overchargeScore, ref scoreFromOvercharges);
            // Chains: KillChainChanged is raised from the kill's own time reward, after this
            // class's EnemyKilled handler bumped the combo, so the bonus uses the same combo the
            // kill was scored at. Boss kills never raise it (R15), so they never score a chain.
            ev.KillChainChanged += (length, _) =>
            {
                var t = sim.Config.shortMode.chainScore;
                if (t == null || t.Length == 0 || length < 1) return;
                AddBonus(t[Mathf.Min(length, t.Length) - 1], ref scoreFromChains);
            };
        }

        int scoreFromOvercharges, scoreFromChains;
        /// <summary>D102: score earned by Overcharged releases (already inside <see cref="Score"/>).</summary>
        public int ScoreFromOvercharges => scoreFromOvercharges;
        /// <summary>D102: score earned by chain kills beyond the first (already inside <see cref="Score"/>).</summary>
        public int ScoreFromChains => scoreFromChains;

        /// <summary>Add a bonus at the current combo, into the total and into its own bucket for the results.</summary>
        void AddBonus(int basePoints, ref int bucket)
        {
            if (basePoints <= 0) return;
            int pts = Mathf.RoundToInt(basePoints * Multiplier);
            Score += pts;
            bucket += pts;
        }

        /// <summary>
        /// Encounter bookkeeping for Untouchable. A backfire costs life through the same damage
        /// path as a hit (it raises PlayerHit), so counting PlayerHit covers both.
        /// </summary>
        void OnRunStateChanged(RunState state)
        {
            // Only a real encounter start resets the count: resuming from a pause also sets
            // Combat, and must not wipe hits taken before the pause.
            if (state == RunState.Combat && (previousState == RunState.Ready || previousState == RunState.UpgradeChoice))
                hitsThisEncounter = 0;
            // Combat -> UpgradeChoice happens only when an encounter is cleared (D50); a resume
            // from a pause back into the choice is not a second clear.
            else if (state == RunState.UpgradeChoice && previousState == RunState.Combat
                     && sim.Setup.Mode == GameMode.Short && hitsThisEncounter == 0)
                UntouchableEncounters++;
            previousState = state;
        }

        RunState previousState = RunState.Ready;

        /// <summary>Captured and returned (or its echo/blast); ripostes were never borrowed.</summary>
        static bool IsReturnedPayload(in DamageEvent d) =>
            d.Kind != AttackKind.Riposte && (d.Category == DamageCategory.ReturnedProjectile
                || d.Category == DamageCategory.Echo || d.Category == DamageCategory.Explosion);

        public float HitRate => PacketsReleased == 0 ? 0f : (float)PacketsHit / PacketsReleased;

        /// <summary>Combo timer: called once per tick from the run flow.</summary>
        public void Tick(double now)
        {
            if (Multiplier > 1f && now >= ComboExpiresAt - 1e-9) Multiplier = 1f;
        }

        void OnEnemyDamaged(EnemyActor e, DamageEvent d)
        {
            // Counted by shot ID, so a perfect shot that pierces three enemies is one perfect
            // shot. Echoes are excluded: they copy the perfect flag but were never caught.
            if (d.Perfect && d.Category != DamageCategory.Echo) perfectHitShots.Add(d.ShotId);
            if (IsReturnedPayload(d)) FirstBorrow = true;
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
            else if (e.Overstayed) OverstayedKills++;
            if (IsReturnedPayload(d) && d.SourceActorId == e.ActorId) ReturnPolicy = true;
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
            hitsThisEncounter++;
            Multiplier = 1f;
            ComboExpiresAt = 0;
        }

        void OnPacketReleased(CapturedPacket p, int root)
        {
            PacketsReleased++;
            totalFirePower += p.FirePower(sim.Stats.Power);
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
        /// <summary>D93: releases fired inside the Overcharge zone.</summary>
        public readonly int Overcharges;
        public readonly int BestChain;   // D95
        public readonly float SecondsSacrificed;   // D96
        public readonly float LifeStolen;          // D99
        public readonly int ScoreFromOvercharges;  // D102
        public readonly int ScoreFromChains;       // D102
        public readonly int UpgradesPaidFor;
        public readonly int MostUpgradesHeld;
        public readonly int Swaps;
        public readonly int PillarsCrumbled;
        public readonly int EnemiesOverstayed;
        public readonly int OverstayedKills;
        public readonly int PerfectHits;
        public readonly bool FirstBorrow, ReturnPolicy;
        public readonly int UntouchableEncounters;
        public readonly float AverageFirePower;
        public readonly float SecondsGained;
        public readonly int BestVolleyKills;
        public readonly IReadOnlyDictionary<AttackKind, int> KillsByKind;
        public readonly int EncountersCompleted;
        /// <summary>Endless: waves whose 30 s ran out, and the cycle the run ended in.</summary>
        public readonly int WavesCompleted;
        /// <summary>Frozen from the setup at the end: a debug-assisted or sandbox run never submits.</summary>
        public readonly bool Debug;
        public readonly int Cycle;

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
            Overcharges = s.Overcharges;
            BestChain = s.BestChain;
            SecondsSacrificed = s.SecondsSacrificed;
            LifeStolen = s.LifeStolen;
            ScoreFromOvercharges = s.ScoreFromOvercharges;
            ScoreFromChains = s.ScoreFromChains;
            UpgradesPaidFor = s.UpgradesPaidFor;
            MostUpgradesHeld = s.MostUpgradesHeld;
            Swaps = s.Swaps;
            PillarsCrumbled = s.PillarsCrumbled;
            EnemiesOverstayed = s.EnemiesOverstayed;
            OverstayedKills = s.OverstayedKills;
            PerfectHits = s.PerfectHits;
            FirstBorrow = s.FirstBorrow;
            ReturnPolicy = s.ReturnPolicy;
            UntouchableEncounters = s.UntouchableEncounters;
            AverageFirePower = s.AverageFirePower;
            SecondsGained = s.SecondsGained;
            BestVolleyKills = s.BestVolleyKills;
            KillsByKind = new Dictionary<AttackKind, int>(s.KillsByKind);
            // Section 7: an encounter counts at its transition, which since D50 means it was
            // cleared (every member killed).
            // Endless counts completed waves instead (section 7 XP).
            EncountersCompleted = sim.IsEndlessRun ? sim.WavesCompleted : sim.TransitionsReached;
            WavesCompleted = sim.WavesCompleted;
            Debug = sim.Setup.Debug || sim.Setup.Sandbox;
            Cycle = sim.Cycle;
        }

        internal static RunSummary Freeze(ArenaSim sim, RunEndReason reason, double now) => new RunSummary(sim, reason, now);
    }
}
