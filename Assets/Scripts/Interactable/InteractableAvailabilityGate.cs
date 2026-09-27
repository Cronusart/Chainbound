using System.Collections;
using UnityEngine;

/// <summary>
/// Shared runtime gate that owns prompt suppression and brief re-trigger blocking for one interactable instance.
/// </summary>
public sealed class InteractableAvailabilityGate
{
    private readonly MonoBehaviour host;
    private readonly InteractableIndicator indicator;
    private readonly bool enableDebugLogs;

    private Coroutine activeRoutine;
    private float blockedUntilUnscaled;
    private InteractableIndicatorSuppressionHandle suppressionHandle;

    public InteractableAvailabilityGate(MonoBehaviour host, InteractableIndicator indicator, bool enableDebugLogs = false)
    {
        this.host = host;
        this.indicator = indicator;
        this.enableDebugLogs = enableDebugLogs;
    }

    public InteractableIndicator Indicator => indicator;

    public bool IsBlocked => activeRoutine != null || Time.unscaledTime < blockedUntilUnscaled;

    public static int CaptureCurrentDialoguePlaybackId()
    {
        DialogueService dialogueService = FindDialogueService();
        return dialogueService != null && dialogueService.CurrentPlayback.IsValid
            ? dialogueService.CurrentPlayback.PlaybackId
            : 0;
    }

    public void BeginForDialogueWindow(InteractableAvailabilitySettings settings, int baselinePlaybackId)
    {
        InteractableAvailabilitySettings resolvedSettings = settings ?? new InteractableAvailabilitySettings();
        StartBlockingRoutine(
            resolvedSettings,
            WaitForDialogueClaimOrGrace(resolvedSettings, baselinePlaybackId));
    }

    public void BeginForNpcSession(
        InteractableAvailabilitySettings settings,
        NPCInteractionSessionService sessionService,
        int sessionId,
        int baselinePlaybackId)
    {
        InteractableAvailabilitySettings resolvedSettings = settings ?? new InteractableAvailabilitySettings();
        IEnumerator busyWindow = sessionService != null && sessionId > 0
            ? WaitForNpcSession(sessionService, sessionId)
            : WaitForDialogueClaimOrGrace(resolvedSettings, baselinePlaybackId);

        StartBlockingRoutine(resolvedSettings, busyWindow);
    }

    public void ResetState()
    {
        StopActiveRoutine();
        blockedUntilUnscaled = 0f;
        ReleaseSuppression();
    }

    private void StartBlockingRoutine(InteractableAvailabilitySettings settings, IEnumerator busyWindow)
    {
        if (host == null)
            return;

        StopActiveRoutine();
        blockedUntilUnscaled = 0f;
        ReleaseSuppression();
        AcquireSuppression();
        activeRoutine = host.StartCoroutine(RunBlockedWindow(settings, busyWindow));
        LogDebug("Started blocked window.");
    }

    private IEnumerator RunBlockedWindow(InteractableAvailabilitySettings settings, IEnumerator busyWindow)
    {
        yield return busyWindow;

        float cooldownSeconds = settings != null ? settings.ReinteractCooldownSeconds : 0f;
        bool keepHiddenUntilAvailable = settings == null || settings.keepIndicatorHiddenUntilAvailable;

        if (!keepHiddenUntilAvailable)
            ReleaseSuppression();

        if (cooldownSeconds > 0f)
        {
            blockedUntilUnscaled = Time.unscaledTime + cooldownSeconds;
            while (Time.unscaledTime < blockedUntilUnscaled)
                yield return null;
        }

        blockedUntilUnscaled = 0f;

        if (keepHiddenUntilAvailable)
            ReleaseSuppression();

        activeRoutine = null;
        LogDebug("Ended blocked window.");
    }

    private IEnumerator WaitForNpcSession(NPCInteractionSessionService sessionService, int sessionId)
    {
        while (sessionService != null && sessionService.IsSessionActive(sessionId))
            yield return null;
    }

    private IEnumerator WaitForDialogueClaimOrGrace(InteractableAvailabilitySettings settings, int baselinePlaybackId)
    {
        float graceEndsAt = Time.unscaledTime + (settings != null ? settings.DownstreamClaimGraceSeconds : 0f);

        while (Time.unscaledTime < graceEndsAt)
        {
            DialogueService dialogueService = FindDialogueService();
            if (HasClaimedDialogue(dialogueService, baselinePlaybackId))
            {
                yield return WaitForDialogueToFinish();
                yield break;
            }

            yield return null;
        }

        DialogueService finalCheckDialogueService = FindDialogueService();
        if (HasClaimedDialogue(finalCheckDialogueService, baselinePlaybackId))
            yield return WaitForDialogueToFinish();
    }

    private IEnumerator WaitForDialogueToFinish()
    {
        while (true)
        {
            DialogueService dialogueService = FindDialogueService();
            if (dialogueService == null || !dialogueService.IsPlaying)
                yield break;

            yield return null;
        }
    }

    private static bool HasClaimedDialogue(DialogueService dialogueService, int baselinePlaybackId)
    {
        return dialogueService != null &&
               dialogueService.CurrentPlayback.IsValid &&
               dialogueService.CurrentPlayback.PlaybackId > baselinePlaybackId;
    }

    private static DialogueService FindDialogueService()
    {
        return Object.FindFirstObjectByType<DialogueService>();
    }

    private void StopActiveRoutine()
    {
        if (host == null || activeRoutine == null)
            return;

        host.StopCoroutine(activeRoutine);
        activeRoutine = null;
    }

    private void AcquireSuppression()
    {
        if (indicator == null)
            return;

        suppressionHandle = indicator.AcquireSuppression();
    }

    private void ReleaseSuppression()
    {
        if (!suppressionHandle.IsValid)
            return;

        suppressionHandle.Release();
        suppressionHandle = default;
    }

    private void LogDebug(string message)
    {
        if (!enableDebugLogs || host == null)
            return;

        Debug.Log($"[InteractableAvailabilityGate] {host.name}: {message}", host);
    }
}
