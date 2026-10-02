using System.Collections.Generic;
using BorrowedHex.Data;
using BorrowedHex.Player;

namespace BorrowedHex.Progression
{
    /// <summary>One selectable capture style (section 7). Data only; the sim never sees this type.</summary>
    public sealed class CaptureStyle
    {
        public string Id;
        public string Name;
        public string Summary;
        public string TradeOff;
    }

    /// <summary>
    /// The three capture styles. A style rewrites the BASE catch numbers before any passive is
    /// applied (Loadout), so e.g. Precision's +15 degrees widens Collector's 140 to 155 rather
    /// than being overwritten by it. Everything outside the catch itself (two slots, the 3.0 s
    /// lifetime, the frozen-unselected rule, the backfire) is never touched here, which is how
    /// every style keeps those rules (section 7) by construction rather than by care.
    /// </summary>
    public static class CaptureStyles
    {
        public const string Snatcher = "snatcher";
        public const string Collector = "collector";
        public const string Daredevil = "daredevil";

        public static readonly IReadOnlyList<CaptureStyle> All = new[]
        {
            new CaptureStyle { Id = Snatcher, Name = "Snatcher", Summary = "The baseline catch cone, range, window and recovery.", TradeOff = "Balanced, precise" },
            new CaptureStyle { Id = Collector, Name = "Collector", Summary = "A much wider cone held open longer, with a longer recovery.", TradeOff = "Broad, more commitment" },
            new CaptureStyle { Id = Daredevil, Name = "Daredevil", Summary = "The catch is a dash that captures along its path. It shares the dash cooldown.", TradeOff = "Aggressive, tied to dash availability" },
        };

        public static bool IsKnown(string id)
        {
            foreach (var s in All) if (s.Id == id) return true;
            return false;
        }

        /// <summary>Section 7: unknown (or empty) ids fall back to Snatcher, never to an error.</summary>
        public static CaptureStyle Resolve(string id)
        {
            foreach (var s in All) if (s.Id == id) return s;
            return All[0];
        }

        /// <summary>Overwrite the catch fields of baseline stats with the style's own (called before passives).</summary>
        public static void Apply(PlayerStats s, string id, StyleTuning t)
        {
            t ??= new StyleTuning();
            switch (Resolve(id).Id)
            {
                case Collector:
                    s.CaptureConeAngle = t.collectorConeAngle;
                    s.CaptureWindow = t.collectorWindow;
                    s.CaptureRecovery = t.collectorRecovery;
                    break;
                case Daredevil:
                    s.CatchIsDash = true;
                    s.DashCatchRadius = t.daredevilCatchRadius;
                    // The window is the dash itself; ArenaSim opens it with the dash's own
                    // duration, so these two only matter for the parry share and the display.
                    s.CaptureWindow = s.DashDuration;
                    // Zero, so the dash cooldown is the single gate (they are shared, section 7).
                    // CaptureController still holds recovery to at least the window.
                    s.CaptureRecovery = 0f;
                    break;
                // Snatcher: the baseline as loaded from CaptureTuning, unchanged.
            }
        }

        /// <summary>
        /// The effective numbers the style panel shows before a run, from the stats the run would
        /// actually get (style AND equipped passives), so the screen can never disagree with play.
        /// </summary>
        public static string Describe(PlayerStats s)
        {
            if (s.CatchIsDash)
                return $"Catch: a {s.DashDistance:0.0} m dash, capturing within {s.DashCatchRadius:0.0} m of your path\n" +
                       $"Window: the dash ({s.DashDuration:0.00} s)   Shared cooldown: {s.DashCooldown:0.00} s\n" +
                       $"Packet capacity: {s.PacketCapacity}";
            return $"Cone: {s.CaptureConeAngle:0}°   Range: {s.CaptureRange:0.0} m\n" +
                   $"Window: {s.CaptureWindow:0.00} s   Recovery: {System.Math.Max(s.CaptureRecovery, s.CaptureWindow):0.00} s\n" +
                   $"Packet capacity: {s.PacketCapacity}";
        }
    }
}
