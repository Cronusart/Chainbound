using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Persistent full-screen glitch veil used during encounter scene handoff.
/// Lives under GameServices and creates its own runtime UI scaffold so the bridge
/// can treat it as a reusable cross-scene transition primitive.
/// </summary>
[DisallowMultipleComponent]
public class EncounterTransitionGlitchOverlay : MonoBehaviour
{
    private const string DefaultShaderName = "SignalJam/UI/SignalGlitchOverlay";

    private static readonly int GlitchIntensityId = Shader.PropertyToID("_GlitchIntensity");
    private static readonly int DissolveThresholdId = Shader.PropertyToID("_DissolveThreshold");
    private static readonly int ScanlineAlphaId = Shader.PropertyToID("_ScanlineAlpha");

    public static EncounterTransitionGlitchOverlay Instance { get; private set; }

    [Header("Shader")]
    [SerializeField] private Shader glitchShader;
    [SerializeField] private Color tintColor = new Color(0.82f, 0.95f, 1f, 1f);

    [Header("Glitch Parameters")]
    [SerializeField] private float peakIntensity = 0.8f;
    [SerializeField] private float peakDissolve = 0.22f;
    [SerializeField] private float scanlineCount = 56f;
    [SerializeField] private float scanlineSpeed = 9f;
    [SerializeField] private float scanlineAlpha = 0.28f;
    [SerializeField] private float staticAmount = 0.5f;
    [SerializeField] private float sliceDisplacement = 0.035f;
    [SerializeField] private float sliceBandCount = 8f;

