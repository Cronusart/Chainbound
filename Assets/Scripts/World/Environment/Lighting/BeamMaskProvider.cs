using UnityEngine;

/// <summary>
/// Pushes beam transform data as global shader params so the 
/// dust particle shader can reconstruct the beam mask in world space.
/// </summary>
[DefaultExecutionOrder(-10)]
public class BeamMaskProvider : MonoBehaviour
{
    // Property IDs cached for performance
    static readonly int ID_Origin     = Shader.PropertyToID("_BeamOrigin");
    static readonly int ID_Forward    = Shader.PropertyToID("_BeamForward");
    static readonly int ID_Right      = Shader.PropertyToID("_BeamRight");
    static readonly int ID_Up         = Shader.PropertyToID("_BeamUp");
    static readonly int ID_HalfWidth  = Shader.PropertyToID("_BeamHalfWidth");
    static readonly int ID_HalfHeight = Shader.PropertyToID("_BeamHalfHeight");
    static readonly int ID_Length     = Shader.PropertyToID("_BeamLength");
    static readonly int ID_EdgeFeather    = Shader.PropertyToID("_DustEdgeFeather");
    static readonly int ID_DistanceFade   = Shader.PropertyToID("_DustDistanceFade");
    static readonly int ID_NoiseScale     = Shader.PropertyToID("_DustNoiseScale");
    static readonly int ID_NoiseSpeed     = Shader.PropertyToID("_DustNoiseSpeed");
    static readonly int ID_NoiseStrength  = Shader.PropertyToID("_DustNoiseStrength");
    static readonly int ID_DepthFadeDist  = Shader.PropertyToID("_DustDepthFadeDist");

    [Header("Linked controller for syncing params")]
    public WindowBeamController beamController;

    public void UpdateGlobals(Transform beamTransform, float halfWidth, float length)
    {
        Shader.SetGlobalVector(ID_Origin,     beamTransform.position);
        Shader.SetGlobalVector(ID_Forward,    beamTransform.forward);
        Shader.SetGlobalVector(ID_Right,      beamTransform.right);
        Shader.SetGlobalVector(ID_Up,         beamTransform.up);
        Shader.SetGlobalFloat (ID_HalfWidth,  halfWidth);
        Shader.SetGlobalFloat (ID_HalfHeight, beamController != null ? beamController.beamHeight * 0.5f : 1f);
        Shader.SetGlobalFloat (ID_Length,     length);

        if (beamController != null)
        {
            Shader.SetGlobalFloat (ID_EdgeFeather,   beamController.edgeFeather);
            Shader.SetGlobalFloat (ID_DistanceFade,  beamController.distanceFade);
            Shader.SetGlobalVector(ID_NoiseScale,   new Vector4(beamController.noiseScale.x, beamController.noiseScale.y, 0f, 0f));
            Shader.SetGlobalFloat (ID_NoiseSpeed,    beamController.noiseSpeed);
            Shader.SetGlobalFloat (ID_NoiseStrength, beamController.noiseStrength);
            Shader.SetGlobalFloat (ID_DepthFadeDist, beamController.depthFadeDistance);
        }
        else
        {
            Shader.SetGlobalFloat (ID_EdgeFeather,   0.25f);
            Shader.SetGlobalFloat (ID_DistanceFade,  2f);
            Shader.SetGlobalVector(ID_NoiseScale,    new Vector4(0.3f, 1.2f, 0f, 0f));
            Shader.SetGlobalFloat (ID_NoiseSpeed,    0.08f);
            Shader.SetGlobalFloat (ID_NoiseStrength,  0.35f);
            Shader.SetGlobalFloat (ID_DepthFadeDist,  0.4f);
        }
    }
}