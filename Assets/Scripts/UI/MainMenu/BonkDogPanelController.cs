using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Primary New Game / Continue destination opened from Bonk Dog.</summary>
[DisallowMultipleComponent]
public sealed class BonkDogPanelController : MainMenuPanelControllerBase
{
    [Header("Views")]
    [SerializeField] private GameObject newGameView;
    [SerializeField] private GameObject continueView;
    [SerializeField] private MainMenuProfileSummaryView profileSummaryView;
    [SerializeField] private TMP_Text statusLabel;

    [Header("Actions")]
    [SerializeField] private Button newGameButton;
    [SerializeField] private Button connectWalletButton;
    [SerializeField] private Button continueButton;

    [Header("Flow")]
    [SerializeField] private MainMenuSequenceController2 sequenceController;

    private const string BootstrapOfflineMessage = "PROFILE SERVICE OFFLINE";

    private bool hasStartedGame;
    private string statusMessage = "";

    /// <summary>
    /// Returns the first/top action in the currently visible authored view.
    /// Keeping this separate from the base panel's single serialized default
    /// prevents the hidden New Game and Continue views from competing for HUD
    /// focus when the panel is opened.
    /// </summary>
    public override Selectable ActiveViewDefaultSelection
    {
        get
        {
            if (continueView != null && continueView.activeInHierarchy && continueButton != null)
                return continueButton;
            if (newGameView != null && newGameView.activeInHierarchy && newGameButton != null)
                return newGameButton;
            return DefaultSelection;
        }
    }

    protected override void Awake()
    {
        base.Awake();
        if (newGameButton != null) newGameButton.onClick.AddListener(HandleNewGame);
        if (connectWalletButton != null) connectWalletButton.onClick.AddListener(HandleConnectWallet);
        if (continueButton != null) continueButton.onClick.AddListener(HandleContinue);
    }

    protected override void OnDestroy()
    {
        if (newGameButton != null) newGameButton.onClick.RemoveListener(HandleNewGame);
        if (connectWalletButton != null) connectWalletButton.onClick.RemoveListener(HandleConnectWallet);
        if (continueButton != null) continueButton.onClick.RemoveListener(HandleContinue);
        base.OnDestroy();
    }

    public void Configure(
        MainMenuProfileSnapshot snapshot,
        bool startedGame,
        bool bootstrapFailed,
        string errorMessage)
    {
        hasStartedGame = startedGame;
        if (newGameView != null) newGameView.SetActive(!startedGame);
        if (continueView != null) continueView.SetActive(startedGame);

        // Show any supplied message, not just bootstrap failures. Wallet and restore
        // errors arrive after bootstrap has already succeeded, so gating on
        // bootstrapFailed left the panel silent on exactly the failures the player
        // triggered themselves.
        statusMessage = string.IsNullOrWhiteSpace(errorMessage)
            ? (bootstrapFailed ? BootstrapOfflineMessage : string.Empty)
            : errorMessage;
        ApplyStatus();

        if (startedGame)
            profileSummaryView?.Bind(snapshot);

        SetDefaultSelection(startedGame ? continueButton : newGameButton);
        ConfigureExplicitNavigation();
    }

    /// <summary>
    /// Writes a player-facing line to the panel's status label. Use for outcomes the
    /// panel can report without a service round trip (for example, no restorable
    /// profile existing on this device). Pass null or empty to clear.
    /// </summary>
    public void SetStatus(string message)
    {
        statusMessage = message ?? string.Empty;
        ApplyStatus();
    }

    private void ApplyStatus()
    {
        if (statusLabel != null)
            statusLabel.text = statusMessage;
    }

    protected override void OnPanelOpening()
    {
        // Re-resolve this every time the panel opens. Configure() normally
        // stamps it, but this also covers a view changed by another flow or by
        // an authored prefab override between openings.
        Selectable activeViewTop = ActiveViewDefaultSelection;
        if (activeViewTop != null)
            SetDefaultSelection(activeViewTop);
        ConfigureExplicitNavigation();
    }

    public void SetActionsInteractable(bool interactable)
    {
        SetInteractable(newGameButton, interactable && !hasStartedGame);
        SetInteractable(connectWalletButton, interactable && !hasStartedGame);
        SetInteractable(continueButton, interactable && hasStartedGame);
    }

    private void HandleNewGame() => sequenceController?.RequestNewGameFromObjectPanel();
    private void HandleConnectWallet() => sequenceController?.RequestConnectWalletFromObjectPanel();
    private void HandleContinue() => sequenceController?.RequestContinueFromObjectPanel();

    private static void SetInteractable(Button button, bool interactable)
    {
        // Always stamp the flag even while the panel is inactive so the first
        // OpenPanel frame does not leave buttons stuck non-interactable.
        if (button != null)
            button.interactable = interactable;
    }
}
