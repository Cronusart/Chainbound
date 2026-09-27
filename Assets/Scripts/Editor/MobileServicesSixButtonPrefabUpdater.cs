#if UNITY_EDITOR
using System.Collections.Generic;
using SignalJam.UI.MobileControls;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem.OnScreen;
using UnityEngine.UI;

/// <summary>
/// One-shot authoring migration for the six-button mobile controller layout.
/// Menu: Tools / Chainbound / Mobile / Update MobileServices Six Button Layout
/// </summary>
public static class MobileServicesSixButtonPrefabUpdater
{
    private const string PrefabPath = "Assets/Prefabs/Controls/MobileServices.prefab";

    [MenuItem("Tools/Chainbound/Mobile/Update MobileServices Six Button Layout")]
    public static void UpdatePrefab()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            RemoveByName(root.transform, "Jump");
            RemoveByName(root.transform, "JumpButton");
            RemoveByName(root.transform, "Sprint");
            RemoveByName(root.transform, "SprintToggle");
            RemoveByName(root.transform, "Wallet");
            RemoveByName(root.transform, "WalletGroup");

            var inputManager = root.GetComponent<MobileInputManager>();
            var audioBridge = root.GetComponent<MobileHudButtonAudioBridge>();
            if (audioBridge == null)
                audioBridge = root.AddComponent<MobileHudButtonAudioBridge>();

            var movementPad = FindChildComponent<HybridMovementPad>(root.transform, "MovementPad");
            var buttonX = FindChildComponent<GameboyButton>(root.transform, "ButtonX");
            var buttonY = FindChildComponent<GameboyButton>(root.transform, "ButtonY");
            var buttonA = FindChildComponent<GameboyButton>(root.transform, "ButtonA");
            var buttonB = FindChildComponent<GameboyButton>(root.transform, "ButtonB");
            var start = FindChildComponent<GameboyButton>(root.transform, "Start");
            var select = FindChildComponent<GameboyButton>(root.transform, "Select");

            ConfigurePad(movementPad);
            ConfigureButton(buttonX, "<Gamepad>/buttonWest", GameboyButton.InteractionMode.Hold);
            ConfigureButton(buttonB, "<Gamepad>/buttonEast", GameboyButton.InteractionMode.Hold);
            ConfigureButton(buttonA, "<Gamepad>/buttonSouth", GameboyButton.InteractionMode.Hold);
            ConfigureButton(buttonY, "<Gamepad>/buttonNorth", GameboyButton.InteractionMode.Hold);
            ConfigureButton(start, "<Gamepad>/start", GameboyButton.InteractionMode.Hold);
            ConfigureButton(select, "<Gamepad>/select", GameboyButton.InteractionMode.Hold);

            ConfigureInputManager(inputManager, movementPad, buttonX, buttonY, buttonA, buttonB, start, select);
            MobileServicesPadAndAudioUpdater.ConfigureAudioBridge(audioBridge, root, movementPad);
            ConfigureVisibilityController(root);

            EditorUtility.SetDirty(root);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            AssetDatabase.SaveAssets();

            Debug.Log("[MobileServicesSixButtonPrefabUpdater] MobileServices prefab updated to six-button layout.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void ConfigurePad(HybridMovementPad pad)
    {
        if (pad == null) return;

        var stick = pad.GetComponent<OnScreenStick>();
        if (stick != null)
            Object.DestroyImmediate(stick);

        var so = new SerializedObject(pad);
        so.FindProperty("digitalOnly").boolValue = true;
        so.FindProperty("m_ControlPath").stringValue = "<Gamepad>/leftStick";
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigureButton(GameboyButton button, string controlPath, GameboyButton.InteractionMode mode)
    {
        if (button == null) return;

        var image = button.GetComponent<Image>();
        var so = new SerializedObject(button);
        so.FindProperty("interactionMode").enumValueIndex = (int)mode;
        so.FindProperty("useScaleAnimation").boolValue = false;
        so.FindProperty("playLegacyAudio").boolValue = false;
        if (image != null && image.sprite != null)
            so.FindProperty("normalSprite").objectReferenceValue = image.sprite;
        so.ApplyModifiedPropertiesWithoutUndo();

        var onScreenButton = button.GetComponent<OnScreenButton>();
        if (onScreenButton == null)
            onScreenButton = button.gameObject.AddComponent<OnScreenButton>();

        var osb = new SerializedObject(onScreenButton);
        osb.FindProperty("m_ControlPath").stringValue = controlPath;
        osb.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigureInputManager(
        MobileInputManager inputManager,
        HybridMovementPad pad,
        GameboyButton x,
        GameboyButton y,
        GameboyButton a,
        GameboyButton b,
        GameboyButton start,
        GameboyButton select)
    {
        if (inputManager == null) return;

        var so = new SerializedObject(inputManager);
        so.FindProperty("movementPad").objectReferenceValue = pad;
        so.FindProperty("buttonX").objectReferenceValue = x;
        so.FindProperty("buttonY").objectReferenceValue = y;
        so.FindProperty("buttonA").objectReferenceValue = a;
        so.FindProperty("buttonB").objectReferenceValue = b;
        so.FindProperty("jumpButton").objectReferenceValue = null;
        so.FindProperty("sprintButton").objectReferenceValue = null;
        so.FindProperty("startButton").objectReferenceValue = start;
        so.FindProperty("selectButton").objectReferenceValue = select;
        so.FindProperty("solanaButton").objectReferenceValue = null;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigureVisibilityController(GameObject root)
    {
        var controller = root.GetComponentInChildren<MobileHudVisibilityController>(true);
        if (controller == null) return;

        var so = new SerializedObject(controller);
        ClearHudGroup(so.FindProperty("jumpSprintGroup"));
        ClearHudGroup(so.FindProperty("walletGroup"));
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ClearHudGroup(SerializedProperty group)
    {
        if (group == null) return;
        group.FindPropertyRelative("group").objectReferenceValue = null;
        group.FindPropertyRelative("visibleIn").intValue = 0;
        group.FindPropertyRelative("dimAlphaWhenHidden").floatValue = 0f;
    }

    private static T FindChildComponent<T>(Transform root, string name) where T : Component
    {
        Transform child = FindChild(root, name);
        return child != null ? child.GetComponent<T>() : null;
    }

    private static Transform FindChild(Transform root, string name)
    {
        if (root == null) return null;
        if (root.name == name) return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform result = FindChild(root.GetChild(i), name);
            if (result != null) return result;
        }

        return null;
    }

    private static void RemoveByName(Transform root, string name)
    {
        List<Transform> matches = new List<Transform>();
        CollectChildren(root, name, matches);

        for (int i = 0; i < matches.Count; i++)
        {
            Object.DestroyImmediate(matches[i].gameObject);
        }
    }

    private static void CollectChildren(Transform root, string name, List<Transform> matches)
    {
        if (root == null) return;

        for (int i = root.childCount - 1; i >= 0; i--)
        {
            Transform child = root.GetChild(i);
            CollectChildren(child, name, matches);
            if (child.name == name)
                matches.Add(child);
        }
    }
}
#endif
