# Pixel Battle VFX System

## Purpose

Pixel Battle VFX applies a virtual low-resolution UV grid to an existing source texture. It does not require duplicate low-resolution art. The shader quantizes local quad or particle UVs before applying `_MainTex_ST`, so sprite atlas frames and `CombatMoveVFX` sequences remain compatible.

The combat implementation uses:

- `PixelBattle_CombatStyle.asset` for the shared base pixel-cell count.
- `M_PixelBattle_Alpha` and `M_PixelBattle_Additive` for render state only.
- one `PixelBattleVfxPresetSO` per visual role for color, emission, grid multiplier, alpha, dissolve, and optional source override.
- `PixelBattleVfxApplicator` on a renderer or prefab child to assign the material and property block.

## Authoring

1. Pick the alpha or additive shared material.
2. Create or duplicate a preset and assign `PixelBattle_CombatStyle`.
3. Set `pixelCellMultiplier`: lower values create chunkier blocks; higher values preserve more texture detail.
4. Leave `overrideSourceTexture` off for sprite sequences, particle sheet animation, and flipbooks. Their renderer-provided texture remains the source.
5. Enable the override only for a static particle or mesh effect that intentionally owns its source shape.
6. Tune tint, emission, alpha cutoff, and optional dissolve in the preset. Render queue, blending, and depth test belong to the shared material.

`PixelBattleVfxApplicator` makes source ownership explicit. Its **Source Texture Authority** is `Preset Override` for normal static effects, or `Renderer` when an author wants to preserve the renderer source. A renderer with a source owner, such as `CombatMoveVFX` with a sprite sequence, always preserves its live frame regardless of that applicator setting. The applicator applies the shared pixel style, while the sequence writes `_MainTex` and `_MainTex_ST` through its own property block.

Do not use the shared material's texture field to diagnose an animated effect: it remains a static fallback. Select the spawned `CombatMoveVFX` clone during playback and inspect **Runtime Sprite Playback**. **Authored Texture** is the selected frame asset, while **Applied Slot Texture** is the texture actually present in the renderer's indexed material property block. Those values should match during sequence playback.

`CombatMoveVFX` and `PixelBattleVfxApplicator` must target the same material slot. This is important because Unity treats renderer-level and indexed material property blocks as different scopes, with the indexed block taking precedence. Sprite billboarding is enabled per move prefab; live combat and the Animation Previewer provide their active camera while preserving the child quad's authored local rotation.

For a frame-authored `CombatMoveVFX` sequence, set **Sprite Size (World Units)**, **Sprite Canvas Size (Pixels)**, and **Sprite Aspect Mode** on the prefab component instead of compensating in the Pixel Battle material. `Sprite Size` is the base width/height envelope in world units before phase/root scale; a zero component falls back to the child renderer's authored local scale. `Sprite Canvas Size` is the shared pixel canvas for the sequence, so all frames resolve the same aspect even if a future importer reports a different frame rect. `PF_PixelBattle_StrikeSlash` explicitly uses `96 x 69` for its authored frames. `Stretch` preserves a fixed mesh exactly. `Preserve Authored Height` keeps the configured height and derives width from the canvas. `Preserve Authored Width` does the inverse. `Fit Inside Authored Bounds` contains the canvas inside the configured envelope. These settings do not crop transparent padding or normalize opaque content; they affect only the sprite-renderer child. Phase scale, Travel scale curves, offsets, and billboard rotation continue to operate normally on their existing owners. Runtime code can use `SetSpriteSize`, matching `CombatChainlingVisual.SetSpriteSize`, and `SetSpriteCanvasSize` for pixel-space metadata.

