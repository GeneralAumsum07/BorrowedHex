namespace BorrowedHex.Presentation.Feedback
{
    /// <summary>
    /// What feedback needs from whoever owns the frame loop and the camera. GameRoot implements
    /// it; tests pass a recorder. Feedback never touches the sim clock: hit-stop is the host
    /// choosing not to step.
    /// </summary>
    public interface IFeedbackHost
    {
        void HitStop(float seconds);
        void Shake(float amp, float duration);
    }
}
