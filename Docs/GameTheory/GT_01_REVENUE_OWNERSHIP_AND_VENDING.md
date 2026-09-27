# GT-01 — Revenue, Ownership, and Vending (Game Theory)

**Status:** Exploratory game-theory report. **Not committed. Not a spec. Not architecture.**  
**Date:** 2026-08-06 (conversation) / 2026-08-13 (filed)  
**Folder rule:** See [README.md](README.md). Do not use this document in plans or builds unless the user explicitly asks to source from **this file by name**.

Prompting context: worry that the candy-machine / vending loop cannot be a primary revenue engine in blockchain gaming, because player ownership + secondary markets (including OTC NFT transfers) let players buy the finished move instead of paying to roll. Backdrop: [web3 games that shut down, paused, or left Web3 this cycle](https://x.com/StarPlatinum_/status/2084940203144224894).

Canonical live rules (if they conflict, they win): `ECONOMY_01`, `MOVE_TIER_PLAYER_FACING_v1`, Bindery folio / Press Charge (design intent, not implemented).

---

## 1. Question

The intended vending fantasy:

- Low-cost rolls
- Free daily rolls
- Paid extra rolls to chase rare / valuable modifiers onto already rare / valuable Known moves

The fear:

- On-chain ownership means a player can skip the factory and buy the finished Stamped / Bound move on a marketplace.
- Marketplace fees are easy to evade via OTC / wallet-to-wallet transfer.
- If “the asset is the product,” maybe the core thesis is inverted: digital collectibles should be valuable, but that same value might starve the roll loop.
- Pokémon still sells packs even though singles exist. What analog, if any, survives digitization?

---

## 2. Verdict (exploratory)

The fear is real, but it is aimed one layer too shallow.

**Secondary markets do not cannibalize issuance revenue**, because the studio is paid at **creation** (the roll / press), not at **trade**. OTC settlement of an already-minted asset does not unwind that issuance.

The threats that actually kill economies like this are:

1. **Supply accumulation** — digital goods are not lost, damaged, or thrown out; supply trends toward infinity and price toward zero unless there are sinks and rotation.
2. **Revenue that requires perpetual growth** — selling assets into an appreciating market makes P&L a function of new capital inflow. Growth stalls → revenue collapses → existing holders become adversarial because every new mint dilutes them.

Working reframe (not a product commitment):

> **Ownership is the moat. Consumables, services, seasons, and competition are the revenue.**  
> Assets-as-value is a strong retention and trust thesis, and a weak primary-revenue thesis.

Guest-first play (profile dossier capture, wallet later) is a larger strategic asset than a marketplace fee. Most shutdown titles in the linked cycle could not function without a wallet; their addressable market was crypto-native and their survival was tied to crypto sentiment.

---

## 3. Game theory: why roll when you can buy?

### 3.1 Equilibrium for a pure optimizer

If a desirable modifier / move combo has market price \(P_{market}\), roll cost \(C\), and success probability \(p\):

\[
P_{market} \approx \frac{C}{p} \times (1 - \delta)
\]

\(\delta\) is the seller’s discount (byproduct pull, impatience, junk inventory). Because \(\delta > 0\), **buying beats rolling** for anyone optimizing only for the finished outcome.

That instinct is correct: nobody who only wants *that specific card* should roll.

### 3.2 Why the factory still gets paid

Every listed item was rolled (or pressed) by someone. Studio revenue is closer to:

\[
\text{Issuance revenue} = \text{total rolls} \times \text{cost per roll}
\]

The market **redistributes outcomes** among players. It does not, by itself, reduce the number of factory events. OTC is irrelevant to that issuance P&L — those assets were already monetized at creation.

### 3.3 Residual value makes rolls *more* valuable, not less

In typical F2P gacha, a pull’s expected value is entertainment + in-game utility, with residual value ≈ 0.

With transferable ownership:

\[
E[\text{roll}] = \text{entertainment} + \text{utility} + \textbf{resale}
\]

The resale term should make a roll worth **more** than an equivalent closed-loop gacha pull, *if* the secondary market stays liquid and supply is regulated. The studio is the sole factory; the market is a distribution channel that can increase willingness to pay at the factory.

Many titles in the shutdown list sold **assets directly** instead of selling the **act of production**. That is a different (and more fragile) business.

---

## 4. Why Pokémon packs still sell (and what digital loses)

Reasons people rip packs instead of only buying singles:

| # | Analog | Survives naive digital? |
|---|--------|-------------------------|
| 1 | The rip is a separate entertainment product | Yes, if the ceremony is good |
| 2 | Print-run scarcity (sets go out of print) | **No, unless engineered** |
| 3 | Condition variance (PSA 10 vs raw) | **No, unless engineered** |
| 4 | Provenance (“I pulled it”) | Partial — can be recorded |
| 5 | Social content (breaks, streams) | Yes, if designed |
| 6 | Breadth (need many cards → boxes beat singles) | Yes, if pools are wide |
| 7 | Friction (shipping, fakes, wait, trust) | **No — digital removes this** |

Default digital gacha next to a liquid in-game market **loses** items 2, 3, and 7. That is why “just buy the move” feels inevitable.

Engineerable substitutes (ideas only — not committed):

- **Print / cosmetic grade on the pull** — collectible identity, not combat power. (Combat-stat variance on Known grants was previously judged a poor fit: it creates dud-rares and fights “rarity ≠ raw power.”)
- **Seasonal pools that retire** — “this pool closes and does not return” is the strongest *why roll instead of buy* for digital.
- **Provenance** — who pressed it, season, roll index, first-of-season marks.
- **Social** — public pull feed, streaks, season firsts.

---

## 5. What actually threatens this game’s loop

### 5.1 Supply without sinks

Live design already includes multiple acquisition surfaces (play grants, vending / dispenser, markets, events, capture, entangled boosts). Duplicate conversion, recycling, and inventory pressure are still thin in canonical docs.

If Stamped / Bound supply only grows, rolling becomes pointless regardless of marketplace fees.

### 5.2 Ponzi-shaped revenue

If P&L is “sell assets into a rising market,” then:

- Revenue depends on new buyers, not on play.
- Holders oppose new issuance (dilution).
- Sentiment crash zeros the business overnight.

That matches a large share of the linked shutdown pattern: blockchain layer first, game second; asset sales as growth, not as a factory plus a game people play without a wallet.

### 5.3 Taxing trades is the small, evadable slice

A % of marketplace sales is:

- Easy to route around (OTC, private transfers).
- Small compared with issuance, passes, and competition.
- Easy to over-index on because it *looks* like “web3 revenue.”

---

## 6. Core thesis: are in-game assets “the thing of value”?

**Half yes.**

| Thesis | Exploratory judgment |
|--------|----------------------|
| Bind-to-Chain / Bound encyclopedias / Stamped moves as **owned, lasting objects** | Strong for retention, trust, word-of-mouth, and willingness to pay **at issuance** |
| Those objects as **primary revenue** | Weak. Revenue should not require the asset to appreciate, and should not require a trade to complete |

Ownership lowers churn (sunk value + an exit option makes quitting costly and returning cheap). It should not also be asked to carry the P&L.

Chainling bindings remain a powerful *fantasy and prestige* layer. They do not have to be the cash register.

---

## 7. Candidate revenue surfaces (filter, not a roadmap)

Filter every candidate with: **can another player give this outcome without paying the studio?**

| Candidate | OTC-proof? | Notes |
|-----------|------------|--------|
| Season pass / “Archive Curriculum” | Yes (account-bound) | Typical large F2P line; academy fantasy fits. Must not sell combat power. |
| Ladder entry / tournament rake | Yes (a match is not tradable) | Crypto-native; ranked / seasons already exist as design surface. **Regulatory review required** before staked play. |
| Rebind / register fee when an asset is attached to a profile | Yes (studio controls utility, not custody) | OTC can move the NFT; it is inert until the game grants use. Small fee; must not make ownership feel rented. |
| Press Charges / stamp / bindery fees | Partly (studio is the factory) | Already the intended inscription-creation spend. Keep as engagement + issuance, not as the whole P&L. |
| Convenience (extra dailies, storage, presets) | Yes | Small, steady. |
| Cosmetics (account-bound vs tradable prestige) | Mixed | Bound cosmetics are OTC-proof; tradable cosmetics re-enter the same market problem. |
| Selling finished assets directly as the main SKU | **No** | Fragile; matches many failed web3 SKUs. |
| Marketplace take-rate | **No** | Evadable; do not plan the business around it. |

Vending / candy machine in this framing: **engagement engine and supply seeder**, not the sole cash register. Free dailies seed liquidity (which makes ownership feel real), build habit, and serve guests.

Possible vending *alterations* discussed (still not committed):

- Generous free dailies
- Seasonal exclusive pools with hard retirement
- Print grade / provenance on pulls
- Duplicate sink that does **not** violate `ECONOMY_01` (no pity that guarantees a specific rarity / modifier)

---

## 8. Guest-first as a differentiator

Profile-first capture (dossier + Known ordinal, wallet later) is called out as a larger strategic bet than marketplace tax:

- Addressable market is not capped at wallet-ready crypto natives.
- Survival is not identical to crypto-market sentiment.
- The game can be a game first; chain is ownership frosting when the player already cares.

Protecting that order of operations is more important than optimizing vending ARPU in isolation.

---

## 9. Risk flags if any of the above were ever sourced

- Staked ladder / rake: legal and geo risk; do not implement from this report.
- Season pass selling power: contradicts “rarity ≠ raw power” and pay-to-win heuristics.
- Rebind fee too high: ownership stops feeling like ownership.
- Free dailies too generous: market flood, residual value of rolls collapses.
- Combat-stat rolls on Known grants: previously rejected as a thrill substitute; collectible grade is the safer analog if ever explored.

---

## 10. Explicit non-commitment

This report does **not**:

- Change vending, RAM, Press Charge, or Bindery recipes
- Mandate season passes, rebind fees, print grades, or seasonal retirement
- Replace `ECONOMY_01` or move-tier specs
- Authorize marketplace, OTC, or wagering implementation

It is a thinking artifact. Promote an idea only by naming this file and writing a real spec afterward.
