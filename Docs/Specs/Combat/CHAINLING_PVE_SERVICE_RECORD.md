# Chainling PvE Service Record

This spec defines the first Chainling service-record slice: PvE counters that can be written after battles, read by roster/menu flows, evaluated by badges, and exported later into Chainling NFT metadata.

## Metric Catalog

Metric IDs are stable public keys. UI, badges, persistence, and metadata contributors must use these IDs rather than database column names.

| Stat | Metric ID | Unit | Increment rule |
|---|---|---:|---|
| Total victories | `pve.total_victories` | count | `+1` for each Chainling that receives a PvE victory XP share. Flee does not count as a victory. |
| XP earned lifetime | `pve.xp_earned_lifetime` | XP | Add post-battle `ChainlingXpReward.xpEarned`. This never resets on rebirth. |
| Progression move grants | `pve.progression_moves_granted` | count | `+1` only when a level-up or star-up grant successfully awards a move. Vending, capture inscriptions, debug/manual grants, and trades do not increment this metric. |
| Damage dealt | `pve.damage_dealt` | HP | Sum damage events whose source instance maps to the Chainling. Includes direct, passive, DoT, and recoil damage when attributed to that source. |
| Damage taken | `pve.damage_taken` | HP | Sum damage events whose target instance maps to the Chainling. |
| Damage mitigated | `pve.damage_mitigated` | HP | Sum `WallInterceptPayload.damageAbsorbed` on blocked/mitigated events where the target maps to the Chainling. |
| Healing | `pve.healing_total` | HP | Sum `HealApplied` events whose source instance maps to the Chainling. |
| Enemy KOs | `pve.enemy_kos` | count | Count `ChainlingKO` events whose source instance maps to the Chainling and whose target is not a player Chainling. |

`pve.progression_moves_granted` deliberately avoids the phrase "moves learned" because a later collection stat can track every move a Chainling has learned or acquired, including vending/capture/manual sources.

## Increment Sources

Battle-sourced metrics are derived from `CombatEventLog` and `BattleRewardSummary` at battle end:

- `BattleStatsAggregator` reads legacy `CombatEvent` entries and produces per-Chainling combat deltas.
- The post-battle bundle merges XP rewards and victory credit.
- PvP results are skipped by this PvE service record.
- Fled battles do not grant victory credit, but combat totals may still be preserved when a result is persisted.

Non-battle sources use narrow recorders:

- `MoveLearnStatsRecorder.RecordLevelUpMoveGrantedAsync`
- `MoveLearnStatsRecorder.RecordStarUpMoveGrantedAsync`

Those recorders must only be called after the grant transaction/function succeeds.

## Supabase Contract

`chainlings` stores the PvE service record as additive columns:

- `pve_total_victories INT NOT NULL DEFAULT 0`
- `pve_xp_earned_lifetime BIGINT NOT NULL DEFAULT 0`
- `pve_progression_moves_granted INT NOT NULL DEFAULT 0`
- `pve_damage_dealt BIGINT NOT NULL DEFAULT 0`
- `pve_damage_taken BIGINT NOT NULL DEFAULT 0`
- `pve_damage_mitigated BIGINT NOT NULL DEFAULT 0`
- `pve_healing_total BIGINT NOT NULL DEFAULT 0`
- `pve_enemy_kos INT NOT NULL DEFAULT 0`
- `service_record_version SMALLINT NOT NULL DEFAULT 1`

The `profile_apply_pve_battle_outcome` edge function resolves the player profile, applies one idempotent per-Chainling stat event per battle, and updates Chainling progression (`mastery_xp`, `stars`) from the reward payload.

When present in the delta payload, the same RPC also persists end-of-battle HP (`current_hp`, `hp_synced_at`). See [`CHAINLING_HP_STATE.md`](CHAINLING_HP_STATE.md).

During the transition, roster reads continue returning legacy `ChainlingBattleStats`. Providers bridge:

- `battlesWon <- pve.total_victories`
- `kosScored <- pve.enemy_kos`
- legacy `battlesLost`, `timesKod`, and `timesCaptured` remain from existing columns until a broader stats view replaces them.

## Badges

Badges are data-driven `ChainlingBadgeDefinitionSO` assets:

- `badgeId`
- display text and optional icon
- `metricKey`
- `threshold`
- `battleModeFilter`

`ChainlingBadgeEvaluator` derives unlocked IDs from a `ChainlingPveServiceRecord` and badge definitions. Persisted unlock timestamps can be added later with a `chainling_badge_unlocks` table; v1 can derive on read.

## NFT Metadata Map

`IChainlingNftMetadataContributor` returns key/value pairs for future inscription JSON. The v1 PvE contributor maps service-record fields to snake_case JSON:

- `service_record_version`
- `pve_total_victories`
- `pve_xp_earned_lifetime`
- `pve_progression_moves_granted`
- `pve_damage_dealt`
- `pve_damage_taken`
- `pve_damage_mitigated`
- `pve_healing_total`
- `pve_enemy_kos`

Minting and inscription writes are out of scope for this spec. The interface is only the data handoff.

## UI Cross-Link

The Chainlings Party Tab **Stats** summary binds a compact career subset of this record (victories, enemy KOs, damage dealt/taken, lifetime XP, times KO'd) plus a dealt/taken balance bar. Milestone badges are **not** on the party summary — they belong to Archive / Full Stat Sheet drill-down.

See [`CHAINLINGS_PARTY_TAB.md`](../UI/CHAINLINGS_PARTY_TAB.md) §5.6.

Competitive / seasonal ranks are **out of scope** for this career record. Do not treat `pve.damage_*` as ranked skill. Groundwork: [`COMPETITIVE_LADDER_AND_SEASONS_v1.md`](../../review-unimplemented/Specs/Combat/COMPETITIVE_LADDER_AND_SEASONS_v1.md).
