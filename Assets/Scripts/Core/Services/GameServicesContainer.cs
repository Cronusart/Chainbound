using UnityEngine;
using SignalJam.Narrative.Combat;
using SignalJam.UI.MobileControls;
using Chainbound.Combat;

/// <summary>
/// Container component for GameServices GameObject.
/// Marks the GameObject as DontDestroyOnLoad and prevents duplicates.
/// 
/// Attach this to your GameServices GameObject in the bootstrap scene.
/// All service components should be children of this GameObject.
/// </summary>
public class GameServicesContainer : MonoBehaviour
{
    private static GameServicesContainer instance;
    
    [Header("Debug")]
    [Tooltip("Enable debug logging for service validation")]
    [SerializeField] private bool enableDebugLogs = false;
    
    [Tooltip("Validate required services exist on Awake")]
    [SerializeField] private bool validateServicesOnAwake = true;
    
    private void Awake()
    {
        // Prevent duplicate GameServices containers
        if (instance != null && instance != this)
        {
            Debug.LogWarning($"[GameServicesContainer] Duplicate GameServices GameObject found. Destroying {gameObject.name}");
            Destroy(gameObject);
            return;
        }
        
        instance = this;
        
        // Mark as DontDestroyOnLoad
        DontDestroyOnLoad(gameObject);

        // Wallet authorization is a process-wide service. Keep the bootstrap
        // resilient while the prefab is being upgraded in older development builds.
        if (GetComponentInChildren<ProfileWalletAccessCoordinator>(true) == null)
            gameObject.AddComponent<ProfileWalletAccessCoordinator>();
        if (GetComponentInChildren<WalletOperationGuard>(true) == null)
            gameObject.AddComponent<WalletOperationGuard>();
        
        if (enableDebugLogs)
        {
            Debug.Log($"[GameServicesContainer] Initialized and marked as DontDestroyOnLoad: {gameObject.name}");
        }
        
        // Optional: Validate services exist
        if (validateServicesOnAwake)
        {
            ValidateServices();
        }
    }
    
    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
            
