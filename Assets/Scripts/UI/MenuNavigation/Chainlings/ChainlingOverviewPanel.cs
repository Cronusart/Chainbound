using System.Collections.Generic;
using System.Text;
using Chainbound.Combat;
using SignalJam.UI.MenuNavigation.PartyTab;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SignalJam.UI.MenuNavigation
{
    /// <summary>
    /// Read-only dossier summary shared by the capture reveal and Player Menu detail view.
    /// All values are derived from the authoritative roster snapshot.
    /// </summary>
    [DisallowMultipleComponent]
    public class ChainlingOverviewPanel : MonoBehaviour
    {
        [Header("Vitals / Placement")]
        [SerializeField] private TMP_Text maxHpLabel;
        [SerializeField] private TMP_Text placementLabel;
        [SerializeField] private TMP_Text loreLabel;

        [Header("Attributes")]
        [SerializeField] private PartyTabAttributeRadarGraphic radarGraphic;
        [SerializeField] private TMP_Text radarValuesLabel;
        [SerializeField] private TMP_Text ivLabel;

        [Header("Default Moves")]
        [Tooltip("Party-tab style move rows (authored clone of the Chainlings tab MovesView). When set, replaces the plain-text move list.")]
        [SerializeField] private ChainlingPartyMovesViewLayout movesView;
        [SerializeField] private TMP_Text defaultMovesLabel;

        [Header("Glance")]
        [SerializeField] private TMP_Text styleLineLabel;
        [SerializeField] private TMP_Text growthLineLabel;

        private void Awake()
        {
            EnsureBuilt();
        }

        public void BuildDefaultLayoutForEditor()
        {
            EnsureBuilt();
        }

        public void Bind(
            PlayerRosterEntry entry,
            SpeciesDefinitionSO species,
            PlayerRosterSnapshot roster,
            SpeciesDatabaseSO speciesDatabase,
            MoveDatabaseSO moveDatabase)
        {
            EnsureBuilt();
            ChainlingSnapshot snapshot = entry != null ? entry.snapshot : null;

            if (maxHpLabel != null)
            {
                int maxHp = ChainlingAttributeDisplayUtility.BuildMaxHp(snapshot, species);
                maxHpLabel.text = snapshot != null && species != null
                    ? $"MAX HP\n<size=150%><b>{maxHp}</b></size>"
                    : "MAX HP\n--";
            }

            if (placementLabel != null)
                placementLabel.text = BuildPlacement(entry, roster);

            if (loreLabel != null)
                loreLabel.text = species != null && !string.IsNullOrWhiteSpace(species.description)
                    ? species.description
                    : "No field notes have been authored for this species yet.";

            BindRadar(snapshot, species, speciesDatabase);

            if (ivLabel != null)
                ivLabel.text = BuildIvText(snapshot);

            BindMoves(entry, species, snapshot, speciesDatabase, moveDatabase);
            BindGlanceLines(entry, snapshot);
        }

        private void BindGlanceLines(PlayerRosterEntry entry, ChainlingSnapshot snapshot)
        {
            if (styleLineLabel != null)
            {
                ChainlingVisualIdentity identity = snapshot != null ? snapshot.visualIdentity : null;
                string variant = identity != null &&
                                 !string.IsNullOrWhiteSpace(identity.visualVariantId) &&
                                 !string.Equals(identity.visualVariantId, ChainlingVisualIdentity.BaseVariantId, System.StringComparison.Ordinal)
                    ? identity.visualVariantId
                    : "Default";
                string aura = identity != null &&
                              !string.IsNullOrWhiteSpace(identity.auraId) &&
                              !string.Equals(identity.auraId, ChainlingVisualIdentity.NoneId, System.StringComparison.Ordinal)
                    ? identity.auraId
                    : "None";
                styleLineLabel.text = $"STYLE\n{variant}  ·  {aura}";
            }

            if (growthLineLabel == null)
                return;

            int stars = snapshot != null ? ChainlingProgressionUtility.NormalizeStars(snapshot.stars) : 0;
            int rebirth = snapshot != null ? ChainlingProgressionUtility.NormalizeRebirthCount(snapshot.rebirthCount) : 0;
            int xp = entry != null ? Mathf.Max(0, entry.masteryXp) : 0;
            growthLineLabel.text =
                $"GROWTH\n{stars}/{CombatConstants.STAR_MAX}    {ChainlingPlayerTerms.FormatCycleLabel(rebirth)}    {xp} XP";
        }

        private void BindMoves(
            PlayerRosterEntry entry,
            SpeciesDefinitionSO species,
            ChainlingSnapshot snapshot,
            SpeciesDatabaseSO speciesDatabase,
            MoveDatabaseSO moveDatabase)
        {
            if (movesView == null)
            {
                if (defaultMovesLabel != null)
                {
                    defaultMovesLabel.gameObject.SetActive(true);
                    defaultMovesLabel.text = BuildMoveText(snapshot, moveDatabase);
                }
                return;
            }

            if (defaultMovesLabel != null)
                defaultMovesLabel.gameObject.SetActive(false);
            movesView.gameObject.SetActive(true);

            if (snapshot == null || string.IsNullOrWhiteSpace(snapshot.speciesId))
            {
                movesView.BindEmpty("No moves recorded.");
                return;
            }

            var summary = new ChainlingPartyColumnSummary();
            ChainlingPartyMoveDisplayUtility.PopulateSummary(summary, new ChainlingPartyColumnContext
            {
                entry = entry,
                species = species,
                speciesDatabase = speciesDatabase,
                moveDatabase = moveDatabase,
            });
            movesView.Bind(summary);
        }

        public static string BuildCompactStats(ChainlingSnapshot snapshot, SpeciesDefinitionSO species)
        {
            return ChainlingAttributeDisplayUtility.BuildCompactStatsWithHp(snapshot, species);
        }

        private void BindRadar(
            ChainlingSnapshot snapshot,
            SpeciesDefinitionSO species,
            SpeciesDatabaseSO speciesDatabase)
        {
            IReadOnlyList<ChainlingAttributeRadarAxis> axes =
                ChainlingAttributeDisplayUtility.BuildPartyRadarAxes(snapshot, species, speciesDatabase);
            var fills = new float[PartyTabAttributeRadarGraphic.AxisCount];
            var values = new StringBuilder("ATTRIBUTES\n");
            for (int i = 0; i < fills.Length; i++)
            {
                bool hasAxis = axes != null && i < axes.Count;
                ChainlingAttributeRadarAxis axis = hasAxis ? axes[i] : default;
                fills[i] = hasAxis ? axis.Fill01 : 0f;
                if (hasAxis)
                    values.Append(axis.Label).Append(' ').Append(axis.Total).Append(i == fills.Length - 1 ? "" : "  ");
            }

            if (radarGraphic != null)
            {
                radarGraphic.SetFills(fills);
                Color typeColor = species != null
                    ? ChainlingTypeDisplaySO.CreateFallback(species.primaryType).badgeColor
                    : PlayerMenuUiFactory.Accent;
                radarGraphic.SetTypeBackgroundColor(typeColor);
            }

            if (radarValuesLabel != null)
                radarValuesLabel.text = values.ToString();
        }

        private static string BuildPlacement(PlayerRosterEntry entry, PlayerRosterSnapshot roster)
        {
            if (entry == null)
                return "DOSSIER LOCATION\n--";

            if (entry.isPartyMember)
                return $"PARTY PLACEMENT\nSLOT {Mathf.Max(0, entry.partySlot) + 1} / {CombatConstants.ROSTER_SIZE}";

            int stored = roster != null ? roster.StorageCount : 0;
            int capacity = roster != null ? roster.StorageCapacity : CombatConstants.MAX_CHAINLINGS - CombatConstants.ROSTER_SIZE;
            return $"PC STORAGE\nSTORED {stored} / {capacity}";
        }

        private static string BuildIvText(ChainlingSnapshot snapshot)
        {
            string[] labels = { "HP", "MATK", "MDEF", "RATK", "RDEF", "SPD" };
            var text = new StringBuilder("IV PLACEMENT\n");
            for (int i = 0; i < labels.Length; i++)
            {
                int value = snapshot != null && snapshot.ivs != null && i < snapshot.ivs.Length
                    ? snapshot.ivs[i]
                    : 0;
                text.Append(labels[i]).Append("  +").Append(value);
                if (i < labels.Length - 1)
                    text.Append('\n');
            }
            return text.ToString();
        }

        private static string BuildMoveText(ChainlingSnapshot snapshot, MoveDatabaseSO moveDatabase)
        {
            var text = new StringBuilder("DEFAULT MOVE LIST\n");
            if (snapshot == null || snapshot.moveIds == null || snapshot.moveIds.Count == 0)
            {
                text.Append("No equipped moves.");
                return text.ToString();
            }

            for (int i = 0; i < snapshot.moveIds.Count; i++)
            {
                string moveId = snapshot.moveIds[i];
                if (string.IsNullOrWhiteSpace(moveId))
                    continue;
                MoveDefinitionSO move = moveDatabase != null ? moveDatabase.GetById(moveId) : null;
                string display = move != null && !string.IsNullOrWhiteSpace(move.displayName) ? move.displayName : moveId;
                text.Append(i + 1).Append(". ").Append(display);
                if (move != null)
                    text.Append("  •  ").Append(move.baseType).Append("  •  ").Append(move.category);
                text.Append('\n');
            }
            return text.ToString().TrimEnd();
        }

        private void EnsureBuilt()
        {
            if (transform.Find("Content") != null)
                return;

            if (maxHpLabel != null && placementLabel != null && loreLabel != null && radarGraphic != null &&
                radarValuesLabel != null && ivLabel != null && defaultMovesLabel != null)
                return;

            var root = PlayerMenuUiFactory.HorizontalLayout(gameObject, 14f);
            root.padding = new RectOffset(18, 18, 16, 16);
            root.childAlignment = TextAnchor.UpperCenter;
            root.childControlWidth = true;
            root.childControlHeight = true;
            root.childForceExpandWidth = true;
            root.childForceExpandHeight = true;

            RectTransform left = CreateColumn("FieldNotes", 0.9f);
            maxHpLabel = CreatePaperText("MaxHp", left, "MAX HP\n--", 24f, 104f, TextAlignmentOptions.Center);
            placementLabel = CreatePaperText("Placement", left, "PC STORAGE", 20f, 82f, TextAlignmentOptions.Center);
            loreLabel = CreatePaperText("Lore", left, "FIELD NOTES", 18f, 230f, TextAlignmentOptions.TopLeft);

            RectTransform middle = CreateColumn("AttributeRadar", 1.25f);
            Image radarPaper = PlayerMenuUiFactory.CreateImage("RadarPaper", middle, new Color(0.17f, 0.13f, 0.10f, 0.82f));
            PlayerMenuUiFactory.LayoutElement(radarPaper.gameObject, preferredHeight: 305f, flexibleWidth: 1f);
            var radarGo = new GameObject("Radar", typeof(RectTransform), typeof(CanvasRenderer), typeof(PartyTabAttributeRadarGraphic));
            radarGo.transform.SetParent(radarPaper.transform, false);
            radarGraphic = radarGo.GetComponent<PartyTabAttributeRadarGraphic>();
            RectTransform radarRect = radarGo.GetComponent<RectTransform>();
            radarRect.anchorMin = radarRect.anchorMax = new Vector2(0.5f, 0.5f);
            radarRect.pivot = new Vector2(0.5f, 0.5f);
            radarRect.sizeDelta = new Vector2(260f, 260f);
            radarValuesLabel = CreatePaperText("RadarValues", middle, "ATTRIBUTES", 18f, 104f, TextAlignmentOptions.TopLeft);

            RectTransform right = CreateColumn("SpecimenData", 1.15f);
            ivLabel = CreatePaperText("IvPlacement", right, "IV PLACEMENT", 18f, 245f, TextAlignmentOptions.TopLeft);
            defaultMovesLabel = CreatePaperText("DefaultMoves", right, "DEFAULT MOVE LIST", 18f, 230f, TextAlignmentOptions.TopLeft);
        }

        private RectTransform CreateColumn(string name, float flexibleWidth)
        {
            Image paper = PlayerMenuUiFactory.CreateImage(name, transform, new Color(0.21f, 0.16f, 0.12f, 0.9f));
            PlayerMenuUiFactory.LayoutElement(paper.gameObject, flexibleWidth: flexibleWidth, flexibleHeight: 1f);
            var layout = PlayerMenuUiFactory.VerticalLayout(paper.gameObject, 10f);
            layout.padding = new RectOffset(12, 12, 12, 12);
            return paper.rectTransform;
        }

        private static TMP_Text CreatePaperText(
            string name,
            Transform parent,
            string value,
            float fontSize,
            float height,
            TextAlignmentOptions alignment)
        {
            TMP_Text text = PlayerMenuUiFactory.CreateText(name, parent, value, fontSize, alignment, true);
            text.color = new Color(0.96f, 0.88f, 0.72f, 1f);
            PlayerMenuUiFactory.LayoutElement(text.gameObject, preferredHeight: height, flexibleWidth: 1f);
            return text;
        }
    }
}
