using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Drives post-processing effects based on time-of-day.
/// Works with URP Volume system. Modifies exposure, color grading, etc.
/// 
/// Setup:
/// 1. Attach to a manager object or the Global Volume
/// 2. Assign a Volume reference (auto-finds if attached to Volume)
/// 3. Assign a TimeOfDayManager reference (or uses singleton)
/// 4. Assign a TimeOfDayConfigSO
/// 
/// Note: This driver works with URP. For HDRP, adjustments may be needed.
/// </summary>
[ExecuteAlways]
public class PostFXTimeDriver : MonoBehaviour
{
    [Header("References")]
    [Tooltip("URP Volume to control. Auto-finds global volume if null.")]
    [SerializeField] private Volume targetVolume;
    
    [Tooltip("TimeOfDayManager reference. Uses singleton if null.")]
    [SerializeField] private TimeOfDayManager timeManager;
    
    [Tooltip("Configuration. Uses manager's config if null.")]
    [SerializeField] private TimeOfDayConfigSO config;
    
    [Header("Features")]
    [Tooltip("Control color adjustments (color filter/tint)")]
    [SerializeField] private bool controlColorFilter = true;
    
    [Tooltip("Control exposure (requires Color Adjustments override)")]
    [SerializeField] private bool controlExposure = true;
    
    [Tooltip("Control saturation at night")]
    [SerializeField] private bool controlSaturation = false;
    
    [Tooltip("Control contrast")]
    [SerializeField] private bool controlContrast = false;
    
    [Header("Night Settings")]
    [Tooltip("Saturation multiplier at night (1 = normal, 0 = grayscale)")]
    [Range(0f, 2f)]
    [SerializeField] private float nightSaturation = 0.8f;
    
    [Tooltip("Contrast adjustment at night (-100 to 100)")]
    [Range(-100f, 100f)]
    [SerializeField] private float nightContrast = 10f;
    
    [Header("Vignette (Optional)")]
    [Tooltip("Control vignette intensity at night")]
    [SerializeField] private bool controlVignette = false;
    
    [Tooltip("Vignette intensity at night")]
    [Range(0f, 1f)]
    [SerializeField] private float nightVignetteIntensity = 0.3f;
    
    [Header("Depth of Field (Optional)")]
    [Tooltip("Control depth of field at night")]
    [SerializeField] private bool controlDepthOfField = false;
    
    [Tooltip("Night DoF focal length")]
    [SerializeField] private float nightFocalLength = 50f;
    
    [Header("Bloom (Optional)")]
    [Tooltip("Control bloom intensity based on time")]
    [SerializeField] private bool controlBloom = false;
    
    [Tooltip("Bloom intensity multiplier at night")]
    [Range(0f, 3f)]
    [SerializeField] private float nightBloomMultiplier = 1.5f;
    
    [Header("Volume Blending (Alternative)")]
    [Tooltip("Use separate day/night volume profiles instead of individual settings")]
    [SerializeField] private bool useVolumeBlending = false;
    
    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;
    
    // Cached components
    private VolumeProfile workingProfile;
    private RuntimeVolumeProfileHandle volumeProfileHandle;
    private float lastAppliedTime = -1f;
    private bool isSubscribed = false;
    
    // Cached default values (for blending)
    private float defaultExposure = 0f;
    private float defaultSaturation = 0f;
    private float defaultContrast = 0f;
    private float defaultVignette = 0f;
    private float defaultBloom = 1f;
    
    private void OnEnable()
    {
        // Auto-find volume
        if (targetVolume == null)
        {
            targetVolume = GetComponent<Volume>();
            if (targetVolume == null)
            {
                // Find global volume in scene
                Volume[] volumes = FindObjectsOfType<Volume>();
                foreach (var vol in volumes)
                {
                    if (vol.isGlobal)
                    {
                        targetVolume = vol;
                        break;
                    }
                }
            }
        }
        
        // Auto-find time manager
        if (timeManager == null)
        {
            timeManager = TimeOfDayManager.Instance;
        }
        
        // Auto-get config
        if (config == null && timeManager != null)
        {
            config = timeManager.Config;
        }
        
        // Cache authored defaults from sharedProfile so Play Mode overrides cannot drift.
        CacheDefaultValues();

        if (Application.isPlaying)
        {
            Subscribe();
            if (timeManager != null)
                ApplyTimeOfDay(timeManager.NormalizedTime);
            return;
        }

        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
        ReleaseVolumeProfileHandle();
        workingProfile = null;
    }
    
    private void Subscribe()
    {
        if (isSubscribed || timeManager == null) return;
        
        timeManager.OnTimeNormalizedChanged += ApplyTimeOfDay;
        isSubscribed = true;
    }
    
    private void Unsubscribe()
    {
        if (!isSubscribed || timeManager == null) return;
        
        timeManager.OnTimeNormalizedChanged -= ApplyTimeOfDay;
        isSubscribed = false;
    }
    
    private void CacheDefaultValues()
    {
        VolumeProfile authored = targetVolume != null ? targetVolume.sharedProfile : null;
        if (authored == null)
            return;

        if (authored.TryGet(out ColorAdjustments colorAdj))
        {
            defaultExposure = colorAdj.postExposure.value;
            defaultSaturation = colorAdj.saturation.value;
            defaultContrast = colorAdj.contrast.value;
        }

        if (authored.TryGet(out Vignette vignette))
            defaultVignette = vignette.intensity.value;

        if (authored.TryGet(out Bloom bloom))
            defaultBloom = bloom.intensity.value;
    }

