using System.Collections;
using UnityEngine;

/// <summary>Composable narrative checkpoint for an optional wallet link.</summary>
[CreateAssetMenu(fileName = "Step_PromptWalletConnection", menuName = "Chainbound/Narrative/Steps/Prompt Wallet Connection")]
public class Step_PromptWalletConnection : SequenceStep
{
    [SerializeField] private WalletPromptDefinitionSO promptDefinition;

    public override IEnumerator Execute(SequenceContext context, System.Action<StepOutcome> onComplete)
    {
        if (promptDefinition == null)
        {
            onComplete?.Invoke(StepOutcome.Failed("Wallet prompt definition is missing."));
            yield break;
        }

        WalletConnectionPromptService prompt = WalletConnectionPromptService.Instance;
        if (prompt == null)
        {
            GameObject host = new GameObject("[WalletConnectionPromptService]");
            prompt = host.AddComponent<WalletConnectionPromptService>();
        }

        MainMenuFlowOrchestrator flow = FindObjectOfType<MainMenuFlowOrchestrator>();
        if (flow == null)
        {
            GameObject host = new GameObject("[AccountPromptFlow]");
            flow = host.AddComponent<MainMenuFlowOrchestrator>();
        }
        prompt.Configure(flow);

        var task = prompt.ShowAsync(promptDefinition);
        while (!task.IsCompleted)
            yield return null;

        WalletPromptResult result = task.Result;
        if (result.Kind == WalletPromptResultKind.Failed)
        {
            onComplete?.Invoke(StepOutcome.Failed(result.Message));
            yield break;
        }

        // Linked, skipped, restored-profile (explicit recovery route), and
        // linked suppression are all non-blocking narrative outcomes.
        onComplete?.Invoke(StepOutcome.Success(result.Message));
    }
}
