using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Blocking sequence step that shows a tutorial read-along card and waits for
/// any-button dismiss. Presentation lives on <see cref="TutorialReadAlongPresenter"/>.
/// </summary>
[CreateAssetMenu(
    fileName = "Step_ShowTutorialReadAlong",
    menuName = "Chainbound/Narrative/Steps/Show Tutorial Read-Along")]
public sealed class Step_ShowTutorialReadAlong : SequenceStep
{
    [Header("Read-Along")]
    [SerializeField] private TutorialReadAlongSnippetSO snippet;

    public TutorialReadAlongSnippetSO Snippet => snippet;

    public override IEnumerator Execute(SequenceContext context, Action<StepOutcome> onComplete)
    {
        if (snippet == null || !snippet.IsValid)
        {
            onComplete?.Invoke(StepOutcome.Failed("Tutorial read-along snippet is missing or empty."));
            yield break;
        }

        TutorialReadAlongPresenter presenter = TutorialReadAlongPresenter.Instance;
        if (presenter == null)
        {
            onComplete?.Invoke(StepOutcome.Failed("TutorialReadAlongPresenter was not found."));
            yield break;
        }

        if (enableDebugLogs)
            Debug.Log($"[Step_ShowTutorialReadAlong] Showing '{snippet.SnippetId}'.", this);

        if (!presenter.Show(snippet))
        {
            onComplete?.Invoke(StepOutcome.Failed("Tutorial read-along presenter failed to show."));
            yield break;
        }

        yield return presenter.WaitUntilDismissed();
        onComplete?.Invoke(StepOutcome.Success());
    }

    public override bool Validate()
    {
        if (snippet == null || !snippet.IsValid)
        {
            Debug.LogWarning($"[Step_ShowTutorialReadAlong] Step '{GetStepName()}' has no valid snippet.");
            return false;
        }

        return base.Validate();
    }
}
