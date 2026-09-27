using System.IO;
using System.Reflection;
using Chainbound.Combat;
using NUnit.Framework;
using SignalJam.UI.MenuNavigation;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEditor.SceneManagement;

[TestFixture]
public sealed class CandyMachineAuthoringTests
{
    private const string ModePath = "SafeAreaPanel/SafeContent/BodyRow/Slot_Mode/ModeColumn";
    private const string BrowsePath = "SafeAreaPanel/SafeContent/BodyRow/Slot_Browse/BrowseColumn";
    private const string DetailPath = "SafeAreaPanel/SafeContent/BodyRow/Slot_Detail/DetailColumn";
    private const string ResultPath = "SafeAreaPanel/SafeContent/ResultPanel";
    private const string DetailBodyPath = "SafeAreaPanel/SafeContent/DetailBody";
    private const string DevGrantPath = "SafeAreaPanel/SafeContent/DetailBody/DevGrantStrip";

    [Test]
    public void RuntimeMode_DevicePlayersAlwaysUseDevnetOwnership()
    {
        Assert.That(
            CandyMachineScreen.ResolveRuntimeMode(CandyMachineRuntimeMode.LocalPreview, true, true),
            Is.EqualTo(CandyMachineRuntimeMode.LocalPreview));
        Assert.That(
            CandyMachineScreen.ResolveRuntimeMode(CandyMachineRuntimeMode.LocalPreview, false, true),
            Is.EqualTo(CandyMachineRuntimeMode.DevnetOwnership));
        Assert.That(
            CandyMachineScreen.ResolveRuntimeMode(CandyMachineRuntimeMode.LocalPreview, false, false),
            Is.EqualTo(CandyMachineRuntimeMode.DevnetOwnership));
        Assert.That(CandyMachineScreen.StampSlotsPerAction, Is.EqualTo(1));
    }

