#if UNITY_EDITOR
using Chainbound.Combat;
using SignalJam.UI.MenuNavigation.PartyTab;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace SignalJam.UI.MenuNavigation.Editor
{
    /// <summary>
    /// Authors AttributesView radar hierarchy on party prefabs. Play mode never Instantiates these.
    /// </summary>
    [InitializeOnLoad]
    public static class PartyTabAttributesViewPrefabUtility
    {
        private const string PartyPrefabPath = "Assets/Prefabs/UI/PlayerMenu/ChainlingsPartyView.prefab";
        private const string TypeChromePalettePath = "Assets/Data/UI/PartyTab/ChainlingPartyTypeChromePalette.asset";
        private const string PixeloidFontPath = "Assets/Resources/TextMesh Pro/Fonts/PixeloidSans SDF.asset";
        private const string AttributeIconsFolder = "Assets/Sprites/UI/PlayerMenu/AttributeIcons";

        // Vertex order matches ChainlingAttributeDisplayUtility.PartyRadarAxisLabels (−90° rotation).
        private static readonly string[] AxisLabels = ChainlingAttributeDisplayUtility.PartyRadarAxisLabels;
        private static readonly string[] IconFileNames =
        {
            "speed.png",
            "melee_def.png",
            "melee_atk.png",
            "ranged_atk.png",
            "ranged_def.png",
        };

        static PartyTabAttributesViewPrefabUtility()
        {
            EditorApplication.delayCall += TryEnsureAttributesRadarOnLoad;
        }

        private static void TryEnsureAttributesRadarOnLoad()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            if (!PrefabNeedsAttributesRadar(PartyPrefabPath))
                return;

            EnsureAttributesRadarInMenuPrefabs();
            CopyColumn1AttributesViewToOtherColumns();
        }

        private static bool PrefabNeedsAttributesRadar(string prefabPath)
        {
            GameObject prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefabRoot == null)
                return false;

            ChainlingPartyInfoViewLayout[] layouts =
                prefabRoot.GetComponentsInChildren<ChainlingPartyInfoViewLayout>(true);
            for (int i = 0; i < layouts.Length; i++)
            {
                ChainlingPartyInfoViewLayout layout = layouts[i];
                if (layout == null || layout.ViewMode != ChainlingPartyViewMode.Attributes)
                    continue;

                ChainlingPartyAttributesViewLayout attributesLayout =
                    layout.GetComponent<ChainlingPartyAttributesViewLayout>();
                if (attributesLayout == null)
                    return true;

                if (layout.transform.Find("RadarRoot") == null)
                    return true;

                if (layout.transform.Find("Axes") == null)
                    return true;

                if (AttributesLayoutMissingIconSprites(attributesLayout))
                    return true;

                if (AttributesLayoutHasWrongAxisOrder(attributesLayout))
                    return true;

                // Legacy leftover surfaces — clean via Ensure.
                if (layout.transform.Find("Lines") != null ||
                    layout.transform.Find("ProgressionFooter") != null)
                    return true;
            }

            return false;
        }

        private static bool AttributesLayoutMissingIconSprites(ChainlingPartyAttributesViewLayout layout)
        {
            SerializedObject so = new SerializedObject(layout);
            SerializedProperty icons = so.FindProperty("attributeIcons");
            if (icons == null || !icons.isArray || icons.arraySize < AxisLabels.Length)
                return true;

            for (int i = 0; i < AxisLabels.Length; i++)
            {
                if (icons.GetArrayElementAtIndex(i).objectReferenceValue == null)
                    return true;
            }

            return false;
        }

        private static bool AttributesLayoutHasWrongAxisOrder(ChainlingPartyAttributesViewLayout layout)
        {
            SerializedObject so = new SerializedObject(layout);
            SerializedProperty axes = so.FindProperty("axes");
            if (axes == null || !axes.isArray || axes.arraySize != AxisLabels.Length)
                return true;

            for (int i = 0; i < AxisLabels.Length; i++)
            {
                string label = axes.GetArrayElementAtIndex(i).FindPropertyRelative("label").stringValue;
                if (!string.Equals(label, AxisLabels[i], System.StringComparison.Ordinal))
                    return true;
            }

            Transform axesRoot = layout.transform.Find("Axes");
            if (axesRoot == null)
                return true;

            for (int i = 0; i < AxisLabels.Length; i++)
            {
                if (axesRoot.Find($"Axis_{AxisLabels[i]}") == null)
                    return true;
            }

            return false;
        }

        [MenuItem("Tools/Chainbound/UI/Player Menu Dev/Ensure Party Tab Attributes Radar View", priority = 45)]
        public static void EnsureAttributesRadarInMenuPrefabs()
        {
            int updated = EnsurePrefab(PartyPrefabPath) ? 1 : 0;
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[PartyTabAttributesViewPrefabUtility] Ensured Attributes radar on {updated} prefab(s).");
        }

        [MenuItem("Tools/Chainbound/UI/Player Menu Dev/Copy Column 1 Attributes View to Party Columns", priority = 46)]
        public static void CopyColumn1AttributesViewToOtherColumns()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PartyPrefabPath);
            try
            {
                ChainlingPartyInfoViewLayout[] attributesViews = FindAttributesViewLayouts(root);
                ChainlingPartyInfoViewLayout source =
                    FindColumnOneAttributesView(attributesViews) ?? FindAttributesLayout(root, "Column_1");
                if (source == null)
                {
                    Debug.LogError(
                        "[PartyTabAttributesViewPrefabUtility] Column_1 AttributesView not found under " +
                        "ChainlingsPartyView → Column_1 → … → AttributesView.");
                    return;
                }

                if (source.GetComponent<ChainlingPartyAttributesViewLayout>() == null)
                    EnsureAttributesLayout(source);

                int copied = 0;
                for (int i = 0; i < attributesViews.Length; i++)
                {
                    ChainlingPartyInfoViewLayout target = attributesViews[i];
                    if (target == null || target == source ||
                        !IsUnderPartyColumn(target.transform, out string columnName))
                        continue;

                    if (columnName != "Column_2" && columnName != "Column_3")
                        continue;

                    if (ReplaceAttributesView(source, target) != null)
                        copied++;
                }

                PrefabUtility.SaveAsPrefabAsset(root, PartyPrefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log(
                    $"[PartyTabAttributesViewPrefabUtility] Copied Column_1 AttributesView " +
                    $"(RadarRoot/Axes + authored layout/components) to {copied} party column(s). " +
                    "Lines/ProgressionFooter are not created.");
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
                Debug.LogWarning($"[PartyTabAttributesViewPrefabUtility] Prefab missing: {prefabPath}");
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
                    if (layouts[i] == null || layouts[i].ViewMode != ChainlingPartyViewMode.Attributes)
                        continue;
                    dirty |= EnsureAttributesLayout(layouts[i]);
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

        private static ChainlingPartyInfoViewLayout FindAttributesLayout(GameObject root, string columnName)
        {
            Transform column = FindDeep(root.transform, columnName);
            if (column == null)
                return null;

            Transform attributes = FindDeep(column, "AttributesView");
            return attributes != null ? attributes.GetComponent<ChainlingPartyInfoViewLayout>() : null;
        }

        private static ChainlingPartyInfoViewLayout[] FindAttributesViewLayouts(GameObject root)
        {
            ChainlingPartyInfoViewLayout[] all =
                root.GetComponentsInChildren<ChainlingPartyInfoViewLayout>(true);
            var matches = new System.Collections.Generic.List<ChainlingPartyInfoViewLayout>();
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].ViewMode == ChainlingPartyViewMode.Attributes)
                    matches.Add(all[i]);
            }

            return matches.ToArray();
        }

        private static ChainlingPartyInfoViewLayout FindColumnOneAttributesView(
            ChainlingPartyInfoViewLayout[] attributesViews)
        {
            if (attributesViews == null)
                return null;

            for (int i = 0; i < attributesViews.Length; i++)
            {
                ChainlingPartyInfoViewLayout view = attributesViews[i];
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
        /// Full AttributesView clone into the target column's Views hierarchy (Moves-style replace).
        /// Preserves sibling index, name, active state; refreshes InfoPanelBinder viewLayouts.
        /// </summary>
        private static ChainlingPartyInfoViewLayout ReplaceAttributesView(
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

            // Copy RectTransform layout from source onto the clone path by instantiating whole view.
            // Full Instantiation preserves TMP font sizes, Graphic colors, RectTransforms, and component fields.
            GameObject clone = Object.Instantiate(source.gameObject, parent);
            clone.name = targetName;
            clone.transform.SetSiblingIndex(siblingIndex);
            clone.SetActive(wasActive);

            Object.DestroyImmediate(target.gameObject);

            ChainlingPartyInfoViewLayout clonedLayout = clone.GetComponent<ChainlingPartyInfoViewLayout>();
            if (clonedLayout != null)
            {
                clonedLayout.ConfigureMode(ChainlingPartyViewMode.Attributes);
                EditorUtility.SetDirty(clonedLayout);
            }

            ChainlingPartyAttributesViewLayout attributesLayout =
                clone.GetComponent<ChainlingPartyAttributesViewLayout>();
            if (attributesLayout != null)
            {
                RemoveLegacyAttributesSurfaces(clone.transform);
                ClearProgressionFooterReference(attributesLayout);
                EditorUtility.SetDirty(attributesLayout);
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

        private static bool EnsureAttributesLayout(ChainlingPartyInfoViewLayout viewLayout)
        {
            if (viewLayout == null)
                return false;

            Transform root = viewLayout.transform;
            bool dirty = false;

            ChainlingPartyAttributesViewLayout layout =
                viewLayout.GetComponent<ChainlingPartyAttributesViewLayout>();
            if (layout == null)
            {
                layout = viewLayout.gameObject.AddComponent<ChainlingPartyAttributesViewLayout>();
                dirty = true;
            }

            dirty |= RemoveLegacyAttributesSurfaces(root);

            RectTransform radarRoot = EnsureRect(root, "RadarRoot", out bool radarRootNew);
            dirty |= radarRootNew;
            // Only apply default stretch when creating — preserve authored RectTransforms.
            if (radarRootNew)
                Stretch(radarRoot, 12f, 28f, 12f, 36f);

            PartyTabAttributeRadarGraphic glow = EnsureRadarGraphic(
                radarRoot,
                "Glow",
                glowMode: true,
                out bool glowNew);
            dirty |= glowNew;
            glow.raycastTarget = false;

            PartyTabAttributeRadarGraphic radar = EnsureRadarGraphic(
                radarRoot,
                "Radar",
                glowMode: false,
                out bool radarNew);
            dirty |= radarNew;
            radar.raycastTarget = false;

            RectTransform axesRoot = EnsureRect(root, "Axes", out bool axesNew);
            dirty |= axesNew;
            if (axesNew)
                Stretch(axesRoot, 0f, 0f, 0f, 0f);
            dirty |= RemoveStaleAxisChildren(axesRoot);

            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(PixeloidFontPath);
            var axisSlots = new ChainlingPartyAttributesViewLayout.AxisSlot[AxisLabels.Length];
            var icons = new Sprite[AxisLabels.Length];

            for (int i = 0; i < AxisLabels.Length; i++)
            {
                string axisName = $"Axis_{AxisLabels[i]}";
                RectTransform axis = EnsureRect(axesRoot, axisName, out bool axisCreated);
                dirty |= axisCreated;
                axis.SetSiblingIndex(i);
                if (axisCreated)
                {
                    axis.sizeDelta = new Vector2(56f, 40f);
                    PlaceAxis(axis, i, 92f);
                }

                Image icon = EnsureImage(axis, "Icon", out bool iconNew);
                dirty |= iconNew;
                if (iconNew)
                {
                    icon.rectTransform.sizeDelta = new Vector2(22f, 22f);
                    icon.rectTransform.anchoredPosition = new Vector2(0f, 8f);
                    icon.preserveAspect = true;
                }

                icons[i] = LoadAttributeIcon(IconFileNames[i]);
                if (icons[i] != null && icon.sprite != icons[i])
                {
                    icon.sprite = icons[i];
                    dirty = true;
                }

                CanvasGroup iconGroup = icon.GetComponent<CanvasGroup>();
                if (iconGroup == null)
                {
                    iconGroup = icon.gameObject.AddComponent<CanvasGroup>();
                    dirty = true;
                }

                TMP_Text value = EnsureText(axis, "Value", font, out bool valueNew);
                dirty |= valueNew;
                // Defaults only on create — keep inspector fontSize, alignment, color, RectTransform.
                if (valueNew)
                {
                    value.fontSize = 16f;
                    value.alignment = TextAlignmentOptions.Center;
                    value.text = "0";
                    value.rectTransform.sizeDelta = new Vector2(48f, 18f);
                    value.rectTransform.anchoredPosition = new Vector2(0f, -12f);
                }

                axisSlots[i] = new ChainlingPartyAttributesViewLayout.AxisSlot
                {
                    label = AxisLabels[i],
                    icon = icon,
                    valueLabel = value,
                    iconCanvasGroup = iconGroup,
                    motionTarget = icon.rectTransform,
                };
            }

            ChainlingPartyTypeChromePaletteSO palette =
                AssetDatabase.LoadAssetAtPath<ChainlingPartyTypeChromePaletteSO>(TypeChromePalettePath);

            SerializedObject so = new SerializedObject(layout);
            so.FindProperty("radarGraphic").objectReferenceValue = radar;
            so.FindProperty("glowGraphic").objectReferenceValue = glow;
            if (palette != null && so.FindProperty("typeChromePalette").objectReferenceValue == null)
                so.FindProperty("typeChromePalette").objectReferenceValue = palette;
            so.FindProperty("progressionFooter").objectReferenceValue = null;
            // Do not force repositionAxes / animation fields — preserve inspector authoring.

            SerializedProperty axesProp = so.FindProperty("axes");
            axesProp.arraySize = axisSlots.Length;
            for (int i = 0; i < axisSlots.Length; i++)
            {
                SerializedProperty element = axesProp.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("label").stringValue = axisSlots[i].label;
                element.FindPropertyRelative("icon").objectReferenceValue = axisSlots[i].icon;
                element.FindPropertyRelative("valueLabel").objectReferenceValue = axisSlots[i].valueLabel;
                element.FindPropertyRelative("iconCanvasGroup").objectReferenceValue = axisSlots[i].iconCanvasGroup;
                element.FindPropertyRelative("motionTarget").objectReferenceValue = axisSlots[i].motionTarget;
            }

            SerializedProperty iconsProp = so.FindProperty("attributeIcons");
            iconsProp.arraySize = icons.Length;
            for (int i = 0; i < icons.Length; i++)
            {
                if (icons[i] != null)
                    iconsProp.GetArrayElementAtIndex(i).objectReferenceValue = icons[i];
            }

            so.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(layout);
            EditorUtility.SetDirty(viewLayout);
            return true;
        }

        /// <summary>
        /// Removes legacy AttributesView children that the radar layout no longer uses.
        /// </summary>
        private static bool RemoveLegacyAttributesSurfaces(Transform attributesRoot)
        {
            if (attributesRoot == null)
                return false;

            bool dirty = false;
            dirty |= DestroyChildIfPresent(attributesRoot, "Lines");
            dirty |= DestroyChildIfPresent(attributesRoot, "ProgressionFooter");
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

        private static void ClearProgressionFooterReference(ChainlingPartyAttributesViewLayout layout)
        {
            if (layout == null)
                return;

            SerializedObject so = new SerializedObject(layout);
            SerializedProperty footer = so.FindProperty("progressionFooter");
            if (footer != null && footer.objectReferenceValue != null)
            {
                footer.objectReferenceValue = null;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static PartyTabAttributeRadarGraphic EnsureRadarGraphic(
            Transform parent,
            string name,
            bool glowMode,
            out bool created)
        {
            Transform existing = parent.Find(name);
            PartyTabAttributeRadarGraphic graphic = existing != null
                ? existing.GetComponent<PartyTabAttributeRadarGraphic>()
                : null;
            if (graphic != null)
            {
                created = false;
                // Preserve authored Graphic / radar inspector settings on existing components.
                return graphic;
            }

            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(PartyTabAttributeRadarGraphic));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            Stretch(rect, 0f, 0f, 0f, 0f);
            graphic = go.GetComponent<PartyTabAttributeRadarGraphic>();
            ConfigureRadarGraphicDefaults(graphic, glowMode);
            created = true;
            return graphic;
        }

        private static void ConfigureRadarGraphicDefaults(PartyTabAttributeRadarGraphic graphic, bool glowMode)
        {
            SerializedObject so = new SerializedObject(graphic);
            so.FindProperty("m_Color").colorValue = Color.white;
            so.FindProperty("glowMode").boolValue = glowMode;
            so.FindProperty("drawGrid").boolValue = !glowMode;
            so.FindProperty("drawFill").boolValue = !glowMode;
            so.FindProperty("drawDataStroke").boolValue = true;
            so.FindProperty("outerRadius").floatValue = 70f;
            so.FindProperty("dataStrokeWidth").floatValue = 2.5f;
            so.FindProperty("glowStrokeWidth").floatValue = 9f;
            so.FindProperty("glowColorAlpha").floatValue = 0.32f;
            so.FindProperty("gridRingCount").intValue = 2;
            so.FindProperty("drawSpokes").boolValue = true;
            so.FindProperty("strokeLighten01").floatValue = 0.35f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Matches PartyTabAttributeRadarGraphic default (−90° + index × 72° CCW).
        /// </summary>
        private static void PlaceAxis(RectTransform axis, int index, float radius)
        {
            float step = 360f / AxisLabels.Length;
            float angle = (-90f + index * step) * Mathf.Deg2Rad;
            axis.anchorMin = new Vector2(0.5f, 0.5f);
            axis.anchorMax = new Vector2(0.5f, 0.5f);
            axis.pivot = new Vector2(0.5f, 0.5f);
            axis.anchoredPosition = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        }

        private static bool RemoveStaleAxisChildren(Transform axesRoot)
        {
            if (axesRoot == null)
                return false;

            bool dirty = false;
            for (int i = axesRoot.childCount - 1; i >= 0; i--)
            {
                Transform child = axesRoot.GetChild(i);
                if (child == null || !child.name.StartsWith("Axis_", System.StringComparison.Ordinal))
                    continue;

                bool keep = false;
                for (int a = 0; a < AxisLabels.Length; a++)
                {
                    if (child.name == $"Axis_{AxisLabels[a]}")
                    {
                        keep = true;
                        break;
                    }
                }

                if (!keep)
                {
                    Object.DestroyImmediate(child.gameObject);
                    dirty = true;
                }
            }

            return dirty;
        }

        private static RectTransform EnsureRect(Transform parent, string name, out bool created)
        {
            Transform existing = parent.Find(name);
            if (existing is RectTransform rect)
            {
                created = false;
                return rect;
            }

            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            created = true;
            return go.GetComponent<RectTransform>();
        }

        private static Image EnsureImage(Transform parent, string name, out bool created)
        {
            Transform existing = parent.Find(name);
            Image image = existing != null ? existing.GetComponent<Image>() : null;
            if (image != null)
            {
                created = false;
                return image;
            }

            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            created = true;
            return go.GetComponent<Image>();
        }

        private static TMP_Text EnsureText(Transform parent, string name, TMP_FontAsset font, out bool created)
        {
            Transform existing = parent.Find(name);
            TMP_Text text = existing != null ? existing.GetComponent<TMP_Text>() : null;
            if (text != null)
            {
                created = false;
                if (font != null && text.font == null)
                    text.font = font;
                return text;
            }

            GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
            if (font != null)
                tmp.font = font;
            tmp.color = Color.white;
            tmp.raycastTarget = false;
            RectTransform rect = tmp.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            created = true;
            return tmp;
        }

        private static void Stretch(RectTransform rect, float left, float top, float right, float bottom)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
            rect.pivot = new Vector2(0.5f, 0.5f);
        }

        private static Sprite LoadAttributeIcon(string fileName)
        {
            string path = $"{AttributeIconsFolder}/{fileName}";
            EnsureSpriteImportSettings(path);

            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite != null)
                return sprite;

            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
            for (int i = 0; i < assets.Length; i++)
            {
                if (assets[i] is Sprite s)
                    return s;
            }

            Debug.LogWarning($"[PartyTabAttributesViewPrefabUtility] Attribute icon missing or not a Sprite: {path}");
            return null;
        }

        private static void EnsureSpriteImportSettings(string assetPath)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
                return;

            bool dirty = false;
            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                dirty = true;
            }

            if (importer.spriteImportMode != SpriteImportMode.Single)
            {
                importer.spriteImportMode = SpriteImportMode.Single;
                dirty = true;
            }

            if (importer.mipmapEnabled)
            {
                importer.mipmapEnabled = false;
                dirty = true;
            }

            if (!importer.alphaIsTransparency)
            {
                importer.alphaIsTransparency = true;
                dirty = true;
            }

            if (importer.filterMode != FilterMode.Point)
            {
                importer.filterMode = FilterMode.Point;
                dirty = true;
            }

            if (importer.npotScale != TextureImporterNPOTScale.None)
            {
                importer.npotScale = TextureImporterNPOTScale.None;
                dirty = true;
            }

            if (dirty)
            {
                importer.SaveAndReimport();
            }
        }

        private static Transform FindDeep(Transform root, string name)
        {
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

    [CustomEditor(typeof(ChainlingPartyAttributesViewLayout))]
    public sealed class ChainlingPartyAttributesViewLayoutEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space(8f);
            EditorGUILayout.HelpBox(
                "Copies Column_1 AttributesView (full authored hierarchy + component settings) onto Column_2 and Column_3. Does not create Lines or ProgressionFooter.",
                MessageType.Info);
            if (GUILayout.Button("Copy Column 1 Attributes View → Columns 2 & 3"))
                PartyTabAttributesViewPrefabUtility.CopyColumn1AttributesViewToOtherColumns();
        }
    }
}
#endif
