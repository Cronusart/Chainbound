using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Chainbound.Combat;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SignalJam.UI.MenuNavigation
{
    [DisallowMultipleComponent]
    public class ChainlingMoveEquipPanel : MonoBehaviour
    {
        [Header("Services")]
        [SerializeField] private MonoBehaviour moveInventoryProviderBehaviour;
        [SerializeField] private MonoBehaviour moveEquipServiceBehaviour;

        [Header("Data")]
        [SerializeField] private SpeciesDatabaseSO speciesDatabase;
        [SerializeField] private MoveDatabaseSO moveDatabase;

        [Header("UI")]
        [SerializeField] private TMP_Text headingLabel;
        [SerializeField] private TMP_Text slotSummaryLabel;
        [SerializeField] private TMP_Text statusLabel;
        [Tooltip("The authored scroll content for the unified inventory list.")]
        [SerializeField] private RectTransform moveRowsRoot;
        [Tooltip("Inactive pooled move-row template inside MoveRowsRoot.")]
        [SerializeField] private Button moveRowTemplate;
        [Tooltip("Focusable non-move placeholder shown while the inventory is empty or loading.")]
        [SerializeField] private Button emptyStateButton;
        [SerializeField] private RectTransform equippedRoot;
        [SerializeField] private RectTransform availableRoot;
        [SerializeField] private Button refreshButton;
        [Tooltip("Legacy authored labels retained so existing prefabs deserialize. The unified list ignores them.")]
        [SerializeField] private TMP_Text[] loadoutRowLabels;

        private readonly List<Button> moveButtons = new List<Button>();
        private readonly List<MenuMoveItem> equippedMoves = new List<MenuMoveItem>();
        private readonly List<MenuMoveItem> allMoves = new List<MenuMoveItem>();
        private PlayerRosterEntry entry;
        private MoveInventorySnapshot inventory;
        private bool isBusy;
        private bool readOnly;

        public event Action LoadoutChanged;
        public event Action NavigationChanged;

        public void SetReadOnly(bool value)
        {
            readOnly = value;
            RefreshLabels();
            RefreshMoveButtons();
        }

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
            if (refreshButton != null)
                refreshButton.onClick.AddListener(HandleRefreshClicked);
        }

        private void OnDisable()
        {
            if (refreshButton != null)
                refreshButton.onClick.RemoveListener(HandleRefreshClicked);
        }

        public void Configure(
            SpeciesDatabaseSO speciesDb,
            MoveDatabaseSO moveDb,
            MonoBehaviour moveInventoryProviderSource,
            MonoBehaviour moveEquipServiceSource)
        {
            if (speciesDb != null)
                speciesDatabase = speciesDb;
            if (moveDb != null)
                moveDatabase = moveDb;
            if (moveInventoryProviderSource != null)
                moveInventoryProviderBehaviour = moveInventoryProviderSource;
            if (moveEquipServiceSource != null)
                moveEquipServiceBehaviour = moveEquipServiceSource;
        }

        public void Bind(PlayerRosterEntry rosterEntry)
        {
            EnsureBuilt();
            entry = rosterEntry;
            inventory = null;
            RefreshLabels();
            _ = RefreshInventoryAsync();
        }

        private void HandleRefreshClicked()
        {
            if (!isBusy)
                _ = RefreshInventoryAsync();
        }

        private async Task RefreshInventoryAsync()
        {
            if (entry == null || entry.snapshot == null)
            {
                SetStatus("Select a Chainling to manage moves.");
                return;
            }

            IMoveInventoryProvider provider = ResolveMoveInventoryProvider();
            if (provider == null)
            {
                SetStatus("No move inventory provider was found.");
                return;
            }

            SetBusy(true, "Loading move inventory...");
            var session = GameSessionContext.Instance;
            inventory = await provider.FetchMoveInventoryAsync(
                session != null ? session.PlayerProfileId : "",
                session != null ? session.WalletAddress : "");
            SetBusy(false, inventory == null ? "Move inventory failed to load." : "");
            RebuildLists();
            RefreshLabels();
            RefreshMoveButtons();
        }

        private async void EquipMove(MenuMoveItem move)
        {
            if (readOnly || isBusy || move == null || entry == null || entry.snapshot == null)
                return;

            if (move.bindSealed)
            {
                SetStatus("This move is sealed into the Chainling archive and cannot be equipped.");
                return;
            }

            if (move.escrowLocked || move.locked)
            {
                SetStatus("This move is escrowed in PvP and cannot be equipped.");
                return;
            }

            if (!string.IsNullOrWhiteSpace(move.equippedChainlingId))
            {
                SetStatus($"{EquippedMark(move)}. Remove it there before equipping it here.");
                return;
            }

            ChainlingLoadout loadout = BuildLoadout();
            MoveInstance instance = move.ToMoveInstance(moveDatabase);
            LoadoutValidationResult validation = loadout.CanEquip(instance);
            if (!validation.isValid)
            {
                SetStatus(validation.message);
                return;
            }

            int slot = FindFirstFreeSlot(loadout.MaxSlots);
            if (slot < 0)
            {
                SetStatus("No loadout slots are available.");
                return;
            }

            await PersistMoveMutation(slot, move.moveItemId, "Equipping move...");
        }

        private async void UnequipMove(MenuMoveItem move)
        {
            if (readOnly || isBusy || move == null || entry == null || entry.snapshot == null)
                return;

            if (move.bindSealed)
            {
                SetStatus("This move is sealed into the Chainling archive and cannot be unequipped.");
                return;
            }

            if (move.escrowLocked || move.locked)
            {
                SetStatus("This move is escrowed in PvP and cannot be unequipped.");
                return;
            }

            LoadoutValidationResult validation = BuildLoadout().CanUnequip(move.ToMoveInstance(moveDatabase));
            if (!validation.isValid)
            {
                SetStatus(validation.message);
                return;
            }

            int slot = move.equippedSlot >= 0 ? move.equippedSlot : FindSlotForMove(move.moveItemId);
            if (slot < 0)
            {
                SetStatus("Could not resolve the equipped slot for this move.");
                return;
            }

            await PersistMoveMutation(slot, "", "Unequipping move...");
        }

        private async Task PersistMoveMutation(int slot, string moveItemId, string busyMessage)
        {
            IMenuMoveEquipService service = ResolveMoveEquipService();
            if (service == null)
            {
                SetStatus("No move equip service was found.");
                return;
            }

            var session = GameSessionContext.Instance;
            string chainlingId = entry != null && entry.snapshot != null ? entry.snapshot.backendChainlingId : "";
            SetBusy(true, busyMessage);
            MenuMutationResult result = await service.EquipMoveAsync(
                session != null ? session.PlayerProfileId : "",
                session != null ? session.WalletAddress : "",
                chainlingId,
                slot,
                moveItemId);

            if (result == null || !result.success)
            {
                SetBusy(false, result != null ? result.error : "Move mutation failed.");
                return;
            }

            LoadoutChanged?.Invoke();
            await RefreshInventoryAsync();
            SetStatus(string.IsNullOrWhiteSpace(moveItemId) ? "Move unequipped." : "Move equipped.");
        }

        private void RebuildLists()
        {
            equippedMoves.Clear();
            allMoves.Clear();

            if (inventory == null || inventory.moves == null || entry == null || entry.snapshot == null)
                return;

            string chainlingId = entry.snapshot.backendChainlingId ?? "";
            for (int i = 0; i < inventory.moves.Count; i++)
            {
                MenuMoveItem move = inventory.moves[i];
                if (move == null)
                    continue;

                allMoves.Add(move);
                if (string.Equals(move.equippedChainlingId, chainlingId, StringComparison.Ordinal))
                    equippedMoves.Add(move);
            }

            equippedMoves.Sort((a, b) => a.equippedSlot.CompareTo(b.equippedSlot));
            allMoves.Sort((a, b) => CompareMoveRows(a, b, chainlingId));
        }

        private ChainlingLoadout BuildLoadout()
        {
            SpeciesDefinitionSO species = ResolveSpecies();
            var snapshot = entry != null ? entry.snapshot : null;
            var loadout = new ChainlingLoadout
            {
                speciesId = snapshot != null ? snapshot.speciesId : "",
                speciesDefinition = species,
                speciesDatabase = speciesDatabase,
                primaryType = species != null ? species.primaryType : ChainlingType.None,
                secondaryType = species != null ? species.secondaryType : ChainlingType.None,
                isEntangled = snapshot != null && snapshot.isEntangled,
                entangledType = snapshot != null ? snapshot.entangledType : ChainlingType.None,
                evolutionStage = snapshot != null && snapshot.evolutionStage > 0 ? snapshot.evolutionStage : 1,
                rebirthCount = snapshot != null ? snapshot.rebirthCount : 0,
            };

            for (int i = 0; i < equippedMoves.Count; i++)
            {
                MenuMoveItem move = equippedMoves[i];
                if (move != null)
                    loadout.equipped.Add(move.ToMoveInstance(moveDatabase));
            }

            return loadout;
        }

        private int FindFirstFreeSlot(int maxSlots)
        {
            for (int slot = 0; slot < maxSlots; slot++)
            {
                bool occupied = false;
                for (int i = 0; i < equippedMoves.Count; i++)
                {
                    if (equippedMoves[i] != null && equippedMoves[i].equippedSlot == slot)
                    {
                        occupied = true;
                        break;
                    }
                }

                if (!occupied)
                    return slot;
            }

            return -1;
        }

        private int FindSlotForMove(string moveItemId)
        {
            if (inventory == null || inventory.loadouts == null)
                return -1;

            for (int i = 0; i < inventory.loadouts.Count; i++)
            {
                MenuMoveLoadoutSlot slot = inventory.loadouts[i];
                if (slot != null &&
                    string.Equals(slot.chainlingId, entry.snapshot.backendChainlingId, StringComparison.Ordinal) &&
                    string.Equals(slot.moveItemId, moveItemId, StringComparison.Ordinal))
                    return slot.slot;
            }

            return -1;
        }

        private void RefreshLabels()
        {
            SpeciesDefinitionSO species = ResolveSpecies();
            string name = entry != null && entry.snapshot != null
                ? (!string.IsNullOrWhiteSpace(entry.snapshot.displayName) ? entry.snapshot.displayName : entry.snapshot.speciesId)
                : "Chainling";
            if (headingLabel != null)
                headingLabel.text = readOnly ? $"Move List - {name}" : $"Moves - {name}";

            ChainlingLoadout loadout = BuildLoadout();
            if (slotSummaryLabel != null)
            {
                string typeText = species != null
                    ? species.secondaryType == ChainlingType.None
                        ? species.primaryType.ToString()
                        : $"{species.primaryType} / {species.secondaryType}"
                    : "Type unknown";
                slotSummaryLabel.text = $"{allMoves.Count} owned | {equippedMoves.Count} equipped | Slots {loadout.UsedSlots}/{loadout.MaxSlots} | {typeText}";
            }
        }

        private bool UsesAuthoredWireframe => transform.Find("Content") != null;

        private void RefreshMoveButtons()
        {
            if (emptyStateButton != null)
            {
                bool showEmpty = allMoves.Count == 0;
                emptyStateButton.gameObject.SetActive(showEmpty);
                emptyStateButton.interactable = false;
                emptyStateButton.onClick.RemoveAllListeners();
                if (showEmpty)
                    SetButtonText(emptyStateButton, inventory == null ? "Loading move inventory..." : "No owned moves yet.");
            }

            EnsureMoveButtonCount(allMoves.Count);
            ChainlingLoadout loadout = BuildLoadout();
            string viewedChainlingId = entry != null && entry.snapshot != null ? entry.snapshot.backendChainlingId : "";
            for (int i = 0; i < moveButtons.Count; i++)
            {
                Button button = moveButtons[i];
                bool active = i < allMoves.Count;
                button.gameObject.SetActive(active);
                if (!active)
                    continue;

                MenuMoveItem move = allMoves[i];
                bool equippedHere = string.Equals(move.equippedChainlingId, viewedChainlingId, StringComparison.Ordinal);
                bool equippedElsewhere = !string.IsNullOrWhiteSpace(move.equippedChainlingId) && !equippedHere;
                button.onClick.RemoveAllListeners();

                if (equippedHere)
                {
                    LoadoutValidationResult validation = loadout.CanUnequip(move.ToMoveInstance(moveDatabase));
                    string restriction = move.bindSealed
                        ? "Bind sealed"
                        : ((move.escrowLocked || move.locked) ? "PvP escrow" : (validation.isValid ? "Tap to unequip" : validation.message));
                    SetButtonText(button, $"Slot {move.equippedSlot + 1}: {FormatMove(move)}\n{EquippedMark(move)} | {restriction}");
                    button.interactable = !readOnly && !isBusy && !move.bindSealed &&
                        !(move.escrowLocked || move.locked) && validation.isValid;
                    button.onClick.AddListener(() => UnequipMove(move));
                    continue;
                }

                if (equippedElsewhere)
                {
                    SetButtonText(button, $"{FormatMove(move)}\n{EquippedMark(move)}");
                    button.interactable = false;
                    continue;
                }

                LoadoutValidationResult equipValidation = loadout.CanEquip(move.ToMoveInstance(moveDatabase));
                string equipRestriction = move.bindSealed
                    ? "Bind sealed"
                    : ((move.escrowLocked || move.locked) ? "PvP escrow" :
                        (equipValidation.isValid ? $"{FitCue(move)} | Tap to equip" : equipValidation.message));
                SetButtonText(button, $"{FormatMove(move)}\n{equipRestriction}");
                button.interactable = !readOnly && !isBusy && !move.bindSealed &&
                    !(move.escrowLocked || move.locked) && equipValidation.isValid;
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => EquipMove(move));
            }

            if (allMoves.Count == 0 && inventory != null)
                SetStatus("No owned moves yet.");
            else if (equippedMoves.Count == 0 && inventory != null)
                SetStatus("No moves equipped on this Chainling yet.");
            if (readOnly && inventory != null)
                SetStatus("Capture dossier preview - move changes unlock after naming.");
            NavigationChanged?.Invoke();
        }

        private string FormatMove(MenuMoveItem move)
        {
            MoveDefinitionSO def = moveDatabase != null ? moveDatabase.GetById(move.moveId) : null;
            string name = GetMoveName(move);
            string type = def != null ? MoveSelectDisplayUtility.FormatTypeName(def.baseType) : "Unknown";
            string category = def != null ? def.category.ToString() : "Move";
            string lockText = move.bindSealed
                ? " | Bind sealed"
                : ((move.escrowLocked || move.locked) ? " | PvP escrow" : "");
            return $"{name} | {move.tier} | {type} | {category}{lockText}";
        }

        private string EquippedMark(MenuMoveItem move)
        {
            string name = move != null ? move.equippedChainlingName : "";
            return $"Equipped · {(string.IsNullOrWhiteSpace(name) ? "Chainling" : name)}";
        }

        private int CompareMoveRows(MenuMoveItem left, MenuMoveItem right, string viewedChainlingId)
        {
            int leftGroup = MoveRowGroup(left, viewedChainlingId);
            int rightGroup = MoveRowGroup(right, viewedChainlingId);
            if (leftGroup != rightGroup)
                return leftGroup.CompareTo(rightGroup);
            if (leftGroup == 0)
                return left.equippedSlot.CompareTo(right.equippedSlot);
            if (leftGroup == 2)
            {
                int owner = string.Compare(left.equippedChainlingName, right.equippedChainlingName, StringComparison.OrdinalIgnoreCase);
                if (owner != 0) return owner;
            }
            return string.Compare(GetMoveName(left), GetMoveName(right), StringComparison.OrdinalIgnoreCase);
        }

        private static int MoveRowGroup(MenuMoveItem move, string viewedChainlingId)
        {
            if (move != null && string.Equals(move.equippedChainlingId, viewedChainlingId, StringComparison.Ordinal)) return 0;
            if (move == null || string.IsNullOrWhiteSpace(move.equippedChainlingId)) return 1;
            return 2;
        }

        private string FitCue(MenuMoveItem move)
        {
            SpeciesDefinitionSO species = ResolveSpecies();
            MoveDefinitionSO def = moveDatabase != null ? moveDatabase.GetById(move.moveId) : null;
            if (species == null || def == null)
                return "Fit unknown";

            bool entangled = entry != null && entry.snapshot != null && entry.snapshot.isEntangled;
            ChainlingType entangledType = entry != null && entry.snapshot != null
                ? entry.snapshot.entangledType
                : ChainlingType.None;
            string typeCue = SpeciesMoveEquipRules.DescribeFit(
                species,
                entangled,
                entangledType,
                def.moveId,
                def.baseType,
                speciesDatabase);
            string statCue = "";
            if (def.category == MoveCategory.DamagingMelee)
                statCue = species.baseMeleeAtk >= species.baseRangedAtk ? "melee favored" : "melee weaker";
            else if (def.category == MoveCategory.DamagingRanged)
                statCue = species.baseRangedAtk >= species.baseMeleeAtk ? "ranged favored" : "ranged weaker";
            else
                statCue = "utility/passive";

            return $"{typeCue}, {statCue}";
        }

        private string GetMoveName(MenuMoveItem move)
        {
            if (move == null)
                return "Unknown Move";
            MoveDefinitionSO def = moveDatabase != null ? moveDatabase.GetById(move.moveId) : null;
            if (def != null && !string.IsNullOrWhiteSpace(def.displayName))
                return def.displayName;
            return string.IsNullOrWhiteSpace(move.moveId) ? "Unknown Move" : move.moveId;
        }

        private SpeciesDefinitionSO ResolveSpecies()
        {
            if (speciesDatabase == null || entry == null || entry.snapshot == null)
                return null;
            return speciesDatabase.GetById(entry.snapshot.speciesId);
        }

        private void SetBusy(bool busy, string message)
        {
            isBusy = busy;
            if (refreshButton != null)
                refreshButton.interactable = !busy;
            SetStatus(message);
            RefreshMoveButtons();
        }

        private void SetStatus(string value)
        {
            if (statusLabel != null)
                statusLabel.text = value ?? "";
        }

        private void BindAuthoredLoadout()
        {
            if (loadoutRowLabels == null || loadoutRowLabels.Length == 0)
                return;

            var names = new List<string>();
            if (equippedMoves.Count > 0)
            {
                for (int i = 0; i < equippedMoves.Count; i++)
                    names.Add(GetMoveName(equippedMoves[i]));
            }
            else if (entry != null && entry.snapshot != null && entry.snapshot.moveIds != null)
            {
                for (int i = 0; i < entry.snapshot.moveIds.Count; i++)
                {
                    string moveId = entry.snapshot.moveIds[i];
                    if (string.IsNullOrWhiteSpace(moveId))
                        continue;
                    MoveDefinitionSO definition = moveDatabase != null ? moveDatabase.GetById(moveId) : null;
                    names.Add(definition != null && !string.IsNullOrWhiteSpace(definition.displayName)
                        ? definition.displayName
                        : moveId);
                }
            }

            for (int i = 0; i < loadoutRowLabels.Length; i++)
            {
                TMP_Text label = loadoutRowLabels[i];
                if (label == null)
                    continue;
                label.text = i < names.Count && !string.IsNullOrWhiteSpace(names[i])
                    ? $"Slot {i + 1}\n{names[i]}"
                    : $"Slot {i + 1}\nEmpty";
            }
        }

        private void EnsureBuilt()
        {
            if (UsesAuthoredWireframe)
            {
                ResolveAuthoredMoveRows();
                return;
            }

            if (moveRowsRoot != null && moveRowTemplate != null && refreshButton != null)
                return;

            var rootLayout = PlayerMenuUiFactory.VerticalLayout(gameObject, 10f);
            rootLayout.padding = new RectOffset(14, 14, 14, 14);

            var header = PlayerMenuUiFactory.CreateRect("Header", transform);
            PlayerMenuUiFactory.LayoutElement(header.gameObject, preferredHeight: 62f);
            PlayerMenuUiFactory.HorizontalLayout(header.gameObject, 10f);

            var headerText = PlayerMenuUiFactory.CreateRect("HeaderText", header);
            PlayerMenuUiFactory.LayoutElement(headerText.gameObject, flexibleWidth: 1f);
            PlayerMenuUiFactory.VerticalLayout(headerText.gameObject, 2f);
            headingLabel = headingLabel != null
                ? headingLabel
                : PlayerMenuUiFactory.CreateText("Heading", headerText, "Moves", 25f, TextAlignmentOptions.MidlineLeft, false);
            slotSummaryLabel = slotSummaryLabel != null
                ? slotSummaryLabel
                : PlayerMenuUiFactory.CreateText("SlotSummary", headerText, "", 18f, TextAlignmentOptions.MidlineLeft, false);
            slotSummaryLabel.color = PlayerMenuUiFactory.Muted;

            refreshButton = refreshButton != null
                ? refreshButton
                : PlayerMenuUiFactory.CreateButton("RefreshMovesButton", header, "Refresh", PlayerMenuUiFactory.PanelAlt);
            PlayerMenuUiFactory.LayoutElement(refreshButton.gameObject, preferredWidth: 130f, preferredHeight: 54f);

            var columns = PlayerMenuUiFactory.CreateRect("MoveColumns", transform);
            PlayerMenuUiFactory.LayoutElement(columns.gameObject, preferredHeight: 390f, flexibleWidth: 1f);
            var horizontal = PlayerMenuUiFactory.HorizontalLayout(columns.gameObject, 12f);
            horizontal.childForceExpandWidth = true;

            var equippedColumn = CreateColumn("EquippedColumn", columns, "Equipped");
            equippedRoot = PlayerMenuUiFactory.CreateRect("EquippedRoot", equippedColumn);
            PlayerMenuUiFactory.VerticalLayout(equippedRoot.gameObject, 8f);
            PlayerMenuUiFactory.LayoutElement(equippedRoot.gameObject, flexibleWidth: 1f);

            var availableColumn = CreateColumn("AvailableColumn", columns, "Available");
            availableRoot = PlayerMenuUiFactory.CreateRect("AvailableRoot", availableColumn);
            PlayerMenuUiFactory.VerticalLayout(availableRoot.gameObject, 8f);
            PlayerMenuUiFactory.LayoutElement(availableRoot.gameObject, flexibleWidth: 1f);
            moveRowsRoot = availableRoot;
            moveRowTemplate = PlayerMenuUiFactory.CreateButton("RowTemplate", moveRowsRoot, "Move", PlayerMenuUiFactory.PanelAlt);
            PlayerMenuUiFactory.LayoutElement(moveRowTemplate.gameObject, preferredHeight: 72f, flexibleWidth: 1f);
            moveRowTemplate.gameObject.SetActive(false);

            statusLabel = statusLabel != null
                ? statusLabel
                : PlayerMenuUiFactory.CreateText("Status", transform, "", 19f, TextAlignmentOptions.MidlineLeft, true);
            statusLabel.color = PlayerMenuUiFactory.Muted;
            PlayerMenuUiFactory.LayoutElement(statusLabel.gameObject, preferredHeight: 54f);
        }

        private void ResolveAuthoredMoveRows()
        {
            if (moveRowsRoot == null)
                moveRowsRoot = transform.Find("Content/Viewport/Rows") as RectTransform;
            if (moveRowTemplate == null && moveRowsRoot != null)
                moveRowTemplate = moveRowsRoot.Find("RowTemplate")?.GetComponent<Button>();
            if (emptyStateButton == null && moveRowsRoot != null)
                emptyStateButton = moveRowsRoot.Find("EmptyState")?.GetComponent<Button>();
            if (moveRowTemplate != null)
                moveRowTemplate.gameObject.SetActive(false);

            // Old four-slot labels are presentation-only. Hide them rather than
            // emitting a second copy of an equipped move beside the inventory.
            if (moveRowsRoot != null)
            {
                for (int i = 0; i < moveRowsRoot.childCount; i++)
                {
                    Transform child = moveRowsRoot.GetChild(i);
                    if (child == null || (moveRowTemplate != null && child == moveRowTemplate.transform))
                        continue;
                    if (child.name.StartsWith("Slot_", StringComparison.Ordinal) ||
                        child.name.StartsWith("Compatible", StringComparison.Ordinal) ||
                        string.Equals(child.name, "LoadoutHeader", StringComparison.Ordinal))
                        child.gameObject.SetActive(false);
                }
            }
        }

        private static RectTransform CreateColumn(string name, Transform parent, string title)
        {
            RectTransform column = PlayerMenuUiFactory.CreateRect(name, parent);
            PlayerMenuUiFactory.VerticalLayout(column.gameObject, 8f);
            PlayerMenuUiFactory.LayoutElement(column.gameObject, flexibleWidth: 1f);
            TMP_Text label = PlayerMenuUiFactory.CreateText("Title", column, title, 22f, TextAlignmentOptions.MidlineLeft, false);
            PlayerMenuUiFactory.LayoutElement(label.gameObject, preferredHeight: 30f);
            return column;
        }

        private void EnsureMoveButtonCount(int count)
        {
            if (moveRowsRoot == null || moveRowTemplate == null)
                return;

            while (moveButtons.Count < count)
            {
                Button button = Instantiate(moveRowTemplate, moveRowsRoot);
                button.name = $"Move_{moveButtons.Count:D2}";
                button.gameObject.SetActive(true);
                TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
                if (text != null)
                    text.fontSize = 16f;
                moveButtons.Add(button);
            }
        }

        private static void SetButtonText(Button button, string value)
        {
            TMP_Text text = button != null ? button.GetComponentInChildren<TMP_Text>(true) : null;
            if (text != null)
                text.text = value ?? "";
        }

        private IMoveInventoryProvider ResolveMoveInventoryProvider()
        {
            if (moveInventoryProviderBehaviour is IMoveInventoryProvider assigned)
                return assigned;

            MonoBehaviour[] behaviours = FindObjectsOfType<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IMoveInventoryProvider service)
                    return service;
            }

            return null;
        }

        private IMenuMoveEquipService ResolveMoveEquipService()
        {
            if (moveEquipServiceBehaviour is IMenuMoveEquipService assigned)
                return assigned;

            MonoBehaviour[] behaviours = FindObjectsOfType<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IMenuMoveEquipService service)
                    return service;
            }

            return null;
        }
    }
}
