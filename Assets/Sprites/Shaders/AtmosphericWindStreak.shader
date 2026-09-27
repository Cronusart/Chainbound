Shader "SignalJam/Sprites/AtmosphericWindStreak"
{
    Properties
    {
        [MainTexture] _MainTex ("Streak", 2D) = "white" {}
        [HDR] _ParticleColor ("Particle Color", Color) = (1, 1, 1, 1)
        [Range(0, 1)] _BaseAlpha ("Base Alpha", Float) = 1
        _DepthFadeDist ("Depth Fade Distance", Float) = 0.45
        _WaveAmplitude ("Wave Amplitude", Range(0, 0.25)) = 0.055
        _WaveFrequency ("Wave Frequency", Range(0.25, 6)) = 1.35
        _WaveSpeed ("Wave Speed", Range(-8, 8)) = 2.2
        _FlowFrequency ("Flow Frequency", Range(0.25, 8)) = 2
        _FlowSpeed ("Flow Speed", Range(-8, 8)) = 2.8
        _FlowStrength ("Flow Strength", Range(0, 1)) = 0.35
        _PulseMinimum ("Pulse Minimum", Range(0, 1)) = 0.55
        _PulseSpeed ("Pulse Speed", Range(0, 8)) = 1.1
        [HideInInspector] _TrailPhase ("Trail Phase", Float) = 0
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderPipeline" = "UniversalPipeline"
        }
        LOD 100
        Blend [_SrcBlend] [_DstBlend]
        ZWrite Off
        ZTest LEqual
        Cull Off

        Pass
        {
            Name "AtmosphericWindStreak"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _ParticleColor;
                float _BaseAlpha;
                float _DepthFadeDist;
                float _WaveAmplitude;
                float _WaveFrequency;
                float _WaveSpeed;
                float _FlowFrequency;
                float _FlowSpeed;
                float _FlowStrength;
                float _PulseMinimum;
                float _PulseSpeed;
                float _TrailPhase;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                float4 custom1 : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
                float eyeDepth : TEXCOORD2;
                float4 vertexColor : TEXCOORD3;
                float phase : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float phase = input.custom1.x * TWO_PI + _TrailPhase;
                float envelope = sin(saturate(input.uv.x) * PI);
                float wave = sin(input.uv.x * _WaveFrequency * TWO_PI + _Time.y * _WaveSpeed + phase);
                float3 displaced = input.positionOS.xyz;
                displaced.y += wave * _WaveAmplitude * envelope;

                float3 positionWS = TransformObjectToWorld(displaced);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.screenPos = ComputeScreenPos(output.positionCS);
                output.eyeDepth = -TransformWorldToView(positionWS).z;
                output.vertexColor = input.color;
                output.phase = phase;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                half4 texel = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                float2 screenUV = input.screenPos.xy / max(input.screenPos.w, 1e-5);
                float rawSceneDepth = SampleSceneDepth(screenUV);
                float sceneDepthLinear = LinearEyeDepth(rawSceneDepth, _ZBufferParams);
                float depthFade = saturate((sceneDepthLinear - input.eyeDepth) / max(_DepthFadeDist, 1e-4));

                float travelingWave = sin(
                    input.uv.x * _FlowFrequency * TWO_PI -
                    _Time.y * _FlowSpeed + input.phase);
                float flow = lerp(1.0, travelingWave * 0.5 + 0.5, _FlowStrength);
                float pulse01 = sin(_Time.y * _PulseSpeed + input.phase) * 0.5 + 0.5;
                float pulse = lerp(_PulseMinimum, 1.0, pulse01);

                half3 color = texel.rgb * _ParticleColor.rgb * input.vertexColor.rgb;
                half alpha = texel.a * _ParticleColor.a * input.vertexColor.a *
                    _BaseAlpha * depthFade * flow * pulse;
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
