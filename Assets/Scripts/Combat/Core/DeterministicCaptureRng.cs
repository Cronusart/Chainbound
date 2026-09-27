using System;

namespace Chainbound.Combat.Core
{
    /// <summary>Xorshift32 with an explicitly serializable state.</summary>
    public struct DeterministicCaptureRng
    {
        private uint state;

        public DeterministicCaptureRng(uint seed)
        {
            state = seed == 0 ? 0x9E3779B9u : seed;
        }

        public uint State => state;

        public uint NextUInt()
        {
            uint value = state;
            value ^= value << 13;
            value ^= value >> 17;
            value ^= value << 5;
            state = value == 0 ? 0x9E3779B9u : value;
            return state;
        }

        public int NextInt(int minimumInclusive, int maximumExclusive)
        {
            if (maximumExclusive <= minimumInclusive)
                return minimumInclusive;

            uint range = (uint)(maximumExclusive - minimumInclusive);
            return minimumInclusive + (int)(NextUInt() % range);
        }

        public bool RollBasisPoints(int chanceBasisPoints)
        {
            int chance = CaptureBalanceData.Clamp(chanceBasisPoints, 0, CaptureBalanceData.BasisPointScale);
            return NextInt(0, CaptureBalanceData.BasisPointScale) < chance;
        }

        public static uint StableSeed(string value, uint salt = 2166136261u)
        {
            unchecked
            {
                uint hash = salt == 0 ? 2166136261u : salt;
                string text = value ?? string.Empty;
                for (int i = 0; i < text.Length; i++)
                {
                    char c = text[i];
                    hash ^= (byte)(c & 0xff);
                    hash *= 16777619u;
                    hash ^= (byte)(c >> 8);
                    hash *= 16777619u;
                }

                return hash == 0 ? 0x9E3779B9u : hash;
            }
        }
    }
}
