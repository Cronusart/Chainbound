# Player Item Catalog & Inventory Scaffolding v1

**Status:** Pass 1 shipping — Favor rename + field heals + Favor PvE sinks; Bindery/Archive still scaffolding.  
**Date:** 2026-07-24  
**Audience:** Design, economy, UI (Inventory tab), Bindery / Archive Binding, combat (out-of-battle use only for v1)  
**Related:** `ECONOMY_01_EconomyAndProgression_v1.md`, `BINDERY_FOLIO_AND_PRESS_CHARGE_v1.md`, `Docs/Specs/UI/CHAINLING_ARCHIVE_ENCYCLOPEDIA.md`, `Docs/Specs/Combat/MOVE_TIER_PLAYER_FACING_v1.md`, `Docs/Specs/Combat/CHAINLING_HP_STATE.md`

---

## 0. Purpose

Lock a **data-driven item catalog** players will see in the Player Menu **Inventory** tab before drill-down / Bindery UI ships.

This doc defines:

1. **Inventory categories** (UI bag sections).
2. **Seed item list** with stable `itemId`s.
3. **Favor (luck)** — temporary consumable vs stacking party passives (formerly Signal Clarity).
4. **Recovery / rewrite tools** — unbind books, extract moves from books, erase stamps for modifier re-roll.
5. **Implementation seams** (IDs + services; no scene-hardcoded item logic).

Numbers below are **starting levers**, not final balance.

---

## 1. Design principles

- **IDs over one-offs** — every item is a catalog row (`itemId`). UI, shops, drops, and Bindery rites only reference IDs.
- **Categories are presentation** — bag tabs filter `category`; effects live on `effectFamily` / opcodes.
- **RAM remains the regulator** — recovery tools always cost **item + RAM** so they cannot soft-delete the Bindery economy.
- **Cosmetic ≠ combat** — case bindings, mutations, style cosmetics never grant Favor or combat power.
- **PvE-first convenience** — Favor and field patches do not apply in ranked / PvP.
- **Rarity ≠ raw power** — higher-tier orbs/patches buy reliability or convenience, not auto-win capture / heal-to-full for free.
- **Favor ≠ vending quality** — Favor never biases Bindery / Dispenser inscription rarity in baseline play. **Event-only exception:** `VendingConfigSO.eventFavorBoost` / server `get_vending_config` may apply a temporary rarity bias during special events (default `0`; server-gated only). See `Docs/Specs/Combat/LEVER_BOARDS_v1.md`.

---

## 2. Inventory categories (player bag)

Use these **exact category keys** in data and Inventory UI segments:

| Category key | Player label | What belongs here |
|--------------|--------------|-------------------|
| `field` | Field | Out-of-battle heals, revives, party rations |
| `capture` | Capture | Signal Orbs and capture tools |
| `favor` | Favor | Temporary Favor Boost consumables (luck) |
| `bindery` | Bindery | Press Charges, stamp solvents, seal breakers |
| `archive` | Archive | Case bindings, mutation rolls, book/move unbind keys |
| `cosmetic` | Style | Owned cosmetics (covers, auras, column frames) — equip elsewhere; Inventory shows ownership/count |
| `quest` | Quest | Key items / story tokens (optional; hide if empty) |

### UI notes

- Default Inventory open tab: `field` (or last-used).
- Stackable consumables show qty; cosmetics may show `1` owned / equipped state.
- Moves (Known / Stamped / Bound) stay on the **Moves / PC terminal** surface — **not** duplicated as bag items.
- Ordinal folios are move inventory, not bag `field` items.

```text
Player Menu
  Inventory
    Field | Capture | Favor | Bindery | Archive | Style
```

Legacy bag key `clarity` still parses to Favor for older saves.
---

## 3. Effect families (code / data)

Catalog rows declare an `effectFamily`. Runtime resolves via a small opcode set (future `IPlayerItemUseService`).

| effectFamily | Typical categories | Notes |
|--------------|--------------------|--------|
| `heal_single` | field | Heal one Chainling HP |
| `heal_party` | field | Small party-wide heal |
| `revive` | field | Revive fainted |
| `status_clear` | field | Clear common statuses |
| `capture_tool` | capture | Multiplies capture success (SPEC_03 rate model) |
| `favor_boost` | favor | Timed Favor buff (session battles) |
| `press_charge` | bindery | Catalyst for inscription press |
| `stamp_erase` | bindery | Stamped → Known (erase modifier; enables re-press) |
| `modifier_reroll` | bindery | Optional: reroll stamp trait without full erase (premium) |
| `case_binding` | archive | Consumed at Archive Binding |
| `binding_mutation_roll` | archive | Display-only mutation |
| `unbind_bound_move` | archive | Unbind Bound move from Chainling loadout |
| `extract_book_move` | archive | Remove fused / listed move from bound encyclopedia |
| `unbind_archive_book` | archive | Fully reverse Archive Binding (book → dossier) |
| `cosmetic_owned` | cosmetic | Ownership only; equip via Style / Loadout |

