using Chainbound.Combat;
using NUnit.Framework;
using SignalJam.UI.MenuNavigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public sealed class InventoryViewTests
{
    [Test]
    public void InventoryPresentationCatalog_ResolvesByItemId()
    {
        InventoryItemPresentationCatalogSO catalog = ScriptableObject.CreateInstance<InventoryItemPresentationCatalogSO>();
        try
        {
            SerializedObject so = new SerializedObject(catalog);
            SerializedProperty entries = so.FindProperty("entries");
            entries.arraySize = 1;
            SerializedProperty entry = entries.GetArrayElementAtIndex(0);
            entry.FindPropertyRelative("itemId").stringValue = "chainling_update";
            entry.FindPropertyRelative("displayNameOverride").stringValue = "Menu Patch";
            so.ApplyModifiedPropertiesWithoutUndo();

            bool found = catalog.TryResolve(
                new MenuInventoryItemStack { itemId = "chainling_update" },
                null,
                out InventoryItemPresentationEntry presentation);

            Assert.IsTrue(found);
            Assert.IsNotNull(presentation);
            Assert.AreEqual("Menu Patch", presentation.displayNameOverride);
        }
        finally
        {
            Object.DestroyImmediate(catalog);
        }
    }

    [Test]
    public void InventoryScreen_BuildDefaultLayoutCreatesDraftStructure()
    {
        GameObject root = new GameObject("InventoryViewTestRoot", typeof(RectTransform), typeof(CanvasGroup));
        try
        {
            InventoryScreen screen = root.AddComponent<InventoryScreen>();
            screen.BuildDefaultLayoutForEditor();

            Assert.IsNotNull(root.transform.Find("SafeContent/CategoryPanel"));
            Assert.IsNotNull(root.transform.Find("SafeContent/ListPanel/ItemListArea"));
            Assert.IsNotNull(root.transform.Find("SafeContent/ListPanel/ItemListArea/ItemRowsViewport")?.GetComponent<ScrollRect>());
            Assert.IsNotNull(root.transform.Find("SafeContent/ListPanel/ItemListArea/ItemRowsViewport/Rows/RowStripeBackground")?.GetComponent<InventoryRowStripeBackground>());
            Assert.IsNotNull(root.transform.Find("SafeContent/ListPanel/ItemListArea/ItemRowsScrollbar")?.GetComponent<Scrollbar>());
            Assert.IsNotNull(root.transform.Find("SafeContent/ListPanel/ItemDetailArea/ItemPortraitFrame/ItemPortrait")?.GetComponent<Image>());
            Assert.IsNotNull(root.transform.Find("SafeContent/ListPanel/ItemDetailArea/ItemDescriptionFrame/DetailBody"));
            Assert.IsNotNull(root.transform.Find("SafeContent/ActionsPanel/ActionPrompt")?.GetComponent<TMPro.TMP_Text>());
            Assert.IsNotNull(root.transform.Find("SafeContent/ActionsPanel/ActionButtonsRoot/UseButton")?.GetComponent<Button>());
            Assert.IsNotNull(root.transform.Find("SafeContent/ActionsPanel/ActionButtonsRoot/DropButton")?.GetComponent<Button>());
            InventoryItemUsePopupController usePopup = root.GetComponentInChildren<InventoryItemUsePopupController>(true);
            Assert.IsNotNull(usePopup);
            Assert.IsNotNull(root.transform.Find("UsePopup/UsePopupRoot"));
            Transform themeBackground = root.transform.Find("UsePopup/UsePopupRoot/UsePopupPanel/PopupThemeBackground");
            Assert.IsNotNull(themeBackground);
            Assert.IsNotNull(themeBackground.GetComponent<PlayerMenuBackgroundController>());
            // Shape mask is only added when UsePopupPanel has a sprite Image (authored prefab).
            Image panelImage = root.transform.Find("UsePopup/UsePopupRoot/UsePopupPanel")?.GetComponent<Image>();
            if (panelImage != null && panelImage.sprite != null)
            {
                Assert.IsNotNull(themeBackground.GetComponent<Mask>());
                Assert.IsNotNull(themeBackground.GetComponent<Image>());
            }
            Transform targetRow = root.transform.Find("UsePopup/UsePopupRoot/UsePopupPanel/TargetRow");
            Assert.IsNotNull(targetRow);
            Assert.IsNotNull(targetRow.GetComponent<UnityEngine.UI.VerticalLayoutGroup>());
            Assert.IsNotNull(targetRow.Find("TargetSlotTemplate"));
            Assert.IsNotNull(
                root.transform.Find("UsePopup/UsePopupRoot/UsePopupPanel/DialoguePanel/Message")
                ?? root.transform.Find("UsePopup/UsePopupRoot/UsePopupPanel/Message"));
            Transform legacyButtons = root.transform.Find("UsePopup/UsePopupRoot/UsePopupPanel/Buttons");
            if (legacyButtons != null)
                Assert.IsFalse(legacyButtons.gameObject.activeSelf);

            RectTransform categoryFrame = root.transform.Find("SafeContent/CategorySectionFrame") as RectTransform;
            RectTransform listFrame = root.transform.Find("SafeContent/ListSectionFrame") as RectTransform;
            RectTransform actionsFrame = root.transform.Find("SafeContent/ActionsSectionFrame") as RectTransform;
            Assert.IsNotNull(categoryFrame);
            Assert.IsNotNull(listFrame);
            Assert.IsNotNull(actionsFrame);
            Assert.AreEqual(new Vector2(0f, 0.5f), categoryFrame.anchorMin);
            Assert.AreEqual(new Vector2(0.5f, 0.5f), listFrame.anchorMin);
            Assert.AreEqual(new Vector2(1f, 0.5f), actionsFrame.anchorMin);
            Assert.AreEqual(new Vector2(350f, 510f), categoryFrame.sizeDelta);
            Assert.AreEqual(new Vector2(1640f, 1000f), listFrame.sizeDelta);
            Assert.AreEqual(new Vector2(350f, 510f), actionsFrame.sizeDelta);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }
}
