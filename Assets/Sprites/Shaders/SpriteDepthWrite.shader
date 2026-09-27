Shader "Sprites/DepthWrite"
{
    // Sprite shader with depth writing enabled for proper occlusion with opaque 3D objects.
    // Uses alpha clipping to discard transparent pixels so they don't write depth.
    // Renders in AlphaTest queue (2450) - after opaque, before transparent.
    // Compatible with all URP versions (doesn't use Shadows.hlsl helper functions).
    // 
    // LIGHTING SUPPORT:
    // - Responds to ambient light (controlled by TimeOfDay FogAmbientTimeDriver)
    // - Responds to directional light (controlled by TimeOfDay DirectionalLightTimeDriver)
    // - Responds to fog (controlled by TimeOfDay FogAmbientTimeDriver)
    // - Uses wrap lighting for billboard-friendly soft shading
    
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint Color", Color) = (1,1,1,1)
        _Cutoff ("Alpha Cutoff", Range(0,1)) = 0.5
        _DepthBias ("Depth Bias", Range(-0.01,0.01)) = 0.0001
        
        [Header(Lighting)]
        [Toggle(_RECEIVE_LIGHTING)] _ReceiveLighting ("Receive Lighting", Float) = 1
        _AmbientStrength ("Ambient Strength", Range(0,2)) = 1.0
        _DirectionalStrength ("Directional Light Strength", Range(0,2)) = 0.5

        [Header(Action Outline)]
        _OutlineWidth ("Outline Width (Pixels)", Float) = 0
        _OutlineColor ("Outline Color", Color) = (0.15, 0.8, 1, 0.8)
        _OutlineSoftness ("Outline Softness", Range(0,1)) = 0.5
        _OutlineIntensity ("Outline Intensity", Range(0,1)) = 0
        _OutlineDepthBias ("Outline Depth Bias", Range(-0.01,0.01)) = 0
        [Enum(UnityEngine.Rendering.CompareFunction)] _OutlineZTest ("Outline ZTest", Float) = 4
    }
    
    SubShader
    {
        Tags 
        { 
            "Queue"="AlphaTest"
            "RenderType"="TransparentCutout" 
            "RenderPipeline"="UniversalPipeline"
            "IgnoreProjector"="True"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }
        
        Cull Off
        ZWrite On      // Write to depth buffer
        ZTest LEqual   // Standard depth testing
        Offset 0, 0    // Minimal depth bias (only for Z-fighting, not for always rendering in front)
        ColorMask RGB  // Write color

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_MainTex);
        SAMPLER(sampler_MainTex);

        // All forward passes must share an identical UnityPerMaterial layout (SRP Batcher requirement).
        CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            half4 _Color;
            half _Cutoff;
            half _DepthBias;
            half _AmbientStrength;
            half _DirectionalStrength;
            half _OutlineWidth;
            half4 _OutlineColor;
            half _OutlineSoftness;
            half _OutlineIntensity;
            half _OutlineDepthBias;
            float4 _MainTex_TexelSize;
        CBUFFER_END
        ENDHLSL
        
        Pass
        {
            Name "SpriteDepthWrite"
            Tags { "LightMode"="UniversalForward" }

            Blend One Zero
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma shader_feature_local _RECEIVE_LIGHTING
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float fogFactor : TEXCOORD3;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            
            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                
                // Transform normal to world space
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS);
                output.normalWS = normalInput.normalWS;
                
                // Apply minimal depth bias ONLY to prevent Z-fighting at intersections
                // Keep bias very small so player can still be occluded when behind objects
                // This only helps when billboard quad intersects building geometry
                output.positionCS.z += _DepthBias * output.positionCS.w;
                
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.color = input.color * _Color;
                
                // Compute fog factor for Time of Day fog support
                output.fogFactor = ComputeFogFactor(vertexInput.positionCS.z);
                
                return output;
            }
            
            half4 frag(Varyings input) : SV_Target
            {
                half4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                
                // Apply vertex color tinting
                half4 finalColor = texColor * input.color;
                
                // Alpha clip: discard pixels below threshold
                // This prevents transparent pixels from writing depth
                clip(finalColor.a - _Cutoff);
                
                #ifdef _RECEIVE_LIGHTING
                // ============================================
                // LIGHTING - Match URP Lit behavior for Time of Day
                // ============================================
                
                // Get main light (directional light from Time of Day manager)
                Light mainLight = GetMainLight();
                float3 lightDirection = mainLight.direction;
                half3 lightColor = mainLight.color;
                
                // Normalize world normal (billboards face camera, so use that for consistent lighting)
                float3 normalWS = normalize(input.normalWS);
                
                // Use wrap lighting for sprites (softer falloff, handles forward-facing billboards better)
                // Standard NdotL would make sprites too dark depending on light angle
                float NdotL = dot(normalWS, lightDirection);
                float wrapLighting = saturate((NdotL + 0.5) / 1.5); // Wrap lighting for softer look
                
                // Get ambient from environment (controlled by Time of Day FogAmbientTimeDriver)
                // This samples the ambient light/color set by RenderSettings
                half3 ambient = SampleSH(normalWS) * _AmbientStrength;
                
                // Directional contribution (attenuated by main light)
                half3 directional = lightColor * wrapLighting * mainLight.distanceAttenuation * _DirectionalStrength;
                
                // Combine ambient + directional (matches URP Lit approach)
                half3 lighting = ambient + directional;
                
                // Apply lighting to final color
                finalColor.rgb *= lighting;
                
                // ============================================
                // FOG - Apply fog from Time of Day manager
                // ============================================
                finalColor.rgb = MixFog(finalColor.rgb, input.fogFactor);
                #endif
                
                return finalColor;
            }
            ENDHLSL
        }

        Pass
        {
            Name "SpriteOutline"
            // URP only draws one pass per LightMode during the forward object pass.
            // Keep the halo separate so the main UniversalForward body still renders.
            Tags { "LightMode"="SRPDefaultUnlit" }

            Blend SrcAlpha OneMinusSrcAlpha
            ColorMask RGBA
            ZWrite Off
            ZTest [_OutlineZTest]

            HLSLPROGRAM
            #pragma vertex vertOutline
            #pragma fragment fragOutline
            #pragma multi_compile_instancing

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
                half4 color : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vertOutline(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = vertexInput.positionCS;
                output.positionCS.z += _OutlineDepthBias * output.positionCS.w;

                float2 cornerDir = input.positionOS.xy;
                float cornerLen = length(cornerDir);
                if (cornerLen > 1e-5 && _OutlineWidth > 0.001)
                {
                    cornerDir /= cornerLen;
                    float expand = _OutlineWidth * 2.0 / _ScreenParams.y;
                    output.positionCS.xy += cornerDir * expand * output.positionCS.w;
                }

                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.color = input.color * _Color;
                return output;
            }

            half SampleSpriteAlpha(float2 uv, half vertexAlpha)
            {
                half4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
                return texColor.a * vertexAlpha;
            }

            half4 fragOutline(Varyings input) : SV_Target
            {
                if (_OutlineWidth <= 0.001 || _OutlineIntensity <= 0.001)
                    discard;

                half centerAlpha = SampleSpriteAlpha(input.uv, input.color.a);
                if (centerAlpha >= _Cutoff)
                    discard;

                float2 texel = _MainTex_TexelSize.xy;
                if (texel.x <= 0.0 || texel.y <= 0.0)
                    texel = float2(1.0 / max(_MainTex_TexelSize.z, 1.0), 1.0 / max(_MainTex_TexelSize.w, 1.0));
                float radius = max(_OutlineWidth, 1.0);
                half maxNeighbor = centerAlpha;

                maxNeighbor = max(maxNeighbor, SampleSpriteAlpha(input.uv + float2(texel.x, 0) * radius, input.color.a));
                maxNeighbor = max(maxNeighbor, SampleSpriteAlpha(input.uv + float2(-texel.x, 0) * radius, input.color.a));
                maxNeighbor = max(maxNeighbor, SampleSpriteAlpha(input.uv + float2(0, texel.y) * radius, input.color.a));
                maxNeighbor = max(maxNeighbor, SampleSpriteAlpha(input.uv + float2(0, -texel.y) * radius, input.color.a));
                maxNeighbor = max(maxNeighbor, SampleSpriteAlpha(input.uv + float2(texel.x, texel.y) * radius * 0.707, input.color.a));
                maxNeighbor = max(maxNeighbor, SampleSpriteAlpha(input.uv + float2(-texel.x, texel.y) * radius * 0.707, input.color.a));
                maxNeighbor = max(maxNeighbor, SampleSpriteAlpha(input.uv + float2(texel.x, -texel.y) * radius * 0.707, input.color.a));
                maxNeighbor = max(maxNeighbor, SampleSpriteAlpha(input.uv + float2(-texel.x, -texel.y) * radius * 0.707, input.color.a));

                half edge = maxNeighbor - centerAlpha;
                if (edge <= 0.001)
                    discard;

                half softness = max(_OutlineSoftness, 0.01);
                half ring = saturate(edge / softness);
                ring = pow(ring, lerp(1.0h, 2.5h, _OutlineSoftness));

                half alpha = ring * _OutlineColor.a * _OutlineIntensity;
                return half4(_OutlineColor.rgb * _OutlineIntensity, alpha);
            }
            ENDHLSL
        }
        
        // Shadow casting pass with alpha clipping (sprite-shaped shadows)
        // Simple implementation without Shadows.hlsl dependencies
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            
            ZWrite On
            ZTest LEqual
            ColorMask 0  // Don't write color, only depth
            Cull Off
            
            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_instancing
            
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
                UNITY_VERTEX_OUTPUT_STEREO
            };
            
            Varyings ShadowPassVertex(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                
                // Simple shadow position - just transform to clip space
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                
                return output;
            }
            
            half4 ShadowPassFragment(Varyings input) : SV_Target
            {
                // Sample texture alpha
                half4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                
                // Clip based on alpha (respect sprite shape)
                clip(texColor.a - _Cutoff);
                
                return 0;
            }
            ENDHLSL
        }
        
        // Depth only pass
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            
            ZWrite On
            ColorMask 0
            Cull Off
            
            HLSLPROGRAM
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            #pragma multi_compile_instancing
            
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
                UNITY_VERTEX_OUTPUT_STEREO
            };
            
            Varyings DepthOnlyVertex(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                return output;
            }
            
            half4 DepthOnlyFragment(Varyings input) : SV_Target
            {
                half4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                clip(texColor.a - _Cutoff);
                return 0;
            }
            ENDHLSL
        }
    }
    
    // Fallback for Built-in Render Pipeline
    SubShader
    {
        Tags 
        { 
            "Queue"="AlphaTest"
            "RenderType"="TransparentCutout" 
            "IgnoreProjector"="True"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }
        
        Cull Off
        ZWrite On
        ZTest LEqual
        Offset 0, 0
        ColorMask RGB
        
        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma shader_feature_local _RECEIVE_LIGHTING
            
            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            
            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            
            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
                float3 worldPos : TEXCOORD1;
                float3 worldNormal : TEXCOORD2;
                UNITY_FOG_COORDS(3)
                UNITY_VERTEX_OUTPUT_STEREO
            };
            
            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            fixed _Cutoff;
            fixed _DepthBias;
            fixed _AmbientStrength;
            fixed _DirectionalStrength;
            
            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                
                o.pos = UnityObjectToClipPos(v.vertex);
                o.pos.z += _DepthBias * o.pos.w;
                
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color * _Color;
                
                UNITY_TRANSFER_FOG(o, o.pos);
                
                return o;
            }
            
            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 texColor = tex2D(_MainTex, i.uv);
                fixed4 finalColor = texColor * i.color;
                clip(finalColor.a - _Cutoff);
                
                #ifdef _RECEIVE_LIGHTING
                // Get normalized world normal
                float3 normalWS = normalize(i.worldNormal);
                
                // Sample ambient (SH) lighting
                half3 ambient = ShadeSH9(half4(normalWS, 1.0)) * _AmbientStrength;
                
                // Get main directional light
                float3 lightDir = normalize(_WorldSpaceLightPos0.xyz);
                half3 lightColor = _LightColor0.rgb;
                
                // Wrap lighting for softer look on billboards
                float NdotL = dot(normalWS, lightDir);
                float wrapLighting = saturate((NdotL + 0.5) / 1.5);
                
                // Directional contribution
                half3 directional = lightColor * wrapLighting * _DirectionalStrength;
                
                // Combine lighting
                half3 lighting = ambient + directional;
                finalColor.rgb *= lighting;
                
                // Apply fog
                UNITY_APPLY_FOG(i.fogCoord, finalColor);
                #endif
                
                return finalColor;
            }
            ENDCG
        }
        
        // Shadow casting pass with alpha clipping (Built-in RP)
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Off
            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_shadowcaster
            
            #include "UnityCG.cginc"
            
            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float3 normal : NORMAL;  // Required for TRANSFER_SHADOW_CASTER_NORMALOFFSET
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            
            struct v2f
            {
                V2F_SHADOW_CASTER;
                float2 uv : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            
            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed _Cutoff;
            
            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                
                TRANSFER_SHADOW_CASTER_NORMALOFFSET(o)
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                
                return o;
            }
            
            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 texColor = tex2D(_MainTex, i.uv);
                clip(texColor.a - _Cutoff);
                SHADOW_CASTER_FRAGMENT(i)
            }
            ENDCG
        }
    }
    
    FallBack "Sprites/Default"
}
