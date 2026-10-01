using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// Marks a scenery root inside the shared combat scene. The object name may
    /// also match a known stage id; this component wins when both are present.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CombatStageMarker : MonoBehaviour
    {
        [SerializeField] private string stageId = CombatStageIds.Loomville;

        public string StageId => CombatStageIds.Normalize(stageId);
    }
}
