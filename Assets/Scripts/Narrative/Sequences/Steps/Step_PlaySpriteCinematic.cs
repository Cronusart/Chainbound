using System.Collections;
using UnityEngine;

[CreateAssetMenu(fileName = "Step_PlaySpriteCinematic", menuName = "Chainbound/Narrative/Steps/Play Sprite Cinematic")]
public class Step_PlaySpriteCinematic : SequenceStep
{
    [SerializeField] private SpriteCinematicDefinitionSO cinematic;
    [Tooltip("Skips the step when the JWT-backed profile already recorded completion.")]
    [SerializeField] private bool suppressWhenProfileComplete = true;

    public override IEnumerator Execute(SequenceContext context, System.Action<StepOutcome> onComplete)
    {
        if (cinematic == null)
        {
            onComplete?.Invoke(StepOutcome.Failed("Sprite cinematic definition is missing."));
            yield break;
        }

        GameSessionContext session = GameSessionContext.Instance;
        if (IntroResumePolicy.ShouldSuppressCompletedCinematic(
                suppressWhenProfileComplete,
                session != null && session.IntroCinematicComplete,
                session != null ? session.DisplayName : ""))
        {
            onComplete?.Invoke(StepOutcome.Success("Sprite cinematic already complete."));
            yield break;
        }

        SpriteCinematicPlayer player = SpriteCinematicPlayer.Instance;
        if (player == null)
        {
            GameObject host = new GameObject("[SpriteCinematicPlayer]");
            player = host.AddComponent<SpriteCinematicPlayer>();
        }

        var task = player.Play(cinematic);
        while (!task.IsCompleted)
            yield return null;

        // Missing frames intentionally resolve as a black, completed cinematic;
        // only missing configuration/service is a failed onboarding step.
        SpriteCinematicResult result = task.Result;
        if (result.Kind == SpriteCinematicResultKind.Failed)
            onComplete?.Invoke(StepOutcome.Failed(result.Message));
        else
            onComplete?.Invoke(StepOutcome.Success(result.Message));
    }
}
