using UnityEngine;

namespace Chainbound.Combat
{
    [CreateAssetMenu(fileName = "PlatformBuildProfile", menuName = "Chainbound/Platform/Accounts/Platform Build Profile")]
    public class PlatformBuildProfileSO : ScriptableObject, IPlatformBuildProfile
    {
        [SerializeField] private BuildPlatform buildPlatform = BuildPlatform.Editor;
        [SerializeField] private PlatformCapabilities capabilities = PlatformCapabilities.EditorDefaults();
        [SerializeField] private string backendEnvironment = "production";
        [SerializeField] private bool walletForwardStart;

        public BuildPlatform BuildPlatform => buildPlatform;
        public PlatformCapabilities Capabilities => capabilities;
        public string BackendEnvironment => backendEnvironment;
        public bool WalletForwardStart => walletForwardStart;
    }
}
