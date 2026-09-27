using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "QuestCatalog", menuName = "Chainbound/Narrative/Persistence/Quest Catalog")]
public sealed class QuestCatalogSO : ScriptableObject
{
    [SerializeField] private List<QuestDefinitionSO> quests = new List<QuestDefinitionSO>();

    private Dictionary<string, QuestDefinitionSO> byId;

    public IReadOnlyList<QuestDefinitionSO> Quests => quests;

    public bool TryGet(string questId, out QuestDefinitionSO definition)
    {
        EnsureIndex();
        return byId.TryGetValue(questId ?? "", out definition);
    }

    private void EnsureIndex()
    {
        if (byId != null)
            return;

        byId = new Dictionary<string, QuestDefinitionSO>(StringComparer.Ordinal);
        if (quests == null)
            return;

        for (int i = 0; i < quests.Count; i++)
        {
            QuestDefinitionSO definition = quests[i];
            if (definition == null || string.IsNullOrWhiteSpace(definition.QuestId))
                continue;
            if (!byId.ContainsKey(definition.QuestId))
                byId.Add(definition.QuestId, definition);
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        byId = null;
        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < quests.Count; i++)
        {
            QuestDefinitionSO definition = quests[i];
            if (definition == null)
            {
                Debug.LogWarning($"[QuestCatalogSO] Entry {i} is missing a quest definition.", this);
                continue;
            }
            if (string.IsNullOrWhiteSpace(definition.QuestId) || !ids.Add(definition.QuestId))
                Debug.LogWarning($"[QuestCatalogSO] Duplicate or empty quest ID at entry {i}.", this);
        }
    }
#endif
}
