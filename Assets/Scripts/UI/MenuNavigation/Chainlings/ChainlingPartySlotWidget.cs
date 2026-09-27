using System;
using Chainbound.Combat;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using SignalJam.UI.MenuNavigation.PartyTab;

namespace SignalJam.UI.MenuNavigation
{
    [DisallowMultipleComponent]
    public class ChainlingPartySlotWidget : MonoBehaviour, ISelectHandler
    {
        [SerializeField] private Button button;
        [SerializeField] private Image portraitImage;
        [SerializeField] private Image focusFrame;
        [SerializeField] private TMP_Text roleLabel;
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TMP_Text speciesLabel;
        [SerializeField] private TMP_Text starsLabel;
        [SerializeField] private TMP_Text typeLabel;
        [SerializeField] private TMP_Text emptyLabel;
        [SerializeField] private Image hpBarBackground;
        [SerializeField] private Image hpBarFill;
        [SerializeField] private TMP_Text hpLabel;

        private int slotIndex;
        private PlayerRosterEntry boundEntry;
        private Action<int> onFocused;
        private Action<int> onSubmitted;

        public Button Button => button;
        public PlayerRosterEntry BoundEntry => boundEntry;

        private void Awake()
        {
            EnsureBuilt();
        }

        private void OnEnable()
        {
            if (button != null)
                button.onClick.AddListener(HandleClicked);
        }

        private void OnDisable()
        {
            if (button != null)
                button.onClick.RemoveListener(HandleClicked);
        }

        public void BuildDefaultLayoutForEditor()
        {
            EnsureBuilt();
            MenuSelectableFocusVisual.EnsureOn(button);
        }

        public void Bind(
            int index,
            PlayerRosterEntry entry,
            SpeciesDefinitionSO species,
            CombatVisualProfileSO visualProfile,
            bool focused,
            Action<int> focusHandler,
            Action<int> submitHandler)
        {
            EnsureBuilt();
            slotIndex = index;
            boundEntry = entry;
            onFocused = focusHandler;
            onSubmitted = submitHandler;

            if (roleLabel != null)
            {
                roleLabel.text = ChainlingsPartyScreen.SlotLabel(index);
                roleLabel.color = ChainlingsPartyScreen.IsBenchedPartySlot(index)
                    ? PlayerMenuUiFactory.Muted
                    : PlayerMenuUiFactory.Accent;
            }

            ChainlingSnapshot snapshot = entry != null ? entry.snapshot : null;
            bool filled = snapshot != null;

            if (nameLabel != null)
                nameLabel.text = filled ? ResolveDisplayName(snapshot, species) : "Empty";
            if (speciesLabel != null)
            {
                speciesLabel.text = filled
                    ? species != null && !string.IsNullOrWhiteSpace(species.displayName) ? species.displayName : snapshot.speciesId
                    : "Assign at a PC";
            }
            if (starsLabel != null)
                starsLabel.text = filled ? BuildStars(snapshot.stars) : "";
            if (typeLabel != null)
                typeLabel.text = filled && species != null ? FormatPrimaryType(species.primaryType) : "";
            if (emptyLabel != null)
            {
                emptyLabel.enabled = !filled;
                emptyLabel.text = "Empty slot";
            }

            BindVitals(filled, entry, species);

            if (portraitImage != null)
            {
                portraitImage.sprite = visualProfile != null ? ResolvePortrait(visualProfile) : null;
                portraitImage.enabled = filled && portraitImage.sprite != null;
                portraitImage.preserveAspect = true;
            }

            if (button != null)
                button.interactable = true;

            SetFocused(focused);
        }

        public void SetFocused(bool focused)
        {
            if (focusFrame != null)
                focusFrame.enabled = focused;
        }

        public void OnSelect(BaseEventData eventData)
        {
            onFocused?.Invoke(slotIndex);
        }

        private void HandleClicked()
        {
            onSubmitted?.Invoke(slotIndex);
        }

