using System;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// Reusable staged projection reveal for authored UI panels.
///
/// The motion mirrors the Chainling starter-selection panel: the panel forms
/// from a horizontal projection line, the dimmer fades in, then the authored
/// content and title settle. The component is intentionally data-driven so it
/// can be reused by other reward and modal panels.
/// </summary>
[DisallowMultipleComponent]
public sealed class ItemObtainPanelReveal : MonoBehaviour
{
    [Header("Targets")]
    [SerializeField] private RectTransform panelRoot;
    [SerializeField] private CanvasGroup rootGroup;
    [SerializeField] private CanvasGroup dimmerGroup;
    [SerializeField] private CanvasGroup titleGroup;
    [SerializeField] private CanvasGroup[] contentGroups;

    [Header("Projection")]
    [SerializeField, Min(0.01f)] private float expandXDuration = 0.22f;
    [SerializeField, Min(0.01f)] private float expandYDuration = 0.38f;
    [SerializeField, Range(0.01f, 0.25f)] private float projectionLineHeight = 0.05f;
    [SerializeField, Min(0f)] private float expandYOvershoot = 1.35f;
    [SerializeField, Min(0f)] private float dimmerFadeDuration = 0.14f;
    [SerializeField, Min(0f)] private float contentDelay = 0.25f;
    [SerializeField, Min(0f)] private float contentFadeDuration = 0.16f;
    [SerializeField] private bool useUnscaledTime = true;

    private Sequence revealSequence;
    private bool revealPlaying;

    public RectTransform PanelRoot => panelRoot;

    /// <summary>
    /// True from the start of a staged reveal until that sequence completes or is killed.
    /// Immediate reveals never report as playing.
    /// </summary>
    public bool IsPlaying => revealPlaying;

    private void Awake()
    {
        AutoConfigure();
    }

    private void OnDestroy()
    {
        revealSequence?.Kill();
    }

    /// <summary>
    /// Binds the authored hierarchy. This is also safe to call after the
    /// runtime fallback builder has completed.
    /// </summary>
    public void Configure(
        RectTransform targetPanelRoot,
        CanvasGroup targetRootGroup,
        CanvasGroup targetDimmerGroup,
        CanvasGroup targetTitleGroup,
        CanvasGroup[] targetContentGroups)
    {
        panelRoot = targetPanelRoot;
        rootGroup = targetRootGroup;
        dimmerGroup = targetDimmerGroup;
        titleGroup = targetTitleGroup;
        contentGroups = targetContentGroups ?? Array.Empty<CanvasGroup>();
    }

    /// <summary>
    /// Resolves the conventional ItemObtainPanel hierarchy without replacing
    /// authored objects. It keeps runtime-generated fallback UI compatible.
    /// </summary>
    public void AutoConfigure()
    {
        if (rootGroup == null)
            rootGroup = GetComponent<CanvasGroup>();

        if (panelRoot == null)
        {
            Transform candidate = transform.Find("SafeArea/Card");
            if (candidate == null)
                candidate = transform.Find("Card");
            panelRoot = candidate as RectTransform;
            if (panelRoot == null)
                panelRoot = transform as RectTransform;
        }

        if (dimmerGroup == null)
            dimmerGroup = FindCanvasGroup("SafeArea/Dismiss", "Dismiss");

        if (titleGroup == null)
        {
            titleGroup = FindCanvasGroup(
                "SafeArea/Card/Details/Title",
                "Card/Details/Title",
                "Details/Title",
                "SafeArea/Card/Title",
                "Card/Title",
                "Title");
            if (titleGroup == null && panelRoot != null)
            {
                Transform title = FindChildByName(panelRoot, "Title");
                if (title != null)
                    titleGroup = title.gameObject.AddComponent<CanvasGroup>();
            }
        }

        if (contentGroups == null || contentGroups.Length == 0 || HasMissingContentGroup())
            contentGroups = DiscoverContentGroups();
    }

