using Chainbound.Combat;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Debug overlay to skip main menu flow and jump directly to chainbound_intro sequence.
/// Mirrors the main menu Play flow: sets GameSessionContext, then starts the intro sequence.
/// The sequence itself loads player_room (via intro_loadfirstscene step). Only active in Editor or Development builds.
///
/// Optional: skip straight to loom_lab at a sequence label (e.g. choosestarter) via <see cref="SequenceRunner.StartSequenceAtLabel"/>.
///
/// Setup: Add to any GameObject in the main menu scene (e.g. MainMenuSequenceController).
/// Assign the intro sequence asset in the Inspector.
/// </summary>
public class DebugMenuSkipHarness : MonoBehaviour
{
#if UNITY_EDITOR || DEVELOPMENT_BUILD

    [Header("Skip Target")]
    [Tooltip("Intro sequence to start (e.g. chainbound_intro_sequence). The sequence loads player_room itself.")]
    [SerializeField] private SequenceAsset introSequence;

    [Tooltip("Quest definition to auto-start when skipping (e.g. Chainbound_Intro). Ensures SceneTransitionTrigger blocking works even if sequence timing differs.")]
    [SerializeField] private QuestDefinitionSO introQuestDefinition;

    [Tooltip("When true, starts the intro quest immediately before the sequence. Ensures chainbound_intro is active when player_room loads, so ExitDoor's SceneTransitionTrigger stays disabled.")]
    [SerializeField] private bool autoStartQuest = true;

    [Header("Skip to loom_lab (mid sequence)")]
    [Tooltip("Scene name for the lab (must be in Build Settings). Loaded before starting the sequence at ResumeAtLabel.")]
    [SerializeField] private string loomLabSceneName = "loom_lab";

    [Tooltip("Step_Label name in chainbound_intro_sequence to start at. choosestarter is immediately before starter select; waitfortoby is the older Toby-interact gate.")]
    [SerializeField] private string resumeAtLabel = "choosestarter";

    [SerializeField] private string debugPlayerName = "DEBUG";

    [Tooltip("If true, clears prof_toby_event on TriggerRegistry before starting so Step_WaitForEvent does not complete instantly.")]
    [SerializeField] private bool resetProfTobyTrigger = true;

    [Header("Combat Test")]
    [Tooltip("Scene to load when skipping to combat harness (e.g. Combat2_TestHarness). Must be in Build Settings.")]
    [SerializeField] private string combatTestSceneName = "Combat2_TestHarness";

    [Header("Overlay")]
    [SerializeField] private bool showOverlay = true;
    [SerializeField] private KeyCode toggleKey = KeyCode.F9;
    [SerializeField] private float overlayWidth = 220f;
    [SerializeField] private float overlayHeight = 170f;

    private bool _overlayVisible = true;

