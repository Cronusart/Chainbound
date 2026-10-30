using System;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Idempotent wiring for the daily gift (Docs/Specs/Economy/DAILY_LOGIN_REWARDS_v1.md):
/// the gift block in EventsPanel.prefab, the gift boxes under the main-menu
/// Xmas tree hotspot, and the player-room gift child under XmasTree. Re-running
/// replaces only the objects this tool created. It never touches the room tree's
/// own InteractableObject or QuestProgressInteractionBridge profile.
/// </summary>
public static class DailyLoginGiftAuthoring
{
    public const string EventsPanelPrefabPath = "Assets/Prefabs/UI/MainMenu/EventsPanel.prefab";
    public const string MenuGiftSpritePath = "Assets/Sprites/UI/MainMenu/MainMenuRefactor/XmasTree/daily_gift_boxes.png";
    private const string MainMenuScenePath = "Assets/Scenes/main_menu.unity";
    private const string PlayerRoomScenePath = "Assets/Scenes/player_room.unity";
    private const string RoomMaterialFolder = "Assets/Sprites/Loomville/Player_Room";

    private const string GiftBlockName = "GiftBlock";
    private const string MenuGiftName = "DailyGiftBoxes";
    private const string RoomGiftName = "DailyGift";

    [MenuItem("Tools/Chainbound/Economy/Wire Daily Gift (Events Panel, Menu Tree, Room Tree)")]
    public static void WireAll()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        string previousScene = SceneManager.GetActiveScene().path;
        WireEventsPanelPrefab();
        WireMainMenuScene();
        WirePlayerRoomScene();
        if (!string.IsNullOrEmpty(previousScene))
            EditorSceneManager.OpenScene(previousScene, OpenSceneMode.Single);
        Debug.Log("[DailyLoginGiftAuthoring] Daily gift wired into EventsPanel.prefab, main_menu.unity and player_room.unity.");
    }

    public static void WireEventsPanelPrefab()
    {
        using (var scope = new PrefabUtility.EditPrefabContentsScope(EventsPanelPrefabPath))
        {
            EventsPanelController controller = scope.prefabContentsRoot.GetComponent<EventsPanelController>();
            if (controller == null)
                throw new InvalidOperationException($"{EventsPanelPrefabPath} has no EventsPanelController.");
            BuildEventsGiftBlock(controller, LoadMenuGiftSprite());
        }
    }

    /// <summary>
    /// Adds the gift block between the news feed and the close button. Shared with
    /// MainMenuObjectNavigationSceneBuilder so a menu rebuild keeps it.
    /// </summary>
    public static void BuildEventsGiftBlock(EventsPanelController controller, Sprite giftSprite)
    {
        Transform root = controller.transform;
        SerializedObject so = new SerializedObject(controller);
        Button close = so.FindProperty("closeButton").objectReferenceValue as Button;
        TMP_Text title = so.FindProperty("titleLabel").objectReferenceValue as TMP_Text;
        if (close == null || title == null)
            throw new InvalidOperationException("EventsPanel needs closeButton and titleLabel before the gift block is added.");

        Transform existing = root.Find(GiftBlockName);
        if (existing != null)
            UnityEngine.Object.DestroyImmediate(existing.gameObject);

        // Make room: the feed keeps the top, the gift sits above BACK.
        Transform scroll = root.Find("Scroll");
        if (scroll is RectTransform scrollRect)
        {
            scrollRect.anchoredPosition = new Vector2(0f, 55f);
            scrollRect.sizeDelta = new Vector2(720f, 170f);
        }

        GameObject block = new GameObject(GiftBlockName, typeof(RectTransform));
        block.transform.SetParent(root, false);
        block.transform.SetSiblingIndex(close.transform.GetSiblingIndex());
        SetRect((RectTransform)block.transform, new Vector2(0f, -90f), new Vector2(720f, 100f));

        GameObject graphic = new GameObject("GiftGraphic", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        graphic.transform.SetParent(block.transform, false);
        SetRect((RectTransform)graphic.transform, new Vector2(-300f, 0f), new Vector2(112f, 70f));
        Image graphicImage = graphic.GetComponent<Image>();
        graphicImage.sprite = giftSprite;
        graphicImage.preserveAspect = true;
        graphicImage.raycastTarget = false;

        GameObject claimObject = UnityEngine.Object.Instantiate(close.gameObject, block.transform, false);
        claimObject.name = "ClaimButton";
        SetRect((RectTransform)claimObject.transform, new Vector2(40f, 20f), new Vector2(460f, 52f));
        Button claim = claimObject.GetComponent<Button>();
        claim.onClick = new Button.ButtonClickedEvent();
        TMP_Text claimLabel = claimObject.GetComponentInChildren<TMP_Text>(true);
        if (claimLabel != null)
            claimLabel.text = "Claim 10 RAM + 1 Signal Orb";

        GameObject statusObject = UnityEngine.Object.Instantiate(title.gameObject, block.transform, false);
        statusObject.name = "GiftStatus";
        SetRect((RectTransform)statusObject.transform, new Vector2(40f, -28f), new Vector2(600f, 44f));
        TMP_Text status = statusObject.GetComponent<TMP_Text>();
        status.text = "";
        status.fontSize = 18f;
        status.fontStyle = FontStyles.Normal;
        status.alignment = TextAlignmentOptions.Center;
        status.textWrappingMode = TextWrappingModes.Normal;
        status.raycastTarget = false;

        block.SetActive(false);

        so.Update();
        so.FindProperty("giftBlockRoot").objectReferenceValue = block;
        so.FindProperty("giftGraphic").objectReferenceValue = graphic;
        so.FindProperty("giftStatusLabel").objectReferenceValue = status;
        so.FindProperty("claimButton").objectReferenceValue = claim;
        so.FindProperty("claimButtonLabel").objectReferenceValue = claimLabel;
        SerializedProperty order = so.FindProperty("navigationOrder");
        order.arraySize = 2;
        order.GetArrayElementAtIndex(0).objectReferenceValue = claim;
        order.GetArrayElementAtIndex(1).objectReferenceValue = close;
        so.FindProperty("defaultSelection").objectReferenceValue = close;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(controller);
    }

    public static Sprite LoadMenuGiftSprite()
    {
        if (AssetImporter.GetAtPath(MenuGiftSpritePath) is TextureImporter importer &&
            (importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single))
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.SaveAndReimport();
        }

        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(MenuGiftSpritePath);
        if (sprite == null)
            Debug.LogWarning($"[DailyLoginGiftAuthoring] {MenuGiftSpritePath} is not a sprite yet; the gift graphic will be blank.");
        return sprite;
    }

    private static void WireMainMenuScene()
    {
        Scene scene = EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);
        MainMenuObjectHotspot tree = FindInScene<MainMenuObjectHotspot>(scene, "XmasTree");
        if (tree == null)
            throw new InvalidOperationException("main_menu.unity has no XmasTree hotspot.");

        Transform visualRoot = tree.transform.Find("VisualRoot");
        if (visualRoot == null)
            visualRoot = tree.transform;
        Transform existing = visualRoot.Find(MenuGiftName);
        if (existing != null)
            UnityEngine.Object.DestroyImmediate(existing.gameObject);

        GameObject boxes = new GameObject(MenuGiftName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        boxes.transform.SetParent(visualRoot, false);
        boxes.transform.SetAsLastSibling();
        Image image = boxes.GetComponent<Image>();
        image.sprite = LoadMenuGiftSprite();
        image.preserveAspect = true;
        image.raycastTarget = false;

        RectTransform rect = (RectTransform)boxes.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0f);
        Image body = new SerializedObject(tree).FindProperty("bodyImage").objectReferenceValue as Image;
        if (body != null)
        {
            // Sit on the tree's base, centred under the art.
            var corners = new Vector3[4];
            body.rectTransform.GetWorldCorners(corners);
            Vector3 bottomLeft = visualRoot.InverseTransformPoint(corners[0]);
            Vector3 topRight = visualRoot.InverseTransformPoint(corners[2]);
            float width = Mathf.Abs(topRight.x - bottomLeft.x);
            float height = Mathf.Abs(topRight.y - bottomLeft.y);
            rect.sizeDelta = new Vector2(width * 0.42f, width * 0.42f * 80f / 128f);
            rect.anchoredPosition = new Vector2((bottomLeft.x + topRight.x) * 0.5f, bottomLeft.y + height * 0.02f);
        }
        else
        {
            rect.sizeDelta = new Vector2(160f, 100f);
            rect.anchoredPosition = Vector2.zero;
        }

        boxes.SetActive(false);

        DailyLoginGiftVisual visual = tree.GetComponent<DailyLoginGiftVisual>();
        if (visual == null)
            visual = tree.gameObject.AddComponent<DailyLoginGiftVisual>();
        SetObject(visual, "target", boxes);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static void WirePlayerRoomScene()
    {
        Scene scene = EditorSceneManager.OpenScene(PlayerRoomScenePath, OpenSceneMode.Single);
        QuestProgressInteractionBridge bridge = FindInScene<QuestProgressInteractionBridge>(scene, "XmasTree");
        if (bridge == null)
            throw new InvalidOperationException("player_room.unity has no XmasTree with a QuestProgressInteractionBridge.");

        Transform tree = bridge.transform;
        Transform existing = tree.Find(RoomGiftName);
        if (existing != null)
            UnityEngine.Object.DestroyImmediate(existing.gameObject);

        Bounds bounds = ResolveBounds(tree);
        Vector3 basePoint = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        float footprint = Mathf.Max(0.4f, Mathf.Min(bounds.extents.x, bounds.extents.z));

        GameObject gift = new GameObject(RoomGiftName);
        gift.layer = tree.gameObject.layer;
        gift.transform.SetParent(tree, false);
        DailyLoginTreeGift treeGift = gift.AddComponent<DailyLoginTreeGift>();

        GameObject giftRoot = new GameObject("GiftRoot");
        giftRoot.layer = tree.gameObject.layer;
        giftRoot.transform.SetParent(gift.transform, false);

        Material red = EnsureMaterial("DailyGift_Red", new Color(0.78f, 0.17f, 0.24f));
        Material gold = EnsureMaterial("DailyGift_Gold", new Color(0.98f, 0.78f, 0.27f));
        Material green = EnsureMaterial("DailyGift_Green", new Color(0.18f, 0.59f, 0.35f));
        float unit = footprint * 0.35f;
        Vector3 front = basePoint + new Vector3(0f, 0f, -footprint * 0.55f);
        CreateBox(giftRoot.transform, "GiftBox_Red", front + new Vector3(-unit, unit * 0.5f, 0f), Vector3.one * unit, red);
        CreateBox(giftRoot.transform, "GiftBox_Gold", front + new Vector3(unit * 0.15f, unit * 0.35f, -unit * 0.4f), Vector3.one * (unit * 0.7f), gold);
        CreateBox(giftRoot.transform, "GiftBox_Green", front + new Vector3(unit, unit * 0.4f, 0.1f * unit), new Vector3(unit, unit * 0.8f, unit), green);

        // Separate interact volume, active only with the boxes.
        GameObject interact = new GameObject("GiftInteract");
        interact.layer = tree.gameObject.layer;
        interact.transform.SetParent(giftRoot.transform, false);
        interact.transform.position = front + new Vector3(0f, unit * 0.5f, 0f);
        SetWorldScale(interact.transform, Vector3.one);
        BoxCollider volume = interact.AddComponent<BoxCollider>();
        volume.isTrigger = true;
        volume.size = new Vector3(unit * 4f, unit * 3f, unit * 4f);
        InteractableObject interactable = interact.AddComponent<InteractableObject>();

        Transform treeIndicator = tree.Find("Indicator");
        if (treeIndicator != null)
        {
            GameObject indicator = UnityEngine.Object.Instantiate(treeIndicator.gameObject, interact.transform, false);
            indicator.name = "Indicator";
            indicator.transform.position = front + new Vector3(0f, unit * 2.2f, 0f);
            InteractableIndicator indicatorComponent = indicator.GetComponent<InteractableIndicator>();
            if (indicatorComponent != null)
                SetObject(interactable, "indicator", indicatorComponent);
        }

        FieldInfo eventField = typeof(InteractableObject).GetField("onInteracted", BindingFlags.Instance | BindingFlags.NonPublic);
        if (eventField == null || !(eventField.GetValue(interactable) is UnityEvent onInteracted))
            throw new InvalidOperationException("InteractableObject.onInteracted was not found.");
        UnityEventTools.AddPersistentListener(onInteracted, treeGift.Claim);

        giftRoot.SetActive(false);
        SetObject(treeGift, "giftRoot", giftRoot);
        EditorUtility.SetDirty(interactable);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static void CreateBox(Transform parent, string name, Vector3 worldPosition, Vector3 worldScale, Material material)
    {
        GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = name;
        box.layer = parent.gameObject.layer;
        UnityEngine.Object.DestroyImmediate(box.GetComponent<Collider>());
        box.transform.SetParent(parent, false);
        box.transform.position = worldPosition;
        SetWorldScale(box.transform, worldScale);
        box.GetComponent<MeshRenderer>().sharedMaterial = material;
    }

    private static Material EnsureMaterial(string name, Color color)
    {
        string path = $"{RoomMaterialFolder}/{name}.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null)
            return material;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");
        material = new Material(shader) { name = name };
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static Bounds ResolveBounds(Transform tree)
    {
        Renderer[] renderers = tree.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length > 0)
        {
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        Collider collider = tree.GetComponent<Collider>();
        return collider != null ? collider.bounds : new Bounds(tree.position, Vector3.one);
    }

    private static void SetWorldScale(Transform transform, Vector3 worldScale)
    {
        Vector3 parentScale = transform.parent != null ? transform.parent.lossyScale : Vector3.one;
        transform.localScale = new Vector3(
            worldScale.x / NonZero(parentScale.x),
            worldScale.y / NonZero(parentScale.y),
            worldScale.z / NonZero(parentScale.z));
    }

    private static float NonZero(float value)
    {
        return Mathf.Abs(value) < 0.0001f ? 1f : value;
    }

    private static T FindInScene<T>(Scene scene, string objectName) where T : Component
    {
        foreach (GameObject rootObject in scene.GetRootGameObjects())
        {
            foreach (T candidate in rootObject.GetComponentsInChildren<T>(true))
            {
                if (candidate.gameObject.name == objectName)
                    return candidate;
            }
        }

        return null;
    }

    private static void SetRect(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void SetObject(UnityEngine.Object target, string field, UnityEngine.Object value)
    {
        SerializedObject so = new SerializedObject(target);
        SerializedProperty property = so.FindProperty(field);
        if (property == null)
            throw new InvalidOperationException($"{target.GetType().Name}.{field} is not serialized.");
        property.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
    }
}
