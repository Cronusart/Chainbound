# Parked for human review

These files were moved here during hackathon doc cleanup. They are not deleted. They are not the live description of the game.

`Docs/GameTheory/` was not touched and is not part of this folder.

Open a file here only if you are deciding whether to build it. For what the repo actually runs, start at [../INDEX.md](../INDEX.md).

## Yet to implement

| File | Why it is here |
|---|---|
| [Guides/PVP_00_Battle_Spine_First.md](Guides/PVP_00_Battle_Spine_First.md) | Shipping order: battle spine (M0) before NGO lobby / escrow / tournaments. Includes M0 agent prompt + anti-cheat foundations. |
| [Guides/PVP_01_Social_Lobby_And_Casual_Matchmaking.md](Guides/PVP_01_Social_Lobby_And_Casual_Matchmaking.md) | Pre-build lobby and casual matchmaking. No lobby or matchmaking types in `Assets/Scripts`. Netcode for GameObjects is a package dependency only. |
| [Guides/PVP_02_Ranked_Escrow_And_Ladder.md](Guides/PVP_02_Ranked_Escrow_And_Ladder.md) | Pre-build ranked escrow. Client wager types exist under `Assets/Scripts/Combat/Runtime/PvP/` and are not a playable mode. No escrow program. `ClosedBetaFeaturePolicy` does not allow wager instructions. |
| [Guides/PVP_03_Tournaments_And_Themed_Brackets.md](Guides/PVP_03_Tournaments_And_Themed_Brackets.md) | Pre-build tournaments and brackets. No tournament code. |
| [Guides/RAM_00_Immediate_Battle_Reward.md](Guides/RAM_00_Immediate_Battle_Reward.md) | RAM v2: Postgres becomes the only RAM balance. Phase 1 immediate battle earning is implemented locally and awaits SQL execution tests, deployment, and Devnet soak. Program v2 spends, mirror retirement, shop migration, and server-resolved combat remain gated follow-on phases. |
| [Guides/DAILY_CRATE_00_Toby_Shipment.md](Guides/DAILY_CRATE_00_Toby_Shipment.md) | Concept for Toby's daily house gift (10 RAM and 1 Signal Orb) and the same claim on the main-menu events tree. No claim service, present, or menu button yet. |
| [Specs/Combat/COMPETITIVE_LADDER_AND_SEASONS_v1.md](Specs/Combat/COMPETITIVE_LADDER_AND_SEASONS_v1.md) | Season/ladder groundwork. `RankedLoadoutValidator` exists; there is no ladder or season service. |
| [Camera/COMBAT_POPUP_OVERLAY_CAMERA_PLAN.md](Camera/COMBAT_POPUP_OVERLAY_CAMERA_PLAN.md) | Plan for a combat popup camera that is not blurred by depth of field. No overlay-camera type in the combat scripts. |
| [Guides/COMBAT_SFX_ADVANCED_SEQUENCING_PLAN.md](Guides/COMBAT_SFX_ADVANCED_SEQUENCING_PLAN.md) | Recipe/role combat audio. The foundation (`CombatSfxOrchestrator`) shipped; this model did not. |
