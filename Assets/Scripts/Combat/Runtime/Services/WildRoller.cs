using System.Collections.Generic;
using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// Rolls wild encounter traits (IVs, entangled) from a seeded RNG.
    /// Mutation FX is rolled separately via <see cref="WildMutationRoller"/>.
    /// </summary>
    public static class WildRoller
    {
        private const int STAT_COUNT = 6;

        public static readonly ChainlingType[] DefaultEntangledTypePool =
        {
            ChainlingType.Node,
            ChainlingType.Gas,
            ChainlingType.Block,
            ChainlingType.Encrypted,
            ChainlingType.Burned,
            ChainlingType.Frozen,
            ChainlingType.Forked,
            ChainlingType.Mined,
            ChainlingType.Staked,
            ChainlingType.Compressed,
            ChainlingType.Bot,
            ChainlingType.Glitched,
            ChainlingType.Shard,
        };

        public static WildTraits RollTraits(
            WildRollConfig config,
            System.Random rng,
            int favorScore = 0,
            FavorConfigSO favorConfig = null)
        {
            if (config == null)
                config = new WildRollConfig();
            if (rng == null)
                rng = new System.Random();

            int range = config.ivMax - config.ivMin + 1;
            if (range < 1)
                range = 1;

            var ivs = new int[STAT_COUNT];
            for (int i = 0; i < STAT_COUNT; i++)
                ivs[i] = rng.Next(range) + config.ivMin;

            FavorConfigSO favor = favorConfig != null ? favorConfig : FavorConfigSO.CreateRuntimeDefault();
            float bumpChance = favor.IvLowestBumpChance(favorScore);
            if (bumpChance > 0f && rng.NextDouble() < bumpChance)
                BumpLowestIv(ivs, config.ivMax);

            bool isEntangled = rng.NextDouble() < Mathf.Clamp01(config.entangledChance);
            ChainlingType entangledType = ChainlingType.None;
            if (isEntangled)
                entangledType = RollEntangledType(config, rng);

            return new WildTraits
            {
                ivs = ivs,
                isMutated = false,
                mutationId = ChainlingVisualIdentity.NoneId,
                mutationRarityRank = 0,
                isEntangled = isEntangled,
                entangledType = entangledType,
            };
        }

        public static ChainlingType RollEntangledType(WildRollConfig config, System.Random rng)
        {
            if (rng == null)
                rng = new System.Random();

            IReadOnlyList<ChainlingType> pool = config != null &&
                                               config.entangledTypePool != null &&
                                               config.entangledTypePool.Count > 0
                ? config.entangledTypePool
                : DefaultEntangledTypePool;

            var valid = new List<ChainlingType>();
            for (int i = 0; i < pool.Count; i++)
            {
                ChainlingType type = pool[i];
                if (type != ChainlingType.None)
                    valid.Add(type);
            }

            if (valid.Count == 0)
                return ChainlingType.Node;

            return valid[rng.Next(valid.Count)];
        }

        private static void BumpLowestIv(int[] ivs, int ivMax)
        {
            if (ivs == null || ivs.Length == 0)
                return;

            int lowestIndex = 0;
            for (int i = 1; i < ivs.Length; i++)
            {
                if (ivs[i] < ivs[lowestIndex])
                    lowestIndex = i;
            }

            if (ivs[lowestIndex] < ivMax)
                ivs[lowestIndex]++;
        }
    }
}
