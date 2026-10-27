# Combat Balance Follow-ups v1 — Baseline Tests

Captured on 2026-10-18 from Unity 6000.0.24f1 before the follow-up implementation.

## Focused combat decision baseline

Command: `unity command run_tests --mode EditMode --filter 'Chainbound.Combat.Tests.CombatDecisionRoundTests' --filter_type testName`

- Total: 8
- Passed: 8
- Failed: 0
- `BenchRechargeTicksAndOtherStatusesPause` still reaches bench AP 4.
- `GroupHugGrantsOneApToActiveAlliesAndDoesNotCopyTheSupportMove` still reaches user AP 3, active partner AP 3, and bench AP 4.
- Apply-once, repeating, and chemistry-owned stages all reverted on expiry in the covered cases.

## Full EditMode baseline

Command: `unity command run_tests --mode EditMode`

- Duration: 233.77 seconds
- Total: 1,107
- Passed: 1,052
- Failed: 55
- Skipped: 0
- Inconclusive: 0

The existing failures are listed below so later combat-pass runs can be compared against this baseline. None are in `CombatDecisionRoundTests`.

1. `AmbientNpcPrefabAuthoringTests.CrayonEaterDialogue_AsksForALuckyCandyMachineRoll`
2. `CameraYawPresentationTests.PlayerShadow_TracksSettledCameraYawWithoutMovementCallback`
3. `CandyMachineAuthoringTests.GameServices_HostsCandyMachinePresenter`
4. `CandyMachineAuthoringTests.Prefab_WiresOnlyTheAuthoredDevGrantStrip`
5. `ChainlingProgressionFolioEditModeTests.AuthoredSpeciesPools_AreRepeatableAndKeepExclusiveMoves`
6. `CombatCapturePreviewHarnessTests.HarnessScene_HasEnabledCapturePreview`
7. `CombatPostBattleFlowAuthoringTests.ReportPrefab_AuthoredLayoutIsNotOverwrittenByStarArtOrCanvasInstance`
8. `CombatPostBattleFlowAuthoringTests.ReportPrefab_HasBannerReportAndRewardsSurfaces`
9. `DialoguePanelPresentationTests.SharedDialoguePanel_AuthorsBottomPopMotionForNarrativeAndCombat`
10. `ExplorationSpritePresentationPresenterTests.DirectPlay_AutoReleaseCompletesPresentation`
11. `ExplorationSpritePresentationPresenterTests.DirectPlaybackOverride_CanOverrideClipDefaultPolicy`
12. `ExplorationSpritePresentationPresenterTests.HoldLastFrame_KeepsPresenterActiveUntilStopped`
13. `ExplorationSpritePresentationPresenterTests.LoopForDuration_CompletesAfterConfiguredDuration`
14. `ExplorationSpritePresentationPresenterTests.TriggerBinding_FiresConfiguredClip`
15. `GameplayModePolicyTests.CombatScenePolicy_BlocksMenuAndDeactivatesOwnedPlayerRoot`
16. `InteractableAvailabilityGateTests.InteractableIndicator_HidesForInteractionLockButNotMovementOnly`
17. `InteractableAvailabilityGateTests.InteractableObject_NoDialoguePath_RestoresAfterGraceAndCooldown`
18. `InteractableAvailabilityGateTests.NPCInteractableBinding_RemainsBlockedForOwnedSessionAndCooldown`
19. `InteractionAuthoringValidationTests.CollectIssues_FlagsDeprecatedFallbackFields`
20. `InteractionAuthoringValidationTests.CollectIssues_FlagsDeprecatedInteractionBridge`
21. `NPCInteractionSessionServiceTests.BeginNormalInteraction_AcquiresConfiguredRouteOverrideAndFacesPlayer`
22. `NPCInteractionSessionServiceTests.LockedDialogue_ClaimsPendingSessionWithoutTrackingPlayer`
23. `NPCInteractionSessionServiceTests.LoopUntilStoppedPresentation_IsStoppedWhenClaimedDialogueCompletes`
24. `NPCInteractionSessionServiceTests.UnlockedDialogue_ClaimsPendingSessionTracksPlayerAndReleasesOnCompletion`
25. `OverworldStreamingCameraTests.Neutralize_DisablesReloadedHubCameraRigWithoutTouchingPersistentPresenter`
26. `PlayerMovementSpeedModifierStackTests.Consumable_ScalesPlayerMovementAndExpiryRestoresPreBoostSpeed`
27. `RuntimeVolumeProfileHandleTests.Release_RestoresAuthoredSharedProfileAndDoesNotMutateAsset`
28. `SceneLoadingPipelineTests.ProjectWiring_PassesLoadingValidation`
29. `SpriteBillboardTests.YLock_PreservesLocalYaw_WhileFollowingCameraTilt`
30. `StarterSelectionUiPrefabTests.Prefab_HasUsableModalCanvasAndButtonPresentation`
31. `TutorialReadAlongTests.AuthoredAssets_IntroSequenceInsertsReadAlongBeats`
32. `TutorialReadAlongTests.Presenter_ArmsDismissOnlyAfterRevealAndReadDelay`
33. `WorldPickupGrantTests.AlreadyClaimed_StayOpenSnapsDoorWithoutOpeningEvent`
34. `WorldPickupGrantTests.Collect_HideVisualDisablesSpentTargetNotHost`
35. `WorldPickupGrantTests.Collect_SetsDurablePickupFlagAndHidesBoundVolume`
36. `WorldPickupGrantTests.Collect_UnknownItemDoesNotSetFlag`
37. `WorldPickupGrantTests.SnapOpen_AppliesOpenPoseWithoutSfxEvents`
38. `Chainbound.Combat.Tests.ChainlingPartyReadModelTests.BuildSummary_GeneralView_ProducesXpBarFill`
39. `Chainbound.Combat.Tests.CombatNarrationPresentationTests.DialogueView_SelectedChoiceSubmit_CommitsSelectedIndex`
40. `Chainbound.Combat.Tests.CombatPopupVisualTests.PopupFacingSignFollowsChainlingFlipX`
41. `Chainbound.Combat.Tests.CombatPopupVisualTests.PopupWorldPositionFallsBackToImpactThenSlotWithoutCameraFocus`
42. `Chainbound.Combat.Tests.CombatPopupVisualTests.PopupWorldPositionPrefersPopUpAnchorOverCameraFocus`
43. `Chainbound.Combat.Tests.CombatPostFxVolumeControllerTests.IdleWeightIsZeroAndDoesNotTouchBaselineVolume`
44. `Chainbound.Combat.Tests.CombatPostFxVolumeControllerTests.PlayRaisesWeightThenReturnsToZero`
45. `Chainbound.Combat.Tests.CombatPostFxVolumeControllerTests.ReplaceLatestCueEndsAtZeroWeight`
46. `Chainbound.Combat.Tests.CombatPresentationAudioTests.SoundOnlyPhaseHonorsSpawnDelay`
47. `Chainbound.Combat.Tests.CombatSfxOrchestratorTests.CritWindupClampsAtZeroForZeroAndShortDelays`
48. `Chainbound.Combat.Tests.CombatSfxOrchestratorTests.CritWindupPrecedesDelayedImpact`
49. `Chainbound.Combat.Tests.CombatSfxOrchestratorTests.KoPlaysAfterImpactNotBefore`
50. `Chainbound.Combat.Tests.CombatSfxOrchestratorTests.MissDodgeAndBlockDoNotPlayCritWindupOrLegacyAccent`
51. `Chainbound.Combat.Tests.CombatSfxOrchestratorTests.StatusAfterDamageIsDelayedAfterImpact`
52. `ChainlingDossierPrefabTests.DossierPrefab_PagesUseVerticalWireframes`
53. `ChainlingDossierPrefabTests.DossierPrefab_PixeloidLabelsUseStampInk`
54. `ChainlingPartyUnboundPresentationTests.PrepareUnboundPresentation_HidesStackedViewLayoutsUntilBind`
55. `PlayerMenuHubTests.MenuTabBar_LockedTabCanFocusButDoesNotSubmit`