        private void EnsureBuilt()
        {
            if (button == null)
                button = GetComponent<Button>();
            if (button == null)
                button = gameObject.AddComponent<Button>();

            Image background = GetComponent<Image>();
            if (background == null)
                background = gameObject.AddComponent<Image>();
            background.color = PlayerMenuUiFactory.Panel;
            background.raycastTarget = true;
            button.targetGraphic = background;

            RectTransform rectTransform = transform as RectTransform;
            if (rectTransform != null && rectTransform.sizeDelta == Vector2.zero)
                rectTransform.sizeDelta = new Vector2(470f, 188f);

            if (focusFrame == null)
            {
                focusFrame = PlayerMenuUiFactory.CreateImage("FocusFrame", transform, PlayerMenuUiFactory.Accent);
                PlayerMenuUiFactory.Stretch(focusFrame.rectTransform, 3f, 3f, 3f, 3f);
                focusFrame.enabled = false;
            }

            if (roleLabel == null)
            {
                roleLabel = PlayerMenuUiFactory.CreateText("Role", transform, "1", 22f, TextAlignmentOptions.TopLeft, false);
                roleLabel.color = PlayerMenuUiFactory.Accent;
                PlayerMenuUiFactory.Top(roleLabel.rectTransform, 34f, 18f, 18f, 12f);
            }

            if (portraitImage == null)
            {
                portraitImage = PlayerMenuUiFactory.CreateImage("Portrait", transform, new Color(0.035f, 0.05f, 0.075f, 1f));
                RectTransform portraitRect = portraitImage.rectTransform;
                portraitRect.anchorMin = new Vector2(0.045f, 0.17f);
                portraitRect.anchorMax = new Vector2(0.29f, 0.78f);
                portraitRect.offsetMin = Vector2.zero;
                portraitRect.offsetMax = Vector2.zero;
                portraitImage.preserveAspect = true;
            }

            if (nameLabel == null)
            {
                nameLabel = PlayerMenuUiFactory.CreateText("Name", transform, "Empty", 27f, TextAlignmentOptions.MidlineLeft, false);
                Anchor(nameLabel.rectTransform, 0.34f, 0.58f, 0.95f, 0.77f);
            }

            if (speciesLabel == null)
            {
                speciesLabel = PlayerMenuUiFactory.CreateText("Species", transform, "", 21f, TextAlignmentOptions.MidlineLeft, false);
                speciesLabel.color = PlayerMenuUiFactory.Muted;
                Anchor(speciesLabel.rectTransform, 0.34f, 0.41f, 0.95f, 0.55f);
            }

            if (starsLabel == null)
            {
                starsLabel = PlayerMenuUiFactory.CreateText("Stars", transform, "", 20f, TextAlignmentOptions.MidlineLeft, false);
                starsLabel.color = new Color(1f, 0.82f, 0.35f, 1f);
                Anchor(starsLabel.rectTransform, 0.34f, 0.24f, 0.62f, 0.37f);
            }

            if (typeLabel == null)
            {
                typeLabel = PlayerMenuUiFactory.CreateText("Types", transform, "", 20f, TextAlignmentOptions.MidlineRight, false);
                typeLabel.color = PlayerMenuUiFactory.Muted;
                Anchor(typeLabel.rectTransform, 0.62f, 0.24f, 0.95f, 0.37f);
            }

            if (emptyLabel == null)
            {
                emptyLabel = PlayerMenuUiFactory.CreateText("EmptyLabel", transform, "Empty slot", 20f, TextAlignmentOptions.MidlineLeft, false);
                emptyLabel.color = PlayerMenuUiFactory.Muted;
                Anchor(emptyLabel.rectTransform, 0.34f, 0.17f, 0.95f, 0.29f);
            }

            EnsureHpBarBuilt();
        }

