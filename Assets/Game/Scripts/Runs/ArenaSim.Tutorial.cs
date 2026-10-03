namespace BorrowedHex.Runs
{
    /// <summary>
    /// Phase 14 wiring for the tutorial (D86). A tutorial run is a sandbox run (no director, never
    /// saved) with two rule changes, both keyed on RunSetup.Tutorial so no other mode is touched:
    /// the life clock does not drain, and hits still flash and grant invulnerability but cost no
    /// time. A first-time player can therefore never lose the tutorial while learning to catch.
    /// </summary>
    public sealed partial class ArenaSim
    {
        /// <summary>The lesson script, or null outside a tutorial run.</summary>
        public TutorialDirector Tutorial { get; private set; }

        void InitTutorial()
        {
            if (Setup.Tutorial) Tutorial = new TutorialDirector(this);
        }
    }
}