    [Header("Curves")]
    [SerializeField] private AnimationCurve alphaCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 0.68f);
    [SerializeField] private AnimationCurve intensityCurve = AnimationCurve.EaseInOut(0f, 0.1f, 1f, 1f);
    [SerializeField] private AnimationCurve dissolveCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Sorting")]
    [SerializeField] private int fallbackSortingOrder = 9999;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs;

    private Canvas overlayCanvas;
    private CanvasGroup overlayCanvasGroup;
    private RawImage overlayImage;
    private Material runtimeMaterial;
    private Coroutine playCoroutine;
    private GameObject runtimeCanvasRoot;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"[EncounterTransitionGlitchOverlay] Duplicate instance found on {gameObject.name}. Destroying duplicate.");
            Destroy(this);
            return;
        }

        Instance = this;
        EnsureOverlayScaffold();
        HideImmediate();
    }

    private void OnDestroy()
    {
        if (playCoroutine != null)
        {
            StopCoroutine(playCoroutine);
            playCoroutine = null;
        }

        if (runtimeMaterial != null)
        {
            Destroy(runtimeMaterial);
            runtimeMaterial = null;
        }

        if (Instance == this)
            Instance = null;
    }

    public void PlayForDuration(float durationSeconds)
    {
        PlayForDuration(durationSeconds, sortingOrderOverride: null);
    }

    /// <summary>
    /// Plays the glitch/scanline veil for a duration. Optionally forces canvas sorting
    /// (e.g. above a sprite cinematic overlay at 30000).
    /// </summary>
    public void PlayForDuration(float durationSeconds, int? sortingOrderOverride)
    {
        if (durationSeconds <= 0f)
        {
            HideImmediate();
            return;
        }

        if (!EnsureOverlayScaffold())
            return;

        if (playCoroutine != null)
        {
            StopCoroutine(playCoroutine);
        }

        if (sortingOrderOverride.HasValue && overlayCanvas != null)
            overlayCanvas.sortingOrder = sortingOrderOverride.Value;
        else
            RefreshSortingOrder();

        runtimeCanvasRoot.SetActive(true);
        playCoroutine = StartCoroutine(PlayRoutine(durationSeconds));
    }

    /// <summary>
    /// Ease-in scanline/glitch ramp that stays at peak until <see cref="HideImmediate"/>.
    /// Used by sprite-cinematic creep shots so overlay intensity tracks the flipbook.
    /// </summary>
    public void PlayCreepRamp(float durationSeconds, int? sortingOrderOverride)
    {
        if (durationSeconds <= 0f)
        {
            HideImmediate();
            return;
        }

        if (!EnsureOverlayScaffold())
            return;

        if (playCoroutine != null)
            StopCoroutine(playCoroutine);

        if (sortingOrderOverride.HasValue && overlayCanvas != null)
            overlayCanvas.sortingOrder = sortingOrderOverride.Value;
        else
            RefreshSortingOrder();

        runtimeCanvasRoot.SetActive(true);
        playCoroutine = StartCoroutine(PlayCreepRampRoutine(durationSeconds));
    }

    public void HideImmediate()
    {
        if (playCoroutine != null)
        {
            StopCoroutine(playCoroutine);
            playCoroutine = null;
        }

        if (overlayCanvasGroup != null)
        {
            overlayCanvasGroup.alpha = 0f;
            overlayCanvasGroup.blocksRaycasts = false;
            overlayCanvasGroup.interactable = false;
        }

        SetShaderState(0f, 0f);
        if (runtimeMaterial != null)
            runtimeMaterial.SetFloat(ScanlineAlphaId, 0f);

        if (runtimeCanvasRoot != null)
        {
            runtimeCanvasRoot.SetActive(false);
        }
    }

    private IEnumerator PlayRoutine(float durationSeconds)
    {
        if (overlayCanvasGroup == null)
            yield break;

        overlayCanvasGroup.blocksRaycasts = false;
        overlayCanvasGroup.interactable = false;

        float elapsed = 0f;
        while (elapsed < durationSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = durationSeconds > 0.0001f ? Mathf.Clamp01(elapsed / durationSeconds) : 1f;

            overlayCanvasGroup.alpha = Mathf.Clamp01(alphaCurve.Evaluate(t));
            float intensity = Mathf.Clamp01(intensityCurve.Evaluate(t)) * peakIntensity;
            float dissolve = Mathf.Clamp01(dissolveCurve.Evaluate(t)) * peakDissolve;
            SetShaderState(intensity, dissolve);

            yield return null;
        }

        overlayCanvasGroup.alpha = Mathf.Clamp01(alphaCurve.Evaluate(1f));
        SetShaderState(peakIntensity, peakDissolve);
        playCoroutine = null;
    }

    private IEnumerator PlayCreepRampRoutine(float durationSeconds)
    {
        if (overlayCanvasGroup == null)
            yield break;

        overlayCanvasGroup.blocksRaycasts = false;
        overlayCanvasGroup.interactable = false;

        float peakAlpha = 0.72f;
        float duration = Mathf.Max(0.01f, durationSeconds);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = t * t;
            ApplyCreepSample(eased, peakAlpha);
            yield return null;
        }

        ApplyCreepSample(1f, peakAlpha);
        playCoroutine = null;
    }

    private void ApplyCreepSample(float eased, float peakAlpha)
    {
        overlayCanvasGroup.alpha = Mathf.Clamp01(eased * peakAlpha);
        SetShaderState(eased * peakIntensity, eased * peakDissolve);
        if (runtimeMaterial != null)
            runtimeMaterial.SetFloat(ScanlineAlphaId, eased * scanlineAlpha);
    }

    private bool EnsureOverlayScaffold()
    {
        if (overlayCanvas != null && overlayCanvasGroup != null && overlayImage != null)
        {
            EnsureRuntimeMaterial();
            return overlayImage.material != null;
        }

        if (runtimeCanvasRoot == null)
        {
            runtimeCanvasRoot = new GameObject(
                "EncounterTransitionGlitchCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasGroup),
                typeof(RawImage));
            runtimeCanvasRoot.transform.SetParent(transform, false);
        }

        RectTransform rectTransform = runtimeCanvasRoot.GetComponent<RectTransform>();
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
        rectTransform.localScale = Vector3.one;
        rectTransform.localRotation = Quaternion.identity;

        overlayCanvas = runtimeCanvasRoot.GetComponent<Canvas>();
        overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        overlayCanvas.overrideSorting = true;

        overlayCanvasGroup = runtimeCanvasRoot.GetComponent<CanvasGroup>();
        overlayCanvasGroup.alpha = 0f;
        overlayCanvasGroup.blocksRaycasts = false;
        overlayCanvasGroup.interactable = false;

        overlayImage = runtimeCanvasRoot.GetComponent<RawImage>();
        overlayImage.texture = Texture2D.whiteTexture;
        overlayImage.color = tintColor;
        overlayImage.raycastTarget = false;

        RefreshSortingOrder();
        runtimeCanvasRoot.SetActive(false);

        EnsureRuntimeMaterial();
        return overlayImage.material != null;
    }

    private void RefreshSortingOrder()
    {
        if (overlayCanvas == null)
            return;

        int sortingOrder = fallbackSortingOrder;
        LoadingScreenUI loadingScreenUi = FindObjectOfType<LoadingScreenUI>(true);
        if (loadingScreenUi != null)
        {
            Canvas loadingCanvas = loadingScreenUi.GetComponent<Canvas>();
            if (loadingCanvas != null)
            {
                sortingOrder = loadingCanvas.sortingOrder - 1;
            }
        }

        overlayCanvas.sortingOrder = sortingOrder;
    }

    private void EnsureRuntimeMaterial()
    {
        if (overlayImage == null)
            return;

        Shader shaderToUse = glitchShader != null ? glitchShader : Shader.Find(DefaultShaderName);
        if (shaderToUse == null)
        {
            if (enableDebugLogs)
            {
                Debug.LogWarning("[EncounterTransitionGlitchOverlay] Glitch shader not found. Overlay will stay disabled.");
            }

            overlayImage.material = null;
            return;
        }

        if (runtimeMaterial == null || runtimeMaterial.shader != shaderToUse)
        {
            if (runtimeMaterial != null)
            {
                Destroy(runtimeMaterial);
            }

            runtimeMaterial = new Material(shaderToUse)
            {
                name = "EncounterTransitionGlitchOverlay (Runtime)"
            };

            runtimeMaterial.SetFloat("_ScanlineCount", scanlineCount);
            runtimeMaterial.SetFloat("_ScanlineSpeed", scanlineSpeed);
            runtimeMaterial.SetFloat("_ScanlineAlpha", scanlineAlpha);
            runtimeMaterial.SetFloat("_StaticAmount", staticAmount);
            runtimeMaterial.SetFloat("_SliceDisplacement", sliceDisplacement);
            runtimeMaterial.SetFloat("_SliceBandCount", sliceBandCount);
        }

        overlayImage.material = runtimeMaterial;
        overlayImage.color = tintColor;
        SetShaderState(0f, 0f);

        if (enableDebugLogs)
        {
            Debug.Log($"[EncounterTransitionGlitchOverlay] Ready with sorting order {overlayCanvas.sortingOrder}.");
        }
    }

    private void SetShaderState(float intensity, float dissolveThreshold)
    {
        if (runtimeMaterial == null)
            return;

        runtimeMaterial.SetFloat(GlitchIntensityId, intensity);
        runtimeMaterial.SetFloat(DissolveThresholdId, dissolveThreshold);
    }
}
