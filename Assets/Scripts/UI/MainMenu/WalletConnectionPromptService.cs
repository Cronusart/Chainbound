using System;
using System.Threading.Tasks;
using Chainbound.Combat;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public enum WalletPromptResultKind
{
    Linked,
    Skipped,
    Cancelled,
    Failed,
}

public readonly struct WalletPromptResult
{
    public WalletPromptResult(WalletPromptResultKind kind, string message = "")
    {
        Kind = kind;
        Message = message ?? "";
    }

    public WalletPromptResultKind Kind { get; }
    public string Message { get; }
}

public enum WalletAccessPromptChoice
{
    Connect,
    ContinueOffline,
    Retry,
    Cancel,
    Close,
}

public sealed class WalletAccessPromptModel
{
    public string title = "Wallet Access";
    public string body = "";
    public string status = "";
    public bool showPrimary = true;
    public string primaryLabel = "Connect Wallet";
    public WalletAccessPromptChoice primaryChoice = WalletAccessPromptChoice.Connect;
    public bool showSecondary = true;
    public string secondaryLabel = "Not Now";
    public WalletAccessPromptChoice secondaryChoice = WalletAccessPromptChoice.Cancel;
    public bool showTertiary;
    public string tertiaryLabel = "Retry";
    public WalletAccessPromptChoice tertiaryChoice = WalletAccessPromptChoice.Retry;
}

/// <summary>
/// Wallet-choice modal used from the main menu and narrative sequences.
/// Prefer scene-authored UI (template buttons + panel). Runtime UI is only a fallback.
/// </summary>
public sealed class WalletConnectionPromptService : MonoBehaviour
{
    [Header("Scene UI (preferred)")]
    [SerializeField] private GameObject root;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI bodyText;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private Button connectButton;
    [SerializeField] private Button skipButton;
    [SerializeField] private Button restoreButton;

    private MainMenuFlowOrchestrator flow;
    private TaskCompletionSource<WalletPromptResult> completion;
    private TaskCompletionSource<WalletAccessPromptChoice> accessCompletion;
    private WalletPromptDefinitionSO currentDefinition;
    private bool connecting;
    private bool listenersWired;
    private bool activationRequested;

    public static WalletConnectionPromptService Instance { get; private set; }
    public bool IsConnecting => connecting;
    public bool IsOpen => root != null && root.activeInHierarchy;

    private void Awake()
    {
        // Multiple presenters are intentional: GameServices owns a persistent
        // fallback while scenes may provide a fully-authored version. Keep the
        // first presenter as the process fallback without destroying the scene UI.
        if (Instance == null)
            Instance = this;
        if (root == null)
            root = gameObject;
        WireButtonListeners();
        if (!activationRequested)
            HidePrompt();
    }

    private void OnDestroy()
    {
        completion?.TrySetResult(new WalletPromptResult(
            WalletPromptResultKind.Cancelled,
            "Wallet prompt closed before a choice was made."));
        accessCompletion?.TrySetResult(WalletAccessPromptChoice.Cancel);

        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        if (root == null || !root.activeSelf)
            return;

        bool cancelPressed = (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) ||
                             (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame);
        if (cancelPressed)
            TryCancel();
    }

    public void Configure(MainMenuFlowOrchestrator orchestrator)
    {
        flow = orchestrator;
    }

    public Task<WalletPromptResult> ShowAsync(WalletPromptDefinitionSO definition)
    {
        if (definition == null)
            return Task.FromResult(new WalletPromptResult(WalletPromptResultKind.Failed, "Wallet prompt definition is missing."));

        if (flow == null)
            flow = FindObjectOfType<MainMenuFlowOrchestrator>();

        if (flow == null)
            return Task.FromResult(new WalletPromptResult(WalletPromptResultKind.Failed, "Wallet connection service is unavailable."));

        PlayerAccountSession session = flow.CurrentSession;
        if (definition.suppressWhenLinked && session != null && session.walletIdentity != null && session.walletIdentity.isActive)
            return Task.FromResult(new WalletPromptResult(WalletPromptResultKind.Linked, "Wallet is already linked."));

        if (!EnsureUiReady())
            return Task.FromResult(new WalletPromptResult(WalletPromptResultKind.Failed, "Wallet prompt UI is not authored."));

        currentDefinition = definition;
        completion = new TaskCompletionSource<WalletPromptResult>();
        connecting = false;
        titleText.text = definition.title;
        bodyText.text = definition.body;
        statusText.text = "";
        connectButton.gameObject.SetActive(true);
        skipButton.gameObject.SetActive(true);
        restoreButton.gameObject.SetActive(false);
        SetLabels(definition.connectLabel, definition.skipLabel, definition.conflictRestoreLabel);
        connectButton.interactable = true;
        skipButton.interactable = true;
        restoreButton.interactable = true;
        ConfigureNavigation(connectButton, skipButton, null);
        ShowPrompt(connectButton);
        return completion.Task;
    }

