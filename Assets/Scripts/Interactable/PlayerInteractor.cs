using SignalJam.UI.MobileControls;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Routes Interact input to the nearest IInteractable in range.
/// Attach to the player or a persistent GameObject. Requires InteractableRegistry.
/// Respects InputLockService.IsInteractionLocked.
/// </summary>
public class PlayerInteractor : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private string actionMapName = "Player";
    [SerializeField] private string interactActionName = "Interact";

    [Header("Player")]
    [Tooltip("Player transform for registry. Auto-finds by tag if null.")]
    [SerializeField] private Transform playerTransform;
    [SerializeField] private MobileInputManager mobileInputManager;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    private InputAction interactAction;

    private void Awake()
    {
        if (inputActions == null)
            inputActions = Resources.Load<InputActionAsset>("InputSystem_Actions");

        if (inputActions != null)
        {
            var map = inputActions.FindActionMap(actionMapName);
            if (map != null)
                interactAction = map.FindAction(interactActionName);
        }

        if (playerTransform == null)
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                playerTransform = player.transform;
                InteractableRegistry.Instance?.SetPlayerTransform(playerTransform);
            }
        }

        if (mobileInputManager == null)
            mobileInputManager = FindObjectOfType<MobileInputManager>(true);
    }

    private void OnEnable()
    {
        if (interactAction != null)
        {
            interactAction.Enable();
            interactAction.performed += OnInteractPressed;
        }

        if (mobileInputManager != null)
            mobileInputManager.OnAPressed += OnMobileInteractPressed;
    }

    private void OnDisable()
    {
        if (interactAction != null)
        {
            interactAction.performed -= OnInteractPressed;
            // Interact is a shared project action used by dialogue advance.
            // Unsubscribe only; disabling it here kills hardware confirm in other systems.
        }

        if (mobileInputManager != null)
            mobileInputManager.OnAPressed -= OnMobileInteractPressed;
    }

    private void OnInteractPressed(InputAction.CallbackContext ctx)
    {
        if (!ctx.performed) return;

        TryInteract();
    }

    private void OnMobileInteractPressed()
    {
        TryInteract();
    }

    private void TryInteract()
    {
        if (InputLockService.Instance != null && InputLockService.Instance.IsInteractionLocked)
        {
            if (enableDebugLogs)
            {
                string reasons = string.Join(", ", InputLockService.Instance.GetActiveLockReasons());
                Debug.Log($"[PlayerInteractor] Interact blocked by InputLockService ({reasons})");
            }
            return;
        }

        var registry = InteractableRegistry.Instance;
        if (registry == null) return;

        var nearest = registry.GetNearest();
        if (nearest != null)
        {
            if (enableDebugLogs)
                Debug.Log($"[PlayerInteractor] Interact -> {nearest.Transform?.name ?? "?"}");
            nearest.Interact();
        }
    }
}
