using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class UpdateGameMenuWalletFlow
{
    private const string PrefabPath = "Assets/Prefabs/UI/MainMenu/GameMenuPanel.prefab";

    public static string Main()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        if (root == null)
            throw new InvalidOperationException($"Could not load {PrefabPath}.");

        try
        {
            Transform newGameView = root.transform.Find("NewGameView");
            if (newGameView == null)
                throw new InvalidOperationException("GameMenuPanel/NewGameView is missing.");

            Transform restore = newGameView.Find("RestoreProfileButton");
            if (restore != null)
                UnityEngine.Object.DestroyImmediate(restore.gameObject);

            Button play = RequireButton(newGameView, "NewGameButton");
            Button connect = RequireButton(newGameView, "ConnectWalletButton");
            Button continueButton = RequireButton(root.transform.Find("ContinueView"), "ContinueButton");

            SetLabel(play, "PLAY GAME");
            SetLabel(connect, "CONNECT WALLET");
            SetTwoButtonNavigation(play, connect);

            BonkDogPanelController controller = root.GetComponent<BonkDogPanelController>();
            if (controller == null)
                throw new InvalidOperationException("GameMenuPanel has no BonkDogPanelController.");

            SerializedObject serialized = new SerializedObject(controller);
            SerializedProperty order = serialized.FindProperty("navigationOrder");
            order.arraySize = 3;
            order.GetArrayElementAtIndex(0).objectReferenceValue = play;
            order.GetArrayElementAtIndex(1).objectReferenceValue = connect;
            order.GetArrayElementAtIndex(2).objectReferenceValue = continueButton;
            serialized.FindProperty("defaultSelection").objectReferenceValue = play;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            string buttons = string.Join(", ", newGameView.GetComponentsInChildren<Button>(true).Select(button => button.name));
            return $"Updated {PrefabPath}: {buttons}";
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    public static string InspectScene()
    {
        UnityEngine.SceneManagement.Scene scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
        Transform panel = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .FirstOrDefault(candidate => candidate.name == "GameMenuPanel");
        if (panel == null)
            return $"{scene.path}: GameMenuPanel not found";

        string source = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(panel.gameObject);
        bool hasRestore = panel.Find("NewGameView/RestoreProfileButton") != null;
        string buttons = string.Join(", ", panel.Find("NewGameView")
            .GetComponentsInChildren<Button>(true)
            .Select(button => button.name));
        return $"{scene.path}: prefab={source}; restore={hasRestore}; buttons={buttons}";
    }

    public static string InspectMainMenuScene()
    {
        const string scenePath = "Assets/Scenes/main_menu.unity";
        UnityEngine.SceneManagement.Scene existing =
            UnityEngine.SceneManagement.SceneManager.GetSceneByPath(scenePath);
        bool opened = !existing.IsValid() || !existing.isLoaded;
        UnityEngine.SceneManagement.Scene scene = opened
            ? UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                scenePath,
                UnityEditor.SceneManagement.OpenSceneMode.Additive)
            : existing;
        try
        {
            Transform panel = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .FirstOrDefault(candidate => candidate.name == "GameMenuPanel");
            if (panel == null)
                return $"{scenePath}: GameMenuPanel not found";

            string source = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(panel.gameObject);
            bool hasRestore = panel.Find("NewGameView/RestoreProfileButton") != null;
            string buttons = string.Join(", ", panel.Find("NewGameView")
                .GetComponentsInChildren<Button>(true)
                .Select(button => button.name));
            return $"{scenePath}: prefab={source}; restore={hasRestore}; buttons={buttons}";
        }
        finally
        {
            if (opened)
                UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true);
        }
    }

    public static string InspectLoadedScenes()
    {
        return string.Join(", ", Enumerable.Range(0, UnityEngine.SceneManagement.SceneManager.sceneCount)
            .Select(index =>
            {
                UnityEngine.SceneManagement.Scene scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(index);
                return $"{scene.path} (loaded={scene.isLoaded}, dirty={scene.isDirty})";
            }));
    }

    private static Button RequireButton(Transform parent, string name)
    {
        Transform child = parent != null ? parent.Find(name) : null;
        Button button = child != null ? child.GetComponent<Button>() : null;
        return button != null
            ? button
            : throw new InvalidOperationException($"Required button '{name}' is missing.");
    }

    private static void SetLabel(Button button, string value)
    {
        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label == null)
            throw new InvalidOperationException($"Button '{button.name}' has no TMP label.");
        label.text = value;
        EditorUtility.SetDirty(label);
    }

    private static void SetTwoButtonNavigation(Button play, Button connect)
    {
        play.navigation = new Navigation
        {
            mode = Navigation.Mode.Explicit,
            selectOnUp = connect,
            selectOnDown = connect,
        };
        connect.navigation = new Navigation
        {
            mode = Navigation.Mode.Explicit,
            selectOnUp = play,
            selectOnDown = play,
        };
    }
}
