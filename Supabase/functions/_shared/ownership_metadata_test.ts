import { PNG } from "npm:pngjs@7.0.0";
import { Buffer } from "npm:buffer@6.0.3";
import { assertEquals, assertStringIncludes } from "https://deno.land/std@0.224.0/assert/mod.ts";
import { buildStampedMetadata, renderStampLibraryCard, stampedMetadataPublicUris } from "./ownership_metadata.ts";

Deno.test("Stamp metadata public URIs match the immutable on-chain stamped path", () => {
  const asset = "12HRhMwESkTzsm39L46PRcqAnMMycPqgkJcf3hLcV8DF";
  const base = "https://project.supabase.co/storage/v1/object/public/chainbound-ownership-metadata";
  const uris = stampedMetadataPublicUris(base, asset);
  assertEquals(uris.metadataUri, `${base}/stamped/v6/${asset}/metadata.json`);
  assertEquals(uris.masterImageUri, `${base}/stamped/v6/${asset}/master.png`);
  assertEquals(uris.imageUri, `${base}/stamped/v6/${asset}/poster.png`);
  assertEquals(uris.animationUri, `${base}/stamped/v6/${asset}/animation.gif`);
});

Deno.test("Stamp metadata renderer bakes the canonical move type sticker", () => {
  const rendered = renderStampLibraryCard({
    assetAddress: "Wrry1Sk4iswAkUhJVUHkHhthh6FoJQSRwNr28PVu6Pt",
    walletAddress: "9zMwTbQtjvexJzuMZjzbKS7LtWNXVYG3auBNeV82VHUj",
    knownId: "f47d705a-98cf-4593-8f1e-aabe072681ab",
    onchainInstanceId: "3",
    moveId: "baseline_jab",
    basePower: 20,
    modifierIds: ["4.1-a"],
    rarity: 1,
    generatorVersion: 1,
    poolHash: "6cea5ce56944a88486cafbb568a628b5aa5664c3db652a79cd90016427aaa4b4",
  });
  const master = (PNG as unknown as {
    sync: { read(value: Uint8Array): { width: number; data: Uint8Array } };
  }).sync.read(Buffer.from(rendered.master));
  const colors = new Set<string>();
  for (let y = 390; y < 552; y += 4) {
    for (let x = 70; x < 250; x += 4) {
      const offset = (y * master.width + x) * 4;
      colors.add(
        `${master.data[offset]},${master.data[offset + 1]},${master.data[offset + 2]},${master.data[offset + 3]}`,
      );
    }
  }
  assertEquals(colors.size > 20, true);
});

Deno.test("Stamp metadata includes type, category, and locked modifier effect copy", () => {
  const input = {
    assetAddress: "Wrry1Sk4iswAkUhJVUHkHhthh6FoJQSRwNr28PVu6Pt",
    walletAddress: "9zMwTbQtjvexJzuMZjzbKS7LtWNXVYG3auBNeV82VHUj",
    knownId: "f47d705a-98cf-4593-8f1e-aabe072681ab",
    onchainInstanceId: "3",
    moveId: "baseline_jab",
    basePower: 20,
    modifierIds: ["2.1-b"],
    modifierEffectValues: [10],
    rarity: 1,
    generatorVersion: 1,
    poolHash: "6cea5ce56944a88486cafbb568a628b5aa5664c3db652a79cd90016427aaa4b4",
  };
  const uris = stampedMetadataPublicUris(
    "https://project.supabase.co/storage/v1/object/public/cards",
    input.assetAddress,
  );
  const metadata = buildStampedMetadata(input, uris) as {
    animation_url?: string;
    attributes: Array<{ trait_type: string; value: string }>;
    properties: { files: Array<{ type: string }> };
  };
  const traits = Object.fromEntries(metadata.attributes.map((entry) => [entry.trait_type, entry.value]));
  assertEquals(metadata.animation_url, undefined);
  assertEquals(metadata.properties.files.some((file) => file.type === "image/gif"), false);
  assertEquals(traits["cb.type"], "Typeless");
  assertEquals(traits["cb.move_category"], "Damaging Melee");
  assertEquals(traits["cb.acquisition"], "candy_roll");
  assertEquals(traits["card_face"], "Rolled");
  assertEquals(traits["mod_1_name"], "Hot Rod");
  assertEquals(traits["mod_1_effect"], "10% base power");
  assertEquals(traits["mod_1_value"], "10");
});

