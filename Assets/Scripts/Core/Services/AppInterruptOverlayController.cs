using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public sealed class AppInterruptOverlayController : MonoBehaviour
{
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Text titleText;
    [SerializeField] private Text bodyText;
    [SerializeField] private Button continueButton;
    [SerializeField] private Button fleeButton;
    [SerializeField] private Button cancelButton;

    public event Action ContinueRequested;
    public event Action FleeRequested;
    public event Action CancelRequested;
    public bool IsVisible => canvasGroup != null && canvasGroup.alpha > 0.001f;

    private void Awake()
    {
        if (continueButton != null) continueButton.onClick.AddListener(() => ContinueRequested?.Invoke());
        if (fleeButton != null) fleeButton.onClick.AddListener(() => FleeRequested?.Invoke());
        if (cancelButton != null) cancelButton.onClick.AddListener(() => CancelRequested?.Invoke());
        Hide();
    }

    public void Show(AppInterruptOverlayMode mode, string title, string body)
    {
        if (canvasGroup == null) canvasGroup = GetComponentInChildren<CanvasGroup>(true);
        if (titleText != null) titleText.text = title ?? "Game Paused";
        if (bodyText != null) bodyText.text = body ?? string.Empty;
        if (continueButton != null) continueButton.gameObject.SetActive(mode == AppInterruptOverlayMode.ContinueOnly || mode == AppInterruptOverlayMode.ContinueAndFlee);
        if (fleeButton != null) fleeButton.gameObject.SetActive(mode == AppInterruptOverlayMode.ContinueAndFlee);
        if (cancelButton != null) cancelButton.gameObject.SetActive(mode == AppInterruptOverlayMode.WalletWait);
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }
        gameObject.SetActive(true);
        Button first = mode == AppInterruptOverlayMode.WalletWait ? cancelButton : continueButton;
        if (first != null && first.gameObject.activeInHierarchy && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(first.gameObject);
    }

    public void Hide()
    {
        if (canvasGroup == null) canvasGroup = GetComponentInChildren<CanvasGroup>(true);
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
        gameObject.SetActive(false);
    }
}