---

## 4. Favor (luck)

### 4.1 Player fantasy

**Favor** is soft affinity with the lattice — better encounter weights, capture luck, wild IV kindness, and rare side-drop odds in **PvE**. It is not combat power and not a vending-quality dial. Display titles stay plain Favor; Solana bandwidth/latency winks live in **description** copy only.

### 4.2 Two sources (locked)

| Source | Duration | Stacking | Impact |
|--------|----------|----------|--------|
| **Favor Boost** (item) | Timed (battles) | **No** — refresh duration only; one active boost | **Primary** Favor contribution |
| **Party Favor** (Chainling passive / trait / passive move) | While Chainling is in the **active party** (bench = off) | **Yes — uncapped** across party members | **Secondary** — each grant is **much smaller** than one boost |

### 4.3 Aggregation

```text
favorScore =
    favorFromBoost              // 0 or AmpouleFavorPoints (e.g. 100)
  + sum(partyFavorGrants)       // each grant e.g. 8–12; no hard cap
```

Downstream systems consume `favorScore` through **`FavorConfigSO` soft curves** (diminishing returns on the *application* side):

| Sink | Hook | Pass 1 |
|------|------|--------|
| Encounter weights | `EncounterRollService` | Soft bias toward rarer (lower-weight) rows |
| Capture success | `CaptureRateCalculator` | Small additive bump on `P` |
| Wild IVs | `WildRoller` | Chance to bump lowest IV +1 |
| Rare consumable side drop | `StandardBattleRewardCalculator` | Base ~1% + Favor bonus |
| Duration tick | `CombatPostBattleRouter` | `FavorService.NotifyBattleCompleted()` |
| **Excluded** | Vending / Bindery inscription **quality**, PvP, damage, ranked matchmaking | — |

**Intent:** three Favor-passives in party should still feel weaker than one Favor Boost.

### 4.4 Seed Favor items & passives

| itemId | Name | Category | Effect | Notes |
|--------|------|----------|--------|-------|
| `favor_boost_s` | Favor Boost | favor | `favor_boost` | Short (3 battles); 100 points |
| `favor_boost_m` | Strong Favor | favor | `favor_boost` | Medium (6 battles); 100 points |
| `favor_boost_l` | Peak Favor | favor | `favor_boost` | Long (10 battles); 110 points |

Deprecated legacy ids (`clarity_ampoule_*`) migrate to `favor_boost_*` in SQL and are disabled in catalog.

Party grant scaffolding (data on species trait / passive move, **not** a bag item):

| grantId | Suggested points | Stacks |
|---------|------------------|--------|
| `party_favor_minor` | 8 | Yes, per qualifying party member |
| `party_favor_species` | 12 | Yes |

**Rules**

- Boost active: new boost **refreshes** timer; does not add a second boost layer.
- Party grants stack with each other and with boost.
- No party-grant hard cap; rely on **tiny per-grant values** + **application soft curve**.
- Ranked / PvP: `favorScore` forced to `0`.

---

## 5. Recovery & rewrite tools (Bindery / Archive)

These are the intentional “undo” valves. Always **item + RAM**. Never free.

### 5.1 Critical separations

| Action | Player name | What it undoes | Does **not** do |
|--------|-------------|----------------|-----------------|
| Unbind Bound move (loadout) | **Archive Key** | Bound move tier lock on a Chainling | Does not unpublish NFT book |
| Extract move from book | **Signature Extract** | Fused / listed move on a **bound encyclopedia** | Does not destroy the book |
| Unbind book entirely | **Archive Severance** | Archive Binding → loose dossier again | Does not delete Chainling; cosmetics/cover already consumed stay spent unless economy later refunds |
| Erase pressed stamp | **Stamp Solvent** | Stamped → Known (modifier wiped) | Does not refund Press Charge; re-press costs a new Press Charge + RAM |
| Optional trait reroll | **Seal Breaker** | Reroll Stamped modifier in place | Does not change tier; rarer than Stamp Solvent |

### 5.2 Seed recovery items

