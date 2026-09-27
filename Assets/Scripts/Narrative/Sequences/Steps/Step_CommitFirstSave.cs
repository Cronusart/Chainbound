using System.Collections;
using UnityEngine;

/// <summary>
/// Step that commits the first save after player name is confirmed.
/// Enforces the rule: no save until name is entered.
/// </summary>
[System.Obsolete("Use Step_CommitPlayerName and Step_CommitStarterChainling for Supabase-backed onboarding commits.", false)]
[CreateAssetMenu(fileName = "Step_CommitFirstSave", menuName = "Chainbound/Narrative/Steps/Commit First Save")]
public class Step_CommitFirstSave : SequenceStep
{
    [Header("Save Settings")]
    [Tooltip("Get player name from context variable")]
    [SerializeField] private bool useContextPlayerName = true;
    
    [Tooltip("Player name variable name (if not using context.PlayerName)")]
    [SerializeField] private string playerNameVariable = "playerName";
    
    [Tooltip("Get starter from context variable")]
    [SerializeField] private bool useContextStarter = false;
    
    [Tooltip("Starter variable name")]
    [SerializeField] private string starterVariable = "starterSelected";
    
    public override IEnumerator Execute(SequenceContext context, System.Action<StepOutcome> onComplete)
    {
        // Get save service
        ISaveService saveService = FindSaveService();
        if (saveService == null)
        {
            Debug.LogError("[Step_CommitFirstSave] Save service not found!");
            onComplete?.Invoke(StepOutcome.Failed("Save service not found"));
            yield break;
        }
        
        // Get player name
        string playerName = "";
        if (useContextPlayerName)
        {
            playerName = context.PlayerName;
        }
        else
        {
            playerName = context.Variables.Get<string>(playerNameVariable) ?? "";
        }
        
        if (string.IsNullOrEmpty(playerName))
        {
            Debug.LogError("[Step_CommitFirstSave] Player name is empty! Cannot commit save.");
            onComplete?.Invoke(StepOutcome.Failed("Player name is empty"));
            yield break;
        }
        
        // Get starter (optional)
        string starterId = "";
        if (useContextStarter)
        {
            starterId = context.StarterSelected;
        }
        else
        {
            starterId = context.Variables.Get<string>(starterVariable) ?? "";
        }
        
        // Create profile data
        ProfileData profileData = new ProfileData
        {
            playerName = playerName,
            starterChainlingId = starterId,
            walletId = "" // Placeholder
        };
        
        // Commit save
        saveService.CommitNewProfile(profileData);
        
        if (enableDebugLogs)
        {
            Debug.Log($"[Step_CommitFirstSave] Committed first save: {playerName}");
        }
        
        onComplete?.Invoke(StepOutcome.Success());
        yield return null;
    }
    
    private ISaveService FindSaveService()
    {
        // Try to find LocalSaveServiceStub
        LocalSaveServiceStub stub = LocalSaveServiceStub.Instance;
        if (stub != null)
        {
            return stub;
        }
        
        // Try to find any ISaveService implementation
        ISaveService service = FindObjectOfType<MonoBehaviour>() as ISaveService;
        return service;
    }
    
    public override bool Validate()
    {
        ISaveService saveService = FindSaveService();
        if (saveService == null)
        {
            Debug.LogWarning("[Step_CommitFirstSave] No save service found! Step will fail.");
            return false;
        }
        return true;
    }
}

