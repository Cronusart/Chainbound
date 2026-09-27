using System.Linq;
using UnityEngine;

/// <summary>
/// Runtime narrative debug HUD: main + child tracks, signals, quests, locks, variables.
/// Add next to <see cref="SequenceRunner"/> (e.g. GameServices). Enable <see cref="SequenceRunner.ShowDebugUI"/> or this overlay's toggle.
/// </summary>
public class NarrativeDebugOverlay : MonoBehaviour
{
    [SerializeField] private bool showOverlay = true;
    [SerializeField] private KeyCode toggleKey = KeyCode.F9;
    [SerializeField] private Vector2 panelPosition = new Vector2(10, 10);
    [SerializeField] private Vector2 panelSize = new Vector2(520, 420);

    private bool _visible = true;

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
            _visible = !_visible;
    }

    private void OnGUI()
    {
        var runner = SequenceRunner.Instance;
        bool runnerWants = runner != null && runner.ShowDebugUI;
        if (!showOverlay && !runnerWants)
            return;
        if (!_visible)
            return;

        GUILayout.BeginArea(new Rect(panelPosition.x, panelPosition.y, panelSize.x, panelSize.y));
        GUILayout.BeginVertical("box");

        GUILayout.Label("Narrative Debug (F9)", new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold });

        DrawMainTrack(runner);
        DrawChildTracks(runner);
        DrawSignals();
        DrawQuests();
        DrawLocks(runner);
        DrawVariables(runner);

        GUILayout.EndVertical();
        GUILayout.EndArea();
    }

    private static void DrawMainTrack(SequenceRunner runner)
    {
        GUILayout.Label("Main track", new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold });
        if (runner == null)
        {
            GUILayout.Label("  SequenceRunner missing");
            return;
        }

        var main = runner.GetMainExecution();
        GUILayout.Label($"  Running: {runner.IsRunning}");
        GUILayout.Label($"  Sequence: {(main.Sequence != null ? main.Sequence.name : "None")}");
        GUILayout.Label($"  Step: {main.CurrentStepIndex + 1}/{(main.Sequence != null ? main.Sequence.StepCount : 0)}");
        GUILayout.Label($"  Call stack: {main.CallStack.Count}");
        GUILayout.Label($"  Last outcome: {main.LastStepOutcome.Result}");
    }

    private static void DrawChildTracks(SequenceRunner runner)
    {
        GUILayout.Space(4);
        GUILayout.Label("Child tracks", new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold });
        if (runner == null)
        {
            GUILayout.Label("  —");
            return;
        }

        var children = runner.GetChildExecutions();
        if (children == null || children.Count == 0)
        {
            GUILayout.Label("  (none)");
            return;
        }

        foreach (var kv in children)
        {
            var e = kv.Value;
            GUILayout.Label($"  • {kv.Key} [{e.TrackType}] running={e.IsRunning} step={e.CurrentStepIndex + 1}/{(e.Sequence != null ? e.Sequence.StepCount : 0)}");
        }
    }

    private static void DrawSignals()
    {
        GUILayout.Space(4);
        GUILayout.Label("Signals (recent)", new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold });
        var bus = NarrativeSignalBus.Instance;
        if (bus == null)
        {
            GUILayout.Label("  NarrativeSignalBus missing");
            return;
        }

        var recent = bus.GetRecentSignals();
        if (recent == null || recent.Count == 0)
        {
            GUILayout.Label("  (none)");
            return;
        }

        foreach (var r in recent.Reverse().Take(8))
            GUILayout.Label($"  • {r.signalId} @ {r.time:F1}s", new GUIStyle(GUI.skin.label) { fontSize = 10 });
    }

    private static void DrawQuests()
    {
        GUILayout.Space(4);
        GUILayout.Label("Quests", new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold });
        var qs = QuestService.Instance;
        if (qs == null)
        {
            GUILayout.Label("  QuestService missing");
            return;
        }

        var ids = qs.GetRegisteredQuestIds();
        if (ids == null || ids.Count == 0)
        {
            GUILayout.Label("  (none registered)");
            return;
        }

        foreach (string id in ids.Take(12))
        {
            var life = qs.GetQuestLifecycleState(id);
            GUILayout.Label($"  • {id}: {life}", new GUIStyle(GUI.skin.label) { fontSize = 10 });
        }
    }

    private static void DrawLocks(SequenceRunner runner)
    {
        GUILayout.Space(4);
        GUILayout.Label("Input locks", new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold });
        var inputLock = InputLockService.Instance;
        if (inputLock != null)
        {
            GUILayout.Label($"  Active: {inputLock.ActiveLockCount}");
            if (runner != null)
            {
                var main = runner.GetMainExecution();
                GUILayout.Label($"  NPC claims (main ctx): {main.Context != null}");
            }
        }
        else
            GUILayout.Label("  InputLockService missing");
    }

    private static void DrawVariables(SequenceRunner runner)
    {
        GUILayout.Space(4);
        GUILayout.Label("Variables (main)", new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold });
        if (runner == null)
            return;
        var ctx = runner.Context;
        GUILayout.Label($"  PlayerName: {ctx.PlayerName}");
        GUILayout.Label($"  Starter: {ctx.StarterSelected}");
        var all = ctx.Variables.GetAllVariables();
        foreach (var kvp in all.Take(10))
            GUILayout.Label($"  {kvp.Key}: {kvp.Value}", new GUIStyle(GUI.skin.label) { fontSize = 9 });
    }
}
