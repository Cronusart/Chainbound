using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Chainbound.Combat
{
    [DisallowMultipleComponent]
    public class CombatModifierOverviewRowUGUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private RectTransform root;
        [SerializeField] private Image bulletImage;
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private LayoutElement layoutElement;

        [Header("Style")]
        [SerializeField] private Color descriptionColor = new Color(0.86f, 0.88f, 0.90f, 1f);
        [SerializeField] private float bulletSize = 10f;

        public void ConfigureGeneratedDefaults(TMP_FontAsset fontOverride, float preferredHeight = 24f)
        {
            EnsureReferences();

            if (layoutElement == null)
                layoutElement = gameObject.AddComponent<LayoutElement>();
            if (layoutElement.preferredHeight <= 0f)
                layoutElement.preferredHeight = preferredHeight;
            if (layoutElement.minHeight <= 0f)
                layoutElement.minHeight = preferredHeight;

            if (descriptionText != null)
            {
                if (fontOverride != null && descriptionText.font != fontOverride)
                    descriptionText.font = fontOverride;
                if (descriptionText.fontSize <= 0f)
                    descriptionText.fontSize = 18f;
                descriptionText.enableWordWrapping = true;
                descriptionText.alignment = TextAlignmentOptions.Left;
                descriptionText.color = descriptionColor;
            }

            if (bulletImage != null)
                bulletImage.preserveAspect = true;
        }

        public void SetVisible(bool visible)
        {
            EnsureReferences();
            gameObject.SetActive(visible);
        }

        public void SetData(string text, RarityTier rarity, Color bulletColor, Color? descriptionTint = null)
        {
            EnsureReferences();

            if (descriptionText != null)
            {
                descriptionText.text = string.IsNullOrEmpty(text) ? "--" : text;
                descriptionText.color = descriptionTint ?? descriptionColor;
            }

            if (bulletImage != null)
            {
                bulletImage.color = bulletColor;
                bulletImage.enabled = true;
            }
        }

        private void Awake()
        {
            EnsureReferences();
        }

        private void OnValidate()
        {
            if (!Application.isPlaying)
                EnsureReferences();
        }

        private void EnsureReferences()
        {
            if (root == null)
                root = transform as RectTransform;
            if (layoutElement == null)
                layoutElement = GetComponent<LayoutElement>();

            if (bulletImage == null)
            {
                var bulletTransform = transform.Find("Image");
                if (bulletTransform == null)
                    bulletTransform = transform.Find("Bullet");
                if (bulletTransform != null)
                    bulletImage = bulletTransform.GetComponent<Image>();
            }

            if (descriptionText == null)
            {
                var labelTransform = transform.Find("Text (TMP)");
                if (labelTransform == null)
                    labelTransform = transform.Find("Description");
                if (labelTransform == null)
                    labelTransform = transform.Find("Label");
                if (labelTransform != null)
                    descriptionText = labelTransform.GetComponent<TMP_Text>();
                if (descriptionText == null)
                    descriptionText = GetComponentInChildren<TMP_Text>(true);
            }

            if (bulletImage != null)
                bulletImage.raycastTarget = false;
            if (descriptionText != null)
                descriptionText.raycastTarget = false;
        }
    }
}
