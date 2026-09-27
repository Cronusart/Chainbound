using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class IndoorVisualPresentationTests
{
    private readonly List<Object> cleanup = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        for (int i = cleanup.Count - 1; i >= 0; i--)
        {
            if (cleanup[i] != null)
                Object.DestroyImmediate(cleanup[i]);
        }

        cleanup.Clear();
    }

    [Test]
    public void ConfigureIndoorLight_ReplacesStickyProfileFromAPreviousRoom()
    {
        IndoorVisualProfileSO roomProfile = ScriptableObject.CreateInstance<IndoorVisualProfileSO>();
        IndoorVisualProfileSO labProfile = ScriptableObject.CreateInstance<IndoorVisualProfileSO>();
        cleanup.Add(roomProfile);
        cleanup.Add(labProfile);

        TimeOfDayVisualApplier applier = CreateApplier();
        IndoorDirectionalLight roomLight = CreateIndoorLight("RoomLight", roomProfile);
        IndoorDirectionalLight labLight = CreateIndoorLight("LabLight", labProfile);

        applier.ConfigureIndoorLight(roomLight);
        Assert.That(
            NarrativeTestUtility.GetPrivateField<IndoorVisualProfileSO>(applier, "indoorProfile"),
            Is.EqualTo(roomProfile));

        applier.ConfigureIndoorLight(labLight);
        Assert.That(
            NarrativeTestUtility.GetPrivateField<IndoorVisualProfileSO>(applier, "indoorProfile"),
            Is.EqualTo(labProfile));
        Assert.That(
            NarrativeTestUtility.GetPrivateField<Light>(applier, "indoorDirectionalLight"),
            Is.EqualTo(labLight.Light));
    }

    [Test]
    public void FindForScene_PrefersTheIndoorLightInThatScene()
    {
        IndoorVisualProfileSO profile = ScriptableObject.CreateInstance<IndoorVisualProfileSO>();
        cleanup.Add(profile);
        IndoorDirectionalLight labLight = CreateIndoorLight("LabLight", profile);

        IndoorDirectionalLight found = IndoorDirectionalLight.FindForScene(labLight.gameObject.scene);
        Assert.That(found, Is.EqualTo(labLight));
        Assert.That(found.gameObject.scene, Is.EqualTo(SceneManager.GetActiveScene()));
    }

    private TimeOfDayVisualApplier CreateApplier()
    {
        GameObject applierObject = new GameObject("VisualApplier");
        cleanup.Add(applierObject);
        return applierObject.AddComponent<TimeOfDayVisualApplier>();
    }

    private IndoorDirectionalLight CreateIndoorLight(string objectName, IndoorVisualProfileSO profile)
    {
        GameObject lightObject = new GameObject(objectName);
        cleanup.Add(lightObject);
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        IndoorDirectionalLight indoorLight = lightObject.AddComponent<IndoorDirectionalLight>();
        NarrativeTestUtility.SetPrivateField(indoorLight, "profile", profile);
        return indoorLight;
    }
}