    public void Reveal(bool immediate = false, bool keepDimmer = false)
    {
        AutoConfigure();
        KillTweens();

        if (rootGroup != null)
        {
            rootGroup.alpha = 1f;
            rootGroup.interactable = true;
            rootGroup.blocksRaycasts = true;
        }

        if (panelRoot == null)
        {
            SetVisibleImmediate();
            return;
        }

        if (immediate)
        {
            SetVisibleImmediate();
            return;
        }

        panelRoot.gameObject.SetActive(true);
        panelRoot.localScale = new Vector3(0.001f, 0.001f, 1f);
        if (keepDimmer)
            SetGroupState(dimmerGroup, 1f, true, true);
        else
            SetGroupState(dimmerGroup, 0f, false, true);
        SetGroupState(titleGroup, 0f, false, false);
        SetContentState(0f, false, false);

        revealSequence = DOTween.Sequence().SetUpdate(useUnscaledTime);
        float xDuration = Mathf.Max(0.01f, expandXDuration);
        revealSequence.Append(
            panelRoot.DOScaleX(1f, xDuration).SetEase(Ease.OutCubic));
        revealSequence.Join(
            panelRoot.DOScaleY(Mathf.Clamp(projectionLineHeight, 0.01f, 0.25f), xDuration)
                .SetEase(Ease.OutCubic));
        revealSequence.Append(
            panelRoot.DOScaleY(1f, Mathf.Max(0.01f, expandYDuration))
                .SetEase(Ease.OutBack, Mathf.Max(0f, expandYOvershoot)));

        if (!keepDimmer && dimmerGroup != null)
        {
            revealSequence.Append(
                dimmerGroup.DOFade(1f, Mathf.Max(0f, dimmerFadeDuration))
                    .SetEase(Ease.OutQuad));
        }

        if (contentDelay > 0f)
            revealSequence.AppendInterval(contentDelay);

        bool hasContentTween = false;
        if (contentGroups != null)
        {
            for (int i = 0; i < contentGroups.Length; i++)
            {
                CanvasGroup group = contentGroups[i];
                if (group == null)
                    continue;

                Tween fade = group.DOFade(1f, Mathf.Max(0f, contentFadeDuration))
                    .SetEase(Ease.OutQuad);
                if (hasContentTween)
                    revealSequence.Join(fade);
                else
                {
                    revealSequence.Append(fade);
                    hasContentTween = true;
                }
            }
        }

        if (titleGroup != null)
        {
            Tween titleFade = titleGroup.DOFade(1f, Mathf.Max(0f, contentFadeDuration))
                .SetEase(Ease.OutQuad);
            revealSequence.Append(titleFade);
        }

        revealPlaying = true;
        revealSequence.OnComplete(() =>
        {
            SetGroupState(dimmerGroup, 1f, true, true);
            SetGroupState(titleGroup, 1f, false, false);
            SetContentState(1f, true, true);
            revealPlaying = false;
            revealSequence = null;
        });
        revealSequence.Play();
    }

    /// <summary>
    /// Hides the card while leaving the dimmer and root group visible.
    /// Used when another tutorial card is about to replace this one.
    /// </summary>
    public void HidePanelKeepDimmer()
    {
        AutoConfigure();
        KillTweens();

        if (rootGroup != null)
        {
            rootGroup.alpha = 1f;
            rootGroup.interactable = true;
            rootGroup.blocksRaycasts = true;
        }

        if (panelRoot != null)
        {
            panelRoot.localScale = Vector3.one;
            panelRoot.gameObject.SetActive(false);
        }

        SetGroupState(dimmerGroup, 1f, true, true);
        SetGroupState(titleGroup, 1f, false, false);
        SetContentState(1f, true, true);
    }

