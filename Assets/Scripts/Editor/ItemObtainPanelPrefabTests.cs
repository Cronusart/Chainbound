using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

[TestFixture]
public sealed class ItemObtainPanelPrefabTests
{
    private const string PrefabPath = "Assets/Prefabs/UI/ItemObtainPanel.prefab";

    [Test]
    public void Prefab_PortraitLivesUnderPortraitBackground()
    {
        GameObject prefab = LoadPrefab();
        Transform portrait = FindChild(prefab.transform, "Portrait");
        Transform portraitBg = FindChild(prefab.transform, "PortraitBG");

        Assert.That(portrait, Is.Not.Null);
        Assert.That(portraitBg, Is.Not.Null);
        Assert.That(portrait.IsChildOf(portraitBg), Is.True);
        Assert.That(portrait.GetComponent<Image>(), Is.Not.Null);
    }

    [Test]
    public void Prefab_LabelsLiveUnderDetails()
    {
        GameObject prefab = LoadPrefab();
        Transform details = FindChild(prefab.transform, "Details");
        Transform title = FindChild(prefab.transform, "Title");
        Transform prompt = FindChild(prefab.transform, "Prompt");

        Assert.That(details, Is.Not.Null);
        Assert.That(title, Is.Not.Null);
        Assert.That(prompt, Is.Not.Null);
        Assert.That(title.parent, Is.SameAs(details));
        Assert.That(prompt.parent, Is.SameAs(details));
        Assert.That(title.GetComponent<TMP_Text>(), Is.Not.Null);
        Assert.That(prompt.GetComponent<TMP_Text>(), Is.Not.Null);
    }

    [Test]
    public void Prefab_LabelFontColorsStayInSync()
    {
        GameObject prefab = LoadPrefab();
        TMP_Text title = FindChild(prefab.transform, "Title").GetComponent<TMP_Text>();
        TMP_Text prompt = FindChild(prefab.transform, "Prompt").GetComponent<TMP_Text>();

        AssertVertexColorMatchesFontColor(title, "Title");
        AssertVertexColorMatchesFontColor(prompt, "Prompt");
    }

    [Test]
    public void Prefab_PresenterWiresAuthoredPortraitAndDetailsLabels()
    {
        GameObject prefab = LoadPrefab();
        ItemObtainPresenter presenter = prefab.GetComponent<ItemObtainPresenter>();
        Assert.That(presenter, Is.Not.Null);

        SerializedObject serialized = new SerializedObject(presenter);
        Image portrait = serialized.FindProperty("portraitImage").objectReferenceValue as Image;
        TMP_Text title = serialized.FindProperty("titleText").objectReferenceValue as TMP_Text;
        TMP_Text prompt = serialized.FindProperty("promptText").objectReferenceValue as TMP_Text;

        Assert.That(portrait, Is.Not.Null, "Presenter should reference the authored Portrait image.");
        Assert.That(portrait.transform.name, Is.EqualTo("Portrait"));
        Assert.That(portrait.transform.parent.name, Is.EqualTo("PortraitBG"));
        Assert.That(title, Is.Not.Null);
        Assert.That(title.transform.parent.name, Is.EqualTo("Details"));
        Assert.That(prompt, Is.Not.Null);
        Assert.That(prompt.transform.parent.name, Is.EqualTo("Details"));
    }

    private static GameObject LoadPrefab()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Assert.That(prefab, Is.Not.Null, $"Could not load {PrefabPath}");
        return prefab;
    }

    private static Transform FindChild(Transform root, string name)
    {
        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            if (transforms[i].name == name)
                return transforms[i];
        }

        return null;
    }

    private static void AssertVertexColorMatchesFontColor(TMP_Text text, string label)
    {
        SerializedObject serialized = new SerializedObject(text);
        Color fontColor = serialized.FindProperty("m_fontColor").colorValue;
        Color vertexColor = serialized.FindProperty("m_fontColor32").colorValue;

        Assert.That(
            ColorsMatch(fontColor, vertexColor),
            Is.True,
            $"{label} m_fontColor {fontColor} should match m_fontColor32 {vertexColor} so TMP mesh rebuilds keep the authored color.");
    }

    private static bool ColorsMatch(Color a, Color b)
    {
        return Mathf.Abs(a.r - b.r) < 0.01f &&
               Mathf.Abs(a.g - b.g) < 0.01f &&
               Mathf.Abs(a.b - b.b) < 0.01f &&
               Mathf.Abs(a.a - b.a) < 0.01f;
    }
}
