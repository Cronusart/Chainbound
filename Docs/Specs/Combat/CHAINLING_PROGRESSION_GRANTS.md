# Chainling Progression Grants

**Status:** Canonical Known-grant contract  
**Ownership spine lock:** 2026-09-15

> **Superseded by Ownership Spine 2026-09-15.** Progression rewards create non-NFT **Known** move instances only. Legacy `ordinal` names may remain in internal RPC/database identifiers during migration, but no grant mints an Inscription/cNFT, Stamped asset, or Bound move.

## Purpose

Star-up and mastery-cycle folio rewards are configured in Unity assets and persisted through Supabase. Post-battle XP/RAM grant amounts live in `PostBattleRewardsConfigSO`; star/mastery/rebirth thresholds and Cycle grants live in `ProgressionConfigSO`; progression folio odds live in `ChainlingProgressionRewardTablesSO`. See `Docs/Specs/Combat/LEVER_BOARDS_v1.md`.

Player-facing name for every granted move in this flow: **Known** (`MOVE_TIER_PLAYER_FACING_v1.md`). Known instances have zero modifiers, live in profile/PDA storage, and require no wallet.

## Config Assets

- Global gate tables: `Assets/Data/Combat/ChainlingProgressionRewardTables.asset`
- Per-species weighted pools: `SpeciesDefinitionSO.progressionMovePool`
- Each pool entry has `weight`. Enable `useWeightAtStarFive` and set `weightAtStarFive` when the 5-star odd should differ. `isSignature` marks the signature move; it does not change the odd by itself.
- Event overlays: `Assets/Data/Combat/ProgressionEvents/ChainlingProgressionEventCatalog.asset`

Legacy `learnableMoves` and `exclusiveMoves` remain for equip fallback. `SpeciesDefinitionSO.OnValidate` seeds `progressionMovePool` from those lists when the pool is empty.

## Roll Flow

1. Battle reward calculation derives previous/new stars from `masteryXp`, not from snapshot stars.
2. `StarProgressionUtility.DetectStarUps` and `DetectMasteryCycles` produce grant events.
3. `StarProgressionGrantService` rolls:
   - Gate: configured percent from reward tables plus enabled event patch bonus.
   - Pool: weighted pick from `ChainlingProgressionPoolResolver`.
4. `SupabaseProgressionGrantService` calls `profile_grant_progression_ordinal`.
5. A passed roll always inserts a fresh non-NFT Known `move_items` row with a stable instance ID and increments `pve_progression_moves_granted`. It never edits a loadout or mints an asset.
6. A failed gate records `NoFolioIssued`; it creates no item and awards no consolation RAM.

The combat router prepares one authoritative `BattleRewardSummary` before the report opens and starts grant persistence immediately. The report consumes that same object and presents XP first, followed by each star/mastery event and its resolved issuance outcome. It never recalculates rewards.

## Preview Odds

`MoveLearnPreviewUtility` exposes party-tab odds:

`P(move) = gatePercent * moveWeight / totalEligibleWeight`

Owned moves stay in the weighted denominator by default. Probability and ownership are independent (`12% · Owned ×2`) because repeatable copies are intentional. `excludeIfOwned` defaults to false and is reserved for explicitly authored one-time/event grants.

The Chainlings tab uses `Next-star folio chance: N%` below five stars and `Five-star mastery folio chance: N%` at five stars. Starter-exclusive moves remain eligible in their species pool and can be learned repeatedly. They equip only on that species.

## Capture Known Move

A captured Chainling rolls the same progression pool with the same star-gate percent as a folio at that star. A star-0 wild uses the first-star gate. A hit grants one Known move and equips it in loadout slot 0. A miss grants no move. The wild battle kit stays on the encounter zone and is not the capture grant.

`EncounterZoneSO.speciesDatabase`, `progressionRewardTables`, and `progressionConfig` point the roll at the live species pool and odds. `Supabase/migrations/20261001180000_capture_progression_known_move.sql` stops the server from substituting `baseline_jab` when the client sends an empty move id. That migration is not applied yet.

## Event Workflow

Create a `ChainlingProgressionEventSO`, add it to the catalog, and toggle `enabled`. Patches can apply globally or to a single `speciesId`, add event moves, add gate percent, or multiply existing move weights.

## Idempotency

Grant keys use battle identity plus Chainling id plus star/mastery event. Distinct keys may award the same move and create separate inventory copies. Replaying one key returns its originally recorded `move_item_id` and live `owned_copy_count` with `already_applied = true`; it never inserts or increments twice. A key collision with a different profile, Chainling, or move is rejected.

## Presentation

Each Known instance ID is the unit later selected for Stamp. Successful Stamp atomically removes only that instance; other copies of the same move definition remain Known and usable.

- First-time star crossings fill only the crossed star.
- At five stars, every completed mastery cycle sweeps all five stars once and then gives one restrained row pulse.
- Outcomes are `FolioIssued`, `NoFolioIssued`, `IssuancePending`, or `IssuanceError`.
- `ISSUANCE INTERRUPTED` is distinct from a normal miss.
- The glitch material is limited to the brief digital-to-analog materialization. The resolved folio uses paper, ink, a rarity seal, and archival copy.
