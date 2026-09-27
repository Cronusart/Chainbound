using UnityEngine;

namespace Chainbound.Combat
{
    public abstract class ChainlingVisualDefinitionSO : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string id = ChainlingVisualIdentity.NoneId;
        [SerializeField] private string displayName;
        [Tooltip("Optional species ID this cosmetic is valid for. Empty means any species.")]
        [SerializeField] private string speciesScope;
        [Tooltip("Optional visual variant ID this cosmetic is valid for. Empty means any variant.")]
        [SerializeField] private string visualVariantScope;

        public string Id => ChainlingVisualIdentity.NormalizeId(id, ChainlingVisualIdentity.NoneId);
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? Id : displayName;
        public string SpeciesScope => speciesScope != null ? speciesScope.Trim() : string.Empty;
        public string VisualVariantScope => visualVariantScope != null ? visualVariantScope.Trim() : string.Empty;

        public bool IsCompatibleWith(string speciesId, string visualVariantId)
        {
            string normalizedSpecies = speciesId != null ? speciesId.Trim() : string.Empty;
            string normalizedVariant = ChainlingVisualIdentity.NormalizeId(visualVariantId, ChainlingVisualIdentity.BaseVariantId);

            return (string.IsNullOrWhiteSpace(SpeciesScope) || string.Equals(SpeciesScope, normalizedSpecies, System.StringComparison.Ordinal)) &&
                   (string.IsNullOrWhiteSpace(VisualVariantScope) || string.Equals(VisualVariantScope, normalizedVariant, System.StringComparison.Ordinal));
        }
    }

    public abstract class ChainlingOverlayDefinitionSO : ChainlingVisualDefinitionSO
    {
        [Header("Overlay")]
        public GameObject prefab;
        public Vector3 localPosition;
        public Vector3 localEulerAngles;
        public Vector3 localScale = Vector3.one;
        public bool mirrorWithChainling = true;

        public Vector3 ResolvedLocalScale => localScale == Vector3.zero ? Vector3.one : localScale;
    }

    public abstract class ChainlingFxDefinitionSO : ChainlingVisualDefinitionSO
    {
        [Header("Effect")]
        public GameObject prefab;
        public SpriteFlipbookVfxDefinitionSO flipbookVfx;
        public Vector3 localPosition;
        public Vector3 localEulerAngles;
        public float scale = 1f;
        public float durationSeconds = 0.2f;
        public bool parentToSlot = true;

        [Header("Optional Body Clip")]
        public SpriteAnimationSequence bodySequence = new SpriteAnimationSequence();
        [Min(0.01f)] public float bodyFps = 8f;

        public bool HasBodySequence => bodySequence != null && bodySequence.Length > 0;

        public float ResolveDurationSeconds()
        {
            float duration = Mathf.Max(0f, durationSeconds);
            if (flipbookVfx != null)
                duration = Mathf.Max(duration, flipbookVfx.GetDurationSeconds());
            return duration;
        }

        public float ResolveBodyDurationSeconds()
        {
            if (!HasBodySequence || bodyFps <= 0f)
                return 0f;

            return bodySequence.Length <= 1 ? 0.05f : (bodySequence.Length - 1) / bodyFps;
        }
    }

    public abstract class ChainlingPoseDefinitionSO : ChainlingVisualDefinitionSO
    {
        [Header("Pose")]
        public SpriteAnimationSequence sequence = new SpriteAnimationSequence();
        [Min(0.01f)] public float fps = 8f;

        [Header("Optional Effect")]
        public GameObject vfxPrefab;
        public SpriteFlipbookVfxDefinitionSO flipbookVfx;
        public Vector3 vfxLocalPosition;
        public float vfxScale = 1f;
        public float vfxDurationSeconds = 0.2f;

        public bool HasSequence => sequence != null && sequence.Length > 0;

        public float ResolveDurationSeconds()
        {
            float duration = 0f;
            if (HasSequence && fps > 0f)
                duration = sequence.Length <= 1 ? 0.05f : (sequence.Length - 1) / fps;

            if (flipbookVfx != null)
                duration = Mathf.Max(duration, flipbookVfx.GetDurationSeconds());

            return Mathf.Max(duration, Mathf.Max(0f, vfxDurationSeconds));
        }
    }

    public abstract class ChainlingCombatAnimationStyleDefinitionSO : ChainlingPoseDefinitionSO
    {
        [Header("Combat Timing")]
        [Tooltip("-1 uses the visual/presenter fallback marker. Non-negative values override the profile marker frame.")]
        public int markerFrame = -1;
    }
}
