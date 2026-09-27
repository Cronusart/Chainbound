using UnityEngine;

public enum DialogueCameraTargetSource
{
    None = 0,
    PresetDefined = 1,
    Player = 2,
    NpcId = 3,
    GameObjectTag = 4,
    SequenceSpeaker = 5
}

/// <summary>
/// Optional per-line camera directive used by DialogueSpeakerCameraBridge.
/// Authoring lives on DialogueLine so dialogue assets can define framing directly.
/// </summary>
[System.Serializable]
public class DialogueLineCameraDirective
{
    [Tooltip("Optional shot preset for this line. If left empty, the bridge can fall back to speaker rule or default shot selection.")]
    public CameraShotPresetSO shotPreset;

    [Tooltip("How this line resolves its camera target. Sequence Speaker uses the owning DialogueSequenceSO's Speaker NPC ID. None means no explicit target override.")]
    public DialogueCameraTargetSource targetSource = DialogueCameraTargetSource.None;

    [Tooltip("NPC registry ID used when Target Source = NpcId.")]
    public string npcId = "";

    [Tooltip("Scene tag used when Target Source = GameObjectTag.")]
    public string targetTag = "";
}