    public void HideImmediate()
    {
        AutoConfigure();
        KillTweens();

        if (rootGroup != null)
        {
            rootGroup.alpha = 0f;
            rootGroup.interactable = false;
            rootGroup.blocksRaycasts = false;
        }

        if (panelRoot != null)
            panelRoot.localScale = Vector3.one;

        SetGroupState(dimmerGroup, 1f, true, true);
        SetGroupState(titleGroup, 1f, false, false);
        SetContentState(1f, true, true);
    }

    private void SetVisibleImmediate()
    {
        if (rootGroup != null)
        {
            rootGroup.alpha = 1f;
            rootGroup.interactable = true;
            rootGroup.blocksRaycasts = true;
        }

        if (panelRoot != null)
        {
            panelRoot.gameObject.SetActive(true);
            panelRoot.localScale = Vector3.one;
        }

        SetGroupState(dimmerGroup, 1f, true, true);
        SetGroupState(titleGroup, 1f, false, false);
        SetContentState(1f, true, true);
    }

    private void SetContentState(float alpha, bool interactable, bool blocksRaycasts)
    {
        if (contentGroups == null)
            return;

        for (int i = 0; i < contentGroups.Length; i++)
            SetGroupState(contentGroups[i], alpha, interactable, blocksRaycasts);
    }

    private static void SetGroupState(CanvasGroup group, float alpha, bool interactable, bool blocksRaycasts)
    {
        if (group == null)
            return;

        group.alpha = Mathf.Clamp01(alpha);
        group.interactable = interactable;
        group.blocksRaycasts = blocksRaycasts;
    }

    private CanvasGroup FindCanvasGroup(params string[] paths)
    {
        if (paths == null)
            return null;

        for (int i = 0; i < paths.Length; i++)
        {
            Transform candidate = transform.Find(paths[i]);
            if (candidate == null)
                continue;

            CanvasGroup group = candidate.GetComponent<CanvasGroup>();
            if (group != null)
                return group;

            return candidate.gameObject.AddComponent<CanvasGroup>();
        }

        return null;
    }

    private bool HasMissingContentGroup()
    {
        if (contentGroups == null || contentGroups.Length == 0)
            return true;

        for (int i = 0; i < contentGroups.Length; i++)
        {
            if (contentGroups[i] == null)
                return true;
        }

        return false;
    }

    private CanvasGroup[] DiscoverContentGroups()
    {
        if (panelRoot == null)
            return Array.Empty<CanvasGroup>();

        var groups = new System.Collections.Generic.List<CanvasGroup>();
        for (int i = 0; i < panelRoot.childCount; i++)
        {
            Transform child = panelRoot.GetChild(i);
            if (child == null || ShouldSkipContentChild(child.name))
                continue;

            CanvasGroup group = child.GetComponent<CanvasGroup>();
            if (group == null)
                group = child.gameObject.AddComponent<CanvasGroup>();
            groups.Add(group);
        }

        return groups.ToArray();
    }

    private static bool ShouldSkipContentChild(string name)
    {
        return string.Equals(name, "ScanlineOverlay", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(name, "CardFrame", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(name, "Frame", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(name, "Title", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(name, "Prompt", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(name, "Quantity", StringComparison.OrdinalIgnoreCase);
    }

    private static Transform FindChildByName(Transform root, string name)
    {
        if (root == null || string.IsNullOrEmpty(name))
            return null;

        Transform direct = root.Find(name);
        if (direct != null)
            return direct;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindChildByName(root.GetChild(i), name);
            if (found != null)
                return found;
        }

        return null;
    }

    private void KillTweens()
    {
        revealPlaying = false;
        revealSequence?.Kill();
        revealSequence = null;
        if (panelRoot != null)
            panelRoot.DOKill();
        if (rootGroup != null)
            rootGroup.DOKill();
        if (dimmerGroup != null)
            dimmerGroup.DOKill();
        if (titleGroup != null)
            titleGroup.DOKill();
        if (contentGroups == null)
            return;

        for (int i = 0; i < contentGroups.Length; i++)
        {
            if (contentGroups[i] != null)
                contentGroups[i].DOKill();
        }
    }
}
