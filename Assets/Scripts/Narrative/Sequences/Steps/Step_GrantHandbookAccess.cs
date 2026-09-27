using System.Collections;
using Chainbound.Combat;
using UnityEngine;

[CreateAssetMenu(
    fileName = "Step_GrantHandbookAccess",
    menuName = "Chainbound/Narrative/Steps/Grant Handbook Access")]
public class Step_GrantHandbookAccess : SequenceStep
{
    [Header("Supabase")]
    [SerializeField] private SupabaseConfigSO supabaseConfig;
    [SerializeField] private bool waitForRemote = false;

    [Header("Flag")]
    [SerializeField] private string flagKey = "handbook_unlocked";
    [SerializeField] private string localPrefsKey = "SignalJam_HandbookUnlocked";

    public override IEnumerator Execute(SequenceContext context, System.Action<StepOutcome> onComplete)
    {
        PlayerPrefs.SetInt(localPrefsKey, 1);
        PlayerPrefs.Save();

        GameSessionContext session = GameSessionContext.Instance;
        string playerProfileId = session != null ? session.PlayerProfileId : "";
        string walletAddress = session != null ? session.WalletAddress : "";

        if (supabaseConfig == null)
        {
            Debug.LogWarning("[Step_GrantHandbookAccess] SupabaseConfigSO not assigned. Local unlock was still written.");
            onComplete?.Invoke(StepOutcome.Success("Handbook unlocked locally."));
            yield return null;
            yield break;
        }

        var task = SupabaseProgressionFlagService.SetFlagAsync(
            supabaseConfig,
            playerProfileId,
            walletAddress,
            session != null ? session.AuthUserId : "",
            flagKey,
            true);

        if (waitForRemote)
        {
            while (!task.IsCompleted)
                yield return null;

            if (task.IsFaulted)
            {
                string message = task.Exception != null
                    ? task.Exception.GetBaseException().Message
                    : "Unknown Supabase progression flag failure.";
                Debug.LogWarning($"[Step_GrantHandbookAccess] Remote unlock failed: {message}");
                onComplete?.Invoke(StepOutcome.Success("Handbook unlocked locally; remote sync failed."));
                yield break;
            }

            MenuMutationResult result = task.Result;
            if (!result.success)
            {
                Debug.LogWarning($"[Step_GrantHandbookAccess] Remote unlock failed: {result.error}");
                onComplete?.Invoke(StepOutcome.Success("Handbook unlocked locally; remote sync failed."));
                yield break;
            }
        }

        onComplete?.Invoke(StepOutcome.Success("Combat Handbook unlocked."));
        yield return null;
    }
}
