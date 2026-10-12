using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class EncounterLeadInCueTests
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
    public void Cue_IsNotAnInteractable_AndHidesImmediately()
    {
        GameObject player = new GameObject("Player");
        cleanup.Add(player);
        GameObject cueObject = new GameObject("EncounterCue");
        cleanup.Add(cueObject);
        cueObject.transform.SetParent(player.transform, false);
        cueObject.AddComponent<MeshFilter>();
        cueObject.AddComponent<MeshRenderer>();
        cueObject.AddComponent<MeshQuadImage>();
        EncounterLeadInCue cue = cueObject.AddComponent<EncounterLeadInCue>();

        Assert.That(cue.GetComponent<InteractableIndicator>(), Is.Null);
        Assert.That(cue.GetComponent<InteractableObject>(), Is.Null);
        Assert.That(EncounterLeadInCue.FindOnPlayer(player), Is.SameAs(cue));
        Assert.That(cue.IsShown, Is.False);

        cue.Show();
        Assert.That(cue.IsShown, Is.True);
        Assert.That(cueObject.activeSelf, Is.True);

        cue.HideImmediate();
        Assert.That(cue.IsShown, Is.False);
        Assert.That(cueObject.activeSelf, Is.False);
        Assert.That(EncounterLeadInCue.FindOnPlayer(player), Is.SameAs(cue));
    }
}
