# Move Ownership and Player-Facing Terms v1

**Status:** Canonical ownership-spine source of truth  
**Ownership spine lock:** 2026-09-15  
**Audience:** Design, UI, backend, combat runtime, and Solana integration

> **Superseded by Ownership Spine 2026-09-15.** This revision supersedes every earlier section or cross-reference that described a three-name `Known -> Stamped -> Bound move` ladder, deferred wallet export, or a separate player name for two-modifier moves. In any conflict, this document and the 2026-09-15 ownership spine win.

## 0. One-sentence rule

> Players learn **Known** moves through play or the Academy candy machine, then **Stamp** a selected Known move into a wallet-owned **Stamped** Core asset with one or two traits. **Bound** describes a Chainling archive book only.

```text
Known move (0 traits, non-NFT)
            |
            | Academy candy machine: Stamp + RAM + wallet
            v
Stamped move (1 or 2 traits, Metaplex Core)

Bound = published Chainling archive book; never a move tier
```

## 1. Canonical terminology

| Player-facing term | Meaning | Authority / storage |
|---|---|---|
| **Known move** | Move with zero modifiers | Non-NFT profile/backend or game PDA entry. Internal legacy name: `Ordinal`. |
| **Stamped move** | Move with one **or two** modifiers | Metaplex Core asset in the trusted `Chainbound Moves` collection. Internal legacy rows may still say `Inscription` or `Rune`; both present as Stamped. |
| **Bound** | A Chainling published as an archive book | Metaplex Core asset in the trusted `Chainbound Archives` collection. |
| **Book cover** | Required consumable for Bind to Chain | Metaplex Core asset in the trusted covers/cosmetics collection. |
| **Cosmetic skin** | Optional wallet-owned visual variant and valid Bind component | Metaplex Core asset in the trusted `Chainbound Cosmetics` collection. |
| **Base skin** | Capture-time visual variant | Non-NFT Chainling data; copied into Bound metadata. |

### Hard language rules

- **Stamped** covers both one- and two-modifier moves. Show `Traits: 1` or `Traits: 2` when the distinction matters.
- **Bound** never labels, describes, badges, filters, or counts a move tier.
- Internal enum/database values may remain `Ordinal`, `Inscription`, and `Rune` during migration, but presentation must collapse them to Known or Stamped.
- Player UI must not say `Ordinal`, `Inscription`, `Rune`, `cNFT`, `Bubblegum`, `Core`, `Bound move`, `Inscribed`, or `Sealed`.
- Primary UI uses **Stamp**, not mint. Transaction review may explain that Stamp creates a wallet asset.

| Context | Known | Stamped |
|---|---|---|
| Badge | `KNOWN` | `STAMPED` |
| Tooltip | `Known Move` | `Stamped Move` |
| Trait line | `Traits: 0` | `Traits: 1` or `Traits: 2` |
| Inventory section | `Known Moves` | `Stamped Moves` |
| Primary action | `Stamp Move` | No tier-up action |

## 2. Acquisition and capture

Known moves come from:

- capture rewards;
- star-up and mastery grants;
- Academy candy-machine rolls/grants;
- other profile-authoritative reward sources.

Every Known grant creates a distinct non-NFT instance. Repeated move IDs are allowed and must still have distinct instance IDs for consumption and idempotency.

Capture persists the Chainling and any granted Known instance without asking for a wallet signature or minting any asset. The Chainling record contains at least:

- species ID;
- IV values and/or their authoritative seed;
- entangled state and entangled type;
- base visual variant ID;
- mutation IDs/flags;
- Known move IDs or instance references.

Player copy: `Validape learned Iron Tail. Added to Known moves.`

## 3. Academy candy machine

The Academy candy machine is the one-stop surface for moves and modifiers.

### 3.1 Roll or grant Known

- Output is a Known move with zero traits.
- It is written to profile/PDA state, not minted.
- This action does not require a wallet merely because the result is Known.
- RAM cost and real-randomness policy remain economy-tuned; there is no rarity pity that changes a committed result.

### 3.2 Stamp Known

Inputs:

1. one owned Known instance;
2. RAM;
3. a connected owner wallet;
4. an unused idempotency ticket tied to the Known instance and resolved randomness.

Output:

- one Metaplex Core asset in the trusted moves collection;
- player tier **Stamped**;
- one or two compatible modifier IDs;
- `cb.move_tier=stamped` and a `cb.modifier_count` of `1` or `2`.

The Known instance is removed in the same successful transaction as the Core create CPI. A Core failure rolls back the RAM debit and Known removal. A retry uses the same ticket/idempotency key and cannot create a second asset. Attempting to stamp the consumed Known instance fails on chain.

Result copy examples:

- `Iron Tail is now Stamped. Traits: 1.`
- `Iron Tail is now Stamped. Traits: 2.`

There is no Stamped-to-Bound move action. Two-trait Stamped results are still Stamped.

## 4. Authority and verification

| Concern | Authority |
|---|---|
| Known inventory and Chainling capture | Profile/backend or Chainbound PDA |
| Random stamp result | Prepared ticket created by the documented `result_authority` crank |
| Permission to consume Known and pay RAM | Owner wallet signature on Stamp |
| Stamped ownership and collection membership | Metaplex Core account plus trusted collection verification |
| Unity display cache | Non-authoritative cache only |

After a confirmed Stamp transaction, Unity must:

1. remove the consumed Known row from its cache;
2. refresh the profile/PDA move inventory;
3. load the new Core asset through a verified DAS/server read;
4. show success only when asset owner, collection, and `cb.*` attributes match the confirmed result.

If verification is delayed, show a pending/indexing state. Never restore a ghost Known row or fabricate a Stamped result locally.

## 5. Combat and inventory presentation

- PvE may equip Known or Stamped moves according to normal compatibility rules.
- Ranked may require Stamped moves, but reports `Stamp this Known move at the Academy first.`
- Any old `MoveTier.Rune` entry is displayed as **Stamped** with two traits.
- Remove Bound-move equip caps and Bound-move inventory sections. If a two-trait cap is still desired, name it by trait count in engineering rules, not as Bound.

## 6. Economy and salvage

- Baseline Stamp: one Known + configured RAM -> one Stamped Core asset.
- Known salvage is a profile/PDA mutation and awards less RAM than acquisition + Stamp can recycle.
- Stamped salvage is an on-chain Core burn followed atomically by bounded RAM credit.
- No buy/stamp/burn combination may self-fund repeated rolls.
- Press Charges are not part of Stamp or Bind to Chain.

## 7. UI acceptance criteria

- [ ] Candy UI contains **Known Roll** and **Stamp Move** paths.
- [ ] Stamp preview clearly says the selected Known is consumed and the result has one or two traits.
- [ ] Wallet connection is requested for Stamp, never for capture or holding Known.
- [ ] Confirmation drops the Known cache row and verifies the Core result.
- [ ] No player-visible Ordinal, Inscription, Rune, cNFT, Bubblegum, or Bound-move copy remains on the active candy surface.
- [ ] Bound appears only in Chainling archive-book contexts.

## 8. Decision log

| Date | Decision |
|---|---|
| 2026-07-09 | Earlier Known / Stamped / Bound-move ladder recorded. |
| **2026-09-15** | **Ownership Spine supersedes it: Known is non-NFT; Stamped is a one- or two-modifier Core move; Bound is Chainling archive only.** |
| **2026-09-15** | **Stamp creates Core immediately and atomically consumes the selected Known instance.** |
| **2026-09-15** | **Academy candy machine owns Known rolls and Stamp; Bindery owns Chainling publish only.** |
