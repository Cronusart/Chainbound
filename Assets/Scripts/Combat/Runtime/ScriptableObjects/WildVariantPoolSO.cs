using System.Collections.Generic;
using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// Reusable weighted visual-variant pool shared across area/zone entries.
    /// </summary>
    [CreateAssetMenu(fileName = "WildVariantPool_", menuName = "Chainbound/Combat/Encounters/Wild Variant Pool")]
    public class WildVariantPoolSO : ScriptableObject
    {
        [Tooltip("Body-skin ids + spawn weights only. Catalog rarity (tier label) lives on ChainlingSpeciesVisualSetSO.")]
        public List<WeightedVariantEntry> entries = new List<WeightedVariantEntry>();
    }
}
