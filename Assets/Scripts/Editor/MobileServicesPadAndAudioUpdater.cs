#if UNITY_EDITOR
using SignalJam.UI.MobileControls;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.OnScreen;
using UnityEngine.UI;

/// <summary>
/// Authoring migration for the mobile movement pad and the HUD tactile audio layout.
/// Menu: Tools / Chainbound / Mobile / Fix Movement Pad And Wire Tactile Audio
///
/// Movement pad: splits the touch rect from the travelling plate art. The pad keeps a
/// transparent full-rect raycast surface, and a child <c>PadVisual</c> carries the
/// sprite and does the rocking. Pointing <c>padVisual</c> at the pad itself drags the
/// touch surface along with the finger, which displaces every subsequent drag sample.
///
/// Audio: fills <see cref="MobileHudButtonAudioBridge"/>'s role table with the tactile
/// placeholder events. Those assets ship with no clips, and the bridge skips clipless
/// events, so the HUD stays silent until the audio files are dropped in.
/// </summary>
public static class MobileServicesPadAndAudioUpdater
{
    private const string PrefabPath = "Assets/Prefabs/Controls/MobileServices.prefab";
    private const string InputActionsPath = "Assets/Scripts/Movement/InputSystem_Actions.inputactions";
    private const string TactileFolder = "Assets/Data/Audio/UI/";
    private const string PadVisualName = "PadVisual";

    [MenuItem("Tools/Chainbound/Mobile/Fix Movement Pad And Wire Tactile Audio")]
    public static void UpdatePrefab()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var pad = root.GetComponentInChildren<HybridMovementPad>(true);
            if (pad == null)
            {
                Debug.LogError($"[MobileServicesPadAndAudioUpdater] No HybridMovementPad found under {PrefabPath}.");
                return;
            }

            RectTransform padVisual = RestructureMovementPad(pad);
            ConfigureMovementPad(pad, padVisual);
            ConfigureInputActions(root.GetComponent<MobileInputManager>());

            var bridge = root.GetComponent<MobileHudButtonAudioBridge>();
            if (bridge == null)
                bridge = root.AddComponent<MobileHudButtonAudioBridge>();

            ConfigureAudioBridge(bridge, root, pad);

            EditorUtility.SetDirty(root);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            AssetDatabase.SaveAssets();

