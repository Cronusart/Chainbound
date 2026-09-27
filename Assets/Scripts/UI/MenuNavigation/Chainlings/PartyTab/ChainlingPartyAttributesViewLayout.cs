using System;
using System.Collections.Generic;
using Chainbound.Combat;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SignalJam.UI.MenuNavigation.PartyTab
{
    /// <summary>
    /// Prefab-authored Attributes radar binder. Never instantiates axis/radar children at runtime.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ChainlingPartyAttributesViewLayout : MonoBehaviour
    {
        [Serializable]
        public sealed class AxisSlot
        {
            public string label = "MATK";
            public Image icon;
            public TMP_Text valueLabel;
            public CanvasGroup iconCanvasGroup;
            public RectTransform motionTarget;
        }

        [Header("Radar (authored)")]
        [SerializeField] private PartyTabAttributeRadarGraphic radarGraphic;
        [SerializeField] private PartyTabAttributeRadarGraphic glowGraphic;

        [Header("Axes (authored, length 5)")]
        [SerializeField] private AxisSlot[] axes = new AxisSlot[PartyTabAttributeRadarGraphic.AxisCount];

        [Header("Icons")]
        [SerializeField] private Sprite[] attributeIcons = new Sprite[PartyTabAttributeRadarGraphic.AxisCount];
        [SerializeField] private bool tintIconsFromType = true;
        [Tooltip("Off by default — keep prefab-authored icon/label RectTransforms. Only enable to snap slots onto radar vertices.")]
        [SerializeField] private bool repositionAxesToRadarVertices;
        [SerializeField] [Range(0.5f, 1.4f)] private float axisRadiusScale = 1.12f;

        [Header("Type Chrome")]
        [SerializeField] private ChainlingPartyTypeChromePaletteSO typeChromePalette;

        [Header("Footer (optional / unused)")]
        [Tooltip("Legacy. Leave null — Attributes radar no longer uses a progression footer.")]
        [SerializeField] private TMP_Text progressionFooter;

        [Header("Radar Animation")]
        [SerializeField] private bool animateRadarOnBind = true;
        [SerializeField] [Min(0f)] private float radarFillDuration = 0.42f;
        [SerializeField] private Ease radarFillEase = Ease.OutCubic;
        [SerializeField] [Min(0f)] private float animationStartDelay = 0.12f;

        [Header("Icon Animation")]
        [SerializeField] private bool animateIconsOnBind = true;
        [SerializeField] [Min(0f)] private float iconStaggerSeconds = 0.04f;
        [SerializeField] [Min(0f)] private float iconPopDuration = 0.18f;
        [SerializeField] private float iconPopScale = 1.08f;
        [SerializeField] private Ease iconPopEase = Ease.OutBack;

        [Header("Label Animation")]
        [SerializeField] private bool animateLabelsOnBind = true;
        [SerializeField] [Min(0f)] private float labelFadeDuration = 0.16f;

        [Header("Tween Safety")]
        [SerializeField] private bool useUnscaledTime = true;
        [SerializeField] private bool killTweensOnDisable = true;

        private readonly float[] targetFills = new float[PartyTabAttributeRadarGraphic.AxisCount];
        private readonly float[] animatedFills = new float[PartyTabAttributeRadarGraphic.AxisCount];
        private Tween radarTween;
        private Sequence iconSequence;
        private Sequence labelSequence;

        /// <summary>
        /// Stagger order for icon/label reveal — clockwise from top-left RATK.
        /// Axis data order stays <see cref="ChainlingAttributeDisplayUtility.PartyRadarAxisLabels"/>:
        /// SPD=0, MDEF=1, MATK=2, RATK=3, RDEF=4.
        /// </summary>
        private static readonly int[] IconPresentationOrder = { 3, 2, 1, 0, 4 };

        public void ConfigureTypeChromePalette(ChainlingPartyTypeChromePaletteSO palette)
        {
            if (palette != null)
                typeChromePalette = palette;
        }

        public void Bind(ChainlingPartyColumnSummary summary)
        {
            KillTweens(complete: false);

            if (summary == null || summary.isEmpty)
            {
                BindEmpty(summary != null ? summary.emptyMessage : "—");
                return;
            }

            Color typeBg = ResolveTypeBackground(summary);
            ApplyRadarColors(summary, typeBg);
            ApplyAxisVisuals(summary, typeBg);

            if (progressionFooter != null)
                progressionFooter.text = summary.attributeProgressionFooter ?? "";

            for (int i = 0; i < PartyTabAttributeRadarGraphic.AxisCount; i++)
            {
                float fill = 0f;
                if (summary.attributeAxes != null && i < summary.attributeAxes.Count)
                    fill = summary.attributeAxes[i].Fill01;
                targetFills[i] = fill;
            }

            // Prefer authored prefab placement; optional snap is opt-in only.
            if (repositionAxesToRadarVertices && radarGraphic != null)
                LayoutAxesOnRadar();

            bool play = Application.isPlaying;
            if (play && animateRadarOnBind && radarFillDuration > 0f)
                PlayRadarFillAnimation();
            else
                ApplyFillsImmediate(targetFills);

            if (play && animateIconsOnBind)
                PlayIconAnimation();
            else
                ResetIconVisualState(visible: true);

            if (play && animateLabelsOnBind)
                PlayLabelAnimation();
            else
                ResetLabelVisualState(visible: true);
        }

        public void BindEmpty(string message)
        {
            KillTweens(complete: false);
            Array.Clear(targetFills, 0, targetFills.Length);
            ApplyFillsImmediate(targetFills);

            if (progressionFooter != null)
                progressionFooter.text = string.IsNullOrWhiteSpace(message) ? "—" : message;

            for (int i = 0; i < axes.Length; i++)
            {
                AxisSlot slot = axes[i];
                if (slot == null)
                    continue;
                if (slot.valueLabel != null)
                    slot.valueLabel.text = "—";
            }

            ResetIconVisualState(visible: false);
            ResetLabelVisualState(visible: true);
        }

        private void OnDisable()
        {
            if (killTweensOnDisable)
                KillTweens(complete: false);
        }

        private void OnDestroy()
        {
            KillTweens(complete: false);
        }

        private void ApplyAxisVisuals(ChainlingPartyColumnSummary summary, Color typeBackground)
        {
            IReadOnlyList<ChainlingAttributeRadarAxis> axisData = summary.attributeAxes;
            for (int i = 0; i < PartyTabAttributeRadarGraphic.AxisCount; i++)
            {
                if (axes == null || i >= axes.Length || axes[i] == null)
                    continue;

                AxisSlot slot = axes[i];
                ChainlingAttributeRadarAxis data = axisData != null && i < axisData.Count
                    ? axisData[i]
                    : default;

                if (slot.icon != null)
                {
                    if (attributeIcons != null && i < attributeIcons.Length && attributeIcons[i] != null)
                        slot.icon.sprite = attributeIcons[i];
                    slot.icon.enabled = slot.icon.sprite != null;
                    if (tintIconsFromType)
                        slot.icon.color = typeBackground;
                }

                if (slot.valueLabel != null)
                    slot.valueLabel.text = data.Total > 0 || (axisData != null && i < axisData.Count)
                        ? data.Total.ToString()
                        : "—";
            }
        }

        private void ApplyRadarColors(ChainlingPartyColumnSummary summary, Color typeBackground)
        {
            Color stroke = typeChromePalette != null
                ? typeChromePalette.ResolveRadarStrokeColor(summary.primaryType, 0.35f, summary.isEntangled)
                : Color.Lerp(typeBackground, Color.white, 0.35f);

            Color fill = stroke;
            if (typeChromePalette == null ||
                !typeChromePalette.TryResolveRadarFillColor(summary.primaryType, out fill))
            {
                fill = stroke;
                fill.a = 0.28f;
            }

            Color grid = stroke;
            grid.a = 0.35f;

            if (radarGraphic != null)
            {
                radarGraphic.SetTypeBackgroundColor(typeBackground);
                radarGraphic.SetResolvedColors(stroke, fill, grid);
            }

            if (glowGraphic != null)
            {
                Color glowStroke = stroke;
                glowStroke.a = 1f;
                glowGraphic.SetTypeBackgroundColor(typeBackground);
                glowGraphic.SetResolvedColors(glowStroke, fill, grid);
            }
        }

        private Color ResolveTypeBackground(ChainlingPartyColumnSummary summary)
        {
            ChainlingType type = summary.primaryType;
            Color baseColor = typeChromePalette != null
                ? typeChromePalette.ResolveBackgroundColor(type)
                : ChainlingTypeDisplaySO.CreateFallback(type).badgeColor;

            if (summary.isEntangled && typeChromePalette != null)
                baseColor = typeChromePalette.ResolveEntangledBackgroundColor(baseColor);

            return baseColor;
        }

        private void LayoutAxesOnRadar()
        {
            for (int i = 0; i < PartyTabAttributeRadarGraphic.AxisCount; i++)
            {
                if (axes == null || i >= axes.Length || axes[i] == null)
                    continue;

                RectTransform target = axes[i].motionTarget != null
                    ? axes[i].motionTarget
                    : axes[i].icon != null ? axes[i].icon.rectTransform : null;
                if (target == null)
                    continue;

                Vector2 local = radarGraphic.GetAxisLocalPosition(i, axisRadiusScale);
                if (target.parent == radarGraphic.rectTransform)
                {
                    target.anchoredPosition = local;
                }
                else if (target.parent is RectTransform parent)
                {
                    Vector3 world = radarGraphic.rectTransform.TransformPoint(local);
                    target.anchoredPosition = parent.InverseTransformPoint(world);
                }
            }
        }

        private void ApplyFillsImmediate(float[] fills)
        {
            for (int i = 0; i < animatedFills.Length; i++)
                animatedFills[i] = fills != null && i < fills.Length ? fills[i] : 0f;

            radarGraphic?.SetFills(animatedFills);
            glowGraphic?.SetFills(animatedFills);
        }

        private void PlayRadarFillAnimation()
        {
            Array.Clear(animatedFills, 0, animatedFills.Length);
            ApplyFillsImmediate(animatedFills);

            float[] from = new float[PartyTabAttributeRadarGraphic.AxisCount];
            float[] to = (float[])targetFills.Clone();

            radarTween = DOVirtual.Float(0f, 1f, radarFillDuration, t =>
                {
                    for (int i = 0; i < animatedFills.Length; i++)
                        animatedFills[i] = Mathf.Lerp(from[i], to[i], t);
                    radarGraphic?.SetFills(animatedFills);
                    glowGraphic?.SetFills(animatedFills);
                })
                .SetEase(radarFillEase)
                .SetUpdate(useUnscaledTime)
                .SetDelay(animationStartDelay)
                .SetTarget(radarGraphic != null ? (UnityEngine.Object)radarGraphic : this);
        }

        private void PlayIconAnimation()
        {
            iconSequence = DOTween.Sequence().SetUpdate(useUnscaledTime).SetDelay(animationStartDelay);
            for (int step = 0; step < IconPresentationOrder.Length; step++)
            {
                int i = IconPresentationOrder[step];
                if (i < 0 || i >= axes.Length)
                    continue;

                AxisSlot slot = axes[i];
                if (slot == null)
                    continue;

                RectTransform motion = slot.motionTarget != null
                    ? slot.motionTarget
                    : slot.icon != null ? slot.icon.rectTransform : null;
                CanvasGroup group = slot.iconCanvasGroup;
                float at = step * iconStaggerSeconds;

                if (group != null)
                {
                    group.alpha = 0f;
                    iconSequence.Insert(
                        at,
                        group.DOFade(1f, iconPopDuration).SetEase(Ease.OutSine).SetTarget(group));
                }

                if (motion != null)
                {
                    Vector3 baseScale = motion.localScale;
                    if (baseScale == Vector3.zero)
                        baseScale = Vector3.one;
                    motion.localScale = baseScale * 0.82f;
                    iconSequence.Insert(
                        at,
                        motion.DOScale(baseScale * iconPopScale, iconPopDuration * 0.65f)
                            .SetEase(iconPopEase)
                            .SetTarget(motion));
                    iconSequence.Insert(
                        at + iconPopDuration * 0.65f,
                        motion.DOScale(baseScale, iconPopDuration * 0.35f)
                            .SetEase(Ease.OutSine)
                            .SetTarget(motion));
                }
            }
        }

        private void PlayLabelAnimation()
        {
            labelSequence = DOTween.Sequence().SetUpdate(useUnscaledTime).SetDelay(animationStartDelay);
            for (int step = 0; step < IconPresentationOrder.Length; step++)
            {
                int i = IconPresentationOrder[step];
                if (i < 0 || i >= axes.Length)
                    continue;

                AxisSlot slot = axes[i];
                if (slot?.valueLabel == null)
                    continue;

                Color c = slot.valueLabel.color;
                c.a = 0f;
                slot.valueLabel.color = c;
                labelSequence.Insert(
                    step * iconStaggerSeconds,
                    slot.valueLabel.DOFade(1f, labelFadeDuration).SetEase(Ease.OutSine).SetTarget(slot.valueLabel));
            }
        }

        private void ResetIconVisualState(bool visible)
        {
            for (int i = 0; i < axes.Length; i++)
            {
                AxisSlot slot = axes[i];
                if (slot == null)
                    continue;
                if (slot.iconCanvasGroup != null)
                    slot.iconCanvasGroup.alpha = visible ? 1f : 0f;
                RectTransform motion = slot.motionTarget != null
                    ? slot.motionTarget
                    : slot.icon != null ? slot.icon.rectTransform : null;
                if (motion != null && motion.localScale == Vector3.zero)
                    motion.localScale = Vector3.one;
            }
        }

        private void ResetLabelVisualState(bool visible)
        {
            for (int i = 0; i < axes.Length; i++)
            {
                AxisSlot slot = axes[i];
                if (slot?.valueLabel == null)
                    continue;
                Color c = slot.valueLabel.color;
                c.a = visible ? 1f : 0f;
                slot.valueLabel.color = c;
            }
        }

        private void KillTweens(bool complete)
        {
            if (radarTween != null && radarTween.IsActive())
            {
                if (complete)
                    radarTween.Complete();
                else
                    radarTween.Kill();
            }

            radarTween = null;

            if (iconSequence != null && iconSequence.IsActive())
            {
                if (complete)
                    iconSequence.Complete();
                else
                    iconSequence.Kill();
            }

            iconSequence = null;

            if (labelSequence != null && labelSequence.IsActive())
            {
                if (complete)
                    labelSequence.Complete();
                else
                    labelSequence.Kill();
            }

            labelSequence = null;

            if (radarGraphic != null)
                DOTween.Kill(radarGraphic);
            if (glowGraphic != null)
                DOTween.Kill(glowGraphic);
        }
    }
}
