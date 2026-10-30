using System;
using Chainbound.Combat;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Data-driven event/news destination opened from the Xmas Tree. Below the feed
/// it shows the daily gift block, driven only by <see cref="DailyLoginRewardService"/>:
/// hidden until the server confirms a status, a locked line, a claim button, or
/// a UTC countdown after today's claim.
/// </summary>
[DisallowMultipleComponent]
public sealed class EventsPanelController : MainMenuPanelControllerBase
{
    private const string LockedCopy = "The tree gift opens after Toby's delivery.";
    private const string StackFullCopy = "Your Signal Orb stack is full. Use one, then claim today's gift.";
    private const string ClaimFailedCopy = "The gift could not be claimed. Try again.";

    [SerializeField] private MainMenuEventsFeedSO feed;
    [SerializeField] private TMP_Text titleLabel;
    [SerializeField] private TMP_Text bodyLabel;

    [Header("Daily gift")]
    [SerializeField] private GameObject giftBlockRoot;
    [SerializeField] private GameObject giftGraphic;
    [SerializeField] private TMP_Text giftStatusLabel;
    [SerializeField] private Button claimButton;
    [SerializeField] private TMP_Text claimButtonLabel;

    private DailyLoginRewardService gift;
    private string grantLine = "";
    private string rejectionLine = "";
    private int shownCountdownSecond = -1;
    private bool claimButtonWasActive;

    protected override void Awake()
    {
        base.Awake();
        if (claimButton != null)
            claimButton.onClick.AddListener(HandleClaimClicked);
    }

    protected override void OnDestroy()
    {
        if (claimButton != null)
            claimButton.onClick.RemoveListener(HandleClaimClicked);
        UnbindGift();
        base.OnDestroy();
    }

    protected override void OnPanelOpening()
    {
        base.OnPanelOpening();
        if (feed != null)
        {
            if (titleLabel != null)
                titleLabel.text = feed.title;
            if (bodyLabel != null)
                bodyLabel.text = feed.body;
        }

        grantLine = "";
        rejectionLine = "";
        BindGift();
        RenderGift();
        _ = gift.RefreshAsync();
    }

    protected override void OnPanelClosing()
    {
        base.OnPanelClosing();
        UnbindGift();
    }

    private void Update()
    {
        if (!IsOpen || gift == null || !IsClaimedToday())
            return;

        int remainingSecond = (int)Math.Ceiling(RemainingUntilNextGift().TotalSeconds);
        if (remainingSecond != shownCountdownSecond)
            RenderGift();
    }

    private void BindGift()
    {
        if (gift != null)
            return;

        gift = DailyLoginRewardService.EnsureInstance();
        gift.OnStatusChanged += HandleGiftStatusChanged;
        gift.OnClaimSucceeded += HandleClaimSucceeded;
        gift.OnClaimRejected += HandleClaimRejected;
    }

    private void UnbindGift()
    {
        if (gift == null)
            return;

        gift.OnStatusChanged -= HandleGiftStatusChanged;
        gift.OnClaimSucceeded -= HandleClaimSucceeded;
        gift.OnClaimRejected -= HandleClaimRejected;
        gift = null;
    }

    private async void HandleClaimClicked()
    {
        if (gift == null || gift.IsClaimInFlight || !gift.CanClaim)
            return;

        rejectionLine = "";
        if (claimButton != null)
            claimButton.interactable = false;
        try
        {
            await gift.ClaimAsync();
        }
        finally
        {
            if (claimButton != null)
                claimButton.interactable = true;
            RenderGift();
        }
    }

    private void HandleGiftStatusChanged()
    {
        RenderGift();
    }

    private void HandleClaimSucceeded(DailyLoginClaimResult result)
    {
        // The grant plays once, inline, so no overlay competes with the menu's
        // own input controller. The countdown follows on the next render.
        grantLine = $"Received {result.ramAmount} RAM + {ItemDisplayName(result.itemId)} x{result.itemQuantity}";
        rejectionLine = "";
        RenderGift();
    }

    private void HandleClaimRejected(string error)
    {
        rejectionLine = string.Equals(error, DailyLoginRewardService.StackFullError, StringComparison.Ordinal)
            ? StackFullCopy
            : string.Equals(error, DailyLoginRewardService.NotUnlockedError, StringComparison.Ordinal)
                ? ""
                : ClaimFailedCopy;
        RenderGift();
    }

    private void RenderGift()
    {
        bool loaded = gift != null && gift.StatusLoaded && gift.Enabled;
        bool locked = loaded && !gift.Unlocked;
        bool claimable = loaded && gift.Unlocked && gift.CanClaim;
        bool claimed = loaded && IsClaimedToday();

        if (giftBlockRoot != null)
            giftBlockRoot.SetActive(locked || claimable || claimed);
        if (giftGraphic != null)
            giftGraphic.SetActive(claimable);

        if (claimButton != null)
        {
            claimButton.gameObject.SetActive(claimable);
            if (claimButtonLabel != null && claimable)
                claimButtonLabel.text = $"Claim {gift.RamAmount} RAM + {gift.ItemQuantity} {ItemDisplayName(gift.ItemId)}";
        }

        if (giftStatusLabel != null)
        {
            string status = "";
            if (locked)
            {
                status = LockedCopy;
            }
            else if (claimable)
            {
                status = rejectionLine;
            }
            else if (claimed)
            {
                TimeSpan remaining = RemainingUntilNextGift();
                shownCountdownSecond = (int)Math.Ceiling(remaining.TotalSeconds);
                string countdown = $"Next gift in {(int)remaining.TotalHours:00}:{remaining.Minutes:00}:{remaining.Seconds:00} (00:00 UTC)";
                status = string.IsNullOrEmpty(grantLine) ? countdown : $"{grantLine}\n{countdown}";
            }

            giftStatusLabel.text = status;
            giftStatusLabel.gameObject.SetActive(status.Length > 0);
        }

        SetDefaultSelection(claimable ? claimButton : null);
        if (claimable != claimButtonWasActive)
        {
            claimButtonWasActive = claimable;
            NotifySelectablesChanged();
        }
    }

    private bool IsClaimedToday()
    {
        return gift != null && gift.StatusLoaded && gift.Unlocked && !gift.CanClaim && gift.NextClaimAtUtc.HasValue;
    }

    private TimeSpan RemainingUntilNextGift()
    {
        if (gift == null || !gift.NextClaimAtUtc.HasValue)
            return TimeSpan.Zero;

        TimeSpan remaining = gift.NextClaimAtUtc.Value - gift.EstimatedServerNowUtc;
        return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }

    private static string ItemDisplayName(string itemId)
    {
        return PlayerItemCatalogSeed.TryGet(itemId, out PlayerItemDefinition definition) &&
               !string.IsNullOrWhiteSpace(definition.displayName)
            ? definition.displayName
            : itemId;
    }
}
