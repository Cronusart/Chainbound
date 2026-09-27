using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

public sealed class OfflineOnboardingCommitTests
{
    private const string ProfileKey = "SignalJam_Profile";

    private GameObject serviceObject;
    private LocalSaveServiceStub service;
    private bool hadProfile;
    private string previousProfile;

    [SetUp]
    public void SetUp()
    {
        hadProfile = PlayerPrefs.HasKey(ProfileKey);
        previousProfile = hadProfile ? PlayerPrefs.GetString(ProfileKey) : null;

        service = Object.FindFirstObjectByType<LocalSaveServiceStub>(FindObjectsInactive.Include);
        if (service == null)
        {
            serviceObject = new GameObject("OfflineOnboardingCommitTests.LocalSaveServiceStub");
            service = serviceObject.AddComponent<LocalSaveServiceStub>();
        }

        PlayerPrefs.DeleteKey(ProfileKey);
    }

    [TearDown]
    public void TearDown()
    {
        if (serviceObject != null)
            Object.DestroyImmediate(serviceObject);

        if (hadProfile)
            PlayerPrefs.SetString(ProfileKey, previousProfile);
        else
            PlayerPrefs.DeleteKey(ProfileKey);

        PlayerPrefs.Save();
    }

    [Test]
    public async Task OfflineCommit_PersistsNameAndStarterWithoutIdentity()
    {
        PlayerNameCommitResult nameResult = await service.CommitDisplayNameAsync("", "Offline Tester");
        StarterCommitResult starterResult = await service.CommitStarterAsync("", "starter_alpha", null, "Byte");

        Assert.That(nameResult.Success, Is.True);
        Assert.That(nameResult.DisplayName, Is.EqualTo("Offline Tester"));
        Assert.That(starterResult.Success, Is.True);
        Assert.That(starterResult.SpeciesId, Is.EqualTo("starter_alpha"));
        Assert.That(starterResult.BackendChainlingId, Is.EqualTo("offline-starter_alpha"));

        ProfileData profile = service.LoadProfile();
        Assert.That(profile, Is.Not.Null);
        Assert.That(profile.playerName, Is.EqualTo("Offline Tester"));
        Assert.That(profile.starterChainlingId, Is.EqualTo("starter_alpha"));
        Assert.That(profile.starterDisplayName, Is.EqualTo("Byte"));

        await service.CommitStarterAsync("", "starter_other", null, "Overwrite Attempt");
        profile = service.LoadProfile();
        Assert.That(profile.starterChainlingId, Is.EqualTo("starter_alpha"),
            "The local stub retains the first starter species on a retry.");
        Assert.That(profile.starterDisplayName, Is.EqualTo("Byte"),
            "A persisted offline starter name is never overwritten by a retry.");
    }
}
