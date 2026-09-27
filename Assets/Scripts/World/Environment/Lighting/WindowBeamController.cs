using UnityEngine;

[ExecuteAlways]
public class WindowBeamController : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Assign one or more beam quad MeshRenderers (e.g. top + camera-facing).")]
    public MeshRenderer[] beamRenderers;
    [Tooltip("Legacy: single beam. Used if beamRenderers is empty.")]
    public MeshRenderer beamRenderer;
    public Transform windowTransform;

    [Header("Visibility")]
    [Tooltip("When false, all beams and dust are hidden. Ignored when Use Time Of Day is enabled.")]
    public bool beamsVisible = true;

    [Header("Time of Day (Optional)")]
    [Tooltip("When enabled, beam visibility is driven by time-of-day. Leave disabled for beams that persist (e.g. outdoor ambient).")]
    public bool useTimeOfDay = false;
    [Tooltip("Time-of-day manager. Uses TimeOfDayManager.Instance if null.")]
    public TimeOfDayManager timeOfDayManager;
    [Tooltip("Normalized time (0–1) when beams start being visible. Default 0.25 ≈ dawn.")]
    [Range(0f, 1f)] public float visibleStartTime = 0.25f;
    [Tooltip("Normalized time (0–1) when beams stop being visible. Default 0.75 ≈ dusk.")]
    [Range(0f, 1f)] public float visibleEndTime = 0.75f;
    [Tooltip("Duration in seconds for fade in/out transitions.")]
    [Min(0.1f)] public float fadeDurationSeconds = 30f;

    /// <summary>Call from time-of-day or other systems to show/hide beams. Ignored when Use Time Of Day is enabled.</summary>
    public void SetBeamsVisible(bool visible)
    {
        beamsVisible = visible;
        if (!useTimeOfDay)
            ApplyVisibility();
    }

    [Header("Window Lock")]
    [Tooltip("When the controller is on the window: lock position so physics/gravity cannot move it.")]
    public bool lockWindowPosition = true;

    [Header("Beam Shape")]
    public float beamLength = 4f;
    public float beamWidth  = 2f;
    public float beamHeight = 1.8f;

    [Header("Beam Params")]
    [ColorUsage(true, true)]
    public Color beamColor = new Color(1f, 0.85f, 0.5f, 1f);
    [Range(0f, 5f)] public float intensity = 1.5f;
    [Range(0f, 1f)] public float edgeFeather = 0.25f;
    [Range(0.5f, 6f)] public float distanceFade = 2f;
    public Vector2 noiseScale = new Vector2(0.3f, 1.2f);
    [Range(0f, 0.5f)] public float noiseSpeed = 0.08f;
    [Range(0f, 1f)] public float noiseStrength = 0.35f;
    [Range(0.05f, 2f)] public float depthFadeDistance = 0.4f;

    [Header("Dust Particle System (optional)")]
    public ParticleSystem dustParticles;
    public BeamMaskProvider maskProvider;

    private MaterialPropertyBlock _mpb;
    private float _currentBeamAlpha = 1f;
    private bool _timeOfDayInitialized;

    void OnEnable()
    {
        Apply();
        ApplyWindowLock();
    }

    TimeOfDayManager GetTimeOfDayManager() => timeOfDayManager != null ? timeOfDayManager : TimeOfDayManager.Instance;

    void OnValidate()
    {
        Apply();
        ApplyWindowLock();
    }

    void ApplyWindowLock()
    {
        if (!lockWindowPosition) return;

        Transform target = windowTransform != null ? windowTransform : transform;
        var rb = target.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.constraints = RigidbodyConstraints.FreezePosition;
        }
    }

    void Update()
    {
        if (windowTransform != null)
            transform.position = windowTransform.position;

        if (useTimeOfDay)
            UpdateTimeOfDayAlpha();
        else
            _timeOfDayInitialized = false;

        ApplyVisibility();
        ApplyMPB();
        UpdateParticleShape();
    }

    void UpdateTimeOfDayAlpha()
    {
        var manager = GetTimeOfDayManager();
        if (manager == null) return;

        float t = manager.NormalizedTime;
        float target = (t >= visibleStartTime && t < visibleEndTime) ? 1f : 0f;

        if (!_timeOfDayInitialized)
        {
            _currentBeamAlpha = target;
            _timeOfDayInitialized = true;
            return;
        }

        float fadeSpeed = fadeDurationSeconds > 0 ? 1f / fadeDurationSeconds : 10f;
        _currentBeamAlpha = Mathf.MoveTowards(_currentBeamAlpha, target, fadeSpeed * Time.deltaTime);
    }

    void Apply()
    {
        if (!HasAnyBeam()) return;
        transform.localScale = new Vector3(beamWidth, beamHeight, beamLength);
        ApplyVisibility();
        ApplyMPB();
        UpdateParticleShape();
    }

    bool HasAnyBeam()
    {
        if (beamRenderers != null && beamRenderers.Length > 0)
        {
            foreach (var r in beamRenderers)
                if (r != null) return true;
        }
        return beamRenderer != null;
    }

    float GetEffectiveBeamAlpha()
    {
        if (useTimeOfDay)
            return _currentBeamAlpha;
        return beamsVisible ? 1f : 0f;
    }

    void ApplyVisibility()
    {
        float alpha = GetEffectiveBeamAlpha();
        bool visible = alpha > 0.01f;

        foreach (var r in GetBeamRenderers())
        {
            if (r != null && r.enabled != visible)
                r.enabled = visible;
        }
        if (dustParticles != null)
        {
            var emission = dustParticles.emission;
            emission.enabled = visible;
        }
    }

    System.Collections.Generic.IEnumerable<MeshRenderer> GetBeamRenderers()
    {
        if (beamRenderers != null && beamRenderers.Length > 0)
        {
            foreach (var r in beamRenderers)
                if (r != null) yield return r;
        }
        else if (beamRenderer != null)
        {
            yield return beamRenderer;
        }
    }

    void ApplyMPB()
    {
        if (!HasAnyBeam()) return;
        if (_mpb == null) _mpb = new MaterialPropertyBlock();

        float alpha = GetEffectiveBeamAlpha();
        Color colorWithAlpha = new Color(beamColor.r, beamColor.g, beamColor.b, beamColor.a * alpha);
        _mpb.SetColor("_BeamColor", colorWithAlpha);
        _mpb.SetFloat("_Intensity", intensity);
        _mpb.SetFloat("_EdgeFeather", edgeFeather);
        _mpb.SetFloat("_DistanceFade", distanceFade);
        _mpb.SetVector("_NoiseScale", noiseScale);
        _mpb.SetFloat("_NoiseSpeed", noiseSpeed);
        _mpb.SetFloat("_NoiseStrength", noiseStrength);
        _mpb.SetFloat("_DepthFadeDistance", depthFadeDistance);

        foreach (var r in GetBeamRenderers())
            r.SetPropertyBlock(_mpb);

        if (maskProvider != null)
            maskProvider.UpdateGlobals(transform, beamWidth * 0.5f, beamLength);
    }

    void UpdateParticleShape()
    {
        if (dustParticles == null) return;
        var shape = dustParticles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(beamWidth * 0.85f, beamHeight * 0.85f, beamLength * 0.85f);
    }
}