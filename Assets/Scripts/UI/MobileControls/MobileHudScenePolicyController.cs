using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SignalJam.UI.MobileControls
{
    /// <summary>
    /// Data-driven scene policy for the mobile HUD.
    /// Attach to a persistent mobile HUD parent and configure scene names in the
    /// inspector. The component acquires/releases MobileHudService context tokens
    /// instead of disabling service objects directly.
    /// </summary>
    [DisallowMultipleComponent]
    public class MobileHudScenePolicyController : MonoBehaviour
    {
        [Serializable]
        public class SceneHudRule
        {
            public bool enabled = true;
            [Tooltip("Unity scene name to match, e.g. main_menu. Leave empty to ignore this rule.")]
            public string sceneName;
            public MobileHudContext context = MobileHudContext.Hidden;
            [Tooltip("Human-readable token reason. Defaults to the scene name and context when blank.")]
            public string reason;
        }

        [Header("References")]
        [SerializeField] private MobileHudService hudService;

        [Header("Scene Rules")]
        [SerializeField] private List<SceneHudRule> sceneRules = new List<SceneHudRule>();
        [SerializeField] private bool applyOnStart = true;
        [Tooltip("When true, only the active scene is evaluated. When false, any loaded scene can match.")]
        [SerializeField] private bool useActiveSceneOnly = true;
        [Tooltip("When true, release this policy's token when no scene rule matches.")]
        [SerializeField] private bool clearTokenWhenNoRuleMatches = true;

        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = false;

        private MobileHudToken activeToken;
        private bool hasActiveToken;
        private string activeRuleKey;

        private void Awake()
        {
            ResolveHudService();
        }

        private void OnEnable()
        {
            SceneManager.activeSceneChanged += HandleActiveSceneChanged;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void Start()
        {
            if (applyOnStart)
                RefreshPolicy();
        }

        private void OnDisable()
        {
            SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            ReleaseActiveToken();
        }

        /// <summary>
        /// Re-evaluate the currently loaded scenes against the configured policy.
        /// </summary>
        public void RefreshPolicy()
        {
            ResolveHudService();

            SceneHudRule rule = FindMatchingRule();
            if (rule == null)
            {
                if (clearTokenWhenNoRuleMatches)
                    ReleaseActiveToken();
                return;
            }

            string ruleKey = GetRuleKey(rule);
            if (hasActiveToken && activeRuleKey == ruleKey)
                return;

            ReleaseActiveToken();

            MobileHudService service = hudService != null ? hudService : MobileHudService.Instance;
            if (service == null)
                return;

            string reason = string.IsNullOrWhiteSpace(rule.reason)
                ? $"ScenePolicy:{rule.sceneName}:{rule.context}"
                : rule.reason;

            activeToken = service.AcquireContext(rule.context, reason);
            hasActiveToken = activeToken.IsValid;
            activeRuleKey = ruleKey;

            if (enableDebugLogs)
                Debug.Log($"[MobileHudScenePolicyController] Applied {rule.context} for scene '{rule.sceneName}'.", this);
        }

        private void HandleActiveSceneChanged(Scene previousScene, Scene nextScene)
        {
            _ = previousScene;
            _ = nextScene;
            RefreshPolicy();
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _ = scene;
            _ = mode;

            if (!useActiveSceneOnly)
                RefreshPolicy();
        }

        private SceneHudRule FindMatchingRule()
        {
            for (int i = 0; i < sceneRules.Count; i++)
            {
                SceneHudRule rule = sceneRules[i];
                if (RuleMatches(rule))
                    return rule;
            }

            return null;
        }

        private bool RuleMatches(SceneHudRule rule)
        {
            if (rule == null || !rule.enabled || string.IsNullOrWhiteSpace(rule.sceneName))
                return false;

            if (useActiveSceneOnly)
                return SceneNameMatches(SceneManager.GetActiveScene(), rule.sceneName);

            int count = SceneManager.sceneCount;
            for (int i = 0; i < count; i++)
            {
                if (SceneNameMatches(SceneManager.GetSceneAt(i), rule.sceneName))
                    return true;
            }

            return false;
        }

        private static bool SceneNameMatches(Scene scene, string sceneName)
        {
            return scene.IsValid()
                && string.Equals(scene.name, sceneName, StringComparison.OrdinalIgnoreCase);
        }

        private static string GetRuleKey(SceneHudRule rule)
        {
            return $"{rule.sceneName}|{rule.context}|{rule.reason}";
        }

        private void ResolveHudService()
        {
            if (hudService == null)
                hudService = GetComponent<MobileHudService>();

            if (hudService == null)
                hudService = GetComponentInParent<MobileHudService>();

            if (hudService == null)
                hudService = FindObjectOfType<MobileHudService>();
        }

        private void ReleaseActiveToken()
        {
            if (!hasActiveToken)
                return;

            activeToken.Release();
            hasActiveToken = false;
            activeRuleKey = null;
        }
    }
}
