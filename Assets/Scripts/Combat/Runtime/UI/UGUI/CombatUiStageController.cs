using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// Staged combat UI presentation: applies hidden state before first frame, then reveals
    /// HUD roots and wallet with mobile-safe animations based on encounter UI mode.
    /// </summary>
    [DisallowMultipleComponent]
    public class CombatUiStageController : MonoBehaviour
    {
        [Header("Info HUD (player/enemy cards)")]
        [SerializeField] private RectTransform playerHudPanel;
        [SerializeField] private RectTransform enemyHudPanel;

        [Header("Interaction HUD (action panel, turn ribbon, targeting)")]
        [SerializeField] private RectTransform turnOrderRibbon;
        [SerializeField] private RectTransform targetIndicatorsRoot;
        [SerializeField] private RectTransform bottomActionPanel;

        [Header("Wallet")]
        [Tooltip("Leave empty to auto-resolve the prefab root via CombatActionSelectorController (required when the device has duplicate child names like Hardware_Wallet).")]
        [SerializeField] private Transform walletRoot;

        [Header("Reveal Timing")]
        [SerializeField] private float hudRevealDuration = 0.28f;
        [SerializeField] private float hudRevealScaleFrom = 0.96f;
        [SerializeField] private float hudRevealOffsetY = -24f;
        [SerializeField] private float walletRevealDuration = 0.32f;
        [SerializeField] private float walletStaggerDelay = 0.08f;
        [SerializeField] private float walletStartOffsetY = -120f;
        [SerializeField] private float walletStartTiltX = -8f;
        [SerializeField] private Ease hudEase = Ease.OutCubic;
        [SerializeField] private Ease walletEase = Ease.OutCubic;
        [SerializeField] private bool useUnscaledTime = true;

        [Header("Capture Chainling HUD")]
        [Tooltip("Downward wind-up before the cards leave through the top of the screen.")]
        [SerializeField] private float captureHudDipDistance = 36f;
        [SerializeField] private float captureHudDipDuration = 0.12f;
        [SerializeField] private float captureHudExitDuration = 0.28f;
        [SerializeField] private float captureHudReturnDuration = 0.26f;
        [SerializeField] private float captureHudSettleDuration = 0.18f;
        [SerializeField] private float captureHudExitPadding = 28f;
        [SerializeField] private Ease captureHudDipEase = Ease.OutQuad;
        [SerializeField] private Ease captureHudExitEase = Ease.InCubic;
        [SerializeField] private Ease captureHudSettleEase = Ease.OutBack;

        private readonly List<CanvasGroup> _infoHudGroups = new List<CanvasGroup>(2);
        private readonly List<CanvasGroup> _interactionHudGroups = new List<CanvasGroup>(3);
        private CanvasGroup _walletGroup;
        private readonly Dictionary<RectTransform, Vector2> _cachedAnchoredPositions = new Dictionary<RectTransform, Vector2>();
        private readonly Dictionary<RectTransform, Vector3> _cachedLocalEulers = new Dictionary<RectTransform, Vector3>();
        private Vector3 _cachedWalletLocalPos;
        private Vector3 _cachedWalletLocalEulers;
        private CombatHardwareWalletOffsetController _walletOffsetController;
        private Sequence _activeRevealSequence;
        private Sequence _captureHudSequence;
        private bool _captureHudHidden;

        /// <summary>
        /// Apply immediate hidden state before any HUD refresh. Call as early as possible (e.g. Awake).
        /// </summary>
        public void ApplyImmediateHidden()
        {
            KillActiveSequence();
            if (!_captureHudHidden)
                SetGroupHidden(GetInfoHudTransforms(), _infoHudGroups, true);
            SetGroupHidden(GetInteractionHudTransforms(), _interactionHudGroups, true);
            SetWalletHidden(true);
        }

        /// <summary>
        /// Reveal UI groups based on mode. Full = info + interaction + wallet; InfoOnly = info only; Hidden = none.
        /// </summary>
        public void RevealByMode(CombatUiPresentationMode mode)
        {
            KillActiveSequence();
            if (mode == CombatUiPresentationMode.Hidden)
            {
                ApplyImmediateHidden();
                return;
            }

            _activeRevealSequence = DOTween.Sequence().SetUpdate(useUnscaledTime);

            if (mode == CombatUiPresentationMode.Full || mode == CombatUiPresentationMode.InfoOnly)
            {
                if (!_captureHudHidden)
                    RevealInfoHud(_activeRevealSequence, 0f);
                if (mode == CombatUiPresentationMode.Full)
                {
                    RevealInteractionHud(_activeRevealSequence, 0f);
                    RevealWallet(_activeRevealSequence, walletStaggerDelay);
                }
            }

            _activeRevealSequence.OnKill(() => _activeRevealSequence = null);
            _activeRevealSequence.Play();
        }

        /// <summary>
        /// Keep all combat UI hidden (e.g. intro flee encounter).
        /// </summary>
        public void KeepAllHidden()
        {
            KillActiveSequence();
            ApplyImmediateHidden();
        }

        /// <summary>
        /// Dips the chainling cards down, then sends them up off the top of the screen.
        /// Returns the unscaled duration so the capture flow can wait for them to clear.
        /// </summary>
        public float PlayCaptureChainlingHudExit()
        {
            EnsureInfoHudGroups();
            if (_captureHudHidden || !HasInfoHud())
                return 0f;

            KillCaptureHudSequence();
            _captureHudHidden = true;
            _captureHudSequence = DOTween.Sequence().SetUpdate(useUnscaledTime);

            float dip = Mathf.Max(0.01f, captureHudDipDuration);
            float exit = Mathf.Max(0.01f, captureHudExitDuration);
            ForEachInfoHud((panel, group) =>
            {
                Vector2 rest = RestPosition(panel);
                float exitDistance = ResolveUpExitDistance(panel);
                panel.gameObject.SetActive(true);
                panel.anchoredPosition = rest;
                SetInfoHudInteractive(group, false);
                if (group != null)
                    group.alpha = 1f;

                _captureHudSequence.Insert(0f, panel.DOAnchorPos(rest + new Vector2(0f, -captureHudDipDistance), dip).SetEase(captureHudDipEase));
                _captureHudSequence.Insert(dip, panel.DOAnchorPos(rest + new Vector2(0f, exitDistance), exit).SetEase(captureHudExitEase));
            });

            _captureHudSequence.OnComplete(() =>
            {
                ForEachInfoHud((panel, group) =>
                {
                    if (group != null)
                        group.alpha = 0f;
                    SetInfoHudInteractive(group, false);
                });
            });
            _captureHudSequence.OnKill(() => _captureHudSequence = null);
            _captureHudSequence.Play();
            return dip + exit;
        }

        /// <summary>
        /// Brings the chainling cards back from above: they overshoot down, then bounce up to rest.
        /// Used when the capture dossier closes into combat or post-battle rewards.
        /// </summary>
        public float PlayCaptureChainlingHudReturn()
        {
            EnsureInfoHudGroups();
            if (!_captureHudHidden || !HasInfoHud())
                return 0f;

            KillCaptureHudSequence();
            _captureHudHidden = false;

            float inbound = Mathf.Max(0.01f, captureHudReturnDuration);
            float settle = Mathf.Max(0.01f, captureHudSettleDuration);
            _captureHudSequence = DOTween.Sequence().SetUpdate(useUnscaledTime);
            ForEachInfoHud((panel, group) =>
            {
                Vector2 rest = RestPosition(panel);
                float exitDistance = ResolveUpExitDistance(panel);
                panel.gameObject.SetActive(true);
                panel.anchoredPosition = rest + new Vector2(0f, exitDistance);
                if (group != null)
                    group.alpha = 1f;
                SetInfoHudInteractive(group, false);

                _captureHudSequence.Insert(0f, panel.DOAnchorPos(rest + new Vector2(0f, -captureHudDipDistance), inbound).SetEase(captureHudDipEase));
                _captureHudSequence.Insert(inbound, panel.DOAnchorPos(rest, settle).SetEase(captureHudSettleEase));
            });

            _captureHudSequence.OnComplete(() =>
            {
                ForEachInfoHud((panel, group) =>
                {
                    panel.anchoredPosition = RestPosition(panel);
                    if (group != null)
                        group.alpha = 1f;
                    SetInfoHudInteractive(group, true);
                });
            });
            _captureHudSequence.OnKill(() => _captureHudSequence = null);
            _captureHudSequence.Play();
            return inbound + settle;
        }

        private void Awake()
        {
            ResolveReferences();
            CacheCanvasGroups();
        }

        private void Start()
        {
            ApplyImmediateHidden();
        }

        private void OnDestroy()
        {
            KillActiveSequence();
            KillCaptureHudSequence();
        }

        private void ResolveReferences()
        {
            if (playerHudPanel == null) playerHudPanel = FindRect("PlayerHudPanel");
            if (enemyHudPanel == null) enemyHudPanel = FindRect("EnemyHudPanel");
            if (turnOrderRibbon == null) turnOrderRibbon = FindRect("TurnOrderRibbon");
            if (targetIndicatorsRoot == null) targetIndicatorsRoot = FindRect("TargetIndicatorsRoot");
            if (bottomActionPanel == null) bottomActionPanel = FindRect("BottomActionPanel");
            if (walletRoot == null)
                walletRoot = CombatWalletRootResolver.Resolve();

            if (walletRoot != null)
                _walletOffsetController = walletRoot.GetComponent<CombatHardwareWalletOffsetController>();
        }

        private RectTransform FindRect(string name)
        {
            var rects = GetComponentsInChildren<RectTransform>(true);
            for (int i = 0; i < rects.Length; i++)
            {
                if (rects[i].name == name)
                    return rects[i];
            }
            return null;
        }

        private void CacheCanvasGroups()
        {
            _infoHudGroups.Clear();
            _interactionHudGroups.Clear();
            _walletGroup = null;

            foreach (var rt in GetInfoHudTransforms())
            {
                if (rt == null) continue;
                var cg = rt.GetComponent<CanvasGroup>();
                if (cg == null) cg = rt.gameObject.AddComponent<CanvasGroup>();
                _infoHudGroups.Add(cg);
                _cachedAnchoredPositions[rt] = rt.anchoredPosition;
                _cachedLocalEulers[rt] = rt.localEulerAngles;
            }

            foreach (var rt in GetInteractionHudTransforms())
            {
                if (rt == null) continue;
                var cg = rt.GetComponent<CanvasGroup>();
                if (cg == null) cg = rt.gameObject.AddComponent<CanvasGroup>();
                _interactionHudGroups.Add(cg);
                _cachedAnchoredPositions[rt] = rt.anchoredPosition;
                _cachedLocalEulers[rt] = rt.localEulerAngles;
            }

            if (walletRoot != null)
            {
                _walletGroup = walletRoot.GetComponent<CanvasGroup>();
                if (_walletGroup == null) _walletGroup = walletRoot.gameObject.AddComponent<CanvasGroup>();
                _cachedWalletLocalPos = _walletOffsetController != null
                    ? _walletOffsetController.AnimationAuthoredLocalPosition
                    : walletRoot.localPosition;
                _cachedWalletLocalEulers = walletRoot.localEulerAngles;
            }
        }

        private void SetWalletAnimationPosition(Vector3 position)
        {
            ResolveWalletOffsetController();
            if (_walletOffsetController != null)
                _walletOffsetController.SetAnimationAuthoredLocalPosition(position);
            else if (walletRoot != null)
                walletRoot.localPosition = position;
        }

        private Tween TweenWalletAnimationPosition(Vector3 target, float duration)
        {
            ResolveWalletOffsetController();
            if (_walletOffsetController == null)
                return walletRoot.DOLocalMove(target, duration);

            return DOTween.To(
                () => _walletOffsetController.AnimationAuthoredLocalPosition,
                position => _walletOffsetController.SetAnimationAuthoredLocalPosition(position),
                target,
                duration);
        }

        private void ResolveWalletOffsetController()
        {
            if (_walletOffsetController == null && walletRoot != null)
                _walletOffsetController = walletRoot.GetComponent<CombatHardwareWalletOffsetController>();
        }

        private List<RectTransform> GetInfoHudTransforms()
        {
            var list = new List<RectTransform>(2);
            if (playerHudPanel != null) list.Add(playerHudPanel);
            if (enemyHudPanel != null) list.Add(enemyHudPanel);
            return list;
        }

        private List<RectTransform> GetInteractionHudTransforms()
        {
            var list = new List<RectTransform>(3);
            if (turnOrderRibbon != null) list.Add(turnOrderRibbon);
            if (targetIndicatorsRoot != null) list.Add(targetIndicatorsRoot);
            if (bottomActionPanel != null) list.Add(bottomActionPanel);
            return list;
        }

        private void SetGroupHidden(List<RectTransform> transforms, List<CanvasGroup> groups, bool hidden)
        {
            for (int i = 0; i < transforms.Count; i++)
            {
                var rt = transforms[i];
                var cg = i < groups.Count ? groups[i] : null;
                if (rt == null) continue;

                rt.gameObject.SetActive(!hidden);
                if (cg != null)
                    cg.alpha = hidden ? 0f : 1f;
                if (hidden && _cachedAnchoredPositions.TryGetValue(rt, out var pos))
                    rt.anchoredPosition = pos + new Vector2(0, hudRevealOffsetY);
                if (hidden)
                    rt.localScale = Vector3.one * hudRevealScaleFrom;
            }
        }

        private void SetWalletHidden(bool hidden)
        {
            if (walletRoot == null) return;

            walletRoot.gameObject.SetActive(!hidden);
            if (_walletGroup != null)
                _walletGroup.alpha = hidden ? 0f : 1f;
            if (hidden)
            {
                SetWalletAnimationPosition(_cachedWalletLocalPos + new Vector3(0, walletStartOffsetY * 0.01f, 0));
                walletRoot.localEulerAngles = _cachedWalletLocalEulers + new Vector3(walletStartTiltX, 0, 0);
            }
        }

        private void RevealInfoHud(Sequence seq, float delay)
        {
            var transforms = GetInfoHudTransforms();
            for (int i = 0; i < transforms.Count; i++)
            {
                var rt = transforms[i];
                var cg = i < _infoHudGroups.Count ? _infoHudGroups[i] : null;
                if (rt == null || cg == null) continue;

                rt.gameObject.SetActive(true);
                var startPos = _cachedAnchoredPositions.TryGetValue(rt, out var pos) ? pos + new Vector2(0, hudRevealOffsetY) : rt.anchoredPosition;
                rt.anchoredPosition = startPos;
                rt.localScale = Vector3.one * hudRevealScaleFrom;
                cg.alpha = 0f;

                seq.Insert(delay, rt.DOAnchorPos(_cachedAnchoredPositions[rt], hudRevealDuration).SetEase(hudEase));
                seq.Insert(delay, rt.DOScale(1f, hudRevealDuration).SetEase(hudEase));
                seq.Insert(delay, DOTween.To(() => cg.alpha, x => cg.alpha = x, 1f, hudRevealDuration).SetEase(hudEase).SetTarget(cg));
            }
        }

        private void RevealInteractionHud(Sequence seq, float delay)
        {
            var transforms = GetInteractionHudTransforms();
            for (int i = 0; i < transforms.Count; i++)
            {
                var rt = transforms[i];
                var cg = i < _interactionHudGroups.Count ? _interactionHudGroups[i] : null;
                if (rt == null || cg == null) continue;

                rt.gameObject.SetActive(true);
                var startPos = _cachedAnchoredPositions.TryGetValue(rt, out var pos) ? pos + new Vector2(0, hudRevealOffsetY) : rt.anchoredPosition;
                rt.anchoredPosition = startPos;
                rt.localScale = Vector3.one * hudRevealScaleFrom;
                cg.alpha = 0f;

                seq.Insert(delay, rt.DOAnchorPos(_cachedAnchoredPositions[rt], hudRevealDuration).SetEase(hudEase));
                seq.Insert(delay, rt.DOScale(1f, hudRevealDuration).SetEase(hudEase));
                seq.Insert(delay, DOTween.To(() => cg.alpha, x => cg.alpha = x, 1f, hudRevealDuration).SetEase(hudEase).SetTarget(cg));
            }
        }

        private void RevealWallet(Sequence seq, float delay)
        {
            if (walletRoot == null || _walletGroup == null) return;

            walletRoot.gameObject.SetActive(true);
            var startPos = _cachedWalletLocalPos + new Vector3(0, walletStartOffsetY * 0.01f, 0);
            var startEulers = _cachedWalletLocalEulers + new Vector3(walletStartTiltX, 0, 0);
            SetWalletAnimationPosition(startPos);
            walletRoot.localEulerAngles = startEulers;
            _walletGroup.alpha = 0f;

            seq.Insert(delay, TweenWalletAnimationPosition(_cachedWalletLocalPos, walletRevealDuration).SetEase(walletEase));
            seq.Insert(delay, walletRoot.DOLocalRotate(_cachedWalletLocalEulers, walletRevealDuration).SetEase(walletEase));
            seq.Insert(delay, DOTween.To(() => _walletGroup.alpha, x => _walletGroup.alpha = x, 1f, walletRevealDuration).SetEase(walletEase).SetTarget(_walletGroup));
        }

        private void KillActiveSequence()
        {
            if (_activeRevealSequence != null)
            {
                _activeRevealSequence.Kill();
                _activeRevealSequence = null;
            }
        }

        private void KillCaptureHudSequence()
        {
            if (_captureHudSequence != null)
            {
                _captureHudSequence.Kill();
                _captureHudSequence = null;
            }
        }

        private bool HasInfoHud()
        {
            List<RectTransform> panels = GetInfoHudTransforms();
            for (int i = 0; i < panels.Count; i++)
            {
                if (panels[i] != null)
                    return true;
            }

            return false;
        }

        private void EnsureInfoHudGroups()
        {
            ResolveReferences();
            List<RectTransform> panels = GetInfoHudTransforms();
            if (_infoHudGroups.Count == panels.Count && panels.Count > 0)
                return;

            _infoHudGroups.Clear();
            for (int i = 0; i < panels.Count; i++)
            {
                RectTransform panel = panels[i];
                if (panel == null)
                    continue;

                CanvasGroup group = panel.GetComponent<CanvasGroup>();
                if (group == null)
                    group = panel.gameObject.AddComponent<CanvasGroup>();
                _infoHudGroups.Add(group);
                if (!_cachedAnchoredPositions.ContainsKey(panel))
                    _cachedAnchoredPositions[panel] = panel.anchoredPosition;
            }
        }

        private void ForEachInfoHud(Action<RectTransform, CanvasGroup> apply)
        {
            List<RectTransform> panels = GetInfoHudTransforms();
            int groupIndex = 0;
            for (int i = 0; i < panels.Count; i++)
            {
                RectTransform panel = panels[i];
                if (panel == null)
                    continue;

                CanvasGroup group = groupIndex < _infoHudGroups.Count ? _infoHudGroups[groupIndex] : null;
                groupIndex++;
                apply(panel, group);
            }
        }

        private Vector2 RestPosition(RectTransform panel)
        {
            if (panel != null && _cachedAnchoredPositions.TryGetValue(panel, out Vector2 cached))
                return cached;

            return panel != null ? panel.anchoredPosition : Vector2.zero;
        }

        private float ResolveUpExitDistance(RectTransform panel)
        {
            if (panel == null)
                return captureHudExitPadding;

            RectTransform parent = panel.parent as RectTransform;
            float parentHeight = parent != null ? Mathf.Abs(parent.rect.height) : 0f;
            float height = Mathf.Abs(panel.rect.height);
            if (height < 1f && parentHeight > 1f)
                height = parentHeight * Mathf.Abs(panel.anchorMax.y - panel.anchorMin.y);
            float topGap = parentHeight > 1f
                ? parentHeight * Mathf.Max(0f, 1f - panel.anchorMax.y)
                : 0f;
            return Mathf.Max(height + captureHudExitPadding, topGap + height + captureHudExitPadding);
        }

        private static void SetInfoHudInteractive(CanvasGroup group, bool interactive)
        {
            if (group == null)
                return;

            group.interactable = interactive;
            group.blocksRaycasts = interactive;
        }

    }
}
