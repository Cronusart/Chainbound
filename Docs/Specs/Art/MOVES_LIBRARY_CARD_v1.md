# Moves Library Card v1

**Status:** Art system + render contract (no live mint, no Unity ownership flags)  
**Ownership spine lock:** 2026-09-15  
**Audience:** Art, inventory UI, ownership agents

This document is the render contract for the landscape **library card**: the player-facing image for Known (off-chain) and Stamped (Metaplex Core) moves. It is **not** a Known→Core mint path. Stamp remains the first on-chain image.

Cross-references:

- Ownership index: `Docs/Specs/Economy/OWNERSHIP_SPINE_v1.md`
- Prepare URI (no bucket required for current slice): `Docs/Specs/Economy/OWNERSHIP_SPINE_PHASE1_DEVNET.md`
- On-chain Core attributes (`cb.*`): `Docs/Specs/Solana/CHAINBOUND_SOLANA_ASSET_TAXONOMY.md`
- Kit: `Docs/Art/MovesLibraryCard/`
- Renderer: `tools/library-card-render/`
- Unity sprites: `Assets/Sprites/UI/LibraryCards/`

## Non-goals

- Do not run Gate A/B, Stamp Prepare/Execute, or flip Unity ownership flags.
- Do not change Solana programs or Edge Functions.
- Do not mint Known as NFTs.
- Do not replace candy-machine TMP fonts. PixeloidSans TMP SDF stays in-game only.

## Lifecycle

1. **Roll (Known)** — selects `bodyTemplateId`, move identity, and provenance `ROLLED BY`. No Core asset. Card may exist as an inventory PNG later; it is **not** an NFT.
2. **Stamp** — first Metaplex Core Moves mint (`CreateV2`). Prepare already embeds  
   `{METADATA_URI_BASE}/stamped/{asset}.json`.  
   The PNG/JSON at that URL may lag the mint. Execute smoke must not require the object to exist.
3. **Later stamps / transfers** — re-render the same URI (document hook only; no live updater in this slice).

```text
Known roll  ->  render input JSON  ->  compositor  ->  1280x640 card
Stamp prepare embeds URI (ix data)     compositor  ->  1280x1280 square + 1024 market alias
optional post-mint upload of JSON+PNG to Storage
```

## Canonical project fields

Do not invent combat fields. Map from these sources:

| Card field | Source |
|---|---|
| `moveId` / `moveName` | `MoveDefinitionSO.moveId`, `displayName` — `Assets/Scripts/Combat/Runtime/ScriptableObjects/MoveDefinitionSO.cs` |
| Type | `MoveDefinitionSO.baseType` (`ChainlingType`) — `Assets/Scripts/Combat/Runtime/Data/ChainlingType.cs` |
| Type icon | `TypeIconPaletteSO` asset `Assets/Data/Combat/Type/Icons/Type_Icons.asset`; sprites `Assets/Sprites/UI/{type}_type.png` (e.g. `staked_type.png`), typeless `Assets/Sprites/UI/typeless.png` |
| Power / accuracy | `MoveDefinitionSO.power`, `accuracy` |
| Category | `MoveCategory` in `Assets/Scripts/Combat/Runtime/Data/MoveEnums.cs` |
| Role/effect line | `MoveSelectDisplayUtility.FormatPowerOrEffectLabel` — `Assets/Scripts/Combat/Runtime/UI/UGUI/MoveSelectDisplayUtility.cs` |
| Accuracy copy | `MoveSelectDisplayUtility.FormatAccuracyLine` (`<= 0` → `AUTO` / `Accuracy Auto`) |
| Mods | Stamp `modifier1Id` / `modifier2Id` + `ModifierDefinitionSO.displayName` (CSV `Modifiers.csv` `sectionId`) |
| Font (bake into PNG) | `Assets/Sprites/UI/PixeloidSans.ttf` |
| Font (in-game TMP only) | `Assets/Resources/TextMesh Pro/Fonts/PixeloidSans SDF.asset` |

### Schema `category` vs `MoveCategory`

| `MoveCategory` | Schema `category` | Front layout |
|---|---|---|
| `DamagingMelee`, `DamagingRanged` | `attack` | `POWER` + `ACCURACY` numeric |
| `Passive` | `passive` | Omit POWER/ACCURACY; one **ROLE/EFFECT** line |
| `StatusUtility`, `Support` | `utility` | Same as passive |

