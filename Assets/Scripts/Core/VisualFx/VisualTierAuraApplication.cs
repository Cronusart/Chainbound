using UnityEngine;

namespace SignalJam.VisualFx
{
    /// <summary>
    /// Configures or spawns particle aura presets on sprites, UI, or 3D targets.
    /// </summary>
    public static class VisualTierAuraApplication
    {
        public static VisualTierAuraInstance ApplyOrConfigure(
            Transform attachTarget,
            VisualTierProfileSO profile,
            ParticleSystem[] existingSystems = null,
            VisualTierAuraInstance existingInstance = null)
        {
            if (attachTarget == null || profile == null)
                return existingInstance;

            VisualTierAuraPresetSO preset = profile.AuraPreset;
            if (preset == null)
                return existingInstance;

            VisualTierAuraInstance instance = existingInstance;
            ParticleSystem[] systems = existingSystems;

            if (preset.AuraPrefab != null)
            {
                Transform auraRoot;
                if (instance == null)
                {
                    GameObject spawned = Object.Instantiate(preset.AuraPrefab, attachTarget);
                    spawned.name = $"{preset.name}_Aura";
                    auraRoot = spawned.transform;
                    instance = spawned.GetComponent<VisualTierAuraInstance>();
                    if (instance == null)
                        instance = spawned.AddComponent<VisualTierAuraInstance>();
                }
                else
                {
                    auraRoot = instance.transform;
                    auraRoot.SetParent(attachTarget, false);
                }

                ConfigureTransform(auraRoot, preset);
                instance.Initialize(attachTarget, preset, profile);
                systems = instance.ParticleSystems;
            }
            else if (systems == null || systems.Length == 0)
            {
                systems = attachTarget.GetComponentsInChildren<ParticleSystem>(true);
            }

            if (instance == null && systems != null && systems.Length > 0)
            {
                instance = attachTarget.GetComponent<VisualTierAuraInstance>();
                if (instance == null)
                    instance = attachTarget.gameObject.AddComponent<VisualTierAuraInstance>();
                instance.Initialize(attachTarget, preset, profile, systems);
            }

            ConfigureSystems(systems, preset, profile.ResolvePaletteForAura());

            if (instance != null)
                instance.RefreshPulseDriver();

            return instance;
        }

        public static void StopAndDestroy(VisualTierAuraInstance instance, bool destroyInstanceRoot = true)
        {
            if (instance == null)
                return;

            instance.StopImmediate();

            if (destroyInstanceRoot)
                Object.Destroy(instance.gameObject);
        }

        private static void ConfigureTransform(Transform auraTransform, VisualTierAuraPresetSO preset)
        {
            if (auraTransform == null || preset == null)
                return;

            if (preset.AttachMode != VisualTierAuraAttachMode.WorldFollow)
            {
                auraTransform.localPosition = preset.LocalPosition;
                auraTransform.localRotation = Quaternion.Euler(preset.LocalEulerAngles);
            }

            Vector3 scale = preset.LocalScale;
            if (preset.InheritTargetLossyScale && auraTransform.parent != null)
                scale = Vector3.Scale(scale, auraTransform.parent.lossyScale);

            auraTransform.localScale = scale;
        }

        private static void ConfigureSystems(
            ParticleSystem[] systems,
            VisualTierAuraPresetSO preset,
            VisualTierPaletteSO palette)
        {
            if (systems == null || preset == null)
                return;

            Color startColor = VisualTierPaletteUtility.ResolveTintedSlot(
                palette,
                preset.StartColorSlot,
                preset.CustomStartColor,
                preset.StartColorTint,
                Color.white);

            Color lifetimeColor = VisualTierPaletteUtility.ResolveSlot(
                palette,
                preset.ColorOverLifetimeSlot,
                preset.CustomColorOverLifetime,
                Color.white);
            lifetimeColor.a = preset.ColorOverLifetimeAlpha;

            for (int i = 0; i < systems.Length; i++)
            {
                ParticleSystem ps = systems[i];
                if (ps == null)
                    continue;

                ParticleSystem.MainModule main = ps.main;
                main.loop = preset.Loop;
                main.simulationSpace = preset.SimulationSpace;

                if (!preset.PreserveAuthoringModules || !Mathf.Approximately(preset.StartSizeMultiplier, 1f))
                {
                    main.startSizeMultiplier = preset.PreserveAuthoringModules
                        ? main.startSizeMultiplier * preset.StartSizeMultiplier
                        : preset.StartSizeMultiplier;
                }

                if (!preset.PreserveAuthoringModules || !Mathf.Approximately(preset.StartSpeedMultiplier, 1f))
                {
                    main.startSpeedMultiplier = preset.PreserveAuthoringModules
                        ? main.startSpeedMultiplier * preset.StartSpeedMultiplier
                        : preset.StartSpeedMultiplier;
                }

                if (!preset.PreserveAuthoringModules || !Mathf.Approximately(preset.LifetimeMultiplier, 1f))
                {
                    main.startLifetimeMultiplier = preset.PreserveAuthoringModules
                        ? main.startLifetimeMultiplier * preset.LifetimeMultiplier
                        : preset.LifetimeMultiplier;
                }

                main.startColor = new ParticleSystem.MinMaxGradient(startColor);

                if (preset.MaxParticlesOverride > 0)
                    main.maxParticles = preset.MaxParticlesOverride;

                ParticleSystem.EmissionModule emission = ps.emission;
                if (!Mathf.Approximately(preset.EmissionRateMultiplier, 1f))
                {
                    ParticleSystem.MinMaxCurve rate = emission.rateOverTime;
                    rate.constant *= preset.EmissionRateMultiplier;
                    emission.rateOverTime = rate;
                }

                ParticleSystem.ShapeModule shape = ps.shape;
                if (shape.enabled && !Mathf.Approximately(preset.ShapeScaleMultiplier, 1f))
                    shape.scale *= preset.ShapeScaleMultiplier;

                if (preset.DriveColorOverLifetime)
                {
                    ParticleSystem.ColorOverLifetimeModule colorOverLifetime = ps.colorOverLifetime;
                    colorOverLifetime.enabled = true;
                    Gradient gradient = new Gradient();
                    gradient.SetKeys(
                        new[]
                        {
                            new GradientColorKey(startColor, 0f),
                            new GradientColorKey(lifetimeColor, 1f),
                        },
                        new[]
                        {
                            new GradientAlphaKey(startColor.a, 0f),
                            new GradientAlphaKey(lifetimeColor.a, 1f),
                        });
                    colorOverLifetime.color = gradient;
                }

                ParticleSystemRenderer renderer = ps.GetComponent<ParticleSystemRenderer>();
                if (renderer != null)
                {
                    if (preset.RendererMaterialOverride != null)
                        renderer.sharedMaterial = preset.RendererMaterialOverride;

                    if (!string.IsNullOrWhiteSpace(preset.SortingLayerName))
                        renderer.sortingLayerName = preset.SortingLayerName;

                    if (preset.SortingOrder != 0)
                        renderer.sortingOrder = preset.SortingOrder;
                }

                if (preset.PlayOnApply)
                {
                    ps.Clear(true);
                    ps.Play(true);
                }
            }
        }
    }
}
