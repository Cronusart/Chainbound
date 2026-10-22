using System.Collections;
using System;
using Chainbound.Combat;
using SignalJam.UI.MobileControls;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public enum ConfirmAdvanceResult
{
    NoDialogueActive,
    RevealingSkipped,
    AdvanceDeniedWaitingBlink,
    AdvanceDeniedWaitingExternalGate,
    Advanced,
}

/// <summary>
/// Narrative dialogue lifecycle and audio adapter. Rendering and typewriter
/// behavior are delegated to the shared DialogueBoxView.
/// </summary>
public class DialogueUIController : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject dialoguePanel;
    [Tooltip("Shared presentation view. May live on a nested authored panel prefab.")]
    [SerializeField] private DialogueBoxView dialogueView;
    [SerializeField] private TextMeshProUGUI dialogueText;
    [SerializeField] private TextMeshProUGUI speakerNameText;
    [SerializeField] private Image portraitImage;
    [SerializeField] private GameObject portraitContainer;

    [Header("Input")]
    [SerializeField] private string advanceActionName = "Interact";
    [SerializeField] private bool allowAnyInput = true;

    [Header("Animation")]
    [SerializeField] private float fadeDuration = 0.2f;
    [SerializeField] private bool useCanvasGroup = true;

    [Header("Continue indicator (inline ellipsis)")]
    [SerializeField] private int minBlinksBeforeAdvance = 2;
    [SerializeField] private float blinkHalfCycle = 0.18f;
    [Range(0f, 1f)] [SerializeField] private float continueGlyphDimAlpha = 0.35f;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs;

    private DialogueBoxView _view;
    private DialogueLine _currentLine;
    private AudioHandle _currentLineSoundHandle;
    private InputAction _advanceAction;
    private MobileInputManager _mobileInput;
    private bool _mobileAdvanceQueued;

    private void Awake()
    {
        _view = dialogueView;
        if (_view == null && dialoguePanel != null)
            _view = dialoguePanel.GetComponent<DialogueBoxView>();
        if (_view == null)
            _view = GetComponent<DialogueBoxView>();
        if (_view == null)
            _view = gameObject.AddComponent<DialogueBoxView>();
        _view.Configure(
            dialoguePanel,
            dialogueText,
            speakerNameText,
            portraitImage,
            portraitContainer,
            prefixSpeakerInBody: false,
            panelFadeDuration: useCanvasGroup ? fadeDuration : 0f,
            requiredBlinkCycles: minBlinksBeforeAdvance,
            blinkHalfCycle: blinkHalfCycle,
            continueDimAlpha: continueGlyphDimAlpha);
        _view.InitializeHidden();
    }

    private void OnEnable()
    {
        _mobileInput = FindFirstObjectByType<MobileInputManager>(FindObjectsInactive.Include);
        if (_mobileInput != null)
            _mobileInput.OnAPressed += HandleMobileAdvancePressed;
    }

    private void OnDisable()
    {
        CancelDialogueForPresentationLoss();

        if (_mobileInput != null)
        {
            _mobileInput.OnAPressed -= HandleMobileAdvancePressed;
            _mobileInput = null;
        }

        _mobileAdvanceQueued = false;
        StopCurrentLineSound();
    }

    /// <summary>
    /// Ends active dialogue when its presentation can no longer receive input.
    /// This is safe to call from scene, app, or UI lifecycle adapters.
    /// </summary>
    public void CancelDialogueForPresentationLoss()
    {
        // DialogueService is persistent. If this scene-owned presentation goes away
        // mid-dialogue, explicitly terminate playback so its Dialogue input-lock
        // token cannot strand world interaction after a resume or scene change.
        DialogueService dialogueService = FindFirstObjectByType<DialogueService>(FindObjectsInactive.Include);
        if (dialogueService != null && dialogueService.IsPlaying)
            dialogueService.StopDialogue();
    }

    public void ShowDialogue()
    {
        _view?.Show();
        DebugLog("[DialogueUIController] Dialogue shown.");
    }

    public void HideDialogue()
    {
        StopCurrentLineSound();
        _view?.Hide();
        DebugLog("[DialogueUIController] Dialogue hidden.");
    }

    public IEnumerator ShowLine(DialogueLine line, DialogueSequenceSO sequence)
    {
        if (line == null || _view == null)
            yield break;

        _currentLine = line;
        StopCurrentLineSound();
        if (line.soundEvent != null && AudioService.Instance != null)
            _currentLineSoundHandle = AudioService.Instance.Play(line.soundEvent);

        string resolvedText = DialogueTextTokens.Resolve(line.text) ?? string.Empty;
        string resolvedSpeaker = DialogueTextTokens.Resolve(line.speakerName) ?? string.Empty;
        yield return _view.PresentLine(
            resolvedSpeaker,
            resolvedText,
            line.typeSpeed,
            line.portraitSprite,
            line.portraitTint);
        StopLoopedSoundIfNeeded();
    }

    public IEnumerator WaitForAdvance(System.Func<bool> externalAdvanceReady = null)
    {
        while (true)
        {
            if (CheckAdvanceInput() && HandleConfirmPressed(externalAdvanceReady) == ConfirmAdvanceResult.Advanced)
                yield break;
            yield return null;
        }
    }

    public ConfirmAdvanceResult OnConfirmPressed()
    {
        return HandleConfirmPressed();
    }

    public void SkipToEndOfLine()
    {
        _view?.SkipReveal();
        StopLoopedSoundIfNeeded();
    }

    public void ShowChoices(DialogueChoice[] choices, Action<string> onPicked)
    {
        if (_view == null || choices == null || choices.Length == 0)
            return;

        string[] labels = new string[choices.Length];
        for (int i = 0; i < choices.Length; i++)
            labels[i] = choices[i] != null ? choices[i].label : string.Empty;

        _view.ShowChoices(labels, index =>
        {
            string value = index >= 0 && index < choices.Length && choices[index] != null
                ? choices[index].value
                : string.Empty;
            onPicked?.Invoke(value ?? string.Empty);
        });
    }

    public void HideChoices()
    {
        _view?.HideChoices();
    }

    public IEnumerator WaitForChoiceMotion()
    {
        if (_view == null)
            yield break;
        yield return _view.WaitForChoiceMotion();
    }

    private ConfirmAdvanceResult HandleConfirmPressed(System.Func<bool> externalAdvanceReady = null)
    {
        if (_view == null)
            return ConfirmAdvanceResult.NoDialogueActive;

        DialogueBoxAdvanceResult result = _view.RequestAdvance(externalAdvanceReady);
        if (result == DialogueBoxAdvanceResult.RevealCompleted)
            StopLoopedSoundIfNeeded();

        switch (result)
        {
            case DialogueBoxAdvanceResult.RevealCompleted:
                return ConfirmAdvanceResult.RevealingSkipped;
            case DialogueBoxAdvanceResult.WaitingForIndicator:
                return ConfirmAdvanceResult.AdvanceDeniedWaitingBlink;
            case DialogueBoxAdvanceResult.WaitingForExternalGate:
                return ConfirmAdvanceResult.AdvanceDeniedWaitingExternalGate;
            case DialogueBoxAdvanceResult.Advanced:
                return ConfirmAdvanceResult.Advanced;
            default:
                return ConfirmAdvanceResult.NoDialogueActive;
        }
    }

    private bool CheckAdvanceInput()
    {
        if (_mobileAdvanceQueued)
        {
            _mobileAdvanceQueued = false;
            return true;
        }

        if (_advanceAction == null && !string.IsNullOrWhiteSpace(advanceActionName))
            _advanceAction = InputSystem.actions?.FindAction(advanceActionName, false);
        if (_advanceAction != null)
        {
            if (!_advanceAction.enabled)
                _advanceAction.Enable();
            if (_advanceAction.WasPressedThisFrame())
                return true;
        }
        if (!allowAnyInput)
            return false;

        return (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) ||
               (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame) ||
               (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) ||
               (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame);
    }

    private void HandleMobileAdvancePressed()
    {
        _mobileAdvanceQueued = true;
    }

    private void StopCurrentLineSound()
    {
        if (!_currentLineSoundHandle.IsValid)
            return;
        _currentLineSoundHandle.Stop(0.1f);
        _currentLineSoundHandle = default;
    }

    private void StopLoopedSoundIfNeeded()
    {
        if (_currentLineSoundHandle.IsValid && _currentLine?.soundEvent != null && _currentLine.soundEvent.Loop)
            StopCurrentLineSound();
    }

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
            Debug.Log(message);
    }
}
