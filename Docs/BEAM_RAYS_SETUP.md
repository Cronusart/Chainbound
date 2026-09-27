# BeamRays Shader Setup

The `BeamRays.shader` (`Assets/Sprites/Shaders/BeamRays.shader`) implements a volumetric light-beam effect with edge feathering, distance fade, noise striation, and depth-based fade.

## Creating the Material

1. In the Project window, right-click → **Create** → **Material**
2. Name it (e.g. `M_BeamRays`)
3. In the Inspector, click the **Shader** dropdown
4. Select **SignalJam** → **Sprites** → **BeamRays**
5. Assign this material to your quad's **MeshRenderer** component

## URP Requirements

1. **Depth Texture**: Enable "Depth Texture" in your URP Asset (Universal Render Pipeline Asset → General → Depth Texture). Without this, the depth fade will not work correctly.

2. **Opaque rendering**: The beam fades based on scene depth. Ensure opaque geometry is rendered before transparent objects (default).

## Properties

| Property | Description |
|----------|-------------|
| **Beam Color** | Base color of the beam |
| **Intensity** | Brightness multiplier |
| **Edge Feather** | Softness of left/right edges (U axis) |
| **Distance Fade Power** | Falloff along beam length (V axis). Higher = steeper fade |
| **Noise Scale** | X = horizontal stretch, Y = vertical stretch (larger Y = elongated bands) |
| **Noise Speed** | Animation speed of noise over time |
| **Noise Strength** | Blend between solid (0) and full noise (1) |
| **Depth Fade Distance** | Distance over which beam fades when occluded by scene geometry |

## Mesh UV Layout

- **U (0→1)**: Across the beam (left to right). Edge mask softens both sides.
- **V (0→1)**: Along the beam. V=0 at window/source, V=1 at far end. Distance mask fades toward V=1.

## Shader Graph Equivalent

This shader was authored as HLSL to match a Shader Graph design. The logic maps 1:1 to the node graph described in the spec.
