> **Parked for review.** Probably scrap. June 2026 findings only. Canonical asset model is `Docs/Specs/Solana/CHAINBOUND_SOLANA_ASSET_TAXONOMY.md`. Review list: [../../README.md](../../README.md).

# Cosmetic Core NFT Adaptability Spec Sheet

Status: findings and recommendations only. No implementation changes in this pass.
Date: 2026-06-25
Scope: Chainbound cosmetic item ownership, future party column variants, Unity Solana SDK integration, and Metaplex Core readiness.

## Executive Verdict

Chainbound is not purely in a stub-service stage. The project has meaningful Solana groundwork in place: a vendored Solana Unity SDK package, wallet connection services, RPC services, a transaction orchestrator, PDA bootstrap flow, and service interfaces that can support blockchain-backed ownership.

However, cosmetic items specifically are still effectively at the stub/off-chain ownership stage. The current cosmetic UI and Supabase visual identity flow already use stable cosmetic IDs and an `ICosmeticInventoryService` seam, which is the right shape for future NFT ownership checks. What is missing is the production adapter that reads wallet-owned Metaplex Core assets, validates them, maps them to those stable cosmetic IDs, and returns them to the existing style UI.

> **Related (deferred):** Partner / first-party **case binding cover** art eligibility is specified in `Docs/Specs/Solana/CASE_BINDING_COVER_ART_DROPS_v1.md`. Do not implement cover-wallet verification until Archive Binding scaffolding exists.

Metaplex Core is technically a sound target for cosmetics, especially for editable game collectibles. Core assets can use mutable metadata controlled by an update authority, delegated update authority through plugins, and on-chain attribute key/value pairs that are useful for game-readable cosmetic identity. The main risk is not the standard. The main risk is that the repo currently has more Unity-side Solana scaffolding than backend/on-chain implementation.

## Compass Used

- `solana-agent-skills`: used as the routing and workflow compass for Solana task analysis.
- `solana-unity`: the project skill path listed in `AGENTS.md` was not present in the repo, so the analysis used the vendored Solana Unity SDK, wallet setup docs, and existing Unity Solana code as the Unity-specific compass.
- Global `solana-dev`: used as a secondary Solana engineering compass for signing, simulation, RPC, generated client, and account validation expectations.

## Current Groundwork

### Solana Unity SDK

The repo vendors the Solana Unity SDK under:

- `Packages/Solana.Unity-SDK-main`

The SDK README advertises wallet, RPC, NFT, transaction encoding/decoding, Phantom/Web3Auth, Solana Mobile Stack, and Wallet Adapter support. This is a good foundation for player wallet connection and transaction signing inside Unity.

Relevant local doc:

- `Docs/Guides/WALLET_CONNECTION_SETUP.md`

That guide describes a real wallet flow with `WalletController`, `MainMenuWalletServiceSolana`, `MainMenuProfileServiceSolana`, and `SolanaConfigSO.useStubServices`.

### Runtime Solana Service Layer

The main composition point is:

- `Assets/Scripts/Solana/Services/SolanaServiceBridge.cs`

This service bridge is not just placeholder code. When `useStubServices` is disabled, it builds a real graph of Solana-facing services:

- `SolanaWalletService`
- `SolanaRpcService`
- `SolanaChainTxOrchestrator`
- `ProfileService`
- `VrfService`
- `MoveVaultService`
- `ChainlingVaultService`
- `EquippedMoveRegistryService`
- `UpgradeTicketService`
- `MoveMintService`
- `RuneMintService`
- `CoreMoveService`
- `CaptureService`
- `VendingMachineService`
- `PdaBootstrapService`

This means the project already has an architectural place to add a production cosmetic Core inventory adapter.

### Wallet And RPC

Relevant files:

- `Assets/Scripts/Solana/Wallet/IWalletService.cs`
- `Assets/Scripts/Solana/Wallet/SolanaWalletService.cs`
- `Assets/Scripts/Solana/Rpc/IRpcService.cs`
- `Assets/Scripts/Solana/Rpc/SolanaRpcService.cs`
- `Assets/Scripts/Solana/Programs/SolanaChainTxOrchestrator.cs`

The wallet service wraps Solana Unity SDK `Web3` login/signing flows. The RPC service wraps Solana RPC clients, sends transactions with preflight settings, confirms signatures, reads account data, and caches recent blockhashes. The transaction orchestrator signs and submits unsigned transaction bytes, confirms them, and caches pending signatures for resume.

