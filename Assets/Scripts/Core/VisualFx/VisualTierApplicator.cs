using TMPro;
using UnityEngine;

namespace SignalJam.VisualFx
{
    /// <summary>
    /// Scene/prefab binding that applies a catalog tier across typography, mesh, and aura channels.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VisualTierApplicator : MonoBehaviour
    {
        [Header("Catalog")]
        [SerializeField] private VisualVariantCatalogSO catalog;
        [SerializeField] private string tierId;
        [SerializeField] private int tierIndex;
        [SerializeField] private bool preferTierId;

        [Header("Channels")]
        [SerializeField] private VisualTierChannelMask channelMask = VisualTierChannelMask.All;

        [Header("Typography Targets")]
        [SerializeField] private TMP_Text[] typographyTargets;
        [SerializeField] private float typographyBaseFontSize;
        [SerializeField] private float typographyExternalScaleMultiplier = 1f;

        [Header("Mesh Targets")]
        [SerializeField] private Renderer[] meshTargets;
        [SerializeField] private bool clearMeshPropertyBlocksOnClear = true;

        [Header("Aura")]
        [SerializeField] private Transform auraAttachPoint;
        [SerializeField] private ParticleSystem[] boundAuraSystems;
        [SerializeField] private bool destroySpawnedAuraOnClear = true;

        [Header("Playback")]
        [SerializeField] private bool applyOnEnable = true;

        private VisualTierAuraInstance auraInstance;
        private MaterialPropertyBlock meshPropertyBlock;

        public VisualVariantCatalogSO Catalog
        {
            get => catalog;
            set => catalog = value;
        }

        public VisualTierProfileSO ActiveProfile { get; private set; }

        private void OnEnable()
        {
            if (applyOnEnable)
                ApplyCurrentTier();
        }

        private void OnDisable()
        {
            ClearAppliedTier(stopAuraOnly: true);
        }

        public void SetCatalog(VisualVariantCatalogSO variantCatalog)
        {
            catalog = variantCatalog;
        }

        public void ApplyTierByIndex(int index)
        {
            tierIndex = index;
            preferTierId = false;
            ApplyCurrentTier();
        }

        public void ApplyTierById(string id)
        {
            tierId = id;
            preferTierId = true;
            ApplyCurrentTier();
        }

        public void ApplyProfile(VisualTierProfileSO profile)
        {
            ActiveProfile = profile;
            ApplyProfileInternal(profile);
        }

        public void ApplyCurrentTier()
        {
            VisualTierProfileSO profile = ResolveProfile();
            ApplyProfile(profile);
        }

        public void ClearAppliedTier(bool stopAuraOnly = false)
        {
            if ((channelMask & VisualTierChannelMask.Typography) != 0 && typographyTargets != null)
            {
                for (int i = 0; i < typographyTargets.Length; i++)
                    VisualTierTypographyApplication.ClearMaterialInstance(typographyTargets[i]);
            }

            if ((channelMask & VisualTierChannelMask.Mesh) != 0 &&
                clearMeshPropertyBlocksOnClear &&
                meshTargets != null)
            {
                for (int i = 0; i < meshTargets.Length; i++)
                {
                    Renderer renderer = meshTargets[i];
                    if (renderer == null)
                        continue;

                    int slot = ActiveProfile != null && ActiveProfile.MeshPreset != null
                        ? ActiveProfile.MeshPreset.MaterialSlotIndex
                        : 0;
                    VisualTierMeshApplication.ClearPropertyBlock(renderer, slot);
                }
            }

            if ((channelMask & VisualTierChannelMask.Aura) != 0 && auraInstance != null)
            {
                VisualTierAuraApplication.StopAndDestroy(auraInstance, destroySpawnedAuraOnClear);
                auraInstance = null;
            }

            if (!stopAuraOnly)
                ActiveProfile = null;
        }

        private VisualTierProfileSO ResolveProfile()
        {
            if (catalog == null)
                return null;

            if (preferTierId && !string.IsNullOrWhiteSpace(tierId) &&
                catalog.TryResolveById(tierId, out VisualTierProfileSO byId))
                return byId;

            return catalog.ResolveByIndex(tierIndex);
        }

        private void ApplyProfileInternal(VisualTierProfileSO profile)
        {
            if (profile == null)
                return;

            if ((channelMask & VisualTierChannelMask.Typography) != 0 && typographyTargets != null)
            {
                for (int i = 0; i < typographyTargets.Length; i++)
                {
                    TMP_Text text = typographyTargets[i];
                    if (text == null)
                        continue;

                    VisualTierTypographyApplication.Apply(
                        text,
                        profile,
                        typographyBaseFontSize,
                        typographyExternalScaleMultiplier);
                }
            }

            if ((channelMask & VisualTierChannelMask.Mesh) != 0 && meshTargets != null)
            {
                meshPropertyBlock ??= new MaterialPropertyBlock();
                for (int i = 0; i < meshTargets.Length; i++)
                {
                    Renderer renderer = meshTargets[i];
                    if (renderer == null)
                        continue;

                    VisualTierMeshApplication.Apply(renderer, profile, meshPropertyBlock);
                }
            }

            if ((channelMask & VisualTierChannelMask.Aura) != 0)
            {
                Transform attach = auraAttachPoint != null ? auraAttachPoint : transform;
                auraInstance = VisualTierAuraApplication.ApplyOrConfigure(
                    attach,
                    profile,
                    boundAuraSystems,
                    auraInstance);
            }
        }
    }
}
