#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

/// <summary>
/// One-shot PlayMode test runner triggered by Library/ResolverSevenBucketsTests.pending.
/// Writes Library/ResolverSevenBucketsResumeResults.txt then deletes itself.
/// </summary>
[InitializeOnLoad]
internal static class ResolverSevenBucketsTestOnce
{
    private const string PendingPath = "Library/ResolverSevenBucketsTests.pending";
    private const string ResultsPath = "Library/ResolverSevenBucketsResumeResults.txt";
    private const string SelfScriptPath = "Assets/Editor/ResolverSevenBucketsTestOnce.cs";

    private static readonly string[] DefaultFilters =
    {
        "Chainbound.Combat.Tests.CombatWallInterceptTests.ResolverLogsChemistryBypassPayloadOnDamage",
        "Chainbound.Combat.Tests.EfficiencyRefundModifierTests.GuaranteedRefundEmitsRefundPayloadOnlyWhenRefundApplies",
        "Chainbound.Combat.Tests.CombatKoAttributionTests.DirectAttackKoWithBenchAutoSwapIncludesIncomingIds",
        "Chainbound.Combat.Tests.ChainlingPartyReadModelTests.BuildSummary_GeneralView_ProducesXpBarFill",
        "Chainbound.Combat.Tests.CombatCosmeticAnimationTests.ResolverUsesVisualVariantAndVariantScopedCosmetics",
        "Chainbound.Combat.Tests.CombatPopupVisualTests.AmountScaleUtilityUsesProfileFontSizeWhenLegacyBoundsAreStale",
        "Chainbound.Combat.Tests.CombatResolverSevenBucketsTests",
    };

    static ResolverSevenBucketsTestOnce()
    {
        if (!File.Exists(PendingPath))
            return;

        EditorApplication.delayCall += StartRun;
    }

    private static void StartRun()
    {
        if (!File.Exists(PendingPath))
            return;

        string[] filters = ReadFilters();
        try { File.Delete(PendingPath); } catch { /* ignore */ }

        File.WriteAllText(ResultsPath, $"STARTED {DateTime.Now:O}\nfilters={string.Join(" | ", filters)}\n");

        var api = ScriptableObject.CreateInstance<TestRunnerApi>();
        var filter = new Filter
        {
            testMode = TestMode.PlayMode,
            testNames = filters,
        };

        api.RegisterCallbacks(new Callbacks(filters));
        api.Execute(new ExecutionSettings(filter));
    }

    private static string[] ReadFilters()
    {
        try
        {
            string raw = File.ReadAllText(PendingPath).Trim();
            if (!string.IsNullOrWhiteSpace(raw) && !raw.Equals("default", StringComparison.OrdinalIgnoreCase))
            {
                string[] lines = raw.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                if (lines.Length > 0)
                    return lines;
            }
        }
        catch
        {
            // fall through
        }

        return DefaultFilters;
    }

    private sealed class Callbacks : ICallbacks
    {
        private readonly string[] _filters;
        private readonly StringBuilder _sb = new StringBuilder();
        private int _passed;
        private int _failed;
        private int _skipped;

        public Callbacks(string[] filters)
        {
            _filters = filters;
        }

        public void RunStarted(ITestAdaptor testsToRun)
        {
            _sb.AppendLine($"RUN_STARTED count={testsToRun.TestCaseCount}");
        }

        public void RunFinished(ITestResultAdaptor result)
        {
            _sb.AppendLine($"RUN_FINISHED pass={_passed} fail={_failed} skip={_skipped} overall={result.TestStatus}");
            _sb.AppendLine($"filters={string.Join(" | ", _filters)}");
            File.WriteAllText(ResultsPath, _sb.ToString());
            Debug.Log($"[ResolverSevenBucketsTestOnce] Done pass={_passed} fail={_failed}. See {ResultsPath}");

            EditorApplication.delayCall += CleanupSelf;
        }

        public void TestStarted(ITestAdaptor test) { }

        public void TestFinished(ITestResultAdaptor result)
        {
            if (!result.HasChildren)
            {
                switch (result.TestStatus)
                {
                    case TestStatus.Passed: _passed++; break;
                    case TestStatus.Failed: _failed++; break;
                    default: _skipped++; break;
                }

                _sb.AppendLine($"{result.TestStatus}\t{result.FullName}");
                if (result.TestStatus == TestStatus.Failed)
                {
                    if (!string.IsNullOrWhiteSpace(result.Message))
                        _sb.AppendLine("  MSG: " + result.Message.Replace("\n", " | "));
                    if (!string.IsNullOrWhiteSpace(result.StackTrace))
                        _sb.AppendLine("  STACK: " + result.StackTrace.Split('\n').FirstOrDefault());
                }
            }
        }

        private static void CleanupSelf()
        {
            try
            {
                if (File.Exists(SelfScriptPath))
                    AssetDatabase.DeleteAsset(SelfScriptPath);
                string meta = SelfScriptPath + ".meta";
                if (File.Exists(meta))
                    File.Delete(meta);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ResolverSevenBucketsTestOnce] Cleanup failed: {ex.Message}");
            }
        }
    }
}
#endif
