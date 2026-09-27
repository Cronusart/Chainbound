using UnityEngine;

namespace Chainbound.Combat
{
    public static class CombatPopupAmountScaleUtility
    {
        public static float ResolveScaleFactor(CombatPopupVisualProfileSO profile, int amount)
        {
            if (profile == null || !profile.scaleByAmount)
                return 1f;

            float minAmount = Mathf.Max(0f, profile.minAmountForScale);
            float maxAmount = Mathf.Max(minAmount + 0.0001f, profile.maxAmountForScale);
            float t = Mathf.InverseLerp(minAmount, maxAmount, Mathf.Abs(amount));
            return Mathf.Lerp(profile.minAmountScaleMultiplier, profile.maxAmountScaleMultiplier, t);
        }

        public static float ResolveFontSize(CombatPopupVisualProfileSO profile, int amount, float baseFontSize)
        {
            if (profile == null || !profile.scaleFontSizeByAmount)
                return baseFontSize;

            // Font size should follow profile.fontSize and the same amount curve as root scale.
            // minAmountFontSize/maxAmountFontSize are legacy absolute bounds and are ignored here
            // so tuning fontSize in the profile asset is reflected at runtime.
            return baseFontSize * ResolveScaleFactor(profile, amount);
        }
    }
}
