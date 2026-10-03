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
        private static readonly Color GrowthHeader = new Color(0.38f, 0.44f, 0.31f, 1f);
        private static readonly Color GrowthInk = new Color(0.90f, 0.91f, 0.82f, 1f);
        private static readonly Color GrowthRow = new Color(0.34f, 0.38f, 0.28f, 0.92f);
        private static readonly Color GrowthTrack = new Color(0.28f, 0.31f, 0.22f, 1f);
        private static readonly Color GrowthXp = new Color(0.98f, 0.94f, 0.38f, 1f);
        private static readonly Color GrowthStrip = new Color(0.24f, 0.34f, 0.27f, 1f);
        private static readonly Color GrowthDivider = new Color(0.55f, 0.58f, 0.42f, 0.95f);
        private static readonly Color GrowthNeutral = new Color(0.84f, 0.85f, 0.74f, 1f);
        private static readonly Color GrowthRadar = new Color(0.20f, 0.15f, 0.26f, 1f);

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

        [MenuItem("Tools/Chainbound/UI/Player Menu Dev/Apply Chainling Growth Page", priority = 45)]
        public static void ApplyChainlingGrowthPage()
        {
            GameObject instance = PrefabUtility.LoadPrefabContents(DetailPrefabPath);
            try
            {
                TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(IndexFontPath);
                Transform stack = instance.transform.Find(PageStackName);
                ApplyGrowthWireframe(stack != null ? stack.Find("GrowthPage") : null, font);
                PrefabUtility.SaveAsPrefabAsset(instance, DetailPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(instance);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[ChainlingsMenuViewsCreator] Applied the fixed Growth page layout to ChainlingDetailView.");
        }

        [MenuItem("Tools/Chainbound/UI/Player Menu Dev/Supplement Chainling Growth Columns", priority = 46)]
        public static void SupplementChainlingGrowthColumns()
        {
            GameObject instance = PrefabUtility.LoadPrefabContents(DetailPrefabPath);
            try
            {
                TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(IndexFontPath);
                Transform stack = instance.transform.Find(PageStackName);
                Transform page = stack != null ? stack.Find("GrowthPage") : null;
                SupplementGrowthColumns(page, font);
                PrefabUtility.SaveAsPrefabAsset(instance, DetailPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(instance);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[ChainlingsMenuViewsCreator] Supplemented the authored Growth page with base stats, record values, and radar icons.");
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

            for (int i = page.childCount - 1; i >= 0; i--)
                UnityEngine.Object.DestroyImmediate(page.GetChild(i).gameObject);

            RectTransform content = PlayerMenuUiFactory.CreateRect("Content", page);
            PlayerMenuUiFactory.Stretch(content, 14f, 12f, 14f, 12f);
            HorizontalLayoutGroup columns = PlayerMenuUiFactory.HorizontalLayout(content.gameObject, 0f);
            columns.childAlignment = TextAnchor.UpperLeft;
            columns.childControlWidth = true;
            columns.childControlHeight = true;
            columns.childForceExpandWidth = true;
            columns.childForceExpandHeight = true;
            columns.padding = new RectOffset(2, 2, 2, 2);

            RectTransform left = CreateGrowthColumn(content, "LeftColumn");
            Image divider = PlayerMenuUiFactory.CreateImage("Divider", content, GrowthDivider);
            PlayerMenuUiFactory.LayoutElement(divider.gameObject, preferredWidth: 2f, flexibleWidth: 0f, flexibleHeight: 1f);
            RectTransform right = CreateGrowthColumn(content, "RightColumn");

            CreateGrowthHeader(left, "ProgressHeader", "PROGRESS", font);
            Image xpTrack = PlayerMenuUiFactory.CreateImage("XpBar", left, GrowthTrack);
            PlayerMenuUiFactory.LayoutElement(xpTrack.gameObject, preferredHeight: 22f, flexibleWidth: 1f);
            Image xpFill = PlayerMenuUiFactory.CreateImage("XpBarFill", xpTrack.transform, GrowthXp);
            RectTransform fillRect = xpFill.rectTransform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = new Vector2(0.92f, 1f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;

            RectTransform rankRow = PlayerMenuUiFactory.CreateRect("RankRow", left);
            Image strip = rankRow.gameObject.AddComponent<Image>();
            strip.color = GrowthStrip;
            strip.raycastTarget = false;
            HorizontalLayoutGroup rankLayout = PlayerMenuUiFactory.HorizontalLayout(rankRow.gameObject, 8f);
            rankLayout.padding = new RectOffset(8, 8, 4, 4);
            rankLayout.childAlignment = TextAnchor.MiddleLeft;
            rankLayout.childControlWidth = true;
            rankLayout.childControlHeight = true;
            rankLayout.childForceExpandWidth = false;
            rankLayout.childForceExpandHeight = true;
            PlayerMenuUiFactory.LayoutElement(rankRow.gameObject, preferredHeight: 52f, flexibleWidth: 1f);

            PartyTabStarRatingDisplay starRating = null;
            PartyTabCycleProgressDisplay cycleProgress = null;
            GameObject partyRoot = PrefabUtility.LoadPrefabContents(PartyPrefabPath);
            try
            {
                starRating = CloneColumnWidget(
                    FindGeneralColumnWidget<PartyTabStarRatingDisplay>(partyRoot),
                    rankRow,
                    "StarRating",
                    1f,
                    -1f);
                cycleProgress = CloneColumnWidget(
                    FindGeneralColumnWidget<PartyTabCycleProgressDisplay>(partyRoot),
                    rankRow,
                    "CycleProgress",
                    0f,
                    136f);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(partyRoot);
            }

            if (starRating == null)
                Debug.LogWarning("[ChainlingsMenuViewsCreator] Growth page could not clone the party star rating.");
            if (cycleProgress == null)
                Debug.LogWarning("[ChainlingsMenuViewsCreator] Growth page could not clone the party cycle indicator.");

            CreateGrowthHeader(left, "NatureHeader", "NATURE", font);
            RectTransform nature = PlayerMenuUiFactory.CreateRect("NatureList", left);
            VerticalLayoutGroup natureLayout = PlayerMenuUiFactory.VerticalLayout(nature.gameObject, 4f);
            natureLayout.childControlHeight = true;
            natureLayout.childControlWidth = true;
            natureLayout.childForceExpandHeight = true;
            natureLayout.childForceExpandWidth = true;
            PlayerMenuUiFactory.LayoutElement(nature.gameObject, flexibleWidth: 1f, flexibleHeight: 1f, preferredHeight: 180f);

            string[] ivNames = { "MaxHp", "MeleeAtk", "MeleeDef", "RangedAtk", "RangedDef", "Speed" };
            string[] ivCopy = { "MAX HP", "Melee Atk", "Melee Def", "Ranged Atk", "Ranged Def", "Speed" };
            TMP_Text[] ivValues = new TMP_Text[ivNames.Length];
            for (int i = 0; i < ivNames.Length; i++)
                ivValues[i] = CreateIvRow(nature, ivNames[i], ivCopy[i], font);

            CreateGrowthHeader(right, "RecordHeader", "SERVICE RECORD", font);
            RectTransform record = PlayerMenuUiFactory.CreateRect("RecordGrid", right);
            VerticalLayoutGroup recordLayout = PlayerMenuUiFactory.VerticalLayout(record.gameObject, 4f);
            recordLayout.childControlHeight = true;
            recordLayout.childControlWidth = true;
            recordLayout.childForceExpandHeight = false;
            recordLayout.childForceExpandWidth = true;
            PlayerMenuUiFactory.LayoutElement(record.gameObject, flexibleWidth: 1f);

            TMP_Text victories;
            TMP_Text xpEarned;
            TMP_Text enemyKos;
            TMP_Text timesKod;
            TMP_Text dealt;
            TMP_Text taken;
            CreateRecordPair(record, font, "Victories", "Victories    0", "XpEarned", "XP earned    0", out victories, out xpEarned);
            CreateRecordPair(record, font, "EnemyKos", "Enemy KOs    0", "TimesKod", "Times KO'd    0", out enemyKos, out timesKod);
            CreateRecordPair(record, font, "DamageDealt", "Damage dealt    0", "DamageTaken", "Damage taken    0", out dealt, out taken);

            RectTransform radarHost = PlayerMenuUiFactory.CreateRect("AttributeRadar", right);
            PlayerMenuUiFactory.LayoutElement(radarHost.gameObject, flexibleWidth: 1f, flexibleHeight: 1f, preferredHeight: 160f);
            PartyTabAttributeRadarGraphic radar = radarHost.gameObject.AddComponent<PartyTabAttributeRadarGraphic>();
            radar.raycastTarget = false;
            SerializedObject radarSo = new SerializedObject(radar);
            radarSo.FindProperty("outerRadius").floatValue = 150f;
            radarSo.FindProperty("gridRingCount").intValue = 2;
            radarSo.FindProperty("drawSpokes").boolValue = false;
            radarSo.FindProperty("useOverrideColors").boolValue = true;
            radarSo.FindProperty("overrideStrokeColor").colorValue = GrowthRadar;
            radarSo.FindProperty("overrideGridColor").colorValue = GrowthRadar;
            Color radarFill = GrowthRadar;
            radarFill.a = 0.2f;
            radarSo.FindProperty("overrideFillColor").colorValue = radarFill;
            radarSo.FindProperty("dataStrokeWidth").floatValue = 3f;
            radarSo.FindProperty("gridStrokeWidth").floatValue = 2.2f;
            radarSo.ApplyModifiedPropertiesWithoutUndo();

            ChainlingProgressionPanel panel = page.GetComponent<ChainlingProgressionPanel>();
            if (panel == null)
                return;

            SerializedObject panelSo = new SerializedObject(panel);
            SetIfPresent(panelSo, "starsXpLabel", null);
            SetIfPresent(panelSo, "cycleLabel", null);
            SetIfPresent(panelSo, "rebirthButton", null);
            SetIfPresent(panelSo, "statButtonRoot", null);
            SetIfPresent(panelSo, "xpBarFill", xpFill);
            SetIfPresent(panelSo, "starRating", starRating);
            SetIfPresent(panelSo, "cycleProgress", cycleProgress);
            SetIfPresent(panelSo, "attributeRadar", radar);
            SetIfPresent(panelSo, "victoriesLabel", victories);
            SetIfPresent(panelSo, "enemyKosLabel", enemyKos);
            SetIfPresent(panelSo, "damageDealtLabel", dealt);
            SetIfPresent(panelSo, "damageTakenLabel", taken);
            SetIfPresent(panelSo, "xpEarnedLabel", xpEarned);
            SetIfPresent(panelSo, "timesKodLabel", timesKod);
            SerializedProperty fitRadar = panelSo.FindProperty("fitRadarToRect");
            if (fitRadar != null)
                fitRadar.boolValue = true;
            SerializedProperty ivLabels = panelSo.FindProperty("ivLabels");
            if (ivLabels != null)
            {
                ivLabels.arraySize = ivValues.Length;
                for (int i = 0; i < ivValues.Length; i++)
                    ivLabels.GetArrayElementAtIndex(i).objectReferenceValue = ivValues[i];
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

        private const string AttributeIconsFolder = "Assets/Sprites/UI/PlayerMenu/AttributeIcons";
        private static readonly string[] RadarIconFiles =
        {
            "speed.png",
            "melee_def.png",
            "melee_atk.png",
            "ranged_atk.png",
            "ranged_def.png",
        };

        private static void SupplementGrowthColumns(Transform page, TMP_FontAsset font)
        {
            if (page == null)
                return;

            Transform left = page.Find("Content/LeftColumn");
            Transform nature = left != null ? left.Find("NatureList") : null;
            Transform right = page.Find("Content/RightColumn");
            Transform record = right != null ? right.Find("RecordGrid") : null;
            Transform radar = right != null ? right.Find("AttributeRadar") : null;
            if (nature == null || record == null || radar == null)
            {
                Debug.LogWarning("[ChainlingsMenuViewsCreator] Growth page is missing Nature, the service record, or the radar. Supplement skipped.");
                return;
            }

            TMP_Text[] baseLabels = EnsureNatureBaseColumn(left, nature, font);
            TMP_Text victoriesValue = null;
            TMP_Text xpValue = null;
            TMP_Text kosValue = null;
            TMP_Text kodValue = null;
            TMP_Text dealtValue = null;
            TMP_Text takenValue = null;
            EnsureServiceRecordValues(record, font, ref victoriesValue, ref xpValue, ref kosValue, ref kodValue, ref dealtValue, ref takenValue);
            RectTransform[] icons = EnsureRadarStatIcons(radar.GetComponent<PartyTabAttributeRadarGraphic>());
            if (left is RectTransform leftRect)
                LayoutRebuilder.ForceRebuildLayoutImmediate(leftRect);
            if (record is RectTransform recordRect)
                LayoutRebuilder.ForceRebuildLayoutImmediate(recordRect);

            ChainlingProgressionPanel panel = page.GetComponent<ChainlingProgressionPanel>();
            if (panel == null)
                return;

            SerializedObject panelSo = new SerializedObject(panel);
            SerializedProperty bases = panelSo.FindProperty("baseLabels");
            if (bases != null)
            {
                bases.arraySize = baseLabels.Length;
                for (int i = 0; i < baseLabels.Length; i++)
                    bases.GetArrayElementAtIndex(i).objectReferenceValue = baseLabels[i];
            }

            SetIfPresent(panelSo, "victoriesValueLabel", victoriesValue);
            SetIfPresent(panelSo, "xpEarnedValueLabel", xpValue);
            SetIfPresent(panelSo, "enemyKosValueLabel", kosValue);
            SetIfPresent(panelSo, "timesKodValueLabel", kodValue);
            SetIfPresent(panelSo, "damageDealtValueLabel", dealtValue);
            SetIfPresent(panelSo, "damageTakenValueLabel", takenValue);
            SerializedProperty iconProp = panelSo.FindProperty("radarStatIcons");
            if (iconProp != null && icons != null)
            {
                iconProp.arraySize = icons.Length;
                for (int i = 0; i < icons.Length; i++)
                    iconProp.GetArrayElementAtIndex(i).objectReferenceValue = icons[i];
            }

            panelSo.ApplyModifiedPropertiesWithoutUndo();
        }

        private static TMP_Text[] EnsureNatureBaseColumn(Transform left, Transform nature, TMP_FontAsset font)
        {
            float leftInset = 48f;
            float rightInset = 48f;
            float valueWidth = 64f;
            VerticalLayoutGroup listLayout = nature.GetComponent<VerticalLayoutGroup>();
            if (listLayout != null)
            {
                leftInset = listLayout.padding.left;
                rightInset = listLayout.padding.right;
            }

            Transform sampleRow = nature.childCount > 0 ? nature.GetChild(0) : null;
            HorizontalLayoutGroup rowLayout = sampleRow != null ? sampleRow.GetComponent<HorizontalLayoutGroup>() : null;
            if (rowLayout != null)
            {
                leftInset += rowLayout.padding.left;
                rightInset += rowLayout.padding.right;
            }

            TMP_Text sampleValue = sampleRow != null ? sampleRow.Find("Value")?.GetComponent<TMP_Text>() : null;
            TMP_Text sampleName = sampleRow != null ? sampleRow.Find("Name")?.GetComponent<TMP_Text>() : null;
            LayoutElement sampleValueLayout = sampleValue != null ? sampleValue.GetComponent<LayoutElement>() : null;
            if (sampleValueLayout != null && sampleValueLayout.preferredWidth > 0f)
                valueWidth = sampleValueLayout.preferredWidth;

            EnsureNatureColumnHeader(left, nature, font, sampleName, leftInset, rightInset, valueWidth);

            var bases = new TMP_Text[nature.childCount];
            int count = 0;
            for (int i = 0; i < nature.childCount; i++)
            {
                Transform row = nature.GetChild(i);
                if (row.Find("Name") == null || row.Find("Value") == null)
                    continue;
                bases[count] = EnsureCenteredBaseLabel(row, sampleName, sampleValue, font);
                count++;
            }

            if (count == bases.Length)
                return bases;

            var trimmed = new TMP_Text[count];
            for (int i = 0; i < count; i++)
                trimmed[i] = bases[i];
            return trimmed;
        }

        private static void EnsureNatureColumnHeader(
            Transform left,
            Transform nature,
            TMP_FontAsset font,
            TMP_Text sampleName,
            float leftInset,
            float rightInset,
            float valueWidth)
        {
            Transform header = left.Find("NatureColumns");
            if (header == null)
            {
                RectTransform created = PlayerMenuUiFactory.CreateRect("NatureColumns", left);
                created.SetSiblingIndex(nature.GetSiblingIndex());
                created.anchorMin = new Vector2(0f, 1f);
                created.anchorMax = new Vector2(1f, 1f);
                created.pivot = new Vector2(0.5f, 1f);
                created.sizeDelta = new Vector2(0f, 24f);
                PlayerMenuUiFactory.LayoutElement(created.gameObject, preferredHeight: 24f, flexibleWidth: 1f, flexibleHeight: 0f);
                header = created;
            }

            Color ink = sampleName != null ? sampleName.color : GrowthHeader;
            float size = 16f;
            EnsureAnchoredColumnLabel(header, "Stat", "STAT", font, ink, size, TextAlignmentOptions.MidlineLeft, 0f, leftInset, 140f);
            EnsureAnchoredColumnLabel(header, "Base", "BASE", font, ink, size, TextAlignmentOptions.Center, 0.5f, 0f, 72f);
            EnsureAnchoredColumnLabel(header, "Iv", "IV", font, ink, size, TextAlignmentOptions.MidlineRight, 1f, -rightInset, valueWidth);
        }

        private static void EnsureAnchoredColumnLabel(
            Transform parent,
            string name,
            string copy,
            TMP_FontAsset font,
            Color color,
            float size,
            TextAlignmentOptions alignment,
            float anchorX,
            float anchoredX,
            float width)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
                return;

            TMP_Text text = PlayerMenuUiFactory.CreateText(name, parent, copy, size, alignment, false);
            text.raycastTarget = false;
            text.color = color;
            if (font != null)
                text.font = font;
            RectTransform rect = text.rectTransform;
            rect.anchorMin = new Vector2(anchorX, 0f);
            rect.anchorMax = new Vector2(anchorX, 1f);
            rect.pivot = new Vector2(anchorX, 0.5f);
            rect.sizeDelta = new Vector2(width, 0f);
            rect.anchoredPosition = new Vector2(anchoredX, 0f);
            LayoutElement layout = text.gameObject.AddComponent<LayoutElement>();
            layout.ignoreLayout = true;
        }

        private static TMP_Text EnsureCenteredBaseLabel(Transform row, TMP_Text sampleName, TMP_Text sampleValue, TMP_FontAsset font)
        {
            Transform existing = row.Find("Base");
            if (existing != null)
                return existing.GetComponent<TMP_Text>();

            float size = sampleValue != null ? sampleValue.fontSize : 22f;
            TMP_Text text = PlayerMenuUiFactory.CreateText("Base", row, "0", size, TextAlignmentOptions.Center, false);
            text.raycastTarget = false;
            text.color = sampleName != null ? sampleName.color : text.color;
            if (font != null)
                text.font = font;
            if (sampleValue != null)
                text.fontStyle = sampleValue.fontStyle;
            RectTransform rect = text.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(72f, 0f);
            rect.anchoredPosition = Vector2.zero;
            LayoutElement layout = text.gameObject.AddComponent<LayoutElement>();
            layout.ignoreLayout = true;
            int valueIndex = row.Find("Value") != null ? row.Find("Value").GetSiblingIndex() : row.childCount - 1;
            rect.SetSiblingIndex(Mathf.Max(0, valueIndex));
            return text;
        }

        private static void EnsureServiceRecordValues(
            Transform record,
            TMP_FontAsset font,
            ref TMP_Text victories,
            ref TMP_Text xpEarned,
            ref TMP_Text enemyKos,
            ref TMP_Text timesKod,
            ref TMP_Text dealt,
            ref TMP_Text taken)
        {
            TMP_Text[] labels = record.GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < labels.Length; i++)
            {
                TMP_Text title = labels[i];
                if (title == null || title.name != "Label" || title.transform.parent == null)
                    continue;
                if (title.transform.parent.parent != record && title.transform.parent.parent.parent != record)
                    continue;

                Transform cell = title.transform.parent;
                string cellName = cell.name;
                bool createdValue = cell.Find("Value") == null;
                TMP_Text value = EnsureRecordValue(cell, title, font);
                if (createdValue)
                {
                    if (cellName == "DamageDealt")
                        title.text = "DMG dealt";
                    else if (cellName == "DamageTaken")
                        title.text = "DMG taken";
                    else
                        title.text = StripTrailingValue(title.text);

                    if (title.fontSize < 22f)
                        title.fontSize = 22f;
                }

                if (cellName == "Victories")
                    victories = value;
                else if (cellName == "XpEarned")
                    xpEarned = value;
                else if (cellName == "EnemyKos")
                    enemyKos = value;
                else if (cellName == "TimesKod")
                    timesKod = value;
                else if (cellName == "DamageDealt")
                    dealt = value;
                else if (cellName == "DamageTaken")
                    taken = value;
            }
        }

        private static TMP_Text EnsureRecordValue(Transform cell, TMP_Text title, TMP_FontAsset font)
        {
            HorizontalLayoutGroup row = cell.GetComponent<HorizontalLayoutGroup>();
            if (row == null)
            {
                row = cell.gameObject.AddComponent<HorizontalLayoutGroup>();
                row.padding = new RectOffset(32, 32, 2, 2);
                row.spacing = 8f;
                row.childAlignment = TextAnchor.MiddleLeft;
                row.childControlWidth = true;
                row.childControlHeight = true;
                row.childForceExpandWidth = false;
                row.childForceExpandHeight = true;
            }

            LayoutElement titleLayout = title.GetComponent<LayoutElement>();
            if (titleLayout == null)
                titleLayout = title.gameObject.AddComponent<LayoutElement>();
            if (titleLayout.flexibleWidth <= 0f)
                titleLayout.flexibleWidth = 1f;

            Transform existing = cell.Find("Value");
            TMP_Text value = existing != null ? existing.GetComponent<TMP_Text>() : null;
            if (value == null)
            {
                value = PlayerMenuUiFactory.CreateText("Value", cell, "0", 22f, TextAlignmentOptions.MidlineRight, false);
                value.raycastTarget = false;
            }

            value.fontSize = 22f;
            value.alignment = TextAlignmentOptions.MidlineRight;
            value.color = title.color;
            value.fontStyle = title.fontStyle;
            if (font != null)
                value.font = font;
            else if (title.font != null)
                value.font = title.font;

            LayoutElement valueLayout = value.GetComponent<LayoutElement>();
            if (valueLayout == null)
                valueLayout = value.gameObject.AddComponent<LayoutElement>();
            if (valueLayout.preferredWidth <= 0f)
                valueLayout.preferredWidth = 72f;
            valueLayout.flexibleWidth = 0f;
            return value;
        }

        private static string StripTrailingValue(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            int split = text.IndexOf("    ", System.StringComparison.Ordinal);
            return split > 0 ? text.Substring(0, split) : text;
        }

        private static float ResolveRadarIconCenter(PartyTabAttributeRadarGraphic radar)
        {
            RectTransform rectTransform = radar.rectTransform;
            float authoredHeight = Mathf.Abs(rectTransform.sizeDelta.y);
            float height = Mathf.Abs(rectTransform.rect.height);
            if (height < 20f)
                height = authoredHeight;

            float width = Mathf.Abs(rectTransform.rect.width);
            float limit = width > 20f ? Mathf.Min(width, height) : height;
            if (limit < 20f)
                limit = Mathf.Max(authoredHeight, radar.OuterRadius * 2f);

            float fitted = limit * 0.42f;
            float visible = Mathf.Min(radar.OuterRadius, fitted);
            float center = visible + 18f;
            float inset = limit * 0.5f - 14f;
            if (inset > 8f)
                center = Mathf.Min(center, inset);
            return Mathf.Max(12f, center);
        }

        private static RectTransform[] EnsureRadarStatIcons(PartyTabAttributeRadarGraphic radar)
        {
            if (radar == null)
                return null;

            Transform axes = radar.transform.Find("StatIcons");
            if (axes == null)
            {
                RectTransform created = PlayerMenuUiFactory.CreateRect("StatIcons", radar.transform);
                created.anchorMin = Vector2.zero;
                created.anchorMax = Vector2.one;
                created.pivot = new Vector2(0.5f, 0.5f);
                created.offsetMin = Vector2.zero;
                created.offsetMax = Vector2.zero;
                LayoutElement layout = created.gameObject.AddComponent<LayoutElement>();
                layout.ignoreLayout = true;
                axes = created;
            }

            var icons = new RectTransform[RadarIconFiles.Length];
            Color ink = new Color(0.21568628f, 0.30588236f, 0.2901961f, 1f);
            float center = ResolveRadarIconCenter(radar);
            float serializedRadius = radar.OuterRadius;

            for (int i = 0; i < RadarIconFiles.Length; i++)
            {
                string iconName = "Icon_" + i;
                Transform existing = axes.Find(iconName);
                Image image = existing != null ? existing.GetComponent<Image>() : null;
                if (image == null)
                {
                    image = PlayerMenuUiFactory.CreateImage(iconName, axes, ink);
                    image.rectTransform.sizeDelta = new Vector2(28f, 28f);
                }

                image.preserveAspect = true;
                image.raycastTarget = false;
                RectTransform iconRect = image.rectTransform;
                bool parkedOnSerializedRadius = serializedRadius > 1f &&
                    Mathf.Abs(iconRect.anchoredPosition.magnitude - serializedRadius) < 4f;
                if (existing == null || parkedOnSerializedRadius)
                {
                    if (existing == null)
                    {
                        image.color = ink;
                        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(AttributeIconsFolder + "/" + RadarIconFiles[i]);
                        if (sprite != null)
                            image.sprite = sprite;
                        iconRect.sizeDelta = new Vector2(28f, 28f);
                    }

                    iconRect.anchorMin = new Vector2(0.5f, 0.5f);
                    iconRect.anchorMax = new Vector2(0.5f, 0.5f);
                    iconRect.pivot = new Vector2(0.5f, 0.5f);
                    iconRect.anchoredPosition = radar.GetAxisPoint(i, center);
                }

                icons[i] = image.rectTransform;
            }

            return icons;
        }

        private static RectTransform CreateGrowthColumn(Transform parent, string name)
        {
            RectTransform column = PlayerMenuUiFactory.CreateRect(name, parent);
            VerticalLayoutGroup layout = PlayerMenuUiFactory.VerticalLayout(column.gameObject, 6f);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.padding = new RectOffset(8, 8, 0, 0);
            PlayerMenuUiFactory.LayoutElement(column.gameObject, flexibleWidth: 1f, flexibleHeight: 1f);
            return column;
        }

        private static TMP_Text CreateGrowthHeader(Transform parent, string name, string copy, TMP_FontAsset font)
        {
            TMP_Text text = CreateSectionLabel(parent, name, copy, font);
            text.color = GrowthHeader;
            text.fontSize = 18f;
            return text;
        }

        private static TMP_Text CreateGrowthBody(
            Transform parent,
            string name,
            string copy,
            TMP_FontAsset font,
            TextAlignmentOptions alignment,
            Color color)
        {
            TMP_Text text = PlayerMenuUiFactory.CreateText(name, parent, copy, 16f, alignment, false);
            text.color = color;
            text.raycastTarget = false;
            text.overflowMode = TextOverflowModes.Ellipsis;
            if (font != null)
                text.font = font;
            return text;
        }

        private static TMP_Text CreateIvRow(Transform parent, string name, string label, TMP_FontAsset font)
        {
            RectTransform row = PlayerMenuUiFactory.CreateRect(name, parent);
            Image background = row.gameObject.AddComponent<Image>();
            background.color = GrowthRow;
            background.raycastTarget = false;
            HorizontalLayoutGroup layout = PlayerMenuUiFactory.HorizontalLayout(row.gameObject, 8f);
            layout.padding = new RectOffset(10, 10, 2, 2);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;
            PlayerMenuUiFactory.LayoutElement(row.gameObject, flexibleWidth: 1f, flexibleHeight: 1f, preferredHeight: 36f);

            TMP_Text nameText = CreateGrowthBody(row, "Name", label, font, TextAlignmentOptions.MidlineLeft, GrowthInk);
            PlayerMenuUiFactory.LayoutElement(nameText.gameObject, flexibleWidth: 1f, preferredHeight: 28f);
            TMP_Text value = CreateGrowthBody(row, "Value", "+0", font, TextAlignmentOptions.MidlineRight, GrowthNeutral);
            PlayerMenuUiFactory.LayoutElement(value.gameObject, preferredWidth: 64f, preferredHeight: 28f);
            return value;
        }

        private static void CreateRecordPair(
            Transform parent,
            TMP_FontAsset font,
            string leftName,
            string leftCopy,
            string rightName,
            string rightCopy,
            out TMP_Text left,
            out TMP_Text right)
        {
            RectTransform row = PlayerMenuUiFactory.CreateRect(leftName + "Row", parent);
            HorizontalLayoutGroup layout = PlayerMenuUiFactory.HorizontalLayout(row.gameObject, 6f);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            PlayerMenuUiFactory.LayoutElement(row.gameObject, preferredHeight: 40f, flexibleWidth: 1f);
            left = CreateRecordCell(row, leftName, leftCopy, font);
            right = CreateRecordCell(row, rightName, rightCopy, font);
        }

        private static TMP_Text CreateRecordCell(Transform parent, string name, string copy, TMP_FontAsset font)
        {
            RectTransform cell = PlayerMenuUiFactory.CreateRect(name, parent);
            Image background = cell.gameObject.AddComponent<Image>();
            background.color = GrowthRow;
            background.raycastTarget = false;
            PlayerMenuUiFactory.LayoutElement(cell.gameObject, flexibleWidth: 1f, preferredHeight: 40f);
            TMP_Text label = CreateGrowthBody(cell, "Label", copy, font, TextAlignmentOptions.MidlineLeft, GrowthInk);
            PlayerMenuUiFactory.Stretch(label.rectTransform, 8f, 2f, 8f, 2f);
            return label;
        }

        private static T FindGeneralColumnWidget<T>(GameObject partyRoot) where T : Component
        {
            if (partyRoot == null)
                return null;

            T[] widgets = partyRoot.GetComponentsInChildren<T>(true);
            T columnFallback = null;
            T any = null;
            for (int i = 0; i < widgets.Length; i++)
            {
                T widget = widgets[i];
                if (widget == null)
                    continue;

                any ??= widget;
                bool column = false;
                bool general = false;
                for (Transform cursor = widget.transform; cursor != null; cursor = cursor.parent)
                {
                    if (cursor.name == "Column_1")
                        column = true;
                    if (cursor.name == "GeneralView")
                        general = true;
                }

                if (column && general)
                    return widget;
                if (column)
                    columnFallback ??= widget;
            }

            return columnFallback != null ? columnFallback : any;
        }

        private static T CloneColumnWidget<T>(
            T source,
            Transform parent,
            string name,
            float flexibleWidth,
            float preferredWidth) where T : Component
        {
            if (source == null || parent == null)
                return null;

            GameObject clone = UnityEngine.Object.Instantiate(source.gameObject, parent, false);
            clone.name = name;
            clone.SetActive(true);
            RectTransform rect = clone.transform as RectTransform;
            if (rect != null)
            {
                rect.localScale = Vector3.one;
                rect.localRotation = Quaternion.identity;
            }

            ContentSizeFitter fitter = clone.GetComponent<ContentSizeFitter>();
            if (fitter != null)
                fitter.enabled = false;

            LayoutElement layout = clone.GetComponent<LayoutElement>();
            if (layout == null)
                layout = clone.AddComponent<LayoutElement>();
            layout.ignoreLayout = false;
            layout.flexibleWidth = flexibleWidth;
            layout.flexibleHeight = 1f;
            layout.minHeight = 32f;
            layout.preferredHeight = 48f;
            layout.preferredWidth = preferredWidth;

            Selectable[] selectables = clone.GetComponentsInChildren<Selectable>(true);
            for (int i = 0; i < selectables.Length; i++)
            {
                if (selectables[i] != null)
                    UnityEngine.Object.DestroyImmediate(selectables[i]);
            }

            Graphic[] graphics = clone.GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < graphics.Length; i++)
            {
                if (graphics[i] != null)
                    graphics[i].raycastTarget = false;
            }

            return clone.GetComponent<T>();
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
