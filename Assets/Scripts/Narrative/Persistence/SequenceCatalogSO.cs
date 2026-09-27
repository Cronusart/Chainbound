using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "SequenceCatalog", menuName = "Chainbound/Narrative/Persistence/Sequence Catalog")]
public sealed class SequenceCatalogSO : ScriptableObject
{
    [SerializeField] private List<SequenceAsset> sequences = new List<SequenceAsset>();

    private Dictionary<string, SequenceAsset> byId;

    public IReadOnlyList<SequenceAsset> Sequences => sequences;

    public bool TryGet(string sequenceId, out SequenceAsset sequence)
    {
        EnsureIndex();
        return byId.TryGetValue(sequenceId ?? "", out sequence);
    }

    private void EnsureIndex()
    {
        if (byId != null)
            return;

        byId = new Dictionary<string, SequenceAsset>(StringComparer.Ordinal);
        if (sequences == null)
            return;

        for (int i = 0; i < sequences.Count; i++)
        {
            SequenceAsset sequence = sequences[i];
            if (sequence == null || string.IsNullOrWhiteSpace(sequence.SequenceId))
                continue;
            if (!byId.ContainsKey(sequence.SequenceId))
                byId.Add(sequence.SequenceId, sequence);
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        byId = null;
        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < sequences.Count; i++)
        {
            SequenceAsset sequence = sequences[i];
            if (sequence == null)
            {
                Debug.LogWarning($"[SequenceCatalogSO] Entry {i} is missing a sequence.", this);
                continue;
            }
            if (string.IsNullOrWhiteSpace(sequence.SequenceId) || !ids.Add(sequence.SequenceId))
                Debug.LogWarning($"[SequenceCatalogSO] Duplicate or empty sequence ID at entry {i}.", this);
        }
    }
#endif
}
