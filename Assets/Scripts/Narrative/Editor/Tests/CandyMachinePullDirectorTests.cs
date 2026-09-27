using DG.Tweening;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

[TestFixture]
public sealed class CandyMachinePullDirectorTests
{
    [Test]
    public void LeverSpringDefaultsAreDramatic()
    {
        GameObject root = new GameObject("CandyMachineLeverDefaults");
        try
        {
            CandyMachinePullDirector director = root.AddComponent<CandyMachinePullDirector>();
            SerializedObject serialized = new SerializedObject(director);
            Assert.That(serialized.FindProperty("leverOvershootDegrees").floatValue, Is.EqualTo(32f).Within(0.01f));
            Assert.That(serialized.FindProperty("leverSpringDuration").floatValue, Is.EqualTo(0.72f).Within(0.01f));
            Assert.That(serialized.FindProperty("leverElasticAmplitude").floatValue, Is.EqualTo(1.85f).Within(0.01f));
            Assert.That(serialized.FindProperty("leverElasticPeriod").floatValue, Is.EqualTo(0.36f).Within(0.01f));
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void PullCompletesOnceAndRestoreReturnsAuthoredPose()
    {
        GameObject root = BuildDirector(out CandyMachinePullDirector director, out Transform lever, out Transform spheres,
            out Transform glow, out Transform door);
        Quaternion leverPose = lever.localRotation;
        Vector3 spheresPose = spheres.localPosition;
        Quaternion doorPose = door.localRotation;
        int completions = 0;
        try
        {
            director.BeginPull(() => completions++);
            Assert.That(director.IsPlaying, Is.True);

            DOTween.Complete(director, true);
            Assert.That(completions, Is.EqualTo(1));
            Assert.That(director.IsPlaying, Is.False);

            director.RestoreAfterReveal();
            Assert.That(Quaternion.Angle(lever.localRotation, leverPose), Is.LessThan(0.01f));
            Assert.That(Vector3.Distance(spheres.localPosition, spheresPose), Is.LessThan(0.0001f));
            Assert.That(Quaternion.Angle(door.localRotation, doorPose), Is.LessThan(0.01f));
            Assert.That(glow.gameObject.activeSelf, Is.False);
        }
        finally
        {
            Object.DestroyImmediate(root);
            DOTween.Kill(director);
        }
    }

    [Test]
    public void DisableCancelsWithoutCompletingAndRestoresPose()
    {
        GameObject root = BuildDirector(out CandyMachinePullDirector director, out _, out Transform spheres,
            out Transform glow, out Transform door);
        Vector3 spheresPose = spheres.localPosition;
        Quaternion doorPose = door.localRotation;
        int completions = 0;
        try
        {
            director.BeginPull(() => completions++);
            director.enabled = false;

            Assert.That(completions, Is.Zero);
            Assert.That(director.IsPlaying, Is.False);
            Assert.That(Vector3.Distance(spheres.localPosition, spheresPose), Is.LessThan(0.0001f));
            Assert.That(Quaternion.Angle(door.localRotation, doorPose), Is.LessThan(0.01f));
            Assert.That(glow.gameObject.activeSelf, Is.False);
        }
        finally
        {
            Object.DestroyImmediate(root);
            DOTween.Kill(director);
        }
    }

    private static GameObject BuildDirector(
        out CandyMachinePullDirector director,
        out Transform lever,
        out Transform spheres,
        out Transform glow,
        out Transform door)
    {
        GameObject root = new GameObject("CandyMachineDirectorTest");
        root.SetActive(false);
        lever = NewChild(root.transform, "Lever_Root");
        spheres = NewChild(root.transform, "Spheres");
        glow = NewChild(spheres, "emissivesphere");
        door = NewChild(root.transform, "CandyMachineDoor");
        lever.localRotation = Quaternion.Euler(5f, 7f, 11f);
        spheres.localPosition = new Vector3(0.1f, 0.2f, 0.3f);
        door.localRotation = Quaternion.Euler(9f, 2f, 1f);
        MeshRenderer renderer = glow.gameObject.AddComponent<MeshRenderer>();

        director = root.AddComponent<CandyMachinePullDirector>();
        SerializedObject serialized = new SerializedObject(director);
        serialized.FindProperty("leverRoot").objectReferenceValue = lever;
        serialized.FindProperty("spheresRoot").objectReferenceValue = spheres;
        serialized.FindProperty("emissiveRenderer").objectReferenceValue = renderer;
        serialized.FindProperty("candyMachineDoor").objectReferenceValue = door;
        serialized.FindProperty("cameraTarget").objectReferenceValue = root.transform;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        root.SetActive(true);
        return root;
    }

    private static Transform NewChild(Transform parent, string name)
    {
        Transform child = new GameObject(name).transform;
        child.SetParent(parent, false);
        return child;
    }
}
