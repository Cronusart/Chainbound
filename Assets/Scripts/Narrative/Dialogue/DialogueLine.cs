using UnityEngine;

/// <summary>
/// Single line of dialogue with speaker, text, and timing options.
/// </summary>
[System.Serializable]
public class DialogueLine
{
    [Tooltip("Speaker name (empty for monologue/narrator)")]
    public string speakerName = "";
    
    [Tooltip("Dialogue text")]
    [TextArea(2, 5)]
    public string text = "";
    
    [Tooltip("Characters per second typing speed (0 = instant)")]
    [Range(0f, 100f)]
    public float typeSpeed = 30f;
    
    [Tooltip("Optional: Sound event for this line (e.g. NPC chatter, computer beep). Configure loop/channel in the asset. Looped sounds stop when text reveal completes.")]
    public SoundEventSO soundEvent;
    
    [Tooltip("Optional: Portrait sprite for speaker")]
    public Sprite portraitSprite;
    
    [Tooltip("Optional: Portrait color tint")]
    public Color portraitTint = Color.white;

    [Header("Camera")]
    [Tooltip("Optional per-line camera directive. Leave empty to use bridge rule/default shot guidance with no explicit target override.")]
    public DialogueLineCameraDirective camera = new DialogueLineCameraDirective();

    [Header("Choices (optional)")]
    [Tooltip("When present, the dialogue waits for one of these choices instead of the normal continue input.")]
    public DialogueChoice[] choices;

    public bool HasChoices => choices != null && choices.Length > 0;
    
    /// <summary>
    /// Duration to display this line (calculated from type speed + text length).
    /// </summary>
    public float GetDisplayDuration()
    {
        if (typeSpeed <= 0f) return 0.5f; // Minimum display time for instant text
        
        float typeTime = text.Length / typeSpeed;
        return Mathf.Max(typeTime, 0.5f); // Minimum 0.5 seconds
    }
}

/// <summary>
/// A presentation label paired with a systems-facing value. The view only sees
/// labels; sequence steps receive the value selected by the player.
/// </summary>
[System.Serializable]
public class DialogueChoice
{
    [Tooltip("Text shown on the choice button.")]
    public string label = "";

    [Tooltip("Value stored by DialogueService when this choice is picked.")]
    public string value = "";

    [Tooltip("End the current dialogue after this choice is picked instead of advancing to the next line.")]
    public bool endDialogue;
}

