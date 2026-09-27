using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// Shared travel timing calculations for live combat and the Animation
    /// Previewer. Duration is resolved once from the spawn-time endpoints;
    /// following anchors may continue to move the path after that snapshot.
    /// </summary>
    public static class CombatVfxTravelTimingResolver
    {
        public const float MinimumDuration = 0.01f;

        private const int PathLengthSamples = 32;

        public static float ResolveDuration(
            CombatVFXLibrary.VFXPhaseConfig config,
            Vector3 startPosition,
            Vector3 endPosition)
        {
            if (config == null || config.travel == null || !config.travel.enabled)
                return 0f;

            config.travel.EnsureDefaults();

            switch (config.travel.durationSource)
            {
                case CombatVFXLibrary.VFXTravelDurationSource.ExplicitSeconds:
                    return Mathf.Max(MinimumDuration, config.travel.explicitDurationSeconds);

                case CombatVFXLibrary.VFXTravelDurationSource.EffectLifetime:
                    return ResolveAuthoredEffectLifetime(config.vfxPrefab);

                case CombatVFXLibrary.VFXTravelDurationSource.ConstantSpeed:
                    float pathLength = EstimatePathLength(
                        startPosition,
                        endPosition,
                        config.travel.travelProgressCurve,
                        config.travel.worldYArc);
                    return Mathf.Max(
                        MinimumDuration,
                        pathLength / Mathf.Max(MinimumDuration, config.travel.speedUnitsPerSecond));

                case CombatVFXLibrary.VFXTravelDurationSource.SpriteFrameSpan:
                default:
                    float frameSpan = ResolveSpriteFrameSpan(config.vfxPrefab);
                    return frameSpan > 0f
                        ? frameSpan
                        : ResolveAuthoredEffectLifetime(config.vfxPrefab);
            }
        }

        public static float ResolveSpriteFrameSpan(GameObject prefab)
        {
            CombatMoveVFX moveVfx = prefab != null ? prefab.GetComponent<CombatMoveVFX>() : null;
            return moveVfx != null ? moveVfx.GetSpriteFrameSpanDuration() : 0f;
        }

        public static float ResolveAuthoredEffectLifetime(GameObject prefab)
        {
            CombatMoveVFX moveVfx = prefab != null ? prefab.GetComponent<CombatMoveVFX>() : null;
            return moveVfx != null
                ? moveVfx.GetAuthoredEffectLifetimeDuration()
                : MinimumDuration;
        }

        public static float EstimatePathLength(
            Vector3 startPosition,
            Vector3 endPosition,
            AnimationCurve travelProgressCurve,
            float worldYArc)
        {
            Vector3 previous = startPosition;
            float length = 0f;

            for (int i = 1; i <= PathLengthSamples; i++)
            {
                float normalizedTime = i / (float)PathLengthSamples;
                Vector3 current = EvaluateTravelPosition(
                    startPosition,
                    endPosition,
                    travelProgressCurve,
                    worldYArc,
                    normalizedTime);
                length += Vector3.Distance(previous, current);
                previous = current;
            }

            return length;
        }

        public static Vector3 EvaluateTravelPosition(
            Vector3 startPosition,
            Vector3 endPosition,
            AnimationCurve travelProgressCurve,
            float worldYArc,
            float normalizedTime)
        {
            float clampedTime = Mathf.Clamp01(normalizedTime);
            if (clampedTime <= 0f)
                return startPosition;
            if (clampedTime >= 1f)
                return endPosition;

            float progress = travelProgressCurve != null && travelProgressCurve.length > 0
                ? Mathf.Clamp01(travelProgressCurve.Evaluate(clampedTime))
                : clampedTime;

            Vector3 position = Vector3.Lerp(startPosition, endPosition, progress);
            position.y += 4f * worldYArc * progress * (1f - progress);
            return position;
        }
    }
}