            if (enableDebugLogs)
            {
                Debug.Log("[GameServicesContainer] GameServices container destroyed");
            }
        }
    }
    
    /// <summary>
    /// Validate that expected services exist as children.
    /// This is a debug helper - services handle their own singleton patterns.
    /// </summary>
    private void ValidateServices()
    {
        if (!enableDebugLogs) return;
        
        bool foundAny = false;
        
        // Check for common services (non-exhaustive list)
        if (GetComponentInChildren<InputLockService>() != null)
        {
            foundAny = true;
            Debug.Log("[GameServicesContainer] âœ“ InputLockService found");
        }

        if (GetComponentInChildren<AppLifecycleService>(true) != null)
        {
            foundAny = true;
            Debug.Log("[GameServicesContainer] AppLifecycleService found");
        }
        
        if (GetComponentInChildren<DialogueService>() != null)
        {
            foundAny = true;
            Debug.Log("[GameServicesContainer] âœ“ DialogueService found");
        }
        
        if (GetComponentInChildren<DialoguePlayerFacingBridge>(true) != null)
        {
            foundAny = true;
            Debug.Log("[GameServicesContainer] DialoguePlayerFacingBridge found");
        }

        if (GetComponentInChildren<PlayerFacingOverrideService>(true) != null)
        {
            foundAny = true;
            Debug.Log("[GameServicesContainer] PlayerFacingOverrideService found");
        }

        if (GetComponentInChildren<CameraShotController>() != null)
        {
            foundAny = true;
            Debug.Log("[GameServicesContainer] âœ“ CameraShotController found");
        }
        
        if (GetComponentInChildren<CinematicOcclusionRevealService>(true) != null)
        {
            foundAny = true;
            Debug.Log("[GameServicesContainer] CinematicOcclusionRevealService found");
        }
        
        if (GetComponentInChildren<SceneLoader>() != null)
        {
            foundAny = true;
            Debug.Log("[GameServicesContainer] âœ“ SceneLoader found");
        }
        
        if (GetComponentInChildren<SpawnService>() != null)
        {
            foundAny = true;
            Debug.Log("[GameServicesContainer] SpawnService found");
        }

        if (GetComponentInChildren<SanctuaryStateService>(true) != null)
        {
            foundAny = true;
            Debug.Log("[GameServicesContainer] SanctuaryStateService found");
        }

        if (GetComponentInChildren<SanctuarySpawnDirector>(true) != null)
        {
            foundAny = true;
            Debug.Log("[GameServicesContainer] SanctuarySpawnDirector found");
        }
        
        if (GetComponentInChildren<TriggerRegistry>() != null)
        {
            foundAny = true;
            Debug.Log("[GameServicesContainer] âœ“ TriggerRegistry found");
        }
        
        if (GetComponentInChildren<SequenceRunner>() != null)
        {
            foundAny = true;
            Debug.Log("[GameServicesContainer] âœ“ SequenceRunner found");
        }
        
        if (GetComponentInChildren<LocalSaveServiceStub>() != null)
        {
            foundAny = true;
            Debug.Log("[GameServicesContainer] âœ“ LocalSaveServiceStub found");
        }
        
        if (GetComponentInChildren<NarrativeEncounterService>(true) != null)
        {
            foundAny = true;
            Debug.Log("[GameServicesContainer] âœ“ NarrativeEncounterService found");
        }

        if (GetComponentInChildren<StarterSelectionService>() != null)
        {
            foundAny = true;
            Debug.Log("[GameServicesContainer] âœ“ StarterSelectionService found");
        }
        
        if (GetComponentInChildren<QuestService>() != null)
        {
            foundAny = true;
            Debug.Log("[GameServicesContainer] âœ“ QuestService found");
        }

        if (GetComponentInChildren<NarrativeProfileStateCoordinator>(true) != null)
        {
            foundAny = true;
            Debug.Log("[GameServicesContainer] NarrativeProfileStateCoordinator found");
        }
        
        if (GetComponentInChildren<AudioService>() != null)
        {
            foundAny = true;
            Debug.Log("[GameServicesContainer] âœ“ AudioService found");
        }

        if (GetComponentInChildren<MusicConductor>() != null)
        {
            foundAny = true;
            Debug.Log("[GameServicesContainer] âœ“ MusicConductor found");
        }
        
        if (GetComponentInChildren<TimeOfDayManager>() != null)
        {
            foundAny = true;
            Debug.Log("[GameServicesContainer] âœ“ TimeOfDayManager found");
        }

        // â”€â”€ Solana / Blockchain Services â”€â”€
        if (GetComponentInChildren<Chainbound.Combat.SolanaServiceBridge>() != null)
        {
            foundAny = true;
            Debug.Log("[GameServicesContainer] âœ“ SolanaServiceBridge found (Wallet + RPC + Profile + Battle + Capture + Move services)");
        }
        else if (enableDebugLogs)
        {
            Debug.LogWarning("[GameServicesContainer] SolanaServiceBridge NOT found. Blockchain features will be unavailable.");
        }

        if (GetComponentInChildren<Chainbound.Combat.EncounterModifierService>(true) != null)
        {
            foundAny = true;
            Debug.Log("[GameServicesContainer] EncounterModifierService found (encounter + reward modifier aggregation)");
        }
        else if (enableDebugLogs)
        {
            Debug.LogWarning("[GameServicesContainer] EncounterModifierService NOT found. Local and server-wide encounter events will not apply.");
        }

        if (GetComponentInChildren<Chainbound.Combat.SupabaseEncounterEventsProvider>(true) != null)
        {
            foundAny = true;
            Debug.Log("[GameServicesContainer] SupabaseEncounterEventsProvider found (server-wide encounter events)");
        }

        if (GetComponentInChildren<Chainbound.Combat.AuthoritativeCombatSessionService>(true) != null)
        {
            foundAny = true;
            Debug.Log("[GameServicesContainer] AuthoritativeCombatSessionService found (PvE session + capture authority)");
        }
        else if (enableDebugLogs)
        {
            Debug.LogWarning("[GameServicesContainer] AuthoritativeCombatSessionService NOT found. Production capture starts will be blocked when the rollout flag is enabled.");
        }

        if (GetComponentInChildren<Chainbound.Combat.BalanceConfigService>(true) != null)
        {
            foundAny = true;
            Debug.Log("[GameServicesContainer] BalanceConfigService found (including capture balance/presentation boards)");
        }
        else if (enableDebugLogs)
        {
            Debug.LogWarning("[GameServicesContainer] SupabaseEncounterEventsProvider NOT found. Only local encounter modifiers will apply.");
        }

        if (GetComponentInChildren<Chainbound.Combat.FavorService>(true) != null)
        {
            foundAny = true;
            Debug.Log("[GameServicesContainer] FavorService found");
        }
        else if (enableDebugLogs)
        {
            Debug.LogWarning("[GameServicesContainer] FavorService NOT found. Favor will fall back to runtime defaults with zero score.");
        }
        
        if (GetComponentInChildren<NPCInteractionSessionService>(true) != null)
        {
            foundAny = true;
            Debug.Log("[GameServicesContainer] NPCInteractionSessionService found");
        }
        
        if (GetComponentInChildren<OnScreenKeyboardService>(true) != null)
        {
            foundAny = true;
            Debug.Log("[GameServicesContainer] OnScreenKeyboardService found");
        }

        if (GetComponentInChildren<UiHapticsService>(true) != null)
        {
            foundAny = true;
            Debug.Log("[GameServicesContainer] UiHapticsService found");
        }

        // Mobile HUD stack (mobile + PSG1 on-screen controls)
        if (GetComponentInChildren<MobileHudService>(true) != null)
        {
            foundAny = true;
            Debug.Log("[GameServicesContainer] MobileHudService found");
        }
        else if (enableDebugLogs)
        {
            Debug.LogWarning("[GameServicesContainer] MobileHudService NOT found. Mobile HUD context tokens will spawn a standalone host via Instance lookup.");
        }

        if (GetComponentInChildren<MobileInputManager>(true) != null)
        {
            foundAny = true;
            Debug.Log("[GameServicesContainer] MobileInputManager found");
        }
        else if (enableDebugLogs)
        {
            Debug.LogWarning("[GameServicesContainer] MobileInputManager NOT found. On-screen mobile controls will be inactive.");
        }

        if (!foundAny && enableDebugLogs)
        {
            Debug.LogWarning("[GameServicesContainer] No services found as children. Ensure services are attached to this GameObject or its children.");
        }
    }
    
    /// <summary>
    /// Get the singleton instance (for external validation if needed).
    /// </summary>
    public static GameServicesContainer Instance => instance;
}

