using System;

namespace Chainbound.Combat
{
    public interface IDebugIvProvider
    {
        int[] GetIvs(string speciesId, int level, int teamIndex, int rosterIndex);
    }

    /// <summary>
    /// Deterministic fallback IV provider for local/dev runs when encounter IVs are absent.
    /// Produces stable values for the same (seed, speciesId, team, roster index, level).
    /// </summary>
    public sealed class SeededDebugIvProvider : IDebugIvProvider
    {
        private readonly int _battleSeed;

        public SeededDebugIvProvider(int battleSeed)
        {
            _battleSeed = battleSeed;
        }

        public int[] GetIvs(string speciesId, int level, int teamIndex, int rosterIndex)
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + _battleSeed;
                hash = hash * 31 + StableHash(speciesId);
                hash = hash * 31 + level;
                hash = hash * 31 + teamIndex;
                hash = hash * 31 + rosterIndex;

                var rng = new Random(hash);
                var ivs = new int[6];
                for (int i = 0; i < ivs.Length; i++)
                    ivs[i] = rng.Next(CombatConstants.IV_MIN, CombatConstants.IV_MAX + 1);
                return ivs;
            }
        }

        private static int StableHash(string value)
        {
            if (string.IsNullOrEmpty(value)) return 0;
            unchecked
            {
                int h = 23;
                for (int i = 0; i < value.Length; i++)
                    h = (h * 31) + value[i];
                return h;
            }
        }
    }
}
