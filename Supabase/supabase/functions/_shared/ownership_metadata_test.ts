import { assertEquals, assertStringIncludes } from "https://deno.land/std@0.224.0/assert/mod.ts";
import { renderStampLibraryCard, stampedMetadataPublicUris } from "./ownership_metadata.ts";

Deno.test("Stamp metadata public URIs match the immutable on-chain stamped path", () => {
  const asset = "12HRhMwESkTzsm39L46PRcqAnMMycPqgkJcf3hLcV8DF";
  const base = "https://project.supabase.co/storage/v1/object/public/chainbound-ownership-metadata";
  const uris = stampedMetadataPublicUris(base, asset);
  assertEquals(uris.metadataUri, `${base}/stamped/v4/${asset}/metadata.json`);
  assertEquals(uris.masterImageUri, `${base}/stamped/v4/${asset}/master.png`);
  assertEquals(uris.imageUri, `${base}/stamped/v4/${asset}/poster.png`);
  assertEquals(uris.animationUri, `${base}/stamped/v4/${asset}/animation.gif`);
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
  assertEquals(new TextDecoder().decode(rendered.animation.slice(0, 6)), "GIF89a");
  assertEquals(rendered.master.length > 10_000, true);
  assertEquals(rendered.market.length > 10_000, true);
  assertEquals(rendered.animation.length > 10_000, true);
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
