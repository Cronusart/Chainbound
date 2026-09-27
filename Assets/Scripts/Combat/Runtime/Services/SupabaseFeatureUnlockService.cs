using System.Threading.Tasks;
using UnityEngine;

namespace Chainbound.Combat
{
    public class SupabaseFeatureUnlockService : MonoBehaviour, IFeatureUnlockService
    {
        [SerializeField] private SupabasePlayerStatsProvider statsProvider;
        [SerializeField] private SupabaseConfigSO supabaseConfig;
        [SerializeField] private string handbookPrefsKey = "SignalJam_HandbookUnlocked";
        [SerializeField] private bool inspectorHandbookOverride = false;
        [SerializeField] private bool enableDebugLogs = true;

        private bool cachedHandbookUnlocked;

        public bool IsHandbookUnlocked =>
            inspectorHandbookOverride ||
            cachedHandbookUnlocked ||
            (!string.IsNullOrWhiteSpace(handbookPrefsKey) && PlayerPrefs.GetInt(handbookPrefsKey, 0) == 1);

        private void Awake()
        {
            if (statsProvider == null)
                statsProvider = GetComponent<SupabasePlayerStatsProvider>();
            if (statsProvider != null && supabaseConfig != null)
                statsProvider.Configure(supabaseConfig);
        }

        public async Task<bool> RefreshHandbookUnlockedAsync(string playerProfileId, string walletAddress)
        {
            if (inspectorHandbookOverride)
            {
                cachedHandbookUnlocked = true;
                return true;
            }

            SupabasePlayerStatsProvider provider = ResolveStatsProvider();
            if (provider == null)
                return IsHandbookUnlocked;

            PlayerStatsSnapshot stats = await provider.FetchStatsAsync(playerProfileId, walletAddress);
            if (stats != null)
            {
                cachedHandbookUnlocked = stats.handbookUnlocked;
                if (cachedHandbookUnlocked && !string.IsNullOrWhiteSpace(handbookPrefsKey))
                {
                    PlayerPrefs.SetInt(handbookPrefsKey, 1);
                    PlayerPrefs.Save();
                }
            }

            if (enableDebugLogs)
                Debug.Log($"[SupabaseFeatureUnlockService] handbookUnlocked={IsHandbookUnlocked}", this);

            return IsHandbookUnlocked;
        }

        public void SetHandbookUnlockedLocal(bool unlocked)
        {
            cachedHandbookUnlocked = unlocked;
            if (!string.IsNullOrWhiteSpace(handbookPrefsKey))
            {
                PlayerPrefs.SetInt(handbookPrefsKey, unlocked ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        private SupabasePlayerStatsProvider ResolveStatsProvider()
        {
            if (statsProvider != null)
                return statsProvider;

            statsProvider = GetComponent<SupabasePlayerStatsProvider>();
            if (statsProvider == null)
                statsProvider = FindObjectOfType<SupabasePlayerStatsProvider>(true);

            if (statsProvider != null && supabaseConfig != null)
                statsProvider.Configure(supabaseConfig);

            return statsProvider;
        }
    }
}
