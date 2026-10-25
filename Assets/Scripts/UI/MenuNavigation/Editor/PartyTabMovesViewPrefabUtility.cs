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
    [InitializeOnLoad]
    public static class PartyTabMovesViewPrefabUtility
    {
        static PartyTabMovesViewPrefabUtility()
        {
            EditorApplication.delayCall += TryEnsureMovesViewLayoutsOnLoad;
        }

        private static void TryEnsureMovesViewLayoutsOnLoad()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            if (!MenuPrefabsNeedMovesViewLayoutUpdate())
                return;

            EnsureMovesViewLayoutsInMenuPrefabs();
        }

        private static bool MenuPrefabsNeedMovesViewLayoutUpdate()
        {
            return PrefabNeedsMovesViewLayoutUpdate(PartyPrefabPath)
                || PrefabNeedsMovesViewLayoutUpdate(ColumnShellPrefabPath);
        }

        private static bool PrefabNeedsMovesViewLayoutUpdate(string prefabPath)
        {
            GameObject prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefabRoot == null)
                return false;

            ChainlingPartyInfoViewLayout[] layouts =
                prefabRoot.GetComponentsInChildren<ChainlingPartyInfoViewLayout>(true);
            for (int i = 0; i < layouts.Length; i++)
            {
                ChainlingPartyInfoViewLayout layout = layouts[i];
                if (layout == null || layout.ViewMode != ChainlingPartyViewMode.Moves)
                    continue;

                if (layout.GetComponent<ChainlingPartyMovesViewLayout>() == null)
                    return true;

                if (layout.transform.Find("SlotCountText") == null)
                    return true;

                if (layout.transform.Find("MoveRowsViewport") == null)
                    return true;

                RectTransform rowsRoot = layout.transform.Find("MoveRowsViewport/MoveRows") as RectTransform
                    ?? layout.transform.Find("MoveRows") as RectTransform;
                if (rowsRoot == null)
                    return true;

                if (rowsRoot.GetComponentsInChildren<ChainlingPartyMoveRowDisplay>(true).Length != MoveLoadoutSlotRules.VisibleRowsBeforeScroll)
                    return true;
            }

            return false;
        }
        private const string PartyPrefabPath = "Assets/Prefabs/UI/PlayerMenu/ChainlingsPartyView.prefab";
        private const string ColumnShellPrefabPath = "Assets/Prefabs/UI/PlayerMenu/ChainlingPartyColumnShell.prefab";
        private const string RarityPalettePath = "Assets/Data/UI/PartyTabMoveRarityPalette.asset";
        private const string TypeIconPalettePath = "Assets/Data/Combat/Type/Icons/Type_Icons.asset";
        private const string PixeloidFontPath = "Assets/Resources/TextMesh Pro/Fonts/PixeloidSans SDF.asset";
        private const string RowBackgroundSpritePath = "Assets/Sprites/UI/PlayerMenu/rarityaccent_movesrow.png";
        private const string LegacyRowBackgroundSpritePath = "Assets/Sprites/UI/PlayerMenu/bar_sliced.png";
        private const float DefaultRowHeight = 60f;
        private const float DefaultRowSpacing = 4f;

        [MenuItem("Tools/Chainbound/UI/Player Menu Dev/Ensure Party Tab Moves View Layout", priority = 44)]
        public static void EnsureMovesViewLayoutsInMenuPrefabs()
        {
            EnsureRarityPaletteAsset();

            PartyTabMoveRarityPaletteSO rarityPalette =
                AssetDatabase.LoadAssetAtPath<PartyTabMoveRarityPaletteSO>(RarityPalettePath);
            TypeIconPaletteSO typeIconPalette =
                AssetDatabase.LoadAssetAtPath<TypeIconPaletteSO>(TypeIconPalettePath);
            TMP_FontAsset rowFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(PixeloidFontPath);
            Sprite rowBackground = AssetDatabase.LoadAssetAtPath<Sprite>(RowBackgroundSpritePath);
            if (rowBackground == null)
            {
                Object[] sprites = AssetDatabase.LoadAllAssetsAtPath(RowBackgroundSpritePath);
                for (int i = 0; i < sprites.Length; i++)
                {
                    if (sprites[i] is Sprite sprite)
                    {
                        rowBackground = sprite;
                        break;
                    }
                }
            }

            int updated = 0;
            updated += EnsureInPrefab(PartyPrefabPath, rarityPalette, typeIconPalette, rowFont, rowBackground);
            updated += EnsureInPrefab(ColumnShellPrefabPath, rarityPalette, typeIconPalette, rowFont, rowBackground);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                $"[PartyTabMovesViewPrefabUtility] Ensured moves view rows on {updated} prefab(s). " +
                "Overflow moves are cloned at runtime and clipped by the four-row viewport.");
        }

        public static void EnsureMovesViewLayoutsInMenuPrefabsBatch()
        {
            EnsureMovesViewLayoutsInMenuPrefabs();
        }

        [MenuItem("Tools/Chainbound/UI/Player Menu Dev/Copy Column 1 Moves View to Party Columns", priority = 45)]
        public static void CopySlotOneMovesViewToPartyColumns()
        {
            GameObject instance = PrefabUtility.LoadPrefabContents(PartyPrefabPath);
            try
            {
                ChainlingPartyInfoViewLayout sourceMovesView = FindMovesLayout(instance, "Column_1");
                if (sourceMovesView == null)
                {
                    Debug.LogError(
                        "[PartyTabMovesViewPrefabUtility] Column_1 MovesView not found under " +
                        "ChainlingsPartyView → Column_1 → … → MovesView.");
                    return;
                }

                // Column_1 is the single source of truth — do not Ensure/rebuild targets first
                // (that can recreate default MoveRow templates and wipe authored formatting).
                DestroyChildIfPresent(sourceMovesView.transform, "Lines");
                PrepareMovesViewForCopy(sourceMovesView);

                int copied = 0;
                string[] targets = { "Column_2", "Column_3" };
                for (int i = 0; i < targets.Length; i++)
                {
                    ChainlingPartyInfoViewLayout target = FindMovesLayout(instance, targets[i]);
                    if (target == null)
                    {
                        Debug.LogWarning(
                            $"[PartyTabMovesViewPrefabUtility] {targets[i]} MovesView not found; skipped.");
                        continue;
                    }

                    if (ReplaceMovesView(sourceMovesView, target) != null)
                        copied++;
                }

                PrefabUtility.SaveAsPrefabAsset(instance, PartyPrefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log(
                    $"[PartyTabMovesViewPrefabUtility] Copied Column_1 MovesView " +
                    $"(viewport, SlotCountText, MoveRow templates + RectTransforms/TMP) to {copied} party column(s). " +
                    "Overflow rows clone MoveRow_01 at runtime — keep Column_1 rows as the template source.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(instance);
            }
        }

        private static ChainlingPartyInfoViewLayout FindMovesLayout(GameObject root, string columnName)
        {
            Transform column = FindDeep(root.transform, columnName);
            if (column == null)
                return null;

            Transform moves = FindDeep(column, "MovesView");
            return moves != null ? moves.GetComponent<ChainlingPartyInfoViewLayout>() : null;
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

        private static bool DestroyChildIfPresent(Transform parent, string childName)
        {
            if (parent == null)
                return false;

            Transform child = parent.Find(childName);
            if (child == null)
                return false;

            Object.DestroyImmediate(child.gameObject);
            return true;
        }

        private static void PrepareMovesViewForCopy(ChainlingPartyInfoViewLayout movesView)
        {
            if (movesView == null)
                return;

            ChainlingPartyMovesViewLayout layout = movesView.GetComponent<ChainlingPartyMovesViewLayout>();
            if (layout == null)
                return;

            RebindAuthoredMoveRows(layout);
            // Authored rows 2–4 (and future overflow clones) inherit MoveRow_01 presentation.
            layout.CopyFirstRowPresentationToAuthoredRows();
            EditorUtility.SetDirty(layout);
        }

        private static void RebindAuthoredMoveRows(ChainlingPartyMovesViewLayout layout)
        {
            if (layout == null)
                return;

            Transform rowsRoot = layout.transform.Find("MoveRowsViewport/MoveRows")
                ?? layout.transform.Find("MoveRows");
            if (rowsRoot == null)
                return;

            ChainlingPartyMoveRowDisplay[] found =
                rowsRoot.GetComponentsInChildren<ChainlingPartyMoveRowDisplay>(true);
            SerializedObject so = new SerializedObject(layout);
            SerializedProperty rowsProperty = so.FindProperty("authoredMoveRows");
            if (rowsProperty == null)
                return;

            rowsProperty.ClearArray();
            int write = 0;
            for (int i = 0; i < found.Length; i++)
            {
                ChainlingPartyMoveRowDisplay row = found[i];
                if (row == null)
                    continue;

                row.CaptureLayoutFromChildren();
                rowsProperty.InsertArrayElementAtIndex(write);
                rowsProperty.GetArrayElementAtIndex(write).objectReferenceValue = row;
                write++;
            }

            SerializedProperty viewport = so.FindProperty("viewportRoot");
            if (viewport != null)
                viewport.objectReferenceValue = layout.transform.Find("MoveRowsViewport");
            SerializedProperty moveRows = so.FindProperty("moveRowsRoot");
            if (moveRows != null)
                moveRows.objectReferenceValue = rowsRoot;
            SerializedProperty slotCount = so.FindProperty("slotCountText");
            if (slotCount != null)
            {
                Transform slot = layout.transform.Find("SlotCountText");
                slotCount.objectReferenceValue = slot != null ? slot.GetComponent<TMP_Text>() : null;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(layout);
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

        private static void EnsureRarityPaletteAsset()
        {
            PartyTabMoveRarityPaletteSO palette =
                AssetDatabase.LoadAssetAtPath<PartyTabMoveRarityPaletteSO>(RarityPalettePath);
            if (palette == null)
            {
                if (!AssetDatabase.IsValidFolder("Assets/Data/UI"))
                {
                    if (!AssetDatabase.IsValidFolder("Assets/Data"))
                        AssetDatabase.CreateFolder("Assets", "Data");
                    AssetDatabase.CreateFolder("Assets/Data", "UI");
                }

                palette = ScriptableObject.CreateInstance<PartyTabMoveRarityPaletteSO>();
                AssetDatabase.CreateAsset(palette, RarityPalettePath);
            }

            string currentSpritePath = palette.RowBackgroundSprite != null
                ? AssetDatabase.GetAssetPath(palette.RowBackgroundSprite)
                : "";
            bool usesLegacyRowBackground = string.Equals(
                currentSpritePath,
                LegacyRowBackgroundSpritePath,
                System.StringComparison.OrdinalIgnoreCase);

            if (palette.RowBackgroundSprite == null || usesLegacyRowBackground)
            {
                Sprite rowBackground = AssetDatabase.LoadAssetAtPath<Sprite>(RowBackgroundSpritePath);
                if (rowBackground == null)
                {
                    Object[] sprites = AssetDatabase.LoadAllAssetsAtPath(RowBackgroundSpritePath);
                    for (int i = 0; i < sprites.Length; i++)
                    {
                        if (sprites[i] is Sprite sprite)
                        {
                            rowBackground = sprite;
                            break;
                        }
                    }
                }

                if (rowBackground != null)
                {
                    SerializedObject serializedPalette = new SerializedObject(palette);
                    serializedPalette.FindProperty("rowBackgroundSprite").objectReferenceValue = rowBackground;
                    serializedPalette.FindProperty("pixelsPerUnitMultiplier").floatValue = 0.2f;
                    serializedPalette.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            EditorUtility.SetDirty(palette);
        }

        private static int EnsureInPrefab(
            string prefabPath,
            PartyTabMoveRarityPaletteSO rarityPalette,
            TypeIconPaletteSO typeIconPalette,
            TMP_FontAsset rowFont,
            Sprite rowBackground)
        {
            GameObject prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefabRoot == null)
            {
                Debug.LogWarning($"[PartyTabMovesViewPrefabUtility] Prefab not found: {prefabPath}");
                return 0;
            }

            GameObject instance = PrefabUtility.LoadPrefabContents(prefabPath);
            ChainlingPartyInfoViewLayout[] movesViews = FindMovesViewLayouts(instance);
            int count = 0;

            for (int i = 0; i < movesViews.Length; i++)
            {
                if (EnsureMovesView(movesViews[i], rarityPalette, typeIconPalette, rowFont, rowBackground))
                    count++;
            }

            if (count > 0)
            {
                PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
                Debug.Log($"[PartyTabMovesViewPrefabUtility] Updated {count} MovesView layout(s) in {prefabPath}.");
            }

            PrefabUtility.UnloadPrefabContents(instance);
            return count;
        }

        private static ChainlingPartyInfoViewLayout[] FindMovesViewLayouts(GameObject root)
        {
            ChainlingPartyInfoViewLayout[] layouts = root.GetComponentsInChildren<ChainlingPartyInfoViewLayout>(true);
            var movesViews = new System.Collections.Generic.List<ChainlingPartyInfoViewLayout>();
            for (int i = 0; i < layouts.Length; i++)
            {
                if (layouts[i] != null && layouts[i].ViewMode == ChainlingPartyViewMode.Moves)
                    movesViews.Add(layouts[i]);
            }

            return movesViews.ToArray();
        }

        private static bool EnsureMovesView(
            ChainlingPartyInfoViewLayout infoLayout,
            PartyTabMoveRarityPaletteSO rarityPalette,
            TypeIconPaletteSO typeIconPalette,
            TMP_FontAsset rowFont,
            Sprite rowBackground)
        {
            if (infoLayout == null)
                return false;

            ChainlingPartyMovesViewLayout movesLayout = infoLayout.GetComponent<ChainlingPartyMovesViewLayout>();
            if (movesLayout == null)
                movesLayout = Undo.AddComponent<ChainlingPartyMovesViewLayout>(infoLayout.gameObject);

            SerializedObject serializedMovesLayout = new SerializedObject(movesLayout);
            serializedMovesLayout.FindProperty("typeIconPalette").objectReferenceValue = typeIconPalette;
            serializedMovesLayout.FindProperty("rarityPalette").objectReferenceValue = rarityPalette;
            serializedMovesLayout.FindProperty("rowFont").objectReferenceValue = rowFont;
            serializedMovesLayout.FindProperty("rowFontSize").floatValue = 18f;
            serializedMovesLayout.FindProperty("rowHeight").floatValue = DefaultRowHeight;
            serializedMovesLayout.FindProperty("rowSpacing").floatValue = DefaultRowSpacing;
            serializedMovesLayout.FindProperty("visibleRowsBeforeScroll").intValue = MoveLoadoutSlotRules.VisibleRowsBeforeScroll;
            serializedMovesLayout.FindProperty("maxAuthoredRows").intValue = MoveLoadoutSlotRules.MaxMoveSlots;
            serializedMovesLayout.FindProperty("preserveAuthoredViewportRect").boolValue = true;
            serializedMovesLayout.FindProperty("autoScrollOverflowRows").boolValue = true;
            serializedMovesLayout.FindProperty("autoScrollPixelsPerSecond").floatValue = 18f;
            serializedMovesLayout.FindProperty("autoScrollEdgeHoldSeconds").floatValue = 0.8f;
            serializedMovesLayout.ApplyModifiedPropertiesWithoutUndo();

            bool preserveAuthoredViewport =
                serializedMovesLayout.FindProperty("preserveAuthoredViewportRect") != null &&
                serializedMovesLayout.FindProperty("preserveAuthoredViewportRect").boolValue;

            TMP_Text slotCountText = EnsureSlotCountText(infoLayout.transform, rowFont, preserveAuthoredViewport);
            RectTransform viewportRoot = EnsureMoveRowsViewport(infoLayout.transform, preserveAuthoredViewport);
            RectTransform moveRowsRoot = EnsureMoveRowsRoot(infoLayout.transform, viewportRoot);
            ScrollRect scrollRect = viewportRoot.GetComponent<ScrollRect>();
            if (scrollRect != null)
            {
                scrollRect.viewport = viewportRoot;
                scrollRect.content = moveRowsRoot;
            }
            LayoutElement viewportLayout = viewportRoot.GetComponent<LayoutElement>();
            serializedMovesLayout.FindProperty("slotCountText").objectReferenceValue = slotCountText;
            serializedMovesLayout.FindProperty("viewportRoot").objectReferenceValue = viewportRoot;
            serializedMovesLayout.FindProperty("scrollRect").objectReferenceValue = scrollRect;
            serializedMovesLayout.FindProperty("viewportLayoutElement").objectReferenceValue = viewportLayout;
            serializedMovesLayout.FindProperty("moveRowsRoot").objectReferenceValue = moveRowsRoot;

            if (infoLayout.transform.Find("Lines") is RectTransform linesRoot)
                linesRoot.gameObject.SetActive(false);

            PruneOverflowAuthoredRows(moveRowsRoot, MoveLoadoutSlotRules.VisibleRowsBeforeScroll);

            var authoredRows = new ChainlingPartyMoveRowDisplay[MoveLoadoutSlotRules.VisibleRowsBeforeScroll];
            for (int i = 0; i < authoredRows.Length; i++)
            {
                string rowName = $"MoveRow_{i + 1:00}";
                Transform existing = moveRowsRoot.Find(rowName);
                bool createdRow = existing == null;
                GameObject rowGo = !createdRow ? existing.gameObject : CreateMoveRow(moveRowsRoot, rowName, rowFont, rowBackground);
                authoredRows[i] = rowGo.GetComponent<ChainlingPartyMoveRowDisplay>();
                if (authoredRows[i] == null)
                {
                    authoredRows[i] = Undo.AddComponent<ChainlingPartyMoveRowDisplay>(rowGo);
                    createdRow = true;
                }

                if (createdRow)
                    authoredRows[i].ApplyEditorDefaults(rowFont, DefaultRowHeight);
                else
                {
                    authoredRows[i].CaptureLayoutFromChildren();
                    authoredRows[i].ApplySerializedPresentation();
                }
            }

            SerializedProperty rowsProperty = serializedMovesLayout.FindProperty("authoredMoveRows");
            rowsProperty.ClearArray();
            for (int i = 0; i < authoredRows.Length; i++)
            {
                rowsProperty.InsertArrayElementAtIndex(i);
                rowsProperty.GetArrayElementAtIndex(i).objectReferenceValue = authoredRows[i];
            }

            serializedMovesLayout.ApplyModifiedPropertiesWithoutUndo();
            // Do not call BuildDefaultLayoutForEditor here — it ApplyEditorDefaults every
            // authored row and wipes Column_1 formatting after copy / authored edits.
            EditorUtility.SetDirty(infoLayout);
            EditorUtility.SetDirty(movesLayout);
            return true;
        }

        private static TMP_Text EnsureSlotCountText(
            Transform parent,
            TMP_FontAsset rowFont,
            bool preserveAuthoredRect)
        {
            Transform existing = parent.Find("SlotCountText");
            TMP_Text text = existing != null ? existing.GetComponent<TMP_Text>() : null;
            bool created = text == null;
            if (created)
            {
                text = CreateText("SlotCountText", parent, "0/4 moves equipped", rowFont, TextAlignmentOptions.MidlineLeft);
                Undo.RegisterCreatedObjectUndo(text.gameObject, "Create Moves Slot Count Text");
            }

            if (created || !preserveAuthoredRect)
            {
                RectTransform rect = text.rectTransform;
                rect.anchorMin = new Vector2(0.04f, 0.82f);
                rect.anchorMax = new Vector2(0.96f, 0.98f);
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
            }

            text.fontSize = 18f;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.color = PlayerMenuUiFactory.Text;
            if (rowFont != null)
                text.font = rowFont;
            return text;
        }

        private static RectTransform EnsureMoveRowsViewport(Transform parent, bool preserveAuthoredRect)
        {
            Transform existing = parent.Find("MoveRowsViewport");
            RectTransform rect = existing as RectTransform;
            bool created = rect == null;
            if (created)
            {
                GameObject go = new GameObject(
                    "MoveRowsViewport",
                    typeof(RectTransform),
                    typeof(RectMask2D),
                    typeof(LayoutElement),
                    typeof(ScrollRect));
                rect = go.GetComponent<RectTransform>();
                rect.SetParent(parent, false);
                Undo.RegisterCreatedObjectUndo(go, "Create MoveRowsViewport");
            }

            if (created || !preserveAuthoredRect)
            {
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.offsetMin = new Vector2(8f, -8f - ResolveDefaultViewportHeight());
                rect.offsetMax = new Vector2(-8f, -8f);
                rect.pivot = new Vector2(0.5f, 1f);
            }

            if (rect.GetComponent<RectMask2D>() == null)
                rect.gameObject.AddComponent<RectMask2D>();

            LayoutElement layoutElement = rect.GetComponent<LayoutElement>();
            if (layoutElement == null)
                layoutElement = rect.gameObject.AddComponent<LayoutElement>();
            if (created || !preserveAuthoredRect || layoutElement.preferredHeight <= 0f)
            {
                float height = ResolveDefaultViewportHeight();
                layoutElement.preferredHeight = height;
                layoutElement.minHeight = height;
            }

            ScrollRect scrollRect = rect.GetComponent<ScrollRect>();
            if (scrollRect == null)
                scrollRect = rect.gameObject.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.inertia = false;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.viewport = rect;

            return rect;
        }

        private static float ResolveDefaultViewportHeight()
        {
            return (MoveLoadoutSlotRules.VisibleRowsBeforeScroll * DefaultRowHeight)
                + ((MoveLoadoutSlotRules.VisibleRowsBeforeScroll - 1) * DefaultRowSpacing);
        }

        private static void PruneOverflowAuthoredRows(RectTransform moveRowsRoot, int keepCount)
        {
            if (moveRowsRoot == null)
                return;

            for (int i = moveRowsRoot.childCount - 1; i >= 0; i--)
            {
                Transform child = moveRowsRoot.GetChild(i);
                if (child == null || !TryGetMoveRowIndex(child.name, out int rowIndex) || rowIndex <= keepCount)
                    continue;

                Object.DestroyImmediate(child.gameObject);
            }
        }

        private static bool TryGetMoveRowIndex(string rowName, out int rowIndex)
        {
            rowIndex = 0;
            const string prefix = "MoveRow_";
            if (string.IsNullOrWhiteSpace(rowName) || !rowName.StartsWith(prefix))
                return false;

            return int.TryParse(rowName.Substring(prefix.Length), out rowIndex);
        }

        private static RectTransform EnsureMoveRowsRoot(Transform parent, RectTransform viewportRoot)
        {
            Transform existing = viewportRoot != null ? viewportRoot.Find("MoveRows") : null;
            if (existing == null)
                existing = parent.Find("MoveRows");
            if (existing != null)
            {
                RectTransform existingRect = existing as RectTransform;
                if (viewportRoot != null && existingRect.parent != viewportRoot)
                    existingRect.SetParent(viewportRoot, false);
                ConfigureMoveRowsRoot(existingRect);
                return existingRect;
            }

            GameObject go = new GameObject(
                "MoveRows",
                typeof(RectTransform),
                typeof(VerticalLayoutGroup),
                typeof(ContentSizeFitter));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(viewportRoot != null ? viewportRoot : parent, false);
            ConfigureMoveRowsRoot(rect);

            VerticalLayoutGroup layout = go.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 4f;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            ContentSizeFitter fitter = go.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            Undo.RegisterCreatedObjectUndo(go, "Create MoveRows");
            return rect;
        }

        private static void ConfigureMoveRowsRoot(RectTransform rect)
        {
            if (rect == null)
                return;

            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.anchoredPosition = Vector2.zero;
        }

        private static GameObject CreateMoveRow(
            RectTransform parent,
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
            rowRect.localScale = Vector3.one;

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
            iconLayout.minWidth = 22f;
            iconLayout.minHeight = 22f;

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
            serializedRow.FindProperty("moveNameFontSize").floatValue = 18f;
            serializedRow.FindProperty("statFontSize").floatValue = 18f;
            serializedRow.FindProperty("preferredRowHeight").floatValue = DefaultRowHeight;
            serializedRow.FindProperty("contentPaddingLeft").intValue = 4;
            serializedRow.FindProperty("contentPaddingRight").intValue = 4;
            serializedRow.FindProperty("contentPaddingTop").intValue = 0;
            serializedRow.FindProperty("contentPaddingBottom").intValue = 0;
            serializedRow.FindProperty("contentSpacing").floatValue = 6f;
            serializedRow.FindProperty("contentAlignment").enumValueIndex = (int)TextAnchor.MiddleLeft;
            serializedRow.FindProperty("typeIconPreferredWidth").floatValue = 22f;
            serializedRow.FindProperty("typeIconPreferredHeight").floatValue = 22f;
            serializedRow.FindProperty("moveNameMinWidth").floatValue = 40f;
            serializedRow.FindProperty("moveNameFlexibleWidth").floatValue = 1f;
            serializedRow.FindProperty("statPreferredWidth").floatValue = 44f;
            serializedRow.FindProperty("statMinWidth").floatValue = 36f;
            serializedRow.ApplyModifiedPropertiesWithoutUndo();

            Undo.RegisterCreatedObjectUndo(rowGo, "Create Move Row");
            return rowGo;
        }

        private static RectTransform CreateStretchRect(string name, Transform parent, float left, float bottom, float right, float top)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
            rect.pivot = new Vector2(0.5f, 0.5f);
            return rect;
        }

        private static Image CreateStretchImage(string name, Transform parent, Color color)
        {
            Image image = CreateImage(name, parent, color);
            RectTransform rect = image.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
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

        private static ChainlingPartyMoveRowDisplay FindTemplateRow(ChainlingPartyInfoViewLayout movesView)
        {
            if (movesView == null)
                return null;

            Transform row = movesView.transform.Find("MoveRowsViewport/MoveRows/MoveRow_01")
                ?? movesView.transform.Find("MoveRows/MoveRow_01");
            return row != null
                ? row.GetComponent<ChainlingPartyMoveRowDisplay>()
                : movesView.GetComponentInChildren<ChainlingPartyMoveRowDisplay>(true);
        }

        private static ChainlingPartyInfoViewLayout ReplaceMovesView(
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
            ChainlingPartyInfoPanelBinder binder = target.GetComponentInParent<ChainlingPartyInfoPanelBinder>(true);

            // Full Instantiation preserves TMP, LayoutElements, sprites, viewport rects, and component fields.
            GameObject clone = Object.Instantiate(source.gameObject, parent);
            clone.name = targetName;
            clone.transform.SetSiblingIndex(siblingIndex);
            clone.SetActive(wasActive);

            CopyRectTransform(source.transform as RectTransform, clone.transform as RectTransform);

            Object.DestroyImmediate(target.gameObject);

            ChainlingPartyInfoViewLayout clonedLayout = clone.GetComponent<ChainlingPartyInfoViewLayout>();
            if (clonedLayout != null)
            {
                clonedLayout.ConfigureMode(ChainlingPartyViewMode.Moves);
                DestroyChildIfPresent(clonedLayout.transform, "Lines");
                EditorUtility.SetDirty(clonedLayout);
            }

            ChainlingPartyMovesViewLayout movesLayout = clone.GetComponent<ChainlingPartyMovesViewLayout>();
            if (movesLayout != null)
            {
                RebindAuthoredMoveRows(movesLayout);
                movesLayout.CopyFirstRowPresentationToAuthoredRows();
                EditorUtility.SetDirty(movesLayout);
            }

            RefreshBinderViewReferences(binder);
            return clonedLayout;
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

        private static ChainlingPartyInfoViewLayout FindColumnOneMovesView(ChainlingPartyInfoViewLayout[] movesViews)
        {
            if (movesViews == null)
                return null;

            for (int i = 0; i < movesViews.Length; i++)
            {
                ChainlingPartyInfoViewLayout view = movesViews[i];
                if (view == null)
                    continue;

                Transform cursor = view.transform;
                while (cursor != null)
                {
                    if (cursor.name == "Column_1")
                        return view;
                    cursor = cursor.parent;
                }
            }

            return null;
        }
    }

    [CustomEditor(typeof(ChainlingPartyMovesViewLayout))]
    public sealed class ChainlingPartyMovesViewLayoutEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space(8f);
            EditorGUILayout.HelpBox(
                "Author MovesView on Column_1 (viewport + MoveRow_01 template). " +
                "Copy pushes the full hierarchy to Columns 2 & 3. " +
                "Runtime overflow rows Instantiate MoveRow_01.",
                MessageType.Info);
            if (GUILayout.Button("Copy Column 1 Moves View → Columns 2 & 3"))
                PartyTabMovesViewPrefabUtility.CopySlotOneMovesViewToPartyColumns();

            if (GUILayout.Button("Ensure Moves View Layout"))
            {
                var layout = (ChainlingPartyMovesViewLayout)target;
                Undo.RegisterFullObjectHierarchyUndo(layout.gameObject, "Ensure Moves View Layout");
                layout.BuildDefaultLayoutForEditor();
                EditorUtility.SetDirty(layout);
            }

            if (GUILayout.Button("Copy MoveRow_01 Presentation To Authored Rows"))
            {
                var layout = (ChainlingPartyMovesViewLayout)target;
                Undo.RegisterFullObjectHierarchyUndo(layout.gameObject, "Copy Move Row Presentation");
                layout.CopyFirstRowPresentationToAuthoredRows();
                EditorUtility.SetDirty(layout);
            }
        }
    }

    [CustomEditor(typeof(ChainlingPartyMoveRowDisplay))]
    public sealed class ChainlingPartyMoveRowDisplayEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space(8f);

            var row = (ChainlingPartyMoveRowDisplay)target;

            if (GUILayout.Button("Capture Layout From Child Components"))
            {
                Undo.RecordObject(row, "Capture Move Row Layout");
                row.CaptureLayoutFromChildren();
                EditorUtility.SetDirty(row);
            }

            if (GUILayout.Button("Apply Serialized Layout To This Row"))
            {
                Undo.RegisterFullObjectHierarchyUndo(row.gameObject, "Apply Move Row Layout");
                row.ApplySerializedPresentation();
                EditorUtility.SetDirty(row);
            }

            if (GUILayout.Button("Copy This Row Presentation To Sibling Move Rows"))
            {
                ChainlingPartyMovesViewLayout layout = row.GetComponentInParent<ChainlingPartyMovesViewLayout>(true);
                if (layout == null)
                {
                    EditorUtility.DisplayDialog(
                        "Move Row Copy",
                        "This row is not under a ChainlingPartyMovesViewLayout.",
                        "OK");
                    return;
                }

                Undo.RegisterFullObjectHierarchyUndo(layout.gameObject, "Copy Move Row Presentation");
                ChainlingPartyMoveRowDisplay[] rows = layout.GetComponentsInChildren<ChainlingPartyMoveRowDisplay>(true);
                for (int i = 0; i < rows.Length; i++)
                {
                    if (rows[i] == null || rows[i] == row)
                        continue;

                    rows[i].CopyPresentationFrom(row);
                    EditorUtility.SetDirty(rows[i]);
                }

                EditorUtility.SetDirty(layout);
            }
        }
    }
}
#endif
