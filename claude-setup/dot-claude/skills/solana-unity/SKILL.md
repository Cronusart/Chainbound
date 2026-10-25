---
name: solana-unity
description: Solana + Unity integration playbook for Chainbound. Use for Unity/C# wallet connect, signing, Solana.Unity-SDK flows, Core ownership reads, RPC/DAS access, PSG1/mobile wallet UX, and transaction safety. Ownership Spine 2026-09-15: Known/Stamped Core; no new cNFT mints.
---

# Solana Unity Integration (Chainbound)

Use this skill whenever work touches Unity, C#, wallet connection, RPC, DAS reads, transaction signing, or Solana-backed inventory in Chainbound.

**Mobile split:** this skill is the Unity client. For Android emulators, `adb`/wallet APKs, dApp Store APK publish, Seeker SGT/`.skr`/Connect, or Expo companions, open `.claude/skills/solana-mobile-routing/SKILL.md` first — do not paste Expo MWA code here.

**Ownership spine:** read [`Docs/Specs/Economy/OWNERSHIP_SPINE_v1.md`](../../../Docs/Specs/Economy/OWNERSHIP_SPINE_v1.md) first. That index locks Known PDA → Stamp Metaplex Core → Bound = Chainling books. Do not invent Bubblegum/cNFT move mints or Press Charge flows.

## Ground Rules

- Unity is the responsive client. It may cache and preview, but server/on-chain checks remain authoritative.
- Never trust Unity-submitted ownership lists for paid or tradable assets.
- Keep all Solana Unity SDK access behind service interfaces in `Assets/Scripts/Solana/**` or existing Combat Runtime service contracts.
- Do not perform per-frame RPC/DAS calls. Refresh on wallet connect, account change, screen open, explicit refresh, and successful transaction confirmation.
- Default to devnet/localnet unless a production cluster is explicitly configured and confirmed.
- Do not ask for, load, log, or store private keys. Use wallet adapter signing only.
- Any transaction UX must show cluster, fee payer, program, action, asset/cost summary, simulation result, and confirmation state before/after signing.

## Chainbound Asset Taxonomy (Ownership Spine 2026-09-15)

| Player / design term | Kind | Standard |
|---|---|---|
| **Known** (legacy internal: Ordinal) | zero-modifier move instance | Game PDA / profile — **not** an NFT |
| **Stamped** move (1–2 modifiers) | tradable move | **Metaplex Core** (`Chainbound Moves`) |
| **Bound** Archive | published Chainling book | **Metaplex Core** (`Chainbound Archives`) |
| Book covers | Bind consumable | **Metaplex Core** (trusted covers) |
| Cosmetics / skins | visual collectible / Bind component | **Metaplex Core** (`Chainbound Cosmetics`) |

Hard taxonomy rules:

- Known / Ordinals = **PDAs** (or profile rows), never minted.
- Stamped moves = **Metaplex Core** only. Stamp consumes the Known instance.
- Bound Archives = **Metaplex Core**. Bound is never a move tier.
- Cosmetics and covers = **Metaplex Core**.
- **No new cNFT / Bubblegum mint** for moves. Legacy Bubblegum / Inscription code (`MoveMintService`, Bubblegum paths in `VendingMachineService`) is **migration-only / disabled**. Do not enable ClosedBeta vending until a Core Stamp path exists.
- Internal enum values may still say `ordinal` / `inscription` / `rune` during migration; presentation maps to Known or Stamped.

## Core Cosmetic Reads

- Unity should call Supabase Edge Functions for production cosmetic inventory, not DAS directly.
- Supabase verifies wallet/profile linkage, Core collection, owner wallet, update authority/delegate policy, `cb.*` attributes, and catalog membership.
- Unity receives stable cosmetic IDs plus optional asset provenance for save/debug UX.
- The style UI should stay catalog/ID driven and remain unaware of Metaplex/DAS internals.

## RPC And DAS

- Plain Solana RPC is acceptable for PDA reads and transaction submission.
- DAS-enabled RPC is required for wallet-owned **Core** inventory reads (moves, covers, cosmetics, archives). Do not build new Bubblegum proof pipelines for Stamp.
- Store RPC/DAS endpoints in config or deployment secrets, never scattered constants.
- Treat all RPC/DAS payloads as untrusted. Validate owner, program/interface, collection, discriminators, account data length, schema version, and expected authorities before deserializing or displaying.

## Placement

- Solana SDK wrappers and chain services: `Assets/Scripts/Solana/**` via `Chainbound.Combat.Runtime.asmref`.
- Menu-facing inventory adapters that implement Combat Runtime interfaces may live in `Assets/Scripts/Combat/Runtime/Services/**`.
- Menu UI may depend on Combat Runtime services; Combat Runtime must not depend on MenuNavigation.

## Mobile device testing

Unity Editor Play Mode is not Mobile Wallet Adapter. When the user needs a phone or emulator:

1. `.claude/skills/solana-mobile/SKILL.md` — `npx solana-mobile@latest doctor` / `emu` / `device install fakewallet` / `localnet`.
2. Keep signing behind `Assets/Scripts/Solana/**` interfaces (`solana-unity` placement rules below).
3. dApp Store ship: `.claude/skills/solana-mobile-publishing/SKILL.md` (APK only).

## Testing Expectations

- Add edit-mode tests for Unity adapters that parse server payloads and drop malformed records.
- Add Supabase tests or function-level fixtures for ownership validation failures.
- Add Anchor/IDL/account-order tests before depending on new transaction builders.
- Run `dotnet build "Signal Jam 3D.sln" -v minimal` after C# changes when feasible.
