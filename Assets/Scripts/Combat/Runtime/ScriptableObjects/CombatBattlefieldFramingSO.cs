using UnityEngine;

namespace Chainbound.Combat
{
    [CreateAssetMenu(fileName = "CombatBattlefieldFraming", menuName = "Chainbound/Combat/Battlefield Framing")]
    public sealed class CombatBattlefieldFramingSO : ScriptableObject
    {
        [Tooltip("Enable the closer settled framing when both teams have exactly one alive active Chainling.")]
        public bool enableSingleCombatFraming = true;

        [Tooltip("Absolute followOffset.z used for settled 1v1 framing.")]
        public float singleCombatFollowOffsetZ = -50f;

        [Tooltip("Added to the authored BattleProfile followOffset.y.")]
        public float singleCombatFollowOffsetYDelta = 1.5f;

        [Tooltip("Seconds used to blend into or out of settled 1v1 framing.")]
        public float framingBlendSeconds = 0.8f;
    }
}
