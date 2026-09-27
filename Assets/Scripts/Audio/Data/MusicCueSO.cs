using UnityEngine;

/// <summary>
/// ScriptableObject defining a 2-layer music cue.
/// Contains base and layer stems that play sample-accurately synchronized.
/// </summary>
[CreateAssetMenu(fileName = "MusicCue", menuName = "Chainbound/Audio/Music/Music Cue")]
public class MusicCueSO : ScriptableObject
{
    [Header("Identification")]
    [Tooltip("Unique ID for this music cue (used for lookup and debugging)")]
    [SerializeField] private string id = "";
    
    [Header("Audio Stems")]
    [Tooltip("Base music stem (always plays)")]
    [SerializeField] private AudioClip baseStem;
    
    [Tooltip("Layer music stem (fades in/out based on tokens)")]
    [SerializeField] private AudioClip layerStem;
    
    [Header("Playback")]
    [Tooltip("Loop both stems")]
    [SerializeField] private bool loop = true;
    
    [Header("Volume Levels (dB)")]
    [Tooltip("Base stem volume in decibels")]
    [SerializeField] private float baseVolDb = 0f;
    
    [Tooltip("Layer stem volume when active (decibels)")]
    [SerializeField] private float layerVolDbActive = 0f;
    
    [Tooltip("Layer stem volume when inactive (decibels, typically -80)")]
    [SerializeField] private float layerVolDbInactive = -80f;
    
    [Header("Transitions")]
    [Tooltip("Crossfade duration for layer transitions (seconds)")]
    [Range(0f, 5f)]
    [SerializeField] private float crossfadeSeconds = 0.5f;
    
    // ============================================
    // PUBLIC ACCESSORS
    // ============================================
    
    public string Id => id;
    public AudioClip BaseStem => baseStem;
    public AudioClip LayerStem => layerStem;
    public bool Loop => loop;
    public float BaseVolDb => baseVolDb;
    public float LayerVolDbActive => layerVolDbActive;
    public float LayerVolDbInactive => layerVolDbInactive;
    public float CrossfadeSeconds => crossfadeSeconds;
    
    private void OnValidate()
    {
        crossfadeSeconds = Mathf.Max(0f, crossfadeSeconds);
        
        // Validate stems have same length for sync safety
        if (baseStem != null && layerStem != null)
        {
            float baseLength = baseStem.length;
            float layerLength = layerStem.length;
            float diff = Mathf.Abs(baseLength - layerLength);
            
            if (diff > 0.01f) // Allow 10ms tolerance
            {
                Debug.LogWarning($"[MusicCueSO] '{name}': Base stem ({baseLength:F3}s) and layer stem ({layerLength:F3}s) have different lengths. " +
                               $"This may cause sync issues. Difference: {diff:F3}s");
            }
        }
    }
}
