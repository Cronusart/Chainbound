Shader "Environment/WaterfallScroll"
{
    // Pixel-art waterfall curtain for Crocotile meshes.
    // Scrolls a repeating strip in object-space XY so atlas-unwrapped
    // scenery UVs do not crawl neighboring tiles.
    Properties
    {
        [Header(Texture)]
        _MainTex ("Waterfall Strip", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)

        [Header(Scroll)]
        _ScrollSpeed ("Scroll Speed (UV/sec)", Float) = 0.45

        [Header(Mapping)]
        [Toggle(_USE_OBJECT_SPACE_UVS)] _UseObjectSpaceUVs ("Use Object Space UVs", Float) = 1
        _UvTileSize ("Object Units Per Repeat (XY)", Vector) = (80, 40, 0, 0)
        _UvOffset ("UV Offset", Vector) = (0, 0, 0, 0)
        [Toggle(_PIXEL_SNAP)] _PixelSnap ("Snap To Texels", Float) = 1

        [Header(Holes)]
        [Toggle(_TREAT_WHITE_AS_CLEAR)] _TreatWhiteAsClear ("Treat White As Clear", Float) = 1
        _WhiteClip ("White Clip", Range(0, 1)) = 0.92
        _Cutoff ("Alpha Clip", Range(0, 1)) = 0.1

        [Header(Lighting)]
        [Toggle(_RECEIVE_LIGHTING)] _ReceiveLighting ("Receive Lighting", Float) = 1
        _AmbientStrength ("Ambient Strength", Range(0, 2)) = 1
        _DirectionalStrength ("Directional Light Strength", Range(0, 2)) = 0.45

        [Header(Render)]
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "AlphaTest"
            "RenderType" = "TransparentCutout"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Cull [_Cull]
        ZWrite On
        ZTest LEqual

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma shader_feature_local _USE_OBJECT_SPACE_UVS
            #pragma shader_feature_local _PIXEL_SNAP
            #pragma shader_feature_local _TREAT_WHITE_AS_CLEAR
            #pragma shader_feature_local _RECEIVE_LIGHTING

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                half fogFactor : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _MainTex_TexelSize;
                half4 _Color;
                float _ScrollSpeed;
                float4 _UvTileSize;
                float4 _UvOffset;
                half _WhiteClip;
                half _Cutoff;
                half _AmbientStrength;
                half _DirectionalStrength;
            CBUFFER_END

            float2 BuildUv(float3 positionOS, float2 meshUv)
            {
                float2 uv;
                #ifdef _USE_OBJECT_SPACE_UVS
                float2 tileSize = max(_UvTileSize.xy, float2(0.0001, 0.0001));
                uv = positionOS.xy / tileSize + _UvOffset.xy;
                #else
                uv = TRANSFORM_TEX(meshUv, _MainTex) + _UvOffset.xy;
                #endif
                uv.y += _Time.y * _ScrollSpeed;

                #ifdef _PIXEL_SNAP
                float2 texelCount = max(_MainTex_TexelSize.zw, float2(1, 1));
                uv = (floor(uv * texelCount) + 0.5) / texelCount;
                #endif
                return uv;
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS);
                output.positionCS = vertexInput.positionCS;
                output.uv = BuildUv(input.positionOS.xyz, input.uv);
                output.normalWS = normalInput.normalWS;
                output.fogFactor = ComputeFogFactor(vertexInput.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                half4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half4 color = texColor * _Color;

                #ifdef _TREAT_WHITE_AS_CLEAR
                half luma = max(texColor.r, max(texColor.g, texColor.b));
                color.a *= step(luma, _WhiteClip);
                #endif

                clip(color.a - _Cutoff);

                #ifdef _RECEIVE_LIGHTING
                float3 normalWS = normalize(input.normalWS);
                Light mainLight = GetMainLight();
                float wrapLighting = saturate((dot(normalWS, mainLight.direction) + 0.5) / 1.5);
                half3 ambient = SampleSH(normalWS) * _AmbientStrength;
                half3 directional = mainLight.color * wrapLighting * mainLight.distanceAttenuation * _DirectionalStrength;
                color.rgb *= ambient + directional;
                #endif

                color.rgb = MixFog(color.rgb, input.fogFactor);
                return color;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
