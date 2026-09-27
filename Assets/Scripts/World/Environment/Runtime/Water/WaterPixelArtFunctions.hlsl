#ifndef WATER_PIXEL_ART_FUNCTIONS_INCLUDED
#define WATER_PIXEL_ART_FUNCTIONS_INCLUDED

// ============================================
// Pixel-Art Water Shader Functions
// For use with URP Shader Graph
// ============================================

// ============================================
// PIXEL SNAPPING
// ============================================
// Snaps UV coordinates to pixel grid to preserve pixel-art aesthetic
// snapScale: 0 = no snapping, 1 = full snapping
// textureSize: Size of the texture (e.g., float2(256, 256))
// Main implementation
float2 PixelSnapUV(float2 uv, float snapScale, float2 textureSize)
{
    if (snapScale < 0.001) return uv;

    float2 snapped = floor(uv * textureSize * snapScale) / (textureSize * snapScale);
    return lerp(uv, snapped, snapScale);
}

// Shader Graph wrapper (Single precision)
void PixelSnapUV_float(float2 uv, float snapScale, float2 textureSize, out float2 Out)
{
    Out = PixelSnapUV(uv, snapScale, textureSize);
}

// ============================================
// SHORELINE GRADIENT
// ============================================
// Calculates shallow-to-deep water gradient
// vertexColorMask: Red channel from vertex colors (0 = deep, 1 = shallow)
// textureMask: Sampled from _ShoreMaskTex (0 = deep, 1 = shallow)
// useVertexColor: Toggle between vertex color and texture mask
// Returns: 0 = deep water, 1 = shallow water
// If you already have a return-style implementation, keep it:
float ShorelineGradient(float vertexColorMask, float textureMask, float useVertexColor)
{
    // Typical logic: choose vertex or texture based on toggle
    // If you already have this in your file, do NOT duplicate; just keep one.
    return lerp(textureMask, vertexColorMask, saturate(useVertexColor));
}

// Shader Graph wrapper (what SG is actually calling)
void ShorelineGradient_float(float vertexColorMask, float textureMask, float useVertexColor, out float Out)
{
    Out = ShorelineGradient(vertexColorMask, textureMask, useVertexColor);
}

// Blends shallow and deep colors based on shoreline mask
// shallowColor: Color for shallow water (near shore)
// deepColor: Color for deep water (far from shore)
// mask: Shoreline mask (0 = deep, 1 = shallow)
// blendPower: Controls gradient sharpness (1 = linear, >1 = sharper)
half3 BlendShorelineColor(half3 shallowColor, half3 deepColor, float mask, float blendPower)
{
    float t = pow(saturate(mask), blendPower);
    return lerp(deepColor, shallowColor, t);
}

// ============================================
// STYLIZED CAUSTICS
// ============================================
// Generates moving caustic light pattern
// causticsSample: Sampled caustics texture (grayscale or color)
// strength: Intensity of caustics (0-1)
// maskToShallow: If > 0, masks caustics to shallow areas
// shorelineMask: Shoreline mask (0 = deep, 1 = shallow)
// Returns: Caustics contribution to add to water color
half3 StylizedCaustics(half3 causticsSample, float strength, float maskToShallow, float shorelineMask)
{
    half3 caustics = causticsSample * strength;
    
    // If masking to shallow, reduce caustics in deep water
    if (maskToShallow > 0.001)
    {
        float shallowMask = pow(shorelineMask, 0.5); // Slight bias toward shallow
        caustics *= lerp(1.0, shallowMask, maskToShallow);
    }
    
    return caustics;
}

// ============================================
// SHORE FOAM
// ============================================
// Calculates foam band near shoreline
// shorelineMask: Shoreline mask (0 = deep, 1 = shallow)
// foamWidth: Width of foam band (0-1, where 1 = full shallow area)
// foamSpeed: Animation speed
// time: Current time (_Time.y)
// Returns: Foam intensity (0-1)
float ShoreFoam(float shorelineMask, float foamWidth, float foamSpeed, float time)
{
    // Create foam band at the edge of shallow water
    float foamEdge = 1.0 - foamWidth;
    float foamMask = smoothstep(foamEdge, 1.0, shorelineMask);
    
    // Animate foam with a simple wave pattern
    float foamWave = sin(time * foamSpeed + shorelineMask * 10.0) * 0.5 + 0.5;
    foamMask *= foamWave;
    
    return foamMask;
}

// ============================================
// ORDERED DITHERING
// ============================================
// Pixel-art friendly dithering to reduce banding in gradients
// Based on Bayer 4x4 dither matrix
// position: Screen-space position (can use UV or screen position)
// Returns: Dither offset (-0.5 to 0.5)
float OrderedDither(float2 position)
{
    // 4x4 Bayer dither matrix
    const float4x4 ditherMatrix = {
        0.0 / 16.0,  8.0 / 16.0,  2.0 / 16.0, 10.0 / 16.0,
        12.0 / 16.0, 4.0 / 16.0, 14.0 / 16.0,  6.0 / 16.0,
        3.0 / 16.0, 11.0 / 16.0,  1.0 / 16.0,  9.0 / 16.0,
        15.0 / 16.0, 7.0 / 16.0, 13.0 / 16.0,  5.0 / 16.0
    };
    
    int x = (int(position.x) % 4);
    int y = (int(position.y) % 4);
    
    float ditherValue = ditherMatrix[y][x];
    return (ditherValue - 0.5) * 0.5; // Scale to -0.25 to 0.25
}

// Apply dithering to a color value
half3 ApplyDither(half3 color, float2 position, float strength)
{
    float dither = OrderedDither(position) * strength;
    return color + dither;
}

// ============================================
// STYLIZED HIGHLIGHTS
// ============================================
// Generates non-PBR sparkle/highlight effect
// noiseSample: Sampled noise texture or procedural noise
// highlightColor: Color of highlights
// strength: Intensity (0-1)
// Returns: Highlight contribution
half3 StylizedHighlights(float noiseSample, half3 highlightColor, float strength)
{
    // Create sparkle pattern from noise
    float sparkle = step(0.85, noiseSample); // Threshold for sparkles
    return highlightColor * sparkle * strength;
}

// ============================================
// WAVE DISTORTION
// ============================================
// Applies subtle UV distortion for wave motion
// noiseSample: Sampled noise texture (2D, can use red/green channels)
// strength: Distortion strength
// Returns: UV offset to add
float2 WaveDistortion(float2 noiseSample, float strength)
{
    // Convert noise from 0-1 to -1 to 1
    float2 offset = (noiseSample - 0.5) * 2.0;
    return offset * strength;
}

#endif // WATER_PIXEL_ART_FUNCTIONS_INCLUDED

