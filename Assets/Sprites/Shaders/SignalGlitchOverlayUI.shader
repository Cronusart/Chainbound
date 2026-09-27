Shader "SignalJam/UI/SignalGlitchOverlay"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
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
        _SliceBandCount ("Slice Band Count", Range(2, 64)) = 6

        [Header(Dissolve)]
        _DissolveThreshold ("Dissolve Threshold", Range(0, 1)) = 0

        [HideInInspector] _StencilComp ("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil ("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp ("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask ("Stencil Read Mask", Float) = 255
        [HideInInspector] _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "False"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Pass
        {
            Name "UI"

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                fixed4 color : COLOR;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            float _GlitchIntensity;
            float _ScanlineCount;
            float _ScanlineSpeed;
            float _ScanlineAlpha;
            float _StaticAmount;
            float _SliceDisplacement;
            float _SliceBandCount;
            float _DissolveThreshold;
            float4 _ClipRect;

            float hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            float hashBand(float p, float t)
            {
                return frac(sin(p * 12.9898 + t * 78.233) * 43758.5453);
            }

            v2f vert(appdata_t v)
            {
                v2f o;
                o.worldPosition = v.vertex;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;
                float time = _Time.y;

                float noise = hash(uv + time * 0.1);
                float noise2 = hash(uv * 2.0 + time * 0.3);
                float scanline = step(0.5, frac(uv.y * _ScanlineCount + time * _ScanlineSpeed));

                float bandY = floor(uv.y * _SliceBandCount);
                float sliceNoise = hashBand(bandY, time * 10.0);
                float2 offsetUv = uv + float2((sliceNoise - 0.5) * 2.0 * _SliceDisplacement * _GlitchIntensity, 0);

                fixed4 tex = tex2D(_MainTex, offsetUv);
                tex.rgb = lerp(tex.rgb, float3(noise, noise, noise), _StaticAmount * _GlitchIntensity);
                tex.rgb *= 1.0 - scanline * _ScanlineAlpha * _GlitchIntensity;

                clip(noise2 - _DissolveThreshold);

                fixed4 finalColor = tex * i.color;
                #ifdef UNITY_UI_CLIP_RECT
                finalColor.a *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
                #endif

                clip(finalColor.a - 0.01);
                return finalColor;
            }
            ENDCG
        }
    }

    FallBack "UI/Default"
}
