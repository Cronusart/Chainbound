# Chainbound mechanics index

Use this page to find a mechanic and the doc that describes it. Status was checked against the Unity scripts, Supabase functions, and `backend/chainbound_game` program in this snapshot.

| Status | Meaning |
|---|---|
| **Implemented** | The client, edge function, or program does this, and the linked doc matches that behavior. |
| **Partial** | A real slice exists. The doc also describes gates, follow-ups, or steps that are not live. |
| **Not in this tree** | Described only under [review-unimplemented/](review-unimplemented/README.md). Do not demo it as shipped. |

`Docs/GameTheory/` is exploratory and is not a mechanic source. It was left in place.

## Play loop

| Mechanic | Status | Where it is documented | Where it lives in code |
|---|---|---|---|
| How a match feels | Implemented | [HOW_TO_PLAY.md](HOW_TO_PLAY.md) | Combat scenes under `Assets/Scenes/` |
| Turn-based 2v2 combat | Implemented | [Assets/Scripts/Combat/README.md](../Assets/Scripts/Combat/README.md), [Combat2 UI setup](Combat/Combat2_UI_SetupGuide.md), [resolution math](Specs/Combat/COMBAT_Resolution.txt), [combat UI spec](Specs/Combat/COMBAT_UI.txt), [combat test scene](Guides/combat_testscene.md) | `Assets/Scripts/Combat/Runtime/Engine/`, `CombatBattleController` |
| Type chemistry | Partial | [COMBAT_Chemistry.txt](Specs/Combat/COMBAT_Chemistry.txt), [type profiles](Specs/Combat/COMBAT_TypeProfiles.md), [matrix pass plan](Combat/COMBAT_CHEMISTRY_MATRIX_PASS_PLAN.md), interaction CSVs in `Specs/Combat/` | `TypeChemistryMatrixSO`. The matrix plan's ranked success checklist is still open. |
| Statuses | Implemented | [COMBAT_Statuses.txt](Specs/Combat/COMBAT_Statuses.txt), [Statuses.csv](Specs/Combat/Data/Statuses.csv). Tooltip and duration mismatches are in the [effects audit](Combat/Audits/BATTLE_EFFECTS_CORRECTNESS_AUDIT.md). Statuses with no producer: [producer research](Combat/Audits/CATALOG_STATUSES_WITHOUT_PRODUCERS.md). | `StatusProcessor` |
| Moves, modifiers, species pools | Implemented | [moves](Specs/Combat/COMBAT_Moves.txt), [modifiers](Specs/Combat/COMBAT_Modifiers.txt), [species pools](Specs/Combat/COMBAT_Species.txt), [CSV legend](Specs/Combat/Data/_LEGEND.md), [Pass A notes](Specs/Combat/PASS_A_NOTES.md). Effect mismatches: [effects audit](Combat/Audits/BATTLE_EFFECTS_CORRECTNESS_AUDIT.md). Unimplemented clauses, one row each: [unimplemented effects](Combat/Audits/UNIMPLEMENTED_MOVE_EFFECTS.md). Rarity screen: [balance audit](Combat/Audits/BATTLE_MOVE_BALANCE_BY_RARITY.md). | Combat data assets and `Docs/Specs/Combat/Data/*.csv` |
| Move loadout rules | Implemented | [MOVE_LOADOUT_RULES_AND_UI_SPEC.md](Specs/Combat/MOVE_LOADOUT_RULES_AND_UI_SPEC.md) | `profile_set_loadout` |
| Walls and targeting | Implemented | [wall plan](Combat/COMBAT_WALL_VISUAL_AND_TARGETING_PLAN.md), [wall direction note](Combat/COMBAT_WALL_VISUAL_AND_TARGETING_PLAN_v2.md). Both were written as plans; the runtime has since landed. | `CombatWallVisual` |
| Modifier resolver and popups | Partial | [modifier resolver plan](Combat/COMBAT_MODIFIER_RESOLVER_AND_POPUP_PLAN.md) | `CombatModifierResolverAndPopupTests`. Some TODO rows in the plan are still open. |
| Combat presentation, VFX, narration | Implemented | [hybrid presentation](Combat/Combat_Presentation_Hybrid_Guide.md), [VFX setup](Combat/Combat_VFX_Presentation_Setup_Guide.md), [visual pipeline](Combat/Combat_Visual_Authoring_Pipeline.md), [narration](Combat/Combat_Narration_And_Battle_Report.md), [animation previewer](Combat/Combat_Animation_Previewer.md), [pixel battle VFX](Combat/Pixel_Battle_VFX_System.md), [signal glitch](Guides/SIGNAL_GLITCH_SETUP.md) | `Assets/Scripts/Combat/Runtime/Presentation/` |
| Combat SFX foundation | Implemented | [AUDIO_SETUP.md](Guides/AUDIO_SETUP.md), [SFX foundation plan](Guides/COMBAT_SFX_FOUNDATION_PLAN.md) | `AudioService`, `CombatSfxOrchestrator` |
| Advanced combat SFX recipes | Not in this tree | [review copy](review-unimplemented/Guides/COMBAT_SFX_ADVANCED_SEQUENCING_PLAN.md) | — |
| Popup overlay camera | Not in this tree | [review copy](review-unimplemented/Camera/COMBAT_POPUP_OVERLAY_CAMERA_PLAN.md) | — |
| Balance levers | Implemented | [LEVER_BOARDS_v1.md](Specs/Combat/LEVER_BOARDS_v1.md), [defense notes](Combat/DEFENSE_BALANCE_SYSTEMS_NOTES.md), [changelog](Specs/Combat/CHANGELOG.md) | `BalanceConfigRuntime` and combat ScriptableObjects |
| Battle effects correctness and rarity screen (2026-10-16) | Follow-up applied on the audit branch | [effects audit](Combat/Audits/BATTLE_EFFECTS_CORRECTNESS_AUDIT.md), [unimplemented effects](Combat/Audits/UNIMPLEMENTED_MOVE_EFFECTS.md), [statuses with no producer](Combat/Audits/CATALOG_STATUSES_WITHOUT_PRODUCERS.md), [rarity balance](Combat/Audits/BATTLE_MOVE_BALANCE_BY_RARITY.md) | `CombatResolver`, `StatusProcessor`, `ChemistryResolver`. The decision round fixed Bean Sip text and +1 AP, timed-stage revert, Group Hug, bench recharge, and the Ice Wall comma. Unimplemented clauses were listed, not built. |
| Combat design ideas (2026-10-16) | Proposals only | [design pass](Combat/Design/COMBAT_DESIGN_IDEAS.md). Balance tweaks, new moves, new mechanics, and Cofi beam options. Open audit items stay open. | none — design only |
| Combat systems analysis (2026-10-16) | Analysis plus proposals | [systems analysis](Combat/Design/COMBAT_SYSTEMS_ANALYSIS.md). Chemistry coverage, AI targeting, pacing, and per-type identity. Open audit items stay open. | none — design only |
| Type gap proposals (2026-10-16) | Proposals only | [type gaps](Combat/Design/TYPE_GAP_PROPOSALS.md). Typed-pool rule. Node passive is Relay. Link Pulse and Open Line are withdrawn. Burned passive is Kindling at 6%, stacked with Rugged. Compressed support and Bot AP denial were not re-cut. Meme chemistry stays out. Open audit items stay open. | none — design only |
| Empty payload investigation (2026-10-16) | Findings only | [empty payloads](Combat/Audits/EMPTY_PAYLOAD_INVESTIGATION.md). Why `extend_protocol` and `network_sync` spend AP and do not apply the described effect. No code change. | none — investigation only |
| Combat issue tracker (2026-10-16) | Index only | [issue master](Combat/COMBAT_ISSUES_MASTER.md). One list of the audit and design items: fixed rows, open defects, calls still waiting, and the two approved proposals. | none — index only |
| Capture difficulty | Partial | [CAPTURE_DIFFICULTY_COMPOSITION_v1.md](Specs/Combat/CAPTURE_DIFFICULTY_COMPOSITION_v1.md) | Practice path for kits, traits, and the HP cliff. SQL assignment rows are still called out as follow-up in that spec. |

