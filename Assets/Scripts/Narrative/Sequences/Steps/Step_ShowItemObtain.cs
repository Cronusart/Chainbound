using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Blocking sequence step that shows the shared item-obtain panel and waits
/// until the player dismisses it. Dialogue around this step should already be closed.
/// </summary>
[CreateAssetMenu(
    fileName = "Step_ShowItemObtain",
    menuName = "Chainbound/Narrative/Steps/Show Item Obtain")]
public sealed class Step_ShowItemObtain : SequenceStep
{
    [SerializeField] private string itemId = "";
    [SerializeField] private int quantity = 1;

    public string ItemId => itemId != null ? itemId.Trim() : "";
    public int Quantity => quantity;

    public override IEnumerator Execute(SequenceContext context, Action<StepOutcome> onComplete)
    {
        string id = ItemId;
        if (id.Length == 0)
        {
            onComplete?.Invoke(StepOutcome.Failed("Item obtain step has no item id."));
            yield break;
        }

        ItemObtainPresenter presenter = ItemObtainPresenter.Instance;
        if (presenter == null)
        {
            onComplete?.Invoke(StepOutcome.Failed("ItemObtainPresenter was not found."));
            yield break;
        }

        if (enableDebugLogs)
            Debug.Log($"[Step_ShowItemObtain] Showing '{id}' x{Mathf.Max(1, quantity)}.", this);

        presenter.Show(id, quantity);
        while (presenter.IsShowing)
            yield return null;

        onComplete?.Invoke(StepOutcome.Success());
    }

    public override bool Validate()
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            Debug.LogWarning($"[Step_ShowItemObtain] Step '{GetStepName()}' has no item id.");
            return false;
        }

        return base.Validate();
    }
}
