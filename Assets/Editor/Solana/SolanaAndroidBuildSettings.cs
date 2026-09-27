using UnityEditor;
using UnityEngine;

namespace Chainbound.Editor.Solana
{
    /// <summary>
    /// Keeps the Android player settings compatible with the official MWA 2.0 client.
    /// This runs only in the Editor and does not add code to the player.
    /// </summary>
    [InitializeOnLoad]
    internal static class SolanaAndroidBuildSettings
    {
        private const AndroidSdkVersions RequiredMinimumSdk = AndroidSdkVersions.AndroidApiLevel24;

        static SolanaAndroidBuildSettings()
        {
            EditorApplication.delayCall += EnforceMinimumSdk;
        }

        private static void EnforceMinimumSdk()
        {
            if (PlayerSettings.Android.minSdkVersion >= RequiredMinimumSdk)
                return;

            PlayerSettings.Android.minSdkVersion = RequiredMinimumSdk;
            AssetDatabase.SaveAssets();
            Debug.Log("[Solana Android] Raised the minimum Android API level to 24, as required by MWA 2.0.");
        }
    }
}
