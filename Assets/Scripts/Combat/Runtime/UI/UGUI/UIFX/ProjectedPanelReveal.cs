using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

[DisallowMultipleComponent]
public class ProjectedPanelReveal : MonoBehaviour
{
    [Header("Targets")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private RectTransform panelRoot;

    [Header("Reveal")]
    [SerializeField] private float fadeInDuration = 0.28f;
    [SerializeField] private float scaleInDuration = 0.30f;
    [SerializeField] private float startScale = 0.985f;
    [SerializeField] private Ease scaleEase = Ease.OutCubic;

    [Header("Flicker")]
    [SerializeField] private bool flickerDuringReveal = true;
    [SerializeField] private int flickerPulses = 5;
    [SerializeField] private float flickerAmplitude = 0.12f; // alpha +/- amp
    [SerializeField] private float flickerStep = 0.04f;

    [Header("Child Stagger")]
    [SerializeField] private bool animateChildren = true;
    [SerializeField] private float childStagger = 0.035f;
    [SerializeField] private float childFade = 0.12f;
    [SerializeField] private float childScaleFrom = 0.98f;

    [Header("Runtime")]
    [SerializeField] private bool ignoreTimeScale = true;

    private Sequence _seq;
    private readonly List<CanvasGroup> _childGroups = new();

    private void Reset()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        panelRoot = GetComponent<RectTransform>();
    }

    private void Awake()
    {
        if (!canvasGroup) canvasGroup = GetComponent<CanvasGroup>();
        if (!canvasGroup) canvasGroup = gameObject.AddComponent<CanvasGroup>();
        if (!panelRoot) panelRoot = GetComponent<RectTransform>();

        CacheChildCanvasGroups();
    }

    private void CacheChildCanvasGroups()
    {
        _childGroups.Clear();
        if (!animateChildren) return;

        // Grab direct children only (keeps layout stable), but you can expand if desired.
        for (int i = 0; i < panelRoot.childCount; i++)
        {
            var child = panelRoot.GetChild(i);
            var cg = child.GetComponent<CanvasGroup>();
            if (!cg) cg = child.gameObject.AddComponent<CanvasGroup>();
            _childGroups.Add(cg);
        }
    }

    public void Reveal()
    {
        if (!canvasGroup || !panelRoot) return;

        Kill();
        EnsureParentActive();
        gameObject.SetActive(true);

        canvasGroup.alpha = 0f;
        panelRoot.localScale = Vector3.one * startScale;

        // Prep children
        if (animateChildren)
        {
            foreach (var cg in _childGroups) cg.alpha = 0f;
            for (int i = 0; i < panelRoot.childCount; i++)
                panelRoot.GetChild(i).localScale = Vector3.one * childScaleFrom;
        }

        _seq = DOTween.Sequence().SetUpdate(ignoreTimeScale);

        // Core fade + scale
        _seq.Join(FadeCanvasGroup(canvasGroup, 1f, fadeInDuration).SetEase(Ease.OutSine));
        _seq.Join(panelRoot.DOScale(1f, scaleInDuration).SetEase(scaleEase));

        // Flicker overlay (alpha modulation) during the first chunk
        if (flickerDuringReveal)
        {
            float baseA = 1f;
            for (int i = 0; i < flickerPulses; i++)
            {
                float a = Mathf.Clamp01(baseA + Random.Range(-flickerAmplitude, flickerAmplitude));
                _seq.Insert(i * flickerStep, FadeCanvasGroup(canvasGroup, a, flickerStep * 0.9f).SetEase(Ease.Linear));
            }
            _seq.Insert(flickerPulses * flickerStep, FadeCanvasGroup(canvasGroup, 1f, 0.06f).SetEase(Ease.OutSine));
        }

        // Child settle (stagger)
        if (animateChildren)
        {
            for (int i = 0; i < panelRoot.childCount; i++)
            {
                var child = panelRoot.GetChild(i);
                var cg = _childGroups[i];

                float at = 0.08f + i * childStagger; // delayed so panel forms first
                _seq.Insert(at, FadeCanvasGroup(cg, 1f, childFade).SetEase(Ease.OutSine));
                _seq.Insert(at, child.DOScale(1f, childFade).SetEase(Ease.OutCubic));
            }
        }

        _seq.Play();
    }

    public void Hide(float fadeOut = 0.12f)
    {
        if (!canvasGroup) return;

        Kill();
        _seq = DOTween.Sequence().SetUpdate(ignoreTimeScale);
        _seq.Append(FadeCanvasGroup(canvasGroup, 0f, fadeOut).SetEase(Ease.InSine));
        _seq.OnComplete(() => gameObject.SetActive(false));
        _seq.Play();
    }

    private void EnsureParentActive()
    {
        var t = transform.parent;
        while (t != null)
        {
            if (!t.gameObject.activeSelf)
                t.gameObject.SetActive(true);
            t = t.parent;
        }
    }

    private static Tweener FadeCanvasGroup(CanvasGroup target, float endValue, float duration)
    {
        return DOTween
            .To(() => target.alpha, x => target.alpha = x, endValue, duration)
            .SetTarget(target);
    }

    private void Kill()
    {
        if (_seq != null)
        {
            _seq.Kill();
            _seq = null;
        }
        if (canvasGroup) canvasGroup.DOKill();
        if (panelRoot) panelRoot.DOKill();
        if (animateChildren)
        {
            foreach (var cg in _childGroups) if (cg) cg.DOKill();
            for (int i = 0; i < panelRoot.childCount; i++) panelRoot.GetChild(i).DOKill();
        }
    }
}
