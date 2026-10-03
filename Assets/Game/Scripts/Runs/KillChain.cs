namespace BorrowedHex.Runs
{
    /// <summary>
    /// D95: consecutive kills inside a short window buy back escalating time. Pure C#, driven by
    /// the gameplay clock, so pauses and choices freeze the window. Several kills in one tick
    /// each count, so a rocket that wipes three enemies is a chain of three.
    /// </summary>
    public sealed class KillChain
    {
        readonly float window;
        readonly float[] bonus;

        public int Length { get; private set; }
        public int Best { get; private set; }
        public double ExpiresAt { get; private set; }

        public KillChain(float window, float[] bonusSeconds)
        {
            this.window = window;
            bonus = bonusSeconds ?? new float[0];
        }

        public bool IsActive(double now) => Length > 0 && now <= ExpiresAt + 1e-9;

        /// <summary>Count a kill at <paramref name="now"/>; returns the bonus seconds it earns.</summary>
        public float Register(double now)
        {
            Length = IsActive(now) ? Length + 1 : 1;
            ExpiresAt = now + window;
            if (Length > Best) Best = Length;
            return BonusFor(Length);
        }

        /// <summary>Bonus for the Nth kill of a chain; past the table, the last entry repeats.</summary>
        public float BonusFor(int length)
        {
            if (length <= 0 || bonus.Length == 0) return 0f;
            return bonus[System.Math.Min(length, bonus.Length) - 1];
        }
    }
}
