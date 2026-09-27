using System.Runtime.InteropServices;
using UnityEngine;

namespace SignalJam.UI.MobileControls
{
    public enum HapticStrength
    {
        Light,
        Medium,
        Heavy,
    }

    /// <summary>
    /// Shared, rate-limited haptic backend for ordinary mobile UI interactions.
    /// Keeps button feedback to a short click instead of using the device's coarse default vibration.
    /// </summary>
    public static class HapticFeedback
    {
        public const string PlayerPrefsKey = "SignalJam_UIHapticsEnabled";
        public const float MinimumPulseIntervalSeconds = 0.075f;
        public const long LightDurationMilliseconds = 8L;
        public const int LightAmplitude = 30;

        private static float lastPulseTime = float.NegativeInfinity;

#if UNITY_ANDROID && !UNITY_EDITOR
        private static AndroidJavaObject androidVibrator;
        private static bool androidBridgeResolved;
        private static int androidApiLevel = -1;
#endif

        /// <summary>
        /// Uses the same persisted preference as menu, overlay, and combat UI haptics.
        /// </summary>
        public static bool Enabled
        {
            get => GetEnabled();
            set => SetEnabled(value);
        }

        public static bool GetEnabled(bool defaultEnabled = true)
        {
            return PlayerPrefs.GetInt(PlayerPrefsKey, defaultEnabled ? 1 : 0) == 1;
        }

        public static void SetEnabled(bool enabled, bool saveImmediately = true)
        {
            PlayerPrefs.SetInt(PlayerPrefsKey, enabled ? 1 : 0);
            if (saveImmediately)
                PlayerPrefs.Save();
        }

        public static void Pulse(HapticStrength strength = HapticStrength.Light)
        {
            if (!Enabled) return;
            if (Time.realtimeSinceStartup - lastPulseTime < MinimumPulseIntervalSeconds) return;
            lastPulseTime = Time.realtimeSinceStartup;

#if UNITY_ANDROID && !UNITY_EDITOR
            TriggerAndroid(strength);
#elif UNITY_IOS && !UNITY_EDITOR
            TriggerIos(strength);
#else
            // Haptics are intentionally a no-op on desktop and in the Editor.
            _ = strength;
#endif
        }

        public static void GetAndroidPattern(HapticStrength strength, out long durationMs, out int amplitude)
        {
            switch (strength)
            {
                case HapticStrength.Heavy:
                    durationMs = 18L;
                    amplitude = 80;
                    break;
                case HapticStrength.Medium:
                    durationMs = 12L;
                    amplitude = 55;
                    break;
                case HapticStrength.Light:
                default:
                    durationMs = LightDurationMilliseconds;
                    amplitude = LightAmplitude;
                    break;
            }
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private static void TriggerAndroid(HapticStrength strength)
        {
            AndroidJavaObject vibrator = GetAndroidVibrator();
            if (vibrator == null)
                return;

            try
            {
                if (!vibrator.Call<bool>("hasVibrator"))
                    return;
            }
            catch
            {
                // Some older Android implementations do not expose hasVibrator reliably.
            }

            GetAndroidPattern(strength, out long durationMs, out int amplitude);

            try
            {
                if (androidApiLevel >= 26)
                {
                    using (var vibrationEffectClass = new AndroidJavaClass("android.os.VibrationEffect"))
                    using (AndroidJavaObject effect = vibrationEffectClass.CallStatic<AndroidJavaObject>(
                               "createOneShot", durationMs, amplitude))
                    {
                        vibrator.Call("vibrate", effect);
                    }
                }
                else
                {
                    // Pre-API-26 cannot control amplitude; keep the pulse extremely short.
                    vibrator.Call("vibrate", durationMs);
                }
            }
            catch
            {
                // Unsupported or restricted haptics should never break button input.
            }
        }

        private static AndroidJavaObject GetAndroidVibrator()
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
            catch
            {
                androidVibrator = null;
            }

            return androidVibrator;
        }
#endif

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void SJUIHaptics_TriggerSelection();

        [DllImport("__Internal")]
        private static extern void SJUIHaptics_TriggerImpactLight();

        private static void TriggerIos(HapticStrength strength)
        {
            try
            {
                if (strength == HapticStrength.Light)
                    SJUIHaptics_TriggerSelection();
                else
                    SJUIHaptics_TriggerImpactLight();
            }
            catch
            {
                // Unsupported or restricted haptics should never break button input.
            }
        }
#endif
    }
}
