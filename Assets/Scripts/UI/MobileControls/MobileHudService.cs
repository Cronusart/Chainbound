using System;
using System.Collections.Generic;
using UnityEngine;

namespace SignalJam.UI.MobileControls
{
    /// <summary>
    /// Authoritative service that resolves which <see cref="MobileHudContext"/> is
    /// currently active and notifies subscribers (the HUD visibility controller,
    /// <see cref="MobileInputManager"/>, etc.) when it changes.
    ///
    /// Pattern mirrors <c>InputLockService</c>: systems call
    /// <see cref="AcquireContext"/>, hold the <see cref="MobileHudToken"/> for the
    /// duration they need, and release it. The service picks the highest-priority
    /// active context from the live token set, with <see cref="MobileHudContext.Overworld"/>
    /// as the implicit fallback when no tokens are held.
    ///
    /// Lifecycle: attach to a child of <c>GameServices</c> so it inherits
    /// DontDestroyOnLoad. If none exists when a caller needs the service,
    /// <see cref="Instance"/> spawns a standalone instance and marks it DDOL.
    /// </summary>
    [DisallowMultipleComponent]
    public class MobileHudService : MonoBehaviour
    {
        private static MobileHudService instance;

        /// <summary>
        /// Lazy-initialized singleton. Prefer injecting the service where possible;
        /// this accessor exists so sequence steps and menu scripts can resolve it
        /// without scene references.
        /// </summary>
        public static MobileHudService Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindFirstObjectByType<MobileHudService>(FindObjectsInactive.Include);
                    if (instance == null)
                    {
                        GameObject go = new GameObject("MobileHudService");
                        instance = go.AddComponent<MobileHudService>();
                        DontDestroyOnLoad(go);
                    }
                }
                return instance;
            }
        }

        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = false;
        [SerializeField] private bool showDebugUI = false;

        [Header("Defaults")]
        [Tooltip("Context reported when no tokens are active. Overworld is the typical gameplay HUD.")]
        [SerializeField] private MobileHudContext defaultContext = MobileHudContext.Overworld;

        private int nextTokenId = 1;
        private readonly Dictionary<int, ActiveRequest> activeTokens = new Dictionary<int, ActiveRequest>();
        private MobileHudContext currentContext;

        private struct ActiveRequest
        {
            public string reason;
            public MobileHudContext context;
        }

        /// <summary>Fires when the resolved context changes (deduped).</summary>
        public event Action<MobileHudContext> OnContextChanged;

        /// <summary>Currently resolved context (highest-priority token or <see cref="defaultContext"/>).</summary>
        public MobileHudContext CurrentContext => currentContext;

        /// <summary>True when the HUD should suppress gameplay inputs (Hidden / Menu / Cutscene / Dialogue).</summary>
        public bool IsSuppressed => IsSuppressingContext(currentContext);

        /// <summary>Number of live tokens (debug).</summary>
        public int ActiveTokenCount => activeTokens.Count;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Debug.LogWarning($"[MobileHudService] Duplicate instance found. Destroying {gameObject.name}", this);
                Destroy(gameObject);
                return;
            }

            instance = this;
            currentContext = defaultContext;
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }

        // ============================================
        // PUBLIC API
        // ============================================

        /// <summary>
        /// Acquire a context request. Returns a <see cref="MobileHudToken"/> that must
        /// be released when the caller is done (dialogue closed, cutscene ended, menu closed).
        /// </summary>
        public MobileHudToken AcquireContext(MobileHudContext context, string reason)
        {
            int id = nextTokenId++;
            activeTokens[id] = new ActiveRequest { reason = reason, context = context };

            if (enableDebugLogs)
            {
                Debug.Log($"[MobileHudService] Context acquired: {context} ('{reason}') id={id} active={activeTokens.Count}");
            }

            ResolveAndNotify();
            return new MobileHudToken(id, reason, context, ReleaseToken);
        }

        /// <summary>
        /// True if the supplied context is currently the resolved context.
        /// </summary>
        public bool IsContextActive(MobileHudContext context) => currentContext == context;

        /// <summary>
        /// Inspectable list of active requests for debug overlays / diagnostics.
        /// </summary>
        public IReadOnlyList<string> GetActiveRequestDescriptions()
        {
            var list = new List<string>(activeTokens.Count);
            foreach (var req in activeTokens.Values)
            {
                list.Add($"{req.context} ('{req.reason}')");
            }
            return list;
        }

        /// <summary>
        /// Force-clear all tokens. Use sparingly; intended for scene transitions where
        /// lock holders may have been destroyed without releasing their handle.
        /// </summary>
        public void ClearAllTokens()
        {
            if (activeTokens.Count == 0) return;

            if (enableDebugLogs)
            {
                Debug.Log($"[MobileHudService] Clearing {activeTokens.Count} stale tokens");
            }

            activeTokens.Clear();
            ResolveAndNotify();
        }

        // ============================================
        // INTERNALS
        // ============================================

        private void ReleaseToken(int id)
        {
            if (!activeTokens.TryGetValue(id, out var request)) return;

            activeTokens.Remove(id);

            if (enableDebugLogs)
            {
                Debug.Log($"[MobileHudService] Context released: {request.context} ('{request.reason}') id={id} remaining={activeTokens.Count}");
            }

            ResolveAndNotify();
        }

        private void ResolveAndNotify()
        {
            MobileHudContext next = ResolveContext();
            if (next == currentContext) return;

            currentContext = next;

            if (enableDebugLogs)
            {
                Debug.Log($"[MobileHudService] Context resolved -> {currentContext}");
            }

            try
            {
                OnContextChanged?.Invoke(currentContext);
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
            }
        }

        private MobileHudContext ResolveContext()
        {
            if (activeTokens.Count == 0) return defaultContext;

            int bestPriority = -1;
            MobileHudContext bestContext = defaultContext;

            foreach (var request in activeTokens.Values)
            {
                int p = PriorityOf(request.context);
                if (p > bestPriority)
                {
                    bestPriority = p;
                    bestContext = request.context;
                }
            }

            // If every active token is lower priority than the default, keep the default.
            return bestPriority >= PriorityOf(defaultContext) ? bestContext : defaultContext;
        }

        /// <summary>
        /// Higher value = wins over lower. Order follows the plan:
        /// Hidden &gt; Menu &gt; Cutscene &gt; Dialogue &gt; Battle &gt; Overworld.
        /// </summary>
        private static int PriorityOf(MobileHudContext context) => context switch
        {
            MobileHudContext.Hidden => 60,
            MobileHudContext.Menu => 50,
            MobileHudContext.Cutscene => 40,
            MobileHudContext.Dialogue => 30,
            MobileHudContext.Battle => 20,
            MobileHudContext.Overworld => 10,
            _ => 0,
        };

        private static bool IsSuppressingContext(MobileHudContext context) => context switch
        {
            MobileHudContext.Hidden => true,
            MobileHudContext.Menu => true,
            MobileHudContext.Cutscene => true,
            MobileHudContext.Dialogue => true,
            _ => false,
        };

        // ============================================
        // DEBUG UI
        // ============================================

        private void OnGUI()
        {
            if (!showDebugUI) return;

            GUILayout.BeginArea(new Rect(10, 10, 360, 140));
            GUILayout.BeginVertical("box");
            GUILayout.Label("Mobile HUD Service");
            GUILayout.Label($"Current: {currentContext} (suppressed={IsSuppressed})");
            GUILayout.Label($"Active tokens: {activeTokens.Count}");
            foreach (var req in activeTokens.Values)
            {
                GUILayout.Label($"  - {req.context} ('{req.reason}')");
            }
            GUILayout.EndVertical();
            GUILayout.EndArea();
        }
    }
}
