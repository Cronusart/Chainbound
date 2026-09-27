#!/usr/bin/env node
"use strict";

const crypto = require("crypto");
const {
  createNoopSigner,
  publicKey,
  signerIdentity,
} = require("@metaplex-foundation/umi");
const {
  createUmi,
} = require("@metaplex-foundation/umi-bundle-defaults");
const {
  createCollection,
  mplCore,
} = require("@metaplex-foundation/mpl-core");

const RPC_URL = process.env.CHAINBOUND_SOLANA_RPC_URL || "https://api.devnet.solana.com";
const PAYER = "DkFmzzzF89tYZu2JWR1uq3H13U5np9NU6MHF4E61gJmG";
const UPDATE_AUTHORITY = "FNRyxJLpcGuWwEUXYiXqjJMDsHAAs1gU8CnkjjBRL3ys";
const MPL_CORE_PROGRAM = "CoREENxT6tW1HoK8ypY1SxRMZTcVPm7R94rH4PZNhX7d";

const DEFINITIONS = [
  {
    key: "moves",
    env: "CHAINBOUND_CORE_MOVES_COLLECTION",
    name: "Chainbound Moves (Devnet)",
    uri: "https://chainbound.local/devnet/collections/moves.json",
  },
  {
    key: "archives",
    env: "CHAINBOUND_CORE_ARCHIVES_COLLECTION",
    name: "Chainbound Archives (Devnet)",
    uri: "https://chainbound.local/devnet/collections/archives.json",
  },
  {
    key: "covers",
    env: "CHAINBOUND_CORE_COVERS_COLLECTION",
    name: "Chainbound Covers (Devnet)",
    uri: "https://chainbound.local/devnet/collections/covers.json",
  },
  {
    key: "cosmetics",
    env: "CHAINBOUND_CORE_COSMETICS_COLLECTION",
    name: "Chainbound Cosmetics (Devnet)",
    uri: "https://chainbound.local/devnet/collections/cosmetics.json",
  },
];

function sha256Hex(bytes) {
  return crypto.createHash("sha256").update(bytes).digest("hex");
}

function jsonReplacer(_key, value) {
  return typeof value === "bigint" ? value.toString() : value;
}

function requireCollectionPublicKeys() {
  const missing = DEFINITIONS.filter((item) => !process.env[item.env]);
  if (missing.length > 0) {
    const names = missing.map((item) => item.env).join(", ");
    throw new Error(`Missing collection public-key environment variable(s): ${names}`);
  }

  const values = DEFINITIONS.map((item) => ({
    ...item,
    address: publicKey(process.env[item.env]),
  }));
  const unique = new Set(values.map((item) => item.address.toString()));
  if (unique.size !== values.length) {
    throw new Error("All four collection public keys must be distinct.");
  }
  return values;
}

function summarizeSimulatedAccount(account) {
  if (!account) return null;
  const encoded = Array.isArray(account.data) ? account.data[0] : "";
  return {
    lamports: account.lamports,
    owner: account.owner,
    executable: account.executable,
    dataBytes: encoded ? Buffer.from(encoded, "base64").length : 0,
  };
}