    [Test]
    public void Prefab_HasCanvasSafeAreaAndNoUiDocument()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CandyMachineAuthoring.PrefabPath);
        Assert.That(prefab, Is.Not.Null, "Run Tools/Chainbound/UI/Author Academy Candy Machine.");
        Assert.That(prefab.GetComponent<CandyMachineScreen>(), Is.Not.Null);
        Assert.That(prefab.GetComponent<Canvas>(), Is.Not.Null);
        Assert.That(prefab.GetComponent("UIDocument"), Is.Null);
        Assert.That(prefab.transform.Find("SafeAreaPanel"), Is.Not.Null);

        RectTransform safeContent = prefab.transform.Find("SafeAreaPanel/SafeContent") as RectTransform;
        Assert.That(safeContent, Is.Not.Null);
        Assert.That(safeContent.offsetMin.x, Is.EqualTo(0f).Within(0.01f));
        Assert.That(safeContent.offsetMax.x, Is.EqualTo(0f).Within(0.01f));
        Assert.That(
            safeContent.offsetMin.y,
            Is.GreaterThanOrEqualTo(160f),
            "Content must clear the gaming-pad thumb band.");
    }

    [Test]
    public void Prefab_WiresOnlyTheAuthoredDevGrantStrip()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CandyMachineAuthoring.PrefabPath);
        Assert.That(prefab, Is.Not.Null);

        CandyMachineDevGrantPanel[] panels =
            prefab.GetComponentsInChildren<CandyMachineDevGrantPanel>(includeInactive: true);
        Assert.That(panels, Has.Length.EqualTo(1), "A stale duplicate grant panel can hide the authored dev strip at runtime.");

        Transform strip = prefab.transform.Find(DevGrantPath);
        Assert.That(strip, Is.Not.Null);
        Assert.That(strip.gameObject.activeSelf, Is.True);
        Assert.That(strip.GetComponent<CanvasGroup>()?.alpha, Is.EqualTo(1f).Within(0.001f));

        CandyMachineScreen screen = prefab.GetComponent<CandyMachineScreen>();
        SerializedObject serialized = new SerializedObject(screen);
        Assert.That(serialized.FindProperty("grantPanel").objectReferenceValue, Is.SameAs(panels[0]));
    }

    [Test]
    public void RuntimeCopy_HasNoMojibakeMarkers()
    {
        string[] relativePaths =
        {
            "Scripts/UI/MenuNavigation/CandyMachine/CandyMachineScreen.cs",
            "Scripts/Combat/Runtime/Services/DevCandyMachineInventory.cs",
        };

        foreach (string relativePath in relativePaths)
        {
            string source = File.ReadAllText(Path.Combine(Application.dataPath, relativePath));
            Assert.That(source, Does.Not.Contain("Ã"), $"Corrupted UTF-8 marker found in {relativePath}.");
            Assert.That(source, Does.Not.Contain("Â"), $"Corrupted UTF-8 marker found in {relativePath}.");
        }
    }

    [Test]
    public void Prefab_UsesLandscapeThreePlusOneLayout()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CandyMachineAuthoring.PrefabPath);
        Assert.That(prefab, Is.Not.Null);

        Assert.That(prefab.transform.Find($"{ModePath}/ModeTabs/FolioTab")?.GetComponent<Button>(), Is.Not.Null);
        Assert.That(prefab.transform.Find($"{ModePath}/ModeTabs/StampTab")?.GetComponent<Button>(), Is.Not.Null);
        Assert.That(prefab.transform.Find($"{BrowsePath}/FolioBrowse/OddsCard/OddsLabel"), Is.Not.Null);
        Assert.That(prefab.transform.Find($"{BrowsePath}/FolioBrowse/RecentStrip"), Is.Not.Null);
        Assert.That(prefab.transform.Find($"{BrowsePath}/StampBrowse/ListViewport/ListContent"), Is.Not.Null);
        Assert.That(prefab.transform.Find($"{DetailPath}/PreviewCard/DetailTitle"), Is.Not.Null);
        Assert.That(prefab.transform.Find($"{DetailPath}/StampSlots/SlotOne/Value"), Is.Not.Null);
        Assert.That(prefab.transform.Find($"{DetailPath}/StampSlots/SlotTwo/Value"), Is.Not.Null);
        Assert.That(prefab.transform.Find($"{DetailPath}/MetaRow/CostChip/CostLabel"), Is.Not.Null);
        Assert.That(prefab.transform.Find($"{DetailPath}/ButtonRow/PrimaryButton")?.GetComponent<Button>(), Is.Not.Null);
        Assert.That(
            prefab.transform.Find($"{ModePath}/PrimaryButton"),
            Is.Null,
            "Primary CTA belongs in the detail column, not the mode column.");

        Assert.That(prefab.transform.Find(ResultPath)?.GetComponent<CandyMachineResultView>(), Is.Not.Null);
        Assert.That(prefab.transform.Find(DetailBodyPath)?.GetComponent<CandyMachineDetailBodyView>(), Is.Not.Null);
        Assert.That(prefab.transform.Find($"{DetailPath}/DetailBody"), Is.Null);
        Assert.That(prefab.transform.Find(ResultPath).gameObject.activeSelf, Is.False);

        Assert.That(prefab.transform.Find($"{ResultPath}/LibraryCardRoot"), Is.Not.Null);
        Assert.That(prefab.transform.Find($"{ResultPath}/LibraryCardRoot").GetComponent<LibraryCardResultPresenter>(), Is.Not.Null);
        Image face = prefab.transform.Find($"{ResultPath}/LibraryCardRoot/CardFace")?.GetComponent<Image>();
        Assert.That(face, Is.Not.Null);
        Assert.That(face.sprite, Is.Not.Null);
        Assert.That(face.sprite.name, Is.EqualTo("librarycard_frontUI"));
        Image outline = prefab.transform.Find($"{ResultPath}/LibraryCardRoot/OutlineFlash")?.GetComponent<Image>();
        Assert.That(outline, Is.Not.Null);
        Assert.That(outline.sprite, Is.Not.Null);
        Assert.That(outline.sprite.name, Is.EqualTo("librarycard_outline"));
        Assert.That(prefab.transform.Find($"{ResultPath}/ContinueButton")?.GetComponent<Button>(), Is.Not.Null);
        Assert.That(prefab.transform.Find($"{ResultPath}/Card/StepOne"), Is.Null);
        Assert.That(prefab.transform.Find($"{ResultPath}/LibraryCardRoot/StepOne"), Is.Null);

        Assert.That(prefab.transform.Find("SafeAreaPanel/SafeContent/BodyRow"), Is.Not.Null);
        LayoutElement detailBodyLayout = prefab.transform.Find(DetailBodyPath)?.GetComponent<LayoutElement>();
        Assert.That(detailBodyLayout, Is.Not.Null);
        Assert.That(detailBodyLayout.preferredHeight, Is.EqualTo(184f).Within(0.01f));
    }

    [Test]
    public void Prefab_WiresRevealViewsAndPreservesAuthoredBackground()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CandyMachineAuthoring.PrefabPath);
        Assert.That(prefab, Is.Not.Null);
        CandyMachineScreen screen = prefab.GetComponent<CandyMachineScreen>();
        SerializedObject serialized = new SerializedObject(screen);
        Assert.That(serialized.FindProperty("presentationGroup").objectReferenceValue, Is.Not.Null);
        Assert.That(serialized.FindProperty("bodyRow").objectReferenceValue, Is.Not.Null);
        Assert.That(serialized.FindProperty("detailBodyView").objectReferenceValue, Is.Not.Null);
        Assert.That(serialized.FindProperty("resultView").objectReferenceValue, Is.Not.Null);
        Assert.That(serialized.FindProperty("allowEditorShellRebuild").boolValue, Is.False);

        PlayerMenuBackgroundController background = prefab.GetComponentInChildren<PlayerMenuBackgroundController>(true);
        Assert.That(background, Is.Not.Null);
        Assert.That(background.RespectAuthoredPresentation, Is.True);
    }

    [Test]
    public void Prefab_DropsBackConfirmAndModifierPickers()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CandyMachineAuthoring.PrefabPath);
        Assert.That(prefab, Is.Not.Null);

        Transform[] children = prefab.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            string name = children[i].name;
            Assert.That(name, Is.Not.EqualTo("BackButton"), "HUD B/Cancel owns back.");
            Assert.That(name, Is.Not.EqualTo("ModifierOneButton"), "No 1/2 mod picker.");
            Assert.That(name, Is.Not.EqualTo("ModifierTwoButton"), "No 1/2 mod picker.");
            Assert.That(name, Is.Not.EqualTo("ModeSectionFrame"), "Legacy three-column shell must be gone.");
            Assert.That(name, Is.Not.EqualTo("ActionsSectionFrame"), "Legacy three-column shell must be gone.");
        }
    }

    [Test]
    public void Prefab_ButtonsUseSlicedChromeWithFocusShimmer()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CandyMachineAuthoring.PrefabPath);
        Assert.That(prefab, Is.Not.Null);

        Assert.That(
            prefab.GetComponentsInChildren<MenuSelectableFocusVisual>(true),
            Is.Empty,
            "Navigation focus reads as shimmer, not the aqua ring.");

        PlayerMenuChromeButtonVisual[] visuals = prefab.GetComponentsInChildren<PlayerMenuChromeButtonVisual>(true);
        Assert.That(visuals, Is.Not.Empty);
        for (int i = 0; i < visuals.Length; i++)
        {
            Assert.That(visuals[i].ShimmerFollowsFocus, Is.True, visuals[i].name);
            Assert.That(
                visuals[i].PixelsPerUnitMultiplier,
                Is.EqualTo(CandyMachineAuthoring.ButtonPixelsPerUnitMultiplier).Within(0.001f),
                visuals[i].name);

            Image background = visuals[i].GetComponent<Image>();
            Assert.That(background, Is.Not.Null, visuals[i].name);
            Assert.That(background.sprite, Is.Not.Null, visuals[i].name);
            Assert.That(background.type, Is.EqualTo(Image.Type.Sliced), visuals[i].name);
            Assert.That(
                background.pixelsPerUnitMultiplier,
                Is.EqualTo(CandyMachineAuthoring.ButtonPixelsPerUnitMultiplier).Within(0.001f),
                visuals[i].name);

            Image shimmer = visuals[i].transform.Find("Shimmer")?.GetComponent<Image>();
            Assert.That(shimmer, Is.Not.Null, visuals[i].name);
            Assert.That(
                shimmer.pixelsPerUnitMultiplier,
                Is.EqualTo(CandyMachineAuthoring.ButtonPixelsPerUnitMultiplier).Within(0.001f),
                visuals[i].name);
        }
    }

    [Test]
    public void Prefab_StripsFaceButtonGlyphsAndUsesCandyMachineTitle()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CandyMachineAuthoring.PrefabPath);
        Assert.That(prefab, Is.Not.Null);

        CandyMachineScreen screen = prefab.GetComponent<CandyMachineScreen>();
        Assert.That(CandyMachineScreen.MachineTitle, Is.EqualTo("Candy Machine"));
        SerializedObject serialized = new SerializedObject(screen);
        TMP_Text title = serialized.FindProperty("titleLabel").objectReferenceValue as TMP_Text;
        Assert.That(title, Is.Not.Null);
        Assert.That(title.text.ToUpperInvariant(), Does.Contain("CANDY MACHINE"));
        Assert.That(title.text.ToUpperInvariant(), Does.Not.Contain("ACADEMY"));

        TMP_Text[] labels = prefab.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < labels.Length; i++)
            Assert.That(labels[i].text, Does.Not.Contain("[A]"), labels[i].name);

        SerializedObject palette = new SerializedObject(screen);
        Assert.That(palette.FindProperty("typeIconPalette").objectReferenceValue, Is.Not.Null);
    }

    [Test]
    public void RevealData_CarriesMoveSelectStatLines()
    {
        var data = new CandyMachineRevealData(
            CandyMachineMode.GetKnown,
            "TYPE",
            "MEME",
            "RARITY",
            "UNCOMMON",
            "KNOWN MOVE",
            "MEME MAGIC",
            "HEAL",
            null,
            "Power 40",
            "Accuracy 95",
            "AP Cost 2");
        Assert.That(data.HasMoveStats, Is.True);
        Assert.That(data.PowerText, Is.EqualTo("Power 40"));
        Assert.That(data.AccuracyText, Is.EqualTo("Accuracy 95"));
        Assert.That(data.ApText, Is.EqualTo("AP Cost 2"));
    }

    [Test]
    public void RevealData_CarriesLibraryCardHostAndModRarity()
    {
        var data = new CandyMachineRevealData(
            CandyMachineMode.Stamp,
            "HOST MOVE",
            "MOVE",
            "STAMP RARITY",
            "EPIC",
            "MODIFIERS",
            "OVERCLOCK",
            "+7% base power",
            null,
            null,
            null,
            null,
            "Move Name",
            "POWER: 17      ACCURACY: 95",
            "Overclock",
            string.Empty,
            "+7% base power",
            string.Empty,
            RarityTier.Rare,
            true,
            RarityTier.Epic,
            false,
            RarityTier.Common);

        Assert.That(data.IsStamp, Is.True);
        Assert.That(data.MoveName, Is.EqualTo("Move Name"));
        Assert.That(data.MoveRarity, Is.EqualTo(RarityTier.Rare));
        Assert.That(data.HasMod1Rarity, Is.True);
        Assert.That(data.Mod1Rarity, Is.EqualTo(RarityTier.Epic));
        Assert.That(data.HasMod2Rarity, Is.False);
        Assert.That(data.StatsInk, Does.Contain("POWER"));
        Assert.That(
            LibraryCardLayout.FormatModStamp("Overclock", "+7% base power"),
            Is.EqualTo("OVERCLOCK      +7% BASE POWER"));
    }

    [Test]
    public void ResultPresenter_StampPaintsHostBeforeMods()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CandyMachineAuthoring.PrefabPath);
        GameObject host = Object.Instantiate(prefab);
        try
        {
            LibraryCardResultPresenter presenter = host.GetComponentInChildren<LibraryCardResultPresenter>(true);
            Assert.That(presenter, Is.Not.Null);
            Image moveName = host.transform.Find($"{ResultPath}/LibraryCardRoot/MoveNameStamp").GetComponent<Image>();
            Image mod1 = host.transform.Find($"{ResultPath}/LibraryCardRoot/Mod1Stamp").GetComponent<Image>();
            Image moveDot = host.transform.Find($"{ResultPath}/LibraryCardRoot/RarityRow/MoveDot").GetComponent<Image>();

            presenter.Prepare(new CandyMachineRevealData(
                CandyMachineMode.GetKnown,
                "TYPE", "MEME", "RARITY", "UNCOMMON", "KNOWN MOVE", "SIGNAL",
                string.Empty, null, null, null, null, "Signal", "POWER: 12      ACCURACY: AUTO",
                moveRarity: RarityTier.Uncommon));
            Assert.That(moveName.color.a, Is.EqualTo(0f).Within(0.01f), "Known starts blank.");
            Assert.That(moveDot.color.a, Is.EqualTo(0f).Within(0.01f));

            presenter.Prepare(new CandyMachineRevealData(
                CandyMachineMode.Stamp,
                "HOST MOVE", "SIGNAL", "STAMP RARITY", "EPIC", "MODIFIERS", "OVERCLOCK",
                "+7% base power", null, null, null, null, "Signal", "POWER: 12      ACCURACY: AUTO",
                "Overclock", string.Empty, "+7% base power", string.Empty,
                RarityTier.Rare, true, RarityTier.Epic));
            Assert.That(moveName.sprite, Is.Not.Null, "Stamp host ink is a distressed strip, not empty TMP.");
            Assert.That(moveName.color.a, Is.EqualTo(1f).Within(0.01f), "Host ink is already painted before the slide.");
            Assert.That(moveDot.color.a, Is.EqualTo(1f).Within(0.01f));
            Assert.That(mod1.color.a, Is.EqualTo(0f).Within(0.01f), "Mod ink waits for its stamp stage.");
            presenter.Clear();
        }
        finally
        {
            Object.DestroyImmediate(host);
        }
    }

    [Test]
    public void Prefab_LabelsUsePixeloidUppercase()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CandyMachineAuthoring.PrefabPath);
        Assert.That(prefab, Is.Not.Null);

        TMP_Text[] labels = prefab.GetComponentsInChildren<TMP_Text>(true);
        Assert.That(labels, Is.Not.Empty);
        for (int i = 0; i < labels.Length; i++)
        {
            Assert.That(labels[i].font, Is.Not.Null, labels[i].name);
            Assert.That(labels[i].font.name, Does.Contain("Pixeloid"), labels[i].name);
            Assert.That(
                labels[i].fontStyle.HasFlag(FontStyles.UpperCase),
                Is.True,
                labels[i].name);
        }
    }

    [Test]
    public void GameServices_HostsCandyMachinePresenter()
    {
        GameObject services = AssetDatabase.LoadAssetAtPath<GameObject>(CandyMachineAuthoring.GameServicesPath);
        Assert.That(services, Is.Not.Null);
        CandyMachineScreen presenter = services.GetComponentInChildren<CandyMachineScreen>(true);
        Assert.That(presenter, Is.Not.Null);
        Assert.That(presenter.gameObject.activeSelf, Is.False, "Presenter must stay hidden until Open.");
    }

    [Test]
    public void Screen_HidesOverlayUntilOpen()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CandyMachineAuthoring.PrefabPath);
        Assert.That(prefab, Is.Not.Null);
        GameObject host = Object.Instantiate(prefab);
        try
        {
            CandyMachineScreen screen = host.GetComponent<CandyMachineScreen>();
            Canvas canvas = host.GetComponent<Canvas>();
            Transform background = host.transform.Find("Background");
            Assert.That(screen.IsShowing, Is.False);

            Assert.That(screen.Open(), Is.True);
            Assert.That(canvas.enabled, Is.True);
            Assert.That(background.gameObject.activeSelf, Is.True);

            screen.Close();
            Assert.That(host.activeSelf, Is.False);
            Assert.That(canvas.enabled, Is.False);
        }
        finally
        {
            Object.DestroyImmediate(host);
        }
    }

    [Test]
    public void Academy_CandyMachineInteractableOpensCandyUi()
    {
        Scene scene = SceneManager.GetSceneByPath(CandyMachineAuthoring.AcademyScenePath);
        bool opened = !scene.IsValid() || !scene.isLoaded;
        if (opened)
            scene = EditorSceneManager.OpenScene(CandyMachineAuthoring.AcademyScenePath, OpenSceneMode.Additive);

        try
        {
            GameObject machine = FindNamed(scene, CandyMachineIds.MachineObjectName);
            Assert.That(machine, Is.Not.Null);
            Assert.That(machine.GetComponent<MeshCollider>(), Is.Not.Null);
            Assert.That(machine.GetComponent<MeshCollider>().isTrigger, Is.False);
            Assert.That(machine.GetComponent<InteractableObject>(), Is.Null);

            Transform interact = machine.transform.Find(CandyMachineIds.InteractableChildName);
            Assert.That(interact, Is.Not.Null);
            BoxCollider box = interact.GetComponent<BoxCollider>();
            Assert.That(box, Is.Not.Null);
            Assert.That(box.isTrigger, Is.True);

            CandyMachineInteractable binding = interact.GetComponent<CandyMachineInteractable>();
            Assert.That(binding, Is.Not.Null);
            Assert.That(binding.ScreenPrefab, Is.Not.Null);

            CandyMachinePullDirector director = machine.GetComponent<CandyMachinePullDirector>();
            Assert.That(director, Is.Not.Null);
            Assert.That(binding.PullDirector, Is.EqualTo(director));
            SerializedObject directorSerialized = new SerializedObject(director);
            Assert.That(directorSerialized.FindProperty("leverRoot").objectReferenceValue, Is.Not.Null);
            Assert.That(directorSerialized.FindProperty("spheresRoot").objectReferenceValue, Is.Not.Null);
            Assert.That(directorSerialized.FindProperty("emissiveRenderer").objectReferenceValue, Is.Not.Null);
            Assert.That(directorSerialized.FindProperty("candyMachineDoor").objectReferenceValue, Is.Not.Null);
            Assert.That(directorSerialized.FindProperty("pullShot").objectReferenceValue, Is.Not.Null);

            InteractableObject interactable = interact.GetComponent<InteractableObject>();
            Assert.That(interactable, Is.Not.Null);
            Assert.That(HasOpenListener(interactable, binding), Is.True);

            InteractableIndicator prompt = interact.GetComponentInChildren<InteractableIndicator>(true);
            Assert.That(prompt, Is.Not.Null, "Candy machine must reuse InteractableIndicator.");
            SerializedObject interactableSerialized = new SerializedObject(interactable);
            Assert.That(
                interactableSerialized.FindProperty("indicator").objectReferenceValue,
                Is.SameAs(prompt));
            SerializedObject promptSerialized = new SerializedObject(prompt);
            Vector3 offset = promptSerialized.FindProperty("offset").vector3Value;
            Assert.That(offset.y, Is.EqualTo(CandyMachineAuthoring.IndicatorOffset.y).Within(0.01f));
            Assert.That(
                offset.z,
                Is.EqualTo(CandyMachineAuthoring.IndicatorOffset.z).Within(0.01f),
                "Prompt must sit forward of the cabinet, toward the approach/camera side.");
            Assert.That(promptSerialized.FindProperty("scale").floatValue, Is.EqualTo(1f).Within(0.01f));
            Vector3 scaleMultiplier = promptSerialized.FindProperty("scaleMultiplier").vector3Value;
            Assert.That(scaleMultiplier.x, Is.EqualTo(CandyMachineAuthoring.IndicatorScaleMultiplier.x).Within(0.01f));
            Assert.That(scaleMultiplier.y, Is.EqualTo(CandyMachineAuthoring.IndicatorScaleMultiplier.y).Within(0.01f));
            Assert.That(
                scaleMultiplier.z,
                Is.EqualTo(CandyMachineAuthoring.IndicatorScaleMultiplier.z).Within(0.01f),
                "Prompt must match NPC Interactable volume scale (4,3,4) without scaling the candy trigger.");
        }
        finally
        {
            if (opened)
                EditorSceneManager.CloseScene(scene, true);
        }
    }

    [Test]
    public void Screen_OpenFailsWithoutView()
    {
        GameObject host = new GameObject("CandyMachine_OpenFail");
        try
        {
            CandyMachineScreen screen = host.AddComponent<CandyMachineScreen>();
            Assert.That(screen.Open(), Is.False);
            Assert.That(screen.IsShowing, Is.False);
        }
        finally
        {
            Object.DestroyImmediate(host);
        }
    }

    [Test]
    public void Interactable_WarnsWhenPresenterMissing()
    {
        GameObject host = new GameObject("CandyMachine_MissingPresenter");
        try
        {
            CandyMachineInteractable binding = host.AddComponent<CandyMachineInteractable>();
            Assert.DoesNotThrow(() => binding.OpenCandyMachine());
        }
        finally
        {
            Object.DestroyImmediate(host);
        }
    }

    [Test]
    public void ProductionPath_RefusesWhenSpineFlagOff()
    {
        Assert.That(typeof(CandyMachineScreen)
            .GetField("enableDevnetOwnershipSpine", BindingFlags.Instance | BindingFlags.NonPublic),
            Is.Not.Null);
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CandyMachineAuthoring.PrefabPath);
        Assume.That(prefab, Is.Not.Null);
        CandyMachineScreen screen = prefab.GetComponent<CandyMachineScreen>();
        Assume.That(screen, Is.Not.Null);
        Assert.That(screen.EnableDevnetOwnershipSpine, Is.False);
    }

    [Test]
    public void Screen_CommittedResultInvokesDirectorBeforeReveal()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CandyMachineAuthoring.PrefabPath);
        GameObject host = Object.Instantiate(prefab);
        var director = new FakePullDirector();
        try
        {
            CandyMachineScreen screen = host.GetComponent<CandyMachineScreen>();
            SetPrivate(screen, "pullDirector", director);
            SetPrivate(screen, "presentationState", CandyMachinePresentationState.Pulling);
            SetPrivate(screen, "pendingReveal", new CandyMachineRevealData(
                CandyMachineMode.GetKnown,
                "TYPE", "ARC",
                "RARITY", "COMMON",
                "KNOWN MOVE", "TEST MOVE",
                "TEST DETAILS"));

            MethodInfo begin = typeof(CandyMachineScreen).GetMethod(
                "StartWorldPull",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(begin, Is.Not.Null);
            begin.Invoke(screen, null);

            Assert.That(director.BeginCount, Is.EqualTo(1));
            Assert.That(screen.PresentationState, Is.EqualTo(CandyMachinePresentationState.Pulling));
            screen.Close();
            Assert.That(host.activeSelf, Is.True, "Cancel/close stays locked during the pull.");
        }
        finally
        {
            Object.DestroyImmediate(host);
        }
    }

    [Test]
    public void Screen_MissingDirectorFallsBackToReveal()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CandyMachineAuthoring.PrefabPath);
        GameObject host = Object.Instantiate(prefab);
        try
        {
            CandyMachineScreen screen = host.GetComponent<CandyMachineScreen>();
            SetPrivate(screen, "showing", true);
            SetPrivate(screen, "presentationState", CandyMachinePresentationState.Pulling);
            SetPrivate(screen, "pendingReveal", new CandyMachineRevealData(
                CandyMachineMode.Stamp,
                "HOST MOVE", "TEST MOVE",
                "STAMP RARITY", "RARE",
                "MODIFIERS", "2.1-A",
                "TEST EFFECT"));
            MethodInfo begin = typeof(CandyMachineScreen).GetMethod(
                "StartWorldPull",
                BindingFlags.Instance | BindingFlags.NonPublic);
            begin.Invoke(screen, null);
            screen.StopAllCoroutines();
            Assert.That(screen.PresentationState, Is.EqualTo(CandyMachinePresentationState.Revealing));
        }
        finally
        {
            Object.DestroyImmediate(host);
        }
    }

    [Test]
    public void ResultView_EnableContinueMakesButtonInteractable()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CandyMachineAuthoring.PrefabPath);
        GameObject host = Object.Instantiate(prefab);
        try
        {
            CandyMachineResultView result = host.GetComponentInChildren<CandyMachineResultView>(true);
            Assert.That(result, Is.Not.Null);
            result.EnableContinue();
            Assert.That(result.ContinueButton, Is.Not.Null);
            Assert.That(result.ContinueButton.gameObject.activeSelf, Is.True);
            Assert.That(result.ContinueButton.interactable, Is.True);
        }
        finally
        {
            Object.DestroyImmediate(host);
        }
    }

    [Test]
    public void Screen_AwaitingConfirmSubmitConfirmsReveal()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CandyMachineAuthoring.PrefabPath);
        GameObject host = Object.Instantiate(prefab);
        try
        {
            CandyMachineScreen screen = host.GetComponent<CandyMachineScreen>();
            SetPrivate(screen, "showing", true);
            SetPrivate(screen, "presentationState", CandyMachinePresentationState.AwaitingConfirm);
            Assert.That(screen.TryHandleSubmit(), Is.True);
            Assert.That(screen.PresentationState, Is.EqualTo(CandyMachinePresentationState.Browse));
        }
        finally
        {
            Object.DestroyImmediate(host);
        }
    }

    [Test]
    public void Screen_OpenLandsOnModeHubAndCancelDoesNotConsume()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CandyMachineAuthoring.PrefabPath);
        GameObject host = Object.Instantiate(prefab);
        try
        {
            CandyMachineScreen screen = host.GetComponent<CandyMachineScreen>();
            Assert.That(screen.Open(), Is.True);
            Assert.That(screen.NavLayer, Is.EqualTo(CandyMachineNavLayer.Mode));
            Assert.That(screen.TryHandleCancel(), Is.False);
            Assert.That(screen.IsShowing, Is.True);
            Assert.That(screen.NavLayer, Is.EqualTo(CandyMachineNavLayer.Mode));
        }
        finally
        {
            Object.DestroyImmediate(host);
        }
    }

    [Test]
    public void Screen_CancelFromKnownDetailReturnsToModeHub()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CandyMachineAuthoring.PrefabPath);
        GameObject host = Object.Instantiate(prefab);
        try
        {
            CandyMachineScreen screen = host.GetComponent<CandyMachineScreen>();
            Assert.That(screen.Open(), Is.True);
            InvokePrivate(screen, "SetMode", CandyMachineMode.GetKnown);
            Assert.That(screen.NavLayer, Is.EqualTo(CandyMachineNavLayer.Detail));
            Assert.That(screen.TryHandleCancel(), Is.True);
            Assert.That(screen.NavLayer, Is.EqualTo(CandyMachineNavLayer.Mode));
            Assert.That(screen.IsShowing, Is.True);
        }
        finally
        {
            Object.DestroyImmediate(host);
        }
    }

    [Test]
    public void Screen_CancelFromStampBrowseReturnsToModeHub()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CandyMachineAuthoring.PrefabPath);
        GameObject host = Object.Instantiate(prefab);
        try
        {
            CandyMachineScreen screen = host.GetComponent<CandyMachineScreen>();
            Assert.That(screen.Open(), Is.True);
            InvokePrivate(screen, "SetMode", CandyMachineMode.Stamp);
            Assert.That(screen.NavLayer, Is.EqualTo(CandyMachineNavLayer.Browse));
            Assert.That(screen.TryHandleCancel(), Is.True);
            Assert.That(screen.NavLayer, Is.EqualTo(CandyMachineNavLayer.Mode));
            Assert.That(screen.IsShowing, Is.True);
        }
        finally
        {
            Object.DestroyImmediate(host);
        }
    }

    [Test]
    public void Screen_CancelFromStampDetailReturnsToBrowseThenMode()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CandyMachineAuthoring.PrefabPath);
        GameObject host = Object.Instantiate(prefab);
        try
        {
            CandyMachineScreen screen = host.GetComponent<CandyMachineScreen>();
            Assert.That(screen.Open(), Is.True);
            InvokePrivate(screen, "SetMode", CandyMachineMode.Stamp);
            SetPrivate(screen, "navLayer", CandyMachineNavLayer.Detail);
            Assert.That(screen.TryHandleCancel(), Is.True);
            Assert.That(screen.NavLayer, Is.EqualTo(CandyMachineNavLayer.Browse));
            Assert.That(screen.IsShowing, Is.True);
            Assert.That(screen.TryHandleCancel(), Is.True);
            Assert.That(screen.NavLayer, Is.EqualTo(CandyMachineNavLayer.Mode));
            Assert.That(screen.IsShowing, Is.True);
        }
        finally
        {
            Object.DestroyImmediate(host);
        }
    }

    [Test]
    public void Screen_AwaitingConfirmCancelDoesNotExit()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CandyMachineAuthoring.PrefabPath);
        GameObject host = Object.Instantiate(prefab);
        try
        {
            CandyMachineScreen screen = host.GetComponent<CandyMachineScreen>();
            SetPrivate(screen, "showing", true);
            SetPrivate(screen, "navLayer", CandyMachineNavLayer.Detail);
            SetPrivate(screen, "presentationState", CandyMachinePresentationState.AwaitingConfirm);
            Assert.That(screen.TryHandleCancel(), Is.True);
            Assert.That(screen.NavLayer, Is.EqualTo(CandyMachineNavLayer.Detail));
            Assert.That(screen.IsShowing, Is.True);
        }
        finally
        {
            Object.DestroyImmediate(host);
        }
    }

    private static void SetPrivate(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, fieldName);
        field.SetValue(target, value);
    }

    private static void InvokePrivate(object target, string methodName, params object[] args)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, methodName);
        method.Invoke(target, args);
    }

    private sealed class FakePullDirector : ICandyMachinePullDirector
    {
        public int BeginCount { get; private set; }
        public bool IsPlaying { get; private set; }

        public void BeginPull(System.Action revealReady)
        {
            BeginCount++;
            IsPlaying = true;
        }

        public void RestoreAfterReveal() { }
        public void CancelAndRestore() => IsPlaying = false;
    }

    private static bool HasOpenListener(InteractableObject interactable, CandyMachineInteractable binding)
    {
        FieldInfo field = typeof(InteractableObject).GetField(
            "onInteracted",
            BindingFlags.Instance | BindingFlags.NonPublic);
        UnityEvent interacted = field != null ? field.GetValue(interactable) as UnityEvent : null;
        if (interacted == null)
            return false;

        for (int i = 0; i < interacted.GetPersistentEventCount(); i++)
        {
            if (interacted.GetPersistentTarget(i) == binding &&
                interacted.GetPersistentMethodName(i) == nameof(CandyMachineInteractable.OpenCandyMachine))
            {
                return true;
            }
        }

        return false;
    }

    private static GameObject FindNamed(Scene scene, string objectName)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Transform[] children = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < children.Length; i++)
            {
                if (children[i] != null && children[i].name == objectName)
                    return children[i].gameObject;
            }
        }

        return null;
    }
}
