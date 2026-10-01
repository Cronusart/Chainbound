#if UNITY_EDITOR
using System.Reflection;
using Chainbound.Combat;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

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
        Transform rowTemplate = FindDeep(prefab.transform, "XpRowTemplate");
        Assert.That(rowTemplate, Is.Not.Null, "XP row chrome is authored so runtime clones it instead of building a new row.");
        Assert.That(rowTemplate.gameObject.activeSelf, Is.True, "The rewards row must be visible on the prefab.");
        Assert.That(rowTemplate.GetComponent<CombatPostBattleXpRowView>(), Is.Not.Null);
        Assert.That(FindDeep(rowTemplate, "StarRowLayout"), Is.Not.Null, "Star icons are authored on the row, not built when rewards open.");
        Assert.That(FindDeep(prefab.transform, "ReportSurface").gameObject.activeSelf, Is.False);
        Assert.That(FindDeep(prefab.transform, "BannerSurface").gameObject.activeSelf, Is.False);
        Assert.That(FindDeep(prefab.transform, "RewardsSurface").gameObject.activeSelf, Is.True);
        Assert.That(FindDeep(prefab.transform, "RewardsIntroPlate"), Is.Null);
        Assert.That(FindDeep(prefab.transform, "RewardsSpoilsPlate"), Is.Null);
        Assert.That(FindDeep(prefab.transform, "RewardsListViewport"), Is.Null);
        Assert.That(FindDeep(prefab.transform, "LogOverflowFade"), Is.Null);
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
    public void Report_RuntimeLeavesAuthoredHierarchyAlone()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        GameObject host = Object.Instantiate(prefab);
        try
        {
            CombatPostBattleReportController report = host.AddComponent<CombatPostBattleReportController>();
            report.BindAuthoredVisuals(
                host,
                FindDeep(host.transform, "Title").GetComponent<TMP_Text>() as TextMeshProUGUI,
                FindDeep(host.transform, "Content").GetComponent<TMP_Text>() as TextMeshProUGUI,
                FindDeep(host.transform, "SummaryTab").GetComponent<Button>(),
                FindDeep(host.transform, "BattleLogTab").GetComponent<Button>(),
                FindDeep(host.transform, "ContinueButton").GetComponent<Button>(),
                FindDeep(host.transform, "Viewport").GetComponent<ScrollRect>());

            Invoke(report, "EnsureRewardPlates");
            Invoke(report, "EnsureRewardsListScroll");
            Invoke(report, "EnsureLogOverflowFade");

            Assert.That(FindDeep(host.transform, "RewardsIntroPlate"), Is.Null);
            Assert.That(FindDeep(host.transform, "RewardsSpoilsPlate"), Is.Null);
            Assert.That(FindDeep(host.transform, "RewardsListViewport"), Is.Null);
            Assert.That(FindDeep(host.transform, "LogOverflowFade"), Is.Null);
            Assert.That(FindDeep(host.transform, "RewardsOverflowFade"), Is.Null);
            Assert.That(FindDeep(host.transform, "XpRows").parent.name, Is.EqualTo("RewardsSurface"));
        }
        finally
        {
            Object.DestroyImmediate(host);
        }
    }

    [Test]
    public void BannerStripMotion_UsesSevenPercentYScale()
    {
        Vector3 strip = CombatBannerStripMotion.StripScale(Vector3.one, CombatBannerStripMotion.DefaultStripScaleY);
        Assert.AreEqual(1f, strip.x);
        Assert.AreEqual(0.07f, strip.y, 0.0001f);
    }

    [Test]
    public void BannerStripMotion_PostBattleUsesAThinSliver()
    {
        CombatBannerStripMotion.Settings settings = CombatBannerStripMotion.Settings.PostBattle;
        Vector3 strip = CombatBannerStripMotion.StripScale(Vector3.one, settings.stripScaleY);
        Assert.AreEqual(1f, strip.x);
        Assert.AreEqual(CombatBannerStripMotion.PostBattleStripScaleY, strip.y, 0.0001f);
        Assert.Less(settings.stripScaleY, CombatBannerStripMotion.DefaultStripScaleY);
        Assert.Greater(settings.overshootScale, 1.2f);
        Assert.Less(settings.slideInDuration, 0.2f);
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

    private static void Invoke(object target, string methodName)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, methodName);
        method.Invoke(target, null);
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