## Chainlings, capture, and encounters

| Mechanic | Status | Where it is documented | Where it lives in code |
|---|---|---|---|
| Authoritative wild capture | Rollout-gated | [PROFILE_FIRST_CAPTURE.md](Specs/Combat/PROFILE_FIRST_CAPTURE.md), [trading-trace capture](Architecture/AuthoritativeTradingTraceCapture.md) | `Chainbound.BattleService`, authoritative capture RPCs |
| Per-capture inscription NFT | Not implemented | [profile-first capture](Specs/Combat/PROFILE_FIRST_CAPTURE.md) | Standard capture does not mint an NFT. The old per-capture inscription spec was removed. |
| Local wild encounters | Implemented | [encounter return snapshot](Narrative/Encounter_Return_Snapshot.md) | `EncounterRollService`, zone encounter providers |
| Encounter live-ops modifiers | Partial | [ENCOUNTER_EVENTS_SUPABASE.md](Specs/Combat/ENCOUNTER_EVENTS_SUPABASE.md) | `get_encounter_events` |
| VRF wild encounters | Not the live path | [encounter return snapshot](Narrative/Encounter_Return_Snapshot.md), [beta allowlist](Release/BETA_FEATURE_ALLOWLIST.md) | Wild encounters use `EncounterRollService`. Program `request_encounter` / `fulfill_encounter` stay off the closed-beta player policy. The old VRF MVP spec was removed. |
| Party tab | Partial | [CHAINLINGS_PARTY_TAB.md](Specs/UI/CHAINLINGS_PARTY_TAB.md) | `PlayerMenuController`, Chainlings menu prefabs. The spec is still marked draft; party persistence is live. |
| PC terminal storage | Implemented | [CHAINLING_PC_TERMINAL.md](Specs/UI/CHAINLING_PC_TERMINAL.md) | `ChainlingPcTerminalScreen`, `profile_set_party` |
| Persisted HP | Implemented | [CHAINLING_HP_STATE.md](Specs/Combat/CHAINLING_HP_STATE.md) | Battle-outcome profile writes |
| PvE service record | Implemented | [CHAINLING_PVE_SERVICE_RECORD.md](Specs/Combat/CHAINLING_PVE_SERVICE_RECORD.md) | `profile_apply_pve_battle_outcome`, `profile_increment_chainling_metric` |
| Stars, mastery, Known grants | Partial | [ECONOMY_01](Specs/Economy/ECONOMY_01_EconomyAndProgression_v1.md), [progression grants](Specs/Combat/CHAINLING_PROGRESSION_GRANTS.md), [legacy level removal](Combat/COMBAT_LEGACY_LEVEL_REMOVAL_AND_BALANCE_BASELINE_PLAN.md) | Profile progression functions. Legacy combat level is documented as removed. |
| Archive / Bind to Chain | Partial | [archive encyclopedia](Specs/UI/CHAINLING_ARCHIVE_ENCYCLOPEDIA.md), [bindery contract](Specs/Economy/BINDERY_FOLIO_AND_PRESS_CHARGE_v1.md), [cover drops](Specs/Solana/CASE_BINDING_COVER_ART_DROPS_v1.md) | Program `bind_chainling`. Closed-beta policy does not expose it to the player build. |
| Competitive ladder and seasons | Not in this tree | [review copy](review-unimplemented/Specs/Combat/COMPETITIVE_LADDER_AND_SEASONS_v1.md) | `RankedLoadoutValidator` only |