This is usable groundwork. The notable gap is that the orchestrator does not currently show an explicit transaction simulation step before wallet signing, which is recommended for higher-confidence user transaction UX.

### Existing Cosmetic Architecture

Relevant files:

- `Assets/Scripts/Combat/Runtime/Data/ChainlingVisualIdentity.cs`
- `Assets/Scripts/Combat/Runtime/ScriptableObjects/ChainlingVisualIdentityDatabaseSO.cs`
- `Assets/Scripts/Combat/Runtime/ScriptableObjects/ChainlingVisualDefinitionSO.cs`
- `Assets/Scripts/UI/MenuNavigation/Chainlings/ChainlingStylePanel.cs`
- `Assets/Scripts/UI/MenuNavigation/Data/PlayerMenuDataContracts.cs`
- `Assets/Scripts/UI/MenuNavigation/Services/SupabaseMenuVisualIdentityService.cs`
- `Assets/Scripts/UI/MenuNavigation/Services/SupabaseVisualIdentityMutation.cs`

The cosmetic system is already close to NFT-friendly from a data modeling perspective:

- Cosmetics use stable string IDs.
- The style panel locks options based on `ICosmeticInventoryService.FetchOwnedCosmeticIdsAsync`.
- Supabase visual identity save paths already validate requested IDs against owned cosmetic IDs.
- Free/default cosmetic IDs are handled explicitly.
- Visual definitions support species and visual variant compatibility.

That means NFT ownership can be introduced behind the existing inventory service interface without rewriting the style UI.

### Current Config

Relevant file:

- `Assets/Data/Solana/SolanaConfig.asset`

Notable values:

- RPC endpoint: `https://api.devnet.solana.com`
- WebSocket endpoint: `wss://api.devnet.solana.com`
- Metaplex Core program ID: `CoREENxT6tW1HoK8ypY1SxRMZTcVPm7R94rH4PZNhX7d`
- `useStubServices: 0`

The configured Metaplex Core program ID matches the official Metaplex Core program ID for devnet/mainnet. That is a positive sign.

## Current Stub Or Incomplete Areas

### Cosmetic Ownership Is Still Stubbed

The interface exists:

- `ICosmeticInventoryService`

But the production wallet-owned Core asset adapter does not appear to exist yet. The concrete service currently found is:

- `StubCosmeticInventoryService`

The style UI can already consume owned cosmetic IDs, but those IDs are not yet derived from wallet-owned Core assets.

### No Cosmetic Core Asset Service Yet

There is no dedicated service currently responsible for:

- Fetching wallet-owned Metaplex Core cosmetic assets.
- Filtering by the official Chainbound cosmetic collection.
- Validating update authority, collection, or verified provenance.
- Reading Core Attributes or metadata URI JSON.
- Mapping a Core asset to a cosmetic ID.
- Returning those IDs through `ICosmeticInventoryService`.
- Minting/updating cosmetic Core assets.

The existing `CoreMoveService` and `RuneMintService` prove the project has explored Core-style flows for moves/runes, but cosmetics do not yet have an equivalent production path.

### Core Read Path Is Not Implemented For Existing Core Move Service

Relevant file:

- `Assets/Scripts/Solana/Programs/CoreMoveService.cs`

This service contains a useful warning sign for future cosmetics. Its chain refresh path is currently marked as an MVP local-cache flow and notes that DAS API support is still needed to fetch Metaplex Core assets by owner.

Cosmetics should not repeat that local-cache-only pattern if they are meant to be NFT-backed. Ownership needs to be derived from wallet-owned Core assets, with Supabase acting as cache/read model rather than authority.

### Backend Program In Repo Does Not Match Unity Expectations

Relevant files:

- `backend/chainbound_game/programs/chainbound_game/src/lib.rs`
- `backend/chainbound_game/Anchor.toml`
- `Assets/Data/Solana/SolanaConfig.asset`

The Unity client contains service code that expects instructions such as profile creation, vault creation, minting, upgrading, and rerolling. The Anchor program currently in the repo is still a minimal/default program with only an `initialize` instruction.

There is also a program ID mismatch:

