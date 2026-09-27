# Chainbound Ownership Spine — Phase 1

Status: implemented behind the local/devnet feature gate. Production and Closed Beta remain disabled. The current deployment audit and enablement decision are recorded in [OWNERSHIP_SPINE_PHASE1_DEVNET.md](OWNERSHIP_SPINE_PHASE1_DEVNET.md).

## Canonical ownership model

- **Known** — a distinct, stable instance with zero modifiers. It is authoritative profile/PDA state and is not an NFT. Academy acquisition does not require a wallet.
- **Stamped** — exactly one or two distinct, authority-derived modifiers. It is created immediately as a Metaplex Core asset in the trusted Chainbound Moves collection.
- There is no third move tier. Two modifiers do not create another tier.
- Bind consumes an already-created Stamped move or a cosmetic; it never creates or stamps a move.

## Runtime flow

1. `ownership_roll_known` (profile authority) or `roll_known_move` (PDA result authority) creates a replay-protected Known instance.
2. The result authority creates a `prepare_stamp_ticket` PDA bound to owner, Known instance, nonce/idempotency key, modifier result, generator version, pool hash, and expiry.
3. Unity requests a prepared transaction, shows the Chainbound program, Core program, exact Known instance, RAM cost, output collection, fee estimate, and simulation result.
4. The wallet signs `stamp_known_move` only after simulation passes.
5. The instruction atomically validates ownership/configuration, consumes the exact Known instance and RAM, and creates the Stamped Core asset. Any Core CPI failure rolls the whole transaction back.
6. Unity waits for confirmation, then accepts the asset only after the Supabase/DAS inventory response verifies Core ownership, schema, trusted collection/update authority, and one or two distinct modifiers.

## Migration table

| Previous active surface | Phase 1 equivalent | Treatment |
|---|---|---|
| zero-modifier stored move records | Known | One-way adapters map historical tier values at storage/API boundaries. |
| one/two-modifier move records | Stamped | Both modifier counts map to the same classification. |
| delayed/export-later move NFT | immediate Stamped Core asset | Deleted; confirmation is followed by verified inventory refresh. |
| compressed move mint path and its program/account list | `stamp_known_move` Core CPI | Deleted from active program, IDL, generated client, and Academy UI. |
| third-tier upgrade/reroll services | none | Deleted. Stamped has one or two modifiers. |
| generic upgrade-ticket service | Stamp ticket PDA | Deleted from Unity; ticket is action-specific and authority-issued. |
| active catalyst item and UI requirement | none | Removed from catalog, UI, rewards, favor config, and test fixtures. A disabled database compatibility row remains migration-only. |
| Academy upgrade-only UI | Get Known / Stamp Move | Replaced with the two canonical modes. |
| old move-mint, third-tier, vending, and level-grant Solana services | ownership services plus read-only vault service | Deleted. |

Historical database values are accepted only by `MoveTierMigrationAdapter` and the equivalent API normalization boundary. They are never emitted back into current runtime models.

## Trust and replay boundaries

- `mpl_core::ID` is an address constraint; callers cannot substitute an arbitrary CPI program.
- Collection accounts must be owned by Core, equal configured collection addresses, and use the program authority PDA as update authority.
- The player signs Stamp and must own the profile and MoveVault.
- Result authority is pinned by `GameConfig` for Known rolls and Stamp ticket creation.
- Known-roll receipts, Stamp tickets, and Stamped asset PDAs include owner plus idempotency key.
- Tickets bind generator version and pool hash and expire. Duplicate, stale, wrong-owner, wrong-collection, empty, and repeated modifiers fail closed.
- State changes before the Core CPI are transaction-atomic and therefore roll back if the CPI fails.

## Devnet configuration checklist

The code is safe to enable only when every item below is configured and verified:

- deploy the current `chainbound_game` program and set its exact program ID in Unity/backend configuration;
- create the four trusted Core collections and initialize `OwnershipConfig` with their addresses;
- transfer/verify collection update authority as the program `core_authority` PDA;
- configure `GameConfig.result_authority` and store its signing key only in the server secret store;
- configure generator version, modifier-pool hash, 75 RAM Stamp cost, schema version, ticket lifetime, and asset metadata base URI;
- deploy the authenticated `ownership_roll_known`, `ownership_prepare_stamp`, `ownership_confirm_stamp`, and `ownership_get_inventory` authority endpoints;
- provide a devnet RPC with transaction simulation and a DAS-capable endpoint for verified inventory;
- set Unity's trusted moves collection and trusted Core update authority, then enable both devnet feature toggles;
- fund the result authority and test wallet; initialize profile, MoveVault, GameConfig, and OwnershipConfig PDAs;
- execute the security matrix below before enabling the flag for testers.

## Verification matrix

Required devnet integration coverage: walletless zero-modifier Known roll; one- and two-modifier Stamp success; replay/double-Stamp rejection; wrong owner; expired/stale ticket; insufficient RAM with no consumption; wrong Core program; wrong collection/update authority; Core CPI rollback; client rejection/cancel keeps Known; confirmation followed by verified Core refresh; forged DAS records filtered.

Current external blocker: the repository contains the on-chain state machine, generated contract, Unity transport/orchestration, and verification filters, but devnet IDs, collections, authority secrets, deployed authority endpoints, and live RPC/DAS credentials are environment-owned and must be supplied before an end-to-end devnet transaction can be executed.

## Archived terminology

Older design documents and SQL migration history may contain superseded names because they are immutable historical evidence. They are non-authoritative. This document and the generated IDL define the current Phase 1 model.
