#!/usr/bin/env node
"use strict";

// Operator-only Gate C executor. This file is intentionally not invoked by
// Codex: it loads local keypairs, signs, and submits transactions only when the
// operator supplies the exact approval token in an external WSL shell. It never
// prints keypair bytes or secret material.

const { readFileSync } = require("fs");
const { homedir } = require("os");
const { join } = require("path");
const bs58Module = require("bs58");
const bs58 = bs58Module.default || bs58Module;
const {
  createSignerFromKeypair,
  publicKey,
  signerIdentity,
} = require("@metaplex-foundation/umi");
const {
  createUmi,
} = require("@metaplex-foundation/umi-bundle-defaults");
const {
  createCollection,
  fetchCollectionV1,
  mplCore,
  safeFetchCollectionV1,
} = require("@metaplex-foundation/mpl-core");

const RPC_URL = process.env.CHAINBOUND_SOLANA_RPC_URL || "https://api.devnet.solana.com";
const DEVNET_GENESIS = "EtWTRABZaYq6iMfeYKouRu166VU2xqa1wcaWoxPkrZBG";
const PAYER = "DkFmzzzF89tYZu2JWR1uq3H13U5np9NU6MHF4E61gJmG";
const UPDATE_AUTHORITY = "FNRyxJLpcGuWwEUXYiXqjJMDsHAAs1gU8CnkjjBRL3ys";
const APPROVAL_TOKEN = "APPROVED_GATE_C_CREATE_FOUR_CORE_COLLECTIONS";

const CONFIG_HOME = process.env.XDG_CONFIG_HOME || join(homedir(), ".config");
const SIGNER_DIR =
  process.env.CHAINBOUND_CORE_SIGNER_DIR ||
  join(CONFIG_HOME, "solana", "chainbound", "ownership-spine-devnet");

const DEFINITIONS = [
  {
    key: "moves",
    env: "CHAINBOUND_CORE_MOVES_COLLECTION",
    signerFile: "core-moves-collection.json",
    name: "Chainbound Moves (Devnet)",
    uri: "https://chainbound.local/devnet/collections/moves.json",
  },
  {
    key: "archives",
    env: "CHAINBOUND_CORE_ARCHIVES_COLLECTION",
    signerFile: "core-archives-collection.json",
    name: "Chainbound Archives (Devnet)",
    uri: "https://chainbound.local/devnet/collections/archives.json",
  },
  {
    key: "covers",
    env: "CHAINBOUND_CORE_COVERS_COLLECTION",
    signerFile: "core-covers-collection.json",
    name: "Chainbound Covers (Devnet)",
    uri: "https://chainbound.local/devnet/collections/covers.json",
  },
  {
    key: "cosmetics",
    env: "CHAINBOUND_CORE_COSMETICS_COLLECTION",
    signerFile: "core-cosmetics-collection.json",
    name: "Chainbound Cosmetics (Devnet)",
    uri: "https://chainbound.local/devnet/collections/cosmetics.json",
  },
];

function assert(condition, message) {
  if (!condition) throw new Error(message);
}

function loadSigner(umi, path) {
  const parsed = JSON.parse(readFileSync(path, "utf8"));
  assert(
    Array.isArray(parsed) && parsed.length === 64 && parsed.every(Number.isInteger),
    `Invalid Solana keypair file at ${path}.`,
  );
  const keypair = umi.eddsa.createKeypairFromSecretKey(Uint8Array.from(parsed));
  return createSignerFromKeypair(umi, keypair);
}

function verifyCollection(collection, expected) {
  assert(
    collection.publicKey.toString() === expected.address.toString(),
    `${expected.key} collection address mismatch.`,
  );
  assert(collection.name === expected.name, `${expected.key} collection name mismatch.`);
  assert(collection.uri === expected.uri, `${expected.key} collection URI mismatch.`);
  assert(
    collection.updateAuthority.toString() === UPDATE_AUTHORITY,
    `${expected.key} collection update authority mismatch.`,
  );
}

function jsonReplacer(_key, value) {
  return typeof value === "bigint" ? value.toString() : value;
}

