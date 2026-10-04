using System;
using BorrowedHex.Core;
using UnityEngine;

namespace BorrowedHex.Presentation.Feedback
{
    /// <summary>Every moment the feedback reacts to (spec 3.4, 4.1, 5).</summary>
    public enum CueEvent
    {
        EnemyHit, PierceHit, OrbitHit, Kill, BossHit, BossKill, PlayerHit, Explosion,
        Parry, PerfectCatch, Overcharge, Backfire, Fusion, Chain,
        PartingGift, Overflow, QuickDraw, LifeStolen,
        ShotExpired, ShotHitWall, ShotCaptured, Muzzle, DashReady,
        BossSlam, BossSweep, SweepDust,
    }

    /// <summary>
    /// What one moment looks like. A value, so the policy stays pure and testable; executing it
    /// (spawning sheets, shaking, freezing) is CombatFeedback's job. Zero/null fields mean "none".
    /// </summary>
    public struct Cue
    {
        public int Tier;
        public string Sheet; public float Size; public Color Color; public int Cell; public bool Ground;
        // A second sheet: kill smoke + spark, fusion star + ring, the pierce arc.
        public string Sheet2; public float Size2; public Color Color2;
        public float ShakeAmp, ShakeDuration, HitStop;
        public string Callout; public Color CalloutColor;
        public bool Recoil, ImpactFrame;
    }

    /// <summary>The facts a cue may depend on, gathered by CombatFeedback from the event.</summary>
    public struct CueContext
    {
        public ActorCategory School;   // whose shot caused the hit
        public Color StateColor;       // the ended shot's state colour
        public float Radius;           // explosion / gift radius
        public float Power;            // an overcharged release's fire power
        public int ChainLength;
        public bool FinalSecond;       // Final Second held: the perfect catch says "Last Second!"
    }

    /// <summary>
    /// The cue table and its tier rules (spec 4.1, 4.2). D1: a readable baseline, with
    /// spectacle only on rare moments, so Tier 0 (every hit) is a small sprite and nothing
    /// else, and only Tier 2 calls out or freezes. Every number is first-pass tuning (spec 9):
    /// retuning touches this file only.
    /// </summary>
    public static class FeedbackPolicy
    {
        /// <summary>No freeze ever exceeds this, however many requests arrive (spec 4.2.3).</summary>
        public const float HitStopCap = 0.15f;
        public const int MaxCallouts = 3;