- Unity config game program ID: `HgApc4htZTqEbjhE2YR2eKpHQmuuB4Z4QrJcCvV7BW7B`
- Local Anchor program ID: `9312ay7Vy8sFLCLsK66EcmGqmzC48nZMbFE3ZQYLiPGF`

This may mean the Unity client targets an external deployed program that is not represented by the checked-in backend. If so, that needs to be documented. If not, the current end-to-end chain path is not technically complete.

### Manual Anchor Instruction Building Is Fragile

Several Unity services manually build Anchor discriminator transactions and manually deserialize account data. This can work, but it creates schema drift risk. The Solana workflow guidance points toward generated clients or IDL-driven clients where practical, especially once the program surface area grows.

For cosmetic NFTs, this matters most if Unity directly calls game-program instructions that CPI into Metaplex Core. A generated interface would reduce the odds that Unity and the Anchor program disagree about account order, discriminators, layout, or signer requirements.

### Existing NFT Specs Have Drift

Relevant docs:

- `Docs/review-unimplemented/Specs/Solana/SPEC_01_WalletConnect_PlayerProfile_CoreInscriptions_MVP.txt`
- `Docs/review-unimplemented/Specs/Solana/SPEC_03_BattleCapture_ChainlingPDA_InscriptionNFT_MVP.txt`

Older specs describe Inscription NFTs as Metaplex Core assets. Current runtime code appears to have evolved toward:

- Ordinals as PDAs.
- Inscriptions as compressed NFTs/cNFTs.
- Runes as Metaplex Core NFTs.

That is not necessarily wrong, but cosmetic NFT work should start with an updated source-of-truth NFT taxonomy so the project does not mix old and new assumptions.

## Metaplex Core Fit For Cosmetics

Metaplex Core is a strong fit for Chainbound cosmetics because:

- Core is a single-account NFT standard designed for lower cost and game-style asset flexibility.
- Core supports plugins for behavior and metadata extension.
- Core assets can be updated by an update authority or authorized delegate.
- The Attributes plugin can store on-chain string key/value pairs, useful for game-readable fields such as cosmetic ID, slot, rarity, and schema version.
- DAS indexing can make wallet-owned asset lookup practical for client inventory reads.

The key design decision is authority. Player ownership should prove that the wallet owns the cosmetic. Game-critical metadata should not be arbitrarily editable by the owner unless that is intentionally part of the economy. For most cosmetics, the game or a game-controlled program should retain update authority or use an Update Delegate pattern.

## Recommended Cosmetic Core Asset Schema

Use Unity cosmetic IDs as the canonical gameplay identifiers. The Core asset should be the ownership container that proves a wallet owns one or more cosmetic unlocks.

Recommended Core collection strategy:

- One top-level `Chainbound Cosmetics` collection, or one collection per cosmetic family if operational separation is needed.
- Keep collection addresses in config or ScriptableObject data, not hard-coded across services.
- Require collection verification/provenance before trusting any asset.

Recommended Core Attributes:

- `cb.kind`: `cosmetic`
- `cb.cosmetic_id`: stable Unity cosmetic ID, for example `hud_card_neon_01`
- `cb.slot`: cosmetic slot, for example `hud_card`, `aura`, `party_column`
- `cb.schema_version`: metadata schema version, for example `1`
- `cb.rarity`: optional gameplay/display rarity
- `cb.species_scope`: optional species compatibility
- `cb.visual_variant_scope`: optional visual variant compatibility
- `cb.asset_rev`: optional content revision for mutable cosmetics

Recommended off-chain metadata fields:

- `name`
- `description`
- `image`
- `animation_url` if needed
- `external_url` if needed
- display attributes for marketplace UX

Game-critical fields should be validated from trusted on-chain collection/attributes first. Off-chain metadata is useful for display and marketplaces, but it should not be the only authority for unlock behavior.

## Recommended Authority Model

For normal cosmetics:

- Player wallet owns the Core asset.
- Game-controlled update authority controls mutable metadata.
- Optional Update Delegate gives the game program permission to update approved fields.
- Owners should not be able to rewrite `cb.cosmetic_id` or `cb.slot`.

For final/immutable cosmetics:

- Use immutability only when the cosmetic content and metadata contract are final.
- Avoid setting update authority to `None` too early, because that prevents later correction or migration.

For evolving cosmetics:

- Keep `cb.asset_rev` and metadata URI versioned.
- Use controlled updates for visual revisions, seasonal state, or rarity evolution.
- Maintain backward compatibility in Unity by mapping old schema versions.

