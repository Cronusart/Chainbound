using NUnit.Framework;
using SignalJam.UI.MenuNavigation;
using UnityEngine;

public class ColosseumMenuVisibilityTests
{
    [TearDown]
    public void TearDown()
    {
        ColosseumMenuVisibility.OverrideForTests = null;
    }

    [Test]
    public void ReleaseBuildOmitsTheColosseumTabWithoutShiftingTheOthers()
    {
        ColosseumMenuVisibility.OverrideForTests = false;
        MenuTabDefinitionSO inventory = Tab("inventory");
        MenuTabDefinitionSO colosseum = Tab(ColosseumMenuVisibility.TabId);
        MenuTabDefinitionSO profile = Tab("profile");
        try
        {
            MenuTabDefinitionSO[] visible = ColosseumMenuVisibility.ForMenuBar(new[] { inventory, colosseum, profile });
            Assert.AreSame(inventory, visible[0]);
            Assert.IsNull(visible[1]);
            Assert.AreSame(profile, visible[2]);
            Assert.IsFalse(ColosseumMenuVisibility.IsMenuTabAllowed(ColosseumMenuVisibility.TabId));
            Assert.IsTrue(ColosseumMenuVisibility.IsMenuTabAllowed("inventory"));
        }
        finally
        {
            Object.DestroyImmediate(inventory);
            Object.DestroyImmediate(colosseum);
            Object.DestroyImmediate(profile);
        }
    }

    [Test]
    public void DevBuildKeepsTheColosseumTab()
    {
        ColosseumMenuVisibility.OverrideForTests = true;
        MenuTabDefinitionSO colosseum = Tab(ColosseumMenuVisibility.TabId);
        try
        {
            MenuTabDefinitionSO[] visible = ColosseumMenuVisibility.ForMenuBar(new[] { colosseum });
            Assert.AreSame(colosseum, visible[0]);
            Assert.IsTrue(ColosseumMenuVisibility.IsMenuTabAllowed(ColosseumMenuVisibility.TabId));
        }
        finally
        {
            Object.DestroyImmediate(colosseum);
        }
    }

    private static MenuTabDefinitionSO Tab(string id)
    {
        MenuTabDefinitionSO definition = ScriptableObject.CreateInstance<MenuTabDefinitionSO>();
        UnityEditor.SerializedObject serialized = new UnityEditor.SerializedObject(definition);
        serialized.FindProperty("tabId").stringValue = id;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return definition;
    }
}
