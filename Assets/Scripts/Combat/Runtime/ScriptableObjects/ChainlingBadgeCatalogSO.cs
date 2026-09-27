using System.Collections.Generic;
using UnityEngine;

namespace Chainbound.Combat
{
    [CreateAssetMenu(fileName = "ChainlingBadgeCatalog", menuName = "Chainbound/Combat/Chainling Badge Catalog")]
    public sealed class ChainlingBadgeCatalogSO : ScriptableObject
    {
        [SerializeField] private List<ChainlingBadgeDefinitionSO> badges = new List<ChainlingBadgeDefinitionSO>();

        public IReadOnlyList<ChainlingBadgeDefinitionSO> Definitions => badges;
    }
}
