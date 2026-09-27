using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class CameraZoneStreamingResyncTests
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
    public void DismissWithoutRestore_KeepsCurrentYaw()
    {
        TopDownCameraController3D camera = CreateCamera();
        camera.SetYawIndex90(1, true);
        CameraZoneBehavior zone = CreateBehavior(camera);
        zone.Activate();
        Assert.That(zone.IsActive, Is.True);

        zone.DismissWithoutRestore();

        Assert.That(zone.IsActive, Is.False);
        Assert.That(camera.YawIndex90, Is.EqualTo(1));
    }

    [Test]
    public void Resync_DismissesFalselyActiveZoneWithoutRestoringWhilePlayerStaysInAnother()
    {
        TopDownCameraController3D camera = CreateCamera();
        GameObject player = CreatePlayer(Vector3.zero);
        CameraZoneTrigger seam = CreateTrigger("SeamZone", Vector3.zero, camera, yawSteps: 1);
        CameraZoneTrigger leftover = CreateTrigger("ReloadedHubZone", new Vector3(20f, 0f, 0f), camera, yawSteps: 1);

        seam.ForceActivateIfPlayerInside();
        Assert.That(seam.IsActivated, Is.True);
        Assert.That(camera.YawIndex90, Is.EqualTo(1));

        leftover.ZoneBehavior.Activate();
        NarrativeTestUtility.SetPrivateField(leftover, "isActivated", true);
        Assert.That(leftover.IsActivated, Is.True);

        CameraZoneOccupancy.ResyncAllLiveZones();

        Assert.That(player, Is.Not.Null);
        Assert.That(seam.IsActivated, Is.True);
        Assert.That(leftover.IsActivated, Is.False);
        Assert.That(leftover.ZoneBehavior.IsActive, Is.False);
        Assert.That(camera.YawIndex90, Is.EqualTo(1));
    }

    [Test]
    public void Resync_RestoresYawWhenPlayerIsOutsideEveryLiveZone()
    {
        TopDownCameraController3D camera = CreateCamera();
        CreatePlayer(new Vector3(40f, 0f, 0f));
        CameraZoneTrigger seam = CreateTrigger("SeamZone", Vector3.zero, camera, yawSteps: 1);

        seam.ZoneBehavior.Activate();
        NarrativeTestUtility.SetPrivateField(seam, "isActivated", true);
        camera.SetYawIndex90(1, true);

        CameraZoneOccupancy.ResyncAllLiveZones();

        Assert.That(seam.IsActivated, Is.False);
        Assert.That(camera.YawIndex90, Is.EqualTo(camera.DefaultYawIndex90));
    }

    [Test]
    public void HardReset_InnerZone_ReassertsOverlappingOuterYaw()
    {
        TopDownCameraController3D camera = CreateCamera();
        GameObject player = CreatePlayer(Vector3.zero);
        CameraZoneTrigger outer = CreateTrigger("OuterZone", Vector3.zero, camera, yawSteps: 1);
        outer.GetComponent<BoxCollider>().size = Vector3.one * 10f;
        CameraZoneTrigger inner = CreateTrigger("InnerZone", Vector3.zero, camera, yawSteps: 2);
        inner.GetComponent<BoxCollider>().size = Vector3.one * 2f;

        outer.ForceActivateIfPlayerInside();
        inner.ForceActivateIfPlayerInside();
        Assert.That(camera.YawIndex90, Is.EqualTo(2));
        Assert.That(outer.IsActivated, Is.False);
        Assert.That(inner.IsActivated, Is.True);

        player.transform.position = new Vector3(4f, 0f, 0f);
        inner.HardReset();

        Assert.That(player, Is.Not.Null);
        Assert.That(inner.IsActivated, Is.False);
        Assert.That(inner.ZoneBehavior.IsActive, Is.False);
        Assert.That(outer.IsActivated, Is.True);
        Assert.That(outer.ZoneBehavior.IsActive, Is.True);
        Assert.That(camera.YawIndex90, Is.EqualTo(1));
    }

    private TopDownCameraController3D CreateCamera()
    {
        GameObject cameraObject = new GameObject("ResyncTestCamera");
        cleanup.Add(cameraObject);
        cameraObject.tag = "MainCamera";
        cameraObject.AddComponent<Camera>();
        TopDownCameraController3D camera = cameraObject.AddComponent<TopDownCameraController3D>();
        GameObject yawPivot = new GameObject("ResyncTestYawPivot");
        cleanup.Add(yawPivot);
        NarrativeTestUtility.SetPrivateField(camera, "yawPivot", yawPivot.transform);
        return camera;
    }

    private CameraZoneBehavior CreateBehavior(TopDownCameraController3D camera)
    {
        GameObject zoneObject = new GameObject("ResyncTestBehavior");
        cleanup.Add(zoneObject);
        CameraZoneBehavior zone = zoneObject.AddComponent<CameraZoneBehavior>();
        NarrativeTestUtility.SetPrivateField(zone, "cameraController", camera);
        NarrativeTestUtility.SetPrivateField(zone, "freezePlayerDuringTransition", false);
        NarrativeTestUtility.SetPrivateField(zone, "profileBlendTime", 0f);
        NarrativeTestUtility.SetPrivateField(zone, "yawRotation90Steps", 1);
        return zone;
    }

    private CameraZoneTrigger CreateTrigger(
        string name,
        Vector3 position,
        TopDownCameraController3D camera,
        int yawSteps)
    {
        GameObject zone = new GameObject(name);
        cleanup.Add(zone);
        zone.SetActive(false);
        zone.transform.position = position;

        BoxCollider box = zone.AddComponent<BoxCollider>();
        box.size = Vector3.one * 2f;
        box.isTrigger = true;

        CameraZoneBehavior behavior = zone.AddComponent<CameraZoneBehavior>();
        NarrativeTestUtility.SetPrivateField(behavior, "cameraController", camera);
        NarrativeTestUtility.SetPrivateField(behavior, "freezePlayerDuringTransition", false);
        NarrativeTestUtility.SetPrivateField(behavior, "profileBlendTime", 0f);
        NarrativeTestUtility.SetPrivateField(behavior, "yawRotation90Steps", yawSteps);

        CameraZoneTrigger trigger = zone.AddComponent<CameraZoneTrigger>();
        NarrativeTestUtility.SetPrivateField(trigger, "useBufferZone", false);
        NarrativeTestUtility.SetPrivateField(trigger, "enableSafetyNet", false);
        NarrativeTestUtility.SetPrivateField(trigger, "zoneBehavior", behavior);
        zone.SetActive(true);
        return trigger;
    }

    private GameObject CreatePlayer(Vector3 position)
    {
        GameObject player = new GameObject("ResyncTestPlayer");
        cleanup.Add(player);
        player.tag = "Player";
        player.transform.position = position;
        BoxCollider collider = player.AddComponent<BoxCollider>();
        collider.size = Vector3.one;
        return player;
    }
}
