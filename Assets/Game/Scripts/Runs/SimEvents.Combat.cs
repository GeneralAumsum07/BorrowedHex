using System;
using BorrowedHex.Combat;
using BorrowedHex.Core;
using BorrowedHex.Enemies;

namespace BorrowedHex.Runs
{
    /// <summary>
    /// Phase 2 combat events. Handlers receive live (pooled) objects: copy what you need
    /// during the callback; a ProjectileActor is reused once it has ended.
    /// </summary>
    public sealed partial class SimEvents
    {
        public event Action<ProjectileActor> ProjectileSpawned;
        public event Action<ProjectileActor, ProjectileEndReason> ProjectileEnded;
        public event Action<EnemyActor> EnemySpawned;
        public event Action<EnemyActor> EnemyTelegraph;
        public event Action<EnemyActor> EnemyFired;
        public event Action<EnemyActor, DamageEvent> EnemyDamaged;
        public event Action<EnemyActor, DamageEvent> EnemyKilled;
        /// <summary>A melee strike was parried: the attacker, and where the strike would have landed.</summary>
        public event Action<EnemyActor, UnityEngine.Vector2> StrikeParried;
        public event Action<EnemyActor> EnemyDespawned;
        /// <summary>A rocket burst: position, radius, and whose it was (only Returned ones deal area damage).</summary>
        public event Action<UnityEngine.Vector2, float, AttackFaction> Explosion;
        public event Action<int> CatchActivated;
        public event Action<CapturedPacket, AttackSnapshot, UnityEngine.Vector2, CaptureResult> ShotCaptured;
        public event Action<UnityEngine.Vector2, CaptureResult> CaptureRejected;
        public event Action<CapturedPacket, int> PacketReleased;
        public event Action<CapturedPacket> PacketBackfired;
        public event Action<int> SlotSwapped;
        public event Action<EnemyActor> EnemyOverstayed;
        /// <summary>Clock wear changed cover: pillar and actual durability lost this tick.</summary>
        public event Action<DecayObstacle, int> PillarDamaged;
        public event Action<DecayObstacle> PillarCrumbled;

        internal void RaiseProjectileSpawned(ProjectileActor p) => ProjectileSpawned?.Invoke(p);
        internal void RaiseProjectileEnded(ProjectileActor p, ProjectileEndReason r) => ProjectileEnded?.Invoke(p, r);
        internal void RaiseEnemySpawned(EnemyActor e) => EnemySpawned?.Invoke(e);
        internal void RaiseEnemyTelegraph(EnemyActor e) => EnemyTelegraph?.Invoke(e);
        internal void RaiseEnemyFired(EnemyActor e) => EnemyFired?.Invoke(e);
        internal void RaiseEnemyDamaged(EnemyActor e, DamageEvent d) => EnemyDamaged?.Invoke(e, d);
        internal void RaiseEnemyKilled(EnemyActor e, DamageEvent d) => EnemyKilled?.Invoke(e, d);
        internal void RaiseStrikeParried(EnemyActor e, UnityEngine.Vector2 at) => StrikeParried?.Invoke(e, at);
        internal void RaiseEnemyDespawned(EnemyActor e) => EnemyDespawned?.Invoke(e);
        internal void RaiseExplosion(UnityEngine.Vector2 at, float radius, AttackFaction f) => Explosion?.Invoke(at, radius, f);
        internal void RaiseCatchActivated(int activation) => CatchActivated?.Invoke(activation);
        internal void RaiseShotCaptured(CapturedPacket p, AttackSnapshot s, UnityEngine.Vector2 at, CaptureResult r) => ShotCaptured?.Invoke(p, s, at, r);
        internal void RaiseCaptureRejected(UnityEngine.Vector2 at, CaptureResult r) => CaptureRejected?.Invoke(at, r);
        internal void RaisePacketReleased(CapturedPacket p, int root) => PacketReleased?.Invoke(p, root);
        internal void RaisePacketBackfired(CapturedPacket p) => PacketBackfired?.Invoke(p);
        internal void RaiseSlotSwapped(int slot) => SlotSwapped?.Invoke(slot);
        internal void RaiseEnemyOverstayed(EnemyActor enemy) => EnemyOverstayed?.Invoke(enemy);
        internal void RaisePillarDamaged(DecayObstacle pillar, int lost) => PillarDamaged?.Invoke(pillar, lost);
        internal void RaisePillarCrumbled(DecayObstacle pillar) => PillarCrumbled?.Invoke(pillar);
    }
}
