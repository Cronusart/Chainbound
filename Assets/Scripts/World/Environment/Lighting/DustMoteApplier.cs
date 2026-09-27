using UnityEngine;

/// <summary>
/// Attaches to the dust ParticleSystem GameObject.
/// Ensures the particle renderer uses a material that reads
/// the global beam mask params set by BeamMaskProvider.
/// Also drives per-renderer properties if needed.
/// </summary>
[RequireComponent(typeof(ParticleSystemRenderer))]
public class DustMoteMaskApplier : MonoBehaviour
{
    public Material dustMaterial; // Assign M_DustMote_Masked

    ParticleSystemRenderer _psr;

    void Awake()
    {
        _psr = GetComponent<ParticleSystemRenderer>();
        if (_psr == null) return;

        if (dustMaterial != null)
            _psr.material = dustMaterial;
        else
            Debug.LogWarning("[DustMoteMaskApplier] No dust material assigned. Assign M_DustMote_Masked.", this);
    }

    // Nothing else needed — BeamMaskProvider pushes globals every frame.
    // The shader reads _BeamOrigin/_BeamForward/etc. as global properties.
}
