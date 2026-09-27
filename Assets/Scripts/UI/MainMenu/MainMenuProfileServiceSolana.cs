using System;
using System.Threading.Tasks;
using UnityEngine;
using Chainbound.Combat;

/// <summary>
/// [DISABLED FOR MVP — use MainMenuProfileServiceSupabase instead]
///
/// On-chain Solana profile adapter. Reads the PlayerProfile PDA for hasProfile/hasStartedGame
/// and falls back to PlayerPrefs for introQuestComplete. Superseded by the Supabase Edge Function
/// path which is the single source of truth for intro_complete. Keep this component in the project
/// for future on-chain validation work, but do NOT assign it to the Main Menu's Profile Service
/// field — only MainMenuProfileServiceSupabase should be assigned there.
/// </summary>
[System.Obsolete("MVP: Use MainMenuProfileServiceSupabase. This component is disabled and should not be assigned to the Main Menu scene.", false)]
public class MainMenuProfileServiceSolana : MainMenuProfileServiceBase
{
    [Header("Profile Mapping")]
    [Tooltip("PlayerPrefs key for intro-quest complete (1 = complete). Same as MainMenuProfileServiceStub.")]
    [SerializeField] private string introCompletePlayerPrefsKey = "SignalJam_IntroQuestComplete";

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    public override void FetchProfile(string walletAddress, Action<MainMenuProfileSnapshot> onComplete)
    {
        if (string.IsNullOrEmpty(walletAddress))
        {
            onComplete?.Invoke(DefaultSnapshot());
            return;
        }

        var bridge = SolanaServiceBridge.Instance;
        if (bridge?.ProfileService == null)
        {
            if (enableDebugLogs)
            {
                Debug.LogWarning("[MainMenuProfileServiceSolana] ProfileService not available.");
            }
            onComplete?.Invoke(DefaultSnapshot());
            return;
        }

        RunFetchProfileAsync(bridge.ProfileService, walletAddress, onComplete);
    }

    private async void RunFetchProfileAsync(IProfileService profileService, string walletAddress, Action<MainMenuProfileSnapshot> onComplete)
    {
        MainMenuProfileSnapshot snapshot = DefaultSnapshot();

        try
        {
            bool hasProfile = await profileService.HasProfileAsync(walletAddress);
            if (hasProfile)
            {
                await profileService.LoadProfileAsync(walletAddress);
            }

            bool introComplete = PlayerPrefs.GetInt(introCompletePlayerPrefsKey, 0) == 1;

            snapshot = new MainMenuProfileSnapshot
            {
                hasProfile = hasProfile,
                hasStartedGame = hasProfile,
                introQuestComplete = introComplete,
                displayName = "",
            };

            if (enableDebugLogs)
            {
                Debug.Log(
                    "[MainMenuProfileServiceSolana] Profile fetched. " +
                    $"wallet={walletAddress}, hasProfile={hasProfile}, introComplete={introComplete}");
            }
        }
        catch (Exception ex)
        {
            if (enableDebugLogs)
            {
                Debug.LogWarning($"[MainMenuProfileServiceSolana] Fetch error: {ex.Message}");
            }
        }

        onComplete?.Invoke(snapshot);
    }

    private static MainMenuProfileSnapshot DefaultSnapshot()
    {
        return new MainMenuProfileSnapshot
        {
            hasProfile = false,
            hasStartedGame = false,
            introQuestComplete = false,
            displayName = "",
        };
    }
}
