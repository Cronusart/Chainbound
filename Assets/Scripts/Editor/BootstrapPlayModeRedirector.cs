using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class BootstrapPlayModeRedirector
{
    private const string MenuPath = "Tools/Signal Jam/Loading/Always Start Through Bootstrap";
    private const string PreferenceKey = "SignalJam.Loading.AlwaysStartThroughBootstrap";
    private const string OneShotLaunchDestinationKey = "SignalJam.Loading.OneShotLaunchDestination";
    private const string RestoreStartSceneAfterPlayKey = "SignalJam.Loading.RestoreStartSceneAfterPlay";
    public const string BootstrapPath = "Assets/Scenes/Bootstrap.unity";
    public const string MainMenuSceneId = "main_menu";
    private const string UnityTestScenePrefix = "InitTestScene";

    static BootstrapPlayModeRedirector()
    {
        EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
        EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
        EditorApplication.delayCall += ApplyPlayModeStartScene;
    }

    [MenuItem(MenuPath)]
    private static void Toggle()
    {
        EditorPrefs.SetBool(PreferenceKey, !IsEnabled);
        ApplyPlayModeStartScene();
    }

    [MenuItem(MenuPath, true)]
    private static bool ValidateToggle()
    {
        Menu.SetChecked(MenuPath, IsEnabled);
        return true;
    }

    private static bool IsEnabled => EditorPrefs.GetBool(PreferenceKey, true);

    /// <summary>
    /// Configures one Play Mode launch through Bootstrap regardless of the
    /// persistent redirector preference. The one-shot destination is consumed
    /// while leaving the developer's normal launch preference untouched.
    /// </summary>
    public static bool PrepareOneShotBootstrapLaunch(string destinationSceneId)
    {
        string destination = string.IsNullOrWhiteSpace(destinationSceneId)
            ? MainMenuSceneId
            : destinationSceneId.Trim();

        if (!TryAssignBootstrapStartScene())
            return false;

        SessionState.SetString(OneShotLaunchDestinationKey, destination);
        SessionState.SetString(BootstrapStartupController.EditorLaunchSceneSessionKey, destination);
        return true;
    }

    private static void ApplyPlayModeStartScene()
    {
        // Unity Test Framework creates a transient InitTestScene before entering
        // PlayMode. Redirecting that scene through Bootstrap prevents the test
        // runner from ever attaching and leaves async runs stuck at "running".
        if (IsUnityGeneratedTestScene(SceneManager.GetActiveScene()))
        {
            EditorSceneManager.playModeStartScene = null;
            return;
        }

        if (!IsEnabled)
        {
            EditorSceneManager.playModeStartScene = null;
            return;
        }

        TryAssignBootstrapStartScene();
    }

    private static bool TryAssignBootstrapStartScene()
    {
        SceneAsset bootstrap = AssetDatabase.LoadAssetAtPath<SceneAsset>(BootstrapPath);
        if (bootstrap == null)
        {
            Debug.LogError($"[Bootstrap Editor] Missing scene at '{BootstrapPath}'.");
            EditorSceneManager.playModeStartScene = null;
            return false;
        }

        EditorSceneManager.playModeStartScene = bootstrap;
        return true;
    }

    private static void HandlePlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode &&
            SessionState.GetBool(RestoreStartSceneAfterPlayKey, false))
        {
            SessionState.EraseBool(RestoreStartSceneAfterPlayKey);
            EditorApplication.delayCall += ApplyPlayModeStartScene;
            return;
        }

        if (state != PlayModeStateChange.ExitingEditMode)
            return;

        if (IsUnityGeneratedTestScene(SceneManager.GetActiveScene()))
        {
            EditorSceneManager.playModeStartScene = null;
            SessionState.EraseString(BootstrapStartupController.EditorLaunchSceneSessionKey);
            return;
        }

        string oneShotDestination = SessionState.GetString(OneShotLaunchDestinationKey, string.Empty);
        if (!string.IsNullOrWhiteSpace(oneShotDestination))
        {
            SessionState.EraseString(OneShotLaunchDestinationKey);
            SessionState.SetBool(RestoreStartSceneAfterPlayKey, true);
            if (!TryAssignBootstrapStartScene())
                return;
            SessionState.SetString(
                BootstrapStartupController.EditorLaunchSceneSessionKey,
                oneShotDestination.Trim());
            return;
        }

        if (!IsEnabled)
            return;

        ApplyPlayModeStartScene();
        Scene scene = SceneManager.GetActiveScene();
        string destination = ResolveLaunchDestination(scene, out string warning);
        SessionState.SetString(BootstrapStartupController.EditorLaunchSceneSessionKey, destination);
        if (!string.IsNullOrEmpty(warning))
            Debug.LogWarning(warning);
    }

    private static string ResolveLaunchDestination(Scene scene, out string warning)
    {
        warning = string.Empty;
        if (!scene.IsValid() || string.IsNullOrWhiteSpace(scene.path))
        {
            warning = "[Bootstrap Editor] The active scene is unsaved or invalid; starting at main_menu.";
            return MainMenuSceneId;
        }

        if (string.Equals(scene.path, BootstrapPath, StringComparison.OrdinalIgnoreCase))
            return MainMenuSceneId;

        string normalizedPath = scene.path.Replace('\\', '/');
        EditorBuildSettingsScene[] buildScenes = EditorBuildSettings.scenes;
        for (int i = 0; i < buildScenes.Length; i++)
        {
            if (buildScenes[i].enabled &&
                string.Equals(buildScenes[i].path.Replace('\\', '/'), normalizedPath, StringComparison.OrdinalIgnoreCase))
            {
                return Path.GetFileNameWithoutExtension(normalizedPath);
            }
        }

        warning = $"[Bootstrap Editor] '{scene.path}' is not an enabled Build Settings scene; starting at main_menu.";
        return MainMenuSceneId;
    }

    private static bool IsUnityGeneratedTestScene(Scene scene)
    {
        if (!scene.IsValid())
            return false;

        if (!string.IsNullOrWhiteSpace(scene.name) &&
            scene.name.StartsWith(UnityTestScenePrefix, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(scene.path))
            return false;

        string normalizedPath = scene.path.Replace('\\', '/');
        string fileName = Path.GetFileNameWithoutExtension(normalizedPath);
        return fileName.StartsWith(UnityTestScenePrefix, StringComparison.OrdinalIgnoreCase);
    }
}
