using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Starts a curated demo/test sequence when a demo scene is opened directly,
/// without bouncing through the main menu's pending-sequence reload path.
/// </summary>
public class DemoSceneSequenceBootstrap : MonoBehaviour
{
    [Header("Launch")]
    [SerializeField] private MainMenuTestSequenceLaunchSO launchEntry;
    [SerializeField] private bool autoStartOnPlay = true;

    [Header("Optional Quest")]
    [SerializeField] private bool autoStartQuest = false;
    [SerializeField] private QuestDefinitionSO questDefinition;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    private bool _startAttempted;

    private IEnumerator Start()
    {
        if (_startAttempted || !autoStartOnPlay)
            yield break;

        _startAttempted = true;

        // Let bootstrap/services finish waking before we stamp context and play.
        yield return null;

        TryStartSequence();
    }

    private void TryStartSequence()
    {
        if (!MainMenuTestSequenceLauncher.TryValidateEntry(launchEntry, out string reason))
        {
            Debug.LogWarning($"[DemoSceneSequenceBootstrap] Cannot auto-start demo sequence: {reason}");
            return;
        }

        Scene activeScene = SceneManager.GetActiveScene();
        if (!string.IsNullOrWhiteSpace(launchEntry.LaunchSceneName) &&
            !string.Equals(activeScene.name, launchEntry.LaunchSceneName, StringComparison.Ordinal))
        {
            Debug.LogWarning(
                $"[DemoSceneSequenceBootstrap] Launch entry '{launchEntry.name}' targets scene '{launchEntry.LaunchSceneName}', " +
                $"but active scene is '{activeScene.name}'.");
            return;
        }

        GameSessionContext context = MainMenuTestSequenceLauncher.ApplyLaunchProfile(launchEntry);

        if (autoStartQuest)
        {
            TryStartQuest();
        }

        SequenceDirector.Instance.Play(launchEntry.Sequence);

        if (enableDebugLogs)
        {
            Debug.Log(
                $"[DemoSceneSequenceBootstrap] Started '{launchEntry.DisplayName}' " +
                $"scene='{activeScene.name}' spawn='{context.DesiredSpawnTag}' sequence='{launchEntry.Sequence.name}'");
        }
    }

    private void TryStartQuest()
    {
        if (questDefinition == null)
        {
            Debug.LogWarning("[DemoSceneSequenceBootstrap] Auto-start quest is enabled, but no quest definition is assigned.");
            return;
        }

        QuestService questService = QuestService.Instance;
        if (questService == null)
        {
            Debug.LogWarning("[DemoSceneSequenceBootstrap] QuestService not found. Demo quest could not be auto-started.");
            return;
        }

        questService.StartQuest(questDefinition.QuestId, questDefinition);

        if (enableDebugLogs)
        {
            Debug.Log($"[DemoSceneSequenceBootstrap] Auto-started quest: {questDefinition.QuestId}");
        }
    }
}
