import { PNG } from "npm:pngjs@7.0.0";
import { Buffer } from "npm:buffer@6.0.3";
import gifenc from "npm:gifenc@1.0.3";
import {
  MODIFIER_CARD_CATALOG,
  MODIFIER_INK_PNG_BASE64,
  MOVE_CARD_CATALOG,
  MOVE_CARD_PNG_BASE64,
  RARITY_DOT_PNG_BASE64,
  SCANLINE_FRAME_PNG_BASE64,
} from "./ownership_card_catalog.generated.ts";
import { STAMP_METADATA_RENDER_VERSION } from "./ownership_spine.ts";

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
}

export interface PublishedStampMetadata {
  metadataUri: string;
  imageUri: string;
  masterImageUri: string;
  animationUri: string;
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

function titleCaseId(value: string): string {
  return value.split("_").filter(Boolean).map((part) => part[0]?.toUpperCase() + part.slice(1)).join(" ");
}

function moveInfo(moveId: string, basePower: number) {
  const catalog = MOVE_CARD_CATALOG as Record<string, {
    displayName: string;
    power: number;
    accuracy: number;
    category: number;
    typeId: number;
  }>;
  return catalog[moveId] ?? {
    displayName: titleCaseId(moveId),
    power: basePower,
    accuracy: 0,
    category: 0,
    typeId: 0,
  };
}

function modifierInfo(modifierId: string) {
  const catalog = MODIFIER_CARD_CATALOG as Record<string, { displayName: string; effect: string }>;
  return catalog[modifierId] ?? { displayName: modifierId, effect: "" };
}

function pngFromBase64(value: string): PNG {
  return PNG.sync.read(Buffer.from(value, "base64"));
}

function alphaComposite(base: PNG, overlay: PNG): void {
  if (base.width !== overlay.width || base.height !== overlay.height) throw new Error("metadata_layer_size_mismatch");
  for (let i = 0; i < base.data.length; i += 4) {
    const alpha = overlay.data[i + 3] / 255;
    if (alpha <= 0) continue;
    const inverse = 1 - alpha;
    base.data[i] = Math.round(overlay.data[i] * alpha + base.data[i] * inverse);
    base.data[i + 1] = Math.round(overlay.data[i + 1] * alpha + base.data[i + 1] * inverse);
    base.data[i + 2] = Math.round(overlay.data[i + 2] * alpha + base.data[i + 2] * inverse);
    base.data[i + 3] = Math.round((alpha + (base.data[i + 3] / 255) * inverse) * 255);
  }
}

function resizeNearest(source: PNG, width: number, height: number): PNG {
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

function shiftVertical(source: PNG, offset: number): void {
  if (offset === 0) return;
  const original = new Uint8Array(source.data);
  const rowBytes = source.width * 4;
  for (let y = 0; y < source.height; y++) {
    const sourceY = (y - offset + source.height) % source.height;
    source.data.set(original.subarray(sourceY * rowBytes, (sourceY + 1) * rowBytes), y * rowBytes);
  }
}

function renderAnimation(card: PNG): Uint8Array {
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
): { master: Uint8Array; market: Uint8Array; animation: Uint8Array } {
  const moveCards = MOVE_CARD_PNG_BASE64 as Record<string, string>;
  const modifierLayers = MODIFIER_INK_PNG_BASE64 as Record<string, string>;
  const rarityLayers = RARITY_DOT_PNG_BASE64 as Record<string, string>;
  const moveCard = moveCards[input.moveId];
  if (!moveCard) throw new Error(`metadata_move_card_missing:${input.moveId}`);
  const modifierId = input.modifierIds[0] ?? "";
  const modifierLayer = modifierLayers[modifierId];
  if (!modifierLayer) throw new Error(`metadata_modifier_layer_missing:${modifierId}`);

  const card = pngFromBase64(moveCard);
  alphaComposite(card, pngFromBase64(modifierLayer));
  alphaComposite(card, pngFromBase64(rarityLayers[String(input.rarity)] ?? rarityLayers["0"]));
  const poster = pngFromBase64(SCANLINE_FRAME_PNG_BASE64[0]);
  alphaComposite(poster, card);
  const market = resizeNearest(poster, 1024, 1024);
  return {
    master: new Uint8Array(PNG.sync.write(card, { colorType: 6 })),
    market: new Uint8Array(PNG.sync.write(market, { colorType: 6 })),
    animation: renderAnimation(card),
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
): PublishedStampMetadata {
  const prefix = `stamped/v${STAMP_METADATA_RENDER_VERSION}/${assetAddress}`;
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
): Promise<void> {
  const bytes = typeof body === "string" ? new TextEncoder().encode(body) : body;
  const expectedHash = await sha256(bytes);
  const existing = await readPublicObject(publicUrl);
  if (existing) {
    if (await sha256(existing) !== expectedHash) throw new Error(`metadata_object_hash_mismatch:${path}`);
    return;
  }
  const { error } = await supabase.storage.from(bucket).upload(path, body, {
    contentType,
    cacheControl: "31536000",
    upsert: false,
  });
  if (error && !/already exists|duplicate/i.test(error.message ?? "")) {
    throw new Error(`metadata_upload_failed:${path}:${error.message ?? "unknown"}`);
  }
  if (!await waitForPublicObject(publicUrl, expectedHash)) throw new Error(`metadata_public_read_failed:${path}`);
}

export async function publishStampedMetadata(
  supabase: StorageClient,
  metadataUriBase: string,
  input: StampMetadataInput,
): Promise<PublishedStampMetadata> {
  const target = storageTarget(metadataUriBase);
  const prefix = `stamped/v${STAMP_METADATA_RENDER_VERSION}/${input.assetAddress}`;
  const uris = stampedMetadataPublicUris(metadataUriBase, input.assetAddress);
  const rendered = renderStampLibraryCard(input);
  await uploadImmutable(supabase, target.bucket, `${prefix}/master.png`, uris.masterImageUri, rendered.master, "image/png");
  await uploadImmutable(supabase, target.bucket, `${prefix}/poster.png`, uris.imageUri, rendered.market, "image/png");
  await uploadImmutable(
    supabase,
    target.bucket,
    `${prefix}/animation.gif`,
    uris.animationUri,
    rendered.animation,
    "image/gif",
  );

  const move = moveInfo(input.moveId, input.basePower);
  const mods = input.modifierIds.map(modifierInfo);
  const metadata = {
    name: move.displayName,
    symbol: "CBMOVE",
    description: `Chainbound Stamped move - ${move.displayName} with ${mods.map((mod) => mod.displayName).join(" + ")}.`,
    image: uris.imageUri,
    animation_url: uris.animationUri,
    external_url: `https://explorer.solana.com/address/${input.assetAddress}?cluster=devnet`,
    attributes: [
      { trait_type: "cb.kind", value: "stamped_move" },
      { trait_type: "cb.move_tier", value: "stamped" },
      { trait_type: "cb.move_id", value: input.moveId },
      { trait_type: "cb.known_instance_id", value: input.onchainInstanceId },
      { trait_type: "cb.rarity", value: String(input.rarity) },
      { trait_type: "rarity_name", value: RARITIES[input.rarity] ?? RARITIES[0] },
      { trait_type: "cb.modifier_count", value: String(input.modifierIds.length) },
      ...input.modifierIds.map((id, index) => ({ trait_type: `cb.modifier_${index + 1}`, value: id })),
      { trait_type: "cb.generator_version", value: String(input.generatorVersion) },
      { trait_type: "cb.schema_version", value: "1" },
      { trait_type: "cb.pool_hash", value: input.poolHash },
      { trait_type: "cb.render_version", value: String(STAMP_METADATA_RENDER_VERSION) },
      { trait_type: "display_name", value: move.displayName },
      { trait_type: "power", value: String(move.power) },
      { trait_type: "accuracy", value: move.accuracy <= 0 ? "AUTO" : String(move.accuracy) },
      ...mods.map((mod, index) => ({ trait_type: `mod_${index + 1}_name`, value: mod.displayName })),
      { trait_type: "stamped_by", value: input.walletAddress },
    ],
    properties: {
      category: "image",
      chainbound: { asset_address: input.assetAddress, known_id: input.knownId },
      files: [
        { uri: uris.imageUri, type: "image/png", note: "1024x1024 marketplace poster" },
        { uri: uris.animationUri, type: "image/gif", note: "animated scanline card" },
        { uri: uris.masterImageUri, type: "image/png", note: "1280x1280 authored master" },
      ],
    },
  };
  await uploadImmutable(
    supabase,
    target.bucket,
    `${prefix}/metadata.json`,
    uris.metadataUri,
    JSON.stringify(metadata),
    "application/json",
  );
  return uris;
}
