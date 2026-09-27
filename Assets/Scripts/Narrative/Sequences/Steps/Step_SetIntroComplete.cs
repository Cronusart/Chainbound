using System.Collections;
using UnityEngine;
using Chainbound.Combat;

/// <summary>
/// Marks the Chainbound intro quest as complete in one step:
///   1. Syncs intro_complete=true through the authenticated profile_set_intro_complete endpoint.
///   2. Updates GameSessionContext.Instance.MarkIntroComplete() for the live session.
///   3. Completes the quest in QuestService so any in-game quest log reflects completion.
///
/// The sequence never blocks on the network call — completion proceeds immediately.
/// Supabase sync failure is logged but does not fail the step; the PlayerPrefs flag
/// written by SupabaseIntroCompleteHelper ensures offline resilience.
/// </summary>
[CreateAssetMenu(fileName = "Step_SetIntroComplete", menuName = "Chainbound/Narrative/Steps/Set Intro Complete")]
public class Step_SetIntroComplete : SequenceStep
{
    [Header("Supabase")]
    [Tooltip("SupabaseConfigSO asset. Assign the same config used by SupabaseClient elsewhere.")]
    [SerializeField] private SupabaseConfigSO supabaseConfig;

    [Header("Quest")]
    [Tooltip("Quest ID to complete in QuestService. Must match Chainbound_Intro.asset questId.")]
    [SerializeField] private string introQuestId = "chainbound_intro";

    public override IEnumerator Execute(SequenceContext context, System.Action<StepOutcome> onComplete)
    {
        // 1. Resolve identity from GameSessionContext so profile-first calls can proceed without a wallet.
        GameSessionContext sessionContext = GameSessionContext.Instance;
        string wallet = sessionContext != null ? sessionContext.WalletAddress : string.Empty;
        string playerProfileId = sessionContext != null ? sessionContext.PlayerProfileId : string.Empty;

        // 2. The profile quest mutation mirrors intro_complete atomically on the
        // backend. Keep the old endpoint only as a compatibility fallback when
        // the new coordinator is not active.
        NarrativeProfileStateCoordinator coordinator = FindFirstObjectByType<NarrativeProfileStateCoordinator>();
        if (coordinator == null || !coordinator.IsReady)
            SupabaseIntroCompleteHelper.SetIntroComplete(supabaseConfig, wallet);

        // 3. Update live session context so downstream systems see the change this frame.
        GameSessionContext.Instance?.MarkIntroComplete();

        // 4. Complete the quest in QuestService.
        if (!string.IsNullOrEmpty(introQuestId))
        {
            QuestService qs = QuestService.Instance;
            if (qs != null)
            {
                if (qs.IsQuestActive(introQuestId))
                    qs.CompleteQuest(introQuestId);
                else if (qs.IsQuestComplete(introQuestId))
                {
                    // Resolution-aware completion already ran. This step remains
                    // only as the legacy intro_complete compatibility mirror.
                }
                else if (enableDebugLogs)
                    Debug.LogWarning($"[Step_SetIntroComplete] Quest '{introQuestId}' is not active — CompleteQuest skipped.");
            }
            else if (enableDebugLogs)
            {
                Debug.LogWarning("[Step_SetIntroComplete] QuestService not found — quest completion skipped.");
            }
        }

        if (enableDebugLogs)
            Debug.Log($"[Step_SetIntroComplete] Intro complete. profile={playerProfileId} wallet={wallet}");

        onComplete?.Invoke(StepOutcome.Success("Intro marked complete."));
        yield return null;
    }

    public override bool Validate()
    {
        bool valid = base.Validate();

        if (supabaseConfig == null)
        {
            Debug.LogWarning("[Step_SetIntroComplete] SupabaseConfigSO not assigned — remote sync will be skipped.");
            // Not a hard failure: offline PlayerPrefs path still works.
        }

        if (string.IsNullOrEmpty(introQuestId))
        {
            Debug.LogWarning("[Step_SetIntroComplete] introQuestId is empty — QuestService.CompleteQuest will be skipped.");
        }

        return valid;
    }
}
