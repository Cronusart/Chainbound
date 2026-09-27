using NUnit.Framework;
using UnityEngine;

[TestFixture]
public sealed class SpriteBillboardTests
{
    [Test]
    public void YLock_PreservesLocalYaw_WhileFollowingCameraTilt()
    {
        GameObject cameraObject = new GameObject("Billboard Test Camera");
        GameObject parent = new GameObject("Billboard Test Parent");
        GameObject visual = new GameObject("Billboard Test Visual");

        try
        {
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.transform.rotation = Quaternion.Euler(35f, 90f, 0f);

            parent.transform.rotation = Quaternion.Euler(0f, 40f, 0f);
            visual.transform.SetParent(parent.transform, false);
            visual.transform.localRotation = Quaternion.Euler(0f, 15f, 0f);

            SpriteBillboard billboard = visual.AddComponent<SpriteBillboard>();
            billboard.SetMode(SpriteBillboard.BillboardMode.FaceCameraForward);
            billboard.SetYRotationLocked(true);
            billboard.SetTargetCamera(camera);

            Assert.That(Mathf.Abs(Mathf.DeltaAngle(visual.transform.localEulerAngles.y, 15f)), Is.LessThan(0.01f));
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(visual.transform.localEulerAngles.x, 0f)), Is.GreaterThan(1f));
        }
        finally
        {
            Object.DestroyImmediate(visual);
            Object.DestroyImmediate(parent);
            Object.DestroyImmediate(cameraObject);
        }
    }

    [Test]
    public void DisabledYLock_AllowsCameraYaw()
    {
        GameObject cameraObject = new GameObject("Billboard Test Camera");
        GameObject visual = new GameObject("Billboard Test Visual");

        try
        {
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.transform.rotation = Quaternion.Euler(30f, 90f, 0f);
            visual.transform.localRotation = Quaternion.Euler(0f, 15f, 0f);

            SpriteBillboard billboard = visual.AddComponent<SpriteBillboard>();
            billboard.SetMode(SpriteBillboard.BillboardMode.FaceCameraForward);
            billboard.SetYRotationLocked(false);
            billboard.SetTargetCamera(camera);

            Assert.That(Mathf.Abs(Mathf.DeltaAngle(visual.transform.eulerAngles.y, 90f)), Is.LessThan(0.01f));
        }
        finally
        {
            Object.DestroyImmediate(visual);
            Object.DestroyImmediate(cameraObject);
        }
    }
}
