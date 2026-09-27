using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// Editor/dev fallbacks when popup spawner references are not wired in scene.
    /// Build scenes should still assign catalog/prefab on <see cref="DamagePopupSpawner"/>.
    /// </summary>
    internal static class CombatPopupRuntimeDefaults
    {
        private const string VisualCatalogPath = "Assets/Data/Combat/PopUps/VisualCatalog/CombatPopupVisualCatalog.asset";
        private const string PopupPrefabPath = "Assets/Prefabs/Combat/DMGPopUp.prefab";

        public static CombatPopupVisualCatalogSO TryLoadVisualCatalog()
        {
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<CombatPopupVisualCatalogSO>(VisualCatalogPath);
#else
            return null;
#endif
        }

        public static DamagePopup TryLoadPopupPrefab()
        {
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<DamagePopup>(PopupPrefabPath);
#else
            return null;
#endif
        }
    }
}
