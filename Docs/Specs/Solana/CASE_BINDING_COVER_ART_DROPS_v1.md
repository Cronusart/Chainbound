# Case Binding Cover Art Drops

**Status:** Canonical cover acquisition and Bind component contract  
**Ownership spine lock:** 2026-09-15  
**Audience:** Design, partnerships, Solana, Bindery UI, and economy

> **Superseded by Ownership Spine 2026-09-15.** Earlier drafts allowed off-chain/profile-only covers, attunement without consumption, or first-party covers that were not assets. A book cover used for Bind is now always a wallet-owned Metaplex Core asset and is consumed by the atomic Bind transaction. Partner acquisition may remain deferred; the Bind contract is not.

## Purpose

Define how cosmetic case-binding covers are acquired, verified, displayed, and consumed when publishing a Chainling as a Bound Archive book.

Cross-references:

- Bind recipe: `Docs/Specs/Economy/BINDERY_FOLIO_AND_PRESS_CHARGE_v1.md`
- Archive metadata/UI: `Docs/Specs/UI/CHAINLING_ARCHIVE_ENCYCLOPEDIA.md`
- Book mesh, cover atlas, and page turn: `Docs/Specs/UI/BOUND_ENCYCLOPEDIA_BOOK_PRESENTATION_v1.md`
- Core asset rules: `Docs/Specs/Solana/CHAINBOUND_SOLANA_ASSET_TAXONOMY.md`

## Player loop

```text
Acquire a Core cover
  -> server verifies wallet ownership and trusted collection
  -> preview it in the Bindery
  -> choose Chainling + cover + (Stamped move OR cosmetic skin) + RAM
  -> atomic Bind burns the cover and selected third Core asset
  -> Bound Archive Core is created with cover provenance and Chainling metadata
```

Covers are cosmetic only and never grant combat power.

## Canonical Core attributes

Every eligible cover must be a Metaplex Core asset in the configured Covers collection and include validated attributes:

| Attribute | Requirement |
|---|---|
| `cb.schema` | supported schema version |
| `cb.kind` | `book_cover` |
| `cb.cover_id` | stable catalog ID |
| `cb.artist_id` | stable artist/partner ID or `chainbound` |
| `cb.season_id` | optional drop/season ID |
| `cb.license_id` | approved commercial-use license record |

The configured collection and its update authority are authoritative. Creator strings, metadata names, or an open marketplace association are never sufficient eligibility gates.

## Non-negotiable verification rules

1. Unity is a display/cache client. An authoritative service and the program validate the owner, Core program/interface, exact collection, update authority policy, schema, and catalog ID.
2. Missing or malformed collection/authority configuration fails closed.
3. The wallet signing Bind must own the cover at instruction execution.
4. The cover must be distinct from the selected Stamped/cosmetic component and Archive output account.
5. A successful Bind burns the cover on-chain. Atomic rollback preserves it if any later burn or Archive creation fails.
6. Replaying the same Bind idempotency key cannot burn again or create a second Archive.
7. A cover cannot be reused after consumption.
8. Written commercial rights are required; token ownership alone is not an art license.

## Acquisition tracks

All tracks end in the same trusted Core Covers collection or in an explicitly configured partner collection accepted by the program.

### First-party baseline

- Chainbound-authored Core covers are the always-available path.
- They may be sold for RAM through an authoritative grant/mint service, earned in events, or airdropped.
- Acquiring or holding a cover requires a wallet; capture and Known progression do not.

### Partner drops

Mallow Gumball/Jellybean or another partner may distribute pre-minted Core covers. Partnership UI and draw transactions are optional integrations, not part of Bind itself.

Eligibility requires:

```text
wallet owns asset
AND exact asset collection is configured for the drop
AND Core update-authority policy passes
AND cb.kind == book_cover
AND cb.cover_id is in the curated catalog/allowlist
AND moderation and license records are active
```

Multi-artist drops use collection plus an asset/catalog allowlist; they never use creator address as the sole gate. Featured editions may use a dedicated trusted collection plus a small explicit 1-of-1 allowlist.

### Marketplace UX phases

| Phase | Behavior |
|---|---|
| P0 | Deep-link to the partner marketplace; refresh verified cover inventory on return. |
| P1 | Optional in-game transaction handoff with wallet review, simulation, and confirmation. |
| P2 | Reconciliation/webhooks for interrupted partner claims. |

Partner transaction building must remain isolated from the Bind transaction and cannot weaken the Bind ownership checks.

## Bind snapshot

The Archive metadata records the consumed cover rather than retaining a live dependency on it:

- `cb.binding_cover_id`
- `cb.binding_cover_asset`
- `cb.binding_cover_collection`
- `cb.binding_cover_artist_id`

The Archive image/URI may compose the approved cover art, but immutable provenance attributes must allow the catalog to reproduce or audit it. Burning the cover is the transfer of its binding utility into the Archive; it does not transfer copyright.

## Unity and service contract

- Cover inventory is returned as stable catalog IDs plus Core provenance.
- Refresh only on wallet connect/account change, Bindery open, explicit refresh, or confirmed transaction.
- Remote images are allowlisted, size-limited, type-checked, cached, and safely cropped before use.
- Bindery UI shows the selected cover, collection verification state, consumption warning, RAM cost, third component, cluster, fee payer, and final confirmation.
- Feature flags and configured collection IDs gate every live wallet action.

## Out of scope

- Commercial partnership terms, revenue shares, and exact drop odds.
- Treating a cover as a reusable equipment skin after it has been consumed.
- Off-chain/profile-only covers as a substitute for the required Bind component.
- Any Bubblegum/cNFT cover mint path.

## Acceptance criteria

- Bind without a cover account cannot be constructed from the IDL and cannot execute.
- A wrong-collection, wrong-owner, malformed, duplicate, or already-burned cover is rejected.
- A valid Bind consumes exactly one cover and records its provenance on exactly one Bound Archive.
- First-party and partner covers use the same verification and consumption contract.