| itemId | Name | Category | effectFamily | Typical use |
|--------|------|----------|--------------|-------------|
| `press_charge` | Press Charge | bindery | `press_charge` | Known → Stamped at Bindery |
| `stamp_solvent` | Stamp Solvent | bindery | `stamp_erase` | Erase stamp → Known folio; enables modifier re-roll via new press |
| `seal_breaker` | Seal Breaker | bindery | `modifier_reroll` | Reroll stamp trait without full erase (premium convenience) |
| `archive_key` | Archive Key | archive | `unbind_bound_move` | Unbind Bound move from Chainling (existing encyclopedia term) |
| `signature_extract` | Signature Extract | archive | `extract_book_move` | Remove individual move from bound book metadata / pages |
| `archive_severance` | Archive Severance | archive | `unbind_archive_book` | Fully unbind bound encyclopedia → dossier |
| `case_binding_standard` | Case Binding | archive | `case_binding` | Consumed at Bind to Chain |
| `binding_mutation_roll` | Binding Mutation Roll | archive | `binding_mutation_roll` | Display-only mutation |

### 5.3 Stamp erase → re-roll loop (locked intent)

```text
Known folio
    + Press Charge + RAM  →  Stamped (modifier rolled)
                              │
                              ├─ Stamp Solvent + RAM  →  Known again (modifier gone)
                              │         └─ press again for a new roll
                              │
                              └─ Seal Breaker + RAM   →  Stamped with new modifier (no tier drop)
```

**Anti-patterns**

- Do not refund Press Charge on erase (prevents free infinite presses).
- Do not let Seal Breaker be cheaper than “solvent + press” in the long run without a scarcity faucet.
- Burning / salvage must not mint Press Charges or Solvents in a self-funding loop (`ECONOMY_01` / Bindery playbook).

### 5.4 Book unbind notes

- **Archive Severance** is rare and expensive (item + high RAM). On-chain phase: requires wallet authority / burn-or-reassign ceremony — exact Solana flow deferred; off-chain scaffolding uses profile state first.
- **Signature Extract** updates encyclopedia pages / `signature_bound_move_item_id` (and any listed move cards). May leave book without a signature move until a new Bound move is fused (ceremony or lighter “re-fuse” rite — TBD).
- Cover / mutation consumables already spent are **not** returned by default.

---

## 6. Field & capture seed catalog

### 6.1 Field

| itemId | Name | effectFamily | Notes |
|--------|------|--------------|-------|
| `chainling_patch` | Chainling Patch | `heal_single` | Small heal |
| `chainling_update` | Chainling Update | `heal_single` | Medium |
| `chainling_reformat` | Chainling Reformat | `heal_single` | Full heal (clamped to max HP) |
| `system_flush` | System Flush | `heal_single` + `status_clear` | Heal + clear common statuses |
| `chainling_reboot` | Chainling Reboot | `revive` | Revive fainted |
| `party_patch` | Party Patch | `heal_party` | Small party heal |

### 6.2 Capture

| itemId | Name | effectFamily | Notes |
|--------|------|--------------|-------|
| `signal_orb` | Signal Orb | `capture_tool` | Baseline multiplier `1.0` |
| `lattice_orb` | Lattice Orb | `capture_tool` | Mid multiplier |
| `entangle_orb` | Entangle Orb | `capture_tool` | Soft entangled bias + capture bump |
| `quick_orb` | Quick Orb | `capture_tool` | Slightly better rate; gate: target must be weakened |

Capture tools multiply / bias SPEC_03 probability `P` — they do not replace VRF / server authority.

---

## 7. Cosmetic / Style (inventory ownership)

| itemId pattern | Name | Notes |
|----------------|------|-------|
| `case_binding_*` | Case Binding variants | Ceremony select; consumed on Bind |
| Existing cosmetic catalog IDs | Auras, party columns, HUD cards, etc. | Via `ICosmeticInventoryService`; Inventory Style tab is a viewer |

Partner artist covers remain deferred per `CASE_BINDING_COVER_ART_DROPS_v1.md`.

---

## 8. Catalog row schema (scaffolding)

Future ScriptableObject / Supabase row shape (illustrative):

| Field | Purpose |
|-------|---------|
| `itemId` | Stable string PK |
| `displayName` | Player-facing |
| `description` | Short Inventory detail |
| `category` | Bag tab key (§2) |
| `effectFamily` | Resolver family (§3) |
| `rarity` | Presentation only unless capture/heal tiers need it |
| `maxStack` | Stack size |
| `sellRam` / `buyRam` | Optional market |
| `usableInBattle` | v1 default `false` for all except future capture-in-battle |
| `usableInMenu` | `true` for field / favor / most tools |
| `binderyOnly` | Must be used at Bindery NPC / mode |
| `payloadJson` | Heal amounts, capture mult, favor points, duration |

Unity placement when implemented:

