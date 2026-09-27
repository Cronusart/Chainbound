#if UNITY_EDITOR
using SignalJam.UI.MenuNavigation;
using SignalJam.UI.MenuNavigation.PartyTab;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace SignalJam.UI.MenuNavigation.Editor
{
    /// <summary>
    /// Authors StatsView career layout (lines + dealt/taken bar) on party prefabs.
    /// </summary>
    [InitializeOnLoad]
    public static class PartyTabStatsViewPrefabUtility
    {
        private const string PartyPrefabPath = "Assets/Prefabs/UI/PlayerMenu/ChainlingsPartyView.prefab";
        private const string PixeloidFontPath = "Assets/Resources/TextMesh Pro/Fonts/PixeloidSans SDF.asset";
        private const float LineHeight = 18f;
        private const float BarHeight = 14f;

        static PartyTabStatsViewPrefabUtility()
        {
            EditorApplication.delayCall += TryEnsureStatsViewOnLoad;
        }

        private static void TryEnsureStatsViewOnLoad()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            if (!PrefabNeedsStatsView(PartyPrefabPath))
                return;

            EnsureStatsViewInMenuPrefabs();
            CopyColumn1StatsViewToOtherColumns();
        }

        private static bool PrefabNeedsStatsView(string prefabPath)
        {
            GameObject prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefabRoot == null)
                return false;

            ChainlingPartyInfoViewLayout[] layouts =
                prefabRoot.GetComponentsInChildren<ChainlingPartyInfoViewLayout>(true);
            for (int i = 0; i < layouts.Length; i++)
            {
                ChainlingPartyInfoViewLayout layout = layouts[i];
                if (layout == null || layout.ViewMode != ChainlingPartyViewMode.Stats)
                    continue;

                if (layout.GetComponent<ChainlingPartyStatsViewLayout>() == null)
                    return true;

                ChainlingPartyStatsViewLayout statsLayout =
                    layout.GetComponent<ChainlingPartyStatsViewLayout>();

                if (layout.transform.Find("DamageBalanceBar") == null)
                    return true;

                if (layout.transform.Find("Victories") == null ||
                    layout.transform.Find("EnemyKos") == null ||
                    layout.transform.Find("DamageDealt") == null ||
                    layout.transform.Find("DamageTaken") == null ||
                    layout.transform.Find("XpEarned") == null ||
                    layout.transform.Find("TimesKod") == null)
                    return true;

                if (StatsLayoutNeedsAnimationParitySync(statsLayout))
                    return true;

                if (layout.transform.Find("Lines") != null || layout.transform.Find("Badges") != null)
                    return true;
            }

            return false;
        }

        private static bool StatsLayoutNeedsAnimationParitySync(ChainlingPartyStatsViewLayout layout)
        {
            if (layout == null)
                return true;

            SerializedObject so = new SerializedObject(layout);
            SerializedProperty stagger = so.FindProperty("itemStaggerSeconds");
            if (stagger == null || !Mathf.Approximately(stagger.floatValue, 0.045f))
                return true;

            SerializedProperty lineY = so.FindProperty("lineStartYOffset");
            if (lineY == null || !Mathf.Approximately(lineY.floatValue, 10f))
                return true;

            SerializedProperty barFall = so.FindProperty("barFallDuration");
            if (barFall == null || !Mathf.Approximately(barFall.floatValue, 0.18f))
                return true;

            SerializedProperty barFill = so.FindProperty("barFillDuration");
            if (barFill == null || !Mathf.Approximately(barFill.floatValue, 0.42f))
                return true;

            return false;
        }

        [MenuItem("Tools/Chainbound/UI/Player Menu Dev/Ensure Party Tab Stats View", priority = 49)]
        public static void EnsureStatsViewInMenuPrefabs()
        {
            int updated = EnsurePrefab(PartyPrefabPath) ? 1 : 0;
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[PartyTabStatsViewPrefabUtility] Ensured Stats view on {updated} prefab(s).");
        }

        [MenuItem("Tools/Chainbound/UI/Player Menu Dev/Copy Column 1 Stats View to Party Columns", priority = 50)]
        public static void CopyColumn1StatsViewToOtherColumns()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PartyPrefabPath);
            try
            {
                ChainlingPartyInfoViewLayout source = FindStatsLayout(root, "Column_1");
                if (source == null)
                {
                    Debug.LogError(
                        "[PartyTabStatsViewPrefabUtility] Column_1 StatsView not found under " +
                        "ChainlingsPartyView → Column_1 → … → StatsView.");
                    return;
                }

                // Author on Column_1, then full-clone into 2/3 (no runtime Instantiates in play mode).
                EnsureStatsLayout(source);
                RemoveLegacyStatsSurfaces(source.transform);

                int copied = 0;
                string[] targets = { "Column_2", "Column_3" };
                for (int i = 0; i < targets.Length; i++)
                {
                    ChainlingPartyInfoViewLayout target = FindStatsLayout(root, targets[i]);
                    if (target == null)
                    {
                        Debug.LogWarning(
                            $"[PartyTabStatsViewPrefabUtility] {targets[i]} StatsView not found; skipped.");
                        continue;
                    }

                    if (ReplaceStatsView(source, target) != null)
                        copied++;
                }

                PrefabUtility.SaveAsPrefabAsset(root, PartyPrefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log(
                    $"[PartyTabStatsViewPrefabUtility] Copied Column_1 StatsView " +
                    $"(full hierarchy + RectTransforms + animation settings) to {copied} party column(s). " +
                    "Play mode does not Instantiate Stats children — edit Column_1 then re-run this copy.");
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
                Debug.LogWarning($"[PartyTabStatsViewPrefabUtility] Prefab missing: {prefabPath}");
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
                    if (layouts[i] == null || layouts[i].ViewMode != ChainlingPartyViewMode.Stats)
                        continue;
                    dirty |= EnsureStatsLayout(layouts[i]);
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

        private static ChainlingPartyInfoViewLayout FindStatsLayout(GameObject root, string columnName)
        {
            Transform column = FindDeep(root.transform, columnName);
            if (column == null)
                return null;

            Transform stats = FindDeep(column, "StatsView");
            return stats != null ? stats.GetComponent<ChainlingPartyInfoViewLayout>() : null;
        }

        private static ChainlingPartyInfoViewLayout ReplaceStatsView(
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
                clonedLayout.ConfigureMode(ChainlingPartyViewMode.Stats);
                clonedLayout.ApplyDefaultPresentationForMode();
                EditorUtility.SetDirty(clonedLayout);
            }

            ChainlingPartyStatsViewLayout statsLayout = clone.GetComponent<ChainlingPartyStatsViewLayout>();
            if (statsLayout != null)
            {
                RemoveLegacyStatsSurfaces(clone.transform);
                EditorUtility.SetDirty(statsLayout);
            }

            RefreshBinderViewReferences(binder);
            return clonedLayout;
        }

        private static bool RemoveLegacyStatsSurfaces(Transform statsRoot)
        {
            if (statsRoot == null)
                return false;

            bool dirty = false;
            dirty |= DestroyChildIfPresent(statsRoot, "Lines");
            dirty |= DestroyChildIfPresent(statsRoot, "Badges");
            return dirty;
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

        private static bool EnsureStatsLayout(ChainlingPartyInfoViewLayout viewLayout)
        {
            if (viewLayout == null)
                return false;

            Transform root = viewLayout.transform;
            bool dirty = false;

            ChainlingPartyStatsViewLayout layout =
                viewLayout.GetComponent<ChainlingPartyStatsViewLayout>();
            if (layout == null)
            {
                layout = viewLayout.gameObject.AddComponent<ChainlingPartyStatsViewLayout>();
                dirty = true;
            }

            dirty |= RemoveLegacyStatsSurfaces(root);

            viewLayout.ApplyDefaultPresentationForMode();

            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(PixeloidFontPath);

            TMP_Text victories = EnsureLabeledText(root, "Victories", font, "Victories: —", out bool vNew);
            dirty |= vNew;
            TMP_Text enemyKos = EnsureLabeledText(root, "EnemyKos", font, "Enemy KOs: —", out bool kNew);
            dirty |= kNew;
            TMP_Text damageDealt = EnsureLabeledText(root, "DamageDealt", font, "Damage dealt: —", out bool ddNew);
            dirty |= ddNew;
            TMP_Text damageTaken = EnsureLabeledText(root, "DamageTaken", font, "Damage taken: —", out bool dtNew);
            dirty |= dtNew;
            TMP_Text xpEarned = EnsureLabeledText(root, "XpEarned", font, "XP earned: —", out bool xpNew);
            dirty |= xpNew;
            TMP_Text timesKod = EnsureLabeledText(root, "TimesKod", font, "Times KO'd: —", out bool tkNew);
            dirty |= tkNew;

            bool restack = vNew || kNew || ddNew || dtNew || xpNew || tkNew;
            if (restack || root.Find("DamageBalanceBar") == null)
            {
                PlaceTop(victories.rectTransform, -4f, LineHeight);
                PlaceBelow(enemyKos.rectTransform, victories.rectTransform, 2f, LineHeight);
                PlaceBelow(damageDealt.rectTransform, enemyKos.rectTransform, 2f, LineHeight);
                PlaceBelow(damageTaken.rectTransform, damageDealt.rectTransform, 2f, LineHeight);
            }

            RectTransform barRoot = EnsureBalanceBar(root, font, out bool barNew);
            dirty |= barNew;
            if ((barNew || restack) && barRoot != null && damageTaken != null)
                PlaceBelow(barRoot, damageTaken.rectTransform, 4f, BarHeight + 14f);

            if (restack || barNew)
            {
                RectTransform afterBar = barRoot != null ? barRoot : damageTaken.rectTransform;
                PlaceBelow(xpEarned.rectTransform, afterBar, 4f, LineHeight);
                PlaceBelow(timesKod.rectTransform, xpEarned.rectTransform, 2f, LineHeight);
            }

            Image track = barRoot != null ? barRoot.Find("Track")?.GetComponent<Image>() : null;
            Image dealtFill = barRoot != null ? barRoot.Find("DealtFill")?.GetComponent<Image>() : null;
            Image takenFill = barRoot != null ? barRoot.Find("TakenFill")?.GetComponent<Image>() : null;
            TMP_Text dealtCap = barRoot != null
                ? barRoot.Find("DealtCap")?.GetComponent<TMP_Text>()
                : null;
            TMP_Text takenCap = barRoot != null
                ? barRoot.Find("TakenCap")?.GetComponent<TMP_Text>()
                : null;

            SerializedObject so = new SerializedObject(layout);
            so.FindProperty("victoriesText").objectReferenceValue = victories;
            so.FindProperty("enemyKosText").objectReferenceValue = enemyKos;
            so.FindProperty("damageDealtText").objectReferenceValue = damageDealt;
            so.FindProperty("damageTakenText").objectReferenceValue = damageTaken;
            so.FindProperty("xpEarnedText").objectReferenceValue = xpEarned;
            so.FindProperty("timesKodText").objectReferenceValue = timesKod;
            so.FindProperty("balanceBarRoot").objectReferenceValue = barRoot;
            so.FindProperty("balanceTrack").objectReferenceValue = track;
            so.FindProperty("dealtFill").objectReferenceValue = dealtFill;
            so.FindProperty("takenFill").objectReferenceValue = takenFill;
            so.FindProperty("dealtCapLabel").objectReferenceValue = dealtCap;
            so.FindProperty("takenCapLabel").objectReferenceValue = takenCap;
            // Keep Entry / Moves / Attributes timing parity on authored layouts.
            SetFloatIfPresent(so, "animationStartDelay", 0.12f);
            SetFloatIfPresent(so, "itemStaggerSeconds", 0.045f);
            SetFloatIfPresent(so, "lineFadeDuration", 0.16f);
            SetFloatIfPresent(so, "lineStartYOffset", 10f);
            SetFloatIfPresent(so, "lineSettleDuration", 0.14f);
            SetFloatIfPresent(so, "barFallDuration", 0.18f);
            SetFloatIfPresent(so, "barSettleDuration", 0.12f);
            SetFloatIfPresent(so, "barStartYOffset", 14f);
            SetFloatIfPresent(so, "barOvershootYOffset", -3f);
            SetFloatIfPresent(so, "barScaleOvershootY", 1.04f);
            SetFloatIfPresent(so, "barFillDuration", 0.42f);
            so.ApplyModifiedPropertiesWithoutUndo();
            dirty = true;

            // Clear legacy InfoViewLayout badge/line wiring for Stats.
            SerializedObject viewSo = new SerializedObject(viewLayout);
            SerializedProperty showBadges = viewSo.FindProperty("showBadges");
            if (showBadges != null)
                showBadges.boolValue = false;
            SerializedProperty badgesRoot = viewSo.FindProperty("badgesRoot");
            if (badgesRoot != null)
                badgesRoot.objectReferenceValue = null;
            SerializedProperty linesRoot = viewSo.FindProperty("linesRoot");
            if (linesRoot != null)
                linesRoot.objectReferenceValue = null;
            viewSo.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(viewLayout);
            EditorUtility.SetDirty(layout);
            return dirty;
        }

        private static RectTransform EnsureBalanceBar(Transform root, TMP_FontAsset font, out bool created)
        {
            created = false;
            Transform existing = root.Find("DamageBalanceBar");
            if (existing != null)
                return existing as RectTransform;

            GameObject barGo = new GameObject("DamageBalanceBar", typeof(RectTransform));
            RectTransform barRoot = barGo.GetComponent<RectTransform>();
            barRoot.SetParent(root, false);
            created = true;

            Image track = CreateStretchImage("Track", barRoot, new Color(0.12f, 0.16f, 0.22f, 0.92f));
            RectTransform trackRect = track.rectTransform;
            trackRect.anchorMin = new Vector2(0f, 0.35f);
            trackRect.anchorMax = new Vector2(1f, 0.85f);
            trackRect.offsetMin = Vector2.zero;
            trackRect.offsetMax = Vector2.zero;

            Image dealt = CreateStretchImage("DealtFill", barRoot, new Color(0.95f, 0.45f, 0.38f, 1f));
            RectTransform dealtRect = dealt.rectTransform;
            dealtRect.anchorMin = new Vector2(0f, 0.35f);
            dealtRect.anchorMax = new Vector2(0.5f, 0.85f);
            dealtRect.offsetMin = Vector2.zero;
            dealtRect.offsetMax = Vector2.zero;

            Image taken = CreateStretchImage("TakenFill", barRoot, new Color(0.35f, 0.62f, 0.95f, 1f));
            RectTransform takenRect = taken.rectTransform;
            takenRect.anchorMin = new Vector2(0.5f, 0.35f);
            takenRect.anchorMax = new Vector2(1f, 0.85f);
            takenRect.offsetMin = Vector2.zero;
            takenRect.offsetMax = Vector2.zero;

            TMP_Text dealtCap = CreateText("DealtCap", barRoot, "Dealt", font, 11f, TextAlignmentOptions.BottomLeft);
            RectTransform dealtCapRect = dealtCap.rectTransform;
            dealtCapRect.anchorMin = new Vector2(0f, 0f);
            dealtCapRect.anchorMax = new Vector2(0.5f, 0.35f);
            dealtCapRect.offsetMin = new Vector2(2f, 0f);
            dealtCapRect.offsetMax = Vector2.zero;
            dealtCap.color = PlayerMenuUiFactory.Muted;

            TMP_Text takenCap = CreateText("TakenCap", barRoot, "Taken", font, 11f, TextAlignmentOptions.BottomRight);
            RectTransform takenCapRect = takenCap.rectTransform;
            takenCapRect.anchorMin = new Vector2(0.5f, 0f);
            takenCapRect.anchorMax = new Vector2(1f, 0.35f);
            takenCapRect.offsetMin = Vector2.zero;
            takenCapRect.offsetMax = new Vector2(-2f, 0f);
            takenCap.color = PlayerMenuUiFactory.Muted;

            return barRoot;
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
                    return text;
            }

            created = true;
            return CreateText(name, root, placeholder, font, 15f, TextAlignmentOptions.MidlineLeft);
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

        private static Image CreateStretchImage(string name, Transform parent, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            Image image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
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

        private static void SetFloatIfPresent(SerializedObject so, string propertyName, float value)
        {
            SerializedProperty property = so.FindProperty(propertyName);
            if (property != null)
                property.floatValue = value;
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

    [CustomEditor(typeof(ChainlingPartyStatsViewLayout))]
    public sealed class ChainlingPartyStatsViewLayoutEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "Stats is prefab-authored only (no play-mode Instantiates). " +
                "Tune Column_1 / StatsView, then copy to Columns 2 & 3.",
                MessageType.Info);
            if (GUILayout.Button("Copy Column 1 Stats View → Columns 2 & 3"))
                PartyTabStatsViewPrefabUtility.CopyColumn1StatsViewToOtherColumns();
            if (GUILayout.Button("Ensure Party Tab Stats View"))
                PartyTabStatsViewPrefabUtility.EnsureStatsViewInMenuPrefabs();
        }
    }
}
#endif
