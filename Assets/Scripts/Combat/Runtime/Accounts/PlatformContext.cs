using UnityEngine;

namespace Chainbound.Combat
{
    public class PlatformContext : MonoBehaviour
    {
        [SerializeField] private PlatformBuildProfileSO buildProfile;
        [SerializeField] private bool enableDebugLogs;

        private static PlatformContext instance;

        public static PlatformContext Instance => instance;

        public BuildPlatform ActiveBuildPlatform => buildProfile != null
            ? buildProfile.BuildPlatform
            : InferBuildPlatform();

        public PlatformCapabilities Capabilities => buildProfile != null
            ? buildProfile.Capabilities
            : PlatformCapabilities.ForBuild(ActiveBuildPlatform);

        public bool WalletForwardStart => buildProfile != null && buildProfile.WalletForwardStart;
        public string BackendEnvironment => buildProfile != null ? buildProfile.BackendEnvironment : "production";

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            if (enableDebugLogs)
                Debug.Log($"[PlatformContext] Active platform: {ActiveBuildPlatform}");
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
