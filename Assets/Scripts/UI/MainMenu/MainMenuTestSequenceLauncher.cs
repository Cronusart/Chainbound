using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Optional sidecar launcher for curated marketing/demo/test sequences from the main menu.
/// Uses the existing pending-sequence startup path without touching the normal Play routing.
/// </summary>
public class MainMenuTestSequenceLauncher : MonoBehaviour
{
    public const string SyntheticWalletAddress = "DEMO";
    public const string DefaultSpawnTag = "DefaultSpawn";

    [Header("Launch Entries")]
    [Tooltip("Optional default launch entry for a dedicated test/demo button.")]
    [SerializeField] private MainMenuTestSequenceLaunchSO defaultLaunchEntry;

    [Tooltip("Optional curated entries. Use LaunchEntryByIndex from button OnClick bindings.")]
    [SerializeField] private List<MainMenuTestSequenceLaunchSO> launchEntries = new List<MainMenuTestSequenceLaunchSO>();

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    private bool _launchInFlight;

    public void LaunchDefaultEntry()
    {
        if (_launchInFlight)
            return;

        MainMenuTestSequenceLaunchSO entry = ResolveDefaultEntry();
        if (entry == null)
        {
            Debug.LogWarning("[MainMenuTestSequenceLauncher] No default test sequence launch entry is configured.");
            return;
        }

        LaunchEntry(entry);
    }

    public void LaunchEntryByIndex(int index)
    {
        if (_launchInFlight)
            return;

        if (index < 0 || index >= launchEntries.Count)
        {
            Debug.LogWarning($"[MainMenuTestSequenceLauncher] Launch index {index} is out of range.");
            return;
        }

        LaunchEntry(launchEntries[index]);
    }

    public void LaunchEntry(MainMenuTestSequenceLaunchSO launchEntry)
    {
        if (_launchInFlight)
            return;

        if (!TryValidateEntry(launchEntry, out string reason))
        {
            Debug.LogWarning($"[MainMenuTestSequenceLauncher] Cannot launch test sequence: {reason}");
            return;
        }

        _launchInFlight = true;

        GameSessionContext context = ApplyLaunchProfile(launchEntry);
        context.SetPendingSequence(launchEntry.Sequence);

        if (enableDebugLogs)
        {
            Debug.Log(
                $"[MainMenuTestSequenceLauncher] Launching '{launchEntry.DisplayName}' " +
                $"scene='{launchEntry.LaunchSceneName}' spawn='{context.DesiredSpawnTag}' sequence='{launchEntry.Sequence.name}'");
        }

        SceneLoader loader = SceneLoader.Instance;
        bool accepted = loader != null && loader.RequestTransition(new SceneTransitionRequest(
            launchEntry.LaunchSceneName,
            styleOverride: launchEntry.TransitionStyle,
            sourceId: "MainMenu.TestSequenceLauncher",
            completed: result =>
            {
                if (!result.Succeeded)
                    _launchInFlight = false;
            }));
        if (!accepted)
            _launchInFlight = false;
    }

    private MainMenuTestSequenceLaunchSO ResolveDefaultEntry()
    {
        if (TryValidateEntry(defaultLaunchEntry, out _))
            return defaultLaunchEntry;

        for (int i = 0; i < launchEntries.Count; i++)
        {
            MainMenuTestSequenceLaunchSO entry = launchEntries[i];
            if (TryValidateEntry(entry, out _))
                return entry;
        }

        return null;
    }

    public static GameSessionContext EnsureGameSessionContext()
    {
        if (GameSessionContext.Instance != null)
            return GameSessionContext.Instance;

        GameObject go = new GameObject("[GameSessionContext]");
        return go.AddComponent<GameSessionContext>();
    }

    public static GameSessionContext ApplyLaunchProfile(MainMenuTestSequenceLaunchSO launchEntry)
    {
        GameSessionContext context = EnsureGameSessionContext();

        context.SetFromProfile(
            walletAddress: SyntheticWalletAddress,
            isNewPlayer: false,
            introComplete: true,
            introCinematicComplete: true,
            desiredSpawnTag: NormalizeSpawnTag(launchEntry != null ? launchEntry.DesiredSpawnTag : null),
            pendingQuestId: string.Empty,
            isOfflineSession: true
        );

        return context;
    }

    public static string NormalizeSpawnTag(string configuredSpawnTag)
    {
        return string.IsNullOrWhiteSpace(configuredSpawnTag) ? DefaultSpawnTag : configuredSpawnTag;
    }

    public static bool TryValidateEntry(MainMenuTestSequenceLaunchSO launchEntry, out string reason)
    {
        if (launchEntry == null)
        {
            reason = "Launch entry is not assigned.";
            return false;
        }

        if (!launchEntry.IsEnabled)
        {
            reason = $"Launch entry '{launchEntry.name}' is disabled.";
            return false;
        }

        if (launchEntry.Sequence == null)
        {
            reason = $"Launch entry '{launchEntry.name}' has no sequence assigned.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(launchEntry.LaunchSceneName))
        {
            reason = $"Launch entry '{launchEntry.name}' has no launch scene assigned.";
            return false;
        }

        reason = null;
        return true;
    }
}
