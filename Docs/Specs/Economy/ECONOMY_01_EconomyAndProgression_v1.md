# Economy And Progression v1.0

**Status:** Canonical ownership-spine economy  
**Ownership spine lock:** 2026-09-15

> **Superseded by Ownership Spine 2026-09-15.** Earlier vending recipes that produced an Inscription/cNFT, the proposed move-stamping Press Charge, and any `Known -> Inscription -> Rune -> Bound move` ladder are retired. The Academy candy machine rolls Known moves and Stamps a selected Known move directly as Metaplex Core. The Bindery publishes Chainlings only.

## Purpose

This spec defines the long-term baseline for move acquisition, modifier rolling, RAM sinks, star progression, mastery farming, rebirth, and salvage.

Canonical terms:

- **Known:** non-NFT move instance with zero modifiers. Internal `ordinal` identifiers may remain during migration.
- **Stamped:** wallet-owned Metaplex Core move with exactly one or two modifiers.
- **Bound:** published Chainling archive book. It is never a move tier.
- **RAM:** primary in-game currency and economy regulator.
- **Entangled:** extremely rare premium Chainling variant.

`Inscription`, `Rune`, `cNFT`, `Bubblegum`, and `Bound move` are not active player or implementation terms. Any legacy Press Charge catalog row is non-canonical and is not required by Stamp or Bind.

## Design goals

- Keep progression understandable and targeted rather than purely random.
- Preserve gacha excitement without fake pity or deterministic VRF overrides.
- Give RAM value across early, mid, and late game.
- Keep capture and basic progression wallet-free.
- Make every tradable transition intentional, atomic, and fee-light.
- Prevent acquisition, Stamp, and salvage from forming a self-funding loop.

## Ownership and wallet boundary

| Action | Result | Wallet required |
|---|---|---:|
| Capture a Chainling | non-NFT Chainling dossier/PDA | No |
| Learn, earn, buy, or roll a move | non-NFT Known instance | No |
| Stamp a Known move | one Stamped Core asset; Known is consumed | Yes |
| Acquire/hold a cover or cosmetic | Core asset | Yes |
| Bind a Chainling | Bound Archive Core asset | Yes |
| Trade or escrow a Core asset | wallet-owned Core asset | Yes |

Capture persists species, IVs/seeds, entangled state, base visual variant, mutations, and Known move IDs. It never performs a per-capture mint.

## Authoritative progression model

Stars (0–5), mastery cycles, and rebirth are the progression source of truth. Legacy numeric levels (1–100) are deprecated.

- **Stars:** XP fills star thresholds; each star-up may grant a Known move from the Chainling's source pool.
- **Mastery:** at star 5, overflow XP fills mastery cycles for additional Known/RAM rewards.
- **Rebirth:** resets stars, grants permanent IV allocation, and increases future XP requirements.

### Battle rewards

| Battle type | Completion RAM | Completion XP |
|---|---:|---:|
| Standard | 3 | 20 |
| Elite / trainer | 15 | 40 |
| Boss / milestone | 50 | 80 |

Battle and quest RAM count toward the 100/UTC-day earn cap. The daily gift's 10 RAM ([DAILY_LOGIN_REWARDS_v1](DAILY_LOGIN_REWARDS_v1.md)) is excluded from that cap, the same as purchased RAM.

Per-enemy XP defaults to 20 standard, 45 elite, and 90 boss. Participating Chainlings receive 100%; bench Chainlings present at battle start receive 50%. A newly captured wild Chainling receives no XP from its capture battle.

### Star-up Known grants

| Star reached | Standard chance | Entangled chance |
|---|---:|---:|
| 1 | 10% | 15% |
| 2 | 20% | 25% |
| 3 | 30% | 35% |
| 4 | 40% | 45% |
| 5 | 50% | 55% |

Rules:

- A successful roll creates a fresh Known instance with a stable instance ID.
- No single star-up guarantees a Known move.
- Source-pool targeting is the reason to progress specific Chainlings.
- Duplicate move definitions are allowed as distinct Known instances.
- Runtime odds live in `ChainlingProgressionRewardTablesSO`; source pools live in `SpeciesDefinitionSO.progressionMovePool`; event deltas use overlays.

### Mastery

At star 5, a mastery cycle costs 3000 base XP and must take longer than the final star step.

- Standard Chainling: 50% chance for one Known from its source pool; otherwise 12 RAM.
- Entangled Chainling: 55% chance for one Known; otherwise 12 RAM.
- Raise mastery XP before lowering the Known roll rate when farming becomes too efficient.

### Star and rebirth curves

