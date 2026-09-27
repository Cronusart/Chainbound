using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SignalJam.UI.MenuNavigation.PartyTab
{
    [DisallowMultipleComponent]
    public class ChainlingPartyMotionController : MonoBehaviour
    {
        [SerializeField] private PartyTabMotionProfileSO profile;

        private readonly Dictionary<int, float> lastHpFillBySlot = new Dictionary<int, float>();
        private readonly List<Tween> activeTweens = new List<Tween>();

        public PartyTabMotionProfileSO Profile => profile;

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

        public void PlayScreenEnter(IReadOnlyList<ChainlingPartyColumnShell> columns, IReadOnlyList<float> targetAlphas = null)
        {
            if (profile == null || columns == null)
                return;

            KillAll();

            for (int i = 0; i < columns.Count; i++)
            {
                ChainlingPartyColumnShell column = columns[i];
                if (column == null)
                    continue;

                CanvasGroup group = column.ColumnCanvasGroup;
                RectTransform stage = column.VisualStage;
                if (group == null)
                    continue;

                group.DOKill();
                if (stage != null)
                    stage.DOKill();

                float targetAlpha = targetAlphas != null && i < targetAlphas.Count
                    ? targetAlphas[i]
                    : 1f;

                column.ApplyColumnVisibility(0f, false);
                if (group != null)
                    group.alpha = 1f;
                if (stage != null)
                    stage.localScale = column.VisualStageBaseScale * profile.ScreenEnterStartScale;

                Image hpFill = column.HpBarFill;
                int slotKey = column.SlotIndex >= 0 ? column.SlotIndex : i;
                lastHpFillBySlot.TryGetValue(slotKey, out float hpTarget);
                if (hpFill != null)
                {
                    PartyTabProgressBarUtility.SetFill01(hpFill, 0f);
                    if (hpTarget > 0.001f)
                    {
                        float fillDuration = profile.ScreenEnterProgressDuration > 0.05f
                            ? profile.ScreenEnterProgressDuration
                            : 0.46f;
                        Track(PartyTabProgressBarUtility
                            .DOFill01(hpFill, hpTarget, fillDuration)
                            .SetDelay(i * profile.ScreenEnterStagger)
                            .SetEase(Ease.OutSine)
                            .SetUpdate(true));
                    }
                }

                column.InfoPanelBinder?.ReplayEnterProgress(i * profile.ScreenEnterStagger);

                float delay = i * profile.ScreenEnterStagger;
                if (column.VisualPresenter != null)
                {
                    column.VisualPresenter.PrepareForEnter();
                    column.VisualPresenter.PlaySwapIn(delay);
                }

                Track(DOVirtual.Float(0f, targetAlpha, profile.ScreenEnterDuration, value =>
                    {
                        column.ApplyColumnVisibility(value, false);
                    })
                    .SetDelay(delay)
                    .SetEase(Ease.OutQuad)
                    .SetUpdate(true));

                if (stage != null)
                {
                    Track(stage.DOScale(column.VisualStageBaseScale, profile.ScreenEnterDuration)
                        .SetDelay(delay)
                        .SetEase(Ease.OutQuad)
                        .SetUpdate(true));
                }
            }
        }

        public void ApplyColumnOpacity(ChainlingPartyColumnShell column, float alpha, bool animate)
        {
            if (column == null)
                return;

            column.ApplyColumnVisibility(alpha, animate);
        }

        public void SetColumnFocused(
            ChainlingPartyColumnShell column,
            bool focused,
            bool columnEditingActive,
            bool animate,
            bool bounceToNormal = false)
        {
            if (profile == null || column == null)
                return;

            RectTransform stage = column.VisualStage;
            RectTransform columnRect = column.ColumnRect;
            CanvasGroup infoPanel = column.InfoPanelGroup;
            Vector3 baseScale = column.VisualStageBaseScale;
            Vector3 columnBaseScale = column.ColumnBaseScale;

            if (columnRect != null)
            {
                columnRect.DOKill();
                Vector3 targetColumnScale = !columnEditingActive
                    ? columnBaseScale
                    : focused
                        ? columnBaseScale * profile.FocusedColumnScale
                        : columnBaseScale * profile.UnfocusedColumnScale;
                bool overshootColumnToNormal = (focused && columnEditingActive) || bounceToNormal;

                if (animate && Application.isPlaying)
                {
                    float duration = focused
                        ? profile.VisualStageFocusInDuration
                        : profile.VisualStageFocusOutDuration;
                    Track(overshootColumnToNormal
                        ? BuildNormalScaleOvershoot(columnRect, targetColumnScale)
                        : BuildSoftScaleTween(columnRect, targetColumnScale, duration));
                }
                else
                {
                    columnRect.localScale = targetColumnScale;
                }
            }

            if (stage != null)
            {
                stage.DOKill();
                Vector3 targetScale = !columnEditingActive
                    ? baseScale
                    : focused
                        ? baseScale * profile.FocusedVisualStageScale
                        : baseScale * profile.UnfocusedVisualStageScale;
                bool overshootStageToNormal = columnRect == null && ((focused && columnEditingActive) || bounceToNormal);

                if (animate && Application.isPlaying)
                {
                    float duration = focused
                        ? profile.VisualStageFocusInDuration
                        : profile.VisualStageFocusOutDuration;
                    Track(overshootStageToNormal
                        ? BuildNormalScaleOvershoot(stage, targetScale)
                        : BuildSoftScaleTween(stage, targetScale, duration));
                }
                else
                {
                    stage.localScale = targetScale;
                }
            }

            if (infoPanel != null)
            {
                infoPanel.DOKill();
                float alpha = 1f;
                if (animate && Application.isPlaying)
                {
                    float duration = focused
                        ? profile.VisualStageFocusInDuration
                        : profile.VisualStageFocusOutDuration;
                    Track(infoPanel.DOFade(alpha, duration)
                        .SetEase(Ease.OutCubic)
                        .SetUpdate(true));
                }
                else
                {
                    infoPanel.alpha = alpha;
                }
            }
        }

        private Tween BuildNormalScaleOvershoot(Transform target, Vector3 targetScale)
        {
            float overshoot = Mathf.Max(0f, profile.NormalScaleOvershoot);
            float overshootDuration = Mathf.Max(0.01f, profile.NormalScaleOvershootDuration);
            float settleDuration = Mathf.Max(0.01f, profile.NormalScaleSettleDuration);
            if (overshoot <= 0.0001f)
                return BuildSoftScaleTween(target, targetScale, overshootDuration + settleDuration);

            Vector3 overshootScale = targetScale * (1f + overshoot);

            Sequence sequence = DOTween.Sequence()
                .SetUpdate(true);
            sequence.Append(target.DOScale(overshootScale, overshootDuration)
                .SetEase(Ease.OutSine));
            sequence.Append(target.DOScale(targetScale, settleDuration)
                .SetEase(Ease.InOutSine));
            return sequence;
        }

        private static Tween BuildSoftScaleTween(Transform target, Vector3 targetScale, float duration)
        {
            return target.DOScale(targetScale, Mathf.Max(0.01f, duration))
                .SetEase(Ease.OutSine)
                .SetUpdate(true);
        }

        public void PlayViewModeCrossfade(CanvasGroup infoPanel, System.Action onMidpoint)
        {
            if (profile == null || infoPanel == null)
            {
                onMidpoint?.Invoke();
                return;
            }

            infoPanel.DOKill();
            float half = profile.ViewPanelFadeDuration * 0.5f;
            Track(infoPanel.DOFade(0f, half)
                .SetEase(Ease.Linear)
                .SetUpdate(true)
                .OnComplete(() =>
                {
                    onMidpoint?.Invoke();
                    Track(infoPanel.DOFade(1f, half)
                        .SetEase(Ease.Linear)
                        .SetUpdate(true));
                }));
        }

        public void ApplyHpBar(
            int slotIndex,
            Image fill,
            TMP_Text label,
            ChainlingPartyColumnVitals vitals,
            bool isEmpty,
            bool animateDelta,
            bool deferEnterReveal = false)
        {
            if (profile == null || fill == null)
                return;

            float targetFill = vitals != null ? vitals.fill01 : 0f;
            bool fainted = vitals != null && vitals.isFainted;
            Color targetColor = isEmpty
                ? profile.MutedHpColor
                : fainted
                    ? profile.FaintedHpColor
                    : profile.HealthyHpColor;

            lastHpFillBySlot.TryGetValue(slotIndex, out float previousFill);
            bool fillChanged = !Mathf.Approximately(previousFill, targetFill);
            lastHpFillBySlot[slotIndex] = targetFill;

            fill.DOKill();
            if (label != null)
                label.transform.DOKill();

            if (deferEnterReveal)
            {
                PartyTabProgressBarUtility.SetFill01(fill, 0f);
                fill.color = targetColor;
                if (label != null)
                    label.text = vitals != null ? vitals.label : "—";
                return;
            }

            if (animateDelta && Application.isPlaying && fillChanged)
            {
                Track(PartyTabProgressBarUtility.DOFill01(fill, targetFill, profile.HpFillDuration)
                    .SetEase(Ease.OutSine)
                    .SetUpdate(true));
                Track(fill.DOColor(targetColor, profile.FaintedColorDuration)
                    .SetEase(Ease.OutSine)
                    .SetUpdate(true));

                if (label != null)
                {
                    Track(label.transform.DOPunchScale(
                            Vector3.one * profile.HpLabelPunchStrength,
                            profile.HpLabelPunchDuration,
                            1,
                            0.5f)
                        .SetUpdate(true));
                }
            }
            else
            {
                PartyTabProgressBarUtility.SetFill01(fill, targetFill);
                fill.color = targetColor;
            }

            if (label != null)
                label.text = vitals != null ? vitals.label : "—";
        }

        public void PlayViewRailOpen(CanvasGroup railGroup, IReadOnlyList<CanvasGroup> rowGroups, bool open)
        {
            if (profile == null || railGroup == null)
                return;

            railGroup.DOKill();
            float targetAlpha = open ? 1f : 0f;
            Track(railGroup.DOFade(targetAlpha, profile.ViewRailOpenDuration)
                .SetEase(Ease.OutCubic)
                .SetUpdate(true));

            if (rowGroups == null)
                return;

            for (int i = 0; i < rowGroups.Count; i++)
            {
                CanvasGroup row = rowGroups[i];
                if (row == null)
                    continue;

                row.alpha = open ? 0f : 1f;
                if (!open)
                    continue;

                float delay = i * profile.ViewRailRowStagger;
                Track(row.DOFade(1f, profile.ViewRailOpenDuration)
                    .SetDelay(delay)
                    .SetEase(Ease.OutCubic)
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
