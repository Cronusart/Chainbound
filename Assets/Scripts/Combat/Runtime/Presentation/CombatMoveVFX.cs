using SignalJam.VisualFx;
using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// Component on a VFX prefab that manages its lifecycle.
    /// Each move can have its own VFX prefab containing any combination of:
    ///
    ///   1) Sprite animation â€” a child with a MeshRenderer and this component's
    ///      built-in sprite sequence player (matches the game's pixel art aesthetic)
    ///   2) ParticleSystem â€” child ParticleSystems for trails, sparks, impacts, etc.
    ///   3) VFX Graph â€” child VisualEffect components for advanced GPU effects
    ///
    /// The prefab is instantiated by the presenter at the configured spawn point
    /// and auto-destroys when the effect finishes.
    ///
    /// Workflow:
    ///   1. Create a prefab with this component at the root
    ///   2. Add child objects for your sprite anim, particles, VFX Graph, etc.
    ///   3. Set durationMode:
    ///      - Auto: duration = max(sprite anim, longest ParticleSystem)
    ///      - Manual: you set the exact duration
    ///   4. Register the prefab in a CombatVFXLibrary asset
    ///   5. The presenter spawns it at the right time during combat
    /// </summary>
    public class CombatMoveVFX : MonoBehaviour, IPixelBattleSourceTextureOwner
    {
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int MainTexStId = Shader.PropertyToID("_MainTex_ST");
        private static readonly int PixelCanvasSizeId = Shader.PropertyToID("_PixelCanvasSize");

        public enum DurationMode
        {
            [Tooltip("Duration calculated from sprite frames and ParticleSystems.")]
            Auto,
            [Tooltip("Use the manually specified duration.")]
            Manual
        }

        /// <summary>
        /// Determines how animated sprite frames size their target mesh. The
        /// renderer's authored local scale remains the reference size.
        /// </summary>
        public enum SpriteAspectMode
        {
            [Tooltip("Use the mesh's authored width and height without aspect correction.")]
            Stretch,
            [Tooltip("Keep the mesh's authored height and derive its width from each sprite frame.")]
            PreserveAuthoredHeight,
            [Tooltip("Keep the mesh's authored width and derive its height from each sprite frame.")]
            PreserveAuthoredWidth,
            [Tooltip("Keep each sprite frame inside the mesh's authored width and height.")]
            FitInsideAuthoredBounds
        }

        /// <summary>
        /// Runtime-only path data resolved by the combat presenter or previewer.
        /// Snapshot positions are always available. Follow anchors are optional and
        /// retain the world-space endpoint offset resolved at spawn time.
        /// </summary>
        public struct TravelPath
        {
            public readonly Vector3 startPosition;
            public readonly Vector3 endPosition;
            public readonly Transform startAnchor;
            public readonly Transform endAnchor;
            public readonly Vector3 startAnchorOffset;
            public readonly Vector3 endAnchorOffset;
            public readonly bool followStartAnchor;
            public readonly bool followEndAnchor;
            public readonly float duration;
            public readonly AnimationCurve travelProgressCurve;
            public readonly float worldYArc;
            public readonly Vector3 startScaleMultiplier;
            public readonly Vector3 endScaleMultiplier;
            public readonly AnimationCurve scaleCurve;
            public readonly CombatVFXLibrary.VFXTravelSpriteTimingMode spriteTimingMode;

            public TravelPath(
                Vector3 startPosition,
                Vector3 endPosition,
                Transform startAnchor,
                Transform endAnchor,
                Vector3 startAnchorOffset,
                Vector3 endAnchorOffset,
                bool followStartAnchor,
                bool followEndAnchor,
                float duration,
                AnimationCurve travelProgressCurve,
                float worldYArc,
                Vector3 startScaleMultiplier,
                Vector3 endScaleMultiplier,
                AnimationCurve scaleCurve,
                CombatVFXLibrary.VFXTravelSpriteTimingMode spriteTimingMode)
            {
                this.startPosition = startPosition;
                this.endPosition = endPosition;
                this.startAnchor = startAnchor;
                this.endAnchor = endAnchor;
                this.startAnchorOffset = startAnchorOffset;
                this.endAnchorOffset = endAnchorOffset;
                this.followStartAnchor = followStartAnchor;
                this.followEndAnchor = followEndAnchor;
                this.duration = duration;
                this.travelProgressCurve = travelProgressCurve;
                this.worldYArc = worldYArc;
                this.startScaleMultiplier = startScaleMultiplier;
                this.endScaleMultiplier = endScaleMultiplier;
                this.scaleCurve = scaleCurve;
                this.spriteTimingMode = spriteTimingMode;
            }
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // Inspector â€” Sprite Animation (optional)
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        [Header("Sprite Animation (optional)")]
        [Tooltip("Sprite sequence for the VFX itself (e.g. slash, energy ball frames).")]
        [SerializeField] private SpriteAnimationSequence spriteSequence = new SpriteAnimationSequence();

        [Tooltip("FPS for the sprite animation.")]
        [SerializeField] private float spriteFPS = 12f;

        [Tooltip("MeshRenderer to apply sprites to. If null, tries GetComponentInChildren.")]
        [SerializeField] private MeshRenderer spriteRenderer;

        [Tooltip("Material slot that receives the animated source texture and UV transform.")]
        [SerializeField, Min(0)] private int spriteMaterialSlotIndex;

        [Header("Sprite Presentation")]
        [Tooltip("Base width and height of the sprite renderer in world units before phase/root scale. A zero component uses the renderer's authored local scale.")]
        [SerializeField] private Vector2 spriteSize = Vector2.zero;

        [Tooltip("Pixel dimensions of the authored animation canvas shared by every frame. A zero component uses the current frame's full texture rect.")]
        [SerializeField] private Vector2Int spriteCanvasSize = Vector2Int.zero;

        [Tooltip("How the world-space Sprite Size uses the sprite canvas aspect. This changes only the sprite-renderer child, not the VFX root or phase Travel scale.")]
        [SerializeField] private SpriteAspectMode spriteAspectMode = SpriteAspectMode.Stretch;

        [Tooltip("Keeps the sprite mesh parallel to the combat camera while preserving its authored local rotation.")]
        [SerializeField] private bool billboardSpriteRenderer;

        [Tooltip("Optional camera override. Runtime combat and the Animation Previewer supply their active camera automatically.")]
        [SerializeField] private Camera billboardCamera;

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // Inspector â€” Timing
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        [Header("Timing")]
        [SerializeField] private DurationMode durationMode = DurationMode.Auto;

        [Tooltip("Manual duration in seconds (only used when durationMode = Manual).")]
        [SerializeField] private float manualDuration = 1f;

        [Tooltip("Delay before the effect becomes visible (e.g. sync with attack frame).")]
        [SerializeField] private float startDelay = 0f;

        [Tooltip("If true, the prefab auto-destroys when the effect finishes.")]
        [SerializeField] private bool autoDestroy = true;

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•





        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // Runtime State
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private MaterialPropertyBlock _propBlock;
        private float _elapsed;
        private float _cachedDuration;
        private bool _started;
        private bool _finished;
        private int _spriteFrame = -1;
        private Vector3 _spriteRendererAuthoredLocalScale = Vector3.one;
        private bool _hasSpriteRendererAuthoredLocalScale;
        private Quaternion _spriteBillboardRotationOffset = Quaternion.identity;
        private bool _hasSpriteBillboardRotationOffset;

        public int CurrentSpriteFrame => _spriteFrame;
        public int SpriteFrameCount => spriteSequence != null ? spriteSequence.Length : 0;
        public Sprite CurrentSpriteFrameSprite =>
            _spriteFrame >= 0 && spriteSequence != null && _spriteFrame < spriteSequence.Length
                ? spriteSequence[_spriteFrame]
                : null;
        public Texture AppliedSpriteSourceTexture => ResolveAppliedSpriteSourceTexture();
        public Vector2 SpriteSize => spriteSize;
        public Vector2Int SpriteCanvasSize => spriteCanvasSize;

        private bool _travelConfigured;
        private Vector3 _travelStartSnapshot;
        private Vector3 _travelEndSnapshot;
        private Transform _travelStartAnchor;
        private Transform _travelEndAnchor;
        private Vector3 _travelStartAnchorOffset;
        private Vector3 _travelEndAnchorOffset;
        private bool _travelFollowsStartAnchor;
        private bool _travelFollowsEndAnchor;
        private float _travelDuration;
        private AnimationCurve _travelProgressCurve;
        private float _travelWorldYArc;
        private Vector3 _travelBaseScale;
        private Vector3 _travelStartScaleMultiplier;
        private Vector3 _travelEndScaleMultiplier;
        private AnimationCurve _travelScaleCurve;
        private CombatVFXLibrary.VFXTravelSpriteTimingMode _travelSpriteTimingMode;
        private float _travelSpriteFPS;
        private bool _phaseScaleConfigured;
        private Vector3 _phaseScaleBaseScale;
        private float _phaseScaleMultiplier = 1f;
        private float _phaseScaleDuration;
        private AnimationCurve _phaseScaleCurve;
        private bool _mirrorX;

        /// <summary>True once the effect has fully played out.</summary>
        public bool IsFinished => _finished;

        /// <summary>Total duration of the effect in seconds.</summary>
        public float Duration => _cachedDuration;
        public bool IsTravelConfigured => _travelConfigured;
        public float TravelDuration => _travelDuration;
        public float TravelProgress => _travelDuration > 0f ? Mathf.Clamp01(_elapsed / _travelDuration) : 0f;
        public bool MirrorX => _mirrorX;

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // Lifecycle
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private void Awake()
        {
            _propBlock = new MaterialPropertyBlock();

            if (spriteRenderer == null)
                spriteRenderer = GetComponentInChildren<MeshRenderer>();

            CaptureSpriteRendererAuthoredLocalScale();
            CaptureSpriteBillboardRotationOffset();

            _cachedDuration = GetAuthoredEffectLifetimeDuration();
            ApplySpriteAspect(null);

            // Push the first authored frame into the renderer before the first
            // camera render, so the material's fallback texture never flashes.
            UpdateSpriteAnimation(force: true);
        }

        /// <summary>
        /// Configures the one shared world-space travel interpreter used by all
        /// combat VFX phases. The caller resolves combat anchors and offsets.
        /// </summary>
        public void ConfigureTravel(TravelPath path)
        {
            _travelConfigured = true;
            _travelStartSnapshot = path.startPosition;
            _travelEndSnapshot = path.endPosition;
            _travelStartAnchor = path.startAnchor;
            _travelEndAnchor = path.endAnchor;
            _travelStartAnchorOffset = path.startAnchorOffset;
            _travelEndAnchorOffset = path.endAnchorOffset;
            _travelFollowsStartAnchor = path.followStartAnchor && path.startAnchor != null;
            _travelFollowsEndAnchor = path.followEndAnchor && path.endAnchor != null;
            _travelDuration = Mathf.Max(0.01f, path.duration);
            _travelProgressCurve = path.travelProgressCurve ?? AnimationCurve.Linear(0f, 0f, 1f, 1f);
            _travelWorldYArc = path.worldYArc;
            _travelBaseScale = _phaseScaleConfigured ? _phaseScaleBaseScale : transform.localScale;
            ApplyMirrorToScale(ref _travelBaseScale);
            _travelStartScaleMultiplier = path.startScaleMultiplier;
            _travelEndScaleMultiplier = path.endScaleMultiplier;
            _travelScaleCurve = path.scaleCurve ?? AnimationCurve.Linear(0f, 0f, 1f, 1f);
            _travelSpriteTimingMode = path.spriteTimingMode;
            _travelSpriteFPS = ResolveTravelSpriteFps(_travelSpriteTimingMode, _travelDuration);

            float travelLifetime = _travelDuration + GetFinalSpriteFrameDisplayDuration();
            if (durationMode == DurationMode.Auto &&
                _travelSpriteTimingMode != CombatVFXLibrary.VFXTravelSpriteTimingMode.PreservePrefabFps)
            {
                _cachedDuration = Mathf.Max(ComputeDuration(includeSpriteSequence: false), travelLifetime);
            }
            else
            {
                _cachedDuration = Mathf.Max(_cachedDuration, travelLifetime);
            }

            UpdateTravel();
        }

        /// <summary>
        /// Applies the phase's normalized scale curve over the authored effect
        /// lifetime. The phase magnitude is separate from the prefab's authored
        /// root scale and is composed with the curve at playback time.
        /// </summary>
        public void ConfigurePhaseScale(float phaseScaleMultiplier, float duration, AnimationCurve curve)
        {
            _phaseScaleConfigured = true;
            _phaseScaleBaseScale = transform.localScale;
            ApplyMirrorToScale(ref _phaseScaleBaseScale);
            _phaseScaleMultiplier = Mathf.Max(0f, phaseScaleMultiplier);
            _phaseScaleDuration = Mathf.Max(0.01f, duration);
            _phaseScaleCurve = curve != null && curve.length > 0
                ? curve
                : AnimationCurve.Linear(0f, 1f, 1f, 1f);
            _cachedDuration = Mathf.Max(_cachedDuration, _phaseScaleDuration);

            if (_travelConfigured)
                UpdateTravel();
            else
                UpdatePhaseScale();
        }

        /// <summary>Duration from first sprite frame to final-frame arrival.</summary>
        public float GetSpriteFrameSpanDuration()
        {
            if (spriteSequence == null || spriteSequence.Length <= 1 || spriteFPS <= 0f)
                return 0f;

            return (spriteSequence.Length - 1) / spriteFPS;
        }

        /// <summary>
        /// Full authored effect lifetime. Sprite sequences include one display
        /// interval for their final frame; particle lifetime is also considered.
        /// </summary>
        public float GetAuthoredEffectLifetimeDuration()
        {
            return ComputeDuration();
        }

        public void SetAutoDestroy(bool shouldAutoDestroy)
        {
            autoDestroy = shouldAutoDestroy;
        }

        public void SetBillboardCamera(Camera targetCamera)
        {
            billboardCamera = targetCamera;
            ApplySpriteBillboard();
        }

        /// <summary>
        /// Mirrors the complete VFX instance around its local X axis. The
        /// combat presenters call this from the source Chainling's facing so
        /// particles, child meshes, and sprite sequences share one direction.
        /// </summary>
        public void SetMirrorX(bool mirror)
        {
            _mirrorX = mirror;

            if (_phaseScaleConfigured)
                ApplyMirrorToScale(ref _phaseScaleBaseScale);

            if (_travelConfigured)
            {
                ApplyMirrorToScale(ref _travelBaseScale);
                UpdateTravel();
                return;
            }

            Vector3 scale = transform.localScale;
            ApplyMirrorToScale(ref scale);
            transform.localScale = scale;
        }

        /// <summary>
        /// Sets the base sprite-renderer envelope at runtime. Aspect policy and
        /// the current frame are applied immediately when available.
        /// </summary>
        public void SetSpriteSize(Vector2 size)
        {
            spriteSize = new Vector2(Mathf.Max(0f, size.x), Mathf.Max(0f, size.y));

            if (spriteRenderer == null)
                spriteRenderer = GetComponentInChildren<MeshRenderer>();

            CaptureSpriteRendererAuthoredLocalScale();
            ApplySpriteAspect(CurrentSpriteFrameSprite);
        }

        /// <summary>
        /// Sets the shared pixel canvas used to resolve frame aspect at runtime.
        /// This is pixel-space metadata, separate from Sprite Size world units.
        /// </summary>
        public void SetSpriteCanvasSize(Vector2Int size)
        {
            spriteCanvasSize = new Vector2Int(Mathf.Max(0, size.x), Mathf.Max(0, size.y));
            ApplySpriteAspect(CurrentSpriteFrameSprite);
        }

        public void AttachTo(Transform anchor, bool worldPositionStays = true)
        {
            if (anchor == null)
                return;

            transform.SetParent(anchor, worldPositionStays);
        }

        private void Update()
        {
            StepPlayback(Time.deltaTime);
        }

        public void StepPlayback(float deltaTime)
        {
            if (_finished)
                return;

            _elapsed += Mathf.Max(0f, deltaTime);

            // Start delay
            if (!_started)
            {
                if (_elapsed < startDelay)
                    return;

                _started = true;
                _elapsed -= startDelay; // reset so effect time starts at 0
                SetVisibility(true);
            }

            // Sprite animation
            UpdateSpriteAnimation();

            if (_travelConfigured)
                UpdateTravel();
            else
                UpdatePhaseScale();

            ApplySpriteBillboard();

            // Check completion
            if (_elapsed >= _cachedDuration)
            {
                _finished = true;

                if (autoDestroy)
                {
                    if (Application.isPlaying)
                        Destroy(gameObject);
                    else
                        DestroyImmediate(gameObject);
                }
            }
        }

        private void OnEnable()
        {
            // Hide during start delay
            if (startDelay > 0f)
                SetVisibility(false);

            ApplySpriteAspect(null);
            UpdateSpriteAnimation(force: true);
            ApplySpriteBillboard();
        }

        private void LateUpdate()
        {
            ApplySpriteBillboard();
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // Sprite Animation
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        public bool OwnsPixelBattleSourceTexture(Renderer renderer, int materialSlotIndex)
        {
            return materialSlotIndex == Mathf.Max(0, spriteMaterialSlotIndex) &&
                   renderer == spriteRenderer &&
                   spriteSequence != null &&
                   spriteSequence.Length > 0;
        }

        private void UpdateSpriteAnimation(bool force = false)
        {
            if (spriteRenderer == null || spriteSequence == null || spriteSequence.Length == 0)
                return;

            float playbackFps = ResolveSpritePlaybackFps();
            int frame = Mathf.FloorToInt(_elapsed * playbackFps);
            frame = Mathf.Clamp(frame, 0, spriteSequence.Length - 1);
            Sprite sprite = spriteSequence[frame];
            if (sprite == null || sprite.texture == null)
                return;

            if (!force && frame == _spriteFrame)
                return; // same frame, skip property block update

            _spriteFrame = frame;
            ApplySpriteAspect(sprite);

            int materialSlot = Mathf.Max(0, spriteMaterialSlotIndex);
            spriteRenderer.GetPropertyBlock(_propBlock, materialSlot);
            _propBlock.SetTexture(MainTexId, sprite.texture);

            _propBlock.SetVector(
                MainTexStId,
                PixelBattleVfxSourceTextureUtility.ResolveTextureSt(sprite, spriteCanvasSize));

            Vector2 canvasSize = PixelBattleVfxSourceTextureUtility.ResolveCanvasSize(sprite, spriteCanvasSize);
            _propBlock.SetVector(PixelCanvasSizeId, new Vector4(canvasSize.x, canvasSize.y, 0f, 0f));

            spriteRenderer.SetPropertyBlock(_propBlock, materialSlot);
        }

        private void CaptureSpriteRendererAuthoredLocalScale()
        {
            if (spriteRenderer == null || _hasSpriteRendererAuthoredLocalScale)
                return;

            _spriteRendererAuthoredLocalScale = spriteRenderer.transform.localScale;
            _hasSpriteRendererAuthoredLocalScale = true;
        }

        private void ApplySpriteAspect(Sprite sprite)
        {
            if (spriteRenderer == null)
                return;

            bool hasExplicitSpriteSize = spriteSize.x > 0f || spriteSize.y > 0f;
            Vector2 canvasSize = ResolveSpriteCanvasSize(sprite);
            bool hasSpriteAspect = canvasSize.x > 0f && canvasSize.y > 0f;
            if (!hasExplicitSpriteSize && !hasSpriteAspect)
                return;

            CaptureSpriteRendererAuthoredLocalScale();

            Vector2 baseSize = ResolveSpriteBaseSize();
            float authoredWidth = baseSize.x;
            float authoredHeight = baseSize.y;
            if (authoredWidth <= 0f || authoredHeight <= 0f)
                return;

            float width;
            float height;
            if (!hasSpriteAspect || spriteAspectMode == SpriteAspectMode.Stretch)
            {
                width = authoredWidth;
                height = authoredHeight;
            }
            else
            {
                float spriteAspect = canvasSize.x / canvasSize.y;
                switch (spriteAspectMode)
                {
                    case SpriteAspectMode.PreserveAuthoredHeight:
                    {
                        height = authoredHeight;
                        width = height * spriteAspect;
                        break;
                    }

                    case SpriteAspectMode.PreserveAuthoredWidth:
                    {
                        width = authoredWidth;
                        height = width / spriteAspect;
                        break;
                    }

                    case SpriteAspectMode.FitInsideAuthoredBounds:
                    {
                        float authoredAspect = authoredWidth / authoredHeight;
                        if (spriteAspect >= authoredAspect)
                        {
                            width = authoredWidth;
                            height = width / spriteAspect;
                        }
                        else
                        {
                            height = authoredHeight;
                            width = height * spriteAspect;
                        }
                        break;
                    }

                    default:
                        width = authoredWidth;
                        height = authoredHeight;
                        break;
                }
            }

            spriteRenderer.transform.localScale = new Vector3(
                Mathf.Sign(_spriteRendererAuthoredLocalScale.x) * width,
                Mathf.Sign(_spriteRendererAuthoredLocalScale.y) * height,
                _spriteRendererAuthoredLocalScale.z);
        }

        private Vector2 ResolveSpriteBaseSize()
        {
            return new Vector2(
                spriteSize.x > 0f ? spriteSize.x : Mathf.Abs(_spriteRendererAuthoredLocalScale.x),
                spriteSize.y > 0f ? spriteSize.y : Mathf.Abs(_spriteRendererAuthoredLocalScale.y));
        }

        private Vector2 ResolveSpriteCanvasSize(Sprite sprite)
        {
            return PixelBattleVfxSourceTextureUtility.ResolveCanvasSize(sprite, spriteCanvasSize);
        }

        private Texture ResolveAppliedSpriteSourceTexture()
        {
            if (spriteRenderer == null)
                return null;

            _propBlock ??= new MaterialPropertyBlock();
            int materialSlot = Mathf.Max(0, spriteMaterialSlotIndex);
            spriteRenderer.GetPropertyBlock(_propBlock, materialSlot);
            Texture propertyBlockTexture = _propBlock.GetTexture(MainTexId);
            if (propertyBlockTexture != null)
                return propertyBlockTexture;

            Material[] materials = spriteRenderer.sharedMaterials;
            if (materials == null || materialSlot >= materials.Length || materials[materialSlot] == null)
                return null;

            Material material = materials[materialSlot];
            return material.HasProperty(MainTexId) ? material.GetTexture(MainTexId) : null;
        }

        private void CaptureSpriteBillboardRotationOffset()
        {
            if (spriteRenderer == null || _hasSpriteBillboardRotationOffset)
                return;

            _spriteBillboardRotationOffset = spriteRenderer.transform.localRotation;
            _hasSpriteBillboardRotationOffset = true;
        }

        private void ApplySpriteBillboard()
        {
            if (!billboardSpriteRenderer || spriteRenderer == null)
                return;

            CaptureSpriteBillboardRotationOffset();
            Camera activeCamera = billboardCamera != null ? billboardCamera : Camera.main;
            if (activeCamera == null)
                return;

            Transform cameraTransform = activeCamera.transform;
            Quaternion cameraFacingRotation = Quaternion.LookRotation(cameraTransform.forward, cameraTransform.up);
            spriteRenderer.transform.rotation = cameraFacingRotation * _spriteBillboardRotationOffset;
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private void UpdateTravel()
        {
            float normalizedTime = _travelDuration > 0f
                ? Mathf.Clamp01(_elapsed / _travelDuration)
                : 1f;
            Vector3 start = ResolveTravelEndpoint(
                _travelStartSnapshot,
                _travelStartAnchor,
                _travelStartAnchorOffset,
                _travelFollowsStartAnchor);
            Vector3 end = ResolveTravelEndpoint(
                _travelEndSnapshot,
                _travelEndAnchor,
                _travelEndAnchorOffset,
                _travelFollowsEndAnchor);

            transform.position = CombatVfxTravelTimingResolver.EvaluateTravelPosition(
                start,
                end,
                _travelProgressCurve,
                _travelWorldYArc,
                normalizedTime);

            float scaleT = Mathf.Clamp01(_travelScaleCurve.Evaluate(normalizedTime));
            Vector3 scaleMultiplier = Vector3.Lerp(
                _travelStartScaleMultiplier,
                _travelEndScaleMultiplier,
                scaleT);
            float phaseScale = _phaseScaleConfigured
                ? _phaseScaleMultiplier * EvaluatePhaseScale()
                : 1f;
            transform.localScale = Vector3.Scale(
                _travelBaseScale,
                Vector3.Scale(Vector3.one * phaseScale, scaleMultiplier));
        }

        private void UpdatePhaseScale()
        {
            if (!_phaseScaleConfigured)
                return;

            transform.localScale = _phaseScaleBaseScale * (_phaseScaleMultiplier * EvaluatePhaseScale());
        }

        private float EvaluatePhaseScale()
        {
            if (!_phaseScaleConfigured)
                return 1f;

            float normalizedTime = _phaseScaleDuration > 0f
                ? Mathf.Clamp01(_elapsed / _phaseScaleDuration)
                : 1f;
            return Mathf.Max(0f, _phaseScaleCurve != null
                ? _phaseScaleCurve.Evaluate(normalizedTime)
                : 1f);
        }

        private static Vector3 ResolveTravelEndpoint(
            Vector3 snapshot,
            Transform anchor,
            Vector3 anchorOffset,
            bool followAnchor)
        {
            return followAnchor && anchor != null
                ? anchor.position + anchorOffset
                : snapshot;
        }

        private void ApplyMirrorToScale(ref Vector3 scale)
        {
            scale.x = Mathf.Abs(scale.x) * (_mirrorX ? -1f : 1f);
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // Helpers
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private float ComputeDuration(bool includeSpriteSequence = true)
        {
            if (durationMode == DurationMode.Manual)
                return manualDuration;

            float dur = 0f;

            // Keep the final sprite frame visible for one full frame interval.
            if (includeSpriteSequence && spriteSequence != null && spriteSequence.Length > 0 && spriteFPS > 0f)
                dur = Mathf.Max(dur, spriteSequence.Length / spriteFPS);

            // Longest ParticleSystem duration
            var particles = GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < particles.Length; i++)
            {
                float pDur = particles[i].main.duration + particles[i].main.startLifetime.constantMax;
                dur = Mathf.Max(dur, pDur);
            }

            // Minimum fallback
            return Mathf.Max(dur, 0.1f);
        }

        private float GetFinalSpriteFrameDisplayDuration()
        {
            if (spriteSequence == null || spriteSequence.Length == 0)
                return 0f;

            if (_travelConfigured &&
                _travelSpriteTimingMode == CombatVFXLibrary.VFXTravelSpriteTimingMode.FitEffectLifetimeToTravel)
            {
                return 0f;
            }

            float playbackFps = ResolveSpritePlaybackFps();
            return playbackFps > 0f ? 1f / playbackFps : 0f;
        }

        private float ResolveSpritePlaybackFps()
        {
            if (!_travelConfigured ||
                _travelSpriteTimingMode == CombatVFXLibrary.VFXTravelSpriteTimingMode.PreservePrefabFps)
            {
                return spriteFPS;
            }

            return _travelSpriteFPS > 0f ? _travelSpriteFPS : spriteFPS;
        }

        private float ResolveTravelSpriteFps(
            CombatVFXLibrary.VFXTravelSpriteTimingMode timingMode,
            float travelDuration)
        {
            if (spriteSequence == null || spriteSequence.Length == 0 || travelDuration <= 0f)
                return spriteFPS;

            switch (timingMode)
            {
                case CombatVFXLibrary.VFXTravelSpriteTimingMode.FitFrameSpanToTravel:
                    return spriteSequence.Length > 1
                        ? (spriteSequence.Length - 1) / travelDuration
                        : spriteFPS;

                case CombatVFXLibrary.VFXTravelSpriteTimingMode.FitEffectLifetimeToTravel:
                    return spriteSequence.Length / travelDuration;

                case CombatVFXLibrary.VFXTravelSpriteTimingMode.PreservePrefabFps:
                default:
                    return spriteFPS;
            }
        }

        private void SetVisibility(bool visible)
        {
            // Toggle renderers â€” keeps ParticleSystems ticking
            var renderers = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
                renderers[i].enabled = visible;
        }

        private void OnValidate()
        {
            spriteMaterialSlotIndex = Mathf.Max(0, spriteMaterialSlotIndex);
            _hasSpriteRendererAuthoredLocalScale = false;
            _hasSpriteBillboardRotationOffset = false;
            CaptureSpriteRendererAuthoredLocalScale();
            CaptureSpriteBillboardRotationOffset();
        }
    }
}
