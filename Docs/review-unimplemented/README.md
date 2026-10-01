# Parked for human review

These files were moved here during hackathon doc cleanup. They are not deleted. They are not the live description of the game.

`Docs/GameTheory/` was not touched and is not part of this folder.

Open a file here only if you are deciding whether to build it or drop it. For what the repo actually runs, start at [../INDEX.md](../INDEX.md).

## Yet to implement

| File | Why it is here |
|---|---|
| [Guides/PVP_01_Social_Lobby_And_Casual_Matchmaking.md](Guides/PVP_01_Social_Lobby_And_Casual_Matchmaking.md) | Pre-build lobby and casual matchmaking. No lobby or matchmaking types in `Assets/Scripts`. Netcode for GameObjects is a package dependency only. |
| [Guides/PVP_02_Ranked_Escrow_And_Ladder.md](Guides/PVP_02_Ranked_Escrow_And_Ladder.md) | Pre-build ranked escrow. Client wager types exist under `Assets/Scripts/Combat/Runtime/PvP/` and are not a playable mode. No escrow program. `ClosedBetaFeaturePolicy` does not allow wager instructions. |
| [Guides/PVP_03_Tournaments_And_Themed_Brackets.md](Guides/PVP_03_Tournaments_And_Themed_Brackets.md) | Pre-build tournaments and brackets. No tournament code. |
| [Specs/Combat/COMPETITIVE_LADDER_AND_SEASONS_v1.md](Specs/Combat/COMPETITIVE_LADDER_AND_SEASONS_v1.md) | Season/ladder groundwork. `RankedLoadoutValidator` exists; there is no ladder or season service. |
| [Camera/COMBAT_POPUP_OVERLAY_CAMERA_PLAN.md](Camera/COMBAT_POPUP_OVERLAY_CAMERA_PLAN.md) | Plan for a combat popup camera that is not blurred by depth of field. No overlay-camera type in the combat scripts. |
| [Guides/COMBAT_SFX_ADVANCED_SEQUENCING_PLAN.md](Guides/COMBAT_SFX_ADVANCED_SEQUENCING_PLAN.md) | Recipe/role combat audio. The foundation (`CombatSfxOrchestrator`) shipped; this model did not. |
| [Specs/Economy/OWNERSHIP_SPINE_LIVE_MATRIX_PLAN.md](Specs/Economy/OWNERSHIP_SPINE_LIVE_MATRIX_PLAN.md) | Operator test plan marked PLAN ONLY / NO-GO. Not a player feature. |
| [Suggestion/EncounterBannerVFX.txt](Suggestion/EncounterBannerVFX.txt) | Banner polish suggestion, not a built VFX spec. |
| [Narrative/narrative_system_v_2_spec_and_cursor_prompt.md](Narrative/narrative_system_v_2_spec_and_cursor_prompt.md) | Broader narrative V2 spec. Child tracks exist on `SequenceRunner`; this file goes past that and is not the authoring guide. |
| [Narrative/Interaction_Guard_And_Parallel_Tracks_Summary_For_Refinement.md](Narrative/Interaction_Guard_And_Parallel_Tracks_Summary_For_Refinement.md) | Proposed direction for parallel quest routing. Not a built system. |

## Probably scrap

Kept so a person can confirm before deletion. Nothing here should be cited as the current design.

| File | Why it is here |
|---|---|
| [Specs/Solana/SPEC_01_WalletConnect_PlayerProfile_CoreInscriptions_MVP.txt](Specs/Solana/SPEC_01_WalletConnect_PlayerProfile_CoreInscriptions_MVP.txt) | Legacy hackathon MVP. The body still describes Ordinals as PDAs, Inscriptions as cNFTs, and Runes as Core NFTs. That taxonomy was retired. Live docs: [wallet setup](../Guides/WALLET_CONNECTION_SETUP.md), [asset taxonomy](../Specs/Solana/CHAINBOUND_SOLANA_ASSET_TAXONOMY.md), [account model](../Architecture/Account_Platform_Refactor.md). |
| [Specs/Solana/SPEC_02_EncounterSystem_VRF_MVP.txt](Specs/Solana/SPEC_02_EncounterSystem_VRF_MVP.txt) | The file says it was superseded in 2026-08. Wild encounters use `EncounterRollService`, not VRF. |
| [Specs/Solana/SPEC_03_BattleCapture_ChainlingPDA_InscriptionNFT_MVP.txt](Specs/Solana/SPEC_03_BattleCapture_ChainlingPDA_InscriptionNFT_MVP.txt) | Says every capture mints an inscription NFT. Live capture is profile-first: [PROFILE_FIRST_CAPTURE.md](../Specs/Combat/PROFILE_FIRST_CAPTURE.md). |
| [Specs/Solana/COSMETIC_CORE_NFT_ADAPTABILITY_SPEC.md](Specs/Solana/COSMETIC_CORE_NFT_ADAPTABILITY_SPEC.md) | June 2026 findings ("no implementation in this pass"). Canonical model is the [asset taxonomy](../Specs/Solana/CHAINBOUND_SOLANA_ASSET_TAXONOMY.md). |
| [Combat/COMBAT_CHEMISTRY_REACTION_REDESIGN_SUGGESTIONS.md](Combat/COMBAT_CHEMISTRY_REACTION_REDESIGN_SUGGESTIONS.md) | Suggestion pass, not the chart. Live chemistry is [COMBAT_Chemistry.txt](../Specs/Combat/COMBAT_Chemistry.txt) and [COMBAT_TypeProfiles.md](../Specs/Combat/COMBAT_TypeProfiles.md). |
| [Specs/Economy/OWNERSHIP_STAMP_OPERATOR_CLEANUP_2026-09-26.md](Specs/Economy/OWNERSHIP_STAMP_OPERATOR_CLEANUP_2026-09-26.md) | One-off 2026-09-26 operator session log for a specific profile and wallet. Not a mechanic spec. |
