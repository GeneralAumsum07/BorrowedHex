namespace BorrowedHex.Presentation.WorldArt
{
    public static class WorldBoundaryPolicy
    {
        // The combat footprint stays safe while its enclosing architecture is consumed.
        // Low remnants still mark the arena edge when no upright walls remain.
        public static float Height(int stage, int segment)
        {
            if (stage == 0 || stage == 3) return 2.8f;
            if (stage == 2) return .12f;
            return segment % 3 == 1 ? 0 : segment % 4 == 0 ? .65f : 1.7f;
        }
    }
}
