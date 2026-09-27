using System.Collections;
using UnityEngine;

/// <summary>
/// Step that waits for player to select a starter chainling.
/// Stores selection in context.
/// </summary>
[CreateAssetMenu(fileName = "Step_SelectStarterChainling", menuName = "Chainbound/Narrative/Steps/Select Starter Chainling")]
public class Step_SelectStarterChainling : SequenceStep
{
    [Header("Selection")]
    [Tooltip("Store selection in context variable")]
    [SerializeField] private bool useContextStarter = true;
    
    [Tooltip("Starter variable name (if not using context.StarterSelected)")]
    [SerializeField] private string starterVariable = "starterSelected";

    [Tooltip("Stable persistent variable promoted into the intro quest resolution.")]
    [SerializeField] private string persistentSpeciesVariable = "starter_species_id";
    
    public override IEnumerator Execute(SequenceContext context, System.Action<StepOutcome> onComplete)
    {
        StarterSelectionService service = StarterSelectionService.Instance;
        if (service == null)
        {
            Debug.LogError("[Step_SelectStarterChainling] StarterSelectionService not found!");
            onComplete?.Invoke(StepOutcome.Failed("StarterSelectionService not found"));
            yield break;
        }
        
        // Start selection
        if (!service.StartSelection())
        {
            onComplete?.Invoke(StepOutcome.Failed("Starter selection could not be started."));
            yield break;
        }
        
        if (enableDebugLogs)
        {
            Debug.Log("[Step_SelectStarterChainling] Waiting for starter selection...");
        }
        
        // Wait for selection
        while (!service.HasSelection())
        {
            yield return null;
        }
        
        // Store selection
        string chainlingId = service.SelectedChainlingId;
        
        if (useContextStarter)
        {
            context.StarterSelected = chainlingId;
        }
        else
        {
            context.Variables.Set(starterVariable, chainlingId, VariableScope.Local);
        }

        if (!string.IsNullOrWhiteSpace(persistentSpeciesVariable))
        {
            context.Variables.Set(persistentSpeciesVariable, chainlingId, VariableScope.Global);
            context.Variables.MarkPersistent(persistentSpeciesVariable);
        }
        
        if (enableDebugLogs)
        {
            Debug.Log($"[Step_SelectStarterChainling] Starter selected: {chainlingId}");
        }
        
        onComplete?.Invoke(StepOutcome.Success());
    }
    
    public override bool Validate()
    {
        if (!Application.isPlaying)
            return true;

        StarterSelectionService service = StarterSelectionService.Instance;
        if (service == null)
        {
            Debug.LogWarning("[Step_SelectStarterChainling] No StarterSelectionService found! Step will fail.");
            return false;
        }
        return true;
    }
}

