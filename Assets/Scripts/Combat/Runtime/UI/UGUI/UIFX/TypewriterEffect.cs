using UnityEngine;
using TMPro;
using DG.Tweening;

[DisallowMultipleComponent]
public class TMPTypewriterReveal : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private TMP_Text tmp;

    [Header("Timing")]
    [SerializeField] private float startDelay = 0.0f;
    [SerializeField] private float charsPerSecond = 45f;
    [SerializeField] private bool ignoreTimeScale = true;

    [Header("Punctuation Pauses")]
    [SerializeField] private bool enablePunctuationPauses = true;
    [SerializeField] private float commaPause = 0.04f;
    [SerializeField] private float periodPause = 0.08f;

    [Header("Optional: Subtle Reveal Flicker")]
    [SerializeField] private bool flickerDuringReveal = false;
    [Range(0f, 1f)][SerializeField] private float flickerAmplitude = 0.10f;
    [SerializeField] private float flickerStep = 0.05f;

    [Header("Debug")]
    [SerializeField] private bool debugLog = false;

    private Tween _tween;
    private Tween _flickerTween;

    private void Reset()
    {
        tmp = GetComponent<TMP_Text>();
    }

    private void Awake()
    {
        if (!tmp) tmp = GetComponent<TMP_Text>();
    }

    private void OnDisable()
    {
        Kill();
    }

    public void ResetToHidden()
    {
        if (!tmp) return;
        tmp.maxVisibleCharacters = 0;
        tmp.alpha = 1f;
    }

    public void Play()
    {
        if (!tmp)
        {
            if (debugLog) Debug.LogWarning($"[TMPTypewriterReveal] Play skipped: no TMP_Text on {name}.", this);
            return;
        }

        Kill();

        tmp.ForceMeshUpdate();
        int total = tmp.textInfo.characterCount;
        if (debugLog)
        {
            var preview = string.IsNullOrEmpty(tmp.text) ? "(empty)" : (tmp.text.Length > 40 ? tmp.text.Substring(0, 40) + "..." : tmp.text);
            Debug.Log($"[TMPTypewriterReveal] Play on '{name}': {total} chars, text='{preview}'", this);
        }

        if (total <= 0)
        {
            tmp.maxVisibleCharacters = int.MaxValue;
            if (debugLog) Debug.Log($"[TMPTypewriterReveal] Play skipped: no characters to reveal on '{name}'.", this);
            return;
        }

        tmp.maxVisibleCharacters = 0;

        // duration base
        float duration = Mathf.Max(0.01f, total / Mathf.Max(1f, charsPerSecond));

        // optional punctuation pauses (cheap approximation by extending duration)
        if (enablePunctuationPauses)
        {
            foreach (char c in tmp.text)
            {
                if (c == ',') duration += commaPause;
                else if (c == '.' || c == '!' || c == '?' ) duration += periodPause;
            }
        }

        // tween visible chars
        float v = 0f;
        _tween = DOTween.To(() => v, x => {
                v = x;
                tmp.maxVisibleCharacters = Mathf.Clamp(Mathf.RoundToInt(v), 0, total);
            },
            total, duration
        )
        .SetDelay(startDelay)
        .SetEase(Ease.Linear)
        .SetUpdate(ignoreTimeScale);

        // optional flicker
        if (flickerDuringReveal)
        {
            float baseA = 1f;
            _flickerTween = DOTween.To(
                () => tmp.alpha,
                a => tmp.alpha = a,
                baseA, flickerStep
            )
            .SetUpdate(ignoreTimeScale)
            .SetLoops(-1, LoopType.Yoyo)
            .OnStepComplete(() =>
            {
                float next = Mathf.Clamp01(baseA + Random.Range(-flickerAmplitude, flickerAmplitude));
                tmp.alpha = next;
            });
        }

        _tween.OnComplete(() =>
        {
            tmp.maxVisibleCharacters = int.MaxValue;
            if (_flickerTween != null) { _flickerTween.Kill(); _flickerTween = null; tmp.alpha = 1f; }
        });
    }

    public void Kill()
    {
        if (_tween != null) { _tween.Kill(); _tween = null; }
        if (_flickerTween != null) { _flickerTween.Kill(); _flickerTween = null; }
    }
}