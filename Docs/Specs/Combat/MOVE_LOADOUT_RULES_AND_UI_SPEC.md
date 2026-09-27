# Move Loadout Rules And UI Spec

**Status:** Canonical working spec  
**Applies to:** combat runtime, Chainlings menu, Supabase loadout validation, and on-chain loadout hydration  
**Supersedes:** star-based `6 + 2 * stars` slot rules, evolution-based slot growth, fixed four-move battle hydration, and party Moves view empty-slot placeholder rows.

Player-facing tier labels (**Known / Stamped / Bound**) and Archive Binding: `Docs/Specs/Combat/MOVE_TIER_PLAYER_FACING_v1.md`, `Docs/Specs/UI/CHAINLING_ARCHIVE_ENCYCLOPEDIA.md`.

## 1. Canonical Terms

| Term | Meaning |
|------|---------|
| Max slot cap | Maximum used slot budget for a Chainling's equipped moves. |
| Equipped count | Number of non-empty equipped move entries. |
| Used slot budget | Sum of each equipped move's slot cost; base move cost is 1, with existing AUG/TRX modifier slot costs applied on top. |
| Passive count | Number of equipped moves with `MoveCategory.Passive`. |
| Rune count | Number of equipped moves with `MoveTier.Rune` (player UI: **Bound** moves). |

## 2. Slot Capacity

Move capacity is rebirth-based:

| Rebirth count | Max move slots |
|---------------|----------------|
| 0 | 4 |
| 1 | 5 |
| 2 | 6 |
| 3 | 7 |
| 4 | 8 |
| 5 | 9 |

Formula:

```text
maxSlots = min(9, 4 + clamp(rebirthCount, 0, 5))
```

Stars and evolution stage do not drive move slot growth.

## 3. Loadout Caps

- Passive moves: max 2 equipped per Chainling at all rebirth levels.
- Passive moves still consume slot budget.
- Rune-tier moves: max 1 equipped per Chainling from rebirth 0-4 (player UI: **Bound** moves).
- Rune-tier moves: max 2 equipped per Chainling at rebirth 5.
- Rune cap applies globally to loadout validation, PvE, PvP, and ranked. Ranked may still reject Ordinal-tier moves separately.
- Duplicate moves remain disallowed in current runtime and server flows. A future duplicate policy must update this spec before code changes.
- Mythic per-team and Mythic passive per-team caps are retired for current runtime validation. Rarity is not a global equip cap.

## 4. Storage And Authority

Runtime and server validation use the rebirth-based max slot cap. The on-chain equipped registry keeps `EQUIPPED_SLOTS_ON_CHAIN = 16` as padded storage for compatibility. Current code must:

- Allow writes only into `0..maxSlots-1`.
- Ignore hydrated on-chain slots at or above `maxSlots`.
- Return compact equipped lists to UI and combat, not placeholder rows.
- Keep modifier ID arrays aligned to the compact equipped list.

## 5. Source Of Truth Matrix

| Context | Equipped move source |
|---------|----------------------|
| Offline/dev/manual party | Snapshot `moveIds` from local provider. |
| Supabase-backed menu | Supabase loadout rows validated by `profile_set_loadout`. |
| Wallet/on-chain battle hydration | On-chain registry slots, filtered by rebirth cap and mapped through local move database. |
| Party UI | Supabase/on-chain hydrated snapshot compact `moveIds`. |
| Combat | Hydrated snapshot compact `moveIds`, capped by `MoveLoadoutSlotRules`. |

The target architecture remains hybrid: Supabase owns profile/equipped-state read models and server validation, while on-chain registries can mirror or hydrate authoritative move ownership where available.

## 6. Party Moves View UI

- Header: `{equippedCount}/{maxSlots} moves equipped`.
- Rows: equipped moves only; no empty slot placeholder rows.
- Zero equipped: one muted empty-state row.
- If equipped count is 4 or fewer, the list does not scroll.
- If equipped count is greater than 4, the view exposes a masked viewport sized for four rows and enables vertical scrolling/content offset.
- Party tab prefabs should author 4 configurable template rows (`MoveRow_01..MoveRow_04`) inside `MoveRowsViewport/MoveRows`.
- Rows 5-9 are runtime clones from the authored row template when the equipped list needs them, then clipped by the four-row viewport.
- Moves view scroll position resets to the top whenever the Moves view is rebound, including live preview/view changes.

## 7. Combat Move Select UI

- The combat move select list shows a four-row window.
- If the move list plus Rest row exceeds four rows, navigation scrolls the list so the selected row remains visible.
- Rest remains appended by the coordinator and participates in list scrolling.

## 8. Validation Checklist

- `ResolveMaxMoveSlots`: rebirth 0 => 4, rebirth 5 => 9, values above 5 clamp to 9.
- `ResolveMaxRuneSlots`: rebirth 0-4 => 1, rebirth 5 => 2.
- Third passive move is rejected at all rebirth levels.
- Second Rune is rejected before rebirth 5 and allowed at rebirth 5.
- Combat hydration keeps 5-9 moves when rebirth allows them.
- Party Moves view binds compact equipped rows and shows scroll only above four rows.
- Combat move selection scrolls selected index into view when more than four rows exist.