        public static Cue For(CueEvent e, in CueContext c)
        {
            switch (e)
            {
                // ---- Tier 0: hits, endings, upgrade and skill moments. A sprite only.
                case CueEvent.EnemyHit: return Hit("Hit Spark", 0.8f, FeedbackColors.School(c.School));
                case CueEvent.PierceHit:
                {
                    // Pierce: a sharper spark plus a brief arc between the two enemies.
                    var cue = Hit("Pierce Spark", 0.9f, FeedbackColors.School(c.School));
                    cue.Sheet2 = "Chain Lightning"; cue.Size2 = 1f; cue.Color2 = FeedbackColors.Returned;
                    return cue;
                }
                case CueEvent.OrbitHit: return Hit("Shock Hit", 0.8f, FeedbackColors.Returned);
                case CueEvent.PartingGift:
                    // Sized to the gift's true reach on the floor, as the old ring pop was.
                    return new Cue { Sheet = "Zap Ring", Size = c.Radius * 2f, Color = FeedbackColors.Returned, Ground = true };
                case CueEvent.Overflow: return Sprite("Overload", 1.2f, FeedbackColors.Returned);
                case CueEvent.QuickDraw: return Sprite("Spark Burst", 0.8f, FeedbackColors.Riposte);
                case CueEvent.LifeStolen: return Sprite("Heal", 0.9f, FeedbackColors.LifeSteal);
                case CueEvent.ShotExpired: return Sprite("Small Pop", 0.6f, c.StateColor);
                case CueEvent.ShotHitWall: return Sprite("Block Spark", 0.7f, Color.white);
                case CueEvent.ShotCaptured: return Sprite("Star Burst", 0.6f, FeedbackColors.Returned);
                case CueEvent.Muzzle: return new Cue { Sheet = "Casting", Size = 0.8f, Color = Color.white, Cell = 100 };
                case CueEvent.DashReady: return Sprite("Notify Ping", 0.8f, Color.white);
                // The sweep's blade tip scuffing the floor. The dust sheets are drawn in their own
                // earth colours, so no tint.
                case CueEvent.SweepDust: return Sprite("Dirt Kick", 0.9f, Color.white);

                // ---- Tier 1: kills, getting hit, explosions, boss hits. Plus a small shake.
                case CueEvent.Kill:
                    return new Cue { Tier = 1, Sheet = "Smoke Burst", Size = 1.2f, Color = FeedbackColors.Smoke,
                        Sheet2 = "Weak Hit", Size2 = 0.8f, Color2 = Color.white, ShakeAmp = 0.05f, ShakeDuration = 0.10f };
                case CueEvent.PlayerHit:
                    // No hit-stop: it would delay the dodge the player needs next (spec 4.1).
                    return new Cue { Tier = 1, Sheet = "Heavy Hit", Size = 1.2f, Color = FeedbackColors.Danger, ShakeAmp = 0.12f, ShakeDuration = 0.20f };
                case CueEvent.Explosion:
                    return new Cue { Tier = 1, Sheet = "Blast", Size = c.Radius * 2f, Color = FeedbackColors.Rocket, ShakeAmp = 0.10f, ShakeDuration = 0.20f };
                case CueEvent.BossSlam:
                    // Playtest: the slam was drawn as a fiery Blast and read as an explosion. It is a
                    // blow to the ground: dust thrown out to the slam's true reach, dirt kicked up at
                    // the centre, and the heaviest Tier 1 shake, since the floor itself is hit. Tier 1,
                    // so no freeze: the player needs the next instant to get clear.
                    return new Cue { Tier = 1, Sheet = "Landing Dust", Size = c.Radius * 2f, Color = Color.white,
                        Sheet2 = "Dirt Kick", Size2 = 2f, Color2 = Color.white, ShakeAmp = 0.20f, ShakeDuration = 0.30f };
                case CueEvent.BossSweep:
                    // The swing starting: a short shove. The blade's dirt trail is SweepDust.
                    return new Cue { Tier = 1, ShakeAmp = 0.08f, ShakeDuration = 0.15f };
                case CueEvent.BossHit:
                    // The boss's Heavy Hit and hurt pose already live in WorldPresentation.
                    return new Cue { Tier = 1, ShakeAmp = 0.06f, ShakeDuration = 0.10f };

                // ---- Tier 2: rare moments. Callouts, freezes, impact frames.
                case CueEvent.Parry:
                    return Moment("Parry Flash", 1.4f, FeedbackColors.Riposte, "Parry!", 0.15f, 0.20f, 0.09f, true);
                case CueEvent.PerfectCatch:
                    return Moment("Critical Star", 1f, FeedbackColors.Returned, c.FinalSecond ? "Last Second!" : "Perfect!", 0f, 0f, 0.05f, true);
                case CueEvent.Overcharge:
                    // Keeps the multiplier ArenaView's old "OVERCHARGE xP" label showed (spec 4.1).
                    return Moment("Overload", 1.6f, FeedbackColors.Overcharge, $"Overcharge! x{c.Power:0.0}", 0.18f, 0.25f, 0.07f, true);
                case CueEvent.Backfire:
                    // 1.3, tuned from the capture: at 2 the boom covered the player whole, and the
                    // player is exactly who needs to be seen right after a backfire.
                    return Moment("Big Boom", 1.3f, FeedbackColors.Danger, "Backfire!", 0.20f, 0.25f, 0f, false);
                case CueEvent.Fusion:
                {
                    var cue = Moment("Star Burst", 1.2f, FeedbackColors.Fusion, "Fusion!", 0f, 0f, 0f, false);
                    cue.Sheet2 = "Zap Ring"; cue.Size2 = 1.6f; cue.Color2 = FeedbackColors.Fusion;
                    return cue;
                }
                case CueEvent.Chain:
                    // D4: rare only. Chains of 1 and 2 are everyday play and stay silent.
                    if (c.ChainLength < 3) return default;
                    return Moment("Star Burst", 1.2f, FeedbackColors.Chain, $"Chain x{c.ChainLength}!", 0f, 0f, 0f, false);
                case CueEvent.BossKill:
                    // The boss's death effects already exist; this adds the weight.
                    return new Cue { Tier = 2, Color = Color.white, ShakeAmp = 0.35f, ShakeDuration = 0.50f, HitStop = HitStopCap, ImpactFrame = true };
            }
            return default;
        }

        static Cue Sprite(string sheet, float size, Color color) => new Cue { Sheet = sheet, Size = size, Color = color };

        // Tier 0 enemy hits also get the visual-only recoil (D6).
        static Cue Hit(string sheet, float size, Color color) { var cue = Sprite(sheet, size, color); cue.Recoil = true; return cue; }

        static Cue Moment(string sheet, float size, Color color, string callout, float amp, float dur, float stop, bool impact) =>
            new Cue { Tier = 2, Sheet = sheet, Size = size, Color = color, Callout = callout, CalloutColor = color,
                ShakeAmp = amp, ShakeDuration = dur, HitStop = stop, ImpactFrame = impact };

        /// <summary>
        /// Reduce flashes (spec 4.2.5): motion and flashes go, while sprites and words stay, because
        /// those carry information and do not flash the whole screen.
        /// </summary>
        public static Cue Filter(Cue cue, bool reduceFlashes)
        {
            if (!reduceFlashes) return cue;
            cue.ShakeAmp = 0f; cue.ShakeDuration = 0f; cue.HitStop = 0f; cue.ImpactFrame = false;
            return cue;
        }

        /// <summary>
        /// Spec 4.2.3: a new freeze extends the current one only up to its own length (never
        /// sums), and no freeze passes the cap. Back-to-back moments read as one beat, never as a
        /// stall.
        /// </summary>
        public static double ExtendHitStop(double until, double now, float seconds) =>
            Math.Min(Math.Max(until, now + Math.Min(seconds, HitStopCap)), now + HitStopCap);
    }
}
