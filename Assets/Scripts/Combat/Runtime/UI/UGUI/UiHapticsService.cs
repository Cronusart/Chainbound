using System.Runtime.InteropServices;
using SignalJam.UI.MobileControls;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Shared UI-scoped haptics service for menus, overlays, and reusable text entry.
/// The setting is owned here so multiple UI surfaces can bind to the same preference.
/// Unsupported platforms resolve safely as a no-op.
/// </summary>
[DisallowMultipleComponent]
public class UiHapticsService : MonoBehaviour
{
    private static UiHapticsService instance;

    [Header("Settings")]
    [SerializeField] private bool defaultEnabled = true;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    private bool isUiHapticsEnabled;
    private Gamepad activeGamepad;
    private float stopGamepadMotorsAt = -1f;

#if UNITY_ANDROID && !UNITY_EDITOR
    private AndroidJavaObject androidVibrator;
    private bool androidBridgeResolved;
    private int androidApiLevel = -1;
#endif

    private enum UiHapticIntent
    {
        Navigate = 0,
        Selection = 1,
        Confirm = 2,
        Cancel = 3,
        Warning = 4,
        CaptureEnter = 5,
        CaptureLeave = 6,
        CaptureSpike = 7,
        CaptureSuccess = 8,
        CaptureFailure = 9,
    }

    public static UiHapticsService Instance
    {
        get
        {
            if (instance == null)
                instance = Object.FindFirstObjectByType<UiHapticsService>(FindObjectsInactive.Include);

            return instance;
        }
    }

