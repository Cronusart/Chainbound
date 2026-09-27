using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Consumes GameSessionContext.PendingSequence on scene load and starts the sequence.
///
/// Setup: Add this component to the GameServices GameObject (same as GameServicesContainer).
/// GameServices is DontDestroyOnLoad, so this persists and runs when any scene loads.
///
/// Flow: Main menu sets PendingSequence when routing to intro path → scene loads →
/// this component starts the sequence via SequenceDirector, then clears PendingSequence.
/// </summary>
public class PendingSequenceStarter : MonoBehaviour
{
    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (GameSessionContext.Instance == null) return;

        ScriptableObject pending = GameSessionContext.Instance.PendingSequence;
        if (pending == null || !(pending is SequenceAsset sequenceAsset)) return;

        GameSessionContext.Instance.ClearPendingSequence();

        if (SequenceDirector.Instance != null)
        {
            if (enableDebugLogs)
                Debug.Log($"[PendingSequenceStarter] Starting sequence: {sequenceAsset.name} (scene: {scene.name})");

            SequenceDirector.Instance.Play(sequenceAsset);
        }
        else
        {
            Debug.LogWarning("[PendingSequenceStarter] SequenceDirector not found! Cannot start pending sequence.");
        }
    }
}
