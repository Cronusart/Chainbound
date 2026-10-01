import { PNG as PngJs } from "npm:pngjs@7.0.0";
import { Buffer } from "npm:buffer@6.0.3";
import gifenc from "npm:gifenc@1.0.3";
import {
  LEARNED_CARD_FRONT_PNG_BASE64,
  LEARNED_MOVE_CARD_DELTA_PNG_BASE64,
  MODIFIER_CARD_CATALOG,
  MODIFIER_INK_PNG_BASE64,
  MOVE_CARD_CATALOG,
  MOVE_CARD_PNG_BASE64,
  RARITY_DOT_PNG_BASE64,
  SCANLINE_FRAME_PNG_BASE64,
  TYPE_STICKER_PNG_BASE64,
} from "./ownership_card_catalog.generated.ts";
import {
  formatLockedModifierEffect,
  formatLockedNumber,
  normalizeAcquisition,
  STAMP_METADATA_RENDER_VERSION,
} from "./ownership_spine.ts";

export interface StampMetadataInput {
  assetAddress: string;
  walletAddress: string;
  knownId: string;
  onchainInstanceId: string;
  moveId: string;
  basePower: number;
  modifierIds: string[];
  rarity: number;
  generatorVersion: number;
  poolHash: string;
  acquisition?: string;
  modifierEffectValues?: number[];
  cluster?: "devnet" | "mainnet-beta";
}

export interface PublishedStampMetadata {
  metadataUri: string;
  imageUri: string;
  masterImageUri: string;
  animationUri: string;
}

export interface PublishStampMetadataOptions {
  /** Defaults to the current immutable render version. Reserved for operator repair/backfill. */
  renderVersion?: number;
  /** Runtime publication remains immutable. Only an explicit operator repair may replace objects. */
  replaceExisting?: boolean;
  /** Optional media cache key used only when repairing a previously published metadata document. */
  mediaCacheNonce?: string;
}

type StorageClient = {
  storage: {
    from(bucket: string): {
      upload(path: string, body: Uint8Array | string, options: Record<string, unknown>): Promise<{
        error: { message?: string } | null;
      }>;
    };
  };
};

const RARITIES = ["Common", "Uncommon", "Rare", "Epic", "Legendary", "Mythic"] as const;

interface PngImage {
  width: number;
  height: number;
  data: Uint8Array;
}

interface EncodedLayer {
  x: number;
  y: number;
  png: string;
}

const PNG = PngJs as unknown as {
  new (options: { width: number; height: number }): PngImage;
  sync: {
    read(value: Uint8Array): PngImage;
    write(value: PngImage, options?: Record<string, unknown>): Uint8Array;
  };
};

function titleCaseId(value: string): string {
  return value.split("_").filter(Boolean).map((part) => part[0]?.toUpperCase() + part.slice(1)).join(" ");
}

function moveInfo(moveId: string, basePower: number) {
  const catalog = MOVE_CARD_CATALOG as Record<string, {
    displayName: string;
    power: number;
    accuracy: number;
    category: number;
    categoryName: string;
    typeId: number;
    typeName: string;
    typeIconPath: string;
  }>;
  return catalog[moveId] ?? {
    displayName: titleCaseId(moveId),
    power: basePower,
    accuracy: 0,
    category: 0,
    categoryName: "Damaging Melee",
    typeId: 0,
    typeName: "Typeless",
    typeIconPath: "Assets/Sprites/UI/typeless.png",
  };
}

function modifierInfo(modifierId: string, lockedValue?: number) {
  const catalog = MODIFIER_CARD_CATALOG as Record<string, { displayName: string; effect: string }>;
  const entry = catalog[modifierId] ?? { displayName: modifierId, effect: "" };
  const locked = lockedValue != null && Number.isFinite(lockedValue);
  return {
    displayName: entry.displayName,
    effect: locked ? formatLockedModifierEffect(entry.effect, lockedValue) : entry.effect,
    lockedValue: locked ? lockedValue : undefined,
  };
}

