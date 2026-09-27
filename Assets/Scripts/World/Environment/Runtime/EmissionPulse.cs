using UnityEngine;

[DisallowMultipleComponent]
public sealed class EmissionPulse : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("Renderer whose emission will be pulsed. If null, uses Renderer on this GameObject.")]
    [SerializeField] private Renderer targetRenderer;

    [Tooltip("Material emission color property name (usually _EmissionColor).")]
    [SerializeField] private string emissionProperty = "_EmissionColor";

    [Header("Pulse Settings")]
    [Tooltip("Base emission color (before intensity multiplier).")]
    [SerializeField] private Color emissionColor = Color.cyan;

    [Tooltip("Minimum emission intensity.")]
    [SerializeField] private float minIntensity = 0.6f;

    [Tooltip("Maximum emission intensity.")]
    [SerializeField] private float maxIntensity = 1.4f;

    [Tooltip("Pulse cycles per second. 0.25 = one pulse every 4 seconds.")]
    [SerializeField] private float pulseSpeed = 0.25f;

    [Tooltip("Ease the pulse for a softer mystical feel.")]
    [SerializeField] private bool useSmoothEase = true;

    [Header("Timing")]
    [Tooltip("Use unscaled time (keeps pulsing during Time.timeScale = 0).")]
    [SerializeField] private bool useUnscaledTime = false;

    [Tooltip("Randomize phase so multiple objects don't sync.")]
    [SerializeField] private bool randomizePhase = true;

    private MaterialPropertyBlock _mpb;
    private int _emissionID;
    private float _phase;
    private bool _ready;

    private void Awake()
    {
        EnsureReady();
        if (randomizePhase)
            _phase = Random.value * Mathf.PI * 2f;
    }

    private void Update()
    {
        if (!EnsureReady())
            return;

        float t = useUnscaledTime ? Time.unscaledTime : Time.time;
        float wave = Mathf.Sin((t * pulseSpeed * Mathf.PI * 2f) + _phase); // -1..1
        float normalized = (wave + 1f) * 0.5f;                             // 0..1

        if (useSmoothEase)
            normalized = Mathf.SmoothStep(0f, 1f, normalized);

        float intensity = Mathf.Lerp(minIntensity, maxIntensity, normalized);
        ApplyEmission(emissionColor * intensity);
    }

    /// <summary>
    /// Stops the idle pulse. Pass <paramref name="active"/> false to freeze emission
    /// at the last <see cref="SetEmissionAmount"/> write (typically zero for a matte hold).
    /// </summary>
    public void SetPulseActive(bool active)
    {
        if (!active)
            SetEmissionAmount(0f);
        enabled = active;
    }

    /// <summary>
    /// Sets emission as a 0–1 amount of the authored HDR color at max intensity.
    /// Uses a renderer property block so the shared material stays unchanged.
    /// </summary>
    public void SetEmissionAmount(float amount01)
    {
        if (!EnsureReady())
            return;

        float intensity = Mathf.Lerp(0f, maxIntensity, Mathf.Clamp01(amount01));
        ApplyEmission(emissionColor * intensity);
    }

    private bool EnsureReady()
    {
        if (_ready && targetRenderer != null && _mpb != null)
            return true;

        if (targetRenderer == null)
            targetRenderer = GetComponent<Renderer>();
        if (targetRenderer == null)
            return false;

        _mpb ??= new MaterialPropertyBlock();
        _emissionID = Shader.PropertyToID(emissionProperty);
        _ready = true;
        return true;
    }

    private void ApplyEmission(Color emission)
    {
        targetRenderer.GetPropertyBlock(_mpb);
        _mpb.SetColor(_emissionID, emission);
        targetRenderer.SetPropertyBlock(_mpb);
    }
}
