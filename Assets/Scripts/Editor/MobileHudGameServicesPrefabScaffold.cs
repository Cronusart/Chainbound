#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem.OnScreen;
using UnityEngine.UI;
using SignalJam.UI.MobileControls;

/// <summary>
/// One-shot prefab authoring for the mobile HUD stack under <c>GameServices.prefab</c>.
/// Intended when Unity MCP / Coplay bridge is not available from the agent: run from the
/// Unity menu after pulling changes.
///
/// Menu: <b>Tools / Chainbound / Mobile / Scaffold Mobile HUD in GameServices Prefab</b>
/// </summary>
public static class MobileHudGameServicesPrefabScaffold
{
    private const string PrefabPath = "Assets/Prefabs/GameServices/GameServices.prefab";

    [MenuItem("Tools/Chainbound/Mobile/Scaffold Mobile HUD in GameServices Prefab")]
    public static void Scaffold()
    {
        if (!EditorUtility.DisplayDialog(
                "Scaffold Mobile HUD",
                "This adds or replaces a child named \"MobileServices\" under the GameServices prefab root " +
                "(MobileHudCanvas, services, placeholder UI). Continue?",
                "OK",
                "Cancel"))
        {
            return;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            Transform rootT = root.transform;
            Transform existing = rootT.Find("MobileServices");
            if (existing != null)
            {
                UnityEngine.Object.DestroyImmediate(existing.gameObject);
            }

            GameObject mobileRoot = new GameObject("MobileServices");
            Undo.RegisterCreatedObjectUndo(mobileRoot, "Create MobileServices");
            mobileRoot.transform.SetParent(rootT, false);
            mobileRoot.transform.localPosition = Vector3.zero;
            mobileRoot.transform.localRotation = Quaternion.identity;
            mobileRoot.transform.localScale = Vector3.one;

            mobileRoot.AddComponent<MobileBootstrap>();
            var hudService = mobileRoot.AddComponent<MobileHudService>();
            var inputMgr = mobileRoot.AddComponent<MobileInputManager>();
            var router = mobileRoot.AddComponent<MobileControlRouter>();

            GameObject canvasGo = new GameObject("MobileHudCanvas");
            Undo.RegisterCreatedObjectUndo(canvasGo, "Create MobileHudCanvas");
            canvasGo.transform.SetParent(mobileRoot.transform, false);

            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.planeDistance = 100f;
            canvas.pixelPerfect = false;
            canvas.sortingOrder = 5000;

            canvasGo.AddComponent<MobileHudScreenSpaceCameraSetup>();

            int uiLayer = LayerMask.NameToLayer("UI");
            if (uiLayer >= 0)
                canvasGo.layer = uiLayer;
            canvasGo.GetComponent<RectTransform>().localScale = Vector3.one;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(2670f, 1200f);
            scaler.matchWidthOrHeight = 0.5f;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;

            canvasGo.AddComponent<GraphicRaycaster>();

            GameObject hudRoot = CreateUiObject("HudRoot", canvasGo.transform, true);
            hudRoot.AddComponent<SafeAreaAdapter>();
            var visibility = hudRoot.AddComponent<MobileHudVisibilityController>();

            CanvasGroup movementGroupCg = CreateGroup("MovementGroup", hudRoot.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(320f, 320f), new Vector2(140f, 140f));
            GameObject padGo = CreateUiObject("MovementPad", movementGroupCg.transform, false);
            var padRt = padGo.GetComponent<RectTransform>();
            padRt.anchorMin = Vector2.zero;
            padRt.anchorMax = Vector2.one;
            padRt.offsetMin = new Vector2(16f, 16f);
            padRt.offsetMax = new Vector2(-16f, -16f);
            padGo.AddComponent<Image>().color = new Color(0.2f, 0.22f, 0.28f, 0.65f);
            var hybrid = padGo.AddComponent<HybridMovementPad>();
            var stick = padGo.AddComponent<OnScreenStick>();
            SetControlPath(stick, "<Gamepad>/leftStick");

            CanvasGroup jumpSprintCg = CreateGroup("JumpSprintGroup", hudRoot.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(220f, 140f), new Vector2(-200f, 160f));
            GameObject jumpGo = CreateFaceButton("Jump", jumpSprintCg.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(100f, 100f), new Vector2(10f, 0f));
            AddOnScreenButton(jumpGo, "<Gamepad>/buttonSouth");
            GameObject sprintGo = CreateFaceButton("Sprint", jumpSprintCg.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(88f, 88f), new Vector2(-10f, 0f));
            AddOnScreenButton(sprintGo, "<Gamepad>/leftShoulder");
            SetGameboyInteractionMode(sprintGo.GetComponent<GameboyButton>(), GameboyButton.InteractionMode.Toggle);

            CanvasGroup actionCg = CreateGroup("ActionClusterGroup", hudRoot.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(300f, 300f), new Vector2(-150f, 150f));
            GameObject yGo = CreateFaceButton("ButtonY", actionCg.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(96f, 96f), new Vector2(0f, -10f));
            GameObject xGo = CreateFaceButton("ButtonX", actionCg.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(96f, 96f), new Vector2(10f, 0f));
            GameObject bGo = CreateFaceButton("ButtonB", actionCg.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(110f, 110f), new Vector2(-10f, 10f));
            AddOnScreenButton(bGo, "<Gamepad>/buttonWest");
            GameObject aGo = CreateFaceButton("ButtonA", actionCg.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(96f, 96f), new Vector2(-10f, 0f));
            AddOnScreenButton(aGo, "<Gamepad>/buttonEast");

            CanvasGroup systemCg = CreateGroup("SystemButtonsGroup", hudRoot.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(200f, 72f), new Vector2(-80f, -40f));
            GameObject startGo = CreateFaceButton("Start", systemCg.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(64f, 64f), new Vector2(-8f, 0f));
            GameObject selectGo = CreateFaceButton("Select", systemCg.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(64f, 64f), new Vector2(8f, 0f));

            CanvasGroup walletCg = CreateGroup("WalletGroup", hudRoot.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(120f, 72f), new Vector2(80f, -40f));
            GameObject walletGo = CreateFaceButton("Wallet", walletCg.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(72f, 72f), Vector2.zero);

            GameObject dialogueGo = CreateUiObject("DialogueAdvanceGroup", hudRoot.transform, true);
            var dialogueCg = dialogueGo.AddComponent<CanvasGroup>();
            var dlgImg = dialogueGo.AddComponent<Image>();
            dlgImg.color = new Color(0f, 0f, 0f, 0.02f);
            dlgImg.raycastTarget = true;

            WireHudGroups(visibility, hudService, movementGroupCg, jumpSprintCg, actionCg, systemCg, walletCg, dialogueCg);
            WireMobileInputManager(inputMgr, hudService, hybrid, jumpGo, sprintGo, yGo, xGo, bGo, aGo, startGo, selectGo, walletGo);
            WireRouter(router, inputMgr);

            EditorUtility.SetDirty(mobileRoot);
            EditorUtility.SetDirty(canvasGo);
            EditorUtility.SetDirty(hudRoot);
            EditorUtility.SetDirty(root);

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            AssetDatabase.SaveAssets();

            Debug.Log(
                $"[MobileHudGameServicesPrefabScaffold] Done. Open {PrefabPath} to tweak layout/sprites and verify OnScreen paths (X/Y/A are placeholders).");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void WireHudGroups(
        MobileHudVisibilityController visibility,
        MobileHudService hudService,
        CanvasGroup movement,
        CanvasGroup jumpSprint,
        CanvasGroup action,
        CanvasGroup system,
        CanvasGroup wallet,
        CanvasGroup dialogue)
    {
        var so = new SerializedObject(visibility);
        so.FindProperty("hudService").objectReferenceValue = hudService;

        SetHudGroup(so, "movementGroup", movement, MobileHudVisibilityController.ContextMask.Overworld);
        SetHudGroup(so, "jumpSprintGroup", jumpSprint, MobileHudVisibilityController.ContextMask.Overworld);
        SetHudGroup(
            so,
            "actionClusterGroup",
            action,
            MobileHudVisibilityController.ContextMask.Overworld | MobileHudVisibilityController.ContextMask.Battle);
        SetHudGroup(
            so,
            "systemButtonsGroup",
            system,
            MobileHudVisibilityController.ContextMask.Overworld | MobileHudVisibilityController.ContextMask.Battle);
        SetHudGroup(
            so,
            "walletGroup",
            wallet,
            MobileHudVisibilityController.ContextMask.Overworld | MobileHudVisibilityController.ContextMask.Battle);
        SetHudGroup(
            so,
            "dialogueAdvanceGroup",
            dialogue,
            MobileHudVisibilityController.ContextMask.Cutscene | MobileHudVisibilityController.ContextMask.Dialogue);

        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetHudGroup(
        SerializedObject so,
        string groupPropertyName,
        CanvasGroup cg,
        MobileHudVisibilityController.ContextMask mask)
    {
        SerializedProperty prop = so.FindProperty(groupPropertyName);
        if (prop == null) return;
        prop.FindPropertyRelative("group").objectReferenceValue = cg;
        prop.FindPropertyRelative("visibleIn").intValue = (int)mask;
        prop.FindPropertyRelative("dimAlphaWhenHidden").floatValue = 0f;
    }

    private static void WireMobileInputManager(
        MobileInputManager inputMgr,
        MobileHudService hudService,
        HybridMovementPad pad,
        GameObject jump,
        GameObject sprint,
        GameObject y,
        GameObject x,
        GameObject b,
        GameObject a,
        GameObject start,
        GameObject select,
        GameObject wallet)
    {
        var so = new SerializedObject(inputMgr);
        so.FindProperty("movementPad").objectReferenceValue = pad;
        so.FindProperty("buttonX").objectReferenceValue = x.GetComponent<GameboyButton>();
        so.FindProperty("buttonY").objectReferenceValue = y.GetComponent<GameboyButton>();
        so.FindProperty("buttonA").objectReferenceValue = a.GetComponent<GameboyButton>();
        so.FindProperty("buttonB").objectReferenceValue = b.GetComponent<GameboyButton>();
        so.FindProperty("jumpButton").objectReferenceValue = jump.GetComponent<GameboyButton>();
        so.FindProperty("sprintButton").objectReferenceValue = sprint.GetComponent<GameboyButton>();
        so.FindProperty("startButton").objectReferenceValue = start.GetComponent<GameboyButton>();
        so.FindProperty("selectButton").objectReferenceValue = select.GetComponent<GameboyButton>();
        so.FindProperty("solanaButton").objectReferenceValue = wallet.GetComponent<GameboyButton>();
        so.FindProperty("hudService").objectReferenceValue = hudService;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void WireRouter(MobileControlRouter router, MobileInputManager inputMgr)
    {
        var so = new SerializedObject(router);
        so.FindProperty("inputManager").objectReferenceValue = inputMgr;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static GameObject CreateUiObject(string name, Transform parent, bool stretch)
    {
        var go = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, "Create UI Object");
        go.layer = 5;
        var rt = go.GetComponent<RectTransform>();
        go.transform.SetParent(parent, false);
        if (stretch)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.localScale = Vector3.one;
        }
        return go;
    }

    private static CanvasGroup CreateGroup(
        string name,
        Transform parent,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 sizeDelta,
        Vector2 anchoredPosition)
    {
        var go = CreateUiObject(name, parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = sizeDelta;
        rt.anchoredPosition = anchoredPosition;
        var cg = go.AddComponent<CanvasGroup>();
        var img = go.AddComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0f);
        img.raycastTarget = false;
        return cg;
    }

    private static GameObject CreateFaceButton(
        string name,
        Transform parent,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 sizeDelta,
        Vector2 anchoredPosition)
    {
        var go = CreateUiObject(name, parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = sizeDelta;
        rt.anchoredPosition = anchoredPosition;
        var img = go.AddComponent<Image>();
        img.color = new Color(0.35f, 0.38f, 0.45f, 0.9f);
        img.raycastTarget = true;
        go.AddComponent<GameboyButton>();
        return go;
    }

    private static void AddOnScreenButton(GameObject go, string controlPath)
    {
        var osb = go.AddComponent<OnScreenButton>();
        SetControlPath(osb, controlPath);
    }

    private static void SetControlPath(UnityEngine.Object control, string path)
    {
        var so = new SerializedObject(control);
        SerializedProperty prop = so.FindProperty("m_ControlPath");
        if (prop == null)
        {
            throw new InvalidOperationException($"[MobileHudGameServicesPrefabScaffold] No m_ControlPath on {control.GetType().Name}");
        }
        prop.stringValue = path;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetGameboyInteractionMode(GameboyButton button, GameboyButton.InteractionMode mode)
    {
        if (button == null) return;
        var so = new SerializedObject(button);
        SerializedProperty prop = so.FindProperty("interactionMode");
        if (prop != null)
        {
            prop.enumValueIndex = (int)mode;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
#endif
