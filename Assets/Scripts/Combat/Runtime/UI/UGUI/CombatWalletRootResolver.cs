using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// Resolves the root transform for the 3D combat hardware wallet (action pad device).
    /// Do not use <see cref="GameObject.Find(string)"/> with "Hardware_Wallet" alone — the prefab may
    /// contain duplicate names (outer root vs FBX child), which hides only part of the device.
    /// </summary>
    public static class CombatWalletRootResolver
    {
        /// <summary>
        /// Returns the transform that should be toggled for full show/hide of the wallet (mesh + UITK screen + screen quad).
        /// </summary>
        public static Transform Resolve()
        {
            // Authoritative: CombatActionSelectorController lives on the outer prefab root for Hardware_Wallet.
            var selectors = Object.FindObjectsOfType<CombatActionSelectorController>(true);
            if (selectors != null && selectors.Length > 0)
            {
                for (int i = 0; i < selectors.Length; i++)
                {
                    if (selectors[i] != null)
                        return selectors[i].transform;
                }
            }

            var byName = GameObject.Find("Hardware_Wallet");
            return byName != null ? byName.transform : null;
        }
    }
}
