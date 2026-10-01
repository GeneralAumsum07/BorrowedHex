using BorrowedHex.Core;
using BorrowedHex.Data;
using UnityEngine;

namespace BorrowedHex.Presentation
{
    /// <summary>
    /// Scene entry point for Arena.unity. Owns the gameplay clock and the current run ID.
    /// Phase 0 skeleton: later phases hang the simulation, views and UI off this object.
    /// </summary>
    public sealed class GameRoot : MonoBehaviour
    {
        public GameConfig config;

        public GameplayClock Clock { get; } = new GameplayClock();
        public string RunId { get; private set; }

        void Awake()
        {
            if (config == null) config = GameConfig.CreateDefault();
            BeginRun();
        }

        public void BeginRun()
        {
            Clock.Reset();
            RunId = RunIdFactory.Create();
        }

        void Update()
        {
            Clock.Advance(Time.deltaTime);
        }

        // Focus loss pauses combat on both targets (section 8). This is a pause reason of its
        // own so regaining focus never resumes a game the player had paused from the menu.
        void OnApplicationFocus(bool hasFocus) => Clock.SetPauseReason(PauseReason.FocusLost, !hasFocus);
        void OnApplicationPause(bool paused) { if (paused) Clock.SetPauseReason(PauseReason.FocusLost, true); }
    }
}
