using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class CinematicOcclusionRevealServiceTests
{
    private readonly List<Object> cleanup = new List<Object>();
    private CinematicOcclusionRevealService service;
    private Camera camera;
    private Transform target;
    private CinematicOcclusionProfileSO profile;

    [SetUp]
    public void SetUp()
    {
        GameObject serviceObject = new GameObject("RevealService");
        cleanup.Add(serviceObject);
        service = serviceObject.AddComponent<CinematicOcclusionRevealService>();

        GameObject cameraObject = new GameObject("ShotCamera");
        cleanup.Add(cameraObject);
        camera = cameraObject.AddComponent<Camera>();
        camera.transform.position = new Vector3(0f, 0f, -5f);

        GameObject targetObject = new GameObject("ShotTarget");
        cleanup.Add(targetObject);
        targetObject.transform.position = new Vector3(0f, 0f, 5f);
        target = targetObject.transform;

        profile = ScriptableObject.CreateInstance<CinematicOcclusionProfileSO>();
        cleanup.Add(profile);
    }

    [TearDown]
    public void TearDown()
    {
        for (int i = cleanup.Count - 1; i >= 0; i--)
        {
            if (cleanup[i] != null)
            {
                Object.DestroyImmediate(cleanup[i]);
            }
        }

        cleanup.Clear();
    }

    [Test]
    public void Reveal_RestoresRendererEnabledStateOnRelease()
    {
        Renderer renderer = CreateMarkedOccluder();

        CinematicOcclusionRevealService.RevealToken token = AcquireToken();

        Assert.That(token, Is.Not.Null);
        Assert.That(renderer.enabled, Is.False);

        token.Dispose();

        Assert.That(renderer.enabled, Is.True);
    }

    [Test]
    public void Reveal_PreDisabledRendererStaysDisabledAfterRelease()
    {
        Renderer renderer = CreateMarkedOccluder();
        renderer.enabled = false;

        CinematicOcclusionRevealService.RevealToken token = AcquireToken();

        Assert.That(token, Is.Not.Null);
        Assert.That(renderer.enabled, Is.False);

        token.Dispose();

        Assert.That(renderer.enabled, Is.False);
    }

    [Test]
    public void Reveal_OverlappingTokensRestoreAfterLastRelease()
    {
        Renderer renderer = CreateMarkedOccluder();

        CinematicOcclusionRevealService.RevealToken first = AcquireToken();
        CinematicOcclusionRevealService.RevealToken second = AcquireToken();

        Assert.That(first, Is.Not.Null);
        Assert.That(second, Is.Not.Null);
        Assert.That(renderer.enabled, Is.False);

        first.Dispose();
        Assert.That(renderer.enabled, Is.False);

        second.Dispose();
        Assert.That(renderer.enabled, Is.True);
    }

    [Test]
    public void Reveal_ReleasingTokenTwiceIsHarmless()
    {
        Renderer renderer = CreateMarkedOccluder();

        CinematicOcclusionRevealService.RevealToken token = AcquireToken();

        token.Dispose();
        token.Dispose();

        Assert.That(renderer.enabled, Is.True);
        Assert.That(service.ActiveTokenCount, Is.EqualTo(0));
        Assert.That(service.HiddenRendererCount, Is.EqualTo(0));
    }

    [Test]
    public void Reveal_DoesNotHidePlayerTaggedOccluder()
    {
        Renderer renderer = CreateMarkedOccluder();
        renderer.gameObject.tag = "Player";
        bool wasEnabled = renderer.enabled;

        CinematicOcclusionRevealService.RevealToken token = AcquireToken();

        Assert.That(token, Is.Null);
        Assert.That(renderer.enabled, Is.EqualTo(wasEnabled));
    }

    private Renderer CreateMarkedOccluder()
    {
        GameObject occluder = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cleanup.Add(occluder);
        occluder.name = "MarkedOccluder";
        occluder.transform.position = Vector3.zero;
        occluder.AddComponent<CinematicOccluder>();

        Physics.SyncTransforms();
        return occluder.GetComponent<Renderer>();
    }

    private CinematicOcclusionRevealService.RevealToken AcquireToken()
    {
        Physics.SyncTransforms();
        return service.AcquireReveal(camera, target, target.position, profile, "test");
    }
}
