# Inventory Use / Inspect Popup Ceremonies

Status: implemented (Pass 1 shell). Source catalog: `Docs/Specs/Economy/PLAYER_ITEM_CATALOG_AND_INVENTORY_v1.md` and `PlayerItemCatalogSeed`.

Goal: keep Inventory Use from dumping every detail on the list row, while offering **drill-down** popups that match the item ceremony. Prefer data-driven ceremony IDs over one-off item UI.

## Menu-facing shapes

| Ceremony ID | Purpose | Catalog families / examples | UI shape |
|-------------|---------|-----------------------------|----------|
| `party_target` | Pick one party Chainling, apply, show result | `HealSingle` (Chainling Patch / Update / Reformat), `StatusClear` (System Flush), `Revive` (Chainling Reboot) | Party rows + **EffectLabel** + dialogue + `TargetSlotFrame`. HP tween + family SFX on apply. |
| `party_aoe_confirm` | Confirm effect on whole party | `HealParty` (Party Patch) | Same party rows + **EffectLabel** + **PartyGroupFrame** (no per-row selection). Multi HP tween + SFX. |
| `self_confirm` | Confirm account/self consumable | `FavorBoost` (Favor / Strong / Peak) | Favor status breakdown + apply projection + dialogue. `FavorActivate` SFX. |
| `item_inspect` | Detailed drill-down (no consume) | Capture, Bindery, Archive/Core, Cosmetic | Portrait + rich body text. Opened via **Use** (inspect-only items) or **Detailed View** (any item). |

Shared shell: dim + panel chrome + scanlines + title + **EffectLabel** + dialogue strip; body content swaps by ceremony (`InventoryItemUsePopupController.OpenCeremony`).

Resolver: `InventoryItemCeremonyResolver` maps `PlayerItemEffectFamily` → ceremony ID.

## Effect label + SFX

- Effect label copy: `InventoryItemEffectCopy.FormatEffectLabel` (heal amounts, Favor duration, capture mult, RAM, etc.).
- Apply SFX by family: `HealTick` / `HealComplete`, `ReviveComplete`, `PartyHealComplete`, `FavorActivate` (`PlayerMenuSfxCue` + profile fallbacks).

## Inspect entry

- Capture / Bindery / Archive / Cosmetic: `usableInMenu = false` in seed; **Use** opens `item_inspect` (no RPC).
- **Detailed View** opens `item_inspect` for any selected item (read-only).

## Favor self_confirm fields

- Current Favor score + luck strength % (`FavorConfigSO.EvaluateCurve01`)
- Sources: Active Boost, Party grants, Accessories stub (“none / coming soon”)
- Apply: points, duration (refresh, not stack), projected total + luck %

## Mapping cheat sheet (`PlayerItemEffectFamily`)

| Family | Ceremony |
|--------|----------|
| `HealSingle`, `StatusClear`, `Revive` | `party_target` |
| `HealParty` | `party_aoe_confirm` |
| `FavorBoost` | `self_confirm` |
| `CaptureTool` | `item_inspect` (battle use) |
| `PressCharge`, `StampErase`, `ModifierReroll` | `item_inspect` → Bindery (soft CTA text) |
| `CaseBinding`, `BindingMutationRoll`, `UnbindBoundMove`, `ExtractBookMove`, `UnbindArchiveBook` | `item_inspect` → Archive/Bindery |
| `CosmeticOwned` | `item_inspect` → Style |

## Out of scope (this pass)

- Full Bindery / Archive / Style navigation from soft CTAs
- Real accessory Favor sources
- Final capture-odds tables
- Per-`itemId` unique audio clips beyond family cues