## Recommended Unity Adapter

Add a production implementation behind the existing interface:

- `CoreCosmeticInventoryService : ICosmeticInventoryService`

Suggested read flow:

1. Wait for wallet connection through `IWalletService`.
2. Fetch wallet-owned Core assets through a DAS-enabled RPC provider.
3. Filter to Chainbound cosmetic collection(s).
4. Validate Core program/interface, collection provenance, and trusted update authority/delegate policy.
5. Read Attributes or trusted metadata fields.
6. Map `cb.cosmetic_id` to the existing `ChainlingVisualIdentityDatabaseSO` catalog.
7. Drop unknown, incompatible, malformed, or untrusted assets.
8. Return owned cosmetic IDs to `ChainlingStylePanel`.
9. Cache by wallet public key with refresh on wallet connect, style panel open, mint/update success, and manual refresh.

The important point: the style UI does not need to know about NFTs. It should keep asking for stable owned cosmetic IDs.

## Supabase Role

Supabase should remain useful, but not authoritative for NFT ownership.

Recommended responsibilities:

- Store equipped visual identity state.
- Store cached profile/read model data for fast menus.
- Store optional last-seen asset IDs for provenance/debugging.
- Store wallet-linked cosmetic cache only as a performance optimization.

Not recommended:

- Treating Supabase `ownedCosmeticIds` as the final proof of ownership for paid/tradable NFT cosmetics.
- Allowing a visual identity save to equip an NFT cosmetic without validating current wallet ownership, at least against a recent trusted Core inventory cache.

Potential visual identity extension:

- Store `cosmetic_id` as the gameplay field.
- Optionally store `asset_id` beside it for NFT-backed cosmetics.

Example:

```json
{
  "hudCardVariantId": "hud_card_neon_01",
  "hudCardVariantAssetId": "CoreAssetPubkey..."
}
```

The ID keeps gameplay stable. The asset ID gives provenance and auditability.

## Column Shell Plan Impact

The referenced plan, `.cursor/plans/column_shell_sync_+_cosmetics_74f1119a.plan.md`, is safe to approach as a local cosmetic scaffold first.

Recommended near-term approach:

- Add `partyColumnVariantId` to `ChainlingVisualIdentity`.
- Add a party column variant ScriptableObject definition and database list.
- Add Supabase payload/readback support.
- Apply the selected variant in the party column shell.
- Add the loadout display row.

Do not make the party column variant NFT-backed in the same step. Treat it like the existing HUD card/aura/style cosmetics first. Once `CoreCosmeticInventoryService` exists, the same stable ID can become wallet-owned without changing the visual application layer.

## Transaction And Signing Recommendations

Before any cosmetic mint/update pipeline signs real transactions:

- Show the user a clear transaction summary before wallet signing.
- Simulate transactions before signing/submission where possible.
- Use explicit devnet/localnet defaults until production readiness.
- Confirm signer, fee payer, cluster, recent blockhash, and target program IDs.
- Prefer generated clients or IDL-derived account schemas for game-program instructions.
- Keep raw manual instruction building isolated and heavily tested if generated clients are not feasible.

## Main Risks

1. Cosmetic ownership is not chain-backed yet.

The UI seam exists, but there is no production Core inventory adapter.

2. Backend implementation is not aligned with Unity services.

The checked-in Anchor program is minimal and does not expose the instructions Unity appears to expect.

3. Program ID mismatch.

Unity config and local Anchor program IDs differ. This must be resolved or explicitly documented as "external deployed program."

4. Core read path needs DAS support.

Fetching all wallet-owned Core assets through plain RPC can be slow or impractical. A DAS-capable provider should be selected.

5. Manual transaction/account schemas may drift.

Generated IDL/client workflows would make the Unity-to-program boundary safer.

6. Existing NFT taxonomy needs cleanup.

Docs still describe Core Inscriptions, while runtime code appears to use cNFT Inscriptions and Core Runes.

7. Update authority policy must be designed before minting.

Core can support editable NFTs, but mutability without a clear authority model can undermine gameplay integrity.

8. Config hygiene risk.

`vrfProgramId` in `SolanaConfig.asset` appears to include a trailing space. If parsed directly as a public key, this may fail at runtime.

## Recommended Phases

