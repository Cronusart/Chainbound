using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Shared pitch-driven additive Y lift for exploration billboards.
/// Uses live camera look elevation (forward vs horizon) so FaceCameraForward sprites lift
/// when the camera looks shallower than the explore baseline.
/// At reference pitch and above, lift is zero. Below reference pitch, lift ramps up to maxPitchLift at minPitch.
/// </summary>
public static class ExplorationVisualPitchYOffset
{
    public const float DefaultReferencePitch = 30f;
    public const float DefaultMinPitch = 10f;

    private static TopDownCameraController3D cachedCameraController;
    private static bool sceneLoadRegistered;
    private static float nextDebugLogTime;

    [Serializable]
    public struct PitchYOffsetSettings
    {
        [Tooltip("When true, applies additive Y lift based on live camera look elevation.")]
        public bool enabled;

        [Tooltip("Look elevation (degrees below horizon) where lift is zero. Explore baseline is 30.")]
        public float referencePitch;

        [Tooltip("Look elevation (degrees below horizon) where lift reaches maxPitchLift. Intro/narrative floor is 10.")]
        public float minPitch;

        [Tooltip("Additive world Y lift applied at minPitch.")]
        public float maxPitchLift;

        public static PitchYOffsetSettings CreateDefault(float maxPitchLift = 0f)
        {
            return new PitchYOffsetSettings
            {
                enabled = true,
                referencePitch = DefaultReferencePitch,
                minPitch = DefaultMinPitch,
                maxPitchLift = maxPitchLift
            };
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        cachedCameraController = null;
        sceneLoadRegistered = false;
        nextDebugLogTime = 0f;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void RegisterSceneLoadInvalidation()
    {
        if (sceneLoadRegistered)
            return;

        SceneManager.sceneLoaded += OnSceneLoaded;
        sceneLoadRegistered = true;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        InvalidateCameraCache();
    }

    /// <summary>
    /// Evaluate additive Y lift for the given pitch and settings.
    /// </summary>
    public static float EvaluateLift(PitchYOffsetSettings settings, float pitchDegrees)
    {
        if (!settings.enabled || settings.maxPitchLift <= 0f)
            return 0f;

        if (pitchDegrees >= settings.referencePitch)
            return 0f;

        float minPitch = Mathf.Min(settings.minPitch, settings.referencePitch - 0.001f);
        float t = Mathf.Clamp01(Mathf.InverseLerp(settings.referencePitch, minPitch, pitchDegrees));
        return settings.maxPitchLift * t;
    }

    /// <summary>
    /// Evaluate additive Y lift using live camera look elevation.
    /// <paramref name="visualWorldPosition"/> is used only for optional debug logging.
    /// </summary>
    public static float EvaluateLiftForSubject(PitchYOffsetSettings settings, Vector3 visualWorldPosition)
    {
        return EvaluateLiftForSubject(settings, visualWorldPosition, false, null);
    }

    /// <summary>
    /// Evaluate additive Y lift using live camera look elevation, with optional throttled debug.
    /// </summary>
    public static float EvaluateLiftForSubject(
        PitchYOffsetSettings settings,
        Vector3 visualWorldPosition,
        bool enableDebugLogs,
        string debugSource)
    {
        float pitch = ResolveLookElevation();
        float lift = EvaluateLift(settings, pitch);

        if (enableDebugLogs)
            LogLiftDebugIfDue(debugSource, visualWorldPosition, pitch, lift);

        return lift;
    }

    /// <summary>
    /// Evaluate additive Y lift using live camera look elevation.
    /// </summary>
    public static float EvaluateLift(PitchYOffsetSettings settings)
    {
        return EvaluateLiftForSubject(settings, Vector3.zero);
    }

    /// <summary>
    /// Read live camera look elevation (degrees below horizon).
    /// </summary>
    public static float ResolveLookElevation()
    {
        TopDownCameraController3D controller = ResolveCameraController();
        if (controller != null)
            return controller.GetLookElevationDegrees();

        return DefaultReferencePitch;
    }

    /// <summary>
    /// Read view elevation from camera position toward the subject (boom height, not billboard tilt).
    /// </summary>
    public static float ResolveViewPitchToward(Vector3 subjectWorldPosition)
    {
        TopDownCameraController3D controller = ResolveCameraController();
        if (controller != null)
            return controller.GetViewPitchToward(subjectWorldPosition);

        return DefaultReferencePitch;
    }

    /// <summary>
    /// Clear cached camera reference (e.g. after scene unload).
    /// </summary>
    public static void InvalidateCameraCache()
    {
        cachedCameraController = null;
    }

    private static void LogLiftDebugIfDue(
        string debugSource,
        Vector3 visualWorldPosition,
        float lookElevation,
        float lift)
    {
        if (Time.unscaledTime < nextDebugLogTime)
            return;

        nextDebugLogTime = Time.unscaledTime + 1f;
        string source = string.IsNullOrEmpty(debugSource) ? "PitchYOffset" : debugSource;
        Debug.Log(
            $"[{source}] lookElevation={lookElevation:0.0} lift={lift:0.000} visualY={visualWorldPosition.y:0.000}");
    }

    private static TopDownCameraController3D ResolveCameraController()
    {
        if (cachedCameraController != null)
        {
            Camera cachedCamera = SceneCameraResolver.GetCamera(cachedCameraController);
            if (SceneCameraResolver.IsUsableFor(cachedCamera, null))
                return cachedCameraController;
        }

        cachedCameraController = SceneCameraResolver.ResolveGameplayCameraController();
        return cachedCameraController;
    }
}