function pngFromBase64(value: string): PngImage {
  return PNG.sync.read(Buffer.from(value, "base64"));
}

function alphaComposite(base: PngImage, overlay: PngImage): void {
  if (base.width !== overlay.width || base.height !== overlay.height) throw new Error("metadata_layer_size_mismatch");
  alphaCompositeAt(base, overlay, 0, 0);
}

function alphaCompositeAt(base: PngImage, overlay: PngImage, offsetX: number, offsetY: number): void {
  if (offsetX < 0 || offsetY < 0 || offsetX + overlay.width > base.width || offsetY + overlay.height > base.height) {
    throw new Error("metadata_layer_bounds_mismatch");
  }
  for (let y = 0; y < overlay.height; y++) {
    for (let x = 0; x < overlay.width; x++) {
      const source = (y * overlay.width + x) * 4;
      const target = ((y + offsetY) * base.width + x + offsetX) * 4;
      const alpha = overlay.data[source + 3] / 255;
      if (alpha <= 0) continue;
      const inverse = 1 - alpha;
      base.data[target] = Math.round(overlay.data[source] * alpha + base.data[target] * inverse);
      base.data[target + 1] = Math.round(overlay.data[source + 1] * alpha + base.data[target + 1] * inverse);
      base.data[target + 2] = Math.round(overlay.data[source + 2] * alpha + base.data[target + 2] * inverse);
      base.data[target + 3] = Math.round((alpha + (base.data[target + 3] / 255) * inverse) * 255);
    }
  }
}

function decodeLayer(layer: EncodedLayer): { image: PngImage; x: number; y: number } {
  return { image: pngFromBase64(layer.png), x: layer.x, y: layer.y };
}

function compositeLayer(base: PngImage, layer: EncodedLayer): void {
  const decoded = decodeLayer(layer);
  alphaCompositeAt(base, decoded.image, decoded.x, decoded.y);
}

function tintAndCompositeLayer(
  base: PngImage,
  layer: EncodedLayer,
  tint?: { red: number; green: number; blue: number },
): void {
  const decoded = decodeLayer(layer);
  if (tint) tintVisiblePixels(decoded.image, tint.red, tint.green, tint.blue);
  alphaCompositeAt(base, decoded.image, decoded.x, decoded.y);
}

function tintVisiblePixels(image: PngImage, red: number, green: number, blue: number): void {
  for (let i = 0; i < image.data.length; i += 4) {
    if (image.data[i + 3] === 0) continue;
    image.data[i] = red;
    image.data[i + 1] = green;
    image.data[i + 2] = blue;
  }
}

function resizeNearest(source: PngImage, width: number, height: number): PngImage {
  const output = new PNG({ width, height });
  for (let y = 0; y < height; y++) {
    const sy = Math.min(source.height - 1, Math.floor(y * source.height / height));
    for (let x = 0; x < width; x++) {
      const sx = Math.min(source.width - 1, Math.floor(x * source.width / width));
      const si = (source.width * sy + sx) << 2;
      const di = (width * y + x) << 2;
      output.data[di] = source.data[si];
      output.data[di + 1] = source.data[si + 1];
      output.data[di + 2] = source.data[si + 2];
      output.data[di + 3] = source.data[si + 3];
    }
  }
  return output;
}

function shiftVertical(source: PngImage, offset: number): void {
  if (offset === 0) return;
  const original = new Uint8Array(source.data);
  const rowBytes = source.width * 4;
  for (let y = 0; y < source.height; y++) {
    const sourceY = (y - offset + source.height) % source.height;
    source.data.set(original.subarray(sourceY * rowBytes, (sourceY + 1) * rowBytes), y * rowBytes);
  }
}

