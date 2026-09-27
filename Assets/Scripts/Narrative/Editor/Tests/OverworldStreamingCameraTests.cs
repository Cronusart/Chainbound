using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

[TestFixture]
public sealed class OverworldStreamingCameraTests
{
    private readonly List<Object> cleanup = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        Camera persistent = SceneCameraResolver.PersistentGameplayCamera;
        if (persistent != null)
            SceneCameraResolver.ClearPersistentCamera(persistent);

        for (int i = cleanup.Count - 1; i >= 0; i--)
        {
            if (cleanup[i] != null)
                Object.DestroyImmediate(cleanup[i]);
        }

        cleanup.Clear();
    }

    [Test]
    public void Neutralize_DisablesReloadedHubCameraRigWithoutTouchingPersistentPresenter()
    {
        Camera persistent = CreateCamera("PersistentOverworldCamera", tagAsMain: false);
        persistent.gameObject.AddComponent<TopDownCameraController3D>();

        GameObject leftoverRoot = new GameObject("CameraRigRoot");
        cleanup.Add(leftoverRoot);
        Camera leftover = leftoverRoot.AddComponent<Camera>();
        leftoverRoot.tag = "MainCamera";
        leftoverRoot.AddComponent<AudioListener>();
        leftoverRoot.AddComponent<TopDownCameraController3D>();

        OverworldStreamingConfigSO config = OverworldStreamingConfigSO.CreateRuntimeDefault();
        cleanup.Add(config);
        SerializedObject serialized = new SerializedObject(config);
        serialized.FindProperty("hubSceneId").stringValue = leftoverRoot.scene.name;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        Assert.That(config.ContainsScene(leftoverRoot.scene.name), Is.True);
        Assert.That(leftover.enabled, Is.True);

        OverworldStreamingService.NeutralizeDuplicateOverworldCameraRigs(
            persistent,
            persistent.gameObject,
            config);

        Assert.That(persistent != null && persistent.enabled, Is.True);
        Assert.That(leftoverRoot == null || leftoverRoot.Equals(null) || !leftoverRoot.activeSelf);
        if (leftover != null && !leftover.Equals(null))
            Assert.That(leftover.enabled, Is.False);
    }

    [Test]
    public void Resolve_StillPrefersPersistentAfterLeftoverHubCameraIsTaggedMain()
    {
        Camera leftover = CreateCamera("CameraRigRoot", tagAsMain: true);
        leftover.gameObject.AddComponent<TopDownCameraController3D>();
        Camera persistent = CreateCamera("PersistentOverworldCamera", tagAsMain: false);
        TopDownCameraController3D persistentController = persistent.gameObject.AddComponent<TopDownCameraController3D>();

        SceneCameraResolver.RegisterPersistentCamera(persistent);

        Scene leftoverScene = leftover.gameObject.scene;
        Assert.That(SceneCameraResolver.Resolve(leftoverScene), Is.SameAs(persistent));
        Assert.That(SceneCameraResolver.ResolveGameplayCameraController(), Is.SameAs(persistentController));

        SceneCameraResolver.ClearPersistentCamera(persistent);
    }

    [Test]
    public void ShouldReleasePersistentCamera_ForCombatAndInteriorsButNotOverworldChunks()
    {
        OverworldStreamingConfigSO config = OverworldStreamingConfigSO.CreateRuntimeDefault();
        cleanup.Add(config);

        Assert.That(OverworldStreamingService.ShouldReleasePersistentCamera(config, "Combat2_TestHarness"), Is.True);
        Assert.That(OverworldStreamingService.ShouldReleasePersistentCamera(config, "loom_lab"), Is.True);
        Assert.That(OverworldStreamingService.ShouldReleasePersistentCamera(config, "Loomville"), Is.False);
        Assert.That(OverworldStreamingService.ShouldReleasePersistentCamera(config, "Link1"), Is.False);
        Assert.That(OverworldStreamingService.ShouldReleasePersistentCamera(config, string.Empty), Is.False);
        Assert.That(OverworldStreamingService.ShouldReleasePersistentCamera(null, "Combat2_TestHarness"), Is.True);
    }

    private Camera CreateCamera(string name, bool tagAsMain)
    {
        GameObject root = new GameObject(name);
        cleanup.Add(root);
        Camera camera = root.AddComponent<Camera>();
        if (tagAsMain)
            root.tag = "MainCamera";
        return camera;
    }
}
