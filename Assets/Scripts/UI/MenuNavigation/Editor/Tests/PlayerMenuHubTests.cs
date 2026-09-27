using NUnit.Framework;
using SignalJam.UI.MenuNavigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public class PlayerMenuHubTests
{
    [Test]
    public void MenuTabDefinition_DescriptionFallsBackToDisplayName()
    {
        MenuTabDefinitionSO definition = ScriptableObject.CreateInstance<MenuTabDefinitionSO>();
        try
        {
            SerializedObject so = new SerializedObject(definition);
            so.FindProperty("tabId").stringValue = "chainlings";
            so.FindProperty("displayName").stringValue = "Chainlings";
            so.FindProperty("description").stringValue = "";
            so.ApplyModifiedPropertiesWithoutUndo();

            Assert.AreEqual("Chainlings", definition.Description);
        }
        finally
        {
            Object.DestroyImmediate(definition);
        }
    }

    [Test]
    public void MenuTabDefinition_DescriptionUsesExplicitCopy()
    {
        MenuTabDefinitionSO definition = ScriptableObject.CreateInstance<MenuTabDefinitionSO>();
        try
        {
            SerializedObject so = new SerializedObject(definition);
            so.FindProperty("tabId").stringValue = "inventory";
            so.FindProperty("displayName").stringValue = "Inventory";
            so.FindProperty("description").stringValue = "Items and RAM.";
            so.ApplyModifiedPropertiesWithoutUndo();

            Assert.AreEqual("Items and RAM.", definition.Description);
        }
        finally
        {
            Object.DestroyImmediate(definition);
        }
    }

    [Test]
    public void MenuTabBar_LockedTabCanFocusButDoesNotSubmit()
    {
        GameObject root = new GameObject("TabBarTestRoot", typeof(RectTransform));
        root.SetActive(false);

        MenuTabDefinitionSO firstTab = CreateTab("chainlings", "Chainlings");
        MenuTabDefinitionSO secondTab = CreateTab("handbook", "Handbook");

        try
        {
            MenuTabBar tabBar = root.AddComponent<MenuTabBar>();
            Button firstButton = CreateButton("First", root.transform);
            Button secondButton = CreateButton("Second", root.transform);

            SerializedObject so = new SerializedObject(tabBar);
            SerializedProperty tabs = so.FindProperty("tabs");
            tabs.arraySize = 2;
            tabs.GetArrayElementAtIndex(0).objectReferenceValue = firstTab;
            tabs.GetArrayElementAtIndex(1).objectReferenceValue = secondTab;

            SerializedProperty bindings = so.FindProperty("bindings");
            bindings.arraySize = 2;
            BindButton(bindings.GetArrayElementAtIndex(0), firstButton);
            BindButton(bindings.GetArrayElementAtIndex(1), secondButton);
            so.ApplyModifiedPropertiesWithoutUndo();

            int focused = -1;
            int submitted = -1;
            int selected = -1;
            tabBar.TabFocused += index => focused = index;
            tabBar.TabSubmitted += index => submitted = index;
            tabBar.TabSelected += index => selected = index;

            root.SetActive(true);
            tabBar.SetLocked(1, true);

            secondButton.GetComponent<MenuTabButtonFocusRelay>().OnSelect(null);
            Assert.AreEqual(1, focused);

            secondButton.onClick.Invoke();
            Assert.AreEqual(1, focused);
            Assert.AreEqual(-1, submitted);
            Assert.AreEqual(-1, selected);

            tabBar.SetLocked(1, false);
            secondButton.onClick.Invoke();
            Assert.AreEqual(1, submitted);
            Assert.AreEqual(1, selected);
        }
        finally
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(firstTab);
            Object.DestroyImmediate(secondTab);
        }
    }

    [Test]
    public void PlayerMenuController_DefaultTabIdOverridesFallbackIndex()
    {
        GameObject root = new GameObject("PlayerMenuDefaultTabRoot", typeof(RectTransform));
        root.SetActive(false);
        MenuTabDefinitionSO inventory = CreateTab("inventory", "Inventory");
        MenuTabDefinitionSO chainlings = CreateTab("chainlings", "Chainlings");

        try
        {
            PlayerMenuController controller = root.AddComponent<PlayerMenuController>();
            SerializedObject so = new SerializedObject(controller);
            SerializedProperty tabs = so.FindProperty("tabDefinitions");
            tabs.arraySize = 2;
            tabs.GetArrayElementAtIndex(0).objectReferenceValue = inventory;
            tabs.GetArrayElementAtIndex(1).objectReferenceValue = chainlings;
            so.FindProperty("defaultTabId").stringValue = "chainlings";
            so.FindProperty("defaultTabIndex").intValue = 0;
            so.ApplyModifiedPropertiesWithoutUndo();

            Assert.AreEqual(1, controller.ResolveDefaultTabIndex());
        }
        finally
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(inventory);
            Object.DestroyImmediate(chainlings);
        }
    }

    [Test]
    public void MenuTabBar_ButtonVisualSuppressesLegacySelectionFrame()
    {
        GameObject root = new GameObject("TabBarVisualRoot", typeof(RectTransform));
        root.SetActive(false);
        MenuTabDefinitionSO chainlings = CreateTab("chainlings", "Chainlings");

        try
        {
            MenuTabBar tabBar = root.AddComponent<MenuTabBar>();
            Button button = CreateButton("Chainlings", root.transform);
            button.gameObject.AddComponent<PlayerMenuTabButtonVisual>();
            Image selectionFrame = CreateImage("SelectionFrame", button.transform);

            SerializedObject so = new SerializedObject(tabBar);
            SerializedProperty tabs = so.FindProperty("tabs");
            tabs.arraySize = 1;
            tabs.GetArrayElementAtIndex(0).objectReferenceValue = chainlings;

            SerializedProperty bindings = so.FindProperty("bindings");
            bindings.arraySize = 1;
            BindButton(bindings.GetArrayElementAtIndex(0), button);
            bindings.GetArrayElementAtIndex(0)
                .FindPropertyRelative("selectionFrame")
                .objectReferenceValue = selectionFrame;
            so.ApplyModifiedPropertiesWithoutUndo();

            root.SetActive(true);
            tabBar.SetSelectedIndex(0);

            Assert.IsFalse(selectionFrame.enabled);
        }
        finally
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(chainlings);
        }
    }

    [Test]
    public void PlayerMenuBootSequence_NoFramesCompletesImmediately()
    {
        GameObject root = new GameObject("BootSequenceTestRoot", typeof(RectTransform));
        GameObject background = new GameObject("Background", typeof(RectTransform));
        GameObject menuPanel = new GameObject("MenuPanel", typeof(RectTransform), typeof(CanvasGroup));
        background.transform.SetParent(root.transform, false);
        menuPanel.transform.SetParent(root.transform, false);

        try
        {
            PlayerMenuBootSequenceController boot = root.AddComponent<PlayerMenuBootSequenceController>();
            boot.ConfigureTargets(
                background.GetComponent<RectTransform>(),
                menuPanel.GetComponent<RectTransform>());

            bool completed = false;
            boot.PlayOpenSequence(() => completed = true);

            Assert.IsTrue(completed);
            Assert.IsFalse(boot.IsPlaying);
            Assert.AreEqual(1f, menuPanel.GetComponent<CanvasGroup>().alpha);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void PlayerMenuBootSequence_CancelHidesBootOverlay()
    {
        Texture2D texture = new Texture2D(2, 2);
        Sprite frame = Sprite.Create(texture, new Rect(0f, 0f, 2f, 2f), new Vector2(0.5f, 0.5f));
        GameObject root = new GameObject("BootOverlayCancelRoot", typeof(RectTransform));
        GameObject background = new GameObject("Background", typeof(RectTransform));
        GameObject menuPanel = new GameObject("MenuPanel", typeof(RectTransform), typeof(CanvasGroup));
        background.transform.SetParent(root.transform, false);
        menuPanel.transform.SetParent(root.transform, false);

        try
        {
            PlayerMenuBootSequenceController boot = root.AddComponent<PlayerMenuBootSequenceController>();
            boot.ConfigureTargets(
                background.GetComponent<RectTransform>(),
                menuPanel.GetComponent<RectTransform>());

            SerializedObject so = new SerializedObject(boot);
            SerializedProperty frames = so.FindProperty("bootFrames");
            frames.arraySize = 1;
            frames.GetArrayElementAtIndex(0).objectReferenceValue = frame;
            so.ApplyModifiedPropertiesWithoutUndo();

            boot.PlayOpenSequence(null);
            Transform overlay = root.transform.Find("BootOverlay");
            Assert.IsNotNull(overlay);
            Assert.IsTrue(overlay.gameObject.activeSelf);
            Assert.IsTrue(boot.IsPlaying);

            boot.Cancel();

            Assert.IsFalse(boot.IsPlaying);
            Assert.IsFalse(overlay.gameObject.activeSelf);
            Assert.AreEqual(0f, overlay.GetComponent<CanvasGroup>().alpha);
            Assert.IsFalse(overlay.GetComponent<Image>().enabled);
        }
        finally
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(frame);
            Object.DestroyImmediate(texture);
        }
    }

    [Test]
    public void PlayerMenuBootSequence_RevealFrameUsesOneBasedTiming()
    {
        GameObject root = new GameObject("BootSequenceTimingRoot", typeof(RectTransform));

        try
        {
            PlayerMenuBootSequenceController boot = root.AddComponent<PlayerMenuBootSequenceController>();
            SerializedObject so = new SerializedObject(boot);
            so.FindProperty("fps").floatValue = 10f;
            so.FindProperty("revealStartFrame").intValue = 5;
            so.ApplyModifiedPropertiesWithoutUndo();

            Assert.AreEqual(0.4f, boot.RevealDelaySeconds, 0.0001f);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static MenuTabDefinitionSO CreateTab(string id, string label)
    {
        MenuTabDefinitionSO definition = ScriptableObject.CreateInstance<MenuTabDefinitionSO>();
        SerializedObject so = new SerializedObject(definition);
        so.FindProperty("tabId").stringValue = id;
        so.FindProperty("displayName").stringValue = label;
        so.ApplyModifiedPropertiesWithoutUndo();
        return definition;
    }

    private static Button CreateButton(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        return go.GetComponent<Button>();
    }

    private static Image CreateImage(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        return go.GetComponent<Image>();
    }

    private static void BindButton(SerializedProperty binding, Button button)
    {
        binding.FindPropertyRelative("button").objectReferenceValue = button;
        binding.FindPropertyRelative("label").objectReferenceValue = null;
        binding.FindPropertyRelative("icon").objectReferenceValue = null;
        binding.FindPropertyRelative("selectionFrame").objectReferenceValue = null;
        binding.FindPropertyRelative("lockedBadge").objectReferenceValue = null;
        binding.FindPropertyRelative("lockedLabel").objectReferenceValue = null;
    }
}
