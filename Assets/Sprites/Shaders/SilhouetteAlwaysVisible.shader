Shader "Sprites/SilhouetteOccluded"
{
    // Silhouette shader that only renders where player is occluded (behind objects).
    // Uses ZTest Greater to only draw where depth test fails (player behind something).
    // Cassette Beasts-style effect: silhouette appears through occluding geometry.
    // Works with both URP and Built-in render pipeline.
    
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint Color", Color) = (0.2, 0.1, 0.3, 0.5)
    }
    
    SubShader
    {
        Tags 
        { 
            "Queue"="Overlay+100"
            "RenderType"="Transparent" 
            "RenderPipeline"="UniversalPipeline"
            "IgnoreProjector"="True"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }
        
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest Greater  // Only render where depth test FAILS (player is behind something)
        Blend SrcAlpha OneMinusSrcAlpha
        
        Pass
        {
            Name "SilhouetteOccluded"
            Tags { "LightMode"="UniversalForward" }
            
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
            CBUFFER_END
            
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
                half4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                
                // Apply tint color, use texture alpha for sprite shape
                half4 finalColor;
                finalColor.rgb = input.color.rgb;
                finalColor.a = texColor.a * input.color.a;
                
                // Discard fully transparent pixels
                clip(finalColor.a - 0.01);
                
                return finalColor;
            }
            ENDHLSL
        }
    }
    
    // Fallback for Built-in Render Pipeline
    SubShader
    {
        Tags 
        { 
            "Queue"="Overlay+100"
            "RenderType"="Transparent" 
            "IgnoreProjector"="True"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }
        
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest Greater  // Only render where depth test FAILS (player is behind something)
        Blend SrcAlpha OneMinusSrcAlpha
        
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            
            #include "UnityCG.cginc"
            
            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            
            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            
            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            
            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color * _Color;
                
                return o;
            }
            
            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 texColor = tex2D(_MainTex, i.uv);
                
                // Apply tint color, use texture alpha for sprite shape
                fixed4 finalColor;
                finalColor.rgb = i.color.rgb;
                finalColor.a = texColor.a * i.color.a;
                
                // Discard fully transparent pixels
                clip(finalColor.a - 0.01);
                
                return finalColor;
            }
            ENDCG
        }
    }
    
    FallBack "Sprites/Default"
}

