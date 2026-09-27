using System.Collections;
using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Step that starts an encounter.
/// Waits for encounter to complete and stores outcome.
/// </summary>
[CreateAssetMenu(fileName = "Step_StartEncounter", menuName = "Chainbound/Narrative/Steps/Start Encounter")]
public class Step_StartEncounter : SequenceStep
{
    [Header("Encounter")]
    [Tooltip("Encounter ID")]
    [SerializeField] private string encounterId = "";
    
    [Tooltip("Can player run?")]
    [SerializeField] private bool canRun = true;
    
    [Tooltip("Can player capture?")]
    [SerializeField] private bool canCapture = false;
    
    [Tooltip("Forced outcome (empty = use config defaults)")]
    [SerializeField] private string forcedOutcome = "";
    
    [Tooltip("Store outcome in context variable")]
    [SerializeField] private string outcomeVariableName = "lastEncounterOutcome";

    [Header("Return position")]
    [Tooltip(
        "When true, do not save overworld return position inside NarrativeEncounterService before battle. " +
        "Use when a prior step (e.g. Wait For Trigger with Save Return Snapshot) already saved the exact spot.")]
    [SerializeField] private bool skipSaveReturnContext;

    [Tooltip("Optional SpawnPoint id for post-battle return when the registry entry does not set one.")]
    [SerializeField] private string returnSpawnPointId = "";
    
    public override IEnumerator Execute(SequenceContext context, System.Action<StepOutcome> onComplete)
    {
        if (string.IsNullOrEmpty(encounterId))
        {
            Debug.LogError("[Step_StartEncounter] Encounter ID is empty!");
            onComplete?.Invoke(StepOutcome.Failed("Encounter ID is empty"));
            yield break;
        }
        
        IEncounterService encounterService = FindEncounterService();
        if (encounterService == null)
        {
            Debug.LogError("[Step_StartEncounter] EncounterService not found!");
            onComplete?.Invoke(StepOutcome.Failed("EncounterService not found"));
            yield break;
        }
        
        // Create config
        EncounterConfig config = new EncounterConfig
        {
            canRun = canRun,
            canCapture = canCapture,
            forcedOutcome = forcedOutcome,
            skipSaveReturnContext = skipSaveReturnContext,
            returnSpawnPointId = returnSpawnPointId,
        };
        
        if (enableDebugLogs)
        {
            Debug.Log($"[Step_StartEncounter] Starting encounter: {encounterId}");
        }
        
        // Start encounter and wait for completion
        yield return encounterService.StartEncounter(encounterId, config);
        
        // Store outcome
        EncounterOutcome outcome = encounterService.LastOutcome;
        context.Variables.Set(outcomeVariableName, outcome.ToString(), VariableScope.Local);
        
        if (enableDebugLogs)
        {
            Debug.Log($"[Step_StartEncounter] Encounter complete: {encounterId} -> {outcome}");
        }
        
        onComplete?.Invoke(StepOutcome.Success());
    }
    
    private IEncounterService FindEncounterService()
    {
        GameServicesContainer servicesContainer = GameServicesContainer.Instance != null
            ? GameServicesContainer.Instance
            : FindObjectOfType<GameServicesContainer>(true);

        if (servicesContainer != null)
        {
            MonoBehaviour[] serviceBehaviours = servicesContainer.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < serviceBehaviours.Length; i++)
            {
                if (serviceBehaviours[i] is IEncounterService service)
                    return service;
            }
        }

        MonoBehaviour[] behaviours = FindObjectsOfType<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (!(behaviours[i] is IEncounterService service))
                continue;

            return service;
        }

        return null;
    }
    
    public override bool Validate()
    {
        if (string.IsNullOrEmpty(encounterId))
        {
            Debug.LogWarning("[Step_StartEncounter] Encounter ID is not set!");
            return false;
        }
        return true;
    }
}

