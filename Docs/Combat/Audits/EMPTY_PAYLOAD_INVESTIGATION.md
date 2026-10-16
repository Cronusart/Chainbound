# Empty payload investigation: Extend Protocol and Network Sync

Date: 2026-10-16. Findings only. No move, importer, or resolver change.

The battle does not read `Moves.csv` at runtime. The editor importer reads `Docs/Specs/Combat/Data/Moves.csv` and writes `MoveDefinitionSO` assets. The resolver reads those assets. Both cards below are blank in the CSV, and the assets match the CSV.

## What the CSV says

Header, 43 columns, from `CombatCsvRoundTrip.MovesHeader` and the header row of `Docs/Specs/Combat/Data/Moves.csv`. Importer folder default is `_dataFolder = "Docs/Specs/Combat/Data"` (`Combat2DataImporter.cs` line 18). `ResolveCsvPath` joins that folder with `Moves.csv` (lines 1191–1193). `ImportMovesFromCSV` (line 1441) is the load.

Verbatim data rows:

```
network_sync,Network Sync,StatusUtility,Node,Common,AllAllies,None,0,2,0,1,1,0,,,UTIL|AOE,None,0,false,,All allies raise their lowest stage stat by +1.,,,,,,,,,,,,,,,,,,,,,,
```

`Moves.csv` line 65.

```
extend_protocol,Extend Protocol,StatusUtility,Node,Common,SingleAlly,None,0,1,0,1,1,3,,,UTIL,None,0,false,,Extend one ally buff by 1 turn. Skips the next 2 turns.,,,,,,,,,,,,,,,,,,,,,,
```

`Moves.csv` line 67.

| # | Column | `network_sync` | `extend_protocol` | What that cell means |
|---|---|---|---|---|
| 0 | MoveId | `network_sync` | `extend_protocol` | Asset name `Move_<id>`, and `definitionId` at runtime. |
| 1 | DisplayName | Network Sync | Extend Protocol | Name on the card. |
| 2 | Category | StatusUtility | StatusUtility | Not a damaging move and not a passive. `ResolveUtilityMove` handles it. |
| 3 | BaseType | Node | Node | Type for pools and chemistry on a hit. These are not hits. |
| 4 | Rarity | Common | Common | Availability. Not a power scalar. |
| 5 | Targeting | AllAllies | SingleAlly | Who `ResolveUtilityMove` walks. AllAllies is every living active, including the user. SingleAlly defaults to `FindAlly`, then the user if no partner is alive. |
| 6 | ContactType | None | None | No contact and no projectile. |
| 7 | Power | 0 | 0 | No damage. |
| 8 | APCost | 2 | 1 | Spent before the effect, by `TrySpendCost` (`CombatResolver.cs` line 1674). |
| 9 | Accuracy | 0 | 0 | Utility path does not roll this. On a damaging move, 0 means a forced hit. |
| 10 | HitMin | 1 | 1 | Unused while power is 0. |
| 11 | HitMax | 1 | 1 | Unused while power is 0. |
| 12 | RechargeTurns | 0 | 3 | After the action, a value above 0 applies `Recharging` (`CombatResolver.cs` lines 1748–1757). 3 skips the next 2 turns. 0 does not recharge. |
| 13 | StatusEffects | *(empty)* | *(empty)* | No status token. Format would be `StatusId:Duration:Chance` (`_LEGEND.md`). |
| 14 | StageChanges | *(empty)* | *(empty)* | No stage. Format would be `Stat:Stages:Chance:TargetsSelf`. Stat is a `CoreStat`, not "lowest". |
| 15 | Tags | `UTIL\|AOE` | `UTIL` | Labels. They do not apply a stage or extend a duration. |
| 16 | TriggerCondition | None | None | Not a passive trigger. |
| 17 | HPThreshold | 0 | 0 | Unused with trigger None. |
| 18 | TriggerOnce | false | false | Not once-per-battle. |
| 19 | HealPercentMaxHP | *(empty)* | *(empty)* | No heal. The importer stores 0. |
| 20 | Description | All allies raise their lowest stage stat by +1. | Extend one ally buff by 1 turn. Skips the next 2 turns. | Card text. The importer copies it onto `description`. Nothing parses it into an effect. |
| 21 | SelectionPolicy | *(empty)* | *(empty)* | Importer default `EffectOnly`. |
| 22 | ActiveConditionType | *(empty)* | *(empty)* | Importer default `None`. The move is not gated. |
| 23 | ActiveConditionThreshold | *(empty)* | *(empty)* | 0. |
| 24 | UtilityEffects | *(empty)* | *(empty)* | No opcode. This is the payload column. |
| 25–42 | Hp cost, resolve timing, resolve conditions, damage multipliers | *(all empty)* | *(all empty)* | No HP cost, no resolve condition, no damage multiplier. Blank timing columns take the importer defaults below. |

