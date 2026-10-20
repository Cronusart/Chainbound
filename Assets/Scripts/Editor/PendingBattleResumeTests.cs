using Chainbound.Combat;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public sealed class PendingBattleResumeTests
{
    private const string PromptPath = "Assets/Prefabs/UI/PendingBattleResumePrompt.prefab";
    private const string ServicesPath = "Assets/Prefabs/GameServices/GameServices.prefab";
    private const string ScanlineTexturePath = "Assets/Sprites/Combat/BattleDevice/SCANLINE_FRAME_PMROOT.png";

    [Test]
    public void PromptPrefab_HasExplicitRejoinAndForfeitControls()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PromptPath);
        Assert.NotNull(prefab);

        PendingBattleResumePresenter presenter =
            prefab.GetComponentInChildren<PendingBattleResumePresenter>(true);
        PendingBattleResumeCoordinator coordinator =
            prefab.GetComponentInChildren<PendingBattleResumeCoordinator>(true);
        Assert.NotNull(presenter);
        Assert.NotNull(coordinator);

        Canvas canvas = prefab.GetComponent<Canvas>();
        Assert.NotNull(canvas);
        Assert.AreEqual(RenderMode.ScreenSpaceOverlay, canvas.renderMode);
        CanvasScaler scaler = prefab.GetComponent<CanvasScaler>();
        Assert.NotNull(scaler);
        Assert.AreEqual(CanvasScaler.ScaleMode.ScaleWithScreenSize, scaler.uiScaleMode);

        CanvasGroup canvasGroup = prefab.GetComponent<CanvasGroup>();
        Assert.NotNull(canvasGroup);
        Assert.AreEqual(0f, canvasGroup.alpha);
        Assert.IsFalse(canvasGroup.interactable);
        Assert.IsFalse(canvasGroup.blocksRaycasts);
        Image dimmer = prefab.GetComponent<Image>();
        Assert.NotNull(dimmer);
        Assert.IsFalse(dimmer.enabled);

        var coordinatorSerialized = new SerializedObject(coordinator);
        Assert.AreSame(
            presenter,
            coordinatorSerialized.FindProperty("presenter").objectReferenceValue);

        var serialized = new SerializedObject(presenter);
        Button primary = serialized.FindProperty("primaryButton").objectReferenceValue as Button;
        Button secondary = serialized.FindProperty("secondaryButton").objectReferenceValue as Button;
        Assert.NotNull(primary);
        Assert.NotNull(secondary);
        Assert.AreEqual("RejoinButton", primary.gameObject.name);
        Assert.AreEqual("ForfeitButton", secondary.gameObject.name);
        RectTransform safeArea = serialized.FindProperty("safeAreaRoot").objectReferenceValue as RectTransform;
        Assert.NotNull(safeArea);
        Assert.AreEqual("DeviceSafeArea", safeArea.name);

        GameObject instance = Object.Instantiate(prefab);
        try
        {
            PendingBattleResumePresenter live =
                instance.GetComponent<PendingBattleResumePresenter>();
            Assert.NotNull(live);
            Assert.IsFalse(live.IsOpen);
            CanvasGroup liveGroup = instance.GetComponent<CanvasGroup>();
            Assert.AreEqual(0f, liveGroup.alpha);
            Assert.IsFalse(liveGroup.blocksRaycasts);
            Assert.IsFalse(instance.GetComponent<Image>().enabled);
        }
        finally
        {
            Object.DestroyImmediate(instance);
        }

        EnsurePromptScanline();
        GameObject authored = AssetDatabase.LoadAssetAtPath<GameObject>(PromptPath);
        Transform scanline = FindNamed(authored.transform, "ScanlineOverlay");
        Assert.NotNull(scanline);
        RawImage scanlineImage = scanline.GetComponent<RawImage>();
        Assert.NotNull(scanlineImage);
        Assert.IsFalse(scanlineImage.raycastTarget);
        Assert.NotNull(scanlineImage.texture);
        Assert.AreEqual(0, scanline.GetSiblingIndex());
    }

    private static void EnsurePromptScanline()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PromptPath);
        try
        {
            Transform panel = FindNamed(root.transform, "Panel");
            Assert.NotNull(panel);
            Transform scanline = panel.Find("ScanlineOverlay");
            RawImage image = scanline != null ? scanline.GetComponent<RawImage>() : null;
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(ScanlineTexturePath);
            Assert.NotNull(texture);
            if (image == null)
            {
                var overlay = new GameObject("ScanlineOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
                overlay.transform.SetParent(panel, false);
                RectTransform rect = overlay.GetComponent<RectTransform>();
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = new Vector2(18f, 18f);
                rect.offsetMax = new Vector2(-18f, -18f);
                image = overlay.GetComponent<RawImage>();
                scanline = overlay.transform;
            }

            image.texture = texture;
            image.color = new Color(1f, 0.80f, 0.55f, 0.10f);
            image.raycastTarget = false;
            image.uvRect = new Rect(0f, 0f, 1f, 6f);
            scanline.SetSiblingIndex(0);
            PrefabUtility.SaveAsPrefabAsset(root, PromptPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static Transform FindNamed(Transform root, string name)
    {
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

    [Test]
    public void GameServices_ContainsPersistentPendingBattleCoordinator()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ServicesPath);
        Assert.NotNull(prefab);
        Assert.NotNull(prefab.GetComponentInChildren<PendingBattleResumeCoordinator>(true));
    }

    [Test]
    public void AuthoritativeEncounterProvider_RestoresSeedIdentityAndHp()
    {
        var state = new AuthoritativeClientBattleState
        {
            seed = 4123,
            battleKind = (int)Chainbound.Combat.Core.AuthoritativeBattleKind.Wild,
            encounterCaptureEnabled = true,
            zoneId = "grass-east",
        };
        state.targets.Add(new AuthoritativeClientBattleTarget
        {
            targetId = "enemy-7",
            speciesId = "sprig",
            displayName = "Sprig",
            currentHp = 17,
            maxHp = 40,
            isActive = true,
            stars = 2,
            level = 4,
            visualVariantId = ChainlingVisualIdentity.BaseVariantId,
        });

        EncounterSnapshot encounter = new AuthoritativeBattleEncounterProvider(state).GenerateEncounter(99);

        Assert.AreEqual(4123, encounter.encounterSeed);
        Assert.AreEqual("grass-east", encounter.zoneId);
        Assert.AreEqual(1, encounter.chainlings.Count);
        Assert.AreEqual("enemy-7", encounter.chainlings[0].backendChainlingId);
        Assert.AreEqual(17, encounter.chainlings[0].currentHp);
    }

    [Test]
    public void TimedOutPromptConfirmsContinueWhenKeyboardFocusWasCleared()
    {
        var root = new GameObject("Prompt");
        var primaryObject = new GameObject("Continue");
        var foreignObject = new GameObject("Play");
        primaryObject.transform.SetParent(root.transform);
        foreignObject.transform.SetParent(root.transform);
        Button primary = primaryObject.AddComponent<Button>();
        Button foreign = foreignObject.AddComponent<Button>();

        try
        {
            Assert.AreSame(primary, PendingBattleResumePresenter.ResolveConfirmTarget(primary, null, null));
            Assert.AreSame(
                primary,
                PendingBattleResumePresenter.ResolveConfirmTarget(primary, null, foreignObject));
            Assert.AreSame(
                primary,
                PendingBattleResumePresenter.ResolveConfirmTarget(primary, foreign, primaryObject));
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void OpenPromptConfirmsTheFocusedChoice()
    {
        var root = new GameObject("Prompt");
        var rejoinObject = new GameObject("Rejoin");
        var forfeitObject = new GameObject("Forfeit");
        rejoinObject.transform.SetParent(root.transform);
        forfeitObject.transform.SetParent(root.transform);
        Button rejoin = rejoinObject.AddComponent<Button>();
        Button forfeit = forfeitObject.AddComponent<Button>();

        try
        {
            Assert.AreSame(
                rejoin,
                PendingBattleResumePresenter.ResolveConfirmTarget(rejoin, forfeit, rejoinObject));
            Assert.AreSame(
                forfeit,
                PendingBattleResumePresenter.ResolveConfirmTarget(rejoin, forfeit, forfeitObject));
            Assert.AreSame(
                rejoin,
                PendingBattleResumePresenter.ResolveConfirmTarget(rejoin, forfeit, null));
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void UnansweredBattleProbeContinuesWhileAKnownSessionStillStops()
    {
        Assert.IsTrue(PendingBattleRouteUtility.ShouldContinueAfterProbe(
            new PendingBattleResumeResult(PendingBattleResumeOutcome.None)));
        Assert.IsTrue(PendingBattleRouteUtility.ShouldContinueAfterProbe(
            new PendingBattleResumeResult(PendingBattleResumeOutcome.Failed, message: "SIGNAL SYNC UNAVAILABLE — RETRYING")));
        Assert.IsFalse(PendingBattleRouteUtility.ShouldContinueAfterProbe(
            new PendingBattleResumeResult(
                PendingBattleResumeOutcome.Failed,
                new Chainbound.Combat.Core.CombatSessionEnvelope(),
                "Pending battle prompt closed before a choice was made.")));
        Assert.IsFalse(PendingBattleRouteUtility.ShouldContinueAfterProbe(
            new PendingBattleResumeResult(PendingBattleResumeOutcome.Rejoin)));
        Assert.IsFalse(PendingBattleRouteUtility.ShouldContinueAfterProbe(
            new PendingBattleResumeResult(PendingBattleResumeOutcome.Wipe)));
    }

    [Test]
    public void EditorStartsALocalEncounterWhenTheBattleServiceDoesNotAnswer()
    {
        Assert.IsTrue(EncounterTransitionBridge.ShouldUseLocalPracticeBattle(true, null));
        Assert.IsTrue(EncounterTransitionBridge.ShouldUseLocalPracticeBattle(true, "authority_request_failed"));
        Assert.IsFalse(EncounterTransitionBridge.ShouldUseLocalPracticeBattle(false, null));
        Assert.IsFalse(EncounterTransitionBridge.ShouldUseLocalPracticeBattle(true, "upgrade_required"));
    }
}
