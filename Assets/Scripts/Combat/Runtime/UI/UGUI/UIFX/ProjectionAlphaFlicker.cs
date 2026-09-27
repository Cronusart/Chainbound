using UnityEngine;
using DG.Tweening;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class ProjectionAlphaFlicker : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private CanvasGroup canvasGroup; // recommended
    [SerializeField] private bool autoAddCanvasGroup = true;

    [Header("Flicker Profile")]
    [Range(0f, 1f)][SerializeField] private float baseAlpha = 0.55f;
    [Range(0f, 1f)][SerializeField] private float minAlpha = 0.35f;
    [Range(0f, 1f)][SerializeField] private float maxAlpha = 0.75f;

    [SerializeField] private float minStepDuration = 0.03f;
    [SerializeField] private float maxStepDuration = 0.10f;

    [Tooltip("How many micro-steps before repeating a loop chunk.")]
    [SerializeField] private int stepsPerLoop = 18;

    [Tooltip("Occasionally spike brighter/dimmer to feel 'projector-y'.")]
    [Range(0f, 1f)][SerializeField] private float spikeChance = 0.18f;
    [SerializeField] private float spikeMultiplier = 1.25f;

    [Header("Runtime")]
    [SerializeField] private bool playOnEnable = true;
    [Tooltip("Delay before starting flicker. Use ~0.35s when sharing a CanvasGroup with ProjectedPanelReveal to avoid alpha conflicts.")]
    [SerializeField] private float delayBeforePlay = 0.35f; // default allows ProjectedPanelReveal to finish first
    [SerializeField] private bool ignoreTimeScale = true;

    private Sequence _seq;
    private Tween _delayTween;

    private void Reset()
    {
        canvasGroup = GetComponent<CanvasGroup>();
    }

    private void Awake()
    {
        if (!canvasGroup)
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (!canvasGroup && autoAddCanvasGroup) canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
        if (canvasGroup) canvasGroup.alpha = baseAlpha;
    }

    private void OnEnable()
    {
        if (!playOnEnable) return;

        KillDelay();
        if (delayBeforePlay <= 0f)
        {
            Play();
            return;
        }

        _delayTween = DOVirtual.DelayedCall(delayBeforePlay, () =>
        {
            _delayTween = null;
            Play();
        }, ignoreTimeScale);
    }

    private void OnDisable()
    {
        KillDelay();
        Stop();
    }

    private void KillDelay()
    {
        if (_delayTween != null)
        {
            _delayTween.Kill();
            _delayTween = null;
        }
    }

    public void Play()
    {
        if (!canvasGroup) return;

        KillDelay();
        Stop();
        canvasGroup.DOKill();
        canvasGroup.alpha = baseAlpha;

        _seq = DOTween.Sequence()
            .SetUpdate(ignoreTimeScale)
            .SetAutoKill(false);

        for (int i = 0; i < stepsPerLoop; i++)
        {
            float t = Random.Range(minStepDuration, maxStepDuration);

            float target = Random.Range(minAlpha, maxAlpha);
            target = Mathf.Lerp(target, baseAlpha, 0.35f); // bias toward base

            if (Random.value < spikeChance)
            {
                float spike = (Random.value < 0.5f) ? -1f : 1f;
                target = Mathf.Clamp01(target * (1f + spike * (spikeMultiplier - 1f)));
            }

            _seq.Append(FadeCanvasGroup(canvasGroup, target, t).SetEase(Ease.Linear));
        }

        // Ease back toward base at end of loop chunk (feels less “random noise”)
        _seq.Append(FadeCanvasGroup(canvasGroup, baseAlpha, 0.08f).SetEase(Ease.OutSine));

        _seq.SetLoops(-1);
        _seq.Play();
    }

    public void Stop()
    {
        if (_seq != null)
        {
            _seq.Kill();
            _seq = null;
        }
    }

    private static Tweener FadeCanvasGroup(CanvasGroup target, float endValue, float duration)
    {
        return DOTween
            .To(() => target.alpha, x => target.alpha = x, endValue, duration)
            .SetTarget(target);
    }
}
