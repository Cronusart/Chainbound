# Chainling Archive Encyclopedia and Bind to Chain

**Status:** Canonical ownership-spine design  
**Ownership spine lock:** 2026-09-15  
**Applies to:** Party/archive UI, Bindery, Core archive metadata, covers, and cosmetic skins

> **Superseded by Ownership Spine 2026-09-15.** Earlier requirements for a `Bound move`, a Press Charge, Cycle progress, or an off-chain cover as a Bind gate are superseded. **Bound** now means the published Chainling archive book only.

## 0. Player model

A captured Chainling starts as a non-NFT dossier. **Bind to Chain** publishes that dossier as a wallet-owned **Bound encyclopedia**.

```text
Captured Chainling dossier (profile/PDA)
  + Book cover (Core)
  + player's choice: Stamped move (Core) OR cosmetic skin (Core)
  + RAM
  + owner wallet signature
        |
        v
Bound Chainling encyclopedia (Core archive asset)
```

The Bindery performs this Chainling-publish rite. It does not roll or Stamp moves.

## 1. Terms

| Term | Meaning |
|---|---|
| **Archive Dossier** | Unbound, non-NFT Chainling detail record. |
| **Bind to Chain / Archive Binding** | Atomic recipe that consumes the selected Core components and publishes the Chainling. |
| **Bound Chainling / Bound encyclopedia** | The resulting Chainling archive Core asset and its book presentation. |
| **Book cover** | Required Core consumable from the trusted covers collection. |
| **Stamped move** | One- or two-trait Core move; one valid optional recipe branch. |
| **Cosmetic skin** | Additive Core visual skin; the other valid optional recipe branch. |
| **Base skin** | Capture-time visual variant stored on the Chainling. It is never replaced by proof of a cosmetic asset. |

Do not use `Bound move`, `Inscribed`, `Sealed`, `Rune`, `cNFT`, or `Bubblegum` in player copy.

## 2. Exact Bind recipe

Every successful Bind requires all of the following:

1. **Chainling:** owned non-NFT Chainling account/profile record, not already Bound.
2. **Book cover:** owned Core asset in the configured trusted covers collection.
3. **Chosen Core component:** exactly one owned **Stamped move** from the trusted moves collection **or** one owned **cosmetic skin** from the trusted cosmetics collection.
4. **RAM:** configured Bind cost available on the owner profile.
5. **Wallet:** the Chainling owner signs and pays the transaction.

There is no Press Charge and no move named Bound in this recipe.

### On-chain transaction behavior

- Validate owner, account type, and exact collection membership for both input Core assets.
- Reject a missing cover, missing chosen component, wrong collection, duplicate component account, already-Bound Chainling, or insufficient RAM.
- Debit RAM, burn/consume the cover, burn/consume the chosen Stamped/cosmetic asset, create the archive Core asset, and mark the Chainling Bound in one transaction.
- Any CPI failure rolls the entire operation back.
- The archive asset belongs to the signing wallet and is created under the trusted `Chainbound Archives` collection.
- An idempotency receipt prevents a retry from consuming another component or creating another book.

## 3. Dossier and encyclopedia UI

### Unbound: Archive Dossier

- Show origin, capture identity, base skin, mutation/entangled state, stats, service record, and Known moves.
- Show **Bind to Chain** when the exact recipe can be assembled.
- Gate checklist copy:
  - `Choose a Chainling.`
  - `Choose a book cover.`
  - `Choose a Stamped move or cosmetic skin.`
  - `Need {RAM} RAM.`
  - `Connect wallet to Bind.`

### Bound: Encyclopedia

- Show the selected book cover on the 3D book in `Docs/Specs/UI/BOUND_ENCYCLOPEDIA_BOOK_PRESENTATION_v1.md`.
- Show the Chainling's captured base visual identity even when no cosmetic is equipped.
- Removing or transferring an equipped cosmetic reveals the base skin; it never produces a blank/no-skin state.
- Remaining Known moves may be listed as archive pages, but they stay non-NFT and are not individually tradable.

## 4. Bindery flow

```text
1. Select eligible Chainling
2. Select owned book cover
3. Select one owned Stamped move OR cosmetic skin
4. Review RAM cost and both consumed Core assets
5. Review cluster, fee payer, program, collection outputs, and estimated fees
6. Simulate transaction
7. Owner wallet signs
8. Wait for confirmed/finalized result
9. Refresh Chainling state and verified archive Core asset
10. Reveal Bound encyclopedia
```

The UI must never imply that merely activating a scene object, owning an off-chain cover row, or having a two-trait move is sufficient.

## 5. Capture data copied into archive metadata

The Bind instruction builds the archive snapshot from the authoritative Chainling record, not from client-submitted display strings.

Required `cb.*` attributes:

- `cb.kind=chainling_archive`
- `cb.schema_version`
- `cb.chainling_id`
- `cb.species_id`
- `cb.ivs` and/or `cb.ivs_seed`
- `cb.entangled`
- `cb.entangled_type`
- `cb.base_visual_variant_id`
- `cb.mutation_ids`
- `cb.known_move_ids` (optional list/snapshot of remaining Known moves)
- `cb.book_cover_asset`
- `cb.bind_component_kind=stamped_move|cosmetic`
- `cb.bind_component_asset`

Gameplay-critical live data remains in the Chainling/profile authority after publish. Archive metadata is the immutable/auditable publish snapshot unless a later version explicitly supports controlled updates.

## 6. Cosmetic rules

- Base skin is always a Chainling field established at capture.
- Cosmetic skins are additive wallet assets verified against the trusted cosmetics collection.
- Equipped-cosmetic metadata may point to the chosen asset, but must not overwrite `cb.base_visual_variant_id`.
- Covers and cosmetic skins grant no combat stats.

## 7. Authority and verification

| Data | Authority |
|---|---|
| Unbound Chainling and Known moves | Profile/backend or Chainbound PDA |
| Cover, Stamped move, cosmetic, Bound book | Metaplex Core account plus trusted collection |
| Bind recipe and RAM debit | Chainbound program transaction |
| Unity view | Cache; refreshed after confirmation |

Unity must fail closed when collection IDs are missing. DAS may support display/indexing, but ownership and collection checks must be verified by the program for assets consumed by Bind.

## 8. Acceptance criteria

- [ ] Bind fails when the book cover account is absent.
- [ ] Bind fails when the Stamped/cosmetic component account is absent.
- [ ] Bind fails for a cover or component from the wrong collection.
- [ ] Bind succeeds only with Chainling + cover + exactly one valid component + RAM + owner signature.
- [ ] Successful Bind consumes both selected Core inputs and creates one archive Core asset atomically.
- [ ] Metadata contains the capture-time base visual variant, mutation/entangled fields, and optional remaining Known list.
- [ ] Bound labels only Chainling books.
- [ ] No Press Charge appears in the flow.

## 9. Decision log

| Date | Decision |
|---|---|
| 2026-07-09 | Earlier dossier/book concept and Bound-move gate recorded. |
| **2026-09-15** | **Ownership Spine removes the Bound-move, Cycle, and Press Charge gates.** |
| **2026-09-15** | **Exact recipe locked: Chainling + Core cover + (Stamped Core move or cosmetic Core skin) + RAM + wallet.** |
| **2026-09-15** | **Capture-time base skin is copied into archive metadata; cosmetics remain additive.** |
