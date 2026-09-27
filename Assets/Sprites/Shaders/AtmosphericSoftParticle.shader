Shader "SignalJam/Sprites/AtmosphericSoftParticle"
{
    Properties
    {
        [MainTexture] _MainTex ("Sprite", 2D) = "white" {}
        [HDR] _ParticleColor ("Particle Color", Color) = (1, 1, 1, 1)
        [Range(0, 1)] _BaseAlpha ("Base Alpha", Float) = 1
        _DepthFadeDist ("Depth Fade Distance", Float) = 0.45
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 10
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
            Name "AtmosphericSoftParticle"
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
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
                float eyeDepth : TEXCOORD2;
                float4 vertexColor : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float LinearEyeDepthLocal(float rawDepth, float4 zBufferParam)
            {
                return 1.0 / (zBufferParam.z * rawDepth + zBufferParam.w);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.screenPos = ComputeScreenPos(output.positionCS);
                output.eyeDepth = -TransformWorldToView(positionWS).z;
                output.vertexColor = input.color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                half4 texel = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                float2 screenUV = input.screenPos.xy / max(input.screenPos.w, 1e-5);
                float rawSceneDepth = SampleSceneDepth(screenUV);
                float sceneDepthLinear = LinearEyeDepthLocal(rawSceneDepth, _ZBufferParams);
                float depthDiff = sceneDepthLinear - input.eyeDepth;
                float fadeDist = max(_DepthFadeDist, 1e-4);
                float depthFade = saturate(depthDiff / fadeDist);

                half3 color = texel.rgb * _ParticleColor.rgb * input.vertexColor.rgb;
                half alpha = texel.a * _ParticleColor.a * input.vertexColor.a * _BaseAlpha * depthFade;
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