    private bool TryEnsureWorkingProfile(VolumeProfile source = null)
    {
        if (!Application.isPlaying || targetVolume == null)
            return false;

        if (volumeProfileHandle == null)
            volumeProfileHandle = RuntimeVolumeProfileHandle.Retain(targetVolume);
        if (volumeProfileHandle == null)
            return false;

        if (source != null)
        {
            workingProfile = volumeProfileHandle.AcquireWorkingProfile(source);
            return workingProfile != null;
        }

        if (volumeProfileHandle.WorkingProfile != null)
        {
            workingProfile = volumeProfileHandle.WorkingProfile;
            return true;
        }

        workingProfile = volumeProfileHandle.AcquireWorkingProfile(targetVolume.sharedProfile);
        return workingProfile != null;
    }

    private void ReleaseVolumeProfileHandle()
    {
        if (volumeProfileHandle == null)
            return;

        volumeProfileHandle.Release();
        volumeProfileHandle = null;
    }
    
    private void ApplyTimeOfDay(float normalizedTime)
    {
        if (!Application.isPlaying || config == null)
            return;

        if (TimeOfDayVisualApplier.IsAnyIndoors)
            return;

        if (!TryEnsureWorkingProfile())
            return;
        
        // Skip if time hasn't changed significantly
        if (Mathf.Approximately(normalizedTime, lastAppliedTime)) return;
        lastAppliedTime = normalizedTime;
        
        // Use volume blending mode if enabled
        if (useVolumeBlending && config.DayVolumeProfile != null && config.NightVolumeProfile != null)
        {
            ApplyVolumeBlending(normalizedTime);
            return;
        }
        
        // Individual setting control
        ApplyColorAdjustments(normalizedTime);
        ApplyVignette(normalizedTime);
        ApplyDepthOfField(normalizedTime);
        ApplyBloom(normalizedTime);
        
        if (enableDebugLogs)
        {
            Debug.Log($"[PostFXTimeDriver] Applied t={normalizedTime:F3}");
        }
    }
    
    private void ApplyColorAdjustments(float normalizedTime)
    {
        if (!workingProfile.TryGet<ColorAdjustments>(out var colorAdj)) return;
        
        // Calculate night factor (0 at day, 1 at deep night)
        float nightFactor = GetNightFactor(normalizedTime);
        
        // Apply color filter
        if (controlColorFilter)
        {
            Color filter = config.EvaluateColorFilter(normalizedTime);
            colorAdj.colorFilter.Override(filter);
        }
        
        // Apply exposure
        if (controlExposure)
        {
            float exposure = config.EvaluateExposure(normalizedTime);
            colorAdj.postExposure.Override(exposure);
        }
        
        // Apply saturation
        if (controlSaturation)
        {
            float saturation = Mathf.Lerp(defaultSaturation, nightSaturation * 100f - 100f, nightFactor);
            colorAdj.saturation.Override(saturation);
        }
        
        // Apply contrast
        if (controlContrast)
        {
            float contrast = Mathf.Lerp(defaultContrast, nightContrast, nightFactor);
            colorAdj.contrast.Override(contrast);
        }
    }
    
    private void ApplyVignette(float normalizedTime)
    {
        if (!controlVignette) return;
        if (!workingProfile.TryGet<Vignette>(out var vignette)) return;
        
        float nightFactor = GetNightFactor(normalizedTime);
        float intensity = Mathf.Lerp(defaultVignette, nightVignetteIntensity, nightFactor);
        vignette.intensity.Override(intensity);
    }
    
    private void ApplyDepthOfField(float normalizedTime)
    {
        if (!controlDepthOfField) return;
        if (!workingProfile.TryGet<DepthOfField>(out var dof)) return;
        
        float nightFactor = GetNightFactor(normalizedTime);
        
        if (nightFactor > 0.1f)
        {
            dof.active = true;
            dof.focalLength.Override(Mathf.Lerp(35f, nightFocalLength, nightFactor));
        }
    }
    
    private void ApplyBloom(float normalizedTime)
    {
        if (!controlBloom) return;
        if (!workingProfile.TryGet<Bloom>(out var bloom)) return;
        
        float nightFactor = GetNightFactor(normalizedTime);
        float intensity = Mathf.Lerp(defaultBloom, defaultBloom * nightBloomMultiplier, nightFactor);
        bloom.intensity.Override(intensity);
    }
    
    private void ApplyVolumeBlending(float normalizedTime)
    {
        if (targetVolume == null || config == null)
            return;

        float nightFactor = GetNightFactor(normalizedTime);
        VolumeProfile source = nightFactor > 0.5f && config.NightVolumeProfile != null
            ? config.NightVolumeProfile
            : config.DayVolumeProfile;
        TryEnsureWorkingProfile(source);
    }
    
    /// <summary>
    /// Calculate a factor representing how "night" it is (0 = full day, 1 = deep night).
    /// </summary>
    private float GetNightFactor(float normalizedTime)
    {
        if (config == null) return 0f;
        
        // Get light intensity as proxy for day/night
        float lightIntensity = config.EvaluateLightIntensity(normalizedTime);
        
        // Invert: low light = high night factor
        // Remap from ~0.1-1.0 to 0-1 night factor
        float nightFactor = 1f - Mathf.InverseLerp(0.1f, 0.8f, lightIntensity);
        
        return Mathf.Clamp01(nightFactor);
    }
    
    /// <summary>
    /// Manually apply a specific time (for preview or override).
    /// </summary>
    public void PreviewTime(float normalizedTime)
    {
        ApplyTimeOfDay(normalizedTime);
    }
    
    /// <summary>
    /// Set the target volume at runtime.
    /// </summary>
    public void SetVolume(Volume volume)
    {
        ReleaseVolumeProfileHandle();
        targetVolume = volume;
        CacheDefaultValues();

        if (Application.isPlaying && timeManager != null)
            ApplyTimeOfDay(timeManager.NormalizedTime);
    }
}

