using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// Typed spawn request for a combat popup beat.
    /// </summary>
    public struct CombatPopupPlayRequest
    {
        public CombatPopupCategory category;
        public Vector3 worldPosition;
        public string label;
        public int amount;
        public bool useNumericLabel;
        public bool isHeal;
        /// <summary>+1 default side, -1 when chainling visuals are flipped.</summary>
        public int facingSign;
        public CombatPopupVisualProfileSO profileOverride;

        public static CombatPopupPlayRequest ForLabel(
            CombatPopupCategory category,
            Vector3 worldPosition,
            string label,
            int facingSign = 1,
            CombatPopupVisualProfileSO profileOverride = null)
        {
            return new CombatPopupPlayRequest
            {
                category = category,
                worldPosition = worldPosition,
                label = label,
                amount = 0,
                useNumericLabel = false,
                isHeal = false,
                facingSign = facingSign >= 0 ? 1 : -1,
                profileOverride = profileOverride,
            };
        }

        public static CombatPopupPlayRequest ForAmount(
            CombatPopupCategory category,
            Vector3 worldPosition,
            int amount,
            bool isHeal,
            int facingSign = 1,
            CombatPopupVisualProfileSO profileOverride = null)
        {
            return new CombatPopupPlayRequest
            {
                category = category,
                worldPosition = worldPosition,
                label = null,
                amount = amount,
                useNumericLabel = true,
                isHeal = isHeal,
                facingSign = facingSign >= 0 ? 1 : -1,
                profileOverride = profileOverride,
            };
        }
    }
}