        private void BindVitals(bool filled, PlayerRosterEntry entry, SpeciesDefinitionSO species)
        {
            EnsureHpBarBuilt();

            if (!filled || entry == null || species == null)
            {
                if (hpBarBackground != null)
                    hpBarBackground.enabled = false;
                if (hpBarFill != null)
                    hpBarFill.enabled = false;
                if (hpLabel != null)
                    hpLabel.enabled = false;
                return;
            }

            var ctx = new ChainlingPartyColumnContext
            {
                entry = entry,
                species = species,
                slotIndex = slotIndex,
            };
            ChainlingPartyColumnVitals vitals = ChainlingPartyVitalsDisplayUtility.Build(ctx);

            if (hpBarBackground != null)
                hpBarBackground.enabled = true;
            if (hpBarFill != null)
            {
                hpBarFill.enabled = true;
                PartyTabProgressBarUtility.SetFill01(hpBarFill, vitals != null ? vitals.fill01 : 0f);
                hpBarFill.color = vitals != null && vitals.isFainted
                    ? new Color(0.45f, 0.12f, 0.12f, 1f)
                    : new Color(0.22f, 0.78f, 0.34f, 1f);
            }

            if (hpLabel != null)
            {
                hpLabel.enabled = true;
                hpLabel.text = vitals != null ? vitals.label : "—";
            }
        }

        private void EnsureHpBarBuilt()
        {
            if (hpBarBackground == null)
            {
                hpBarBackground = PlayerMenuUiFactory.CreateImage("HpBarBackground", transform, new Color(0.08f, 0.12f, 0.1f, 1f));
                Anchor(hpBarBackground.rectTransform, 0.045f, 0.06f, 0.95f, 0.14f);
            }

            if (hpBarFill == null)
            {
                hpBarFill = PlayerMenuUiFactory.CreateImage("HpBarFill", hpBarBackground.transform, new Color(0.22f, 0.78f, 0.34f, 1f));
                PlayerMenuUiFactory.Stretch(hpBarFill.rectTransform, 0f, 0f, 0f, 0f);
            }
            PartyTabProgressBarUtility.ConfigureRectFill(hpBarFill);

            if (hpLabel == null)
            {
                hpLabel = PlayerMenuUiFactory.CreateText("HpLabel", transform, "—", 18f, TextAlignmentOptions.MidlineRight, false);
                hpLabel.color = new Color(0.82f, 0.96f, 0.86f, 1f);
                Anchor(hpLabel.rectTransform, 0.34f, 0.06f, 0.95f, 0.14f);
            }
        }

        private static void Anchor(RectTransform rectTransform, float left, float bottom, float right, float top)
        {
            rectTransform.anchorMin = new Vector2(left, bottom);
            rectTransform.anchorMax = new Vector2(right, top);
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }

        private static string ResolveDisplayName(ChainlingSnapshot snapshot, SpeciesDefinitionSO species)
        {
            if (snapshot != null && !string.IsNullOrWhiteSpace(snapshot.displayName))
                return snapshot.displayName;
            if (species != null && !string.IsNullOrWhiteSpace(species.displayName))
                return species.displayName;
            return snapshot != null && !string.IsNullOrWhiteSpace(snapshot.speciesId) ? snapshot.speciesId : "Chainling";
        }

        private static Sprite ResolvePortrait(CombatVisualProfileSO profile)
        {
            if (profile == null)
                return null;
            if (profile.portrait != null)
                return profile.portrait;
            if (profile.idleSequence != null && profile.idleSequence.Length > 0)
                return profile.idleSequence[0];
            return null;
        }

        private static string BuildStars(int stars)
        {
            int clamped = Mathf.Clamp(stars, 0, CombatConstants.STAR_MAX);
            return new string('*', clamped).PadRight(CombatConstants.STAR_MAX, '-');
        }

        private static string FormatPrimaryType(ChainlingType primary)
        {
            return primary == ChainlingType.None ? "" : primary.ToString();
        }
    }
}