    public bool IsUiHapticsEnabled => isUiHapticsEnabled;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Debug.LogWarning($"[UiHapticsService] Duplicate instance found. Destroying {gameObject.name}");
            Destroy(gameObject);
            return;
        }

        instance = this;
        isUiHapticsEnabled = HapticFeedback.GetEnabled(defaultEnabled);
    }

    private void OnDestroy()
    {
        StopGamepadMotors();
        if (instance == this)
            instance = null;
    }

    private void OnDisable()
    {
        StopGamepadMotors();
    }

    private void Update()
    {
        if (stopGamepadMotorsAt >= 0f && Time.unscaledTime >= stopGamepadMotorsAt)
            StopGamepadMotors();
    }

    public void SetUiHapticsEnabled(bool enabled)
    {
        if (isUiHapticsEnabled == enabled)
            return;

        isUiHapticsEnabled = enabled;
        if (!enabled)
            StopGamepadMotors();
        HapticFeedback.SetEnabled(enabled);

        if (enableDebugLogs)
            Debug.Log($"[UiHapticsService] UI haptics {(enabled ? "enabled" : "disabled")}.");
    }

    public void TriggerNavigate()
    {
        Trigger(UiHapticIntent.Navigate);
    }

    public void TriggerSelection()
    {
        Trigger(UiHapticIntent.Selection);
    }

    public void TriggerConfirm()
    {
        Trigger(UiHapticIntent.Confirm);
    }

    public void TriggerCancel()
    {
        Trigger(UiHapticIntent.Cancel);
    }

    public void TriggerWarning()
    {
        Trigger(UiHapticIntent.Warning);
    }

    public void TriggerCaptureEnterBand()
    {
        Trigger(UiHapticIntent.CaptureEnter);
    }

    public void TriggerCaptureLeaveBand()
    {
        Trigger(UiHapticIntent.CaptureLeave);
    }

    public void TriggerCaptureSpike()
    {
        Trigger(UiHapticIntent.CaptureSpike);
    }

    public void TriggerCaptureSuccess()
    {
        Trigger(UiHapticIntent.CaptureSuccess);
    }

    public void TriggerCaptureFailure()
    {
        Trigger(UiHapticIntent.CaptureFailure);
    }

    private void Trigger(UiHapticIntent intent)
    {
        if (!isUiHapticsEnabled)
            return;

        bool captureIntent = intent >= UiHapticIntent.CaptureEnter;
        if (captureIntent && TryTriggerGamepad(intent))
            return;

        if (!captureIntent)
        {
            HapticFeedback.Pulse(intent == UiHapticIntent.Warning
                ? HapticStrength.Medium
                : HapticStrength.Light);
            return;
        }

        if (!Application.isMobilePlatform)
            return;

#if UNITY_ANDROID && !UNITY_EDITOR
        TriggerAndroid(intent);
#elif UNITY_IOS && !UNITY_EDITOR
        TriggerIos(intent);
#endif
    }

    private bool TryTriggerGamepad(UiHapticIntent intent)
    {
        Gamepad gamepad = Gamepad.current;
        if (gamepad == null)
            return false;

        StopGamepadMotors();
        float low;
        float high;
        float duration;
        switch (intent)
        {
            case UiHapticIntent.CaptureEnter:
                low = 0.08f; high = 0.18f; duration = 0.045f;
                break;
            case UiHapticIntent.CaptureLeave:
                low = 0.22f; high = 0.36f; duration = 0.07f;
                break;
            case UiHapticIntent.CaptureSpike:
                low = 0.72f; high = 0.85f; duration = 0.12f;
                break;
            case UiHapticIntent.CaptureSuccess:
                low = 0.5f; high = 0.8f; duration = 0.22f;
                break;
            case UiHapticIntent.CaptureFailure:
                low = 0.62f; high = 0.22f; duration = 0.18f;
                break;
            default:
                return false;
        }

        activeGamepad = gamepad;
        activeGamepad.SetMotorSpeeds(low, high);
        stopGamepadMotorsAt = Time.unscaledTime + duration;
        return true;
    }

    private void StopGamepadMotors()
    {
        if (activeGamepad != null)
            activeGamepad.SetMotorSpeeds(0f, 0f);
        activeGamepad = null;
        stopGamepadMotorsAt = -1f;
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    private void TriggerAndroid(UiHapticIntent intent)
    {
        AndroidJavaObject vibrator = GetAndroidVibrator();
        if (vibrator == null)
            return;

        bool hasVibrator;
        try
        {
            hasVibrator = vibrator.Call<bool>("hasVibrator");
        }
        catch
        {
            hasVibrator = true;
        }

        if (!hasVibrator)
            return;

        GetAndroidPattern(intent, out long durationMs, out int amplitude);

        if (androidApiLevel >= 26)
        {
            using (var vibrationEffectClass = new AndroidJavaClass("android.os.VibrationEffect"))
            using (AndroidJavaObject effect = vibrationEffectClass.CallStatic<AndroidJavaObject>("createOneShot", durationMs, amplitude))
            {
                vibrator.Call("vibrate", effect);
            }
        }
        else
        {
            vibrator.Call("vibrate", durationMs);
        }
    }

    private AndroidJavaObject GetAndroidVibrator()
    {
        if (androidBridgeResolved)
            return androidVibrator;

        androidBridgeResolved = true;

        try
        {
            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (AndroidJavaObject currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var buildVersion = new AndroidJavaClass("android.os.Build$VERSION"))
            using (var contextClass = new AndroidJavaClass("android.content.Context"))
            {
                androidApiLevel = buildVersion.GetStatic<int>("SDK_INT");
                string vibratorService = contextClass.GetStatic<string>("VIBRATOR_SERVICE");
                androidVibrator = currentActivity.Call<AndroidJavaObject>("getSystemService", vibratorService);
            }
        }
        catch (System.Exception exception)
        {
            androidVibrator = null;

            if (enableDebugLogs)
                Debug.LogWarning($"[UiHapticsService] Failed to resolve Android vibrator service. {exception.Message}");
        }

        return androidVibrator;
    }

    private static void GetAndroidPattern(UiHapticIntent intent, out long durationMs, out int amplitude)
    {
        switch (intent)
        {
            case UiHapticIntent.Navigate:
                durationMs = 8L;
                amplitude = 30;
                break;
            case UiHapticIntent.Selection:
                durationMs = 10L;
                amplitude = 40;
                break;
            case UiHapticIntent.Confirm:
                durationMs = 18L;
                amplitude = 80;
                break;
            case UiHapticIntent.Cancel:
                durationMs = 14L;
                amplitude = 55;
                break;
            case UiHapticIntent.CaptureEnter:
                durationMs = 14L;
                amplitude = 45;
                break;
            case UiHapticIntent.CaptureLeave:
                durationMs = 24L;
                amplitude = 90;
                break;
            case UiHapticIntent.CaptureSpike:
                durationMs = 45L;
                amplitude = 220;
                break;
            case UiHapticIntent.CaptureSuccess:
                durationMs = 90L;
                amplitude = 180;
                break;
            case UiHapticIntent.CaptureFailure:
                durationMs = 75L;
                amplitude = 130;
                break;
            case UiHapticIntent.Warning:
            default:
                durationMs = 24L;
                amplitude = 100;
                break;
        }
    }
#endif

#if UNITY_IOS && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void SJUIHaptics_TriggerSelection();

    [DllImport("__Internal")]
    private static extern void SJUIHaptics_TriggerImpactLight();

    [DllImport("__Internal")]
    private static extern void SJUIHaptics_TriggerNotificationSuccess();

    [DllImport("__Internal")]
    private static extern void SJUIHaptics_TriggerNotificationWarning();

    private static void TriggerIos(UiHapticIntent intent)
    {
        switch (intent)
        {
            case UiHapticIntent.Navigate:
            case UiHapticIntent.Selection:
                SJUIHaptics_TriggerSelection();
                break;
            case UiHapticIntent.Confirm:
                SJUIHaptics_TriggerNotificationSuccess();
                break;
            case UiHapticIntent.Cancel:
            case UiHapticIntent.CaptureEnter:
                SJUIHaptics_TriggerImpactLight();
                break;
            case UiHapticIntent.CaptureSuccess:
                SJUIHaptics_TriggerNotificationSuccess();
                break;
            case UiHapticIntent.CaptureLeave:
            case UiHapticIntent.CaptureSpike:
            case UiHapticIntent.CaptureFailure:
            case UiHapticIntent.Warning:
            default:
                SJUIHaptics_TriggerNotificationWarning();
                break;
        }
    }
#endif
}
