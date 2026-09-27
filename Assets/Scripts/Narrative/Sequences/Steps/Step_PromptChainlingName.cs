using System.Collections;
using Chainbound.Combat;
using TMPro;
using UnityEngine;

/// <summary>
/// Reusable starter-panel name prompt. The selected display name is stored as
/// a persistent global so the following commit step can use it without a
/// starter-specific dialogue or backend branch.
/// </summary>
[CreateAssetMenu(fileName = "Step_PromptChainlingName", menuName = "Chainbound/Narrative/Steps/Prompt Chainling Name")]
public class Step_PromptChainlingName : SequenceStep
{
    [Header("Variable")]
    [SerializeField] private string displayNameVariable = "starter_display_name";

    [Header("Validation")]
    [SerializeField] private string keyboardTitle = "Chainling's Name";
    [SerializeField] private string keyboardPrompt = "What will you call your Chainling?";

    public override IEnumerator Execute(SequenceContext context, System.Action<StepOutcome> onComplete)
    {
        StarterSelectionService selectionService = StarterSelectionService.Instance;
        StarterOption selected = selectionService != null ? selectionService.SelectedStarter : null;
        string fallbackName = ResolveFallbackName(selected, context);
        string resolvedName = fallbackName;

        StarterSelectionUI selectionUi = selectionService != null ? selectionService.SelectionUI : null;
        OnScreenKeyboardService keyboard = OnScreenKeyboardService.Instance;
        if (selectionUi != null && keyboard != null && selectionUi.ShowStarterNameInput())
        {
            bool finished = false;
            TMP_InputField inputField = selectionUi.StarterNameInputField;
            bool started = inputField != null && keyboard.BeginSession(
                inputField,
                new KeyboardSessionOptions
                {
                    Title = string.IsNullOrWhiteSpace(keyboardTitle) ? "Chainling's Name" : keyboardTitle,
                    Prompt = string.IsNullOrWhiteSpace(keyboardPrompt) ? "What will you call your Chainling?" : keyboardPrompt,
                    SubmitLabel = "Confirm",
                    EmptyDisplayText = "...",
                    ShowLivePreviewStrip = false,
                    MaxLength = ChainlingDisplayNameRules.MaximumLength,
                    StartShifted = true,
                    CharacterPolicy = KeyboardCharacterPolicy.NameEntry,
                    SubmitValidator = ChainlingDisplayNameRules.IsValid,
                    OnSubmitted = value =>
                    {
                        resolvedName = ChainlingDisplayNameRules.ResolveOrFallback(value, fallbackName);
                        finished = true;
                    },
                    OnCancelled = () => finished = true,
                });

            if (started)
            {
                while (!finished)
                    yield return null;
            }
        }

        // Cancel, a missing keyboard, and any invalid/empty result all take
        // the species display name path. This keeps onboarding recoverable.
        resolvedName = ChainlingDisplayNameRules.ResolveOrFallback(resolvedName, fallbackName);

        selectionUi?.HideStarterNameInput();
        context.Variables.Set(displayNameVariable, resolvedName, VariableScope.Global);
        context.Variables.MarkPersistent(displayNameVariable);
        onComplete?.Invoke(StepOutcome.Success());
    }

    public override bool Validate()
    {
        return !string.IsNullOrWhiteSpace(displayNameVariable);
    }

    private string ResolveFallbackName(StarterOption selected, SequenceContext context)
    {
        if (selected != null && !string.IsNullOrWhiteSpace(selected.DisplayName))
            return selected.DisplayName.Trim();

        string speciesId = context != null ? context.StarterSelected : string.Empty;
        return string.IsNullOrWhiteSpace(speciesId) ? "Chainling" : speciesId.Trim();
    }

}