Deno.test("Stamp metadata explorer URL follows the selected ownership cluster", () => {
  const input = {
    assetAddress: "Wrry1Sk4iswAkUhJVUHkHhthh6FoJQSRwNr28PVu6Pt",
    walletAddress: "9zMwTbQtjvexJzuMZjzbKS7LtWNXVYG3auBNeV82VHUj",
    knownId: "f47d705a-98cf-4593-8f1e-aabe072681ab",
    onchainInstanceId: "3",
    moveId: "baseline_jab",
    basePower: 20,
    modifierIds: ["4.1-a"],
    rarity: 1,
    generatorVersion: 1,
    poolHash: "6cea5ce56944a88486cafbb568a628b5aa5664c3db652a79cd90016427aaa4b4",
    cluster: "mainnet-beta" as const,
  };
  const uris = stampedMetadataPublicUris(
    "https://project.supabase.co/storage/v1/object/public/cards",
    input.assetAddress,
  );
  const metadata = buildStampedMetadata(input, uris) as { external_url: string };
  assertEquals(metadata.external_url, `https://explorer.solana.com/address/${input.assetAddress}`);
});

Deno.test("Stamp metadata renderer produces authored master and marketplace PNGs", () => {
  const rendered = renderStampLibraryCard({
    assetAddress: "12HRhMwESkTzsm39L46PRcqAnMMycPqgkJcf3hLcV8DF",
    walletAddress: "9zMwTbQtjvexJzuMZjzbKS7LtWNXVYG3auBNeV82VHUj",
    knownId: "c9eca3e5-0c50-4d31-97a2-a4d7da3978f5",
    onchainInstanceId: "2",
    moveId: "baseline_thrust",
    basePower: 35,
    modifierIds: ["2.1-b"],
    rarity: 1,
    generatorVersion: 1,
    poolHash: "6cea5ce56944a88486cafbb568a628b5aa5664c3db652a79cd90016427aaa4b4",
  });
  assertEquals(Array.from(rendered.master.slice(0, 8)), [137, 80, 78, 71, 13, 10, 26, 10]);
  assertEquals(Array.from(rendered.market.slice(0, 8)), [137, 80, 78, 71, 13, 10, 26, 10]);
  assertEquals(rendered.master.length > 10_000, true);
  assertEquals(rendered.market.length > 10_000, true);
});

Deno.test("Stamp metadata uses rust ink only for a learned Known", () => {
  const input = {
    assetAddress: "Wrry1Sk4iswAkUhJVUHkHhthh6FoJQSRwNr28PVu6Pt",
    walletAddress: "9zMwTbQtjvexJzuMZjzbKS7LtWNXVYG3auBNeV82VHUj",
    knownId: "f47d705a-98cf-4593-8f1e-aabe072681ab",
    onchainInstanceId: "3",
    moveId: "baseline_jab",
    basePower: 20,
    modifierIds: ["4.1-a"],
    rarity: 1,
    generatorVersion: 1,
    poolHash: "6cea5ce56944a88486cafbb568a628b5aa5664c3db652a79cd90016427aaa4b4",
  };
  const rolled = renderStampLibraryCard(input);
  const learned = renderStampLibraryCard({ ...input, acquisition: "progression_learn" });
  const eventGrant = renderStampLibraryCard({ ...input, acquisition: "event_grant" });
  assertEquals(Array.from(eventGrant.master), Array.from(rolled.master));
  const rust = (bytes: Uint8Array) => {
    const image = (PNG as unknown as {
      sync: { read(value: Uint8Array): { data: Uint8Array } };
    }).sync.read(Buffer.from(bytes));
    let count = 0;
    for (let i = 0; i < image.data.length; i += 16) {
      if (image.data[i] === 158 && image.data[i + 1] === 69 && image.data[i + 2] === 57 && image.data[i + 3] > 200) {
        count++;
      }
    }
    return count;
  };
  assertEquals(rust(learned.master) > 0, true);
  assertEquals(rust(rolled.master), 0);
});

