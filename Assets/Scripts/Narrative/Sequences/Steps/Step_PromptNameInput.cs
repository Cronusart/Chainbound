using System.Collections;
using Chainbound.Combat;
using UnityEngine;

/// <summary>
/// Step that prompts for player name input and stores the confirmed result in context.
/// </summary>
[CreateAssetMenu(fileName = "Step_PromptNameInput", menuName = "Chainbound/Narrative/Steps/Prompt Name Input")]
public class Step_PromptNameInput : SequenceStep
{
    [Header("Name Input")]
    [Tooltip("Store name in context.PlayerName")]
    [SerializeField] private bool useContextPlayerName = true;

    [Tooltip("Name variable name (if not using context.PlayerName)")]
    [SerializeField] private string nameVariable = "playerName";

    [Header("Lifecycle")]
    [Tooltip("Destroy the name-input UI after a successful prompt instead of simply disabling it.")]
    [SerializeField] private bool destroyAfterUse;

    [Header("UI")]
    [Tooltip("Name input UI. Auto-finds if null.")]
    [SerializeField] private NameInputUI nameInputUI;

    public override IEnumerator Execute(SequenceContext context, System.Action<StepOutcome> onComplete)
    {
        nameInputUI = ResolveNameInputUi();

        if (nameInputUI == null)
        {
            Debug.LogError("[Step_PromptNameInput] NameInputUI not found!");
            onComplete?.Invoke(StepOutcome.Failed("NameInputUI not found"));
            yield break;
        }

        if (enableDebugLogs)
        {
            Debug.Log(
                $"[Step_PromptNameInput] Resolved {nameInputUI.name} (instanceId={nameInputUI.GetInstanceID()}). " +
                $"activeSelf={nameInputUI.gameObject.activeSelf}, activeInHierarchy={nameInputUI.gameObject.activeInHierarchy}.",
                nameInputUI);
        }

        if (!nameInputUI.gameObject.activeSelf)
            nameInputUI.gameObject.SetActive(true);

        string playerName = null;

        void HandleConfirmed(string name) => playerName = name;

        nameInputUI.OnNameConfirmed += HandleConfirmed;

        bool showSucceeded = nameInputUI.Show();
        if (enableDebugLogs)
        {
            OnScreenKeyboardService keyboardService = OnScreenKeyboardService.Instance;
            Debug.Log(
                $"[Step_PromptNameInput] NameInputUI.Show() => {showSucceeded}. " +
                $"OnScreenKeyboardService.IsOpen={keyboardService != null && keyboardService.IsOpen}.",
                nameInputUI);
        }

        if (!showSucceeded)
        {
            nameInputUI.OnNameConfirmed -= HandleConfirmed;
            nameInputUI.Hide();
            nameInputUI.gameObject.SetActive(false);
            onComplete?.Invoke(StepOutcome.Failed("NameInputUI failed to show"));
            yield break;
        }

        while (playerName == null)
            yield return null;

        nameInputUI.OnNameConfirmed -= HandleConfirmed;
        nameInputUI.Hide();

        // Zoom the held monitor frame back to rest before post-name sprite shots continue.
        SpriteCinematicPlayer cinematicPlayer = SpriteCinematicPlayer.Instance;
        if (cinematicPlayer != null)
        {
            var zoomOutTask = cinematicPlayer.AnimateNameHandoffZoomOut();
            while (!zoomOutTask.IsCompleted)
                yield return null;
        }

        if (destroyAfterUse && nameInputUI != null)
        {
            GameObject nameInputUiObject = nameInputUI.gameObject;
            nameInputUI = null;
            Destroy(nameInputUiObject);
        }
        else if (nameInputUI != null)
        {
            nameInputUI.gameObject.SetActive(false);
        }

        if (useContextPlayerName)
        {
            context.PlayerName = playerName;
        }
        else
        {
            context.Variables.Set(nameVariable, playerName, VariableScope.Local);
        }

        // Persist locally even if a later Supabase commit fails.
        GameSessionContext.Instance?.SetDisplayName(playerName);

        if (enableDebugLogs)
            Debug.Log($"[Step_PromptNameInput] Player name set: {playerName}");

        onComplete?.Invoke(StepOutcome.Success());
    }

    public override bool Validate()
    {
        return base.Validate();
    }

    private NameInputUI ResolveNameInputUi()
    {
        if (nameInputUI != null)
            return nameInputUI;

        nameInputUI = FindFirstObjectByType<NameInputUI>(FindObjectsInactive.Include);
        return nameInputUI;
    }
}
