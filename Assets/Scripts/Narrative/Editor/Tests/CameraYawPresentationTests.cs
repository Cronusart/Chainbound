using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

[TestFixture]
public sealed class CameraYawPresentationTests
{
    private const string PlayerPrefabPath = "Assets/Prefabs/Player/PlayerRoot.prefab";

    [Test]
    public void PlayerRootResolver_MapsTaggedPresentationChildrenToMovementRoot()
    {
        GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        Assert.That(playerPrefab, Is.Not.Null, $"Missing player prefab at '{PlayerPrefabPath}'.");

        Transform[] hierarchy = playerPrefab.GetComponentsInChildren<Transform>(true);
        Transform silhouette = hierarchy.FirstOrDefault(item => item.name == "Silhouette");
        Transform fallCollider = hierarchy.FirstOrDefault(item => item.name == "FallCollider");

        Assert.That(silhouette, Is.Not.Null);
        Assert.That(fallCollider, Is.Not.Null);
        Assert.That(silhouette.CompareTag("Player"), Is.True);
        Assert.That(fallCollider.CompareTag("Player"), Is.True);

        Assert.That(
            PlayerRootResolver.ResolveCandidate(silhouette.gameObject),
            Is.SameAs(playerPrefab));
        Assert.That(
            PlayerRootResolver.ResolveCandidate(fallCollider.gameObject),
            Is.SameAs(playerPrefab));
    }

    [Test]
    public void PlayerFacingUtility_ResolvesControllerFromTaggedSilhouetteChild()
    {
        GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        Assert.That(playerPrefab, Is.Not.Null, $"Missing player prefab at '{PlayerPrefabPath}'.");

        Transform silhouette = playerPrefab
            .GetComponentsInChildren<Transform>(true)
            .FirstOrDefault(item => item.name == "Silhouette");
        Assert.That(silhouette, Is.Not.Null);
        Assert.That(silhouette.CompareTag("Player"), Is.True);

        bool resolved = PlayerFacingUtility.TryGetPlayerFacingComponents(
            silhouette.gameObject,
            "Player",
            out GameObject playerObject,
            out Transform playerTransform,
            out PlayerAnimationController3D_Mesh animationController,
            out PlayerMovement3D playerMovement,
            out string error);

        Assert.That(resolved, Is.True, error);
        Assert.That(playerObject, Is.SameAs(playerPrefab));
        Assert.That(playerTransform, Is.SameAs(playerPrefab.transform));
        Assert.That(animationController, Is.Not.Null);
        Assert.That(playerMovement, Is.Not.Null);
    }

    [Test]
    public void PlayerShadow_TracksSettledCameraYawWithoutMovementCallback()
    {
        GameObject cameraObject = new GameObject("Shadow Yaw Test Camera");
        GameObject shadowParent = new GameObject("Shadow Yaw Test Parent");
        GameObject shadowObject = new GameObject("Shadow Yaw Test Shadow");
        GameObject cameraRigRoot = cameraObject;

        try
        {
            cameraObject.AddComponent<Camera>();
            TopDownCameraController3D cameraController = cameraObject.AddComponent<TopDownCameraController3D>();
            cameraRigRoot = cameraObject.transform.root.gameObject;

            shadowObject.transform.SetParent(shadowParent.transform, false);
            PlayerShadowCaster shadow = shadowObject.AddComponent<PlayerShadowCaster>();
            NarrativeTestUtility.SetPrivateField(shadow, "useGroundRaycast", false);
            NarrativeTestUtility.SetPrivateField(shadow, "usePerYawXZOffsets", true);
            NarrativeTestUtility.SetPrivateField(shadow, "yPositionOffset", 0.1f);
            NarrativeTestUtility.SetPrivateField(
                shadow,
                "xzPositionOffsetsPerYaw",
                new[]
                {
                    new Vector2(0f, -2f),
                    new Vector2(-2f, 0f),
                    new Vector2(0f, 2f),
                    new Vector2(2f, 0f)
                });

            cameraController.SetYawIndex90(0, true);
            shadow.SetCameraController(cameraController);
            Assert.That(shadow.CurrentCameraYawIndex, Is.EqualTo(0));

            // This deliberately bypasses PlayerMovement3D, matching dialogue camera
            // shots and proving the shadow owns its camera-yaw presentation state.
            cameraController.SetYawIndex90(1, true);
            shadow.SynchronizeWithCameraYaw();

            Assert.That(shadow.CurrentCameraYawIndex, Is.EqualTo(1));
            Assert.That(shadowObject.transform.localPosition.x, Is.EqualTo(-2f).Within(0.001f));
            Assert.That(shadowObject.transform.localPosition.z, Is.EqualTo(0f).Within(0.001f));
            Assert.That(
                Mathf.Abs(Mathf.DeltaAngle(shadowObject.transform.localEulerAngles.y, 90f)),
                Is.LessThan(0.01f));
        }
        finally
        {
            Object.DestroyImmediate(shadowParent);
            if (cameraRigRoot != null)
                Object.DestroyImmediate(cameraRigRoot);
        }
    }

