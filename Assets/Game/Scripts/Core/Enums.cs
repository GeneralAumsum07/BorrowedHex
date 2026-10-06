namespace BorrowedHex.Core
{
    // Shared contracts from section 8 of the plan. Keep these central so every system
    // (combat, runs, progression, UI) agrees on one vocabulary.
    public enum GameMode { Short, Endless }

    public enum RunEndReason { Victory, Death, TimeExpired, Retired }

    // Riposte = a parried melee strike redirected by the player (D26); never fired by enemies.
    public enum AttackKind { Bolt, HeavyShot, Rocket, Riposte }

    // Hostile = fired by enemies (can hurt the player, can be captured).
    // Returned = released by the player (hurts enemies, never recapturable).
    public enum AttackFaction { Hostile, Returned }

    public enum ActorCategory { Player, Acolyte, Pursuer, ScatterCaster, SiegeFamiliar, Boss }

    // Every reason the gameplay clock may be frozen. Using a set of reasons instead of a
    // single bool means overlapping pauses (focus loss during an upgrade choice) cannot
    // accidentally resume combat when only one of them clears.
    public enum PauseReason { Menu, UpgradeChoice, Results, FocusLost, BossIntro, Manual, WorldTransition, Narrative }

    // High-level run flow from Phase 5.
    public enum RunState { Ready, Combat, UpgradeChoice, BossIntro, BossCombat, Paused, Results }

    // Where a piece of damage came from, so score/combo/achievements can treat each
    // category by its own rule (orbit damage never builds combo, for example).
    public enum DamageCategory { Contact, ReturnedProjectile, Echo, Explosion, PartingGift, Orbit, HostileProjectile }
}