## Ownership, items, and Solana

| Mechanic | Status | Where it is documented | Where it lives in code |
|---|---|---|---|
| Ownership spine (Known / Stamped / Bound) | Partial | [OWNERSHIP_SPINE_v1.md](Specs/Economy/OWNERSHIP_SPINE_v1.md), [Phase 1](Specs/Economy/OWNERSHIP_SPINE_PHASE1.md), [devnet report](Specs/Economy/OWNERSHIP_SPINE_PHASE1_DEVNET.md), [operator checklist](Specs/Economy/OWNERSHIP_SPINE_PHASE1_OPERATOR_CHECKLIST.md), [bootstrap design](Specs/Economy/OWNERSHIP_SPINE_PHASE1_BOOTSTRAP_DESIGN.md), [player terms](Specs/Combat/MOVE_TIER_PLAYER_FACING_v1.md), [asset taxonomy](Specs/Solana/CHAINBOUND_SOLANA_ASSET_TAXONOMY.md) | `stamp_known_move`, `roll_known_move`, `bind_chainling` in the Anchor program; `Supabase/functions/ownership_*`. Devnet report is NO-GO for turning the player flags on. |
| Closed-beta instruction gate | Implemented | [BETA_FEATURE_ALLOWLIST.md](Release/BETA_FEATURE_ALLOWLIST.md), [build sequence](Specs/Solana/CHAINBOUND_SOLANA_BETA_BUILD_SEQUENCE.md), [Seeker release gate](Release/SolanaSeekerBeta.md) | `ClosedBetaFeaturePolicy` allows profile/vault init and `update_position` only. |
| Second Stamp (add a mod to an already-Stamped move) | Not implemented | [audit 04](Specs/Security/EconomyAudits/04_Second_stamp_deferred.md) | No program instruction. The 150 RAM pin is documented and blocked. |
| Program IDs and IDL | Implemented | [CHAINBOUND_PROGRAM_REGISTRY.md](Specs/Solana/CHAINBOUND_PROGRAM_REGISTRY.md), [backend README](../backend/chainbound_game/README.md) | `backend/chainbound_game`, generated Unity contract |
| Wallet connect and profile accounts | Implemented | [WALLET_CONNECTION_SETUP.md](Guides/WALLET_CONNECTION_SETUP.md), [account model](Architecture/Account_Platform_Refactor.md), [menu confirm and account flow](Guides/MENU_CONFIRM_ROUTING_AND_ACCOUNT_FLOW.md) | `AccountSessionService`, `wallet_connect_verified`, `wallet_restore_profile` |
| Mobile wallet hardening | Partial | [MOBILE_WALLET_ACCOUNT_SESSION_HARDENING_PLAN.md](Specs/Solana/MOBILE_WALLET_ACCOUNT_SESSION_HARDENING_PLAN.md) | Client dependency-removal phase is marked done in that plan. Device security validation is still pending there. |
| Candy machine and library cards | Partial | [library card contract](Specs/Art/MOVES_LIBRARY_CARD_v1.md), [candy result plan](Specs/Art/LIBRARY_CARD_CANDY_RESULT_PLAN_v1.md), [art kit](Art/MovesLibraryCard/references/README.md), [render tool](../tools/library-card-render/README.md) | `CandyMachineScreen`, `LibraryCardResultPresenter`. On-chain vending stays gated. |
| Cosmetic reads | Partial | [asset taxonomy](Specs/Solana/CHAINBOUND_SOLANA_ASSET_TAXONOMY.md), [beta allowlist](Release/BETA_FEATURE_ALLOWLIST.md) | DAS cosmetic reads are allowed. The old adaptability findings spec was removed. |
| Item catalog and inventory ceremonies | Partial | [PLAYER_ITEM_CATALOG_AND_INVENTORY_v1.md](Specs/Economy/PLAYER_ITEM_CATALOG_AND_INVENTORY_v1.md), [use/inspect popups](Specs/UI/INVENTORY_USE_POPUP_CEREMONIES.md), [core bag pipeline](Specs/Solana/CORE_BAG_ITEM_INVENTORY_PIPELINE_v1.md) | `InventoryScreen`, `profile_get_item_inventory`, `profile_use_field_item`. Core burn builder is still deferred in the bag spec. |
| Profile tab | Partial | [PLAYER_MENU_PROFILE_TAB.md](Specs/UI/PLAYER_MENU_PROFILE_TAB.md) | Profile menu prefab. The spec calls this demo v1. |
| Economy security audit (2026-09-25) | Audit, not a feature | [audit index](Specs/Security/EconomyAudits/README.md) | Static review of RAM, Known rolls, Stamp, items, bind, and debug grants. |
| Solana stack audit (2026-09-11) | Audit, not a feature | [SOLANA_STACK_AUDIT_2026-09-11.md](Reports/SOLANA_STACK_AUDIT_2026-09-11.md) | Point-in-time. The program has grown since (ownership spine instructions). Read it with the Phase 1 docs. |
| PvP lobby, ranked escrow, tournaments | Not a shipped mode | [PvP 1](review-unimplemented/Guides/PVP_01_Social_Lobby_And_Casual_Matchmaking.md), [PvP 2](review-unimplemented/Guides/PVP_02_Ranked_Escrow_And_Ladder.md), [PvP 3](review-unimplemented/Guides/PVP_03_Tournaments_And_Themed_Brackets.md) | Client wager types live under `Assets/Scripts/Combat/Runtime/PvP/` and stay off the closed-beta player path. No lobby, escrow program, or tournament flow. |
| Ownership live-matrix run | Not executed | [devnet report](Specs/Economy/OWNERSHIP_SPINE_PHASE1_DEVNET.md) | The separate live-matrix plan file was removed. This report is still NO-GO for enabling the player flags. |

