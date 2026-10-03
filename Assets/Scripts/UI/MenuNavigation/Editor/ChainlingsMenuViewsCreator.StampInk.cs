#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;

namespace SignalJam.UI.MenuNavigation.Editor
{
    public static partial class ChainlingsMenuViewsCreator
    {
        private const string StampInkMaterialPath = "Assets/Resources/TextMesh Pro/Fonts/PixeloidSans_Stamp.mat";

        [MenuItem("Tools/Chainbound/UI/Player Menu Dev/Apply Chainling Stamp Ink", priority = 47)]
        public static void ApplyChainlingStampInk()
        {
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(IndexFontPath);
            Material stamp = AssetDatabase.LoadAssetAtPath<Material>(StampInkMaterialPath);
            if (font == null || stamp == null)
            {
                Debug.LogWarning("[ChainlingsMenuViewsCreator] Stamp ink material or PixeloidSans SDF is missing.");
                return;
            }

            GameObject instance = PrefabUtility.LoadPrefabContents(DetailPrefabPath);
            try
            {
                int assigned = 0;
                TMP_Text[] labels = instance.GetComponentsInChildren<TMP_Text>(true);
                for (int i = 0; i < labels.Length; i++)
                {
                    TMP_Text label = labels[i];
                    if (label == null || label.font != font || label.fontSharedMaterial == stamp)
                        continue;

                    label.fontSharedMaterial = stamp;
                    assigned++;
                }

                PrefabUtility.SaveAsPrefabAsset(instance, DetailPrefabPath);
                Debug.Log("[ChainlingsMenuViewsCreator] Assigned stamp ink to " + assigned + " PixeloidSans labels on ChainlingDetailView.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(instance);
            }

            AssetDatabase.SaveAssets();
        }
    }
}
#endif
