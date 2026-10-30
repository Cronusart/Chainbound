using Chainbound.Combat;
using UnityEngine;

/// <summary>
/// Shows a serialized gift-box child only while the server says today's daily
/// gift can be claimed. A failed or missing status keeps it hidden; there is no
/// local "claimed" flag.
/// </summary>
[DisallowMultipleComponent]
public sealed class DailyLoginGiftVisual : MonoBehaviour
{
    [SerializeField] private GameObject target;

    private DailyLoginRewardService gift;

    private void OnEnable()
    {
        gift = DailyLoginRewardService.EnsureInstance();
        gift.OnStatusChanged += Apply;
        Apply();
    }

    private void OnDisable()
    {
        if (gift != null)
            gift.OnStatusChanged -= Apply;
        gift = null;
    }

    private void Apply()
    {
        if (target == null || target == gameObject)
            return;

        bool claimable = gift != null && gift.StatusLoaded && gift.Unlocked && gift.CanClaim;
        if (target.activeSelf != claimable)
            target.SetActive(claimable);
    }
}
