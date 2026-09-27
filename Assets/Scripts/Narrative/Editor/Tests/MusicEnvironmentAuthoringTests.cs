using System;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[TestFixture]
public sealed class MusicEnvironmentAuthoringTests
{
    private const string ProfilePath = "Assets/Data/Audio/Music/Environments/DistantHallway.asset";
    private const string AcademyScenePath = "Assets/Scenes/Academy_Interior.unity";

    [Test]
    public void DistantHallwayProfile_UsesRequestedDefaults()
    {
        MusicEnvironmentProfileSO profile = AssetDatabase.LoadAssetAtPath<MusicEnvironmentProfileSO>(ProfilePath);

        Assert.That(profile, Is.Not.Null);
        Assert.That(profile.ProfileId, Is.EqualTo("distant_hallway"));
        Assert.That(profile.LowPassCutoffHz, Is.EqualTo(1200f));
        Assert.That(profile.GainDb, Is.EqualTo(-4f));
        Assert.That(profile.EnterFadeSeconds, Is.EqualTo(3f));
        Assert.That(profile.ExitFadeSeconds, Is.EqualTo(3f));
        Assert.That(profile.Priority, Is.EqualTo(10));
    }

    [Test]
    public void AcademySecondFloor_HasIndependentAuthoredMusicEnvironmentVolume()
    {
        Scene scene = SceneManager.GetSceneByPath(AcademyScenePath);
        bool openedForTest = !scene.IsValid() || !scene.isLoaded;
        if (openedForTest)
            scene = EditorSceneManager.OpenScene(AcademyScenePath, OpenSceneMode.Additive);

        try
        {
            GameObject volumeObject = Array.Find(
                scene.GetRootGameObjects(),
                root => root.name == "MusicEnvironment_SecondFloor");
            Assert.That(volumeObject, Is.Not.Null);

            BoxCollider collider = volumeObject.GetComponent<BoxCollider>();
            MusicEnvironmentVolume volume = volumeObject.GetComponent<MusicEnvironmentVolume>();
            MusicEnvironmentProfileSO profile = AssetDatabase.LoadAssetAtPath<MusicEnvironmentProfileSO>(ProfilePath);

            Assert.That(collider, Is.Not.Null);
            Assert.That(collider.isTrigger, Is.True);
            Assert.That(collider.size.x, Is.GreaterThan(80f));
            Assert.That(collider.size.y, Is.EqualTo(17f).Within(0.01f));
            Assert.That(volume, Is.Not.Null);
            Assert.That(volume.Profile, Is.SameAs(profile));
            Assert.That(volumeObject.transform.parent, Is.Null, "The volume must stay active independently of floor-root activation.");
        }
        finally
        {
            if (openedForTest && scene.IsValid())
                EditorSceneManager.CloseScene(scene, removeScene: true);
        }
    }
}