Deno.test("Learned Stamp metadata exposes its authoritative acquisition face", () => {
  const input = {
    assetAddress: "Learned11111111111111111111111111111111111",
    walletAddress: "9zMwTbQtjvexJzuMZjzbKS7LtWNXVYG3auBNeV82VHUj",
    knownId: "f47d705a-98cf-4593-8f1e-aabe072681ab",
    onchainInstanceId: "4",
    moveId: "baseline_jab",
    basePower: 20,
    modifierIds: ["4.1-a"],
    rarity: 1,
    generatorVersion: 1,
    poolHash: "6cea5ce56944a88486cafbb568a628b5aa5664c3db652a79cd90016427aaa4b4",
    acquisition: "progression_learn",
  };
  const uris = stampedMetadataPublicUris(
    "https://project.supabase.co/storage/v1/object/public/cards",
    input.assetAddress,
  );
  const metadata = buildStampedMetadata(input, uris) as {
    attributes: Array<{ trait_type: string; value: string }>;
    properties: { chainbound: { acquisition: string; card_face: string } };
  };
  const traits = Object.fromEntries(metadata.attributes.map((entry) => [entry.trait_type, entry.value]));
  assertEquals(traits["cb.acquisition"], "progression_learn");
  assertEquals(traits["card_face"], "Learned");
  assertEquals(metadata.properties.chainbound.acquisition, "progression_learn");
  assertEquals(metadata.properties.chainbound.card_face, "learned");
});

Deno.test("Stamp prepare publishes metadata before returning a signable transaction", async () => {
  const source = await Deno.readTextFile(new URL("../ownership_prepare_stamp/index.ts", import.meta.url));
  const publication = source.indexOf("await publishStampedMetadata");
  const response = source.indexOf("return jsonResponse({", publication);
  assertEquals(publication >= 0, true);
  assertEquals(response > publication, true);
  assertStringIncludes(source, "stamp_metadata_publication_failed");
  assertStringIncludes(source, "metadataPublished: true");
});

Deno.test("Stamp lifecycle snapshots and returns the authoritative Known acquisition", async () => {
  const [prepare, confirm, reconcile, inventory] = await Promise.all([
    Deno.readTextFile(new URL("../ownership_prepare_stamp/index.ts", import.meta.url)),
    Deno.readTextFile(new URL("../ownership_confirm_stamp/index.ts", import.meta.url)),
    Deno.readTextFile(new URL("../ownership_reconcile_stamp/index.ts", import.meta.url)),
    Deno.readTextFile(new URL("../ownership_get_inventory/index.ts", import.meta.url)),
  ]).then((sources) => sources.map((source) => source.replaceAll("\r\n", "\n")));
  assertStringIncludes(prepare, "const acquisition = normalizeAcquisition(str(known.acquisition))");
  assertStringIncludes(prepare, "acquisition,\n      onchain_instance_id");
  assertStringIncludes(confirm, "acquisition: normalizeAcquisition(str(operation.acquisition)");
  assertStringIncludes(reconcile, "acquisition: normalizeAcquisition(str(operation.acquisition)");
  assertStringIncludes(inventory, "stamped_asset_address,acquisition");
  assertStringIncludes(inventory, "acquisition: normalizeAcquisition(str(operation.acquisition)");
});
