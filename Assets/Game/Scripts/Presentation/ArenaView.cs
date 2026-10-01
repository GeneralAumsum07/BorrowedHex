using BorrowedHex.Core;
using BorrowedHex.Runs;
using UnityEngine;

namespace BorrowedHex.Presentation
{
    /// <summary>
    /// Draws one ArenaSim. Views are disposable: on restart the whole view root is destroyed
    /// and rebuilt against the new sim, mirroring the sim's own one-instance-per-run rule, so
    /// nothing visual can survive into the next run.
    ///
    /// Positions are interpolated between the previous and current fixed step using the
    /// accumulator fraction GameRoot passes in. Without this, a 60 Hz sim on a 144 Hz display
    /// visibly stutters; with it, the sim stays deterministic and only the drawing is smooth.
    /// </summary>
    public sealed class ArenaView : MonoBehaviour
    {
        ArenaSim sim;
        CharacterView player;
        SpriteRenderer aimMarker;
        Vector2 prevPlayer, currPlayer;

        public CharacterView PlayerView => player;

        public static ArenaView Create(ArenaSim sim)
        {
            var go = new GameObject("ArenaView");
            var view = go.AddComponent<ArenaView>();
            view.Bind(sim);
            return view;
        }

        void Bind(ArenaSim s)
        {
            sim = s;
            player = CharacterView.Create(transform, "Player", PixelSprites.Kind.Magician, 1f, 0.45f);
            prevPlayer = currPlayer = sim.Player.Position;

            var marker = new GameObject("AimMarker");
            marker.transform.SetParent(transform, false);
            marker.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            marker.transform.localScale = Vector3.one * 0.32f;
            aimMarker = marker.AddComponent<SpriteRenderer>();
            aimMarker.sprite = PixelSprites.Disc(true);
            aimMarker.color = new Color(1f, 0.85f, 0.35f, 0.85f);

            sim.Events.PlayerHit += (_, __) => player.Flash(0.12f);
        }

        /// <summary>Called by GameRoot immediately before each sim step.</summary>
        public void BeforeStep() => prevPlayer = sim.Player.Position;

        /// <summary>Called by GameRoot immediately after each sim step.</summary>
        public void AfterStep() => currPlayer = sim.Player.Position;

        /// <summary><paramref name="alpha"/> is the leftover accumulator fraction in [0, 1).</summary>
        public void Render(float alpha)
        {
            var p = sim.Player;
            Vector2 pos = Vector2.Lerp(prevPlayer, currPlayer, alpha);
            player.transform.position = Geometry2D.ToWorld(pos);
            player.SetFacing(p.AimDirection.x);
            // Post-hit invulnerability blinks; dash i-frames are short enough to skip.
            player.SetBlink(p.Alive && p.InvulnerableUntil > sim.Clock.Now);
            player.gameObject.SetActive(p.Alive);

            aimMarker.enabled = p.Alive;
            aimMarker.transform.position = Geometry2D.ToWorld(pos + p.AimDirection * 1.4f) + Vector3.up * 0.03f;
        }
    }
}
