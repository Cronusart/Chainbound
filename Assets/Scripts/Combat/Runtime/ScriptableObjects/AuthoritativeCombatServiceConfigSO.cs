using UnityEngine;

namespace Chainbound.Combat
{
    [CreateAssetMenu(fileName = "AuthoritativeCombatServiceConfig", menuName = "Chainbound/Combat/Authority/Service Config")]
    public sealed class AuthoritativeCombatServiceConfigSO : ScriptableObject
    {
        [Tooltip("HTTPS base URL for the containerized battle service; no trailing slash.")]
        public string serviceBaseUrl = "";
        [Tooltip("Production rollout gate. When false, normal exploration remains available and battle starts stay local/editor-only.")]
        public bool authoritativePveCaptureEnabled;
        [Tooltip("Canonical manifest hash exported by the capture manifest tool.")]
        public string gameplayManifestHash = "";
        [Range(1, 60)] public int requestTimeoutSeconds = 12;
        [Tooltip("Explicit practice fallback. It is only honored in the Editor or test builds.")]
        public bool allowLocalPracticeInEditor = true;

        public bool IsConfigured =>
            authoritativePveCaptureEnabled &&
            !string.IsNullOrWhiteSpace(serviceBaseUrl) &&
            !string.IsNullOrWhiteSpace(gameplayManifestHash);

        private void OnValidate()
        {
            serviceBaseUrl = (serviceBaseUrl ?? "").Trim().TrimEnd('/');
            gameplayManifestHash = (gameplayManifestHash ?? "").Trim().ToLowerInvariant();
            requestTimeoutSeconds = Mathf.Clamp(requestTimeoutSeconds, 1, 60);
        }
    }
}
