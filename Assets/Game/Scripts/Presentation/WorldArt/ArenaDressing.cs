using System;
using System.Collections.Generic;
using BorrowedHex.Data;
using UnityEngine;

namespace BorrowedHex.Presentation.WorldArt
{
    /// <summary>Where a dressing piece stands. Sides only appears in recipe rules (it expands
    /// to East and West); Light marks the holders under the eight flame anchors.</summary>
    public enum DressingBand { North, East, West, South, Inside, Sides, Light }

    public struct DressingPlacement
    {
        public string Model;
        public DressingBand Band;
        public Vector3 Position; // on the ground (y = 0); the model's pivot is bottom-centre
        public float Yaw;
        public float Height;     // target world height; the model is scaled uniformly to it
    }

    /// <summary>
    /// The per-theme enclosure recipes (spec 3.2/3.5) and their deterministic placement.
    /// Pure data and arithmetic so the height grade that hides the void is EditMode-testable.
    /// Every first-pass number here is the owner's to tune at the Task 12 sign-off.
    /// </summary>
    public static class ArenaDressing
    {
        public const float InsideLimit = .3f, SouthLimit = 1.2f, NorthMinimum = 6, RibbonThickness = 2;
        // Kenney models' front faces are assumed to point -Z after import; verify in the
        // Task 12 screenshots and flip to 0 if crypt doors face away (ledger it as a ruling).
        const float HeroYaw = 180;
        static readonly string[] Themes = { "Courtyard", "Graveyard", "Cave", "Sanctum" };

        sealed class Rule
        {
            public readonly DressingBand Band; public readonly bool Aligned; public readonly string[] Models;
            public readonly float Spacing, DepthMin, DepthMax, HeightMin, HeightMax;
            public Rule(DressingBand band, float spacing, float depthMin, float depthMax, float heightMin, float heightMax,
                bool aligned, params string[] models)
            {
                Band = band; Spacing = spacing; DepthMin = depthMin; DepthMax = depthMax;
                HeightMin = heightMin; HeightMax = heightMax; Aligned = aligned; Models = models;
            }
        }

        readonly struct Hero
        {
            public readonly string Model; public readonly float X, Depth, Height;
            public Hero(string model, float x, float depth, float height) { Model = model; X = x; Depth = depth; Height = height; }
        }

        public static string Known(string theme) => Array.IndexOf(Themes, theme) >= 0 ? theme : "Courtyard";

        // The north ribbon stands back far enough for the hero row to sit in front of it.
        public static float RibbonInset(DressingBand side) => side == DressingBand.North ? 5 : 1.2f;

        public static float RibbonHeight(Rect bounds, DressingBand side, float z)
            => side == DressingBand.North ? 7.5f : side == DressingBand.South ? 1f
                : Mathf.Lerp(2, 7, Mathf.InverseLerp(bounds.yMin, bounds.yMax, z));

        // Multiplied over the baked night colormap; a cold cast per theme.
        public static Color KitTint(string theme)
        {
            switch (Known(theme))
            {
                case "Graveyard": return new Color(.75f, .85f, .8f);
                case "Cave": return new Color(.7f, .7f, .95f);
                case "Sanctum": return new Color(.8f, .7f, .95f);
                default: return new Color(.8f, .8f, .9f);
            }
        }

        // The Sanctum's flame anchors are its obelisk tops, so it needs no holders.
        public static string LightModel(string theme)
        {
            switch (Known(theme))
            {
                case "Graveyard": return "lightpost-single";
                case "Cave": return WorldModelCatalog.Crystal;
                case "Sanctum": return null;
                default: return "fire-basket";
            }
        }

        static float LightHeight(string theme) => Known(theme) == "Graveyard" ? 2.2f : Known(theme) == "Cave" ? .9f : 1.1f;

        static string[] Series(string prefix, int count)
        { var names = new string[count]; for (int i = 0; i < count; i++) names[i] = prefix + (i + 1); return names; }

        static string[] Plus(string[] names, params string[] more)
        { var all = new string[names.Length + more.Length]; names.CopyTo(all, 0); more.CopyTo(all, names.Length); return all; }

        static Hero[] Heroes(string theme)
        {
            switch (theme)
            {
                case "Graveyard": return new[] { new Hero("crypt-large", 0, 3, 6.5f) };
                case "Cave": return new[] { new Hero("rocks-tall", 0, 2.5f, 7.5f) };
                case "Sanctum": return new[] { new Hero("crypt-large-door", 0, 4.2f, 6.5f), new Hero("altar-stone", 0, 1.8f, 1.6f) };
                default: return new[] { new Hero("column-large", -9, 1.6f, 6.5f), new Hero("column-large", 9, 1.6f, 6.5f) };
            }
        }

