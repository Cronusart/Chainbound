using System;
using Chainbound.Combat;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SignalJam.UI.MenuNavigation
{
    [DisallowMultipleComponent]
    public class ChainlingCardWidget : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Image portraitImage;
        [SerializeField] private Image selectionFrame;
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TMP_Text speciesLabel;
        [SerializeField] private TMP_Text starsLabel;
        [SerializeField] private TMP_Text typeLabel;
        [SerializeField] private TMP_Text partyBadgeLabel;
        [SerializeField] private TMP_Text entangledLabel;

        private PlayerRosterEntry boundEntry;
        private Action<PlayerRosterEntry> onClicked;

        public Button Button => button;
        public PlayerRosterEntry BoundEntry => boundEntry;

        public void BuildDefaultLayoutForEditor()
        {
            EnsureBuilt();
            MenuSelectableFocusVisual.EnsureOn(button);
        }

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

        public void Bind(
            PlayerRosterEntry entry,
            SpeciesDefinitionSO species,
            CombatVisualProfileSO visualProfile,
            bool selectedForLineup,
            Action<PlayerRosterEntry> clickHandler)
        {
            EnsureBuilt();
            boundEntry = entry;
            onClicked = clickHandler;

            ChainlingSnapshot snapshot = entry != null ? entry.snapshot : null;
            string displayName = ResolveDisplayName(snapshot, species);
            string speciesName = species != null ? species.displayName : snapshot != null ? snapshot.speciesId : "Unknown";

            if (nameLabel != null)
                nameLabel.text = displayName;
            if (speciesLabel != null)
                speciesLabel.text = speciesName;
            if (starsLabel != null)
                starsLabel.text = BuildStars(snapshot != null ? snapshot.stars : 0);
            if (typeLabel != null)
                typeLabel.text = species != null
                    ? FormatPrimaryType(species.primaryType)
                    : "";
            if (partyBadgeLabel != null)
            {
                partyBadgeLabel.enabled = entry != null && entry.isPartyMember;
                partyBadgeLabel.text = entry != null && entry.isPartyMember
                    ? SlotLabel(entry.partySlot)
                    : "";
            }
            if (entangledLabel != null)
            {
                bool entangled = snapshot != null &&
                    snapshot.visualIdentity != null &&
                    snapshot.isEntangled;
                entangledLabel.enabled = entangled;
                entangledLabel.text = entangled ? "ENT" : "";
            }
            if (portraitImage != null)
            {
                portraitImage.sprite = visualProfile != null ? visualProfile.portrait : null;
                portraitImage.enabled = portraitImage.sprite != null;
            }
            if (selectionFrame != null)
                selectionFrame.enabled = selectedForLineup;
        }

        private void HandleClicked()
        {
            onClicked?.Invoke(boundEntry);
        }

        private void EnsureBuilt()
        {
            if (button == null)
                button = GetComponent<Button>();
            if (button == null)
                button = gameObject.AddComponent<Button>();

            var bg = GetComponent<Image>();
            if (bg == null)
                bg = gameObject.AddComponent<Image>();
            bg.color = PlayerMenuUiFactory.PanelAlt;
            button.targetGraphic = bg;

            var rt = transform as RectTransform;
            if (rt != null && rt.sizeDelta == Vector2.zero)
                rt.sizeDelta = new Vector2(220f, 260f);

            if (portraitImage == null)
            {
                portraitImage = PlayerMenuUiFactory.CreateImage("Portrait", transform, new Color(0.04f, 0.06f, 0.09f, 1f));
                var prt = portraitImage.rectTransform;
                prt.anchorMin = new Vector2(0.08f, 0.38f);
                prt.anchorMax = new Vector2(0.92f, 0.92f);
                prt.offsetMin = Vector2.zero;
                prt.offsetMax = Vector2.zero;
                portraitImage.preserveAspect = true;
            }

            if (selectionFrame == null)
            {
                selectionFrame = PlayerMenuUiFactory.CreateImage("SelectedFrame", transform, PlayerMenuUiFactory.Accent);
                PlayerMenuUiFactory.Stretch(selectionFrame.rectTransform);
                selectionFrame.enabled = false;
            }

            if (partyBadgeLabel == null)
            {
                partyBadgeLabel = PlayerMenuUiFactory.CreateText("PartyBadge", transform, "", 18f, TextAlignmentOptions.Center, false);
                var rtBadge = partyBadgeLabel.rectTransform;
                rtBadge.anchorMin = new Vector2(0f, 1f);
                rtBadge.anchorMax = new Vector2(0f, 1f);
                rtBadge.pivot = new Vector2(0f, 1f);
                rtBadge.sizeDelta = new Vector2(88f, 32f);
                rtBadge.anchoredPosition = new Vector2(8f, -8f);
                partyBadgeLabel.color = PlayerMenuUiFactory.Accent;
            }

            if (entangledLabel == null)
            {
                entangledLabel = PlayerMenuUiFactory.CreateText("EntangledBadge", transform, "", 17f, TextAlignmentOptions.Center, false);
                var ert = entangledLabel.rectTransform;
                ert.anchorMin = new Vector2(1f, 1f);
                ert.anchorMax = new Vector2(1f, 1f);
                ert.pivot = new Vector2(1f, 1f);
                ert.sizeDelta = new Vector2(70f, 32f);
                ert.anchoredPosition = new Vector2(-8f, -8f);
                entangledLabel.color = new Color(1f, 0.68f, 0.35f, 1f);
            }

            if (nameLabel == null)
            {
                nameLabel = PlayerMenuUiFactory.CreateText("Name", transform, "--", 24f, TextAlignmentOptions.MidlineLeft, false);
                PlayerMenuUiFactory.Bottom(nameLabel.rectTransform, 34f, 12f, 12f, 58f);
            }
            if (speciesLabel == null)
            {
                speciesLabel = PlayerMenuUiFactory.CreateText("Species", transform, "", 18f, TextAlignmentOptions.MidlineLeft, false);
                speciesLabel.color = PlayerMenuUiFactory.Muted;
                PlayerMenuUiFactory.Bottom(speciesLabel.rectTransform, 28f, 12f, 12f, 34f);
            }
            if (starsLabel == null)
            {
                starsLabel = PlayerMenuUiFactory.CreateText("Stars", transform, "", 18f, TextAlignmentOptions.MidlineLeft, false);
                starsLabel.color = new Color(1f, 0.82f, 0.35f, 1f);
                PlayerMenuUiFactory.Bottom(starsLabel.rectTransform, 26f, 12f, 82f, 8f);
            }
            if (typeLabel == null)
            {
                typeLabel = PlayerMenuUiFactory.CreateText("Types", transform, "", 17f, TextAlignmentOptions.MidlineRight, false);
                typeLabel.color = PlayerMenuUiFactory.Muted;
                PlayerMenuUiFactory.Bottom(typeLabel.rectTransform, 26f, 82f, 12f, 8f);
            }
        }

        private static string ResolveDisplayName(ChainlingSnapshot snapshot, SpeciesDefinitionSO species)
        {
            if (snapshot != null && !string.IsNullOrWhiteSpace(snapshot.displayName))
                return snapshot.displayName;
            if (species != null && !string.IsNullOrWhiteSpace(species.displayName))
                return species.displayName;
            return snapshot != null && !string.IsNullOrWhiteSpace(snapshot.speciesId) ? snapshot.speciesId : "Chainling";
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

        private static string SlotLabel(int slot)
        {
            switch (slot)
            {
                case 0: return "LEAD";
                case 1: return "PARTNER";
                case 2: return "BENCH";
                default: return "PARTY";
            }
        }
    }
}
