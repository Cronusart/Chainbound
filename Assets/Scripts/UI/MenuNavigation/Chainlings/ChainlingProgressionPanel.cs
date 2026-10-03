using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Chainbound.Combat;
using SignalJam.UI.MenuNavigation.PartyTab;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace SignalJam.UI.MenuNavigation
{
    [DisallowMultipleComponent]
    public class ChainlingProgressionPanel : MonoBehaviour
    {
        [Header("Services")]
        [SerializeField] private MonoBehaviour rebirthServiceBehaviour;

        [Header("Data")]
        [FormerlySerializedAs("rewardConfig")]
        [SerializeField] private ProgressionConfigSO progressionConfig;
        [SerializeField] private ChainlingProgressionRewardTablesSO progressionRewardTables;
        [SerializeField] private ChainlingProgressionEventCatalogSO progressionEventCatalog;
        [SerializeField] private SpeciesDatabaseSO speciesDatabase;

        [Header("UI")]
        [SerializeField] private TMP_Text headingLabel;
        [SerializeField] private TMP_Text summaryLabel;
        [SerializeField] private TMP_Text statsLabel;
        [SerializeField] private TMP_Text statusLabel;
        [SerializeField] private RectTransform statButtonRoot;
        [SerializeField] private Button rebirthButton;
        [SerializeField] private TMP_Text starsXpLabel;
        [SerializeField] private TMP_Text cycleLabel;
        [SerializeField] private TMP_Text[] ivLabels;
        [SerializeField] private TMP_Text[] baseLabels;
        [SerializeField] private TMP_Text victoriesLabel;
        [SerializeField] private TMP_Text victoriesValueLabel;
        [SerializeField] private TMP_Text enemyKosLabel;
        [SerializeField] private TMP_Text enemyKosValueLabel;
        [SerializeField] private TMP_Text damageDealtLabel;
        [SerializeField] private TMP_Text damageDealtValueLabel;
        [SerializeField] private TMP_Text damageTakenLabel;
        [SerializeField] private TMP_Text damageTakenValueLabel;
        [SerializeField] private TMP_Text xpEarnedLabel;
        [SerializeField] private TMP_Text xpEarnedValueLabel;
        [SerializeField] private TMP_Text timesKodLabel;
        [SerializeField] private TMP_Text timesKodValueLabel;
        [SerializeField] private RectTransform[] radarStatIcons;
        [SerializeField] private float radarIconGap = 18f;
        [SerializeField] private float radarIconSize = 28f;
        [SerializeField] private Image xpBarFill;
        [SerializeField] private PartyTabStarRatingDisplay starRating;
        [SerializeField] private PartyTabCycleProgressDisplay cycleProgress;
        [SerializeField] private PartyTabAttributeRadarGraphic attributeRadar;
        [SerializeField] private bool fitRadarToRect = true;
        [SerializeField, Range(0.2f, 0.48f)] private float radarRadiusFactor = 0.42f;
        [SerializeField] private Color positiveIvColor = new Color(0.48f, 0.82f, 0.40f, 1f);
        [SerializeField] private Color negativeIvColor = new Color(0.86f, 0.34f, 0.36f, 1f);
        [SerializeField] private Color neutralIvColor = new Color(0.84f, 0.85f, 0.74f, 1f);

        private readonly List<Button> statButtons = new List<Button>();
        private readonly CoreStat[] stats =
        {
            CoreStat.MaxHP,
            CoreStat.MeleeAtk,
            CoreStat.MeleeDef,
            CoreStat.RangedAtk,
            CoreStat.RangedDef,
            CoreStat.Speed,
        };

        private PlayerRosterEntry entry;
        private PlayerRosterSnapshot rosterCache;
        private SpeciesDefinitionSO species;
        private CoreStat selectedStat = CoreStat.MaxHP;
        private bool hasSelectedStat;
        private bool awaitingConfirm;
        private bool isBusy;

        public event Action<PlayerRosterEntry> Reborn;

        private void Awake()
        {
            EnsureBuilt();
        }

        public void BuildDefaultLayoutForEditor()
        {
            EnsureBuilt();
        }

        private void OnEnable()
        {
            if (rebirthButton != null)
                rebirthButton.onClick.AddListener(HandleRebirthClicked);
            FitRadarToRect();
        }

        private void OnRectTransformDimensionsChange()
        {
            FitRadarToRect();
        }

        private void OnDisable()
        {
            if (rebirthButton != null)
                rebirthButton.onClick.RemoveListener(HandleRebirthClicked);
        }

        public void Configure(
            ProgressionConfigSO config,
            MonoBehaviour rebirthServiceSource,
            ChainlingProgressionRewardTablesSO rewardTables = null,
            ChainlingProgressionEventCatalogSO eventCatalog = null,
            SpeciesDatabaseSO speciesDb = null)
        {
            if (config != null)
                progressionConfig = config;
            if (rebirthServiceSource != null)
                rebirthServiceBehaviour = rebirthServiceSource;
            if (rewardTables != null)
                progressionRewardTables = rewardTables;
            if (eventCatalog != null)
                progressionEventCatalog = eventCatalog;
            if (speciesDb != null)
                speciesDatabase = speciesDb;
        }

        public void Bind(PlayerRosterEntry rosterEntry, SpeciesDefinitionSO speciesDefinition, PlayerRosterSnapshot cache)
        {
            EnsureBuilt();
            entry = rosterEntry;
            species = speciesDefinition;
            rosterCache = cache;
            hasSelectedStat = false;
            awaitingConfirm = false;
            Refresh();
        }

        private void Refresh()
        {
            ChainlingSnapshot snapshot = entry != null ? entry.snapshot : null;
            if (snapshot == null)
            {
                if (transform.Find("Content") == null)
                    SetStatus("Select a Chainling to inspect progression.");
                return;
            }

            string name = !string.IsNullOrWhiteSpace(snapshot.displayName)
                ? snapshot.displayName
                : species != null && !string.IsNullOrWhiteSpace(species.displayName)
                    ? species.displayName
                    : snapshot.speciesId;
            if (headingLabel != null)
                headingLabel.text = $"Progression - {name}";

            int stars = ChainlingProgressionUtility.NormalizeStars(snapshot.stars);
            int rebirth = ChainlingProgressionUtility.NormalizeRebirthCount(snapshot.rebirthCount);
            int masteryXp = Mathf.Max(0, entry.masteryXp);
            int nextXp = StarProgressionUtility.GetXpToNextMilestone(masteryXp, rebirth, ResolveConfig());
            int rebirthCost = ResolveConfig().GetRebirthRamCost(rebirth);
            int autoCombat = ChainlingProgressionUtility.GetCycleAutoFlat(CoreStat.MeleeAtk, rebirth);
            int speciesBaseHp = species != null ? species.baseMaxHP : 0;
            int autoHp = ChainlingProgressionUtility.GetCycleAutoFlat(CoreStat.MaxHP, rebirth, speciesBaseHp);
            MoveLearnSummaryPreview folioPreview = MoveLearnPreviewUtility.BuildSummaryPreview(
                species,
                stars,
                ChainlingProgressionIdentityUtility.IsEntangled(snapshot),
                ResolveRewardTables(),
                progressionEventCatalog,
                ChainlingProgressionOwnedMovesUtility.CollectFromSnapshot(snapshot));
            string folioChance = folioPreview != null && folioPreview.poolConfigured
                ? $"{folioPreview.gatePercent}%"
                : MoveLearnPreviewUtility.UnconfiguredPoolLabel;
            string folioLabel = folioPreview != null && folioPreview.isMastery
                ? "Five-star mastery folio chance"
                : "Next-star folio chance";

            if (transform.Find("Content") != null)
            {
                BindAuthoredWireframe(snapshot, stars, rebirth, masteryXp, nextXp, autoCombat, folioLabel, folioChance);
                return;
            }

            if (summaryLabel != null)
            {
                summaryLabel.text =
                    $"Stars {stars}/{CombatConstants.STAR_MAX} | {ChainlingPlayerTerms.FormatCycleLabel(rebirth)} | " +
                    $"Mastery XP {masteryXp} | Next milestone {nextXp} XP | " +
                    $"{folioLabel}: {folioChance} | Cycle auto +{autoCombat} combat / +{autoHp} MaxHP";
            }

            if (statsLabel != null)
                statsLabel.text = BuildStatsText(snapshot, rebirthCost);

            RefreshStatButtons();
            RefreshRebirthButton();
        }

        private string BuildStatsText(ChainlingSnapshot snapshot, int rebirthCost)
        {
            int maxHp = ChainlingAttributeDisplayUtility.BuildMaxHp(snapshot, species);
            IReadOnlyList<ChainlingAttributeStatValue> party =
                ChainlingAttributeDisplayUtility.BuildPartyStatValues(snapshot, species);
            int meleeAtk = party.Count > 0 ? party[0].Total : 1;
            int meleeDef = party.Count > 1 ? party[1].Total : 1;
            int rangedAtk = party.Count > 2 ? party[2].Total : 1;
            int rangedDef = party.Count > 3 ? party[3].Total : 1;
            int speed = party.Count > 4 ? party[4].Total : 1;
            return
                $"HP {maxHp}   MELEE {meleeAtk}/{meleeDef}   RANGED {rangedAtk}/{rangedDef}   SPEED {speed}\n" +
                $"Next cycle cost: {rebirthCost} RAM. Cycling resets stars/mastery and grants permanent Cycle flats " +
                $"(+{CombatConstants.CYCLE_AUTO_STAT_FLAT} all combat / +{CombatConstants.CYCLE_AUTO_HP_PERCENT:P0} species MaxHP auto, " +
                $"+{CombatConstants.CYCLE_PICK_STAT_FLAT} chosen combat or +{CombatConstants.CYCLE_PICK_HP_PERCENT:P0} species MaxHP pick).";
        }

        private void RefreshStatButtons()
        {
            EnsureStatButtonCount();
            for (int i = 0; i < statButtons.Count; i++)
            {
                CoreStat stat = stats[i];
                Button button = statButtons[i];
                Image image = button.targetGraphic as Image;
                if (image != null)
                    image.color = hasSelectedStat && selectedStat == stat ? PlayerMenuUiFactory.Accent : PlayerMenuUiFactory.PanelAlt;

                TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
                if (text != null)
                    text.text = StatLabel(stat);

                button.interactable = !isBusy;
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() =>
                {
                    selectedStat = stat;
                    hasSelectedStat = true;
                    awaitingConfirm = false;
                    SetStatus($"{StatLabel(stat)} selected for the permanent cycle bonus.");
                    Refresh();
                });
            }
        }

        private void RefreshRebirthButton()
        {
            if (rebirthButton == null)
                return;

            bool canRebirth = CanRebirth(out string reason);
            rebirthButton.interactable = !isBusy && canRebirth && hasSelectedStat;
            TMP_Text text = rebirthButton.GetComponentInChildren<TMP_Text>(true);
            if (text != null)
            {
                if (!canRebirth)
                    text.text = reason;
                else if (!hasSelectedStat)
                    text.text = "Choose Cycle Stat";
                else
                    text.text = awaitingConfirm ? "Confirm Cycle" : "Begin Cycle";
            }
        }

        private bool CanRebirth(out string reason)
        {
            reason = "Cycle";
            ChainlingSnapshot snapshot = entry != null ? entry.snapshot : null;
            if (snapshot == null)
            {
                reason = "No Chainling";
                return false;
            }

            int stars = ChainlingProgressionUtility.NormalizeStars(snapshot.stars);
            int rebirth = ChainlingProgressionUtility.NormalizeRebirthCount(snapshot.rebirthCount);
            if (string.IsNullOrWhiteSpace(snapshot.backendChainlingId))
            {
                reason = "No Backend ID";
                return false;
            }

            if (stars < CombatConstants.STAR_MAX)
            {
                reason = $"Needs {CombatConstants.STAR_MAX} Stars";
                return false;
            }

            if (rebirth >= CombatConstants.REBIRTH_MAX)
            {
                reason = "Max Cycle";
                return false;
            }

            return true;
        }

        private async void HandleRebirthClicked()
        {
            if (isBusy || !hasSelectedStat)
                return;

            if (!CanRebirth(out string reason))
            {
                SetStatus(reason);
                RefreshRebirthButton();
                return;
            }

            if (!awaitingConfirm)
            {
                awaitingConfirm = true;
                SetStatus($"Press Confirm Cycle to spend RAM and add +1 {StatLabel(selectedStat)}.");
                RefreshRebirthButton();
                return;
            }

            await RebirthAsync();
        }

        private async Task RebirthAsync()
        {
            IChainlingRebirthService service = ResolveRebirthService();
            if (service == null)
            {
                SetStatus("No cycle service was found.");
                return;
            }

            var session = GameSessionContext.Instance;
            string chainlingId = entry != null && entry.snapshot != null ? entry.snapshot.backendChainlingId : "";
            SetBusy(true, "Cycling Chainling...");
            ChainlingRebirthResult result = await service.RebirthAsync(
                session != null ? session.PlayerProfileId : "",
                session != null ? session.WalletAddress : "",
                chainlingId,
                selectedStat);

            SetBusy(false, "");
            awaitingConfirm = false;
            if (result == null || !result.success)
            {
                SetStatus(result != null ? result.error : "Cycle failed.");
                Refresh();
                return;
            }

            if (result.updatedChainling != null && entry != null)
            {
                entry.snapshot = result.updatedChainling;
                entry.masteryXp = 0;
                PatchRosterCache(result.updatedChainling);
            }

            SetStatus($"Cycle complete. RAM balance: {result.ramBalance}");
            hasSelectedStat = false;
            Refresh();
            Reborn?.Invoke(entry);
        }

        private void PatchRosterCache(ChainlingSnapshot updated)
        {
            if (rosterCache == null || rosterCache.chainlings == null || updated == null)
                return;

            for (int i = 0; i < rosterCache.chainlings.Count; i++)
            {
                PlayerRosterEntry rosterEntry = rosterCache.chainlings[i];
                if (rosterEntry != null && rosterEntry.snapshot != null &&
                    string.Equals(rosterEntry.snapshot.backendChainlingId, updated.backendChainlingId, StringComparison.Ordinal))
                {
                    rosterEntry.snapshot = updated;
                    rosterEntry.masteryXp = 0;
                    return;
                }
            }
        }

        private void SetBusy(bool busy, string status)
        {
            isBusy = busy;
            SetStatus(status);
            RefreshStatButtons();
            RefreshRebirthButton();
        }

        private void SetStatus(string value)
        {
            if (statusLabel != null)
                statusLabel.text = value ?? "";
        }

        private ProgressionConfigSO ResolveConfig()
        {
            return progressionConfig != null ? progressionConfig : BalanceConfigRuntime.Progression;
        }

        private ChainlingProgressionRewardTablesSO ResolveRewardTables()
        {
            if (progressionRewardTables != null)
                return progressionRewardTables;

            PostBattleRewardsConfigSO rewards = BalanceConfigRuntime.PostBattleRewards;
            return rewards != null ? rewards.progressionRewardTables : null;
        }

        private IChainlingRebirthService ResolveRebirthService()
        {
            if (rebirthServiceBehaviour is IChainlingRebirthService assigned)
                return assigned;

            MonoBehaviour[] behaviours = FindObjectsOfType<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IChainlingRebirthService service)
                    return service;
            }

            return null;
        }

        private void BindAuthoredWireframe(
            ChainlingSnapshot snapshot,
            int stars,
            int rebirth,
            int masteryXp,
            int nextXp,
            int autoCombat,
            string folioLabel,
            string folioChance)
        {
            StarProgressionUtility.GetMilestoneSegment(
                masteryXp,
                rebirth,
                ResolveConfig(),
                out _,
                out _,
                out float xpFill);
            if (xpBarFill != null)
                PartyTabProgressBarUtility.SetFill01(xpBarFill, xpFill);
            if (starRating != null)
                starRating.SetRating(stars, true, true);
            if (cycleProgress != null)
                cycleProgress.SetCycle(ChainlingPlayerTerms.FormatCycleLabel(rebirth), rebirth, true);

            if (starsXpLabel != null)
            {
                starsXpLabel.text =
                    $"Stars {stars}/{CombatConstants.STAR_MAX}    {masteryXp} XP    Next {nextXp}    {folioLabel}: {folioChance}";
            }

            if (cycleLabel != null)
                cycleLabel.text = $"{ChainlingPlayerTerms.FormatCycleLabel(rebirth)}    Cycle auto +{autoCombat}";

            if (ivLabels != null)
            {
                for (int i = 0; i < ivLabels.Length; i++)
                {
                    TMP_Text label = ivLabels[i];
                    if (label == null)
                        continue;
                    int value = snapshot.ivs != null && i < snapshot.ivs.Length ? snapshot.ivs[i] : 0;
                    label.text = value > 0 ? $"+{value}" : value < 0 ? value.ToString() : "+0";
                    label.color = value > 0 ? positiveIvColor : value < 0 ? negativeIvColor : neutralIvColor;
                }
            }

            BindNatureBases();

            var career = new ChainlingPartyColumnSummary();
            ChainlingPartyStatsDisplayUtility.PopulateSummary(career, new ChainlingPartyColumnContext
            {
                entry = entry,
                species = species,
            });
            SetCareerLabel(victoriesLabel, victoriesValueLabel, career, ChainlingPartyStatsDisplayUtility.VictoriesLabel, null);
            SetCareerLabel(enemyKosLabel, enemyKosValueLabel, career, ChainlingPartyStatsDisplayUtility.EnemyKosLabel, null);
            SetCareerLabel(damageDealtLabel, damageDealtValueLabel, career, ChainlingPartyStatsDisplayUtility.DamageDealtLabel, "DMG dealt");
            SetCareerLabel(damageTakenLabel, damageTakenValueLabel, career, ChainlingPartyStatsDisplayUtility.DamageTakenLabel, "DMG taken");
            SetCareerLabel(xpEarnedLabel, xpEarnedValueLabel, career, ChainlingPartyStatsDisplayUtility.XpEarnedLabel, null);
            SetCareerLabel(timesKodLabel, timesKodValueLabel, career, ChainlingPartyStatsDisplayUtility.TimesKodLabel, null);
            BindAttributeRadar(snapshot);
        }

        private void BindAttributeRadar(ChainlingSnapshot snapshot)
        {
            if (attributeRadar == null)
                return;

            IReadOnlyList<ChainlingAttributeRadarAxis> axes =
                ChainlingAttributeDisplayUtility.BuildPartyRadarAxes(snapshot, species, speciesDatabase);
            var fills = new float[PartyTabAttributeRadarGraphic.AxisCount];
            for (int i = 0; i < fills.Length; i++)
                fills[i] = axes != null && i < axes.Count ? axes[i].Fill01 : 0f;
            attributeRadar.SetFills(fills);
            Canvas.ForceUpdateCanvases();
            FitRadarToRect();
        }

        private void FitRadarToRect()
        {
            if (!Application.isPlaying || !fitRadarToRect || attributeRadar == null)
                return;

            Rect rect = attributeRadar.rectTransform.rect;
            float radius = Mathf.Min(rect.width, rect.height) * radarRadiusFactor;
            if (radius < 12f)
                return;

            attributeRadar.SetOuterRadius(radius);
            PlaceRadarStatIcons();
        }

        private void BindNatureBases()
        {
            if (baseLabels == null)
                return;

            int[] bases = ResolveNatureBases(species);
            for (int i = 0; i < baseLabels.Length; i++)
            {
                TMP_Text label = baseLabels[i];
                if (label == null)
                    continue;
                label.text = bases[i].ToString();
            }
        }

        private static int[] ResolveNatureBases(SpeciesDefinitionSO speciesDefinition)
        {
            if (speciesDefinition == null)
                return new[] { 0, 0, 0, 0, 0, 0 };

            return new[]
            {
                speciesDefinition.baseMaxHP,
                speciesDefinition.baseMeleeAtk,
                speciesDefinition.baseMeleeDef,
                speciesDefinition.baseRangedAtk,
                speciesDefinition.baseRangedDef,
                speciesDefinition.baseSpeed,
            };
        }

        private void PlaceRadarStatIcons()
        {
            if (attributeRadar == null || radarStatIcons == null)
                return;

            float radius = attributeRadar.OuterRadius;
            if (radius < 1f)
                return;

            Rect rect = attributeRadar.rectTransform.rect;
            float width = Mathf.Abs(rect.width);
            float height = Mathf.Abs(rect.height);
            float limit = width > 1f && height > 1f ? Mathf.Min(width, height) : Mathf.Max(width, height);
            float center = radius + Mathf.Max(0f, radarIconGap);
            if (limit > 20f)
                center = Mathf.Min(center, limit * 0.5f - Mathf.Max(8f, radarIconSize) * 0.5f);

            for (int i = 0; i < radarStatIcons.Length; i++)
            {
                RectTransform icon = radarStatIcons[i];
                if (icon == null)
                    continue;
                icon.anchorMin = new Vector2(0.5f, 0.5f);
                icon.anchorMax = new Vector2(0.5f, 0.5f);
                icon.pivot = new Vector2(0.5f, 0.5f);
                icon.anchoredPosition = attributeRadar.GetAxisPoint(i, center);
            }
        }

        private static void SetCareerLabel(
            TMP_Text label,
            TMP_Text valueLabel,
            ChainlingPartyColumnSummary career,
            string key,
            string displayTitle)
        {
            if (label == null || career == null || career.lines == null)
                return;

            for (int i = 0; i < career.lines.Count; i++)
            {
                ChainlingPartyDisplayLine line = career.lines[i];
                if (!string.Equals(line.Label, key, System.StringComparison.Ordinal))
                    continue;

                string title = string.IsNullOrEmpty(displayTitle) ? line.Label : displayTitle;
                if (valueLabel != null)
                {
                    label.text = title;
                    valueLabel.text = line.Value;
                }
                else
                {
                    label.text = $"{title}    {line.Value}";
                }
                return;
            }
        }

        private void EnsureBuilt()
        {
            if (transform.Find("Content") != null)
                return;

            if (statButtonRoot != null && rebirthButton != null)
                return;

            var rootLayout = PlayerMenuUiFactory.VerticalLayout(gameObject, 12f);
            rootLayout.padding = new RectOffset(14, 14, 14, 14);

            headingLabel = headingLabel != null
                ? headingLabel
                : PlayerMenuUiFactory.CreateText("Heading", transform, "Progression", 25f, TextAlignmentOptions.MidlineLeft, false);
            PlayerMenuUiFactory.LayoutElement(headingLabel.gameObject, preferredHeight: 34f);

            summaryLabel = summaryLabel != null
                ? summaryLabel
                : PlayerMenuUiFactory.CreateText("Summary", transform, "", 20f, TextAlignmentOptions.MidlineLeft, true);
            summaryLabel.color = PlayerMenuUiFactory.Muted;
            PlayerMenuUiFactory.LayoutElement(summaryLabel.gameObject, preferredHeight: 76f);

            statsLabel = statsLabel != null
                ? statsLabel
                : PlayerMenuUiFactory.CreateText("Stats", transform, "", 20f, TextAlignmentOptions.MidlineLeft, true);
            PlayerMenuUiFactory.LayoutElement(statsLabel.gameObject, preferredHeight: 86f);

            TMP_Text selectorLabel = PlayerMenuUiFactory.CreateText("SelectorLabel", transform, "Cycle stat bonus", 21f, TextAlignmentOptions.MidlineLeft, false);
            PlayerMenuUiFactory.LayoutElement(selectorLabel.gameObject, preferredHeight: 30f);

            statButtonRoot = statButtonRoot != null ? statButtonRoot : PlayerMenuUiFactory.CreateRect("StatButtons", transform);
            PlayerMenuUiFactory.LayoutElement(statButtonRoot.gameObject, preferredHeight: 126f);
            var grid = PlayerMenuUiFactory.GetOrAdd<GridLayoutGroup>(statButtonRoot.gameObject);
            grid.cellSize = new Vector2(170f, 52f);
            grid.spacing = new Vector2(8f, 8f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 3;

            rebirthButton = rebirthButton != null
                ? rebirthButton
                : PlayerMenuUiFactory.CreateButton("RebirthButton", transform, "Begin Cycle", PlayerMenuUiFactory.Accent);
            PlayerMenuUiFactory.LayoutElement(rebirthButton.gameObject, preferredHeight: 60f);

            statusLabel = statusLabel != null
                ? statusLabel
                : PlayerMenuUiFactory.CreateText("Status", transform, "", 19f, TextAlignmentOptions.MidlineLeft, true);
            statusLabel.color = PlayerMenuUiFactory.Muted;
            PlayerMenuUiFactory.LayoutElement(statusLabel.gameObject, preferredHeight: 60f);
        }

        private void EnsureStatButtonCount()
        {
            while (statButtons.Count < stats.Length)
            {
                Button button = PlayerMenuUiFactory.CreateButton($"Stat_{statButtons.Count:D2}", statButtonRoot, "", PlayerMenuUiFactory.PanelAlt);
                TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
                if (text != null)
                    text.fontSize = 18f;
                statButtons.Add(button);
            }
        }

        private static string StatLabel(CoreStat stat)
        {
            switch (stat)
            {
                case CoreStat.MaxHP: return "Max HP";
                case CoreStat.MeleeAtk: return "Melee Atk";
                case CoreStat.MeleeDef: return "Melee Def";
                case CoreStat.RangedAtk: return "Ranged Atk";
                case CoreStat.RangedDef: return "Ranged Def";
                case CoreStat.Speed: return "Speed";
                default: return stat.ToString();
            }
        }
    }
}
