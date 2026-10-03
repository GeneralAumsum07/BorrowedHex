using UnityEngine;

namespace BorrowedHex.Presentation
{
    /// <summary>
    /// D94: a short, decaying positional shake on the camera, in unscaled time so it plays
    /// through hit-stop. Pure presentation: the sim never reads the camera.
    ///
    /// The shake is an OFFSET layered on whatever position the camera already has, never a
    /// stored "base" it snaps back to: the world-arena camera (WorldPresentation) rewrites the
    /// camera position every frame to follow the player, and a stored base would freeze that
    /// follow for the whole shake and then jump. Each frame the previous offset is removed only
    /// if nobody else moved the camera since it was applied; if the follow camera already wrote
    /// a fresh position, that position is the new base.
    ///
    /// Execution order 300 puts this LateUpdate AFTER WorldPresentation's (200). The base logic
    /// above survives either order, but the offset does not: run first and the follow camera
    /// overwrites it in the same frame, so nothing ever shakes (final review, Important 1;
    /// pinned by CameraShake_SurvivesTheWorldFollowCamera).
    /// </summary>
    [DefaultExecutionOrder(300)]
    public sealed class CameraShake : MonoBehaviour
    {
        float until, amplitude, duration;
        bool shaking;
        Vector3 lastOffset, lastApplied;

        public void Kick(float amp, float dur)
        {
            // A second kick during a shake keeps the stronger of the two, so back-to-back
            // perfect releases do not shrink the shake already playing.
            amplitude = Mathf.Max(amp, amplitude * Remaining01());
            duration = Mathf.Max(0.01f, dur);
            until = Time.unscaledTime + duration;
            shaking = true;
        }

        float Remaining01() => shaking ? Mathf.Clamp01((until - Time.unscaledTime) / duration) : 0f;

        void LateUpdate()
        {
            if (!shaking) return;
            var pos = transform.localPosition;
            // Unmoved since our last write -> strip our offset; moved -> the new position is the base.
            var basePos = pos == lastApplied ? pos - lastOffset : pos;
            float k = Remaining01();
            if (k <= 0f)
            {
                transform.localPosition = basePos;
                shaking = false; amplitude = 0f; lastOffset = Vector3.zero;
                return;
            }
            // Quadratic falloff: a hard kick that settles fast reads as impact, not as wobble.
            Vector2 j = Random.insideUnitCircle * amplitude * k * k;
            lastOffset = new Vector3(j.x, j.y, 0f);
            lastApplied = basePos + lastOffset;
            transform.localPosition = lastApplied;
        }
    }
}
