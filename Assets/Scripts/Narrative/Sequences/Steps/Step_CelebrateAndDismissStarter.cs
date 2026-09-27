using System.Collections;
using UnityEngine;

/// <summary>
/// Shared post-commit starter beat: duck the active soundtrack, play the
/// celebration audio/visuals, then close the retained selection panel.
/// </summary>
[CreateAssetMenu(fileName = "Step_CelebrateAndDismissStarter", menuName = "Chainbound/Narrative/Steps/Celebrate And Dismiss Starter")]
public class Step_CelebrateAndDismissStarter : SequenceStep
{
    [Header("Celebration audio")]
    [SerializeField] private SoundEventSO victoryChime;
    [Tooltip("Fallback wait when the chime asset has no assigned clip yet.")]
    [SerializeField, Min(0f)] private float unassignedChimeWaitSeconds = 0.2f;

    [Header("Music duck")]
    [SerializeField, Range(0f, 1f)] private float duckTargetMultiplier = 0.05f;
    [SerializeField, Min(0f)] private float duckAttackSeconds = 0.08f;
    [SerializeField, Min(0f)] private float duckHoldSeconds = 0f;
    [SerializeField, Min(0f)] private float duckReleaseSeconds = 0.5f;

    public override IEnumerator Execute(SequenceContext context, System.Action<StepOutcome> onComplete)
    {
        StarterSelectionService selectionService = StarterSelectionService.Instance;
        StarterSelectionUI selectionUi = selectionService != null ? selectionService.SelectionUI : null;

        MusicDuckToken duckToken = MusicConductor.Instance.AcquireDuckToken(
            "Starter celebration",
            duckTargetMultiplier,
            duckAttackSeconds,
            duckHoldSeconds,
            duckReleaseSeconds);

        try
        {
            AudioHandle chimeHandle = victoryChime != null
                ? AudioService.Instance.Play(victoryChime)
                : default;
            float visualDuration = selectionUi != null && selectionUi.PreviewVisual != null
                ? selectionUi.PreviewVisual.PlayCelebration()
                : 0f;
            float audioDuration = ResolveChimeDuration(victoryChime);
            float minimumDuration = Mathf.Max(visualDuration, audioDuration, unassignedChimeWaitSeconds);

            float elapsed = 0f;
            while (elapsed < minimumDuration || (chimeHandle.IsValid && AudioService.Instance.IsPlaying(chimeHandle)))
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }
        finally
        {
            duckToken.Release();
        }

        if (selectionService != null)
            yield return selectionService.DismissSelectionAnimated();

        onComplete?.Invoke(StepOutcome.Success());
    }

    public override bool Validate()
    {
        // The chime intentionally starts unassigned so audio can be authored
        // independently; visual celebration and dismissal remain valid.
        return true;
    }

    private static float ResolveChimeDuration(SoundEventSO soundEvent)
    {
        if (soundEvent == null || soundEvent.Clips == null)
            return 0f;

        float duration = 0f;
        for (int i = 0; i < soundEvent.Clips.Length; i++)
        {
            AudioClip clip = soundEvent.Clips[i];
            if (clip != null)
                duration = Mathf.Max(duration, clip.length);
        }
        return duration;
    }
}
