#if UNITY_EDITOR
using Chainbound.Combat;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class CombatPostBattleFlowAuthoringTests
{
    private const string PrefabPath = "Assets/Prefabs/Combat/CombatPostBattleReport.prefab";
    private const string HarnessPath = "Assets/Scenes/Combat2_TestHarness.unity";

    [Test]
    public void ReportPrefab_HasBannerReportAndRewardsSurfaces()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Assert.That(prefab, Is.Not.Null);
        Assert.That(FindDeep(prefab.transform, "BannerSurface"), Is.Not.Null);
        Assert.That(FindDeep(prefab.transform, "BannerCard"), Is.Not.Null);
        Assert.That(FindDeep(prefab.transform, "BannerContents"), Is.Not.Null);
        Assert.That(FindDeep(prefab.transform, "ReportSurface"), Is.Not.Null);
        Assert.That(FindDeep(prefab.transform, "RewardsSurface"), Is.Not.Null);
        Assert.That(FindDeep(prefab.transform, "BannerTitle"), Is.Not.Null);
        Assert.That(FindDeep(prefab.transform, "XpRows"), Is.Not.Null);
        Assert.That(FindDeep(prefab.transform, "RamLabel"), Is.Not.Null);

        Transform report = FindDeep(prefab.transform, "ReportSurface");
        Assert.That(report.GetComponent<CombatUiScanlineBackground>(), Is.Not.Null);
        Transform continueButton = FindDeep(prefab.transform, "ContinueButton");
        Assert.That(continueButton.GetComponent<CombatUiSelectableButtonVisual>(), Is.Not.Null);
        RectTransform continueRect = continueButton as RectTransform;
        Assert.That(continueRect.sizeDelta.x, Is.GreaterThan(200f));
        Assert.That(continueRect.sizeDelta.y, Is.GreaterThan(40f));
        RectTransform rewardsContinue = FindDeep(prefab.transform, "RewardsContinueButton") as RectTransform;
        Assert.That(rewardsContinue.sizeDelta.x, Is.GreaterThan(200f));
        Assert.That(rewardsContinue.sizeDelta.y, Is.GreaterThan(40f));
    }

    [Test]
    public void BannerStripMotion_UsesSevenPercentYScale()
    {
        Vector3 strip = CombatBannerStripMotion.StripScale(Vector3.one, CombatBannerStripMotion.DefaultStripScaleY);
        Assert.AreEqual(1f, strip.x);
        Assert.AreEqual(0.07f, strip.y, 0.0001f);
    }

    [Test]
    public void TestHarness_HasNoCombatOutcomePopupController()
    {
        Scene scene = EditorSceneManager.OpenScene(HarnessPath, OpenSceneMode.Additive);
        try
        {
            MonoBehaviour[] behaviours = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] == null)
                    continue;
                Assert.That(
                    behaviours[i].GetType().Name,
                    Is.Not.EqualTo("CombatOutcomePopupController"),
                    "Combat2_TestHarness still has CombatOutcomePopupController.");
                Assert.That(behaviours[i].gameObject.name, Is.Not.EqualTo("OutcomePopup"));
            }
        }
        finally
        {
            if (scene.IsValid() && scene.isLoaded && SceneManager.sceneCount > 1)
                EditorSceneManager.CloseScene(scene, true);
        }
    }

    [Test]
    public void LastWildCapture_IsCaptureNotDraw()
    {
        BattleOutcomePresentation presentation = BattleOutcomePresentation.Resolve(new BattleResult
        {
            winnerTeamIndex = -1,
            completionReason = BattleCompletionReason.Captured,
            captureAttempted = true,
            captureSucceeded = true,
        });

        Assert.AreEqual(BattleOutcomeKind.Capture, presentation.Kind);
        Assert.AreEqual("CAPTURE", presentation.ReportTitle);
        Assert.IsTrue(presentation.ShowRewardsPage);
        Assert.That(CombatNarrationComposer.ComposeBattleResult(
            new BattleResult
            {
                winnerTeamIndex = -1,
                turnCount = 4,
                completionReason = BattleCompletionReason.Captured,
                captureAttempted = true,
                captureSucceeded = true,
            },
            null,
            0).text, Does.Not.Contain("draw"));
    }

    [Test]
    public void KoAfterPriorCapture_IsVictoryWithCaptureFlag()
    {
        var result = new BattleResult
        {
            winnerTeamIndex = 0,
            turnCount = 6,
            captureAttempted = true,
            captureSucceeded = true,
            capturedBackendChainlingId = "abc",
        };
        result.playerParticipants.Add(new BattleParticipant { backendChainlingId = "abc", displayName = "Kitsauto" });

        BattleOutcomePresentation presentation = BattleOutcomePresentation.Resolve(result);
        Assert.AreEqual(BattleOutcomeKind.Victory, presentation.Kind);
        Assert.IsTrue(presentation.DidCapture);
        Assert.That(CombatNarrationComposer.ComposeBattleResult(result, null, 0).text, Does.Contain("Kitsauto"));
    }

    [Test]
    public void FleeAndTrueDraw_ResolveExpectedKinds()
    {
        Assert.AreEqual(BattleOutcomeKind.Withdrawn, BattleOutcomePresentation.Resolve(new BattleResult
        {
            winnerTeamIndex = 1,
            wasFled = true,
        }).Kind);
        Assert.AreEqual(BattleOutcomeKind.Draw, BattleOutcomePresentation.Resolve(new BattleResult
        {
            winnerTeamIndex = -2,
        }).Kind);
        Assert.IsFalse(BattleOutcomePresentation.Resolve(new BattleResult
        {
            winnerTeamIndex = 1,
            wasFled = true,
        }).ShowRewardsPage);
    }

    [Test]
    public void CaptureTerminal_DoesNotApplyRamPenalty()
    {
        PostBattleRewardsConfigSO config = PostBattleRewardsConfigSO.CreateRuntimeDefault();
        try
        {
            config.capturedReceivesXp = true;
            var result = new BattleResult
            {
                battleType = BattleType.PvE,
                winnerTeamIndex = -1,
                completionReason = BattleCompletionReason.Captured,
                captureAttempted = true,
                captureSucceeded = true,
                totalEnemyKOs = 1,
            };
            result.playerParticipants.Add(new BattleParticipant { displayName = "Validape", speciesId = "validape", masteryXp = 0 });
            BattleRewardSummary summary = new StandardBattleRewardCalculator(config).Calculate(result);

            Assert.AreEqual(0, summary.ramPenalty);
            Assert.Greater(summary.chainlingRewards.Count, 0);
        }
        finally
        {
            Object.DestroyImmediate(config);
        }
    }

    private static Transform FindDeep(Transform root, string name)
    {
        if (root == null)
            return null;
        if (string.Equals(root.name, name, System.StringComparison.OrdinalIgnoreCase))
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