    /// <summary>
    /// Passive, reusable wallet-access prompt. The coordinator owns all wallet and
    /// backend operations; this presenter only captures an explicit player choice.
    /// </summary>
    public Task<WalletAccessPromptChoice> ShowAccessAsync(WalletAccessPromptModel model)
    {
        if (model == null || !EnsureUiReady())
            return Task.FromResult(WalletAccessPromptChoice.Cancel);

        if ((completion != null && !completion.Task.IsCompleted) ||
            (accessCompletion != null && !accessCompletion.Task.IsCompleted))
        {
            return Task.FromResult(WalletAccessPromptChoice.Cancel);
        }

        currentDefinition = null;
        connecting = false;
        accessCompletion = new TaskCompletionSource<WalletAccessPromptChoice>();

        titleText.text = model.title ?? "Wallet Access";
        bodyText.text = model.body ?? "";
        statusText.text = model.status ?? "";

        ConfigureAccessButton(connectButton, model.showPrimary, model.primaryLabel, model.primaryChoice);
        ConfigureAccessButton(skipButton, model.showSecondary, model.secondaryLabel, model.secondaryChoice);
        ConfigureAccessButton(restoreButton, model.showTertiary, model.tertiaryLabel, model.tertiaryChoice);

        Button first = model.showPrimary ? connectButton : model.showSecondary ? skipButton : restoreButton;
        Button second = model.showPrimary && model.showSecondary
            ? skipButton
            : model.showPrimary && model.showTertiary ? restoreButton : null;
        Button third = model.showPrimary && model.showSecondary && model.showTertiary ? restoreButton : null;

        ConfigureNavigation(first, second, third);
        Button defaultButton = model.showPrimary ? connectButton : model.showSecondary ? skipButton : restoreButton;
        ShowPrompt(defaultButton);
        return accessCompletion.Task;
    }

    public bool TryCancel()
    {
        if (accessCompletion != null && !accessCompletion.Task.IsCompleted)
        {
            CompleteAccess(WalletAccessPromptChoice.Cancel);
            return true;
        }

        if (completion == null || completion.Task.IsCompleted)
            return false;
        if (SolanaServiceBridge.Instance?.WalletService is IWalletRequestCancellation cancellable)
            cancellable.CancelActiveWalletRequest();

        Complete(new WalletPromptResult(WalletPromptResultKind.Cancelled, "Wallet prompt cancelled."));
        AppLifecycleService.Instance?.EndWalletSigning();
        return true;
    }

