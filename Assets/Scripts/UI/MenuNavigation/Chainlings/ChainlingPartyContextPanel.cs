using System.Collections.Generic;
using Chainbound.Combat;
using SignalJam.UI.MenuNavigation.PartyTab;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SignalJam.UI.MenuNavigation
{
    [DisallowMultipleComponent]
    public class ChainlingPartyContextPanel : MonoBehaviour
    {
        [SerializeField] private MoveDatabaseSO moveDatabase;
        [SerializeField] private TMP_Text headingLabel;
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TMP_Text metaLabel;
        [SerializeField] private TMP_Text statsLabel;
        [SerializeField] private TMP_Text movesLabel;
        [SerializeField] private TMP_Text emptyLabel;
        [SerializeField] private Button inspectButton;

        public Button InspectButton => inspectButton;

        private void Awake()
        {
            EnsureBuilt();
        }

        public void Configure(MoveDatabaseSO moveDb)
        {
            if (moveDb != null)
                moveDatabase = moveDb;
        }

        public void BuildDefaultLayoutForEditor()
        {
            EnsureBuilt();
            MenuSelectableFocusVisual.EnsureOn(inspectButton);
        }

        public void Bind(PlayerRosterEntry entry, SpeciesDefinitionSO species, int slotIndex)
        {
            EnsureBuilt();

            ChainlingSnapshot snapshot = entry != null ? entry.snapshot : null;
            bool filled = snapshot != null;

            if (headingLabel != null)
                headingLabel.text = $"SLOT {ChainlingsPartyScreen.SlotLabel(slotIndex)}";

            if (!filled)
            {
                if (nameLabel != null)
                    nameLabel.text = $"Slot {ChainlingsPartyScreen.SlotLabel(slotIndex)}";
                if (metaLabel != null)
                    metaLabel.text = "Empty";
                if (statsLabel != null)
                    statsLabel.text = "";
                if (movesLabel != null)
                    movesLabel.text = "";
                if (emptyLabel != null)
                {
                    emptyLabel.enabled = true;
                    emptyLabel.text = "Assign at a PC.";
                }
                if (inspectButton != null)
                    inspectButton.gameObject.SetActive(false);
                return;
            }

            if (nameLabel != null)
                nameLabel.text = ResolveDisplayName(snapshot, species);
            if (metaLabel != null)
                metaLabel.text = BuildMeta(snapshot, species);
            if (statsLabel != null)
                statsLabel.text = ChainlingOverviewPanel.BuildCompactStats(snapshot, species);
            if (movesLabel != null)
                movesLabel.text = BuildMoveSummary(snapshot);
            if (emptyLabel != null)
                emptyLabel.enabled = false;
            if (inspectButton != null)
            {
                inspectButton.gameObject.SetActive(true);
                inspectButton.interactable = true;
            }
        }

        private void EnsureBuilt()
        {
            if (headingLabel != null &&
                nameLabel != null &&
                metaLabel != null &&
                statsLabel != null &&
                movesLabel != null &&
                emptyLabel != null &&
                inspectButton != null)
            {
                return;
            }

            VerticalLayoutGroup layout = PlayerMenuUiFactory.VerticalLayout(gameObject, 12f);
            layout.padding = new RectOffset(34, 34, 30, 30);
            layout.childForceExpandHeight = false;

            headingLabel = headingLabel != null
                ? headingLabel
                : PlayerMenuUiFactory.CreateText("Heading", transform, "PARTY SLOT", 26f, TextAlignmentOptions.TopLeft, false);
            headingLabel.color = PlayerMenuUiFactory.Accent;
            PlayerMenuUiFactory.LayoutElement(headingLabel.gameObject, preferredHeight: 38f);

            nameLabel = nameLabel != null
                ? nameLabel
                : PlayerMenuUiFactory.CreateText("Name", transform, "Chainling", 34f, TextAlignmentOptions.TopLeft, false);
            PlayerMenuUiFactory.LayoutElement(nameLabel.gameObject, preferredHeight: 48f);

            metaLabel = metaLabel != null
                ? metaLabel
                : PlayerMenuUiFactory.CreateText("Meta", transform, "", 22f, TextAlignmentOptions.TopLeft, true);
            metaLabel.color = PlayerMenuUiFactory.Muted;
            PlayerMenuUiFactory.LayoutElement(metaLabel.gameObject, preferredHeight: 64f);

            statsLabel = statsLabel != null
                ? statsLabel
                : CreateBlock("StatsBlock");
            PlayerMenuUiFactory.LayoutElement(statsLabel.transform.parent.gameObject, preferredHeight: 134f, flexibleWidth: 1f);

            movesLabel = movesLabel != null
                ? movesLabel
                : CreateBlock("MovesBlock");
            PlayerMenuUiFactory.LayoutElement(movesLabel.transform.parent.gameObject, preferredHeight: 100f, flexibleWidth: 1f);

            emptyLabel = emptyLabel != null
                ? emptyLabel
                : PlayerMenuUiFactory.CreateText("Empty", transform, "", 24f, TextAlignmentOptions.TopLeft, true);
            emptyLabel.color = PlayerMenuUiFactory.Muted;
            PlayerMenuUiFactory.LayoutElement(emptyLabel.gameObject, preferredHeight: 52f);

            inspectButton = inspectButton != null
                ? inspectButton
                : PlayerMenuUiFactory.CreateButton("InspectButton", transform, "Inspect", PlayerMenuUiFactory.Accent);
            PlayerMenuUiFactory.LayoutElement(inspectButton.gameObject, preferredHeight: 64f, flexibleWidth: 1f);
        }

        private TMP_Text CreateBlock(string blockName)
        {
            Image image = PlayerMenuUiFactory.CreateImage(blockName, transform, PlayerMenuUiFactory.Panel);
            TMP_Text text = PlayerMenuUiFactory.CreateText("Text", image.transform, "", 22f, TextAlignmentOptions.TopLeft, true);
            PlayerMenuUiFactory.Stretch(text.rectTransform, 18f, 14f, 18f, 14f);
            return text;
        }

        private string BuildMoveSummary(ChainlingSnapshot snapshot)
        {
            if (snapshot == null || snapshot.moveIds == null || snapshot.moveIds.Count == 0)
                return "Moves\nNo equipped moves in snapshot.";

            var labels = new List<string>();
            for (int i = 0; i < snapshot.moveIds.Count && labels.Count < 2; i++)
            {
                string moveId = snapshot.moveIds[i];
                if (string.IsNullOrWhiteSpace(moveId))
                    continue;

                MoveDefinitionSO definition = moveDatabase != null ? moveDatabase.GetById(moveId) : null;
                labels.Add(definition != null && !string.IsNullOrWhiteSpace(definition.displayName)
                    ? definition.displayName
                    : moveId);
            }

            return labels.Count > 0 ? "Moves\n" + string.Join(" / ", labels) : "Moves\nNo equipped moves in snapshot.";
        }

        private static string BuildMeta(ChainlingSnapshot snapshot, SpeciesDefinitionSO species)
        {
            string typeText = species != null
                ? species.primaryType.ToString()
                : "Unknown type";

            return $"{typeText}\nStars {snapshot.stars}/{CombatConstants.STAR_MAX}   {ChainlingPlayerTerms.FormatCycleLabel(snapshot.rebirthCount)}";
        }

        private static string ResolveDisplayName(ChainlingSnapshot snapshot, SpeciesDefinitionSO species)
        {
            if (snapshot != null && !string.IsNullOrWhiteSpace(snapshot.displayName))
                return snapshot.displayName;
            if (species != null && !string.IsNullOrWhiteSpace(species.displayName))
                return species.displayName;
            return snapshot != null && !string.IsNullOrWhiteSpace(snapshot.speciesId) ? snapshot.speciesId : "Chainling";
        }
    }
}
