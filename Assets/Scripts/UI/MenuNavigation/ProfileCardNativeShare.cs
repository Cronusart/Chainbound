using System.IO;
using UnityEngine;

namespace SignalJam.UI.MenuNavigation
{
    /// <summary>
    /// Writes a PNG and opens the Android share sheet. Editor writes the file only.
    /// </summary>
    public static class ProfileCardNativeShare
    {
        public const string FileProviderSuffix = ".fileprovider";

        public static string WritePng(byte[] pngBytes, string fileName = "chainbound-profile.png")
        {
            if (pngBytes == null || pngBytes.Length == 0)
                return "";

            string safeName = string.IsNullOrWhiteSpace(fileName) ? "chainbound-profile.png" : fileName;
            string directory = Application.temporaryCachePath;
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, safeName);
            File.WriteAllBytes(path, pngBytes);
            return path;
        }

        public static bool TrySharePng(string pngPath, string text, out string error)
        {
            error = "";
            if (string.IsNullOrWhiteSpace(pngPath) || !File.Exists(pngPath))
            {
                error = "Share image was not written.";
                return false;
            }

#if UNITY_ANDROID && !UNITY_EDITOR
            return TryShareAndroid(pngPath, text ?? "", out error);
#else
            Debug.Log($"[ProfileCardNativeShare] Wrote profile card PNG: {pngPath}");
            return true;
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private static bool TryShareAndroid(string pngPath, string text, out string error)
        {
            error = "";
            try
            {
                using (var file = new AndroidJavaObject("java.io.File", pngPath))
                using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var fileProvider = new AndroidJavaClass("androidx.core.content.FileProvider"))
                using (var intent = new AndroidJavaObject("android.content.Intent", "android.intent.action.SEND"))
                using (var intentClass = new AndroidJavaClass("android.content.Intent"))
                {
                    string authority = activity.Call<string>("getPackageName") + FileProviderSuffix;
                    AndroidJavaObject uri = fileProvider.CallStatic<AndroidJavaObject>(
                        "getUriForFile",
                        activity,
                        authority,
                        file);

                    intent.Call<AndroidJavaObject>("setType", "image/png");
                    intent.Call<AndroidJavaObject>("putExtra", "android.intent.extra.STREAM", uri);
                    if (!string.IsNullOrWhiteSpace(text))
                        intent.Call<AndroidJavaObject>("putExtra", "android.intent.extra.TEXT", text);
                    intent.Call<AndroidJavaObject>("addFlags", 1);

                    using (AndroidJavaObject chooser = intentClass.CallStatic<AndroidJavaObject>(
                        "createChooser",
                        intent,
                        "Share"))
                    {
                        activity.Call("startActivity", chooser);
                    }
                }

                return true;
            }
            catch (System.Exception ex)
            {
                error = ex.Message;
                Debug.LogWarning($"[ProfileCardNativeShare] Android share failed: {ex.Message}");
                return false;
            }
        }
#endif
    }
}
