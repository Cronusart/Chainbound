#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using Chainbound.Combat;
using DG.Tweening;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;

public sealed class ChainlingProgressionFolioEditModeTests
{
    private const string SpeciesRoot = "Assets/Data/Combat/ChainlingSpecies";
    private const string CanvasPrefabPath = "Assets/Prefabs/Combat/CombatBattleCanvas.prefab";
    private const string ProfilePath = "Assets/Data/Combat/UI/ProgressionFolioPresentation.asset";

    [Test]
    public void AuthoredSpeciesPools_AreRepeatableAndKeepExclusiveMoves()
    {
        string[] guids = AssetDatabase.FindAssets("t:SpeciesDefinitionSO", new[] { SpeciesRoot });
        int entryCount = 0;
        int exclusiveCount = 0;
        int populatedSpeciesCount = 0;
        for (int i = 0; i < guids.Length; i++)
        {
            SpeciesDefinitionSO species = AssetDatabase.LoadAssetAtPath<SpeciesDefinitionSO>(AssetDatabase.GUIDToAssetPath(guids[i]));
            Assert.That(species, Is.Not.Null);
            if (species.progressionMovePool.Count > 0)
                populatedSpeciesCount++;
            for (int j = 0; j < species.progressionMovePool.Count; j++)
            {
                SpeciesProgressionMoveEntry entry = species.progressionMovePool[j];
                entryCount++;
                Assert.That(entry.excludeIfOwned, Is.False, $"{species.speciesId}/{entry.move?.moveId} still excludes owned copies.");
                if (entry.poolTag == ProgressionPoolTag.Exclusive)
                    exclusiveCount++;
            }
        }

        Assert.That(populatedSpeciesCount, Is.EqualTo(11));
        Assert.That(entryCount, Is.EqualTo(142));
        Assert.That(exclusiveCount, Is.GreaterThan(0));
    }

    [Test]
    public void OwnedMovePreview_RetainsProbabilityAndReportsCopies()
    {
        MoveDefinitionSO move = ScriptableObject.CreateInstance<MoveDefinitionSO>();
        SpeciesDefinitionSO species = ScriptableObject.CreateInstance<SpeciesDefinitionSO>();
        ChainlingProgressionRewardTablesSO tables = ChainlingProgressionRewardTablesSO.CreateRuntimeDefault();
        try
        {
            move.moveId = "repeatable_move";
            move.displayName = "Repeatable Move";
            species.progressionMovePool = new List<SpeciesProgressionMoveEntry>
            {
                new SpeciesProgressionMoveEntry
                {
                    move = move,
                    weight = 10,
                    minStar = 0,
                    maxStar = 5,
                    excludeIfOwned = false,
                },
            };
            tables.starKnownMoveChanceStandard = new[] { 100, 100, 100, 100, 100 };

            List<MoveLearnPreviewRow> rows = MoveLearnPreviewUtility.BuildPreviewRows(
                species,
                0,
                false,
                tables,
                null,
                new[] { move.moveId },
                new Dictionary<string, int> { { move.moveId, 3 } });

            Assert.That(rows, Has.Count.EqualTo(1));
            Assert.That(rows[0].learnPercent, Is.EqualTo(100f).Within(0.001f));
            Assert.That(rows[0].ownedCopyCount, Is.EqualTo(3));
        }
        finally
        {
            Object.DestroyImmediate(move);
            Object.DestroyImmediate(species);
            Object.DestroyImmediate(tables);
        }
    }

