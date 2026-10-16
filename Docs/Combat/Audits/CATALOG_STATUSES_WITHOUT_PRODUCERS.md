# Catalog statuses with no producer

Date: 2026-10-16. No status in this file was given a producer, and no catalog text was rewritten. The question is whether the missing producer is an intentional cut or unfinished work.

## How this was checked

Public history for `Docs/Specs/Combat/Data/Statuses.csv` is two commits: `3a56ccc` (initial hackathon snapshot) and `d0c4cf6` (sync). Every status below is already in that first snapshot. `git log -S` on `Moves.csv` finds no commit that adds or removes `Rekt`, `Offline`, `GlitchedMind`, `HODLed`, `DustStorm`, `ForkedChain`, `CompressedData`, `MemeSurge`, `NodeRelay`, `Unsealed`, `BridgedOut`, or `Reinforced` as a move token. `Relay` appears in move names (`effect_relay`, `relay_packet`, `charged_relay`), not as a status effect.

The live chemistry chart is `COMBAT_Interaction_Matrix_v1.csv` (57 filled cells). `Reinforced` is absent. The legacy chart `COMBAT_Interaction_Matrix_v0.csv` has one Reinforced cell. `COMBAT_Chemistry.txt` §7 names the ids that cannot be newly authored: Firewalled, Halted, Crashed, LiquidityPump, Evasive, Bridged, Narrowcast, Overclocked, TypeCoating, HypedTypeCoating, and Amplified. None of the statuses below are on that list.

`COMBAT_CHEMISTRY_MATRIX_PASS_PLAN.md` records the v1 pass as done: one status per forward cell, Meme excluded from that pass, and the separate reaction-redesign suggestion file removed. The changelog has no entry that retires these catalog rows.

The shared timed-stage rule from this follow-up does apply if one of these is ever attached: apply-once and repeating stages are owned by the instance and revert when it expires. Refresh does not reapply apply-once stages. That rule was not a content change to these rows. HODLed's double ramp was not retuned.

## Per status

### Rekt

Unfinished. `COMBAT_Statuses.txt` §7.1 still sets the action-loss chance at 25%, and `CombatResolver` skips the action when the status is present. `Data/_LEGEND.md` uses `Rekt:2:0.25:SingleEnemy` as a format example, not as a live move. No type-profile row names Rekt. It was never in `Moves.csv` and it is not deprecated. The body is waiting for a move that was never authored.

### Offline

Unfinished, same shape as Rekt. §7.1 says it wakes on any damage. `StatusProcessor` and `CombatResolver` both have that wake. No move, no matrix cell, not deprecated.

### GlitchedMind

Unfinished. §7.1 sets mis-act at 50%, and `CombatResolver` has the mis-act branch. `COMBAT_TypeProfiles.md` lists GlitchedMind next to ExploitMark as a Glitched key status. ExploitMark has a move. GlitchedMind does not. The profile still treats it as part of the type fantasy, so the gap reads as missing authoring, not a cut.

### HODLed

Unfinished, and still named as design intent. Type profiles list it for Node, Block, Frozen, and Staked. `CHAINLING_ARCHETYPE_AND_RARITY_SLOTS_v1.md` uses it for the Block shield and the Staked long fight. The resolver ramps Defense if the status is applied: the repeating `MeleeDef:+1` plus an extra +1 while Defense is below +3, so the first end tick can reach +2. There is no stat-drop immunity. Nothing in the move list or v1 matrix applies it. The follow-up did not add a producer and did not change the ramp. If it is applied, both Defense writes now revert when the 3-turn status expires.

### DustStorm

Unfinished, and the effect body was never written. The catalog says "Field eva down on apply." No `StatusId.DustStorm` read exists in the resolver. It is not in the type-profile key column, not in a move, and not deprecated. This is an empty catalog row, not a chemistry cut.

### ForkedChain

Unfinished. The Forked type profile names ForkedChain beside Splinter. Splinter is a live v1 chemistry output (2 cells). ForkedChain has no resolver body and no move. The spread fantasy was given to Splinter; ForkedChain was left in the catalog and the profile.

### CompressedData

Unfinished. The Compressed profile names it as the key general status. The catalog stage is Speed -1 only. The summary also says Eva +2, which the stage column cannot store. No special resolver body. No move. If it is applied, the Speed drop now reverts with the 3-turn duration. Eva +2 was not added.

### MemeSurge

Deferred with the Meme kit, not deleted from a working move. The matrix pass plan says Meme is out of that pass and handled in a later wildcard pass. The Meme profile still lists MemeSurge, Staggered, and Surging. Staggered and Surging have move producers. MemeSurge does not, and there is no resolver body for the "random stat +1" summary. §7.3 still cites it as the random-stat example. Treat the missing producer as the unfinished Meme wildcard, not as a retirement.

### NodeRelay

Unfinished. The Node profile names NodeRelay beside HODLed and Immovable. Immovable has `immovable_stance`. NodeRelay has no resolver body. The moves `effect_relay` and `relay_packet` move an existing buff or debuff. They do not apply this status. The catalog line "Redirect healing" was never wired.

### Unsealed

Unfinished authoring against a spec that already promises a producer. `COMBAT_Statuses.txt` §10.1 adds Unsealed in the v1 revision and says it is "applied via support moves." `CombatResolver` consumes it for a 50% single-hit barrier bypass. No support move in git history of `Moves.csv` applies `Unsealed`. DamageIntercept, added in the same section, does have `damage_intercept` and `signal_redirect`. Unsealed was specified and coded, then never given a card.

### Relay

Same as Unsealed. §10.2 says it is the full barrier bypass for the next hit, and the resolver consumes `StatusId.Relay` ahead of Unsealed. No move applies the status. The word "relay" on Effect Relay, Relay Packet, and Charged Relay is a different mechanic.

### BridgedOut

Unfinished, and easy to confuse with a real cut. BridgedOut is a General instant, "Swap with ally," with no resolver body and no move. Bridged (a different id) is on the chemistry deprecated list in `COMBAT_Chemistry.txt` §7. BridgedOut is not. The swap fantasy was not moved onto another status in the commits that exist here.

### Reinforced

The live chart cut the only producer. The catalog row was kept. v0 cell Glitched → Mined is `Reinforced x3`. v1 has 57 cells and no Reinforced. `COMBAT_Chemistry.txt` §4 still lists Reinforced in the Structure family, and §7 does not deprecate the id. So the simplification pass dropped it from the matrix on purpose, and nobody then marked the catalog row retired or replaced that cell. If something applies it, Def +3 now reverts when the 3-turn status expires. This follow-up did not put Reinforced back on a cell.

## What this is not

The deprecated chemistry ids in `COMBAT_Chemistry.txt` §7 are the intentional authoring cut. They are a different list from the thirteen rows above. Leaving Rekt, Offline, and the rest in the live catalog, the type profiles, and §7.1 / §10 means a later pass still has to either author a producer or move the row onto the deprecated list. This follow-up did not choose that.
