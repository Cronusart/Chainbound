using System.Collections.Generic;
using UnityEngine;

public enum SanctuaryWakeKind
{
    Continue = 0,
    CombatDefeat = 1
}

[CreateAssetMenu(
    fileName = "SanctuaryWakeCatalog",
    menuName = "Chainbound/Narrative/Dialogue/Sanctuary Wake Catalog")]
public class SanctuaryWakeCatalogSO : ScriptableObject
{
    public const string DefaultResourcePath = "Narrative/SanctuaryWakeCatalog";
    private const string ContinueRoundRobinPrefKey = "SignalJam_SanctuaryWakeIndex_v1";
    private const string DefeatRoundRobinPrefKey = "SignalJam_SanctuaryWakeDefeatIndex_v1";
    private const string DogFallbackRoundRobinPrefKey = "SignalJam_SanctuaryWakeDogIndex_v1";

    [Tooltip("Warm Sun greetings for continue / game restart. Empty speaker, auto-advance, skippable.")]
    [SerializeField] private List<DialogueSequenceSO> lines = new List<DialogueSequenceSO>();

    [Tooltip("Rebirth lines after combat defeat. Empty speaker, auto-advance, skippable.")]
    [SerializeField] private List<DialogueSequenceSO> defeatLines = new List<DialogueSequenceSO>();

    [Tooltip("Dog fallback used when a sanctuary latch has Use Dog Wake Fallback enabled. Shared by continue and party wipe.")]
    [SerializeField] private List<DialogueSequenceSO> dogFallbackLines = new List<DialogueSequenceSO>();

    public IReadOnlyList<DialogueSequenceSO> Lines => lines;
    public IReadOnlyList<DialogueSequenceSO> DefeatLines => defeatLines;
    public IReadOnlyList<DialogueSequenceSO> DogFallbackLines => dogFallbackLines;

    public DialogueSequenceSO PickNext()
    {
        return PickNext(SanctuaryWakeKind.Continue);
    }

    public DialogueSequenceSO PickNext(SanctuaryWakeKind kind)
    {
        List<DialogueSequenceSO> pool = kind == SanctuaryWakeKind.CombatDefeat ? defeatLines : lines;
        string prefKey = kind == SanctuaryWakeKind.CombatDefeat
            ? DefeatRoundRobinPrefKey
            : ContinueRoundRobinPrefKey;
        return PickNextFrom(pool, prefKey);
    }

    public DialogueSequenceSO PickDogFallback()
    {
        return PickNextFrom(dogFallbackLines, DogFallbackRoundRobinPrefKey);
    }

    private static DialogueSequenceSO PickNextFrom(List<DialogueSequenceSO> pool, string prefKey)
    {
        if (pool == null || pool.Count == 0)
            return null;

        int index = PlayerPrefs.GetInt(prefKey, 0);
        if (index < 0)
            index = 0;

        DialogueSequenceSO picked = null;
        for (int i = 0; i < pool.Count; i++)
        {
            int candidate = (index + i) % pool.Count;
            DialogueSequenceSO line = pool[candidate];
            if (line == null)
                continue;
            picked = line;
            PlayerPrefs.SetInt(prefKey, candidate + 1);
            PlayerPrefs.Save();
            break;
        }

        return picked;
    }
}
