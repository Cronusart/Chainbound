#if UNITY_EDITOR
using SignalJam.UI.MenuNavigation.PartyTab;
using SignalJam.VisualFx;
using UnityEditor;
using UnityEngine;

namespace SignalJam.UI.MenuNavigation.Editor
{
    /// <summary>
    /// Wires cycle typography VFX references and label-only layout on party column prefabs.
    /// </summary>
    public static class ChainlingsPartyCycleVfxPrefabUtility
    {
        private const string PartyPrefabPath = "Assets/Prefabs/UI/PlayerMenu/ChainlingsPartyView.prefab";
        private const string ColumnShellPrefabPath = "Assets/Prefabs/UI/PlayerMenu/ChainlingPartyColumnShell.prefab";
        private const string CycleTypographyPath = "Assets/Data/UI/PartyTab/PartyTabCycleTypography.asset";

        [MenuItem("Chainbound/VFX/Wire Cycle VFX On Party Prefabs")]
        public static void WireCycleVfxOnPartyPrefabs()
        {
            PartyTabCycleTypographySO typography = AssetDatabase.LoadAssetAtPath<PartyTabCycleTypographySO>(CycleTypographyPath);
            if (typography == null)
            {
                Debug.LogError($"[CycleVfx] Missing typography asset at {CycleTypographyPath}");
                return;
            }

            int partyCount = WirePrefab(PartyPrefabPath, typography, previewActiveLayouts: true);
            int shellCount = WirePrefab(ColumnShellPrefabPath, typography, previewActiveLayouts: true);

            AssetDatabase.SaveAssets();
            Debug.Log(
                $"[CycleVfx] Wired {partyCount + shellCount} cycle display(s) across party prefabs. " +
                "General views preview Cycle V typography in prefab mode.");
        }

        [MenuItem("Chainbound/VFX/Cleanup Cycle Progress Prefabs")]
        public static void CleanupCycleProgressPrefabs()
        {
            int partyRemoved = CleanupPrefab(PartyPrefabPath);
            int shellRemoved = CleanupPrefab(ColumnShellPrefabPath);
            AssetDatabase.SaveAssets();
            Debug.Log(
                $"[CycleVfx] Cleanup complete. Removed {partyRemoved + shellRemoved} legacy cycle/pip object(s). " +
                "Cycle progress now lives only under GeneralView.");
        }

        [MenuItem("Chainbound/VFX/Preview Cycle Tiers/Preview Cycle I")]
        public static void PreviewCycleI() => PreviewTier(0);

        [MenuItem("Chainbound/VFX/Preview Cycle Tiers/Preview Cycle II")]
        public static void PreviewCycleII() => PreviewTier(1);

        [MenuItem("Chainbound/VFX/Preview Cycle Tiers/Preview Cycle III")]
        public static void PreviewCycleIII() => PreviewTier(2);

        [MenuItem("Chainbound/VFX/Preview Cycle Tiers/Preview Cycle IV")]
        public static void PreviewCycleIV() => PreviewTier(3);

        [MenuItem("Chainbound/VFX/Preview Cycle Tiers/Preview Cycle V")]
        public static void PreviewCycleV() => PreviewTier(4);

        private static void PreviewTier(int tier)
        {
            PartyTabCycleTypographySO typography = AssetDatabase.LoadAssetAtPath<PartyTabCycleTypographySO>(CycleTypographyPath);
            if (typography == null)
                return;

            PreviewTierOnPrefab(PartyPrefabPath, typography, tier);
            PreviewTierOnPrefab(ColumnShellPrefabPath, typography, tier);
            AssetDatabase.SaveAssets();
        }

        private static int WirePrefab(
            string prefabPath,
            PartyTabCycleTypographySO typography,
            bool previewActiveLayouts)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            if (root == null)
            {
                Debug.LogError($"[CycleVfx] Could not load prefab at {prefabPath}");
                return 0;
            }

            int wired = 0;
            try
            {
                CleanupLoadedPrefab(root);

                ChainlingPartyInfoViewLayout[] layouts = root.GetComponentsInChildren<ChainlingPartyInfoViewLayout>(true);
                for (int i = 0; i < layouts.Length; i++)
                {
                    ChainlingPartyInfoViewLayout layout = layouts[i];
                    if (layout == null)
                        continue;

                    SerializedObject layoutObject = new SerializedObject(layout);
                    layoutObject.FindProperty("cycleTypography").objectReferenceValue = typography;
                    layoutObject.ApplyModifiedPropertiesWithoutUndo();
                }

                ChainlingPartyInfoPanelBinder[] binders = root.GetComponentsInChildren<ChainlingPartyInfoPanelBinder>(true);
                for (int i = 0; i < binders.Length; i++)
                {
                    ChainlingPartyInfoPanelBinder binder = binders[i];
                    if (binder == null)
                        continue;

                    SerializedObject binderObject = new SerializedObject(binder);
                    binderObject.FindProperty("cycleTypography").objectReferenceValue = typography;
                    binderObject.FindProperty("cycleProgressDisplay").objectReferenceValue = null;
                    binderObject.ApplyModifiedPropertiesWithoutUndo();
                }

                ChainlingPartyColumnShell[] shells = root.GetComponentsInChildren<ChainlingPartyColumnShell>(true);
                for (int i = 0; i < shells.Length; i++)
                {
                    ChainlingPartyColumnShell shell = shells[i];
                    if (shell == null)
                        continue;

                    SerializedObject shellObject = new SerializedObject(shell);
                    shellObject.FindProperty("cycleTypography").objectReferenceValue = typography;
                    shellObject.ApplyModifiedPropertiesWithoutUndo();
                }

                PartyTabCycleProgressDisplay[] displays = root.GetComponentsInChildren<PartyTabCycleProgressDisplay>(true);
                for (int i = 0; i < displays.Length; i++)
                {
                    PartyTabCycleProgressDisplay display = displays[i];
                    if (display == null || !IsUnderGeneralView(display))
                        continue;

                    SerializedObject displayObject = new SerializedObject(display);
                    displayObject.FindProperty("typography").objectReferenceValue = typography;
                    displayObject.ApplyModifiedPropertiesWithoutUndo();

                    ApplyLabelOnlyLayout(display);
                    wired++;

                    if (previewActiveLayouts && ShouldPreviewDisplay(display))
                    {
                        display.SetCycle(ChainlingPlayerTerms.FormatCycleLabel(4), 4, true);
                        FinalizeCycleLabelForPrefabSave(display, typography, 4);
                    }
                }

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            return wired;
        }

