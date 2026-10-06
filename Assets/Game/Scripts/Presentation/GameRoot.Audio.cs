using BorrowedHex.Presentation.Audio;

namespace BorrowedHex.Presentation
{
    /// <summary>
    /// Audio wiring (Assets/SFX). The player lives on the GameRoot object; SimAudio is rebound
    /// to every new sim in BeginRun, and its state layers are driven from LateUpdate, which runs
    /// on every frame (Update returns early in menus and pauses, and music must still follow).
    /// </summary>
    public sealed partial class GameRoot
    {
        readonly SimAudio simAudio = new SimAudio();

        void InitAudio() => GameAudio.Ensure(gameObject);

        void BindAudio() => simAudio.Bind(Sim);

        void LateUpdate() =>
            simAudio.Tick(InMainMenu, kind == RunKind.Tutorial, kind == RunKind.Sandbox || kind == RunKind.EndlessDebug);
    }
}
