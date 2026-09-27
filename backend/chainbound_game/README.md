# Chainbound Anchor Workspace

The active move ownership contract is the Phase 1 Ownership Spine: walletless,
zero-modifier `KnownMove` records and atomic `stamp_known_move` creation of one- or
two-modifier Metaplex Core `StampedMove` assets. The generated IDL in
`idl/chainbound_game.json` is canonical. Superseded move-mint and third-tier
entrypoints are intentionally absent.

This folder is an optional Solana/Anchor workspace that sits alongside the Unity client. It is included in the public monorepo for completeness, but it is not required to open or run the core Unity combat demo.

## Contents
- `programs/chainbound_game/`
  - Rust program workspace.
- `tests/`
  - Anchor/TypeScript-side tests.
- `Anchor.toml`
  - Anchor workspace configuration.
- `Cargo.toml`
  - Rust workspace manifest.

## Intended Use
- Prototype on-chain state and transaction flows for Chainbound features.
- Keep Solana program work versioned beside the Unity client and Supabase layer.
- Support devnet-oriented iteration without blocking local gameplay iteration.

## Notes
- This workspace currently targets devnet in `Anchor.toml`.
- Local wallet and deploy credentials must remain outside the public repo.
- Build artifacts, ledgers, and nested Git state are intentionally excluded from source control.
- `idl/chainbound_game.json` is the checked-in Anchor-generated IDL. Regenerate it and the Unity client with `anchor build` followed by `scripts/generate-unity-client.ps1`; never edit either artifact by hand.
- The legacy `mint_inscription` Core path is disabled. Current runtime taxonomy is Ordinals as PDAs, Inscriptions as cNFTs, Runes as Metaplex Core NFTs, and cosmetics as Metaplex Core assets under `Chainbound Cosmetics`.
