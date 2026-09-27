# Ownership Spine v1 — Canonical Index

**Status:** Locked canonical ownership model  
**Ownership spine lock:** 2026-09-15  
**Audience:** Design, UI, combat runtime, Solana/Unity agents, backend

This document is the **index and lock statement** for Chainbound's ownership model. Detailed recipes live in the linked specs below. In any conflict between skills, code comments, or older drafts and this spine, **Ownership Spine 2026-09-15 wins**.

---

## 1. Lock statement

On **2026-09-15**, Chainbound locked the following ownership spine:

| Layer | Player term | Storage / standard | Wallet |
|---|---|---|---|
| Non-NFT move | **Known** | Profile / PDA | No |
| Tradable move | **Stamped** (1–2 mods) | Metaplex Core (`Chainbound Moves`) | Yes |
| Published Chainling | **Bound** archive book | Metaplex Core (`Chainbound Archives`) | Yes |
| Bind consumables | Book covers, cosmetic skins | Metaplex Core (trusted collections) | Yes |

Hard rules of the lock:

- **Known** moves are PDAs / profile rows — never minted as NFTs.
- **Stamp** consumes one Known instance + RAM and creates one **Stamped** Metaplex Core asset with **exactly one or two** modifiers. There is no later move tier.
- **Bound** means a **Chainling archive book only**. It is never a move tier.
- **Academy candy machine** = roll/grant Known **and** Stamp. Dual role; one destination for moves/modifiers.
- **Bindery** = Bind Chainlings into archive books **only**. No move rolls, no Stamp, no Press Charge.
- **No new cNFT / Bubblegum** mint for moves. Legacy Bubblegum paths are migration-only / disabled.
- **No Press Charge** for Stamp or Bind.
- Stamp **atomically consumes** the Known instance (no double-mint on retry).

## Phase 1 RAM pins

- Known roll: **5 RAM**.
- First Stamp (Known → Stamped): **75 RAM** unless the operator explicitly overrides it before
  configuration initialization.
- Second Stamp/add-mod on an already-Stamped move: **150 RAM**. This operation is not implemented
  by the Phase 1 program/server and remains blocked/deferred; the pin does not authorize inventing
  an instruction.

```text
Known (0 traits, PDA / non-NFT)
        |
        | Candy: Stamp + RAM + wallet
        v
Stamped (1–2 traits, Metaplex Core)

Bound = Chainling archive book (Core); never a move tier
Bindery: Chainling + cover + (Stamped OR cosmetic) + RAM → Bound Archive
```

---

## 2. Locked loop (summary)

1. **Capture / progress** → unbound Chainling dossier (PDA) + Known move grants (PDA). No per-capture mint.
2. **Candy roll** → fresh Known instance (non-NFT). May spend RAM; never mints NFT.
3. **Candy Stamp** → select one Known → consume Known + RAM → mint **Stamped Core** (1–2 mods via VRF/ticket). Atomic.
4. **Bindery Bind** → unbound Chainling + Core cover + (Core Stamped **or** Core cosmetic) + RAM → **Bound Archive** Core. Burns the two Core components.
5. **Trade / equip** → only verified Core ownership (server/program authority). Unity caches; never trusts client ownership lists for paid assets.

---

## 3. Canonical specs (updated for this spine)

| Spec | Role |
|---|---|
| [`Docs/Specs/Combat/MOVE_TIER_PLAYER_FACING_v1.md`](../../Combat/MOVE_TIER_PLAYER_FACING_v1.md) | Player-facing Known / Stamped / Bound language |
| [`Docs/Specs/Solana/CHAINBOUND_SOLANA_ASSET_TAXONOMY.md`](../../Solana/CHAINBOUND_SOLANA_ASSET_TAXONOMY.md) | Asset kinds, Core collections, attributes, no Bubblegum |
| [`Docs/Specs/UI/CHAINLING_ARCHIVE_ENCYCLOPEDIA.md`](../../UI/CHAINLING_ARCHIVE_ENCYCLOPEDIA.md) | Bound archive / encyclopedia presentation |
| [`Docs/Specs/Economy/BINDERY_FOLIO_AND_PRESS_CHARGE_v1.md`](./BINDERY_FOLIO_AND_PRESS_CHARGE_v1.md) | Bindery Bind-only contract (no Press Charge) |
| [`Docs/Specs/Economy/ECONOMY_01_EconomyAndProgression_v1.md`](./ECONOMY_01_EconomyAndProgression_v1.md) | Faucets, sinks, candy dual role, salvage, rebirth |
| [`Docs/Specs/Solana/CASE_BINDING_COVER_ART_DROPS_v1.md`](../../Solana/CASE_BINDING_COVER_ART_DROPS_v1.md) | Book-cover Core drops |
| [`Docs/Specs/Combat/CHAINLING_PROGRESSION_GRANTS.md`](../../Combat/CHAINLING_PROGRESSION_GRANTS.md) | Star / mastery Known grant tables |

Agents and skills must point here first, then to the row above for detail.

---

## 4. Superseded ideas (do not implement)

| Retired idea | Replacement |
|---|---|
| **Bound** as a move tier (`Known → Stamped → Bound move`) | Bound = Chainling book only; Stamped is the final move tier |
| **Press Charge** required for Stamp or Bind | No Press Charge; candy Stamp + Bindery Bind recipes only |
| **Bubblegum / cNFT Inscription** mints for moves | Metaplex Core Stamp CPI only |
| **Every-Known-as-NFT** / mint-on-grant | Known stays PDA / profile until Stamp |
| Ordinal → Inscription → Rune ladder as player terms | Known / Stamped (internal enums may migrate later) |
| Bindery as move press / folio stamp | Bindery publishes Chainlings only |
| Pity that guarantees modifier rarity/category | Pool + RAM + reroll access only |

Legacy code (`VendingMachineService`, `MoveMintService`, Bubblegum builders) may remain on disk **quarantined / disabled**. Do not re-enable ClosedBeta vending until a Metaplex Core Stamp path exists. Do not delete large bodies in drive-by cleanups.

---

## 5. Production checklist (short)

- [ ] Unity + skills use **Known / Stamped / Bound=Chainling** only in player-facing copy.
- [ ] Candy machine exposes **Roll Known** and **Stamp** (Core); Bindery exposes **Bind** only.
- [ ] Stamp CPI: consume Known + debit RAM + create Core move (1–2 mods); idempotent.
- [ ] Bind CPI: Chainling + cover + (Stamped \| cosmetic) + RAM → archive Core; no Press Charge.
- [ ] No new Bubblegum/cNFT mint paths for moves; legacy paths stay disabled.
- [ ] Cosmetic / cover / archive reads go through verified server or program checks (not raw Unity DAS trust).
- [ ] ClosedBeta / live vending **off** until Core Stamp path ships (separate P0).
- [ ] Salvage: Known via profile/PDA; Stamped only after on-chain Core burn; no self-funding Stamp loop.
- [ ] Skills and playbooks link this index; supersede Ordinal/Inscription player rules.

---

## 6. Decision log

| Date | Decision |
|---|---|
| **2026-09-15** | **Ownership Spine locked:** Known PDA → Stamp Core (1–2 mods) → Bound = Chainling books; candy dual role; Bindery Bind-only; no cNFT; no Press Charge. |
| 2026-09-15 | Docs/Specs listed in §3 updated to match this spine. Skills and legacy Bubblegum services aligned / warned in a follow-up pass. |

---

*Canonical index. Prefer this file over skill prose or legacy code comments when they disagree.*
