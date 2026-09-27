Shader "Environment/WaterWaves"
{
    // Water shader with wave vertex displacement, UV scrolling, and URP lighting support.
    // Configurable wave properties allow for various water effects.
    
    Properties
    {
        [Header(Base Water)]
        _MainTex ("Water Texture", 2D) = "white" {}
        _Color ("Color Tint", Color) = (0.42, 0.67, 0.82, 0.8)
        _UVScrollSpeed ("UV Scroll Speed", Vector) = (0.1, 0.1, 0, 0)
        
        [Header(Wave Settings)]
        [Toggle(_ENABLE_WAVES)] _EnableWaves ("Enable Waves", Float) = 1
        _WaveAmplitude ("Wave Amplitude", Range(0, 2)) = 0.3
        _WaveFrequency ("Wave Frequency", Range(0.1, 10)) = 2.0
        _WaveSpeed ("Wave Speed", Range(0, 5)) = 1.0
        _WaveDirection ("Wave Direction (XZ)", Vector) = (1, 0, 0.5, 0)
        
        [Header(Secondary Wave)]
        [Toggle(_ENABLE_SECONDARY_WAVE)] _EnableSecondaryWave ("Enable Secondary Wave", Float) = 1
        _Wave2Amplitude ("Wave 2 Amplitude", Range(0, 1)) = 0.15
        _Wave2Frequency ("Wave 2 Frequency", Range(0.1, 20)) = 4.0
        _Wave2Speed ("Wave 2 Speed", Range(0, 5)) = 0.8
        _Wave2Direction ("Wave 2 Direction (XZ)", Vector) = (0.3, 0, 1, 0)

        [Header(Local Interaction)]
        [Toggle(_ENABLE_PLAYER_WAKE)] _EnablePlayerWake ("Enable Player Wake", Float) = 0
        
        [Header(Transparency)]
        _Alpha ("Base Alpha", Range(0, 1)) = 0.8
        _EdgeFadeDistance ("Edge Fade Distance", Range(0, 5)) = 1.0
        
        [Header(Lighting)]
        _Smoothness ("Smoothness", Range(0, 1)) = 0.8
        _SpecularStrength ("Specular Strength", Range(0, 2)) = 1.0
        
        [Header(Render Settings)]
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.01
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull Mode", Float) = 2
    }
    
    SubShader
    {
        Tags 
        { 
            "RenderType" = "Transparent" 
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }
        
        LOD 300
        Cull [_Cull]
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fog
            #pragma shader_feature_local _ENABLE_WAVES
            #pragma shader_feature_local _ENABLE_SECONDARY_WAVE
            #pragma shader_feature_local _ENABLE_PLAYER_WAKE
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "WaterWakeInteraction.hlsl"
            
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
                float2 _UVScrollSpeed;
                
                // Wave parameters
                half _WaveAmplitude;
                half _WaveFrequency;
                half _WaveSpeed;
                float4 _WaveDirection;
                
                // Secondary wave
                half _Wave2Amplitude;
                half _Wave2Frequency;
                half _Wave2Speed;
                float4 _Wave2Direction;
                
                // Transparency
                half _Alpha;
                half _EdgeFadeDistance;
                
                // Lighting
                half _Smoothness;
                half _SpecularStrength;
                
                half _Cutoff;
            CBUFFER_END
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                float3 worldNormal : TEXCOORD2;
                float3 worldTangent : TEXCOORD3;
                float4 color : COLOR;
                half fogFactor : TEXCOORD4;
                float3 worldBitangent : TEXCOORD5;
                float2 wakeMask : TEXCOORD6;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            
            // ============================================
            // WAVE FUNCTIONS
            // ============================================
            
            // Gerstner wave function for realistic water movement
            float3 GerstnerWave(float3 worldPos, float2 direction, float amplitude, float frequency, float speed, float time, out float3 tangent, out float3 bitangent)
            {
                // Normalize direction
                float2 dir = normalize(direction);
                
                // Calculate wave phase
                float phase = dot(dir, worldPos.xz) * frequency + time * speed;
                
                // Sine and cosine for wave shape
                float sinPhase = sin(phase);
                float cosPhase = cos(phase);
                
                // Gerstner wave displacement
                float3 displacement;
                displacement.x = dir.x * amplitude * cosPhase;
                displacement.y = amplitude * sinPhase;
                displacement.z = dir.y * amplitude * cosPhase;
                
                // Calculate tangent (derivative in X direction)
                tangent = float3(
                    1 - dir.x * dir.x * amplitude * frequency * sinPhase,
                    dir.x * amplitude * frequency * cosPhase,
                    -dir.x * dir.y * amplitude * frequency * sinPhase
                );
                
                // Calculate bitangent (derivative in Z direction)
                bitangent = float3(
                    -dir.x * dir.y * amplitude * frequency * sinPhase,
                    dir.y * amplitude * frequency * cosPhase,
                    1 - dir.y * dir.y * amplitude * frequency * sinPhase
                );
                
                return displacement;
            }
            
            // Simple sine wave (less realistic but cheaper)
            float SimpleWave(float3 worldPos, float2 direction, float amplitude, float frequency, float speed, float time)
            {
                float2 dir = normalize(direction);
                float phase = dot(dir, worldPos.xz) * frequency + time * speed;
                return amplitude * sin(phase);
            }
            
            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                
                // Start with object space position
                float3 positionOS = input.positionOS.xyz;
                
                // Transform to world space for wave calculations
                float3 worldPos = TransformObjectToWorld(positionOS);
                
                // Get time
                float time = _Time.y;
                
                // Initialize normal calculation helpers
                float3 tangent1 = float3(1, 0, 0);
                float3 bitangent1 = float3(0, 0, 1);
                float3 tangent2 = float3(1, 0, 0);
                float3 bitangent2 = float3(0, 0, 1);
                
                // ============================================
                // WAVE DISPLACEMENT
                // ============================================
                #if defined(_ENABLE_WAVES)
                {
                    // Primary wave using Gerstner function
                    float3 wave1 = GerstnerWave(
                        worldPos,
                        _WaveDirection.xz,
                        _WaveAmplitude,
                        _WaveFrequency,
                        _WaveSpeed,
                        time,
                        tangent1,
                        bitangent1
                    );
                    worldPos += wave1;
                    
                    #if defined(_ENABLE_SECONDARY_WAVE)
                    {
                        // Secondary wave for more complex water surface
                        float3 wave2 = GerstnerWave(
                            worldPos,
                            _Wave2Direction.xz,
                            _Wave2Amplitude,
                            _Wave2Frequency,
                            _Wave2Speed,
                            time,
                            tangent2,
                            bitangent2
                        );
                        worldPos += wave2;
                        
                        // Combine tangents for normal calculation
                        tangent1 = normalize(tangent1 + tangent2 - float3(1, 0, 0));
                        bitangent1 = normalize(bitangent1 + bitangent2 - float3(0, 0, 1));
                    }
                    #endif
                }
                #endif

                float2 wakeMask = float2(0.0, 0.0);
                #if defined(_ENABLE_PLAYER_WAKE)
                    wakeMask = ApplyWaterWakeInteraction(worldPos, _WaveDirection.xz);
                #endif
                
                // Calculate world normal from wave tangents
                float3 worldNormal = normalize(cross(bitangent1, tangent1));
                
                // If waves are disabled, use mesh normal
                #if !defined(_ENABLE_WAVES)
                    worldNormal = TransformObjectToWorldNormal(input.normalOS);
                #endif
                
                // Transform back to object space for clip space transform
                positionOS = TransformWorldToObject(float4(worldPos, 1.0)).xyz;
                
                // Output clip space position
                output.positionCS = TransformObjectToHClip(positionOS);
                
                // UV with scrolling
                output.uv = TRANSFORM_TEX(input.uv, _MainTex) + _UVScrollSpeed * time;
                
                // World space data
                output.worldPos = worldPos;
                output.worldNormal = worldNormal;
                output.worldTangent = normalize(tangent1);
                output.worldBitangent = normalize(bitangent1);
                output.wakeMask = wakeMask;
                
                // Vertex color
                output.color = input.color;
                
                // Compute fog factor in vertex shader
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                
                return output;
            }
            
            half4 frag(Varyings input) : SV_Target
            {
                // Sample water texture
                half4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                
                // Apply color tint
                half4 finalColor = texColor * _Color;
                
                // Apply vertex color if available
                finalColor *= input.color;
                
                // ============================================
                // LIGHTING
                // ============================================
                
                // Normalize world normal
                float3 normalWS = normalize(input.worldNormal);
                
                // Get main light
                Light mainLight = GetMainLight();
                float3 lightDir = mainLight.direction;
                half3 lightColor = mainLight.color;
                
                // View direction for specular
                float3 viewDir = normalize(GetCameraPositionWS() - input.worldPos);
                
                // Diffuse lighting (half-Lambert for softer look)
                float NdotL = dot(normalWS, lightDir);
                float halfLambert = NdotL * 0.5 + 0.5;
                half3 diffuse = lightColor * halfLambert;
                
                // Specular (Blinn-Phong)
                float3 halfVec = normalize(lightDir + viewDir);
                float NdotH = saturate(dot(normalWS, halfVec));
                float specPower = exp2(10 * _Smoothness + 1);
                half3 specular = lightColor * pow(NdotH, specPower) * _SpecularStrength;
                
                // Fresnel effect - water is more reflective at grazing angles
                float fresnel = pow(1.0 - saturate(dot(normalWS, viewDir)), 4);
                specular *= (1.0 + fresnel * 2.0);

                #if defined(_ENABLE_PLAYER_WAKE)
                    specular *= 1.0 + input.wakeMask.y * (0.75 + _WakeFoamStrength);
                #endif
                
                // Ambient from environment
                half3 ambient = SampleSH(normalWS);
                ambient = max(ambient, half3(0.1, 0.1, 0.1)); // Minimum ambient
                
                // Combine lighting
                half3 lighting = ambient + diffuse;
                finalColor.rgb *= lighting;
                finalColor.rgb += specular;

                #if defined(_ENABLE_PLAYER_WAKE)
                {
                    half bowlShade = saturate(input.wakeMask.x * 0.04);
                    half foamMask = saturate(input.wakeMask.y * _WakeFoamStrength);
                    half3 foamTint = half3(0.82h, 1.0h, 1.0h);
                    finalColor.rgb *= 1.0h - bowlShade;
                    finalColor.rgb = lerp(finalColor.rgb, foamTint, foamMask);
                    finalColor.a = saturate(finalColor.a + foamMask * 0.08h);
                }
                #endif
                
                // ============================================
                // TRANSPARENCY
                // ============================================
                
                // Base alpha
                finalColor.a *= _Alpha;
                
                // Alpha clip
                clip(finalColor.a - _Cutoff);
                
                // ============================================
                // FOG
                // ============================================
                finalColor.rgb = MixFog(finalColor.rgb, input.fogFactor);
                
                return finalColor;
            }
            ENDHLSL
        }
        
        // Shadow caster pass for casting shadows
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]
            
            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_instancing
            #pragma shader_feature_local _ENABLE_WAVES
            #pragma shader_feature_local _ENABLE_SECONDARY_WAVE
            #pragma shader_feature_local _ENABLE_PLAYER_WAKE
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            #include "WaterWakeInteraction.hlsl"
            
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
                float2 _UVScrollSpeed;
                half _WaveAmplitude;
                half _WaveFrequency;
                half _WaveSpeed;
                float4 _WaveDirection;
                half _Wave2Amplitude;
                half _Wave2Frequency;
                half _Wave2Speed;
                float4 _Wave2Direction;
                half _Alpha;
                half _EdgeFadeDistance;
                half _Smoothness;
                half _SpecularStrength;
                half _Cutoff;
            CBUFFER_END
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            
            float3 GerstnerWaveSimple(float3 worldPos, float2 direction, float amplitude, float frequency, float speed, float time)
            {
                float2 dir = normalize(direction);
                float phase = dot(dir, worldPos.xz) * frequency + time * speed;
                float sinPhase = sin(phase);
                float cosPhase = cos(phase);
                
                float3 displacement;
                displacement.x = dir.x * amplitude * cosPhase;
                displacement.y = amplitude * sinPhase;
                displacement.z = dir.y * amplitude * cosPhase;
                
                return displacement;
            }
            
            float3 _LightDirection;
            
            Varyings ShadowVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                
                float3 positionOS = input.positionOS.xyz;
                float3 worldPos = TransformObjectToWorld(positionOS);
                float time = _Time.y;
                
                #if defined(_ENABLE_WAVES)
                {
                    worldPos += GerstnerWaveSimple(worldPos, _WaveDirection.xz, _WaveAmplitude, _WaveFrequency, _WaveSpeed, time);
                    
                    #if defined(_ENABLE_SECONDARY_WAVE)
                    {
                        worldPos += GerstnerWaveSimple(worldPos, _Wave2Direction.xz, _Wave2Amplitude, _Wave2Frequency, _Wave2Speed, time);
                    }
                    #endif
                }
                #endif

                #if defined(_ENABLE_PLAYER_WAKE)
                    ApplyWaterWakeInteraction(worldPos, _WaveDirection.xz);
                #endif
                
                positionOS = TransformWorldToObject(float4(worldPos, 1.0)).xyz;
                
                float3 worldNormal = TransformObjectToWorldNormal(input.normalOS);
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(worldPos, worldNormal, _LightDirection));
                
                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                
                output.positionCS = positionCS;
                return output;
            }
            
            half4 ShadowFrag(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
        
        // Depth pass for depth buffer
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            
            ZWrite On
            ColorMask R
            Cull [_Cull]
            
            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing
            #pragma shader_feature_local _ENABLE_WAVES
            #pragma shader_feature_local _ENABLE_SECONDARY_WAVE
            #pragma shader_feature_local _ENABLE_PLAYER_WAKE
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "WaterWakeInteraction.hlsl"
            
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
                float2 _UVScrollSpeed;
                half _WaveAmplitude;
                half _WaveFrequency;
                half _WaveSpeed;
                float4 _WaveDirection;
                half _Wave2Amplitude;
                half _Wave2Frequency;
                half _Wave2Speed;
                float4 _Wave2Direction;
                half _Alpha;
                half _EdgeFadeDistance;
                half _Smoothness;
                half _SpecularStrength;
                half _Cutoff;
            CBUFFER_END
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            
            float3 GerstnerWaveSimple(float3 worldPos, float2 direction, float amplitude, float frequency, float speed, float time)
            {
                float2 dir = normalize(direction);
                float phase = dot(dir, worldPos.xz) * frequency + time * speed;
                float sinPhase = sin(phase);
                float cosPhase = cos(phase);
                
                float3 displacement;
                displacement.x = dir.x * amplitude * cosPhase;
                displacement.y = amplitude * sinPhase;
                displacement.z = dir.y * amplitude * cosPhase;
                
                return displacement;
            }
            
            Varyings DepthVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                
                float3 positionOS = input.positionOS.xyz;
                float3 worldPos = TransformObjectToWorld(positionOS);
                float time = _Time.y;
                
                #if defined(_ENABLE_WAVES)
                {
                    worldPos += GerstnerWaveSimple(worldPos, _WaveDirection.xz, _WaveAmplitude, _WaveFrequency, _WaveSpeed, time);
                    
                    #if defined(_ENABLE_SECONDARY_WAVE)
                    {
                        worldPos += GerstnerWaveSimple(worldPos, _Wave2Direction.xz, _Wave2Amplitude, _Wave2Frequency, _Wave2Speed, time);
                    }
                    #endif
                }
                #endif

                #if defined(_ENABLE_PLAYER_WAKE)
                    ApplyWaterWakeInteraction(worldPos, _WaveDirection.xz);
                #endif
                
                positionOS = TransformWorldToObject(float4(worldPos, 1.0)).xyz;
                output.positionCS = TransformObjectToHClip(positionOS);
                
                return output;
            }
            
            half4 DepthFrag(Varyings input) : SV_Target
            {
                return input.positionCS.z;
            }
            ENDHLSL
        }
    }
    
    FallBack "Universal Render Pipeline/Lit"
}

