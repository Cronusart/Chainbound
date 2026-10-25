using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// Bridges Combat state/events to the battle scene visuals.
    /// - Binds Chainlings to 2v2 slot views
    /// - Tracks the acting Chainling for camera focus
    /// - Drives attack animations via CombatChainlingVisual (no fixed delays)
    /// - Spawns per-move VFX from a CombatVFXLibrary
    /// - Triggers camera shake on hits
    /// </summary>
    public partial class Combat2BattlefieldPresenter : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Optional: drag any MonoBehaviour that implements ICombatStateSource. " +
                 "If null, auto-discovers CombatBattleController or Combat2TestHarness in the scene.")]
        [SerializeField] private MonoBehaviour stateSourceOverride;
        [SerializeField] private MonoBehaviour combatCameraController;
        [SerializeField] private Camera combatCamera;
        [SerializeField] private Transform defaultCameraTarget;
        [Tooltip("Optional wallet offset controller. Auto-discovers the hardware wallet when unset.")]
        [SerializeField] private MonoBehaviour hardwareWalletOffsetController;
        [Tooltip("Settled battlefield framing rules. If unset, no automatic 1v1 framing is applied.")]
        [SerializeField] private CombatBattlefieldFramingSO battlefieldFraming;
        [Tooltip("Optional presentation-only slot layout controller. Camera and wallet framing remain independent.")]
        [SerializeField] private MonoBehaviour battlefieldSingleCombatLayoutController;

        /// <summary>Resolved at runtime from stateSourceOverride or auto-discovery.</summary>
        private ICombatStateSource _stateSource;
        private ICombatBattlefieldFramingOffsetController _walletFramingOffset;
        private ICombatBattlefieldSingleCombatLayoutController _battlefieldSingleCombatLayout;
        private ICombatSingleCombatHudLayoutController _singleCombatHudLayout;

        [Header("Slots (2v2)")]
        [SerializeField] private Combat2ChainlingView team0Slot0;
        [SerializeField] private Combat2ChainlingView team0Slot1;
        [SerializeField] private Combat2ChainlingView team1Slot0;
        [SerializeField] private Combat2ChainlingView team1Slot1;

        [Header("Polling")]
        [SerializeField] private float pollInterval = 0.1f;

        [Header("Camera Tracking")]
        [SerializeField] private bool trackActingChainling = true;
        [SerializeField] private float targeterFlashDuration = 0.35f;

        [Header("Camera Focus")]
        [SerializeField] [Range(0f, 1f)] private float selectionFocusWeight = 0.22f;
        [SerializeField] [Range(0f, 1f)] private float actionFocusWeight = 0.38f;
        [SerializeField] [Range(0f, 1f)] private float impactFocusWeight = 0.52f;
        [SerializeField] private float maxFocusShift = 1.35f;
        [SerializeField] private float selectionFocusSmoothTime = 0.18f;
        [SerializeField] private float actionFocusSmoothTime = 0.14f;
        [SerializeField] private float impactFocusSmoothTime = 0.10f;
        [SerializeField] private float defaultReturnSmoothTime = 0.16f;
        [SerializeField] private float impactHoldDuration = 0.18f;
        [SerializeField] private float defaultRangedTravelDuration = 0.20f;
        [SerializeField] private AnimationCurve rangedLeadCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("VFX")]
        [Tooltip("Assign a CombatVFXLibrary asset to enable per-move visual effects.")]
        [SerializeField] private CombatVFXLibrary vfxLibrary;

        [Header("Visual Identity")]
        [Tooltip("Assign a CombatVisualDatabaseSO to auto-resolve species sprites when chainlings are assigned to slots.")]
        [SerializeField] private CombatVisualDatabaseSO visualDatabase;
        [Tooltip("Optional registry for Chainling auras, HUD card variants, poses, and identity FX.")]
        [SerializeField] private ChainlingVisualIdentityDatabaseSO visualIdentityDatabase;

        [Tooltip("Optional: layered frame presentation dispatcher for species and move tracks.")]
        [SerializeField] private CombatPresentationDirector presentationDirector;

        [Tooltip("Combat-owned post-FX volume controller. Auto-resolved under this presenter; never the scene Global Volume.")]
        [SerializeField] private CombatPostFxVolumeController postFxVolumeController;

        public ChainlingVisualIdentityDatabaseSO VisualIdentityDatabase => visualIdentityDatabase;

        public bool TryGetBossHoverWorldPosition(float extraY, out Vector3 position)
        {
            ResolveBossCenterPosition();
            if (bossCenterPosition == null)
            {
                position = Vector3.zero;
                return false;
            }

            position = bossCenterPosition.position + Vector3.up * extraY;
            return true;
        }

        public bool TryGetSlotAimWorldPosition(string instanceId, out Vector3 position)
        {
            Combat2ChainlingView view = GetView(instanceId);
            if (view == null)
            {
                position = Vector3.zero;
                return false;
            }

            position = view.ResolvePopupWorldPosition();
            return true;
        }

        public float PlayCaptureAttemptHide(string instanceId)
        {
            Combat2ChainlingView view = GetView(instanceId);
            return view != null ? view.PlayCaptureAttemptHide() : 0f;
        }

        public float PlayCaptureAttemptRestore(string instanceId)
        {
            Combat2ChainlingView view = GetView(instanceId);
            return view != null ? view.PlayCaptureAttemptRestore() : 0f;
        }

        [Header("Timing")]
        [Tooltip("Optional shared pacing/feedback profile. Falls back to CombatBattleController.PacingProfile when unset.")]
        [SerializeField] private CombatBattlePacingProfileSO pacingProfile;
        [Tooltip("Small buffer after swap events so the scene settles.")]
        [SerializeField] private float swapSettleDelay = 0.4f;
        [Tooltip("Pause after impact before playing the faint (KO) animation. Makes the KO moment feel more impactful.")]
        [SerializeField] private float faintPostImpactDelay = 0.25f;

        [Header("Camera Shake")]
        [SerializeField] private bool shakeOnHit = true;
        [SerializeField] private float minShakeIntensity = 0.03f;
        [SerializeField] private float maxShakeIntensity = 0.12f;
        [SerializeField] private float maxDamageForShake = 80f;
        [SerializeField] private float shakeDuration = 0.15f;
        [SerializeField] private float shakeFrequency = 20f;

        [Header("Combat UX")]
        [SerializeField] private DamagePopupSpawner damagePopupSpawner;
        [SerializeField] private CombatPopupOverlayCameraBootstrap popupOverlayCameraBootstrap;
        [SerializeField] private CombatUiAnimTuning uiAnimTuning;
        [SerializeField] private CombatActionOutlineTuningSO actionOutlineTuning;
        [Tooltip("Optional: refresh HUD vitals after damage animation so HP bar aligns with hit visuals.")]
        [SerializeField] private CombatBattleUGUIController hudController;
        [Tooltip("Optional: staged combat UI reveal. If null, falls back to hudController SetBattleHudVisible.")]
        [SerializeField] private CombatUiStageController uiStageController;
        [Tooltip("Log HP refresh when HandleDamage completes.")]
        [SerializeField] private bool enableHpDebugLogs = false;

        [Header("Battle Intro")]
        [SerializeField] private BattleIntroController battleIntroController;
        [SerializeField] private Transform bossCenterPosition;
        [SerializeField] private float battleIntroDelay = 0.6f;
        [SerializeField] private float postGlitchWalletDelay = 0.15f;
        [SerializeField] private bool hideHudDuringBattleIntro = true;
        [SerializeField] private bool useBattleIntroGlitch = true;
        [Tooltip("Log battle intro sequence flow for debugging intro hang.")]
        [SerializeField] private bool enableBattleIntroDebugLogs = false;

        [Header("Wall Visuals")]
        [SerializeField] private CombatWallVisual wallVisualPrefab;
        [SerializeField] private CombatWallVisualProfileSO wallVisualProfile;

        private readonly Dictionary<string, Combat2ChainlingView> _viewById = new Dictionary<string, Combat2ChainlingView>();
        private readonly Dictionary<string, MovePresentationContext> _moveContextByMoveId = new Dictionary<string, MovePresentationContext>();
        private readonly Dictionary<string, bool> _barrierActiveByInstanceId = new Dictionary<string, bool>();
        private readonly Dictionary<string, string> _wallSourceByInstanceId = new Dictionary<string, string>();
        private readonly Dictionary<string, GameObject> _wallLoopVfxByInstanceId = new Dictionary<string, GameObject>();
        private readonly HashSet<string> _wallBreakPendingByInstanceId = new HashSet<string>();
        private readonly HashSet<string> _wallRaisePendingByInstanceId = new HashSet<string>();
        private readonly HashSet<string> _wallExpirePresentedKeys = new HashSet<string>();
        private readonly Queue<CombatEvent> _eventQueue = new Queue<CombatEvent>();
        private readonly Queue<IEnumerator> _feedbackQueue = new Queue<IEnumerator>();
        private int _lastPresentedCombatEventTimestamp = -1;
        private int _lastEventCount = -1;
        private float _nextPollTime;
        private Coroutine _sequenceRoutine;
        private Coroutine _battleIntroRoutine;
        private Coroutine _introCameraRestoreRoutine;
        private Coroutine _battlefieldFramingRoutine;
        private Coroutine _cameraBlendOperation;
        private Coroutine _layoutBlendOperation;
        private Coroutine _walletBlendOperation;
        private bool _battlefieldFramingMode;
        private bool _battlefieldLayoutMode;
        private bool _cameraWarningLogged;
        private CombatBattleController _battleController;
        private BattleIntroPresentationSO _activeIntroPresentation;
        private Transform _cameraRuntimeFocusTarget;
        private Vector3 _cameraFocusVelocity;
        private bool _pendingResetToDefault;
        private CombatCameraFocusMode _cameraFocusMode = CombatCameraFocusMode.Default;
        private Transform _focusPrimaryAnchor;
        private Transform _focusSecondaryAnchor;
        private float _currentFocusWeight;
        private float _currentFocusSmoothTime = 0.16f;
        private float _focusModeStartTime;
        private float _rangedLeadDuration;
        private Tween _introLeadSlideTween;
        private Vector3 _team1Slot0DefaultLocalPosition;
        private Vector3 _team1Slot1DefaultLocalPosition;
        private bool _hasTeam1Slot0DefaultLocalPosition;
        private bool _hasTeam1Slot1DefaultLocalPosition;
        private bool _introTeam1Slot1CenterStaged;

        // UI-side preview state (action/target selection).
        private string _previewActorId;
        private string _previewTargetId;

        /// <summary>True when the presenter has no pending events and no running animation sequence.</summary>
        public bool IsEventQueueIdle => _sequenceRoutine == null && _eventQueue.Count == 0 && _feedbackQueue.Count == 0 && !HasPendingNarration;

        /// <summary>
        /// Stops leftover intro/turn presentation so post-battle flow cannot hang
        /// waiting for an event queue that never drains (scripted flee).
        /// </summary>
        public void AbortQueuedPresentation()
        {
            if (_sequenceRoutine != null)
            {
                StopCoroutine(_sequenceRoutine);
                _sequenceRoutine = null;
            }

            _eventQueue.Clear();
            _feedbackQueue.Clear();
            ClearNarrationQueue();
        }

        /// <summary>Fired when the presenter finishes processing all queued events.</summary>
        public event System.Action OnEventQueueIdle;

        public void SetIntroPresentation(BattleIntroPresentationSO presentation)
        {
            _activeIntroPresentation = presentation;
        }

        public void ClearIntroPresentation()
        {
            _activeIntroPresentation = null;
        }

        private enum CombatCameraFocusMode
        {
            Default,
            SelectionActor,
            ActionSource,
            RangedLead,
            ImpactTarget,
            RecipientTarget
        }

        private sealed class MovePresentationContext
        {
            public string moveId;
            public string sourceId;
            public string targetId;
            public bool initialTargetsWall;
            public MoveCategory category;
            public MoveDefinitionSO moveDefinition;
            public CombatVFXLibrary.VFXEntry vfxEntry;
            public VfxResolveTier vfxResolveTier;
        }

        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â
        // Unity Lifecycle
        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â

        private void OnEnable()
        {
            ResolveStateSource();
            ResolveUxFallbacks();
            _battleController = _stateSource as CombatBattleController;
            ResolvePresentationDirector();
            ResolvePostFxVolumeController();
            ResolveNarrationController();

            // Subscribe to OnChainlingFainted as a fallback for KO animations
            if (_battleController != null)
            {
                _battleController.OnBattleStarted += HandleBattleStarted;
                _battleController.OnChainlingFainted += HandleChainlingFaintedFallback;
                _battleController.OnTurnActorSelected += HandleTurnActorSelected;
                _battleController.OnActionTargetsResolved += HandleActionTargetsResolved;
                _battleController.OnTurnEnded += HandleTurnEnded;
                _battleController.OnBattleEnded += HandleBattleEnded;
            }

            // Camera: prefer an active controller in this combat scene.
            if (!IsCombatSceneCameraHost(combatCameraController) ||
                !CanControllerAcceptCombatTarget(combatCameraController))
            {
                if (combatCameraController != null && !_cameraWarningLogged)
                {
                    Debug.LogWarning($"[Combat2Presenter] Assigned camera controller ({combatCameraController.GetType().Name}) is not a live combat-scene camera. Falling back to scene discovery.");
                    _cameraWarningLogged = true;
                }

                combatCameraController = FindCameraController(combatCameraController);
            }

            if (!IsCombatSceneCameraHost(combatCamera))
            {
                Camera localCamera = FindCombatSceneCamera();
                if (localCamera != null)
                    combatCamera = localCamera;
                else if (combatCamera == null)
                    combatCamera = Camera.main;
            }

            if (defaultCameraTarget == null)
            {
                var focusObj = GameObject.Find("Combat2CameraFocus");
                if (focusObj != null)
                    defaultCameraTarget = focusObj.transform;
            }

            InitializeCombatCameraFocus();
            ResolveWalletFramingOffsetController();

            AutoFindSlots();
            ResolveBattlefieldSingleCombatLayoutController();
            ResolveSingleCombatHudLayoutController();
            ApplyOutlineTuningToSlots();
            ResolveBossCenterPosition();
            CacheBattleIntroSlotDefaults();
            ResetIntroLeadSlotPosition();
            RefreshSlotBindings();
        }

        private void OnDisable()
        {
            if (_battleController != null)
            {
                _battleController.OnBattleStarted -= HandleBattleStarted;
                _battleController.OnChainlingFainted -= HandleChainlingFaintedFallback;
                _battleController.OnTurnActorSelected -= HandleTurnActorSelected;
                _battleController.OnActionTargetsResolved -= HandleActionTargetsResolved;
                _battleController.OnTurnEnded -= HandleTurnEnded;
                _battleController.OnBattleEnded -= HandleBattleEnded;
                _battleController = null;
            }

            if (_battleIntroRoutine != null)
            {
                StopCoroutine(_battleIntroRoutine);
                _battleIntroRoutine = null;
            }

            CancelIntroCameraRestore();
            StopBattlefieldPresentationTransition();
            battleIntroController?.CancelIntro();
            CancelIntroLeadSlide(resetLeadPosition: true);
            ClearIntroPresentation();

            RestoreDefaultCameraTarget();
            DestroyRuntimeCameraFocusTarget();
            ClearAllGroundCircles();
            ForceClearCombatPostFx();
            _moveContextByMoveId.Clear();
            _barrierActiveByInstanceId.Clear();
            _wallSourceByInstanceId.Clear();
            ClearAllWallLoopVfx();
            _wallBreakPendingByInstanceId.Clear();
            _wallRaisePendingByInstanceId.Clear();
            _wallExpirePresentedKeys.Clear();
            _lastPresentedCombatEventTimestamp = -1;
            _batchedOutcomeSfxEventTimestamps.Clear();
            _pendingResetToDefault = false;
            ClearPresentationLocks();
            _focusPrimaryAnchor = null;
            _focusSecondaryAnchor = null;
            _cameraFocusMode = CombatCameraFocusMode.Default;
            ClearNarrationQueue();
            SetWalletFramingActive(false);
            SetSingleCombatHudLayoutActive(false);
            RestoreBattlefieldSingleCombatLayout();
            _battlefieldFramingMode = false;
            _battlefieldLayoutMode = false;
        }

        private void Update()
        {
            if (Time.time < _nextPollTime)
                return;

            _nextPollTime = Time.time + pollInterval;

            PrelockUnqueuedPresentationEvents();
            RefreshSlotBindings();
            EnqueueNewEvents();
            EvaluateBattlefieldFraming();
        }

        private void PrelockUnqueuedPresentationEvents()
        {
            if (_stateSource == null)
                return;

            var log = _stateSource.EventLog;
            if (log == null || _lastEventCount < 0)
                return;

            ApplyPresentationLocksForNewEvents(log.Events, _lastEventCount);
        }

        private void LateUpdate()
        {
            UpdateCombatCameraFocus(Time.deltaTime);
        }

        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â
        // Slot Discovery & Binding
        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â

        private void AutoFindSlots()
        {
            if (team0Slot0 != null && team0Slot1 != null && team1Slot0 != null && team1Slot1 != null)
                return;

            Transform root = transform;
            TryFindSlot(root, "Team0_Slot0", ref team0Slot0);
            TryFindSlot(root, "Team0_Slot1", ref team0Slot1);
            TryFindSlot(root, "Team1_Slot0", ref team1Slot0);
            TryFindSlot(root, "Team1_Slot1", ref team1Slot1);
        }

        private void TryFindSlot(Transform root, string name, ref Combat2ChainlingView slot)
        {
            if (slot != null || root == null)
                return;

            var found = root.Find(name);
            if (found != null)
                slot = found.GetComponent<Combat2ChainlingView>();
        }

        private void RefreshSlotBindings()
        {
            var state = _stateSource != null ? _stateSource.State : null;
            if (state == null)
            {
                AssignSlot(team0Slot0, null);
                AssignSlot(team0Slot1, null);
                AssignSlot(team1Slot0, null);
                AssignSlot(team1Slot1, null);
                SyncWallVisual(team0Slot0, null);
                SyncWallVisual(team0Slot1, null);
                SyncWallVisual(team1Slot0, null);
                SyncWallVisual(team1Slot1, null);
                _barrierActiveByInstanceId.Clear();
                _wallSourceByInstanceId.Clear();
                ClearAllWallLoopVfx();
                _wallBreakPendingByInstanceId.Clear();
                _wallRaisePendingByInstanceId.Clear();
                RebuildViewLookup();
                return;
            }

            ChainlingInstance t0s0 = state.team0.GetActiveInSlot(0);
            ChainlingInstance t0s1 = state.team0.GetActiveInSlot(1);
            ChainlingInstance t1s0 = state.team1.GetActiveInSlot(0);
            ChainlingInstance t1s1 = state.team1.GetActiveInSlot(1);

            AssignSlotRespectingLock(team0Slot0, t0s0, 0, 0);
            AssignSlotRespectingLock(team0Slot1, t0s1, 0, 1);
            AssignSlotRespectingLock(team1Slot0, t1s0, 1, 0);
            AssignSlotRespectingLock(team1Slot1, t1s1, 1, 1);
            SyncWallVisual(team0Slot0, ResolveWallChainlingForSlot(t0s0, 0, 0));
            SyncWallVisual(team0Slot1, ResolveWallChainlingForSlot(t0s1, 0, 1));
            SyncWallVisual(team1Slot0, ResolveWallChainlingForSlot(t1s0, 1, 0));
            SyncWallVisual(team1Slot1, ResolveWallChainlingForSlot(t1s1, 1, 1));
            PruneBarrierTrackingToActive(t0s0, t0s1, t1s0, t1s1);
            RebuildViewLookup();
            // Do NOT call ApplySelectionPreview here — it runs every pollInterval (0.1s) and would
            // ClearAllGroundCircles + re-apply every tick, killing the pulse animation and resetting
            // scale before it can complete. The preview is driven by SetSelectionPreview() from the
            // coordinator when the user selects a move/target.
        }

        public void SetSelectionPreview(string actorInstanceId, string targetInstanceId)
        {
            _previewActorId = actorInstanceId;
            _previewTargetId = targetInstanceId;
            ApplySelectionPreview();
        }

        public void ClearSelectionPreview()
        {
            _previewActorId = null;
            _previewTargetId = null;
            ClearAllTargeters();
            ClearAllGroundCircles();
        }

        public void SetSelectingActorSlot(int teamIndex, int slotIndex)
        {
            _previewActorId = null;
            _previewTargetId = null;
            ClearAllTargeters();
            ClearAllGroundCircles();

            var actorView = GetViewBySlot(teamIndex, slotIndex);
            actorView?.GroundCircle?.SetState(CombatSlotGroundCircle.CircleState.ActiveActor);
            _pendingResetToDefault = false;
            FocusSelectionActor(actorView);
        }

        public void ClearSelectingActorSlot()
        {
            ClearAllTargeters();
            ClearAllGroundCircles();
        }

        private void AssignSlot(Combat2ChainlingView view, ChainlingInstance chainling)
        {
            if (view == null)
                return;

            view.ConfigureOutlineTuning(actionOutlineTuning);
            view.AssignChainling(chainling, visualDatabase, visualIdentityDatabase);
            view.SetPresentationScaleMultiplier(ResolvePresentationScaleMultiplier(view));
        }

        private float ResolvePresentationScaleMultiplier(Combat2ChainlingView view)
        {
            // Team 0 is the player's side. Only the opponent slots consume the
            // encounter-authored multiplier; all normal/player visuals remain 1x.
            if (view != team1Slot0 && view != team1Slot1)
                return 1f;

            EncounterSnapshot encounter = _battleController != null
                ? _battleController.CurrentEncounter
                : null;
            return encounter != null
                ? Mathf.Max(0.01f, encounter.opponentVisualScaleMultiplier)
                : 1f;
        }

        private void ApplyOutlineTuningToSlots()
        {
            ConfigureSlotOutlineTuning(team0Slot0);
            ConfigureSlotOutlineTuning(team0Slot1);
            ConfigureSlotOutlineTuning(team1Slot0);
            ConfigureSlotOutlineTuning(team1Slot1);
        }

        private void ConfigureSlotOutlineTuning(Combat2ChainlingView view)
        {
            view?.ConfigureOutlineTuning(actionOutlineTuning);
        }

        private void ClearAllActionHighlights()
        {
            team0Slot0?.SetHighlighted(false);
            team0Slot1?.SetHighlighted(false);
            team1Slot0?.SetHighlighted(false);
            team1Slot1?.SetHighlighted(false);
        }

        private void PlayVictoryPoses(BattleResult result)
        {
            if (result == null || result.winnerTeamIndex < 0)
                return;

            if (result.winnerTeamIndex == 0)
            {
                PlayVictoryPose(team0Slot0);
                PlayVictoryPose(team0Slot1);
                return;
            }

            if (result.winnerTeamIndex == 1)
            {
                PlayVictoryPose(team1Slot0);
                PlayVictoryPose(team1Slot1);
            }
        }

        private static void PlayVictoryPose(Combat2ChainlingView view)
        {
            if (view != null && !string.IsNullOrEmpty(view.InstanceId))
                view.PlayVictoryPose();
        }

        private void SyncWallVisual(Combat2ChainlingView view, ChainlingInstance chainling)
        {
            if (view == null)
                return;

            if (chainling == null || string.IsNullOrEmpty(chainling.instanceId))
            {
                // Slot clears mid-sequence (KO → swap) must not destroy a wall the event handlers own.
                if (_sequenceRoutine != null)
                    return;

                view.ForceHideWall();
                return;
            }

            string instanceId = chainling.instanceId;
            bool hasBarrier = CombatWallStateUtility.HasActiveWallState(chainling);
            bool wasActive = _barrierActiveByInstanceId.TryGetValue(instanceId, out var prev) && prev;
            bool breakPending = _wallBreakPendingByInstanceId.Contains(instanceId);
            bool pendingMoveRaise = _wallRaisePendingByInstanceId.Contains(instanceId) || HasQueuedWallMoveForTarget(instanceId);

            // Event presentation owns wall raise/break timing. Poll sync must not restart tweens
            // (especially after KO locks keep a fainted chainling on-slot while engine actives change).
            if (breakPending)
            {
                _barrierActiveByInstanceId[instanceId] = true;
                return;
            }

            if (hasBarrier)
            {
                string sourceTag = CombatWallStateUtility.ResolveLatestWallSourceTag(chainling);
                CombatWallVisual wall = view.GetWallVisual();
                bool missingVisual = wall == null;
                _wallSourceByInstanceId.TryGetValue(instanceId, out string previousSourceTag);
                bool sourceChanged = wasActive && CombatWallPresentationPlanner.ShouldRefreshArt(
                    previousSourceTag,
                    sourceTag,
                    true);

                // Only EnsureWallVisual (ApplyArt) when creating/first-activating.
                // Polling Ensure every RefreshSlotBindings tick was resetting scale and
                // cancelling DOTween raise/hit/break mid-animation.
                if (missingVisual || !wasActive || sourceChanged)
                    wall = EnsureWallVisual(view, sourceTag, chainling);

                if (CombatWallPresentationPlanner.ShouldPollRaise(hasBarrier, wasActive, pendingMoveRaise))
                {
                    // Always Raise on first activation. !wasActive already gates once-per-barrier;
                    // do NOT skip Raise when _sequenceRoutine is running — Instantiate leaves the
                    // wall active at full scale, so the old wallNeedsRaise check suppressed the
                    // DOTween grow/bounce for barriers applied during a move presentation.
                    if (wall != null)
                    {
                        wall.Raise();
                        PlayWallCue(
                            CombatWallSfxCue.Raise,
                            view,
                            null,
                            sourceTag,
                            -1,
                            0f);
                    }

                    if (_sequenceRoutine == null)
                        QueueBarrierFeedback(view, chainling, preferBarrierAnchor: true, isBroken: false);
                }
                else if (sourceChanged && wall != null)
                {
                    wall.PlayRefresh();
                    SpawnWallStatePhase(view, null, sourceTag, chainling, WallStatePhase.Raise);
                    SpawnWallStatePhase(view, null, sourceTag, chainling, WallStatePhase.Loop);
                }
                else if (missingVisual && wall != null)
                {
                    // Barrier still active but the visual was destroyed — restore with a raise.
                    wall.Raise();
                    PlayWallCue(
                        CombatWallSfxCue.Raise,
                        view,
                        null,
                        sourceTag,
                        -1,
                        0f);
                }

                _wallSourceByInstanceId[instanceId] = sourceTag ?? string.Empty;
            }
            else
            {
                if (wasActive)
                {
                    if (_sequenceRoutine != null)
                    {
                        // Damage / WallExpired handlers will break the wall at the correct beat.
                        _barrierActiveByInstanceId[instanceId] = true;
                        return;
                    }

                    PlayWallBreak(view);
                    QueueBarrierFeedback(view, null, preferBarrierAnchor: true, isBroken: true);
                    _barrierActiveByInstanceId[instanceId] = true;
                    return;
                }

                if (_sequenceRoutine == null)
                    view.ForceHideWall();
            }

            _barrierActiveByInstanceId[instanceId] = hasBarrier;
        }

        private enum WallStatePhase
        {
            Raise,
            Loop,
            Hit,
            Break,
            Expire,
        }

        private bool HasQueuedWallMoveForTarget(string instanceId)
        {
            if (string.IsNullOrEmpty(instanceId))
                return false;

            CombatState state = _stateSource != null ? _stateSource.State : null;
            foreach (CombatEvent queued in _eventQueue)
            {
                if (queued.eventType != CombatEventType.MoveUsed ||
                    !string.Equals(queued.targetId, instanceId, StringComparison.Ordinal))
                    continue;

                MoveInfo info = ResolveMoveInfo(state, queued.moveId);
                if (CombatWallStateUtility.MovePlacesBlock(info.instance) ||
                    CombatWallStateUtility.MoveDefinitionPlacesWall(info.definition))
                    return true;
            }

            return false;
        }

        private void PruneBarrierTrackingToActive(params ChainlingInstance[] activeChainlings)
        {
            if (_barrierActiveByInstanceId.Count == 0)
                return;

            var activeIds = new HashSet<string>(StringComparer.Ordinal);
            if (activeChainlings != null)
            {
                for (int i = 0; i < activeChainlings.Length; i++)
                {
                    ChainlingInstance chainling = activeChainlings[i];
                    if (chainling == null || string.IsNullOrEmpty(chainling.instanceId))
                        continue;
                    activeIds.Add(chainling.instanceId);
                }
            }

            // Presentation locks keep KO'd / swapping chainlings on-slot after engine actives change.
            // Dropping their barrier tracking makes SyncWallVisual think the wall is brand new and
            // restart Raise() every poll (scale Y flicker).
            foreach (string lockedInstanceId in _slotPresentationLockByKey.Values)
            {
                if (!string.IsNullOrEmpty(lockedInstanceId))
                    activeIds.Add(lockedInstanceId);
            }

            foreach (string pendingId in _wallBreakPendingByInstanceId)
            {
                if (!string.IsNullOrEmpty(pendingId))
                    activeIds.Add(pendingId);
            }

            if (activeIds.Count == 0)
            {
                if (_wallBreakPendingByInstanceId.Count > 0)
                    return;

                _barrierActiveByInstanceId.Clear();
                _wallSourceByInstanceId.Clear();
                ClearAllWallLoopVfx();
                return;
            }

            var keys = new List<string>(_barrierActiveByInstanceId.Keys);
            for (int i = 0; i < keys.Count; i++)
            {
                string key = keys[i];
                if (activeIds.Contains(key) || _wallBreakPendingByInstanceId.Contains(key))
                    continue;

                _barrierActiveByInstanceId.Remove(key);
                _wallSourceByInstanceId.Remove(key);
                StopWallLoopVfx(key);
            }
        }

        private void RebuildViewLookup()
        {
            _viewById.Clear();
            AddView(team0Slot0);
            AddView(team0Slot1);
            AddView(team1Slot0);
            AddView(team1Slot1);

            foreach (KeyValuePair<int, string> entry in _slotPresentationLockByKey)
            {
                int teamIndex = entry.Key / 2;
                int slotIndex = entry.Key % 2;
                Combat2ChainlingView view = GetViewBySlot(teamIndex, slotIndex);
                if (view != null && !string.IsNullOrEmpty(entry.Value))
                    _viewById[entry.Value] = view;
            }
        }

        private void AddView(Combat2ChainlingView view)
        {
            if (view == null || string.IsNullOrEmpty(view.InstanceId))
                return;

            _viewById[view.InstanceId] = view;
        }

        private void ApplySelectionPreview()
        {
            ClearAllTargeters();
            bool hasPreview = !string.IsNullOrEmpty(_previewActorId) || !string.IsNullOrEmpty(_previewTargetId);
            if (!hasPreview)
                return;

            ClearAllGroundCircles();

            if (!string.IsNullOrEmpty(_previewActorId))
            {
                var actor = GetView(_previewActorId);
                actor?.SetTargeted(true);
                actor?.GroundCircle?.SetState(CombatSlotGroundCircle.CircleState.ActiveActor);
            }

            if (!string.IsNullOrEmpty(_previewTargetId) && _previewTargetId != _previewActorId)
            {
                var target = GetView(_previewTargetId);
                target?.SetTargeted(true);
                if (target != null && target.GroundCircle != null)
                {
                    var actor = GetView(_previewActorId);
                    bool isSupport = actor != null && AreViewsOnSameTeam(actor, target);
                    target.GroundCircle.SetState(isSupport
                        ? CombatSlotGroundCircle.CircleState.TargetingSupport
                        : CombatSlotGroundCircle.CircleState.TargetingAttack);
                }
            }
        }

        private void ClearAllTargeters()
        {
            if (team0Slot0 != null) team0Slot0.SetTargeted(false);
            if (team0Slot1 != null) team0Slot1.SetTargeted(false);
            if (team1Slot0 != null) team1Slot0.SetTargeted(false);
            if (team1Slot1 != null) team1Slot1.SetTargeted(false);
        }

        private void HandleTurnActorSelected(CombatBattleController.HudSlotRef actorRef)
        {
            ClearAllGroundCircles();
            var actorView = GetViewBySlot(actorRef.teamIndex, actorRef.slotIndex);
            actorView?.GroundCircle?.SetState(CombatSlotGroundCircle.CircleState.ActiveActor);
            _pendingResetToDefault = false;
            FocusSelectionActor(actorView);
        }

        private void HandleActionTargetsResolved(CombatBattleController.HudSlotRef actorRef, List<CombatBattleController.HudSlotRef> targets)
        {
            ClearAllGroundCircles();

            if (targets == null || targets.Count == 0)
                return;

            var actorView = GetViewBySlot(actorRef.teamIndex, actorRef.slotIndex);
            actorView?.GroundCircle?.SetState(CombatSlotGroundCircle.CircleState.ActiveActor);

            for (int i = 0; i < targets.Count; i++)
            {
                var target = targets[i];
                var targetView = GetViewBySlot(target.teamIndex, target.slotIndex);
                if (targetView == null || targetView.GroundCircle == null)
                    continue;

                bool isSupport = target.teamIndex == actorRef.teamIndex;
                targetView.GroundCircle.SetState(isSupport
                    ? CombatSlotGroundCircle.CircleState.TargetingSupport
                    : CombatSlotGroundCircle.CircleState.TargetingAttack);
            }
        }

        private void HandleTurnEnded(int turnIndex)
        {
            ClearAllGroundCircles();
            _pendingResetToDefault = true;
        }

        private void HandleBattleStarted(CombatState state)
        {
            ResolveUxFallbacks();
            _wallExpirePresentedKeys.Clear();
            _wallRaisePendingByInstanceId.Clear();

            if (_battleIntroRoutine != null)
            {
                StopCoroutine(_battleIntroRoutine);
                _battleIntroRoutine = null;
            }

            CancelIntroCameraRestore();
            battleIntroController?.CancelIntro();
            CancelIntroLeadSlide(resetLeadPosition: true);
            StopBattlefieldPresentationTransition();
            RestoreBattlefieldSingleCombatLayout();
            SetSingleCombatHudLayoutActive(false);
            SetWalletFramingActive(false);
            _battlefieldFramingMode = false;
            _battlefieldLayoutMode = false;
            CacheBattleIntroSlotDefaults();
            _battleIntroRoutine = StartCoroutine(BattleIntroSequence(state));
        }

        private void HandleBattleEnded(BattleResult result)
        {
            if (_battleIntroRoutine != null)
            {
                StopCoroutine(_battleIntroRoutine);
                _battleIntroRoutine = null;
            }

            CancelIntroCameraRestore();
            battleIntroController?.CancelIntro();
            CancelIntroLeadSlide(resetLeadPosition: true);
            ClearIntroPresentation();
            var encounterOnEnd = _battleController?.CurrentEncounter;
            var uiModeOnEnd = encounterOnEnd?.EffectiveUiMode ?? CombatUiPresentationMode.Full;
            if (hideHudDuringBattleIntro && uiModeOnEnd != CombatUiPresentationMode.Hidden)
                RevealCombatUI(uiModeOnEnd);

            ClearAllGroundCircles();
            ClearPresentationLocks();
            StopBattlefieldPresentationTransition();
            ForceClearCombatPostFx();
            // Preserve the settled 1v1 visual slot placement through the victory pose.
            // The next battle start and OnDisable already restore authored positions before
            // any subsequent battle presentation can run.
            SetSingleCombatHudLayoutActive(false);
            SetWalletFramingActive(false);
            _battlefieldFramingMode = false;
            _battlefieldLayoutMode = false;
            PlayVictoryPoses(result);
            _pendingResetToDefault = false;
            FocusDefault(false);
        }

        private IEnumerator BattleIntroSequence(CombatState state)
        {
            ClearSelectionPreview();
            ClearAllTargeters();
            ClearAllGroundCircles();
            _pendingResetToDefault = false;
            FocusDefault(true);

            ResolveBossCenterPosition();
            ResetIntroLeadSlotPosition();
            RefreshSlotBindings();
            ResetActiveSlotFaintStates();

            var encounter = _battleController?.CurrentEncounter;
            var uiMode = encounter?.EffectiveUiMode ?? CombatUiPresentationMode.Full;
            if (uiMode == CombatUiPresentationMode.Hidden)
                uiStageController?.KeepAllHidden();
            else if (hideHudDuringBattleIntro)
                uiStageController?.ApplyImmediateHidden();

            HideActiveSlotVisuals();

            BattleIntroPresentationSO introPresentation = _activeIntroPresentation;
            var request = BuildBattleIntroRequest(state);
            request.vfxProfile = ResolveBattleIntroVfxProfile(introPresentation, request);
            ApplyBattleIntroCopy(state, ref request, introPresentation);
            request.allowSkip = introPresentation == null || introPresentation.allowSkipIntroMessage;
            bool useCenterStaging = CanUseCenterStaging(introPresentation);
            BattleIntroLog(useCenterStaging
                ? "Starting center-staged battle intro."
                : "Starting standard battle intro.");

            if (useCenterStaging)
            {
                // Blend into intro framing, then optional hold (battleIntroDelay) before spawn.
                yield return PlayCenterStagedBattleIntro(request, introPresentation, battleIntroDelay);
            }
            else
            {
                if (battleIntroDelay > 0f)
                {
                    BattleIntroLog($"Waiting {battleIntroDelay:0.###}s before intro.");
                    yield return new WaitForSeconds(battleIntroDelay);
                }

                yield return PlayStandardBattleIntro(request);
            }

            RefreshSlotBindings();
            SyncAllDisplayVitals();

            float effectiveWalletDelay = ResolvePostGlitchWalletDelay(introPresentation);
            BattleIntroLog($"Glitch spawn complete, waiting {effectiveWalletDelay:0.###}s before wallet reveal");
            if (effectiveWalletDelay > 0f)
                yield return new WaitForSeconds(effectiveWalletDelay);

            if (uiMode == CombatUiPresentationMode.Hidden)
            {
                BattleIntroLog("Combat UI remains hidden for this encounter.");
                uiStageController?.KeepAllHidden();
            }
            else
            {
                BattleIntroLog("Wallet reveal starting");
                RevealCombatUI(uiMode);
            }

            if (hudController != null)
                hudController.RefreshAllHud();

            BattleIntroLog("NotifyBattleIntroComplete -- action selection unlocked");
            _battleIntroRoutine = null;
            EvaluateBattlefieldFraming();
            _battleController?.NotifyBattleIntroComplete();
            ClearIntroPresentation();
        }

        private void RevealCombatUI(CombatUiPresentationMode mode)
        {
            if (uiStageController != null)
            {
                uiStageController.RevealByMode(mode);
                return;
            }
            if (hudController == null)
                return;
            if (mode == CombatUiPresentationMode.Hidden)
                hudController.SetCombatUIHidden(true);
            else
                hudController.SetBattleHudVisible(true);
        }

        private IEnumerator PlayStandardBattleIntro(BattleIntroRequest request)
        {
            yield return PlayIntroDialogue(request);
            if (_activeIntroPresentation != null)
                yield return PlayBattleIntroSpawnWithAdvance(_activeIntroPresentation.spawnAdvanceDelaySeconds, team0Slot0, team0Slot1, team1Slot0, team1Slot1);
            else
                yield return PlayBattleIntroSpawn(team0Slot0, team0Slot1, team1Slot0, team1Slot1);
        }

        private IEnumerator PlayCenterStagedBattleIntro(
            BattleIntroRequest request,
            BattleIntroPresentationSO introPresentation,
            float preSpawnHoldSeconds = 0f)
        {
            var leadView = team1Slot0;
            if (leadView == null)
                yield break;

            Transform leadTransform = leadView.transform;
            Vector3 centerLocal = ResolveLocalPositionInParentSpace(leadTransform, bossCenterPosition);

            var supportView = team1Slot1;
            bool stageSupport = supportView != null && !string.IsNullOrEmpty(supportView.InstanceId);
            if (stageSupport)
            {
                Vector3 halfSpan = introPresentation.secondaryCenterOffset * 0.5f;
                leadTransform.localPosition = centerLocal - halfSpan;
                supportView.transform.localPosition = centerLocal + halfSpan;
                _introTeam1Slot1CenterStaged = true;
                BattleIntroStagingLog(
                    $"Dual wilds staged symmetrically around center (±{halfSpan}).",
                    introPresentation);
            }
            else
            {
                leadTransform.localPosition = centerLocal;
                BattleIntroStagingLog("Enemy lead moved to center position.", introPresentation);
            }

            object introCameraSnapshot = null;
            ICombatIntroCameraController introCamera = null;
            if (introPresentation.enableIntroCameraBeat)
            {
                introCamera = ResolveIntroCameraController();
                if (introCamera != null)
                {
                    introCameraSnapshot = introCamera.CaptureIntroFraming();
                    BattleIntroStagingLog(
                        $"Blending intro camera in over {introPresentation.introCameraBlendInSeconds:0.###}s (yDelta={introPresentation.introFollowOffsetY}, z={introPresentation.introFollowOffsetZ}, pitch={introPresentation.introPitchAngle}).",
                        introPresentation);
                    yield return introCamera.BlendToIntroFraming(
                        introPresentation.introFollowOffsetY,
                        introPresentation.introFollowOffsetZ,
                        introPresentation.introPitchAngle,
                        introPresentation.introCameraBlendInSeconds);
                }
                else
                {
                    BattleIntroLog("Intro camera beat requested but no ICombatIntroCameraController was found.");
                }
            }

            // Optional hold after blend-in reaches intro framing, before spawn FX.
            if (preSpawnHoldSeconds > 0f)
            {
                BattleIntroLog($"Holding intro framing for {preSpawnHoldSeconds:0.###}s before spawn.");
                yield return new WaitForSeconds(preSpawnHoldSeconds);
            }

            yield return PlayBattleIntroSpawnWithAdvance(
                introPresentation.spawnAdvanceDelaySeconds,
                leadView,
                _introTeam1Slot1CenterStaged ? supportView : null);

            if (!introPresentation.delayPlayerPartyUntilAfterDialogue)
            {
                BattleIntroStagingLog("Spawning player lineup before dialogue.", introPresentation);
                yield return PlayBattleIntroSpawnWithAdvance(
                    introPresentation.ResolvePlayerSpawnAdvanceDelay(),
                    team0Slot0,
                    team0Slot1);
            }

            // Pull back to BattleProfile while the intro message plays (not after it).
            CancelIntroCameraRestore();
            if (introCamera != null && introCameraSnapshot != null)
            {
                BattleIntroStagingLog(
                    $"Restoring battle camera over {introPresentation.introCameraBlendOutSeconds:0.###}s during intro message.",
                    introPresentation);
                _introCameraRestoreRoutine = StartCoroutine(introCamera.RestoreIntroFraming(
                    introCameraSnapshot,
                    introPresentation.introCameraBlendOutSeconds));
            }

            yield return PlayIntroDialogue(request);

            if (_introCameraRestoreRoutine != null)
            {
                yield return _introCameraRestoreRoutine;
                _introCameraRestoreRoutine = null;
            }

            if (introPresentation.postDialogueDelay > 0f)
            {
                BattleIntroStagingLog($"Waiting {introPresentation.postDialogueDelay:0.###}s after dialogue before slide.", introPresentation);
                yield return new WaitForSeconds(introPresentation.postDialogueDelay);
            }

            yield return SlideCenterStagedLeadToSlot(introPresentation);

            if (introPresentation.delayPlayerPartyUntilAfterDialogue)
            {
                BattleIntroStagingLog("Spawning player lineup after dialogue.", introPresentation);
                yield return PlayBattleIntroSpawnWithAdvance(
                    introPresentation.ResolvePlayerSpawnAdvanceDelay(),
                    team0Slot0,
                    team0Slot1);
            }
        }

        private IEnumerator PlayIntroDialogue(BattleIntroRequest request)
        {
            BattleIntroLog("Playing intro dialogue.");

            if (battleIntroController != null)
            {
                if (request.isBoss)
                {
                    yield return battleIntroController.PlayExternalIntro(request);
                }
                else if (!string.IsNullOrWhiteSpace(request.messageText))
                {
                    yield return battleIntroController.PlayLocalIntro(request);
                }
            }

            BattleIntroLog("Intro dialogue complete.");

            if (!this || !gameObject.activeInHierarchy)
                Debug.LogError("[Combat2BattlefieldPresenter] Presenter inactive after intro yield!", this);
        }

        private bool CanUseCenterStaging(BattleIntroPresentationSO introPresentation)
        {
            if (introPresentation == null || !introPresentation.useCenterStaging)
                return false;

            ResolveBossCenterPosition();
            if (bossCenterPosition == null)
            {
                Debug.LogWarning("[BattleIntro] Center staging requested but Boss_CenterPosition was not found. Falling back to standard intro.", this);
                return false;
            }

            if (team1Slot0 == null || string.IsNullOrEmpty(team1Slot0.InstanceId))
            {
                Debug.LogWarning("[BattleIntro] Center staging requested but the enemy lead slot is unavailable. Falling back to standard intro.", this);
                return false;
            }

            if (!_hasTeam1Slot0DefaultLocalPosition)
            {
                Debug.LogWarning("[BattleIntro] Center staging requested but the enemy lead default slot position was not cached. Falling back to standard intro.", this);
                return false;
            }

            if (team1Slot1 != null && !string.IsNullOrEmpty(team1Slot1.InstanceId) && !_hasTeam1Slot1DefaultLocalPosition)
            {
                Debug.LogWarning("[BattleIntro] Center staging requested but the enemy support default slot position was not cached. Falling back to standard intro.", this);
                return false;
            }

            return true;
        }

        private float ResolvePostGlitchWalletDelay(BattleIntroPresentationSO introPresentation)
        {
            if (introPresentation != null && introPresentation.postGlitchWalletDelay >= 0f)
                return introPresentation.postGlitchWalletDelay;

            return postGlitchWalletDelay;
        }

        private BattleIntroVfxProfileSO ResolveBattleIntroVfxProfile(BattleIntroPresentationSO introPresentation, BattleIntroRequest request)
        {
            return introPresentation != null ? introPresentation.ResolveVfxProfile(request) : null;
        }

        private IEnumerator SlideCenterStagedLeadToSlot(BattleIntroPresentationSO introPresentation)
        {
            if (team1Slot0 == null || !_hasTeam1Slot0DefaultLocalPosition)
                yield break;

            CancelIntroLeadSlide(resetLeadPosition: false);

            bool slideSupport = _introTeam1Slot1CenterStaged && team1Slot1 != null && _hasTeam1Slot1DefaultLocalPosition;

            if (introPresentation.centerSlideDuration <= 0f)
            {
                team1Slot0.transform.localPosition = _team1Slot0DefaultLocalPosition;
                if (slideSupport)
                    team1Slot1.transform.localPosition = _team1Slot1DefaultLocalPosition;
                _introTeam1Slot1CenterStaged = false;
                BattleIntroStagingLog("Enemy lead snapped back to slot.", introPresentation);
                yield break;
            }

            BattleIntroStagingLog(
                $"Sliding enemy lead back to slot over {introPresentation.centerSlideDuration:0.###}s ({introPresentation.centerSlideEase}).",
                introPresentation);

            var sequence = DOTween.Sequence();
            sequence.Join(team1Slot0.transform
                .DOLocalMove(_team1Slot0DefaultLocalPosition, introPresentation.centerSlideDuration)
                .SetEase(introPresentation.centerSlideEase));

            if (slideSupport)
            {
                sequence.Join(team1Slot1.transform
                    .DOLocalMove(_team1Slot1DefaultLocalPosition, introPresentation.centerSlideDuration)
                    .SetEase(introPresentation.centerSlideEase));
            }

            _introLeadSlideTween = sequence;
            yield return sequence.WaitForCompletion();
            _introLeadSlideTween = null;
            _introTeam1Slot1CenterStaged = false;
        }

        private void ResolveBossCenterPosition()
        {
            if (bossCenterPosition != null)
                return;

            Transform found = transform.Find("Boss_CenterPosition");
            if (found == null)
            {
                Transform battlefieldRoot = team1Slot0 != null ? team1Slot0.transform.parent : null;
                if (battlefieldRoot == null && team0Slot0 != null)
                    battlefieldRoot = team0Slot0.transform.parent;

                if (battlefieldRoot != null)
                    found = battlefieldRoot.Find("Boss_CenterPosition");
            }

            if (found == null)
            {
                var centerObject = GameObject.Find("Boss_CenterPosition");
                if (centerObject != null)
                    found = centerObject.transform;
            }

            if (found != null)
                bossCenterPosition = found;
        }

        private void CacheBattleIntroSlotDefaults()
        {
            if (team1Slot0 != null)
            {
                _team1Slot0DefaultLocalPosition = team1Slot0.transform.localPosition;
                _hasTeam1Slot0DefaultLocalPosition = true;
            }
            else
            {
                _hasTeam1Slot0DefaultLocalPosition = false;
            }

            if (team1Slot1 != null)
            {
                _team1Slot1DefaultLocalPosition = team1Slot1.transform.localPosition;
                _hasTeam1Slot1DefaultLocalPosition = true;
            }
            else
            {
                _hasTeam1Slot1DefaultLocalPosition = false;
            }
        }

        private void ResetIntroLeadSlotPosition()
        {
            CancelIntroLeadSlide(resetLeadPosition: true);
        }

        private void CancelIntroLeadSlide(bool resetLeadPosition)
        {
            if (_introLeadSlideTween != null)
            {
                _introLeadSlideTween.Kill();
                _introLeadSlideTween = null;
            }

            if (resetLeadPosition)
            {
                if (team1Slot0 != null && _hasTeam1Slot0DefaultLocalPosition)
                    team1Slot0.transform.localPosition = _team1Slot0DefaultLocalPosition;
                if (team1Slot1 != null && _hasTeam1Slot1DefaultLocalPosition)
                    team1Slot1.transform.localPosition = _team1Slot1DefaultLocalPosition;
                _introTeam1Slot1CenterStaged = false;
            }
        }

        private static Vector3 ResolveLocalPositionInParentSpace(Transform targetTransform, Transform sourceTransform)
        {
            if (targetTransform == null || sourceTransform == null)
                return Vector3.zero;

            Transform parent = targetTransform.parent;
            if (parent == null)
                return sourceTransform.position;

            return parent.InverseTransformPoint(sourceTransform.position);
        }

        private void CancelIntroCameraRestore()
        {
            if (_introCameraRestoreRoutine == null)
                return;

            StopCoroutine(_introCameraRestoreRoutine);
            _introCameraRestoreRoutine = null;
        }

        private void BattleIntroLog(string message)
        {
            if (enableBattleIntroDebugLogs)
                Debug.Log($"[BattleIntro] {message}", this);
        }

        private void BattleIntroStagingLog(string message, BattleIntroPresentationSO introPresentation)
        {
            if (introPresentation != null && introPresentation.enableDebugLogs)
                Debug.Log($"[BattleIntro:Staging] {message}", this);
        }

        private BattleIntroRequest BuildBattleIntroRequest(CombatState state)
        {
            var encounter = _battleController != null ? _battleController.CurrentEncounter : null;
            var request = new BattleIntroRequest
            {
                isPvP = _battleController != null && _battleController.IsPvP,
                difficulty = encounter != null ? encounter.difficulty : EncounterDifficulty.Standard,
                opponentTeamName = !string.IsNullOrWhiteSpace(encounter?.teamName)
                    ? encounter.teamName
                    : state?.team1?.teamName,
            };

            BattleIntroRequestContextUtility.ApplyOpponentContext(ref request, state);
            request.isBoss = request.difficulty == EncounterDifficulty.Boss;
            request.allowSkip = true;
            return request;
        }

        private void ApplyBattleIntroCopy(
            CombatState state,
            ref BattleIntroRequest request,
            BattleIntroPresentationSO introPresentation)
        {
            BattleIntroCopyConfigSO copyConfig = introPresentation != null
                ? introPresentation.ResolveCopyConfig()
                : BalanceConfigRuntime.BattleIntroCopy;
            request.copyConfig = copyConfig;

            Color accent = request.vfxProfile != null
                ? request.vfxProfile.accentColor
                : new Color(1f, 0.78f, 0.22f, 1f);

            bool isWild = string.Equals(request.opponentTeamName, "Wild", StringComparison.OrdinalIgnoreCase);
            List<BattleIntroCopySubject> subjects = GetActiveOpponentSubjects(state);
            BattleIntroCopyResult result = WildEncounterIntroFormatter.Format(
                copyConfig,
                subjects,
                request.isPvP,
                isWild,
                request.opponentTeamName,
                accent);

            request.messageText = result != null ? result.richText : string.Empty;
            request.copySpans = result != null && result.spans != null
                ? result.spans.ToArray()
                : Array.Empty<BattleIntroCopySpan>();
        }

        private ICombatIntroCameraController ResolveIntroCameraController()
        {
            if (combatCameraController is ICombatIntroCameraController typed)
                return typed;

            if (combatCameraController == null)
                combatCameraController = FindCameraController();

            if (combatCameraController is ICombatIntroCameraController fromAssigned)
                return fromAssigned;

            return FindCameraController() as ICombatIntroCameraController;
        }

        private List<BattleIntroCopySubject> GetActiveOpponentSubjects(CombatState state)
        {
            var subjects = new List<BattleIntroCopySubject>(2);
            if (state?.team1 == null)
                return subjects;

            for (int slot = 0; slot < 2; slot++)
            {
                ChainlingInstance opponent = state.team1.GetActiveInSlot(slot);
                if (opponent == null || !opponent.isAlive)
                    continue;

                subjects.Add(BattleIntroCopySubject.FromChainling(opponent));
            }

            return subjects;
        }

        private void HideActiveSlotVisuals()
        {
            HideSlotForBattleIntro(team0Slot0);
            HideSlotForBattleIntro(team0Slot1);
            HideSlotForBattleIntro(team1Slot0);
            HideSlotForBattleIntro(team1Slot1);
        }

        private void HideSlotForBattleIntro(Combat2ChainlingView view)
        {
            if (view != null && !string.IsNullOrEmpty(view.InstanceId))
                view.HideForBattleIntro();
        }

        private IEnumerator PlayBattleIntroSpawn(params Combat2ChainlingView[] views)
        {
            if (!useBattleIntroGlitch)
            {
                if (views != null)
                {
                    for (int i = 0; i < views.Length; i++)
                        ResetActiveSlotFaintState(views[i]);
                }

                yield break;
            }

            float maxDuration = 0f;
            if (views != null)
            {
                for (int i = 0; i < views.Length; i++)
                    maxDuration = Mathf.Max(maxDuration, StartBattleIntroSpawn(views[i]));
            }

            if (maxDuration > 0f)
                yield return new WaitForSeconds(maxDuration);
        }

        private IEnumerator PlayBattleIntroSpawnWithAdvance(float advanceDelay, params Combat2ChainlingView[] views)
        {
            if (views == null || views.Length == 0)
                yield break;

            float delay = Mathf.Max(0f, advanceDelay);
            for (int i = 0; i < views.Length; i++)
            {
                Combat2ChainlingView view = views[i];
                if (view == null || string.IsNullOrEmpty(view.InstanceId))
                    continue;

                if (useBattleIntroGlitch)
                    StartBattleIntroSpawn(view);
                else
                    ResetActiveSlotFaintState(view);

                if (delay > 0f)
                    yield return new WaitForSeconds(delay);
            }
        }

        private float StartBattleIntroSpawn(Combat2ChainlingView view)
        {
            if (view == null || string.IsNullOrEmpty(view.InstanceId))
                return 0f;

            ICombatSfxOrchestrator sfx = CombatSfx;
            return view.PlaySpawnAnimation(() => sfx?.PlaySpawn(view));
        }

        private void ResetActiveSlotFaintStates()
        {
            ResetActiveSlotFaintState(team0Slot0);
            ResetActiveSlotFaintState(team0Slot1);
            ResetActiveSlotFaintState(team1Slot0);
            ResetActiveSlotFaintState(team1Slot1);
        }

        private static void ResetActiveSlotFaintState(Combat2ChainlingView view)
        {
            if (view == null || string.IsNullOrEmpty(view.InstanceId))
                return;

            if (view.IsTransitionOutActive)
                return;

            view.ResetFaintState();
        }

        private Combat2ChainlingView GetViewBySlot(int teamIndex, int slotIndex)
        {
            if (teamIndex == 0)
                return slotIndex == 0 ? team0Slot0 : slotIndex == 1 ? team0Slot1 : null;
            if (teamIndex == 1)
                return slotIndex == 0 ? team1Slot0 : slotIndex == 1 ? team1Slot1 : null;
            return null;
        }

        /// <summary>
        /// World position between team 0 slot roots (for 3D flee SFX). Returns zero if no slots.
        /// </summary>
        public Vector3 GetPlayerTeamFleeAudioWorldPosition()
        {
            int count = 0;
            Vector3 sum = Vector3.zero;
            if (team0Slot0 != null)
            {
                sum += team0Slot0.transform.position;
                count++;
            }

            if (team0Slot1 != null)
            {
                sum += team0Slot1.transform.position;
                count++;
            }

            return count > 0 ? sum / count : Vector3.zero;
        }

        /// <summary>
        /// Plays faint/run-away animations on visible player (team 0) chainling views.
        /// Returns the longest reported duration so callers can wait before ending the battle.
        /// </summary>
        public float PlayFleePresentationForPlayerTeam()
        {
            float maxDuration = 0f;
            maxDuration = Mathf.Max(maxDuration, TryPlayFleeAnimationOnSlot(team0Slot0));
            maxDuration = Mathf.Max(maxDuration, TryPlayFleeAnimationOnSlot(team0Slot1));
            return maxDuration;
        }

        /// <summary>
        /// Plays the targeted wild's run-off, then clears that slot (bench replacement binds after).
        /// Other living wilds stay on the field.
        /// </summary>
        public void PresentCaptureTargetRemoved(string instanceId)
        {
            if (string.IsNullOrEmpty(instanceId))
            {
                RefreshSlotBindings();
                return;
            }

            Combat2ChainlingView view = GetView(instanceId);
            if (!TryResolveSlot(view, out int teamIndex, out int slotIndex) &&
                TryResolveTeam1SlotByInstanceId(instanceId, out teamIndex, out slotIndex))
            {
                view = GetViewBySlot(teamIndex, slotIndex);
            }

            if (teamIndex >= 0)
                LockSlotPresentation(teamIndex, slotIndex, instanceId);

            float duration = 0f;
            if (view != null && view.IsHiddenForCaptureAttempt)
                view.ClearCaptureAttemptHidden();
            else if (view != null)
                duration = view.PlaySwapOutTransition();
            StartCoroutine(ReleaseCaptureRemovalPresentation(teamIndex, slotIndex, duration));
        }

        private IEnumerator ReleaseCaptureRemovalPresentation(int teamIndex, int slotIndex, float duration)
        {
            if (duration > 0f)
                yield return new WaitForSeconds(duration);
            if (teamIndex >= 0)
                UnlockSlotPresentation(teamIndex, slotIndex);
            RefreshSlotBindings();
        }

        private bool TryResolveSlot(Combat2ChainlingView view, out int teamIndex, out int slotIndex)
        {
            teamIndex = -1;
            slotIndex = -1;
            if (view == null)
                return false;
            if (view == team0Slot0) { teamIndex = 0; slotIndex = 0; return true; }
            if (view == team0Slot1) { teamIndex = 0; slotIndex = 1; return true; }
            if (view == team1Slot0) { teamIndex = 1; slotIndex = 0; return true; }
            if (view == team1Slot1) { teamIndex = 1; slotIndex = 1; return true; }
            return false;
        }

        private bool TryResolveTeam1SlotByInstanceId(string instanceId, out int teamIndex, out int slotIndex)
        {
            teamIndex = 1;
            slotIndex = -1;
            if (team1Slot0 != null && string.Equals(team1Slot0.InstanceId, instanceId, StringComparison.Ordinal))
            {
                slotIndex = 0;
                return true;
            }
            if (team1Slot1 != null && string.Equals(team1Slot1.InstanceId, instanceId, StringComparison.Ordinal))
            {
                slotIndex = 1;
                return true;
            }
            teamIndex = -1;
            return false;
        }

        private static float TryPlayFleeAnimationOnSlot(Combat2ChainlingView view)
        {
            if (view == null)
                return 0f;

            if (view.Visual == null || !view.Visual.gameObject.activeInHierarchy)
                return 0f;

            return view.PlaySwapOutTransition();
        }

        private void ClearAllGroundCircles()
        {
            SetGroundCircleState(team0Slot0, CombatSlotGroundCircle.CircleState.Off);
            SetGroundCircleState(team0Slot1, CombatSlotGroundCircle.CircleState.Off);
            SetGroundCircleState(team1Slot0, CombatSlotGroundCircle.CircleState.Off);
            SetGroundCircleState(team1Slot1, CombatSlotGroundCircle.CircleState.Off);
            ClearAllActionHighlights();
        }

        private static void SetGroundCircleState(Combat2ChainlingView view, CombatSlotGroundCircle.CircleState state)
        {
            if (view != null && view.GroundCircle != null)
                view.GroundCircle.SetState(state);
        }

        private bool AreViewsOnSameTeam(Combat2ChainlingView first, Combat2ChainlingView second)
        {
            if (first == null || second == null)
                return false;

            bool firstTeam0 = first == team0Slot0 || first == team0Slot1;
            bool firstTeam1 = first == team1Slot0 || first == team1Slot1;
            bool secondTeam0 = second == team0Slot0 || second == team0Slot1;
            bool secondTeam1 = second == team1Slot0 || second == team1Slot1;

            return (firstTeam0 && secondTeam0) || (firstTeam1 && secondTeam1);
        }

        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â
        // Event Queue
        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â

        /// <summary>
        /// Immediately poll the event log and enqueue any new events. Called by the controller
        /// after FireNewEvents so the presenter has events in queue before WaitUntil(IsEventQueueIdle).
        /// </summary>
        public void EnqueueNewEventsImmediate()
        {
            EnqueueNewEvents();
        }

        private void EnqueueNewEvents()
        {
            if (_stateSource == null)
                return;

            var log = _stateSource.EventLog;
            if (log == null)
            {
                _lastEventCount = -1;
                return;
            }

            if (_lastEventCount < 0)
                _lastEventCount = log.EventCount;

            var events = log.Events;
            ApplyPresentationLocksForNewEvents(events, _lastEventCount);
            for (int i = _lastEventCount; i < events.Count; i++)
                _eventQueue.Enqueue(events[i]);

            _lastEventCount = log.EventCount;

            if (_sequenceRoutine == null && _eventQueue.Count > 0)
                _sequenceRoutine = StartCoroutine(PlayEventSequence());
        }

        private IEnumerator PlayEventSequence()
        {
            try
            {
                while (_eventQueue.Count > 0 || _feedbackQueue.Count > 0 || HasPendingNarration)
                {
                    if (TryActivateNextNarrationPlan())
                        yield return PresentActiveNarrationAction();

                    if (_feedbackQueue.Count > 0)
                    {
                        yield return _feedbackQueue.Dequeue();
                        continue;
                    }

                    if (_eventQueue.Count == 0)
                    {
                        yield return CompleteActiveNarrationWithoutEvent();
                        continue;
                    }

                    var evt = _eventQueue.Dequeue();
                    NotePresentedCombatEvent(evt);
                    TryPlayOutcomeBatchAudio(evt);
                    yield return PlayEvent(evt);
                    yield return CompleteNarrationAfterEvent(evt);
                }
            }
            finally
            {
                _sequenceRoutine = null;
                if (_pendingResetToDefault)
                {
                    _pendingResetToDefault = false;
                    FocusDefault(false);
                }

                try
                {
                    HideNarrationBox();
                }
                catch (Exception hideEx)
                {
                    Debug.LogWarning($"[Combat2Presenter] HideNarrationBox failed: {hideEx.Message}");
                }

                EvaluateBattlefieldFraming();
                OnEventQueueIdle?.Invoke();
            }
        }

        private bool IsSingleCombatFraming(CombatState state)
        {
            return state != null && state.team0 != null && state.team1 != null &&
                   state.team0.CountAliveActiveSlots() == 1 &&
                   state.team1.CountAliveActiveSlots() == 1;
        }

        private void EvaluateBattlefieldFraming()
        {
            if (_battleIntroRoutine != null ||
                _sequenceRoutine != null ||
                _eventQueue.Count > 0 ||
                _feedbackQueue.Count > 0 ||
                HasPendingNarration ||
                _introCameraRestoreRoutine != null ||
                _slotPresentationLockByKey.Count > 0)
                return;

            CombatState state = _stateSource != null ? _stateSource.State : null;
            if (_battlefieldSingleCombatLayout is UnityEngine.Object layoutObject && layoutObject == null)
                _battlefieldSingleCombatLayout = null;
            bool absoluteSingleCombat = IsSingleCombatFraming(state);
            bool desiredMode = absoluteSingleCombat &&
                               battlefieldFraming != null &&
                               battlefieldFraming.enableSingleCombatFraming;
            bool desiredLayoutMode = absoluteSingleCombat &&
                                     _battlefieldSingleCombatLayout != null &&
                                     _battlefieldSingleCombatLayout.IsEnabled;
            var framingCamera = combatCameraController as ICombatBattlefieldFramingController;
            bool cameraTransitionNeeded = desiredMode != _battlefieldFramingMode;
            bool layoutTransitionNeeded = desiredLayoutMode != _battlefieldLayoutMode;
            if (!cameraTransitionNeeded && !layoutTransitionNeeded)
                return;

            StopBattlefieldPresentationTransition();

            _battlefieldFramingMode = desiredMode;
            _battlefieldLayoutMode = desiredLayoutMode;
            SetSingleCombatHudLayoutActive(desiredLayoutMode);

            if (!cameraTransitionNeeded && !layoutTransitionNeeded)
                return;

            float blendSeconds = battlefieldFraming != null ? battlefieldFraming.framingBlendSeconds : 0f;
            float yDelta = desiredMode && battlefieldFraming != null
                ? battlefieldFraming.singleCombatFollowOffsetYDelta
                : 0f;
            float z = desiredMode && battlefieldFraming != null
                ? battlefieldFraming.singleCombatFollowOffsetZ
                : float.NaN;
            _battlefieldFramingRoutine = StartCoroutine(BlendBattlefieldPresentation(
                cameraTransitionNeeded ? framingCamera : null,
                layoutTransitionNeeded ? _battlefieldSingleCombatLayout : null,
                state,
                desiredLayoutMode,
                yDelta,
                z,
                blendSeconds));
        }

        private void ResolveBattlefieldSingleCombatLayoutController()
        {
            if (battlefieldSingleCombatLayoutController is ICombatBattlefieldSingleCombatLayoutController assigned)
                _battlefieldSingleCombatLayout = assigned;
            else
            {
                var discovered = FindObjectOfType<Combat2BattlefieldSingleCombatLayoutController>(true);
                if (discovered == null)
                    discovered = gameObject.AddComponent<Combat2BattlefieldSingleCombatLayoutController>();
                battlefieldSingleCombatLayoutController = discovered;
                _battlefieldSingleCombatLayout = discovered;
            }

            if (_battlefieldSingleCombatLayout is Combat2BattlefieldSingleCombatLayoutController concrete)
            {
                concrete.SetSlotRoots(team0Slot0, team0Slot1, team1Slot0, team1Slot1);
            }
        }

        private void ResolveSingleCombatHudLayoutController()
        {
            _singleCombatHudLayout = hudController as ICombatSingleCombatHudLayoutController;
            if (_singleCombatHudLayout != null)
                return;

            var discovered = FindObjectOfType<CombatBattleUGUIController>(true);
            if (discovered != null)
            {
                hudController = discovered;
                _singleCombatHudLayout = discovered;
            }
        }

        private void ResolveWalletFramingOffsetController()
        {
            if (hardwareWalletOffsetController is ICombatBattlefieldFramingOffsetController assigned)
            {
                _walletFramingOffset = assigned;
                return;
            }

            var discovered = FindObjectOfType<CombatHardwareWalletOffsetController>(true);
            if (discovered != null)
            {
                _walletFramingOffset = discovered;
                return;
            }

            // Keep this runtime-only so prefab import/serialization remains untouched.
            Transform walletRoot = CombatWalletRootResolver.Resolve();
            if (walletRoot != null)
                _walletFramingOffset = walletRoot.GetComponent<CombatHardwareWalletOffsetController>() ??
                    walletRoot.gameObject.AddComponent<CombatHardwareWalletOffsetController>();
        }

        private void SetWalletFramingActive(bool active)
        {
            if (_walletFramingOffset is UnityEngine.Object unityObject && unityObject == null)
            {
                _walletFramingOffset = null;
                return;
            }

            _walletFramingOffset?.SetBattlefieldFramingActive(active);
        }

        private void SetSingleCombatHudLayoutActive(bool active)
        {
            if (_singleCombatHudLayout is UnityEngine.Object unityObject && unityObject == null)
            {
                _singleCombatHudLayout = null;
                return;
            }

            _singleCombatHudLayout?.SetSingleCombatLayoutActive(active);
        }

        private void RestoreBattlefieldSingleCombatLayout()
        {
            if (_battlefieldSingleCombatLayout is UnityEngine.Object unityObject && unityObject == null)
            {
                _battlefieldSingleCombatLayout = null;
                return;
            }

            _battlefieldSingleCombatLayout?.RestoreAuthoredLayout();
        }

        private void StopBattlefieldPresentationTransition()
        {
            if (_battlefieldFramingRoutine != null)
                StopCoroutine(_battlefieldFramingRoutine);
            if (_cameraBlendOperation != null)
                StopCoroutine(_cameraBlendOperation);
            if (_layoutBlendOperation != null)
                StopCoroutine(_layoutBlendOperation);
            if (_walletBlendOperation != null)
                StopCoroutine(_walletBlendOperation);

            _battlefieldFramingRoutine = null;
            _cameraBlendOperation = null;
            _layoutBlendOperation = null;
            _walletBlendOperation = null;
        }

        private IEnumerator BlendBattlefieldPresentation(
            ICombatBattlefieldFramingController framingCamera,
            ICombatBattlefieldSingleCombatLayoutController layoutController,
            CombatState state,
            bool layoutActive,
            float yDelta,
            float z,
            float blendSeconds)
        {
            bool cameraComplete = framingCamera == null;
            bool layoutComplete = layoutController == null;
            if (_walletFramingOffset is UnityEngine.Object walletObject && walletObject == null)
                _walletFramingOffset = null;
            bool walletComplete = _walletFramingOffset == null;

            if (!cameraComplete)
            {
                _cameraBlendOperation = StartCoroutine(BlendCameraOperation(
                    framingCamera,
                    yDelta,
                    z,
                    blendSeconds,
                    () => cameraComplete = true));
            }

            if (!layoutComplete)
            {
                _layoutBlendOperation = StartCoroutine(BlendLayoutOperation(
                    layoutController,
                    state,
                    layoutActive,
                    blendSeconds,
                    () => layoutComplete = true));
            }

            if (!walletComplete)
            {
                _walletBlendOperation = StartCoroutine(BlendWalletOperation(
                    _walletFramingOffset,
                    _battlefieldFramingMode,
                    blendSeconds,
                    () => walletComplete = true));
            }

            while (!cameraComplete || !layoutComplete || !walletComplete)
                yield return null;

            _cameraBlendOperation = null;
            _layoutBlendOperation = null;
            _walletBlendOperation = null;
            _battlefieldFramingRoutine = null;

            // Reassert the wallet mode after the camera/layout blend. The wallet has
            // independent reveal/hide and selector tweens that may have written its
            // root while this transition was running; the offset controller applies
            // only the settled 1v1 delta and preserves those animation-authored values.
            SetWalletFramingActive(_battlefieldFramingMode);
        }

        private static IEnumerator BlendCameraOperation(
            ICombatBattlefieldFramingController framingCamera,
            float yDelta,
            float z,
            float blendSeconds,
            System.Action onComplete)
        {
            yield return framingCamera.SoftBlendBattlefieldFollowOffset(yDelta, z, blendSeconds);
            onComplete?.Invoke();
        }

        private static IEnumerator BlendLayoutOperation(
            ICombatBattlefieldSingleCombatLayoutController layoutController,
            CombatState state,
            bool active,
            float blendSeconds,
            System.Action onComplete)
        {
            yield return layoutController.BlendSingleCombatLayout(state, active, blendSeconds);
            onComplete?.Invoke();
        }

        private static IEnumerator BlendWalletOperation(
            ICombatBattlefieldFramingOffsetController walletController,
            bool active,
            float blendSeconds,
            System.Action onComplete)
        {
            yield return walletController.BlendBattlefieldFraming(active, blendSeconds);
            onComplete?.Invoke();
        }

        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â
        // Event Dispatch
        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â

        private IEnumerator PlayEvent(CombatEvent evt)
        {
            switch (evt.eventType)
            {
                case CombatEventType.MoveUsed:
                    yield return HandleMoveUsed(evt);
                    yield break;
                case CombatEventType.ActionFailed:
                    yield return HandleActionFailed(evt);
                    yield break;
                case CombatEventType.PassiveTriggered:
                    yield return HandlePassiveTriggered(evt);
                    yield break;
                case CombatEventType.PassiveDamageDealt:
                    yield return HandlePassiveDamageDealt(evt);
                    yield break;
                case CombatEventType.SwapPerformed:
                    yield return HandleSwapPerformed(evt);
                    yield break;
                case CombatEventType.DamageDealt:
                case CombatEventType.HPDotDamage:
                case CombatEventType.HPRecoilDamage:
                    yield return HandleDamage(evt);
                    yield break;
                case CombatEventType.HealApplied:
                    yield return HandleHeal(evt);
                    yield break;
                case CombatEventType.Miss:
                    yield return HandleMiss(evt);
                    yield break;
                case CombatEventType.Dodge:
                    yield return HandleDodge(evt);
                    yield break;
                case CombatEventType.BarrierBlocked:
                    yield return HandleBarrierBlocked(evt);
                    yield break;
                case CombatEventType.WallExpired:
                    yield return HandleWallExpired(evt);
                    yield break;
                case CombatEventType.ChainlingKO:
                    yield return HandleChainlingKO(evt);
                    yield break;
                case CombatEventType.AutoSwapIn:
                    yield return HandleAutoSwapIn(evt);
                    yield break;
                case CombatEventType.StatusApplied:
                case CombatEventType.ChemistryStatusApplied:
                case CombatEventType.StatusRefreshed:
                case CombatEventType.StatusImmune:
                case CombatEventType.StatusCleansed:
                    yield return HandleStatusFeedback(evt);
                    yield break;
                case CombatEventType.StageChanged:
                    yield return HandleStageChanged(evt);
                    yield break;
                case CombatEventType.TurnStart:
                    yield return HandleTurnStartBeat();
                    yield break;
                case CombatEventType.APGained:
                case CombatEventType.APSpent:
                case CombatEventType.APRefunded:
                case CombatEventType.APStolen:
                case CombatEventType.APGainBlocked:
                case CombatEventType.APCapped:
                    if (TryBuildTurnStartApBatch(evt, out List<CombatEvent> turnStartApBatch))
                    {
                        yield return HandleTurnStartApGrantBatch(turnStartApBatch);
                        yield break;
                    }

                    yield return HandleApFeedback(evt);
                    yield break;
                case CombatEventType.ModifierTriggered:
                    yield return HandleModifierTriggered(evt);
                    yield break;
            }

            yield return null;
        }

        private IEnumerator HandleActionFailed(CombatEvent evt)
        {
            var sourceView = GetView(evt.sourceId);
            if (sourceView != null)
            {
                FocusActionSource(sourceView);
                SpawnCategoryPopup(sourceView, CombatPopupCategory.NoAp, "NO AP");
                float duration = sourceView.PlayNoActionNudge();
                float hold = ResolveNoApHold();
                float waitDuration = Mathf.Max(duration, hold);
                if (waitDuration > 0f)
                    yield return new WaitForSeconds(waitDuration);
            }
        }

        private IEnumerator HandlePassiveTriggered(CombatEvent evt)
        {
            var sourceView = GetView(evt.sourceId);
            if (sourceView != null)
            {
                FocusActionSource(sourceView);
                string label = ResolvePassiveTriggerLabel(evt);
                SpawnCategoryPopup(sourceView, ResolvePassiveTriggerCategory(evt), label);
                float hold = ResolvePassiveLabelHold();
                if (hold > 0f)
                    yield return new WaitForSeconds(hold);
            }
        }

        private IEnumerator HandlePassiveDamageDealt(CombatEvent evt)
        {
            var sourceView = GetView(evt.sourceId);
            var targetView = GetView(evt.targetId);
            if (sourceView != null)
            {
                FocusActionSource(sourceView);
                string label = ResolvePassiveTriggerLabel(evt);
                SpawnCategoryPopup(sourceView, ResolvePassiveTriggerCategory(evt), label);
            }
            if (targetView != null)
            {
                float duration = targetView.PlayHitReaction(ResolveKnockbackSource(sourceView));
                SpawnCategoryAmount(targetView, CombatPopupCategory.Damage, evt.intValue);
                float hold = Mathf.Max(duration, ResolveDamageHold());
                if (hold > 0f)
                    yield return new WaitForSeconds(hold);
            }
            else
            {
                float hold = ResolvePassiveLabelHold();
                if (hold > 0f)
                    yield return new WaitForSeconds(hold);
            }
        }

        /// <summary>
        /// Plays the shared transition-out animation for the KO'd chainling.
        /// </summary>
        private IEnumerator HandleChainlingKO(CombatEvent evt)
        {
            var faintedView = ResolvePresentationView(evt.targetId, evt.intValue2, evt.intValue);
            if (faintedView == null)
            {
                Debug.Log($"[Combat2Presenter] Chainling KO: {evt.targetId} — no view found, skipping animation.");
                yield break;
            }

            FocusImpactTarget(faintedView, false);
            if (faintPostImpactDelay > 0f)
                yield return new WaitForSeconds(ResolveFaintPostImpactDelay());

            if (enableHpDebugLogs)
                Debug.Log($"[Combat2Presenter] Chainling KO: {evt.targetId} — playing faint animation.");

            // Mitigated walls can still be tracked on a KO'd chainling; clear before faint/swap.
            if (faintedView.GetWallVisual() != null)
                ClearWallTracking(faintedView);

            SpawnCategoryPopup(faintedView, CombatPopupCategory.Ko, "KO");

            float duration = faintedView.PlayKoTransition();
            if (IsDirectAttackKo(evt))
            {
                var attackerView = ResolvePresentationView(evt.sourceId, -1, -1);
                if (attackerView != null && attackerView != faintedView)
                    duration = Mathf.Max(duration, attackerView.PlayAttackerKoTaunt());
            }

            if (duration > 0f)
                yield return new WaitForSeconds(duration);

            UnlockKoPresentationIfNeeded(evt);

            SyncDisplayVitalsForChainling(evt.targetId);

            if (hudController != null)
                hudController.RefreshAllHud();
        }

        /// <summary>
        /// Fallback handler for KO animations. Subscribed to
        /// <see cref="CombatBattleController.OnChainlingFainted"/> so that even if
        /// the event-log polling misses the KO event, the faint animation still plays.
        /// When the presenter has a state source, the event log (ChainlingKO) will handle
        /// the faint at the correct time (after impact); we skip to avoid playing faint too early.
        /// </summary>
        private void HandleChainlingFaintedFallback(string instanceId, int teamIndex, int slotIndex)
        {
            // Event log will deliver ChainlingKO; HandleChainlingKO will play faint at the right time
            if (_stateSource != null && _stateSource.EventLog != null)
                return;

            var view = GetView(instanceId);
            if (view == null)
                return;

            if (view.Visual == null || !view.Visual.gameObject.activeInHierarchy)
                return;

            view.PlayKoTransition();
        }

        /// <summary>
        /// Handles a bench chainling auto-swapping in after a KO.
        /// Refreshes slot bindings so the new chainling appears, resets any
        /// faint animation state on the slot, and pauses briefly.
        /// </summary>
        private IEnumerator HandleAutoSwapIn(CombatEvent evt)
        {
            Debug.Log($"[Combat2Presenter] AutoSwapIn: {evt.message}");

            if (TryResolveEventSlot(evt.intValue2, evt.intValue, out int teamIndex, out int slotIndex))
                UnlockSlotPresentation(teamIndex, slotIndex);

            RefreshSlotBindings();

            SyncAllDisplayVitals();

            if (hudController != null)
                hudController.RefreshAllHud();

            Combat2ChainlingView swapInView = ResolvePresentationView(evt.targetId, evt.intValue2, evt.intValue);
            if (swapInView == null && TryResolveEventSlot(evt.intValue2, evt.intValue, out teamIndex, out slotIndex))
                swapInView = GetViewBySlot(teamIndex, slotIndex);

            ResetActiveSlotFaintState(swapInView);
            FocusActionSource(swapInView);

            if (swapInView != null)
            {
                ICombatSfxOrchestrator sfx = CombatSfx;
                swapInView.PlaySpawnAnimation(() => sfx?.PlaySwapIn(evt, swapInView));
            }

            float swapHold = ResolveSwapSettleDelay();
            if (swapHold > 0f)
                yield return new WaitForSeconds(swapHold);
        }

        private void ResetAllSlotFaintStates()
        {
            ResetActiveSlotFaintState(team0Slot0);
            ResetActiveSlotFaintState(team0Slot1);
            ResetActiveSlotFaintState(team1Slot0);
            ResetActiveSlotFaintState(team1Slot1);
        }

        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â
        // Move Used Ã¢â‚¬â€ Animation-Driven Timing + VFX
        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â

        private IEnumerator HandleMoveUsed(CombatEvent evt)
        {
            var state = _stateSource != null ? _stateSource.State : null;
            var sourceView = GetView(evt.sourceId);
            var targetView = GetView(evt.targetId);
            MoveInfo moveInfo = ResolveMoveInfo(state, evt.moveId);
            bool presentsWallRaise = CombatWallStateUtility.MovePlacesBlock(moveInfo.instance) ||
                                     CombatWallStateUtility.MoveDefinitionPlacesWall(moveInfo.definition);
            if (presentsWallRaise && !string.IsNullOrEmpty(evt.targetId))
                _wallRaisePendingByInstanceId.Add(evt.targetId);

            _pendingResetToDefault = false;
            FocusActionSource(sourceView);

            if (sourceView != null)
                sourceView.FlashTargeter(targeterFlashDuration);
            if (targetView != null)
                targetView.FlashTargeter(targeterFlashDuration);

            float announceHold = ResolveMoveAnnounceHold();
            if (announceHold > 0f)
                yield return new WaitForSeconds(announceHold);

            if (sourceView != null)
                sourceView.SetHighlighted(true);

            if (state == null || sourceView == null)
            {
                if (!string.IsNullOrEmpty(evt.targetId))
                    _wallRaisePendingByInstanceId.Remove(evt.targetId);
                if (sourceView != null)
                    sourceView.SetHighlighted(false);
                yield break;
            }

            var category = moveInfo.category;
            string definitionId = moveInfo.definitionId;
            MoveDefinitionSO moveDefinition = moveInfo.definition;
            bool initialTargetsWall = ResolveInitialWallRoutingForMove(evt, state);

            VfxResolution vfxResolution = default;
            if (vfxLibrary != null)
            {
                VfxMoveKey key = moveInfo.instance != null
                    ? VfxMoveKey.From(moveInfo.instance)
                    : moveDefinition != null
                        ? VfxMoveKey.From(moveDefinition)
                        : new VfxMoveKey(definitionId, category, ChainlingType.None,
                            category == MoveCategory.DamagingMelee || category == MoveCategory.DamagingRanged
                                ? MoveVfxRole.Damage
                                : category == MoveCategory.StatusUtility
                                    ? MoveVfxRole.Debuff
                                    : category == MoveCategory.Passive ? MoveVfxRole.Passive : MoveVfxRole.Buff);
                vfxResolution = vfxLibrary.Resolve(key);
            }
            CombatVFXLibrary.VFXEntry vfxEntry = vfxResolution.entry;

            if (!string.IsNullOrEmpty(evt.moveId))
            {
                _moveContextByMoveId[evt.moveId] = new MovePresentationContext
                {
                    moveId = evt.moveId,
                    sourceId = evt.sourceId,
                    targetId = evt.targetId,
                    initialTargetsWall = initialTargetsWall,
                    category = category,
                    moveDefinition = moveDefinition,
                    vfxEntry = vfxEntry,
                    vfxResolveTier = vfxResolution.tier
                };
            }

            bool movePresentationActive = TryBeginMovePresentation(sourceView, moveDefinition, evt.moveId);
            try
            {
                if (category == MoveCategory.DamagingMelee)
                {
                    yield return PresentMeleeMoveSequence(
                        evt,
                        sourceView,
                        targetView,
                        vfxEntry,
                        initialTargetsWall);
                    yield break;
                }

                if (category == MoveCategory.DamagingRanged)
                {
                    CombatChainlingVisual visual = sourceView.Visual;
                    bool releaseSpawned = false;
                    float rangedLeadDuration = 0f;
                    Action releaseHandler = () =>
                    {
                        if (releaseSpawned)
                            return;

                        releaseSpawned = true;
                        SpawnPhaseVfx(CombatVFXLibrary.VFXPhase.Projectile, vfxEntry, sourceView, targetView, initialTargetsWall);
                        rangedLeadDuration = StartRangedLeadFocus(sourceView, targetView, initialTargetsWall, vfxEntry);
                    };

                    if (visual != null)
                        visual.OnRangedRelease += releaseHandler;

                    try
                    {
                        float animDuration = sourceView.PlayRangedAttack();
                        SpawnPhaseVfx(CombatVFXLibrary.VFXPhase.Cast, vfxEntry, sourceView, targetView, initialTargetsWall);

                        float markerTime = visual != null
                            ? Mathf.Max(0f, visual.GetMarkerTimeOrDefault(CombatChainlingVisual.CombatAnimState.RangedAttack))
                            : animDuration * 0.5f;

                        if (markerTime > 0f)
                            yield return new WaitForSeconds(markerTime);

                        releaseHandler();

                        float effectiveImpact = vfxEntry != null
                            ? vfxEntry.GetEffectiveImpactDelay(rangedLeadDuration)
                            : -1f;

                        if (effectiveImpact > 0f)
                        {
                            yield return new WaitForSeconds(effectiveImpact);
                        }
                        else
                        {
                            float remaining = Mathf.Max(0f, animDuration - markerTime);
                            float waitAfterRelease = Mathf.Max(remaining, rangedLeadDuration);
                            if (waitAfterRelease > 0f)
                                yield return new WaitForSeconds(waitAfterRelease);
                        }
                    }
                    finally
                    {
                        if (visual != null)
                            visual.OnRangedRelease -= releaseHandler;
                    }

                    yield break;
                }

                if (definitionId == "position_swap")
                {
                    SpawnPhaseVfx(CombatVFXLibrary.VFXPhase.Cast, vfxEntry, sourceView, targetView, initialTargetsWall);
                    float utilityHold = ResolveUtilityCastHold();
                    if (utilityHold > 0f)
                        yield return new WaitForSeconds(utilityHold);
                    RefreshSlotBindings();
                    yield break;
                }

                SpawnPhaseVfx(CombatVFXLibrary.VFXPhase.Cast, vfxEntry, sourceView, targetView, initialTargetsWall);
                float castHold = ResolveUtilityCastHold();
                if (castHold > 0f)
                    yield return new WaitForSeconds(castHold);

                if (presentsWallRaise)
                    PresentWallRaise(targetView, moveInfo.instance, moveDefinition, evt.timestamp, evt.sourceId);
            }
            finally
            {
                if (!string.IsNullOrEmpty(evt.targetId))
                    _wallRaisePendingByInstanceId.Remove(evt.targetId);
                if (sourceView != null)
                    sourceView.SetHighlighted(false);

                if (movePresentationActive)
                    EndMovePresentation(sourceView, evt.moveId);
            }
        }

        private IEnumerator HandleSwapPerformed(CombatEvent evt)
        {
            var sourceView = GetView(evt.sourceId);

            FocusActionSource(sourceView);

            if (sourceView != null)
            {
                CombatSfx?.PlaySwapOut(evt, sourceView);
                sourceView.SetHighlighted(true);
                float swapOutDuration = sourceView.PlaySwapOutTransition();
                if (swapOutDuration > 0f)
                    yield return new WaitForSeconds(swapOutDuration);
            }

            // Engine has already updated activeIndices at this point; rebind slots to show the incoming chainling.
            if (TryResolveEventSlot(evt.intValue2, evt.intValue, out int teamIndex, out int slotIndex))
                UnlockSlotPresentation(teamIndex, slotIndex);

            RefreshSlotBindings();

            SyncAllDisplayVitals();

            if (hudController != null)
                hudController.RefreshAllHud();

            var targetView = ResolvePresentationView(evt.targetId, evt.intValue2, evt.intValue);
            ResetActiveSlotFaintState(targetView);
            if (targetView != null)
            {
                FocusActionSource(targetView);
                targetView.SetHighlighted(true);
                ICombatSfxOrchestrator sfx = CombatSfx;
                targetView.PlaySpawnAnimation(() => sfx?.PlaySwapIn(evt, targetView));
            }

            float swapHold = ResolveSwapSettleDelay();
            if (swapHold > 0f)
                yield return new WaitForSeconds(swapHold);

            if (targetView != null)
                targetView.SetHighlighted(false);
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // Damage — Animation-Driven Hit Reaction

        private IEnumerator PresentMeleeMoveSequence(
            CombatEvent moveEvent,
            Combat2ChainlingView sourceView,
            Combat2ChainlingView targetView,
            CombatVFXLibrary.VFXEntry vfxEntry,
            bool initialTargetsWall)
        {
            List<CombatEvent> hitOutcomes = DrainMeleeHitOutcomes(moveEvent);
            Transform strikeAnchor = ResolveMeleeStrikeAnchor(targetView, initialTargetsWall);

            SpawnPhaseVfx(CombatVFXLibrary.VFXPhase.Cast, vfxEntry, sourceView, targetView, initialTargetsWall);

            float approachDuration = sourceView.BeginMeleeApproach(strikeAnchor);
            if (approachDuration > 0f)
                yield return new WaitForSeconds(approachDuration);

            if (hitOutcomes.Count == 0)
            {
                yield return PresentMeleeStrikeCycle(sourceView, targetView, vfxEntry, initialTargetsWall);
            }
            else
            {
                for (int i = 0; i < hitOutcomes.Count; i++)
                {
                    CombatEvent outcome = hitOutcomes[i];
                    NotePresentedCombatEvent(outcome);
                    PlayCombatSfxOutcomeImmediate(outcome);

                    Combat2ChainlingView outcomeTarget = GetView(outcome.targetId) ?? targetView;
                    bool outcomeTargetsWall = ResolveOutcomeTargetsWall(outcome, initialTargetsWall);
                    Transform cycleAnchor = ResolveMeleeStrikeAnchor(outcomeTarget, outcomeTargetsWall);
                    if (cycleAnchor != null && cycleAnchor != strikeAnchor)
                        sourceView.SetFacingToward(cycleAnchor);

                    yield return PresentMeleeStrikeCycle(sourceView, outcomeTarget, vfxEntry, outcomeTargetsWall);
                    yield return PresentMeleeHitOutcome(outcome);
                }
            }

            float returnDuration = sourceView.EndMeleeApproach();
            if (returnDuration > 0f)
                yield return new WaitForSeconds(returnDuration);

            if (sourceView != null)
                sourceView.SetHighlighted(false);
        }

        private List<CombatEvent> DrainMeleeHitOutcomes(CombatEvent moveEvent)
        {
            var outcomes = new List<CombatEvent>(4);
            while (_eventQueue.Count > 0)
            {
                CombatEvent peek = _eventQueue.Peek();
                if (peek.eventType == CombatEventType.MoveUsed)
                    break;

                if (!IsMeleeHitOutcomeEvent(peek.eventType))
                    break;

                if (!IsOutcomeForMove(moveEvent, peek))
                    break;

                outcomes.Add(_eventQueue.Dequeue());
            }

            return outcomes;
        }

        private static bool IsMeleeHitOutcomeEvent(CombatEventType type)
        {
            return type == CombatEventType.DamageDealt ||
                   type == CombatEventType.Miss ||
                   type == CombatEventType.Dodge ||
                   type == CombatEventType.BarrierBlocked;
        }

        private bool ResolveOutcomeTargetsWall(CombatEvent outcome, bool fallbackTargetsWall)
        {
            if (WallInterceptPayload.TryFromEvent(outcome, out var wallPayload) && wallPayload != null)
                return wallPayload.RoutesAttackToWall();
            return fallbackTargetsWall;
        }

        private IEnumerator PresentMeleeStrikeCycle(
            Combat2ChainlingView sourceView,
            Combat2ChainlingView targetView,
            CombatVFXLibrary.VFXEntry vfxEntry,
            bool targetWall)
        {
            CombatChainlingVisual visual = sourceView != null ? sourceView.Visual : null;
            bool strikeSpawned = false;
            System.Action markerHandler = () =>
            {
                if (strikeSpawned)
                    return;

                strikeSpawned = true;
                SpawnPhaseVfx(CombatVFXLibrary.VFXPhase.Strike, vfxEntry, sourceView, targetView, targetWall);
            };

            if (visual != null)
                visual.OnMeleeContact += markerHandler;

            try
            {
                float animDuration = sourceView != null ? sourceView.PlayMeleeStrikeCycle() : 0.08f;
                float markerTime = sourceView != null
                    ? sourceView.GetMeleeContactMarkerTime()
                    : animDuration * 0.5f;

                if (markerTime > 0f)
                    yield return new WaitForSeconds(markerTime);

                if (!strikeSpawned)
                    markerHandler();

                float strikeTravelDuration = ResolvePhaseTravelDuration(
                    vfxEntry,
                    CombatVFXLibrary.VFXPhase.Strike,
                    sourceView,
                    targetView,
                    targetWall);
                float effectiveImpact = vfxEntry != null
                    ? vfxEntry.GetEffectiveImpactDelay(strikeTravelDuration)
                    : -1f;

                if (effectiveImpact > 0f)
                {
                    yield return new WaitForSeconds(effectiveImpact);
                }
                else
                {
                    float remaining = Mathf.Max(strikeTravelDuration, Mathf.Max(0f, animDuration - markerTime));
                    if (remaining > 0f)
                        yield return new WaitForSeconds(remaining);
                }
            }
            finally
            {
                if (visual != null)
                    visual.OnMeleeContact -= markerHandler;
            }
        }

        private IEnumerator PresentMeleeHitOutcome(CombatEvent evt)
        {
            switch (evt.eventType)
            {
                case CombatEventType.DamageDealt:
                    yield return HandleDamage(evt);
                    break;
                case CombatEventType.Miss:
                    yield return HandleMiss(evt);
                    break;
                case CombatEventType.Dodge:
                    yield return HandleDodge(evt);
                    break;
                case CombatEventType.BarrierBlocked:
                    yield return HandleBarrierBlocked(evt);
                    break;
            }
        }

        private void NotePresentedCombatEvent(CombatEvent evt)
        {
            if (evt.timestamp > _lastPresentedCombatEventTimestamp)
                _lastPresentedCombatEventTimestamp = evt.timestamp;
        }

        private IEnumerator HandleDamage(CombatEvent evt)
        {
            var targetView = ResolvePresentationView(evt.targetId, -1, -1);
            MovePresentationContext moveContext = null;
            if (!string.IsNullOrEmpty(evt.moveId))
                _moveContextByMoveId.TryGetValue(evt.moveId, out moveContext);

            WallInterceptPayload.TryFromEvent(evt, out var wallPayload);
            bool bodyHitReaction = ShouldPlayBodyHitReaction(evt, wallPayload);
            IReadOnlyList<CombatWallPresentationBeat> wallPlan = CombatWallPresentationPlanner.PlanImpact(
                wallPayload,
                bodyHitReaction);
            bool impactTargetsWall = CombatWallPresentationPlanner.Contains(
                wallPlan,
                CombatWallPresentationBeat.AttackerImpactAtWall);
            if (wallPayload == null && moveContext != null && moveContext.initialTargetsWall)
                impactTargetsWall = true;
            bool breakBeforeBody = CombatWallPresentationPlanner.Contains(
                wallPlan,
                CombatWallPresentationBeat.WallBreak) && bodyHitReaction;
            bool pendingDirectKo = HasQueuedDirectKoForDamage(evt);

            FocusImpactTarget(targetView, impactTargetsWall);

            if (targetView != null && wallPayload != null)
                SpawnWallMitigatedPopup(targetView, wallPayload);

            if (moveContext != null && targetView != null)
            {
                SpawnPhaseVfx(
                    CombatVFXLibrary.VFXPhase.Impact,
                    moveContext.vfxEntry,
                    GetView(moveContext.sourceId),
                    targetView,
                    impactTargetsWall);
            }

            float wallDuration = PlayWallOutcome(targetView, wallPayload, evt.timestamp, evt.sourceId);

            if (breakBeforeBody && wallDuration > 0f)
                yield return new WaitForSeconds(wallDuration);

            float hitDuration = 0f;
            if (targetView != null && bodyHitReaction)
            {
                hitDuration = pendingDirectKo
                    ? targetView.PlayHitImpactOnly()
                    : targetView.PlayHitReaction(ResolveKnockbackSource(GetView(evt.sourceId)));
                SpawnCategoryAmount(
                    targetView,
                    evt.boolValue ? CombatPopupCategory.Crit : CombatPopupCategory.Damage,
                    evt.intValue);
                if (evt.boolValue)
                    SpawnCategoryPopup(targetView, CombatPopupCategory.Crit, "CRIT");
            }

            if (wallPayload != null)
            {
                SpawnWallAttackerHitFeedback(evt.sourceId, wallPayload);
                PlayWallReflectCue(evt.sourceId, targetView, wallPayload, evt.timestamp);
            }

            // Camera shake
            if (shakeOnHit && combatCameraController != null)
            {
                float damage = Mathf.Abs(evt.intValue);
                float t = maxDamageForShake <= 0f ? 1f : Mathf.Clamp01(damage / maxDamageForShake);
                float intensity = Mathf.Lerp(minShakeIntensity, maxShakeIntensity, t);
                ShakeCamera(intensity, shakeDuration, shakeFrequency);
            }

            // Release buffered HP/AP so the HUD sees post-damage values at impact
            SyncDisplayVitalsForChainling(evt.targetId);

            if (hudController != null)
            {
                hudController.PlayCardDamageShake(evt.targetId);
                hudController.RefreshAllHud();
                if (enableHpDebugLogs)
                    Debug.Log($"[Combat2Presenter] HandleDamage target={evt.targetId} damage={evt.intValue} -> RefreshAllHud (synced with popup)");
            }

            float waitDuration = pendingDirectKo ? hitDuration : Mathf.Max(hitDuration, ResolveDamageHold());
            if (!breakBeforeBody)
                waitDuration = Mathf.Max(waitDuration, wallDuration);
            if (waitDuration > 0f)
                yield return new WaitForSeconds(waitDuration);
        }

        private IEnumerator HandleHeal(CombatEvent evt)
        {
            var targetView = GetView(evt.targetId);
            FocusRecipientTarget(targetView);
            if (targetView != null && evt.intValue > 0)
                SpawnCategoryAmount(targetView, CombatPopupCategory.Heal, evt.intValue, isHeal: true);

            SyncDisplayVitalsForChainling(evt.targetId);

            if (hudController != null)
            {
                hudController.PlayCardHealAscend(evt.targetId);
                hudController.RefreshAllHud();
            }

            float healHold = ResolveHealHold();
            if (healHold > 0f)
                yield return new WaitForSeconds(healHold);
        }

        private IEnumerator HandleMiss(CombatEvent evt)
        {
            var targetView = GetView(evt.targetId);
            MovePresentationContext moveContext = null;
            if (!string.IsNullOrEmpty(evt.moveId))
                _moveContextByMoveId.TryGetValue(evt.moveId, out moveContext);

            FocusImpactTarget(targetView, moveContext != null && moveContext.initialTargetsWall);
            if (targetView != null)
                SpawnCategoryPopup(targetView, CombatPopupCategory.Miss, "MISS");

            float missHold = ResolveMissDodgeHold();
            if (missHold > 0f)
                yield return new WaitForSeconds(missHold);
        }

        private IEnumerator HandleDodge(CombatEvent evt)
        {
            var targetView = GetView(evt.targetId);
            MovePresentationContext moveContext = null;
            if (!string.IsNullOrEmpty(evt.moveId))
                _moveContextByMoveId.TryGetValue(evt.moveId, out moveContext);

            FocusImpactTarget(targetView, moveContext != null && moveContext.initialTargetsWall);
            if (targetView != null)
                SpawnCategoryPopup(targetView, CombatPopupCategory.Dodge, "DODGE");

            float dodgeHold = ResolveMissDodgeHold();
            if (dodgeHold > 0f)
                yield return new WaitForSeconds(dodgeHold);
        }

        private IEnumerator HandleBarrierBlocked(CombatEvent evt)
        {
            var targetView = GetView(evt.targetId);
            Combat2ChainlingView sourceView = GetView(evt.sourceId);
            CombatVFXLibrary.VFXEntry entry = null;
            if (!string.IsNullOrEmpty(evt.moveId) && _moveContextByMoveId.TryGetValue(evt.moveId, out var moveContext))
            {
                sourceView = GetView(moveContext.sourceId);
                entry = moveContext.vfxEntry;
            }

            WallInterceptPayload.TryFromEvent(evt, out var wallPayload);
            IReadOnlyList<CombatWallPresentationBeat> wallPlan = CombatWallPresentationPlanner.PlanImpact(
                wallPayload,
                false);
            bool impactTargetsWall = CombatWallPresentationPlanner.Contains(
                wallPlan,
                CombatWallPresentationBeat.AttackerImpactAtWall);
            FocusImpactTarget(targetView, impactTargetsWall);
            if (targetView != null)
                SpawnPhaseVfx(CombatVFXLibrary.VFXPhase.Impact, entry, sourceView, targetView, impactTargetsWall);

            float wallDuration = PlayWallOutcome(targetView, wallPayload, evt.timestamp, evt.sourceId);

            if (targetView != null)
            {
                if (wallPayload != null && wallPayload.damageAbsorbed > 0)
                    SpawnWallMitigatedPopup(targetView, wallPayload);
                else
                    SpawnCategoryPopup(targetView, CombatPopupCategory.Blocked, "BLOCKED", preferBarrierAnchor: true);
            }

            if (wallPayload != null)
            {
                SpawnWallAttackerHitFeedback(evt.sourceId, wallPayload);
                PlayWallReflectCue(evt.sourceId, targetView, wallPayload, evt.timestamp);
            }

            SyncDisplayVitalsForChainling(evt.targetId);

            if (hudController != null)
                hudController.RefreshAllHud();

            float blockedHold = ResolveBarrierBlockedHold();
            blockedHold = Mathf.Max(blockedHold, wallDuration);
            if (blockedHold > 0f)
                yield return new WaitForSeconds(blockedHold);
        }

        private IEnumerator HandleWallExpired(CombatEvent evt)
        {
            var targetView = GetView(evt.targetId);
            WallInterceptPayload.TryFromEvent(evt, out var wallPayload);
            FocusImpactTarget(targetView, true);
            float duration = PlayWallExpire(targetView, wallPayload, evt.timestamp, evt.sourceId);
            if (targetView != null)
                SpawnCategoryPopup(targetView, CombatPopupCategory.WallBroken, CombatWallPresentationPlanner.ExpirePopupLabel, preferBarrierAnchor: true);
            SyncDisplayVitalsForChainling(evt.targetId);
            if (duration > 0f)
                yield return new WaitForSeconds(duration);
        }

        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â
        // VFX Spawning
        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â

        private bool ResolveInitialWallRoutingForMove(CombatEvent moveEvent, CombatState state)
        {
            foreach (CombatEvent queued in _eventQueue)
            {
                if (queued.eventType == CombatEventType.MoveUsed)
                    break;

                if (!IsOutcomeForMove(moveEvent, queued))
                    continue;

                if (WallInterceptPayload.TryFromEvent(queued, out var payload))
                    return payload.UsesWallForInitialAim();

                if (queued.eventType == CombatEventType.Miss || queued.eventType == CombatEventType.Dodge)
                    break;
            }

            return ResolveTargetBarrierState(state, moveEvent.targetId);
        }

        private static bool IsOutcomeForMove(CombatEvent moveEvent, CombatEvent outcomeEvent)
        {
            if (!string.IsNullOrEmpty(moveEvent.moveId))
                return string.Equals(moveEvent.moveId, outcomeEvent.moveId, StringComparison.Ordinal);

            return string.Equals(moveEvent.sourceId, outcomeEvent.sourceId, StringComparison.Ordinal)
                && string.Equals(moveEvent.targetId, outcomeEvent.targetId, StringComparison.Ordinal);
        }

        private bool HasQueuedDirectKoForDamage(CombatEvent damageEvent)
        {
            if (damageEvent.eventType != CombatEventType.DamageDealt ||
                string.IsNullOrEmpty(damageEvent.sourceId) ||
                string.IsNullOrEmpty(damageEvent.moveId) ||
                string.IsNullOrEmpty(damageEvent.targetId))
                return false;

            foreach (CombatEvent queued in _eventQueue)
            {
                if (queued.eventType == CombatEventType.MoveUsed)
                    break;

                if (queued.eventType != CombatEventType.ChainlingKO)
                    continue;

                if (!string.Equals(queued.targetId, damageEvent.targetId, StringComparison.Ordinal))
                    continue;

                return string.Equals(queued.sourceId, damageEvent.sourceId, StringComparison.Ordinal) &&
                       string.Equals(queued.moveId, damageEvent.moveId, StringComparison.Ordinal);
            }

            return false;
        }

        private static bool IsDirectAttackKo(CombatEvent evt)
        {
            return evt.eventType == CombatEventType.ChainlingKO &&
                   !string.IsNullOrEmpty(evt.sourceId) &&
                   !string.IsNullOrEmpty(evt.moveId) &&
                   !string.Equals(evt.sourceId, evt.targetId, StringComparison.Ordinal);
        }

        private bool ShouldPlayBodyHitReaction(CombatEvent evt, WallInterceptPayload payload)
        {
            if (evt.intValue <= 0)
                return false;

            if (payload == null)
                return true;

            WallInterceptMode mode = payload.Mode;
            return mode != WallInterceptMode.WallBlocked && mode != WallInterceptMode.WallHitNoDamage;
        }

        private float PlayWallOutcome(
            Combat2ChainlingView targetView,
            WallInterceptPayload payload,
            int eventTimestamp = -1,
            string sourceId = null)
        {
            if (targetView == null || payload == null)
                return 0f;

            bool shouldResolveVisual = payload.wallActiveBeforeHit
                || payload.wallActiveAfterHit
                || payload.wallExpiredThisStep
                || payload.Mode == WallInterceptMode.WallBlocked;

            CombatWallVisual wall = shouldResolveVisual
                ? EnsureWallVisual(targetView, payload.wallSourceTag)
                : targetView.GetWallVisual();

            if (payload.HasWallOnHitFeedback())
            {
                ChainlingInstance chainling = FindChainlingByInstanceId(
                    _stateSource != null ? _stateSource.State : null,
                    targetView.InstanceId);
                SpawnWallStatePhase(targetView, GetView(sourceId), payload.wallSourceTag, chainling, WallStatePhase.Hit);
            }

            switch (payload.Mode)
            {
                case WallInterceptMode.WallMitigated:
                    TrackWallState(targetView, payload.wallActiveAfterHit);
                    PlayWallCue(
                        CombatWallSfxCue.Absorb,
                        targetView,
                        null,
                        payload.wallSourceTag,
                        eventTimestamp,
                        0f,
                        sourceId);
                    return wall != null ? wall.PlayHit() : 0f;

                case WallInterceptMode.WallBlocked:
                {
                    float blockDuration = wall != null ? wall.PlayBlocked() : 0f;
                    if (!payload.wallActiveAfterHit || payload.wallExpiredThisStep)
                    {
                        MarkWallBreakPending(targetView);
                        StartCoroutine(PlayWallBreakAfterDelay(
                            targetView,
                            blockDuration,
                            payload.wallSourceTag,
                            eventTimestamp,
                            sourceId));
                        return blockDuration + ResolveWallBreakDuration();
                    }

                    TrackWallState(targetView, true);
                    return blockDuration;
                }

                case WallInterceptMode.WallBrokenThrough:
                    return PlayWallBreak(targetView, payload.wallSourceTag, eventTimestamp, sourceId);

                case WallInterceptMode.WallHitNoDamage:
                    if (!payload.wallActiveAfterHit || payload.wallExpiredThisStep)
                        return PlayWallBreak(targetView, payload.wallSourceTag, eventTimestamp, sourceId);

                    TrackWallState(targetView, true);
                    PlayWallCue(
                        CombatWallSfxCue.Absorb,
                        targetView,
                        null,
                        payload.wallSourceTag,
                        eventTimestamp,
                        0f,
                        sourceId);
                    return wall != null ? wall.PlayHit() : 0f;

                case WallInterceptMode.Bypass:
                case WallInterceptMode.None:
                default:
                    TrackWallState(targetView, payload.wallActiveAfterHit);
                    return 0f;
            }
        }

        private IEnumerator PlayWallBreakAfterDelay(
            Combat2ChainlingView view,
            float delay,
            string wallSourceTag,
            int eventTimestamp,
            string sourceId)
        {
            if (delay > 0f)
                yield return new WaitForSeconds(delay);

            PlayWallBreak(view, wallSourceTag, eventTimestamp, sourceId);
        }

        private float PlayWallExpire(
            Combat2ChainlingView view,
            WallInterceptPayload payload,
            int eventTimestamp,
            string sourceId)
        {
            if (view == null)
                return 0f;

            string sourceTag = payload != null ? payload.wallSourceTag : string.Empty;
            string key = $"{view.InstanceId}:{eventTimestamp}:{sourceTag}";
            if (!_wallExpirePresentedKeys.Add(key))
                return 0f;

            MarkWallBreakPending(view);
            CombatWallVisual wall = view.GetWallVisual();
            if (wall == null)
                wall = EnsureWallVisual(view, sourceTag);

            ChainlingInstance chainling = FindChainlingByInstanceId(
                _stateSource != null ? _stateSource.State : null,
                view.InstanceId);
            SpawnWallStatePhase(view, GetView(sourceId), sourceTag, chainling, WallStatePhase.Expire);
            PlayWallCue(
                CombatWallSfxCue.Break,
                view,
                null,
                sourceTag,
                eventTimestamp,
                0f,
                sourceId);

            if (wall == null)
            {
                ClearWallTracking(view);
                return 0f;
            }

            return wall.PlayBreak(() => ClearWallTracking(view));
        }

        private float ResolveWallBreakDuration()
        {
            return wallVisualProfile != null ? Mathf.Max(0f, wallVisualProfile.breakDuration) : 0.2f;
        }

        private bool ResolveTargetBarrierState(CombatState state, string targetInstanceId)
        {
            ChainlingInstance target = FindChainlingByInstanceId(state, targetInstanceId);
            return CombatWallStateUtility.HasActiveWallState(target);
        }

        private Transform ResolveMeleeStrikeAnchor(Combat2ChainlingView targetView, bool targetWall)
        {
            if (targetView == null)
                return null;

            if (targetWall && targetView.BarrierStopAnchor != null)
                return targetView.BarrierStopAnchor;

            if (targetView.MeleeStopAnchor != null)
                return targetView.MeleeStopAnchor;

            if (targetView.ImpactAnchor != null)
                return targetView.ImpactAnchor;

            return targetView.CameraFocus;
        }

        private static Transform ResolveKnockbackSource(Combat2ChainlingView sourceView)
        {
            if (sourceView == null)
                return null;

            if (sourceView.ImpactAnchor != null)
                return sourceView.ImpactAnchor;
            if (sourceView.CameraFocus != null)
                return sourceView.CameraFocus;

            return sourceView.transform;
        }

        private ChainlingInstance FindChainlingByInstanceId(CombatState state, string instanceId)
        {
            if (state == null || string.IsNullOrEmpty(instanceId))
                return null;

            ChainlingInstance result = FindChainlingInTeam(state.team0, instanceId);
            if (result != null)
                return result;

            return FindChainlingInTeam(state.team1, instanceId);
        }

        private void SyncDisplayVitalsForChainling(string instanceId)
        {
            var state = _stateSource != null ? _stateSource.State : null;
            if (state == null) return;
            var chainling = FindChainlingByInstanceId(state, instanceId);
            chainling?.SyncDisplayVitals();
        }

        private void SyncAllDisplayVitals()
        {
            var state = _stateSource != null ? _stateSource.State : null;
            if (state == null) return;
            SyncTeamDisplayVitals(state.team0);
            SyncTeamDisplayVitals(state.team1);
        }

        private static void SyncTeamDisplayVitals(TeamState team)
        {
            if (team?.chainlings == null) return;
            for (int i = 0; i < team.chainlings.Count; i++)
                team.chainlings[i]?.SyncDisplayVitals();
        }

        private static ChainlingInstance FindChainlingInTeam(TeamState team, string instanceId)
        {
            if (team == null || team.chainlings == null || string.IsNullOrEmpty(instanceId))
                return null;

            for (int i = 0; i < team.chainlings.Count; i++)
            {
                ChainlingInstance chainling = team.chainlings[i];
                if (chainling == null)
                    continue;
                if (string.Equals(chainling.instanceId, instanceId, StringComparison.Ordinal))
                    return chainling;
            }

            return null;
        }

        private CombatWallVisual EnsureWallVisual(
            Combat2ChainlingView view,
            string wallSourceTag,
            ChainlingInstance chainling = null,
            BlockKind preferredKind = BlockKind.None,
            BlockTier preferredTier = BlockTier.None)
        {
            if (view == null)
                return null;

            MoveWallPresentation art = ResolveWallPresentation(wallSourceTag);
            ResolveWallKindAndTier(
                wallSourceTag,
                chainling,
                ref preferredKind,
                ref preferredTier);
            if (vfxLibrary != null && vfxLibrary.Fallbacks != null)
                art = vfxLibrary.Fallbacks.ResolveWallArt(preferredKind, preferredTier, art);
            string key = string.IsNullOrWhiteSpace(wallSourceTag)
                ? $"wall_{view.InstanceId}"
                : $"wall_{view.InstanceId}_{wallSourceTag.Trim()}";
            return view.EnsureWallVisual(wallVisualPrefab, key, wallVisualProfile, art, view.ResolvePopupFacingSign());
        }

        private void PresentWallRaise(
            Combat2ChainlingView targetView,
            MoveInstance move,
            MoveDefinitionSO definition,
            int eventTimestamp,
            string sourceId)
        {
            if (targetView == null)
                return;

            string sourceTag = !string.IsNullOrWhiteSpace(move?.definitionId)
                ? move.definitionId.Trim()
                : definition != null ? definition.moveId : string.Empty;
            BlockKind kind = BlockKind.None;
            BlockTier tier = BlockTier.None;
            CombatWallStateUtility.TryResolveMoveBlockProfile(move, out kind, out tier);

            ChainlingInstance chainling = FindChainlingByInstanceId(
                _stateSource != null ? _stateSource.State : null,
                targetView.InstanceId);
            CombatWallVisual wall = EnsureWallVisual(targetView, sourceTag, chainling, kind, tier);
            if (wall == null)
                return;

            wall.Raise();
            TrackWallState(targetView, true);
            _wallSourceByInstanceId[targetView.InstanceId] = sourceTag ?? string.Empty;
            PlayWallCue(
                CombatWallSfxCue.Raise,
                targetView,
                null,
                sourceTag,
                eventTimestamp,
                0f,
                sourceId);
            SpawnWallStatePhase(targetView, null, sourceTag, chainling, WallStatePhase.Raise, kind, tier);
            SpawnWallStatePhase(targetView, null, sourceTag, chainling, WallStatePhase.Loop, kind, tier);
            QueueBarrierFeedback(targetView, chainling, preferBarrierAnchor: true, isBroken: false);
        }

        private void ResolveWallKindAndTier(
            string wallSourceTag,
            ChainlingInstance chainling,
            ref BlockKind kind,
            ref BlockTier tier)
        {
            if (kind != BlockKind.None && tier != BlockTier.None)
                return;

            if (CombatWallStateUtility.TryResolveLatestBlockProfile(chainling, out BlockKind activeKind,
                    out BlockTier activeTier, out _))
            {
                if (kind == BlockKind.None)
                    kind = activeKind;
                if (tier == BlockTier.None)
                    tier = activeTier;
                return;
            }

            MoveDefinitionSO definition = ResolveMoveDefinitionAsset(wallSourceTag);
            if (definition == null || definition.utilityEffects == null)
                return;

            var transient = new MoveInstance { utilityEffects = definition.utilityEffects };
            if (CombatWallStateUtility.TryResolveMoveBlockProfile(transient, out BlockKind moveKind, out BlockTier moveTier))
            {
                if (kind == BlockKind.None)
                    kind = moveKind;
                if (tier == BlockTier.None)
                    tier = moveTier;
            }
        }

        private void SpawnWallStatePhase(
            Combat2ChainlingView targetView,
            Combat2ChainlingView sourceView,
            string wallSourceTag,
            ChainlingInstance chainling,
            WallStatePhase phase,
            BlockKind kind = BlockKind.None,
            BlockTier tier = BlockTier.None)
        {
            CombatVFXFallbacks fallbacks = vfxLibrary != null ? vfxLibrary.Fallbacks : null;
            if (fallbacks == null || targetView == null)
                return;

            ResolveWallKindAndTier(wallSourceTag, chainling, ref kind, ref tier);
            CombatVFXFallbacks.WallStateRow row = fallbacks.ResolveWallState(kind, tier);
            if (row == null)
                return;

            CombatVFXLibrary.VFXPhaseConfig config;
            switch (phase)
            {
                case WallStatePhase.Raise: config = row.raiseVfx; break;
                case WallStatePhase.Loop: config = row.loopVfx; break;
                case WallStatePhase.Hit: config = row.hitVfx; break;
                case WallStatePhase.Break: config = row.breakVfx; break;
                case WallStatePhase.Expire: config = row.expireVfx; break;
                default: return;
            }

            if (config == null)
                return;
            var entry = new CombatVFXLibrary.VFXEntry { impactVfx = config };
            if (phase == WallStatePhase.Loop && config.spawnDelay <= 0f &&
                entry.TryGetPhaseConfig(CombatVFXLibrary.VFXPhase.Impact, out CombatVFXLibrary.VFXPhaseConfig usableLoop))
            {
                StopWallLoopVfx(targetView.InstanceId);
                GameObject loop = SpawnPhaseVfxNow(
                    CombatVFXLibrary.VFXPhase.Impact,
                    usableLoop,
                    sourceView,
                    targetView,
                    true,
                    1f);
                if (loop != null && !string.IsNullOrEmpty(targetView.InstanceId))
                    _wallLoopVfxByInstanceId[targetView.InstanceId] = loop;
                return;
            }
            SpawnPhaseVfx(CombatVFXLibrary.VFXPhase.Impact, entry, sourceView, targetView, true);
        }

        private void TrackWallState(Combat2ChainlingView view, bool isActive)
        {
            if (view == null || string.IsNullOrEmpty(view.InstanceId))
                return;

            if (isActive)
            {
                _wallBreakPendingByInstanceId.Remove(view.InstanceId);
                _barrierActiveByInstanceId[view.InstanceId] = true;
            }
            else
            {
                _barrierActiveByInstanceId[view.InstanceId] = false;
            }
        }

        private void MarkWallBreakPending(Combat2ChainlingView view)
        {
            if (view == null || string.IsNullOrEmpty(view.InstanceId))
                return;

            _wallBreakPendingByInstanceId.Add(view.InstanceId);
            _barrierActiveByInstanceId[view.InstanceId] = true;
        }

        private void ClearWallTracking(Combat2ChainlingView view)
        {
            if (view == null)
                return;

            if (!string.IsNullOrEmpty(view.InstanceId))
            {
                _wallBreakPendingByInstanceId.Remove(view.InstanceId);
                _barrierActiveByInstanceId[view.InstanceId] = false;
                _wallSourceByInstanceId.Remove(view.InstanceId);
                StopWallLoopVfx(view.InstanceId);
            }

            view.ForceHideWall();
        }

        private void StopWallLoopVfx(string instanceId)
        {
            if (string.IsNullOrEmpty(instanceId) || !_wallLoopVfxByInstanceId.TryGetValue(instanceId, out GameObject loop))
                return;
            _wallLoopVfxByInstanceId.Remove(instanceId);
            if (loop != null)
                Destroy(loop);
        }

        private void ClearAllWallLoopVfx()
        {
            foreach (GameObject loop in _wallLoopVfxByInstanceId.Values)
            {
                if (loop != null)
                    Destroy(loop);
            }
            _wallLoopVfxByInstanceId.Clear();
        }

        private float PlayWallBreak(
            Combat2ChainlingView view,
            string wallSourceTag = null,
            int eventTimestamp = -1,
            string sourceId = null)
        {
            if (view == null)
                return 0f;

            MarkWallBreakPending(view);
            CombatWallVisual wall = view.GetWallVisual();
            if (wall == null)
                wall = EnsureWallVisual(view, wallSourceTag);

            if (wall == null)
            {
                ClearWallTracking(view);
                return 0f;
            }

            PlayWallCue(
                CombatWallSfxCue.Break,
                view,
                null,
                wallSourceTag,
                eventTimestamp,
                0f,
                sourceId);

            ChainlingInstance chainling = FindChainlingByInstanceId(
                _stateSource != null ? _stateSource.State : null,
                view.InstanceId);
            SpawnWallStatePhase(view, GetView(sourceId), wallSourceTag, chainling, WallStatePhase.Break);

            return wall.PlayBreak(() => ClearWallTracking(view));
        }

        private static string ResolveBarrierSourceTag(ChainlingInstance chainling)
        {
            return CombatWallStateUtility.ResolveLatestWallSourceTag(chainling);
        }

        private MoveWallPresentation ResolveWallPresentation(string wallSourceTag)
        {
            if (string.IsNullOrWhiteSpace(wallSourceTag))
                return default;

            MoveDefinitionSO moveDef = ResolveMoveDefinitionAsset(wallSourceTag.Trim());
            return moveDef != null ? moveDef.wallPresentation : default;
        }

        private string ResolveWallSfxId(string wallSourceTag)
        {
            MoveWallPresentation presentation = ResolveWallPresentation(wallSourceTag);
            if (!string.IsNullOrWhiteSpace(presentation.wallSfxId))
                return presentation.wallSfxId.Trim();

            return string.IsNullOrWhiteSpace(wallSourceTag) ? string.Empty : wallSourceTag.Trim();
        }

        private void PlayWallCue(
            CombatWallSfxCue cue,
            Combat2ChainlingView targetView,
            Combat2ChainlingView sourceView,
            string wallSourceTag,
            int eventTimestamp,
            float delaySeconds,
            string sourceId = null)
        {
            ICombatSfxOrchestrator sfx = CombatSfx;
            if (sfx == null)
                return;

            sfx.PlayWallCue(new CombatWallSfxRequest
            {
                cue = cue,
                wallSfxId = ResolveWallSfxId(wallSourceTag),
                wallSourceTag = wallSourceTag ?? string.Empty,
                targetId = targetView != null ? targetView.InstanceId : string.Empty,
                sourceId = sourceId ?? (sourceView != null ? sourceView.InstanceId : string.Empty),
                eventTimestamp = eventTimestamp,
                targetView = targetView,
                sourceView = sourceView,
                delaySeconds = delaySeconds,
                preferBarrierAnchor = true,
            });
        }

        private void PlayWallReflectCue(
            string attackerId,
            Combat2ChainlingView targetView,
            WallInterceptPayload payload,
            int eventTimestamp)
        {
            if (payload == null || !payload.HasWallOnHitFeedback())
                return;

            Combat2ChainlingView attackerView = GetView(attackerId);
            if (attackerView == null)
                return;

            PlayWallCue(
                CombatWallSfxCue.Reflect,
                targetView,
                attackerView,
                payload.wallSourceTag,
                eventTimestamp,
                0f,
                attackerId);
        }

        private void SpawnPhaseVfx(CombatVFXLibrary.VFXPhase phase, CombatVFXLibrary.VFXEntry entry, Combat2ChainlingView targetView, bool targetWall)
        {
            SpawnPhaseVfx(phase, entry, null, targetView, targetWall);
        }

        private void SpawnPhaseVfx(
            CombatVFXLibrary.VFXPhase phase,
            CombatVFXLibrary.VFXEntry entry,
            Combat2ChainlingView sourceView,
            Combat2ChainlingView targetView,
            bool targetWall)
        {
            if (!TryResolvePhaseConfig(phase, entry, out var config))
                return;

            // Impact phase audio is only spawned from connect handlers (DamageDealt / BarrierBlocked).
            // Miss/Dodge never call Impact spawn. When both outcome impactSound and Impact
            // phaseStartSound are authored, duck the phase accent for mobile headroom.
            float phaseAudioVolumeScale = phase == CombatVFXLibrary.VFXPhase.Impact
                ? ResolveImpactPhaseAudioVolumeScale(entry)
                : 1f;

            if (config.spawnDelay > 0f)
            {
                StartCoroutine(SpawnPhaseVfxDelayed(
                    phase, config, sourceView, targetView, targetWall, phaseAudioVolumeScale));
                return;
            }

            SpawnPhaseVfxNow(phase, config, sourceView, targetView, targetWall, phaseAudioVolumeScale);
        }

        private bool TryResolvePhaseConfig(
            CombatVFXLibrary.VFXPhase phase,
            CombatVFXLibrary.VFXEntry entry,
            out CombatVFXLibrary.VFXPhaseConfig config)
        {
            config = null;
            return entry != null &&
                   entry.TryGetPhaseConfig(phase, out config) &&
                   config != null;
        }

        private IEnumerator SpawnPhaseVfxDelayed(
            CombatVFXLibrary.VFXPhase phase,
            CombatVFXLibrary.VFXPhaseConfig config,
            Combat2ChainlingView sourceView,
            Combat2ChainlingView targetView,
            bool targetWall,
            float phaseAudioVolumeScale)
        {
            yield return new WaitForSeconds(config.spawnDelay);
            SpawnPhaseVfxNow(phase, config, sourceView, targetView, targetWall, phaseAudioVolumeScale);
        }

        private GameObject SpawnPhaseVfxNow(
            CombatVFXLibrary.VFXPhase phase,
            CombatVFXLibrary.VFXPhaseConfig config,
            Combat2ChainlingView sourceView,
            Combat2ChainlingView targetView,
            bool targetWall,
            float phaseAudioVolumeScale = 1f)
        {
            if (config == null)
                return null;

            bool usesTravel = config.travel != null && config.travel.enabled;
            bool mirrorX = ResolveVfxMirror(sourceView);
            Transform staticAnchor = ResolveAnchorTransform(config.anchor, sourceView, targetView, targetWall);
            Vector3 staticRawSpawnPos = ResolveSpawnPosition(config.anchor, sourceView, targetView, targetWall, staticAnchor);
            staticRawSpawnPos += ApplyVfxFacingToOffset(config.spawnOffset, mirrorX);
            Vector3 staticSpawnPos = ApplyCameraDepthOffset(staticRawSpawnPos, config.cameraDepthOffset);

            Transform travelStartAnchor = null;
            Transform travelEndAnchor = null;
            Vector3 travelStartPos = staticSpawnPos;
            Vector3 travelEndPos = staticSpawnPos;
            if (usesTravel)
            {
                travelStartPos = ResolveTravelEndpoint(
                    config,
                    config.travel.start,
                    sourceView,
                    targetView,
                    targetWall,
                    mirrorX,
                    out travelStartAnchor);
                travelEndPos = ResolveTravelEndpoint(
                    config,
                    config.travel.end,
                    sourceView,
                    targetView,
                    targetWall,
                    mirrorX,
                    out travelEndAnchor);
            }

            if (config.phaseStartSound != null && phaseAudioVolumeScale > 0f)
            {
                PlayPhaseAudio(
                    config.phaseStartSound,
                    usesTravel ? travelStartPos : staticSpawnPos,
                    0f,
                    phaseAudioVolumeScale);
            }

            if (config.postFxCue != null && config.postFxCue.IsPlayable)
                PlayCombatPostFx(config.postFxCue);

            if (config.vfxPrefab == null)
                return null;

            Vector3 spawnPos = usesTravel ? travelStartPos : staticSpawnPos;
            Transform rotationAnchor = usesTravel ? travelStartAnchor : staticAnchor;
            Quaternion spawnRot = rotationAnchor != null ? rotationAnchor.rotation : Quaternion.identity;

            GameObject go = Instantiate(config.vfxPrefab, spawnPos, spawnRot);
            ApplyRendererSortingOffset(go, config.sortingOrderOffset);
            float phaseScaleMultiplier = vfxLibrary != null
                ? vfxLibrary.GetEffectivePhaseScaleMultiplier(config)
                : config.scaleMultiplier;

            Transform attachTarget = ResolveAttachTarget(config.attachPolicy, sourceView, targetView, targetWall);
            var moveVfx = go.GetComponent<CombatMoveVFX>();
            if (moveVfx != null)
            {
                moveVfx.SetMirrorX(mirrorX);
                moveVfx.SetBillboardCamera(combatCamera != null ? combatCamera : Camera.main);

                float phaseScaleDuration = usesTravel
                    ? CombatVfxTravelTimingResolver.ResolveDuration(config, travelStartPos, travelEndPos)
                    : moveVfx.GetAuthoredEffectLifetimeDuration();
                moveVfx.ConfigurePhaseScale(phaseScaleMultiplier, phaseScaleDuration, config.phaseScaleCurve);

                if (config.persistent)
                    moveVfx.SetAutoDestroy(false);

                if (!usesTravel && attachTarget != null && config.attachPolicy != CombatVFXLibrary.VFXAttachPolicy.World)
                    moveVfx.AttachTo(attachTarget, true);

                if (usesTravel)
                {
                    Vector3 startAnchorOffset = travelStartAnchor != null
                        ? travelStartPos - travelStartAnchor.position
                        : Vector3.zero;
                    Vector3 endAnchorOffset = travelEndAnchor != null
                        ? travelEndPos - travelEndAnchor.position
                        : Vector3.zero;
                    float duration = CombatVfxTravelTimingResolver.ResolveDuration(
                        config,
                        travelStartPos,
                        travelEndPos);
                    moveVfx.ConfigureTravel(new CombatMoveVFX.TravelPath(
                        travelStartPos,
                        travelEndPos,
                        travelStartAnchor,
                        travelEndAnchor,
                        startAnchorOffset,
                        endAnchorOffset,
                        config.travel.start != null && config.travel.start.followAnchor,
                        config.travel.end != null && config.travel.end.followAnchor,
                        duration,
                        config.travel.travelProgressCurve,
                        config.travel.worldYArc,
                        config.travel.startScaleMultiplier,
                        config.travel.endScaleMultiplier,
                        config.travel.scaleCurve,
                        config.travel.spriteTimingMode));
                }
            }
            else
            {
                if (!Mathf.Approximately(phaseScaleMultiplier, 1f))
                    go.transform.localScale *= phaseScaleMultiplier;

                ApplyVfxRootMirror(go, mirrorX);
                if (usesTravel)
                    return go;

                if (attachTarget == null || config.attachPolicy == CombatVFXLibrary.VFXAttachPolicy.World)
                    return go;

                go.transform.SetParent(attachTarget, true);
            }

            return go;
        }

        private Vector3 ResolveTravelEndpoint(
            CombatVFXLibrary.VFXPhaseConfig config,
            CombatVFXLibrary.VFXTravelEndpoint endpoint,
            Combat2ChainlingView sourceView,
            Combat2ChainlingView targetView,
            bool targetWall,
            bool mirrorX,
            out Transform anchor)
        {
            CombatVFXLibrary.VFXAnchor endpointAnchor = endpoint != null ? endpoint.anchor : config.anchor;
            anchor = ResolveAnchorTransform(endpointAnchor, sourceView, targetView, targetWall);
            Vector3 position = ResolveSpawnPosition(endpointAnchor, sourceView, targetView, targetWall, anchor);
            position += ApplyVfxFacingToOffset(config.spawnOffset, mirrorX);
            if (endpoint != null)
                position += ApplyVfxFacingToOffset(endpoint.offset, mirrorX);

            return ApplyCameraDepthOffset(position, config.cameraDepthOffset);
        }

        private Vector3 ApplyCameraDepthOffset(Vector3 position, float cameraDepthOffset)
        {
            if (Mathf.Approximately(cameraDepthOffset, 0f))
                return position;

            Camera activeCamera = combatCamera != null ? combatCamera : Camera.main;
            if (activeCamera == null)
                return position + Vector3.back * cameraDepthOffset;

            Vector3 toCamera = activeCamera.transform.position - position;
            if (toCamera.sqrMagnitude < 0.0001f)
                return position;

            return position + toCamera.normalized * cameraDepthOffset;
        }

        private static void ApplyRendererSortingOffset(GameObject root, int sortingOrderOffset)
        {
            if (root == null || sortingOrderOffset == 0)
                return;

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer != null)
                    renderer.sortingOrder += sortingOrderOffset;
            }
        }

        private static bool ResolveVfxMirror(Combat2ChainlingView sourceView)
        {
            return sourceView != null && sourceView.Visual != null && sourceView.Visual.FlipX;
        }

        private static Vector3 ApplyVfxFacingToOffset(Vector3 authoredOffset, bool mirrorX)
        {
            if (mirrorX)
                authoredOffset.x = -authoredOffset.x;

            return authoredOffset;
        }

        private static void ApplyVfxRootMirror(GameObject root, bool mirrorX)
        {
            if (root == null || !mirrorX)
                return;

            Vector3 scale = root.transform.localScale;
            scale.x = -Mathf.Abs(scale.x);
            root.transform.localScale = scale;
        }

        private static Transform ResolveAnchorTransform(
            CombatVFXLibrary.VFXAnchor anchor,
            Combat2ChainlingView sourceView,
            Combat2ChainlingView targetView,
            bool targetWall)
        {
            switch (anchor)
            {
                case CombatVFXLibrary.VFXAnchor.AttackerCameraFocus:
                    return sourceView != null ? sourceView.CameraFocus : null;
                case CombatVFXLibrary.VFXAnchor.AttackerProjectileOrigin:
                    if (sourceView == null)
                        return null;
                    return sourceView.ProjectileOriginAnchor != null ? sourceView.ProjectileOriginAnchor : sourceView.CameraFocus;
                case CombatVFXLibrary.VFXAnchor.TargetBarrier:
                    if (targetView == null)
                        return null;
                    if (targetWall && targetView.BarrierAnchor != null)
                        return targetView.BarrierAnchor;
                    return targetView.BarrierAnchor != null ? targetView.BarrierAnchor : targetView.ImpactAnchor;
                case CombatVFXLibrary.VFXAnchor.TargetImpact:
                    if (targetView == null)
                        return null;
                    if (targetWall && targetView.BarrierAnchor != null)
                        return targetView.BarrierAnchor;
                    return targetView.ImpactAnchor != null ? targetView.ImpactAnchor : targetView.CameraFocus;
                default:
                    return null;
            }
        }

        private static Vector3 ResolveSpawnPosition(
            CombatVFXLibrary.VFXAnchor anchor,
            Combat2ChainlingView sourceView,
            Combat2ChainlingView targetView,
            bool targetWall,
            Transform resolvedAnchor)
        {
            if (anchor == CombatVFXLibrary.VFXAnchor.Midpoint)
            {
                Vector3 sourcePos = sourceView != null && sourceView.CameraFocus != null
                    ? sourceView.CameraFocus.position
                    : Vector3.zero;

                Transform targetAnchor = targetWall
                    ? (targetView != null ? targetView.BarrierAnchor : null)
                    : (targetView != null ? targetView.ImpactAnchor : null);
                if (targetAnchor == null && targetView != null)
                    targetAnchor = targetView.CameraFocus;

                Vector3 targetPos = targetAnchor != null ? targetAnchor.position : sourcePos;
                return (sourcePos + targetPos) * 0.5f;
            }

            return resolvedAnchor != null ? resolvedAnchor.position : Vector3.zero;
        }

        private static Transform ResolveAttachTarget(
            CombatVFXLibrary.VFXAttachPolicy attachPolicy,
            Combat2ChainlingView sourceView,
            Combat2ChainlingView targetView,
            bool targetWall)
        {
            switch (attachPolicy)
            {
                case CombatVFXLibrary.VFXAttachPolicy.AttachToAttacker:
                    return sourceView != null ? sourceView.CameraFocus : null;
                case CombatVFXLibrary.VFXAttachPolicy.AttachToTarget:
                    if (targetWall && targetView != null && targetView.BarrierAnchor != null)
                        return targetView.BarrierAnchor;
                    return targetView != null ? targetView.ImpactAnchor : null;
                case CombatVFXLibrary.VFXAttachPolicy.AttachToBarrierAnchor:
                    return targetView != null ? targetView.BarrierAnchor : null;
                default:
                    return null;
            }
        }

        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â
        // Move Info Resolution
        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â

        private struct MoveInfo
        {
            public MoveCategory category;
            public string definitionId;
            public MoveDefinitionSO definition;
            public MoveInstance instance;
        }

        private MoveInfo ResolveMoveInfo(CombatState state, string moveInstanceId)
        {
            var info = new MoveInfo
            {
                category = MoveCategory.Support,
                definitionId = string.Empty,
                definition = null,
                instance = null,
            };

            if (state == null || string.IsNullOrEmpty(moveInstanceId))
                return info;

            var result = FindMove(state.team0, moveInstanceId);
            if (result == null)
                result = FindMove(state.team1, moveInstanceId);

            if (result != null)
            {
                info.category = result.category;
                info.definitionId = result.definitionId;
                info.definition = ResolveMoveDefinitionAsset(result.definitionId);
                info.instance = result;
            }

            return info;
        }

        private MoveDefinitionSO ResolveMoveDefinitionAsset(string definitionId)
        {
            if (string.IsNullOrEmpty(definitionId))
                return null;

            MoveDatabaseSO moveDatabase = null;
            if (_battleController != null)
                moveDatabase = _battleController.MoveDatabase;
            else if (_stateSource is Combat2TestHarness testHarness)
                moveDatabase = testHarness.MoveDatabase;

            return moveDatabase != null ? moveDatabase.GetById(definitionId) : null;
        }

        private bool TryBeginMovePresentation(Combat2ChainlingView sourceView, MoveDefinitionSO moveDefinition, string moveInstanceId)
        {
            if (presentationDirector == null || sourceView == null || sourceView.Visual == null)
                return false;

            if (moveDefinition == null || moveDefinition.presentationTrack == null)
                return false;

            presentationDirector.BeginMovePresentation(sourceView, moveDefinition, moveInstanceId);
            return true;
        }

        private void EndMovePresentation(Combat2ChainlingView sourceView, string moveInstanceId)
        {
            if (presentationDirector == null || sourceView == null || sourceView.Visual == null)
                return;

            presentationDirector.EndMovePresentation(sourceView, moveInstanceId);
        }

        private void ResolvePresentationDirector()
        {
            if (presentationDirector != null)
                return;

            presentationDirector = FindObjectOfType<CombatPresentationDirector>(true);
        }

        private void PlayCombatPostFx(CombatPostFxCueSO cue)
        {
            CombatPostFxVolumeController controller = ResolvePostFxVolumeController();
            if (controller == null || cue == null || !cue.IsPlayable)
                return;

            controller.Play(cue);
        }

        private void ForceClearCombatPostFx()
        {
            ResolvePostFxVolumeController()?.ForceClear();
        }

        private CombatPostFxVolumeController ResolvePostFxVolumeController()
        {
            if (postFxVolumeController != null)
                return postFxVolumeController;

            // Scoped discovery only — never grab the scene/GameServices Global Volume.
            postFxVolumeController = GetComponent<CombatPostFxVolumeController>();
            if (postFxVolumeController == null)
                postFxVolumeController = GetComponentInChildren<CombatPostFxVolumeController>(true);

            if (postFxVolumeController == null)
            {
                postFxVolumeController = gameObject.AddComponent<CombatPostFxVolumeController>();
            }

            if (presentationDirector != null)
                presentationDirector.SetPostFxVolumeController(postFxVolumeController);

            return postFxVolumeController;
        }

        private MoveInstance FindMove(TeamState team, string moveInstanceId)
        {
            if (team == null || team.chainlings == null)
                return null;

            for (int i = 0; i < team.chainlings.Count; i++)
            {
                var chainling = team.chainlings[i];
                if (chainling == null || chainling.moves == null)
                    continue;

                for (int j = 0; j < chainling.moves.Count; j++)
                {
                    var move = chainling.moves[j];
                    if (move != null && move.instanceId == moveInstanceId)
                        return move;
                }
            }

            return null;
        }

        // View Lookup
        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â

        private Combat2ChainlingView GetView(string instanceId)
        {
            if (string.IsNullOrEmpty(instanceId))
                return null;

            return _viewById.TryGetValue(instanceId, out var view) ? view : null;
        }

        private void ResolveUxFallbacks()
        {
            if (uiAnimTuning == null)
                uiAnimTuning = GetComponentInParent<CombatUiAnimTuning>(true);
            if (uiAnimTuning == null)
                uiAnimTuning = FindObjectOfType<CombatUiAnimTuning>(true);

            if (damagePopupSpawner == null)
                damagePopupSpawner = GetComponentInChildren<DamagePopupSpawner>(true);
            if (damagePopupSpawner == null && team0Slot0 != null)
                damagePopupSpawner = team0Slot0.GetComponentInParent<DamagePopupSpawner>(true);
            if (damagePopupSpawner == null)
                damagePopupSpawner = FindObjectOfType<DamagePopupSpawner>(true);

            if (damagePopupSpawner == null)
            {
                var go = new GameObject("DamagePopupSpawner");
                go.transform.SetParent(transform, false);
                damagePopupSpawner = go.AddComponent<DamagePopupSpawner>();
            }

            Camera popupCamera = combatCamera != null ? combatCamera : Camera.main;
            EnsurePopupOverlayCameraBootstrap(popupCamera);
            damagePopupSpawner.Configure(popupCamera);

            if (hudController == null)
                hudController = FindObjectOfType<CombatBattleUGUIController>(true);
            if (uiStageController == null)
                uiStageController = FindObjectOfType<CombatUiStageController>(true);
            if (battleIntroController == null)
                battleIntroController = GetComponentInChildren<BattleIntroController>(true);
            if (battleIntroController == null)
                battleIntroController = FindObjectOfType<BattleIntroController>(true);
        }

        private void EnsurePopupOverlayCameraBootstrap(Camera popupCamera)
        {
            if (popupOverlayCameraBootstrap == null && damagePopupSpawner != null)
                popupOverlayCameraBootstrap = damagePopupSpawner.GetComponent<CombatPopupOverlayCameraBootstrap>();
            if (popupOverlayCameraBootstrap == null)
                popupOverlayCameraBootstrap = GetComponentInChildren<CombatPopupOverlayCameraBootstrap>(true);
            if (popupOverlayCameraBootstrap == null)
                popupOverlayCameraBootstrap = FindFirstObjectByType<CombatPopupOverlayCameraBootstrap>(FindObjectsInactive.Include);
            if (popupOverlayCameraBootstrap == null && damagePopupSpawner != null)
                popupOverlayCameraBootstrap = damagePopupSpawner.gameObject.AddComponent<CombatPopupOverlayCameraBootstrap>();

            if (popupOverlayCameraBootstrap != null)
                popupOverlayCameraBootstrap.Initialize(popupCamera);
        }

        private void SpawnPopup(Combat2ChainlingView view, int amount, bool isHeal, bool isCrit, string impactText)
        {
            if (view == null)
                return;

            if (string.IsNullOrEmpty(impactText) && amount <= 0)
                return;

            ResolveUxFallbacks();
            if (damagePopupSpawner == null)
                return;

            Vector3 anchor = ResolvePopupAnchorPosition(view, false);
            int facingSign = view.ResolvePopupFacingSign();
            CombatPopupCategory category = CombatPopupLegacyMapper.ResolveLegacyCategory(amount, isHeal, isCrit, impactText);
            CombatPopupPlayRequest request = string.IsNullOrEmpty(impactText)
                ? CombatPopupPlayRequest.ForAmount(category, anchor, amount, isHeal, facingSign)
                : CombatPopupPlayRequest.ForLabel(category, anchor, impactText, facingSign);
            damagePopupSpawner.Spawn(request);
        }

        private void SpawnPopupAtAnchor(Combat2ChainlingView view, int amount, bool isHeal, bool isCrit, string impactText,
            bool preferBarrierAnchor)
        {
            if (view == null)
                return;
            if (string.IsNullOrEmpty(impactText) && amount <= 0)
                return;

            ResolveUxFallbacks();
            if (damagePopupSpawner == null)
                return;

            Vector3 anchor = ResolvePopupAnchorPosition(view, preferBarrierAnchor);
            int facingSign = view.ResolvePopupFacingSign();
            CombatPopupCategory category = CombatPopupLegacyMapper.ResolveLegacyCategory(amount, isHeal, isCrit, impactText);
            CombatPopupPlayRequest request = string.IsNullOrEmpty(impactText)
                ? CombatPopupPlayRequest.ForAmount(category, anchor, amount, isHeal, facingSign)
                : CombatPopupPlayRequest.ForLabel(category, anchor, impactText, facingSign);
            damagePopupSpawner.Spawn(request);
        }

        private static Vector3 ResolvePopupAnchorPosition(Combat2ChainlingView view, bool preferBarrierAnchor)
        {
            return view.ResolvePopupWorldPosition(preferBarrierAnchor);
        }

        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â
        // State Source Discovery
        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â

        /// <summary>
        /// Resolve the <see cref="ICombatStateSource"/> from the Inspector override
        /// or by auto-discovering a CombatBattleController / Combat2TestHarness.
        /// </summary>
        private void ResolveStateSource()
        {
            // 1. Try explicit Inspector override
            if (stateSourceOverride != null && stateSourceOverride is ICombatStateSource overrideSource)
            {
                _stateSource = overrideSource;
                return;
            }

            // 2. Auto-discover: prefer real battle controller, fall back to test harness
            var controller = FindObjectOfType<CombatBattleController>();
            if (controller != null)
            {
                _stateSource = controller;
                return;
            }

            var harness = FindObjectOfType<Combat2TestHarness>();
            if (harness != null)
            {
                _stateSource = harness;
            }
        }

        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â
        // Camera Helpers (interface-first, reflection fallback)
        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â

        private bool CanControllerAcceptCombatTarget(MonoBehaviour controller)
        {
            if (controller == null)
                return false;

            if (controller is ICombatCameraController)
                return true;

            return controller.GetType().GetMethod("SetTarget", new[] { typeof(Transform) }) != null;
        }

        private bool IsCombatSceneCameraHost(Component host)
        {
            if (host == null || !host.gameObject.activeInHierarchy)
                return false;
            if (host is Behaviour behaviour && !behaviour.enabled)
                return false;
            if (!host.gameObject.scene.IsValid())
                return false;
            if (host.gameObject.scene.name == "DontDestroyOnLoad")
                return false;
            return host.gameObject.scene == gameObject.scene;
        }

        private MonoBehaviour FindCameraController()
        {
            return FindCameraController(null);
        }

        private MonoBehaviour FindCameraController(MonoBehaviour exclude)
        {
            MonoBehaviour sceneMatch = FindCameraControllerInScene(exclude);
            if (sceneMatch != null)
                return sceneMatch;

            if (!_cameraWarningLogged)
            {
                Debug.LogWarning("[Combat2Presenter] Camera controller not found in scene. " +
                                 "Assign combatCameraController in Inspector or ensure the camera rig is loaded. " +
                                 "Camera tracking disabled.");
                _cameraWarningLogged = true;
            }

            return null;
        }

        private MonoBehaviour FindCameraControllerInScene(MonoBehaviour exclude)
        {
            MonoBehaviour[] behaviours = FindObjectsByType<MonoBehaviour>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            for (int i = 0; i < behaviours.Length; i++)
            {
                var behaviour = behaviours[i];
                if (behaviour == null)
                    continue;
                if (exclude != null && ReferenceEquals(behaviour, exclude))
                    continue;
                if (!IsCombatSceneCameraHost(behaviour))
                    continue;
                if (!CanControllerAcceptCombatTarget(behaviour))
                    continue;

                if (behaviour is ICombatCameraController)
                    return behaviour;
            }

            return null;
        }

        private Camera FindCombatSceneCamera()
        {
            if (combatCameraController != null)
            {
                Camera fromController = combatCameraController.GetComponent<Camera>();
                if (fromController == null)
                    fromController = combatCameraController.GetComponentInChildren<Camera>(true);
                if (IsCombatSceneCameraHost(fromController))
                    return fromController;
            }

            Camera[] cameras = FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < cameras.Length; i++)
            {
                Camera camera = cameras[i];
                if (IsCombatSceneCameraHost(camera))
                    return camera;
            }

            return null;
        }

        private void InitializeCombatCameraFocus()
        {
            if (defaultCameraTarget == null)
                return;

            if (_cameraRuntimeFocusTarget == null)
            {
                var focusGo = new GameObject("CombatCameraRuntimeFocus");
                focusGo.hideFlags = HideFlags.HideInHierarchy;
                _cameraRuntimeFocusTarget = focusGo.transform;
            }

            _cameraRuntimeFocusTarget.position = defaultCameraTarget.position;
            _cameraFocusVelocity = Vector3.zero;
            _pendingResetToDefault = false;
            FocusDefault(true);

            if (combatCameraController != null)
                SetCameraTarget(_cameraRuntimeFocusTarget);
        }

        private void RestoreDefaultCameraTarget()
        {
            if (combatCameraController != null && defaultCameraTarget != null)
                SetCameraTarget(defaultCameraTarget);
        }

        private void DestroyRuntimeCameraFocusTarget()
        {
            if (_cameraRuntimeFocusTarget == null)
                return;

            if (Application.isPlaying)
                Destroy(_cameraRuntimeFocusTarget.gameObject);
            else
                DestroyImmediate(_cameraRuntimeFocusTarget.gameObject);

            _cameraRuntimeFocusTarget = null;
            _cameraFocusVelocity = Vector3.zero;
        }

        private void UpdateCombatCameraFocus(float deltaTime)
        {
            if (_cameraRuntimeFocusTarget == null || defaultCameraTarget == null)
                return;

            Vector3 desired = ResolveDesiredCameraFocusPosition();
            if (_currentFocusSmoothTime <= 0f)
            {
                _cameraRuntimeFocusTarget.position = desired;
                _cameraFocusVelocity = Vector3.zero;
                return;
            }

            _cameraRuntimeFocusTarget.position = Vector3.SmoothDamp(
                _cameraRuntimeFocusTarget.position,
                desired,
                ref _cameraFocusVelocity,
                _currentFocusSmoothTime,
                Mathf.Infinity,
                Mathf.Max(0.0001f, deltaTime));
        }

        private Vector3 ResolveDesiredCameraFocusPosition()
        {
            Vector3 defaultPos = defaultCameraTarget != null ? defaultCameraTarget.position : Vector3.zero;
            Vector3 actionPos = ResolveCurrentActionFocusPoint(defaultPos);
            Vector3 delta = actionPos - defaultPos;
            delta.y = 0f;

            Vector3 biasedDelta = Vector3.ClampMagnitude(delta * Mathf.Clamp01(_currentFocusWeight), Mathf.Max(0f, maxFocusShift));
            Vector3 desired = defaultPos + biasedDelta;
            desired.y = defaultPos.y;
            return desired;
        }

        private Vector3 ResolveCurrentActionFocusPoint(Vector3 fallback)
        {
            switch (_cameraFocusMode)
            {
                case CombatCameraFocusMode.RangedLead:
                {
                    Vector3 from = ResolveAnchorPosition(_focusPrimaryAnchor, fallback);
                    Vector3 to = ResolveAnchorPosition(_focusSecondaryAnchor, fallback);
                    float duration = Mathf.Max(0.0001f, _rangedLeadDuration > 0f ? _rangedLeadDuration : defaultRangedTravelDuration);
                    float t = Mathf.Clamp01((Time.time - _focusModeStartTime) / duration);
                    float curveT = rangedLeadCurve != null ? rangedLeadCurve.Evaluate(t) : t;
                    return Vector3.Lerp(from, to, curveT);
                }
                case CombatCameraFocusMode.Default:
                    return fallback;
                default:
                    return ResolveAnchorPosition(_focusPrimaryAnchor, fallback);
            }
        }

        private static Vector3 ResolveAnchorPosition(Transform anchor, Vector3 fallback)
        {
            return anchor != null ? anchor.position : fallback;
        }

        private void SetFocusState(
            CombatCameraFocusMode mode,
            Transform primaryAnchor,
            float weight,
            float smoothTime,
            Transform secondaryAnchor = null,
            float rangedLeadDuration = 0f,
            bool snap = false)
        {
            _cameraFocusMode = mode;
            _focusPrimaryAnchor = primaryAnchor;
            _focusSecondaryAnchor = secondaryAnchor;
            _currentFocusWeight = Mathf.Clamp01(weight);
            _currentFocusSmoothTime = Mathf.Max(0f, smoothTime);
            _focusModeStartTime = Time.time;
            _rangedLeadDuration = Mathf.Max(0f, rangedLeadDuration);

            if (snap && _cameraRuntimeFocusTarget != null)
            {
                _cameraRuntimeFocusTarget.position = ResolveDesiredCameraFocusPosition();
                _cameraFocusVelocity = Vector3.zero;
            }
        }

        private void FocusDefault(bool snap)
        {
            SetFocusState(CombatCameraFocusMode.Default, defaultCameraTarget, 0f, defaultReturnSmoothTime, snap: snap);
        }

        private void FocusSelectionActor(Combat2ChainlingView actorView)
        {
            if (!trackActingChainling)
                return;

            Transform anchor = actorView != null ? actorView.CameraFocus : defaultCameraTarget;
            SetFocusState(CombatCameraFocusMode.SelectionActor, anchor, selectionFocusWeight, selectionFocusSmoothTime);
        }

        private void FocusActionSource(Combat2ChainlingView sourceView)
        {
            if (!trackActingChainling)
                return;

            Transform anchor = sourceView != null ? sourceView.CameraFocus : defaultCameraTarget;
            SetFocusState(CombatCameraFocusMode.ActionSource, anchor, actionFocusWeight, actionFocusSmoothTime);
        }

        private void FocusImpactTarget(Combat2ChainlingView targetView, bool preferBarrier)
        {
            if (!trackActingChainling)
                return;

            Transform anchor = ResolveImpactFocusAnchor(targetView, preferBarrier);
            SetFocusState(CombatCameraFocusMode.ImpactTarget, anchor, impactFocusWeight, impactFocusSmoothTime);
        }

        private void FocusRecipientTarget(Combat2ChainlingView targetView)
        {
            if (!trackActingChainling)
                return;

            Transform anchor = ResolveImpactFocusAnchor(targetView, false);
            SetFocusState(CombatCameraFocusMode.RecipientTarget, anchor, impactFocusWeight, impactFocusSmoothTime);
        }

        private float StartRangedLeadFocus(
            Combat2ChainlingView sourceView,
            Combat2ChainlingView targetView,
            bool targetWall,
            CombatVFXLibrary.VFXEntry entry)
        {
            if (!trackActingChainling)
                return 0f;

            Transform sourceAnchor = null;
            if (sourceView != null)
                sourceAnchor = sourceView.ProjectileOriginAnchor != null ? sourceView.ProjectileOriginAnchor : sourceView.CameraFocus;

            Transform targetAnchor = ResolveImpactFocusAnchor(targetView, targetWall);
            float duration = ResolvePhaseTravelDuration(
                entry,
                CombatVFXLibrary.VFXPhase.Projectile,
                sourceView,
                targetView,
                targetWall,
                defaultRangedTravelDuration);
            SetFocusState(
                CombatCameraFocusMode.RangedLead,
                sourceAnchor,
                impactFocusWeight,
                actionFocusSmoothTime,
                targetAnchor,
                duration);
            return duration;
        }

        private Transform ResolveImpactFocusAnchor(Combat2ChainlingView targetView, bool preferBarrier)
        {
            if (targetView == null)
                return defaultCameraTarget;

            if (preferBarrier && targetView.BarrierAnchor != null)
                return targetView.BarrierAnchor;

            if (targetView.ImpactAnchor != null)
                return targetView.ImpactAnchor;

            if (targetView.CameraFocus != null)
                return targetView.CameraFocus;

            return defaultCameraTarget;
        }

        private float ResolvePhaseTravelDuration(
            CombatVFXLibrary.VFXEntry entry,
            CombatVFXLibrary.VFXPhase phase,
            Combat2ChainlingView sourceView,
            Combat2ChainlingView targetView,
            bool targetWall,
            float fallbackDuration = -1f)
        {
            if (entry != null &&
                TryResolvePhaseConfig(phase, entry, out var config) &&
                config != null &&
                config.travel != null &&
                config.travel.enabled)
            {
                bool mirrorX = ResolveVfxMirror(sourceView);
                Vector3 startPosition = ResolveTravelEndpoint(
                    config,
                    config.travel.start,
                    sourceView,
                    targetView,
                    targetWall,
                    mirrorX,
                    out _);
                Vector3 endPosition = ResolveTravelEndpoint(
                    config,
                    config.travel.end,
                    sourceView,
                    targetView,
                    targetWall,
                    mirrorX,
                    out _);
                float resolvedDuration = CombatVfxTravelTimingResolver.ResolveDuration(
                    config,
                    startPosition,
                    endPosition);
                return Mathf.Max(
                    fallbackDuration,
                    Mathf.Max(0f, config.spawnDelay) + resolvedDuration);
            }

            return fallbackDuration;
        }

        private void SetCameraTarget(Transform target)
        {
            if (combatCameraController == null || target == null)
                return;

            // Fast path: interface
            if (combatCameraController is ICombatCameraController iface)
            {
                iface.SetTarget(target);
                return;
            }

            // Reflection fallback
            var method = combatCameraController.GetType().GetMethod("SetTarget", new[] { typeof(Transform) });
            if (method != null)
            {
                method.Invoke(combatCameraController, new object[] { target });
            }
            else if (!_cameraWarningLogged)
            {
                Debug.LogWarning($"[Combat2Presenter] Camera controller ({combatCameraController.GetType().Name}) " +
                                 "missing SetTarget(Transform). Camera tracking disabled.");
                _cameraWarningLogged = true;
            }
        }

        private void ShakeCamera(float intensity, float duration, float frequency)
        {
            if (combatCameraController == null)
                return;

            // Fast path: interface
            if (combatCameraController is ICombatCameraController iface)
            {
                iface.ShakeCamera(intensity, duration, frequency);
                return;
            }

            // Reflection fallback
            var method = combatCameraController.GetType().GetMethod("ShakeCamera", new[] { typeof(float), typeof(float), typeof(float) });
            if (method != null)
            {
                method.Invoke(combatCameraController, new object[] { intensity, duration, frequency });
            }
            else if (!_cameraWarningLogged)
            {
                Debug.LogWarning($"[Combat2Presenter] Camera controller ({combatCameraController.GetType().Name}) " +
                                 "missing ShakeCamera(float,float,float). Camera shake disabled.");
                _cameraWarningLogged = true;
            }
        }
    }
}
