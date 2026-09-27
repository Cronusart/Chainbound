using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SignalJam.UI.MenuNavigation
{
    [DisallowMultipleComponent]
    public sealed class PlayerMenuPanelTransitionController : MonoBehaviour
    {
        private static readonly int GlitchIntensityId = Shader.PropertyToID("_GlitchIntensity");
        private static readonly int SliceDisplacementId = Shader.PropertyToID("_SliceDisplacement");
        private static readonly int SliceBandCountId = Shader.PropertyToID("_SliceBandCount");
        private static readonly int ScanlineCountId = Shader.PropertyToID("_ScanlineCount");
        private static readonly int ScanlineSpeedId = Shader.PropertyToID("_ScanlineSpeed");
        private static readonly int ScanlineAlphaId = Shader.PropertyToID("_ScanlineAlpha");
        private static readonly int StaticAmountId = Shader.PropertyToID("_StaticAmount");
        private static readonly int DissolveThresholdId = Shader.PropertyToID("_DissolveThreshold");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private const string DefaultShaderName = "SignalJam/UI/SignalGlitchOverlay";

        [SerializeField] private PlayerMenuPanelTransitionProfileSO profile;
        [SerializeField] private bool transitionsEnabled = true;
        [SerializeField] private bool includeInactiveGraphics;
        [SerializeField] private bool debugLogs;

        private readonly List<GraphicBinding> graphicBindings = new List<GraphicBinding>();
        private readonly List<Material> runtimeMaterials = new List<Material>();
        private readonly List<CanvasGroup> affectedCanvasGroups = new List<CanvasGroup>();
        private Coroutine transitionRoutine;

        public bool IsPlaying => transitionRoutine != null;
        public PlayerMenuPanelTransitionProfileSO Profile => profile;

        public void ConfigureProfile(PlayerMenuPanelTransitionProfileSO transitionProfile)
        {
            if (transitionProfile != null)
                profile = transitionProfile;
        }

        public void PlaySwap(
            Transform outgoingRoot,
            Action swapAction,
            Transform incomingRoot,
            Action onComplete = null)
        {
            Cancel(restoreMaterials: true);

            if (!CanAnimate())
            {
                swapAction?.Invoke();
                onComplete?.Invoke();
                return;
            }

            transitionRoutine = StartCoroutine(PlaySwapRoutine(outgoingRoot, swapAction, incomingRoot, onComplete));
        }

        public void PlayIn(Transform incomingRoot, Action onComplete = null)
        {
            PlaySwap(null, null, incomingRoot, onComplete);
        }

        public void PlayOut(Transform outgoingRoot, Action onComplete = null)
        {
            PlaySwap(outgoingRoot, null, null, onComplete);
        }

        public void Cancel(bool restoreMaterials = true)
        {
            if (transitionRoutine != null)
            {
                StopCoroutine(transitionRoutine);
                transitionRoutine = null;
            }

            DOTween.Kill(this);

            RestoreCanvasGroups();

            if (restoreMaterials)
                RestoreBoundMaterials();

            DestroyRuntimeMaterials();
        }

        /// <summary>
        /// Re-enables any CanvasGroups this controller faded for a transition.
        /// Safe to call after a killed animation so Back/B is never left dead.
        /// </summary>
        public void RestoreCanvasGroups()
        {
            for (int i = 0; i < affectedCanvasGroups.Count; i++)
            {
                CanvasGroup group = affectedCanvasGroups[i];
                if (group == null)
                    continue;

                group.alpha = 1f;
                group.interactable = true;
                group.blocksRaycasts = true;
            }

            affectedCanvasGroups.Clear();
        }

        private void OnDisable()
        {
            Cancel(restoreMaterials: true);
        }

        private IEnumerator PlaySwapRoutine(
            Transform outgoingRoot,
            Action swapAction,
            Transform incomingRoot,
            Action onComplete)
        {
            yield return PlayPhase(outgoingRoot, fadeIn: false);

            if (ResolveSwapHoldDuration() > 0f)
                yield return Wait(ResolveSwapHoldDuration());

            swapAction?.Invoke();

            PrepareIncomingRoot(incomingRoot);
            yield return null;
            yield return PlayPhase(incomingRoot, fadeIn: true);

            transitionRoutine = null;
            affectedCanvasGroups.Clear();
            onComplete?.Invoke();
        }

        private IEnumerator PlayPhase(Transform root, bool fadeIn)
        {
            if (root == null)
                yield break;

            if (fadeIn)
                root.gameObject.SetActive(true);

            CanvasGroup group = GetOrAddCanvasGroup(root.gameObject);
            if (group == null)
                yield break;

            float duration = fadeIn ? ResolveFadeInDuration() : ResolveFadeOutDuration();
            if (duration <= 0f)
            {
                group.alpha = fadeIn ? 1f : 0f;
                group.interactable = fadeIn;
                group.blocksRaycasts = fadeIn;
                yield break;
            }

            Material runtimeMaterial = CreateRuntimeMaterial();
            BindGraphics(root, runtimeMaterial);

            float fromAlpha = fadeIn ? 0f : 1f;
            float toAlpha = fadeIn ? 1f : 0f;
            float peak = ResolvePeakGlitchIntensity();
            float fromGlitch = fadeIn ? peak : 0f;
            float toGlitch = fadeIn ? 0f : peak;

            group.alpha = fromAlpha;
            group.interactable = false;
            group.blocksRaycasts = false;
            RememberCanvasGroup(group);
            SetMaterialIntensity(runtimeMaterial, fromGlitch);

            bool complete = false;
            Sequence sequence = DOTween.Sequence()
                .SetId(this)
                .SetUpdate(ResolveUseUnscaledTime());

            sequence.Join(group
                .DOFade(toAlpha, duration)
                .SetEase(fadeIn ? ResolveFadeInEase() : ResolveFadeOutEase())
                .SetUpdate(ResolveUseUnscaledTime()));

            sequence.Join(DOTween
                .To(
                    () => fromGlitch,
                    value => SetMaterialIntensity(runtimeMaterial, value),
                    toGlitch,
                    duration)
                .SetEase(Ease.Linear)
                .SetUpdate(ResolveUseUnscaledTime()));

            sequence.OnComplete(() => complete = true);

            while (!complete && sequence.IsActive())
                yield return null;

            group.alpha = toAlpha;
            group.interactable = fadeIn;
            group.blocksRaycasts = fadeIn;
            if (fadeIn)
                RememberCanvasGroup(group);
            RestoreBoundMaterials();
            DestroyRuntimeMaterial(runtimeMaterial);
            runtimeMaterials.Remove(runtimeMaterial);
        }

        private void PrepareIncomingRoot(Transform incomingRoot)
        {
            if (incomingRoot == null)
                return;

            incomingRoot.gameObject.SetActive(true);
            CanvasGroup group = GetOrAddCanvasGroup(incomingRoot.gameObject);
            if (group == null)
                return;

            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
            RememberCanvasGroup(group);
        }

        private void RememberCanvasGroup(CanvasGroup group)
        {
            if (group == null || affectedCanvasGroups.Contains(group))
                return;

            affectedCanvasGroups.Add(group);
        }

        private bool CanAnimate()
        {
            return transitionsEnabled &&
                isActiveAndEnabled &&
                (profile != null && profile.GlitchMaterial != null || Shader.Find(DefaultShaderName) != null);
        }

        private Material CreateRuntimeMaterial()
        {
            Material material = profile != null && profile.GlitchMaterial != null
                ? new Material(profile.GlitchMaterial)
                : new Material(Shader.Find(DefaultShaderName));
            material.name = profile != null && profile.GlitchMaterial != null
                ? $"{profile.GlitchMaterial.name} (PlayerMenu Runtime)"
                : "Signal_Glitch_UI_PlayerMenu Fallback (Runtime)";

            ApplyMaterialDefaults(material, ResolvePeakGlitchIntensity());
            runtimeMaterials.Add(material);
            return material;
        }

        private void ApplyMaterialDefaults(Material material, float intensity)
        {
            if (material == null)
                return;

            if (material.HasProperty(GlitchIntensityId))
                material.SetFloat(GlitchIntensityId, intensity);
            if (material.HasProperty(SliceDisplacementId))
                material.SetFloat(SliceDisplacementId, ResolveSliceDisplacement());
            if (material.HasProperty(SliceBandCountId))
                material.SetFloat(SliceBandCountId, ResolveSliceBandCount());
            if (material.HasProperty(ScanlineCountId))
                material.SetFloat(ScanlineCountId, ResolveScanlineCount());
            if (material.HasProperty(ScanlineSpeedId))
                material.SetFloat(ScanlineSpeedId, ResolveScanlineSpeed());
            if (material.HasProperty(ScanlineAlphaId))
                material.SetFloat(ScanlineAlphaId, ResolveScanlineAlpha());
            if (material.HasProperty(StaticAmountId))
                material.SetFloat(StaticAmountId, ResolveStaticAmount());
            if (material.HasProperty(DissolveThresholdId))
                material.SetFloat(DissolveThresholdId, ResolveDissolveThreshold());
            if (material.HasProperty(ColorId))
                material.SetColor(ColorId, ResolveTint());
        }

        private void SetMaterialIntensity(Material material, float intensity)
        {
            if (material == null)
                return;

            if (material.HasProperty(GlitchIntensityId))
                material.SetFloat(GlitchIntensityId, Mathf.Clamp01(intensity));
        }

        private void BindGraphics(Transform root, Material runtimeMaterial)
        {
            RestoreBoundMaterials();

            if (root == null || runtimeMaterial == null)
                return;

            Graphic[] graphics = root.GetComponentsInChildren<Graphic>(includeInactiveGraphics);
            for (int i = 0; i < graphics.Length; i++)
            {
                Graphic graphic = graphics[i];
                if (!CanBindGraphic(graphic))
                    continue;

                graphicBindings.Add(new GraphicBinding(graphic, graphic.material));
                graphic.material = runtimeMaterial;
            }

            if (debugLogs)
                Debug.Log($"[PlayerMenuPanelTransitionController] Bound {graphicBindings.Count} graphics under {root.name}.", this);
        }

        private bool CanBindGraphic(Graphic graphic)
        {
            if (graphic == null)
                return false;

            if (graphic is TMP_Text)
                return false;

            return graphic is Image || graphic is RawImage;
        }

        private void RestoreBoundMaterials()
        {
            for (int i = 0; i < graphicBindings.Count; i++)
            {
                GraphicBinding binding = graphicBindings[i];
                if (binding.Graphic != null)
                    binding.Graphic.material = binding.OriginalMaterial;
            }

            graphicBindings.Clear();
        }

        private static void DestroyRuntimeMaterial(Material material)
        {
            if (material == null)
                return;

            if (Application.isPlaying)
                Destroy(material);
            else
                DestroyImmediate(material);
        }

        private void DestroyRuntimeMaterials()
        {
            for (int i = 0; i < runtimeMaterials.Count; i++)
                DestroyRuntimeMaterial(runtimeMaterials[i]);

            runtimeMaterials.Clear();
        }

        private object Wait(float seconds)
        {
            if (ResolveUseUnscaledTime())
                return new WaitForSecondsRealtime(seconds);

            return new WaitForSeconds(seconds);
        }

        private float ResolveFadeOutDuration() => profile != null ? profile.FadeOutDuration : 0.14f;
        private float ResolveSwapHoldDuration() => profile != null ? profile.SwapHoldDuration : 0.02f;
        private float ResolveFadeInDuration() => profile != null ? profile.FadeInDuration : 0.16f;
        private Ease ResolveFadeOutEase() => profile != null ? profile.FadeOutEase : Ease.InSine;
        private Ease ResolveFadeInEase() => profile != null ? profile.FadeInEase : Ease.OutSine;
        private bool ResolveUseUnscaledTime() => profile == null || profile.UseUnscaledTime;
        private float ResolvePeakGlitchIntensity() => profile != null ? profile.PeakGlitchIntensity : 1f;
        private float ResolveSliceDisplacement() => profile != null ? profile.SliceDisplacement : 0.2f;
        private float ResolveSliceBandCount() => profile != null ? profile.SliceBandCount : 12f;
        private float ResolveScanlineCount() => profile != null ? profile.ScanlineCount : 11f;
        private float ResolveScanlineSpeed() => profile != null ? profile.ScanlineSpeed : 4f;
        private float ResolveScanlineAlpha() => profile != null ? profile.ScanlineAlpha : 0.555f;
        private float ResolveStaticAmount() => profile != null ? profile.StaticAmount : 0.8f;
        private float ResolveDissolveThreshold() => profile != null ? profile.DissolveThreshold : 0f;
        private Color ResolveTint() => profile != null ? profile.Tint : new Color(0.78039217f, 0.8627451f, 0.8156863f, 1f);

        private static CanvasGroup GetOrAddCanvasGroup(GameObject target)
        {
            if (target == null)
                return null;

            CanvasGroup group = target.GetComponent<CanvasGroup>();
            return group != null ? group : target.AddComponent<CanvasGroup>();
        }

        private readonly struct GraphicBinding
        {
            public readonly Graphic Graphic;
            public readonly Material OriginalMaterial;

            public GraphicBinding(Graphic graphic, Material originalMaterial)
            {
                Graphic = graphic;
                OriginalMaterial = originalMaterial;
            }
        }
    }
}
