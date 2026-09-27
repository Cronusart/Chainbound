using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class CameraZoneYawRestoreTests
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
    public void RelativeZone_ExitRestoresDefaultYawWhenAlreadyAtZoneYaw()
    {
        GameObject cameraObject = new GameObject("ZoneYawCamera");
        cleanup.Add(cameraObject);
        cameraObject.AddComponent<Camera>();
        TopDownCameraController3D camera = cameraObject.AddComponent<TopDownCameraController3D>();
        InitializeTestCamera(camera);
        camera.SetYawIndex90(1, true);

        CameraZoneBehavior zone = CreateRelativeZone(camera, yawSteps: 1);

        Assert.That(camera.DefaultYawIndex90, Is.EqualTo(0));
        Assert.That(camera.YawIndex90, Is.EqualTo(1));

        zone.Activate();
        zone.Deactivate();

        Assert.That(camera.YawIndex90, Is.EqualTo(camera.DefaultYawIndex90));
        Assert.That(camera.YawIndex90, Is.EqualTo(0));
    }

    [Test]
    public void RelativeZone_ExitRestoresDefaultYawAfterNormalEntry()
    {
        GameObject cameraObject = new GameObject("ZoneYawCamera");
        cleanup.Add(cameraObject);
        cameraObject.AddComponent<Camera>();
        TopDownCameraController3D camera = cameraObject.AddComponent<TopDownCameraController3D>();
        InitializeTestCamera(camera);
        camera.SetYawIndex90(0, true);

        CameraZoneBehavior zone = CreateRelativeZone(camera, yawSteps: 1);
        zone.Activate();
        camera.SetYawIndex90(1, true);
        zone.Deactivate();

        Assert.That(camera.YawIndex90, Is.EqualTo(0));
    }

    [Test]
    public void ReassertPresentation_KeepsOriginalRestoreSnapshot()
    {
        GameObject cameraObject = new GameObject("ZoneYawCamera");
        cleanup.Add(cameraObject);
        cameraObject.AddComponent<Camera>();
        TopDownCameraController3D camera = cameraObject.AddComponent<TopDownCameraController3D>();
        InitializeTestCamera(camera);
        camera.SetYawIndex90(0, true);

        CameraZoneBehavior zone = CreateRelativeZone(camera, yawSteps: 1);
        zone.Activate();
        Assert.That(camera.YawIndex90, Is.EqualTo(1));

        camera.SetYawIndex90(2, true);
        zone.ReassertPresentation();

        Assert.That(camera.YawIndex90, Is.EqualTo(1));
        zone.Deactivate();
        Assert.That(camera.YawIndex90, Is.EqualTo(0));
    }

    [Test]
    public void DismissWithoutRestore_ThenActivate_KeepsOriginalRestoreSnapshot()
    {
        GameObject cameraObject = new GameObject("ZoneYawCamera");
        cleanup.Add(cameraObject);
        cameraObject.AddComponent<Camera>();
        TopDownCameraController3D camera = cameraObject.AddComponent<TopDownCameraController3D>();
        InitializeTestCamera(camera);
        camera.SetYawIndex90(0, true);

        CameraZoneBehavior zone = CreateRelativeZone(camera, yawSteps: 1);
        zone.Activate();
        Assert.That(camera.YawIndex90, Is.EqualTo(1));

        zone.DismissWithoutRestore();
        camera.SetYawIndex90(2, true);

        zone.Activate();
        Assert.That(camera.YawIndex90, Is.EqualTo(1));
        zone.Deactivate();
        Assert.That(camera.YawIndex90, Is.EqualTo(0));
    }

    [Test]
    public void AbsoluteZone_ExitRestoresDefaultYawWhenEnteredFromAnotherIndex()
    {
        GameObject cameraObject = new GameObject("ZoneYawCamera");
        cleanup.Add(cameraObject);
        cameraObject.AddComponent<Camera>();
        TopDownCameraController3D camera = cameraObject.AddComponent<TopDownCameraController3D>();
        InitializeTestCamera(camera);
        camera.SetYawIndex90(2, true);

        CameraZoneBehavior zone = CreateAbsoluteZone(camera, absoluteYawIndex: 3);
        zone.Activate();
        Assert.That(camera.YawIndex90, Is.EqualTo(3));

        zone.Deactivate();

        Assert.That(camera.YawIndex90, Is.EqualTo(camera.DefaultYawIndex90));
        Assert.That(camera.YawIndex90, Is.EqualTo(0));
    }

    [Test]
    public void AbsoluteZone_ExitRestoresDefaultYawWhenSpawnedAlreadyAtZoneYaw()
    {
        GameObject cameraObject = new GameObject("ZoneYawCamera");
        cleanup.Add(cameraObject);
        cameraObject.AddComponent<Camera>();
        TopDownCameraController3D camera = cameraObject.AddComponent<TopDownCameraController3D>();
        InitializeTestCamera(camera);
        camera.SetYawIndex90(2, true);

        CameraZoneBehavior zone = CreateAbsoluteZone(camera, absoluteYawIndex: 2);
        zone.Activate();
        Assert.That(camera.YawIndex90, Is.EqualTo(2));

        zone.Deactivate();

        Assert.That(camera.YawIndex90, Is.EqualTo(0));
    }

    [Test]
    public void DismissWithoutRestore_DeactivateRestoresOriginalSnapshotWithoutReactivate()
    {
        GameObject cameraObject = new GameObject("ZoneYawCamera");
        cleanup.Add(cameraObject);
        cameraObject.AddComponent<Camera>();
        TopDownCameraController3D camera = cameraObject.AddComponent<TopDownCameraController3D>();
        InitializeTestCamera(camera);
        camera.SetYawIndex90(0, true);

        CameraZoneBehavior zone = CreateRelativeZone(camera, yawSteps: 1);
        zone.Activate();
        zone.DismissWithoutRestore();
        camera.SetYawIndex90(2, true);

        zone.Deactivate();
        Assert.That(camera.YawIndex90, Is.EqualTo(0));
        Assert.That(zone.IsActive, Is.False);
    }

    private CameraZoneBehavior CreateRelativeZone(TopDownCameraController3D camera, int yawSteps)
    {
        GameObject zoneObject = new GameObject("CameraZone");
        cleanup.Add(zoneObject);
        CameraZoneBehavior zone = zoneObject.AddComponent<CameraZoneBehavior>();
        NarrativeTestUtility.SetPrivateField(zone, "cameraController", camera);
        NarrativeTestUtility.SetPrivateField(zone, "yawRotation90Steps", yawSteps);
        NarrativeTestUtility.SetPrivateField(zone, "useAbsoluteYaw", false);
        NarrativeTestUtility.SetPrivateField(zone, "freezePlayerDuringTransition", false);
        NarrativeTestUtility.SetPrivateField(zone, "profileBlendTime", 0f);
        NarrativeTestUtility.SetPrivateField(
            zone,
            "exitBehavior",
            CameraZoneBehavior.ExitBehavior.RestoreSnapshot);
        return zone;
    }

    private CameraZoneBehavior CreateAbsoluteZone(TopDownCameraController3D camera, int absoluteYawIndex)
    {
        CameraZoneBehavior zone = CreateRelativeZone(camera, yawSteps: 0);
        NarrativeTestUtility.SetPrivateField(zone, "useAbsoluteYaw", true);
        NarrativeTestUtility.SetPrivateField(zone, "absoluteYawIndex", absoluteYawIndex);
        return zone;
    }

    private void InitializeTestCamera(TopDownCameraController3D camera)
    {
        camera.gameObject.tag = "MainCamera";
        GameObject yawPivot = new GameObject(camera.gameObject.name + "_YawPivot");
        cleanup.Add(yawPivot);
        NarrativeTestUtility.SetPrivateField(camera, "yawPivot", yawPivot.transform);
    }
}
