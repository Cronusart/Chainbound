using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Service for managing starter chainling selection.
/// Keeps the world starter objects interactive while the sequence waits on confirmation.
/// </summary>
public class StarterSelectionService : MonoBehaviour
{
    private static StarterSelectionService instance;

    public static StarterSelectionService Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindFirstObjectByType<StarterSelectionService>(FindObjectsInactive.Include);
                if (instance == null)
                {
                    if (!Application.isPlaying)
                        return null;

                    GameObject go = new GameObject("StarterSelectionService");
                    instance = go.AddComponent<StarterSelectionService>();
                    DontDestroyOnLoad(go);
                }
            }
            return instance;
        }
    }

    [Header("UI")]
    [Tooltip("Authored StarterSelectionUI prefab instance. A runtime fallback is created only when this is missing or incomplete.")]
    [SerializeField] private StarterSelectionUI selectionUI;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    private readonly List<StarterOption> registeredOptions = new List<StarterOption>();
    private bool isSelecting;
    private bool ownsSelectionUi;
    private StarterOption selectedStarter;
    private StarterOption previewStarter;

    public event Action<StarterOption> OnStarterSelected;

    public bool IsSelecting => isSelecting;
    public StarterOption SelectedStarter => selectedStarter;
    public StarterOption PreviewStarterOption => previewStarter;
    public string SelectedChainlingId => selectedStarter != null ? selectedStarter.ChainlingId : "";
    public StarterSelectionUI SelectionUI => selectionUI;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Debug.LogWarning($"[StarterSelectionService] Duplicate instance found. Destroying {gameObject.name}");
            Destroy(gameObject);
            return;
        }

        instance = this;

        if (selectionUI == null)
            selectionUI = FindFirstObjectByType<StarterSelectionUI>(FindObjectsInactive.Include);

        ownsSelectionUi = false;
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    public void RegisterOption(StarterOption option)
    {
        if (option == null || registeredOptions.Contains(option))
            return;

        registeredOptions.Add(option);

        if (enableDebugLogs)
            Debug.Log($"[StarterSelectionService] Registered option: {option.ChainlingId}");
    }

    public void UnregisterOption(StarterOption option)
    {
        if (option == null)
            return;

        registeredOptions.Remove(option);
        if (previewStarter == option)
            previewStarter = null;
    }

    public bool StartSelection()
    {
        if (isSelecting)
        {
            Debug.LogWarning("[StarterSelectionService] Selection already in progress.");
            return false;
        }

        if (registeredOptions.Count == 0)
        {
            Debug.LogError("[StarterSelectionService] No starter options registered.");
            return false;
        }

        isSelecting = true;
        selectedStarter = null;
        previewStarter = null;

        EnsureSelectionUi();
        selectionUI?.PrepareSelection(registeredOptions);

        if (enableDebugLogs)
            Debug.Log($"[StarterSelectionService] Started selection with {registeredOptions.Count} option(s).");

        return true;
    }

    public bool PreviewStarter(StarterOption option)
    {
        if (!isSelecting || option == null)
            return false;

        if (!registeredOptions.Contains(option))
        {
            Debug.LogWarning($"[StarterSelectionService] Option {option.ChainlingId} not registered.");
            return false;
        }

        EnsureSelectionUi();
        previewStarter = option;
        selectionUI?.ShowOption(option);

        if (enableDebugLogs)
            Debug.Log($"[StarterSelectionService] Previewing starter: {option.ChainlingId}");

        return true;
    }

    public void SelectStarter(StarterOption option)
    {
        if (option == null)
        {
            Debug.LogWarning("[StarterSelectionService] Cannot select a null starter.");
            return;
        }

        if (!registeredOptions.Contains(option))
        {
            Debug.LogWarning($"[StarterSelectionService] Option {option.ChainlingId} not registered.");
            return;
        }

        selectedStarter = option;
        previewStarter = null;
        isSelecting = false;
        selectionUI?.EnterNamingPresentation();

        option.NotifyConfirmedSelection();
        OnStarterSelected?.Invoke(option);
        option.ApplyConfirmedWorldState();

        if (enableDebugLogs)
            Debug.Log($"[StarterSelectionService] Starter selected: {option.ChainlingId}");
    }

    public void CancelPreview(StarterOption option = null)
    {
        if (!isSelecting)
            return;

        if (option != null && previewStarter != option)
            return;

        previewStarter = null;
        selectionUI?.HideSelection();

        if (enableDebugLogs)
            Debug.Log("[StarterSelectionService] Preview cancelled.");
    }

    public bool HasSelection()
    {
        return selectedStarter != null;
    }

    public void ResetSelection()
    {
        selectedStarter = null;
        previewStarter = null;
        isSelecting = false;
        selectionUI?.HideSelection();
        ReleaseOwnedSelectionUiIfIdle();
    }

    /// <summary>
    /// Completes the post-selection presentation and only then releases a
    /// runtime-created UI. This keeps the selection screen alive for reusable
    /// naming, confirmation, or celebration steps.
    /// </summary>
    public IEnumerator DismissSelectionAnimated()
    {
        if (selectionUI != null)
            yield return selectionUI.DismissSelectionAnimated();

        ReleaseOwnedSelectionUiIfIdle();
    }

    private void EnsureSelectionUi()
    {
        if (selectionUI != null)
            return;

        selectionUI = FindFirstObjectByType<StarterSelectionUI>(FindObjectsInactive.Include);
        if (selectionUI != null)
        {
            ownsSelectionUi = false;
            return;
        }

        GameObject go = new GameObject("StarterSelectionUI");
        Transform parent = GameServicesContainer.Instance != null ? GameServicesContainer.Instance.transform : null;
        if (parent != null)
            go.transform.SetParent(parent, false);
        else
            DontDestroyOnLoad(go);

        selectionUI = go.AddComponent<StarterSelectionUI>();
        ownsSelectionUi = true;
    }

    private void ReleaseOwnedSelectionUiIfIdle()
    {
        if (!ownsSelectionUi || selectionUI == null || isSelecting || previewStarter != null)
            return;

        StarterSelectionUI uiToDestroy = selectionUI;
        selectionUI = null;
        ownsSelectionUi = false;
        Destroy(uiToDestroy.gameObject);
    }
}
