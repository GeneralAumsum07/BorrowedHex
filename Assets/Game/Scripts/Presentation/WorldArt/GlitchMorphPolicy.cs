using BorrowedHex.Runs;
using UnityEngine;

namespace BorrowedHex.Presentation.WorldArt
{
    /// <summary>
    /// What one object (cover, rubble or dressing) shows at a moment of its patch's
    /// timeline. Fill: the solid share, rising from the base when forming and sinking from
    /// the top when retiring. Tear: the sideways slice jitter and flicker. Ghost: the
    /// shimmering non-solid preview of cover that cannot form yet.
    /// </summary>
    public readonly struct GlitchState
    {
        public readonly float Fill, Tear, Ghost;
        public GlitchState(float fill, float tear, float ghost) { Fill = fill; Tear = tear; Ghost = ghost; }
        public static readonly GlitchState Solid = new GlitchState(1, 0, 0), Gone = new GlitchState(0, 0, 0);
    }

    /// <summary>
    /// The per-object half of the reality-glitch morph (design Section 2). Pure functions of
    /// the sim's patch timings, so pausing freezes them and tests can sample them densely.
    /// Every output is continuous in time: nothing switches in one frame except at the
    /// very start of a patch, where the object is either fully old or fully absent.
    /// </summary>
    public static class GlitchMorphPolicy
    {
        // A cover piece that waited for its footprint to clear fills in over this long.
        public const float LateFill = MorphField.LateFill;

        static float Ease(float t) { t = Mathf.Clamp01(t); return t * t * (3 - 2 * t); }

        // Tear rises over the first 0.15 s of the unstable phase and fades out by the end of
        // growth, so it never pops on or off.
        static float TearAt(float age)
            => Mathf.Clamp01(age / .15f) * (1 - Ease((age - MorphField.Unstable) / (MorphField.Grow * .8f)));

        /// <summary>Incoming dressing: no collision, so no waiting.</summary>
        public static GlitchState Forming(MorphField field, int patch, double now)
        {
            if (field == null || patch < 0) return GlitchState.Solid;
            if (!field.IsTriggered(patch)) return GlitchState.Gone;
            float age = (float)(now - field.StartedAt(patch));
            if (age >= MorphField.Duration) return GlitchState.Solid;
            // Unstable: a ghost of what is coming flickers in; growth then fills it from the base.
            float grow = Ease((age - MorphField.Unstable) / MorphField.Grow);
            float ghost = Mathf.Clamp01(age / .15f) * (1 - grow);
            return new GlitchState(grow, TearAt(age), ghost);
        }

        // Fill is capped just short of solid until the sim forms the cover, so it never looks
        // solid ahead of its collision.
        const float Uncollided = .97f;

        static GlitchState Lerp(GlitchState a, GlitchState b, float t)
            => new GlitchState(Mathf.Lerp(a.Fill, b.Fill, t), Mathf.Lerp(a.Tear, b.Tear, t), Mathf.Lerp(a.Ghost, b.Ghost, t));

        // Patch finished but someone stands in the footprint: the almost-solid cover sinks
        // back into a torn ghost over WaitFade instead of flipping to it in one frame. With
        // no field left (the morph was released) the fade is long over.
        static GlitchState Waiting(MorphField field, int patch, double now)
        {
            float w = field == null || patch < 0 || !field.IsTriggered(patch) ? 1
                : Ease((float)((now - field.StartedAt(patch) - MorphField.Duration) / MorphField.WaitFade));
            return Lerp(new GlitchState(Uncollided, 0, 0), new GlitchState(0, .4f, 1), w);
        }

        /// <summary>Incoming cover: as dressing, but it only turns solid once the sim forms it.</summary>
        public static GlitchState FormingCover(MorphField field, int patch, bool formed, bool late, double formedAt, double now)
        {
            if (formed)
            {
                // Formed on schedule: the patch growth already filled it. Formed late: fill in
                // from wherever the waiting fade had got to at the moment it formed.
                if (!late) return GlitchState.Solid;
                return Lerp(Waiting(field, patch, formedAt), GlitchState.Solid, Ease((float)((now - formedAt) / LateFill)));
            }
            if (field == null || field.Complete(patch, now)) return Waiting(field, patch, now);
            var state = Forming(field, patch, now);
            return new GlitchState(Mathf.Min(state.Fill, Uncollided), state.Tear, state.Ghost);
        }

        /// <summary>Outgoing cover, rubble and dressing.</summary>
        public static GlitchState Retiring(MorphField field, int patch, double now)
        {
            if (field == null) return GlitchState.Gone;
            if (patch < 0 || !field.IsTriggered(patch)) return GlitchState.Solid;
            float age = (float)(now - field.StartedAt(patch));
            if (age >= MorphField.Duration) return GlitchState.Gone;
            return new GlitchState(1 - Ease((age - MorphField.Unstable) / MorphField.Grow), TearAt(age), 0);
        }
    }

    /// <summary>
    /// Uploads the patch field as shader globals once per frame; PaintedWorld surfaces
    /// (floor, outer ground, ribbon) resolve their patch per pixel from these.
    /// </summary>
    public static class GlitchField
    {
        // Fixed size: Unity locks a global array's length the first time it is set.
        public const int MaxPatches = 32;
        // Untriggered patches start "in the far future", so the shader shows the old world.
        const float Never = 1e7f;
        static readonly Vector4[] seeds = new Vector4[MaxPatches];

        public static void Upload(MorphField field, double now, bool reduceFlashes, Color seam)
        {
            Shader.SetGlobalFloat("_WorldTime", (float)now);
            Shader.SetGlobalFloat("_GlitchStrength", reduceFlashes ? 0 : 1);
            Shader.SetGlobalColor("_SeamColor", seam);
            Shader.SetGlobalFloat("_PatchActive", field != null ? 1 : 0);
            if (field == null) return;
            Shader.SetGlobalVector("_PatchGrid", new Vector4(field.Origin.x, field.Origin.y, field.Cell.x, field.Cell.y));
            Shader.SetGlobalVector("_PatchDims", new Vector4(MorphField.Columns, MorphField.Rows, MorphField.Unstable, MorphField.Grow));
            for (int i = 0; i < MaxPatches; i++)
            {
                if (i >= MorphField.Count) { seeds[i] = new Vector4(0, 0, Never, 0); continue; }
                var seed = field.Seed(i);
                seeds[i] = new Vector4(seed.x, seed.y, field.IsTriggered(i) ? (float)field.StartedAt(i) : Never, 0);
            }
            Shader.SetGlobalVectorArray("_PatchSeeds", seeds);
        }

        /// <summary>Leaves every PaintedWorld surface showing its own world (scene teardown).</summary>
        public static void Clear() => Shader.SetGlobalFloat("_PatchActive", 0);
    }
}