| Step | XP required |
|---|---:|
| 0 -> 1 star | 450 |
| 1 -> 2 stars | 700 |
| 2 -> 3 stars | 1050 |
| 3 -> 4 stars | 1600 |
| 4 -> 5 stars | 2400 |

Future star and mastery requirements use `1.18 ^ rebirth_count`. Rebirth is capped at five and costs 1000, 1500, 2000, 2500, then 3000 RAM. Rebirth combat-stat rules remain owned by `LEVER_BOARDS_v1.md`; this ownership revision does not change them.

## RAM sinks

### Market

The market is a low-risk source of starter-friendly Known moves.

- Mostly typeless Common and Uncommon Known moves.
- No standard sale of finished Stamped moves.
- Refresh baseline: 12 hours.
- Common Known: 90 RAM.
- Uncommon Known: 150 RAM.

### Academy candy machine

The Academy candy machine is the one-stop move and modifier destination. The Bindery never rolls or Stamps moves.

#### Roll / grant Known

- Produces a fresh non-NFT Known instance with zero modifiers.
- Uses authoritative profile/PDA storage and does not require a wallet.
- May spend RAM according to the active vending balance board; it never mints an NFT.

#### Stamp Known

- The player selects one exact Known instance.
- The owner wallet signs one transaction that consumes the Known instance and RAM and creates one **Stamped** Metaplex Core move with exactly one or two compatible modifiers.
- Baseline cost: 75 RAM plus one Known and the network fee. The balance board may tune RAM, never the recipe.
- Modifier outcome comes from a verified ticket/VRF result. There is no pity that guarantees a specific rarity, category, or modifier after a fixed count.
- Known consumption, RAM spend, ticket consumption, and Core creation are atomic. A retry cannot mint twice and re-stamping the consumed instance fails.
- After confirmation, Unity removes the Known cache row and refreshes verified Core inventory immediately.
- One- and two-modifier results share the player term **Stamped**. There is no later move tier.

Baseline modifier rarity weights:

| Rarity | Weight |
|---|---:|
| Common | 45% |
| Uncommon | 30% |
| Rare | 17.5% |
| Epic | 5% |
| Legendary | 2% |
| Mythic | 0.5% |

The active vending balance board and authoritative result service must converge on these weights. Modifier rerolls, if enabled later, operate on an existing Stamped asset and never recreate the consumed Known instance or introduce another tier.

### Bindery

The Bindery is a Chainling-publishing RAM sink only. A Bind consumes:

1. one non-NFT Chainling;
2. one trusted Core book cover;
3. player choice of one trusted Core Stamped move or one trusted Core cosmetic skin;
4. configured RAM.

It burns the two chosen Core components on-chain and creates a Bound Archive Core asset containing authoritative Chainling fields. There is no Press Charge and no Bound-move gate.

### Salvage

Baseline returns:

- Known: 10 RAM through the authoritative profile/PDA path.
- Stamped: 20 RAM only after the Core asset is burned on-chain.
- Covers, cosmetics, or Bound books: only through an explicitly supported on-chain burn recipe.
- Unbound Chainlings: profile/PDA salvage only.

Hard guardrails:

- Buying Known and salvaging it must never generate positive RAM.
- Known acquisition, Stamp, and salvage combined must never self-fund repeated stamping.
- A client-submitted asset ID or burn claim is never sufficient for RAM credit.
- Idempotency keys prevent repeated credit for the same Known row or Core burn.

## Events and premium rewards

- Events may grant Known moves, cosmetics, rare reroll consumables, or horizontal Stamped rewards.
- Event-exclusive Stamped moves remain rare and horizontal in power.
- Entangled Chainlings retain a +5 percentage-point Known chance on star-ups and mastery cycles.
- Entangled capture may grant a premium Known or reroll-related reward, but still requires no wallet and never mints at capture.

## Economy guardrails and feel checks

- Routine battles pay RAM and XP first.
- Known moves stay more common than Stamped moves.
- Modifier access is spend-driven.
- If Stamping feels frustrating, tune pool width or RAM cost before adding deterministic outcomes.
- If mastery farming dominates, raise mastery XP or reduce RAM payout before reducing the Known chance.
- Players should be able to explain: candy means moves/modifiers; the press binds books; Known is non-NFT; Stamped is a one- or two-mod Core move; Bound means a Chainling book.

## Open tuning levers

- Battle RAM payouts and XP.
- Market prices and refresh timer.
- Known-roll and Stamp RAM costs.
- One-versus-two modifier probability and modifier rarity weights.
- Mastery XP and rebirth curves.
- Bind RAM cost and salvage returns.

