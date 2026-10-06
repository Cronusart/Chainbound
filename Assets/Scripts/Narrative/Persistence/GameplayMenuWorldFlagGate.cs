using System;
using SignalJam.UI.MenuNavigation;
using UnityEngine;

/// <summary>
/// Keeps the player menu suppressed until Reynard's check-in sets
/// <see cref="MenuUnlockFlagKey"/>. Saves already past that beat stay open
/// when <see cref="PastCheckInUnlockFlagKey"/> is set. The house Chainling PC
/// keeps its own flags.
/// </summary>
[DisallowMultipleComponent]
public sealed class GameplayMenuWorldFlagGate : MonoBehaviour
{
    public const string MenuUnlockFlagKey = "world.player.menu";
    public const string PastCheckInUnlockFlagKey = "world.coffee.zeb_talked";

    private const string SuppressionReason = "chainling_pc_locked";

    [SerializeField] private string unlockFlagKey = MenuUnlockFlagKey;
    [SerializeField] private string legacyUnlockFlagKey = PastCheckInUnlockFlagKey;

    private static GameplayMenuWorldFlagGate instance;

    private ProfileWorldFlagStateService flags;
    private GameplayMenuAvailabilityToken suppression;
    private bool hasSuppression;

    public static GameplayMenuWorldFlagGate Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<GameplayMenuWorldFlagGate>();
                if (instance == null)
                {
                    GameServicesContainer services = GameServicesContainer.Instance;
                    var host = new GameObject("GameplayMenuWorldFlagGate");
                    if (services != null)
                        host.transform.SetParent(services.transform, false);
                    instance = host.AddComponent<GameplayMenuWorldFlagGate>();
                    if (services == null && Application.isPlaying)
                        DontDestroyOnLoad(host);
                }
            }

            return instance;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsurePlayModeHost()
    {
        if (!Application.isPlaying)
            return;

        _ = Instance;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        Subscribe();
        Apply();
    }

    private void OnEnable()
    {
        Subscribe();
        Apply();
    }

    private void OnDisable()
    {
        Unsubscribe();
        ReleaseSuppression();
    }

    private void OnDestroy()
    {
        Unsubscribe();
        ReleaseSuppression();
        if (instance == this)
            instance = null;
    }

    private void Subscribe()
    {
        flags = ProfileWorldFlagStateService.Instance;
        if (flags == null)
            return;

        flags.OnReadyChanged -= HandleReadyChanged;
        flags.OnFlagChanged -= HandleFlagChanged;
        flags.OnReadyChanged += HandleReadyChanged;
        flags.OnFlagChanged += HandleFlagChanged;
    }

    private void Unsubscribe()
    {
        if (flags == null)
            return;

        flags.OnReadyChanged -= HandleReadyChanged;
        flags.OnFlagChanged -= HandleFlagChanged;
    }

    private void HandleReadyChanged(bool ready)
    {
        Apply();
    }

    private void HandleFlagChanged(string changedKey, bool value)
    {
        if (MatchesUnlockKey(changedKey))
            Apply();
    }

    private void Apply()
    {
        if (IsMenuUnlocked())
            ReleaseSuppression();
        else
            AcquireSuppression();
    }

    private bool IsMenuUnlocked()
    {
        if (flags == null || !flags.IsHydrated)
            return false;

        return flags.GetFlag(unlockFlagKey) || flags.GetFlag(legacyUnlockFlagKey);
    }

    private bool MatchesUnlockKey(string changedKey)
    {
        return string.Equals(changedKey, unlockFlagKey, StringComparison.Ordinal) ||
               string.Equals(changedKey, legacyUnlockFlagKey, StringComparison.Ordinal);
    }

    private void AcquireSuppression()
    {
        if (hasSuppression)
            return;

        GameplayMenuAvailabilityService service = GameplayMenuAvailabilityService.Instance;
        if (service == null)
            return;

        suppression = service.AcquireSuppression(SuppressionReason);
        hasSuppression = suppression.IsValid;
    }

    private void ReleaseSuppression()
    {
        if (!hasSuppression)
            return;

        suppression.Release();
        suppression = default;
        hasSuppression = false;
    }
}