## World, narrative, camera, and shell

| Mechanic | Status | Where it is documented | Where it lives in code |
|---|---|---|---|
| System split | Implemented | [ARCHITECTURE.md](ARCHITECTURE.md) | Unity client, Supabase functions, optional Anchor program |
| Narrative sequences | Implemented | [authoring guide](Narrative/SEQUENCE_AUTHORING_GUIDE.md), [systems summary](Narrative/SEQUENCE_SYSTEMS_SUMMARY.md), [step GUID / execute once](Narrative/Sequence_Step_Guid_And_ExecuteOnce.md), [inventory of what exists](Narrative/Narrative_System_Inventory.md) | `SequenceRunner`, `Assets/Scripts/Narrative/` |
| Intro quest and new-game sequence | Implemented | [intro quest setup](Narrative/CHAINBOUND_INTRO_QUEST_SETUP.md), [intro sequence setup](Narrative/INTRO_SEQUENCE_SETUP.md), [new-game authoring](Narrative/NEW_GAME_INTRO_SETUP_GUIDE.md), [intro spec](Architecture/introsequencespec.md.txt), [admiral cliff beat](Narrative/chainbound_intro_admiral_cliff_sequence.md), [Loomville transition](Narrative/chainbound_intro_loomville_transition.md) | Sequence assets and quest steps. The setup guides say the systems are in place and the remaining work is authoring. |
| NPC control during sequences | Implemented | [claim and release](Narrative/NPC_Sequence_Claim_Release.md), [autonomy suspend](Narrative/NPC_Narrative_Movement_Autonomy.md), [NPC spec](Architecture/NPC_System_Spec.txt), [NPC setup](../Assets/Scripts/NPC/NPC_SETUP_GUIDE.md) | `Step_NPC_ClaimControl`, `NPCController` |
| Interactables | Implemented | [INTERACTABLE_SETUP.md](Narrative/INTERACTABLE_SETUP.md) | `Assets/Scripts/Interactable/` |
| Concurrent sequences | Partial | [CONCURRENT_NARRATIVE_ARCHITECTURE.md](Architecture/CONCURRENT_NARRATIVE_ARCHITECTURE.md), [authoring guide](Narrative/SEQUENCE_AUTHORING_GUIDE.md) | Main track plus child tracks. The later migration phases in that doc are not a separate manager. The broader V2 spec and the parallel-quest proposal were removed. |
| Profile narrative persistence | Implemented | [PersistentProfileNarrativeStateV1.md](Architecture/PersistentProfileNarrativeStateV1.md) | `profile_get_narrative_state`, quest and checkpoint functions |
| Bootstrap and loading | Implemented | [LOADING_SCREEN_SETUP.md](Guides/LOADING_SCREEN_SETUP.md), [bootstrap scene notes](../Assets/Scenes/BOOTSTRAP_SETUP.md) | `Assets/Scenes/Bootstrap.unity` |
| Main menu and mobile HUD | Implemented | [main menu](Guides/MAIN_MENU_SETUP.md), [menu input](Guides/MENU_INPUT_CONTEXT_CONTROLLER_SETUP.md), [HUD scene policy](Guides/MOBILE_HUD_SCENE_POLICY_SETUP.md), [movement pad and tactile audio](Guides/MOBILE_HUD_INPUT_AND_TACTILE_AUDIO.md), [app lifecycle](Guides/APP_LIFECYCLE_INTERRUPTIONS.md) | `Assets/Scripts/UI/MainMenu/`, `MobileControls` |
| Overworld camera, lighting, blur | Implemented | [3D camera](Camera/3D_CAMERA_SETUP_GUIDE.md), [perspective](Camera/PERSPECTIVE_CAMERA_GUIDE.md), [clipping](Camera/CAMERA_CLIPPING_FIX.md), [rig](Camera/CAMERA_RIG_FIX.md), [zoom](Camera/CAMERA_ZOOM_FIX.md), [depth blur](Camera/DEPTH_BLUR_SETUP.md), [darkening](Camera/DARKENING_FIX_GUIDE.md), [top-down lighting](Camera/TOPDOWN_LIGHTING_GUIDE.md), [unlit shadows](Camera/SHADOWS_WITH_UNLIT_GUIDE.md), [dialogue speaker camera](Camera/DialogueSpeakerCameraBridge.md) | `TopDownCameraController3D` and `Assets/Scripts/Camera/` |
| Window beams and dust | Implemented | [beam rays](Guides/BEAM_RAYS_SETUP.md), [dust motes](Guides/DUSTMOTE_MASKED_SETUP.md) | `Assets/Sprites/Shaders/` |
| Supabase connectivity | Implemented | [troubleshooting](Guides/SUPABASE_CONNECTIVITY_TROUBLESHOOTING.md), [Supabase README](../Supabase/README.md) | `Supabase/functions/` |
| Assembly layout | Implemented | [AssemblyModules.md](Architecture/AssemblyModules.md) | `*.asmdef` under `Assets/` |
| Clean-slate play mode | Implemented | [CleanSlatePlayModeHarness.md](Testing/CleanSlatePlayModeHarness.md) | Editor menu `Tools > Signal Jam > Testing` |
| Player-menu harnesses | Implemented as dev tools | [PLAYER_MENU_TEST_HARNESSES.md](Guides/PLAYER_MENU_TEST_HARNESSES.md), [production cutover](Guides/PRODUCTION_CUTOVER_AND_HARNESS_DISENGAGE.md) | Dev bootstrap and stub services |

## Folder map

| Folder | What belongs here |
|---|---|
| `Docs/` root | This index, [README](README.md), [architecture](ARCHITECTURE.md), [how to play](HOW_TO_PLAY.md) |
| `Docs/Guides/` | Setup for systems that exist |
| `Docs/Specs/` | Canonical combat, economy, Solana, UI, and security-audit specs |
| `Docs/Combat/`, `Docs/Narrative/`, `Docs/Camera/` | Authoring and presentation notes for those systems |
| `Docs/Architecture/` | Account model, assemblies, capture split, narrative persistence |
| `Docs/Release/` | Closed-beta allowlist and Seeker gate |
| `Docs/Art/` | Library-card render kit |
| `Docs/review-unimplemented/` | Specs and plans that are not what the code does |
| `Docs/GameTheory/` | Exploratory only. Not indexed as design. |
