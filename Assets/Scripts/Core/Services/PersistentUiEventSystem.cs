using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// Ensures a single <see cref="EventSystem"/> with <see cref="InputSystemUIInputModule"/>
/// wired to <see cref="InputSystem_Actions"/> (UI map). Lives under GameServices so it
/// persists with <see cref="GameServicesContainer"/> and survives scene loads that omit an EventSystem.
/// Duplicate scene EventSystem components (e.g. Bootstrap or a menu scene) are removed
/// after local modules exist and again at every scene-loaded boundary; runs at
/// order 100 so default scene EventSystems typically Awake first, then this
/// replaces them.
/// </summary>
[DefaultExecutionOrder(100)]
public class PersistentUiEventSystem : MonoBehaviour
{
    [Header("Input")]
    [Tooltip("Defaults to project InputSystem_Actions when unset.")]
    [SerializeField] private InputActionAsset inputActions;

    [Header("Behaviour")]
    [SerializeField] private bool destroyDuplicateSceneEventSystems = true;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs;

    // GameServices owns the persistent EventSystem, so it also owns the
    // lifetime of the shared UI action map. MenuInputContextController may
    // temporarily detach individual module actions while it routes menu
    // input itself, but no screen should be responsible for enabling the
    // global map as a side effect of opening.
    private InputActionMap configuredUiActionMap;

    private void Awake()
    {
        // A menu scene commonly contains an authored EventSystem so its UI is
        // editable in isolation. Disable those scene-local systems before
        // adding the persistent one; adding an enabled EventSystem while one
        // is already active logs Unity's "There can be only one active Event
        // System" error and can leave controllers holding a dead reference.
        DisableOtherEventSystemsBeforeCreation();

        if (GetComponent<EventSystem>() == null)
            gameObject.AddComponent<EventSystem>();

        if (GetComponent<InputSystemUIInputModule>() == null)
            gameObject.AddComponent<InputSystemUIInputModule>();

        ConfigureInputModule();

        if (destroyDuplicateSceneEventSystems)
            RemoveOtherEventSystems();

        // Scene EventSystems are created after this persistent service when a
        // new scene is loaded. Clean them up at the scene-loaded boundary too;
        // otherwise two InputSystemUIInputModules can both process the same
        // navigation/pointer input and repeatedly churn Selectable focus.
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void DisableOtherEventSystemsBeforeCreation()
    {
        if (!destroyDuplicateSceneEventSystems)
            return;

        EventSystem ours = GetComponent<EventSystem>();
        EventSystem[] all = FindObjectsByType<EventSystem>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            EventSystem es = all[i];
            if (es == null || es == ours)
                continue;

            // Disable the paired input module as well so it cannot consume a
            // navigation tick while the persistent owner is being installed.
            es.enabled = false;
            BaseInputModule[] duplicateModules = es.GetComponents<BaseInputModule>();
            for (int moduleIndex = 0; moduleIndex < duplicateModules.Length; moduleIndex++)
                duplicateModules[moduleIndex].enabled = false;
        }
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (destroyDuplicateSceneEventSystems)
            RemoveOtherEventSystems();

        EnsureAuthoritativeEventSystem();
    }

    private void EnsureAuthoritativeEventSystem()
    {
        EventSystem ours = GetComponent<EventSystem>();
        if (ours != null && !ours.enabled)
            ours.enabled = true;

        InputSystemUIInputModule uiModule = GetComponent<InputSystemUIInputModule>();
        if (uiModule != null && !uiModule.enabled)
            uiModule.enabled = true;

        EnsureUiActionMapEnabled();

        if (ours != null && EventSystem.current != ours)
            ours.enabled = true;
    }

    private void RemoveOtherEventSystems()
    {
        EventSystem ours = GetComponent<EventSystem>();
        if (ours == null)
            return;

        EventSystem[] all = FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            EventSystem es = all[i];
            if (es == null || es == ours)
                continue;

            if (enableDebugLogs)
                Debug.Log($"[PersistentUiEventSystem] Destroying duplicate EventSystem on '{es.gameObject.name}'.", es.gameObject);

            // Destroy is deferred until the end of the frame. Disable the
            // duplicate immediately so its modules cannot process one more
            // navigation/pointer tick while the scene is settling. Preserve
            // the GameObject because authored scenes sometimes co-locate
            // unrelated menu/input bridge components on their EventSystem.
            es.enabled = false;
            BaseInputModule[] duplicateModules = es.GetComponents<BaseInputModule>();
            for (int moduleIndex = 0; moduleIndex < duplicateModules.Length; moduleIndex++)
            {
                BaseInputModule duplicateModule = duplicateModules[moduleIndex];
                duplicateModule.enabled = false;
                Destroy(duplicateModule);
            }

            Destroy(es);
        }
    }

    private void ConfigureInputModule()
    {
        var uiModule = GetComponent<InputSystemUIInputModule>();
        if (uiModule == null)
            return;

        InputActionAsset asset = inputActions;
        if (asset == null)
            asset = Resources.Load<InputActionAsset>("InputSystem_Actions");

        configuredUiActionMap = asset != null
            ? asset.FindActionMap("UI", false)
            : null;
        EnsureUiActionMapEnabled();

        if (asset != null && TryConfigureUiModuleFromAsset(uiModule, asset))
        {
            if (enableDebugLogs)
                Debug.Log(
                    "[PersistentUiEventSystem] InputSystemUIInputModule wired from InputSystem_Actions (UI map). " +
                    $"UI action map enabled={configuredUiActionMap != null && configuredUiActionMap.enabled}.",
                    this);
            return;
        }

        uiModule.AssignDefaultActions();
        if (enableDebugLogs)
            Debug.LogWarning(
                "[PersistentUiEventSystem] Could not bind project UI actions; InputSystemUIInputModule.AssignDefaultActions() was used. " +
                "Assign InputSystem_Actions on this component for consistent UI bindings.",
                this);
    }

    private void EnsureUiActionMapEnabled()
    {
        if (configuredUiActionMap != null && !configuredUiActionMap.enabled)
            configuredUiActionMap.Enable();
    }

    private static bool TryConfigureUiModuleFromAsset(InputSystemUIInputModule uiModule, InputActionAsset asset)
    {
        if (uiModule == null || asset == null)
            return false;

        InputActionMap uiMap = asset.FindActionMap("UI", false);
        if (uiMap == null)
            return false;

        InputAction point = FindAction(uiMap, "Point");
        InputAction click = FindAction(uiMap, "Click", "LeftClick");
        InputAction scrollWheel = FindAction(uiMap, "ScrollWheel");
        InputAction move = FindAction(uiMap, "Navigate");
        InputAction submit = FindAction(uiMap, "Submit");
        InputAction cancel = FindAction(uiMap, "Cancel");

        if (point == null || click == null || move == null || submit == null || cancel == null)
            return false;

        uiModule.actionsAsset = asset;
        uiModule.point = InputActionReference.Create(point);
        uiModule.leftClick = InputActionReference.Create(click);
        if (scrollWheel != null)
            uiModule.scrollWheel = InputActionReference.Create(scrollWheel);
        uiModule.move = InputActionReference.Create(move);
        uiModule.submit = InputActionReference.Create(submit);
        uiModule.cancel = InputActionReference.Create(cancel);
        return true;
    }

    private static InputAction FindAction(InputActionMap map, params string[] names)
    {
        if (map == null || names == null)
            return null;

        for (int i = 0; i < names.Length; i++)
        {
            InputAction action = map.FindAction(names[i], false);
            if (action != null)
                return action;
        }

        return null;
    }
}