function renderAnimation(card: PngImage): Uint8Array {
  const { GIFEncoder, quantize, applyPalette } = gifenc as {
    GIFEncoder: () => {
      writeFrame: (
        indexed: Uint8Array,
        width: number,
        height: number,
        options: Record<string, unknown>,
      ) => void;
      finish: () => void;
      bytes: () => Uint8Array;
    };
    quantize: (rgba: Uint8Array, maxColors: number) => number[][];
    applyPalette: (rgba: Uint8Array, palette: number[][]) => Uint8Array;
  };
  const gif = GIFEncoder();
  for (let frameIndex = 0; frameIndex < SCANLINE_FRAME_PNG_BASE64.length; frameIndex++) {
    const encodedFrame = SCANLINE_FRAME_PNG_BASE64[frameIndex];
    const frame = pngFromBase64(encodedFrame);
    // The authored source currently contains duplicate palette frames. Advance
    // the scanline phase here so marketplace animation remains visibly alive.
    shiftVertical(frame, frameIndex * 6);
    alphaComposite(frame, card);
    const marketFrame = resizeNearest(frame, 1024, 1024);
    const pixels = new Uint8Array(marketFrame.data);
    const palette = quantize(pixels, 128);
    gif.writeFrame(applyPalette(pixels, palette), 1024, 1024, { palette, delay: 140, repeat: 0 });
  }
  gif.finish();
  return gif.bytes();
}

export function renderStampLibraryCard(
  input: StampMetadataInput,
): { master: Uint8Array; market: Uint8Array } {
  const learned = normalizeAcquisition(input.acquisition ?? "") === "progression_learn";
  const moveCards = MOVE_CARD_PNG_BASE64 as Record<string, string>;
  const learnedMoveDeltas = LEARNED_MOVE_CARD_DELTA_PNG_BASE64 as Record<string, EncodedLayer>;
  const typeStickerLayers = TYPE_STICKER_PNG_BASE64 as Record<string, EncodedLayer>;
  const modifierLayers = MODIFIER_INK_PNG_BASE64 as Record<string, EncodedLayer>;
  const rarityLayers = RARITY_DOT_PNG_BASE64 as Record<string, EncodedLayer>;
  const moveCard = learned ? LEARNED_CARD_FRONT_PNG_BASE64 : moveCards[input.moveId];
  if (!moveCard) throw new Error(`metadata_move_card_missing:${input.moveId}`);
  const learnedMoveDelta = learned ? learnedMoveDeltas[input.moveId] : undefined;
  if (learned && !learnedMoveDelta) throw new Error(`metadata_learned_move_layer_missing:${input.moveId}`);
  const modifierId = input.modifierIds[0] ?? "";
  const modifierLayer = modifierLayers[modifierId];
  if (!modifierLayer) throw new Error(`metadata_modifier_layer_missing:${modifierId}`);

  const card = pngFromBase64(moveCard);
  if (learned && learnedMoveDelta) compositeLayer(card, learnedMoveDelta);
  const typeStickerLayer = typeStickerLayers[String(moveInfo(input.moveId, input.basePower).typeId)];
  if (!typeStickerLayer) throw new Error(`metadata_type_sticker_missing:${input.moveId}`);
  compositeLayer(card, typeStickerLayer);
  tintAndCompositeLayer(
    card,
    modifierLayer,
    learned ? { red: 158, green: 69, blue: 57 } : undefined,
  );
  compositeLayer(card, rarityLayers[String(input.rarity)] ?? rarityLayers["0"]);
  const poster = pngFromBase64(SCANLINE_FRAME_PNG_BASE64[0]);
  alphaComposite(poster, card);
  const market = resizeNearest(poster, 1024, 1024);
  return {
    master: new Uint8Array(PNG.sync.write(card, { colorType: 6 })),
    market: new Uint8Array(PNG.sync.write(market, { colorType: 6 })),
  };
}

function storageTarget(metadataUriBase: string): { bucket: string; publicBase: string } {
  const url = new URL(metadataUriBase.replace(/\/+$/, ""));
  const marker = "/storage/v1/object/public/";
  const markerIndex = url.pathname.indexOf(marker);
  if (markerIndex < 0) throw new Error("metadata_uri_base_not_public_storage");
  const tail = url.pathname.slice(markerIndex + marker.length).replace(/^\/+|\/+$/g, "");
  const bucket = tail.split("/")[0];
  if (!bucket) throw new Error("metadata_bucket_missing");
  return { bucket, publicBase: `${url.origin}${marker}${tail}` };
}