## Catalog wiring baseline

- The combat build scene, `Assets/Scenes/Combat2_TestHarness.unity`, serializes `Assets/Data/Combat/StatusDef/Catalog/StatusCatalog.asset` (GUID `55cb3a889f16cf04ab896f1443e68fc9`).
- That canonical catalog contains both `Kindled` and `SearField`.
- `Assets/Data/Combat/StatusDef/StatusCatalog.asset` (GUID `1dc265199365bd44dabde021e3d3dc12`) is not referenced by scenes, prefabs, assets, or code and does not contain `Kindled`.
- The importer writes the canonical nested catalog. Editor validation should load that path explicitly rather than accepting the first catalog returned by `AssetDatabase.FindAssets`.

## Post-change verification

Final focused Unity EditMode runs after data import and manifest export:

- `CombatDecisionRoundTests`: 10/10 passed. Bench AP remained 4; Group Hug remained user 3, partner 3, bench 4; stage expiry, Cleanse reversion, and Cipher Lock buff blocking passed.
- `ApCostLadderTests`: 17/17 passed, including All-In spend narration, Fee Collector narration, boss payoff scoring/tells, Cofi's Grind Tax timing, AP drain, and the Thread Lock cross-battle reset.
- `CombatRuntimeFixTests`: 17/17 passed.
- `CombatMoveResolveProfileTests`: 11/11 passed.
- `BossNoDrawTests`: 4/4 passed.
- `CombatCsvIntegrityTests`: 3/3 passed.
- `LibraryCardAcquisitionTests`: 1/1 passed.
- `KnownPoolGenerationTests`: 1/1 passed.
- `ManifestParityTests`: 1/1 passed after the final export, including every move field and every `StatusId` fallback row.

