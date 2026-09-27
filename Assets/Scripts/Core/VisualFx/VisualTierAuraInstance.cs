using UnityEngine;

namespace SignalJam.VisualFx
{
    /// <summary>
    /// Runtime helper spawned/managed by <see cref="VisualTierAuraApplication"/>.
    /// Optional palette pulse drives emission intensity over time.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VisualTierAuraInstance : MonoBehaviour
    {
        [SerializeField] private ParticleSystem[] particleSystems;

        private Transform followTarget;
        private VisualTierAuraPresetSO preset;
        private VisualTierPaletteSO palette;
        private float baseEmissionRate = 1f;
        private bool hasBaseEmissionRate;

        public ParticleSystem[] ParticleSystems => particleSystems;

        public void Initialize(
            Transform attachTarget,
            VisualTierAuraPresetSO auraPreset,
            VisualTierProfileSO profile,
            ParticleSystem[] boundSystems = null)
        {
            followTarget = attachTarget;
            preset = auraPreset;
            palette = profile != null ? profile.ResolvePaletteForAura() : auraPreset != null ? auraPreset.Palette : null;
            particleSystems = boundSystems != null && boundSystems.Length > 0
                ? boundSystems
                : GetComponentsInChildren<ParticleSystem>(true);

            CacheBaseEmission();
            RefreshPulseDriver();
        }

        public void RefreshPulseDriver()
        {
            enabled = preset != null && preset.AnimateIntensityFromPalettePulse && palette != null;
        }

        public void StopImmediate()
        {
            if (particleSystems == null)
                return;

            for (int i = 0; i < particleSystems.Length; i++)
            {
                ParticleSystem ps = particleSystems[i];
                if (ps == null)
                    continue;

                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        private void LateUpdate()
        {
            if (preset == null || palette == null)
                return;

            if (preset.AttachMode == VisualTierAuraAttachMode.WorldFollow && followTarget != null)
            {
                transform.position = followTarget.position;
                transform.rotation = followTarget.rotation;
            }

            if (!preset.AnimateIntensityFromPalettePulse || particleSystems == null)
                return;

            float pulse = palette.EvaluatePulse01(Time.time) * preset.PulseEmissionMultiplier;
            for (int i = 0; i < particleSystems.Length; i++)
            {
                ParticleSystem ps = particleSystems[i];
                if (ps == null)
                    continue;

                ParticleSystem.EmissionModule emission = ps.emission;
                ParticleSystem.MinMaxCurve rate = emission.rateOverTime;
                rate.constant = (hasBaseEmissionRate ? baseEmissionRate : rate.constant) * pulse;
                emission.rateOverTime = rate;
            }
        }

        private void CacheBaseEmission()
        {
            if (particleSystems == null || particleSystems.Length == 0 || particleSystems[0] == null)
                return;

            baseEmissionRate = particleSystems[0].emission.rateOverTime.constant;
            hasBaseEmissionRate = true;
        }
    }
}