        private static int CleanupPrefab(string prefabPath)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            if (root == null)
                return 0;

            int removed = 0;
            try
            {
                removed = CleanupLoadedPrefab(root);
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            return removed;
        }

        private static int CleanupLoadedPrefab(GameObject root)
        {
            int removed = 0;

            ChainlingPartyInfoPanelBinder[] binders = root.GetComponentsInChildren<ChainlingPartyInfoPanelBinder>(true);
            for (int i = 0; i < binders.Length; i++)
            {
                ChainlingPartyInfoPanelBinder binder = binders[i];
                if (binder == null)
                    continue;

                SerializedObject binderObject = new SerializedObject(binder);
                binderObject.FindProperty("cycleProgressDisplay").objectReferenceValue = null;
                binderObject.ApplyModifiedPropertiesWithoutUndo();

                Transform legacyCycle = binder.transform.Find("CycleProgress");
                if (legacyCycle != null && !IsUnderViewsRoot(binder, legacyCycle))
                {
                    Object.DestroyImmediate(legacyCycle.gameObject);
                    removed++;
                }
            }

            PartyTabCycleProgressDisplay[] displays = root.GetComponentsInChildren<PartyTabCycleProgressDisplay>(true);
            for (int i = 0; i < displays.Length; i++)
            {
                PartyTabCycleProgressDisplay display = displays[i];
                if (display == null)
                    continue;

                if (!IsUnderGeneralView(display))
                {
                    Object.DestroyImmediate(display.gameObject);
                    removed++;
                    continue;
                }

                Transform pipsRoot = display.transform.Find("CyclePips");
                if (pipsRoot != null)
                {
                    Object.DestroyImmediate(pipsRoot.gameObject);
                    removed++;
                }

                ApplyLabelOnlyLayout(display);
            }

            return removed;
        }

        private static void PreviewTierOnPrefab(string prefabPath, PartyTabCycleTypographySO typography, int tier)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            if (root == null)
                return;

            try
            {
                CleanupLoadedPrefab(root);

                PartyTabCycleProgressDisplay[] displays = root.GetComponentsInChildren<PartyTabCycleProgressDisplay>(true);
                for (int i = 0; i < displays.Length; i++)
                {
                    PartyTabCycleProgressDisplay display = displays[i];
                    if (display == null || !ShouldPreviewDisplay(display) || !IsUnderGeneralView(display))
                        continue;

                    display.Configure(typography);
                    display.SetCycle(ChainlingPlayerTerms.FormatCycleLabel(tier), tier, true);
                    FinalizeCycleLabelForPrefabSave(display, typography, tier);
                }

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static bool ShouldPreviewDisplay(PartyTabCycleProgressDisplay display)
        {
            if (!IsUnderGeneralView(display))
                return false;

            ChainlingPartyInfoViewLayout layout = display.GetComponentInParent<ChainlingPartyInfoViewLayout>(true);
            if (layout == null)
                return true;

            SerializedObject layoutObject = new SerializedObject(layout);
            return layoutObject.FindProperty("showCycleProgress").boolValue;
        }

        private static bool IsUnderGeneralView(PartyTabCycleProgressDisplay display)
        {
            ChainlingPartyInfoViewLayout layout = display.GetComponentInParent<ChainlingPartyInfoViewLayout>(true);
            return layout != null && layout.ViewMode == ChainlingPartyViewMode.General;
        }

        private static bool IsUnderViewsRoot(ChainlingPartyInfoPanelBinder binder, Transform target)
        {
            SerializedObject binderObject = new SerializedObject(binder);
            RectTransform viewsRoot = binderObject.FindProperty("viewsRoot").objectReferenceValue as RectTransform;
            return viewsRoot != null && target.IsChildOf(viewsRoot);
        }

        private static void FinalizeCycleLabelForPrefabSave(
            PartyTabCycleProgressDisplay display,
            PartyTabCycleTypographySO typography,
            int tier)
        {
            if (display == null || typography == null || display.CycleLabel == null)
                return;

            VisualTierProfileSO profile = typography.ResolveProfile(tier);
            if (profile == null)
                return;

            VisualTierTypographyApplication.PrepareForPrefabSerialization(display.CycleLabel, profile);
            EditorUtility.SetDirty(display.CycleLabel);
        }

        private static void ApplyLabelOnlyLayout(PartyTabCycleProgressDisplay display)
        {
            if (display.CycleLabel != null)
            {
                RectTransform labelRect = display.CycleLabel.rectTransform;
                labelRect.anchorMin = new Vector2(0f, 0f);
                labelRect.anchorMax = new Vector2(1f, 1f);
                labelRect.offsetMin = Vector2.zero;
                labelRect.offsetMax = Vector2.zero;
            }
        }
    }
}
#endif
