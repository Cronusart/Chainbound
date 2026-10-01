using UnityEngine;

namespace Chainbound.Combat
{
    [DefaultExecutionOrder(-10000)]
    public class PlatformContext : MonoBehaviour
    {
        [SerializeField] private PlatformBuildProfileSO buildProfile;
        [SerializeField] private bool enableDebugLogs;

        private static PlatformContext instance;
        private PlatformBuildProfileSO activeBuildProfile;

        public static PlatformContext Instance => instance;
        public PlatformBuildProfileSO ActiveBuildProfile => activeBuildProfile != null ? activeBuildProfile : buildProfile;

        public BuildPlatform ActiveBuildPlatform => ActiveBuildProfile != null
            ? ActiveBuildProfile.BuildPlatform
            : InferBuildPlatform();

        public PlatformCapabilities Capabilities => ActiveBuildProfile != null
            ? ActiveBuildProfile.Capabilities
            : PlatformCapabilities.ForBuild(ActiveBuildPlatform);

        public bool WalletForwardStart => ActiveBuildProfile != null && ActiveBuildProfile.WalletForwardStart;
        public string BackendEnvironment => ActiveBuildProfile != null ? ActiveBuildProfile.BackendEnvironment : "disabled";

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            activeBuildProfile = ResolveCompiledProfile(buildProfile);
            if (!Application.isEditor && !HasExplicitDeploymentSelection)
            {
                activeBuildProfile = null;
                Debug.LogError("[PlatformContext] Player build has no explicit DevnetOwnership or MainnetSmoke deployment selection. Ownership and backend access are disabled.");
            }
            if (enableDebugLogs)
                Debug.Log($"[PlatformContext] Active platform: {ActiveBuildPlatform}; deployment: {ResolveOwnershipEnvironment()}");
        }

        private void OnDestroy()
        {
            if (instance == this)
                instance = null;
        }

        public static BuildPlatform ResolveActiveBuildPlatform()
        {
            return instance != null ? instance.ActiveBuildPlatform : InferBuildPlatform();
        }

        public static PlatformCapabilities ResolveCapabilities()
        {
            return instance != null ? instance.Capabilities : PlatformCapabilities.ForBuild(InferBuildPlatform());
        }

        public static PlatformBuildProfileSO ResolveActiveBuildProfile()
        {
            if (instance != null)
                return instance.ActiveBuildProfile;
            return ResolveCompiledProfile(null);
        }

        public static SolanaConfigSO ResolveSolanaConfig(SolanaConfigSO fallback = null)
        {
            PlatformBuildProfileSO profile = ResolveActiveBuildProfile();
            if (profile != null && profile.SolanaConfig != null)
                return profile.SolanaConfig;
            return Application.isEditor ? fallback : null;
        }

        public static SupabaseConfigSO ResolveSupabaseConfig(SupabaseConfigSO fallback = null)
        {
            PlatformBuildProfileSO profile = ResolveActiveBuildProfile();
            if (profile != null && profile.SupabaseConfig != null)
                return profile.SupabaseConfig;
            return Application.isEditor ? fallback : null;
        }

        public static OwnershipDeploymentEnvironment ResolveOwnershipEnvironment()
        {
            PlatformBuildProfileSO profile = ResolveActiveBuildProfile();
            return profile != null ? profile.OwnershipEnvironment : OwnershipDeploymentEnvironment.Disabled;
        }

        public static bool ResolveOwnershipRuntimeEnabled()
        {
            PlatformBuildProfileSO profile = ResolveActiveBuildProfile();
            return profile != null && profile.EnableOwnershipRuntime;
        }

        public static bool ResolveAllowDevelopmentTools()
        {
            PlatformBuildProfileSO profile = ResolveActiveBuildProfile();
            return profile != null && profile.AllowDevelopmentTools;
        }

        public static string BuildScopedStorageKey(string prefix)
        {
            PlatformBuildProfileSO profile = ResolveActiveBuildProfile();
            string scope = profile != null ? profile.BackendEnvironment : "disabled";
            if (string.IsNullOrWhiteSpace(scope))
                scope = "disabled";
            return (prefix ?? "signaljam") + "." + scope.Trim().ToLowerInvariant().Replace(' ', '-');
        }

        public static bool HasExplicitDeploymentSelection
        {
            get
            {
#if CHAINBOUND_DEVNET_OWNERSHIP || CHAINBOUND_MAINNET_SMOKE
                return true;
#else
                return false;
#endif
            }
        }

        private static PlatformBuildProfileSO ResolveCompiledProfile(PlatformBuildProfileSO fallback)
        {
#if CHAINBOUND_MAINNET_SMOKE
            return Resources.Load<PlatformBuildProfileSO>(OwnershipDeploymentProfileRules.MainnetResourcePath);
#elif CHAINBOUND_DEVNET_OWNERSHIP
            return Resources.Load<PlatformBuildProfileSO>(OwnershipDeploymentProfileRules.DevnetResourcePath);
#else
            if (Application.isEditor)
            {
                PlatformBuildProfileSO editorProfile = Resources.Load<PlatformBuildProfileSO>(
                    OwnershipDeploymentProfileRules.DevnetResourcePath);
                return editorProfile != null ? editorProfile : fallback;
            }
            return null;
#endif
        }

        private static BuildPlatform InferBuildPlatform()
        {
#if SIGNALJAM_BUILD_SOLANA_DAPP_STORE
            return BuildPlatform.SolanaDappStore;
#elif SIGNALJAM_BUILD_GOOGLE_PLAY
            return BuildPlatform.GooglePlay;
#elif SIGNALJAM_BUILD_IOS_APP_STORE
            return BuildPlatform.IOSAppStore;
#elif UNITY_EDITOR
            return BuildPlatform.Editor;
#elif UNITY_IOS
            return BuildPlatform.IOSAppStore;
#elif UNITY_ANDROID
            return BuildPlatform.GooglePlay;
#else
            return BuildPlatform.Editor;
#endif
        }
    }
}
