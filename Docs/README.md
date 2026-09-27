# Documentation Index

This folder is the public documentation surface for the project. It is curated for setup, architecture review, and hackathon judging rather than day-to-day internal working notes.

## Start Here
- [../README.md](../README.md): project overview, repo map, and demo entry points
- [ARCHITECTURE.md](ARCHITECTURE.md): high-level system responsibilities and data flow
- [HOW_TO_PLAY.md](HOW_TO_PLAY.md): gameplay loop and control expectations

## Setup And Demo Guides
- [Guides/combat_testscene.md](Guides/combat_testscene.md): quickest path to the combat harness
- [Guides/PLAYER_MENU_TEST_HARNESSES.md](Guides/PLAYER_MENU_TEST_HARNESSES.md): safe pattern for disposable player-menu mock data and sandbox prefabs
- [Guides/PRODUCTION_CUTOVER_AND_HARNESS_DISENGAGE.md](Guides/PRODUCTION_CUTOVER_AND_HARNESS_DISENGAGE.md): checklist to leave harnesses behind and wire production adapters before live testing
- [WALLET_CONNECTION_SETUP.md](WALLET_CONNECTION_SETUP.md): wallet and RPC setup
- [MAIN_MENU_SETUP.md](MAIN_MENU_SETUP.md): main menu scene setup
- [Combat2_UI_SetupGuide.md](Combat2_UI_SetupGuide.md): combat UI setup notes

## PvP / Multiplayer Phase Guides
- [Guides/PVP_01_Social_Lobby_And_Casual_Matchmaking.md](Guides/PVP_01_Social_Lobby_And_Casual_Matchmaking.md): NGO lobby, casual queue, Supabase turn relay, dual-sim forensic hashes (beta)
- [Guides/PVP_02_Ranked_Escrow_And_Ladder.md](Guides/PVP_02_Ranked_Escrow_And_Ladder.md): wallet-gated ranked, Anchor match escrow, referee settle, ladder wiring (Solana MCP mandatory)
- [Guides/PVP_03_Tournaments_And_Themed_Brackets.md](Guides/PVP_03_Tournaments_And_Themed_Brackets.md): tournament pool, brackets, CombatRulesetSO themes, server rewards

## Specs Worth Reviewing
- [Specs/Combat/MOVE_TIER_PLAYER_FACING_v1.md](Specs/Combat/MOVE_TIER_PLAYER_FACING_v1.md): **Known / Stamped / Bound** move tiers, Archive Dispenser, player copy (locked)
- [Specs/UI/CHAINLING_ARCHIVE_ENCYCLOPEDIA.md](Specs/UI/CHAINLING_ARCHIVE_ENCYCLOPEDIA.md): Archive Dossier, Bound Encyclopedia, Bind to Chain ceremony (locked)
- [Specs/Combat/COMBAT_Revisions_v1.md](Specs/Combat/COMBAT_Revisions_v1.md): combat design and revision notes
- [Specs/Economy/ECONOMY_01_EconomyAndProgression_v1.md](Specs/Economy/ECONOMY_01_EconomyAndProgression_v1.md): long-term economy, vending, mastery, rebirth, and **authoritative progression model** (stars/mastery/rebirth replace legacy levels)
- `Specs/Solana/`: Solana-facing specs and integration notes

## Notes
- Runtime gameplay code lives under `Assets/Scripts/`.
- Combat code lives under `Assets/Scripts/Combat/`.
- Internal planning files, forensic logs, and local working notes are intentionally kept out of the public GitHub surface.
- `GameTheory/` is **exploratory only** (not a spec). Do not use it in plans or builds unless a named file in that folder is explicitly sourced. See [GameTheory/README.md](GameTheory/README.md).
