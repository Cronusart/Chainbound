# Chainling HP State

This spec defines persisted per-Chainling HP for PvE: battle-end writes, roster reads, combat hydration, and menu vitals for the Chainlings Party Tab HP bar.

Related: [`CHAINLING_PVE_SERVICE_RECORD.md`](CHAINLING_PVE_SERVICE_RECORD.md) (HP is written in the same battle-outcome RPC but is a separate concern from PvE stat counters).

## Field Semantics

| Storage | Client sentinel | Meaning |
|---------|-----------------|---------|
| `chainlings.current_hp IS NULL` | `ChainlingSnapshot.currentHp == -1` | Full HP (never synced, or reset after rebirth) |
| `0 .. N` | `0 .. N` | Last persisted end-of-battle HP |

- **0 HP** = fainted until healed with Field items (`chainling_reboot` revive; patches do not work on fainted).
- **Clamp on read/hydrate only:** if stored HP exceeds recomputed max (star/rebirth/IV change), clamp to `ChainlingAttributeDisplayUtility.BuildMaxHp`. Do not clamp on write.
- **`hp_synced_at`:** set when `current_hp` is written from a battle outcome or field-item heal RPC.

## Persist Policy (`persist_all`)

After every **PvE** battle end — **win, flee, or defeat** — persist **absolute** end-of-battle HP for all party Chainlings with backend IDs, including **0**.

- Benched party members who never entered combat persist at their end roster slot HP (typically full if untouched).
- **PvP** battles do not write HP (unchanged from PvE service record scope).
- Idempotency uses the same per-chainling battle outcome key as PvE stats. On replay, SQL only updates `current_hp` when the key is present in `p_delta_json` (`has_key` guard).

## Write Path

1. `CombatBattleController.BuildParticipants` captures `endCurrentHp` / `endMaxHp` from each roster slot instance.
2. `SupabaseBattleOutcomePersistence` merges all `playerParticipants` with backend IDs into the outcome payload (`current_hp` field).
3. `profile_apply_pve_battle_outcome` edge function passes `current_hp` in `p_delta_json`.
4. `profile_apply_pve_battle_outcome_tx` sets `current_hp = GREATEST(0, value)` and `hp_synced_at = now()` when the key is present.

## Read Path

- `profile_get_chainlings` and `profile_get_party` (plus legacy `get_player_party`) return `current_hp`.
- `SupabasePlayerRosterProvider` / `SupabasePlayerLineupPartyProvider` map to `ChainlingSnapshot.currentHp` (`NULL` → `-1`).
- Menu: `ChainlingVitalsUtility` + `ChainlingPartyVitalsDisplayUtility` produce `{current} / {max}` for the party tab HP bar.

## Battle Start Hydration

`RosterSlotUtility` applies persisted HP after `CombatFactory.CreateChainling`:

- `-1` / unsynced → full HP (factory default).
- Otherwise → `ChainlingVitalsUtility.ResolveEffectiveCurrentHp` clamped to effective max.

Wild encounters and manual test parties without backend IDs are unchanged.

## Rebirth Reset

`profile_rebirth_chainling_tx` sets `current_hp = NULL` and `hp_synced_at = NULL` (full HP on next read).

## Out of Scope (this slice)

- Phase 1 prefab / layout shell binding
- In-battle items
- Blocking wild encounters when lead Chainling is fainted (optional follow-up)
- PvP HP persistence

## Field item heals (Pass 1)

Menu Field consumables apply via `StubPlayerItemUseService` (harness/offline) and `profile_use_field_item` / `profile_use_field_item_tx` (authority):

| Family | Behavior |
|--------|----------|
| `heal_single` / `status_clear` | Target party member; no consume if heal would be 0; flush status wipe TODO |
| `heal_party` | Small heal to all party members with room; skip fainted / unsynced-full |
| `revive` | Fainted only; HP from `FieldItemConfigSO.reviveHpFraction` (client) / placeholder on server |

Patches update `chainlings.current_hp` and cached roster via `ChainlingRosterHpMutation.PatchEntryHp`.
## UI Cross-Link

Party tab HP bar binds to `ChainlingPartyColumnSummary.vitals`. See [`CHAINLINGS_PARTY_TAB.md`](../UI/CHAINLINGS_PARTY_TAB.md) §4 (Green — HP).
