# Chainbound Solana Asset Taxonomy

**Status:** Canonical for new implementation  
**Ownership spine lock:** 2026-09-15

> **Superseded by Ownership Spine 2026-09-15.** The previous Ordinal/PDA -> Inscription/cNFT -> Rune/Core taxonomy and every active Bubblegum mint path are retired. Legacy code may remain quarantined for migration reads only; it must never be selected for new Stamp or Bind work.

## Canonical asset layers

| Player term | Kind | Standard / storage | Wallet needed |
|---|---|---|---|
| **Known move** | zero-modifier move instance | Profile/backend or Chainbound PDA; non-NFT | No |
| **Unbound Chainling** | captured creature record | Profile/backend or Chainbound PDA; non-NFT | No |
| **Stamped move** | one- or two-modifier tradable move | Metaplex Core, trusted `Chainbound Moves` collection | Yes |
| **Book cover** | Bind consumable | Metaplex Core, trusted covers collection | Yes |
| **Cosmetic skin** | additive visual collectible / Bind component | Metaplex Core, trusted `Chainbound Cosmetics` collection | Yes |
| **Bound Chainling** | published archive book | Metaplex Core, trusted `Chainbound Archives` collection | Yes |

Internal storage may retain `ordinal`, `inscription`, or `rune` migration values. Presentation maps `ordinal` to Known and both modifier-bearing legacy values to Stamped. Bound is never a move tier.

## Program IDs and standards

- Metaplex Core program: `CoREENxT6tW1HoK8ypY1SxRMZTcVPm7R94rH4PZNhX7d`.
- New move and archive assets use Core single-account assets and trusted Core collections.
- No new move mint uses Bubblegum, Merkle trees, cNFT proofs, or Bubblegum DAS proof builders.
- DAS is an indexing/read surface. The program validates Core account ownership and exact collection membership for consumed assets.

## Stamped move attributes

Required attributes:

- `cb.kind=stamped_move`
- `cb.schema_version`
- `cb.move_id`
- `cb.move_tier=stamped`
- `cb.modifier_count=1|2`
- `cb.modifier_1`
- `cb.modifier_2` (empty/omitted when count is one)
- `cb.known_instance_id`
- `cb.rarity`

Stamp is an immediate Core create CPI. The same transaction debits RAM and removes the Known instance. If Core creation fails, all state changes roll back.

## Cosmetic Core attributes

- `cb.kind=cosmetic`
- `cb.cosmetic_id`
- `cb.slot`
- `cb.schema_version`
- `cb.rarity` (optional)
- `cb.species_scope` (optional)
- `cb.visual_variant_scope` (optional)
- `cb.asset_rev` (optional)

Only server/program verification converts wallet ownership into equip eligibility. Unity caches but is never the ownership authority.

## Book-cover Core attributes

- `cb.kind=book_cover`
- `cb.cover_id`
- `cb.schema_version`
- `cb.artist_id` / provenance fields when applicable
- `cb.rarity` (optional)

Collection membership is the hard on-chain gate. Creator or free-form metadata alone is insufficient.

## Bound archive attributes

- `cb.kind=chainling_archive`
- `cb.schema_version`
- `cb.chainling_id`
- `cb.species_id`
- `cb.ivs` and/or `cb.ivs_seed`
- `cb.entangled` and `cb.entangled_type`
- `cb.base_visual_variant_id`
- `cb.mutation_ids`
- `cb.known_move_ids` (optional snapshot; still non-tradable)
- `cb.book_cover_asset`
- `cb.bind_component_kind=stamped_move|cosmetic`
- `cb.bind_component_asset`

The equipped cosmetic reference is additive. It never replaces the capture-time base visual variant.

## Atomicity and replay protection

- Stamp uses a result-authority-prepared ticket tied to owner, Known instance, result, expiry, and idempotency key.
- Owner wallet finalization consumes the ticket and Known instance while creating the Core asset.
- A consumed Known ID or consumed ticket cannot be reused.
- Bind validates owner and trusted collections, burns both selected Core components, debits RAM, and creates the archive asset in one transaction.
- Asset addresses/receipts are deterministic or otherwise idempotently bound to the operation key.

## Burn and salvage boundary

- Core assets: on-chain burn/consume, then bounded RAM credit in the same program flow.
- Known moves and unbound Chainlings: profile/PDA salvage, never an NFT burn.
- Returns must remain below acquisition/Stamp costs so no self-funding loop exists.

## Legacy quarantine

The following are migration-only and fail closed for new operations:

- `mint_inscription_cnft`;
- Bubblegum-targeted `VendingMachineService` transaction builders;
- `MoveMintService` cNFT mint flows;
- `RuneMintService` as a second player tier;
- legacy Core Inscription surfaces that do not implement the atomic Ownership Spine Stamp.

## Acceptance criteria

- [ ] Every new tradable move asset is Core and player-facing Stamped.
- [ ] Known and captured Chainlings remain usable without a wallet.
- [ ] Stamp consumes Known and immediately creates Core.
- [ ] Bind creates only a Chainling archive and requires the exact recipe.
- [ ] No new Bubblegum/cNFT mint surface exists.
