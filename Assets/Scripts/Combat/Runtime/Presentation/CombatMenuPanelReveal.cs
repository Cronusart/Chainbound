using DG.Tweening;
using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// Player-menu-style CRT panel open/close: scanline plate ScaleY, then content overshoot.
    /// Combat.Runtime copy of the boot-sequence motion without MenuNavigation or boot flipbooks.
    /// </summary>
    public static class CombatMenuPanelReveal
    {
        public struct Settings
        {
            public float backgroundRevealDuration;
            public Ease backgroundRevealEase;
            public float contentStartScale;
            public float overshootScale;
            public float overshootDuration;
            public float settleDuration;
            public Ease overshootEase;
            public Ease settleEase;
            public float contentFadeDuration;
            public float closeScaleDownDuration;
            public float closeFadeDuration;

            public static Settings Default => new Settings
            {
                backgroundRevealDuration = 0.16f,
                backgroundRevealEase = Ease.OutSine,
                contentStartScale = 0.92f,
                overshootScale = 1.05f,
                overshootDuration = 0.14f,
                settleDuration = 0.2f,
                overshootEase = Ease.OutSine,
                settleEase = Ease.InOutSine,
                contentFadeDuration = 0.1f,
                closeScaleDownDuration = 0.18f,
                closeFadeDuration = 0.12f,
            };
        }

        public static void ApplyClosedState(
            RectTransform background,
            Vector3 backgroundRestScale,
            RectTransform panel,
            Vector3 panelRestScale,
            CanvasGroup group,
            Settings settings)
        {
            if (background != null && background == panel)
            {
                background.localScale = new Vector3(backgroundRestScale.x, 0.01f, backgroundRestScale.z);
            }
            else
            {
                if (background != null)
                    background.localScale = new Vector3(backgroundRestScale.x, 0.01f, backgroundRestScale.z);

                if (panel != null)
                    panel.localScale = panelRestScale * Mathf.Clamp(settings.contentStartScale, 0.01f, 1f);
            }

            if (group != null)
            {
                group.alpha = 0f;
                group.interactable = false;
                group.blocksRaycasts = true;
            }
        }

        public static Sequence BuildOpen(
            RectTransform background,
            Vector3 backgroundRestScale,
            RectTransform panel,
            Vector3 panelRestScale,
            CanvasGroup group,
            Settings settings,
            bool useUnscaledTime = true)
        {
            var sequence = DOTween.Sequence().SetUpdate(useUnscaledTime);
            ApplyClosedState(background, backgroundRestScale, panel, panelRestScale, group, settings);

            if (background != null)
            {
                sequence.Append(
                    background.DOScaleY(backgroundRestScale.y, Mathf.Max(0.04f, settings.backgroundRevealDuration))
                        .SetEase(settings.backgroundRevealEase));
            }

            if (panel != null)
            {
                Vector3 overshoot = panelRestScale * Mathf.Max(1.01f, settings.overshootScale);
                sequence.Append(
                    panel.DOScale(overshoot, Mathf.Max(0.04f, settings.overshootDuration))
                        .SetEase(settings.overshootEase));
                if (group != null)
                    sequence.Join(group.DOFade(1f, Mathf.Max(0.04f, settings.contentFadeDuration)).SetEase(Ease.Linear));
                sequence.Append(
                    panel.DOScale(panelRestScale, Mathf.Max(0.04f, settings.settleDuration))
                        .SetEase(settings.settleEase));
            }
            else if (group != null)
            {
                sequence.Append(group.DOFade(1f, Mathf.Max(0.04f, settings.contentFadeDuration)).SetEase(Ease.Linear));
            }

            if (group != null)
            {
                sequence.OnComplete(() =>
                {
                    group.interactable = true;
                    group.blocksRaycasts = true;
                    group.alpha = 1f;
                });
            }

            return sequence;
        }

        public static Sequence BuildClose(
            RectTransform background,
            Vector3 backgroundRestScale,
            RectTransform panel,
            Vector3 panelRestScale,
            CanvasGroup group,
            Settings settings,
            bool useUnscaledTime = true)
        {
            var sequence = DOTween.Sequence().SetUpdate(useUnscaledTime);
            float scaleDown = Mathf.Max(0.04f, settings.closeScaleDownDuration);
            bool sameTarget = background != null && background == panel;

            if (sameTarget)
            {
                Vector3 overshoot = panelRestScale * Mathf.Max(1.01f, settings.overshootScale);
                sequence.Append(panel.DOScale(overshoot, 0.08f).SetEase(Ease.OutSine));
                sequence.Append(
                    background.DOScale(new Vector3(backgroundRestScale.x, 0.01f, backgroundRestScale.z), scaleDown)
                        .SetEase(Ease.InSine));
                if (group != null)
                    sequence.Join(group.DOFade(0f, Mathf.Max(0.04f, settings.closeFadeDuration)).SetEase(Ease.Linear));
                return sequence;
            }

            if (panel != null)
            {
                Vector3 overshoot = panelRestScale * Mathf.Max(1.01f, settings.overshootScale);
                sequence.Append(panel.DOScale(overshoot, 0.08f).SetEase(Ease.OutSine));
                sequence.Append(panel.DOScale(panelRestScale * settings.contentStartScale, scaleDown).SetEase(Ease.InSine));
            }

            if (group != null)
                sequence.Join(group.DOFade(0f, Mathf.Max(0.04f, settings.closeFadeDuration)).SetEase(Ease.Linear));

            if (background != null)
            {
                sequence.Join(
                    background.DOScaleY(0.01f, scaleDown)
                        .SetEase(Ease.InSine));
            }

            return sequence;
        }
    }
}
