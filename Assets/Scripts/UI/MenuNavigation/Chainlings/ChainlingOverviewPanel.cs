using Chainbound.Combat;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SignalJam.UI.MenuNavigation
{
    [DisallowMultipleComponent]
    public class ChainlingOverviewPanel : MonoBehaviour
    {
        [SerializeField] private TMP_Text statsLabel;
        [SerializeField] private TMP_Text loreLabel;
        [SerializeField] private TMP_Text recordLabel;

        private void Awake()
        {
            EnsureBuilt();
        }

        public void Bind(PlayerRosterEntry entry, SpeciesDefinitionSO species)
        {
            EnsureBuilt();
            ChainlingSnapshot snapshot = entry != null ? entry.snapshot : null;

            if (statsLabel != null)
                statsLabel.text = BuildStats(snapshot, species);
            if (loreLabel != null)
                loreLabel.text = species != null && !string.IsNullOrWhiteSpace(species.description)
                    ? species.description
                    : "No lore entry authored yet.";
            if (recordLabel != null)
                recordLabel.text = BuildRecord(entry);
        }

        private void EnsureBuilt()
        {
            if (statsLabel != null && loreLabel != null && recordLabel != null)
                return;

            var layout = PlayerMenuUiFactory.VerticalLayout(gameObject, 14f);
            layout.padding = new RectOffset(16, 16, 16, 16);

            statsLabel = statsLabel != null
                ? statsLabel
                : CreateBlock("StatsBlock", "Stats");
            loreLabel = loreLabel != null
                ? loreLabel
                : CreateBlock("LoreBlock", "Lore");
            recordLabel = recordLabel != null
                ? recordLabel
                : CreateBlock("RecordBlock", "Service Record");
        }

        private TMP_Text CreateBlock(string name, string heading)
        {
            var image = PlayerMenuUiFactory.CreateImage(name, transform, PlayerMenuUiFactory.PanelAlt);
            PlayerMenuUiFactory.LayoutElement(image.gameObject, preferredHeight: 150f, flexibleWidth: 1f);
            var text = PlayerMenuUiFactory.CreateText("Text", image.transform, heading, 22f, TextAlignmentOptions.TopLeft);
            PlayerMenuUiFactory.Stretch(text.rectTransform, 14f, 12f, 14f, 12f);
            return text;
        }

        public static string BuildCompactStats(ChainlingSnapshot snapshot, SpeciesDefinitionSO species)
        {
            return ChainlingAttributeDisplayUtility.BuildCompactStatsWithHp(snapshot, species);
        }

        private static string BuildStats(ChainlingSnapshot snapshot, SpeciesDefinitionSO species)
        {
            return
                BuildCompactStats(snapshot, species) + "\n" +
                "Tap expansion hooks can show base / IV / cycle contribution per stat.";
        }

        private static string BuildRecord(PlayerRosterEntry entry)
        {
            ChainlingBattleStats stats = entry != null ? entry.battleStats : null;
            if (stats == null)
                return "Service Record\nNo recorded battles.";

            return
                "Service Record\n" +
                $"Wins {stats.battlesWon}   Losses {stats.battlesLost}\n" +
                $"KOs {stats.kosScored}   Times KO'd {stats.timesKod}\n" +
                $"Times Captured {stats.timesCaptured}";
        }
    }
}