There is no `DesignNotes` cell. That column is optional and absent from this header.

## Why the payload does not run

The cells are empty in the file the importer reads. The parser does not drop a token, and the asset is not stale.

1. `ImportMovesFromCSV` maps columns by header name (`CombatMovesCsvSchema`, lines 14–64). `StatusEffects`, `StageChanges`, and `UtilityEffects` are known columns. `Description` is known too. A blank cell comes back as `""` (`CombatCsvColumnMap.Get`, lines 148–160).
2. `ParseMoveStatusEffects` returns an empty list when the string is blank (`Combat2DataImporter.cs` lines 1606–1609). `ParseMoveStageChanges` does the same (lines 1629–1632). `ParseUtilityEffects` does the same (lines 1646–1650). An unknown opcode would also be skipped (`Enum.TryParse` fails, line 1663), but these rows contain no opcode string to skip. This is not a typo and not an ignored column.
3. The importer writes that onto the asset (lines 1571–1577): `statusEffects`, `stageChanges`, and `utilityEffects`.
4. `Assets/Data/Combat/Moves/Move_network_sync.asset` lines 17 and 29–37: description is the CSV sentence, `statusEffects: []`, `stageChanges: []`, `utilityEffects: []`, `apCost: 2`, `targeting: 3` (`AllAllies`), `rechargeTurns: 0`. `Move_extend_protocol.asset` lines 17 and 29–37: description is the CSV sentence, the same three empty lists, `apCost: 1`, `targeting: 2` (`SingleAlly`), `rechargeTurns: 3`. Both sit in `MoveDatabase.asset` (lines 79 and 81).
5. `resolveProfile.primaryTiming: 1` and `utilityExecutionTiming: 0` on both assets are the blank-column defaults, not a skip flag. `ParseMoveResolveProfile` (lines 1865–1870) uses `MoveEffectTimingBucket.ResolveTimePreHit` (enum value 1, `MoveEnums.cs` lines 157–161) and `MoveUtilityExecutionTiming.UtilityOnly` (enum value 0, lines 171–173). The comment on that enum says utility and support moves still execute payloads in the utility resolver.
6. `CombatFactory.CreateMove` (`CombatFactory.cs` lines 94–156) copies those lists onto the instance. Empty stays empty. `definitionId` is the move id.
7. `ResolveUtilityMove` (`CombatResolver.cs` line 2486) special-cases `position_swap`, `ap_donation`, `ap_redistribution`, `ap_router`, `damage_intercept`, `signal_redirect`, `effect_relay`, and `relay_packet` (lines 2491–2542). These two ids are not in that set. Passive id branches (`turn_start_ap_gain`, `patience_passive`, and the others around lines 4251–4352 and 4871–4881) only run for passives, and only when `utilityEffects` is empty so the id is not blanked (lines 4251–4253). These moves are `StatusUtility`.
8. The generic path resolves targets, then `ApplyUtilityEffects` (line 2607). That calls `ApplyMoveStatuses`, `ApplyMoveStageChanges`, and `UtilityEffectExecutor.Execute` (lines 3174–3182). The executor returns immediately when `utilityEffects` is null or empty (`UtilityEffectExecutor.cs` lines 44–45). Empty stage and status lists add nothing.
9. After the utility resolve returns, recharge is applied if `rechargeTurns > 0` (lines 1746–1757). That is why Extend Protocol still locks itself. Network Sync has nothing left to do after the 2 AP spend.

`UtilityEffectType` (`MoveEnums.cs` lines 261–298) has no "extend a buff" opcode and no "lowest stage" opcode. `ChangeStage` needs a named stat. Filling the blank cell with the description sentence would not parse. The blank is the reason nothing runs today. A later implement pass still needs a new handler even after a payload is authored.

## Other sources

No localization table contains these names. Searched `Assets/` and `Docs/` for the display names. The card text is `MoveDefinitionSO.description`. `LibraryCardLayout.FormatStatsInk` prints that string as the effect line (`LibraryCardLayout.cs` lines 170–181). `CaptureCommandCoordinatorUGUI` uses the same field (line 1161). Players read the description. The battle does not perform it.