    private async void ConnectPressed()
    {
        if (TryCompleteAccess(connectButton))
            return;

        if (connecting || flow == null)
            return;

        connecting = true;
        AppLifecycleService.Instance?.BeginWalletSigning();
        statusText.text = "Connecting wallet…";
        connectButton.interactable = false;
        skipButton.interactable = true;
        SetLabels("Connecting…", "Cancel", currentDefinition != null ? currentDefinition.conflictRestoreLabel : "Restore Wallet Profile");

        PlayerAccountSession session;
        try
        {
            // This is the complete main-menu path: authorize through MWA, request a
            // server nonce, sign it, and resolve the verified wallet session.
            session = await flow.ConnectWalletToCurrentProfileAsync();
        }
        catch (Exception exception)
        {
            session = PlayerAccountSession.Empty();
            session.fetchError = true;
            session.errorMessage = exception.Message;
        }
        finally
        {
            connecting = false;
            AppLifecycleService.Instance?.EndWalletSigning();
        }

        if (completion == null || completion.Task.IsCompleted)
            return;

        if (HasVerifiedWalletSession(session))
        {
            Complete(new WalletPromptResult(WalletPromptResultKind.Linked));
            return;
        }

        string error = session != null ? session.errorMessage : "Wallet connection failed.";
        if (IsUserCancellation(error))
        {
            Complete(new WalletPromptResult(WalletPromptResultKind.Cancelled, error));
            return;
        }

        statusText.text = !string.IsNullOrWhiteSpace(error)
            ? MainMenuAccountStatusCopy.Resolve(error)
            : "Couldn’t connect that wallet. Try again, or continue without linking.";
        connectButton.interactable = true;
        skipButton.interactable = true;
        connectButton.gameObject.SetActive(true);
        restoreButton.gameObject.SetActive(false);
        SetLabels(
            currentDefinition != null ? currentDefinition.connectLabel : "Connect Wallet",
            currentDefinition != null ? currentDefinition.skipLabel : "Continue Without Wallet",
            currentDefinition != null ? currentDefinition.conflictRestoreLabel : "Restore Wallet Profile");
        ConfigureNavigation(connectButton, skipButton, null);
        Select(connectButton);
    }

    private void SkipPressed()
    {
        if (TryCompleteAccess(skipButton))
            return;

        if (connecting)
        {
            TryCancel();
            return;
        }
        Complete(new WalletPromptResult(WalletPromptResultKind.Skipped));
    }

    private void RestorePressed()
    {
        if (TryCompleteAccess(restoreButton))
            return;

        if (connecting || flow == null)
            return;

        connecting = true;
        AppLifecycleService.Instance?.BeginWalletSigning();
        statusText.text = "Restoring wallet profile…";
        restoreButton.interactable = false;
        skipButton.interactable = true;
        SetLabels(currentDefinition != null ? currentDefinition.connectLabel : "Connect Wallet", "Cancel", "Restoring…");
        flow.RestoreProfile(session =>
        {
            connecting = false;
            AppLifecycleService.Instance?.EndWalletSigning();
            if (session != null && session.hasSession && !session.fetchError)
            {
                Complete(new WalletPromptResult(WalletPromptResultKind.Linked));
                return;
            }

            statusText.text = "That wallet profile could not be restored. Continue without linking to keep this profile unchanged.";
            restoreButton.interactable = true;
            skipButton.interactable = true;
            SetLabels(
                currentDefinition != null ? currentDefinition.connectLabel : "Connect Wallet",
                currentDefinition != null ? currentDefinition.skipLabel : "Continue Without Wallet",
                currentDefinition != null ? currentDefinition.conflictRestoreLabel : "Restore Wallet Profile");
            Select(restoreButton);
        }, error =>
        {
            connecting = false;
            AppLifecycleService.Instance?.EndWalletSigning();
            statusText.text = MainMenuAccountStatusCopy.Resolve(error);
            restoreButton.interactable = true;
            skipButton.interactable = true;
            SetLabels(
                currentDefinition != null ? currentDefinition.connectLabel : "Connect Wallet",
                currentDefinition != null ? currentDefinition.skipLabel : "Continue Without Wallet",
                currentDefinition != null ? currentDefinition.conflictRestoreLabel : "Restore Wallet Profile");
            Select(restoreButton);
        });
    }

    private void Complete(WalletPromptResult result)
    {
        if (completion == null || completion.Task.IsCompleted)
            return;

        HidePrompt();
        completion.TrySetResult(result);
    }

    private void ConfigureAccessButton(
        Button button,
        bool visible,
        string label,
        WalletAccessPromptChoice choice)
    {
        if (button == null)
            return;

        button.gameObject.SetActive(visible);
        button.interactable = visible;
        SetButtonLabel(button, label);

        WalletAccessPromptChoiceBinding binding = button.GetComponent<WalletAccessPromptChoiceBinding>();
        if (binding == null)
            binding = button.gameObject.AddComponent<WalletAccessPromptChoiceBinding>();
        binding.SetChoice(choice);
    }

