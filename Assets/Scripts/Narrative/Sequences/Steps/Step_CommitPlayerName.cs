using System.Collections;
using System.Threading.Tasks;
using Chainbound.Combat;
using UnityEngine;

/// <summary>
/// Persists the name chosen in the onboarding flow to Supabase.
/// </summary>
[CreateAssetMenu(fileName = "Step_CommitPlayerName", menuName = "Chainbound/Narrative/Steps/Commit Player Name")]
public class Step_CommitPlayerName : SequenceStep
{
    [Header("Name Source")]
    [Tooltip("Read the player name from SequenceContext.PlayerName.")]
    [SerializeField] private bool useContextPlayerName = true;

    [Tooltip("Variable to read when useContextPlayerName is false.")]
    [SerializeField] private string playerNameVariable = "playerName";

    [Header("Supabase")]
    [Tooltip("Supabase config used for the authenticated profile_set_display_name operation.")]
    [SerializeField] private SupabaseConfigSO supabaseConfig;

    public override IEnumerator Execute(SequenceContext context, System.Action<StepOutcome> onComplete)
    {
        string playerName = ResolvePlayerName(context);
        if (string.IsNullOrWhiteSpace(playerName))
        {
            onComplete?.Invoke(StepOutcome.Failed("Player name was empty."));
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

        IPlayerNameService service = ResolveService();
        if (service == null)
        {
            onComplete?.Invoke(StepOutcome.Failed("IPlayerNameService could not be resolved."));
            yield break;
        }

        while (true)
        {
            Task<PlayerNameCommitResult> commitTask = service.CommitDisplayNameAsync(walletAddress, playerName, supabaseConfig);
            while (!commitTask.IsCompleted)
                yield return null;

            if (commitTask.IsFaulted)
            {
                onComplete?.Invoke(StepOutcome.Failed(commitTask.Exception?.GetBaseException().Message ?? "Unknown commit failure."));
                yield break;
            }

            PlayerNameCommitResult result = commitTask.Result;
            if (result.Success)
            {
                string confirmedName = string.IsNullOrWhiteSpace(result.DisplayName) ? playerName.Trim() : result.DisplayName.Trim();
                context.PlayerName = confirmedName;
                GameSessionContext.Instance?.SetDisplayName(confirmedName);

                if (enableDebugLogs)
                    Debug.Log($"[Step_CommitPlayerName] Display name committed: {confirmedName}");

                onComplete?.Invoke(StepOutcome.Success());
                yield break;
            }

            if (!PlayerNameCommitFlow.ShouldRePrompt(result))
            {
                onComplete?.Invoke(StepOutcome.Failed(result.ErrorMessage));
                yield break;
            }

            if (enableDebugLogs)
                Debug.LogWarning($"[Step_CommitPlayerName] {PlayerNameCommitFlow.NameTakenMessage}");

            string replacement = null;
            bool promptFinished = false;
            // SequenceRunner pumps this enumerator itself and forwards each yield
            // to Unity. A nested IEnumerator must be walked here or the prompt
            // never runs and the sequence fails immediately.
            IEnumerator prompt = PromptReplacementName(PlayerNameCommitFlow.NameTakenMessage, name =>
            {
                replacement = name;
                promptFinished = true;
            });
            while (prompt.MoveNext())
                yield return prompt.Current;

            if (!promptFinished || string.IsNullOrWhiteSpace(replacement))
            {
                onComplete?.Invoke(StepOutcome.Failed(PlayerNameCommitFlow.NameTakenMessage));
                yield break;
            }

            playerName = replacement.Trim();
            context.PlayerName = playerName;
            GameSessionContext.Instance?.SetDisplayName(playerName);
        }
    }

    private static IEnumerator PromptReplacementName(string message, System.Action<string> onName)
    {
        NameInputUI nameInputUI = Object.FindFirstObjectByType<NameInputUI>(FindObjectsInactive.Include);
        if (nameInputUI == null)
        {
            onName?.Invoke(null);
            yield break;
        }

        nameInputUI.SetEntryCopy("Dog's Name", message);
        if (!nameInputUI.gameObject.activeSelf)
            nameInputUI.gameObject.SetActive(true);

        string playerName = null;
        void HandleConfirmed(string name) => playerName = name;
        nameInputUI.OnNameConfirmed += HandleConfirmed;

        bool shown = nameInputUI.Show();
        if (!shown)
        {
            nameInputUI.OnNameConfirmed -= HandleConfirmed;
            onName?.Invoke(null);
            yield break;
        }

        while (playerName == null)
            yield return null;

        nameInputUI.OnNameConfirmed -= HandleConfirmed;
        nameInputUI.Hide();
        onName?.Invoke(playerName);
    }

    /// <summary>
    /// A failed name save must stop the intro. Skip-and-continue would walk
    /// into the bedroom, starter grant, and intro complete with a null server name.
    /// name_taken is handled inside Execute and does not fail the step.
    /// </summary>
    public override StepFailurePolicy GetFailurePolicy() => StepFailurePolicy.AbortSequence;

    public override bool Validate()
    {
        bool valid = base.Validate();

        if (supabaseConfig == null)
        {
            Debug.LogWarning("[Step_CommitPlayerName] SupabaseConfigSO is not assigned.");
            valid = false;
        }

        return valid;
    }

    private string ResolvePlayerName(SequenceContext context)
    {
        if (context == null)
            return "";

        if (useContextPlayerName)
            return context.PlayerName ?? "";

        return context.Variables.Get<string>(playerNameVariable) ?? "";
    }

    private IPlayerNameService ResolveService()
    {
        if (GameSessionContext.Instance != null && GameSessionContext.Instance.IsOfflineSession)
            return LocalSaveServiceStub.Instance;

        SupabasePlayerNameService service = Object.FindFirstObjectByType<SupabasePlayerNameService>(FindObjectsInactive.Include);
        if (service == null)
        {
            Transform parent = GameServicesContainer.Instance != null ? GameServicesContainer.Instance.transform : null;
            GameObject go = new GameObject("[SupabasePlayerNameService]");
            if (parent != null)
                go.transform.SetParent(parent, false);
            else
                Object.DontDestroyOnLoad(go);

            service = go.AddComponent<SupabasePlayerNameService>();
        }

        service.Configure(supabaseConfig);
        return service;
    }
}
