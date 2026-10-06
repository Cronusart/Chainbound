using System;
using System.IO;
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
        private SolanaConfigSO activeSolanaConfig;
        private bool solanaConfigResolutionFailed;

        private const string RpcEndpointEnvVar = "CHAINBOUND_SOLANA_RPC_ENDPOINT";
        private const string LocalConfigResourcePath = "ChainboundLocalConfig";
        private const string LegacyRuntimeConfigFileName = "Config.json";

        [Serializable]
        private sealed class RuntimeConfigOverrides
        {
            public string ownershipCluster;
            public string solanaRpcEndpoint;
            public string solanaWsEndpoint;
        }

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

            try
            {
                activeSolanaConfig = ApplyLocalSolanaOverrides(activeBuildProfile != null
                    ? activeBuildProfile.SolanaConfig
                    : null);
            }
            catch (Exception exception)
            {
                solanaConfigResolutionFailed = true;
                activeSolanaConfig = null;
                Debug.LogError("[PlatformContext] Local Solana RPC configuration is invalid. Ownership and wallet access are disabled. " + exception.Message);
            }

            if (enableDebugLogs)
                Debug.Log($"[PlatformContext] Active platform: {ActiveBuildPlatform}; deployment: {ResolveOwnershipEnvironment()}");
        }

        private void OnDestroy()
        {
            if (activeSolanaConfig != null &&
                (activeBuildProfile == null || !ReferenceEquals(activeSolanaConfig, activeBuildProfile.SolanaConfig)))
            {
                Destroy(activeSolanaConfig);
            }

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
            if (instance != null)
            {
                if (instance.solanaConfigResolutionFailed)
                    return null;
                if (instance.activeSolanaConfig != null)
                    return instance.activeSolanaConfig;
            }

            PlatformBuildProfileSO profile = ResolveActiveBuildProfile();
            if (profile != null && profile.SolanaConfig != null)
                return ApplyLocalSolanaOverrides(profile.SolanaConfig);
            return Application.isEditor ? fallback : null;
        }

        /// <summary>
        /// Produces a runtime-only copy of the deployment config when a local client RPC override
        /// is present. The cluster and expected genesis hash remain owned by the deployment asset.
        /// </summary>
        public static SolanaConfigSO ApplyLocalSolanaOverrides(
            SolanaConfigSO source,
            bool includeEditorOnlySources = true)
        {
            if (source == null)
                return null;

            RuntimeConfigOverrides overrides = null;
            string endpoint = null;

#if UNITY_EDITOR
            if (includeEditorOnlySources)
                endpoint = Environment.GetEnvironmentVariable(RpcEndpointEnvVar);
#endif

            if (string.IsNullOrWhiteSpace(endpoint))
            {
                TextAsset localConfig = Resources.Load<TextAsset>(LocalConfigResourcePath);
                if (localConfig != null)
                    overrides = ParseRuntimeOverrides(localConfig.text, "Resources/" + LocalConfigResourcePath + ".json");
            }

#if UNITY_EDITOR
            // Backwards-compatible Editor-only lookup. Android StreamingAssets live inside the APK
            // and cannot be read synchronously with System.IO, so device builds use Resources above.
            if (includeEditorOnlySources && string.IsNullOrWhiteSpace(endpoint) && overrides == null)
            {
                string legacyPath = Path.Combine(Application.streamingAssetsPath, LegacyRuntimeConfigFileName);
                if (File.Exists(legacyPath))
                    overrides = ParseRuntimeOverrides(File.ReadAllText(legacyPath), "StreamingAssets/" + LegacyRuntimeConfigFileName);
            }
#endif

            endpoint = !string.IsNullOrWhiteSpace(endpoint)
                ? endpoint.Trim()
                : overrides?.solanaRpcEndpoint?.Trim();
            if (string.IsNullOrWhiteSpace(endpoint))
                return source;

            ValidateHttpsEndpoint(endpoint, "Solana RPC endpoint");

            string declaredCluster = overrides?.ownershipCluster?.Trim();
            if (overrides != null)
            {
                if (string.IsNullOrWhiteSpace(declaredCluster))
                    throw new InvalidOperationException("Local client RPC configuration must declare ownershipCluster.");
                if (!string.Equals(declaredCluster, source.ownershipCluster?.Trim(), StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Local client RPC cluster does not match the selected deployment profile.");
            }

            string websocket = overrides?.solanaWsEndpoint?.Trim();
            if (!string.IsNullOrWhiteSpace(websocket))
                ValidateWssEndpoint(websocket);

            SolanaConfigSO resolved = Instantiate(source);
            resolved.name = source.name + " (Local RPC Override)";
            resolved.hideFlags = HideFlags.DontSave;
            resolved.rpcEndpoint = endpoint;
            if (!string.IsNullOrWhiteSpace(websocket))
                resolved.wsEndpoint = websocket;
            return resolved;
        }

        private static RuntimeConfigOverrides ParseRuntimeOverrides(string json, string sourceLabel)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new InvalidOperationException(sourceLabel + " is empty.");

            RuntimeConfigOverrides overrides;
            try
            {
                overrides = JsonUtility.FromJson<RuntimeConfigOverrides>(json);
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(sourceLabel + " contains invalid JSON.", exception);
            }

            if (overrides == null || string.IsNullOrWhiteSpace(overrides.solanaRpcEndpoint))
                throw new InvalidOperationException(sourceLabel + " must define solanaRpcEndpoint.");
            return overrides;
        }

        private static void ValidateHttpsEndpoint(string value, string label)
        {
            if (!Uri.TryCreate(value, UriKind.Absolute, out Uri endpoint) ||
                endpoint.Scheme != Uri.UriSchemeHttps ||
                string.IsNullOrWhiteSpace(endpoint.Host))
            {
                throw new InvalidOperationException(label + " must be an absolute HTTPS URL.");
            }
        }

        private static void ValidateWssEndpoint(string value)
        {
            if (!Uri.TryCreate(value, UriKind.Absolute, out Uri endpoint) ||
                !string.Equals(endpoint.Scheme, "wss", StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(endpoint.Host))
            {
                throw new InvalidOperationException("Solana WebSocket endpoint must be an absolute WSS URL.");
            }
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
