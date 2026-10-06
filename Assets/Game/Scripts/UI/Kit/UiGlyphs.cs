using System.Collections.Generic;
using UnityEngine;

namespace BorrowedHex.UI
{
    /// <summary>
    /// Code-drawn 12x12 icons (plan 0.5): skills, branches, upgrades, payloads, slot states and
    /// the capture-style emblems. Same string-bitmap idea as PixelSprites, with one shared gold
    /// ramp so they read as one set. The caller tints them for meaning (Life red, frozen blue):
    /// the ramp's light end is near white, so a multiply tint carries the colour cleanly.
    /// A licensed icon pack could later replace any id here without touching a screen.
    ///
    /// PPU 25 against the canvas's 100: one art pixel is 4 reference pixels, so a glyph is
    /// 48x48 on screen and stays on the integer grid at every Interface scale step.
    /// </summary>
    public static class UiGlyphs
    {
        // '.' clear, 'd' outline (the panel's darkest ink, so a glyph reads on gold frames),
        // 'm' Camel, 'h' Honey, 'w' ivory sparkle. Light falls from the top left.
        static readonly Dictionary<char, Color32> Ramp = new Dictionary<char, Color32>
        {
            ['.'] = new Color32(0, 0, 0, 0), ['d'] = new Color32(0x15, 0x12, 0x1C, 255),
            ['m'] = new Color32(0xC1, 0x91, 0x49, 255), ['h'] = new Color32(0xDC, 0xC4, 0x7C, 255),
            ['w'] = new Color32(0xF2, 0xE8, 0xC9, 255),
        };

