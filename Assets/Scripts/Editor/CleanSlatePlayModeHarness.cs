using UnityEditor;
using UnityEngine;

/// <summary>
/// Development-only clean-profile launcher. It resets Editor PlayerPrefs after
/// Play Mode has fully stopped, reloads the scripting domain to discard process
/// caches, and then launches Bootstrap with main_menu as the one-shot target.
/// Backend profiles are never mutated or deleted by this tool.
/// </summary>
[InitializeOnLoad]
public static class CleanSlatePlayModeHarness
{
    private const string PlayMenuPath = "Tools/Signal Jam/Testing/Play Bootstrap With Clean Local State";
    private const string WipeMenuPath = "Tools/Signal Jam/Testing/Wipe Local State Only...";
    private const string PendingActionKey = "SignalJam.Testing.CleanSlate.PendingAction";

    private const string WipeAndPlayAfterExit = "wipe_and_play_after_exit";
    private const string WipeOnlyAfterExit = "wipe_only_after_exit";
    private const string PlayAfterReload = "play_after_reload";
    private const string CompleteAfterReload = "complete_after_reload";

    static CleanSlatePlayModeHarness()
    {
        EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
        EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
        EditorApplication.delayCall += ContinuePendingActionAfterReload;
    }

    [MenuItem(PlayMenuPath, priority = 2100)]
    private static void PlayBootstrapWithCleanLocalState()
    {
        if (!ConfirmReset(willRelaunch: true))
            return;

        QueueReset(willRelaunch: true);
    }

    [MenuItem(WipeMenuPath, priority = 2101)]
    private static void WipeLocalStateOnly()
    {
        if (!ConfirmReset(willRelaunch: false))
            return;

        QueueReset(willRelaunch: false);
    }

    [MenuItem(PlayMenuPath, true)]
    [MenuItem(WipeMenuPath, true)]
    private static bool ValidateCommands()
    {
        return !EditorApplication.isCompiling &&
               (EditorApplication.isPlaying || !EditorApplication.isPlayingOrWillChangePlaymode);
    }

    private static bool ConfirmReset(bool willRelaunch)
    {
        string nextStep = willRelaunch
            ? "Unity will then relaunch through Bootstrap into main_menu."
            : "Unity will remain in Edit Mode after the reset.";

        return EditorUtility.DisplayDialog(
            "Clear local Chainbound test state?",
            "This deletes all Unity Editor PlayerPrefs for this project, including narrative snapshots/outbox, " +
            "offline saves, anonymous authentication, guest identity, wallet hints, and local settings.\n\n" +
            "It does not delete or modify any Supabase/backend profile. Reconnecting an existing wallet will " +
            "restore that wallet's server state.\n\n" + nextStep,
            willRelaunch ? "Wipe and Play" : "Wipe Local State",
            "Cancel");
    }

    private static void QueueReset(bool willRelaunch)
    {
        if (EditorApplication.isPlaying)
        {
            SessionState.SetString(
                PendingActionKey,
                willRelaunch ? WipeAndPlayAfterExit : WipeOnlyAfterExit);
            EditorApplication.ExitPlaymode();
            return;
        }

        ResetAndReload(willRelaunch);
    }

    private static void HandlePlayModeStateChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredEditMode)
            return;

        string action = SessionState.GetString(PendingActionKey, string.Empty);
        if (action != WipeAndPlayAfterExit && action != WipeOnlyAfterExit)
            return;

        SessionState.EraseString(PendingActionKey);
        bool willRelaunch = action == WipeAndPlayAfterExit;
        EditorApplication.delayCall += () => ResetAndReload(willRelaunch);
    }

    private static void ResetAndReload(bool willRelaunch)
    {
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();

        SessionState.SetString(
            PendingActionKey,
            willRelaunch ? PlayAfterReload : CompleteAfterReload);

        Debug.Log(
            "[Clean Slate Harness] Cleared local PlayerPrefs. Backend profile data was not changed. " +
            (willRelaunch ? "Reloading scripts before Bootstrap launch." : "Reloading scripts to clear process caches."));
        EditorUtility.RequestScriptReload();
    }

    private static void ContinuePendingActionAfterReload()
    {
        string action = SessionState.GetString(PendingActionKey, string.Empty);
        if (action == CompleteAfterReload)
        {
            SessionState.EraseString(PendingActionKey);
            Debug.Log("[Clean Slate Harness] Local reset complete. Unity remains in Edit Mode.");
            return;
        }

        if (action != PlayAfterReload)
            return;

        SessionState.EraseString(PendingActionKey);
        // Other InitializeOnLoad tools may still be restoring their normal
        // playModeStartScene during this delay cycle. Launch on the next cycle
        // so the one-shot Bootstrap assignment is the final authoritative one.
        EditorApplication.delayCall += LaunchCleanBootstrap;
    }

    private static void LaunchCleanBootstrap()
    {
        if (!BootstrapPlayModeRedirector.PrepareOneShotBootstrapLaunch(
                BootstrapPlayModeRedirector.MainMenuSceneId))
        {
            Debug.LogError("[Clean Slate Harness] Bootstrap scene was unavailable; Play Mode was not started.");
            return;
        }

        Debug.Log(
            "[Clean Slate Harness] Starting Bootstrap with a clean local identity. " +
            "Use a fresh guest session to keep the run isolated from existing backend profiles.");
        EditorApplication.EnterPlaymode();
    }
}
