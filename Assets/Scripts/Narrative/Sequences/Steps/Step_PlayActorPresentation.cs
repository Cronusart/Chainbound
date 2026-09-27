using System.Collections;
using UnityEngine;

/// <summary>
/// Plays an exploration presentation clip on the player or an NPC presenter.
/// Uses the actor-local direct presenter API rather than TriggerRegistry indirection.
/// </summary>
[CreateAssetMenu(
    fileName = "Step_PlayActorPresentation",
    menuName = "Chainbound/Narrative/Steps/Play Actor Presentation")]
public class Step_PlayActorPresentation : SequenceStep
{
    public enum ActorTargetType
    {
        Player,
        NPCById
    }

    [Header("Target")]
    [SerializeField] private ActorTargetType targetType = ActorTargetType.NPCById;
    [SerializeField] private string npcId = string.Empty;

    [Header("Presentation")]
    [SerializeField] private ExplorationSpritePresentationClipSO clip;
    [SerializeField] private bool waitForClipPlayback = true;

    public override IEnumerator Execute(SequenceContext context, System.Action<StepOutcome> onComplete)
    {
        if (clip == null)
        {
            onComplete?.Invoke(StepOutcome.Failed("Presentation clip is not assigned."));
            yield break;
        }

        if (!clip.TryGetDefaultPlaybackRequest(out ExplorationPresentationPlaybackRequest playbackRequest, out string validationMessage))
        {
            onComplete?.Invoke(StepOutcome.Failed(validationMessage));
            yield break;
        }

        if (waitForClipPlayback && playbackRequest.Mode == ExplorationPresentationPlaybackMode.LoopUntilStopped)
        {
            onComplete?.Invoke(StepOutcome.Failed(
                "waitForClipPlayback cannot be used with LoopUntilStopped clips unless an explicit stop path exists."));
            yield break;
        }

        ExplorationSpritePresentationPresenter presenter = ResolvePresenter();
        if (presenter == null)
        {
            onComplete?.Invoke(StepOutcome.Failed("Target presenter could not be resolved."));
            yield break;
        }

        if (!presenter.PlayClip(clip, playbackRequest))
        {
            onComplete?.Invoke(StepOutcome.Failed("Presenter rejected the clip."));
            yield break;
        }

        if (waitForClipPlayback)
        {
            while (presenter != null && presenter.IsPlaybackAnimating)
                yield return null;
        }

        onComplete?.Invoke(StepOutcome.Success());
    }

    public override bool Validate()
    {
        bool baseValid = base.Validate();
        if (clip == null)
        {
            Debug.LogWarning("[Step_PlayActorPresentation] Presentation clip is not assigned.");
            return false;
        }

        if (!clip.TryGetDefaultPlaybackRequest(out ExplorationPresentationPlaybackRequest playbackRequest, out string validationMessage))
        {
            Debug.LogWarning($"[Step_PlayActorPresentation] {validationMessage}");
            return false;
        }

        if (waitForClipPlayback && playbackRequest.Mode == ExplorationPresentationPlaybackMode.LoopUntilStopped)
        {
            Debug.LogWarning(
                "[Step_PlayActorPresentation] waitForClipPlayback should not be enabled for LoopUntilStopped clips without an explicit stop path.");
            return false;
        }

        if (targetType == ActorTargetType.NPCById && string.IsNullOrWhiteSpace(npcId))
        {
            Debug.LogWarning("[Step_PlayActorPresentation] NPC target type requires an npcId.");
            return false;
        }

        return baseValid;
    }

    private ExplorationSpritePresentationPresenter ResolvePresenter()
    {
        switch (targetType)
        {
            case ActorTargetType.Player:
            {
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                return player != null ? player.GetComponent<ExplorationSpritePresentationPresenter>() : null;
            }
            case ActorTargetType.NPCById:
            default:
            {
                if (string.IsNullOrWhiteSpace(npcId))
                    return null;

                NPCController npc = NPCRegistry.Instance.GetNPC(npcId);
                return npc != null ? npc.GetComponent<ExplorationSpritePresentationPresenter>() : null;
            }
        }
    }
}
