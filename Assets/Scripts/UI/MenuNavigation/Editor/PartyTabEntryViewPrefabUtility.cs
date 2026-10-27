#if UNITY_EDITOR
using Chainbound.Combat;
using SignalJam.UI.MenuNavigation;
using SignalJam.UI.MenuNavigation.PartyTab;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace SignalJam.UI.MenuNavigation.Editor
{
    /// <summary>
    /// Authors Entry (DetailView) layout on party prefabs. Play mode never Instantiates these.
    /// </summary>
    [InitializeOnLoad]
    public static class PartyTabEntryViewPrefabUtility
    {
        private const string PartyPrefabPath = "Assets/Prefabs/UI/PlayerMenu/ChainlingsPartyView.prefab";
        private const string TypeChromePalettePath = "Assets/Data/UI/PartyTab/ChainlingPartyTypeChromePalette.asset";
        private const string RarityPalettePath = "Assets/Data/UI/PartyTabMoveRarityPalette.asset";
        private const string TypeIconPalettePath = "Assets/Data/Combat/Type/Icons/Type_Icons.asset";
        private const string PixeloidFontPath = "Assets/Resources/TextMesh Pro/Fonts/PixeloidSans SDF.asset";
        private const string RowBackgroundSpritePath = "Assets/Sprites/UI/PlayerMenu/rarityaccent_movesrow.png";
        private const string LegacyRowBackgroundSpritePath = "Assets/Sprites/UI/PlayerMenu/bar_sliced.png";
        private const float DefaultRowHeight = 60f;

        static PartyTabEntryViewPrefabUtility()
        {
            EditorApplication.delayCall += TryEnsureEntryViewOnLoad;
        }

        private static void TryEnsureEntryViewOnLoad()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            if (!PrefabNeedsEntryView(PartyPrefabPath))
                return;

            EnsureEntryViewInMenuPrefabs();
            CopyColumn1EntryViewToOtherColumns();
        }

        private static bool PrefabNeedsEntryView(string prefabPath)
        {
            GameObject prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefabRoot == null)
                return false;

            ChainlingPartyInfoViewLayout[] layouts =
                prefabRoot.GetComponentsInChildren<ChainlingPartyInfoViewLayout>(true);
            for (int i = 0; i < layouts.Length; i++)
            {
                ChainlingPartyInfoViewLayout layout = layouts[i];
                if (layout == null || layout.ViewMode != ChainlingPartyViewMode.Detail)
                    continue;

                ChainlingPartyEntryViewLayout entryLayout =
                    layout.GetComponent<ChainlingPartyEntryViewLayout>();
                if (entryLayout == null)
                    return true;

                // Legacy chip / Lines surfaces — destroy when present (Attributes-style).
                Transform chipRow = layout.transform.Find("ChipRow");
                if (chipRow != null)
                    return true;
                Transform lines = layout.transform.Find("Lines");
                if (lines != null)
                    return true;

                if (layout.transform.Find("SpeciesName") == null)
                    return true;
                if (layout.transform.Find("PreferredType") == null)
                    return true;
                if (layout.transform.Find("PreferredRole") == null)
                    return true;
                if (layout.transform.Find("Description") == null)
                    return true;
                if (layout.transform.Find("SignatureLabel") == null)
                    return true;
                if (layout.transform.Find("SignatureMoveRow") == null &&
                    entryLayout.SignatureMoveRow == null)
                    return true;
                if (layout.transform.Find("Encounters") == null)
                    return true;

                SerializedObject so = new SerializedObject(entryLayout);
                if (so.FindProperty("preferredTypeText")?.objectReferenceValue == null ||
                    so.FindProperty("preferredRoleText")?.objectReferenceValue == null ||
                    so.FindProperty("speciesNameText")?.objectReferenceValue == null)
                    return true;
            }

            // Column_2 / Column_3 drifted from Column_1 authored Entry hierarchy.
            if (EntryColumnsOutOfSync(prefabRoot))
                return true;

            return false;
        }

        private static bool EntryColumnsOutOfSync(GameObject prefabRoot)
        {
            ChainlingPartyInfoViewLayout source = FindEntryLayout(prefabRoot, "Column_1");
            if (source == null)
                return false;

            string fingerprint = BuildEntryHierarchyFingerprint(source.transform);
            string[] columns = { "Column_2", "Column_3" };
            for (int i = 0; i < columns.Length; i++)
            {
                ChainlingPartyInfoViewLayout other = FindEntryLayout(prefabRoot, columns[i]);
                if (other == null)
                    return true;

                if (!string.Equals(
                        fingerprint,
                        BuildEntryHierarchyFingerprint(other.transform),
                        System.StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static string BuildEntryHierarchyFingerprint(Transform root)
        {
            if (root == null)
                return "";

            var builder = new System.Text.StringBuilder(256);
            AppendFingerprint(root, root, builder);
            return builder.ToString();
        }

        private static void AppendFingerprint(Transform root, Transform node, System.Text.StringBuilder builder)
        {
            if (node == null)
                return;

            string relative = BuildRelativePath(root, node);
            builder.Append(relative)
                .Append('|')
                .Append(node.gameObject.activeSelf ? '1' : '0')
                .Append('|');

            RectTransform rect = node as RectTransform;
            if (rect != null)
            {
                builder.Append(rect.anchorMin).Append(',')
                    .Append(rect.anchorMax).Append(',')
                    .Append(rect.pivot).Append(',')
                    .Append(rect.anchoredPosition).Append(',')
                    .Append(rect.sizeDelta).Append('|');
            }

            TMP_Text tmp = node.GetComponent<TMP_Text>();
            if (tmp != null)
            {
                builder.Append("fs=").Append(tmp.fontSize.ToString("0.###")).Append('|')
                    .Append("fw=").Append((int)tmp.fontWeight).Append('|')
                    .Append("al=").Append((int)tmp.alignment).Append('|');
            }

            builder.Append(';');

            for (int i = 0; i < node.childCount; i++)
                AppendFingerprint(root, node.GetChild(i), builder);
        }

        private static string BuildRelativePath(Transform root, Transform node)
        {
            if (node == root)
                return ".";

            var parts = new System.Collections.Generic.List<string>(8);
            Transform cursor = node;
            while (cursor != null && cursor != root)
            {
                parts.Add(cursor.name);
                cursor = cursor.parent;
            }

            parts.Reverse();
            return string.Join("/", parts);
        }

        [MenuItem("Tools/Chainbound/UI/Player Menu Dev/Ensure Party Tab Entry View", priority = 47)]
        public static void EnsureEntryViewInMenuPrefabs()
        {
            int updated = EnsurePrefab(PartyPrefabPath) ? 1 : 0;
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[PartyTabEntryViewPrefabUtility] Ensured Entry view on {updated} prefab(s).");
        }

        [MenuItem("Tools/Chainbound/UI/Player Menu Dev/Copy Column 1 Entry View to Party Columns", priority = 48)]
        public static void CopyColumn1EntryViewToOtherColumns()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PartyPrefabPath);
            try
            {
                ChainlingPartyInfoViewLayout source = FindEntryLayout(root, "Column_1");
                if (source == null)
                {
                    Debug.LogError(
                        "[PartyTabEntryViewPrefabUtility] Column_1 DetailView (Entry) not found under " +
                        "ChainlingsPartyView → Column_1 → … → DetailView.");
                    return;
                }

                if (source.GetComponent<ChainlingPartyEntryViewLayout>() == null)
                    EnsureEntryLayout(source);

                // Strip legacy surfaces from the source of truth before cloning.
                RemoveLegacyEntrySurfaces(source.transform);

                // Do not Ensure columns 2/3 before copy — that can recreate removed objects.
                // Full Instantiate of Column_1 is the single source of truth.
                int copied = 0;
                string[] targets = { "Column_2", "Column_3" };
                for (int i = 0; i < targets.Length; i++)
                {
                    ChainlingPartyInfoViewLayout target = FindEntryLayout(root, targets[i]);
                    if (target == null)
                    {
                        Debug.LogWarning(
                            $"[PartyTabEntryViewPrefabUtility] {targets[i]} DetailView not found; skipped.");
                        continue;
                    }

                    if (ReplaceEntryView(source, target) != null)
                        copied++;
                }

                PrefabUtility.SaveAsPrefabAsset(root, PartyPrefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log(
                    $"[PartyTabEntryViewPrefabUtility] Copied Column_1 DetailView/Entry " +
                    $"(full hierarchy + RectTransforms + TMP/component settings) to {copied} party column(s). " +
                    "Objects absent from Column_1 are not present on the copies.");
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
                Debug.LogWarning($"[PartyTabEntryViewPrefabUtility] Prefab missing: {prefabPath}");
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
                    if (layouts[i] == null || layouts[i].ViewMode != ChainlingPartyViewMode.Detail)
                        continue;
                    dirty |= EnsureEntryLayout(layouts[i]);
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

        private static ChainlingPartyInfoViewLayout FindEntryLayout(GameObject root, string columnName)
        {
            Transform column = FindDeep(root.transform, columnName);
            if (column == null)
                return null;

            Transform detail = FindDeep(column, "DetailView");
            return detail != null ? detail.GetComponent<ChainlingPartyInfoViewLayout>() : null;
        }

        private static ChainlingPartyInfoViewLayout[] FindEntryViewLayouts(GameObject root)
        {
            ChainlingPartyInfoViewLayout[] all =
                root.GetComponentsInChildren<ChainlingPartyInfoViewLayout>(true);
            var matches = new System.Collections.Generic.List<ChainlingPartyInfoViewLayout>();
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].ViewMode == ChainlingPartyViewMode.Detail)
                    matches.Add(all[i]);
            }

            return matches.ToArray();
        }

        private static ChainlingPartyInfoViewLayout FindColumnOneEntryView(
            ChainlingPartyInfoViewLayout[] entryViews)
        {
            if (entryViews == null)
                return null;

            for (int i = 0; i < entryViews.Length; i++)
            {
                ChainlingPartyInfoViewLayout view = entryViews[i];
                if (view != null && IsUnderPartyColumn(view.transform, out string columnName) &&
                    columnName == "Column_1")
                    return view;
            }

            return null;
        }

        private static bool IsUnderPartyColumn(Transform transform, out string columnName)
        {
            columnName = "";
            Transform cursor = transform;
            while (cursor != null)
            {
                if (cursor.name == "Column_1" || cursor.name == "Column_2" || cursor.name == "Column_3")
                {
                    columnName = cursor.name;
                    return true;
                }

                cursor = cursor.parent;
            }

            return false;
        }

        /// <summary>
        /// Full DetailView clone into the target column's Views hierarchy.
        /// Preserves sibling index / name / active state; copies root RectTransform from source;
        /// refreshes InfoPanelBinder viewLayouts. Anything removed from Column_1 is absent after replace.
        /// </summary>
        private static ChainlingPartyInfoViewLayout ReplaceEntryView(
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

            // Full Instantiation preserves TMP font sizes, Graphic colors, RectTransforms, and component fields.
            GameObject clone = Object.Instantiate(source.gameObject, parent);
            clone.name = targetName;
            clone.transform.SetSiblingIndex(siblingIndex);
            clone.SetActive(wasActive);

            CopyRectTransform(source.transform as RectTransform, clone.transform as RectTransform);

            Object.DestroyImmediate(target.gameObject);

            ChainlingPartyInfoViewLayout clonedLayout = clone.GetComponent<ChainlingPartyInfoViewLayout>();
            if (clonedLayout != null)
            {
                clonedLayout.ConfigureMode(ChainlingPartyViewMode.Detail);
                EditorUtility.SetDirty(clonedLayout);
            }

            ChainlingPartyEntryViewLayout entryLayout = clone.GetComponent<ChainlingPartyEntryViewLayout>();
            if (entryLayout != null)
            {
                RemoveLegacyEntrySurfaces(clone.transform);
                EditorUtility.SetDirty(entryLayout);
            }

            RefreshBinderViewReferences(binder);
            return clonedLayout;
        }

        private static bool RemoveLegacyEntrySurfaces(Transform entryRoot)
        {
            if (entryRoot == null)
                return false;

            bool dirty = false;
            dirty |= DestroyChildIfPresent(entryRoot, "ChipRow");
            dirty |= DestroyChildIfPresent(entryRoot, "Lines");
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

        private static bool EnsureEntryLayout(ChainlingPartyInfoViewLayout viewLayout)
        {
            if (viewLayout == null)
                return false;

            Transform root = viewLayout.transform;
            bool dirty = false;

            ChainlingPartyEntryViewLayout layout =
                viewLayout.GetComponent<ChainlingPartyEntryViewLayout>();
            if (layout == null)
            {
                layout = viewLayout.gameObject.AddComponent<ChainlingPartyEntryViewLayout>();
                dirty = true;
            }

            if (root.Find("Lines") is Transform lines)
            {
                Object.DestroyImmediate(lines.gameObject);
                dirty = true;
            }

            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(PixeloidFontPath);
            TypeIconPaletteSO typeIcons =
                AssetDatabase.LoadAssetAtPath<TypeIconPaletteSO>(TypeIconPalettePath);
            PartyTabMoveRarityPaletteSO rarity =
                AssetDatabase.LoadAssetAtPath<PartyTabMoveRarityPaletteSO>(RarityPalettePath);
            Sprite rowBackground = LoadRowBackgroundSprite();

            if (root.Find("ChipRow") is Transform chipRow)
            {
                Object.DestroyImmediate(chipRow.gameObject);
                dirty = true;
            }

            TMP_Text speciesName = EnsureLabeledText(
                root,
                "SpeciesName",
                font,
                "Species: —",
                16f,
                out bool speciesNew);
            dirty |= speciesNew;

            TMP_Text preferredType = EnsureLabeledText(
                root,
                "PreferredType",
                font,
                "Preferred type: —",
                16f,
                out bool typeNew);
            dirty |= typeNew;

            TMP_Text preferredRole = EnsureLabeledText(
                root,
                "PreferredRole",
                font,
                "Preferred role: —",
                16f,
                out bool roleNew);
            dirty |= roleNew;

            bool restackCatalog = speciesNew || typeNew || roleNew;
            if (restackCatalog && speciesName != null)
            {
                PlaceTop(speciesName.rectTransform, -6f, 20f);
                speciesName.fontStyle = FontStyles.Normal;
                speciesName.color = PlayerMenuUiFactory.Text;
                if (preferredType != null)
                    PlaceBelow(preferredType.rectTransform, speciesName.rectTransform, 2f, 20f);
                if (preferredRole != null && preferredType != null)
                    PlaceBelow(preferredRole.rectTransform, preferredType.rectTransform, 2f, 20f);
            }

            TMP_Text description = EnsureLabeledText(root, "Description", font, "Description…", 14f, out bool descNew);
            dirty |= descNew;
            if (descNew)
            {
                PlaceBelow(description.rectTransform, preferredRole.rectTransform, 4f, 48f);
                description.textWrappingMode = TextWrappingModes.Normal;
                description.overflowMode = TextOverflowModes.Ellipsis;
                description.maxVisibleLines = 3;
                description.color = PlayerMenuUiFactory.Muted;
            }
            else if (restackCatalog && description != null && preferredRole != null)
            {
                PlaceBelow(description.rectTransform, preferredRole.rectTransform, 4f, 48f);
            }

            TMP_Text signatureLabel = EnsureLabeledText(root, "SignatureLabel", font, "Signature", 14f, out bool sigLabelNew);
            dirty |= sigLabelNew;
            if (sigLabelNew || (restackCatalog && signatureLabel != null && description != null))
                PlaceBelow(signatureLabel.rectTransform, description.rectTransform, 6f, 18f);

            ChainlingPartyMoveRowDisplay signatureRow = EnsureSignatureMoveRow(
                root,
                viewLayout,
                font,
                rowBackground,
                out bool rowNew);
            dirty |= rowNew;
            if ((rowNew || restackCatalog) && signatureRow != null && signatureLabel != null)
            {
                RectTransform rowRect = signatureRow.GetComponent<RectTransform>();
                PlaceBelow(rowRect, signatureLabel.rectTransform, 2f, DefaultRowHeight);
                rowRect.offsetMin = new Vector2(8f, rowRect.offsetMin.y);
                rowRect.offsetMax = new Vector2(-8f, rowRect.offsetMax.y);
            }

            TMP_Text encounters = EnsureLabeledText(root, "Encounters", font, "Encounters · …", 13f, out bool encNew);
            dirty |= encNew;
            if (encNew || restackCatalog)
            {
                RectTransform anchor = signatureRow != null
                    ? signatureRow.GetComponent<RectTransform>()
                    : signatureLabel.rectTransform;
                PlaceBelow(encounters.rectTransform, anchor, 6f, 18f);
                if (encNew)
                {
                    encounters.color = PlayerMenuUiFactory.Muted;
                    encounters.textWrappingMode = TextWrappingModes.NoWrap;
                    encounters.overflowMode = TextOverflowModes.Ellipsis;
                }
            }

            SerializedObject so = new SerializedObject(layout);
            so.FindProperty("typeIconPalette").objectReferenceValue = typeIcons;
            so.FindProperty("rarityPalette").objectReferenceValue = rarity;
            so.FindProperty("speciesNameText").objectReferenceValue = speciesName;
            so.FindProperty("preferredTypeText").objectReferenceValue = preferredType;
            so.FindProperty("preferredRoleText").objectReferenceValue = preferredRole;
            so.FindProperty("descriptionText").objectReferenceValue = description;
            so.FindProperty("signatureLabelText").objectReferenceValue = signatureLabel;
            so.FindProperty("encountersText").objectReferenceValue = encounters;
            so.FindProperty("signatureMoveRow").objectReferenceValue = signatureRow;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(viewLayout);
            EditorUtility.SetDirty(layout);
            return dirty;
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

        private static ChainlingPartyMoveRowDisplay EnsureSignatureMoveRow(
            Transform root,
            ChainlingPartyInfoViewLayout detailView,
            TMP_FontAsset font,
            Sprite rowBackground,
            out bool created)
        {
            created = false;
            Transform existing = root.Find("SignatureMoveRow");
            if (existing != null)
            {
                ChainlingPartyMoveRowDisplay existingDisplay =
                    existing.GetComponent<ChainlingPartyMoveRowDisplay>();
                if (existingDisplay != null)
                    return existingDisplay;
            }

            ChainlingPartyMoveRowDisplay template = FindMovesTemplateRow(detailView);
            GameObject rowGo;
            if (template != null)
            {
                rowGo = Object.Instantiate(template.gameObject, root);
                rowGo.name = "SignatureMoveRow";
                created = true;
            }
            else
            {
                rowGo = CreateMoveRow(root, "SignatureMoveRow", font, rowBackground);
                created = true;
            }

            ChainlingPartyMoveRowDisplay display = rowGo.GetComponent<ChainlingPartyMoveRowDisplay>();
            if (display == null)
                display = rowGo.AddComponent<ChainlingPartyMoveRowDisplay>();

            LayoutElement le = rowGo.GetComponent<LayoutElement>();
            if (le == null)
                le = rowGo.AddComponent<LayoutElement>();
            le.preferredHeight = DefaultRowHeight;
            le.minHeight = DefaultRowHeight;

            return display;
        }

        private static ChainlingPartyMoveRowDisplay FindMovesTemplateRow(ChainlingPartyInfoViewLayout detailView)
        {
            if (detailView == null)
                return null;

            Transform views = detailView.transform.parent;
            if (views == null)
                return null;

            Transform moves = views.Find("MovesView");
            if (moves == null)
                return null;

            Transform row = moves.Find("MoveRowsViewport/MoveRows/MoveRow_01")
                ?? moves.Find("MoveRows/MoveRow_01");
            return row != null
                ? row.GetComponent<ChainlingPartyMoveRowDisplay>()
                : moves.GetComponentInChildren<ChainlingPartyMoveRowDisplay>(true);
        }

        private static GameObject CreateMoveRow(
            Transform parent,
            string rowName,
            TMP_FontAsset rowFont,
            Sprite rowBackground)
        {
            GameObject rowGo = new GameObject(
                rowName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(LayoutElement),
                typeof(ChainlingPartyMoveRowDisplay));
            RectTransform rowRect = rowGo.GetComponent<RectTransform>();
            rowRect.SetParent(parent, false);

            LayoutElement rowLayout = rowGo.GetComponent<LayoutElement>();
            rowLayout.preferredHeight = DefaultRowHeight;
            rowLayout.minHeight = DefaultRowHeight;

            Image background = CreateStretchImage("RowBackground", rowGo.transform, Color.white);
            background.sprite = rowBackground;
            background.type = Image.Type.Sliced;
            background.pixelsPerUnitMultiplier = 0.2f;

            RectTransform content = CreateStretchRect("Content", rowGo.transform, 4f, 2f, 4f, 2f);
            HorizontalLayoutGroup contentLayout = content.gameObject.AddComponent<HorizontalLayoutGroup>();
            contentLayout.spacing = 6f;
            contentLayout.childAlignment = TextAnchor.MiddleLeft;
            contentLayout.childControlWidth = true;
            contentLayout.childControlHeight = true;
            contentLayout.childForceExpandWidth = false;
            contentLayout.childForceExpandHeight = false;
            contentLayout.padding = new RectOffset(4, 4, 0, 0);

            Image typeIcon = CreateImage("TypeIcon", content, Color.white);
            typeIcon.preserveAspect = true;
            LayoutElement iconLayout = typeIcon.gameObject.AddComponent<LayoutElement>();
            iconLayout.preferredWidth = 22f;
            iconLayout.preferredHeight = 22f;

            TMP_Text moveName = CreateText("MoveName", content, "Move Name", rowFont, TextAlignmentOptions.MidlineLeft);
            LayoutElement nameLayout = moveName.gameObject.AddComponent<LayoutElement>();
            nameLayout.flexibleWidth = 1f;
            nameLayout.minWidth = 40f;

            TMP_Text statText = CreateText("StatText", content, "85", rowFont, TextAlignmentOptions.MidlineRight);
            LayoutElement statLayout = statText.gameObject.AddComponent<LayoutElement>();
            statLayout.preferredWidth = 44f;
            statLayout.minWidth = 36f;

            ChainlingPartyMoveRowDisplay rowDisplay = rowGo.GetComponent<ChainlingPartyMoveRowDisplay>();
            SerializedObject serializedRow = new SerializedObject(rowDisplay);
            serializedRow.FindProperty("rowBackground").objectReferenceValue = background;
            serializedRow.FindProperty("contentRoot").objectReferenceValue = content;
            serializedRow.FindProperty("contentLayoutGroup").objectReferenceValue = contentLayout;
            serializedRow.FindProperty("typeIcon").objectReferenceValue = typeIcon;
            serializedRow.FindProperty("moveNameText").objectReferenceValue = moveName;
            serializedRow.FindProperty("statText").objectReferenceValue = statText;
            serializedRow.FindProperty("layoutElement").objectReferenceValue = rowLayout;
            serializedRow.FindProperty("typeIconLayoutElement").objectReferenceValue = iconLayout;
            serializedRow.FindProperty("moveNameLayoutElement").objectReferenceValue = nameLayout;
            serializedRow.FindProperty("statLayoutElement").objectReferenceValue = statLayout;
            serializedRow.FindProperty("moveNameFont").objectReferenceValue = rowFont;
            serializedRow.FindProperty("statFont").objectReferenceValue = rowFont;
            serializedRow.FindProperty("preferredRowHeight").floatValue = DefaultRowHeight;
            serializedRow.ApplyModifiedPropertiesWithoutUndo();
            return rowGo;
        }

        private static TMP_Text EnsureLabeledText(
            Transform parent,
            string name,
            TMP_FontAsset font,
            string defaultText,
            float fontSize,
            out bool created)
        {
            Transform existing = parent.Find(name);
            TMP_Text text = existing != null ? existing.GetComponent<TMP_Text>() : null;
            created = text == null;
            if (text == null)
            {
                GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                go.transform.SetParent(parent, false);
                text = go.GetComponent<TMP_Text>();
                text.text = defaultText;
                text.font = font;
                text.fontSize = fontSize;
                text.alignment = TextAlignmentOptions.TopLeft;
                text.color = PlayerMenuUiFactory.Text;
                text.textWrappingMode = TextWrappingModes.NoWrap;
                text.overflowMode = TextOverflowModes.Ellipsis;
                text.raycastTarget = false;
            }

            return text;
        }

        private static void PlaceBelow(RectTransform rect, RectTransform above, float gap, float height)
        {
            if (rect == null)
                return;

            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            float y = -8f;
            if (above != null)
                y = above.anchoredPosition.y - above.rect.height - gap;
            rect.anchoredPosition = new Vector2(0f, y);
            rect.sizeDelta = new Vector2(-16f, height);
        }

        private static RectTransform EnsureRect(Transform parent, string name, out bool created)
        {
            Transform existing = parent.Find(name);
            if (existing is RectTransform existingRect)
            {
                created = false;
                return existingRect;
            }

            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            created = true;
            return go.GetComponent<RectTransform>();
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static RectTransform CreateStretchRect(
            string name,
            Transform parent,
            float left,
            float bottom,
            float right,
            float top)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
            return rect;
        }

        private static Image CreateStretchImage(string name, Transform parent, Color color)
        {
            Image image = CreateImage(name, parent, color);
            Stretch(image.rectTransform);
            return image;
        }

        private static Image CreateImage(string name, Transform parent, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            Image image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static TMP_Text CreateText(
            string name,
            Transform parent,
            string value,
            TMP_FontAsset font,
            TextAlignmentOptions alignment)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            TMP_Text text = go.GetComponent<TMP_Text>();
            text.text = value;
            text.font = font;
            text.fontSize = 18f;
            text.alignment = alignment;
            text.color = PlayerMenuUiFactory.Text;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.raycastTarget = false;
            return text;
        }

        private static Sprite LoadRowBackgroundSprite()
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(RowBackgroundSpritePath);
            if (sprite != null)
                return sprite;
            return AssetDatabase.LoadAssetAtPath<Sprite>(LegacyRowBackgroundSpritePath);
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

    [CustomEditor(typeof(ChainlingPartyEntryViewLayout))]
    public sealed class ChainlingPartyEntryViewLayoutEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space(8f);
            EditorGUILayout.HelpBox(
                "Copies Column_1 DetailView/Entry (full authored hierarchy + RectTransforms + TMP font sizes + component settings) onto Column_2 and Column_3. " +
                "Anything removed from Column_1 is removed from the copies.",
                MessageType.Info);
            if (GUILayout.Button("Copy Column 1 Entry View → Columns 2 & 3"))
                PartyTabEntryViewPrefabUtility.CopyColumn1EntryViewToOtherColumns();
        }
    }
}
#endif
