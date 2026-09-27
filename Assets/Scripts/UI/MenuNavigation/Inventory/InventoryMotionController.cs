using System.Collections.Generic;
using DG.Tweening;
using SignalJam.UI.MenuNavigation.PartyTab;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SignalJam.UI.MenuNavigation
{
    [DisallowMultipleComponent]
    public sealed class InventoryMotionController : MonoBehaviour
    {
        [SerializeField] private PartyTabMotionProfileSO profile;
        [SerializeField] private float panelEnterOffsetY = -16f;
        [SerializeField] private float rowEnterOffsetY = 14f;
        [SerializeField] private float rowEnterDuration = 0.16f;
        [SerializeField] private float rowEnterSettleDuration = 0.08f;
        [SerializeField] private float rowEnterStagger = 0.035f;
        [SerializeField, Range(0f, 1f)] private float rowEnterStartScaleY = 0f;
        [SerializeField] private float rowEnterOvershootY = -4f;
        [SerializeField] private float selectedRowScale = 1.02f;
        [SerializeField] private float unselectedRowScale = 1f;
        [SerializeField] private float rowFocusDuration = 0.12f;
        [SerializeField] private float portraitFadeDuration = 0.12f;
        [SerializeField] private float portraitStartScale = 0.96f;
        [SerializeField] private float actionFocusScale = 1.12f;
        [SerializeField] private float statusFlashScale = 1.04f;

        private readonly List<Tween> activeTweens = new List<Tween>();

        public void Configure(PartyTabMotionProfileSO motionProfile)
        {
            if (motionProfile != null)
                profile = motionProfile;
        }

        private void OnDisable()
        {
            KillAll();
        }

        public void KillAll()
        {
            for (int i = activeTweens.Count - 1; i >= 0; i--)
            {
                if (activeTweens[i] != null && activeTweens[i].IsActive())
                    activeTweens[i].Kill();
            }

            activeTweens.Clear();
        }

        public void PlayScreenEnter(IReadOnlyList<CanvasGroup> sections)
        {
            if (sections == null)
                return;

            KillAll();

            float duration = profile != null ? profile.ScreenEnterDuration : 0.12f;
            float stagger = profile != null ? profile.ScreenEnterStagger : 0.04f;
            float startScale = profile != null ? profile.ScreenEnterStartScale : 0.99f;

            for (int i = 0; i < sections.Count; i++)
            {
                CanvasGroup group = sections[i];
                if (group == null)
                    continue;

                RectTransform rect = group.transform as RectTransform;
                Vector2 restPos = rect != null ? rect.anchoredPosition : Vector2.zero;
                group.DOKill();
                if (rect != null)
                    rect.DOKill();

                group.alpha = 0f;
                if (rect != null)
                {
                    rect.localScale = Vector3.one * startScale;
                    rect.anchoredPosition = restPos + new Vector2(0f, panelEnterOffsetY);
                }

                float delay = i * stagger;
                Track(group.DOFade(1f, duration)
                    .SetDelay(delay)
                    .SetEase(Ease.OutSine)
                    .SetUpdate(true));

                if (rect != null)
                {
                    Track(rect.DOAnchorPos(restPos, duration)
                        .SetDelay(delay)
                        .SetEase(Ease.OutSine)
                        .SetUpdate(true));
                    Track(rect.DOScale(Vector3.one, duration)
                        .SetDelay(delay)
                        .SetEase(Ease.OutSine)
                        .SetUpdate(true));
                }
            }
        }

        public void PlayRowsIn(IReadOnlyList<InventoryItemRowDisplay> rows)
        {
            if (rows == null)
                return;

            for (int i = 0; i < rows.Count; i++)
            {
                InventoryItemRowDisplay row = rows[i];
                if (row == null)
                    continue;

                CanvasGroup group = row.CanvasGroup;
                RectTransform rect = row.transform as RectTransform;
                Vector2 restPos = row.RestAnchoredPosition;
                Vector3 restScale = rect != null ? rect.localScale : Vector3.one;
                bool layoutDriven = IsLayoutDriven(rect);
                if (group != null)
                    group.DOKill();
                if (rect != null)
                    rect.DOKill();

                if (group != null)
                    group.alpha = 0f;
                if (rect != null)
                {
                    RestoreLayoutPosition(rect, restPos);
                    if (!layoutDriven)
                        rect.anchoredPosition = restPos + new Vector2(0f, rowEnterOffsetY);
                    rect.localScale = new Vector3(restScale.x, rowEnterStartScaleY, restScale.z);
                }

                float delay = i * rowEnterStagger;
                if (group != null)
                {
                    Track(group.DOFade(1f, rowEnterDuration)
                        .SetDelay(delay)
                        .SetEase(Ease.OutSine)
                        .SetUpdate(true));
                }

                if (rect != null)
                {
                    Sequence sequence = DOTween.Sequence().SetUpdate(true);
                    sequence.SetDelay(delay);
                    sequence.Join(rect.DOScale(restScale, rowEnterDuration)
                        .SetEase(Ease.OutSine));
                    if (!layoutDriven)
                    {
                        sequence.Join(rect.DOAnchorPos(restPos + new Vector2(0f, rowEnterOvershootY), rowEnterDuration)
                            .SetEase(Ease.OutSine));
                        sequence.Append(rect.DOAnchorPos(restPos, rowEnterSettleDuration)
                            .SetEase(Ease.InOutSine));
                    }
                    else
                    {
                        sequence.Append(rect.DOScale(restScale, rowEnterSettleDuration)
                            .SetEase(Ease.InOutSine));
                    }

                    Track(sequence);
                }
            }
        }

        public void ResetRowAnimation(IReadOnlyList<InventoryItemRowDisplay> rows)
        {
            if (rows == null)
                return;

            for (int i = 0; i < rows.Count; i++)
            {
                InventoryItemRowDisplay row = rows[i];
                if (row == null)
                    continue;

                CanvasGroup group = row.CanvasGroup;
                RectTransform rect = row.transform as RectTransform;
                if (group != null)
                {
                    group.DOKill();
                    group.alpha = 1f;
                }

                if (rect != null)
                {
                    rect.DOKill();
                    RestoreLayoutPosition(rect, row.RestAnchoredPosition);
                    rect.localScale = Vector3.one;
                }
            }
        }

        public void SetRowSelected(InventoryItemRowDisplay row, bool selected, bool animate)
        {
            if (row == null)
                return;

            RectTransform rect = row.transform as RectTransform;
            if (rect == null)
                return;

            rect.DOKill();
            RestoreLayoutPosition(rect, row.RestAnchoredPosition);
            Vector3 targetScale = Vector3.one * (selected ? selectedRowScale : unselectedRowScale);
            if (animate && Application.isPlaying)
            {
                Track(rect.DOScale(targetScale, rowFocusDuration)
                    .SetEase(Ease.OutSine)
                    .SetUpdate(true));
            }
            else
            {
                rect.localScale = targetScale;
            }
        }

        private static void RestoreLayoutPosition(RectTransform rect, Vector2 fallbackPosition)
        {
            if (rect == null)
                return;

            if (IsLayoutDriven(rect))
            {
                RectTransform parent = rect.parent as RectTransform;
                if (parent != null)
                    LayoutRebuilder.MarkLayoutForRebuild(parent);
                return;
            }

            rect.anchoredPosition = fallbackPosition;
        }

        private static bool IsLayoutDriven(RectTransform rect)
        {
            if (rect == null || rect.parent == null)
                return false;

            HorizontalOrVerticalLayoutGroup layout = rect.parent.GetComponent<HorizontalOrVerticalLayoutGroup>();
            if (layout == null || !layout.isActiveAndEnabled)
                return false;

            LayoutElement layoutElement = rect.GetComponent<LayoutElement>();
            return layoutElement == null || !layoutElement.ignoreLayout;
        }

        public void SetActionSelected(Selectable action, bool selected, bool animate)
        {
            if (action == null)
                return;

            RectTransform rect = action.transform as RectTransform;
            if (rect == null)
                return;

            rect.DOKill();
            Vector3 targetScale = Vector3.one * (selected ? actionFocusScale : 1f);
            if (animate && Application.isPlaying)
            {
                Track(rect.DOScale(targetScale, rowFocusDuration)
                    .SetEase(Ease.OutSine)
                    .SetUpdate(true));
            }
            else
            {
                rect.localScale = targetScale;
            }
        }

        public void PlayDetailUpdate(CanvasGroup group, Transform target)
        {
            if ((group == null && target == null) || !Application.isPlaying)
                return;

            if (group != null)
            {
                group.DOKill();
                group.alpha = 0f;
                Track(group.DOFade(1f, portraitFadeDuration)
                    .SetEase(Ease.OutSine)
                    .SetUpdate(true));
            }

            if (target != null)
            {
                target.DOKill();
                target.localScale = Vector3.one * portraitStartScale;
                Track(target.DOScale(Vector3.one, portraitFadeDuration)
                    .SetEase(Ease.OutSine)
                    .SetUpdate(true));
            }
        }

        public void PlayPortraitUpdate(CanvasGroup portraitGroup)
        {
            PlayDetailUpdate(portraitGroup, portraitGroup != null ? portraitGroup.transform : null);
        }

        public void PlayUseConfirm(Transform target, TMP_Text statusLabel)
        {
            PlayActionFeedback(target, statusLabel, true);
        }

        public void PlayActionFeedback(Transform target, TMP_Text statusLabel, bool valid)
        {
            if (!Application.isPlaying)
                return;

            if (target != null)
            {
                target.DOKill();
                float pressScale = valid
                    ? profile != null ? profile.ButtonPressScale : 0.95f
                    : 0.97f;
                float pressDuration = profile != null ? profile.ButtonPressDuration : 0.08f;
                Sequence sequence = DOTween.Sequence().SetUpdate(true);
                sequence.Append(target.DOScale(Vector3.one * pressScale, pressDuration).SetEase(Ease.OutSine));
                sequence.Append(target.DOScale(Vector3.one, pressDuration).SetEase(Ease.OutSine));
                Track(sequence);
            }

            if (statusLabel != null)
            {
                statusLabel.transform.DOKill();
                Track(statusLabel.transform
                    .DOPunchScale(
                        Vector3.one * (statusFlashScale - 1f),
                        valid ? 0.16f : 0.2f,
                        valid ? 2 : 4,
                        valid ? 0.4f : 0.8f)
                    .SetUpdate(true));
            }
        }

        private void Track(Tween tween)
        {
            if (tween == null)
                return;

            activeTweens.Add(tween);
            tween.OnKill(() => activeTweens.Remove(tween));
        }
    }
}