async function main() {
  assert(
    process.env.CHAINBOUND_GATE_C_APPROVAL === APPROVAL_TOKEN,
    `Refusing to sign: set CHAINBOUND_GATE_C_APPROVAL=${APPROVAL_TOKEN} only after explicit Gate C approval.`,
  );
  const payerPath = process.env.CHAINBOUND_OPERATOR_KEYPAIR;
  assert(
    payerPath,
    "CHAINBOUND_OPERATOR_KEYPAIR must point to the operator's locally configured keypair.",
  );

  const umi = createUmi(RPC_URL).use(mplCore());
  const genesisHash = await umi.rpc.getGenesisHash();
  assert(genesisHash === DEVNET_GENESIS, `RPC is not Solana devnet (${genesisHash}).`);

  const payer = loadSigner(umi, payerPath);
  assert(payer.publicKey.toString() === PAYER, "Operator keypair does not match the pinned payer.");
  umi.use(signerIdentity(payer));

  const definitions = DEFINITIONS.map((item) => {
    const addressText = process.env[item.env];
    assert(addressText, `Missing ${item.env}.`);
    const signer = loadSigner(umi, join(SIGNER_DIR, item.signerFile));
    const address = publicKey(addressText);
    assert(
      signer.publicKey.toString() === address.toString(),
      `${item.key} signer does not match ${item.env}.`,
    );
    return { ...item, address, signer };
  });
  assert(
    new Set(definitions.map((item) => item.address.toString())).size === definitions.length,
    "All four collection addresses must be distinct.",
  );

  const results = [];
  for (const item of definitions) {
    const existing = await safeFetchCollectionV1(umi, item.address, {
      commitment: "confirmed",
    });
    if (existing) {
      verifyCollection(existing, item);
      results.push({
        key: item.key,
        collection: item.address.toString(),
        signature: null,
        resumedExistingVerifiedCollection: true,
      });
      continue;
    }

    const builder = createCollection(umi, {
      collection: item.signer,
      name: item.name,
      uri: item.uri,
      updateAuthority: publicKey(UPDATE_AUTHORITY),
    }).useLegacyVersion();

    // Public Devnet RPCs often return BlockhashNotFound when getLatestBlockhash and
    // simulateTransaction land on different behind-the-scenes nodes. Rebuild with a
    // fresh blockhash and retry; optionally skip preflight on the last attempt.
    const maxAttempts = Number(process.env.CHAINBOUND_GATE_C_BLOCKHASH_ATTEMPTS || 8);
    const skipPreflightAlways = process.env.CHAINBOUND_GATE_C_SKIP_PREFLIGHT === "1";
    let signatureBytes = null;
    let latestBlockhash = null;
    let simulationUnitsConsumed = null;
    let lastSimErr = null;

    for (let attempt = 1; attempt <= maxAttempts; attempt += 1) {
      latestBlockhash = await umi.rpc.getLatestBlockhash({ commitment: "finalized" });
      const transaction = await builder
        .setBlockhash(latestBlockhash)
        .buildAndSign(umi);

      const skipPreflight = skipPreflightAlways || attempt >= maxAttempts - 1;
      if (!skipPreflight) {
        const simulation = await umi.rpc.simulateTransaction(transaction, {
          commitment: "confirmed",
          verifySignatures: true,
        });
        if (simulation.err !== null) {
          lastSimErr = simulation.err;
          const errText = JSON.stringify(simulation.err);
          const isBlockhash =
            errText.includes("BlockhashNotFound") ||
            (simulation.err && simulation.err.BlockhashNotFound !== undefined);
          if (isBlockhash && attempt < maxAttempts) {
            process.stderr.write(
              `${item.key}: BlockhashNotFound on simulate (attempt ${attempt}/${maxAttempts}); refreshing...\n`,
            );
            await new Promise((r) => setTimeout(r, 750 * attempt));
            continue;
          }
          assert(
            false,
            `${item.key} signed simulation failed: ${JSON.stringify({
              err: simulation.err,
              logs: simulation.logs,
            })}`,
          );
        }
        simulationUnitsConsumed = simulation.unitsConsumed ?? null;
      } else {
        process.stderr.write(
          `${item.key}: sending with skipPreflight=true (attempt ${attempt}/${maxAttempts})\n`,
        );
      }

      try {
        signatureBytes = await umi.rpc.sendTransaction(transaction, {
          commitment: "confirmed",
          preflightCommitment: "confirmed",
          skipPreflight,
          maxRetries: 15,
        });
        break;
      } catch (sendErr) {
        const msg = sendErr instanceof Error ? sendErr.message : String(sendErr);
        if (msg.includes("BlockhashNotFound") && attempt < maxAttempts) {
          process.stderr.write(
            `${item.key}: BlockhashNotFound on send (attempt ${attempt}/${maxAttempts}); refreshing...\n`,
          );
          await new Promise((r) => setTimeout(r, 750 * attempt));
          continue;
        }
        throw sendErr;
      }
    }

    assert(
      signatureBytes && latestBlockhash,
      `${item.key} failed to send after ${maxAttempts} blockhash attempts. Last sim err: ${JSON.stringify(lastSimErr)}`,
    );

    const confirmation = await umi.rpc.confirmTransaction(signatureBytes, {
      commitment: "confirmed",
      strategy: {
        type: "blockhash",
        blockhash: latestBlockhash.blockhash,
        lastValidBlockHeight: latestBlockhash.lastValidBlockHeight,
      },
    });
    assert(confirmation.value.err === null, `${item.key} create transaction failed.`);

    const created = await fetchCollectionV1(umi, item.address, {
      commitment: "confirmed",
    });
    verifyCollection(created, item);
    results.push({
      key: item.key,
      collection: item.address.toString(),
      signature: bs58.encode(Buffer.from(signatureBytes)),
      slot: confirmation.context.slot,
      simulationUnitsConsumed,
      resumedExistingVerifiedCollection: false,
    });
  }

  process.stdout.write(
    `${JSON.stringify(
      {
        verified: true,
        gate: "C",
        cluster: "devnet",
        rpcUrl: new URL(RPC_URL).origin,
        genesisHash,
        payer: PAYER,
        updateAuthority: UPDATE_AUTHORITY,
        collections: results,
      },
      jsonReplacer,
      2,
    )}\n`,
  );
}

main().catch((error) => {
  process.stderr.write(`${error instanceof Error ? error.stack || error.message : error}\n`);
  process.exitCode = 1;
});
