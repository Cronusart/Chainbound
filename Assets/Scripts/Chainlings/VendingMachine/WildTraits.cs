using System;

namespace Chainbound.Combat
{
    /// <summary>
    /// Traits rolled for a wild encounter: IVs, mutation, entangled flags.
    /// Produced by <see cref="WildRoller"/> and consumed by battle initialization.
    /// </summary>
    [Serializable]
    public struct WildTraits
    {
        /// <summary>IVs in stat order: [MaxHP, MeleeAtk, MeleeDef, RangedAtk, RangedDef, Speed].</summary>
        public int[] ivs;

        public bool isMutated;
        /// <summary>Mutation FX id from the mutation rarity table; <see cref="ChainlingVisualIdentity.NoneId"/> when not mutated.</summary>
        public string mutationId;
        public int mutationRarityRank;
        public bool isEntangled;
        /// <summary>Rolled entangled type when <see cref="isEntangled"/>; otherwise <see cref="ChainlingType.None"/>.</summary>
        public ChainlingType entangledType;
    }
}
