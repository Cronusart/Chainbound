using System;
using System.Collections.Generic;
using Chainbound.Combat;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SignalJam.UI.MenuNavigation
{
    /// <summary>Read-only species progression pool shown in the shared Chainling dossier.</summary>
    [DisallowMultipleComponent]
    public sealed class ChainlingLearnableMovesPanel : MonoBehaviour
    {
        [SerializeField] private TMP_Text headingLabel;
        [SerializeField] private TMP_Text statusLabel;
        [SerializeField] private ChainlingProgressionRewardTablesSO progressionRewardTables;
        [SerializeField] private ChainlingProgressionEventCatalogSO progressionEventCatalog;
        [SerializeField] private Button scrollUpButton;
        [SerializeField] private Button scrollDownButton;
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private RectTransform rowsRoot;
        [Tooltip("Inactive row the dossier clones for each progression-pool move. Styled in the prefab.")]
        [SerializeField] private Button rowTemplate;

        private readonly List<GameObject> rows = new List<GameObject>();

        private void Awake()
        {
            EnsureBuilt();
        }

        private void OnEnable()
        {
            if (scrollUpButton != null) scrollUpButton.onClick.AddListener(ScrollUp);
            if (scrollDownButton != null) scrollDownButton.onClick.AddListener(ScrollDown);
        }

        private void OnDisable()
        {
            if (scrollUpButton != null) scrollUpButton.onClick.RemoveListener(ScrollUp);
            if (scrollDownButton != null) scrollDownButton.onClick.RemoveListener(ScrollDown);
        }

        public void BuildDefaultLayoutForEditor()
        {
            EnsureBuilt();
        }

        public void Bind(PlayerRosterEntry entry, SpeciesDefinitionSO species)
        {
            EnsureBuilt();
            ClearRows();

            ChainlingSnapshot snapshot = entry != null ? entry.snapshot : null;
            if (headingLabel != null)
                headingLabel.text = species != null
                    ? $"{species.displayName.ToUpperInvariant()} • LEARNABLE MOVES"
                    : "LEARNABLE MOVES";

            List<SpeciesProgressionMoveEntry> pool = BuildUniquePool(species);
            int stars = snapshot != null ? snapshot.stars : 0;
            bool entangled = ChainlingProgressionIdentityUtility.IsEntangled(snapshot);
            ChainlingProgressionRewardTablesSO tables = ResolveRewardTables();
            MoveLearnSummaryPreview summary = MoveLearnPreviewUtility.BuildSummaryPreview(
                species,
                stars,
                entangled,
                tables,
                progressionEventCatalog);
            ChainlingProgressionResolvedPool resolved = MoveLearnPreviewUtility.ResolvePreviewPool(
                species,
                stars,
                entangled,
                progressionEventCatalog);
            Dictionary<string, float> folioShares = BuildFolioShares(resolved);

            if (statusLabel != null)
            {
                if (pool.Count == 0 || summary == null || !summary.poolConfigured)
                    statusLabel.text = pool.Count > 0
                        ? "Species progression pool • learned entries are marked"
                        : "No learnable move pool has been authored for this species.";
                else
                    statusLabel.text = summary.isMastery
                        ? $"Five-star mastery folio {summary.gatePercent}%"
                        : $"Next-star folio {summary.gatePercent}%";
            }

            SortForDossier(species, pool);
            for (int i = 0; i < pool.Count; i++)
                CreateRow(species, pool[i], snapshot, folioShares);
        }

        private ChainlingProgressionRewardTablesSO ResolveRewardTables()
        {
            if (progressionRewardTables != null)
                return progressionRewardTables;

            PostBattleRewardsConfigSO rewards = BalanceConfigRuntime.PostBattleRewards;
            return rewards != null ? rewards.progressionRewardTables : null;
        }

        private static Dictionary<string, float> BuildFolioShares(ChainlingProgressionResolvedPool pool)
        {
            var shares = new Dictionary<string, float>(StringComparer.Ordinal);
            if (pool?.entries == null)
                return shares;

            int total = pool.TotalWeight;
            for (int i = 0; i < pool.entries.Count; i++)
            {
                ResolvedProgressionMoveEntry entry = pool.entries[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.moveId))
                    continue;

                shares[entry.moveId] = total > 0
                    ? 100f * entry.effectiveWeight / total
                    : 0f;
            }

            return shares;
        }

        private static void SortForDossier(SpeciesDefinitionSO species, List<SpeciesProgressionMoveEntry> pool)
        {
            pool.Sort((a, b) =>
            {
                int signature = (IsSignature(species, b) ? 1 : 0) - (IsSignature(species, a) ? 1 : 0);
                if (signature != 0)
                    return signature;

                int rarityA = a?.move != null ? (int)a.move.rarity : -1;
                int rarityB = b?.move != null ? (int)b.move.rarity : -1;
                int rarity = rarityB.CompareTo(rarityA);
                if (rarity != 0)
                    return rarity;

                string nameA = a?.move != null ? a.move.displayName : "";
                string nameB = b?.move != null ? b.move.displayName : "";
                return string.Compare(nameA, nameB, StringComparison.Ordinal);
            });
        }

        private static bool IsSignature(SpeciesDefinitionSO species, SpeciesProgressionMoveEntry entry)
        {
            if (entry == null)
                return false;
            if (entry.isSignature)
                return true;

            return species != null && species.flagshipMove != null && entry.move == species.flagshipMove;
        }

        private static List<SpeciesProgressionMoveEntry> BuildUniquePool(SpeciesDefinitionSO species)
        {
            var result = new List<SpeciesProgressionMoveEntry>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            if (species == null)
                return result;

            if (species.progressionMovePool != null)
            {
                for (int i = 0; i < species.progressionMovePool.Count; i++)
                {
                    SpeciesProgressionMoveEntry entry = species.progressionMovePool[i];
                    if (entry?.move == null || entry.poolTag == ProgressionPoolTag.EventOnly ||
                        string.IsNullOrWhiteSpace(entry.move.moveId) || !ids.Add(entry.move.moveId))
                        continue;
                    result.Add(entry);
                }
            }

            if (result.Count == 0 && species.learnableMoves != null)
            {
                for (int i = 0; i < species.learnableMoves.Count; i++)
                {
                    MoveDefinitionSO move = species.learnableMoves[i];
                    if (move == null || string.IsNullOrWhiteSpace(move.moveId) || !ids.Add(move.moveId))
                        continue;
                    result.Add(new SpeciesProgressionMoveEntry { move = move, poolTag = ProgressionPoolTag.Learnable });
                }
            }
            return result;
        }

        private void CreateRow(
            SpeciesDefinitionSO species,
            SpeciesProgressionMoveEntry poolEntry,
            ChainlingSnapshot snapshot,
            Dictionary<string, float> folioShares)
        {
            MoveDefinitionSO move = poolEntry.move;
            bool learned = snapshot != null && snapshot.moveIds != null && snapshot.moveIds.Contains(move.moveId);
            float share = folioShares != null && folioShares.TryGetValue(move.moveId, out float found) ? found : 0f;
            bool signature = IsSignature(species, poolEntry);
            if (rowTemplate != null)
            {
                CreateRowFromTemplate(move, poolEntry, learned, share, signature);
                return;
            }

            Image paper = PlayerMenuUiFactory.CreateImage(
                $"Move_{move.moveId}",
                rowsRoot,
                learned ? new Color(0.24f, 0.25f, 0.15f, 0.95f) : new Color(0.20f, 0.15f, 0.12f, 0.92f));
            rows.Add(paper.gameObject);
            PlayerMenuUiFactory.LayoutElement(paper.gameObject, preferredHeight: 72f, flexibleWidth: 1f);

            TMP_Text text = PlayerMenuUiFactory.CreateText(
                "Label",
                paper.transform,
                string.Empty,
                18f,
                TextAlignmentOptions.MidlineLeft,
                false);
            PlayerMenuUiFactory.Stretch(text.rectTransform, 18f, 8f, 18f, 8f);
            text.color = new Color(0.96f, 0.88f, 0.72f, 1f);

            int min = Mathf.Clamp(poolEntry.minStar, 0, CombatConstants.STAR_MAX);
            int max = poolEntry.maxStar <= 0 ? CombatConstants.STAR_MAX : Mathf.Clamp(poolEntry.maxStar, min, CombatConstants.STAR_MAX);
            string gate = min == 0 && max == CombatConstants.STAR_MAX ? "Any star" : $"Stars {min}-{max}";
            string tag = signature ? "SIGNATURE" : poolEntry.poolTag.ToString().ToUpperInvariant();
            string shareLabel = MoveLearnPreviewUtility.FormatPercent(share);
            text.text = $"<b>{move.displayName}</b>   {move.baseType} • {move.category}   {move.rarity}   {shareLabel} of the folio   {gate}   [{tag}]" +
                        (learned ? "   <color=#79E0B1>LEARNED</color>" : string.Empty);
        }

        private void CreateRowFromTemplate(
            MoveDefinitionSO move,
            SpeciesProgressionMoveEntry poolEntry,
            bool learned,
            float share,
            bool signature)
        {
            GameObject row = Instantiate(rowTemplate.gameObject, rowsRoot);
            row.name = $"Move_{move.moveId}";
            row.SetActive(true);
            rows.Add(row);

            TMP_Text text = row.GetComponentInChildren<TMP_Text>(true);
            if (text == null)
                return;

            int min = Mathf.Clamp(poolEntry.minStar, 0, CombatConstants.STAR_MAX);
            int max = poolEntry.maxStar <= 0 ? CombatConstants.STAR_MAX : Mathf.Clamp(poolEntry.maxStar, min, CombatConstants.STAR_MAX);
            string gate = min == 0 && max == CombatConstants.STAR_MAX ? "Any star" : $"Stars {min}-{max}";
            string tag = signature ? "SIGNATURE" : poolEntry.poolTag.ToString().ToUpperInvariant();
            string shareLabel = MoveLearnPreviewUtility.FormatPercent(share);
            text.text = $"{move.displayName}    {move.baseType} · {move.category}    {move.rarity}    {shareLabel} of the folio    {gate}    {tag}" +
                        (learned ? "    LEARNED" : string.Empty);
        }

        private void ClearRows()
        {
            for (int i = 0; i < rows.Count; i++)
                if (rows[i] != null) Destroy(rows[i]);
            rows.Clear();

            if (rowsRoot == null)
                return;

            for (int i = rowsRoot.childCount - 1; i >= 0; i--)
            {
                Transform child = rowsRoot.GetChild(i);
                if (rowTemplate != null && child == rowTemplate.transform)
                    continue;
                if (child.GetComponent<Button>() == null)
                    continue;
                Destroy(child.gameObject);
            }
        }

        private void ScrollUp()
        {
            if (scrollRect != null)
                scrollRect.verticalNormalizedPosition = Mathf.Clamp01(scrollRect.verticalNormalizedPosition + 0.24f);
        }

        private void ScrollDown()
        {
            if (scrollRect != null)
                scrollRect.verticalNormalizedPosition = Mathf.Clamp01(scrollRect.verticalNormalizedPosition - 0.24f);
        }

        private void EnsureBuilt()
        {
            if (transform.Find("Content") != null && rowsRoot != null && scrollRect != null)
                return;

            if (headingLabel != null && statusLabel != null && scrollUpButton != null && scrollDownButton != null &&
                scrollRect != null && rowsRoot != null)
                return;

            var root = PlayerMenuUiFactory.VerticalLayout(gameObject, 10f);
            root.padding = new RectOffset(18, 18, 16, 16);
            RectTransform header = PlayerMenuUiFactory.CreateRect("Header", transform);
            PlayerMenuUiFactory.LayoutElement(header.gameObject, preferredHeight: 64f, flexibleWidth: 1f);
            var headerLayout = PlayerMenuUiFactory.HorizontalLayout(header.gameObject, 8f);
            headerLayout.childForceExpandWidth = false;
            headingLabel = PlayerMenuUiFactory.CreateText(
                "Heading", header, "LEARNABLE MOVES", 24f, TextAlignmentOptions.MidlineLeft, false);
            headingLabel.color = new Color(0.96f, 0.88f, 0.72f, 1f);
            PlayerMenuUiFactory.LayoutElement(headingLabel.gameObject, preferredHeight: 64f, flexibleWidth: 1f);
            scrollUpButton = PlayerMenuUiFactory.CreateButton("ScrollUp", header, "UP", new Color(0.25f, 0.18f, 0.13f, 1f));
            scrollDownButton = PlayerMenuUiFactory.CreateButton("ScrollDown", header, "DOWN", new Color(0.25f, 0.18f, 0.13f, 1f));
            PlayerMenuUiFactory.LayoutElement(scrollUpButton.gameObject, preferredWidth: 112f, preferredHeight: 60f);
            PlayerMenuUiFactory.LayoutElement(scrollDownButton.gameObject, preferredWidth: 112f, preferredHeight: 60f);
            MenuSelectableFocusVisual.EnsureOn(scrollUpButton);
            MenuSelectableFocusVisual.EnsureOn(scrollDownButton);
            statusLabel = PlayerMenuUiFactory.CreateText(
                "Status", transform, "Species progression pool", 17f, TextAlignmentOptions.MidlineLeft, false);
            statusLabel.color = new Color(0.72f, 0.62f, 0.50f, 1f);
            PlayerMenuUiFactory.LayoutElement(statusLabel.gameObject, preferredHeight: 32f, flexibleWidth: 1f);

            Image viewportImage = PlayerMenuUiFactory.CreateImage("Viewport", transform, new Color(0.14f, 0.10f, 0.08f, 0.9f));
            PlayerMenuUiFactory.LayoutElement(viewportImage.gameObject, flexibleWidth: 1f, flexibleHeight: 1f);
            Mask mask = viewportImage.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = true;
            rowsRoot = PlayerMenuUiFactory.CreateRect("Rows", viewportImage.transform);
            PlayerMenuUiFactory.Top(rowsRoot, 0f, 10f, 10f, 10f);
            rowsRoot.anchorMin = new Vector2(0f, 1f);
            rowsRoot.anchorMax = new Vector2(1f, 1f);
            rowsRoot.pivot = new Vector2(0.5f, 1f);
            rowsRoot.sizeDelta = Vector2.zero;
            var rowsLayout = PlayerMenuUiFactory.VerticalLayout(rowsRoot.gameObject, 8f);
            rowsLayout.padding = new RectOffset(0, 0, 0, 0);
            ContentSizeFitter fitter = rowsRoot.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect = viewportImage.gameObject.AddComponent<ScrollRect>();
            scrollRect.viewport = viewportImage.rectTransform;
            scrollRect.content = rowsRoot;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 32f;
        }
    }
}
