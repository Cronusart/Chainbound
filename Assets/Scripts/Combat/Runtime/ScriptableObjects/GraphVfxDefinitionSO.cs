using UnityEngine;

namespace Chainbound.Combat
{
    [CreateAssetMenu(fileName = "GraphVfx_", menuName = "Chainbound/Combat/Presentation/VFX/Graph VFX")]
    public class GraphVfxDefinitionSO : CombatVfxDefinitionSO
    {
        public GameObject vfxGraphPrefab;
    }
}