using TMPro;
using UnityEngine;

namespace SignalJam.VisualFx
{
    /// <summary>
    /// Applies a full-text-bounds gradient to TMP mesh vertex colors, with optional
    /// Play-mode parallax shimmer (scrolling sample offset).
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class VisualTierTypographyBoundsGradientDriver : MonoBehaviour
    {
        [SerializeField] private TMP_Text targetText;
        [SerializeField] private bool gradientEnabled;
        [SerializeField] private VisualTierVertexGradientMode gradientMode =
            VisualTierVertexGradientMode.Horizontal;
        [SerializeField] private Color gradientStart = Color.white;
        [SerializeField] private Color gradientEnd = Color.white;
        [SerializeField] private bool shimmerEnabled;
        [SerializeField] private float shimmerSpeed = 0.35f;
        [SerializeField] private bool shimmerUseUnscaledTime = true;
        [SerializeField] [Range(0f, 1f)] private float shimmerScroll01;

        private bool subscribed;
        private bool applyQueued;

        public void Configure(TMP_Text text, VisualTierTypographyBoundsGradient.Settings gradientSettings)
        {
            targetText = text;
            gradientEnabled = gradientSettings.Enabled;
            gradientMode = gradientSettings.Mode;
            gradientStart = gradientSettings.Start;
            gradientEnd = gradientSettings.End;
            shimmerScroll01 = Mathf.Repeat(gradientSettings.ScrollOffset01, 1f);
            enabled = targetText != null && gradientEnabled;
            EnsureSubscription();
            ApplyNow();
        }

        public void ConfigureShimmer(bool enabled, float speed, bool useUnscaledTime)
        {
            shimmerEnabled = enabled;
            shimmerSpeed = Mathf.Max(0f, speed);
            shimmerUseUnscaledTime = useUnscaledTime;
        }

        public void DisableDriver()
        {
            gradientEnabled = false;
            shimmerEnabled = false;
            enabled = false;
            RemoveSubscription();
        }

        /// <summary>
        /// Rebuilds TMP mesh and paints bounds-gradient vertex colors immediately.
        /// </summary>
        public void ApplyNow()
        {
            if (!gradientEnabled || targetText == null)
                return;

            EnsureSubscription();
            EnsureWhiteFaceColor(targetText);
            VisualTierTypographyApplication.SafeForceMeshUpdate(targetText);
            PaintVertexColors(targetText);
        }

        private void OnEnable()
        {
            EnsureSubscription();
            QueueApply();
        }

        private void OnDisable()
        {
            RemoveSubscription();
        }

        private void LateUpdate()
        {
            if (!gradientEnabled || targetText == null || !shimmerEnabled)
                return;

            if (!Application.isPlaying || shimmerSpeed <= 0f)
                return;

            float dt = shimmerUseUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            shimmerScroll01 = Mathf.Repeat(shimmerScroll01 + shimmerSpeed * dt, 1f);

            // Prefer OnPreRenderText for the next canvas generate; also paint immediately
            // so the scroll is visible even if TMP skips a regenerate this frame.
            PaintVertexColors(targetText);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!gradientEnabled || targetText == null)
                return;

            QueueApply();
        }
#endif

        private void QueueApply()
        {
            if (applyQueued)
                return;

            applyQueued = true;
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEditor.EditorApplication.delayCall += () =>
                {
                    applyQueued = false;
                    if (this != null && isActiveAndEnabled)
                        ApplyNow();
                };
                return;
            }
#endif
            applyQueued = false;
            ApplyNow();
        }

        private VisualTierTypographyBoundsGradient.Settings BuildSettings()
        {
            return new VisualTierTypographyBoundsGradient.Settings
            {
                Enabled = gradientEnabled,
                Mode = gradientMode,
                Start = gradientStart,
                End = gradientEnd,
                ScrollOffset01 = shimmerScroll01,
            };
        }

        private void EnsureSubscription()
        {
            if (targetText == null || !gradientEnabled)
                return;

            if (subscribed)
            {
                targetText.OnPreRenderText -= HandlePreRenderText;
                subscribed = false;
            }

            targetText.OnPreRenderText += HandlePreRenderText;
            subscribed = true;
        }

        private void RemoveSubscription()
        {
            if (!subscribed || targetText == null)
            {
                subscribed = false;
                return;
            }

            targetText.OnPreRenderText -= HandlePreRenderText;
            subscribed = false;
        }

        private void HandlePreRenderText(TMP_TextInfo textInfo)
        {
            if (!gradientEnabled || textInfo == null)
                return;

            VisualTierTypographyBoundsGradient.ApplyToTextInfo(textInfo, BuildSettings());
        }

        private void PaintVertexColors(TMP_Text text)
        {
            if (text == null)
                return;

            TMP_TextInfo textInfo = text.textInfo;
            if (textInfo == null || textInfo.characterCount <= 0)
                return;

            VisualTierTypographyBoundsGradient.ApplyToTextInfo(textInfo, BuildSettings());

            try
            {
                text.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
            }
            catch (System.Exception)
            {
            }
        }

        private static void EnsureWhiteFaceColor(TMP_Text text)
        {
            if (text == null)
                return;

            try
            {
                Material shared = text.fontSharedMaterial;
                if (shared != null && shared.HasProperty("_FaceColor"))
                {
                    Color face = shared.GetColor("_FaceColor");
                    if (face.r < 0.99f || face.g < 0.99f || face.b < 0.99f)
                        shared.SetColor("_FaceColor", Color.white);
                }
            }
            catch (System.Exception)
            {
            }
        }
    }
}
