using UnityEngine;

namespace BorrowedHex.Presentation
{
    /// <summary>Phase 12: the Endless menu entry and the debug-assisted endless session.</summary>
    public sealed partial class GameRoot
    {
        public void PlayEndless() => StartRun(RunKind.Endless);

        /// <summary>
        /// A debug-assisted endless run: marked Debug from the start (so it never submits), with
        /// the dev panel showing. Reached from the dev panel, which only shows in practice and
        /// debug runs, and only exists in the editor and development builds.
        /// </summary>
        public void PlayEndlessDebug() => StartRun(RunKind.EndlessDebug);

        partial void BuildEndlessMenus()
        {
            Main.EnableEntry("endless", PlayEndless);
            // Debug reward tools stay out of player builds (Phase 13 handoff rule).
            if (!Application.isEditor && !UnityEngine.Debug.isDebugBuild) return;
            Hud.AddDevButton("Endless (debug)", PlayEndlessDebug);
            // Ends the current wave at once. Usable only in an endless run; in a normal one it
            // would also turn the run debug (DebugSkipWave), but the panel is not shown there.
            Hud.AddDevButton("Skip wave", () => Sim.DebugSkipWave());
        }
    }
}
