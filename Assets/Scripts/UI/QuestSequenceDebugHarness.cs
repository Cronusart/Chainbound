using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Phase 0 instrumentation: runtime overlay showing active quests and sequence state.
/// Helps identify which steps are pending and which quests are active during testing.
/// Only active in Editor or Development builds.
///
/// Setup: Add to a persistent GameObject (e.g. GameServices). Toggle with F10.
/// </summary>
public class QuestSequenceDebugHarness : MonoBehaviour
{
#if UNITY_EDITOR || DEVELOPMENT_BUILD

    [Header("Overlay")]
    [SerializeField] private bool showOverlay = true;
    [SerializeField] private KeyCode toggleKey = KeyCode.F10;
    [SerializeField] private float panelWidth = 320f;
    [SerializeField] private float panelHeight = 280f;
    [SerializeField] private float panelX = 10f;
    [SerializeField] private float panelY = 10f;

    private bool _overlayVisible = true;

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
            _overlayVisible = !_overlayVisible;
    }

    private void OnGUI()
    {
        if (!showOverlay || !_overlayVisible) return;

        GUILayout.BeginArea(new Rect(panelX, panelY, panelWidth, panelHeight));
        GUI.Box(new Rect(0, 0, panelWidth, panelHeight), "");
        GUILayout.BeginVertical(GUILayout.Width(panelWidth - 10));

        GUILayout.Label("Quest & Sequence Harness (F10)", new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold });

        // --- Active Quests ---
        GUILayout.Space(4);
        GUILayout.Label("Active Quests:", new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold });
        var questService = QuestService.Instance;
        if (questService != null)
        {
            var questIds = questService.GetActiveQuestIds();
            if (questIds.Count == 0)
            {
                GUILayout.Label("  (none)");
            }
            else
            {
                foreach (string questId in questIds)
                {
                    var state = questService.GetQuestState(questId);
                    if (state != null)
                    {
                        string objSummary = FormatObjectives(state);
                        GUILayout.Label($"  • {questId}", new GUIStyle(GUI.skin.label) { fontSize = 10 });
                        if (!string.IsNullOrEmpty(objSummary))
                        {
                            GUILayout.Label($"    {objSummary}", new GUIStyle(GUI.skin.label) { fontSize = 9 });
                        }
                    }
                }
            }
        }
        else
        {
            GUILayout.Label("  QuestService not found");
        }

        // --- Active Sequence ---
        GUILayout.Space(6);
        GUILayout.Label("Sequence:", new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold });
        var runner = SequenceRunner.Instance;
        if (runner != null)
        {
            GUILayout.Label($"  Running: {runner.IsRunning}");
            var seq = runner.CurrentSequence;
            GUILayout.Label($"  Current: {(seq != null ? seq.name : "None")}");
            int stepIdx = runner.CurrentStepIndex;
            int stepCount = seq != null ? seq.StepCount : 0;
            string stepDisplay = stepIdx >= 0 ? $"{stepIdx + 1}/{stepCount}" : (seq != null ? $"-/{stepCount}" : "-");
            GUILayout.Label($"  Step: {stepDisplay}");

            if (seq != null && stepIdx >= 0 && stepIdx < seq.StepCount)
            {
                var step = seq.GetStep(stepIdx);
                if (step != null)
                {
                    GUILayout.Label($"  Current step: {step.GetStepName()}", new GUIStyle(GUI.skin.label) { fontSize = 10 });
                }
            }

            int pendingIdx = stepIdx + 1;
            if (seq != null && pendingIdx >= 0 && pendingIdx < seq.StepCount)
            {
                var pendingStep = seq.GetStep(pendingIdx);
                if (pendingStep != null)
                {
                    GUILayout.Label($"  Pending: {pendingStep.GetStepName()}", new GUIStyle(GUI.skin.label) { fontSize = 10 });
                }
            }
        }
        else
        {
            GUILayout.Label("  SequenceRunner not found");
        }

        GUILayout.EndVertical();
        GUILayout.EndArea();
    }

    private static string FormatObjectives(QuestState state)
    {
        if (state?.objectives == null || state.objectives.Count == 0)
            return "";

        var parts = new List<string>();
        foreach (var kvp in state.objectives)
        {
            var obj = kvp.Value;
            if (obj != null)
            {
                parts.Add($"{obj.objectiveId}: {obj.currentProgress}/{obj.targetAmount}");
            }
        }
        return string.Join(" | ", parts);
    }

#endif
}
