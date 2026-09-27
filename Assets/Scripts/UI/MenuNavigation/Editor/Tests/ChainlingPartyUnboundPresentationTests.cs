using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using SignalJam.UI.MenuNavigation.PartyTab;
using TMPro;
using UnityEngine;

public sealed class ChainlingPartyUnboundPresentationTests
{
    [Test]
    public void PrepareUnboundPresentation_HidesStackedViewLayoutsUntilBind()
    {
        var root = new GameObject("PartyUnboundPresentationTests");
        root.SetActive(false);
        try
        {
            var views = new GameObject("Views", typeof(RectTransform));
            views.transform.SetParent(root.transform, false);

            ChainlingPartyInfoViewLayout general = CreateView(
                views.transform,
                "GeneralView",
                ChainlingPartyViewMode.General);
            ChainlingPartyInfoViewLayout moves = CreateView(
                views.transform,
                "MovesView",
                ChainlingPartyViewMode.Moves);
            MakeRuntimeBindable(general);

            root.AddComponent<ChainlingPartyInfoPanelBinder>();
            root.SetActive(true);

            Assert.That(general.gameObject.activeSelf, Is.True);
            Assert.That(moves.gameObject.activeSelf, Is.False);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static ChainlingPartyInfoViewLayout CreateView(
        Transform parent,
        string name,
        ChainlingPartyViewMode mode)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        ChainlingPartyInfoViewLayout layout = go.AddComponent<ChainlingPartyInfoViewLayout>();
        layout.ConfigureMode(mode);
        return layout;
    }

    private static void MakeRuntimeBindable(ChainlingPartyInfoViewLayout layout)
    {
        var line = new GameObject("Line", typeof(RectTransform));
        line.transform.SetParent(layout.transform, false);
        TextMeshProUGUI tmp = line.AddComponent<TextMeshProUGUI>();
        SetPrivate(layout, "lineTemplate", tmp);
        SetPrivate(layout, "authoredLineSlots", new List<TMP_Text> { tmp });
        SetPrivate(layout, "linesRoot", layout.transform as RectTransform);
    }

    private static void SetPrivate(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"Expected private field '{fieldName}'.");
        field.SetValue(target, value);
    }
}