    // Static one-shot: harness GameObject is destroyed when main menu unloads before loom_lab finishes loading.
    private static string s_pendingLoomSceneName;
    private static SequenceAsset s_pendingIntroSequence;
    private static string s_pendingResumeLabel;
    private static bool s_pendingResetProfTobyTrigger;
    private static string s_pendingDebugPlayerName;

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
            _overlayVisible = !_overlayVisible;
    }

    private void OnGUI()
    {
        if (!showOverlay || !_overlayVisible) return;

        float x = Screen.width - overlayWidth - 10f;
        float y = 10f;
        float w = overlayWidth;
        float h = overlayHeight;

        GUILayout.BeginArea(new Rect(x, y, w, h));
        GUI.Box(new Rect(0, 0, w, h), "");
        GUILayout.BeginVertical();

        GUILayout.Label("Debug Skip (F9 to hide)");
        if (GUILayout.Button("Skip to chainbound_intro"))
        {
            SkipToChainboundIntro();
        }
        if (GUILayout.Button("Skip to loom_lab (starter)"))
        {
            SkipToLoomLabAtLabel();
        }
        if (GUILayout.Button("Skip to Combat2 TestHarness"))
        {
            SkipToCombatTestHarness();
        }

        GUILayout.EndVertical();
        GUILayout.EndArea();
    }

    private void SkipToChainboundIntro()
    {
        if (introSequence == null)
        {
            Debug.LogWarning("[DebugMenuSkipHarness] No intro sequence assigned! Assign chainbound_intro_sequence in Inspector.");
            return;
        }

        ApplyDebugSessionAndQuest();

        // Start the intro sequence in the current scene (main menu). The sequence's intro_loadfirstscene
        // step will load player_room - same flow as the normal Play button.
        if (SequenceDirector.Instance != null)
        {
            SequenceDirector.Instance.Play(introSequence);
        }
        else
        {
            Debug.LogError("[DebugMenuSkipHarness] SequenceDirector not found! Ensure GameServices/SequenceDirector exists.");
        }
    }

    /// <summary>
    /// Seeds session + quest like a normal intro skip, loads loom_lab, then starts chainbound_intro at <see cref="resumeAtLabel"/>
    /// without running earlier sequence steps.
    /// </summary>
    private void SkipToLoomLabAtLabel()
    {
        if (introSequence == null)
        {
            Debug.LogWarning("[DebugMenuSkipHarness] No intro sequence assigned! Assign chainbound_intro_sequence in Inspector.");
            return;
        }

        if (string.IsNullOrWhiteSpace(loomLabSceneName))
        {
            Debug.LogWarning("[DebugMenuSkipHarness] Loom lab scene name is empty.");
            return;
        }

        if (string.IsNullOrWhiteSpace(resumeAtLabel))
        {
            Debug.LogWarning("[DebugMenuSkipHarness] Resume label is empty.");
            return;
        }

        ApplyDebugSessionAndQuest();
        AbortOnboardingPresentation();

        s_pendingLoomSceneName = loomLabSceneName;
        s_pendingIntroSequence = introSequence;
        s_pendingResumeLabel = resumeAtLabel.Trim();
        s_pendingResetProfTobyTrigger = resetProfTobyTrigger;
        s_pendingDebugPlayerName = string.IsNullOrWhiteSpace(debugPlayerName) ? "DEBUG" : debugPlayerName.Trim();
        Debug.Log($"[DebugMenuSkipHarness] Loading '{loomLabSceneName}', will start '{introSequence.name}' at label '{resumeAtLabel}'.");
        SceneLoader loader = SceneLoader.Instance;
        bool accepted = loader != null && loader.RequestTransition(new SceneTransitionRequest(
            loomLabSceneName,
            sourceId: "DebugMenuSkipHarness.LoomLab",
            beforeReveal: context => OnLoomLabLoadedStatic(context.Scene, LoadSceneMode.Single),
            completed: result =>
            {
                if (!result.Succeeded)
                    ClearLoomSkipStatics();
            }));
        if (!accepted)
        {
            Debug.LogError("[DebugMenuSkipHarness] SceneLoader rejected the loom_lab debug launch.");
            ClearLoomSkipStatics();
        }
    }

    private static void OnLoomLabLoadedStatic(Scene scene, LoadSceneMode mode)
    {
        if (s_pendingIntroSequence == null || string.IsNullOrEmpty(s_pendingLoomSceneName))
        {
            ClearLoomSkipStatics();
            return;
        }

        if (!string.Equals(scene.name, s_pendingLoomSceneName, System.StringComparison.Ordinal))
            return;

        AbortOnboardingPresentation();

        if (s_pendingResetProfTobyTrigger && TriggerRegistry.Instance != null)
            TriggerRegistry.Instance.ResetTrigger("prof_toby_event");

        if (SequenceDirector.Instance == null)
        {
            Debug.LogError("[DebugMenuSkipHarness] SequenceDirector not found after loading loom_lab.");
            ClearLoomSkipStatics();
            return;
        }

        string label = s_pendingResumeLabel;
        SequenceAsset seq = s_pendingIntroSequence;
        string playerName = s_pendingDebugPlayerName;
        ClearLoomSkipStatics();

        // Starter select owns its own UI; do not take a director input lock.
        var playParams = new SequenceParameters { acquireInputLock = false };
        SequenceDirector.Instance.PlayFromLabel(seq, label, playParams);
        SeedDebugPlayerName(playerName);
        Debug.Log($"[DebugMenuSkipHarness] Started sequence at label '{label}' (no director input lock).");
    }

    private static void AbortOnboardingPresentation()
    {
        if (SequenceDirector.Instance != null && SequenceDirector.Instance.IsPlaying)
            SequenceDirector.Instance.StopCurrent();

        SpriteCinematicPlayer.Instance?.AbortPlayback();

        NameInputUI nameInput = Object.FindFirstObjectByType<NameInputUI>(FindObjectsInactive.Include);
        if (nameInput != null)
        {
            nameInput.Hide();
            nameInput.gameObject.SetActive(false);
        }

        if (OnScreenKeyboardService.Instance != null && OnScreenKeyboardService.Instance.IsOpen)
            OnScreenKeyboardService.Instance.EndSession();
    }

    private static void SeedDebugPlayerName(string playerName)
    {
        if (string.IsNullOrWhiteSpace(playerName))
            playerName = "DEBUG";

        GameSessionContext.Instance?.SetDisplayName(playerName);
        if (SequenceRunner.Instance != null && SequenceRunner.Instance.Context != null)
            SequenceRunner.Instance.Context.PlayerName = playerName;
    }

    private static void ClearLoomSkipStatics()
    {
        s_pendingLoomSceneName = null;
        s_pendingIntroSequence = null;
        s_pendingResumeLabel = null;
        s_pendingDebugPlayerName = null;
    }

    private void ApplyDebugSessionAndQuest()
    {
        if (GameSessionContext.Instance == null)
        {
            var go = new GameObject("[GameSessionContext]");
            go.AddComponent<GameSessionContext>();
        }

        GameSessionContext.Instance.SetFromProfile(
            walletAddress: "DEBUG",
            isNewPlayer: true,
            introComplete: false,
            introCinematicComplete: false,
            desiredSpawnTag: "IntroSpawn",
            pendingQuestId: "chainbound_intro",
            isOfflineSession: true);
        GameSessionContext.Instance.SetDisplayName(
            string.IsNullOrWhiteSpace(debugPlayerName) ? "DEBUG" : debugPlayerName.Trim());

        if (autoStartQuest && introQuestDefinition != null)
        {
            QuestService questService = QuestService.Instance;
            if (questService != null)
            {
                questService.StartQuest(introQuestDefinition.QuestId, introQuestDefinition);
                Debug.Log($"[DebugMenuSkipHarness] Auto-started quest: {introQuestDefinition.QuestId}");
            }
            else
            {
                Debug.LogWarning("[DebugMenuSkipHarness] QuestService not found.");
            }
        }
    }

    private void SkipToCombatTestHarness()
    {
        if (string.IsNullOrEmpty(combatTestSceneName))
        {
            Debug.LogWarning("[DebugMenuSkipHarness] Combat test scene name is empty! Assign in Inspector.");
            return;
        }

        Debug.Log($"[DebugMenuSkipHarness] Loading combat test scene: {combatTestSceneName}");
        SceneLoader loader = SceneLoader.Instance;
        if (loader == null || !loader.RequestTransition(new SceneTransitionRequest(
                combatTestSceneName,
                styleOverride: TransitionStyle.FullLoading,
                sourceId: "DebugMenuSkipHarness.Combat")))
        {
            Debug.LogError("[DebugMenuSkipHarness] SceneLoader rejected the combat debug launch.");
        }
    }

#endif
}