    private bool TryCompleteAccess(Button button)
    {
        if (accessCompletion == null || accessCompletion.Task.IsCompleted)
            return false;

        WalletAccessPromptChoiceBinding binding = button != null
            ? button.GetComponent<WalletAccessPromptChoiceBinding>()
            : null;
        CompleteAccess(binding != null ? binding.Choice : WalletAccessPromptChoice.Cancel);
        return true;
    }

    private void CompleteAccess(WalletAccessPromptChoice choice)
    {
        if (accessCompletion == null || accessCompletion.Task.IsCompleted)
            return;

        HidePrompt();
        accessCompletion.TrySetResult(choice);
    }

    private void ShowPrompt(Button defaultButton)
    {
        if (root == null)
            return;

        // Activation is the only visual state controlled here. CanvasGroup,
        // transform, sibling order, sprites, and layout remain exactly as authored.
        activationRequested = true;
        try
        {
            root.SetActive(true);
        }
        finally
        {
            activationRequested = false;
        }
        Select(defaultButton);
    }

    private void HidePrompt()
    {
        if (root == null)
            return;

        root.SetActive(false);
        if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null &&
            EventSystem.current.currentSelectedGameObject.transform.IsChildOf(root.transform))
        {
            EventSystem.current.SetSelectedGameObject(null);
        }
    }

    private bool EnsureUiReady()
    {
        if (root != null && titleText != null && bodyText != null && statusText != null &&
            connectButton != null && skipButton != null && restoreButton != null)
        {
            WireButtonListeners();
            return true;
        }

        Debug.LogError(
            "[WalletConnectionPromptService] Scene UI refs are incomplete. " +
            "Author WalletConnectionPrompt under MainMenuCanvas and assign all fields.");
        return false;
    }

    private void WireButtonListeners()
    {
        if (listenersWired)
            return;

        if (connectButton != null)
            connectButton.onClick.AddListener(ConnectPressed);
        if (skipButton != null)
            skipButton.onClick.AddListener(SkipPressed);
        if (restoreButton != null)
            restoreButton.onClick.AddListener(RestorePressed);
        listenersWired = connectButton != null && skipButton != null && restoreButton != null;
    }

    private void SetLabels(string connect, string skip, string restore)
    {
        SetButtonLabel(connectButton, connect);
        SetButtonLabel(skipButton, skip);
        SetButtonLabel(restoreButton, restore);
    }

    private static void SetButtonLabel(Button button, string value)
    {
        if (button == null) return;
        TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
        if (text != null) text.text = value;
    }

    private static void ConfigureNavigation(Button first, Button second, Button third)
    {
        SetNavigation(first, null, second);
        SetNavigation(second, first, third);
        SetNavigation(third, second, null);
    }

    private static void SetNavigation(Button button, Button up, Button down)
    {
        if (button == null) return;
        button.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = up, selectOnDown = down };
    }

    private static void Select(Button button)
    {
        if (button != null && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(button.gameObject);
    }

    private static bool HasVerifiedWalletSession(PlayerAccountSession session)
    {
        return session != null &&
               session.hasSession &&
               !session.fetchError &&
               session.walletIdentity != null &&
               session.walletIdentity.isActive &&
               !string.IsNullOrWhiteSpace(session.PlayerProfileId) &&
               !string.IsNullOrWhiteSpace(session.WalletPublicKey);
    }

    private static bool IsUserCancellation(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return false;

        string normalized = message.ToUpperInvariant();
        return normalized.Contains("CANCEL") ||
               normalized.Contains("TIMED OUT") ||
               normalized.Contains("TIMEOUT") ||
               normalized.Contains("USER_REJECT");
    }

    private static bool IsWalletProfileConflict(string message)
    {
        return !string.IsNullOrWhiteSpace(message) &&
               message.Contains("wallet_linked_to_different_profile", StringComparison.OrdinalIgnoreCase);
    }
}

[DisallowMultipleComponent]
public sealed class WalletAccessPromptChoiceBinding : MonoBehaviour
{
    public WalletAccessPromptChoice Choice { get; private set; }
    public void SetChoice(WalletAccessPromptChoice choice) => Choice = choice;
}
