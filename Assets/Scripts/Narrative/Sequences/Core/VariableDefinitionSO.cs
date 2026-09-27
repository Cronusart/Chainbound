using UnityEngine;

/// <summary>
/// Authoring-time variable identity for migration from raw string keys. Steps may reference this asset optionally.
/// </summary>
[CreateAssetMenu(fileName = "VariableDefinition", menuName = "Chainbound/Narrative/Sequences/Variable Definition")]
public class VariableDefinitionSO : ScriptableObject
{
    [Tooltip("Key used in SequenceVariableStore / SequenceContext.")]
    [SerializeField] private string key = "";

    [SerializeField] private NarrativeVariableType variableType = NarrativeVariableType.String;

    [SerializeField] private VariableScope defaultScope = VariableScope.Local;

    [SerializeField] private bool isPersistent;

    [SerializeField] private string category = "";

    [TextArea(2, 4)]
    [SerializeField] private string description = "";

    public string Key => key;
    public NarrativeVariableType VariableType => variableType;
    public VariableScope DefaultScope => defaultScope;
    public bool IsPersistent => isPersistent;
    public string Category => category;
    public string Description => description;
}
