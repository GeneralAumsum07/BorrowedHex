using System.Collections.Generic;
using BorrowedHex.Combat;
using BorrowedHex.Core;
using UnityEngine;

namespace BorrowedHex.Presentation.Feedback
{
    /// <summary>
    /// Piercing Return's moment without a sim change (spec 2, 5.1): the same returned shot
    /// damaging a second enemy. Keyed by (root release, shot, echo) because an echo copies its
    /// original's ShotId and is a different shot. Entries are forgotten when the shot ends, so
    /// the map stays as small as the number of live shots.
    /// </summary>
    public sealed class PierceTracker
    {
        readonly Dictionary<(int, int, bool), Vector2> lastHit = new Dictionary<(int, int, bool), Vector2>();

        public int Count => lastHit.Count;

        public static bool Tracks(DamageCategory c) => c == DamageCategory.ReturnedProjectile || c == DamageCategory.Echo;

        /// <summary>True when this shot already hit another enemy; <paramref name="previous"/> is where.</summary>
        public bool Hit(in DamageEvent d, Vector2 at, out Vector2 previous)
        {
            previous = at;
            if (!Tracks(d.Category)) return false;
            var key = (d.RootReleaseId, d.ShotId, d.Category == DamageCategory.Echo);
            bool pierced = lastHit.TryGetValue(key, out var before);
            if (pierced) previous = before;
            lastHit[key] = at;
            return pierced;
        }

        public void Forget(int root, int shotId, bool echo) => lastHit.Remove((root, shotId, echo));
    }
}
