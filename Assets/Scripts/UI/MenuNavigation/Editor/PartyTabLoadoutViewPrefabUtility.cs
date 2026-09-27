#if UNITY_EDITOR
using SignalJam.UI.MenuNavigation;
using SignalJam.UI.MenuNavigation.PartyTab;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace SignalJam.UI.MenuNavigation.Editor
{
    /// <summary>
    /// Authors LoadoutView look-card hierarchy on party prefabs. Play mode never Instantiates these.
    /// Typography mirrors authored Stats lines: PixeloidSans 18, UpperCase.
    /// </summary>
    [InitializeOnLoad]
    public static class PartyTabLoadoutViewPrefabUtility
    {
        private const string PartyPrefabPath = "Assets/Prefabs/UI/PlayerMenu/ChainlingsPartyView.prefab";
        private const string PixeloidFontPath = "Assets/Resources/TextMesh Pro/Fonts/PixeloidSans SDF.asset";
        private const float LineHeight = 22f;
        private const float LineFontSize = 18f;

        static PartyTabLoadoutViewPrefabUtility()
        {
            EditorApplication.delayCall += TryEnsureLoadoutViewOnLoad;
        }

        private static void TryEnsureLoadoutViewOnLoad()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            if (!PrefabNeedsLoadoutView(PartyPrefabPath))
                return;

            EnsureLoadoutViewInMenuPrefabs();
            CopyColumn1LoadoutViewToOtherColumns();
        }

        private static bool PrefabNeedsLoadoutView(string prefabPath)
        {
            GameObject prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefabRoot == null)
                return false;

            ChainlingPartyInfoViewLayout[] layouts =
                prefabRoot.GetComponentsInChildren<ChainlingPartyInfoViewLayout>(true);
            for (int i = 0; i < layouts.Length; i++)
            {
                ChainlingPartyInfoViewLayout layout = layouts[i];
                if (layout == null || layout.ViewMode != ChainlingPartyViewMode.Loadout)
                    continue;

                if (layout.GetComponent<ChainlingPartyLoadoutViewLayout>() == null)
                    return true;

                if (layout.transform.Find("Variant") == null ||
                    layout.transform.Find("Aura") == null ||
                    layout.transform.Find("PartyColumn") == null ||
                    layout.transform.Find("Idle") == null ||
                    layout.transform.Find("CombatFx") == null)
                    return true;

                if (layout.transform.Find("Lines") != null)
                    return true;
            }

            return false;
        }

        [MenuItem("Tools/Chainbound/UI/Player Menu Dev/Ensure Party Tab Loadout View", priority = 51)]
        public static void EnsureLoadoutViewInMenuPrefabs()
        {
            int updated = EnsurePrefab(PartyPrefabPath) ? 1 : 0;
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[PartyTabLoadoutViewPrefabUtility] Ensured Loadout view on {updated} prefab(s).");
        }

        [MenuItem("Tools/Chainbound/UI/Player Menu Dev/Copy Column 1 Loadout View to Party Columns", priority = 52)]
        public static void CopyColumn1LoadoutViewToOtherColumns()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PartyPrefabPath);
            try
            {
                ChainlingPartyInfoViewLayout source = FindLoadoutLayout(root, "Column_1");
                if (source == null)
                {
                    Debug.LogError(
                        "[PartyTabLoadoutViewPrefabUtility] Column_1 LoadoutView not found under " +
                        "ChainlingsPartyView → Column_1 → … → LoadoutView.");
                    return;
                }

                EnsureLoadoutLayout(source);
                RemoveLegacyLoadoutSurfaces(source.transform);

                int copied = 0;
                string[] targets = { "Column_2", "Column_3" };
                for (int i = 0; i < targets.Length; i++)
                {
                    ChainlingPartyInfoViewLayout target = FindLoadoutLayout(root, targets[i]);
                    if (target == null)
                    {
                        Debug.LogWarning(
                            $"[PartyTabLoadoutViewPrefabUtility] {targets[i]} LoadoutView not found; skipped.");
                        continue;
                    }

                    if (ReplaceLoadoutView(source, target) != null)
                        copied++;
                }

                PrefabUtility.SaveAsPrefabAsset(root, PartyPrefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log(
                    $"[PartyTabLoadoutViewPrefabUtility] Copied Column_1 LoadoutView " +
                    $"(full hierarchy + RectTransforms + TMP/animation settings) to {copied} party column(s). " +
                    "Play mode does not Instantiate Loadout children — edit Column_1 then re-run this copy.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static bool EnsurePrefab(string prefabPath)
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (source == null)
            {
                Debug.LogWarning($"[PartyTabLoadoutViewPrefabUtility] Prefab missing: {prefabPath}");
                return false;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                ChainlingPartyInfoViewLayout[] layouts =
                    root.GetComponentsInChildren<ChainlingPartyInfoViewLayout>(true);
                bool dirty = false;
                for (int i = 0; i < layouts.Length; i++)
                {
                    if (layouts[i] == null || layouts[i].ViewMode != ChainlingPartyViewMode.Loadout)
                        continue;
                    dirty |= EnsureLoadoutLayout(layouts[i]);
                }

                if (dirty)
                    PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                return dirty;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static ChainlingPartyInfoViewLayout FindLoadoutLayout(GameObject root, string columnName)
        {
            Transform column = FindDeep(root.transform, columnName);
            if (column == null)
                return null;

            Transform loadout = FindDeep(column, "LoadoutView");
            return loadout != null ? loadout.GetComponent<ChainlingPartyInfoViewLayout>() : null;
        }

        private static ChainlingPartyInfoViewLayout ReplaceLoadoutView(
            ChainlingPartyInfoViewLayout source,
            ChainlingPartyInfoViewLayout target)
        {
            if (source == null || target == null || source == target)
                return null;

            Transform parent = target.transform.parent;
            if (parent == null)
                return null;

            int siblingIndex = target.transform.GetSiblingIndex();
            string targetName = target.name;
            bool wasActive = target.gameObject.activeSelf;
            ChainlingPartyInfoPanelBinder binder =
                target.GetComponentInParent<ChainlingPartyInfoPanelBinder>(true);

            GameObject clone = Object.Instantiate(source.gameObject, parent);
            clone.name = targetName;
            clone.transform.SetSiblingIndex(siblingIndex);
            clone.SetActive(wasActive);

            CopyRectTransform(source.transform as RectTransform, clone.transform as RectTransform);

            Object.DestroyImmediate(target.gameObject);

            ChainlingPartyInfoViewLayout clonedLayout = clone.GetComponent<ChainlingPartyInfoViewLayout>();
            if (clonedLayout != null)
            {
                clonedLayout.ConfigureMode(ChainlingPartyViewMode.Loadout);
                clonedLayout.ApplyDefaultPresentationForMode();
                EditorUtility.SetDirty(clonedLayout);
            }

            ChainlingPartyLoadoutViewLayout loadoutLayout =
                clone.GetComponent<ChainlingPartyLoadoutViewLayout>();
            if (loadoutLayout != null)
            {
                RemoveLegacyLoadoutSurfaces(clone.transform);
                EditorUtility.SetDirty(loadoutLayout);
            }

            RefreshBinderViewReferences(binder);
            return clonedLayout;
        }

        private static bool RemoveLegacyLoadoutSurfaces(Transform loadoutRoot)
        {
            if (loadoutRoot == null)
                return false;

            return DestroyChildIfPresent(loadoutRoot, "Lines");
        }

        private static bool DestroyChildIfPresent(Transform parent, string childName)
        {
            Transform child = parent.Find(childName);
            if (child == null)
                return false;

            Object.DestroyImmediate(child.gameObject);
            return true;
        }

        private static void CopyRectTransform(RectTransform source, RectTransform destination)
        {
            if (source == null || destination == null)
                return;

            destination.anchorMin = source.anchorMin;
            destination.anchorMax = source.anchorMax;
            destination.pivot = source.pivot;
            destination.anchoredPosition = source.anchoredPosition;
            destination.sizeDelta = source.sizeDelta;
            destination.localRotation = source.localRotation;
            destination.localScale = source.localScale;
            destination.offsetMin = source.offsetMin;
            destination.offsetMax = source.offsetMax;
        }

        private static void RefreshBinderViewReferences(ChainlingPartyInfoPanelBinder binder)
        {
            if (binder == null)
                return;

            SerializedObject serializedBinder = new SerializedObject(binder);
            RectTransform viewsRoot = binder.transform.Find("Views") as RectTransform;

            SerializedProperty viewsRootProperty = serializedBinder.FindProperty("viewsRoot");
            if (viewsRootProperty != null)
                viewsRootProperty.objectReferenceValue = viewsRoot;

            SerializedProperty viewLayoutsProperty = serializedBinder.FindProperty("viewLayouts");
            if (viewLayoutsProperty != null)
            {
                viewLayoutsProperty.ClearArray();
                ChainlingPartyInfoViewLayout[] layouts = viewsRoot != null
                    ? viewsRoot.GetComponentsInChildren<ChainlingPartyInfoViewLayout>(true)
                    : binder.GetComponentsInChildren<ChainlingPartyInfoViewLayout>(true);

                for (int i = 0; i < layouts.Length; i++)
                {
                    viewLayoutsProperty.InsertArrayElementAtIndex(i);
                    viewLayoutsProperty.GetArrayElementAtIndex(i).objectReferenceValue = layouts[i];
                }
            }

            serializedBinder.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(binder);
        }

        private static bool EnsureLoadoutLayout(ChainlingPartyInfoViewLayout viewLayout)
        {
            if (viewLayout == null)
                return false;

            Transform root = viewLayout.transform;
            bool dirty = false;

            ChainlingPartyLoadoutViewLayout layout =
                viewLayout.GetComponent<ChainlingPartyLoadoutViewLayout>();
            if (layout == null)
            {
                layout = viewLayout.gameObject.AddComponent<ChainlingPartyLoadoutViewLayout>();
                dirty = true;
            }

            dirty |= RemoveLegacyLoadoutSurfaces(root);
            viewLayout.ApplyDefaultPresentationForMode();

            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(PixeloidFontPath);

            TMP_Text variant = EnsureLabeledText(root, "Variant", font, "Variant: —", out bool vNew);
            dirty |= vNew;
            TMP_Text aura = EnsureLabeledText(root, "Aura", font, "Aura: —", out bool aNew);
            dirty |= aNew;
            TMP_Text partyColumn = EnsureLabeledText(root, "PartyColumn", font, "Party Column: —", out bool pNew);
            dirty |= pNew;
            TMP_Text idle = EnsureLabeledText(root, "Idle", font, "Idle: —", out bool iNew);
            dirty |= iNew;
            TMP_Text combatFx = EnsureLabeledText(root, "CombatFx", font, "Combat FX: —", out bool cNew);
            dirty |= cNew;

            bool restack = vNew || aNew || pNew || iNew || cNew;
            if (restack)
            {
                PlaceTop(variant.rectTransform, -4f, LineHeight);
                PlaceBelow(aura.rectTransform, variant.rectTransform, 2f, LineHeight);
                PlaceBelow(partyColumn.rectTransform, aura.rectTransform, 2f, LineHeight);
                PlaceBelow(idle.rectTransform, partyColumn.rectTransform, 2f, LineHeight);
                PlaceBelow(combatFx.rectTransform, idle.rectTransform, 4f, LineHeight);
            }

            SerializedObject so = new SerializedObject(layout);
            so.FindProperty("variantText").objectReferenceValue = variant;
            so.FindProperty("auraText").objectReferenceValue = aura;
            so.FindProperty("partyColumnText").objectReferenceValue = partyColumn;
            so.FindProperty("idleText").objectReferenceValue = idle;
            so.FindProperty("combatFxText").objectReferenceValue = combatFx;
            SetFloatIfPresent(so, "animationStartDelay", 0.12f);
            SetFloatIfPresent(so, "itemStaggerSeconds", 0.045f);
            SetFloatIfPresent(so, "lineFadeDuration", 0.16f);
            SetFloatIfPresent(so, "lineStartYOffset", 10f);
            SetFloatIfPresent(so, "lineSettleDuration", 0.14f);
            so.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject viewSo = new SerializedObject(viewLayout);
            SerializedProperty linesRoot = viewSo.FindProperty("linesRoot");
            if (linesRoot != null)
                linesRoot.objectReferenceValue = null;
            SerializedProperty lineTemplate = viewSo.FindProperty("lineTemplate");
            if (lineTemplate != null)
                lineTemplate.objectReferenceValue = null;
            SerializedProperty authored = viewSo.FindProperty("authoredLineSlots");
            if (authored != null)
                authored.ClearArray();
            viewSo.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(viewLayout);
            EditorUtility.SetDirty(layout);
            return true;
        }

        private static void SetFloatIfPresent(SerializedObject so, string propertyName, float value)
        {
            SerializedProperty property = so.FindProperty(propertyName);
            if (property != null)
                property.floatValue = value;
        }

        private static TMP_Text EnsureLabeledText(
            Transform root,
            string name,
            TMP_FontAsset font,
            string placeholder,
            out bool created)
        {
            created = false;
            Transform existing = root.Find(name);
            if (existing != null)
            {
                TMP_Text text = existing.GetComponent<TMP_Text>();
                if (text != null)
                {
                    ApplyStatsLineTypography(text, font);
                    return text;
                }
            }

            created = true;
            TMP_Text createdText = CreateText(
                name,
                root,
                placeholder,
                font,
                LineFontSize,
                TextAlignmentOptions.MidlineLeft);
            ApplyStatsLineTypography(createdText, font);
            return createdText;
        }

        private static void ApplyStatsLineTypography(TMP_Text text, TMP_FontAsset font)
        {
            if (text == null)
                return;

            if (font != null)
                text.font = font;
            text.fontSize = LineFontSize;
            text.fontStyle = FontStyles.UpperCase;
            text.color = PlayerMenuUiFactory.Text;
            text.richText = true;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.raycastTarget = false;
            text.alignment = TextAlignmentOptions.MidlineLeft;
        }

        private static TMP_Text CreateText(
            string name,
            Transform parent,
            string value,
            TMP_FontAsset font,
            float size,
            TextAlignmentOptions alignment)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);

            TMP_Text text = go.GetComponent<TMP_Text>();
            if (font != null)
                text.font = font;
            text.text = value;
            text.fontSize = size;
            text.color = PlayerMenuUiFactory.Text;
            text.alignment = alignment;
            text.richText = true;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.raycastTarget = false;
            return text;
        }

        private static void PlaceTop(RectTransform rect, float y, float height)
        {
            if (rect == null)
                return;

            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, y);
            rect.sizeDelta = new Vector2(-16f, height);
        }

        private static void PlaceBelow(RectTransform rect, RectTransform above, float gap, float height)
        {
            if (rect == null || above == null)
                return;

            float y = above.anchoredPosition.y - above.sizeDelta.y - gap;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, y);
            rect.sizeDelta = new Vector2(-16f, height);
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root == null)
                return null;
            if (root.name == name)
                return root;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindDeep(root.GetChild(i), name);
                if (found != null)
                    return found;
            }

            return null;
        }
    }

    [CustomEditor(typeof(ChainlingPartyLoadoutViewLayout))]
    public sealed class ChainlingPartyLoadoutViewLayoutEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "Loadout is prefab-authored only (no play-mode Instantiates). " +
                "Typography matches Stats lines (18 / UpperCase). " +
                "Tune Column_1 / LoadoutView, then copy to Columns 2 & 3.",
                MessageType.Info);
            if (GUILayout.Button("Copy Column 1 Loadout View → Columns 2 & 3"))
                PartyTabLoadoutViewPrefabUtility.CopyColumn1LoadoutViewToOtherColumns();
            if (GUILayout.Button("Ensure Party Tab Loadout View"))
                PartyTabLoadoutViewPrefabUtility.EnsureLoadoutViewInMenuPrefabs();
        }
    }
}
#endif