            Debug.Log(
                "[MobileServicesPadAndAudioUpdater] Movement pad restructured and tactile audio table wired. " +
                "Drop clips into Assets/Data/Audio/UI/UI_hud_tactile_*.asset to enable HUD button sounds.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    /// <summary>
    /// Moves the plate sprite onto a child so the touch rect can stay still, and leaves
    /// the pad with a transparent full-rect raycast surface.
    /// </summary>
    private static RectTransform RestructureMovementPad(HybridMovementPad pad)
    {
        var padRect = pad.GetComponent<RectTransform>();
        var padImage = pad.GetComponent<Image>();

        Sprite plateSprite = padImage != null ? padImage.sprite : null;
        Color plateColor = padImage != null ? padImage.color : Color.white;

        RectTransform visual = FindOrCreatePadVisual(padRect);

        var visualImage = visual.GetComponent<Image>();
        if (visualImage == null)
            visualImage = visual.gameObject.AddComponent<Image>();

        if (plateSprite != null)
            visualImage.sprite = plateSprite;
        visualImage.color = new Color(plateColor.r, plateColor.g, plateColor.b, 1f);
        visualImage.type = Image.Type.Simple;
        visualImage.preserveAspect = true;
        visualImage.raycastTarget = false;

        if (padImage != null)
        {
            padImage.sprite = null;
            padImage.color = new Color(0f, 0f, 0f, 0f);
            padImage.preserveAspect = false;
            padImage.raycastTarget = true;

            // GraphicRaycaster skips graphics whose CanvasRenderer is culled, and
            // cullTransparentMesh culls at zero alpha. Leaving it on would make the
            // transparent touch surface unhittable.
            var renderer = pad.GetComponent<CanvasRenderer>();
            if (renderer != null)
                renderer.cullTransparentMesh = false;
        }

        var legacyStick = pad.GetComponent<OnScreenStick>();
        if (legacyStick != null)
            Object.DestroyImmediate(legacyStick);

        return visual;
    }

    private static RectTransform FindOrCreatePadVisual(RectTransform padRect)
    {
        RectTransform visual = padRect.Find(PadVisualName) as RectTransform;

        if (visual == null)
        {
            var go = new GameObject(PadVisualName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.layer = padRect.gameObject.layer;
            visual = go.GetComponent<RectTransform>();
            visual.SetParent(padRect, false);
        }

        visual.anchorMin = Vector2.zero;
        visual.anchorMax = Vector2.one;
        visual.pivot = new Vector2(0.5f, 0.5f);
        visual.offsetMin = Vector2.zero;
        visual.offsetMax = Vector2.zero;
        visual.localScale = Vector3.one;
        visual.localRotation = Quaternion.identity;

        return visual;
    }

    private static void ConfigureMovementPad(HybridMovementPad pad, RectTransform padVisual)
    {
        var padRect = pad.GetComponent<RectTransform>();

        var so = new SerializedObject(pad);
        so.FindProperty("disc").objectReferenceValue = padRect;
        so.FindProperty("padVisual").objectReferenceValue = padVisual;
        so.FindProperty("dpadOverlay").objectReferenceValue = null;

        // FractionOfPadRect keeps the throw correct if the pad is ever resized or rescaled.
        so.FindProperty("deflectionRadiusMode").enumValueIndex =
            (int)HybridMovementPad.DeflectionRadiusMode.FractionOfPadRect;
        so.FindProperty("deflectionRadiusFraction").floatValue = 0.55f;
        so.FindProperty("deadzone").floatValue = 0.12f;
        so.FindProperty("digitalOnly").boolValue = true;

        so.FindProperty("travelRadiusPixels").floatValue = 28f;
        so.FindProperty("snapTravelToSector").boolValue = true;
        so.FindProperty("travelFollowSpeed").floatValue = 26f;
        so.FindProperty("segmentActivationMagnitude").floatValue = 0.35f;

        so.FindProperty("useReleaseWatchdog").boolValue = true;
        so.FindProperty("watchdogGraceSeconds").floatValue = 0.4f;
        so.FindProperty("m_ControlPath").stringValue = "<Gamepad>/leftStick";
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigureInputActions(MobileInputManager inputManager)
    {
        if (inputManager == null) return;

        var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
        if (asset == null)
        {
            Debug.LogWarning($"[MobileServicesPadAndAudioUpdater] InputActionAsset not found at {InputActionsPath}.");
            return;
        }

        var so = new SerializedObject(inputManager);
        so.FindProperty("inputActions").objectReferenceValue = asset;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// Public so the older six-button migration can reuse the same wiring instead of
    /// duplicating property names.
    /// </summary>
    public static void ConfigureAudioBridge(
        MobileHudButtonAudioBridge bridge,
        GameObject root,
        HybridMovementPad pad)
    {
        if (bridge == null) return;

        var buttonA = FindChildComponent<GameboyButton>(root.transform, "ButtonA");
        var buttonB = FindChildComponent<GameboyButton>(root.transform, "ButtonB");
        var start = FindChildComponent<GameboyButton>(root.transform, "Start");
        var select = FindChildComponent<GameboyButton>(root.transform, "Select");

        var so = new SerializedObject(bridge);
        so.FindProperty("enableAudio").boolValue = true;
        so.FindProperty("autoRegisterChildren").boolValue = true;
        so.FindProperty("includeInactiveChildren").boolValue = true;
        so.FindProperty("playDirectionalTicks").boolValue = true;
        so.FindProperty("autoResolveMovementPad").boolValue = true;
        so.FindProperty("movementPad").objectReferenceValue = pad;

        SerializedProperty table = so.FindProperty("roleSounds");
        table.arraySize = 5;
        SetRoleSound(table.GetArrayElementAtIndex(0), MobileHudButtonAudioBridge.ButtonSoundRole.Default,
            "UI_hud_tactile_press", "UI_hud_tactile_release");
        SetRoleSound(table.GetArrayElementAtIndex(1), MobileHudButtonAudioBridge.ButtonSoundRole.Confirm,
            "UI_hud_tactile_confirm", "UI_hud_tactile_release");
        SetRoleSound(table.GetArrayElementAtIndex(2), MobileHudButtonAudioBridge.ButtonSoundRole.Cancel,
            "UI_hud_tactile_cancel", "UI_hud_tactile_release");
        SetRoleSound(table.GetArrayElementAtIndex(3), MobileHudButtonAudioBridge.ButtonSoundRole.System,
            "UI_hud_tactile_system", null);
        SetRoleSound(table.GetArrayElementAtIndex(4), MobileHudButtonAudioBridge.ButtonSoundRole.Directional,
            "UI_hud_tactile_dpad", null);

        SerializedProperty overrides = so.FindProperty("buttonOverrides");
        overrides.arraySize = 4;
        SetOverride(overrides.GetArrayElementAtIndex(0), buttonA, MobileHudButtonAudioBridge.ButtonSoundRole.Confirm);
        SetOverride(overrides.GetArrayElementAtIndex(1), buttonB, MobileHudButtonAudioBridge.ButtonSoundRole.Cancel);
        SetOverride(overrides.GetArrayElementAtIndex(2), start, MobileHudButtonAudioBridge.ButtonSoundRole.System);
        SetOverride(overrides.GetArrayElementAtIndex(3), select, MobileHudButtonAudioBridge.ButtonSoundRole.System);

        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetRoleSound(
        SerializedProperty property,
        MobileHudButtonAudioBridge.ButtonSoundRole role,
        string pressAssetName,
        string releaseAssetName)
    {
        property.FindPropertyRelative("role").enumValueIndex = (int)role;
        property.FindPropertyRelative("pressEvent").objectReferenceValue = LoadSoundEvent(pressAssetName);
        property.FindPropertyRelative("releaseEvent").objectReferenceValue = LoadSoundEvent(releaseAssetName);
    }

    private static SoundEventSO LoadSoundEvent(string assetName)
    {
        if (string.IsNullOrEmpty(assetName)) return null;

        string path = TactileFolder + assetName + ".asset";
        var asset = AssetDatabase.LoadAssetAtPath<SoundEventSO>(path);
        if (asset == null)
            Debug.LogWarning($"[MobileServicesPadAndAudioUpdater] Sound event not found at {path}.");

        return asset;
    }

    private static void SetOverride(
        SerializedProperty property,
        GameboyButton button,
        MobileHudButtonAudioBridge.ButtonSoundRole role)
    {
        property.FindPropertyRelative("button").objectReferenceValue = button;
        property.FindPropertyRelative("role").enumValueIndex = (int)role;
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
}
#endif
