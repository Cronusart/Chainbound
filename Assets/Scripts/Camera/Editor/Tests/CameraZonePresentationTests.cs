using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class CameraZonePresentationTests
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
    public void Activate_AppliesEnableAndDisableListsWhenYawDoesNotChange()
    {
        TopDownCameraController3D camera = CreateCamera(yawIndex: 0);
        GameObject show = CreateNamed("ShowFloor", active: false);
        GameObject hide = CreateNamed("HideFloor", active: true);
        CameraZoneBehavior zone = CreateZone(camera, CameraZoneBehavior.ExitBehavior.RestoreSnapshot, show, hide);

        zone.Activate();

        Assert.That(show.activeSelf, Is.True);
        Assert.That(hide.activeSelf, Is.False);
        Assert.That(camera.YawIndex90, Is.EqualTo(0));
    }

    [Test]
    public void RestoreSnapshot_RestoresCapturedActiveStates()
    {
        TopDownCameraController3D camera = CreateCamera(yawIndex: 0);
        GameObject show = CreateNamed("ShowFloor", active: false);
        GameObject hide = CreateNamed("HideFloor", active: true);
        CameraZoneBehavior zone = CreateZone(camera, CameraZoneBehavior.ExitBehavior.RestoreSnapshot, show, hide);

        zone.Activate();
        zone.Deactivate();

        Assert.That(show.activeSelf, Is.False);
        Assert.That(hide.activeSelf, Is.True);
    }

    [Test]
    public void DoNothing_KeepsPresentationAfterDeactivate()
    {
        TopDownCameraController3D camera = CreateCamera(yawIndex: 0);
        GameObject show = CreateNamed("ShowFloor", active: false);
        GameObject hide = CreateNamed("HideFloor", active: true);
        CameraZoneBehavior zone = CreateZone(camera, CameraZoneBehavior.ExitBehavior.DoNothing, show, hide);

        zone.Activate();
        zone.Deactivate();

        Assert.That(show.activeSelf, Is.True);
        Assert.That(hide.activeSelf, Is.False);
    }

    [Test]
    public void RestoreProfileOnly_KeepsPresentationAfterDeactivate()
    {
        TopDownCameraController3D camera = CreateCamera(yawIndex: 0);
        GameObject show = CreateNamed("ShowFloor", active: false);
        GameObject hide = CreateNamed("HideFloor", active: true);
        CameraZoneBehavior zone = CreateZone(
            camera,
            CameraZoneBehavior.ExitBehavior.RestoreProfileOnly,
            show,
            hide);

        zone.Activate();
        zone.Deactivate();

        Assert.That(show.activeSelf, Is.True);
        Assert.That(hide.activeSelf, Is.False);
    }

    [Test]
    public void DismissWithoutRestore_KeepsPresentation()
    {
        TopDownCameraController3D camera = CreateCamera(yawIndex: 0);
        GameObject show = CreateNamed("ShowFloor", active: false);
        GameObject hide = CreateNamed("HideFloor", active: true);
        CameraZoneBehavior zone = CreateZone(camera, CameraZoneBehavior.ExitBehavior.RestoreSnapshot, show, hide);

        zone.Activate();
        zone.DismissWithoutRestore();

        Assert.That(zone.IsActive, Is.False);
        Assert.That(show.activeSelf, Is.True);
        Assert.That(hide.activeSelf, Is.False);
    }

    [Test]
    public void ReassertPresentation_ReappliesListsWithoutRecapturingSnapshot()
    {
        TopDownCameraController3D camera = CreateCamera(yawIndex: 0);
        GameObject show = CreateNamed("ShowFloor", active: false);
        GameObject hide = CreateNamed("HideFloor", active: true);
        CameraZoneBehavior zone = CreateZone(camera, CameraZoneBehavior.ExitBehavior.RestoreSnapshot, show, hide);

        zone.Activate();
        show.SetActive(false);
        hide.SetActive(true);

        zone.ReassertPresentation();

        Assert.That(show.activeSelf, Is.True);
        Assert.That(hide.activeSelf, Is.False);

        zone.Deactivate();

        Assert.That(show.activeSelf, Is.False);
        Assert.That(hide.activeSelf, Is.True);
    }

    [Test]
    public void Activate_WhenYawAndProfileAlreadyMatch_SkipsFreezeAndWaits()
    {
        TopDownCameraController3D camera = CreateCamera(yawIndex: 0);
        CameraProfile profile = ScriptableObject.CreateInstance<CameraProfile>();
        cleanup.Add(profile);
        camera.ApplyProfile(profile, 0f);

        GameObject show = CreateNamed("ShowFloor", active: false);
        GameObject hide = CreateNamed("HideFloor", active: true);
        CameraZoneBehavior zone = CreateZone(camera, CameraZoneBehavior.ExitBehavior.DoNothing, show, hide);
        NarrativeTestUtility.SetPrivateField(zone, "zoneProfile", profile);
        NarrativeTestUtility.SetPrivateField(zone, "freezePlayerDuringTransition", true);
        NarrativeTestUtility.SetPrivateField(zone, "profileBlendTime", 0.5f);
        NarrativeTestUtility.SetPrivateField(zone, "freezeExtraBuffer", 0.1f);

        PlayerMovement3D movement = CreatePlayerMovement();
        int freezeCount = 0;
        zone.OnPlayerFrozen.AddListener(() => freezeCount++);

        zone.Activate();

        Assert.That(show.activeSelf, Is.True);
        Assert.That(hide.activeSelf, Is.False);
        Assert.That(zone.IsActive, Is.True);
        Assert.That(zone.IsTransitioning, Is.False);
        Assert.That(freezeCount, Is.EqualTo(0));
        Assert.That(movement.IsFrozen, Is.False);
        Assert.That(camera.YawIndex90, Is.EqualTo(0));
        Assert.That(camera.CurrentProfile, Is.SameAs(profile));
        Assert.That(camera.IsBlendingProfile, Is.False);
    }

    [Test]
    public void Activate_WhenYawDiffers_FreezesPlayerForTheTransition()
    {
        TopDownCameraController3D camera = CreateCamera(yawIndex: 0);
        GameObject show = CreateNamed("ShowFloor", active: false);
        GameObject hide = CreateNamed("HideFloor", active: true);
        CameraZoneBehavior zone = CreateZone(camera, CameraZoneBehavior.ExitBehavior.DoNothing, show, hide);
        NarrativeTestUtility.SetPrivateField(zone, "useAbsoluteYaw", true);
        NarrativeTestUtility.SetPrivateField(zone, "absoluteYawIndex", 2);
        NarrativeTestUtility.SetPrivateField(zone, "freezePlayerDuringTransition", true);
        NarrativeTestUtility.SetPrivateField(zone, "rotationDuration", 0.3f);
        NarrativeTestUtility.SetPrivateField(zone, "profileBlendTime", 0f);

        PlayerMovement3D movement = CreatePlayerMovement();
        int freezeCount = 0;
        zone.OnPlayerFrozen.AddListener(() => freezeCount++);

        zone.Activate();

        Assert.That(zone.IsActive, Is.True);
        Assert.That(zone.IsTransitioning, Is.True);
        Assert.That(freezeCount, Is.EqualTo(1));
        Assert.That(movement.IsFrozen, Is.True);

        movement.ForceUnfreeze();
    }

    [Test]
    public void Activate_WhenOnlyProfileDiffers_FreezesPlayerForTheBlend()
    {
        TopDownCameraController3D camera = CreateCamera(yawIndex: 0);
        CameraProfile current = ScriptableObject.CreateInstance<CameraProfile>();
        CameraProfile target = ScriptableObject.CreateInstance<CameraProfile>();
        cleanup.Add(current);
        cleanup.Add(target);
        camera.ApplyProfile(current, 0f);

        GameObject show = CreateNamed("ShowFloor", active: false);
        GameObject hide = CreateNamed("HideFloor", active: true);
        CameraZoneBehavior zone = CreateZone(camera, CameraZoneBehavior.ExitBehavior.DoNothing, show, hide);
        NarrativeTestUtility.SetPrivateField(zone, "zoneProfile", target);
        NarrativeTestUtility.SetPrivateField(zone, "freezePlayerDuringTransition", true);
        NarrativeTestUtility.SetPrivateField(zone, "profileBlendTime", 0.5f);
        NarrativeTestUtility.SetPrivateField(zone, "rotationDuration", 0f);

        PlayerMovement3D movement = CreatePlayerMovement();
        int freezeCount = 0;
        zone.OnPlayerFrozen.AddListener(() => freezeCount++);

        zone.Activate();

        Assert.That(zone.IsTransitioning, Is.True);
        Assert.That(freezeCount, Is.EqualTo(1));
        Assert.That(movement.IsFrozen, Is.True);

        movement.ForceUnfreeze();
    }

    private TopDownCameraController3D CreateCamera(int yawIndex)
    {
        GameObject cameraObject = new GameObject("ZonePresentationCamera");
        cleanup.Add(cameraObject);
        cameraObject.AddComponent<Camera>();
        TopDownCameraController3D camera = cameraObject.AddComponent<TopDownCameraController3D>();
        camera.SetYawIndex90(yawIndex, true);
        return camera;
    }

    private GameObject CreateNamed(string name, bool active)
    {
        GameObject go = new GameObject(name);
        cleanup.Add(go);
        go.SetActive(active);
        return go;
    }

    private CameraZoneBehavior CreateZone(
        TopDownCameraController3D camera,
        CameraZoneBehavior.ExitBehavior exitBehavior,
        GameObject enable,
        GameObject disable)
    {
        GameObject zoneObject = new GameObject("CameraZone");
        cleanup.Add(zoneObject);
        CameraZoneBehavior zone = zoneObject.AddComponent<CameraZoneBehavior>();
        // Runtime scene serialization supplies these events, but a component
        // created directly in an EditMode test starts with null UnityEvents.
        zone.OnPlayerFrozen = new UnityEngine.Events.UnityEvent();
        zone.OnPlayerUnfrozen = new UnityEngine.Events.UnityEvent();
        NarrativeTestUtility.SetPrivateField(zone, "cameraController", camera);
        NarrativeTestUtility.SetPrivateField(zone, "yawRotation90Steps", 0);
        NarrativeTestUtility.SetPrivateField(zone, "useAbsoluteYaw", false);
        NarrativeTestUtility.SetPrivateField(zone, "freezePlayerDuringTransition", false);
        NarrativeTestUtility.SetPrivateField(zone, "profileBlendTime", 0f);
        NarrativeTestUtility.SetPrivateField(zone, "exitBehavior", exitBehavior);
        NarrativeTestUtility.SetPrivateField(zone, "enableWhenActive", new List<GameObject> { enable });
        NarrativeTestUtility.SetPrivateField(zone, "disableWhenActive", new List<GameObject> { disable });
        return zone;
    }

    private PlayerMovement3D CreatePlayerMovement()
    {
        GameObject player = new GameObject("CameraZoneTestPlayer");
        cleanup.Add(player);
        player.tag = "Player";
        player.AddComponent<Rigidbody>();
        return player.AddComponent<PlayerMovement3D>();
    }
}
