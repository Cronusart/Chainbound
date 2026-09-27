#if UNITY_EDITOR
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class NpcInteractableAuthoringTests
{
    [Test]
    public void NpcPrefab_HasWiredInteractableIndicator()
    {
        AssertPrefabHasWiredIndicator(NpcInteractableAuthoring.NpcPrefabPath);
    }

    [Test]
    public void MomoPrefab_HasWiredInteractableIndicator()
    {
        AssertPrefabHasWiredIndicator(NpcInteractableAuthoring.MomoPrefabPath);
    }

    [Test]
    public void MartPrefab_HasWiredInteractableIndicator()
    {
        AssertPrefabHasWiredIndicator(NpcInteractableAuthoring.MartPrefabPath);
    }

    [Test]
    public void SailorPrefab_HasWiredInteractableIndicator()
    {
        AssertPrefabHasWiredIndicator(NpcInteractableAuthoring.SailorPrefabPath);
    }

    [TestCase("Assets/Prefabs/NPC/Fisher-Ape.prefab")]
    [TestCase("Assets/Prefabs/NPC/Crayon-Eater.prefab")]
    public void AmbientNpcPrefab_HasWiredInteractableIndicator(string path)
    {
        AssertPrefabHasWiredIndicator(path);
    }

    private static void AssertPrefabHasWiredIndicator(string path)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        Assert.That(prefab, Is.Not.Null, path);

        InteractableObject interactable = prefab.GetComponentInChildren<InteractableObject>(true);
        Assert.That(interactable, Is.Not.Null, path + " InteractableObject");

        InteractableIndicator indicator = prefab.GetComponentInChildren<InteractableIndicator>(true);
        Assert.That(indicator, Is.Not.Null, path + " InteractableIndicator");

        var serialized = new SerializedObject(interactable);
        Assert.That(
            serialized.FindProperty("indicator").objectReferenceValue,
            Is.SameAs(indicator),
            path + " indicator reference");
    }
}
#endif
