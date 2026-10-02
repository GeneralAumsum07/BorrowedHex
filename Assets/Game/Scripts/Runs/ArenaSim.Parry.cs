using BorrowedHex.Combat;
using BorrowedHex.Core;
using BorrowedHex.Data;
using BorrowedHex.Enemies;
using UnityEngine;

namespace BorrowedHex.Runs
{
    /// <summary>
    /// Parry (D26, replaces the arcane lantern): the magician's catch also intercepts a melee
    /// strike and flings it back at the attacker.
    ///
    /// Why it reuses the catch rather than a new button: the game's single verb is "borrow the
    /// enemy's attack". A Pursuer strike is just an attack that doesn't fly, so the same press,
    /// the same cone and the same 0.25 s window apply — nothing new to learn, and mistiming
    /// still costs the full catch recovery, which is the risk that pays for the reward.
    ///
    /// Why it answers ammunition starvation: a melee-only remainder used to need the lantern to
    /// supply bolts. Now the melee enemies ARE the ammunition — every Pursuer strike is a
    /// riposte waiting to happen — so the arena never needs an outside emitter.
    /// </summary>
    public sealed partial class ArenaSim
    {
        /// <summary>
        /// Called by a melee brain at the instant its strike resolves, BEFORE damage. Returns
        /// true if the strike was parried (the caller must then deal no damage).
        ///
        /// Conditions, all at that one instant (D36, owner direction: "make parrying harder"):
        ///   - the PARRY part of the catch window is open — its first half (ParryWindow), so a
        ///     parry is an early committed press, not anything inside the full catch window;
        ///   - the player's parry band (a thin arc inside the cone, at ParryRingRadius) touches
        ///     the strike circle's thin outer rim band (ParryGeometry.BandsMeet).
        /// Facing is implied: the band only exists inside the cone. Being inside the strike
        /// circle is NOT required — a band can reach the near rim from just outside it — and is
        /// not sufficient either: inside it with the bands apart, the strike lands.
        /// Packet slots and capacity are deliberately NOT consulted (D28): the riposte never
        /// enters a packet, so holding ammunition can never stop the player answering a melee enemy.
        /// </summary>
        internal bool TryParry(EnemyActor attacker, Vector2 strikeCentre, float strikeRadius, float rimWidth)
        {
            double now = Clock.Now;
            if (!Player.Alive || !Capture.IsParryOpen(now)) return false;
            if (!ParryGeometry.BandsMeet(Player.Position, Player.AimDirection, Stats.CaptureConeAngle * 0.5f,
                    Stats.ParryRingRadius, Stats.ParryRingWidth, strikeCentre, strikeRadius, rimWidth))
                return false;

            // Redirect at the ATTACKER, not along the aim: the cone is 90° wide, so a strike
            // parried at the cone edge would otherwise sail past the enemy that made it. Pierce
            // then carries it on to whatever stands behind on that line.
            Vector2 dir = attacker.Position - Player.Position;
            if (dir.sqrMagnitude < 1e-8f) dir = Player.AimDirection;
            var shot = AttackSnapshot.From(Attacks.Get(AttackIds.Riposte), attacker.ActorId, Ids.Next(), 0f);
            // Spawned from the player's centre: returned shots never touch the player, and the
            // centre is never inside a wall, so the riposte cannot start embedded in anything.
            // Its own root ID keeps it separate from any packet release for stats.
            SpawnProjectile(shot, AttackFaction.Returned, Player.Position, dir, rootReleaseId: Ids.Next(),
                pierce: Config.combat.ripostePierce);
            Events.RaiseStrikeParried(attacker, strikeCentre);
            return true;
        }
    }
}
