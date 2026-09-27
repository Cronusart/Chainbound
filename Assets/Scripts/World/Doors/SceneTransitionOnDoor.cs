using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Stub component for scene transitions triggered by door opening.
/// Listens to door open event and invokes transition event.
/// Actual scene loading/fade logic can be connected via UnityEvent.
/// </summary>
public class SceneTransitionOnDoor : MonoBehaviour
{
    [Header("Door Reference")]
    [Tooltip("Door controller to listen to")]
    [SerializeField] private DoorController door;
    
    [Header("Transition Settings")]
    [Tooltip("Target scene name")]
    [SerializeField] private string targetSceneName = "";
    
    [Tooltip("Spawn point ID in target scene")]
    [SerializeField] private string spawnId = "";
    
    [Tooltip("Loading transition style")]
    [SerializeField] private TransitionStyle transitionStyle = TransitionStyle.QuickFade;
    
    [Header("Events")]
    [Tooltip("Invoked when door opens (connect scene loader/fade here if not using auto-transition)")]
    [SerializeField] private UnityEvent OnTransitionRequested;
    
    [Tooltip("Automatically trigger scene transition when door opens")]
    [SerializeField] private bool autoTransition = true;
    
    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    private void OnEnable()
    {
        if (door != null)
        {
            door.OnDoorOpened.AddListener(OnDoorOpened);
        }
        else
        {
            Debug.LogWarning($"[SceneTransitionOnDoor] Door not assigned on {gameObject.name}. Scene transition will not work.");
        }
    }

    private void OnDisable()
    {
        if (door != null)
        {
            door.OnDoorOpened.RemoveListener(OnDoorOpened);
        }
    }

    private void OnDoorOpened()
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[SceneTransitionOnDoor] Door opened on {door.gameObject.name}. Transition to '{targetSceneName}' requested.");
        }

        // Auto-transition if enabled
        if (autoTransition && !string.IsNullOrEmpty(targetSceneName))
        {
            SceneLoader sceneLoader = SceneLoader.Instance;
            if (sceneLoader != null)
            {
                sceneLoader.LoadScene(targetSceneName, transitionStyle, spawnId);
            }
            else
            {
                Debug.LogError("[SceneTransitionOnDoor] SceneLoader not found!");
            }
        }
        
        // Invoke event - connect scene loader/fade system here if not using auto-transition
        OnTransitionRequested?.Invoke();
    }
}