export function stampedMetadataPublicUris(
  metadataUriBase: string,
  assetAddress: string,
  renderVersion = STAMP_METADATA_RENDER_VERSION,
): PublishedStampMetadata {
  const prefix = `stamped/v${renderVersion}/${assetAddress}`;
  const { publicBase } = storageTarget(metadataUriBase);
  return {
    masterImageUri: `${publicBase}/${prefix}/master.png`,
    imageUri: `${publicBase}/${prefix}/poster.png`,
    animationUri: `${publicBase}/${prefix}/animation.gif`,
    metadataUri: `${publicBase}/${prefix}/metadata.json`,
  };
}

async function sha256(value: Uint8Array): Promise<string> {
  const digest = new Uint8Array(await crypto.subtle.digest("SHA-256", value as BufferSource));
  return Array.from(digest).map((byte) => byte.toString(16).padStart(2, "0")).join("");
}

async function readPublicObject(url: string): Promise<Uint8Array | null> {
  try {
    const response = await fetch(`${url}?cb_probe=${Date.now()}`, { headers: { "Cache-Control": "no-cache" } });
    return response.ok ? new Uint8Array(await response.arrayBuffer()) : null;
  } catch {
    return null;
  }
}

async function waitForPublicObject(url: string, expectedHash: string): Promise<boolean> {
  const delaysMs = [0, 200, 400, 800, 1200, 1600];
  for (const delayMs of delaysMs) {
    if (delayMs > 0) await new Promise((resolve) => setTimeout(resolve, delayMs));
    const value = await readPublicObject(url);
    if (value && await sha256(value) === expectedHash) return true;
  }
  return false;
}

async function uploadImmutable(
  supabase: StorageClient,
  bucket: string,
  path: string,
  publicUrl: string,
  body: Uint8Array | string,
  contentType: string,
  replaceExisting = false,
): Promise<void> {
  const bytes = typeof body === "string" ? new TextEncoder().encode(body) : body;
  const expectedHash = await sha256(bytes);
  const existing = await readPublicObject(publicUrl);
  if (existing) {
    if (await sha256(existing) === expectedHash) return;
    if (!replaceExisting) throw new Error(`metadata_object_hash_mismatch:${path}`);
  }
  const { error } = await supabase.storage.from(bucket).upload(path, body, {
    contentType,
    cacheControl: replaceExisting ? "60" : "31536000",
    upsert: replaceExisting,
  });
  if (error && !/already exists|duplicate/i.test(error.message ?? "")) {
    throw new Error(`metadata_upload_failed:${path}:${error.message ?? "unknown"}`);
  }
  if (!await waitForPublicObject(publicUrl, expectedHash)) throw new Error(`metadata_public_read_failed:${path}`);
}

function withCacheNonce(uri: string, nonce?: string): string {
  const value = nonce?.trim();
  if (!value) return uri;
  const url = new URL(uri);
  url.searchParams.set("render", value);
  return url.toString();
}

