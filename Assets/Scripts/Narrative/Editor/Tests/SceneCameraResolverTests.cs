using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;

[TestFixture]
public sealed class SceneCameraResolverTests
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
    public void Resolve_PrefersRegisteredPersistentCameraOverLeftoverSceneRig()
    {
        Camera leftover = CreateCamera("Leftover CameraRigRoot", tagAsMain: true);
        leftover.gameObject.AddComponent<TopDownCameraController3D>();
        Camera persistent = CreateCamera("Persistent CameraRigRoot", tagAsMain: false);
        TopDownCameraController3D persistentController = persistent.gameObject.AddComponent<TopDownCameraController3D>();

        SceneCameraResolver.RegisterPersistentCamera(persistent);

        Scene scene = leftover.gameObject.scene;
        Assert.That(SceneCameraResolver.Resolve(scene), Is.SameAs(persistent));
        Assert.That(SceneCameraResolver.ResolveGameplayCamera(), Is.SameAs(persistent));
        Assert.That(SceneCameraResolver.ResolveGameplayCameraController(), Is.SameAs(persistentController));
        Assert.That(SceneCameraResolver.IsUsableFor(leftover, leftover.gameObject), Is.False);
        Assert.That(SceneCameraResolver.IsUsableFor(persistent, leftover.gameObject), Is.True);
        Assert.That(SceneCameraResolver.ResolveFor(leftover.gameObject), Is.SameAs(persistent));
        Assert.That(SceneCameraResolver.ResolveOrKeep(leftover, leftover.gameObject), Is.SameAs(persistent));
        Assert.That(SceneCameraResolver.HasLivePersistentCamera, Is.True);
    }

    [Test]
    public void IsUsableFor_FallsBackToSceneCameraAfterPersistentIsCleared()
    {
        Camera leftover = CreateCamera("Interior CameraRigRoot", tagAsMain: true);
        leftover.gameObject.AddComponent<TopDownCameraController3D>();
        Camera persistent = CreateCamera("Persistent CameraRigRoot", tagAsMain: false);
        persistent.gameObject.AddComponent<TopDownCameraController3D>();

        SceneCameraResolver.RegisterPersistentCamera(persistent);
        Assert.That(SceneCameraResolver.IsUsableFor(leftover, leftover.gameObject), Is.False);

        SceneCameraResolver.ClearPersistentCamera(persistent);
        persistent.gameObject.SetActive(false);

        Assert.That(SceneCameraResolver.IsUsableFor(leftover, leftover.gameObject), Is.True);
        Assert.That(SceneCameraResolver.Resolve(leftover.gameObject.scene), Is.SameAs(leftover));
        Assert.That(SceneCameraResolver.ResolveGameplayCameraController(), Is.Not.Null);
    }

    [Test]
    public void Resolve_UsesSceneCameraWhenRegisteredPersistentIsDisabled()
    {
        Camera leftover = CreateCamera("Combat CameraRigRoot", tagAsMain: true);
        TopDownCameraController3D leftoverController = leftover.gameObject.AddComponent<TopDownCameraController3D>();
        Camera persistent = CreateCamera("Persistent CameraRigRoot", tagAsMain: false);
        persistent.gameObject.AddComponent<TopDownCameraController3D>();

        SceneCameraResolver.RegisterPersistentCamera(persistent);
        persistent.gameObject.SetActive(false);

        Assert.That(SceneCameraResolver.HasLivePersistentCamera, Is.False);
        Assert.That(SceneCameraResolver.Resolve(leftover.gameObject.scene), Is.SameAs(leftover));
        Assert.That(SceneCameraResolver.FindLocalController(leftover.gameObject.scene), Is.SameAs(leftoverController));
        Assert.That(SceneCameraResolver.ResolveGameplayCameraController(), Is.SameAs(leftoverController));
    }

    [Test]
    public void FindLocalController_IgnoresExclusivePersistentPresenter()
    {
        Camera leftover = CreateCamera("Interior CameraRigRoot", tagAsMain: true);
        TopDownCameraController3D leftoverController = leftover.gameObject.AddComponent<TopDownCameraController3D>();
        Camera persistent = CreateCamera("Persistent CameraRigRoot", tagAsMain: false);
        persistent.gameObject.AddComponent<TopDownCameraController3D>();

        SceneCameraResolver.RegisterPersistentCamera(persistent);

        Assert.That(SceneCameraResolver.ResolveGameplayCamera(), Is.SameAs(persistent));
        Assert.That(SceneCameraResolver.FindLocalController(leftover.gameObject.scene), Is.SameAs(leftoverController));
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
