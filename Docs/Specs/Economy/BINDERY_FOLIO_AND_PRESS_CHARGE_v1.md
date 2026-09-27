# Bindery — Chainling Archive Binding

**Status:** Canonical destination split  
**Ownership spine lock:** 2026-09-15

> **Superseded by Ownership Spine 2026-09-15.** The earlier Press Charge catalyst, Bindery move press, Stamped-to-Bound move step, and Bound-move Archive gate are rejected. This filename remains for links and history; its canonical content is now the no-Press-Charge Bindery contract.

## 0. Destination split

| Destination | Owns | Does not own |
|---|---|---|
| **Academy candy machine** | Roll/grant Known moves; Stamp Known into one- or two-trait Stamped Core moves | Chainling publishing |
| **Bindery / Press** | Bind an unbound Chainling dossier into a Bound archive book | Move rolls, modifier rolls, move tiers, Press Charges |

World copy:

> The Academy teaches the move. The candy machine stamps its traits. The Bindery publishes the Chainling's archive.

## 1. Bindery recipe

The Bindery accepts exactly:

1. one owned unbound Chainling profile/PDA record;
2. one owned Core **book cover** from the trusted covers collection;
3. one owned Core **Stamped move** from the trusted moves collection **or** one owned Core **cosmetic skin** from the trusted cosmetics collection;
4. configured RAM;
5. the Chainling owner's wallet signature.

It outputs one Core **Bound Chainling** under the trusted archives collection.

The cover and chosen Core component are consumed on chain. RAM debit, both burns, archive create CPI, Chainling state update, and idempotency receipt must succeed atomically or all roll back.

## 2. Explicit exclusions

- No Press Charge item is required or consumed.
- No folio is stamped at the Bindery.
- No Stamped-to-Bound move operation exists.
- No two-modifier move receives a separate player name.
- No Bubblegum/cNFT mint is part of Bind.
- No Archive Binding gate checks for a `Bound move`.

Existing `press_charge` catalog rows may remain as retired/unobtainable migration data until a cleanup pass. New recipes, UI, drops, or quests must not award or require them.

## 3. UI contract

The Bindery panel contains:

- Chainling selector;
- book-cover selector filtered to verified owned Core covers;
- component selector with two filters: **Stamped Moves** and **Cosmetic Skins**;
- RAM balance/cost;
- output preview showing base skin and additive cosmetic behavior;
- transaction review with cluster, fee payer, game program, Core program, both consumed asset addresses, archive collection, and fees;
- one **Bind to Chain** action.

The panel shows no Press tab and no Press Charge counter.

## 4. Authority and security

- Chainling/profile owner must equal the signing wallet.
- Core asset owner must equal the signing wallet for both components.
- Cover collection must equal the configured trusted covers collection.
- Chosen component collection must equal either the trusted moves or cosmetics collection and must match the selected component kind.
- Cover and chosen component must be distinct accounts.
- Archive collection and Core CPI program IDs are fixed in the program configuration and checked on chain.
- Unity-provided metadata strings never replace authoritative Chainling fields.

## 5. Metadata handoff

The archive snapshot copies species, IVs/seed, entangled state/type, base visual variant, mutation IDs, and optional remaining Known move IDs from the authoritative Chainling account. It also records the consumed cover and chosen component addresses/kind.

Known moves listed in a Bound encyclopedia remain non-NFT profile/PDA entries and are not individually tradable.

## 6. Economy guardrails

- Bind RAM cost is a one-way publish sink configured separately from Stamp cost.
- Burning the two required Core components is part of the cost; their normal salvage payout is not also granted.
- A failed or retried Bind never double-burns, double-debits, or double-mints.
- Cover/Stamped/cosmetic acquisition and salvage tables must never create a profitable Bind loop.

## 7. Systems-first implementation

- Program recipe and collection IDs live in one ownership configuration account.
- Unity reads one feature-flagged `IOwnershipSpineService`; NPCs only open a mode.
- Selectors consume catalog/verified inventory DTOs, not scene-hardcoded IDs.
- Confirmed transactions trigger explicit refreshes; no per-frame RPC/DAS.
- Legacy VendingMachine/MoveMint/Rune/Bubblegum services remain disabled and are not dependencies.

## 8. Acceptance criteria

- [ ] A stranger describes the Bindery as `bind Chainlings into books`.
- [ ] Bind always requires Chainling + cover + (Stamped or cosmetic) + RAM + wallet.
- [ ] Missing/wrong assets fail before signing when detectable and fail on chain authoritatively.
- [ ] No Press Charge or Bound-move gate appears in UI, code contracts, or active recipes.
- [ ] Archive metadata includes the base variant and mutations/entangled fields.

## 9. Decision log

| Date | Decision |
|---|---|
| 2026-07-20 | Earlier Press Charge and Bindery move-press draft recorded. |
| **2026-09-15** | **Ownership Spine supersedes that draft: candy machine stamps moves; Bindery publishes Chainlings only.** |
| **2026-09-15** | **Press Charge removed from Stamp and Bind. Bound reserved for Chainling archive books.** |