        // (band, spacing, depth min/max, height min/max, aligned to the band, models)
        static Rule[] Rules(string theme)
        {
            var deadTrees = Series("CommonTree_Dead_", 5); var rocks = Series("Rock_", 7); var mossRocks = Series("Rock_Moss_", 7);
            var gravestones = new[] { "gravestone-round", "gravestone-cross", "gravestone-decorative", "gravestone-broken" };
            switch (theme)
            {
                case "Graveyard":
                    return new[]
                    {
                        new Rule(DressingBand.North, 7, 1, 2, 4, 5, true, "crypt-a", "crypt-b"),
                        new Rule(DressingBand.North, 1.8f, .4f, .9f, .9f, 1.3f, false, gravestones),
                        new Rule(DressingBand.North, 5, 7.5f, 10, 8, 10, false, Plus(Series("Willow_Dead_", 5), "pine-crooked")),
                        new Rule(DressingBand.Sides, 2, .4f, .7f, 1, 1.4f, true, "iron-fence"),
                        new Rule(DressingBand.Sides, 1.7f, .8f, 1.1f, .8f, 1.3f, false, gravestones),
                        new Rule(DressingBand.Sides, 5, 3.5f, 8, 2.5f, 3.5f, false, "crypt-small"),
                        new Rule(DressingBand.Sides, 3.5f, 4, 15, 6, 9, false, deadTrees),
                        new Rule(DressingBand.South, 2.2f, .4f, .7f, .8f, 1.1f, true, "iron-fence-damaged"),
                        new Rule(DressingBand.South, 2.6f, .8f, 1.1f, .3f, .5f, true, "grave-border"),
                    };
                case "Cave":
                    return new[]
                    {
                        new Rule(DressingBand.North, 3, .5f, 3.5f, 4, 7, false, rocks),
                        new Rule(DressingBand.North, 4, 7.5f, 10, 8, 11, false, Plus(rocks, "rocks-tall")),
                        new Rule(DressingBand.Sides, 2.5f, .3f, 1, .8f, 1.8f, false, mossRocks),
                        new Rule(DressingBand.Sides, 3, 3.5f, 8, 4, 7, false, "rocks-tall"),
                        new Rule(DressingBand.Sides, 4, 8, 16, 5, 9, false, rocks),
                        new Rule(DressingBand.South, 2, .3f, 1, .5f, 1, false, Plus(mossRocks, "rocks")),
                    };
                case "Sanctum":
                    return new[]
                    {
                        new Rule(DressingBand.North, 3.5f, 1, 1.6f, 6, 7, false, "column-large"),
                        new Rule(DressingBand.North, 5, 7.5f, 10, 7, 9, false, "pillar-obelisk"),
                        new Rule(DressingBand.Sides, 4, .5f, 1, 3.5f, 6, false, "column-large"),
                        new Rule(DressingBand.Sides, 5, 3.5f, 7, 5, 8, false, "pillar-obelisk"),
                        new Rule(DressingBand.Sides, 3, 7, 15, 3, 5, true, "stone-wall", "brick-wall"),
                        new Rule(DressingBand.South, 3, .4f, .7f, .9f, 1.1f, false, "border-pillar"),
                        new Rule(DressingBand.South, 3, .8f, 1.1f, .3f, .5f, true, "grave-border"),
                        // Candles at the north wall's foot: the only pieces allowed inside the rect.
                        new Rule(DressingBand.Inside, 4, .3f, .4f, .2f, .28f, false, "candle-multiple"),
                    };
                default: // Courtyard: a ruined cloister
                    return new[]
                    {
                        new Rule(DressingBand.North, 2.4f, .6f, 1.4f, 3.2f, 4.2f, true, "stone-wall", "stone-wall-column", "stone-wall-damaged"),
                        new Rule(DressingBand.North, 6, 7.5f, 10, 8, 10.5f, false, "pine-crooked"),
                        new Rule(DressingBand.Sides, 3, .4f, 1, 1.4f, 2.4f, true, "stone-wall-damaged", "brick-wall"),
                        new Rule(DressingBand.Sides, 5, 3.5f, 6, 2.5f, 4, true, "brick-wall", "stone-wall"),
                        new Rule(DressingBand.Sides, 3.5f, 4, 15, 6, 9, false, deadTrees),
                        new Rule(DressingBand.South, 2.6f, .4f, .7f, .7f, 1.1f, true, "stone-wall-damaged"),
                        new Rule(DressingBand.South, 2.2f, .8f, 1.1f, .5f, .8f, true, "iron-fence-border"),
                    };
            }
        }

