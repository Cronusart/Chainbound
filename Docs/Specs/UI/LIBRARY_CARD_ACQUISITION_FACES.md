# Library card faces by acquisition

**Status:** Contract for Known-move presentation and Stamp bake  
**Related:** `Docs/Specs/Economy/OWNERSHIP_SPINE_v1.md`, `Docs/Specs/Combat/CHAINLING_PROGRESSION_GRANTS.md`

Rolled and learned moves are both player-facing **Known** moves. The instance records how it was acquired. Candy, the post-battle folio, and the Stamp NFT bake read that value and pick a face. They do not invent a third card.

## Acquisition

Stored on `ownership_known_moves.acquisition`.

| Value | Written by | Face |
|---|---|---|
| `candy_roll` | Candy Known roll. Also the default for rows created before this column. | Rolled |
| `event_grant` | Event / boss Known grant | Rolled |
| `progression_learn` | Battle star or mastery folio | Learned |

Missing or unknown values read as `candy_roll`. Stamp prepare reads the column on the Known row. The client does not choose the bake face.

A battle learn inserts one `ownership_known_moves` row with `progression_learn` and the grant idempotency key, and returns `known_id`. Replaying that key returns the same row and does not insert again.

## Faces

The square bake files are 1280×1280. Their center 640-pixel band matches the landscape UI file exactly. `tools/library-card-render/layout.json` stays the only grid. `content_y_offset` already centers that band on the square canvas. Do not shift rolled wells to fit the learned card.

| Face | UI sprite | Bake sprite | Ink |
|---|---|---|---|
| Rolled | `Assets/Sprites/UI/LibraryCards/librarycard_frontUI.png` | `Assets/Sprites/UI/LibraryCards/librarycard_front.png` | Sage `55, 78, 74` (`ink` in `tools/library-card-render/ink.json`) |
| Learned | `Assets/Sprites/UI/LibraryCards/librarycard_learned_frontUI.png` | `Assets/Sprites/UI/LibraryCards/librarycard_learned_front.png` | Rust `#9e4539` / `158, 69, 57` (`learned_ink`) |

Learned ink colors the move text and any modifiers later stamped onto that instance. Type stickers and rarity dots stay shared.

## Motion

- A candy roll keeps the stamp-press reveal and distressed sage glyphs.
- A battle learn dissolves clean rust glyphs onto the learned card: type, then the rarity dot, then the name and stats, with the same gap the candy Known roll uses between beats. Each beat flashes the rarity outline once.
- A later Stamp of a learned Known still uses the candy stamp-press. The host is already the learned face in rust. The new modifier ink is rust as well.
- Already published Stamp metadata is not re-uploaded.

Roll cost, Stamp RAM, VRF, and Known consume rules are unchanged.