    [Test]
    public void LockedBillboard_TracksCameraZoneYawInCameraPlane()
    {
        GameObject cameraObject = new GameObject("Billboard Yaw Test Camera");
        GameObject billboardParent = new GameObject("Billboard Yaw Test Parent");
        GameObject billboardObject = new GameObject("Billboard Yaw Test Subject");

        try
        {
            Camera camera = cameraObject.AddComponent<Camera>();
            billboardParent.transform.rotation = Quaternion.Euler(0f, 35f, 0f);
            billboardObject.transform.SetParent(billboardParent.transform, false);
            SpriteBillboard billboard = billboardObject.AddComponent<SpriteBillboard>();
            billboard.SetMode(SpriteBillboard.BillboardMode.FaceCameraForward);
            billboard.SetYRotationLocked(true);

            cameraObject.transform.rotation = Quaternion.Euler(30f, 90f, 0f);
            billboard.SetTargetCamera(camera);
            AssertPlanarDirectionsMatch(
                billboardObject.transform.forward,
                cameraObject.transform.forward);

            cameraObject.transform.rotation = Quaternion.Euler(30f, 180f, 0f);
            billboard.SetTargetCamera(camera);
            AssertPlanarDirectionsMatch(
                billboardObject.transform.forward,
                cameraObject.transform.forward);
        }
        finally
        {
            Object.DestroyImmediate(billboardParent);
            Object.DestroyImmediate(cameraObject);
        }
    }

    [Test]
    public void OcclusionSilhouette_AppliesYawOffsetImmediately()
    {
        GameObject root = new GameObject("Silhouette Yaw Test Root");
        GameObject rendererObject = new GameObject("Silhouette Yaw Test Renderer");

        try
        {
            root.SetActive(false);
            rendererObject.transform.SetParent(root.transform, false);
            MeshRenderer renderer = rendererObject.AddComponent<MeshRenderer>();
            OcclusionSilhouette silhouette = root.AddComponent<OcclusionSilhouette>();
            NarrativeTestUtility.SetPrivateField(silhouette, "silhouetteRenderer", renderer);
            NarrativeTestUtility.SetPrivateField(silhouette, "usePerYawXZOffsets", true);
            NarrativeTestUtility.SetPrivateField(silhouette, "yPositionOffset", 0.25f);
            NarrativeTestUtility.SetPrivateField(
                silhouette,
                "xzPositionOffsetsPerYaw",
                new[]
                {
                    Vector2.zero,
                    new Vector2(-1.5f, 0.75f),
                    Vector2.zero,
                    Vector2.zero
                });

            silhouette.UpdateSilhouetteForCameraYaw(1);

            Assert.That(silhouette.CurrentCameraYawIndex, Is.EqualTo(1));
            Assert.That(rendererObject.transform.localPosition.x, Is.EqualTo(-1.5f).Within(0.001f));
            Assert.That(rendererObject.transform.localPosition.y, Is.EqualTo(0.25f).Within(0.001f));
            Assert.That(rendererObject.transform.localPosition.z, Is.EqualTo(0.75f).Within(0.001f));
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void OcclusionSilhouette_FollowsAssignedVisualWorldPose()
    {
        GameObject root = new GameObject("Silhouette Follow Visual Root");
        GameObject visualObject = new GameObject("Visual");
        GameObject rendererObject = new GameObject("Silhouette");

        try
        {
            root.SetActive(false);
            visualObject.transform.SetParent(root.transform, false);
            rendererObject.transform.SetParent(root.transform, false);
            MeshRenderer visualRenderer = visualObject.AddComponent<MeshRenderer>();
            MeshRenderer silhouetteRenderer = rendererObject.AddComponent<MeshRenderer>();
            OcclusionSilhouette silhouette = root.AddComponent<OcclusionSilhouette>();
            NarrativeTestUtility.SetPrivateField(silhouette, "silhouetteRenderer", silhouetteRenderer);
            NarrativeTestUtility.SetPrivateField(silhouette, "mainMeshRenderer", visualRenderer);
            NarrativeTestUtility.SetPrivateField(silhouette, "yPositionOffset", 4f);

            visualObject.transform.position = new Vector3(2f, 3.4f, -1f);
            silhouette.UpdateSilhouetteForCameraYaw(0);

            Assert.That(rendererObject.transform.position.x, Is.EqualTo(2f).Within(0.001f));
            Assert.That(rendererObject.transform.position.y, Is.EqualTo(3.4f).Within(0.001f));
            Assert.That(rendererObject.transform.position.z, Is.EqualTo(-1f).Within(0.001f));
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static void AssertPlanarDirectionsMatch(Vector3 actual, Vector3 expected)
    {
        Vector3 actualPlanar = Vector3.ProjectOnPlane(actual, Vector3.up).normalized;
        Vector3 expectedPlanar = Vector3.ProjectOnPlane(expected, Vector3.up).normalized;
        Assert.That(Vector3.Dot(actualPlanar, expectedPlanar), Is.GreaterThan(0.999f));
    }
}
