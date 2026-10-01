using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Chainbound.Combat;
using UnityEngine;

/// <summary>
/// Grants and consumes profile bag stacks through the item-delta edge function.
/// Writes a bool result variable so the sequence can retry without opening the next flag.
/// </summary>
[CreateAssetMenu(
    fileName = "Step_ApplyProfileItemDelta",
    menuName = "Chainbound/Narrative/Steps/Apply Profile Item Delta")]
public class Step_ApplyProfileItemDelta : SequenceStep
{
    [Serializable]
    public class ItemLine
    {
        public string itemId = "";
        public int quantity = 1;
    }

    [Header("Delta")]
    [SerializeField] private string idempotencyKey = "";
    [SerializeField] private List<ItemLine> grants = new List<ItemLine>();
    [SerializeField] private List<ItemLine> consumes = new List<ItemLine>();

    [Header("Result")]
    [Tooltip("Local bool set true only when the server accepts the delta, including an idempotent replay.")]
    [SerializeField] private string resultVariableName = "lastItemDeltaOk";

    public string IdempotencyKey => idempotencyKey != null ? idempotencyKey.Trim() : "";
    public IReadOnlyList<ItemLine> Grants => grants;
    public IReadOnlyList<ItemLine> Consumes => consumes;
    public string ResultVariableName =>
        string.IsNullOrWhiteSpace(resultVariableName) ? "lastItemDeltaOk" : resultVariableName.Trim();

    public override IEnumerator Execute(SequenceContext context, Action<StepOutcome> onComplete)
    {
        if (context == null)
        {
            onComplete?.Invoke(StepOutcome.Failed("Sequence context is missing"));
            yield break;
        }

        if (string.IsNullOrWhiteSpace(IdempotencyKey))
        {
            context.Variables.Set(ResultVariableName, false, VariableScope.Local);
            Debug.LogError("[Step_ApplyProfileItemDelta] Idempotency key is empty.");
            onComplete?.Invoke(StepOutcome.Success("Item delta skipped: missing idempotency key."));
            yield break;
        }

        CoreBagProfileItemDeltaService service = CoreBagProfileItemDeltaService.EnsureInstance();
        Task<ProfileItemDeltaResult> task = service.ApplyAsync(
            IdempotencyKey,
            ToLines(grants),
            ToLines(consumes));

        while (task != null && !task.IsCompleted)
            yield return null;

        bool ok = task != null && task.Status == TaskStatus.RanToCompletion &&
            task.Result != null && task.Result.ok;
        if (!ok)
        {
            string error = task != null && task.Result != null ? task.Result.error : "item_delta_failed";
            Debug.LogWarning($"[Step_ApplyProfileItemDelta] '{IdempotencyKey}' failed: {error}");
        }
        else if (enableDebugLogs)
        {
            Debug.Log($"[Step_ApplyProfileItemDelta] '{IdempotencyKey}' applied.");
        }

        context.Variables.Set(ResultVariableName, ok, VariableScope.Local);
        onComplete?.Invoke(StepOutcome.Success(ok ? "Item delta applied." : "Item delta failed."));
    }

    public override bool Validate()
    {
        if (string.IsNullOrWhiteSpace(IdempotencyKey))
        {
            Debug.LogWarning("[Step_ApplyProfileItemDelta] Idempotency key is empty.");
            return false;
        }

        if ((grants == null || grants.Count == 0) && (consumes == null || consumes.Count == 0))
        {
            Debug.LogWarning("[Step_ApplyProfileItemDelta] Grant and consume lists are both empty.");
            return false;
        }

        return true;
    }

    private static ProfileItemDeltaLine[] ToLines(List<ItemLine> lines)
    {
        if (lines == null || lines.Count == 0)
            return Array.Empty<ProfileItemDeltaLine>();

        var result = new ProfileItemDeltaLine[lines.Count];
        for (int i = 0; i < lines.Count; i++)
        {
            ItemLine line = lines[i];
            result[i] = new ProfileItemDeltaLine
            {
                itemId = line != null ? line.itemId : "",
                quantity = line != null ? line.quantity : 0,
            };
        }

        return result;
    }
}