async function main() {
  const collections = requireCollectionPublicKeys();
  const umi = createUmi(RPC_URL).use(mplCore());
  const payer = createNoopSigner(publicKey(PAYER));
  umi.use(signerIdentity(payer));

  const genesisHash = await umi.rpc.getGenesisHash();
  // Devnet's stable genesis hash. This prevents accidental use against mainnet/testnet/localnet.
  if (genesisHash !== "EtWTRABZaYq6iMfeYKouRu166VU2xqa1wcaWoxPkrZBG") {
    throw new Error(`RPC is not Solana devnet (unexpected genesis hash ${genesisHash}).`);
  }

  const addresses = collections.map((item) => item.address);
  const existing = await umi.rpc.getAccounts(addresses, { commitment: "confirmed" });
  const collisions = collections.filter((_item, index) => existing[index].exists);
  if (collisions.length > 0) {
    throw new Error(
      `Collection account(s) already exist: ${collisions.map((item) => item.address).join(", ")}`,
    );
  }

  const payerBalance = await umi.rpc.getBalance(payer.publicKey, { commitment: "confirmed" });
  const plans = [];

  for (const item of collections) {
    const collectionSigner = createNoopSigner(item.address);
    const builder = createCollection(umi, {
      collection: collectionSigner,
      name: item.name,
      uri: item.uri,
      updateAuthority: publicKey(UPDATE_AUTHORITY),
    }).useLegacyVersion();

    const transaction = await builder.buildWithLatestBlockhash(umi, {
      commitment: "confirmed",
    });
    const serialized = umi.transactions.serialize(transaction);
    const serializedMessage = transaction.serializedMessage;
    const feeResponse = await umi.rpc.call("getFeeForMessage", [
      Buffer.from(serializedMessage).toString("base64"),
      { commitment: "confirmed" },
    ]);
    const simulation = await umi.rpc.simulateTransaction(transaction, {
      commitment: "confirmed",
      verifySignatures: false,
      replaceRecentBlockhash: true,
      accounts: [payer.publicKey, item.address],
    });
    if (simulation.err) {
      throw new Error(
        `${item.key} create simulation failed: ${JSON.stringify({
          err: simulation.err,
          logs: simulation.logs,
        })}`,
      );
    }

    const postPayer = summarizeSimulatedAccount(simulation.accounts?.[0]);
    const postCollection = summarizeSimulatedAccount(simulation.accounts?.[1]);
    const feeLamports = feeResponse.value;
    const rentLamports = postCollection?.lamports ?? null;

    plans.push({
      key: item.key,
      name: item.name,
      uri: item.uri,
      collection: item.address.toString(),
      updateAuthority: UPDATE_AUTHORITY,
      payer: PAYER,
      mplCoreProgram: MPL_CORE_PROGRAM,
      requiredSignerPublicKeys: transaction.message.accounts
        .slice(0, transaction.message.header.numRequiredSignatures)
        .map(String),
      transactionVersion: transaction.message.version,
      transactionBytes: serialized.length,
      unsignedTransactionBase64: Buffer.from(serialized).toString("base64"),
      messageSha256: sha256Hex(serializedMessage),
      recentBlockhash: transaction.message.blockhash,
      feeLamports,
      rentLamports,
      estimatedTotalLamports:
        feeLamports !== null && rentLamports !== null
          ? feeLamports + rentLamports
          : null,
      simulation: {
        error: simulation.err,
        unitsConsumed: simulation.unitsConsumed ?? null,
        postPayer,
        postCollection,
      },
    });
  }

  const totalEstimatedLamports = plans.reduce(
    (sum, item) => sum + (item.estimatedTotalLamports ?? 0),
    0,
  );
  const output = {
    gate: "C",
    operation: "prepare-four-mpl-core-collections",
    signingPerformed: false,
    sendingPerformed: false,
    cluster: "devnet",
    rpcUrl: RPC_URL,
    genesisHash,
    payer: PAYER,
    payerBalanceLamports: payerBalance.basisPoints,
    authorityPolicy: {
      allCollectionsUpdateAuthority: UPDATE_AUTHORITY,
      reason:
        "The deployed Chainbound program validates all four collection update authorities against its core_authority PDA.",
    },
    packagePins: {
      mplCore: "1.10.0",
      umi: "1.6.0",
      umiBundleDefaults: "1.6.0",
    },
    plans,
    totalEstimatedLamports,
    note:
      "Unsigned transaction bytes are simulation artifacts with expiring recent blockhashes. After explicit approval, rebuild the identical instructions with a fresh blockhash and sign locally in WSL.",
  };
  process.stdout.write(`${JSON.stringify(output, jsonReplacer, 2)}\n`);
}

main().catch((error) => {
  console.error(error instanceof Error ? error.stack || error.message : error);
  process.exitCode = 1;
});
