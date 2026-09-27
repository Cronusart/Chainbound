using System.Collections;
using UnityEngine;

namespace SignalJam.UI.VisualFx
{
    /// <summary>
    /// Emitter-agnostic playback for two UI particle streams with opposite horizontal travel.
    /// Optionally slides emitter RectTransforms along the layout card width (corner-to-corner).
    /// </summary>
    public static class UiDualSweepParticleBurst
    {
        /// <param name="sweepLayoutCard">Intro card (or banner root) used for horizontal span; may be null to disable path mode unless preset disables animation.</param>
        /// <param name="sweepDurationSeconds">Scatter / emitter-travel duration (typically preset sweep duration or a profile override).</param>
        /// <param name="burstLifetimeScale">Caller-supplied multiplier for lifetime (1.0 = neutral).</param>
        /// <param name="burstSpeedScale">Caller-supplied multiplier for start speed (1.0 = neutral).</param>
        public static IEnumerator CoPlay(
            ParticleSystem topStreamSweep,
            ParticleSystem bottomStreamSweep,
            UiSweepParticlePresetSO preset,
            Color contextualTint,
            int totalParticleBudget,
            bool useUnscaledTime,
            RectTransform sweepLayoutCard,
            float sweepDurationSeconds,
            float burstLifetimeScale = 1f,
            float burstSpeedScale = 1f)
        {
            if (preset == null || totalParticleBudget <= 0)
                yield break;

            int topBudget = Mathf.CeilToInt(totalParticleBudget * 0.5f);
            int bottomBudget = Mathf.FloorToInt(totalParticleBudget * 0.5f);

            if (topBudget > 0 && topStreamSweep != null)
                Prepare(topStreamSweep, preset, contextualTint, topBudget, burstLifetimeScale, burstSpeedScale);

            if (bottomBudget > 0 && bottomStreamSweep != null)
                Prepare(bottomStreamSweep, preset, contextualTint, bottomBudget, burstLifetimeScale, burstSpeedScale);

            RectTransform topRt = topStreamSweep != null ? topStreamSweep.transform as RectTransform : null;
            RectTransform bottomRt = bottomStreamSweep != null ? bottomStreamSweep.transform as RectTransform : null;

            bool pathMode = preset.AnimateEmittersAlongCardWidth && sweepLayoutCard != null;
            float spanPixels = 0f;
            Vector2 topStart = default;
            Vector2 topEnd = default;
            Vector2 bottomStart = default;
            Vector2 bottomEnd = default;

            if (pathMode)
            {
                float w = Mathf.Max(1f, sweepLayoutCard.rect.width);
                float inset = Mathf.Clamp(preset.HorizontalInsetPixels, 0f, w * 0.49f);
                spanPixels = Mathf.Max(1f, w - 2f * inset);

                if (topRt != null && topBudget > 0)
                {
                    topStart = topRt.anchoredPosition;
                    topEnd = topStart + Vector2.right * spanPixels;
                }

                if (bottomRt != null && bottomBudget > 0)
                {
                    bottomStart = bottomRt.anchoredPosition;
                    bottomEnd = bottomStart + Vector2.left * spanPixels;
                }
            }

            bool topUsePath = pathMode && topRt != null && topBudget > 0;
            bool bottomUsePath = pathMode && bottomRt != null && bottomBudget > 0;

            if (preset.BottomStreamDelaySeconds > 0f && bottomBudget > 0 && bottomStreamSweep != null)
                yield return WaitSeconds(preset.BottomStreamDelaySeconds, useUnscaledTime);

            float scatterDuration = Mathf.Max(0.02f, sweepDurationSeconds);

            IEnumerator topRoutine = topUsePath
                ? ScatterEmitOverDurationWithPath(topStreamSweep, topRt, topStart, topEnd, preset, topBudget, useUnscaledTime, scatterDuration)
                : ScatterEmitOverDuration(topStreamSweep, preset, topBudget, useUnscaledTime, scatterDuration);

            IEnumerator bottomRoutine = bottomUsePath
                ? ScatterEmitOverDurationWithPath(bottomStreamSweep, bottomRt, bottomStart, bottomEnd, preset, bottomBudget, useUnscaledTime, scatterDuration)
                : ScatterEmitOverDuration(bottomStreamSweep, preset, bottomBudget, useUnscaledTime, scatterDuration);

            if (topRoutine != null && bottomRoutine != null)
            {
                IEnumerator parallel = RunParallel(topRoutine, bottomRoutine);
                while (parallel.MoveNext())
                    yield return parallel.Current;
            }
            else if (topRoutine != null)
            {
                while (topRoutine.MoveNext())
                    yield return topRoutine.Current;
            }
            else if (bottomRoutine != null)
            {
                while (bottomRoutine.MoveNext())
                    yield return bottomRoutine.Current;
            }
        }

        private static IEnumerator RunParallel(IEnumerator a, IEnumerator b)
        {
            bool na = MoveNextSafely(ref a);
            bool nb = MoveNextSafely(ref b);

            while (na || nb)
            {
                yield return null;

                if (na && a != null)
                    na = MoveNextSafely(ref a);

                if (nb && b != null)
                    nb = MoveNextSafely(ref b);
            }
        }

