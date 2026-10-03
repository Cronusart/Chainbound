using Chainbound.Combat;
using NUnit.Framework;
using SignalJam.UI.MenuNavigation;
using SignalJam.UI.MenuNavigation.PartyTab;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public sealed class ChainlingDossierPrefabTests
{
    private const string DossierPath = "Assets/Prefabs/UI/PlayerMenu/ChainlingDetailView.prefab";
    private const string CombatPath = "Assets/Prefabs/Combat/CombatBattleCanvas.prefab";

    [Test]
    public void DossierPrefab_IsAuthoredAndTouchTargetsMeetMinimum()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DossierPath);
        Assert.That(prefab, Is.Not.Null);

        ChainlingDetailScreen screen = prefab.GetComponent<ChainlingDetailScreen>();
        Assert.That(screen, Is.Not.Null);
        Assert.That(screen.IsPrefabAuthored, Is.True);
        SerializedObject screenSo = new SerializedObject(screen);
        Assert.That(screenSo.FindProperty("captureSlideSound").objectReferenceValue, Is.Not.Null);
        Assert.That(prefab.GetComponentInChildren<PartyTabAttributeRadarGraphic>(true), Is.Not.Null);
        Assert.That(prefab.GetComponentInChildren<ChainlingLearnableMovesPanel>(true), Is.Not.Null);

        RectTransform book = FindBook(prefab) as RectTransform;
        Assert.That(book, Is.Not.Null);
        Assert.That(book.anchorMin.x, Is.EqualTo(0f).Within(0.0001f));
        Assert.That(book.anchorMin.y, Is.EqualTo(0f).Within(0.0001f));
        Assert.That(book.anchorMax.x, Is.EqualTo(1f).Within(0.0001f));
        Assert.That(book.anchorMax.y, Is.EqualTo(1f).Within(0.0001f));
        RectTransform stack = FindPageStack(prefab);
        Assert.That(stack, Is.Not.Null);
        Assert.That(stack.anchorMin.x, Is.EqualTo(0.20625f).Within(0.0001f));
        Assert.That(stack.anchorMin.y, Is.EqualTo(0.01389f).Within(0.0001f));
        Assert.That(stack.anchorMax.x, Is.EqualTo(0.79375f).Within(0.0001f));
        Assert.That(stack.anchorMax.y, Is.EqualTo(0.95139f).Within(0.0001f));

        PlayerMenuBackgroundController background = prefab.GetComponent<PlayerMenuBackgroundController>();
        Assert.That(background, Is.Not.Null);
        Assert.That(background.RespectAuthoredPresentation, Is.True);
        Assert.That(background.ScanlinesEnabled, Is.True);
        Assert.That(background.ScanlineTexture, Is.Not.Null);
        Assert.That(prefab.transform.Find("BaseTint"), Is.Not.Null);
        RawImage scanline = prefab.transform.Find("ScanlineOverlay")?.GetComponent<RawImage>();
        Assert.That(scanline, Is.Not.Null);
        Assert.That(scanline.raycastTarget, Is.False);

        Transform tabs = FindBook(prefab).Find("DossierTabs");
        Assert.That(tabs, Is.Not.Null);
        Assert.That(tabs.childCount, Is.EqualTo(5));
        Assert.That(tabs.GetComponent<VerticalLayoutGroup>(), Is.Not.Null);
        for (int i = 0; i < tabs.childCount; i++)
        {
            Button button = tabs.GetChild(i).GetComponent<Button>();
            Assert.That(button, Is.Not.Null);
            Assert.That(button.GetComponent<LayoutElement>().preferredHeight, Is.GreaterThanOrEqualTo(60f));
            Assert.That(button.GetComponent<ChainlingIndexRow>(), Is.Not.Null);
            Image selector = tabs.GetChild(i).Find("Selector")?.GetComponent<Image>();
            Assert.That(selector, Is.Not.Null);
            Assert.That(selector.raycastTarget, Is.False);
            Assert.That(tabs.GetChild(i).Find("Title"), Is.Not.Null);
            Assert.That(tabs.GetChild(i).Find("PageNumber"), Is.Not.Null);
        }

        Assert.That(FindBook(prefab).Find("DossierHeader/HeaderText/Name"), Is.Not.Null);
        Assert.That(FindBook(prefab).Find("DossierHeader/HeaderText/Meta"), Is.Not.Null);

        Button continueButton = FindFooter(prefab)?.Find("ContinueButton")?.GetComponent<Button>();
        Assert.That(continueButton, Is.Not.Null);
        Assert.That(continueButton.GetComponent<LayoutElement>().preferredHeight, Is.GreaterThanOrEqualTo(60f));
        Assert.That(screenSo.FindProperty("closeButton").objectReferenceValue, Is.SameAs(continueButton));
    }

    [Test]
    public void DossierPrefab_PagesUseVerticalWireframes()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DossierPath);
        Assert.That(prefab, Is.Not.Null);

        string[] pages = { "SummaryPage", "MovesPage", "LearnablePage", "StylePage" };
        for (int i = 0; i < pages.Length; i++)
        {
            Transform page = FindPage(prefab, pages[i]);
            Assert.That(page, Is.Not.Null, pages[i]);
            Transform content = page.Find("Content");
            Assert.That(content, Is.Not.Null, pages[i] + " Content");

            ScrollRect scroll = content.GetComponent<ScrollRect>();
            Assert.That(scroll, Is.Not.Null, pages[i]);
            Assert.That(scroll.vertical, Is.True, pages[i]);
            Assert.That(scroll.horizontal, Is.False, pages[i]);
            Assert.That(content.GetComponent<ChainlingDossierFocusScroll>(), Is.Not.Null, pages[i]);

            bool hasFocusRow = false;
            Button[] buttons = content.GetComponentsInChildren<Button>(true);
            for (int b = 0; b < buttons.Length; b++)
            {
                LayoutElement layout = buttons[b].GetComponent<LayoutElement>();
                Navigation navigation = buttons[b].navigation;
                Assert.That(layout, Is.Not.Null, buttons[b].name);
                Assert.That(layout.preferredHeight, Is.GreaterThanOrEqualTo(60f), buttons[b].name);
                Assert.That(navigation.mode, Is.EqualTo(Navigation.Mode.Explicit), buttons[b].name);
                Assert.That(navigation.selectOnLeft, Is.Null, buttons[b].name);
                Assert.That(navigation.selectOnRight, Is.Null, buttons[b].name);
                if (buttons[b].gameObject.activeSelf)
                    hasFocusRow = true;
            }

            Assert.That(hasFocusRow, Is.True, pages[i] + " needs an active focus row");
        }

        Transform growth = FindPage(prefab, "GrowthPage");
        Transform growthContent = growth.Find("Content");
        Assert.That(growthContent, Is.Not.Null);
        Assert.That(growthContent.GetComponent<ScrollRect>(), Is.Null, "Growth stays on screen without a scroll view.");
        Assert.That(growthContent.Find("LeftColumn/NatureList"), Is.Not.Null);
        Assert.That(growthContent.Find("RightColumn/RecordGrid"), Is.Not.Null);
        Assert.That(growthContent.Find("LeftColumn/RankRow").GetComponentInChildren<PartyTabStarRatingDisplay>(true), Is.Not.Null);
        Assert.That(growthContent.Find("LeftColumn/RankRow").GetComponentInChildren<PartyTabCycleProgressDisplay>(true), Is.Not.Null);
        Assert.That(growthContent.Find("RightColumn/AttributeRadar").GetComponent<PartyTabAttributeRadarGraphic>(), Is.Not.Null);
        Assert.That(growthContent.GetComponentsInChildren<Button>(true).Length, Is.EqualTo(0));
        SerializedObject growthSo = new SerializedObject(growth.GetComponent<ChainlingProgressionPanel>());
        Assert.That(growthSo.FindProperty("ivLabels").arraySize, Is.EqualTo(6));
        Assert.That(growthSo.FindProperty("baseLabels").arraySize, Is.EqualTo(6));
        Assert.That(growthContent.Find("LeftColumn/NatureColumns"), Is.Not.Null);
        Assert.That(growthContent.Find("LeftColumn/NatureList/MaxHp/Base"), Is.Not.Null);
        Assert.That(growthContent.Find("RightColumn/RecordGrid").GetComponentsInChildren<HorizontalLayoutGroup>(true).Length, Is.GreaterThanOrEqualTo(3));
        Assert.That(growthContent.Find("RightColumn/AttributeRadar/StatIcons").childCount, Is.EqualTo(5));
        Assert.That(growthSo.FindProperty("xpBarFill").objectReferenceValue, Is.Not.Null);
        Assert.That(growthSo.FindProperty("attributeRadar").objectReferenceValue, Is.Not.Null);

        Transform learnable = FindPage(prefab, "LearnablePage");
        Assert.That(learnable.Find("Header/ScrollUp"), Is.Null);
        Assert.That(learnable.Find("Header/ScrollDown"), Is.Null);
        Assert.That(learnable.Find("Content/Viewport/Rows/RowTemplate").gameObject.activeSelf, Is.False);

        Transform moves = FindPage(prefab, "MovesPage");
        Assert.That(moves.Find("Content/Viewport/Rows/RowTemplate").gameObject.activeSelf, Is.False);
        ChainlingMoveEquipPanel movePanel = moves.GetComponent<ChainlingMoveEquipPanel>();
        SerializedObject moveSo = new SerializedObject(movePanel);
        Assert.That(moveSo.FindProperty("loadoutRowLabels").arraySize, Is.EqualTo(4));

        ChainlingStylePanel stylePanel = FindPage(prefab, "StylePage").GetComponent<ChainlingStylePanel>();
        Assert.That(new SerializedObject(stylePanel).FindProperty("authoredWireframe").boolValue, Is.True);

        GameObject instance = Object.Instantiate(prefab);
        try
        {
            instance.GetComponentInChildren<ChainlingOverviewPanel>(true).BuildDefaultLayoutForEditor();
            instance.GetComponentInChildren<ChainlingMoveEquipPanel>(true).BuildDefaultLayoutForEditor();
            instance.GetComponentInChildren<ChainlingLearnableMovesPanel>(true).BuildDefaultLayoutForEditor();
            instance.GetComponentInChildren<ChainlingStylePanel>(true).BuildDefaultLayoutForEditor();
            instance.GetComponentInChildren<ChainlingProgressionPanel>(true).BuildDefaultLayoutForEditor();

            Assert.That(FindNamed(instance.transform, "MoveColumns"), Is.Null);
            Assert.That(FindNamed(instance.transform, "SlotList"), Is.Null);
            Assert.That(FindNamed(instance.transform, "StatButtons"), Is.Null);
            Assert.That(FindNamed(instance.transform, "ScrollUp"), Is.Null);
        }
        finally
        {
            Object.DestroyImmediate(instance);
        }
    }

    [Test]
    public void DossierPrefab_PixeloidLabelsUseStampInk()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DossierPath);
        Assert.That(prefab, Is.Not.Null);
        Material stampInk = AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/TextMesh Pro/Fonts/PixeloidSans_Stamp.mat");
        TMP_FontAsset pixeloid = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Resources/TextMesh Pro/Fonts/PixeloidSans SDF.asset");
        Assert.That(stampInk, Is.Not.Null);
        Assert.That(pixeloid, Is.Not.Null);

        TMP_Text[] dossierLabels = prefab.GetComponentsInChildren<TMP_Text>(true);
        int pixeloidCount = 0;
        int otherFontCount = 0;
        for (int i = 0; i < dossierLabels.Length; i++)
        {
            TMP_Text label = dossierLabels[i];
            if (label.GetComponentInParent<PartyTabCycleProgressDisplay>(true) != null)
                continue;

            if (label.font == pixeloid)
            {
                pixeloidCount++;
                Assert.That(label.fontSharedMaterial, Is.SameAs(stampInk), label.transform.name);
            }
            else if (label.font != null)
            {
                otherFontCount++;
                Assert.That(label.fontSharedMaterial, Is.Not.SameAs(stampInk), label.transform.name);
            }
        }

        Assert.That(pixeloidCount, Is.GreaterThan(0));
        Assert.That(otherFontCount, Is.GreaterThan(0));
        TMP_Text nature = FindPage(prefab, "GrowthPage").Find("Content/LeftColumn/NatureHeader").GetComponent<TMP_Text>();
        Assert.That(nature.fontSharedMaterial, Is.SameAs(stampInk));
    }

    [Test]
    public void DossierPrefab_SummaryUsesPartyTabMoveRows()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DossierPath);
        Assert.That(prefab, Is.Not.Null);

        Transform summary = FindPage(prefab, "SummaryPage");
        Assert.That(summary, Is.Not.Null);
        ChainlingOverviewPanel overview = summary.GetComponent<ChainlingOverviewPanel>();
        Assert.That(overview, Is.Not.Null);

        ChainlingPartyMovesViewLayout movesView = summary.GetComponentInChildren<ChainlingPartyMovesViewLayout>(true);
        Assert.That(movesView, Is.Not.Null, "SUMMARY page should carry a cloned party-tab MovesView.");
        Assert.That(movesView.GetComponent<ChainlingPartyInfoViewLayout>(), Is.Null, "Column-only info layout must be stripped from the dossier clone.");
        Assert.That(movesView.GetComponentsInChildren<ChainlingPartyMoveRowDisplay>(true).Length, Is.GreaterThanOrEqualTo(1));
        Assert.That(movesView.TypeIconPalette, Is.Not.Null);
        Assert.That(movesView.RarityPalette, Is.Not.Null);

        var overviewSo = new SerializedObject(overview);
        Assert.That(overviewSo.FindProperty("movesView").objectReferenceValue, Is.SameAs(movesView));
    }

    [Test]
    public void DossierPrefab_StacksOneSheetPerTabBehindTheBookPage()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DossierPath);
        Assert.That(prefab, Is.Not.Null);

        Transform book = FindBook(prefab);
        RectTransform stack = FindPageStack(prefab);
        Assert.That(book, Is.Not.Null);
        Assert.That(stack, Is.Not.Null);
        Assert.That(stack.gameObject.activeSelf, Is.True, "The paper stack stays visible so pages peek behind the front sheet.");
        Assert.That(book.parent, Is.SameAs(stack));
        Assert.That(book.GetSiblingIndex(), Is.EqualTo(stack.childCount - 1), "Index sheet stays on top at rest.");

        string[] pages = { "SummaryPage", "MovesPage", "LearnablePage", "StylePage", "GrowthPage" };
        Sprite paper = book.GetComponent<Image>().sprite;
        Assert.That(paper, Is.Not.Null);
        for (int i = 0; i < pages.Length; i++)
        {
            Transform page = stack.Find(pages[i]);
            Assert.That(page, Is.Not.Null, pages[i]);
            Image sheet = page.GetComponent<Image>();
            Assert.That(sheet, Is.Not.Null);
            Assert.That(sheet.sprite, Is.SameAs(paper), pages[i] + " uses the same paper art as the index sheet.");
        }

        var screenSo = new SerializedObject(prefab.GetComponent<ChainlingDetailScreen>());
        Assert.That(screenSo.FindProperty("sheetStackRoot").objectReferenceValue, Is.SameAs(stack));
        Assert.That(screenSo.FindProperty("bookPageRoot").objectReferenceValue, Is.SameAs(stack));
    }

    [Test]
    public void DossierPrefab_ScanlinesDrawBehindTheBookPage()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DossierPath);
        Assert.That(prefab, Is.Not.Null);
        Assert.That(prefab.GetComponent<PlayerMenuBackgroundController>().ScanlinesAboveContent, Is.False);

        GameObject instance = Object.Instantiate(prefab);
        try
        {
            instance.GetComponent<PlayerMenuBackgroundController>().BuildDefaultLayoutForEditor();
            Transform scanline = instance.transform.Find("ScanlineOverlay");
            Transform stack = FindPageStack(instance);
            Assert.That(scanline.GetSiblingIndex(), Is.LessThan(stack.GetSiblingIndex()));
            Assert.That(scanline.GetSiblingIndex(), Is.EqualTo(instance.transform.Find("BaseTint").GetSiblingIndex() + 1));
            RawImage scanlineImage = scanline.GetComponent<RawImage>();
            Assert.That(scanlineImage.color.a, Is.LessThanOrEqualTo(0.2f));
        }
        finally
        {
            Object.DestroyImmediate(instance);
        }
    }

    [Test]
    public void CombatPrefab_WiresSharedDossierPresenter()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CombatPath);
        Assert.That(prefab, Is.Not.Null);

        ChainlingCaptureDossierPresenter presenter = prefab.GetComponent<ChainlingCaptureDossierPresenter>();
        CaptureCommandCoordinatorUGUI coordinator = prefab.GetComponentInChildren<CaptureCommandCoordinatorUGUI>(true);
        Assert.That(presenter, Is.Not.Null);
        Assert.That(coordinator, Is.Not.Null);

        var presenterSo = new SerializedObject(presenter);
        Assert.That(presenterSo.FindProperty("dossierPrefab").objectReferenceValue, Is.Not.Null);
        Assert.That(presenterSo.FindProperty("overlayParent").objectReferenceValue, Is.Not.Null);

        var coordinatorSo = new SerializedObject(coordinator);
        Assert.That(coordinatorSo.FindProperty("captureDossierPresenterBehaviour").objectReferenceValue, Is.SameAs(presenter));
    }

    private static RectTransform FindPageStack(GameObject root)
    {
        return root.transform.Find("DossierPageStack") as RectTransform;
    }

    private static Transform FindBook(GameObject root)
    {
        return root.transform.Find("BookPage") ??
               root.transform.Find("DossierPageStack/BookPage");
    }

    private static Transform FindFooter(GameObject root)
    {
        return root.transform.Find("DossierFooter") ??
               FindBook(root)?.Find("DossierFooter");
    }

    private static Transform FindPage(GameObject root, string pageName)
    {
        Transform stack = FindPageStack(root);
        if (stack != null)
        {
            Transform page = stack.Find(pageName);
            if (page != null)
                return page;
        }

        return root.transform.Find("BookPage/DossierPages/" + pageName);
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
