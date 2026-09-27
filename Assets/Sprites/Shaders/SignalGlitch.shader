Shader "SignalJam/Sprites/SignalGlitch"
{
    // Universal glitch effect: scanlines, static noise, slice displacement, dissolve.
    // All sub-effects are driven by _GlitchIntensity (0-1).
    // Used for faint (GlitchOut), spawn (GlitchIn), and looped overworld effects.
    
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint Color", Color) = (1, 1, 1, 1)
        
        [Header(Glitch)]
        _GlitchIntensity ("Glitch Intensity", Range(0, 1)) = 0
        
        [Header(Scanlines)]
        _ScanlineCount ("Scanline Count", Range(4, 128)) = 40
        _ScanlineSpeed ("Scanline Speed", Float) = 8
        _ScanlineAlpha ("Scanline Darkening", Range(0, 1)) = 0.35
        
        [Header(Static)]
        _StaticAmount ("Static Noise Blend", Range(0, 1)) = 0.5
        
        [Header(Slice Displacement)]
        _SliceDisplacement ("Slice UV Offset", Range(0, 0.2)) = 0.06
        _SliceBandCount ("Slice Band Count", Range(2, 16)) = 6
        
        [Header(Dissolve)]
        _DissolveThreshold ("Dissolve Threshold", Range(0, 1)) = 0
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
        ZWrite Off
        ZTest LEqual
        Blend SrcAlpha OneMinusSrcAlpha
        
        Pass
        {
            Name "SignalGlitch"
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
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
                half _GlitchIntensity;
                half _ScanlineCount;
                half _ScanlineSpeed;
                half _ScanlineAlpha;
                half _StaticAmount;
                half _SliceDisplacement;
                half _SliceBandCount;
                half _DissolveThreshold;
            CBUFFER_END
            
            float hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }
            
            float hash(float p, float t)
            {
                return frac(sin(p * 12.9898 + t * 78.233) * 43758.5453);
            }
            
            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.color = input.color * _Color;
                
                return output;
            }
            
            half4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                float time = _Time.y;
                
                float noise = hash(uv + time * 0.1);
                float noise2 = hash(uv * 2.0 + time * 0.3);
                
                float scanline = step(0.5, frac(uv.y * _ScanlineCount + time * _ScanlineSpeed));
                
                float bandY = floor(uv.y * _SliceBandCount);
                float sliceNoise = hash(bandY, time * 10.0);
                float2 offsetUV = uv + float2((sliceNoise - 0.5) * 2.0 * _SliceDisplacement * _GlitchIntensity, 0);
                
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, offsetUV);
                
                tex.rgb = lerp(tex.rgb, float3(noise, noise, noise), _StaticAmount * _GlitchIntensity);
                tex.rgb *= 1.0 - scanline * _ScanlineAlpha * _GlitchIntensity;
                
                clip(noise2 - _DissolveThreshold);
                
                half4 finalColor = tex * input.color;
                clip(finalColor.a - 0.01);
                
                return finalColor;
            }
            ENDHLSL
        }
    }
    
    FallBack "Sprites/Default"
}
