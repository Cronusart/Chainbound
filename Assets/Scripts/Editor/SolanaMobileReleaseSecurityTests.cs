#if UNITY_EDITOR
using System.IO;
using Chainbound.Combat;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class SolanaMobileReleaseSecurityTests
{
    private const string LegacyWalletLinkPath = "supabase/functions/account_link_wallet/index.ts";
    private const string MainManifestPath = "Assets/Plugins/Android/AndroidManifest.xml";
    private const string MwaManifestPath = "Assets/Plugins/Android/SignalJamSolanaMwa.androidlib/src/main/AndroidManifest.xml";
    private const string MwaGradlePath = "Assets/Plugins/Android/SignalJamSolanaMwa.androidlib/build.gradle";
    private const string MwaBridgePath = "Assets/Plugins/Android/SignalJamSolanaMwa.androidlib/src/main/java/com/signaljam/solana/mwa/SignalJamMwaBridge.kt";
    private const string WalletServicePath = "Assets/Scripts/Solana/Wallet/SolanaWalletService.cs";
    private const string ClusterGuardPath = "Assets/Scripts/Solana/Config/SolanaClusterGuard.cs";
    private const string PlatformContextPath = "Assets/Scripts/Combat/Runtime/Accounts/PlatformContext.cs";
    private const string OwnershipGatewayPath = "Assets/Scripts/Combat/Runtime/Services/SupabaseOwnershipSpineGateway.cs";
    private const string CandyMachinePath = "Assets/Scripts/UI/MenuNavigation/CandyMachine/CandyMachineScreen.cs";
    private const string AndroidBuildCommandPath = "Assets/Scripts/Editor/DevelopmentAndroidBuildCommand.cs";
    private const string SupabaseAuthPath = "Assets/Scripts/Combat/Runtime/Accounts/Supabase/SupabaseAnonymousAuthSessionProvider.cs";
    private const string DevnetProfilePath = "Assets/Resources/DeploymentProfiles/DevnetOwnership.asset";
    private const string MainnetProfilePath = "Assets/Resources/DeploymentProfiles/MainnetSmoke.asset";
    private const string ReleaseValidatorPath = "Assets/Scripts/Editor/SolanaMobileReleaseValidator.cs";
    private const string SolanaBridgePath = "Assets/Scripts/Solana/Services/SolanaServiceBridge.cs";
    private const string WalletPromptPath = "Assets/Scripts/UI/MainMenu/WalletConnectionPromptService.cs";
    private const string WalletConnectPath = "supabase/functions/wallet_connect_verified/index.ts";
    private const string AccountSessionPath = "Assets/Scripts/Combat/Runtime/Accounts/AccountSessionService.cs";
    private const string WalletMigrationPath = "supabase/migrations/20260924061959_single_active_profile_wallet.sql";
    private const string WalletHardeningMigrationPath = "supabase/migrations/20261009143000_ram_v2_pre_rollout_hardening.sql";
    private const string WalletRestorePath = "supabase/functions/wallet_restore_profile/index.ts";
    private const string GameServicesPrefabPath = "Assets/Prefabs/GameServices/GameServices.prefab";
    private const string WalletPromptPrefabPath = "Assets/Prefabs/UI/WalletConnectionPrompt.prefab";

    [Test]
    public void LegacyWalletLinkEndpoint_IsFailClosedAndNonMutating()
    {
        string source = File.ReadAllText(LegacyWalletLinkPath);

        StringAssert.Contains("legacy_wallet_link_disabled", source);
        StringAssert.Contains("410", source);
        StringAssert.DoesNotContain("profile_wallets", source);
        StringAssert.DoesNotContain(".insert(", source);
    }

    [Test]
    public void AndroidWalletHostAndUnityActivities_AreLandscape()
    {
        string appManifest = File.ReadAllText(MainManifestPath);
        string walletManifest = File.ReadAllText(MwaManifestPath);

        Assert.GreaterOrEqual(Count(appManifest, "android:screenOrientation=\"sensorLandscape\""), 2);
        StringAssert.Contains("SignalJamMwaActivity", walletManifest);
        StringAssert.Contains("android:screenOrientation=\"sensorLandscape\"", walletManifest);
    }

    [Test]
    public void AndroidWalletClient_PinsReviewedOfficialVersion()
    {
        string gradle = File.ReadAllText(MwaGradlePath);
        StringAssert.Contains("com.solanamobile:mobile-wallet-adapter-clientlib-ktx:2.0.7", gradle);
        StringAssert.Contains("id 'org.jetbrains.kotlin.android'", gradle);

        string validator = File.ReadAllText(ReleaseValidatorPath);
        StringAssert.Contains("KotlinPluginVersion = \"2.1.10\"", validator);
        StringAssert.Contains("version '{KotlinPluginVersion}' apply false", validator);
    }

    [Test]
    public void AndroidWalletClient_SelectsClusterBeforeRestoringAuthorization()
    {
        string nativeBridge = File.ReadAllText(MwaBridgePath);
        int clusterAssignment = nativeBridge.IndexOf("adapter.rpcCluster =", System.StringComparison.Ordinal);
        int tokenRestore = nativeBridge.IndexOf("adapter.authToken = request.authToken", System.StringComparison.Ordinal);

        StringAssert.Contains("val cluster: String", nativeBridge);
        StringAssert.Contains("RpcCluster.Devnet", nativeBridge);
        StringAssert.Contains("RpcCluster.MainnetBeta", nativeBridge);
        Assert.That(clusterAssignment, Is.GreaterThanOrEqualTo(0));
        Assert.That(tokenRestore, Is.GreaterThan(clusterAssignment),
            "MWA clears authorization when blockchain changes, so cluster selection must happen first.");

        string walletService = File.ReadAllText(WalletServicePath);
        StringAssert.Contains("mwa.authToken.", walletService);
        StringAssert.Contains("never consider it for Mainnet authorization", walletService);
        StringAssert.Contains("PersistAuthorizationToken(submission.AuthToken)", walletService);
        StringAssert.Contains("PersistAuthorizationToken(signedMessage.AuthToken)", walletService);
    }

    [Test]
    public void WalletService_NormalizesSupportedMwaClustersAndScopesTokenKeys()
    {
        System.Reflection.MethodInfo normalize = typeof(SolanaWalletService).GetMethod(
            "NormalizeMwaCluster",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        System.Reflection.MethodInfo tokenKey = typeof(SolanaWalletService).GetMethod(
            "GetAuthTokenStorageKey",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        Assert.NotNull(normalize);
        Assert.NotNull(tokenKey);
        Assert.AreEqual("devnet", normalize.Invoke(null, new object[] { " DEVNET " }));
        Assert.AreEqual("mainnet-beta", normalize.Invoke(null, new object[] { "mainnet" }));
        Assert.AreEqual("mainnet-beta", normalize.Invoke(null, new object[] { "mainnet-beta" }));
        Assert.AreEqual("mwa.authToken.devnet", tokenKey.Invoke(null, new object[] { "devnet" }));
        Assert.AreEqual("mwa.authToken.mainnet-beta", tokenKey.Invoke(null, new object[] { "mainnet" }));

        var error = Assert.Throws<System.Reflection.TargetInvocationException>(
            () => normalize.Invoke(null, new object[] { "testnet" }));
        Assert.IsInstanceOf<System.InvalidOperationException>(error.InnerException);
    }

    [Test]
    public void DeploymentProfiles_KeepDevnetOperationalAndMainnetFailClosedUntilProvisioned()
    {
        PlatformBuildProfileSO devnet = AssetDatabase.LoadAssetAtPath<PlatformBuildProfileSO>(DevnetProfilePath);
        PlatformBuildProfileSO mainnet = AssetDatabase.LoadAssetAtPath<PlatformBuildProfileSO>(MainnetProfilePath);

        Assert.NotNull(devnet);
        Assert.NotNull(mainnet);
        Assert.AreEqual(OwnershipDeploymentEnvironment.DevnetOwnership, devnet.OwnershipEnvironment);
        Assert.AreEqual(OwnershipDeploymentEnvironment.MainnetSmoke, mainnet.OwnershipEnvironment);
        Assert.AreEqual("devnet", devnet.BackendEnvironment);
        Assert.AreEqual("mainnet-smoke", mainnet.BackendEnvironment);
        Assert.IsTrue(devnet.AllowDevelopmentTools);
        Assert.IsFalse(mainnet.AllowDevelopmentTools);
        Assert.AreNotSame(devnet.SolanaConfig, mainnet.SolanaConfig);
        Assert.AreNotSame(devnet.SupabaseConfig, mainnet.SupabaseConfig);
        Assert.IsEmpty(OwnershipDeploymentProfileRules.Validate(devnet, requireComplete: true));
        Assert.AreEqual("DP3rTx145BPfunueM9SHjYJXBDCK5GmrdwZjpCvgf6U", devnet.SolanaConfig.coreArchivesCollectionId);
        Assert.AreEqual("6wPnru8RDTsnCyujzPMq8v9U8pD6ynRa8es3ZXcesbNs", devnet.SolanaConfig.coreCoversCollectionId);
        Assert.AreEqual("76aVsw9xScewNG4wkiE75asdM5bbZaHS5CG78263yMcV", devnet.SolanaConfig.coreCosmeticsCollectionId);
        Assert.IsNotEmpty(OwnershipDeploymentProfileRules.Validate(mainnet, requireComplete: true),
            "Mainnet Smoke must remain non-buildable until its real isolated infrastructure is supplied.");
    }

    [Test]
    public void DeploymentBoundary_RequiresExplicitSelectionAndNeverAutoEnablesMainnetDevTools()
    {
        string buildCommand = File.ReadAllText(AndroidBuildCommandPath);
        string context = File.ReadAllText(PlatformContextPath);
        string gateway = File.ReadAllText(OwnershipGatewayPath);
        string candy = File.ReadAllText(CandyMachinePath);

        StringAssert.Contains("CHAINBOUND_DEVNET_OWNERSHIP", buildCommand);
        StringAssert.Contains("CHAINBOUND_MAINNET_SMOKE", buildCommand);
        StringAssert.Contains("CHAINBOUND_DEVNET_OWNERSHIP || CHAINBOUND_MAINNET_SMOKE", context);
        StringAssert.Contains("return Application.isEditor ? fallback : null", context);
        StringAssert.Contains("opposite Mainnet deployment define", File.ReadAllText(ReleaseValidatorPath));
        StringAssert.Contains("opposite Devnet deployment define", File.ReadAllText(ReleaseValidatorPath));
        StringAssert.DoesNotContain("#if DEVELOPMENT_BUILD", gateway);
        StringAssert.Contains("ResolveOwnershipRuntimeEnabled", gateway);
        StringAssert.Contains("ResolveAllowDevelopmentTools", candy);
    }

    [Test]
    public void WalletAndProfileCaches_AreEnvironmentScopedAndRpcGenesisIsVerified()
    {
        string wallet = File.ReadAllText(WalletServicePath);
        string guard = File.ReadAllText(ClusterGuardPath);
        string context = File.ReadAllText(PlatformContextPath);
        string bridge = File.ReadAllText(SolanaBridgePath);
        string account = File.ReadAllText(AccountSessionPath);
        string auth = File.ReadAllText(SupabaseAuthPath);

        StringAssert.Contains("SolanaClusterGuard.EnsureExpectedClusterAsync", wallet);
        StringAssert.Contains("getGenesisHash", File.ReadAllText("Assets/Scripts/Solana/Rpc/SolanaRpcService.cs"));
        StringAssert.Contains("RPC cluster mismatch", guard);
        StringAssert.Contains("ChainboundLocalConfig", context);
        StringAssert.Contains("ApplyLocalSolanaOverrides", context);
        StringAssert.Contains("ApplyLocalSolanaOverrides(sourceConfig, false)", File.ReadAllText(ReleaseValidatorPath));
        StringAssert.DoesNotContain("ResolveRpcEndpoint", bridge,
            "Wallet verification and RPC services must resolve one shared runtime Solana config.");
        StringAssert.Contains("SupabaseAuthSessionContext.AccessToken", File.ReadAllText("Assets/Scripts/Solana/Rpc/SolanaRpcService.cs"));
        StringAssert.Contains("/solana_rpc_proxy", File.ReadAllText("Assets/Scripts/Solana/Rpc/SolanaRpcService.cs"));
        StringAssert.Contains("BuildScopedStorageKey", account);
        StringAssert.Contains("BuildScopedStorageKey", auth);
        StringAssert.Contains("DevnetOwnership", auth,
            "Only Devnet may migrate the legacy unscoped auth cache.");
    }

    [Test]
    public void ClosedBetaBridge_DoesNotComposeGatedServices()
    {
        string source = File.ReadAllText(SolanaBridgePath);
        string[] gated = { "new SupabaseOwnershipSpineGateway(", "enableDevnetOwnershipSpine = true" };
        foreach (string constructor in gated)
            StringAssert.DoesNotContain(constructor, source);
    }

    [Test]
    public void MainMenuWalletPrompt_UsesCompleteVerifiedConnectFlow()
    {
        string source = File.ReadAllText(WalletPromptPath);

        StringAssert.Contains("flow.ConnectWalletToCurrentProfileAsync()", source);
        StringAssert.DoesNotContain("flow.LinkWalletToCurrentProfile(", source);
        StringAssert.Contains("ShowAccessAsync", source);
    }

    [Test]
    public void CandidateWalletConnection_DoesNotOverwriteLinkedProfileIdentity()
    {
        string source = File.ReadAllText(AccountSessionPath);
        int connectStart = source.IndexOf("public async Task<WalletConnectResult> ConnectWalletAsync", System.StringComparison.Ordinal);
        int verifiedStart = source.IndexOf("public async Task<PlayerAccountSession> ConnectWalletAndResolveSessionAsync", connectStart, System.StringComparison.Ordinal);
        string connectBody = source.Substring(connectStart, verifiedStart - connectStart);

        StringAssert.DoesNotContain("walletIdentity.walletPublicKey =", connectBody);
        StringAssert.DoesNotContain("walletIdentity.isActive = false", source);
        StringAssert.Contains("wallet_mismatch_for_profile", source);
    }

    [Test]
    public void WalletDatabase_EnforcesPermanentOneToOneBindingAndOneActiveSessionLink()
    {
        string migration = File.ReadAllText(WalletMigrationPath);
        StringAssert.Contains("ux_profile_wallets_active_profile", migration);
        StringAssert.Contains("PARTITION BY player_profile_id", migration);
        StringAssert.Contains("WHERE is_active = true", migration);
        StringAssert.Contains("AND unlinked_at IS NULL", migration);

        string hardening = File.ReadAllText(WalletHardeningMigrationPath);
        StringAssert.Contains("bound_wallet_address", hardening);
        StringAssert.Contains("player_profiles_bound_wallet_address_key", hardening);
        StringAssert.Contains("wallet_binding_is_permanent", hardening);
        StringAssert.Contains("wallet_already_bound_to_profile", hardening);
        StringAssert.Contains("wallet_bound_to_other_profile", hardening);
    }

    [Test]
    public void VerifiedConnect_UsesReadOnlyResolveAndServiceRolePermanentBindingTransactions()
    {
        string function = File.ReadAllText(WalletConnectPath);
        string migration = File.ReadAllText(WalletHardeningMigrationPath);

        StringAssert.Contains("wallet_resolve_bound_profile_service", function);
        StringAssert.Contains("wallet_bind_profile_service", function);
        StringAssert.Contains("wallet_create_bound_profile_service", function);
        StringAssert.Contains("mode === \"resolve\"", function);
        StringAssert.DoesNotContain("profile_recover_by_wallet_tx", function);
        StringAssert.Contains("pg_advisory_xact_lock", migration);
        StringAssert.Contains(
            "REVOKE ALL ON FUNCTION public.wallet_resolve_bound_profile_service(TEXT) FROM PUBLIC, anon, authenticated",
            migration);
        StringAssert.Contains(
            "GRANT EXECUTE ON FUNCTION public.wallet_create_bound_profile_service(TEXT, UUID, TEXT, TEXT, TEXT) TO service_role",
            migration);
    }

    [Test]
    public void GameMenuPrefab_OffersPlayAndConnectWithoutSeparateRestoreAction()
    {
        const string path = "Assets/Prefabs/UI/MainMenu/GameMenuPanel.prefab";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        Assert.NotNull(prefab);

        Transform newGameView = prefab.transform.Find("NewGameView");
        Assert.NotNull(newGameView);
        Assert.NotNull(newGameView.Find("NewGameButton"));
        Assert.NotNull(newGameView.Find("ConnectWalletButton"));
        Assert.IsNull(newGameView.Find("RestoreProfileButton"));

        Button[] buttons = newGameView.GetComponentsInChildren<Button>(true);
        Assert.AreEqual(2, buttons.Length);
        foreach (Button button in buttons)
            Assert.AreEqual(Navigation.Mode.Explicit, button.navigation.mode, button.name);
    }

    [Test]
    public void WalletAccessPrefab_HasPersistentCoordinatorSafeAreaAndTouchTargets()
    {
        GameObject promptPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(WalletPromptPrefabPath);
        Assert.NotNull(promptPrefab, "The scene-authored wallet prompt must have a reusable prefab.");
        WalletConnectionPromptService prefabPresenter =
            promptPrefab.GetComponent<WalletConnectionPromptService>();
        Assert.NotNull(prefabPresenter);
        Assert.IsFalse(promptPrefab.activeSelf);
        Transform authoredPanel = promptPrefab.transform.Find("Panel");
        Assert.NotNull(authoredPanel, "The original wallet panel must remain authored.");
        Assert.NotNull(authoredPanel.Find("Title"));
        Assert.NotNull(authoredPanel.Find("Body"));
        Assert.NotNull(authoredPanel.Find("Status"));
        Assert.NotNull(authoredPanel.Find("WalletPromptConnectButton"));
        Assert.NotNull(authoredPanel.Find("WalletPromptSkipButton"));
        Assert.NotNull(authoredPanel.Find("WalletPromptRestoreButton"));

        Transform theme = authoredPanel.Find("PanelThemeBackground");
        Assert.NotNull(theme, "Wallet prompt should reuse the authored panel tint/scanline language.");
        SignalJam.UI.MenuNavigation.PlayerMenuBackgroundController themeController =
            theme.GetComponent<SignalJam.UI.MenuNavigation.PlayerMenuBackgroundController>();
        Assert.NotNull(themeController);
        Assert.IsTrue(themeController.RespectAuthoredPresentation);
        Assert.IsTrue(themeController.ScanlinesEnabled);
        Image themeTint = theme.Find("BaseTint")?.GetComponent<Image>();
        RawImage themeScanlines = theme.Find("ScanlineOverlay")?.GetComponent<RawImage>();
        Assert.NotNull(themeTint);
        Assert.NotNull(themeScanlines);
        Assert.IsFalse(themeTint.raycastTarget);
        Assert.IsFalse(themeScanlines.raycastTarget);
        Canvas promptCanvas = promptPrefab.GetComponent<Canvas>();
        Assert.NotNull(promptCanvas);
        Assert.IsTrue(promptCanvas.overrideSorting);
        Assert.That(promptCanvas.sortingOrder, Is.GreaterThanOrEqualTo(500));
        Assert.NotNull(promptPrefab.GetComponent<GraphicRaycaster>());
        CanvasGroup promptGroup = promptPrefab.GetComponent<CanvasGroup>();
        Assert.NotNull(promptGroup);
        Assert.AreEqual(1f, promptGroup.alpha);

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(GameServicesPrefabPath);
        Assert.NotNull(prefab);
        ProfileWalletAccessCoordinator coordinator = prefab.GetComponent<ProfileWalletAccessCoordinator>();
        Assert.NotNull(coordinator);
        Assert.NotNull(prefab.GetComponent<WalletOperationGuard>());
        WalletConnectionPromptService[] presenters =
            prefab.GetComponentsInChildren<WalletConnectionPromptService>(true);
        Assert.AreEqual(1, presenters.Length, "Exactly one persistent prompt presenter must own wallet access.");
        WalletConnectionPromptService prompt = presenters[0];
        Assert.NotNull(prompt);
        Transform walletCanvas = prefab.transform.Find("WalletAccessCanvas");
        Assert.NotNull(walletCanvas);
        Transform safeAreaTransform = walletCanvas.Find("SafeArea");
        Assert.NotNull(safeAreaTransform);
        SignalJam.UI.MobileControls.SafeAreaAdapter safeArea =
            safeAreaTransform.GetComponent<SignalJam.UI.MobileControls.SafeAreaAdapter>();
        Assert.NotNull(safeArea);

        Transform panel = safeArea.transform.Find("WalletConnectionPrompt");
        Assert.NotNull(panel);
        Assert.AreSame(panel.gameObject, prompt.gameObject,
            "The reusable prompt prefab must own its presenter and activation state.");
        Assert.IsFalse(panel.gameObject.activeSelf);
        Assert.IsNull(safeArea.GetComponent<WalletConnectionPromptService>());
        Assert.AreEqual(
            WalletPromptPrefabPath,
            PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(panel.gameObject));

        SerializedObject serializedPrompt = new SerializedObject(prompt);
        Assert.AreSame(panel.gameObject,
            serializedPrompt.FindProperty("root").objectReferenceValue);

        SerializedObject serializedCoordinator = new SerializedObject(coordinator);
        Assert.AreSame(prompt,
            serializedCoordinator.FindProperty("promptPresenter").objectReferenceValue);

        Button[] buttons = prompt.GetComponentsInChildren<Button>(true);
        Assert.That(buttons.Length, Is.GreaterThanOrEqualTo(2));
        foreach (Button button in buttons)
        {
            LayoutElement layout = button.GetComponent<LayoutElement>();
            Assert.NotNull(layout, $"{button.name} needs an authored touch target.");
            Assert.That(layout.minHeight, Is.GreaterThanOrEqualTo(60f), button.name);
            Assert.AreEqual(Navigation.Mode.Explicit, button.navigation.mode, button.name);
        }
    }

    [Test]
    public void WalletPrompt_FirstRequestedActivation_RemainsOpenAndSelectable()
    {
        GameObject root = new GameObject("WalletPromptTestRoot", typeof(RectTransform), typeof(CanvasGroup));
        try
        {
            TextMeshProUGUI title = CreateText(root.transform, "Title");
            TextMeshProUGUI body = CreateText(root.transform, "Body");
            TextMeshProUGUI status = CreateText(root.transform, "Status");
            Button connect = CreateButton(root.transform, "Connect");
            Button offline = CreateButton(root.transform, "Offline");
            Button retry = CreateButton(root.transform, "Retry");

            root.SetActive(false);
            WalletConnectionPromptService presenter = root.AddComponent<WalletConnectionPromptService>();
            SerializedObject serializedPresenter = new SerializedObject(presenter);
            serializedPresenter.FindProperty("root").objectReferenceValue = root;
            serializedPresenter.FindProperty("titleText").objectReferenceValue = title;
            serializedPresenter.FindProperty("bodyText").objectReferenceValue = body;
            serializedPresenter.FindProperty("statusText").objectReferenceValue = status;
            serializedPresenter.FindProperty("connectButton").objectReferenceValue = connect;
            serializedPresenter.FindProperty("skipButton").objectReferenceValue = offline;
            serializedPresenter.FindProperty("restoreButton").objectReferenceValue = retry;
            serializedPresenter.ApplyModifiedPropertiesWithoutUndo();

            var task = presenter.ShowAccessAsync(new WalletAccessPromptModel
            {
                showPrimary = true,
                primaryLabel = "Connect Linked Wallet",
                primaryChoice = WalletAccessPromptChoice.Connect,
                showSecondary = true,
                secondaryLabel = "Continue Offline",
                secondaryChoice = WalletAccessPromptChoice.ContinueOffline,
                showTertiary = false,
            });

            Assert.IsTrue(root.activeInHierarchy,
                "A requested first activation must not be hidden by Awake.");
            Assert.IsFalse(task.IsCompleted);
            // EditMode has no live UI EventSystem. Runtime default selection is
            // guarded here by the presenter's explicit selection call.
            string presenterSource = File.ReadAllText(WalletPromptPath);
            StringAssert.Contains("ShowPrompt(defaultButton)", presenterSource);
            StringAssert.Contains("Select(defaultButton)", presenterSource);
            StringAssert.DoesNotContain("Destroy(gameObject)", presenterSource,
                "A persistent fallback must not destroy a scene-authored presenter.");
            StringAssert.DoesNotContain("group.alpha =", presenterSource,
                "Runtime prompting must preserve the authored CanvasGroup appearance.");
            StringAssert.DoesNotContain("SetAsLastSibling", presenterSource,
                "Runtime prompting must preserve the authored hierarchy order.");
            Assert.AreSame(offline, connect.navigation.selectOnDown);
            Assert.AreSame(connect, offline.navigation.selectOnUp);

            Assert.IsTrue(presenter.TryCancel());
            Assert.IsTrue(task.IsCompleted);
            Assert.AreEqual(WalletAccessPromptChoice.Cancel, task.Result);
            Assert.IsFalse(root.activeSelf);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void WalletPrompt_CancelRestoresPreviousSelection()
    {
        GameObject eventSystemObject = new GameObject("WalletPromptEventSystem", typeof(EventSystem));
        EventSystem eventSystem = eventSystemObject.GetComponent<EventSystem>();
        GameObject returnTargetObject = new GameObject(
            "ReturnTarget",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button));
        GameObject root = new GameObject("WalletPromptTestRoot", typeof(RectTransform), typeof(CanvasGroup));
        try
        {
            TextMeshProUGUI title = CreateText(root.transform, "Title");
            TextMeshProUGUI body = CreateText(root.transform, "Body");
            TextMeshProUGUI status = CreateText(root.transform, "Status");
            Button connect = CreateButton(root.transform, "Connect");
            Button offline = CreateButton(root.transform, "Offline");
            Button retry = CreateButton(root.transform, "Retry");

            root.SetActive(false);
            WalletConnectionPromptService presenter = root.AddComponent<WalletConnectionPromptService>();
            SerializedObject serializedPresenter = new SerializedObject(presenter);
            serializedPresenter.FindProperty("root").objectReferenceValue = root;
            serializedPresenter.FindProperty("titleText").objectReferenceValue = title;
            serializedPresenter.FindProperty("bodyText").objectReferenceValue = body;
            serializedPresenter.FindProperty("statusText").objectReferenceValue = status;
            serializedPresenter.FindProperty("connectButton").objectReferenceValue = connect;
            serializedPresenter.FindProperty("skipButton").objectReferenceValue = offline;
            serializedPresenter.FindProperty("restoreButton").objectReferenceValue = retry;
            serializedPresenter.ApplyModifiedPropertiesWithoutUndo();

            System.Reflection.MethodInfo enableEventSystem = typeof(EventSystem).GetMethod(
                "OnEnable",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.NotNull(enableEventSystem);
            enableEventSystem.Invoke(eventSystem, null);
            EventSystem.current.SetSelectedGameObject(returnTargetObject);
            var task = presenter.ShowAccessAsync(new WalletAccessPromptModel());
            Assert.AreSame(connect.gameObject, EventSystem.current.currentSelectedGameObject);

            Assert.IsTrue(presenter.TryCancel());
            Assert.IsTrue(task.IsCompleted);
            Assert.AreSame(returnTargetObject, EventSystem.current.currentSelectedGameObject);
        }
        finally
        {
            System.Reflection.MethodInfo disableEventSystem = typeof(EventSystem).GetMethod(
                "OnDisable",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            disableEventSystem?.Invoke(eventSystem, null);
            if (EventSystem.current == eventSystem)
                EventSystem.current = null;
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(returnTargetObject);
            Object.DestroyImmediate(eventSystemObject);
        }
    }

    [Test]
    public void CandyMachine_StampPromptExplainsWalletAndOperationErrorsRestoreFocus()
    {
        string source = File.ReadAllText(CandyMachinePath);
        StringAssert.Contains("A LINKED WALLET IS REQUIRED; WE'LL PROMPT IF NEEDED.", source);
        StringAssert.Contains("RestoreOperationFocus();", source);
        StringAssert.Contains("FocusDetailPrimary();", source);
        StringAssert.Contains("FocusSelectedRow();", source);
        StringAssert.Contains("FocusModeTab();", source);
    }

    private static TextMeshProUGUI CreateText(Transform parent, string name)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        return textObject.GetComponent<TextMeshProUGUI>();
    }

    private static Button CreateButton(Transform parent, string name)
    {
        GameObject buttonObject = new GameObject(
            name,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        return buttonObject.GetComponent<Button>();
    }

    [Test]
    public void WalletAccessCoordinator_AuthorsRequiredPromptCopyAndShortAddress()
    {
        string source = File.ReadAllText("Assets/Scripts/Core/Services/ProfileWalletAccessCoordinator.cs");
        StringAssert.Contains("Connect Linked Wallet", source);
        StringAssert.Contains("Continue Offline", source);
        StringAssert.Contains("Connect & Link Wallet", source);
        StringAssert.Contains("Wrong Wallet", source);
        StringAssert.Contains("Substring(0, 4)", source);
    }

    [Test]
    public void WalletAccessCoordinator_ReportsClosedBetaGateSeparatelyFromProofFailure()
    {
        System.Reflection.MethodInfo method = typeof(ProfileWalletAccessCoordinator).GetMethod(
            "BuildRetryPrompt",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(method);

        var model = (WalletAccessPromptModel)method.Invoke(
            null,
            new object[] { null, "wallet_connect_verified returned 403: beta_not_allowlisted", false });

        Assert.AreEqual("Closed Beta Access Required", model.title);
        StringAssert.Contains("wallet proof succeeded", model.body.ToLowerInvariant());
        Assert.AreEqual(MainMenuAccountStatusCopy.BetaAccessRequired, model.status);
    }

    [Test]
    public void ClosedBetaInstructionPolicy_FailsClosed()
    {
        Assert.IsTrue(ClosedBetaFeaturePolicy.IsPlayerInstructionAllowed("create_profile"));
        Assert.IsTrue(ClosedBetaFeaturePolicy.IsPlayerInstructionAllowed("update_position"));
        foreach (string denied in new[]
        {
            "update_wins_losses", "roll_known_move", "prepare_stamp_ticket", "stamp_known_move",
            "add_chainling_from_capture", "create_chainling", "spend_ram", "set_equipped_slot", "ranked_wager", "unknown"
        })
            Assert.IsFalse(ClosedBetaFeaturePolicy.IsPlayerInstructionAllowed(denied), denied);
    }

    [TestCase(WalletConnectPath)]
    [TestCase(WalletRestorePath)]
    public void ClosedBetaMembership_IsCheckedOnlyAfterCryptographicProof(string path)
    {
        string source = File.ReadAllText(path);
        int proof = source.IndexOf("if (!verified && !devBypass)", System.StringComparison.Ordinal);
        int allowlist = source.IndexOf("isApprovedBetaWallet", proof + 1, System.StringComparison.Ordinal);
        Assert.That(proof, Is.GreaterThanOrEqualTo(0));
        Assert.That(allowlist, Is.GreaterThan(proof));
        StringAssert.Contains("beta_not_allowlisted", source);
    }

    [TestCase("NO_WALLET_FOUND: No compatible wallet", MainMenuAccountStatusCopy.WalletUnavailable)]
    [TestCase("REQUEST_IN_PROGRESS: Another wallet request is already active.", MainMenuAccountStatusCopy.WalletBusy)]
    [TestCase("legacy_wallet_link_disabled", MainMenuAccountStatusCopy.ClientUpdateRequired)]
    [TestCase("beta_not_allowlisted", MainMenuAccountStatusCopy.BetaAccessRequired)]
    public void WalletErrors_MapToPlayerFacingCopy(string error, string expected)
    {
        Assert.AreEqual(expected, MainMenuAccountStatusCopy.Resolve(error));
    }

    private static int Count(string source, string value)
    {
        int count = 0;
        int offset = 0;
        while ((offset = source.IndexOf(value, offset, System.StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += value.Length;
        }
        return count;
    }
}
#endif
