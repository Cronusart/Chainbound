using System;
using System.Collections.Generic;
using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// Universal configuration for rolling wild encounter traits (IVs + entangled).
    /// Mutation FX odds live on <see cref="WildMutationRarityTableSO"/>; <see cref="mutationChance"/>
    /// scales non-none mutation weights when a table is used.
    /// </summary>
    [Serializable]
    public class WildRollConfig
    {
        [Tooltip("Scalar applied to non-none mutation rarity weights when a mutation table is active. Also used as legacy boolean chance when no table is assigned.")]
        [Range(0f, 1f)]
        public float mutationChance = 0.047f;

        [Tooltip("Chance of entangled/bootleg (0-1). ~2.3% default.")]
        [Range(0f, 1f)]
        public float entangledChance = 0.023f;

        [Tooltip("Uniform pool for entangled type rolls. Empty = default (all types except None/Meme).")]
        public List<ChainlingType> entangledTypePool = new List<ChainlingType>();

        [Tooltip("Minimum IV value per stat.")]
        public int ivMin = CombatConstants.IV_MIN;

        [Tooltip("Maximum IV value per stat.")]
        public int ivMax = CombatConstants.IV_MAX;
    }
}
