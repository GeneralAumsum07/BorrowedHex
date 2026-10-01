using System;
using BorrowedHex.Combat;
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
        public event Action LanternFired;

        internal void RaiseProjectileSpawned(ProjectileActor p) => ProjectileSpawned?.Invoke(p);
        internal void RaiseProjectileEnded(ProjectileActor p, ProjectileEndReason r) => ProjectileEnded?.Invoke(p, r);
        internal void RaiseEnemySpawned(EnemyActor e) => EnemySpawned?.Invoke(e);
        internal void RaiseEnemyTelegraph(EnemyActor e) => EnemyTelegraph?.Invoke(e);
        internal void RaiseEnemyFired(EnemyActor e) => EnemyFired?.Invoke(e);
        internal void RaiseEnemyDamaged(EnemyActor e, DamageEvent d) => EnemyDamaged?.Invoke(e, d);
        internal void RaiseEnemyKilled(EnemyActor e, DamageEvent d) => EnemyKilled?.Invoke(e, d);
        internal void RaiseLanternFired() => LanternFired?.Invoke();
    }
}
