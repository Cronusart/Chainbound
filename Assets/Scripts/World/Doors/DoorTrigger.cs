using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Triggers door opening/closing based on player proximity and input.
/// Supports auto-open on enter or require interact input.
/// </summary>
[RequireComponent(typeof(Collider))]
public class DoorTrigger : MonoBehaviour
{
    public enum DoorTriggerMode { AutoOnEnter, RequireInteract }

    [Header("Door Reference")]
    [Tooltip("Door controller to trigger")]
    [SerializeField] private DoorController doorController;
    
    [Header("Trigger Settings")]
    [Tooltip("How the door should be triggered")]
    [SerializeField] private DoorTriggerMode triggerMode = DoorTriggerMode.AutoOnEnter;
    [Tooltip("Automatically close door when player exits")]
    [SerializeField] private bool autoCloseOnExit = true;
    [Tooltip("Delay before auto-closing (seconds)")]
    [SerializeField] private float autoCloseDelay = 1f;
    
    [Header("Input")]
    [Tooltip("Input Action Asset (for Interact action)")]
    [SerializeField] private InputActionAsset inputActions;
    [Tooltip("Action map name (default: Player)")]
    [SerializeField] private string actionMapName = "Player";
    [Tooltip("Interact action name (default: Interact)")]
    [SerializeField] private string interactActionName = "Interact";
    
    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    private Collider triggerCollider;
    private bool playerInRange = false;
    private InputActionMap gameplayMap;
    private InputAction interactAction;
    private Coroutine autoCloseCoroutine;

    private void Awake()
    {
        triggerCollider = GetComponent<Collider>();
        if (triggerCollider != null)
        {
            triggerCollider.isTrigger = true;
        }

        // Auto-find door controller if not assigned
        if (doorController == null)
        {
            doorController = GetComponentInParent<DoorController>();
            if (doorController == null)
            {
                doorController = FindObjectOfType<DoorController>();
            }
        }

        // Setup input if using interact mode
        if (triggerMode == DoorTriggerMode.RequireInteract)
        {
            SetupInput();
        }
    }

    private void SetupInput()
    {
        if (inputActions == null)
        {
            inputActions = Resources.Load<InputActionAsset>("InputSystem_Actions");
            if (inputActions == null)
            {
                Debug.LogWarning($"[DoorTrigger] InputActionAsset not found! Interact mode will not work. Please assign InputActions or place InputSystem_Actions in Resources folder.");
                return;
            }
        }

        gameplayMap = inputActions.FindActionMap(actionMapName);
        if (gameplayMap == null)
        {
            Debug.LogWarning($"[DoorTrigger] Action map '{actionMapName}' not found! Interact mode will not work.");
            return;
        }

        interactAction = gameplayMap.FindAction(interactActionName);
        if (interactAction == null)
        {
            Debug.LogWarning($"[DoorTrigger] Action '{interactActionName}' not found! Interact mode will not work.");
            return;
        }
    }

    private void OnEnable()
    {
        if (triggerMode == DoorTriggerMode.RequireInteract && interactAction != null)
        {
            interactAction.Enable();
            interactAction.performed += OnInteractPressed;
        }
    }

    private void OnDisable()
    {
        if (interactAction != null)
        {
            interactAction.performed -= OnInteractPressed;
            interactAction.Disable();
        }

        // Cancel auto-close coroutine if active
        if (autoCloseCoroutine != null)
        {
            StopCoroutine(autoCloseCoroutine);
            autoCloseCoroutine = null;
        }
    }

    private void Update()
    {
        // Check for interact input if player is in range and mode requires it
        if (triggerMode == DoorTriggerMode.RequireInteract && playerInRange)
        {
            // Input is handled via event callback, but we can also check here if needed
            // (already handled in OnInteractPressed)
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        if (doorController == null) return;

        playerInRange = true;

        if (enableDebugLogs)
            Debug.Log($"[DoorTrigger] Player entered trigger on {gameObject.name}");

        // Cancel any pending auto-close
        if (autoCloseCoroutine != null)
        {
            StopCoroutine(autoCloseCoroutine);
            autoCloseCoroutine = null;
        }

        // Auto-open if mode is AutoOnEnter
        if (triggerMode == DoorTriggerMode.AutoOnEnter)
        {
            doorController.Open();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        if (doorController == null) return;

        playerInRange = false;

        if (enableDebugLogs)
            Debug.Log($"[DoorTrigger] Player exited trigger on {gameObject.name}");

        // Auto-close if enabled
        if (autoCloseOnExit && triggerMode == DoorTriggerMode.AutoOnEnter)
        {
            if (autoCloseDelay > 0f)
            {
                autoCloseCoroutine = StartCoroutine(AutoCloseDelayed());
            }
            else
            {
                doorController.Close();
            }
        }
    }

    private void OnInteractPressed(InputAction.CallbackContext context)
    {
        if (!context.performed) return;
        if (!playerInRange) return;
        if (doorController == null) return;

        if (enableDebugLogs)
            Debug.Log($"[DoorTrigger] Interact pressed while player in range on {gameObject.name}");

        // Toggle door state
        doorController.Toggle();
    }

    private IEnumerator AutoCloseDelayed()
    {
        yield return new WaitForSeconds(autoCloseDelay);
        
        if (doorController != null && !playerInRange)
        {
            doorController.Close();
        }
        
        autoCloseCoroutine = null;
    }
}

