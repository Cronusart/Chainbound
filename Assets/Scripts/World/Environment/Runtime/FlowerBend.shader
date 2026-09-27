Shader "Environment/FlowerBend"
{
    // Flower shader with idle sway and interactive bending.
    // Uses alpha cutout for proper depth testing and shadow casting.
    // Compatible with URP (Universal Render Pipeline).
    
    Properties
    {
        _MainTex ("Grass Texture", 2D) = "white" {}
        _Color ("Tint Color", Color) = (1,1,1,1)
        _Cutoff ("Alpha Cutoff", Range(0,1)) = 0.5
        _SwaySpeed ("Sway Speed", Float) = 1.0
        _SwayStrength ("Sway Strength", Range(0,0.5)) = 0.1
        
        // These are set via MaterialPropertyBlock by GrassPatchController
        [HideInInspector] _BendDir ("Bend Direction", Vector) = (0,0,0,0)
        [HideInInspector] _BendStrength ("Bend Strength", Range(0,1)) = 0.0
        [HideInInspector] _BendCenter ("Bend Center", Vector) = (0,0,0,0)
        [HideInInspector] _PathOffset ("Path Offset", Range(0,1)) = 0.0
        [HideInInspector] _PathDir ("Path Direction", Vector) = (0,0,0,0)
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
            
            // MaterialPropertyBlock properties (per-instance)
            float2 _BendDir;
            float _BendStrength;
            float3 _BendCenter;
            float _PathOffset;
            float2 _PathDir;
            
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
                
                // ============================================
                // IDLE SWAY - Gentle wind-like motion
                // ============================================
                float swayTime = _Time.y * _SwaySpeed;
                float swayOffset = sin(swayTime + worldPos.x * 0.5 + worldPos.z * 0.5) * _SwayStrength;
                
                // Apply sway perpendicular to the quad (along X axis in object space)
                // Assuming quad faces forward (Z+), sway along X
                positionOS.x += swayOffset;
                
                // ============================================
                // INTERACTIVE BEND - Player movement effect
                // ============================================
                if (_BendStrength > 0.001)
                {
                    // Calculate distance from bend center
                    float2 bendCenterXZ = float2(_BendCenter.x, _BendCenter.z);
                    float2 worldPosXZ = float2(worldPos.x, worldPos.z);
                    float2 toVertex = worldPosXZ - bendCenterXZ;
                    float distFromCenter = length(toVertex);
                    
                    // Bend strength falls off with distance (max radius ~1.5 units for tighter effect)
                    float bendRadius = 1.5;
                    float distanceFalloff = saturate(1.0 - (distFromCenter / bendRadius));
                    
                    // Safely normalize bend direction (avoid NaN from zero vector)
                    float bendDirLen = length(_BendDir);
                    float2 bendDirWorld = bendDirLen > 0.001 ? _BendDir / bendDirLen : float2(0, 0);
                    
                    // Apply bend offset (displace vertices in the direction of movement)
                    // Higher vertices bend more (grass top bends more than base)
                    // Mesh vertices Y range from 0 (bottom) to height (top), typically 0 to 1
                    float vertexHeight = positionOS.y; // 0 at bottom, ~1 at top
                    float heightFactor = saturate(vertexHeight); // Direct mapping: 0 at bottom, 1 at top
                    
                    // Increased multiplier (0.8) for more visible bending
                    float2 bendOffset = bendDirWorld * _BendStrength * distanceFalloff * heightFactor * 0.8;
                    
                    // Apply bend in world space, then transform back
                    worldPos.xz += bendOffset;
                    positionOS = TransformWorldToObject(float4(worldPos, 1.0)).xyz;
                }
                
                // ============================================
                // PATH EFFECT - X-axis movement opposite to player direction
                // ============================================
                if (_PathOffset > 0.001)
                {
                    // Calculate distance from bend center (reuse same calculation)
                    float2 bendCenterXZ = float2(_BendCenter.x, _BendCenter.z);
                    float2 worldPosXZ = float2(worldPos.x, worldPos.z);
                    float2 toVertex = worldPosXZ - bendCenterXZ;
                    float distFromCenter = length(toVertex);
                    
                    // Path effect falls off with distance (max radius ~1.5 units)
                    float pathRadius = 1.5;
                    float pathDistanceFalloff = saturate(1.0 - (distFromCenter / pathRadius));
                    
                    // Safely normalize path direction
                    float pathDirLen = length(_PathDir);
                    float2 pathDirWorld = pathDirLen > 0.001 ? _PathDir / pathDirLen : float2(0, 0);
                    
                    // Apply path offset along X-axis (perpendicular to movement)
                    // Effect is uniform along grass height (whole blade parts)
                    // Use both X and Z components of path direction for full 2D parting
                    float2 pathOffset2D = pathDirWorld * _PathOffset * pathDistanceFalloff * 0.5;
                    
                    // Apply path offset in object space
                    positionOS.x += pathOffset2D.x;
                }
                
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
                
                // Use wrap lighting for flowers (softer falloff, handles forward-facing quads better)
                // Standard NdotL would make flowers too dark when sun is overhead
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
            
            // MaterialPropertyBlock properties
            float2 _BendDir;
            float _BendStrength;
            float3 _BendCenter;
            float _PathOffset;
            float2 _PathDir;
            
            Varyings ShadowPassVertex(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                
                float3 positionOS = input.positionOS.xyz;
                
                // Apply same vertex displacement as main pass
                float3 worldPos = TransformObjectToWorld(positionOS);
                
                // Idle sway
                float swayTime = _Time.y * _SwaySpeed;
                float swayOffset = sin(swayTime + worldPos.x * 0.5 + worldPos.z * 0.5) * _SwayStrength;
                positionOS.x += swayOffset;
                
                // Interactive bend
                if (_BendStrength > 0.001)
                {
                    float2 bendCenterXZ = float2(_BendCenter.x, _BendCenter.z);
                    float2 worldPosXZ = float2(worldPos.x, worldPos.z);
                    float2 toVertex = worldPosXZ - bendCenterXZ;
                    float distFromCenter = length(toVertex);
                    float bendRadius = 1.5;
                    float distanceFalloff = saturate(1.0 - (distFromCenter / bendRadius));
                    float bendDirLen = length(_BendDir);
                    float2 bendDirWorld = bendDirLen > 0.001 ? _BendDir / bendDirLen : float2(0, 0);
                    float vertexHeight = positionOS.y;
                    float heightFactor = saturate(vertexHeight);
                    float2 bendOffset = bendDirWorld * _BendStrength * distanceFalloff * heightFactor * 0.8;
                    worldPos.xz += bendOffset;
                    positionOS = TransformWorldToObject(float4(worldPos, 1.0)).xyz;
                }
                
                // Path effect
                if (_PathOffset > 0.001)
                {
                    float2 bendCenterXZ = float2(_BendCenter.x, _BendCenter.z);
                    float2 worldPosXZ = float2(worldPos.x, worldPos.z);
                    float2 toVertex = worldPosXZ - bendCenterXZ;
                    float distFromCenter = length(toVertex);
                    float pathRadius = 1.5;
                    float pathDistanceFalloff = saturate(1.0 - (distFromCenter / pathRadius));
                    float pathDirLen = length(_PathDir);
                    float2 pathDirWorld = pathDirLen > 0.001 ? _PathDir / pathDirLen : float2(0, 0);
                    float2 pathOffset2D = pathDirWorld * _PathOffset * pathDistanceFalloff * 0.5;
                    positionOS.x += pathOffset2D.x;
                }
                
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
            
            // MaterialPropertyBlock properties
            float2 _BendDir;
            float _BendStrength;
            float3 _BendCenter;
            float _PathOffset;
            float2 _PathDir;
            
            Varyings DepthOnlyVertex(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                
                float3 positionOS = input.positionOS.xyz;
                
                // Apply same vertex displacement as main pass
                float3 worldPos = TransformObjectToWorld(positionOS);
                
                // Idle sway
                float swayTime = _Time.y * _SwaySpeed;
                float swayOffset = sin(swayTime + worldPos.x * 0.5 + worldPos.z * 0.5) * _SwayStrength;
                positionOS.x += swayOffset;
                
                // Interactive bend
                if (_BendStrength > 0.001)
                {
                    float2 bendCenterXZ = float2(_BendCenter.x, _BendCenter.z);
                    float2 worldPosXZ = float2(worldPos.x, worldPos.z);
                    float2 toVertex = worldPosXZ - bendCenterXZ;
                    float distFromCenter = length(toVertex);
                    float bendRadius = 1.5;
                    float distanceFalloff = saturate(1.0 - (distFromCenter / bendRadius));
                    float bendDirLen = length(_BendDir);
                    float2 bendDirWorld = bendDirLen > 0.001 ? _BendDir / bendDirLen : float2(0, 0);
                    float vertexHeight = positionOS.y;
                    float heightFactor = saturate(vertexHeight);
                    float2 bendOffset = bendDirWorld * _BendStrength * distanceFalloff * heightFactor * 0.8;
                    worldPos.xz += bendOffset;
                    positionOS = TransformWorldToObject(float4(worldPos, 1.0)).xyz;
                }
                
                // Path effect
                if (_PathOffset > 0.001)
                {
                    float2 bendCenterXZ = float2(_BendCenter.x, _BendCenter.z);
                    float2 worldPosXZ = float2(worldPos.x, worldPos.z);
                    float2 toVertex = worldPosXZ - bendCenterXZ;
                    float distFromCenter = length(toVertex);
                    float pathRadius = 1.5;
                    float pathDistanceFalloff = saturate(1.0 - (distFromCenter / pathRadius));
                    float pathDirLen = length(_PathDir);
                    float2 pathDirWorld = pathDirLen > 0.001 ? _PathDir / pathDirLen : float2(0, 0);
                    float2 pathOffset2D = pathDirWorld * _PathOffset * pathDistanceFalloff * 0.5;
                    positionOS.x += pathOffset2D.x;
                }
                
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