export function buildStampedMetadata(
  input: StampMetadataInput,
  uris: PublishedStampMetadata,
  renderVersion = STAMP_METADATA_RENDER_VERSION,
  mediaCacheNonce?: string,
): Record<string, unknown> {
  const move = moveInfo(input.moveId, input.basePower);
  const lockedValues = Array.isArray(input.modifierEffectValues) ? input.modifierEffectValues : [];
  const mods = input.modifierIds.map((id, index) => modifierInfo(id, lockedValues[index]));
  const acquisition = normalizeAcquisition(input.acquisition ?? "");
  const cardFace = acquisition === "progression_learn" ? "Learned" : "Rolled";
  const imageUri = withCacheNonce(uris.imageUri, mediaCacheNonce);
  const masterImageUri = withCacheNonce(uris.masterImageUri, mediaCacheNonce);
  return {
    name: move.displayName,
    symbol: "CBMOVE",
    description: `Chainbound Stamped move - ${move.displayName} with ${
      mods.map((mod) => mod.displayName).join(" + ")
    }.`,
    image: imageUri,
    external_url: input.cluster === "mainnet-beta"
      ? `https://explorer.solana.com/address/${input.assetAddress}`
      : `https://explorer.solana.com/address/${input.assetAddress}?cluster=devnet`,
    attributes: [
      { trait_type: "cb.kind", value: "stamped_move" },
      { trait_type: "cb.move_tier", value: "stamped" },
      { trait_type: "cb.move_id", value: input.moveId },
      { trait_type: "cb.known_instance_id", value: input.onchainInstanceId },
      { trait_type: "cb.acquisition", value: acquisition },
      { trait_type: "card_face", value: cardFace },
      { trait_type: "cb.type", value: move.typeName },
      { trait_type: "cb.move_category", value: move.categoryName },
      { trait_type: "cb.rarity", value: String(input.rarity) },
      { trait_type: "rarity_name", value: RARITIES[input.rarity] ?? RARITIES[0] },
      { trait_type: "cb.modifier_count", value: String(input.modifierIds.length) },
      ...input.modifierIds.map((id, index) => ({ trait_type: `cb.modifier_${index + 1}`, value: id })),
      { trait_type: "cb.generator_version", value: String(input.generatorVersion) },
      { trait_type: "cb.schema_version", value: "1" },
      { trait_type: "cb.pool_hash", value: input.poolHash },
      { trait_type: "cb.render_version", value: String(renderVersion) },
      { trait_type: "display_name", value: move.displayName },
      { trait_type: "type", value: move.typeName },
      { trait_type: "move_category", value: move.categoryName },
      { trait_type: "power", value: String(move.power) },
      { trait_type: "accuracy", value: move.accuracy <= 0 ? "AUTO" : String(move.accuracy) },
      ...mods.flatMap((mod, index) => [
        { trait_type: `mod_${index + 1}_name`, value: mod.displayName },
        { trait_type: `mod_${index + 1}_effect`, value: mod.effect },
        ...(mod.lockedValue != null
          ? [{ trait_type: `mod_${index + 1}_value`, value: formatLockedNumber(mod.lockedValue) }]
          : []),
      ]),
      { trait_type: "stamped_by", value: input.walletAddress },
    ],
    properties: {
      category: "image",
      chainbound: {
        asset_address: input.assetAddress,
        known_id: input.knownId,
        move_id: input.moveId,
        acquisition,
        card_face: cardFace.toLowerCase(),
        type: move.typeName,
        move_category: move.categoryName,
      },
      files: [
        { uri: imageUri, type: "image/png", note: "1024x1024 marketplace poster" },
        { uri: masterImageUri, type: "image/png", note: "1280x1280 authored master" },
      ],
    },
  };
}

export async function publishStampedMetadata(
  supabase: StorageClient,
  metadataUriBase: string,
  input: StampMetadataInput,
  options: PublishStampMetadataOptions = {},
): Promise<PublishedStampMetadata> {
  const renderVersion = options.renderVersion ?? STAMP_METADATA_RENDER_VERSION;
  const target = storageTarget(metadataUriBase);
  const prefix = `stamped/v${renderVersion}/${input.assetAddress}`;
  const uris = stampedMetadataPublicUris(metadataUriBase, input.assetAddress, renderVersion);
  const rendered = renderStampLibraryCard(input);
  await uploadImmutable(
    supabase,
    target.bucket,
    `${prefix}/master.png`,
    uris.masterImageUri,
    rendered.master,
    "image/png",
    options.replaceExisting,
  );
  await uploadImmutable(
    supabase,
    target.bucket,
    `${prefix}/poster.png`,
    uris.imageUri,
    rendered.market,
    "image/png",
    options.replaceExisting,
  );
  const metadata = buildStampedMetadata(input, uris, renderVersion, options.mediaCacheNonce);
  await uploadImmutable(
    supabase,
    target.bucket,
    `${prefix}/metadata.json`,
    uris.metadataUri,
    JSON.stringify(metadata),
    "application/json",
    options.replaceExisting,
  );
  return uris;
}