- Data assets → `Assets/Data/**` (prefer `Assets/Data/Economy/Items/` or existing economy folder once created **only if** user asks for folder creation; until then keep catalog doc as source of truth).
- Runtime services → `Assets/Scripts/` under Core or UI domain per assembly rules.

---

## 9. Service seams (systems-first)

Do not hard-code item behavior in Inventory UI controllers.

| Seam | Responsibility |
|------|----------------|
| `IPlayerItemCatalog` | Resolve `itemId` → definition |
| `IPlayerItemInventory` | World-pickup bag grant (`GrantWorldPickupAsync(spawnId)`); consume stays on `IPlayerItemUseService` |
| `IPlayerItemUseService` | Validate + apply `effectFamily` (field heals + Favor Boost in Pass 1) |
| `IFavorService` / `FavorService` | Aggregate boost + party grants → `favorScore`; battle duration tick |
| `FavorConfigSO` / `FieldItemConfigSO` | Tunable Favor curves + revive fraction |
| Existing Bindery / Archive services | Consume `press_charge`, solvents, keys, severance |
| `ICosmeticInventoryService` | Style ownership (already exists) |

Party Favor grants should be emitted from Chainling loadout / passive resolution into `IFavorService`, not from Inventory.

---

## 10. Acquisition sketch (open levers)

| Item family | Candidate faucets |
|-------------|-------------------|
| Field patches / orbs | **World pickups** (`profile_grant_world_pickup` / spawn catalog), shops, battle rare drop (`ECONOMY_01` 1%), quests |
| Favor Boost | Rare drop, weekly quest, small shop stock |
| Press Charge | Bindery playbook faucets (elite drop, mastery side roll, weekly) |
| Stamp Solvent / Seal Breaker | Rare drop, Bindery shop (expensive), event |
| Archive Key / Signature Extract / Severance | Bindery shop, late quests — **never** common battle trash |
| Case bindings | Shop, dispenser adjacent stock, artist drops (later) |

World pickups are a **determined field-item faucet**. Authority is `chainbound_world_pickup_spawns` (Unity sends `spawn_id` only). Unique chests never mint Core collectibles (`ownership_kind != profile_stack` is rejected). Idempotent retry returns `already_claimed` plus the current stack. Scene presence still uses `world.pickup.*` flags; SQL claims are the bag fact. Link 1 backpack: `world.pickup.link1.backpack_01` → `chainling_patch` × 1.

---

## 11. Inventory tab MVP scope

**Phase A — scaffolding (this doc + 2026-07-22 impl)**  
Catalog IDs, categories, Favor rules, recovery tool names.

**Phase B — Inventory UI + service seams (in progress / landed stub)**  
- `InventoryScreen` category tabs + stack list  
- `IInventoryService` stacks, harness grants, `StubInventoryService` seed  
- `CoreBagItemInventoryService` → `profile_get_item_inventory`  
- `ICoreAssetConsumeService` prepare/finalize → Supabase consume intents  
- Pipeline doc: `Docs/Specs/Solana/CORE_BAG_ITEM_INVENTORY_PIPELINE_v1.md`

**Phase C — Use flows**  
Menu use for patches/Favor (Pass 1); Bindery modes for press / solvent / keys / severance (Pass 2); Favor HUD pip optional.

**Out of scope for first Inventory pass**  
In-battle bag, live Core burn tx bytes, partner cover verification, DAS refresh for bag Core (cache table ready).

---

## 12. Open tuning levers

- Favor Boost points vs per-party grant points (keep grant ≪ boost).
- Soft curve knee for encounter / capture / IV / rare-drop (`FavorConfigSO`).
- RAM costs for Solvent vs Seal Breaker vs Severance.
- Whether Signature Extract requires the book to stay Archived with empty signature slot.
- Whether Severance burns NFT or soft-archives off-chain first.

---

## Changelog

| Date | Change |
|------|--------|
| 2026-07-22 | Initial scaffolding: categories, seed catalog, Clarity (ampoule primary; party stacks uncapped but tiny), Stamp Solvent, Signature Extract, Archive Severance. |
| 2026-07-22 | Implementation groundwork: Unity inventory contracts, InventoryScreen, harness grants, Supabase item tables + Core consume intents. |
| 2026-07-24 | Pass 1: Clarity → Favor Boost rename; field heal/revive use + target picker; Favor sinks (encounter/capture/IV/rare drop) + post-battle tick; `profile_use_field_item` RPC. |
| 2026-09-01 | World pickup bag grant: `IPlayerItemInventory.GrantWorldPickupAsync`, `profile_grant_world_pickup`, spawn catalog + claims. Link 1 backpack seeds `chainling_patch` × 1. |
