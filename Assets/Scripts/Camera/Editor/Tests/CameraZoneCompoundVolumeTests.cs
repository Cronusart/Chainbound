using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class CameraZoneCompoundVolumeTests
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
    public void ContainsPosition_UnionsMultipleBoxCollidersOnTheSameObject()
    {
        CameraZoneTrigger trigger = CreateTrigger(
            useBuffer: false,
            new Vector3(0f, 0f, 0f),
            new Vector3(4f, 0f, 0f));

        Assert.That(trigger.ContainsPosition(Vector3.zero), Is.True);
        Assert.That(trigger.ContainsPosition(new Vector3(4f, 0f, 0f)), Is.True);
        Assert.That(trigger.ContainsPosition(new Vector3(2f, 0f, 0f)), Is.False);
    }

    [Test]
    public void ContainsPosition_IncludesChildVolumePieces()
    {
        GameObject zone = CreateInactiveZoneRoot();
        BoxCollider rootBox = zone.AddComponent<BoxCollider>();
        rootBox.center = Vector3.zero;
        rootBox.size = Vector3.one;

        GameObject piece = new GameObject("VolumePiece");
        piece.transform.SetParent(zone.transform, false);
        piece.transform.localPosition = new Vector3(8f, 0f, 0f);
        BoxCollider childBox = piece.AddComponent<BoxCollider>();
        childBox.size = Vector3.one;

        CameraZoneTrigger trigger = FinishTrigger(zone, useBuffer: false);

        Assert.That(trigger.ContainsPosition(Vector3.zero), Is.True);
        Assert.That(trigger.ContainsPosition(new Vector3(8f, 0f, 0f)), Is.True);
        Assert.That(trigger.ContainsPosition(new Vector3(4f, 0f, 0f)), Is.False);
    }

    [Test]
    public void ContainsPosition_IncludesChildVolumeWithNegativeBoxSize()
    {
        GameObject zone = CreateInactiveZoneRoot();
        BoxCollider rootBox = zone.AddComponent<BoxCollider>();
        rootBox.center = Vector3.zero;
        rootBox.size = Vector3.one;

        GameObject piece = new GameObject("CompoundCollider");
        piece.transform.SetParent(zone.transform, false);
        piece.transform.localPosition = new Vector3(8f, 0f, 0f);
        BoxCollider childBox = piece.AddComponent<BoxCollider>();
        childBox.size = new Vector3(1f, -1.9f, 1f);

        CameraZoneTrigger trigger = FinishTrigger(zone, useBuffer: false);

        Assert.That(trigger.ContainsPosition(Vector3.zero), Is.True);
        Assert.That(trigger.ContainsPosition(new Vector3(8f, 0f, 0f)), Is.True);
        Assert.That(CameraZoneOccupancy.FindTriggerContaining(new Vector3(8f, 0.4f, 0f)), Is.SameAs(trigger));
        Assert.That(trigger.ContainsPosition(new Vector3(4f, 0f, 0f)), Is.False);
    }

    [Test]
    public void BufferSetup_CopiesEachInnerCollider()
    {
        CameraZoneTrigger trigger = CreateTrigger(
            useBuffer: true,
            new Vector3(0f, 0f, 0f),
            new Vector3(4f, 0f, 0f));

        CameraZoneBuffer buffer = trigger.GetComponentInChildren<CameraZoneBuffer>();
        Assert.That(buffer, Is.Not.Null);

        Collider[] bufferColliders = buffer.GetComponentsInChildren<Collider>(true);
        Assert.That(bufferColliders.Length, Is.EqualTo(2));
    }

    [Test]
    public void CollectInnerColliders_DoesNotTreatBufferCopiesAsInnerVolume()
    {
        CameraZoneTrigger trigger = CreateTrigger(
            useBuffer: true,
            Vector3.zero);

        var inner = new List<Collider>();
        CameraZoneOccupancy.CollectInnerColliders(trigger, inner, markAsTrigger: false);
        Assert.That(inner.Count, Is.EqualTo(1));
        Assert.That(inner[0].GetComponentInParent<CameraZoneBuffer>(), Is.Null);
    }

    [Test]
    public void Activate_IsIdempotentWhenAlreadyActive()
    {
        GameObject cameraObject = new GameObject("CompoundZoneCamera");
        cleanup.Add(cameraObject);
        cameraObject.AddComponent<Camera>();
        TopDownCameraController3D camera = cameraObject.AddComponent<TopDownCameraController3D>();
        InitializeTestCamera(camera);

        GameObject zoneObject = new GameObject("CompoundZoneBehavior");
        cleanup.Add(zoneObject);
        CameraZoneBehavior zone = zoneObject.AddComponent<CameraZoneBehavior>();
        NarrativeTestUtility.SetPrivateField(zone, "cameraController", camera);
        NarrativeTestUtility.SetPrivateField(zone, "freezePlayerDuringTransition", false);
        NarrativeTestUtility.SetPrivateField(zone, "profileBlendTime", 0f);

        zone.Activate();
        Assert.That(zone.IsActive, Is.True);
        zone.Activate();
        Assert.That(zone.IsActive, Is.True);
    }

    [Test]
    public void FindTriggerContaining_IgnoresInactiveZoneUntilEnabled()
    {
        GameObject zone = CreateInactiveZoneRoot();
        BoxCollider rootBox = zone.AddComponent<BoxCollider>();
        rootBox.size = Vector3.one * 4f;
        CameraZoneTrigger trigger = FinishTrigger(zone, useBuffer: false);
        zone.SetActive(false);

        Assert.That(CameraZoneOccupancy.FindTriggerContaining(Vector3.zero), Is.Null);

        zone.SetActive(true);

        Assert.That(CameraZoneOccupancy.FindTriggerContaining(Vector3.zero), Is.SameAs(trigger));
    }

    [Test]
    public void WaitThenResyncOccupancy_FindsZoneEnabledAfterAFrame()
    {
        GameObject zone = CreateInactiveZoneRoot();
        BoxCollider rootBox = zone.AddComponent<BoxCollider>();
        rootBox.size = Vector3.one * 4f;
        CameraZoneTrigger trigger = FinishTrigger(zone, useBuffer: false);
        zone.SetActive(false);

        IEnumerator wait = CameraZoneOccupancy.WaitThenResyncOccupancy(() => Vector3.zero, timeoutSeconds: 2f, maxFrames: 8);
        Assert.That(wait.MoveNext(), Is.True, "Should wait at least one frame when the zone is not live yet.");

        zone.SetActive(true);

        Assert.That(wait.MoveNext(), Is.False, "Should stop once the live zone contains the spawn position.");
        Assert.That(CameraZoneOccupancy.FindTriggerContaining(Vector3.zero), Is.SameAs(trigger));
    }

    [Test]
    public void ForceActivateAtPosition_RetriesActivateWhenTriggerAlreadyOccupied()
    {
        GameObject cameraObject = new GameObject("RetryZoneCamera");
        cleanup.Add(cameraObject);
        cameraObject.AddComponent<Camera>();
        TopDownCameraController3D camera = cameraObject.AddComponent<TopDownCameraController3D>();
        InitializeTestCamera(camera);

        CameraZoneTrigger trigger = CreateTrigger(useBuffer: false, Vector3.zero);
        CameraZoneBehavior behavior = trigger.GetComponent<CameraZoneBehavior>();
        NarrativeTestUtility.SetPrivateField(behavior, "cameraController", camera);
        NarrativeTestUtility.SetPrivateField(behavior, "freezePlayerDuringTransition", false);
        NarrativeTestUtility.SetPrivateField(behavior, "profileBlendTime", 0f);
        NarrativeTestUtility.SetPrivateField(trigger, "zoneBehavior", behavior);

        Assert.That(trigger.ContainsPosition(Vector3.zero), Is.True);

        NarrativeTestUtility.SetPrivateField(trigger, "isActivated", true);
        Assert.That(behavior.IsActive, Is.False);

        trigger.ForceActivateAtPosition(Vector3.zero);

        Assert.That(trigger.IsActivated, Is.True);
        Assert.That(behavior.IsActive, Is.True);
    }

    [Test]
    public void NestedZone_InsideCompoundVolume_DismissesOuterOccupancyAndKeepsInnerYaw()
    {
        GameObject cameraObject = new GameObject("NestedCompoundCamera");
        cleanup.Add(cameraObject);
        cameraObject.AddComponent<Camera>();
        TopDownCameraController3D camera = cameraObject.AddComponent<TopDownCameraController3D>();
        InitializeTestCamera(camera);
        camera.SetYawIndex90(0, true);

        GameObject player = new GameObject("NestedCompoundPlayer");
        cleanup.Add(player);
        player.tag = "Player";
        player.transform.position = Vector3.zero;
        BoxCollider playerCollider = player.AddComponent<BoxCollider>();
        playerCollider.size = Vector3.one;

        CameraZoneTrigger outer = CreateNestedOuterWithCompound(camera, yawSteps: 1);
        CameraZoneTrigger inner = CreateYawTrigger("InnerNestedZone", Vector3.zero, camera, yawSteps: 2, size: 2f);

        inner.NotifyPlayerEnter(playerCollider);
        outer.NotifyPlayerEnter(playerCollider);

        Assert.That(inner.IsActivated, Is.True);
        Assert.That(outer.IsActivated, Is.False);
        Assert.That(inner.ZoneBehavior.IsActive, Is.True);
        Assert.That(outer.ZoneBehavior.IsActive, Is.False);
        Assert.That(camera.YawIndex90, Is.EqualTo(2));
    }

    [Test]
    public void NestedZone_ActivatesWhenChildColliderEntersBeforePlayerRoot()
    {
        GameObject cameraObject = new GameObject("NestedChildEnterCamera");
        cleanup.Add(cameraObject);
        cameraObject.AddComponent<Camera>();
        TopDownCameraController3D camera = cameraObject.AddComponent<TopDownCameraController3D>();
        InitializeTestCamera(camera);
        camera.SetYawIndex90(0, true);

        GameObject player = new GameObject("NestedChildEnterPlayer");
        cleanup.Add(player);
        player.tag = "Player";
        player.transform.position = new Vector3(6f, 0f, 0f);
        player.AddComponent<BoxCollider>().size = Vector3.one;

        GameObject child = new GameObject("FallCollider");
        cleanup.Add(child);
        child.tag = "Player";
        child.transform.SetParent(player.transform, true);
        child.transform.position = Vector3.zero;
        BoxCollider childCollider = child.AddComponent<BoxCollider>();
        childCollider.size = Vector3.one;

        CameraZoneTrigger outer = CreateNestedOuterWithCompound(camera, yawSteps: 1);
        CameraZoneTrigger inner = CreateYawTrigger("InnerNestedZone", Vector3.zero, camera, yawSteps: 2, size: 2f);

        inner.NotifyPlayerEnter(childCollider);

        Assert.That(inner.ZoneBehavior.IsActive, Is.True);
        Assert.That(camera.YawIndex90, Is.EqualTo(2));
    }

    [Test]
    public void NestedZone_WalkInLeadingEdgeActivatesBeforePlayerRootCrossesBoundary()
    {
        GameObject cameraObject = new GameObject("NestedLeadingEdgeCamera");
        cleanup.Add(cameraObject);
        cameraObject.AddComponent<Camera>();
        TopDownCameraController3D camera = cameraObject.AddComponent<TopDownCameraController3D>();
        InitializeTestCamera(camera);
        camera.SetYawIndex90(1, true);

        GameObject player = new GameObject("NestedLeadingEdgePlayer");
        cleanup.Add(player);
        player.tag = "Player";
        // Inner volume spans -1..1. The root is outside, but this 1-unit box
        // overlaps it from 0.9..1.9, matching Unity's first OnTriggerEnter beat.
        player.transform.position = new Vector3(1.4f, 0f, 0f);
        BoxCollider playerCollider = player.AddComponent<BoxCollider>();
        playerCollider.size = Vector3.one;
        Physics.SyncTransforms();

        CameraZoneTrigger outer = CreateNestedOuterWithCompound(camera, yawSteps: 1);
        CameraZoneTrigger inner = CreateYawTrigger("InnerNestedZone", Vector3.zero, camera, yawSteps: 2, size: 2f);

        outer.ForceActivateAtPosition(player.transform.position);
        Assert.That(outer.IsActivated, Is.True);
        Assert.That(inner.ContainsPosition(player.transform.position), Is.False);
        Assert.That(inner.IsPlayerPhysicallyInsideInner(), Is.True);

        inner.NotifyPlayerEnter(playerCollider);

        Assert.That(inner.IsActivated, Is.True);
        Assert.That(inner.ZoneBehavior.IsActive, Is.True);
        Assert.That(outer.IsActivated, Is.False);
        Assert.That(outer.ZoneBehavior.IsActive, Is.False);
        Assert.That(camera.YawIndex90, Is.EqualTo(2));
    }

    [Test]
    public void FindTriggerContaining_PrefersNestedInnerOverOuterCompoundPiece()
    {
        CameraZoneTrigger outer = CreateNestedOuterWithCompound(camera: null, yawSteps: 1);
        CameraZoneTrigger inner = CreateYawTrigger("InnerNestedZone", Vector3.zero, camera: null, yawSteps: 2, size: 2f);

        Assert.That(CameraZoneOccupancy.FindTriggerContaining(Vector3.zero), Is.SameAs(inner));
        Assert.That(outer.ContainsPosition(Vector3.zero), Is.True);
    }

    [Test]
    public void NestedZone_LeavingInner_ReassertsOuterYaw()
    {
        GameObject cameraObject = new GameObject("NestedLeaveCamera");
        cleanup.Add(cameraObject);
        cameraObject.AddComponent<Camera>();
        TopDownCameraController3D camera = cameraObject.AddComponent<TopDownCameraController3D>();
        InitializeTestCamera(camera);
        camera.SetYawIndex90(0, true);

        GameObject player = new GameObject("NestedLeavePlayer");
        cleanup.Add(player);
        player.tag = "Player";
        player.transform.position = Vector3.zero;
        BoxCollider playerCollider = player.AddComponent<BoxCollider>();
        playerCollider.size = Vector3.one;

        CameraZoneTrigger outer = CreateNestedOuterWithCompound(camera, yawSteps: 1);
        CameraZoneTrigger inner = CreateYawTrigger("InnerNestedZone", Vector3.zero, camera, yawSteps: 2, size: 2f);

        outer.ForceActivateAtPosition(Vector3.zero);
        inner.ForceActivateAtPosition(Vector3.zero);
        Assert.That(inner.IsActivated, Is.True);
        Assert.That(outer.IsActivated, Is.False);
        Assert.That(camera.YawIndex90, Is.EqualTo(2));

        player.transform.position = new Vector3(6f, 0f, 0f);
        inner.HardReset();

        Assert.That(inner.IsActivated, Is.False);
        Assert.That(outer.IsActivated, Is.True);
        Assert.That(outer.ZoneBehavior.IsActive, Is.True);
        Assert.That(camera.YawIndex90, Is.EqualTo(1));
    }

    [Test]
    public void NestedZone_LeavingOuter_RestoresProfileFromBeforeOuterActivate()
    {
        CameraProfile overworld = ScriptableObject.CreateInstance<CameraProfile>();
        CameraProfile innerProfile = ScriptableObject.CreateInstance<CameraProfile>();
        cleanup.Add(overworld);
        cleanup.Add(innerProfile);
        overworld.name = "OverworldExplore";
        innerProfile.name = "InnerAcademy";

        GameObject cameraObject = new GameObject("NestedProfileCamera");
        cleanup.Add(cameraObject);
        cameraObject.AddComponent<Camera>();
        TopDownCameraController3D camera = cameraObject.AddComponent<TopDownCameraController3D>();
        InitializeTestCamera(camera);
        NarrativeTestUtility.SetPrivateField(camera, "defaultProfile", overworld);
        camera.ApplyProfile(overworld, 0f);
        camera.SetYawIndex90(0, true);

        GameObject player = new GameObject("NestedProfilePlayer");
        cleanup.Add(player);
        player.tag = "Player";
        player.transform.position = new Vector3(6f, 0f, 0f);
        player.AddComponent<BoxCollider>().size = Vector3.one;

        CameraZoneTrigger outer = CreateNestedOuterWithCompound(camera, yawSteps: 1);
        CameraZoneTrigger inner = CreateYawTrigger("InnerNestedZone", Vector3.zero, camera, yawSteps: 2, size: 2f);
        NarrativeTestUtility.SetPrivateField(outer.ZoneBehavior, "zoneProfile", overworld);
        NarrativeTestUtility.SetPrivateField(inner.ZoneBehavior, "zoneProfile", innerProfile);

        outer.ForceActivateAtPosition(player.transform.position);
        Assert.That(outer.ZoneBehavior.IsActive, Is.True);
        Assert.That(camera.CurrentProfile, Is.SameAs(overworld));
        Assert.That(camera.YawIndex90, Is.EqualTo(1));

        player.transform.position = Vector3.zero;
        inner.ForceActivateAtPosition(Vector3.zero);
        Assert.That(camera.CurrentProfile, Is.SameAs(innerProfile));
        Assert.That(camera.YawIndex90, Is.EqualTo(2));

        player.transform.position = new Vector3(6f, 0f, 0f);
        inner.HardReset();
        Assert.That(outer.ZoneBehavior.IsActive, Is.True);
        Assert.That(camera.CurrentProfile, Is.SameAs(overworld));
        Assert.That(camera.YawIndex90, Is.EqualTo(1));

        player.transform.position = new Vector3(40f, 0f, 0f);
        outer.ZoneBehavior.Deactivate();
        Assert.That(outer.ZoneBehavior.IsActive, Is.False);
        Assert.That(camera.CurrentProfile, Is.SameAs(overworld));
        Assert.That(camera.YawIndex90, Is.EqualTo(0));
    }

    [Test]
    public void NestedZone_EnterThenLeaveInnerWhileStillInCompound_TransfersExclusivePresentation()
    {
        GameObject cameraObject = new GameObject("NestedTransferCamera");
        cleanup.Add(cameraObject);
        cameraObject.tag = "MainCamera";
        cameraObject.AddComponent<Camera>();
        TopDownCameraController3D camera = cameraObject.AddComponent<TopDownCameraController3D>();
        InitializeTestCamera(camera);
        camera.SetYawIndex90(0, true);

        GameObject player = new GameObject("NestedTransferPlayer");
        cleanup.Add(player);
        player.tag = "Player";
        player.transform.position = new Vector3(6f, 0f, 0f);
        BoxCollider playerCollider = player.AddComponent<BoxCollider>();
        playerCollider.size = Vector3.one;

        CameraZoneTrigger outer = CreateNestedOuterWithCompound(camera, yawSteps: 1);
        CameraZoneTrigger inner = CreateYawTrigger("InnerNestedZone", Vector3.zero, camera, yawSteps: 2, size: 2f);

        outer.NotifyPlayerEnter(playerCollider);
        Assert.That(outer.IsActivated, Is.True);
        Assert.That(camera.YawIndex90, Is.EqualTo(1));

        player.transform.position = Vector3.zero;
        Physics.SyncTransforms();
        inner.NotifyPlayerEnter(playerCollider);
        Assert.That(inner.IsActivated, Is.True);
        Assert.That(inner.ZoneBehavior.IsActive, Is.True);
        Assert.That(outer.IsActivated, Is.False);
        Assert.That(outer.ZoneBehavior.IsActive, Is.False);
        Assert.That(camera.YawIndex90, Is.EqualTo(2));

        player.transform.position = new Vector3(6f, 0f, 0f);
        Physics.SyncTransforms();
        inner.NotifyPlayerExit(playerCollider);

        Assert.That(inner.IsActivated, Is.False);
        Assert.That(inner.ZoneBehavior.IsActive, Is.False);
        Assert.That(outer.IsActivated, Is.True);
        Assert.That(outer.ZoneBehavior.IsActive, Is.True);
        Assert.That(camera.YawIndex90, Is.EqualTo(1));
    }

    [Test]
    public void ReassertPresentation_WhenYawAlreadyMatches_ReappliesObjectListsOnly()
    {
        GameObject cameraObject = new GameObject("ReassertListsCamera");
        cleanup.Add(cameraObject);
        cameraObject.AddComponent<Camera>();
        TopDownCameraController3D camera = cameraObject.AddComponent<TopDownCameraController3D>();
        InitializeTestCamera(camera);
        camera.SetYawIndex90(0, true);

        GameObject show = new GameObject("ShowOnReassert");
        cleanup.Add(show);
        show.SetActive(false);

        CameraZoneBehavior zone = CreateYawBehavior(camera, yawSteps: 0);
        NarrativeTestUtility.SetPrivateField(zone, "enableWhenActive", new List<GameObject> { show });
        zone.Activate();
        Assert.That(show.activeSelf, Is.True);

        show.SetActive(false);
        zone.ReassertPresentation();

        Assert.That(show.activeSelf, Is.True);
        Assert.That(camera.YawIndex90, Is.EqualTo(0));
        Assert.That(zone.IsTransitioning, Is.False);
    }

    [Test]
    public void ReassertPresentation_ReappliesProfileWhenYawAlreadyMatches()
    {
        CameraProfile explore = ScriptableObject.CreateInstance<CameraProfile>();
        CameraProfile academy = ScriptableObject.CreateInstance<CameraProfile>();
        cleanup.Add(explore);
        cleanup.Add(academy);
        explore.name = "Explore";
        academy.name = "Academy";

        GameObject cameraObject = new GameObject("ReassertProfileCamera");
        cleanup.Add(cameraObject);
        cameraObject.AddComponent<Camera>();
        TopDownCameraController3D camera = cameraObject.AddComponent<TopDownCameraController3D>();
        InitializeTestCamera(camera);
        NarrativeTestUtility.SetPrivateField(camera, "defaultProfile", explore);
        camera.ApplyProfile(explore, 0f);
        camera.SetYawIndex90(0, true);

        CameraZoneBehavior zone = CreateYawBehavior(camera, yawSteps: 0);
        NarrativeTestUtility.SetPrivateField(zone, "zoneProfile", explore);
        zone.Activate();
        Assert.That(camera.CurrentProfile, Is.SameAs(explore));

        camera.ApplyProfile(academy, 0f);
        Assert.That(camera.CurrentProfile, Is.SameAs(academy));
        Assert.That(camera.YawIndex90, Is.EqualTo(0));

        zone.ReassertPresentation();

        Assert.That(camera.CurrentProfile, Is.SameAs(explore));
        Assert.That(camera.YawIndex90, Is.EqualTo(0));
    }

    [Test]
    public void ForceActivateAtPosition_DoesNotTreatColliderOverlapAsInnerOccupancy()
    {
        GameObject cameraObject = new GameObject("PointOccupancyCamera");
        cleanup.Add(cameraObject);
        cameraObject.AddComponent<Camera>();
        TopDownCameraController3D camera = cameraObject.AddComponent<TopDownCameraController3D>();
        InitializeTestCamera(camera);
        camera.SetYawIndex90(0, true);

        GameObject player = new GameObject("PointOccupancyPlayer");
        cleanup.Add(player);
        player.tag = "Player";
        player.transform.position = new Vector3(1.4f, 0f, 0f);
        player.AddComponent<BoxCollider>().size = Vector3.one;
        Physics.SyncTransforms();

        CameraZoneTrigger outer = CreateNestedOuterWithCompound(camera, yawSteps: 1);
        CameraZoneTrigger inner = CreateYawTrigger("InnerNestedZone", Vector3.zero, camera, yawSteps: 2, size: 2f);

        Assert.That(inner.ContainsPosition(player.transform.position), Is.False);
        Assert.That(inner.IsPlayerPhysicallyInsideInner(), Is.True);

        inner.ForceActivateAtPosition(player.transform.position);

        Assert.That(inner.IsActivated, Is.False);
        Assert.That(inner.ZoneBehavior.IsActive, Is.False);
        Assert.That(camera.YawIndex90, Is.EqualTo(0));

        outer.ForceActivateAtPosition(player.transform.position);
        Assert.That(outer.IsActivated, Is.True);
        Assert.That(camera.YawIndex90, Is.EqualTo(1));
    }

    [Test]
    public void Resync_StandingInOuterCompound_ReclaimsOuterProfileAfterFalseInnerActivate()
    {
        CameraProfile explore = ScriptableObject.CreateInstance<CameraProfile>();
        CameraProfile academy = ScriptableObject.CreateInstance<CameraProfile>();
        cleanup.Add(explore);
        cleanup.Add(academy);
        explore.name = "OuterExplore";
        academy.name = "InnerAcademy";

        GameObject cameraObject = new GameObject("CompoundSpawnCamera");
        cleanup.Add(cameraObject);
        cameraObject.AddComponent<Camera>();
        TopDownCameraController3D camera = cameraObject.AddComponent<TopDownCameraController3D>();
        InitializeTestCamera(camera);
        NarrativeTestUtility.SetPrivateField(camera, "defaultProfile", explore);
        camera.ApplyProfile(explore, 0f);
        camera.SetYawIndex90(0, true);

        GameObject player = new GameObject("CompoundSpawnPlayer");
        cleanup.Add(player);
        player.tag = "Player";
        player.transform.position = Vector3.zero;
        player.AddComponent<BoxCollider>().size = Vector3.one;

        CameraZoneTrigger outer = CreateNestedOuterWithCompound(camera, yawSteps: 1);
        CameraZoneTrigger inner = CreateYawTrigger("InnerNestedZone", Vector3.zero, camera, yawSteps: 2, size: 2f);
        NarrativeTestUtility.SetPrivateField(outer.ZoneBehavior, "zoneProfile", explore);
        NarrativeTestUtility.SetPrivateField(inner.ZoneBehavior, "zoneProfile", academy);

        inner.ForceActivateAtPosition(Vector3.zero);
        Assert.That(camera.CurrentProfile, Is.SameAs(academy));
        Assert.That(inner.IsActivated, Is.True);
        Assert.That(outer.IsActivated, Is.False);

        player.transform.position = new Vector3(6f, 0f, 0f);
        Physics.SyncTransforms();
        CameraZoneOccupancy.ResyncAllLiveZones();

        Assert.That(inner.IsActivated, Is.False);
        Assert.That(inner.ZoneBehavior.IsActive, Is.False);
        Assert.That(outer.IsActivated, Is.True);
        Assert.That(outer.ZoneBehavior.IsActive, Is.True);
        Assert.That(camera.CurrentProfile, Is.SameAs(explore));
        Assert.That(camera.YawIndex90, Is.EqualTo(1));
    }

    [Test]
    public void VolumeRelay_ClearsWiredBufferOnInnerCompoundPiece()
    {
        CameraZoneTrigger trigger = CreateTrigger(useBuffer: true, Vector3.zero);
        CameraZoneBuffer buffer = trigger.GetComponentInChildren<CameraZoneBuffer>();
        Assert.That(buffer, Is.Not.Null);

        GameObject piece = new GameObject("CompoundCollider");
        cleanup.Add(piece);
        piece.transform.SetParent(trigger.transform, false);
        piece.AddComponent<BoxCollider>();
        CameraZoneVolumeRelay relay = piece.AddComponent<CameraZoneVolumeRelay>();
        NarrativeTestUtility.SetPrivateField(relay, "trigger", trigger);
        NarrativeTestUtility.SetPrivateField(relay, "buffer", buffer);

        typeof(CameraZoneVolumeRelay)
            .GetMethod("OnValidate", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .Invoke(relay, null);

        Assert.That(NarrativeTestUtility.GetPrivateField<CameraZoneBuffer>(relay, "buffer"), Is.Null);
        Assert.That(NarrativeTestUtility.GetPrivateField<CameraZoneTrigger>(relay, "trigger"), Is.SameAs(trigger));
    }

    private CameraZoneTrigger CreateNestedOuterWithCompound(TopDownCameraController3D camera, int yawSteps)
    {
        GameObject zone = CreateInactiveZoneRoot();
        zone.name = "OuterCompoundZone";
        BoxCollider rootBox = zone.AddComponent<BoxCollider>();
        rootBox.size = Vector3.one * 4f;

        GameObject compound = new GameObject("CompoundCollider");
        cleanup.Add(compound);
        compound.transform.SetParent(zone.transform, false);
        BoxCollider compoundBox = compound.AddComponent<BoxCollider>();
        compoundBox.size = Vector3.one * 20f;

        CameraZoneTrigger trigger = FinishTrigger(zone, useBuffer: false);
        CameraZoneBehavior behavior = trigger.GetComponent<CameraZoneBehavior>();
        NarrativeTestUtility.SetPrivateField(behavior, "cameraController", camera);
        NarrativeTestUtility.SetPrivateField(behavior, "freezePlayerDuringTransition", false);
        NarrativeTestUtility.SetPrivateField(behavior, "profileBlendTime", 0f);
        NarrativeTestUtility.SetPrivateField(behavior, "rotationDuration", 0f);
        NarrativeTestUtility.SetPrivateField(behavior, "yawRotation90Steps", yawSteps);
        NarrativeTestUtility.SetPrivateField(trigger, "zoneBehavior", behavior);
        return trigger;
    }

    private CameraZoneTrigger CreateYawTrigger(
        string name,
        Vector3 position,
        TopDownCameraController3D camera,
        int yawSteps,
        float size)
    {
        GameObject zone = new GameObject(name);
        cleanup.Add(zone);
        zone.SetActive(false);
        zone.transform.position = position;

        BoxCollider box = zone.AddComponent<BoxCollider>();
        box.size = Vector3.one * size;
        box.isTrigger = true;

        CameraZoneBehavior behavior = zone.AddComponent<CameraZoneBehavior>();
        NarrativeTestUtility.SetPrivateField(behavior, "cameraController", camera);
        NarrativeTestUtility.SetPrivateField(behavior, "freezePlayerDuringTransition", false);
        NarrativeTestUtility.SetPrivateField(behavior, "profileBlendTime", 0f);
        NarrativeTestUtility.SetPrivateField(behavior, "rotationDuration", 0f);
        NarrativeTestUtility.SetPrivateField(behavior, "yawRotation90Steps", yawSteps);

        CameraZoneTrigger trigger = zone.AddComponent<CameraZoneTrigger>();
        NarrativeTestUtility.SetPrivateField(trigger, "useBufferZone", false);
        NarrativeTestUtility.SetPrivateField(trigger, "enableSafetyNet", false);
        NarrativeTestUtility.SetPrivateField(trigger, "zoneBehavior", behavior);
        zone.SetActive(true);
        return trigger;
    }

    private CameraZoneBehavior CreateYawBehavior(TopDownCameraController3D camera, int yawSteps)
    {
        GameObject zoneObject = new GameObject("YawBehaviorZone");
        cleanup.Add(zoneObject);
        CameraZoneBehavior zone = zoneObject.AddComponent<CameraZoneBehavior>();
        NarrativeTestUtility.SetPrivateField(zone, "cameraController", camera);
        NarrativeTestUtility.SetPrivateField(zone, "freezePlayerDuringTransition", false);
        NarrativeTestUtility.SetPrivateField(zone, "profileBlendTime", 0f);
        NarrativeTestUtility.SetPrivateField(zone, "rotationDuration", 0f);
        NarrativeTestUtility.SetPrivateField(zone, "yawRotation90Steps", yawSteps);
        return zone;
    }

    private CameraZoneTrigger CreateTrigger(bool useBuffer, params Vector3[] boxCenters)
    {
        GameObject zone = CreateInactiveZoneRoot();
        for (int i = 0; i < boxCenters.Length; i++)
        {
            BoxCollider box = zone.AddComponent<BoxCollider>();
            box.center = boxCenters[i];
            box.size = Vector3.one;
        }

        return FinishTrigger(zone, useBuffer);
    }

    private GameObject CreateInactiveZoneRoot()
    {
        GameObject zone = new GameObject("CompoundCameraZone");
        cleanup.Add(zone);
        zone.SetActive(false);
        return zone;
    }

    private CameraZoneTrigger FinishTrigger(GameObject zone, bool useBuffer)
    {
        zone.AddComponent<CameraZoneBehavior>();
        CameraZoneTrigger trigger = zone.AddComponent<CameraZoneTrigger>();
        NarrativeTestUtility.SetPrivateField(trigger, "useBufferZone", useBuffer);
        NarrativeTestUtility.SetPrivateField(trigger, "enableSafetyNet", false);
        NarrativeTestUtility.SetPrivateField(trigger, "bufferZoneExtension", 1.5f);
        zone.SetActive(true);
        if (useBuffer)
            InvokePrivate(trigger, "SetupBufferZone");
        return trigger;
    }

    private void InitializeTestCamera(TopDownCameraController3D camera)
    {
        camera.gameObject.tag = "MainCamera";
        GameObject yawPivot = new GameObject(camera.gameObject.name + "_YawPivot");
        cleanup.Add(yawPivot);
        NarrativeTestUtility.SetPrivateField(camera, "yawPivot", yawPivot.transform);
    }

    private static void InvokePrivate(CameraZoneTrigger trigger, string methodName)
    {
        typeof(CameraZoneTrigger)
            .GetMethod(methodName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .Invoke(trigger, null);
    }
}
