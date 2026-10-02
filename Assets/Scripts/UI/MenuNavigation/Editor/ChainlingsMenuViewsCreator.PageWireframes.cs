#if UNITY_EDITOR
using System;
using SignalJam.UI.MenuNavigation;
using SignalJam.UI.MenuNavigation.PartyTab;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace SignalJam.UI.MenuNavigation.Editor
{
    public static partial class ChainlingsMenuViewsCreator
    {
        private const float WireframeRowHeight = 60f;
        private static readonly Color WireframeRow = new Color(0.21f, 0.16f, 0.12f, 0.42f);
        private static readonly Color WireframeInk = new Color(0.96f, 0.88f, 0.72f, 1f);
        private static readonly Color WireframeSection = new Color(0.55f, 0.48f, 0.36f, 1f);

        [MenuItem("Tools/Chainbound/UI/Player Menu Dev/Apply Chainling Dossier Page Wireframes", priority = 44)]
        public static void ApplyChainlingDossierPageWireframes()
        {
            GameObject instance = PrefabUtility.LoadPrefabContents(DetailPrefabPath);
            try
            {
                ApplyDossierPageWireframes(instance);
                PrefabUtility.SaveAsPrefabAsset(instance, DetailPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(instance);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[ChainlingsMenuViewsCreator] Applied dossier page wireframes to ChainlingDetailView.");
        }

        private static void ApplyDossierPageWireframes(GameObject root)
        {
            if (root == null)
                return;

            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(IndexFontPath);
            Transform stack = root.transform.Find(PageStackName);
            if (stack == null)
                return;

            ApplySummaryWireframe(stack.Find("SummaryPage"), font);
            ApplyMovesWireframe(stack.Find("MovesPage"), font);
            ApplyLearnableWireframe(stack.Find("LearnablePage"), font);
            ApplyStyleWireframe(stack.Find("StylePage"), font);
            ApplyGrowthWireframe(stack.Find("GrowthPage"), font);
            RepairNestedIndexTabs(stack.Find("BookPage"));
        }

        /// <summary>
        /// A bare RectTransform named DossierTabs can end up wrapping the real tab list.
        /// Promote the list that actually holds the index rows.
        /// </summary>
        private static void RepairNestedIndexTabs(Transform book)
        {
            if (book == null)
                return;

            Transform outer = book.Find("DossierTabs");
            Transform inner = outer != null ? outer.Find("DossierTabs") : null;
            if (inner == null || inner.GetComponent<VerticalLayoutGroup>() == null)
                return;

            int sibling = outer.GetSiblingIndex();
            inner.SetParent(book, false);
            inner.SetSiblingIndex(sibling);
            UnityEngine.Object.DestroyImmediate(outer.gameObject);
        }

        private static void ApplySummaryWireframe(Transform page, TMP_FontAsset font)
        {
            if (page == null)
                return;

            DestroyLayoutDriver(page);
            Transform fieldNotes = FindNamed(page, "FieldNotes");
            Transform radar = FindNamed(page, "AttributeRadar");
            Transform moves = FindNamed(page, "MovesList");
            if (fieldNotes == null || radar == null)
                return;

            RectTransform rows = EnsureSummaryRows(page);
            Transform vitals = FindNamed(page, "Vitals");
            if (vitals == null)
            {
                vitals = PlayerMenuUiFactory.CreateRect("Vitals", rows);
                VerticalLayoutGroup vitalsLayout = PlayerMenuUiFactory.VerticalLayout(vitals.gameObject, 4f);
                vitalsLayout.padding = new RectOffset(8, 8, 6, 6);
                vitalsLayout.childControlWidth = true;
                vitalsLayout.childControlHeight = true;
                vitalsLayout.childForceExpandWidth = true;
                vitalsLayout.childForceExpandHeight = false;
            }

            Reparent(FindNamed(page, "MaxHp"), vitals);
            Reparent(FindNamed(page, "Placement"), vitals);
            Reparent(FindNamed(page, "IvPlacement"), radar);
            Reparent(fieldNotes, rows);
            Reparent(vitals, rows);
            Reparent(radar, rows);
            if (moves != null)
                Reparent(moves, rows);

            Transform defaultMoves = FindNamed(page, "DefaultMoves");
            if (defaultMoves != null)
            {
                Reparent(defaultMoves, rows);
                defaultMoves.gameObject.SetActive(false);
            }

            Transform specimen = FindNamed(page, "SpecimenData");
            if (specimen != null && specimen.childCount == 0)
                UnityEngine.Object.DestroyImmediate(specimen.gameObject);

            Transform lore = FindNamed(page, "Lore");
            LayoutElement loreLayout = lore != null ? lore.GetComponent<LayoutElement>() : null;
            if (loreLayout != null)
                loreLayout.preferredHeight = 88f;

            Transform iv = FindNamed(page, "IvPlacement");
            LayoutElement ivLayout = iv != null ? iv.GetComponent<LayoutElement>() : null;
            if (ivLayout != null)
                ivLayout.preferredHeight = 96f;

            EnsureFocusButton(fieldNotes.gameObject, 120f);
            EnsureFocusButton(vitals.gameObject, 200f);
            EnsureFocusButton(radar.gameObject, 360f);
            PrepareSummaryMoves(moves);

            Button style = EnsureGlanceRow(rows, "StyleLine", "STYLE\nVariant  ·  Aura", font);
            Button growth = EnsureGlanceRow(rows, "GrowthLine", "GROWTH\nStars  ·  Cycle  ·  XP", font);

            int index = 0;
            fieldNotes.SetSiblingIndex(index++);
            vitals.SetSiblingIndex(index++);
            radar.SetSiblingIndex(index++);
            if (moves != null)
                moves.SetSiblingIndex(index++);
            if (defaultMoves != null)
                defaultMoves.SetSiblingIndex(index++);
            style.transform.SetSiblingIndex(index++);
            growth.transform.SetSiblingIndex(index);

            ChainlingOverviewPanel overview = page.GetComponent<ChainlingOverviewPanel>();
            if (overview == null)
                return;

            SerializedObject overviewSo = new SerializedObject(overview);
            SetIfPresent(overviewSo, "styleLineLabel", style.GetComponentInChildren<TMP_Text>(true));
            SetIfPresent(overviewSo, "growthLineLabel", growth.GetComponentInChildren<TMP_Text>(true));
            overviewSo.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void PrepareSummaryMoves(Transform moves)
        {
            if (moves == null)
                return;

            LayoutElement block = moves.GetComponent<LayoutElement>();
            if (block == null)
                block = moves.gameObject.AddComponent<LayoutElement>();
            block.preferredHeight = 340f;
            block.minHeight = 180f;
            block.flexibleWidth = 1f;
            block.flexibleHeight = 0f;

            ChainlingPartyMoveRowDisplay[] rowDisplays = moves.GetComponentsInChildren<ChainlingPartyMoveRowDisplay>(true);
            Array.Sort(rowDisplays, (a, b) => string.CompareOrdinal(a.name, b.name));
            for (int i = 0; i < rowDisplays.Length; i++)
            {
                ChainlingPartyMoveRowDisplay row = rowDisplays[i];
                if (row == null)
                    continue;

                EnsureFocusButton(row.gameObject, WireframeRowHeight);
                row.transform.SetSiblingIndex(i);
                SerializedObject rowSo = new SerializedObject(row);
                SerializedProperty height = rowSo.FindProperty("preferredRowHeight");
                if (height != null)
                    height.floatValue = WireframeRowHeight;
                rowSo.ApplyModifiedPropertiesWithoutUndo();
            }

            ChainlingPartyMovesViewLayout layout = moves.GetComponent<ChainlingPartyMovesViewLayout>();
            if (layout == null)
                return;

            SerializedObject viewSo = new SerializedObject(layout);
            SerializedProperty rowHeight = viewSo.FindProperty("rowHeight");
            if (rowHeight != null)
                rowHeight.floatValue = WireframeRowHeight;
            SerializedProperty autoScroll = viewSo.FindProperty("autoScrollOverflowRows");
            if (autoScroll != null)
                autoScroll.boolValue = false;
            viewSo.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ApplyMovesWireframe(Transform page, TMP_FontAsset font)
        {
            if (page == null)
                return;

            RectTransform rows = CreateWireframeRows(page);
            CreateSectionLabel(rows, "LoadoutHeader", "LOADOUT", font);
            TMP_Text summary = CreateSectionLabel(rows, "SlotSummary", "0 / 4 equipped", font);
            Button[] slots = new Button[4];
            TMP_Text[] labels = new TMP_Text[4];
            for (int i = 0; i < slots.Length; i++)
            {
                slots[i] = CreateFocusRow(rows, $"Slot_{i + 1}", $"Slot {i + 1}\nEmpty", font, WireframeRowHeight);
                labels[i] = slots[i].GetComponentInChildren<TMP_Text>(true);
            }

            CreateSectionLabel(rows, "CompatibleHeader", "COMPATIBLE", font);
            CreateFocusRow(rows, "CompatibleRow", "Compatible move    Owned", font, WireframeRowHeight);
            Button template = CreateFocusRow(rows, "RowTemplate", "Compatible move", font, WireframeRowHeight);
            template.gameObject.SetActive(false);

            ChainlingMoveEquipPanel panel = page.GetComponent<ChainlingMoveEquipPanel>();
            if (panel == null)
                return;

            SerializedObject panelSo = new SerializedObject(panel);
            SetIfPresent(panelSo, "slotSummaryLabel", summary);
            SetIfPresent(panelSo, "equippedRoot", null);
            SetIfPresent(panelSo, "availableRoot", null);
            SetIfPresent(panelSo, "refreshButton", null);
            SerializedProperty rowLabels = panelSo.FindProperty("loadoutRowLabels");
            if (rowLabels != null)
            {
                rowLabels.arraySize = labels.Length;
                for (int i = 0; i < labels.Length; i++)
                    rowLabels.GetArrayElementAtIndex(i).objectReferenceValue = labels[i];
            }

            panelSo.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ApplyLearnableWireframe(Transform page, TMP_FontAsset font)
        {
            if (page == null)
                return;

            RectTransform rows = CreateWireframeRows(page);
            TMP_Text heading = CreateSectionLabel(rows, "Heading", "LEARNABLE MOVES", font);
            TMP_Text status = CreateSectionLabel(rows, "Status", "Species progression pool", font);
            Button template = CreateFocusRow(rows, "RowTemplate", "Move name    Type    Gate", font, WireframeRowHeight);
            template.gameObject.SetActive(false);
            CreateFocusRow(rows, "Sample_1", "Sample move    Next star", font, WireframeRowHeight);
            CreateFocusRow(rows, "Sample_2", "Sample move    Learned", font, WireframeRowHeight);
            CreateFocusRow(rows, "Sample_3", "Sample move    Cycle-gated", font, WireframeRowHeight);

            ChainlingLearnableMovesPanel panel = page.GetComponent<ChainlingLearnableMovesPanel>();
            ScrollRect scroll = page.Find("Content")?.GetComponent<ScrollRect>();
            if (panel == null)
                return;

            SerializedObject panelSo = new SerializedObject(panel);
            SetIfPresent(panelSo, "headingLabel", heading);
            SetIfPresent(panelSo, "statusLabel", status);
            SetIfPresent(panelSo, "scrollUpButton", null);
            SetIfPresent(panelSo, "scrollDownButton", null);
            SetIfPresent(panelSo, "scrollRect", scroll);
            SetIfPresent(panelSo, "rowsRoot", rows);
            SetIfPresent(panelSo, "rowTemplate", template);
            panelSo.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ApplyStyleWireframe(Transform page, TMP_FontAsset font)
        {
            if (page == null)
                return;

            RectTransform rows = CreateWireframeRows(page);
            CreateSectionLabel(rows, "EntryHeader", "ENTRY", font);
            CreateFocusRow(rows, "Species", "Species    —", font, WireframeRowHeight);
            CreateFocusRow(rows, "PreferredType", "Preferred type    —", font, WireframeRowHeight);
            CreateFocusRow(rows, "PreferredRole", "Preferred role    —", font, WireframeRowHeight);
            CreateFocusRow(rows, "Hook", "Field notes for this species.", font, 96f);
            CreateFocusRow(rows, "Signature", "Signature    —", font, WireframeRowHeight);
            CreateFocusRow(rows, "Encounters", "Encounters    Origins unlogged", font, WireframeRowHeight);
            CreateSectionLabel(rows, "LookHeader", "LOOK", font);
            CreateFocusRow(rows, "Variant", "Variant    Default", font, WireframeRowHeight);
            CreateFocusRow(rows, "Aura", "Aura    None", font, WireframeRowHeight);
            CreateFocusRow(rows, "PartyColumn", "Party column    Default", font, WireframeRowHeight);
            CreateFocusRow(rows, "Idle", "Idle    Cool sway  ·  Flourish", font, WireframeRowHeight);
            CreateFocusRow(rows, "CombatFx", "Combat FX    Stock", font, WireframeRowHeight);
            CreateFocusRow(rows, "SaveStyle", "Save", font, WireframeRowHeight);

            ChainlingStylePanel panel = page.GetComponent<ChainlingStylePanel>();
            if (panel == null)
                return;

            SerializedObject panelSo = new SerializedObject(panel);
            SerializedProperty flag = panelSo.FindProperty("authoredWireframe");
            if (flag != null)
                flag.boolValue = true;
            panelSo.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ApplyGrowthWireframe(Transform page, TMP_FontAsset font)
        {
            if (page == null)
                return;

            RectTransform rows = CreateWireframeRows(page);
            CreateSectionLabel(rows, "ProgressHeader", "PROGRESS", font);
            Button stars = CreateFocusRow(rows, "StarsXp", "Stars 0/5    0 XP", font, WireframeRowHeight);
            Button cycle = CreateFocusRow(rows, "Cycle", "Cycle I", font, WireframeRowHeight);
            CreateSectionLabel(rows, "NatureHeader", "NATURE", font);
            string[] ivNames = { "MaxHp", "MeleeAtk", "MeleeDef", "RangedAtk", "RangedDef", "Speed" };
            string[] ivCopy =
            {
                "Max HP    +0", "Melee Atk    +0", "Melee Def    +0",
                "Ranged Atk    +0", "Ranged Def    +0", "Speed    +0",
            };
            Button[] ivRows = new Button[ivNames.Length];
            for (int i = 0; i < ivRows.Length; i++)
                ivRows[i] = CreateFocusRow(rows, ivNames[i], ivCopy[i], font, WireframeRowHeight);

            CreateSectionLabel(rows, "RecordHeader", "SERVICE RECORD", font);
            Button victories = CreateFocusRow(rows, "Victories", "Victories    0", font, WireframeRowHeight);
            Button enemyKos = CreateFocusRow(rows, "EnemyKos", "Enemy KOs    0", font, WireframeRowHeight);
            Button dealt = CreateFocusRow(rows, "DamageDealt", "Damage dealt    0", font, WireframeRowHeight);
            Button taken = CreateFocusRow(rows, "DamageTaken", "Damage taken    0", font, WireframeRowHeight);
            Button xp = CreateFocusRow(rows, "XpEarned", "XP earned    0", font, WireframeRowHeight);
            Button kod = CreateFocusRow(rows, "TimesKod", "Times KO'd    0", font, WireframeRowHeight);

            ChainlingProgressionPanel panel = page.GetComponent<ChainlingProgressionPanel>();
            if (panel == null)
                return;

            SerializedObject panelSo = new SerializedObject(panel);
            SetIfPresent(panelSo, "starsXpLabel", stars.GetComponentInChildren<TMP_Text>(true));
            SetIfPresent(panelSo, "cycleLabel", cycle.GetComponentInChildren<TMP_Text>(true));
            SetIfPresent(panelSo, "victoriesLabel", victories.GetComponentInChildren<TMP_Text>(true));
            SetIfPresent(panelSo, "enemyKosLabel", enemyKos.GetComponentInChildren<TMP_Text>(true));
            SetIfPresent(panelSo, "damageDealtLabel", dealt.GetComponentInChildren<TMP_Text>(true));
            SetIfPresent(panelSo, "damageTakenLabel", taken.GetComponentInChildren<TMP_Text>(true));
            SetIfPresent(panelSo, "xpEarnedLabel", xp.GetComponentInChildren<TMP_Text>(true));
            SetIfPresent(panelSo, "timesKodLabel", kod.GetComponentInChildren<TMP_Text>(true));
            SerializedProperty ivLabels = panelSo.FindProperty("ivLabels");
            if (ivLabels != null)
            {
                ivLabels.arraySize = ivRows.Length;
                for (int i = 0; i < ivRows.Length; i++)
                    ivLabels.GetArrayElementAtIndex(i).objectReferenceValue = ivRows[i].GetComponentInChildren<TMP_Text>(true);
            }

            panelSo.ApplyModifiedPropertiesWithoutUndo();
        }

        private static RectTransform EnsureSummaryRows(Transform page)
        {
            Transform content = page.Find("Content");
            Transform rows = content != null ? content.Find("Viewport/Rows") : null;
            if (rows != null)
                return (RectTransform)rows;

            return BuildWireframeShell(page);
        }

        private static RectTransform CreateWireframeRows(Transform page)
        {
            for (int i = page.childCount - 1; i >= 0; i--)
                UnityEngine.Object.DestroyImmediate(page.GetChild(i).gameObject);

            return BuildWireframeShell(page);
        }

        private static RectTransform BuildWireframeShell(Transform page)
        {
            RectTransform content = PlayerMenuUiFactory.CreateRect("Content", page);
            PlayerMenuUiFactory.Stretch(content, 16f, 16f, 16f, 16f);

            Image viewportImage = PlayerMenuUiFactory.CreateImage("Viewport", content, Color.white);
            PlayerMenuUiFactory.Stretch(viewportImage.rectTransform);
            viewportImage.raycastTarget = false;
            Mask mask = viewportImage.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            RectTransform rows = PlayerMenuUiFactory.CreateRect("Rows", viewportImage.transform);
            rows.anchorMin = new Vector2(0f, 1f);
            rows.anchorMax = new Vector2(1f, 1f);
            rows.pivot = new Vector2(0.5f, 1f);
            rows.anchoredPosition = Vector2.zero;
            rows.sizeDelta = new Vector2(0f, 0f);
            VerticalLayoutGroup layout = PlayerMenuUiFactory.VerticalLayout(rows.gameObject, 6f);
            layout.padding = new RectOffset(4, 4, 4, 8);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            ContentSizeFitter fitter = rows.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            ScrollRect scroll = content.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewportImage.rectTransform;
            scroll.content = rows;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.inertia = false;
            scroll.scrollSensitivity = 16f;

            ChainlingDossierFocusScroll follower = content.gameObject.AddComponent<ChainlingDossierFocusScroll>();
            SerializedObject followerSo = new SerializedObject(follower);
            SetIfPresent(followerSo, "scrollRect", scroll);
            followerSo.ApplyModifiedPropertiesWithoutUndo();
            return rows;
        }

        private static Button EnsureGlanceRow(Transform rows, string name, string copy, TMP_FontAsset font)
        {
            Transform existing = rows.Find(name);
            if (existing != null)
            {
                Button existingButton = existing.GetComponent<Button>();
                if (existingButton != null)
                    return existingButton;
            }

            return CreateFocusRow(rows, name, copy, font, 64f);
        }

        private static Button CreateFocusRow(Transform parent, string name, string copy, TMP_FontAsset font, float height)
        {
            Button button = PlayerMenuUiFactory.CreateButton(name, parent, copy, WireframeRow);
            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.alignment = TextAlignmentOptions.MidlineLeft;
                label.color = WireframeInk;
                label.fontSize = 18f;
                label.enableWordWrapping = true;
                label.raycastTarget = false;
                if (font != null)
                    label.font = font;
            }

            EnsureFocusButton(button.gameObject, height);
            return button;
        }

        private static TMP_Text CreateSectionLabel(Transform parent, string name, string copy, TMP_FontAsset font)
        {
            TMP_Text text = PlayerMenuUiFactory.CreateText(name, parent, copy, 16f, TextAlignmentOptions.MidlineLeft, false);
            text.color = WireframeSection;
            text.fontStyle = FontStyles.UpperCase;
            text.raycastTarget = false;
            if (font != null)
                text.font = font;
            PlayerMenuUiFactory.LayoutElement(text.gameObject, preferredHeight: 28f, flexibleWidth: 1f);
            return text;
        }

        private static void EnsureFocusButton(GameObject target, float preferredHeight)
        {
            if (target == null)
                return;

            Image image = target.GetComponent<Image>();
            if (image == null)
            {
                image = target.AddComponent<Image>();
                image.color = WireframeRow;
            }

            image.raycastTarget = true;
            Graphic[] graphics = target.GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < graphics.Length; i++)
            {
                if (graphics[i] != null && graphics[i] != image)
                    graphics[i].raycastTarget = false;
            }

            Button button = target.GetComponent<Button>();
            if (button == null)
                button = target.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = image;
            Navigation navigation = button.navigation;
            navigation.mode = Navigation.Mode.Explicit;
            navigation.selectOnLeft = null;
            navigation.selectOnRight = null;
            navigation.selectOnUp = null;
            navigation.selectOnDown = null;
            button.navigation = navigation;

            LayoutElement element = target.GetComponent<LayoutElement>();
            if (element == null)
                element = target.AddComponent<LayoutElement>();
            element.minHeight = Mathf.Max(WireframeRowHeight, element.minHeight);
            if (element.preferredHeight < preferredHeight)
                element.preferredHeight = preferredHeight;
            element.flexibleWidth = 1f;
            element.flexibleHeight = 0f;
            MenuSelectableFocusVisual.EnsureOn(button, ChainlingIndexRow.SelectorTint);
        }

        private static void DestroyLayoutDriver(Transform page)
        {
            HorizontalLayoutGroup horizontal = page.GetComponent<HorizontalLayoutGroup>();
            if (horizontal != null)
                UnityEngine.Object.DestroyImmediate(horizontal);
            VerticalLayoutGroup vertical = page.GetComponent<VerticalLayoutGroup>();
            if (vertical != null)
                UnityEngine.Object.DestroyImmediate(vertical);
            ContentSizeFitter fitter = page.GetComponent<ContentSizeFitter>();
            if (fitter != null)
                UnityEngine.Object.DestroyImmediate(fitter);
        }

        private static void Reparent(Transform child, Transform parent)
        {
            if (child == null || parent == null || child.parent == parent)
                return;
            child.SetParent(parent, false);
        }

        private static Transform FindNamed(Transform root, string name)
        {
            if (root == null)
                return null;
            if (root.name == name)
                return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindNamed(root.GetChild(i), name);
                if (found != null)
                    return found;
            }

            return null;
        }
    }
}
#endif
