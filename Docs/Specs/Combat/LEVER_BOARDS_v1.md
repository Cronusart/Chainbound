# Lever Boards v1 — Per-System Balance Config

Authoritative product/spec for Signal Jam 3D balance lever boards.
Implementation lives under `Assets/Scripts/Combat/Runtime/ScriptableObjects/` + `BalanceConfigRuntime`.

## Decisions

- **Per-system boards, no monolith.** One value has exactly one owning board; others reference it.
- **Rewards ≠ progression.** Post-battle grants and advancement curves are separate boards.
- **Vending rarity curve (ECONOMY_01):** Common 45 / Uncommon 30 / Rare 17.5 / Epic 5 / Legendary 2 / Mythic 0.5.
- **CSV SoT for catalog rows:** `Docs/Specs/Combat/Data/Moves.csv` + `Modifiers.csv` round-trip via `Combat2DataImporter`. Overlays are event/experiment patches only.
- **Server override for vending:** `get_vending_config` is authoritative when reachable; local SO is fallback.
- **XP threshold changes need no retroactive migration** (pre-production tuning).

## Ownership matrix

| Value | Owner |
|---|---|
| Tier roll odds, vending costs/caps, event favor boost | `VendingConfigSO` |
| Modifier min rarity, effect ranges, within-tier weight | `Modifiers.csv` → `ModifierDefinitionSO` (+ optional `ModifierBalanceOverlaySO`) |
| Battle formula/flow (AP, damage, mitigation, stages, status procs) | `CombatResolverConfigSO` via `CombatRulesetSO` |
| Post-battle KO/completion XP, RAM, XP shares | `PostBattleRewardsConfigSO` |
| Ordinal gate % on star/mastery | `ChainlingProgressionRewardTablesSO` (referenced by rewards board; not duplicated) |
| Star XP thresholds, mastery, rebirth costs, Cycle flat+%, move-learn rarity weights | `ProgressionConfigSO` |
| Passive Chainling HP recovery interval, MaxHP fraction, catch-up cap, scope | `ChainlingRecoveryConfigSO` |
| Favor soft curves | `FavorConfigSO` (unchanged) |
| Recoil / DoT percents | StatusDef assets (unchanged) |
| Encounter species/odds | `EncounterZoneSO` / `EncounterAreaSO` (reference granularity; unchanged) |
| Capture trace physics, HP/IV/Alertness stacking | `CaptureBalanceConfigSO` |
| Capture volatility kits (Calm–Extreme) | `CaptureProfileCatalogSO` (referenced by the capture balance board) |
| Capture board look (candles, dashes, copy) | `CapturePresentationConfigSO` (presentation only) |
| Wild intro copy, trait order, trait colors | `BattleIntroCopyConfigSO` |
| World-presence lifetime prefixes and hydrate/rewind policy | `WorldPresenceConfigSO` (narrative / Assembly-CSharp; not Combat.Runtime) |

## Boards

### 1. CombatResolverConfigSO
AP, random roll, crit, progression damage factor, chemistry factors, STAB (`enableStab` default **false** to preserve current behavior), stage caps, mitigation/barrier exclusivity, status proc chances, `turnActionTimeLimitSeconds` (0 = disabled; UI enforcement is a separate feature).

### 2. CombatRulesetSO (profiles)
`rulesetId`, `resolverConfig`, stat mutators, roster eligibility (allowed types, required/banned moves).
Shipped defaults: `Assets/Data/Combat/Config/Ruleset_PvE_Default.asset`, `Ruleset_PvP_Ranked.asset`.

### 3. PostBattleRewardsConfigSO
What is granted after battle. Does **not** own XP thresholds.

### 4. ProgressionConfigSO
How/when a Chainling advances. Cycle grants are flat **and** percent of species base (additive). Move-learn rarity weights multiply pool entry weights.

### 5. VendingConfigSO
ECONOMY_01 rarity weights (basis points, `generatorVersion`), costs, daily cap, item-class table, server-gated `eventFavorBoost` (default 0).

### 6. ChainlingRecoveryConfigSO
Passive health recovery: `intervalSeconds`, MaxHP fraction per completed interval, `maxCatchupIntervals`, and storage-only scope. Default: 10% MaxHP every 30 minutes for Chainlings in PC storage, capped at 48 elapsed intervals per resolver read. Equipped party members do not recover. The matching server board in `Supabase/functions/_shared/chainling_recovery_config.ts` is authoritative; Unity's `ChainlingRecoveryConfigService` fetches it for UI/presentation only.