        public static List<DressingPlacement> Place(ArenaLayout arena)
        {
            string theme = Known(arena.worldTheme); var b = arena.bounds;
            var list = new List<DressingPlacement>();
            // Seeded per theme index: string.GetHashCode is not stable across runtimes.
            var random = new System.Random(1009 + Array.IndexOf(Themes, theme) * 7919);
            var keepClear = new List<(Vector3 at, float radius)>();
            string light = LightModel(theme);
            for (int i = 0; i < 8; i++)
            {
                var anchor = WorldGeometry.FlameAnchor(arena, i); var ground = new Vector3(anchor.x, 0, anchor.z);
                keepClear.Add((ground, 1.2f));
                if (light != null)
                    list.Add(new DressingPlacement { Model = light, Band = DressingBand.Light, Position = ground, Yaw = i * 45, Height = LightHeight(theme) });
            }
            foreach (var hero in Heroes(theme))
            {
                var at = new Vector3(b.center.x + hero.X, 0, b.yMax + hero.Depth);
                keepClear.Add((at, hero.Height > 4 ? 3.5f : 1.5f));
                list.Add(new DressingPlacement { Model = hero.Model, Band = DressingBand.North, Position = at, Yaw = HeroYaw, Height = hero.Height });
            }
            foreach (var rule in Rules(theme))
                if (rule.Band == DressingBand.Sides)
                { Walk(list, b, rule, DressingBand.East, random, keepClear); Walk(list, b, rule, DressingBand.West, random, keepClear); }
                else Walk(list, b, rule, rule.Band, random, keepClear);
            return list;
        }

        static void Walk(List<DressingPlacement> list, Rect b, Rule rule, DressingBand side, System.Random random,
            List<(Vector3 at, float radius)> keepClear)
        {
            bool alongX = side == DressingBand.North || side == DressingBand.South || side == DressingBand.Inside;
            // North and south rows overrun the corners so the diagonal corner views meet dressing.
            float start = side == DressingBand.Inside ? b.xMin + .5f : alongX ? b.xMin - (side == DressingBand.North ? 4 : 2) : b.yMin - 2;
            float end = side == DressingBand.Inside ? b.xMax - .5f : alongX ? b.xMax + (side == DressingBand.North ? 4 : 2) : b.yMax + 4;
            for (float s = start + rule.Spacing * .5f * Next(random); s <= end; s += rule.Spacing * (.75f + .5f * Next(random)))
            {
                // Every draw happens before any skip, so one skipped spot never reshuffles the rest.
                float depth = Mathf.Lerp(rule.DepthMin, rule.DepthMax, Next(random));
                float height = Mathf.Lerp(rule.HeightMin, rule.HeightMax, Next(random));
                float spin = Next(random), flip = Next(random);
                string model = rule.Models[random.Next(rule.Models.Length)];
                var at = side == DressingBand.North ? new Vector3(s, 0, b.yMax + depth)
                    : side == DressingBand.South ? new Vector3(s, 0, b.yMin - depth)
                    : side == DressingBand.East ? new Vector3(b.xMax + depth, 0, s)
                    : side == DressingBand.West ? new Vector3(b.xMin - depth, 0, s)
                    : new Vector3(s, 0, b.yMax - depth);
                // Side masses follow the §1 grade: low at the camera's end, tall at the north.
                if (side == DressingBand.East || side == DressingBand.West)
                    height *= Mathf.Lerp(.4f, 1, Mathf.InverseLerp(b.yMin, b.yMax, s));
                if (Blocked(at, keepClear)) continue;
                float yaw = rule.Aligned ? (alongX ? 0 : 90) + (flip < .5f ? 0 : 180) : spin * 360;
                list.Add(new DressingPlacement { Model = model, Band = side, Position = at, Yaw = yaw, Height = height });
            }
        }

        static float Next(System.Random random) => (float)random.NextDouble();

        static bool Blocked(Vector3 at, List<(Vector3 at, float radius)> keepClear)
        {
            foreach (var (centre, radius) in keepClear)
                if (new Vector2(at.x - centre.x, at.z - centre.z).sqrMagnitude < radius * radius) return true;
            return false;
        }
    }
}