The shader's **Pixel Cells** value is the horizontal virtual-pixel density. The shader derives the vertical cell count from **Pixel Canvas Size**, keeping virtual pixels square on rectangular canvases. For the 96 x 69 strike canvas, a 64-cell horizontal grid becomes approximately 64 x 46 cells instead of a distorted 64 x 64 grid. `CombatMoveVFX` writes the explicit canvas and frame UV transform into its indexed property block every time it changes frames. `SpriteFlipbookVfxPlayer` does the same and exposes a shared `spriteCanvasSize` field for padded or texture-sheet frames. When no animated source owner exists, `PixelBattleVfxApplicator` infers the canvas from the selected source texture; its optional **Pixel Canvas Size Override** is the authoring escape hatch for texture-sheet cells or other framed sources.

Use point filtering for deliberately authored pixel sprites. Use bilinear filtering for smooth source art that should become broad, clean UV-grid blocks. Authored animation frames should remain standalone PNGs with Sprite Mode **Single**, Mesh Type **Full Rect**, a centered pivot, alpha transparency enabled, point filtering, and no compression. Full Rect keeps the transparent canvas available to the quad; do not slice these frames or use a tight sprite mesh. The `baseline_striker` frames are all `96 x 69` and retain transparent padding around their opaque pixel bounds. A 1 x 1 quad with a 96 x 69 canvas must have a 96:69 world-space envelope; that non-uniform transform is the correct representation of the canvas and does not itself stretch pixels. Do not force uniform child scale to solve a shader-grid problem.

## Combat Composition

`CombatVFXLibrary` is the canonical move registry. Each entry contains four independent phase configs:

- Cast: move startup or charge.
- Strike: melee contact.
- Projectile: ranged release and travel.
- Impact: hit, miss, dodge, or barrier resolution.

The library selects prefabs, anchors, attachment, delay, scale, spawn offset, depth offset, sorting offset, and optional world-space Travel. `spawnOffset` is a world-space correction applied after anchor resolution, so a positive Y value raises a phase to compensate for a billboarded Chainling visual origin without changing the reusable prefab. `CombatMoveVFX` owns the spawned prefab lifecycle and can combine a sprite sequence, particles, and shared phase travel. This keeps choreography in combat data while shader appearance stays reusable.

The starter proof entries are `baseline_striker` (Cast, Strike, Impact) and `cipher_shot` (Cast, Projectile, Impact). `PF_PixelBattle_StrikeSlash` intentionally uses its authored `spriteSequence` as the source texture; its preset does not replace those frames.

The shared `CombatVFXLibrary` has a **World Scale Multiplier**, currently `1.2` to match the `Combat2Battlefield` Chainling `Billboard` visual child. It scales each spawned phase root, so sprite sequences, particles, trails, and world-space Travel share the same combat pixel scale in both live combat and the Animation Previewer. This is world geometry calibration only; it does not change the authored pixel canvas or the shader's virtual grid. Use a phase's existing `scaleMultiplier` for an intentional per-move adjustment on top of the shared baseline.

Each phase also has a **Phase Scale Curve** beside its `scaleMultiplier`. The curve is evaluated from `0` to `1` over the phase's authored effect lifetime, or over its resolved Travel duration when Travel is enabled, with its value multiplied by the composed world-plus-phase scale. A constant `1` curve preserves the current size. Values below `1` create a shrink or grow-in, values above `1` create an overshoot, and the curve can be edited directly in the Combat VFX Library inspector. For travelling phases, this phase-lifetime curve composes with Travel's independent start/end scale curve; it does not alter the path, sprite canvas, or shader grid.

Combat phase anchors must stay in the Chainling visual space. In particular, `ImpactAnchor` resolves from the slot's `CameraFocus`/visual root; the `Targeter` transform is a ground-selection indicator and must not be used for strike or impact VFX placement.

### Facing And Mirroring

Phase positions and offsets are authored from the player's non-mirrored perspective. At spawn time, live combat and the Animation Previewer read the attacking Chainling's existing `CombatChainlingVisual.FlipX` value. When it is enabled, they negate authored X offsets while preserving Y and camera-depth corrections, then call `CombatMoveVFX.SetMirrorX` on the complete VFX instance. This mirrors sprite meshes, particles, trails, and child presentation together, including travel phases whose scale is updated every frame. Prefabs therefore need only one authored orientation; opponent attacks automatically use the opposite facing.

