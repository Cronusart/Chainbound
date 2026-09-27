using System;

namespace Chainbound.Combat
{
    /// <summary>
    /// Seedable deterministic RNG for combat resolution.
    /// Uses System.Random internally for reproducibility.
    /// All combat randomness MUST go through this class.
    /// </summary>
    public class CombatRng
    {
        private Random _random;
        private int _seed;
        private int _callCount;

        /// <summary>The seed used to initialize this RNG.</summary>
        public int Seed => _seed;

        /// <summary>Number of random calls made (for replay verification).</summary>
        public int CallCount => _callCount;

        public CombatRng(int seed)
        {
            _seed = seed;
            _random = new Random(seed);
            _callCount = 0;
        }

        /// <summary>
        /// Reset to original seed (for replay from start).
        /// </summary>
        public void Reset()
        {
            _random = new Random(_seed);
            _callCount = 0;
        }

        /// <summary>
        /// Returns a random float in [0, 1).
        /// Used for hit checks, crit checks, trigger chances.
        /// </summary>
        public float NextFloat()
        {
            _callCount++;
            return (float)_random.NextDouble();
        }

        /// <summary>
        /// Returns a random float in [min, max).
        /// Used for damage roll (0.85â€“1.0 per 02C Â§3).
        /// </summary>
        public float NextFloat(float min, float max)
        {
            _callCount++;
            return min + (float)_random.NextDouble() * (max - min);
        }

        /// <summary>
        /// Returns a random int in [min, max] (inclusive on both ends).
        /// Used for multi-hit count, random stat selection, etc.
        /// </summary>
        public int NextInt(int min, int maxInclusive)
        {
            _callCount++;
            return _random.Next(min, maxInclusive + 1);
        }

        /// <summary>
        /// Returns a random int in [min, max] and exposes the normalized roll [0,1).
        /// </summary>
        public int NextInt(int min, int maxInclusive, out float roll)
        {
            _callCount++;
            roll = (float)_random.NextDouble();
            int range = (maxInclusive - min) + 1;
            int offset = (int)(roll * range);
            if (offset >= range) offset = range - 1;
            return min + offset;
        }

        /// <summary>
        /// Returns true with the given probability (0-1).
        /// </summary>
        public bool Check(float probability)
        {
            return NextFloat() < probability;
        }

        /// <summary>
        /// Returns true with the given probability and exposes the sampled roll.
        /// </summary>
        public bool Check(float probability, out float roll)
        {
            roll = NextFloat();
            return roll < probability;
        }
    }
}