| Source | What it says | Agreement |
|---|---|---|
| Both `Move_*.asset` files | Same description as the CSV. Effect lists empty. Costs, targeting, and recharge match. | Matches the CSV. Not an asset drift. |
| `COMBAT_Resolution.txt` line 58 | Extend Protocol is Recharging(3) and skips the next 2 turns. | Matches the part that runs. Does not mention extending a buff. |
| `Docs/Guides/COMBAT_07_ImplementationGuide_v1.0.txt` line 79 | "Extend Protocol (Node): Recharging(3)". | Same. Recharge only. |
| `COMBAT_Moves.txt` | Neither name. | No second move spec. |
| `Docs/Specs/Combat/Data/_LEGEND.md` | Stage and utility formats. No row for these moves. | Does not define "lowest stage" or "extend buff". |
| `SpeciesMovePools.csv` line 5 | Validape's pool is `hub_heal`, `coordinated_shot`, `ap_router`, exclusives `hammer_sync` and `pack_rally`. | Neither move is learnable now. |
| `Validape.asset` design notes, lines 90–92 | "Exclusive moves: Validape Slam, Extend Protocol (Spec 06 v1.2)." | Disagrees with the pool. `validape_slam` is not a move id. The Extend Protocol guid `8ba9f3a0ca92c8e41a545987f5e408a1` is not on a species asset. |
| `combat_species_pool_routing_inventory.md` lines 59 and 88 | Both moves on Validape. Utility column "—". `EncodedSupport` full, `GapType` none, `FixClass` csv_only. Extend Protocol's summary still says "3-turn recharge." | Stale pool, and it marks a missing effect as no gap. The description in that row for Extend Protocol is the pre-decision sentence. |
| `UNIMPLEMENTED_MOVE_EFFECTS.md` lines 46 and 49 | Extend clause does not run; recharge does. Network Sync spends 2 AP on an empty payload. | Matches this investigation. |
| `BATTLE_MOVE_BALANCE_BY_RARITY.md` lines 274 and 276 | Quotes the descriptions as the move's effect and does not say the columns are empty. | Presents text that does not run. |
| `BATTLE_EFFECTS_CORRECTNESS_AUDIT.md` E-02 body, around line 143 | Still quotes an older Extend Protocol description, "3-turn recharge." | The decision table and the current CSV say "Skips the next 2 turns." The recharge value 3 did not change. |

## Did the correctness audit flag them?

Yes. They were not missed.

`BATTLE_EFFECTS_CORRECTNESS_AUDIT.md` E-19, "Described effects with no payload and no move-id branch" (line 398), lists `extend_protocol` (line 412) and `network_sync` (line 415). The decision table marks E-19 deferred (line 31) and says the recharge sentence was fixed and the extend clause was not implemented. The per-move options are `UNIMPLEMENTED_MOVE_EFFECTS.md` lines 46 and 49.

What that audit did: it compared the description to empty `StatusEffects`, `StageChanges`, and `UtilityEffects`, and it checked that the id is outside the hardcoded resolver set. That is enough to know the clause does not run.

What it did not do: it did not open the importer and show that the blank cells are copied through `ParseUtilityEffects`'s empty-string return, and it did not set the asset YAML next to the CSV row. A reader could still think a parser had eaten a payload. The asset and the CSV agree. The payload was never authored.

The species-pool inventory is the write-up that calls the same rows `GapType none`. That classifier treated "asset matches CSV" as no gap, including when both sides are empty and the description claims an effect.

## Same failure, other moves

Same test: `StatusEffects`, `StageChanges`, and `UtilityEffects` are all blank, power is 0, heal is blank, and `CombatResolver` has no move-id branch. The cast spends AP and applies no effect. Extend Protocol is the one that also recharges.

| Move | Also in the audit as |
|---|---|
| `accuracy_focus`, `evasion_maneuver`, `accuracy_disrupt`, `evasion_pin`, `gas_cloud`, `dust_fees`, `optimize_aim` | E-18. The stage column cannot name Accuracy or Evasion, and no utility opcode was authored either. |
| `ap_drain`, `transaction_tax`, `blockade`, `script_loop`, `system_reset`, `isolation_tool`, `viral_surge`, `wildcard_swap`, `hype_burst` | E-19, whole card does not run. |
| `status_resistance`, `extraction_hit` | E-19. Passives, so they are not spent as actions. No body runs. |
| `network_sync`, `extend_protocol` | E-19, this note. |
| `crystal_bash` | E-19 rider table, not the whole-card table. It is `DamagingMelee` at power 0, so the hit index is 0 and the Def-scaling text has no column. Same blank columns. |

No move outside that set has all three effect columns blank, power 0, no heal, and no resolver id branch. Hits that still deal their power, and heals that still heal, are the E-19 rider list (`all_in`, `recoil_strike`, `rally_cry`, and the rest). Their extra sentence is also missing from the payload columns. That is the same authoring hole on a card that already does something else. `system_reset` is in the whole-card set; the unimplemented doc says it spends 2 AP, and `Moves.csv` line 94 says `APCost` 3.

Hardcoded ids with blank utility columns are a different case. `ap_router`, `relay_packet`, `reactive_ping`, and the other ids in `ResolveUtilityMove` and the passive branches run from the move id, not from `UtilityEffects`. Empty columns there are expected.