    [Test]
    public void CombatCanvas_AuthorsFolioOverlayAndPresentationProfile()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CanvasPrefabPath);
        ProgressionFolioPresentationSO profile = AssetDatabase.LoadAssetAtPath<ProgressionFolioPresentationSO>(ProfilePath);
        Assert.That(prefab, Is.Not.Null);
        Assert.That(profile, Is.Not.Null);
        Assert.That(profile.uiGlitchMaterial, Is.Not.Null);
        Assert.That(profile.noFolioClip, Is.Not.Null);

        CombatPostBattleReportController report = prefab.GetComponentInChildren<CombatPostBattleReportController>(true);
        CombatProgressionFolioRevealView reveal = prefab.GetComponentInChildren<CombatProgressionFolioRevealView>(true);
        Assert.That(report, Is.Not.Null);
        Assert.That(reveal, Is.Not.Null);
        Assert.That(reveal.transform.name, Is.EqualTo("ProgressionFolioOverlay"));
        Assert.That(reveal.GetComponent<AudioSource>(), Is.Not.Null);
        Assert.That(reveal.GetComponent<UnityEngine.UI.Button>(), Is.Not.Null);

        SerializedObject serialized = new SerializedObject(report);
        Assert.That(serialized.FindProperty("progressionFolioReveal").objectReferenceValue, Is.EqualTo(reveal));
        Assert.That(serialized.FindProperty("progressionFolioPresentation").objectReferenceValue, Is.EqualTo(profile));
    }

    [Test]
    public void Report_ConsumesPreparedSummaryByReference()
    {
        GameObject host = new GameObject("ReportTest");
        try
        {
            CombatPostBattleReportController report = host.AddComponent<CombatPostBattleReportController>();
            var result = new BattleResult();
            var summary = new BattleRewardSummary { ramEarned = 123 };
            report.SetPreparedRewards(new PreparedBattleRewards { result = result, summary = summary });

            FieldInfo resultField = typeof(CombatPostBattleReportController).GetField("_result", BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo summaryField = typeof(CombatPostBattleReportController).GetField("_rewardSummary", BindingFlags.Instance | BindingFlags.NonPublic);
            resultField.SetValue(report, result);
            MethodInfo bind = typeof(CombatPostBattleReportController).GetMethod("BindRewards", BindingFlags.Instance | BindingFlags.NonPublic);
            bind.Invoke(report, null);

            Assert.That(summaryField.GetValue(report), Is.SameAs(summary));
        }
        finally
        {
            Object.DestroyImmediate(host);
        }
    }

    [Test]
    public void FolioView_UsesDistinctOutcomeHeadings()
    {
        GameObject parent = new GameObject("FolioTestParent", typeof(RectTransform));
        ProgressionFolioPresentationSO profile = ProgressionFolioPresentationSO.CreateRuntimeDefault();
        try
        {
            CombatProgressionFolioRevealView view = CombatProgressionFolioRevealView.Create(parent.transform, null);
            var expectations = new Dictionary<ProgressionGrantOutcome, string>
            {
                { ProgressionGrantOutcome.FolioIssued, profile.issuedHeading },
                { ProgressionGrantOutcome.NoFolioIssued, profile.noFolioHeading },
                { ProgressionGrantOutcome.IssuancePending, profile.pendingHeading },
                { ProgressionGrantOutcome.IssuanceError, profile.interruptedHeading },
            };

            foreach (KeyValuePair<ProgressionGrantOutcome, string> expectation in expectations)
            {
                view.Play(new ProgressionGrantResult
                {
                    outcome = expectation.Key,
                    eventType = ProgressionGrantEventType.StarUp,
                    starIndex = 3,
                    moveId = "test",
                    grantSucceeded = expectation.Key == ProgressionGrantOutcome.FolioIssued,
                }, null, null, profile);
                if (expectation.Key == ProgressionGrantOutcome.FolioIssued)
                {
                    Assert.That(FindDeep(view.transform, "LibraryCardRoot").gameObject.activeSelf, Is.True);
                    Assert.That(FindDeep(view.transform, "CardFace"), Is.Not.Null);
                    Assert.That(FindDeep(view.transform, "ContinueButton").gameObject.activeSelf, Is.False);
                }
                else
                {
                    TextMeshProUGUI outcome = FindDeep(view.transform, "Outcome").GetComponent<TextMeshProUGUI>();
                    Assert.That(outcome.text, Is.EqualTo(expectation.Value));
                }
            }
        }
        finally
        {
            Object.DestroyImmediate(parent);
            Object.DestroyImmediate(profile);
        }
    }

    [Test]
    public void StarDisplay_UsesNewStarOnlyAndProvidesMasterySweep()
    {
        GameObject host = new GameObject("StarDisplayTest", typeof(RectTransform), typeof(CompactStarRatingDisplay));
        try
        {
            CompactStarRatingDisplay display = host.GetComponent<CompactStarRatingDisplay>();
            display.BuildDefaultLayoutForEditor();
            display.SetRating(2, true, false);

            Tween starGain = display.PlayStarGain(3, 0.5f);
            Assert.That(starGain, Is.Not.Null);
            Assert.That(starGain.target, Is.Not.SameAs(host.transform), "First-time star-up must not scale the whole reward row.");

            Tween mastery = display.PlayMasteryCycle(0.8f);
            Assert.That(mastery, Is.Not.Null);
            Assert.That(mastery.Duration(), Is.GreaterThanOrEqualTo(0.75f));
            starGain.Kill(false);
            mastery.Kill(false);
        }
        finally
        {
            Object.DestroyImmediate(host);
        }
    }

    private static Transform FindDeep(Transform root, string name)
    {
        if (root == null)
            return null;
        if (root.name == name)
            return root;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindDeep(root.GetChild(i), name);
            if (found != null)
                return found;
        }
        return null;
    }
}
#endif