        // Row 0 is the top of the drawing. Grouped as the brief's table: branches, skills,
        // upgrades, payloads, slot states, style emblems.
        internal static readonly Dictionary<string, string[]> Bitmaps = new Dictionary<string, string[]>
        {
            // ---- Branches -------------------------------------------------------------------
            ["branch.precision"] = new[]   // an open hand, fingers splayed: the grasp
            {
                "..d.d.d.....",
                ".dmdmdmd....",
                ".dmdmdmd.d..",
                ".dmdmdmddmd.",
                ".dmhmhmhdmd.",
                ".dmhhhhhmmd.",
                ".dmhwwhhmd..",
                ".dmhhhhhmd..",
                "..dmhhhmd...",
                "..dmmmmmd...",
                "...ddddd....",
                "............",
            },
            ["branch.mobility"] = new[]   // a winged boot
            {
                "............",
                ".....dddd...",
                "....dmhhd...",
                ".d..dmhhd...",
                "dwd.dmhhd...",
                "dhwddmhhd...",
                ".dhwdmhhdd..",
                "..ddmhhhhmd.",
                "...dmhhhhhmd",
                "...dmmmmmmmd",
                "...ddddddddd",
                "............",
            },
            ["branch.resilience"] = new[]   // a shield with a vertical rune
            {
                ".dddddddddd.",
                ".dhhhhhhhmd.",
                ".dhwhddhhmd.",
                ".dhhhdwdhmd.",
                ".dhhhdwdhmd.",
                ".dmhhdwdmmd.",
                ".dmhhdwdmmd.",
                "..dmhddhmd..",
                "..dmmhhmmd..",
                "...dmmmmd...",
                "....dmmd....",
                ".....dd.....",
            },
            ["branch.blood"] = new[]   // a drop over a coin: the price side
            {
                ".....dd.....",
                "....dhmd....",
                "...dhwmmd...",
                "...dhmmmd...",
                "....dmmd....",
                ".....dd.....",
                "..dddddddd..",
                ".dhhhhhhmmd.",
                "dhwhmmmmhmmd",
                ".dmhhhhhhmd.",
                "..dddddddd..",
                "............",
            },

            // ---- Skills (SkillTree node ids) -----------------------------------------------
            ["skill.precision_angle"] = new[]   // Wide Grasp: two edges spreading from a point
            {
                ".........ddd",
                ".......ddhhd",
                ".....ddhhd..",
                "...ddhhd....",
                ".ddhmd......",
                "dwhmd.......",
                ".ddhmd......",
                "...ddhhd....",
                ".....ddhhd..",
                ".......ddhhd",
                ".........ddd",
                "............",
            },
            ["skill.precision_capacity"] = new[]   // Deep Pockets: a pouch under three stars
            {
                ".h...h...h..",
                "hwh.hwh.hwh.",
                ".h...h...h..",
                "....dddd....",
                "...dmhhmd...",
                "....dmmd....",
                "..dmhhhhmd..",
                ".dmhwhhhhmd.",
                ".dmhhhhhhmd.",
                ".dmmhhhhmmd.",
                "..dmmmmmmd..",
                "...dddddd...",
            },
            ["skill.quick_draw"] = new[]   // a fist with motion streaks behind it
            {
                "............",
                "....dddd....",
                "...dhhhhd...",
                "dd.dhwhhdd..",
                "...dhhhhhmd.",
                "ddddhhhhhmd.",
                "...dhhhhhmd.",
                "dd.dmhhhmd..",
                "....dmmmd...",
                "....dmmd....",
                ".....dd.....",
                "............",
            },
            ["skill.mobility_speed"] = new[]   // Light Feet: a feather
            {
                "..........dd",
                "........ddhd",
                ".......dhwd.",
                "......dhwhd.",
                ".....dhwhd..",
                "....dhwhmd..",
                "...dhwhmd...",
                "..dhhhmd....",
                "..dhmmd.....",
                ".d.ddd......",
                "d...........",
                "............",
            },
            ["skill.mobility_dash_recovery"] = new[]   // Quick Recovery: a circular arrow
            {
                "....dddd....",
                "..ddhhhhdd..",
                ".dhhd..dhhd.",
                ".dhd....dd..",
                "dhd.........",
                "dhd.....d...",
                "dhd....dhd..",
                ".dhd..dhhhd.",
                ".dhhddhhhhhd",
                "..ddhhhhdd..",
                "....dddd....",
                "............",
            },
            ["skill.mobility_dash_distance"] = new[]   // Long Stride: footprints far apart
            {
                ".........dd.",
                "........dhhd",
                "........dhmd",
                ".........dd.",
                ".....dd.....",
                "....dhhd....",
                "....dhmd....",
                ".....dd.....",
                ".dd.........",
                "dhhd........",
                "dhmd........",
                ".dd.........",
            },
            ["skill.resilience_grace"] = new[]   // Steady Nerves: an upright candle flame
            {
                ".....dd.....",
                "....dwhd....",
                "....dwhd....",
                "...dhwhmd...",
                "...dhwhmd...",
                "....dhmd....",
                ".....dd.....",
                "...dddddd...",
                "...dhhhmd...",
                "...dhhhmd...",
                "...dhhhmd...",
                "..dddddddd..",
            },
            ["skill.resilience_time"] = new[]   // Borrowed Hours: an hourglass
            {
                ".dddddddddd.",
                ".dmhhhhhhmd.",
                "..dwwwwwwd..",
                "...dhwwhd...",
                "....dhhd....",
                ".....dd.....",
                "....dmmd....",
                "...dm..md...",
                "..dm.hh.md..",
                ".dmhhwwhhmd.",
                ".dddddddddd.",
                "............",
            },
            ["skill.resilience_dash_grace"] = new[]   // Slippery: a drop sliding off a shield
            {
                ".dddddddd...",
                ".dhhhhhmd...",
                ".dhwhhhmd...",
                ".dhhhhhmd.d.",
                ".dmhhhmmddhd",
                "..dmhhmd.dwd",
                "..dmhhmd..d.",
                "...dmmd.....",
                "....dd......",
                "............",
                "............",
                "............",
            },
            ["skill.blood_leech"] = new[]   // Leech: a fang
            {
                "............",
                ".dddddddddd.",
                ".dhhhhhhhmd.",
                ".dmhhhhhmmd.",
                "..ddhwhddd..",
                "...dhwhd....",
                "...dhwmd....",
                "....dhmd....",
                "....dhmd....",
                ".....dhd....",
                "......d.....",
                "............",
            },
            ["skill.blood_siphon"] = new[]   // Siphon: a chalice
            {
                ".dddddddddd.",
                ".dhwhhhhhmd.",
                ".dhhhhhhhmd.",
                "..dhhhhhmd..",
                "...dmhhmd...",
                "....dmmd....",
                ".....dd.....",
                ".....dd.....",
                "....dmmd....",
                "...dhhhmd...",
                "..dddddddd..",
                "............",
            },
            ["skill.blood_debt"] = new[]   // Blood Debt: a quill over a ledger line
            {
                "..........dd",
                "........ddwd",
                ".......dhwd.",
                "......dhwd..",
                ".....dhwd...",
                "....dhmd....",
                "...dhmd.....",
                "...ddd......",
                "..d.........",
                "dddddddddddd",
                "dmmmmmmmmmmd",
                "dddddddddddd",
            },

            // ---- Encounter upgrades (UpgradeId names) --------------------------------------
            ["upgrade.PiercingReturn"] = new[]   // an arrow through a ring
            {
                "....dddd....",
                "...dhhhhd...",
                "..dhd..dhd..",
                "..dd....dd..",
                "dddddddddd..",
                "dwwwwwwwwwd.",
                "dddddddddd..",
                "..dd....dd..",
                "..dhd..dhd..",
                "...dhhhhd...",
                "....dddd....",
                "............",
            },
            ["upgrade.EchoVolley"] = new[]   // three stacked chevrons, fading
            {
                ".....dd.....",
                "....dwhd....",
                "...dhddhd...",
                "..dhd..dhd..",
                ".....dd.....",
                "....dhmd....",
                "...dhddmd...",
                "..dmd..dmd..",
                ".....dd.....",
                "....dmmd....",
                "...dmddmd...",
                "..dmd..dmd..",
            },
            ["upgrade.HeavyOrbit"] = new[]   // a ball riding an orbit ellipse (the ring in Camel: an outline-only ring vanished on the dark panel)
            {
                "............",
                ".......ddd..",
                "......dhwhd.",
                "..mmmmdhhhd.",
                ".mm....dmd..",
                "mm.......mm.",
                "m.........m.",
                "mm.......mm.",
                ".mm.....mm..",
                "..mmmmmmm...",
                "............",
                "............",
            },
            ["upgrade.PartingGift"] = new[]   // an opened box with a spark rising
            {
                ".....h......",
                "...h.w.h....",
                "....hwh.....",
                ".d..hh...d..",
                "dhd.....dhd.",
                ".dhd...dhd..",
                ".dddddddddd.",
                ".dhhhhhhhmd.",
                ".dmhhwhhhmd.",
                ".dmhhhhhmmd.",
                ".dmmmmmmmmd.",
                ".dddddddddd.",
            },
            ["upgrade.FinalSecond"] = new[]   // an emptied hourglass with one grain left (glass in Camel, as HeavyOrbit)
            {
                ".dddddddddd.",
                ".dmhhhhhhmd.",
                "..m......m..",
                "...m....m...",
                "....m..m....",
                ".....mm.....",
                ".....mm.....",
                "....mhwm....",
                "...m.hh.m...",
                "..m......m..",
                ".dmhhhhhhmd.",
                ".dddddddddd.",
            },
            ["upgrade.Overflow"] = new[]   // a cup spilling over its rim
            {
                "...hw.......",
                "..hwwh......",
                ".dddddddd...",
                ".dhwhhhhdh..",
                ".dhhhhhhdwh.",
                ".dmhhhhmd.h.",
                "..dmhhmd..h.",
                "...dmmd.....",
                "....dd....h.",
                "....dd......",
                "...dmmd.....",
                "..dddddd....",
            },
            ["upgrade.Fusion"] = new[]   // two circles merging
            {
                "............",
                "..ddd..ddd..",
                ".dhhhddmmmd.",
                "dhwhdhhdmmmd",
                "dhhdwhhhdmmd",
                "dhhdhhhhdmmd",
                "dhhdhhhhdmmd",
                "dmhhdhhdmmmd",
                ".dmmmddmmmd.",
                "..ddd..ddd..",
                "............",
                "............",
            },

            // ---- Payloads ------------------------------------------------------------------
            ["payload.bolt"] = new[]   // a diagonal bolt
            {
                "..........dd",
                ".........dwd",
                "........dwd.",
                ".......dwd..",
                "......dhd...",
                ".....dhd....",
                "....dhd.....",
                "...dmd......",
                "..dmd.......",
                ".dmmd.......",
                "dmmd........",
                "ddd.........",
            },
            ["payload.riposte"] = new[]   // crossed blades
            {
                "dd........dd",
                "dwd......dwd",
                ".dwd....dwd.",
                "..dwd..dwd..",
                "...dhddhd...",
                "....dhhd....",
                "....dhhd....",
                "...dhddhd...",
                ".ddmd..dmdd.",
                "dmdd....ddmd",
                "dd........dd",
                "............",
            },
            ["payload.rocket"] = new[]   // a shell with a flame tail
            {
                ".........dd.",
                "........dwhd",
                ".......dwhhd",
                "......dwhhd.",
                ".....dhhhd..",
                "....dhhmd...",
                "..ddhmmd....",
                ".dmhdmd.....",
                "..h.dd......",
                ".hwh........",
                "hwh.........",
                ".h..........",
            },
            // Added in plan Task 14: AttackKind has four kinds and the brief's table drew three.
            // A heavy shot shown with the bolt glyph would read as two "bolt" entries in one slot.
            ["payload.heavy"] = new[]   // a cannonball, lit from the top left like the rest
            {
                "............",
                "....dddd....",
                "..ddwhhmdd..",
                ".dwwhhhmmmd.",
                ".dwhhhhmmmd.",
                "dhhhhhmmmmmd",
                "dhhhhmmmmmmd",
                "dhhhmmmmmmmd",
                ".dmmmmmmmmd.",
                ".dmmmmmmmmd.",
                "..ddmmmmdd..",
                "....dddd....",
            },

            // ---- Slot states ---------------------------------------------------------------
            ["state.frozen"] = new[]   // a snowflake (the caller tints it ice blue)
            {
                ".....dd.....",
                "..d..dw..d..",
                "..dd.dw.dd..",
                "...dddwdd...",
                ".....dw.....",
                "dddddwwddddd",
                ".wwwwwwwwwd.",
                ".....dw.....",
                "...dddwdd...",
                "..dd.dw.dd..",
                "..d..dw..d..",
                ".....dd.....",
            },
            ["state.unstable"] = new[]   // a jagged crack
            {
                ".....dd.....",
                "....dwd.....",
                "....dhd.....",
                "...dhd......",
                "...dhdd.....",
                "....dhhd....",
                ".....dhd....",
                "....dhd.....",
                "...dmd......",
                "...dmdd.....",
                "....dmd.....",
                ".....d......",
            },
            ["state.fused"] = new[]   // a chain link
            {
                "............",
                ".dddd.......",
                "dhhhhd......",
                "dhd.dhd.....",
                "dhd..dddd...",
                ".dhddhhhhd..",
                "..ddhhd.dhd.",
                "...dhd..dhd.",
                "....dddhhhd.",
                ".......dhhd.",
                "........dd..",
                "............",
            },
            ["state.locked"] = new[]
            {
                "....dddd....",
                "...dmhhmd...",
                "..dmd..dmd..",
                "..dmd..dmd..",
                ".dddddddddd.",
                ".dhwwwwwwhd.",
                ".dhhhddhhhd.",
                ".dmhhddhhmd.",
                ".dmmhddhmmd.",
                ".dmmmmmmmmd.",
                ".dddddddddd.",
                "............",
            },
            ["state.overcharge"] = new[]   // a crown
            {
                "............",
                "d....dd....d",
                "dd..dwhd..dd",
                "dhd.dhhd.dhd",
                "dhhddhhddhhd",
                "dhwhhhhhhhmd",
                "dhhhhhhhhhmd",
                "dmhwhhhwhmmd",
                "dmmmmmmmmmmd",
                "dddddddddddd",
                "............",
                "............",
            },
            ["state.check"] = new[]
            {
                "............",
                "..........dd",
                ".........dwd",
                "........dwhd",
                ".dd....dwhd.",
                "dwwd..dwhd..",
                "dhwwddwhd...",
                ".dhwwwhd....",
                "..dhwhd.....",
                "...dhd......",
                "....d.......",
                "............",
            },

            // ---- Capture-style emblems (CaptureStyles ids) ---------------------------------
            ["style.snatcher"] = new[]   // a grabbing claw
            {
                ".d..d..d....",
                "dhddhddhd...",
                "dhddhddhd.d.",
                "dhddhddhddhd",
                "dhhhhhhhddhd",
                "dhwhhhhhhhd.",
                "dhhhhhhhhmd.",
                ".dhhhhhhmd..",
                "..dmhhhmd...",
                "...dmmmd....",
                "...dmmmd....",
                "...ddddd....",
            },
            ["style.collector"] = new[]   // an open ledger
            {
                "............",
                ".ddddd.ddddd",
                "dwhhhhdhhhhd",
                "dhdddhdhdddd",
                "dhhhhhdhhhhd",
                "dhdddhdhdddd",
                "dhhhhhdhhhmd",
                "dhhhhhdhhhmd",
                "dmmmmmdmmmmd",
                ".dddddddddd.",
                "............",
                "............",
            },
            ["style.daredevil"] = new[]   // a blade over a wing
            {
                "..........dd",
                ".........dwd",
                "........dwd.",
                ".d.....dwd..",
                "dhd...dwd...",
                "dhhd.dwd....",
                "dhhhdwd.....",
                ".dhhddd.....",
                ".ddhdhdd....",
                "..ddd.dmd...",
                ".......dd...",
                "............",
            },
        };

        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

        public static IEnumerable<string> Ids => Bitmaps.Keys;

        /// <summary>The glyph's sprite, built once and cached; null for an unknown id.</summary>
        public static Sprite Get(string id)
        {
            if (id == null || !Bitmaps.TryGetValue(id, out var rows) || rows == null) return null;
            // A cached sprite can be destroyed by a domain or scene teardown; rebuild then.
            if (cache.TryGetValue(id, out var s) && s != null) return s;
            var t = new Texture2D(12, 12, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            for (int y = 0; y < 12; y++)
                for (int x = 0; x < 12; x++)
                    t.SetPixel(x, 11 - y, Ramp[rows[y][x]]);   // row 0 is the top of the drawing
            t.Apply(false);
            return cache[id] = Sprite.Create(t, new Rect(0, 0, 12, 12), new Vector2(0.5f, 0.5f), 25f);
        }
    }
}
