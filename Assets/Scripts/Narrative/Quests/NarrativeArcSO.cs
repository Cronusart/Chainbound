using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Top-level narrative grouping above individual quests. Data-only in MVP; runtime orchestration can query this asset later.
/// </summary>
public enum NarrativeArcType
{
    Main,
    Side,
    WorldEvent,
    Faction,
    Seasonal
}

[CreateAssetMenu(fileName = "NarrativeArc", menuName = "Chainbound/Narrative/Quests/Narrative Arc")]
public class NarrativeArcSO : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string arcId = "";

    [SerializeField] private NarrativeArcType arcType = NarrativeArcType.Main;

    [Tooltip("Higher runs first when multiple arcs compete (future use).")]
    [SerializeField] private int priority = 0;

    [Header("Content")]
    [SerializeField] private List<QuestDefinitionSO> quests = new List<QuestDefinitionSO>();

    [Header("Conditions")]
    [Tooltip("All must pass for arc to be considered enterable (evaluated by future services).")]
    [SerializeField] private List<SequenceCondition> entryConditions = new List<SequenceCondition>();

    [Header("Notes")]
    [TextArea(2, 5)]
    [SerializeField] private string designerNotes = "";

    public string ArcId => arcId;
    public NarrativeArcType ArcType => arcType;
    public int Priority => priority;
    public IReadOnlyList<QuestDefinitionSO> Quests => quests;
    public IReadOnlyList<SequenceCondition> EntryConditions => entryConditions;
    public string DesignerNotes => designerNotes;
}