The resolver runs lazily before authenticated `profile_get_chainlings` and `profile_get_party` responses. It prefers a finalized Solana block time, walking back from the tip slot when that slot has no timestamp, then falls back to server UTC. Both clocks are the real 24-hour day; the player device clock is not accepted. It locks the affected storage rows, caps recovery at server-derived MaxHP, and does not revive KO Chainlings. A missing clock does not block login or combat.
Depositing a Chainling into storage starts its recovery anchor at that clock. Editing the party does not reset anchors for Chainlings that were already in storage, so stored time is not discarded. Time spent equipped does not convert into healing.

### 7. ModifierBalanceOverlaySO (event layer)
Per-`sectionId` overrides + optional tier-weight override. Never exported to CSV. Overlay id folded into config stamps.

### 8. BattleIntroCopyConfigSO (presentation copy)
Wild/trainer/PvP intro templates, trait order (`mutated` → `entangled` → name → `variant`), labels, and per-trait color sources. Mutated uses a `VisualTierPaletteSO`; entangled uses `ChainlingTypeDisplaySO` badge colors; variant uses Cycle V / Max Cycle `VisualTierProfileSO`. `BattleIntroPresentationSO.copyConfig` overrides the board when assigned.

Shipped default: `Assets/Data/Combat/Config/BattleIntroCopyConfig_Default.asset`.

## Registry

`BalanceConfigService` (GameServices) + static `BalanceConfigRuntime` (FavorRuntime pattern).
Fallbacks: injected → service → `CreateRuntimeDefault()`.

## Config stamping (fairness)

On `CombatResolver.InitializeBattle`, forensic event `ConfigStamp` records `rulesetId`, versions, hashes.
Also stamped onto `BattleResult`, `CombatReplayData`, forensic JSON export, and Supabase `config_hash` (migration `20260802201500_battle_outcome_config_hash.sql`).

## Vending server contract

- Edge: `Supabase/functions/get_vending_config`
- Shared constants: `Supabase/functions/_shared/vending_config.ts`
- Client: `VendingConfigService` → `BalanceConfigService.SetVendingOverride`
- Roll results echo `configVersion` / `configHash` / `generatorVersion`
- **Known trust gap (documented, out of scope for on-chain this pass):** client precomputes rarity/modifier into Anchor ix args; on-chain vending ixns are still stubs. Future verifier must re-derive `sha256(vrf || context) % basis` against config version X.

## Tools

| Menu | Purpose |
|---|---|
| Tools/Chainbound/Combat/Validate Balance Boards | OnValidate-style scan of board assets |
| Tools/Chainbound/Combat/Data Importer | Import + **Export** Moves/Modifiers CSV |
| Tools/Chainbound/Combat/Moves Modifiers Balance Board | Inspector-style edit → export CSV → re-import |
| Tools/Chainbound/Combat/Odds Simulator | Monte Carlo tier/modifier distributions |
| Tools/Chainbound/Combat/Capture Balance Simulator | Monte Carlo trading-trace lock rates vs HP/profile/orb |
| Tools/Chainbound/Capture/Ensure Default Lever Boards | Create default capture balance/profile/presentation assets |

## Unwired / authoring-only levers

These remain in `CombatConstants` or StatusDefs and are **not** on resolver boards (anti-duplication):

- `AP_COST_*` bands (authoring guidance)
- `DEFAULT_ON_HIT_CHANCE` / `DEFAULT_ON_CRIT_CHANCE` (unused by runtime)
- Recoil constants (`OVERLEVERAGED_*`, `FOMO_SELF_DOT_HP`) — FOMO uses StatusDef `endOfTurnDamagePercent`
- In-battle turn timer enforcement (lever exists; UI flow not shipped)

## Anti-duplication rule

New tunable systems expose levers via a **per-system Config SO** registered in `BalanceConfigRuntime`. One board owns each value. CSVs stay SoT for catalog rows; overlays are patches, not forks.

Narrative world-presence lifetime/hydrate policy is a **separate board**: `WorldPresenceConfigSO` (`Assets/Resources/Narrative/WorldPresenceConfig.asset`), accessed through `WorldPresenceConfigRuntime.Current` on `ProfileWorldFlagStateService`. Combat.Runtime cannot reference Assembly-CSharp, so this board is not registered in `BalanceConfigRuntime`. Presence **content IDs** stay on `WorldPresenceBinding` and sequence steps, not on the board.
