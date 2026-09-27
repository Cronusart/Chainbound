using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

/// <summary>
/// Manages the "CONNECTING..." pending state on a wallet-connect button:
/// label text swap, pixel spinner animation, success tick, and failure flash.
/// All tweens use SetUpdate(true) to run at timeScale 0.
/// </summary>
public class UiPendingIndicator : MonoBehaviour
{
    [Header("Label")]
    [SerializeField] private TMP_Text label;
    [SerializeField] private string idleText = "CONNECT WALLET";
    [SerializeField] private string pendingText = "CONNECTING\u2026";

    [Header("Spinner")]
    [SerializeField] private Image spinnerImage;
    [SerializeField] private UIImageFrameAnimator spinnerAnimator;

    [Header("Success (Optional)")]
    [SerializeField] private Image tickImage;
    [SerializeField] private float tickFlashDuration = 0.6f;

    [Header("Failure (Optional)")]
    [SerializeField] private Image errorImage;
    [SerializeField] private float errorFlashDuration = 0.35f;

    private Color _labelBaseColor;

    private void Awake()
    {
        if (label != null)
            _labelBaseColor = label.color;

        SetImageAlpha(tickImage, 0f);
        SetImageAlpha(errorImage, 0f);

        if (spinnerImage != null)
            spinnerImage.enabled = false;
    }

    /// <summary>
    /// Enter or exit the pending state.
    /// </summary>
    public void SetPending(bool pending)
    {
        if (label != null)
            label.text = pending ? pendingText : idleText;

        if (spinnerImage != null)
            spinnerImage.enabled = pending;

        if (spinnerAnimator != null)
        {
            if (pending) spinnerAnimator.Play();
            else spinnerAnimator.Stop();
        }
    }

    /// <summary>
    /// Brief success confirmation. Uses tickImage if assigned, otherwise
    /// flashes the label color green.
    /// </summary>
    public void ShowSuccess()
    {
        if (tickImage != null)
        {
            FlashImage(tickImage, tickFlashDuration);
            return;
        }

        if (label != null)
            FlashLabelColor(new Color(0.2f, 0.9f, 0.3f, 1f), tickFlashDuration);
    }

    /// <summary>
    /// Brief failure indication. Uses errorImage if assigned, otherwise
    /// flashes the label color red.
    /// </summary>
    public void ShowFailure()
    {
        if (errorImage != null)
        {
            FlashImage(errorImage, errorFlashDuration);
            return;
        }

        if (label != null)
            FlashLabelColor(new Color(1f, 0.25f, 0.2f, 1f), errorFlashDuration);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private void FlashImage(Image img, float duration)
    {
        img.DOKill();
        float half = duration * 0.5f;
        DOTween.Sequence()
            .Append(img.DOFade(1f, half * 0.3f).SetEase(Ease.OutSine))
            .AppendInterval(half * 0.4f)
            .Append(img.DOFade(0f, half * 0.3f).SetEase(Ease.InSine))
            .SetUpdate(true);
    }

    private void FlashLabelColor(Color flashColor, float duration)
    {
        label.DOKill();
        float half = duration * 0.5f;
        DOTween.Sequence()
            .Append(label.DOColor(flashColor, half).SetEase(Ease.OutSine))
            .Append(label.DOColor(_labelBaseColor, half).SetEase(Ease.InSine))
            .SetUpdate(true);
    }

    private static void SetImageAlpha(Image img, float alpha)
    {
        if (img == null) return;
        var c = img.color;
        c.a = alpha;
        img.color = c;
    }

    private void OnDestroy()
    {
        if (tickImage != null) tickImage.DOKill();
        if (errorImage != null) errorImage.DOKill();
        if (label != null) label.DOKill();
    }
}