**MoveSelectPanel difference:** combat UI still keeps a Power slot for non-damaging moves (`FormatPowerOrEffectLabel` writes `Heal` / `Buff` / `Utility` / `Passive` into that slot). The **library card is stricter**: those categories omit POWER/ACCURACY chrome and use a single ROLE/EFFECT line.

Effect-line vocabulary (from `FormatPowerOrEffectLabel`): `Heal`, `Buff`, `De-Buff`, `Utility`, `Passive`, `Support`.

## Layout grid (native 1280 × 640)

Source empty art is already 1280×640:

- `Assets/Sprites/UI/LibraryCards/librarycard_front.png`
- `Assets/Sprites/UI/LibraryCards/librarycard_back.png`

Sage body fill: RGB `(178, 186, 144)`. Rule/well stroke: `(146, 169, 132)`. Metal frame is the outer chunky chrome (do not stamp-distress the frame).

### Front

| Region | Pixel box (x, y, w, h) | Content |
|---|---|---|
| Outer frame | full card | Metal/pixel chrome; keep as-is |
| Type well outer | `70, 70, 180, 180` | Stroke; inner fill `80,80,160,160` |
| Type icon | centered in inner well | Nearest-neighbor scale from 21×21 type sprite |
| Move name | `280, 90` origin, width `920` | PixeloidSans, stamped ink |
| Name underline | y `210–219` | Existing rule on base PNG |
| POWER / ACCURACY or ROLE/EFFECT | y `250–400` | Attack: two labels + values. Else: one effect line |
| Stats rule | y `420–429` | Existing rule |
| MOD #1 | y `440–490` | Ghost if empty; `displayName` if filled |
| MOD #2 | y `500–540` | Same |
| Mods rule | y `540–549` | Existing rule |
| Barcode | y `560–600`, x `70–280` | Decorative placeholder on base PNG; do not encode IDs in v1 |

Ink treatment applies to **glyphs and POWER/ACCURACY/ROLE labels only**. Body stays flat sage.

### Back

Ledger is a two-column ruled block on `librarycard_back.png`.

| Region | Content |
|---|---|
| Left column rows | `PREVIOUS OWNERS` header + wallet short (`AbC...XyZ`) or username when provided |
| Events block | `ROLLED BY {actor}`, `STAMPED BY {actor}` when present |
| Reserved rows | `TRANSFERRED`, `BOUND` (leave blank in v1) |
| Footer glyph cluster | Keep as decorative chrome |

Event `type` values: `rolled` | `stamped` | `transferred` | `bound`.

## Output sizes

| Role | Size | Notes |
|---|---|---|
| Inventory master | **1280 × 640** | Front or back card |
| NFT / wallet square | **1280 × 1280** | Landscape card centered on sage pad |
| Marketplace `image` | **1024 × 1024** | Nearest-neighbor downscale of the square |

## Layers and naming

Kit root: `Docs/Art/MovesLibraryCard/`

| File | Role |
|---|---|
| `templates/card_front_base.png` | Empty front (copy of Unity empty card) |
| `templates/card_back_base.png` | Empty back |
| `templates/layers/frame.png` | Metal frame alpha |
| `templates/layers/body.png` | Sage fill |
| `templates/layers/type_well.png` | Type well stroke |
| `templates/layers/mod_slot_empty.png` | Ghost/dashed hint (optional overlay) |
| `templates/layers/mod_slot_filled.png` | Filled-row tint (optional) |
| `templates/layers/barcode.png` | Footer barcode crop |
| `templates/layers/ink_noise.png` | Stamp-noise overlay for glyphs |
| `fonts/README.md` | Path to TTF; do not duplicate the font |
| `examples/smoke_front_0mod.png` | Known-style attack, no mods |
| `examples/smoke_front_1mod.png` | One modifier |
| `examples/smoke_front_2mod.png` | Two modifiers |
| `examples/smoke_back.png` | Provenance back |
| `examples/smoke_market_1024.png` | 1024 square |

Unity copies live under `Assets/Sprites/UI/LibraryCards/` without deleting `librarycard_front.png` / `librarycard_back.png`.

## Render input

JSON Schema: `Docs/Art/MovesLibraryCard/schema/library_card_render_input.schema.json`

Minimum fields: `moveId`, `moveName`, `typeId`, `typeIconPath`, `category` (`attack|passive|utility`), optional `power` / `accuracy` / `effectLine`, `mods[]` (`slot` 1|2, `id`, `displayName`), `provenance` (`rolledBy`, `stampedBy?`, `owners[]`, `events[{type,actor,at}]`), `variant` (`bodyTemplateId`).

