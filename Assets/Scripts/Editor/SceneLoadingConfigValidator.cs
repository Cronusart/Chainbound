using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

public static class SceneLoadingConfigValidator
{
    private const string BootstrapPath = "Assets/Scenes/Bootstrap.unity";
    private const string MainMenuPath = "Assets/Scenes/main_menu.unity";
    private const string GameServicesPrefabGuid = "e6351eaca24e6d549a6a2a16341a09d2";
    private const string EventSystemScriptGuid = "76c392e42b5098c458856cdf6ecaaaa1";

    [MenuItem("Tools/Signal Jam/Loading/Validate Loading Config")]
    public static void ValidateFromMenu()
    {
        List<string> errors = ValidateProject();
        if (errors.Count == 0)
        {
            Debug.Log("[Loading Validation] Configuration, Build Settings, and GameServices placement are valid.");
            return;
        }

        for (int i = 0; i < errors.Count; i++)
            Debug.LogError($"[Loading Validation] {errors[i]}");
        Debug.LogError($"[Loading Validation] Failed with {errors.Count} issue(s).");
    }

    public static List<string> ValidateProject()
    {
        List<string> errors = new List<string>();
        string[] configGuids = AssetDatabase.FindAssets("t:SceneLoadingConfigSO");
        if (configGuids.Length == 0)
        {
            errors.Add("No SceneLoadingConfigSO asset exists.");
            return errors;
        }

        HashSet<string> enabledSceneIds = GetEnabledBuildSceneIds();
        for (int i = 0; i < configGuids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(configGuids[i]);
            SceneLoadingConfigSO config = AssetDatabase.LoadAssetAtPath<SceneLoadingConfigSO>(path);
            ValidateConfig(config, path, enabledSceneIds, errors);
        }

        ValidateBuildOrder(errors);
        ValidateGameServicesPlacement(errors);
        ValidateSceneLocalEventSystems(errors);
        ValidateStreamedChunkContent(errors);
        return errors;
    }

    private static void ValidateSceneLocalEventSystems(ICollection<string> errors)
    {
        EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
        for (int i = 0; i < scenes.Length; i++)
        {
            if (!scenes[i].enabled || string.IsNullOrEmpty(scenes[i].path) || !File.Exists(scenes[i].path))
                continue;

            string[] components = File.ReadAllText(scenes[i].path)
                .Split(new[] { "--- !u!114" }, StringSplitOptions.None);
            for (int componentIndex = 0; componentIndex < components.Length; componentIndex++)
            {
                string component = components[componentIndex];
                if (!component.Contains(EventSystemScriptGuid) || !component.Contains("m_Enabled: 1"))
                    continue;
                errors.Add($"Enabled scene-local EventSystem found in '{scenes[i].path}'. The persistent GameServices EventSystem must be authoritative.");
                break;
            }
        }
    }

