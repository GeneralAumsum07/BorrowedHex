using System;

namespace BorrowedHex.Core
{
    /// <summary>
    /// Monotonic integer IDs scoped to one run (actors, shots, packets, releases).
    /// Integers rather than object references so packets and damage records stay valid
    /// after the enemy or projectile that produced them has been destroyed or pooled.
    /// </summary>
    public sealed class IdGenerator
    {
        int last;
        public int Next() => ++last;
        public void Reset() => last = 0;
    }

    public static class RunIdFactory
    {
        // A GUID per run: progression finalization is keyed on this, so it must never
        // repeat across restarts or relaunches (the profile remembers finalized IDs).
        public static string Create() => Guid.NewGuid().ToString("N");
    }

    /// <summary>
    /// Small deterministic PRNG (xorshift32 seeded through splitmix). Implemented here rather
    /// than using System.Random so seeded offers/formations reproduce identically on the
    /// Windows and Web runtimes regardless of the BCL implementation behind them.
    /// </summary>
    public sealed class SeededRandom
    {
        uint state;

        public SeededRandom(int seed)
        {
            // splitmix-style scramble so small consecutive seeds give unrelated streams;
            // xorshift's state must also never be zero.
            uint z = unchecked((uint)seed + 0x9E3779B9u);
            z = unchecked((z ^ (z >> 16)) * 0x85EBCA6Bu);
            z = unchecked((z ^ (z >> 13)) * 0xC2B2AE35u);
            z ^= z >> 16;
            state = z == 0 ? 0x6D2B79F5u : z;
        }

        public uint NextUInt()
        {
            uint x = state;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            state = x;
            return x;
        }

        /// <summary>Uniform integer in [minInclusive, maxExclusive).</summary>
        public int NextInt(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive) return minInclusive;
            uint range = (uint)(maxExclusive - minInclusive);
            return minInclusive + (int)(NextUInt() % range);
        }

        /// <summary>Uniform float in [0, 1).</summary>
        public float NextFloat() => (NextUInt() >> 8) * (1f / 16777216f);

        public float Range(float min, float max) => min + (max - min) * NextFloat();
    }
}
