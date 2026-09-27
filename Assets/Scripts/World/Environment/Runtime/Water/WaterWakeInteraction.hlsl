#ifndef SIGNAL_JAM_WATER_WAKE_INTERACTION_INCLUDED
#define SIGNAL_JAM_WATER_WAKE_INTERACTION_INCLUDED

// Intentionally outside UnityPerMaterial: WaterStreamInteractor writes these per renderer via MPB.
float4 _InteractorPosWS;
float4 _InteractorVelWS;
float _InteractRadius;
float _WakeBowlStrength;
float _WakeRingStrength;
float _WakeRingWidth;
float _WakeMaxDown;
float _WakeStretch;
float _WakeFoamStrength;
float _WakeDownstreamBias;

float2 WaterWakeSafeDirection(float2 value, float2 fallback)
{
    float lengthSquared = dot(value, value);
    if (lengthSquared > 0.0001)
        return value * rsqrt(lengthSquared);

    float fallbackLengthSquared = dot(fallback, fallback);
    return fallbackLengthSquared > 0.0001
        ? fallback * rsqrt(fallbackLengthSquared)
        : float2(1.0, 0.0);
}

// Returns (bowl mask, ring mask) and applies only a small world-Y displacement.
float2 ApplyWaterWakeInteraction(inout float3 positionWS, float2 waveDirection)
{
    float radius = max(_InteractRadius, 0.0);
    if (radius <= 0.0001)
        return float2(0.0, 0.0);

    float2 velocity = _InteractorVelWS.xz;
    float speed = length(velocity);
    float movementMask = saturate(speed * 2.0);
    float2 currentDirection = WaterWakeSafeDirection(waveDirection, float2(1.0, 0.0));
    float2 travelDirection = WaterWakeSafeDirection(velocity, currentDirection);
    float2 downstreamDirection = WaterWakeSafeDirection(currentDirection + travelDirection * 0.35, currentDirection);

    float velocityLead = min(speed * 0.025, radius * 0.2);
    float2 center = _InteractorPosWS.xz +
                    currentDirection * (radius * _WakeDownstreamBias) +
                    downstreamDirection * velocityLead;

    float2 delta = positionWS.xz - center;
    float parallelDistance = dot(delta, travelDirection);
    float perpendicularDistance = dot(delta, float2(-travelDirection.y, travelDirection.x));
    float stretch = lerp(1.0, max(1.0, _WakeStretch), movementMask);
    float ellipticalDistance = length(float2(parallelDistance / stretch, perpendicularDistance));

    float inner = saturate(1.0 - ellipticalDistance / radius);
    float bowlMask = inner * inner;

    float ringWidth = max(_WakeRingWidth, 0.001);
    float ringInner = smoothstep(radius * 0.72, radius, ellipticalDistance);
    float ringOuter = 1.0 - smoothstep(radius, radius + ringWidth, ellipticalDistance);
    float ringMask = saturate(ringInner * ringOuter) * movementMask;

    float downwardOffset = min(bowlMask * max(_WakeBowlStrength, 0.0), max(_WakeMaxDown, 0.0));
    float upwardOffset = ringMask * max(_WakeRingStrength, 0.0);
    positionWS.y += upwardOffset - downwardOffset;

    return float2(bowlMask, ringMask);
}

#endif