Final parity and non-Unity verification:

- Gameplay manifest SHA-256: `427d8dffa848a665f19fe809b244560cbb8e9ff24457e48997927946229bcf24`. The `move_ap.ts`, `move_flags.generated.ts`, and `known_pool.generated.ts` headers all carry that hash.
- `dotnet build backend/Chainbound.BattleService/Chainbound.BattleService.csproj --no-restore`: passed with 0 warnings and 0 errors.
- `Chainbound.CaptureCoreVerification`: passed gameplay-manifest parity, manifest-v3 assertions, and both deterministic goldens.
- Full shared Deno suite: 120/120 passed, including move AP parity, Known pool parity, boss-only Known exclusion, PvP boss-only rejection, and ownership metadata.
- The ownership card catalog generator wrote 10 moves, 104 modifiers, and 210 authorities; `grind_tax` has 0 catalog occurrences.
- Learned-card renderer suite: 55/55 passed after square-padding learned art.
- CSV-to-asset move parity: 211 CSV rows, 211 assets, 0 field differences, 0 CSV-only rows, 0 asset-only rows.

Full-suite runner notes:

- The first post-change monolithic EditMode command timed out after 900 seconds while the live Editor retained the synchronous Pipeline command lock. This was inconclusive.
- A later async monolithic run completed the tests but failed to publish its completion file, so the suite was split by its three project assemblies.
- `Chainbound.Combat.Tests` published a result for 670 tests: 655 passed and 15 failed in the NUnit summary. Its combat-refinement tests passed. The failure leaves were the recorded presentation/editor baseline failures plus a pre-recompile Validape pool assertion and an unrelated interaction-gate timeout; the current test source now asserts the approved `baseline_striker` pool replacement.
- A synchronous confirmation retry orphaned the Pipeline execution gate before the corrected fixture and the two remaining assemblies could be rerun. A clean Editor restart is required to finish the full-suite comparison; do not treat the partial assembly run as the final green light.
