using UnityEngine;

namespace BorrowedHex.Presentation.WorldArt
{
    /// <summary>Travel has its own clock because combat is held throughout the cinematic.</summary>
    public sealed class WorldTravel
    {
        public float Elapsed { get; private set; }
        public float Duration { get; private set; }
        public bool Active { get; private set; }
        public bool Magical { get; private set; }
        public float Progress => Duration <= 0 ? 1 : Mathf.Clamp01(Elapsed / Duration);
        public float Blend => Mathf.SmoothStep(0, 1, Progress);
        public void Begin(bool magical) { Magical = magical; Duration = magical ? WorldIntroPolicy.Duration : 4.5f; Elapsed = 0; Active = true; }
        public void Reset() { Elapsed = 0; Duration = 0; Active = false; }
        public void Advance(float delta, bool held)
        {
            if (!Active || held || float.IsNaN(delta) || float.IsInfinity(delta) || delta <= 0) return;
            Elapsed = Mathf.Min(Duration, Elapsed + delta);
            if (Elapsed >= Duration) Active = false;
        }
    }
}
