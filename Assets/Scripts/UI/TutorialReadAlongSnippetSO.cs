using SignalJam.UI.MobileControls;
using UnityEngine;

/// <summary>
/// Data for one non-interactive tutorial read-along card. Sequences and other
/// systems show this through <see cref="TutorialReadAlongPresenter"/>; copy lives
/// here so future tutorials do not need new step types.
/// </summary>
[CreateAssetMenu(
    fileName = "TutorialReadAlongSnippet",
    menuName = "Chainbound/UI/Tutorial Read-Along Snippet")]
public sealed class TutorialReadAlongSnippetSO : ScriptableObject
{
    public const string DefaultContinueHint = "Press any button";

    [SerializeField] private string snippetId = "";
    [SerializeField] private string title = "";
    [TextArea(2, 6)]
    [SerializeField] private string body = "";
    [SerializeField] private Sprite icon;
    [SerializeField] private string continueHint = DefaultContinueHint;
    [SerializeField] private MobileHudEmphasisTarget emphasis = MobileHudEmphasisTarget.None;

    public string SnippetId => snippetId != null ? snippetId.Trim() : "";
    public string Title => title != null ? title.Trim() : "";
    public string Body => body != null ? body.Trim() : "";
    public Sprite Icon => icon;
    public string ContinueHint => string.IsNullOrWhiteSpace(continueHint)
        ? DefaultContinueHint
        : continueHint.Trim();
    public MobileHudEmphasisTarget Emphasis => emphasis;
    public bool KeepsHudVisible => emphasis != MobileHudEmphasisTarget.None;

    public bool IsValid => !string.IsNullOrWhiteSpace(SnippetId) && !string.IsNullOrWhiteSpace(Body);
}
