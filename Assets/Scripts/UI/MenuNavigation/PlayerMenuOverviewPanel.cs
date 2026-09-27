using System;
using System.Text;
using System.Threading.Tasks;
using Chainbound.Combat;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SignalJam.UI.MenuNavigation
{
    [DisallowMultipleComponent]
    public class PlayerMenuOverviewPanel : MonoBehaviour
    {
        [Header("Tab Preview")]
        [SerializeField] private TMP_Text tabTitleLabel;
        [SerializeField] private TMP_Text tabDescriptionLabel;

        [Header("Gameplay Info")]
        [SerializeField] private TMP_Text areaLabel;
        [SerializeField] private TMP_Text dateTimeLabel;
        [SerializeField] private TMP_Text segmentLabel;
        [SerializeField] private TMP_Text ramLabel;

        [Header("Services")]
        [Tooltip("Optional MonoBehaviour implementing IPlayerMenuLocationProvider.")]
        [SerializeField] private MonoBehaviour locationProviderBehaviour;
        [Tooltip("Optional MonoBehaviour implementing IPlayerStatsProvider.")]
        [SerializeField] private MonoBehaviour playerStatsProviderBehaviour;
        [SerializeField] private SupabaseConfigSO supabaseConfig;

        [Header("Fallbacks")]
        [SerializeField] private string fallbackAreaName = "Unknown Area";
        [SerializeField] private string ramUnavailableText = "RAM ----";
        [SerializeField] private float timeRefreshInterval = 0.5f;

        private IPlayerMenuLocationProvider locationProvider;
        private IPlayerStatsProvider statsProvider;
        private TimeOfDayManager timeManager;
        private MenuTabDefinitionSO focusedTab;
        private bool focusedTabLocked;
        private int statsRequestVersion;
        private float nextTimeRefresh;

        private void OnEnable()
        {
            RefreshOverview();
        }

        private void Update()
        {
            if (Time.unscaledTime < nextTimeRefresh)
                return;

            RefreshTime();
            nextTimeRefresh = Time.unscaledTime + Mathf.Max(0.1f, timeRefreshInterval);
        }

        public void SetFocusedTab(MenuTabDefinitionSO tab, bool locked)
        {
            focusedTab = tab;
            focusedTabLocked = locked;
            RefreshTabPreview();
        }

        public void RefreshOverview()
        {
            RefreshTabPreview();
            RefreshLocation();
            RefreshTime();
            _ = RefreshStatsAsync(++statsRequestVersion);
        }

        public void Configure(
            SupabaseConfigSO config,
            MonoBehaviour statsProviderSource = null,
            MonoBehaviour locationProviderSource = null)
        {
            if (config != null)
                supabaseConfig = config;
            if (statsProviderSource != null)
                playerStatsProviderBehaviour = statsProviderSource;
            if (locationProviderSource != null)
                locationProviderBehaviour = locationProviderSource;

            locationProvider = null;
            statsProvider = null;
        }

        private void RefreshTabPreview()
        {
            if (tabTitleLabel != null)
                tabTitleLabel.text = focusedTab != null ? focusedTab.DisplayName : "Player Menu";

            if (tabDescriptionLabel == null)
                return;

            if (focusedTab == null)
            {
                tabDescriptionLabel.text = "";
                return;
            }

            tabDescriptionLabel.text = focusedTabLocked
                ? $"{focusedTab.Description}\nLocked: {focusedTab.LockedLabel}"
                : focusedTab.Description;
        }

        private void RefreshLocation()
        {
            if (areaLabel == null)
                return;

            IPlayerMenuLocationProvider provider = ResolveLocationProvider();
            string area = provider != null ? provider.CurrentAreaName : "";
            if (string.IsNullOrWhiteSpace(area))
                area = PrettifySceneName(SceneManager.GetActiveScene().name);
            if (string.IsNullOrWhiteSpace(area))
                area = fallbackAreaName;

            areaLabel.text = area.ToUpperInvariant();
        }

        private void RefreshTime()
        {
            TimeOfDayManager manager = ResolveTimeManager();
            string clock = manager != null ? manager.GetTimeString() : "--:--";
            string segment = manager != null ? manager.CurrentSegment.ToString() : "Unknown";

            if (dateTimeLabel != null)
                dateTimeLabel.text = $"{DateTime.Now:MM/dd/yy}\n{clock}";
            if (segmentLabel != null)
                segmentLabel.text = segment.ToUpperInvariant();
        }

        private async Task RefreshStatsAsync(int requestVersion)
        {
            IPlayerStatsProvider provider = ResolveStatsProvider();
            if (provider == null)
            {
                SetRamUnavailable();
                return;
            }

            try
            {
                GameSessionContext session = GameSessionContext.Instance;
                PlayerStatsSnapshot stats = await provider.FetchStatsAsync(
                    session != null ? session.PlayerProfileId : "",
                    session != null ? session.WalletAddress : "");

                if (requestVersion != statsRequestVersion)
                    return;

                if (stats != null)
                    SetRam(stats.ramBalance);
                else
                    SetRamUnavailable();
            }
            catch (Exception ex)
            {
                if (requestVersion != statsRequestVersion)
                    return;

                Debug.LogWarning($"[PlayerMenuOverviewPanel] Failed to refresh RAM: {ex.Message}", this);
                SetRamUnavailable();
            }
        }

        private void SetRam(long value)
        {
            if (ramLabel != null)
                ramLabel.text = $"RAM {Math.Max(0, value):N0}";
        }

        private void SetRamUnavailable()
        {
            if (ramLabel != null)
                ramLabel.text = ramUnavailableText;
        }

        private IPlayerMenuLocationProvider ResolveLocationProvider()
        {
            if (locationProvider != null)
                return locationProvider;

            if (locationProviderBehaviour is IPlayerMenuLocationProvider assigned)
                return locationProvider = assigned;

            MonoBehaviour[] behaviours = FindObjectsOfType<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IPlayerMenuLocationProvider provider)
                    return locationProvider = provider;
            }

            return null;
        }

        private TimeOfDayManager ResolveTimeManager()
        {
            if (timeManager != null)
                return timeManager;

            timeManager = FindObjectOfType<TimeOfDayManager>();
            return timeManager;
        }

        private IPlayerStatsProvider ResolveStatsProvider()
        {
            if (statsProvider != null)
                return statsProvider;

            if (playerStatsProviderBehaviour is IPlayerStatsProvider assigned)
                return statsProvider = assigned;

            MonoBehaviour[] localBehaviours = GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < localBehaviours.Length; i++)
            {
                if (localBehaviours[i] is IPlayerStatsProvider localProvider)
                    return statsProvider = localProvider;
            }

            MonoBehaviour[] behaviours = FindObjectsOfType<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IPlayerStatsProvider provider)
                    return statsProvider = provider;
            }

            if (supabaseConfig == null)
                return null;

            SupabasePlayerStatsProvider supabaseProvider = gameObject.AddComponent<SupabasePlayerStatsProvider>();
            supabaseProvider.Configure(supabaseConfig);
            playerStatsProviderBehaviour = supabaseProvider;
            return statsProvider = supabaseProvider;
        }

        private static string PrettifySceneName(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
                return "";

            var builder = new StringBuilder(sceneName.Length + 8);
            char previous = '\0';
            for (int i = 0; i < sceneName.Length; i++)
            {
                char current = sceneName[i];
                if (current == '_' || current == '-')
                {
                    AppendSpace(builder);
                    previous = ' ';
                    continue;
                }

                if (i > 0 && char.IsUpper(current) && char.IsLetterOrDigit(previous) && !char.IsWhiteSpace(previous))
                    AppendSpace(builder);

                builder.Append(current);
                previous = current;
            }

            return builder.ToString().Trim();
        }

        private static void AppendSpace(StringBuilder builder)
        {
            if (builder.Length > 0 && builder[builder.Length - 1] != ' ')
                builder.Append(' ');
        }
    }
}