### Payload map — Known roll → render JSON

Known is profile/DB (`ownership_known_moves`), **not** an NFT.

| Render field | Known / client source |
|---|---|
| `moveId` | `ownership_known_moves.move_id` / `MoveDefinitionSO.moveId` |
| `moveName` | `MoveDefinitionSO.displayName` |
| `typeId` | `ChainlingType` name from `baseType` (e.g. `Staked`) |
| `typeIconPath` | `Assets/Sprites/UI/{type}_type.png` |
| `category` / `power` / `accuracy` / `effectLine` | From `MoveDefinitionSO` + `MoveSelectDisplayUtility` |
| `mods` | empty array |
| `provenance.rolledBy` | profile display name or wallet short |
| `provenance.events` | `[{ type: "rolled", actor, at }]` |
| `variant` | `library_card_v1` |

### Payload map — Stamp confirm → render JSON

`ownership_prepare_stamp` HTTP success does **not** include `image`, `metadataUri`, or `rolled_by`. The metadata URI is borsh-encoded in the prepare instruction (`ownership_spine.ts` `assetUri`).

`ownership_confirm_stamp` `/ asset` (`VerifiedStampedAsset`) supplies:

| Render field | Confirm / DAS field |
|---|---|
| `moveId` | confirm replaces on-chain u16 with DB string `move_id` |
| `mods[1].id` | `modifier1Id` |
| `mods[2].id` | `modifier2Id` when `modifierCount == 2` |
| `mods[].displayName` | look up `ModifierDefinitionSO` / `Modifiers.csv` |
| `provenance.stampedBy` | `ownerAddress` (short) |
| `provenance.events` | append `{ type: "stamped", actor: owner, at }` |

Do not treat off-chain JSON `attributes[]` as the same object as on-chain `cb.*`. On-chain remains `cb.move_id` (u16 hash string), `cb.modifier_1`, etc.

## Off-chain metadata JSON (Storage)

Example: `Docs/Art/MovesLibraryCard/schema/stamped_metadata.example.json`

URI already used by prepare:

`https://jhzqcbrzkausrrtcavuu.supabase.co/storage/v1/object/public/chainbound-ownership-metadata/stamped/{asset}.json`

Env: `CHAINBOUND_OWNERSHIP_METADATA_URI_BASE` (must be `https://…`). Current Phase 1 note: **bucket is not required for Execute smoke**. Upload may happen **after** mint.

Recommended Metaplex-friendly body:

- `name` — player-facing move name (not `Chainbound Stamped {u16}` long-term; on-chain CreateV2 name is still the program string until a later rename path)
- `image` — **1024×1024** public PNG URL
- `properties.files` — 1280×640 master + 1280×1280 square
- `attributes[]` display keys: `move_id`, `type`, `power`, `accuracy`, `mod_1`, `mod_2`, `rolled_by`, `stamped_by`

## Texture / type

- Distressed **rubber-stamp ink** on glyphs (noise + erode + dark red-brown). Visual reference only: do **not** import third-party stock alphabets (copyright).
- Body: flat sage. Frame: chunky metal/pixel from the authored PNG.

## Renderer

`tools/library-card-render/render.py` (Python + Pillow). Reads render-input JSON, composites templates, draws PixeloidSans from the Assets TTF, writes card / square / 1024 outputs.

```text
python tools/library-card-render/render.py --input fixtures/known_0mod.json --out-dir ../../Docs/Art/MovesLibraryCard/examples
```

## Next agent (do not do in this slice)

1. Build render JSON from `ownership_prepare_stamp` (decode URI from ix **or** reconstruct `{base}/stamped/{stamped_asset_address}.json` from the prepare response `stamped_asset_address`) plus Known row + `MoveDefinitionSO`.
2. After `ownership_confirm_stamp`, re-render with filled mods + `STAMPED BY`.
3. Optional: upload PNG + JSON to the public Storage prefix. **Still no Unity ownership flag flips, no Execute from this art pipeline.**
4. Inventory UI: bind 1280×640 sprites in the player menu; TMP remains PixeloidSans SDF in-engine.

## Success (this slice)

- This spec + schema on disk.
- Base templates + smoke 0-mod, 1-mod, back, 1024 market PNG.
- Real repo paths cited for PixeloidSans, power/accuracy/type, type icons.
- Stamp execute, Edge Functions, and Unity flags untouched.
