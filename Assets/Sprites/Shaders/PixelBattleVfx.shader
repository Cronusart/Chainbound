Shader "SignalJam/VFX/PixelBattleVfx"
{
    Properties
    {
        _MainTex ("Source Texture", 2D) = "white" {}
        [HDR] _Color ("Tint Color", Color) = (1, 1, 1, 1)

        [Header(Pixel Grid)]
        _PixelCells ("Pixel Cells", Range(1, 128)) = 16
        _PixelCanvasSize ("Pixel Canvas Size (Pixels)", Vector) = (0, 0, 0, 0)
        _PosterizeSteps ("Posterize Steps (0 = Off)", Float) = 0

        [Header(Alpha)]
        _AlphaCutoff ("Alpha Cutoff", Range(0, 1)) = 0.01
        _DissolveAmount ("Dissolve Amount", Range(0, 1)) = 0
        _DissolveFeather ("Dissolve Feather", Range(0.001, 1)) = 0.08
        _DissolveNoiseScale ("Dissolve Noise Scale", Vector) = (16, 16, 0, 0)
        _DissolveNoiseSpeed ("Dissolve Noise Speed", Float) = 0

        [Header(Output)]
        _EmissionStrength ("Emission Strength", Float) = 1

        [Header(Render State)]
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Source Blend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Destination Blend", Float) = 10
        // ZWrite Off + ZTest LessEqual: soft transparent edges, still occluded by Chainling depth quads.
        [Enum(Off, 0, On, 1)] _ZWrite ("Z Write", Float) = 0
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("Z Test", Float) = 4
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Cull Off
        ZWrite [_ZWrite]
        ZTest [_ZTest]
        Blend [_SrcBlend] [_DstBlend]

        Pass
        {
            Name "PixelBattleVfx"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 localUv : TEXCOORD0;
                float4 color : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _MainTex_TexelSize;
                float4 _Color;
                float _PixelCells;
                float4 _PixelCanvasSize;
                float _PosterizeSteps;
                float _AlphaCutoff;
                float _DissolveAmount;
                float _DissolveFeather;
                float4 _DissolveNoiseScale;
                float _DissolveNoiseSpeed;
                float _EmissionStrength;
            CBUFFER_END

            float Hash21(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            float ValueNoise(float2 p)
            {
                float2 cell = floor(p);
                float2 local = frac(p);
                local = local * local * (3.0 - 2.0 * local);

                float a = Hash21(cell);
                float b = Hash21(cell + float2(1.0, 0.0));
                float c = Hash21(cell + float2(0.0, 1.0));
                float d = Hash21(cell + float2(1.0, 1.0));

                return lerp(lerp(a, b, local.x), lerp(c, d, local.x), local.y);
            }

            float2 ResolvePixelCanvasSize()
            {
                float2 explicitCanvasSize = _PixelCanvasSize.xy;
                if (explicitCanvasSize.x > 0.0 && explicitCanvasSize.y > 0.0)
                    return explicitCanvasSize;

                // Atlas-aware fallback: _MainTex_ST describes the active frame,
                // so infer its pixel dimensions from the source texture.
                float2 inferredCanvasSize = _MainTex_TexelSize.zw * max(abs(_MainTex_ST.xy), float2(0.0001, 0.0001));
                return max(inferredCanvasSize, float2(1.0, 1.0));
            }

            float2 PixelGridUv(float2 localUv)
            {
                float2 canvasSize = ResolvePixelCanvasSize();
                float cellsX = max(_PixelCells, 1.0);
                // _PixelCells is the horizontal density. Deriving the vertical
                // count keeps each virtual pixel square on rectangular canvases.
                float cellsY = max(1.0, round(cellsX * canvasSize.y / max(canvasSize.x, 1.0)));
                float2 cells = float2(cellsX, cellsY);
                float2 clampedUv = min(saturate(localUv), 1.0 - 0.0001);
                return (floor(clampedUv * cells) + 0.45) / cells;
            }

            float3 Posterize(float3 color)
            {
                float steps = max(_PosterizeSteps, 0.0);
                if (steps <= 1.0)
                    return color;

                return floor(saturate(color) * steps + 0.5) / steps;
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.localUv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 snappedLocalUv = PixelGridUv(input.localUv);
                float2 sourceUv = snappedLocalUv * _MainTex_ST.xy + _MainTex_ST.zw;
                half4 source = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, sourceUv);
                half4 color = source * input.color * _Color;

                if (_DissolveAmount > 0.001)
                {
                    float2 noiseUv = snappedLocalUv * max(_DissolveNoiseScale.xy, float2(0.001, 0.001));
                    noiseUv += _Time.y * _DissolveNoiseSpeed;
                    float noise = ValueNoise(noiseUv);
                    float feather = max(_DissolveFeather, 0.001);
                    color.a *= smoothstep(_DissolveAmount - feather, _DissolveAmount + feather, noise);
                }

                clip(color.a - _AlphaCutoff);
                color.rgb = Posterize(color.rgb) * max(_EmissionStrength, 0.0);
                return color;
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Unlit"
}