### Phase 1: Readiness And Source Of Truth

- Define the cosmetic Core metadata/attributes schema.
- Decide collection layout and update authority policy.
- Reconcile NFT taxonomy docs with current code.
- Document whether the game program is external or bring the checked-in Anchor program in line with Unity expectations.

### Phase 2: Read-Only Core Cosmetic Inventory

- Implement `CoreCosmeticInventoryService`.
- Configure a DAS-enabled RPC provider.
- Map wallet-owned Core assets to existing cosmetic IDs.
- Validate collection/update authority/provenance.
- Keep Supabase as cache/equipped state only.

### Phase 3: Party Column Cosmetic Scaffold

- **Shipped:** `partyColumnVariantId` on `ChainlingVisualIdentity`, `ChainlingPartyColumnVariantDefinitionSO`, Style panel equip, column shell cosmetic applier (`CardChrome`), non-cosmetic party type chrome palette (`TypeBackground` / pedestal tint), Supabase payload + ownership validation.
- Stable cosmetic IDs participate in the same ownership flow as HUD Card (`ICosmeticInventoryService` / `ValidateOwnedCosmetics`).
- Do not block this work on minting.

### Phase 4: Mint And Update Pipeline

- Add a dedicated cosmetic mint/update service.
- Use game-program CPI into Metaplex Core or a controlled backend/ops mint flow.
- Add transaction simulation and clear wallet signing UX.
- Add generated client/IDL coverage where possible.

### Phase 5: On-Chain Equip Provenance, If Needed

- Optionally store equipped cosmetic asset IDs on-chain or in a profile PDA.
- Only do this if gameplay, marketplace, anti-fraud, or tournament rules require on-chain equipped state.
- Otherwise, keep equipped state in Supabase and validate wallet ownership on login/save.

## Go / No-Go

Go:

- Continue building local/data-driven cosmetic scaffolding, including party column variants.
- Use stable cosmetic IDs as the bridge between Unity catalog data and future NFTs.
- Build toward Metaplex Core for cosmetic ownership.

No-go for production NFT cosmetics until:

- A Core asset inventory adapter exists.
- A cosmetic metadata/attribute schema is defined.
- Collection and update authority policy is set.
- Backend program alignment is resolved.
- DAS/read path is available.
- Transaction simulation/signing UX is added for mint/update flows.

## References

Local project references:

- `Packages/Solana.Unity-SDK-main/README.md`
- `Docs/Guides/WALLET_CONNECTION_SETUP.md`
- `Assets/Scripts/Solana/Services/SolanaServiceBridge.cs`
- `Assets/Scripts/Solana/Wallet/SolanaWalletService.cs`
- `Assets/Scripts/Solana/Rpc/SolanaRpcService.cs`
- `Assets/Scripts/Solana/Programs/SolanaChainTxOrchestrator.cs`
- `Assets/Scripts/Solana/Programs/CoreMoveService.cs`
- `Assets/Scripts/Solana/Programs/RuneMintService.cs`
- `Assets/Scripts/Combat/Runtime/Data/ChainlingVisualIdentity.cs`
- `Assets/Scripts/UI/MenuNavigation/Chainlings/ChainlingStylePanel.cs`
- `Assets/Scripts/UI/MenuNavigation/Services/SupabaseMenuVisualIdentityService.cs`
- `backend/chainbound_game/programs/chainbound_game/src/lib.rs`
- `.cursor/plans/column_shell_sync_+_cosmetics_74f1119a.plan.md`

External references consulted:

- Metaplex Core overview: https://www.metaplex.com/docs/smart-contracts/core
- Metaplex Core create asset: https://www.metaplex.com/docs/smart-contracts/core/create-asset
- Metaplex Core update asset: https://www.metaplex.com/docs/smart-contracts/core/update
- Metaplex Core Update Delegate plugin: https://www.metaplex.com/docs/smart-contracts/core/plugins/update-delegate
- Metaplex Core Attributes plugin: https://www.metaplex.com/docs/smart-contracts/core/plugins/attribute
- Metaplex Core fetch assets: https://www.metaplex.com/docs/smart-contracts/core/fetch
- Metaplex Core with Anchor: https://www.metaplex.com/docs/smart-contracts/core/using-core-in-anchor
- Solana Unity SDK: https://github.com/magicblock-labs/Solana.Unity-SDK