        private static bool MoveNextSafely(ref IEnumerator e)
        {
            if (e == null)
                return false;

            try
            {
                return e.MoveNext();
            }
            catch (MissingReferenceException)
            {
                e = null;
                return false;
            }
        }

        private static IEnumerator ScatterEmitOverDuration(
            ParticleSystem system,
            UiSweepParticlePresetSO preset,
            int budget,
            bool unscaled,
            float sweepDurationSeconds)
        {
            if (system == null || budget <= 0)
                yield break;

            float duration = Mathf.Max(0.02f, sweepDurationSeconds);
            float elapsed = 0f;

            for (int emitted = 0; emitted < budget;)
            {
                elapsed += Delta(unscaled);
                float linearT = Mathf.Clamp01(elapsed / duration);
                float easedT = preset.EvaluateSweepProgress(linearT);

                float ideal = easedT * budget;
                while (emitted < ideal && emitted < budget)
                {
                    system.Emit(1);
                    emitted++;
                }

                if (elapsed >= duration)
                {
                    while (emitted < budget)
                    {
                        system.Emit(1);
                        emitted++;
                    }
                    yield break;
                }

                yield return null;
            }
        }

        private static IEnumerator ScatterEmitOverDurationWithPath(
            ParticleSystem system,
            RectTransform emitterRect,
            Vector2 anchoredStart,
            Vector2 anchoredEnd,
            UiSweepParticlePresetSO preset,
            int budget,
            bool unscaled,
            float sweepDurationSeconds)
        {
            if (system == null || emitterRect == null || budget <= 0)
                yield break;

            float duration = Mathf.Max(0.02f, sweepDurationSeconds);
            float elapsed = 0f;
            emitterRect.anchoredPosition = anchoredStart;

            for (int emitted = 0; emitted < budget;)
            {
                elapsed += Delta(unscaled);
                float linearT = Mathf.Clamp01(elapsed / duration);
                float easedT = preset.EvaluateSweepProgress(linearT);

                emitterRect.anchoredPosition = Vector2.LerpUnclamped(anchoredStart, anchoredEnd, easedT);

                float ideal = easedT * budget;
                while (emitted < ideal && emitted < budget)
                {
                    system.Emit(1);
                    emitted++;
                }

                if (elapsed >= duration)
                {
                    emitterRect.anchoredPosition = anchoredEnd;
                    while (emitted < budget)
                    {
                        system.Emit(1);
                        emitted++;
                    }
                    yield break;
                }

                yield return null;
            }
        }

        private static IEnumerator WaitSeconds(float seconds, bool unscaled)
        {
            float t = Mathf.Max(0f, seconds);
            while (t > 0f)
            {
                t -= Delta(unscaled);
                yield return null;
            }
        }

        private static float Delta(bool unscaled) => unscaled ? Time.unscaledDeltaTime : Time.deltaTime;

        private static void Prepare(ParticleSystem ps, UiSweepParticlePresetSO preset, Color contextualTint, int particleBudget, float burstLifetimeScale, float burstSpeedScale)
        {
            ps.gameObject.SetActive(true);

            contextualTint *= preset.TintMultiplier;
            contextualTint.a *= preset.AlphaMultiplier;

            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.Clear(true);

            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;

            burstLifetimeScale = Mathf.Max(0.01f, burstLifetimeScale);
            burstSpeedScale = Mathf.Max(0.01f, burstSpeedScale);

            if (preset.PreserveAuthoringModules)
            {
                main.maxParticles = Mathf.Max(main.maxParticles, particleBudget + 16);
            }
            else
            {
                float life = Mathf.Max(preset.ProceduralLifetimeSeconds * burstLifetimeScale, 0.05f);
                float speed = Mathf.Max(preset.ProceduralSpeed * burstSpeedScale, 0.01f);
                main.duration = life;
                main.startLifetime = life;
                main.startSpeed = speed;
                main.startSize = preset.ProceduralStartSize;
                main.maxParticles = Mathf.Max(main.maxParticles, particleBudget + 16);

                var emission = ps.emission;
                emission.enabled = false;
            }

            main.startColor = BuildTintedGradient(main.startColor, contextualTint);
        }

        private static ParticleSystem.MinMaxGradient BuildTintedGradient(ParticleSystem.MinMaxGradient authoring, Color multiply)
        {
            switch (authoring.mode)
            {
                case ParticleSystemGradientMode.Color:
                    return new ParticleSystem.MinMaxGradient(MultiplyRgb(authoring.color, multiply));
                case ParticleSystemGradientMode.TwoColors:
                    return new ParticleSystem.MinMaxGradient(
                        MultiplyRgb(authoring.colorMin, multiply),
                        MultiplyRgb(authoring.colorMax, multiply));
                case ParticleSystemGradientMode.Gradient:
                case ParticleSystemGradientMode.TwoGradients:
                default:
                    return new ParticleSystem.MinMaxGradient(multiply);
            }
        }

        private static Color MultiplyRgb(Color a, Color b) =>
            new Color(a.r * b.r, a.g * b.g, a.b * b.b, a.a * b.a);
    }
}
