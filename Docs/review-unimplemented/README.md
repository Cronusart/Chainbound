# Parked for human review

These files were moved here during hackathon doc cleanup. They are not deleted. They are not the live description of the game.

`Docs/GameTheory/` was not touched and is not part of this folder.

Open a file here only if you are deciding whether to build it. For what the repo actually runs, start at [../INDEX.md](../INDEX.md).

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
