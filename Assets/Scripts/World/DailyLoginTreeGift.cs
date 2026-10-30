using System;
using System.Collections;
using System.Threading.Tasks;
using Chainbound.Combat;
using UnityEngine;

/// <summary>
/// Player-room daily gift under the Xmas tree. Enables <see cref="giftRoot"/>
/// (gift meshes plus their own interact volume) only while the server says
/// today's gift can be claimed. The tree's own interaction is left as authored.
/// Wire the gift volume's <c>InteractableObject.onInteracted</c> to <see cref="Claim"/>.
/// </summary>
[DisallowMultipleComponent]
public sealed class DailyLoginTreeGift : MonoBehaviour
{
    private const string UnlockFlagKey = "world.coffee.toby_delivery_accepted";
    private const string StackFullCopy = "Your Signal Orb stack is full. Use one, then claim today's gift.";
    private const string ClaimFailedCopy = "The gift could not be claimed. Try again.";

    [SerializeField] private GameObject giftRoot;

    [Tooltip("Second status read after the Toby flag flips, so the quest completion outbox has time to land.")]
    [SerializeField] [Min(0f)] private float unlockRetryDelaySeconds = 4f;

    [SerializeField] private bool enableDebugLogs;

    private DailyLoginRewardService gift;
    private NarrativeProfileStateCoordinator narrative;
    private ProfileWorldFlagStateService flags;
    private bool collectInFlight;

    private void OnEnable()
    {
        gift = DailyLoginRewardService.EnsureInstance();
        gift.OnStatusChanged += ApplyVisibility;

        narrative = NarrativeProfileStateCoordinator.Instance;
        if (narrative != null)
            narrative.OnReadyChanged += HandleNarrativeReadyChanged;

        flags = ProfileWorldFlagStateService.Instance;
        if (flags != null)
            flags.OnFlagChanged += HandleFlagChanged;

        ApplyVisibility();
        _ = gift.RefreshAsync();
    }

    private void OnDisable()
    {
        if (gift != null)
            gift.OnStatusChanged -= ApplyVisibility;
        if (narrative != null)
            narrative.OnReadyChanged -= HandleNarrativeReadyChanged;
        if (flags != null)
            flags.OnFlagChanged -= HandleFlagChanged;
        gift = null;
        narrative = null;
        flags = null;
    }

    /// <summary>UnityEvent target for the gift's <c>InteractableObject</c>.</summary>
    public void Claim()
    {
        if (collectInFlight || gift == null || !gift.CanClaim)
            return;
        StartCoroutine(ClaimRoutine());
    }

    private IEnumerator ClaimRoutine()
    {
        Task task = ClaimInternalAsync();
        while (!task.IsCompleted)
            yield return null;
        if (task.IsFaulted && task.Exception != null)
            Debug.LogException(task.Exception, this);
    }

    private async Task ClaimInternalAsync()
    {
        if (collectInFlight)
            return;

        collectInFlight = true;
        InputLockHandle lockHandle = default;
        try
        {
            InputLockService inputLock = InputLockService.Instance;
            if (inputLock != null)
                lockHandle = inputLock.AcquireLock("DailyLoginTreeGift", InputLockChannel.All, this);

            DailyLoginClaimResult result = await gift.ClaimAsync();
            DailyLoginClaimPresenter presenter = DailyLoginClaimPresenter.Instance;
            if (result.ok)
            {
                // A replay only refreshes state; the service fires no celebration for it.
                if (!result.alreadyClaimed && presenter != null)
                    presenter.ShowGrant(result.ramAmount, result.itemId, result.itemQuantity);
            }
            else if (string.Equals(result.error, DailyLoginRewardService.StackFullError, StringComparison.Ordinal))
            {
                presenter?.ShowNotice(StackFullCopy);
            }
            else if (!string.Equals(result.error, DailyLoginRewardService.NotUnlockedError, StringComparison.Ordinal))
            {
                presenter?.ShowNotice(ClaimFailedCopy);
            }

            if (enableDebugLogs)
                Debug.Log($"[DailyLoginTreeGift] Claim ok={result.ok} already={result.alreadyClaimed} error={result.error}", this);
        }
        finally
        {
            if (lockHandle.IsValid)
                lockHandle.Release();
            collectInFlight = false;
        }
    }

    private void ApplyVisibility()
    {
        if (giftRoot == null || giftRoot == gameObject)
            return;

        bool claimable = gift != null && gift.StatusLoaded && gift.Unlocked && gift.CanClaim;
        if (giftRoot.activeSelf != claimable)
            giftRoot.SetActive(claimable);
    }

    private void HandleNarrativeReadyChanged(bool ready)
    {
        if (ready && gift != null)
            _ = gift.RefreshAsync();
    }

    private void HandleFlagChanged(string key, bool value)
    {
        if (!value || gift == null || !string.Equals(key, UnlockFlagKey, StringComparison.Ordinal))
            return;

        _ = gift.RefreshAsync();
        StartCoroutine(RefreshAfterDelay());
    }

    private IEnumerator RefreshAfterDelay()
    {
        yield return new WaitForSecondsRealtime(unlockRetryDelaySeconds);
        if (gift != null && !gift.CanClaim)
            _ = gift.RefreshAsync();
    }
}
