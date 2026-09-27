Shader "Environment/ServerLedFlicker_AlphaClip"
{
    // Pixel-perfect LED flicker shader for server rack overlays.
    // Handles irregular row spacing via explicit Y position mapping.
    // Uses deterministic per-cell random for consistent flicker across frames.
    // Compatible with URP (Universal Render Pipeline).
    //
    // USAGE:
    // - Apply to quad overlay mesh sitting in front of "lights OFF" base texture
    // - Overlay texture has transparent background with LED pixels visible
    // - Each instance uses MaterialPropertyBlock for unique _Seed
    //
    // GRID LAYOUT (default for 23x27 texture):
    // - 17 columns x 15 logical rows
    // - First LED at pixel (4, 4) from bottom-left (UV origin)
    // - Horizontal: 1px LED, 1px gap pattern
    // - Vertical: 4 bands with irregular spacing
    
    Properties
    {
        [Header(Textures)]
        _MaskTex ("LED Mask Texture", 2D) = "white" {}
        
        [Header(Appearance)]
        _Tint ("LED Tint Color", Color) = (0.0, 1.0, 0.85, 1.0)
        _Emission ("Emission Strength", Range(0, 5)) = 1.5
        _Clip ("Alpha Clip Threshold", Range(0, 1)) = 0.5
        
        [Header(Animation)]
        _Speed ("Blink Speed", Range(0.1, 20)) = 4.0
        _Density ("LED On Probability", Range(0, 1)) = 0.45
        _Seed ("Random Seed (per-instance)", Float) = 0.0
        
        [Header(Grid Layout)]
        _TexSize ("Texture Size (pixels)", Vector) = (23, 27, 0, 0)
        _Cols ("LED Columns", Float) = 17
        _Rows ("LED Rows (logical)", Float) = 15
        _LedStartX ("First LED X (pixels)", Float) = 4
        _LedSizePx ("LED Size (pixels)", Float) = 1
        _LedGapPx ("LED Gap (pixels)", Float) = 1
        
        [Header(Fake Glow)]
        [Toggle] _UseFakeGlow ("Enable Fake Glow", Float) = 0
        _GlowStrength ("Glow Strength", Range(0, 1)) = 0.3
        _GlowSpread ("Glow Spread (pixels)", Range(0.5, 3)) = 1.0
    }
    
    SubShader
    {
        Tags 
        { 
            "Queue"="Transparent"
            "RenderType"="TransparentCutout" 
            "RenderPipeline"="UniversalPipeline"
            "IgnoreProjector"="True"
            "PreviewType"="Plane"
        }
        
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        ZTest LEqual
        
        Pass
        {
            Name "ServerLedFlicker"
            Tags { "LightMode"="UniversalForward" }
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            
            // ============================================
            // STRUCTS
            // ============================================
            
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
                half fogFactor : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            
            // ============================================
            // TEXTURES & SAMPLERS
            // ============================================
            
            TEXTURE2D(_MaskTex);
            SAMPLER(sampler_MaskTex);
            
            // ============================================
            // CBUFFER - SRP Batcher compatible
            // ============================================
            
            CBUFFER_START(UnityPerMaterial)
                float4 _MaskTex_ST;
                half4 _Tint;
                half _Emission;
                half _Clip;
                half _Speed;
                half _Density;
                float _Seed;
                float4 _TexSize;
                float _Cols;
                float _Rows;
                float _LedStartX;
                float _LedSizePx;
                float _LedGapPx;
                half _UseFakeGlow;
                half _GlowStrength;
                half _GlowSpread;
            CBUFFER_END
            
            // ============================================
            // ROW Y POSITIONS (from bottom, in pixels)
            // Encoded as constants for 15 logical rows in 4 bands
            // Band 1 (bottom): rows 0-3
            // Band 2: rows 4-7  
            // Band 3: rows 8-11
            // Band 4 (top): rows 12-14
            // ============================================
            
            // Default row Y positions for 27-pixel tall texture
            // These map logical row index to pixel Y coordinate (from bottom)
            // Adjust via script if your texture differs
            static const float ROW_Y_POSITIONS[15] = {
                // Band 1 (bottom) - rows 0,1,2,3 - 1px spacing
                4.0, 6.0, 8.0, 10.0,
                // Band 2 - rows 4,5,6,7 - after 5px gap from band 1
                16.0, 18.0, 20.0, 22.0,
                // Band 3 - rows 8,9,10,11 - pattern continues (adjust as needed)
                // For textures with only 2 LED bands at top, these may be unused
                0.0, 0.0, 0.0, 0.0,
                // Band 4 (top) - rows 12,13,14
                0.0, 0.0, 0.0
            };
            
            // ============================================
            // HASH FUNCTIONS (GPU-friendly, deterministic)
            // ============================================
            
            // Fast integer hash for grid-based random
            float Hash3(float3 p)
            {
                p = frac(p * float3(443.8975, 397.2973, 491.1871));
                p += dot(p.zxy, p.yxz + 19.19);
                return frac(p.x * p.y * p.z);
            }
            
            // 2D hash for cell-based flickering
            float HashCell(int col, int row, int timeStep, float seed)
            {
                float3 p = float3(col + 0.5, row + 0.5, timeStep + seed * 127.1);
                return Hash3(p);
            }
            
            // ============================================
            // GRID MAPPING FUNCTIONS
            // ============================================
            
            // Convert pixel X to column index (-1 if not on LED)
            int GetColumnFromPixelX(int px, float startX, float ledSize, float gap)
            {
                float cellWidth = ledSize + gap;
                float localX = px - startX;
                
                if (localX < 0) return -1;
                
                int col = (int)floor(localX / cellWidth);
                float withinCell = localX - col * cellWidth;
                
                // Check if within LED portion (not in gap)
                if (withinCell < ledSize)
                {
                    return col;
                }
                return -1; // In gap
            }
            
            // Get logical row from pixel Y coordinate
            // Uses distance-to-center approach for irregular spacing
            int GetRowFromPixelY(int py, int numRows)
            {
                int bestRow = -1;
                float bestDist = 999.0;
                
                // Find closest row center
                for (int i = 0; i < 15; i++)
                {
                    if (i >= numRows) break;
                    float rowY = ROW_Y_POSITIONS[i];
                    if (rowY <= 0.001) continue; // Skip disabled rows
                    
                    float dist = abs(py - rowY);
                    if (dist < bestDist && dist <= 1.5) // Within 1.5px of center
                    {
                        bestDist = dist;
                        bestRow = i;
                    }
                }
                
                return bestRow;
            }
            
            // ============================================
            // VERTEX SHADER
            // ============================================
            
            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _MaskTex);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                
                return output;
            }
            
            // ============================================
            // FRAGMENT SHADER
            // ============================================
            
            half4 frag(Varyings input) : SV_Target
            {
                // Sample mask texture
                half4 maskSample = SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, input.uv);
                half maskAlpha = maskSample.a;
                
                // Early out for transparent pixels (background)
                if (maskAlpha < _Clip)
                {
                    discard;
                }
                
                // Convert UV to pixel coordinates
                float2 texSize = _TexSize.xy;
                float2 pixelCoord = input.uv * texSize;
                int px = (int)floor(pixelCoord.x);
                int py = (int)floor(pixelCoord.y);
                
                // Determine grid cell
                int col = GetColumnFromPixelX(px, _LedStartX, _LedSizePx, _LedGapPx);
                int row = GetRowFromPixelY(py, (int)_Rows);
                
                // Default: LED is on if we have valid mask alpha
                float ledOn = 1.0;
                
                // Only apply flicker logic if we're on a valid grid cell
                if (col >= 0 && col < (int)_Cols && row >= 0)
                {
                    // Compute time step for flicker
                    int timeStep = (int)floor(_Time.y * _Speed);
                    
                    // Deterministic random per cell
                    float rand = HashCell(col, row, timeStep, _Seed);
                    
                    // LED on/off based on density threshold
                    ledOn = (rand < _Density) ? 1.0 : 0.0;
                }
                
                // Calculate base LED color
                half3 ledColor = maskSample.rgb * _Tint.rgb;
                
                // Apply flicker
                half3 finalColor = ledColor * ledOn;
                half finalAlpha = maskAlpha * ledOn;
                
                // ============================================
                // FAKE GLOW (optional)
                // ============================================
                
                if (_UseFakeGlow > 0.5 && ledOn > 0.5)
                {
                    // Sample neighbors for glow spread
                    float2 pixelSize = 1.0 / texSize;
                    float2 spreadOffset = _GlowSpread * pixelSize;
                    
                    // Sample 4 neighbors
                    half alphaL = SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, input.uv + float2(-spreadOffset.x, 0)).a;
                    half alphaR = SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, input.uv + float2(spreadOffset.x, 0)).a;
                    half alphaU = SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, input.uv + float2(0, spreadOffset.y)).a;
                    half alphaD = SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, input.uv + float2(0, -spreadOffset.y)).a;
                    
                    // Expand mask slightly
                    half glowMask = max(max(alphaL, alphaR), max(alphaU, alphaD));
                    
                    // Only add glow outside core LED pixels
                    half glowContrib = glowMask * (1.0 - maskAlpha) * _GlowStrength;
                    
                    finalColor += _Tint.rgb * glowContrib * _Emission * 0.5;
                    finalAlpha = max(finalAlpha, glowContrib * 0.5);
                }
                
                // Apply emission
                finalColor *= (1.0 + (_Emission - 1.0) * ledOn);
                
                // Alpha clip for crisp edges
                clip(finalAlpha - _Clip);
                
                // Apply fog
                finalColor = MixFog(finalColor, input.fogFactor);
                
                return half4(finalColor, finalAlpha);
            }
            ENDHLSL
        }
        
        // ============================================
        // DEPTH ONLY PASS
        // ============================================
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            
            ZWrite On
            ColorMask 0
            Cull Off
            
            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            
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
            
            TEXTURE2D(_MaskTex);
            SAMPLER(sampler_MaskTex);
            
            CBUFFER_START(UnityPerMaterial)
                float4 _MaskTex_ST;
                half _Clip;
            CBUFFER_END
            
            Varyings DepthVert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _MaskTex);
                return output;
            }
            
            half4 DepthFrag(Varyings input) : SV_Target
            {
                half alpha = SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, input.uv).a;
                clip(alpha - _Clip);
                return 0;
            }
            ENDHLSL
        }
    }
    
    FallBack "Sprites/Default"
}
