Shader "Environment/GrassBend"
{
    // Grass shader with idle sway and interactive world-space proximity push.
    // Uses alpha cutout for proper depth testing and shadow casting.
    // Compatible with URP (Universal Render Pipeline).
    //
    // INTERACTION MODEL:
    // - Radial XZ push: Grass blades push away from player in world XZ plane
    // - Velocity bend: Blades bend in player movement direction
    // - Downward press: Blades press down when player is close
    // - Height mask: Base stays planted, tips move more
    
    Properties
    {
        _MainTex ("Grass Texture", 2D) = "white" {}
        _Color ("Tint Color", Color) = (1,1,1,1)
        _Cutoff ("Alpha Cutoff", Range(0,1)) = 0.5
        _SwaySpeed ("Sway Speed", Float) = 1.0
        _SwayStrength ("Sway Strength", Range(0,0.5)) = 0.1
        
        // Legacy bend properties (kept for backward compatibility with GrassPatchController)
        [HideInInspector] _BendDir ("Bend Direction", Vector) = (0,0,0,0)
        [HideInInspector] _BendStrength ("Bend Strength", Range(0,1)) = 0.0
        [HideInInspector] _BendCenter ("Bend Center", Vector) = (0,0,0,0)
        [HideInInspector] _PathOffset ("Path Offset", Range(0,1)) = 0.0
        [HideInInspector] _PathDir ("Path Direction", Vector) = (0,0,0,0)
        
        // New world-space interaction properties (set via GrassPatchManager MPB)
        [HideInInspector] _InteractorPosWS ("Interactor Pos WS", Vector) = (0,0,0,0)
        [HideInInspector] _InteractorVelWS ("Interactor Vel WS", Vector) = (0,0,0,0)
        [HideInInspector] _InteractRadius ("Interact Radius", Float) = 1.5
        [HideInInspector] _PushStrength ("Push Strength", Float) = 0.0
        [HideInInspector] _DownStrength ("Down Strength", Float) = 0.0
        [HideInInspector] _MaxDownOffset ("Max Down Offset", Float) = 0.15
        [HideInInspector] _HeightMaskPower ("Height Mask Power", Float) = 1.0
        [HideInInspector] _BladeBaseY ("Blade Base Y", Float) = 0.0
        [HideInInspector] _BladeHeight ("Blade Height", Float) = 1.0
        [HideInInspector] _UsePivotFalloff ("Use Pivot Falloff", Float) = 0.0
        [HideInInspector] _BladePivotWS ("Blade Pivot WS", Vector) = (0,0,0,0)
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
        }
        
        Cull Off
        ZWrite On
        ZTest LEqual
        ColorMask RGB
        
        // ============================================
        // UNIVERSAL FORWARD PASS - Main rendering
        // ============================================
        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode"="UniversalForward" }
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fog
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                float3 worldPos : TEXCOORD1;
                float3 worldNormal : TEXCOORD2;
                half fogFactor : TEXCOORD3;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
                half _Cutoff;
                half _SwaySpeed;
                half _SwayStrength;
            CBUFFER_END
            
            // Legacy MaterialPropertyBlock properties (per-instance, backward compatible)
            float2 _BendDir;
            float _BendStrength;
            float3 _BendCenter;
            float _PathOffset;
            float2 _PathDir;
            
            // New world-space interaction properties (per-instance via MPB)
            float3 _InteractorPosWS;
            float3 _InteractorVelWS;
            float _InteractRadius;
            float _PushStrength;
            float _DownStrength;
            float _MaxDownOffset;
            float _HeightMaskPower;
            float _BladeBaseY;
            float _BladeHeight;
            float _UsePivotFalloff;
            float3 _BladePivotWS;
            
            #include "GrassBendInteraction.hlsl"
            
            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                
                float3 positionOS = input.positionOS.xyz;
                
                // Transform to world space for calculations
                float3 worldPos = TransformObjectToWorld(positionOS);
                output.worldPos = worldPos;
                
                // Transform normal to world space for lighting
                float3 normalOS = input.normalOS;
                float3 worldNormal = TransformObjectToWorldNormal(normalOS);
                output.worldNormal = worldNormal;
                
                ApplyGrassBendDisplacement(positionOS, worldPos);
                output.worldPos = worldPos;
                
                // Transform final position to clip space
                output.positionCS = TransformObjectToHClip(positionOS);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.color = input.color * _Color;
                
                // Compute fog factor in vertex shader (correct method for URP)
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                
                return output;
            }
            
            half4 frag(Varyings input) : SV_Target
            {
                half4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                
                // Ensure vertex color defaults to white if not set
                half4 vertexColor = input.color;
                if (length(vertexColor.rgb) < 0.001)
                {
                    vertexColor = half4(1.0, 1.0, 1.0, 1.0);
                }
                
                half4 finalColor = texColor * vertexColor;
                
                // Alpha clip: discard transparent pixels
                clip(finalColor.a - _Cutoff);
                
                // ============================================
                // LIGHTING - Match URP Lit behavior for Time of Day
                // ============================================
                // Get main light (directional light from Time of Day manager)
                Light mainLight = GetMainLight();
                float3 lightDirection = mainLight.direction;
                half3 lightColor = mainLight.color;
                
                // Normalize world normal
                float3 normalWS = normalize(input.worldNormal);
                
                // Use wrap lighting for grass (softer falloff, handles forward-facing quads better)
                // Standard NdotL would make grass too dark when sun is overhead
                float NdotL = dot(normalWS, lightDirection);
                float wrapLighting = saturate((NdotL + 0.5) / 1.5); // Wrap lighting for softer look
                
                // Get ambient from environment (controlled by Time of Day FogAmbientTimeDriver)
                // This samples the ambient light/color set by RenderSettings
                half3 ambient = SampleSH(normalWS);
                
                // Directional contribution (attenuated by shadow if enabled)
                half3 directional = lightColor * wrapLighting * mainLight.distanceAttenuation;
                
                // Combine ambient + directional (matches URP Lit approach)
                half3 lighting = ambient + directional;
                
                // Apply lighting to final color
                finalColor.rgb *= lighting;
                
                // ============================================
                // FOG - Apply fog from Time of Day manager
                // ============================================
                // Use pre-computed fog factor from vertex shader (matches URP Lit behavior)
                finalColor.rgb = MixFog(finalColor.rgb, input.fogFactor);
                
                return finalColor;
            }
            ENDHLSL
        }
        
        // ============================================
        // SHADOW CASTER PASS - Casts sprite-shaped shadows
        // ============================================
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Off
            
            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_instancing
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            
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
                UNITY_VERTEX_OUTPUT_STEREO
            };
            
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half _Cutoff;
                half _SwaySpeed;
                half _SwayStrength;
            CBUFFER_END
            
            // Legacy properties
            float2 _BendDir;
            float _BendStrength;
            float3 _BendCenter;
            float _PathOffset;
            float2 _PathDir;
            
            // New world-space interaction properties
            float3 _InteractorPosWS;
            float3 _InteractorVelWS;
            float _InteractRadius;
            float _PushStrength;
            float _DownStrength;
            float _MaxDownOffset;
            float _HeightMaskPower;
            float _BladeBaseY;
            float _BladeHeight;
            float _UsePivotFalloff;
            float3 _BladePivotWS;
            
            #include "GrassBendInteraction.hlsl"
            
            Varyings ShadowPassVertex(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                
                float3 positionOS = input.positionOS.xyz;
                float3 worldPos = TransformObjectToWorld(positionOS);
                ApplyGrassBendDisplacement(positionOS, worldPos);
                output.positionCS = TransformObjectToHClip(positionOS);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                
                return output;
            }
            
            half4 ShadowPassFragment(Varyings input) : SV_Target
            {
                half4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                clip(texColor.a - _Cutoff);
                return 0;
            }
            ENDHLSL
        }
        
        // ============================================
        // DEPTH ONLY PASS - For depth prepass
        // ============================================
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
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            
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
                UNITY_VERTEX_OUTPUT_STEREO
            };
            
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half _Cutoff;
                half _SwaySpeed;
                half _SwayStrength;
            CBUFFER_END
            
            // Legacy properties
            float2 _BendDir;
            float _BendStrength;
            float3 _BendCenter;
            float _PathOffset;
            float2 _PathDir;
            
            // New world-space interaction properties
            float3 _InteractorPosWS;
            float3 _InteractorVelWS;
            float _InteractRadius;
            float _PushStrength;
            float _DownStrength;
            float _MaxDownOffset;
            float _HeightMaskPower;
            float _BladeBaseY;
            float _BladeHeight;
            float _UsePivotFalloff;
            float3 _BladePivotWS;
            
            #include "GrassBendInteraction.hlsl"
            
            Varyings DepthOnlyVertex(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                
                float3 positionOS = input.positionOS.xyz;
                float3 worldPos = TransformObjectToWorld(positionOS);
                ApplyGrassBendDisplacement(positionOS, worldPos);
                output.positionCS = TransformObjectToHClip(positionOS);
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
    
    FallBack "Sprites/Default"
}
