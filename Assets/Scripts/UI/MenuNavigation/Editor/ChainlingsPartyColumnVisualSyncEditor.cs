#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using SignalJam.UI.MenuNavigation.PartyTab;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace SignalJam.UI.MenuNavigation.Editor
{
    /// <summary>
    /// Copies authored visual/layout settings from Column_1 to Column_2 and Column_3.
    /// Skips each column root RectTransform and LayoutElement so HorizontalLayoutGroup sizing is preserved.
    /// </summary>
    public static class ChainlingsPartyColumnVisualSyncEditor
    {
        private const string PartyPrefabPath = "Assets/Prefabs/UI/PlayerMenu/ChainlingsPartyView.prefab";
        private const string ColumnShellPrefabPath = "Assets/Prefabs/UI/PlayerMenu/ChainlingPartyColumnShell.prefab";

        private static readonly string[] LegacyColumnNames = { "Column_Lead", "Column_Partner", "Column_Bench" };

        public struct ColumnVisualCopyOptions
        {
            public bool IncludeMenuSelectableFocusVisual;

            public static ColumnVisualCopyOptions ForPartyColumns => new ColumnVisualCopyOptions
            {
                IncludeMenuSelectableFocusVisual = true,
            };

            public static ColumnVisualCopyOptions ForColumnShellPrefab => new ColumnVisualCopyOptions
            {
                IncludeMenuSelectableFocusVisual = false,
            };
        }

        private static readonly Type[] CopyComponentTypes =
        {
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(TextMeshProUGUI),
            typeof(CanvasGroup),
            typeof(LayoutElement),
            typeof(HorizontalLayoutGroup),
            typeof(VerticalLayoutGroup),
            typeof(ContentSizeFitter),
            typeof(Shadow),
            typeof(Outline),
            typeof(Button),
            typeof(PartyTabFrameAnimator),
            typeof(MenuSelectableFocusVisual),
        };

        [MenuItem("Tools/Chainbound/UI/Player Menu Dev/Copy Slot 1 Column Visuals to Column Shell Prefab", priority = 41)]
        public static void CopyFromPartyPrefabToColumnShell()
        {
            GameObject partyPrefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(PartyPrefabPath);
            if (partyPrefabRoot == null)
            {
                EditorUtility.DisplayDialog(
                    "Copy Slot 1 Column Visuals",
                    $"Could not find prefab at:\n{PartyPrefabPath}",
                    "OK");
                return;
            }

            GameObject shellPrefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(ColumnShellPrefabPath);
            if (shellPrefabRoot == null)
            {
                EditorUtility.DisplayDialog(
                    "Copy Slot 1 Column Visuals",
                    $"Could not find prefab at:\n{ColumnShellPrefabPath}",
                    "OK");
                return;
            }

            GameObject partyInstance = PrefabUtility.LoadPrefabContents(PartyPrefabPath);
            GameObject shellInstance = PrefabUtility.LoadPrefabContents(ColumnShellPrefabPath);
            try
            {
                if (!TryResolveSourceColumn(partyInstance.transform, out Transform source))
                {
                    EditorUtility.DisplayDialog(
                        "Copy Slot 1 Column Visuals",
                        "Could not find Column_1 (or legacy Column_Lead) under ColumnsRoot.",
                        "OK");
                    return;
                }

                SyncSourceShellRoleLabelColorFromLabel(source);
                CopyColumnVisuals(source, shellInstance.transform, ColumnVisualCopyOptions.ForColumnShellPrefab);
                StripMenuSelectableFocusVisual(shellInstance.transform);
                ApplyTemplateRoleLabel(shellInstance.transform);
                PrefabUtility.SaveAsPrefabAsset(shellInstance, ColumnShellPrefabPath);
                Debug.Log($"[ChainlingsPartyColumnVisualSync] Copied slot 1 visuals to {ColumnShellPrefabPath}.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(shellInstance);
                PrefabUtility.UnloadPrefabContents(partyInstance);
            }

            AssetDatabase.SaveAssets();
        }

        [MenuItem("Tools/Chainbound/UI/Player Menu Dev/Copy Slot 1 Column Visuals to Slots 2 and 3", priority = 42)]
        public static void CopyFromPartyPrefab()
        {
            GameObject prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(PartyPrefabPath);
            if (prefabRoot == null)
            {
                EditorUtility.DisplayDialog(
                    "Copy Slot 1 Column Visuals",
                    $"Could not find prefab at:\n{PartyPrefabPath}",
                    "OK");
                return;
            }

            GameObject instance = PrefabUtility.LoadPrefabContents(PartyPrefabPath);
            try
            {
                if (!TryResolveSourceAndTargets(instance.transform, out Transform source, out Transform[] targets))
                {
                    EditorUtility.DisplayDialog(
                        "Copy Slot 1 Column Visuals",
                        "Could not find Column_1 (or legacy Column_Lead) plus slots 2 and 3 under ColumnsRoot.",
                        "OK");
                    return;
                }

                int copied = CopySourceVisualsToTargets(source, targets);
                PrefabUtility.SaveAsPrefabAsset(instance, PartyPrefabPath);
                Debug.Log($"[ChainlingsPartyColumnVisualSync] Copied slot 1 visuals to {copied} column(s) on {PartyPrefabPath}.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(instance);
            }

            AssetDatabase.SaveAssets();
        }

        [MenuItem("GameObject/Chainbound/UI/Copy Slot 1 Column Visuals to Other Columns", false, 10)]
        public static void CopyFromSelection(MenuCommand command)
        {
            GameObject context = command.context as GameObject;
            if (context == null)
                return;

            if (!TryResolveSourceAndTargets(context.transform, out Transform source, out Transform[] targets))
            {
                EditorUtility.DisplayDialog(
                    "Copy Slot 1 Column Visuals",
                    "Select Column_1, ColumnsRoot, or ChainlingsPartyView, then run this command again.",
                    "OK");
                return;
            }

            int copied = CopySourceVisualsToTargets(source, targets);
            Debug.Log($"[ChainlingsPartyColumnVisualSync] Copied slot 1 visuals to {copied} column(s) on '{source.root.name}'.");
        }

        [MenuItem("GameObject/Chainbound/UI/Copy Slot 1 Column Visuals to Other Columns", true)]
        public static bool ValidateCopyFromSelection()
        {
            GameObject context = Selection.activeGameObject;
            return context != null && TryResolveSourceAndTargets(context.transform, out _, out _);
        }

        public static int CopySourceVisualsToTargets(Transform source, IReadOnlyList<Transform> targets)
        {
            if (source == null || targets == null)
                return 0;

            SyncSourceShellRoleLabelColorFromLabel(source);

            int copied = 0;
            for (int i = 0; i < targets.Count; i++)
            {
                Transform target = targets[i];
                if (target == null || target == source)
                    continue;

                Undo.RegisterFullObjectHierarchyUndo(target.gameObject, "Copy Slot 1 Column Visuals");
                CopyColumnVisuals(source, target, ColumnVisualCopyOptions.ForPartyColumns);
                ApplyColumnSlotLabel(source, target, i + 1);
                EditorUtility.SetDirty(target.gameObject);
                copied++;
            }

            return copied;
        }

        public static bool TryResolveSourceAndTargets(
            Transform context,
            out Transform source,
            out Transform[] targets)
        {
            source = null;
            targets = Array.Empty<Transform>();

            if (context == null)
                return false;

            Transform columnsRoot = FindColumnsRoot(context);
            if (columnsRoot == null)
                return false;

            source = FindColumn(columnsRoot, 0);
            Transform slot2 = FindColumn(columnsRoot, 1);
            Transform slot3 = FindColumn(columnsRoot, 2);

            if (source == null)
            {
                if (IsSourceColumn(context))
                    source = context;
                else
                    return false;
            }

            if (slot2 == null || slot3 == null)
                return false;

            targets = new[] { slot2, slot3 };
            return true;
        }

        public static bool TryResolveSourceColumn(Transform context, out Transform source)
        {
            source = null;
            if (context == null)
                return false;

            Transform columnsRoot = FindColumnsRoot(context);
            if (columnsRoot != null)
                source = FindColumn(columnsRoot, 0);

            if (source == null && IsSourceColumn(context))
                source = context;

            return source != null;
        }

        private static bool IsSourceColumn(Transform transform)
        {
            if (transform == null)
                return false;

            return string.Equals(transform.name, ChainlingsPartyScreen.SlotColumnObjectName(0), StringComparison.Ordinal)
                || string.Equals(transform.name, LegacyColumnNames[0], StringComparison.Ordinal);
        }

        private static Transform FindColumn(Transform columnsRoot, int slotIndex)
        {
            if (columnsRoot == null)
                return null;

            Transform column = columnsRoot.Find(ChainlingsPartyScreen.SlotColumnObjectName(slotIndex));
            if (column != null)
                return column;

            if (slotIndex >= 0 && slotIndex < LegacyColumnNames.Length)
                return columnsRoot.Find(LegacyColumnNames[slotIndex]);

            return null;
        }

        private static Transform FindColumnsRoot(Transform context)
        {
            if (context == null)
                return null;

            Transform current = context;
            while (current != null)
            {
                if (string.Equals(current.name, "ColumnsRoot", StringComparison.Ordinal))
                    return current;

                ChainlingPartyColumnShell shell = current.GetComponent<ChainlingPartyColumnShell>();
                if (shell != null && current.parent != null)
                    return current.parent;

                current = current.parent;
            }

            ChainlingsPartyScreen screen = context.GetComponentInParent<ChainlingsPartyScreen>(true);
            if (screen != null)
            {
                ChainlingPartyColumnShell[] shells = screen.GetComponentsInChildren<ChainlingPartyColumnShell>(true);
                if (shells != null && shells.Length > 0 && shells[0] != null)
                    return shells[0].transform.parent;
            }

            return null;
        }

        private static void CopyColumnVisuals(
            Transform sourceRoot,
            Transform targetRoot,
            ColumnVisualCopyOptions options)
        {
            sourceRoot.GetComponent<ChainlingPartyColumnShell>()?.BuildDefaultLayoutForEditor();
            targetRoot.GetComponent<ChainlingPartyColumnShell>()?.BuildDefaultLayoutForEditor();

            CopyNodeRecursive(sourceRoot, targetRoot, isColumnRoot: true, options);
            CopyColumnFrameVisuals(sourceRoot, targetRoot, options);
        }

        private static void CopyColumnFrameVisuals(
            Transform sourceRoot,
            Transform targetRoot,
            ColumnVisualCopyOptions options)
        {
            ChainlingPartyColumnShell sourceShell = sourceRoot.GetComponent<ChainlingPartyColumnShell>();
            ChainlingPartyColumnShell targetShell = targetRoot.GetComponent<ChainlingPartyColumnShell>();
            if (sourceShell != null && targetShell != null)
            {
                CopyShellSerializedFields(sourceShell, targetShell);
                CopyFrameImage(GetShellFrameImage(sourceShell, "typeBackgroundImage"), GetShellFrameImage(targetShell, "typeBackgroundImage"));
                CopyFrameImage(GetShellFrameImage(sourceShell, "typeIconPrimaryImage"), GetShellFrameImage(targetShell, "typeIconPrimaryImage"));
                CopyFrameImage(GetShellFrameImage(sourceShell, "typeIconSecondaryImage"), GetShellFrameImage(targetShell, "typeIconSecondaryImage"));
                CopyFrameImage(GetShellFrameImage(sourceShell, "cardChromeImage"), GetShellFrameImage(targetShell, "cardChromeImage"));
                CopyFrameImage(GetShellFrameImage(sourceShell, "emptyPedestalImage"), GetShellFrameImage(targetShell, "emptyPedestalImage"));
            }

            if (!options.IncludeMenuSelectableFocusVisual)
                return;

            MenuSelectableFocusVisual sourceNav = sourceRoot.GetComponent<MenuSelectableFocusVisual>();
            MenuSelectableFocusVisual targetNav = targetRoot.GetComponent<MenuSelectableFocusVisual>();
            if (sourceNav != null && targetNav != null)
            {
                CopyMenuFocusVisualFields(sourceNav, targetNav);
                CopyFrameImage(GetNavFocusFrameImage(sourceNav), GetNavFocusFrameImage(targetNav));
            }
        }

        private static void CopyNodeRecursive(
            Transform source,
            Transform target,
            bool isColumnRoot,
            ColumnVisualCopyOptions options)
        {
            CopyMatchingComponents(source.gameObject, target.gameObject, skipRootLayout: isColumnRoot, options);

            if (!isColumnRoot)
                CopyRectTransform(source as RectTransform, target as RectTransform);

            int childCount = source.childCount;
            for (int i = 0; i < childCount; i++)
            {
                Transform sourceChild = source.GetChild(i);
                Transform targetChild = FindMatchingChild(source, target, sourceChild, i);

                if (targetChild == null)
                {
                    Debug.LogWarning(
                        $"[ChainlingsPartyColumnVisualSync] Missing target child '{sourceChild.name}' under '{target.name}'.",
                        target);
                    continue;
                }

                CopyNodeRecursive(sourceChild, targetChild, isColumnRoot: false, options);
            }
        }

        private static void CopyMatchingComponents(
            GameObject source,
            GameObject target,
            bool skipRootLayout,
            ColumnVisualCopyOptions options)
        {
            for (int t = 0; t < CopyComponentTypes.Length; t++)
            {
                Type componentType = CopyComponentTypes[t];
                if (skipRootLayout && componentType == typeof(LayoutElement))
                    continue;

                if (!options.IncludeMenuSelectableFocusVisual && componentType == typeof(MenuSelectableFocusVisual))
                    continue;

                CopyComponentsOfType(source, target, componentType);
            }
        }

        private static void StripMenuSelectableFocusVisual(Transform columnRoot)
        {
            if (columnRoot == null)
                return;

            ChainlingPartyColumnShell shell = columnRoot.GetComponent<ChainlingPartyColumnShell>();

            MenuSelectableFocusVisual navVisual = columnRoot.GetComponent<MenuSelectableFocusVisual>();
            if (navVisual == null)
                return;

            Image navFocusFrame = GetNavFocusFrameImage(navVisual);
            UnityEngine.Object.DestroyImmediate(navVisual, true);

            if (navFocusFrame != null)
                UnityEngine.Object.DestroyImmediate(navFocusFrame.gameObject, true);
        }

        private static void ApplyTemplateRoleLabel(Transform columnRoot)
        {
            TMP_Text roleLabel = FindRoleLabel(columnRoot);
            if (roleLabel == null)
                return;

            roleLabel.text = ChainlingsPartyScreen.SlotLabel(0);

            ChainlingPartyColumnShell shell = columnRoot.GetComponent<ChainlingPartyColumnShell>();
            if (shell != null)
                roleLabel.color = shell.RoleLabelColor;
        }

        private static void CopyComponentsOfType(GameObject source, GameObject target, Type componentType)
        {
            Component[] sourceComponents = source.GetComponents(componentType);
            if (sourceComponents == null || sourceComponents.Length == 0)
                return;

            Component[] targetComponents = target.GetComponents(componentType);
            int copyCount = Math.Min(sourceComponents.Length, targetComponents.Length);
            for (int i = 0; i < copyCount; i++)
                EditorUtility.CopySerialized(sourceComponents[i], targetComponents[i]);

            if (targetComponents.Length < sourceComponents.Length)
            {
                Debug.LogWarning(
                    $"[ChainlingsPartyColumnVisualSync] '{target.name}' has fewer {componentType.Name} component(s) than '{source.name}'.",
                    target);
            }
        }

        private static Transform FindMatchingChild(
            Transform sourceParent,
            Transform targetParent,
            Transform sourceChild,
            int sourceChildIndex)
        {
            if (sourceChild == null || targetParent == null)
                return null;

            string childName = sourceChild.name;
            int sameNameIndex = 0;
            for (int i = 0; i < sourceChildIndex; i++)
            {
                if (string.Equals(sourceParent.GetChild(i).name, childName, StringComparison.Ordinal))
                    sameNameIndex++;
            }

            int matched = 0;
            for (int i = 0; i < targetParent.childCount; i++)
            {
                Transform candidate = targetParent.GetChild(i);
                if (!string.Equals(candidate.name, childName, StringComparison.Ordinal))
                    continue;

                if (matched == sameNameIndex)
                    return candidate;

                matched++;
            }

            if (sourceChildIndex < targetParent.childCount)
                return targetParent.GetChild(sourceChildIndex);

            return targetParent.Find(childName);
        }

        private static void CopyRectTransform(RectTransform source, RectTransform target)
        {
            if (source == null || target == null)
                return;

            target.anchorMin = source.anchorMin;
            target.anchorMax = source.anchorMax;
            target.pivot = source.pivot;
            target.anchoredPosition = source.anchoredPosition;
            target.sizeDelta = source.sizeDelta;
            target.offsetMin = source.offsetMin;
            target.offsetMax = source.offsetMax;
            target.localRotation = source.localRotation;
            target.localScale = source.localScale;
        }

        private static void CopyFrameImage(Image source, Image target)
        {
            if (source == null || target == null)
                return;

            bool wasEnabled = target.enabled;
            CopyRectTransform(source.rectTransform, target.rectTransform);
            EditorUtility.CopySerialized(source, target);
            target.enabled = wasEnabled;
        }

        private static Image GetShellFrameImage(ChainlingPartyColumnShell shell, string propertyName)
        {
            if (shell == null)
                return null;

            SerializedObject serializedShell = new SerializedObject(shell);
            SerializedProperty frameProperty = serializedShell.FindProperty(propertyName);
            if (frameProperty == null)
                return null;

            return frameProperty.objectReferenceValue as Image;
        }

        private static Image GetNavFocusFrameImage(MenuSelectableFocusVisual visual)
        {
            if (visual == null)
                return null;

            SerializedObject serializedVisual = new SerializedObject(visual);
            SerializedProperty frameProperty = serializedVisual.FindProperty("focusFrame");
            if (frameProperty == null)
                return null;

            return frameProperty.objectReferenceValue as Image;
        }

        private static void CopyShellSerializedFields(ChainlingPartyColumnShell source, ChainlingPartyColumnShell target)
        {
            SerializedObject sourceObject = new SerializedObject(source);
            SerializedObject targetObject = new SerializedObject(target);

            CopyColorProperty(sourceObject, targetObject, "roleLabelColor");
            CopyColorProperty(sourceObject, targetObject, "benchedRoleLabelColor");
            CopyObjectReferenceProperty(sourceObject, targetObject, "typeChromePalette");
            CopyObjectReferenceProperty(sourceObject, targetObject, "typeIconPalette");
            CopyBoolProperty(sourceObject, targetObject, "typeIconOverrideAuthoredViewport");
            CopyVector2Property(sourceObject, targetObject, "typeIconViewportAnchorMin");
            CopyVector2Property(sourceObject, targetObject, "typeIconViewportAnchorMax");
            CopyVector2Property(sourceObject, targetObject, "typeIconViewportAnchoredPosition");
            CopyVector2Property(sourceObject, targetObject, "typeIconViewportSizeDelta");
            CopyBoolProperty(sourceObject, targetObject, "typeIconOverrideAuthoredIconRects");
            CopyVector2Property(sourceObject, targetObject, "typeIconSize");
            CopyVector2Property(sourceObject, targetObject, "typeIconScrollDirection");
            CopyFloatProperty(sourceObject, targetObject, "typeIconScrollDistance");
            CopyFloatProperty(sourceObject, targetObject, "typeIconScrollPixelsPerSecond");
            CopyFloatProperty(sourceObject, targetObject, "typeIconAlpha");
            CopyFloatProperty(sourceObject, targetObject, "typeIconScrollDuration");
            CopyVector2Property(sourceObject, targetObject, "typeIconScrollOffset");
            targetObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CopyMenuFocusVisualFields(MenuSelectableFocusVisual source, MenuSelectableFocusVisual target)
        {
            SerializedObject sourceObject = new SerializedObject(source);
            SerializedObject targetObject = new SerializedObject(target);

            CopyColorProperty(sourceObject, targetObject, "focusColor");
            CopyFloatProperty(sourceObject, targetObject, "focusOutlineInset");
            targetObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CopyColorProperty(SerializedObject source, SerializedObject target, string propertyName)
        {
            SerializedProperty sourceProperty = source.FindProperty(propertyName);
            SerializedProperty targetProperty = target.FindProperty(propertyName);
            if (sourceProperty == null || targetProperty == null)
                return;

            targetProperty.colorValue = sourceProperty.colorValue;
        }

        private static void CopyFloatProperty(SerializedObject source, SerializedObject target, string propertyName)
        {
            SerializedProperty sourceProperty = source.FindProperty(propertyName);
            SerializedProperty targetProperty = target.FindProperty(propertyName);
            if (sourceProperty == null || targetProperty == null)
                return;

            targetProperty.floatValue = sourceProperty.floatValue;
        }

        private static void CopyBoolProperty(SerializedObject source, SerializedObject target, string propertyName)
        {
            SerializedProperty sourceProperty = source.FindProperty(propertyName);
            SerializedProperty targetProperty = target.FindProperty(propertyName);
            if (sourceProperty == null || targetProperty == null)
                return;

            targetProperty.boolValue = sourceProperty.boolValue;
        }

        private static void CopyVector2Property(SerializedObject source, SerializedObject target, string propertyName)
        {
            SerializedProperty sourceProperty = source.FindProperty(propertyName);
            SerializedProperty targetProperty = target.FindProperty(propertyName);
            if (sourceProperty == null || targetProperty == null)
                return;

            targetProperty.vector2Value = sourceProperty.vector2Value;
        }

        private static void CopyObjectReferenceProperty(SerializedObject source, SerializedObject target, string propertyName)
        {
            SerializedProperty sourceProperty = source.FindProperty(propertyName);
            SerializedProperty targetProperty = target.FindProperty(propertyName);
            if (sourceProperty == null || targetProperty == null)
                return;

            targetProperty.objectReferenceValue = sourceProperty.objectReferenceValue;
        }

        private static void SyncSourceShellRoleLabelColorFromLabel(Transform sourceColumn)
        {
            if (sourceColumn == null)
                return;

            ChainlingPartyColumnShell sourceShell = sourceColumn.GetComponent<ChainlingPartyColumnShell>();
            TMP_Text sourceRoleLabel = FindRoleLabel(sourceColumn);
            if (sourceShell == null || sourceRoleLabel == null)
                return;

            SetShellRoleLabelColor(sourceShell, sourceRoleLabel.color);
        }

        private static void ApplyColumnSlotLabel(Transform sourceColumn, Transform targetColumn, int slotIndex)
        {
            if (targetColumn == null)
                return;

            ChainlingPartyColumnShell sourceShell = sourceColumn != null
                ? sourceColumn.GetComponent<ChainlingPartyColumnShell>()
                : null;
            ChainlingPartyColumnShell targetShell = targetColumn.GetComponent<ChainlingPartyColumnShell>();
            TMP_Text sourceRoleLabel = sourceColumn != null ? FindRoleLabel(sourceColumn) : null;
            TMP_Text targetRoleLabel = FindRoleLabel(targetColumn);
            if (targetRoleLabel == null)
                return;

            if (sourceShell != null && targetShell != null)
                CopyShellSerializedFields(sourceShell, targetShell);

            targetRoleLabel.text = ChainlingsPartyScreen.SlotLabel(slotIndex);

            if (ChainlingsPartyScreen.IsBenchedPartySlot(slotIndex))
            {
                targetRoleLabel.color = ResolveBenchedRoleLabelColor(sourceShell, targetShell);
                return;
            }

            Color activeColor = sourceRoleLabel != null
                ? sourceRoleLabel.color
                : sourceShell != null
                    ? sourceShell.RoleLabelColor
                    : PlayerMenuUiFactory.Accent;
            targetRoleLabel.color = activeColor;

            if (targetShell != null)
                SetShellRoleLabelColor(targetShell, activeColor);
        }

        private static Color ResolveBenchedRoleLabelColor(
            ChainlingPartyColumnShell sourceShell,
            ChainlingPartyColumnShell targetShell)
        {
            if (sourceShell != null)
                return sourceShell.BenchedRoleLabelColor;

            if (targetShell != null)
                return targetShell.BenchedRoleLabelColor;

            return PlayerMenuUiFactory.Muted;
        }

        private static void SetShellRoleLabelColor(ChainlingPartyColumnShell shell, Color color)
        {
            if (shell == null)
                return;

            SerializedObject serializedShell = new SerializedObject(shell);
            SerializedProperty roleLabelColorProperty = serializedShell.FindProperty("roleLabelColor");
            if (roleLabelColorProperty == null)
                return;

            roleLabelColorProperty.colorValue = color;
            serializedShell.ApplyModifiedPropertiesWithoutUndo();
        }

        [MenuItem("Tools/Chainbound/UI/Player Menu Dev/Ensure Party Column Shell Cosmetic Layers", priority = 42)]
        public static void EnsurePartyColumnShellCosmeticLayers()
        {
            GameObject shellPrefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(ColumnShellPrefabPath);
            if (shellPrefabRoot == null)
            {
                EditorUtility.DisplayDialog(
                    "Ensure Party Column Shell Cosmetic Layers",
                    $"Could not find prefab at:\n{ColumnShellPrefabPath}",
                    "OK");
                return;
            }

            GameObject shellInstance = PrefabUtility.LoadPrefabContents(ColumnShellPrefabPath);
            try
            {
                ChainlingPartyColumnShell shell = shellInstance.GetComponent<ChainlingPartyColumnShell>();
                if (shell == null)
                {
                    EditorUtility.DisplayDialog(
                        "Ensure Party Column Shell Cosmetic Layers",
                        $"Missing {nameof(ChainlingPartyColumnShell)} on {ColumnShellPrefabPath}.",
                        "OK");
                    return;
                }

                EnsureShellCosmeticLayers(shell, shellInstance.transform);
                StripMenuSelectableFocusVisual(shellInstance.transform);
                PrefabUtility.SaveAsPrefabAsset(shellInstance, ColumnShellPrefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"[ChainlingsPartyColumnVisualSync] Ensured cosmetic layers on {ColumnShellPrefabPath}.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(shellInstance);
            }
        }

        private static void EnsureShellCosmeticLayers(ChainlingPartyColumnShell shell, Transform columnRoot)
        {
            shell.BuildDefaultLayoutForEditor();

            Image typeBackground = shell.TypeBackgroundImage ?? FindChildImage(columnRoot, "TypeBackground");
            RectTransform typeIconMask = shell.TypeIconMask ?? FindChildRect(columnRoot, "TypeIconMask");
            RectTransform typeIconScrollRoot = typeIconMask != null ? FindChildRect(typeIconMask, "TypeIconScrollRoot") : null;
            Image typeIconPrimary = shell.TypeIconPrimaryImage ?? FindChildImage(columnRoot, "PrimaryTypeIcon");
            Image typeIconSecondary = shell.TypeIconSecondaryImage ?? FindChildImage(columnRoot, "SecondaryTypeIcon");
            Image cardChrome = shell.CardChromeImage ?? FindChildImage(columnRoot, "CardChrome");
            Image pedestal = shell.EmptyPedestalImage ?? FindChildImage(columnRoot, "EmptyPedestal");

            if (typeBackground != null)
            {
                typeBackground.raycastTarget = false;
                typeBackground.enabled = true;
                typeBackground.transform.SetAsFirstSibling();
            }

            if (typeIconMask != null)
            {
                RectTransform typeBackgroundRect = typeBackground != null ? typeBackground.rectTransform : null;
                if (typeBackgroundRect != null && typeIconMask.parent != typeBackgroundRect)
                    typeIconMask.SetParent(typeBackgroundRect, false);

                Image maskImage = typeIconMask.GetComponent<Image>();
                if (maskImage != null)
                    maskImage.raycastTarget = false;

                Mask mask = typeIconMask.GetComponent<Mask>();
                if (mask != null)
                    mask.showMaskGraphic = false;

                typeIconMask.SetAsLastSibling();
            }

            if (typeIconPrimary != null)
                typeIconPrimary.raycastTarget = false;
            if (typeIconSecondary != null)
                typeIconSecondary.raycastTarget = false;

            if (cardChrome != null)
            {
                cardChrome.raycastTarget = false;
                cardChrome.type = cardChrome.sprite != null ? Image.Type.Sliced : Image.Type.Simple;
                cardChrome.fillCenter = false;
                cardChrome.enabled = false;
                cardChrome.transform.SetAsLastSibling();
            }

            Image dimOverlay = FindChildImage(columnRoot, "ColumnDimOverlay");
            if (dimOverlay != null)
                dimOverlay.transform.SetAsLastSibling();

            if (pedestal != null)
            {
                pedestal.raycastTarget = false;
                pedestal.enabled = true;
            }

            SerializedObject serializedShell = new SerializedObject(shell);
            SetObjectReference(serializedShell, "typeBackgroundImage", typeBackground);
            SetObjectReference(serializedShell, "typeIconMask", typeIconMask);
            SetObjectReference(serializedShell, "typeIconScrollRoot", typeIconScrollRoot);
            SetObjectReference(serializedShell, "typeIconPrimaryImage", typeIconPrimary);
            SetObjectReference(serializedShell, "typeIconSecondaryImage", typeIconSecondary);
            SetObjectReference(serializedShell, "cardChromeImage", cardChrome);
            SetObjectReference(serializedShell, "emptyPedestalImage", pedestal);
            serializedShell.ApplyModifiedPropertiesWithoutUndo();
        }

        private static RectTransform FindChildRect(Transform root, string childName)
        {
            if (root == null)
                return null;

            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform current = transforms[i];
                if (!string.Equals(current.name, childName, StringComparison.Ordinal))
                    continue;

                return current as RectTransform;
            }

            return null;
        }

        private static Image FindChildImage(Transform root, string childName)
        {
            if (root == null)
                return null;

            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform current = transforms[i];
                if (!string.Equals(current.name, childName, StringComparison.Ordinal))
                    continue;

                Image image = current.GetComponent<Image>();
                if (image != null)
                    return image;
            }

            return null;
        }

        private static void SetObjectReference(SerializedObject serializedObject, string propertyName, UnityEngine.Object value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null)
                return;

            property.objectReferenceValue = value;
        }

        private static TMP_Text FindRoleLabel(Transform column)
        {
            Transform[] transforms = column.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform current = transforms[i];
                if (!string.Equals(current.name, "Role", StringComparison.Ordinal))
                    continue;

                TMP_Text label = current.GetComponent<TMP_Text>();
                if (label != null)
                    return label;
            }

            return null;
        }
    }

    [CustomEditor(typeof(ChainlingsPartyScreen))]
    public sealed class ChainlingsPartyScreenEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Column Visual Sync", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Copies slot 1 column anchors, Image/TMP colors and sizes, TypeBackground, masked type icon layer, pedestal, and CardChrome sprites (including 9-slice), " +
                "layout groups, and other visual components to slots 2 and 3, or into ChainlingPartyColumnShell.prefab. " +
                "Shell sync strips MenuSelectableFocusVisual. Run Ensure Party Column Shell Cosmetic Layers once after shell sync. " +
                "Each column root RectTransform and LayoutElement are left unchanged.",
                MessageType.Info);

            ChainlingsPartyScreen screen = (ChainlingsPartyScreen)target;
            using (new EditorGUI.DisabledScope(!ChainlingsPartyColumnVisualSyncEditor.TryResolveSourceColumn(
                screen.transform,
                out _)))
            {
                if (GUILayout.Button("Copy Slot 1 Column Visuals → Column Shell Prefab"))
                    ChainlingsPartyColumnVisualSyncEditor.CopyFromPartyPrefabToColumnShell();
            }

            using (new EditorGUI.DisabledScope(!ChainlingsPartyColumnVisualSyncEditor.TryResolveSourceAndTargets(
                screen.transform,
                out _,
                out _)))
            {
                if (GUILayout.Button("Copy Slot 1 Column Visuals → Slots 2 & 3"))
                {
                    ChainlingsPartyColumnVisualSyncEditor.TryResolveSourceAndTargets(
                        screen.transform,
                        out Transform source,
                        out Transform[] targets);
                    ChainlingsPartyColumnVisualSyncEditor.CopySourceVisualsToTargets(source, targets);
                }
            }

            if (GUILayout.Button("Ensure Party Column Shell Cosmetic Layers"))
                ChainlingsPartyColumnVisualSyncEditor.EnsurePartyColumnShellCosmeticLayers();
        }
    }
}
#endif
