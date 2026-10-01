using DG.Tweening;
using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// Shared CRT banner strip motion for encounter intro and post-battle outcome banners.
    /// Encounter intro slides in as a ~7% Y strip. Post-battle slides in as a thin sliver,
    /// then snaps open with an exaggerated overshoot. Exit collapses to the same strip
    /// while contents fade, then slides out left.
    /// </summary>
    public static class CombatBannerStripMotion
    {
        public const float DefaultStripScaleY = 0.07f;
        public const float PostBattleStripScaleY = 0.028f;

        public struct Settings
        {
            public float stripScaleY;
            public float slideInDuration;
            public Ease slideInEase;
            public float growDuration;
            public Ease growEase;
            public float overshootScale;
            public float settleDuration;
            public Ease settleEase;
            public float collapseDuration;
            public float slideOutDuration;
            public Ease slideOutEase;

            public static Settings Default => new Settings
            {
                stripScaleY = DefaultStripScaleY,
                slideInDuration = 0.22f,
                slideInEase = Ease.OutCubic,
                growDuration = 0.16f,
                growEase = Ease.OutCubic,
                overshootScale = 1.12f,
                settleDuration = 0.1f,
                settleEase = Ease.OutQuad,
                collapseDuration = 0.12f,
                slideOutDuration = 0.16f,
                slideOutEase = Ease.InCubic,
            };

            public static Settings PostBattle => new Settings
            {
                stripScaleY = PostBattleStripScaleY,
                slideInDuration = 0.14f,
                slideInEase = Ease.OutExpo,
                growDuration = 0.12f,
                growEase = Ease.OutExpo,
                overshootScale = 1.28f,
                settleDuration = 0.1f,
                settleEase = Ease.OutQuad,
                collapseDuration = 0.1f,
                slideOutDuration = 0.14f,
                slideOutEase = Ease.InCubic,
            };

            public static Settings FromIntroProfile(BattleIntroVfxProfileSO profile)
            {
                Settings settings = Default;
                if (profile == null)
                    return settings;

                settings.stripScaleY = Mathf.Clamp(profile.stripScaleY, 0.02f, 0.25f);
                settings.slideInDuration = Mathf.Max(0.04f, profile.entryDuration);
                settings.growEase = Ease.OutCubic;
                settings.overshootScale = Mathf.Max(1.01f, profile.overshootScale);
                settings.settleDuration = Mathf.Max(0.04f, profile.settleDuration);
                settings.settleEase = profile.settleEase;
                float exit = Mathf.Max(0.08f, profile.exitDuration);
                settings.collapseDuration = exit * 0.45f;
                settings.slideOutDuration = exit * 0.55f;
                settings.slideOutEase = profile.exitEase;
                return settings;
            }
        }

        public static float ResolveOffscreenTravel(RectTransform banner)
        {
            if (banner == null)
                return 960f;

            RectTransform parent = banner.parent as RectTransform;
            float parentWidth = parent != null ? Mathf.Abs(parent.rect.width) : 1920f;
            float bannerWidth = Mathf.Abs(banner.rect.width);
            return Mathf.Max(240f, parentWidth * 0.55f + bannerWidth * 0.35f);
        }

        public static Vector3 StripScale(Vector3 restScale, float stripScaleY)
        {
            return new Vector3(restScale.x, restScale.y * Mathf.Clamp(stripScaleY, 0.01f, 1f), restScale.z);
        }

        public static void ApplyEnterStart(
            RectTransform banner,
            Vector2 restPosition,
            Vector3 restScale,
            Settings settings)
        {
            if (banner == null)
                return;

            float travel = ResolveOffscreenTravel(banner);
            banner.anchoredPosition = restPosition + new Vector2(travel, 0f);
            banner.localScale = StripScale(restScale, settings.stripScaleY);
        }

        public static Sequence BuildEnter(
            RectTransform banner,
            Vector2 restPosition,
            Vector3 restScale,
            Settings settings,
            bool useUnscaledTime = true,
            CanvasGroup contents = null)
        {
            var sequence = DOTween.Sequence().SetUpdate(useUnscaledTime);
            if (banner == null)
                return sequence;

            Vector3 overshoot = restScale * Mathf.Max(1.01f, settings.overshootScale);
            ApplyEnterStart(banner, restPosition, restScale, settings);
            if (contents != null)
                contents.alpha = 0f;

            float grow = Mathf.Max(0.04f, settings.growDuration);
            sequence.Append(
                banner.DOAnchorPos(restPosition, Mathf.Max(0.04f, settings.slideInDuration))
                    .SetEase(settings.slideInEase));
            sequence.Append(
                banner.DOScale(overshoot, grow)
                    .SetEase(settings.growEase));
            if (contents != null)
                sequence.Join(contents.DOFade(1f, grow).SetEase(Ease.OutQuad));
            sequence.Append(
                banner.DOScale(restScale, Mathf.Max(0.04f, settings.settleDuration))
                    .SetEase(settings.settleEase));
            return sequence;
        }

        public static Sequence BuildExit(
            RectTransform banner,
            CanvasGroup contents,
            Vector2 restPosition,
            Vector3 restScale,
            Settings settings,
            bool useUnscaledTime = true)
        {
            var sequence = DOTween.Sequence().SetUpdate(useUnscaledTime);
            if (banner == null)
                return sequence;

            Vector3 strip = StripScale(restScale, settings.stripScaleY);
            float travel = ResolveOffscreenTravel(banner);
            Vector2 exitPos = restPosition + new Vector2(-travel, 0f);
            float collapse = Mathf.Max(0.04f, settings.collapseDuration);

            sequence.Append(banner.DOScale(strip, collapse).SetEase(Ease.InCubic));
            if (contents != null)
                sequence.Join(contents.DOFade(0f, collapse).SetEase(Ease.Linear));

            sequence.Append(
                banner.DOAnchorPos(exitPos, Mathf.Max(0.04f, settings.slideOutDuration))
                    .SetEase(settings.slideOutEase));
            return sequence;
        }
    }
}