    private static void ValidateStreamedChunkContent(ICollection<string> errors)
    {
        Dictionary<string, string> buildScenePaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        EditorBuildSettingsScene[] buildScenes = EditorBuildSettings.scenes;
        for (int i = 0; i < buildScenes.Length; i++)
        {
            if (buildScenes[i].enabled)
                buildScenePaths[Path.GetFileNameWithoutExtension(buildScenes[i].path)] = buildScenes[i].path;
        }

        string[] configGuids = AssetDatabase.FindAssets("t:SceneLoadingConfigSO");
        HashSet<string> validatedMemberIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int configIndex = 0; configIndex < configGuids.Length; configIndex++)
        {
            SceneLoadingConfigSO loadingConfig = AssetDatabase.LoadAssetAtPath<SceneLoadingConfigSO>(
                AssetDatabase.GUIDToAssetPath(configGuids[configIndex]));
            OverworldStreamingConfigSO config = loadingConfig != null ? loadingConfig.OverworldStreamingConfig : null;
            if (config == null)
                continue;

            List<string> sceneIds = config.GetAllSceneIds();
            for (int sceneIndex = 0; sceneIndex < sceneIds.Count; sceneIndex++)
            {
                string sceneId = sceneIds[sceneIndex];
                if (string.Equals(sceneId, config.HubSceneId, StringComparison.OrdinalIgnoreCase) ||
                    !validatedMemberIds.Add(sceneId))
                {
                    continue;
                }

                if (!buildScenePaths.TryGetValue(sceneId, out string path) || !File.Exists(path))
                    continue;

                string contents = File.ReadAllText(path);
                if (contents.Contains("--- !u!20 "))
                    errors.Add($"Streamed chunk '{sceneId}' contains a Camera. Use the persistent CameraRigRoot instead.");
                if (contents.Contains("--- !u!81 "))
                    errors.Add($"Streamed chunk '{sceneId}' contains an AudioListener. GameServices owns the listener.");
                if (contents.Contains("m_TagString: Player"))
                    errors.Add($"Streamed chunk '{sceneId}' contains a Player-tagged object.");
                if (Regex.IsMatch(contents, @"--- !u!108[\s\S]{0,250}?m_Type: 1"))
                    errors.Add($"Streamed chunk '{sceneId}' contains a directional light. The outdoor sun must be persistent.");
                if (ContainsEnabledScript(contents, "2f2abcc2f8b54de4bbc6bb75d8413751"))
                {
                    errors.Add(
                        $"Streamed chunk '{sceneId}' contains an enabled TimeOfDayVisualApplier. " +
                        "GameServices must remain the only lighting writer.");
                }
            }
        }
    }

    private static bool ContainsEnabledScript(string sceneContents, string scriptGuid)
    {
        string[] components = sceneContents.Split(new[] { "--- !u!114" }, StringSplitOptions.None);
        for (int i = 0; i < components.Length; i++)
        {
            if (components[i].Contains(scriptGuid) && components[i].Contains("m_Enabled: 1"))
                return true;
        }

        return false;
    }

    public static void ValidateConfig(
        SceneLoadingConfigSO config,
        string assetLabel,
        ISet<string> enabledSceneIds,
        IList<string> errors)
    {
        if (config == null)
        {
            errors.Add($"Unable to load config '{assetLabel}'.");
            return;
        }

        if (!enabledSceneIds.Contains(config.StartupSceneId))
            errors.Add($"{assetLabel}: startup scene '{config.StartupSceneId}' is not enabled in Build Settings.");
        if (!enabledSceneIds.Contains(config.RecoverySceneId))
            errors.Add($"{assetLabel}: recovery scene '{config.RecoverySceneId}' is not enabled in Build Settings.");
        if (config.GetMinimumDisplaySeconds(TransitionStyle.FullLoading) < 0f ||
            config.GetMinimumDisplaySeconds(TransitionStyle.QuickFade) < 0f ||
            config.FadeInSeconds < 0f || config.FadeOutSeconds < 0f ||
            config.PostLoadFadeOutDelaySeconds < 0f || config.WarmupTaskTimeoutSeconds <= 0f)
        {
            errors.Add($"{assetLabel}: one or more timing values are invalid.");
        }

        HashSet<string> destinations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        OverworldStreamingConfigSO overworldConfig = config.OverworldStreamingConfig;
        bool overworldHubIsFull = false;
        string overworldHubId = overworldConfig != null ? overworldConfig.HubSceneId : "Loomville";
        IReadOnlyList<SceneLoadingConfigSO.DestinationRule> rules = config.DestinationRules;
        for (int i = 0; i < rules.Count; i++)
        {
            string sceneId = rules[i].SceneId;
            if (string.IsNullOrWhiteSpace(sceneId))
            {
                errors.Add($"{assetLabel}: destination rule {i} has an empty scene id.");
                continue;
            }
            if (!destinations.Add(sceneId))
                errors.Add($"{assetLabel}: destination scene '{sceneId}' is duplicated.");
            if (!enabledSceneIds.Contains(sceneId))
                errors.Add($"{assetLabel}: destination scene '{sceneId}' is not enabled in Build Settings.");
            if (string.Equals(sceneId, overworldHubId, StringComparison.OrdinalIgnoreCase) &&
                rules[i].Style == TransitionStyle.FullLoading)
            {
                overworldHubIsFull = true;
            }
        }

        if (overworldConfig == null)
        {
            errors.Add($"{assetLabel}: no OverworldStreamingConfigSO is assigned.");
            return;
        }

        List<string> overworldErrors = overworldConfig.Validate();
        for (int i = 0; i < overworldErrors.Count; i++)
            errors.Add($"{assetLabel}: overworld streaming config: {overworldErrors[i]}");

        List<string> residentSceneIds = overworldConfig.GetEntryResidentSceneIds();
        for (int i = 0; i < residentSceneIds.Count; i++)
        {
            if (!enabledSceneIds.Contains(residentSceneIds[i]))
            {
                errors.Add(
                    $"{assetLabel}: overworld resident scene '{residentSceneIds[i]}' is not enabled in Build Settings.");
            }
        }

        // Only entering the set is a covered load. Members such as Link1 are
        // additive residents and deliberately need no FullLoading destination rule.
        if (!overworldHubIsFull)
            errors.Add($"{assetLabel}: overworld hub '{overworldHubId}' must have a FullLoading destination rule.");
    }

    private static HashSet<string> GetEnabledBuildSceneIds()
    {
        HashSet<string> ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
        for (int i = 0; i < scenes.Length; i++)
        {
            if (scenes[i].enabled)
                ids.Add(Path.GetFileNameWithoutExtension(scenes[i].path));
        }
        return ids;
    }

    private static void ValidateBuildOrder(ICollection<string> errors)
    {
        EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
        if (scenes.Length < 2 || !scenes[0].enabled || !SamePath(scenes[0].path, BootstrapPath))
            errors.Add("Bootstrap must be the enabled scene at build index 0.");
        if (scenes.Length < 2 || !scenes[1].enabled || !SamePath(scenes[1].path, MainMenuPath))
            errors.Add("main_menu must be the enabled scene at build index 1.");
    }

    private static void ValidateGameServicesPlacement(ICollection<string> errors)
    {
        string sourceMarker = $"m_SourcePrefab: {{fileID: 100100000, guid: {GameServicesPrefabGuid}, type: 3}}";
        EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
        for (int i = 0; i < scenes.Length; i++)
        {
            string path = scenes[i].path;
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                continue;
            bool containsPrefab = File.ReadAllText(path).Contains(sourceMarker);
            if (SamePath(path, BootstrapPath))
            {
                if (!containsPrefab)
                    errors.Add("Bootstrap does not contain the production GameServices prefab.");
            }
            else if (containsPrefab)
            {
                errors.Add($"GameServices production prefab must not be embedded in '{path}'.");
            }
        }
    }

    private static bool SamePath(string left, string right)
    {
        return string.Equals(
            left?.Replace('\\', '/'),
            right?.Replace('\\', '/'),
            StringComparison.OrdinalIgnoreCase);
    }
}
