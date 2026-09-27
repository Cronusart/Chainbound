Shader "SignalJam/Sprites/DustMote_Masked"
{
    Properties
    {
        [Header(Particle)]
        [HDR] _ParticleColor ("Particle Color", Color) = (1, 1, 1, 1)
        [Range(0, 1)] _BaseAlpha ("Base Alpha", Float) = 0.6

        // Globals below are set by BeamMaskProvider via Shader.SetGlobal* — not editable in Inspector
        [HideInInspector] _BeamOrigin ("", Vector) = (0, 0, 0, 0)
        [HideInInspector] _BeamForward ("", Vector) = (0, 0, 1, 0)
        [HideInInspector] _BeamRight ("", Vector) = (1, 0, 0, 0)
        [HideInInspector] _BeamUp ("", Vector) = (0, 1, 0, 0)
        [HideInInspector] _BeamHalfWidth ("", Float) = 1
        [HideInInspector] _BeamHalfHeight ("", Float) = 1
        [HideInInspector] _BeamLength ("", Float) = 4
        [HideInInspector] _DustEdgeFeather ("", Float) = 0.25
        [HideInInspector] _DustDistanceFade ("", Float) = 2
        [HideInInspector] _DustNoiseScale ("", Vector) = (0.3, 1.2, 0, 0)
        [HideInInspector] _DustNoiseSpeed ("", Float) = 0.08
        [HideInInspector] _DustNoiseStrength ("", Float) = 0.35
        [HideInInspector] _DustDepthFadeDist ("", Float) = 0.4
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
            Name "DustMoteMasked"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            // Material properties (exposed in Inspector)
            CBUFFER_START(UnityPerMaterial)
                float4 _ParticleColor;
                float _BaseAlpha;
            CBUFFER_END

            // Global properties (set by BeamMaskProvider via Shader.SetGlobal*)
            float4 _BeamOrigin;
            float4 _BeamForward;
            float4 _BeamRight;
            float4 _BeamUp;
            float _BeamHalfWidth;
            float _BeamHalfHeight;
            float _BeamLength;
            float _DustEdgeFeather;
            float _DustDistanceFade;
            float4 _DustNoiseScale;
            float _DustNoiseSpeed;
            float _DustNoiseStrength;
            float _DustDepthFadeDist;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
                float eyeDepth : TEXCOORD2;
                float4 vertexColor : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float LinearEyeDepthLocal(float rawDepth, float4 zBufferParam)
            {
                return 1.0 / (zBufferParam.z * rawDepth + zBufferParam.w);
            }

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

                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.screenPos = ComputeScreenPos(output.positionCS);
                output.eyeDepth = -TransformWorldToView(output.positionWS).z;
                output.vertexColor = input.color;

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float beamLen = max(_BeamLength, 0.001);
                float halfW = max(_BeamHalfWidth, 0.001);

                // === Project into beam-local space ===
                float3 toFrag = input.positionWS - _BeamOrigin.xyz;
                float localX = dot(toFrag, _BeamRight.xyz);
                float localY = dot(toFrag, _BeamUp.xyz);
                float localZ = dot(toFrag, _BeamForward.xyz);

                // === Beam UV equivalent ===
                float u = localX / halfW * 0.5 + 0.5;
                float v = localZ / beamLen;
                float uClamped = saturate(u);
                float vClamped = saturate(v);

                // === Same masks as beam shader ===
                float edgeL = smoothstep(0, _DustEdgeFeather, uClamped);
                float edgeR = smoothstep(0, _DustEdgeFeather, 1.0 - uClamped);
                float edgeMask = edgeL * edgeR;

                float distMask = pow(1.0 - vClamped, _DustDistanceFade);

                // === Noise (same logic) ===
                float2 noiseCoord = input.positionWS.xz * _DustNoiseScale.xy + _Time.y * _DustNoiseSpeed;
                float noiseVal = noise(noiseCoord);
                float bands = lerp(1.0, noiseVal, _DustNoiseStrength);

                // === Depth fade ===
                float2 screenUV = input.screenPos.xy / input.screenPos.w;
                float rawSceneDepth = SampleSceneDepth(screenUV);
                float sceneDepthLinear = LinearEyeDepthLocal(rawSceneDepth, _ZBufferParams);
                float depthDiff = sceneDepthLinear - input.eyeDepth;
                float depthFade = saturate(depthDiff / _DustDepthFadeDist);

                // === Particle vertex color alpha (from Color Over Lifetime) ===
                float vertexAlpha = input.vertexColor.a;

                // === Combine ===
                float beamMask = edgeMask * distMask * bands * depthFade;
                float finalAlpha = _BaseAlpha * vertexAlpha * beamMask;

                half3 baseColor = _ParticleColor.rgb;
                return half4(baseColor, finalAlpha);
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Unlit"
}
