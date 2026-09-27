#ifndef GRASS_BEND_INTERACTION_INCLUDED
#define GRASS_BEND_INTERACTION_INCLUDED

// Shared idle-sway + interaction displacement for Environment/GrassBend.
// Include after Core.hlsl and after the interaction uniforms are declared.

void ApplyGrassBendDisplacement(inout float3 positionOS, inout float3 worldPos)
{
    float bladeHeight = max(_BladeHeight, 0.001);
    float heightNorm = saturate((worldPos.y - _BladeBaseY) / bladeHeight);
    float heightMask = pow(heightNorm, max(_HeightMaskPower, 0.001));

    float swayTime = _Time.y * _SwaySpeed;
    float swayOffset = sin(swayTime + worldPos.x * 0.5 + worldPos.z * 0.5) * _SwayStrength;
    positionOS.x += swayOffset * heightMask;
    worldPos = TransformObjectToWorld(positionOS);

    float totalInteraction = _PushStrength + _DownStrength + _BendStrength;
    if (totalInteraction > 0.001)
    {
        float2 sampleXZ = _UsePivotFalloff > 0.5
            ? float2(_BladePivotWS.x, _BladePivotWS.z)
            : float2(worldPos.x, worldPos.z);
        float2 toSampleXZ = sampleXZ - float2(_InteractorPosWS.x, _InteractorPosWS.z);
        float distXZ = length(toSampleXZ);
        float falloff = saturate(1.0 - distXZ / max(_InteractRadius, 0.001));
        float effectMult = falloff * heightMask;

        if (_PushStrength > 0.001 && distXZ > 0.001)
        {
            float2 pushDirXZ = toSampleXZ / distXZ;
            float2 pushOffset = pushDirXZ * _PushStrength * effectMult;
            worldPos.x += pushOffset.x;
            worldPos.z += pushOffset.y;
        }

        if (_BendStrength > 0.001)
        {
            float2 velXZ = float2(_InteractorVelWS.x, _InteractorVelWS.z);
            float velMag = length(velXZ);
            if (velMag > 0.1)
            {
                float2 velDir = velXZ / velMag;
                float speedFactor = saturate(velMag / 5.0);
                float2 bendOffset = velDir * _BendStrength * speedFactor * effectMult;
                worldPos.x += bendOffset.x;
                worldPos.z += bendOffset.y;
            }
        }

        if (_DownStrength > 0.001)
        {
            float downOffset = min(_DownStrength * effectMult, _MaxDownOffset);
            worldPos.y -= downOffset;
            worldPos.y = max(worldPos.y, _BladeBaseY);
        }
    }

    float legacyBendActive = _BendStrength > 0.001 && length(_BendCenter) > 0.001 && _PushStrength < 0.001;
    if (legacyBendActive)
    {
        float2 bendCenterXZ = float2(_BendCenter.x, _BendCenter.z);
        float2 worldPosXZ = float2(worldPos.x, worldPos.z);
        float2 toVertex = worldPosXZ - bendCenterXZ;
        float distFromCenter = length(toVertex);
        float bendRadius = 1.5;
        float distanceFalloff = saturate(1.0 - (distFromCenter / bendRadius));
        float bendDirLen = length(_BendDir);
        float2 bendDirWorld = bendDirLen > 0.001 ? _BendDir / bendDirLen : float2(0, 0);
        float2 bendOffset = bendDirWorld * _BendStrength * distanceFalloff * heightMask * 0.8;
        worldPos.xz += bendOffset;
    }

    positionOS = TransformWorldToObject(float4(worldPos, 1.0)).xyz;
}

#endif
