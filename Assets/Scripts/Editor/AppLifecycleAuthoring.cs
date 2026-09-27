#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using SignalJam.UI.MenuNavigation;

/// <summary>Creates the lifecycle policy/overlay assets and wires them into GameServices.</summary>
public static class AppLifecycleAuthoring
{
    private const string BuiltinFontName = "LegacyRuntime.ttf";
    private const string PolicyPath = "Assets/Data/UI/AppInterruptPolicy.asset";
    private const string OverlayPath = "Assets/Prefabs/UI/AppInterruptOverlay.prefab";
    private const string ServicesPath = "Assets/Prefabs/GameServices/GameServices.prefab";

    [MenuItem("Tools/Signal Jam/App Lifecycle/Wire GameServices")]
    public static void WireGameServices()
    {
        Debug.Log("[AppLifecycleAuthoring] Starting lifecycle wiring.");
        EnsureFolder("Assets/Data/UI");
        EnsureFolder("Assets/Prefabs/UI");

        AppInterruptPolicySO policy = AssetDatabase.LoadAssetAtPath<AppInterruptPolicySO>(PolicyPath);
        if (policy == null)
        {
            policy = ScriptableObject.CreateInstance<AppInterruptPolicySO>();
            policy.ResetToDefaults();
            AssetDatabase.CreateAsset(policy, PolicyPath);
        }
        EditorUtility.SetDirty(policy);

        GameObject overlayPrefab = CreateOverlayPrefab();
        if (overlayPrefab == null) { Debug.LogError("[AppLifecycleAuthoring] Overlay prefab could not be created."); return; }
        GameObject services = PrefabUtility.LoadPrefabContents(ServicesPath);
        try
        {
            AppLifecycleService lifecycle = services.GetComponentInChildren<AppLifecycleService>(true);
            if (lifecycle == null)
            {
                GameObject serviceObject = new GameObject("AppLifecycleService");
                serviceObject.transform.SetParent(services.transform, false);
                lifecycle = serviceObject.AddComponent<AppLifecycleService>();
            }

            Transform overlayTransform = lifecycle.transform.Find("AppInterruptOverlay");
            if (overlayTransform == null)
            {
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(overlayPrefab, lifecycle.transform);
                instance.name = "AppInterruptOverlay";
                overlayTransform = instance.transform;
            }

            AppInterruptOverlayController overlay = overlayTransform.GetComponent<AppInterruptOverlayController>();
            SerializedObject serializedLifecycle = new SerializedObject(lifecycle);
            serializedLifecycle.FindProperty("policy").objectReferenceValue = policy;
            serializedLifecycle.FindProperty("overlay").objectReferenceValue = overlay;
            serializedLifecycle.ApplyModifiedPropertiesWithoutUndo();

            if (services.GetComponentInChildren<CombatMenuSuppressionBridge>(true) == null)
            {
                GameObject bridge = new GameObject("CombatMenuSuppressionBridge");
                bridge.transform.SetParent(services.transform, false);
                bridge.AddComponent<CombatMenuSuppressionBridge>();
            }

            PrefabUtility.SaveAsPrefabAsset(services, ServicesPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(services);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[AppLifecycleAuthoring] Lifecycle policy, overlay prefab, and GameServices wiring are ready.");
    }

    private static GameObject CreateOverlayPrefab()
    {
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(OverlayPath);
        if (existing != null) return existing;

        GameObject root = new GameObject("AppInterruptOverlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup), typeof(GraphicRaycaster), typeof(AppInterruptOverlayController));
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5000;

        GameObject panel = CreateUiObject("Panel", root.transform);
        Image panelImage = panel.AddComponent<Image>();
        panelImage.color = new Color(0.02f, 0.02f, 0.03f, 0.94f);
        Stretch(panel.GetComponent<RectTransform>());

        GameObject content = CreateUiObject("Content", panel.transform);
        RectTransform contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0.5f, 0.5f);
        contentRect.anchorMax = new Vector2(0.5f, 0.5f);
        contentRect.sizeDelta = new Vector2(700f, 260f);
        contentRect.anchoredPosition = Vector2.zero;

        Text title = CreateText("Title", content.transform, 30, FontStyle.Bold, new Vector2(0f, 70f));
        title.alignment = TextAnchor.MiddleCenter;
        Text body = CreateText("Body", content.transform, 20, FontStyle.Normal, new Vector2(0f, 15f));
        body.alignment = TextAnchor.MiddleCenter;
        Button continueButton = CreateButton("Continue", content.transform, new Vector2(-170f, -70f));
        Button fleeButton = CreateButton("Flee", content.transform, new Vector2(0f, -70f));
        Button cancelButton = CreateButton("Cancel", content.transform, new Vector2(170f, -70f));

        AppInterruptOverlayController controller = root.GetComponent<AppInterruptOverlayController>();
        SerializedObject serialized = new SerializedObject(controller);
        serialized.FindProperty("canvasGroup").objectReferenceValue = root.GetComponent<CanvasGroup>();
        serialized.FindProperty("titleText").objectReferenceValue = title;
        serialized.FindProperty("bodyText").objectReferenceValue = body;
        serialized.FindProperty("continueButton").objectReferenceValue = continueButton;
        serialized.FindProperty("fleeButton").objectReferenceValue = fleeButton;
        serialized.FindProperty("cancelButton").objectReferenceValue = cancelButton;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, OverlayPath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    private static GameObject CreateUiObject(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    private static Text CreateText(string name, Transform parent, int size, FontStyle style, Vector2 position)
    {
        GameObject go = CreateUiObject(name, parent);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(650f, 50f);
        rect.anchoredPosition = position;
        Text text = go.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>(BuiltinFontName);
        text.fontSize = size;
        text.fontStyle = style;
        text.color = Color.white;
        text.text = name;
        return text;
    }

    private static Button CreateButton(string label, Transform parent, Vector2 position)
    {
        GameObject go = CreateUiObject(label + "Button", parent);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(180f, 48f);
        rect.anchoredPosition = position;
        Image image = go.AddComponent<Image>();
        image.color = new Color(0.15f, 0.15f, 0.2f, 1f);
        Button button = go.AddComponent<Button>();
        Text text = CreateText("Label", go.transform, 18, FontStyle.Bold, Vector2.zero);
        text.text = label;
        text.rectTransform.sizeDelta = rect.sizeDelta;
        return button;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void EnsureFolder(string path)
    {
        string[] parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }
}
#endif