## Travel Authoring

Every phase config has static placement plus an optional **Travel** section. Static placement owns the phase anchor, attachment policy, shared `spawnOffset`, camera-depth offset, sorting offset, and base scale. Enable Travel only when the whole spawned prefab should cross battlefield space.

- **Start / End**: choose independent anchors and endpoint offsets. The shared `spawnOffset` and depth correction apply to both endpoints; endpoint offsets are the additional move-specific correction.
- **Follow Anchor**: off by default, taking a deterministic snapshot at spawn. Enable it for a moving target, tether, or homing visual. `Midpoint` has no single transform and therefore always behaves as a snapshot.
- **Duration Source**: `Sprite Frame Span` uses `(frame count - 1) / FPS`, so the final sprite frame arrives at the end. `Effect Lifetime` uses the full authored lifetime. `Explicit Seconds` ignores prefab timing. `Constant Speed` derives duration from the resolved path length and `Speed Units Per Second`.
- **Travel Progress / Velocity Profile**: the cumulative curve controls path progress and therefore acceleration/deceleration; it starts at 0 and ends at 1. World Y Arc adds a world-space parabola on top. Keep a zero arc for contact slashes.
- **Sprite Timing**: `Preserve Prefab FPS` keeps the authored sequence speed. `Fit Frame Span To Travel` makes the final unique frame arrive at the endpoint. `Fit Effect Lifetime To Travel` fits the final-frame display interval into the travel duration.
- **Scale**: start/end scale multipliers are applied to the prefab's authored root scale and interpolated through the Travel scale curve. The phase's separate Phase Scale Curve can animate the complete phase on top of that travel scaling.

Travel is always world-space. It never attaches the spawned VFX to a combatant after spawn, although either endpoint can optionally follow its live anchor. Constant-speed duration is resolved from the spawn-time endpoint snapshot; following anchors continue to move the path without changing that duration. Keep local sprite-frame motion in the prefab for slash smears and expressive animation; use Travel for the distance between combatants.

The proof entries demonstrate both timing modes: `baseline_striker` moves its Strike from `AttackerCameraFocus` to `TargetImpact` using the authored sprite-frame span, while `cipher_shot` moves its Projectile from `AttackerProjectileOrigin` to `TargetImpact` over 0.35 seconds with a 0.35 world-Y arc.

For a speed-authored projectile, enable Travel, choose `Constant Speed`, set `Speed Units Per Second` to `10`, and leave the default linear progress curve in place. A 5-unit resolved path lasts about 0.5 seconds; changing the target distance changes the duration while preserving the configured average speed. Add a shallow ease-in/ease-out progress curve when the projectile should accelerate and decelerate, then use `Fit Frame Span To Travel` when its final authored frame must arrive exactly at the target.

## Frame Events And Previewing

Frame-authored `VFX_Sprite` events remain `SpriteFlipbookVfxDefinitionSO` events. Assign its optional `pixelBattlePreset` to render with the shared pixel system; this does not convert the event into a `CombatMoveVFX` prefab.

The current `baseline_striker` move track is empty, so `Baseline_Striker_VFX.asset` is not dispatched during the canonical phase preview or live move path. It remains available as an isolated flipbook definition for explicit `VFX_Sprite` events; it does not overwrite `PF_PixelBattle_StrikeSlash`.

Use `Tools/Chainbound/Combat/Animation Previewer` and `Play Attack Pair` to test move phases. Use a temporary `VFX_Sprite` event to test a frame-authored flipbook. For both paths, verify source frames, lifetime alpha, grid chunkiness, and front-of-body depth before promoting content into a saved track or library entry.
