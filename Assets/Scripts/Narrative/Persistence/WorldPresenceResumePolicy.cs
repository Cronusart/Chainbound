using System;
using System.Collections.Generic;

/// <summary>
/// Continue-game restores rail flags from the checkpoint row. An empty snapshot
/// (old saves) falls back to the authored <see cref="Step_SetCheckpoint"/> list.
/// </summary>
public static class WorldPresenceResumePolicy
{
    public static List<(string key, bool value)> Resolve(
        IReadOnlyList<NarrativeFlagValue> checkpointSnapshot,
        IReadOnlyList<Step_SetCheckpoint.ResumeProfileWorldFlag> authoredFallback)
    {
        var resolved = new List<(string key, bool value)>();
        AppendSnapshot(checkpointSnapshot, resolved);
        if (resolved.Count > 0)
            return resolved;

        if (authoredFallback == null)
            return resolved;

        for (int i = 0; i < authoredFallback.Count; i++)
        {
            Step_SetCheckpoint.ResumeProfileWorldFlag entry = authoredFallback[i];
            if (entry == null || string.IsNullOrWhiteSpace(entry.flagKey))
                continue;
            resolved.Add((entry.flagKey.Trim(), entry.flagValue));
        }

        return resolved;
    }

    private static void AppendSnapshot(
        IReadOnlyList<NarrativeFlagValue> snapshot,
        List<(string key, bool value)> destination)
    {
        if (snapshot == null || destination == null)
            return;

        for (int i = 0; i < snapshot.Count; i++)
        {
            NarrativeFlagValue entry = snapshot[i];
            if (entry == null || string.IsNullOrWhiteSpace(entry.key))
                continue;
            destination.Add((entry.key.Trim(), entry.value));
        }
    }
}
