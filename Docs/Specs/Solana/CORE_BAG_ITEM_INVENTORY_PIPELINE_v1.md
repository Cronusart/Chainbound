# Core Bag Item Inventory Pipeline v1

**Status:** Scaffolding — Unity + Supabase seams live; Metaplex Core burn tx builder deferred.  
**Date:** 2026-07-25  
**Related:** `COSMETIC_CORE_NFT_ADAPTABILITY_SPEC.md`, `Docs/Specs/Economy/PLAYER_ITEM_CATALOG_AND_INVENTORY_v1.md`, `CHAINBOUND_SOLANA_ASSET_TAXONOMY.md`, `Docs/Guides/PRODUCTION_CUTOVER_AND_HARNESS_DISENGAGE.md`

---

## 0. Authority model

| Layer | Authority |
|-------|-----------|
| Profile stack items (patches, orbs, Press Charges, solvents) | Supabase `chainbound_profile_item_stacks` |
| Core collectible bag items (case bindings, Archive Key, Signature Extract, Severance) | Wallet ownership of Metaplex Core under trusted collection + server DAS verify |
| Unity client | Cache + display + wallet sign only — **never** authority for paid/tradable consume |

Matches Solana Unity skill: Supabase Edge verifies Core; Unity uses Solana.Unity-SDK for signing via `IChainTxOrchestrator`.

---

## 1. Read path

```text
InventoryScreen
  → IInventoryService.FetchInventoryAsync
      ├─ Stub / ChainlingsMenuTestHarness (sandbox)
      └─ CoreBagItemInventoryService
           → POST profile_get_item_inventory
                → load profile stacks (guest-safe; no wallet required)
                → if a verified wallet is linked, also attach Core ownership-cache stacks
                → return stacks[] + ram_balance
```

Core DAS refresh for bag items will mirror `onchain_cosmetics.ts` (`cb.kind` / `cb.item_id`) — cache table already exists; DAS refresh helper can be added next to cosmetics without changing Unity contracts.

---

## 2. Consume / burn path (Core collectibles)

```text
IPlayerItemUseService / Bindery rite
  → ICoreAssetConsumeService.PrepareConsumeAsync
       → POST profile_prepare_core_item_consume
            → verify wallet + ownership cache row
            → insert consume_intent
            → return unsigned_tx_base64 (empty until burn builder ships)
  → if unsigned bytes present:
       IChainTxOrchestrator.SubmitAsync (Solana Unity wallet sign + send)
  → ICoreAssetConsumeService.FinalizeConsumeAsync
       → POST profile_finalize_core_item_consume
            → mark intent finalized
            → remove ownership cache row
            → apply game effect (unbind / severance / etc. — Bindery services)
```

**UX requirement (when unsigned tx is live):** show cluster, fee payer, program, action summary, simulation, confirmation — before/after sign.

---

## 3. Core metadata (`cb.*`) for bag collectibles

| Attribute | Purpose |
|-----------|---------|
| `cb.kind` | `case_binding` / `archive_key` / `signature_extract` / `archive_severance` / `binding_mutation` |
| `cb.item_id` | Stable catalog id (`case_binding_standard`, …) |
| `cb.schema_version` | Metadata schema |
| `cb.rarity` | Optional display |

Trusted collection env (proposed): `CHAINBOUND_CORE_ITEMS_COLLECTION` (separate from cosmetics). Until set, prepare still validates against ownership cache rows written by future DAS refresh / admin tooling.

---

## 4. Unity types

| Type | Assembly | Role |
|------|----------|------|
| `IInventoryService` / `InventorySnapshot.stacks` | Combat.Runtime | Bag read model |
| `IInventoryItemProvenanceService` | Combat.Runtime | item_id → asset_id |
| `ICoreAssetConsumeService` / `CoreAssetConsumeService` | Combat.Runtime (Solana/) | prepare → sign → finalize |
| `CoreBagItemInventoryService` | Combat.Runtime | Supabase bag adapter |
| `SupabasePlayerItemUseService` | Combat.Runtime | Production Use → `profile_use_field_item` |
| `StubPlayerItemUseService` | Combat.Runtime | Harness / sandbox only |
| `InventoryScreen` | MenuNavigation | Category UI (wires production vs harness) |

---

## 5. Supabase artifacts

| Artifact | Path |
|----------|------|
| Migration (catalog + stacks) | `Supabase/migrations/20260722030000_chainbound_player_item_inventory.sql` |
| Migration (Favor + field use RPC) | `Supabase/migrations/20260724090000_favor_boost_and_field_item_use.sql` |
| Migration (world pickup grant) | `Supabase/migrations/20260901010000_world_pickup_grant.sql` |
| Shared helpers | `Supabase/functions/_shared/onchain_items.ts` |
| Read | `profile_get_item_inventory` |
| Field / Favor use | `profile_use_field_item` → `profile_use_field_item_tx` |
| World pickup grant | `profile_grant_world_pickup` → `profile_grant_world_pickup_tx` (spawn catalog; `spawn_id` only) |
| Prepare burn | `profile_prepare_core_item_consume` |
| Finalize burn | `profile_finalize_core_item_consume` |

---

## 6. Next engineering slices

1. DAS refresh for Core bag items (clone cosmetics paging + `cb.item_id` catalog join).
2. Build Metaplex Core burn unsigned tx (update authority / collection policy) into prepare response.
3. Wire Bindery rites to `ICoreAssetConsumeService` for Severance / Extract / Key.
4. Repair `supabase migration list` history so local files match remote (several migrations were applied via SQL Editor / `db query`).

---

## Changelog

| Date | Change |
|------|--------|
| 2026-07-22 | Initial pipeline scaffolding (Unity + Supabase + Inventory UI). |
| 2026-07-24 | Production `SupabasePlayerItemUseService`; stub harness-only; Favor + `profile_use_field_item` deployed. |
| 2026-09-01 | World pickup bag grant: spawn catalog + claims, `profile_grant_world_pickup`, `CoreBagWorldPickupGrantService`. |
| 2026-09-04 | `profile_get_item_inventory` returns profile stacks without a verified wallet; Core collectibles still require Bind to Chain. |
