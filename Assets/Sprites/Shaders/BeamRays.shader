Shader "SignalJam/Sprites/BeamRays"
{
    Properties
    {
        [Header(Beam)]
        _BeamColor ("Beam Color", Color) = (1, 1, 1, 1)
        _Intensity ("Intensity", Range(0, 10)) = 1

        [Header(Edge)]
        _EdgeFeather ("Edge Feather", Range(0.001, 0.5)) = 0.05

        [Header(Distance)]
        _DistanceFade ("Distance Fade Power", Range(0.1, 5)) = 1.5

        [Header(Noise Striation)]
        _NoiseScale ("Noise Scale (X=U stretch, Y=V stretch)", Vector) = (0.5, 20, 0, 0)
        _NoiseSpeed ("Noise Speed", Float) = 0.5
        _NoiseStrength ("Noise Strength", Range(0, 1)) = 0.6

        [Header(Depth)]
        _DepthFadeDistance ("Depth Fade Distance", Range(0.01, 10)) = 0.5
    }
    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }
        LOD 100
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "BeamRays"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BeamColor;
                float _Intensity;
                float _EdgeFeather;
                float _DistanceFade;
                float4 _NoiseScale;
                float _NoiseSpeed;
                float _NoiseStrength;
                float _DepthFadeDistance;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float4 screenPos : TEXCOORD2;
                float eyeDepth : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            // Converts raw depth buffer value to linear eye depth (avoids CommonDepth.hlsl dependency)
            float LinearEyeDepthLocal(float rawDepth, float4 zBufferParam)
            {
                return 1.0 / (zBufferParam.z * rawDepth + zBufferParam.w);
            }

            // Simple value noise (approximates Shader Graph Simple Noise)
            float hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }
            float noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);

                float a = hash(i);
                float b = hash(i + float2(1, 0));
                float c = hash(i + float2(0, 1));
                float d = hash(i + float2(1, 1));

                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.screenPos = ComputeScreenPos(output.positionCS);
                output.eyeDepth = -TransformWorldToView(output.positionWS).z;

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float U = input.uv.x;
                float V = input.uv.y;

                // === EDGE MASK ===
                float edgeL = smoothstep(0, _EdgeFeather, U);
                float edgeR = smoothstep(0, _EdgeFeather, 1.0 - U);
                float edgeMask = edgeL * edgeR;

                // === DISTANCE MASK ===
                float distMask = pow(1.0 - V, _DistanceFade);

                // === NOISE STRIATION ===
                float2 noiseUV = input.positionWS.xz * _NoiseScale.xy;
                noiseUV += float2(_Time.y * _NoiseSpeed, _Time.y * _NoiseSpeed);
                float noise01 = noise(noiseUV);
                float bands = lerp(1.0, noise01, _NoiseStrength);

                // === DEPTH FADE ===
                float2 screenUV = input.screenPos.xy / input.screenPos.w;
                float rawSceneDepth = SampleSceneDepth(screenUV);
                float sceneDepthLinear = LinearEyeDepthLocal(rawSceneDepth, _ZBufferParams);
                float fragDepth = input.eyeDepth;
                float depthDiff = sceneDepthLinear - fragDepth;
                float depthFade = saturate(depthDiff / _DepthFadeDistance);

                // === COMBINE ===
                float beamMask = edgeMask * distMask * bands * depthFade;

                // === OUTPUT ===
                half3 baseColor = _BeamColor.rgb * _Intensity * beamMask;
                half alpha = beamMask * _BeamColor.a;

                return half4(baseColor, alpha);
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Unlit"
}
