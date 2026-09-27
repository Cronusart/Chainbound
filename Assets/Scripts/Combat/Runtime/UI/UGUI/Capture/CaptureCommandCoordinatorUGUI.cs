using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Chainbound.Combat.Core;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Chainbound.Combat
{
    /// <summary>
    /// Per-slot capture flow: queue a Capture action after target/Orb pick, then run the
    /// trading-trace challenge only if the resolver still finds the recorder and target eligible.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CaptureCommandCoordinatorUGUI : MonoBehaviour
    {
        [Header("Services")]
        [SerializeField] private AuthoritativeCombatSessionService authority;
        [SerializeField] private MonoBehaviour inventoryServiceBehaviour;
        [SerializeField] private MonoBehaviour displayNameServiceBehaviour;
        [SerializeField] private CaptureBalanceConfigSO practiceBalance;
        [SerializeField] private CombatVisualDatabaseSO visualDatabase;
        [SerializeField] private CaptureAttemptDirector attemptDirector;

        [Header("UI (authored prefabs preferred)")]
        [SerializeField] private CaptureChallengeController challengePrefab;
        [SerializeField] private CanvasGroup pickerPrefab;
        [SerializeField] private CaptureChallengeController challengeController;
        [SerializeField] private CanvasGroup picker;
        [SerializeField] private TMP_Text pickerTitle;
        [SerializeField] private TMP_Text pickerStatus;
        [SerializeField] private RectTransform choicesRoot;
        [SerializeField] private Button choiceTemplate;
        [SerializeField] private Image orbPortrait;
        [SerializeField] private TMP_Text orbDetailName;
        [SerializeField] private TMP_Text orbDescription;
        [SerializeField] private TMP_Text orbMeta;
        [SerializeField] private ProjectedPanelReveal projectedReveal;
        [SerializeField] private TMP_Text footerPrompt;
        [SerializeField] private ScriptableObject itemPresentationCatalog;

        private readonly Dictionary<string, int> practiceAlertness = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly List<Button> choiceButtons = new List<Button>();
        private readonly MoveRowSelectionTuning choiceRowTuning = MoveRowSelectionTuning.CreateDefaults();
        private Tween portraitTween;
        private CaptureOrbChoice focusedOrb;
        private Button focusedChoiceButton;
        private CombatState combatState;
        private EncounterSnapshot encounter;
        private CaptureTargetChoice selectedTarget;
        private Action<CombatAction> queuedCallback;
        private Action<string, CaptureOutcome, bool> outcomeCallback;
        private Action canceledCallback;
        private bool selecting;
        private bool attemptAccepted;
        private PickerStage pickerStage;

        public bool IsSelecting => selecting || (challengeController != null && challengeController.IsRunning);
        public bool IsPickerOpen => selecting && !attemptAccepted;

        private enum PickerStage
        {
            None = 0,
            Orbs = 1,
        }

        private void Awake()
        {
            ResolveReferences();
            SetPickerVisible(false);
            SetChallengeRootActive(false);
        }

        private void OnDisable()
        {
            KillPortraitTween(resetScale: true);
            if (selecting && !attemptAccepted)
                CancelSelection();
            challengeController?.Close();
            SetChallengeRootActive(false);
        }

        public bool CanCapture(CombatState state, EncounterSnapshot currentEncounter, bool isPvp)
        {
            if (state == null || state.team1 == null || isPvp)
                return false;

            if (authority == null)
                authority = FindFirstObjectByType<AuthoritativeCombatSessionService>(FindObjectsInactive.Include);
            if (authority != null && authority.IsRolloutEnabled)
            {
                if (!AuthoritativeBattleLaunchContext.TryReadState(out AuthoritativeClientBattleState serverState))
                    return false;
                return serverState.targets != null && serverState.targets.Exists(t =>
                    t != null && t.isActive && !t.isRemoved && t.currentHp > 0 && t.captureEnabled);
            }

#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
            if (authority != null && !authority.AllowsLocalPractice)
                return false;
            EncounterSnapshot source = currentEncounter;
            bool allowed = CaptureChallengeResolver.IsCaptureAllowed(
                source != null ? source.authoritativeBattleKind : AuthoritativeBattleKind.Scripted,
                source != null ? source.capturePolicy : EncounterCapturePolicy.UseEncounterDefault,
                EncounterCapturePolicy.UseEncounterDefault,
                source != null && source.captureEnabled,
                true,
                true);
            return allowed && state.team1.CountAliveActiveSlots() > 0;
#else
            return false;
#endif
        }

        public bool BeginSelection(
            CombatState state,
            EncounterSnapshot currentEncounter,
            bool isPvp,
            Action<CombatAction> onQueued,
            Action onCanceled)
        {
            if (IsSelecting || !CanCapture(state, currentEncounter, isPvp) || onQueued == null)
                return false;

            ResolveReferences();
            combatState = state;
            encounter = currentEncounter;
            queuedCallback = onQueued;
            outcomeCallback = null;
            canceledCallback = onCanceled;
            attemptAccepted = false;
            selecting = true;
            List<CaptureTargetChoice> targets = ResolveTargetChoices();
            if (targets.Count != 1)
            {
                ResetQueuedAttemptWithoutStarting();
                return false;
            }
            _ = ShowOrbChoicesAsync(targets[0]);
            return true;
        }

        public IReadOnlyList<string> GetEligibleTargetInstanceIds(
            CombatState state,
            EncounterSnapshot currentEncounter,
            bool isPvp)
        {
            if (!CanCapture(state, currentEncounter, isPvp))
                return Array.Empty<string>();

            ResolveReferences();
            combatState = state;
            encounter = currentEncounter;
            List<CaptureTargetChoice> targets = ResolveTargetChoices();
            var ids = new List<string>(targets.Count);
            for (int i = 0; i < targets.Count; i++)
            {
                string id = targets[i]?.LocalInstanceId;
                if (!string.IsNullOrWhiteSpace(id))
                    ids.Add(id);
            }
            return ids;
        }

        public bool BeginOrbSelection(
            CombatState state,
            EncounterSnapshot currentEncounter,
            bool isPvp,
            string targetInstanceId,
            Action<CombatAction> onQueued,
            Action onCanceled)
        {
            if (IsSelecting || !CanCapture(state, currentEncounter, isPvp) || onQueued == null)
                return false;

            ResolveReferences();
            combatState = state;
            encounter = currentEncounter;
            queuedCallback = onQueued;
            outcomeCallback = null;
            canceledCallback = onCanceled;
            attemptAccepted = false;
            selecting = true;
            CaptureTargetChoice target = FindTargetByInstanceId(targetInstanceId);
            if (target == null)
            {
                ResetQueuedAttemptWithoutStarting();
                return false;
            }

            _ = ShowOrbChoicesAsync(target);
            return true;
        }

        public IEnumerator PlayQueuedAttempt(
            CombatState state,
            EncounterSnapshot currentEncounter,
            PendingCaptureAttempt pending,
            Action<string, CaptureOutcome, bool> onOutcome)
        {
            ResolveReferences();
            combatState = state;
            encounter = currentEncounter;
            queuedCallback = null;
            outcomeCallback = onOutcome;
            canceledCallback = null;
            attemptAccepted = false;
            selecting = true;
            selectedTarget = FindTargetByInstanceId(pending != null ? pending.targetInstanceId : null);
            if (selectedTarget == null || selectedTarget.LocalTarget == null || !selectedTarget.LocalTarget.isAlive)
            {
                ResetQueuedAttemptWithoutStarting();
                yield break;
            }

            var orb = new CaptureOrbChoice
            {
                ItemId = pending != null ? pending.orbItemId : "signal_orb",
                DisplayName = pending != null ? pending.orbItemId : "Signal Orb",
                Quantity = 1,
            };
            yield return StartAttemptRoutine(orb);
        }

        public void AbortSelection()
        {
            if (attemptAccepted)
            {
                challengeController?.Close();
                SetChallengeRootActive(false);
                return;
            }
            CancelSelection();
        }

        public void HandleCancel()
        {
            if (!IsPickerOpen)
                return;
            CancelSelection();
        }

        private async Task ShowOrbChoicesAsync(CaptureTargetChoice target)
        {
            selectedTarget = target;
            pickerStage = PickerStage.Orbs;
            SetPickerVisible(true);
            if (pickerTitle != null)
                pickerTitle.text = "SELECT ORB";
            if (pickerStatus != null)
                pickerStatus.text = "Loading profile inventory...";
            if (footerPrompt != null)
                footerPrompt.text = "A Select   B Back";
            SetChoiceInteractable(false);

            List<CaptureOrbChoice> orbs = await ResolveOrbChoicesAsync(target.IsEntangled);
            if (!selecting || pickerStage != PickerStage.Orbs || selectedTarget != target)
                return;
            if (orbs.Count == 0)
            {
                if (pickerStatus != null)
                    pickerStatus.text = "No compatible Orbs are available. Press B to choose another target.";
                ClearChoiceButtons();
                ShowOrbDetails(null, animate: false);
                SetChoiceInteractable(true);
                return;
            }

            if (pickerStatus != null)
                pickerStatus.text = target.DisplayName;
            RebuildOrbChoices(orbs);
            SetChoiceInteractable(true);
        }

        private IEnumerator StartAttemptRoutine(CaptureOrbChoice orb)
        {
            if (!selecting || attemptAccepted || selectedTarget == null || orb == null)
                yield break;

            ResolveReferences();
            if (challengeController == null)
            {
                ResetQueuedAttemptWithoutStarting();
                yield break;
            }

            attemptAccepted = true;
            SetChoiceInteractable(false);
            if (pickerStatus != null)
                pickerStatus.text = "Synchronizing capture signal...";

            bool practice = authority == null || !authority.IsRolloutEnabled;
            ICaptureSession captureSession = null;
            Exception error = null;
            if (!practice)
            {
                Task<ICaptureSession> open = OpenAuthoritativeSessionAsync(orb);
                while (!open.IsCompleted)
                    yield return null;
                if (open.IsFaulted || open.IsCanceled)
                {
                    error = open.Exception != null
                        ? open.Exception.GetBaseException()
                        : new InvalidOperationException("Capture authority failed to open a session.");
                }
                else
                {
                    captureSession = open.Result;
                }
            }
            else
            {
#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
                try
                {
                    captureSession = BuildPracticeSession(orb);
                }
                catch (Exception exception)
                {
                    error = exception;
                }
#else
                error = new InvalidOperationException("Production capture requires the battle authority.");
#endif
            }

            if (error != null || captureSession == null)
            {
                ResolveAttemptDirector()?.Abort();
                attemptAccepted = false;
                if (pickerStatus != null)
                    pickerStatus.text = error != null ? error.Message : "Capture attempt failed.";
                SetChoiceInteractable(true);
                UiHapticsService.Instance?.TriggerWarning();
                ResetQueuedAttemptWithoutStarting();
                yield break;
            }

            SetPickerVisible(false);
            yield return PlayAttemptPresentation(captureSession, practice);
        }

        private void ResetQueuedAttemptWithoutStarting()
        {
            selecting = false;
            attemptAccepted = false;
            pickerStage = PickerStage.None;
            SetPickerVisible(false);
            SetChallengeRootActive(false);
            ClearCallbacks();
        }

        private async Task<ICaptureSession> OpenAuthoritativeSessionAsync(CaptureOrbChoice orb)
        {
            CombatSessionEnvelope active = AuthoritativeBattleLaunchContext.Current;
            if (active == null)
                throw new InvalidOperationException("The authoritative battle session is not bound.");
            CombatSessionEnvelope updated = await authority.SubmitTeamCommandAsync(
                active.BattleId,
                new CombatTeamCommand
                {
                    CommandId = Guid.NewGuid().ToString("D"),
                    ExpectedVersion = active.Version,
                    Kind = "capture",
                    TargetId = selectedTarget.AuthorityTargetId,
                    OrbId = orb.ItemId,
                });
            AuthoritativeBattleLaunchContext.Publish(updated);
            if (!AuthoritativeBattleLaunchContext.TryReadState(out AuthoritativeClientBattleState serverState) ||
                serverState.captureChallenge == null || string.IsNullOrWhiteSpace(serverState.captureChallenge.attemptId))
            {
                throw new InvalidOperationException("Capture authority accepted the command without an attempt ID.");
            }

            return await authority.OpenCaptureSessionAsync(updated.BattleId, serverState.captureChallenge.attemptId);
        }

        private IEnumerator PlayAttemptPresentation(ICaptureSession captureSession, bool practice)
        {
            CaptureAttemptDirector director = ResolveAttemptDirector();
            string introTarget = selectedTarget != null ? selectedTarget.LocalInstanceId : "";
            if (director != null)
                yield return director.PlayIntro(introTarget);

            SetChallengeRootActive(true);
            challengeController?.PrepareFreshAttempt();
            if (challengeController != null)
                yield return challengeController.PlayOpen();
            director?.BeginChallengeAudio(challengeController);

            if (challengeController == null)
            {
                director?.Abort();
                ResetQueuedAttemptWithoutStarting();
                yield break;
            }

            CaptureOutcome outcome = null;
            Task playTask = challengeController.PlayAsync(
                captureSession,
                result => outcome = result);
            while (!playTask.IsCompleted)
                yield return null;

            if (playTask.IsFaulted)
            {
                director?.Abort();
                attemptAccepted = false;
                selecting = false;
                Exception exception = playTask.Exception != null
                    ? playTask.Exception.GetBaseException()
                    : new InvalidOperationException("Capture challenge failed.");
                if (pickerStatus != null)
                    pickerStatus.text = exception.Message;
                SetChoiceInteractable(true);
                UiHapticsService.Instance?.TriggerWarning();
                yield break;
            }

            if (outcome != null && outcome.Kind == CaptureOutcomeKind.Locked)
                yield return FinishLockedOutcome(outcome, practice, introTarget);
            else
                yield return FinishAfterOutcomeDelay(outcome, practice, introTarget);
        }

        private IEnumerator FinishLockedOutcome(CaptureOutcome outcome, bool practice, string localTarget)
        {
            CaptureAttemptDirector director = ResolveAttemptDirector();
            if (director != null)
                yield return director.PlayResultAndExit(true, localTarget, challengeController);
            else
            {
                if (challengeController != null)
                    yield return challengeController.PlayClose();
            }
            SetChallengeRootActive(false);
            CapturePresentationConfigSO board = BalanceConfigRuntime.CapturePresentation;
            string fallbackName = selectedTarget != null ? selectedTarget.DisplayName : "Chainling";
            string subject = CaptureResultCopy.FormatCapturedSubject(
                selectedTarget != null ? selectedTarget.LocalTarget : null,
                fallbackName);
            string prompt = CaptureResultCopy.FormatPrompt(
                board != null ? board.successNamePromptCopy : null,
                subject);
            bool nameChosen = false;
            yield return PresentCaptureNarrationChoices(
                prompt,
                board != null ? board.nameChoiceYesLabel : "Yes",
                board != null ? board.nameChoiceNoLabel : "No",
                choseYes => nameChosen = choseYes);

            string resolvedName = ChainlingDisplayNameRules.ResolveOrFallback(null, fallbackName);
            bool promptFinished = !nameChosen;
            OnScreenKeyboardService keyboard = nameChosen ? OnScreenKeyboardService.Instance : null;
            var input = new CaptureNameKeyboardTarget();
            bool prompted = keyboard != null && keyboard.BeginSession(
                input,
                new KeyboardSessionOptions
                {
                    Title = "Chainling's Name",
                    Prompt = "What will you call your Chainling?",
                    SubmitLabel = "Confirm",
                    EmptyDisplayText = fallbackName,
                    ShowLivePreviewStrip = true,
                    MaxLength = ChainlingDisplayNameRules.MaximumLength,
                    StartShifted = true,
                    CharacterPolicy = KeyboardCharacterPolicy.NameEntry,
                    SubmitValidator = ChainlingDisplayNameRules.IsValid,
                    OnSubmitted = value =>
                    {
                        resolvedName = ChainlingDisplayNameRules.ResolveOrFallback(value, fallbackName);
                        promptFinished = true;
                    },
                    OnCancelled = () =>
                    {
                        resolvedName = ChainlingDisplayNameRules.ResolveOrFallback(null, fallbackName);
                        promptFinished = true;
                    },
                });

            if (prompted)
            {
                while (!promptFinished)
                    yield return null;
            }

            string saveWarning = "";
            if (!practice && !string.IsNullOrWhiteSpace(outcome.GrantedChainlingId))
            {
                IChainlingDisplayNameService nameService = ResolveDisplayNameService();
                if (nameService != null)
                {
                    string profileId = GameSessionContext.Instance != null
                        ? GameSessionContext.Instance.PlayerProfileId
                        : "";
                    Task<MenuMutationResult> saveTask =
                        nameService.SetDisplayNameAsync(profileId, outcome.GrantedChainlingId, resolvedName);
                    while (!saveTask.IsCompleted)
                        yield return null;

                    if (saveTask.IsCanceled || saveTask.IsFaulted || saveTask.Result == null || !saveTask.Result.success)
                        saveWarning = "\nNickname could not be saved; the capture is safe.";
                }
                else
                {
                    saveWarning = "\nNickname service unavailable; the capture is safe.";
                }
            }

            string confirmation;
            if (practice)
            {
                confirmation = $"{resolvedName} was captured in practice.\nNo dossier was saved.";
            }
            else if (outcome.AddedToParty)
            {
                confirmation = $"{resolvedName} was captured and joined your party.\nManage them at a PC.";
            }
            else
            {
                confirmation = $"{resolvedName} was captured and stored.\nEquip them at a PC in your room.";
            }

            outcome.Message = confirmation + saveWarning;
            challengeController?.SetOutcomeMessage(outcome.Message);
            yield return CompleteAttempt(outcome, practice, localTarget, skipExit: true);
        }

        private IEnumerator FinishAfterOutcomeDelay(CaptureOutcome outcome, bool practice, string localTarget)
        {
            CaptureAttemptDirector director = ResolveAttemptDirector();
            if (director != null)
                yield return director.PlayResultAndExit(false, localTarget, challengeController);
            else
            {
                if (challengeController != null)
                    yield return challengeController.PlayClose();
            }

            SetChallengeRootActive(false);
            CapturePresentationConfigSO board = BalanceConfigRuntime.CapturePresentation;
            yield return PresentCaptureNarrationUntilAdvanced(CaptureResultCopy.ResolveFailureCopy(board));
            yield return CompleteAttempt(outcome, practice, localTarget, skipExit: true);
        }

        private IEnumerator CompleteAttempt(CaptureOutcome outcome, bool practice, string localTarget, bool skipExit)
        {
            if (!skipExit)
            {
                yield return new WaitForSecondsRealtime(1.1f);
                challengeController?.Close();
            }

            SetChallengeRootActive(false);
            selecting = false;
            attemptAccepted = false;
            Action<string, CaptureOutcome, bool> callback = outcomeCallback;
            ClearCallbacks();
            callback?.Invoke(localTarget, outcome, practice);
        }

        private CaptureAttemptDirector ResolveAttemptDirector()
        {
            if (attemptDirector == null)
                attemptDirector = CaptureAttemptDirector.Resolve(this);
            return attemptDirector;
        }

        private IEnumerator PresentCaptureNarrationUntilAdvanced(string text)
        {
            CombatNarrationBoxController narration = ResolveNarrationBox();
            if (narration == null || string.IsNullOrWhiteSpace(text))
                yield break;

            var page = new CombatNarrationPage
            {
                kind = CombatNarrationPageKind.Consequence,
                speaker = narration.Profile != null ? narration.Profile.SpeakerLabel : "BATTLE LINK",
                text = text,
                minimumDisplaySeconds = 0.35f,
            };
            yield return narration.PresentUntilAdvanced(page);
            narration.HideForSelection();
        }

        private IEnumerator PresentCaptureNarrationChoices(
            string text,
            string yesLabel,
            string noLabel,
            Action<bool> onChoseYes)
        {
            CombatNarrationBoxController narration = ResolveNarrationBox();
            if (narration == null || string.IsNullOrWhiteSpace(text))
            {
                onChoseYes?.Invoke(true);
                yield break;
            }

            var page = new CombatNarrationPage
            {
                kind = CombatNarrationPageKind.Consequence,
                speaker = narration.Profile != null ? narration.Profile.SpeakerLabel : "BATTLE LINK",
                text = text,
                minimumDisplaySeconds = 0.2f,
            };
            int picked = 0;
            yield return narration.PresentChoices(
                page,
                new[]
                {
                    string.IsNullOrWhiteSpace(yesLabel) ? "Yes" : yesLabel,
                    string.IsNullOrWhiteSpace(noLabel) ? "No" : noLabel,
                },
                index => picked = index);
            narration.HideForSelection();
            onChoseYes?.Invoke(picked == 0);
        }

        private static CombatNarrationBoxController ResolveNarrationBox()
        {
            Combat2BattlefieldPresenter presenter = FindFirstObjectByType<Combat2BattlefieldPresenter>(FindObjectsInactive.Include);
            return CombatNarrationBoxController.FindOrCreate(presenter);
        }

        private IChainlingDisplayNameService ResolveDisplayNameService()
        {
            IChainlingDisplayNameService service = displayNameServiceBehaviour as IChainlingDisplayNameService;
            if (service != null)
                return service;

            MonoBehaviour[] behaviours =
                FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IChainlingDisplayNameService found)
                {
                    displayNameServiceBehaviour = behaviours[i];
                    return found;
                }
            }

            SupabaseChainlingDisplayNameService created =
                gameObject.AddComponent<SupabaseChainlingDisplayNameService>();
            displayNameServiceBehaviour = created;
            return created;
        }

        private CombatVisualDatabaseSO ResolveVisualDatabase()
        {
            if (visualDatabase != null)
                return visualDatabase;

            CombatVisualDatabaseSO[] loaded = Resources.FindObjectsOfTypeAll<CombatVisualDatabaseSO>();
            for (int i = 0; i < loaded.Length; i++)
            {
                CombatVisualDatabaseSO candidate = loaded[i];
                if (candidate != null && (candidate.hideFlags & HideFlags.HideAndDontSave) == 0)
                {
                    visualDatabase = candidate;
                    return candidate;
                }
            }

            return null;
        }

        private ICaptureSession BuildPracticeSession(CaptureOrbChoice orb)
        {
            if (practiceBalance == null)
                practiceBalance = BalanceConfigRuntime.CaptureBalance;
            CaptureBalanceData balance = practiceBalance != null
                ? practiceBalance.ToCoreData()
                : CaptureBalanceData.CreateDefault();
            ChainlingInstance source = selectedTarget.LocalTarget;
            practiceAlertness.TryGetValue(source.instanceId ?? "", out int alertness);
            string alertnessKey = source.instanceId ?? "";
            var target = new CaptureTargetContext
            {
                TargetId = source.instanceId ?? "practice-target",
                SpeciesId = source.speciesId ?? "unknown",
                VisualVariantId = source.visualIdentity != null && !string.IsNullOrWhiteSpace(source.visualIdentity.visualVariantId)
                    ? source.visualIdentity.visualVariantId
                    : "base",
                MutationId = source.visualIdentity != null && !string.IsNullOrWhiteSpace(source.visualIdentity.auraId)
                    ? source.visualIdentity.auraId
                    : "none",
                IsEntangled = source.isEntangled,
                EntangledTypeId = source.entangledType.ToString().ToLowerInvariant(),
                AreaId = encounter != null ? encounter.areaId : "",
                ZoneId = encounter != null ? encounter.zoneId : "",
                DifficultyId = encounter != null ? encounter.difficulty.ToString().ToLowerInvariant() : "standard",
                CurrentHp = Mathf.Max(0, source.currentHP),
                MaxHp = Mathf.Max(1, source.EffectiveMaxHP),
                LegacyBaseCaptureRate = source.legacyBaseCaptureRate,
                Alertness = alertness,
                Ivs = new CaptureIvValues
                {
                    MeleeAttack = source.ivMeleeAtk,
                    MeleeDefense = source.ivMeleeDef,
                    RangedAttack = source.ivRangedAtk,
                    RangedDefense = source.ivRangedDef,
                    Speed = source.ivSpeed,
                },
            };
            CaptureDifficultyContextBuilder.ApplyTo(target, source, ResolveVisualDatabase());
            CaptureItemCatalog.TryResolveOrb(orb.ItemId, out CaptureOrbProfile orbProfile);
            uint seed = DeterministicCaptureRng.StableSeed($"{target.TargetId}:{DateTime.UtcNow.Ticks}");
            string attemptId = $"practice-{Guid.NewGuid():N}";
            CaptureChallengeSnapshot snapshot = CaptureChallengeResolver.Resolve(
                attemptId,
                "editor-practice",
                seed,
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + balance.MaximumAttemptMilliseconds,
                practiceBalance != null ? practiceBalance.ComputeConfigHash() : "practice-default",
                target,
                null,
                orbProfile,
                CaptureEffectVector.None,
                CaptureEffectVector.None,
                balance);
            if (!string.IsNullOrEmpty(alertnessKey))
                practiceAlertness[alertnessKey] = alertness + 1;
            return new LocalCapturePracticeSession(snapshot);
        }

        private List<CaptureTargetChoice> ResolveTargetChoices()
        {
            var result = new List<CaptureTargetChoice>();
            List<ChainlingInstance> local = combatState != null && combatState.team1 != null
                ? combatState.team1.GetActiveChainlings()
                : new List<ChainlingInstance>();

            if (authority != null && authority.IsRolloutEnabled &&
                AuthoritativeBattleLaunchContext.TryReadState(out AuthoritativeClientBattleState server))
            {
                int localIndex = 0;
                for (int i = 0; i < server.targets.Count; i++)
                {
                    AuthoritativeClientBattleTarget target = server.targets[i];
                    if (target == null || !target.isActive || target.isRemoved || target.currentHp <= 0 || !target.captureEnabled)
                        continue;
                    ChainlingInstance localTarget = localIndex < local.Count ? local[localIndex++] : null;
                    result.Add(new CaptureTargetChoice
                    {
                        AuthorityTargetId = target.targetId,
                        LocalInstanceId = localTarget != null ? localTarget.instanceId : target.targetId,
                        LocalTarget = localTarget,
                        DisplayName = string.IsNullOrWhiteSpace(target.displayName) ? target.speciesId : target.displayName,
                        IsEntangled = target.isEntangled,
                    });
                }
                return result;
            }

            for (int i = 0; i < local.Count; i++)
            {
                ChainlingInstance target = local[i];
                result.Add(new CaptureTargetChoice
                {
                    AuthorityTargetId = target.instanceId,
                    LocalInstanceId = target.instanceId,
                    LocalTarget = target,
                    DisplayName = string.IsNullOrWhiteSpace(target.displayName) ? target.speciesId : target.displayName,
                    IsEntangled = target.isEntangled,
                });
            }
            return result;
        }

        private async Task<List<CaptureOrbChoice>> ResolveOrbChoicesAsync(bool targetEntangled)
        {
            var result = new List<CaptureOrbChoice>();
            IInventoryService inventory = ResolveInventoryService();
            InventorySnapshot snapshot = null;
            if (inventory != null)
            {
                string profile = GameSessionContext.Instance != null ? GameSessionContext.Instance.PlayerProfileId : "";
                string wallet = GameSessionContext.Instance != null ? GameSessionContext.Instance.WalletAddress : "";
                snapshot = await inventory.FetchInventoryAsync(profile, wallet);
            }

            if (snapshot != null && snapshot.stacks != null)
            {
                for (int i = 0; i < snapshot.stacks.Count; i++)
                {
                    MenuInventoryItemStack stack = snapshot.stacks[i];
                    if (stack == null || stack.quantity <= 0 || !CaptureItemCatalog.TryResolveOrb(stack.itemId, out CaptureOrbProfile profile))
                        continue;
                    if (targetEntangled && profile.EntanglesOnSuccess)
                        continue;
                    PlayerItemCatalogSeed.TryGet(stack.itemId, out PlayerItemDefinition definition);
                    result.Add(new CaptureOrbChoice
                    {
                        ItemId = stack.itemId,
                        DisplayName = definition != null ? definition.displayName : stack.displayName,
                        Quantity = stack.quantity,
                        Description = definition != null ? definition.description : "No catalog description available.",
                    });
                }
            }

#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
            if ((authority == null || !authority.IsRolloutEnabled) && result.Count == 0)
                result.Add(new CaptureOrbChoice
                {
                    ItemId = "signal_orb",
                    DisplayName = "Signal Orb (Practice)",
                    Quantity = 1,
                    Description = "Neutral capture band and lock profile.",
                });
#endif
            return result;
        }

        private IInventoryService ResolveInventoryService()
        {
            if (inventoryServiceBehaviour is IInventoryService assigned)
                return assigned;
            MonoBehaviour[] behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IInventoryService found)
                {
                    inventoryServiceBehaviour = behaviours[i];
                    return found;
                }
            }
            return null;
        }

        private void QueueSelectedCapture(CaptureOrbChoice orb)
        {
            if (!selecting || selectedTarget == null || orb == null)
                return;

            CombatAction action = CombatAction.Capture(0, selectedTarget.LocalInstanceId, orb.ItemId);
            selecting = false;
            attemptAccepted = false;
            pickerStage = PickerStage.None;
            SetPickerVisible(false);
            Action<CombatAction> queued = queuedCallback;
            ClearCallbacks();
            queued?.Invoke(action);
        }

        private CaptureTargetChoice FindTargetByInstanceId(string instanceId)
        {
            List<CaptureTargetChoice> targets = ResolveTargetChoices();
            if (string.IsNullOrWhiteSpace(instanceId))
                return targets.Count == 1 ? targets[0] : null;

            for (int i = 0; i < targets.Count; i++)
            {
                CaptureTargetChoice candidate = targets[i];
                if (candidate != null &&
                    string.Equals(candidate.LocalInstanceId, instanceId, StringComparison.Ordinal))
                {
                    return candidate;
                }
            }

            return null;
        }

        private void RebuildOrbChoices(List<CaptureOrbChoice> values)
        {
            ClearChoiceButtons();
            focusedOrb = null;
            for (int i = 0; i < values.Count; i++)
            {
                CaptureOrbChoice value = values[i];
                Button button = CreateChoiceButton();
                TryResolveOrbSprites(value.ItemId, out Sprite rowIcon, out _);
                CaptureOrbChoiceRowUGUI row = button.GetComponent<CaptureOrbChoiceRowUGUI>()
                    ?? button.gameObject.AddComponent<CaptureOrbChoiceRowUGUI>();
                row.SetSelectionTuning(choiceRowTuning);
                row.Bind(value.DisplayName, value.Quantity, rowIcon, selected: false, animate: false);
                row.Configure(() => HandleOrbRowFocused(button, value));
                button.onClick.AddListener(() => HandleOrbRowChosen(row, value));
            }

            if (EventSystem.current != null && choiceButtons.Count > 0)
                SelectFirstChoice();
            else if (values.Count > 0)
                ShowOrbDetails(values[0], animate: false);

            ConfigureChoiceNavigation();
        }

        private Button CreateChoiceButton()
        {
            Button button;
            if (choiceTemplate != null)
            {
                button = Instantiate(choiceTemplate, choicesRoot);
                button.gameObject.SetActive(true);
            }
            else
            {
                var go = new GameObject("CaptureChoice", typeof(RectTransform), typeof(Image), typeof(Button));
                go.transform.SetParent(choicesRoot, false);
                Image image = go.GetComponent<Image>();
                image.color = new Color(0.08f, 0.25f, 0.32f, 0.96f);
                button = go.GetComponent<Button>();
                var layout = go.AddComponent<LayoutElement>();
                layout.preferredHeight = 58f;
                CreateText("Label", go.transform, "ORB", 21f);
            }

            if (button.transition != Selectable.Transition.None)
                button.transition = Selectable.Transition.None;
            choiceButtons.Add(button);
            return button;
        }

        private void HandleOrbRowFocused(Button focusedButton, CaptureOrbChoice orb)
        {
            RefreshRowSelection(focusedButton, animate: true);
            ShowOrbDetails(orb, animate: true);
        }

        private void HandleOrbRowChosen(CaptureOrbChoiceRowUGUI row, CaptureOrbChoice orb)
        {
            row?.PlaySelectBounce();
            PlayPortraitTween();
            QueueSelectedCapture(orb);
        }

        private void RefreshRowSelection(Button focusedButton, bool animate)
        {
            bool changed = focusedChoiceButton != focusedButton;
            focusedChoiceButton = focusedButton;
            for (int i = 0; i < choiceButtons.Count; i++)
            {
                Button button = choiceButtons[i];
                if (button == null)
                    continue;
                CaptureOrbChoiceRowUGUI row = button.GetComponent<CaptureOrbChoiceRowUGUI>();
                bool selected = button == focusedButton;
                row?.SetSelected(selected, animate && changed);
            }
        }

        private void ResolveReferences()
        {
            if (authority == null)
                authority = FindFirstObjectByType<AuthoritativeCombatSessionService>(FindObjectsInactive.Include);
            if (challengeController == null)
                challengeController = FindFirstObjectByType<CaptureChallengeController>(FindObjectsInactive.Include);
            if (picker == null)
            {
                CaptureCommandCoordinatorUGUI[] coordinators = FindObjectsByType<CaptureCommandCoordinatorUGUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                for (int i = 0; i < coordinators.Length; i++)
                {
                    if (coordinators[i] != null && coordinators[i].picker != null)
                    {
                        picker = coordinators[i].picker;
                        break;
                    }
                }
            }

            if (picker == null || challengeController == null)
                BuildRuntimeUi();
            BindPickerChildren();
            if (projectedReveal == null && picker != null)
                projectedReveal = picker.GetComponent<ProjectedPanelReveal>();
            if (footerPrompt == null && picker != null)
                footerPrompt = FindChildText(picker.transform, "Footer");
        }

        private void BindPickerChildren()
        {
            if (picker == null)
                return;
            Transform root = picker.transform;
            if (pickerTitle == null)
                pickerTitle = FindChildText(root, "Title");
            if (pickerStatus == null)
                pickerStatus = FindChildText(root, "Status");
            if (choicesRoot == null)
            {
                Transform choices = FindDescendant(root, "Choices");
                if (choices != null)
                    choicesRoot = choices as RectTransform;
            }
            if (choiceTemplate == null)
            {
                Transform template = FindDescendant(root, "ChoiceTemplate");
                if (template != null)
                    choiceTemplate = template.GetComponent<Button>();
            }
            if (orbPortrait == null)
                orbPortrait = FindChildImage(root, "OrbPortrait");
            if (orbDetailName == null)
                orbDetailName = FindChildTextDeep(root, "OrbDetailName");
            if (orbDescription == null)
                orbDescription = FindChildTextDeep(root, "OrbDescription");
            if (orbMeta == null)
                orbMeta = FindChildTextDeep(root, "OrbMeta");
            if (choiceTemplate != null)
                choiceTemplate.gameObject.SetActive(false);
            HideOrbDetailNameAndMeta();
        }

        private void HideOrbDetailNameAndMeta()
        {
            if (orbDetailName != null)
                orbDetailName.gameObject.SetActive(false);
            if (orbMeta != null)
                orbMeta.gameObject.SetActive(false);
        }

        private void BuildRuntimeUi()
        {
            RectTransform pickerParent = ResolvePickerParent();
            RectTransform challengeParent = ResolveChallengeParent();

            if (picker == null)
            {
                if (pickerPrefab != null)
                {
                    picker = Instantiate(pickerPrefab, pickerParent);
                    picker.name = "CaptureCommandPicker";
                    ApplyWalletProjectionRect(picker.transform as RectTransform);
                    projectedReveal = picker.GetComponent<ProjectedPanelReveal>();
                }
                else
                {
                    var go = new GameObject("CaptureCommandPicker", typeof(RectTransform), typeof(CanvasGroup), typeof(Image), typeof(ProjectedPanelReveal));
                    RectTransform root = go.GetComponent<RectTransform>();
                    root.SetParent(pickerParent, false);
                    ApplyWalletProjectionRect(root);
                    Image frame = go.GetComponent<Image>();
                    frame.color = new Color(1f, 1f, 1f, 1f);
                    frame.raycastTarget = false;
                    picker = go.GetComponent<CanvasGroup>();
                    projectedReveal = go.GetComponent<ProjectedPanelReveal>();
                    pickerTitle = CreateText("Title", root, "SELECT TARGET", 30f);
                    pickerTitle.rectTransform.anchorMin = new Vector2(0.05f, 0.82f);
                    pickerTitle.rectTransform.anchorMax = new Vector2(0.95f, 0.97f);
                    pickerStatus = CreateText("Status", root, "", 18f);
                    pickerStatus.rectTransform.anchorMin = new Vector2(0.05f, 0.70f);
                    pickerStatus.rectTransform.anchorMax = new Vector2(0.95f, 0.82f);
                    choicesRoot = new GameObject("Choices", typeof(RectTransform), typeof(VerticalLayoutGroup)).GetComponent<RectTransform>();
                    choicesRoot.SetParent(root, false);
                    choicesRoot.anchorMin = new Vector2(0.07f, 0.18f);
                    choicesRoot.anchorMax = new Vector2(0.93f, 0.68f);
                    choicesRoot.offsetMin = Vector2.zero;
                    choicesRoot.offsetMax = Vector2.zero;
                    VerticalLayoutGroup layout = choicesRoot.GetComponent<VerticalLayoutGroup>();
                    layout.spacing = 10f;
                    layout.childControlHeight = true;
                    layout.childControlWidth = true;
                    layout.childForceExpandHeight = false;
                    layout.childForceExpandWidth = true;
                    footerPrompt = CreateText("Footer", root, "A Select   B Back", 18f);
                    footerPrompt.rectTransform.anchorMin = new Vector2(0.08f, 0.02f);
                    footerPrompt.rectTransform.anchorMax = new Vector2(0.55f, 0.14f);
                }
            }

            if (challengeController == null)
            {
                if (challengePrefab != null)
                {
                    challengeController = Instantiate(challengePrefab, challengeParent);
                    challengeController.name = "CaptureChallenge";
                }
                else
                {
                    var challenge = new GameObject("CaptureChallenge", typeof(RectTransform), typeof(CaptureChallengeController));
                    RectTransform rect = challenge.GetComponent<RectTransform>();
                    rect.SetParent(challengeParent, false);
                    Stretch(rect, 0f);
                    challengeController = challenge.GetComponent<CaptureChallengeController>();
                }
            }

            SetPickerVisible(false);
            SetChallengeRootActive(false);
        }

        private void SetChallengeRootActive(bool active)
        {
            if (challengeController != null)
                challengeController.gameObject.SetActive(active);
        }

        private static RectTransform ResolvePickerParent()
        {
            Transform bottomPanel = FindDeepTransform("BottomActionPanel");
            if (bottomPanel != null)
                return bottomPanel as RectTransform;
            return ResolveChallengeParent();
        }

        private static RectTransform ResolveChallengeParent()
        {
            Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < canvases.Length; i++)
            {
                Canvas canvas = canvases[i];
                if (canvas == null)
                    continue;
                if (canvas.name == "CombatHudCanvas")
                    return canvas.transform as RectTransform;
            }

            Transform safeArea = FindDeepTransform("SafeAreaRoot");
            if (safeArea != null)
                return safeArea as RectTransform;

            for (int i = 0; i < canvases.Length; i++)
            {
                if (canvases[i] != null)
                    return canvases[i].transform as RectTransform;
            }

            var canvasObject = new GameObject("CaptureCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas created = canvasObject.GetComponent<Canvas>();
            created.renderMode = RenderMode.ScreenSpaceOverlay;
            DontDestroyOnLoad(canvasObject);
            return canvasObject.transform as RectTransform;
        }

        private static Transform FindDeepTransform(string name)
        {
            Transform[] transforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < transforms.Length; i++)
            {
                if (transforms[i] != null && transforms[i].name == name)
                    return transforms[i];
            }
            return null;
        }

        private static TMP_Text FindChildText(Transform root, string childName)
        {
            Transform child = root.Find(childName);
            return child != null ? child.GetComponent<TMP_Text>() : null;
        }

        private static TMP_Text FindChildTextDeep(Transform root, string childName)
        {
            Transform child = FindDescendant(root, childName);
            return child != null ? child.GetComponent<TMP_Text>() : null;
        }

        private static Image FindChildImage(Transform root, string childName)
        {
            Transform child = FindDescendant(root, childName);
            return child != null ? child.GetComponent<Image>() : null;
        }

        private static Transform FindDescendant(Transform root, string childName)
        {
            if (root == null)
                return null;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child.name == childName)
                    return child;
                Transform nested = FindDescendant(child, childName);
                if (nested != null)
                    return nested;
            }
            return null;
        }

        private void CancelSelection()
        {
            if (!selecting || attemptAccepted)
                return;
            selecting = false;
            pickerStage = PickerStage.None;
            SetPickerVisible(false);
            Action canceled = canceledCallback;
            ClearCallbacks();
            canceled?.Invoke();
        }

        private void ConfigureChoiceNavigation()
        {
            for (int i = 0; i < choiceButtons.Count; i++)
            {
                if (choiceButtons[i] == null)
                    continue;
                var navigation = new Navigation { mode = Navigation.Mode.Explicit };
                navigation.selectOnUp = choiceButtons.Count > 0 ? choiceButtons[(i - 1 + choiceButtons.Count) % choiceButtons.Count] : null;
                navigation.selectOnDown = choiceButtons.Count > 0 ? choiceButtons[(i + 1) % choiceButtons.Count] : null;
                choiceButtons[i].navigation = navigation;
            }
        }

        private void FailBeforeAttempt(string message)
        {
            if (pickerStatus != null)
                pickerStatus.text = message;
            UiHapticsService.Instance?.TriggerWarning();
            CancelSelection();
        }

        private void ClearCallbacks()
        {
            combatState = null;
            encounter = null;
            selectedTarget = null;
            outcomeCallback = null;
            queuedCallback = null;
            canceledCallback = null;
            ClearChoiceButtons();
        }

        private void ClearChoiceButtons()
        {
            focusedOrb = null;
            focusedChoiceButton = null;
            for (int i = 0; i < choiceButtons.Count; i++)
            {
                if (choiceButtons[i] != null)
                    Destroy(choiceButtons[i].gameObject);
            }
            choiceButtons.Clear();
        }

        private void SetChoiceInteractable(bool interactable)
        {
            for (int i = 0; i < choiceButtons.Count; i++)
                if (choiceButtons[i] != null) choiceButtons[i].interactable = interactable;
        }

        private void SelectFirstChoice()
        {
            if (EventSystem.current != null && choiceButtons.Count > 0 && choiceButtons[0] != null)
                EventSystem.current.SetSelectedGameObject(choiceButtons[0].gameObject);
        }

        private void ShowOrbDetails(CaptureOrbChoice orb, bool animate)
        {
            HideOrbDetailNameAndMeta();
            if (orbDescription != null)
            {
                orbDescription.text = orb != null && !string.IsNullOrWhiteSpace(orb.Description)
                    ? orb.Description
                    : "Select an available Orb to inspect its capture profile.";
            }

            Sprite portrait = null;
            if (orb != null)
                TryResolveOrbSprites(orb.ItemId, out _, out portrait);
            if (orbPortrait != null)
            {
                if (portrait != null)
                    orbPortrait.sprite = portrait;
                orbPortrait.preserveAspect = orbPortrait.sprite != null;
                orbPortrait.enabled = true;
                orbPortrait.color = orb != null ? Color.white : new Color(1f, 1f, 1f, 0.18f);
            }

            bool changed = orb != focusedOrb;
            focusedOrb = orb;
            if (animate && orb != null && changed)
                PlayPortraitTween();
        }

        private bool TryResolveOrbSprites(string itemId, out Sprite rowIcon, out Sprite detailPortrait)
        {
            rowIcon = null;
            detailPortrait = null;
            if (itemPresentationCatalog is IItemPresentationSprites lookup &&
                lookup.TryGetItemSprites(itemId, out rowIcon, out detailPortrait))
            {
                return rowIcon != null || detailPortrait != null;
            }

            if (choiceTemplate != null)
            {
                Transform icon = choiceTemplate.transform.Find("RowContent/ImageBG/Image")
                    ?? choiceTemplate.transform.Find("Icon");
                if (icon != null)
                {
                    Image iconImage = icon.GetComponent<Image>();
                    if (iconImage != null)
                        rowIcon = iconImage.sprite;
                }
            }

            if (orbPortrait != null)
                detailPortrait = orbPortrait.sprite;
            return rowIcon != null || detailPortrait != null;
        }

        private void PlayPortraitTween()
        {
            if (orbPortrait == null)
                return;

            RectTransform rect = orbPortrait.rectTransform;
            KillPortraitTween(resetScale: true);
            portraitTween = rect
                .DOScale(1.06f, 0.08f)
                .SetEase(Ease.OutQuad)
                .SetLoops(2, LoopType.Yoyo)
                .SetUpdate(true)
                .OnKill(() => portraitTween = null)
                .OnComplete(() =>
                {
                    portraitTween = null;
                    rect.localScale = Vector3.one;
                });
        }

        private void KillPortraitTween(bool resetScale)
        {
            if (portraitTween != null && portraitTween.IsActive())
                portraitTween.Kill(complete: false);
            portraitTween = null;
            if (resetScale && orbPortrait != null)
            {
                orbPortrait.rectTransform.DOKill();
                orbPortrait.rectTransform.localScale = Vector3.one;
            }
        }

        private void SetPickerVisible(bool visible)
        {
            if (picker == null)
                return;

            ProjectedPanelReveal reveal = projectedReveal != null
                ? projectedReveal
                : picker.GetComponent<ProjectedPanelReveal>();
            picker.interactable = visible;
            picker.blocksRaycasts = visible;

            if (!visible)
                KillPortraitTween(resetScale: true);

            if (visible)
            {
                if (reveal != null)
                {
                    reveal.Reveal();
                }
                else
                {
                    picker.alpha = 1f;
                    picker.gameObject.SetActive(true);
                }
                return;
            }

            if (reveal != null)
            {
                reveal.Hide();
                return;
            }

            picker.alpha = 0f;
            picker.interactable = false;
            picker.blocksRaycasts = false;
            picker.gameObject.SetActive(false);
        }

        private static TMP_Text CreateText(string name, Transform parent, string value, float size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            var text = go.GetComponent<TextMeshProUGUI>();
            text.text = value;
            text.fontSize = size;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.raycastTarget = false;
            return text;
        }

        private static void ApplyWalletProjectionRect(RectTransform rect)
        {
            if (rect == null)
                return;
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, -0.62f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(1320f, 280f);
        }

        private static void Stretch(RectTransform rect, float inset)
        {
            if (rect == null) return;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }

        private sealed class CaptureTargetChoice
        {
            public string AuthorityTargetId;
            public string LocalInstanceId;
            public ChainlingInstance LocalTarget;
            public string DisplayName;
            public bool IsEntangled;
        }

        private sealed class CaptureOrbChoice
        {
            public string ItemId;
            public string DisplayName;
            public int Quantity;
            public string Description;
        }

        private sealed class CaptureNameKeyboardTarget : IKeyboardInputTarget
        {
            public string Text { get; set; } = "";
            public int CharacterLimit => ChainlingDisplayNameRules.MaximumLength;
            public bool IsValid => true;
            public void Focus() { }
        }
    }
}
