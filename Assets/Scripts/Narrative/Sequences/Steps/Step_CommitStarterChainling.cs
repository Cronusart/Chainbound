using System.Collections;
using System.Threading.Tasks;
using Chainbound.Combat;
using UnityEngine;

/// <summary>
/// Persists the chosen starter to Supabase after the player confirms it.
/// </summary>
[CreateAssetMenu(fileName = "Step_CommitStarterChainling", menuName = "Chainbound/Narrative/Steps/Commit Starter Chainling")]
public class Step_CommitStarterChainling : SequenceStep
{
    [Header("Starter Source")]
    [Tooltip("Read the chosen starter species id from SequenceContext.StarterSelected.")]
    [SerializeField] private bool useContextStarter = true;

    [Tooltip("Variable to read when useContextStarter is false.")]
    [SerializeField] private string starterVariable = "starterSelected";

    [Header("Starter display name")]
    [Tooltip("Persistent variable containing the resolved starter name. Empty values fall back to the selected StarterOption display name.")]
    [SerializeField] private string displayNameVariable = "starter_display_name";

    [Header("Supabase")]
    [Tooltip("Supabase config used for the authenticated profile_commit_starter operation.")]
    [SerializeField] private SupabaseConfigSO supabaseConfig;

    public override IEnumerator Execute(SequenceContext context, System.Action<StepOutcome> onComplete)
    {
        string starterSpeciesId = ResolveStarterSpeciesId(context);
        if (string.IsNullOrWhiteSpace(starterSpeciesId))
        {
            onComplete?.Invoke(StepOutcome.Failed("Starter species id was empty."));
            yield break;
        }

        GameSessionContext sessionContext = GameSessionContext.Instance;
        string walletAddress = sessionContext != null ? sessionContext.WalletAddress : string.Empty;
        string playerProfileId = sessionContext != null ? sessionContext.PlayerProfileId : string.Empty;
        bool useOfflineCommit = sessionContext != null && sessionContext.IsOfflineSession;
        if (!useOfflineCommit && string.IsNullOrWhiteSpace(playerProfileId) && string.IsNullOrWhiteSpace(walletAddress))
        {
            onComplete?.Invoke(StepOutcome.Failed("GameSessionContext player profile id and wallet were empty."));
            yield break;
        }

        IStarterCommitService service = ResolveService();
        if (service == null)
        {
            onComplete?.Invoke(StepOutcome.Failed("IStarterCommitService could not be resolved."));
            yield break;
        }

        string displayName = ResolveStarterDisplayName(context, starterSpeciesId);
        Task<StarterCommitResult> commitTask = service.CommitStarterAsync(walletAddress, starterSpeciesId, supabaseConfig, displayName);
        while (!commitTask.IsCompleted)
            yield return null;

        if (commitTask.IsFaulted)
        {
            onComplete?.Invoke(StepOutcome.Failed(commitTask.Exception?.GetBaseException().Message ?? "Unknown commit failure."));
            yield break;
        }

        StarterCommitResult result = commitTask.Result;
        if (!result.Success)
        {
            onComplete?.Invoke(StepOutcome.Failed(result.ErrorMessage));
            yield break;
        }

        string confirmedSpeciesId = string.IsNullOrWhiteSpace(result.SpeciesId) ? starterSpeciesId.Trim() : result.SpeciesId.Trim();
        context.StarterSelected = confirmedSpeciesId;
        GameSessionContext.Instance?.SetStarterResult(confirmedSpeciesId, result.BackendChainlingId);

        if (enableDebugLogs)
        {
            Debug.Log(
                $"[Step_CommitStarterChainling] Starter committed: species={confirmedSpeciesId} chainling={result.BackendChainlingId}");
        }

        onComplete?.Invoke(StepOutcome.Success());
    }

    public override bool Validate()
    {
        bool valid = base.Validate();

        if (supabaseConfig == null)
        {
            Debug.LogWarning("[Step_CommitStarterChainling] SupabaseConfigSO is not assigned.");
            valid = false;
        }

        return valid;
    }

    private string ResolveStarterSpeciesId(SequenceContext context)
    {
        if (context == null)
            return "";

        if (useContextStarter)
            return context.StarterSelected ?? "";

        return context.Variables.Get<string>(starterVariable) ?? "";
    }

    private string ResolveStarterDisplayName(SequenceContext context, string starterSpeciesId)
    {
        string resolved = !string.IsNullOrWhiteSpace(displayNameVariable) && context != null
            ? context.Variables.Get<string>(displayNameVariable)
            : string.Empty;
        if (!string.IsNullOrWhiteSpace(resolved))
            return resolved.Trim();

        StarterOption selected = StarterSelectionService.Instance != null
            ? StarterSelectionService.Instance.SelectedStarter
            : null;
        if (selected != null &&
            string.Equals(selected.ChainlingId, starterSpeciesId, System.StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(selected.DisplayName))
        {
            resolved = selected.DisplayName.Trim();
            PersistResolvedDisplayName(context, resolved);
            return resolved;
        }

        resolved = starterSpeciesId != null ? starterSpeciesId.Trim() : string.Empty;
        PersistResolvedDisplayName(context, resolved);
        return resolved;
    }

    private void PersistResolvedDisplayName(SequenceContext context, string displayName)
    {
        if (context == null || string.IsNullOrWhiteSpace(displayNameVariable))
            return;

        context.Variables.Set(displayNameVariable, displayName, VariableScope.Global);
        context.Variables.MarkPersistent(displayNameVariable);
    }

    private IStarterCommitService ResolveService()
    {
        if (GameSessionContext.Instance != null && GameSessionContext.Instance.IsOfflineSession)
            return LocalSaveServiceStub.Instance;

        SupabaseStarterCommitService service = Object.FindFirstObjectByType<SupabaseStarterCommitService>(FindObjectsInactive.Include);
        if (service == null)
        {
            Transform parent = GameServicesContainer.Instance != null ? GameServicesContainer.Instance.transform : null;
            GameObject go = new GameObject("[SupabaseStarterCommitService]");
            if (parent != null)
                go.transform.SetParent(parent, false);
            else
                Object.DontDestroyOnLoad(go);

            service = go.AddComponent<SupabaseStarterCommitService>();
        }

        service.Configure(supabaseConfig);
        return service;
    }
}
