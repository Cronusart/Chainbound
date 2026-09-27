#!/usr/bin/env node
"use strict";

// Builds and simulates the exact unsigned OwnershipConfig initialization. It
// requires the explicitly approved 250-RAM Bind pin, loads no signer, and
// submits nothing. The deferred 150-RAM second-Stamp pin is not encoded here.

const { createHash } = require("crypto");
const { Connection, PublicKey, SystemProgram, Transaction, TransactionInstruction } = require("@solana/web3.js");
const { createUmi } = require("@metaplex-foundation/umi-bundle-defaults");
const { fetchCollectionV1, mplCore } = require("@metaplex-foundation/mpl-core");

const RPC_URL = process.env.CHAINBOUND_SOLANA_RPC_URL || "https://api.devnet.solana.com";
const DEVNET_GENESIS = "EtWTRABZaYq6iMfeYKouRu166VU2xqa1wcaWoxPkrZBG";
const PROGRAM_ID = new PublicKey("9312ay7VrpvNYmyEx8TYCqhafbZ2Y5WGVbt6Qp772uUQ");
const PROGRAM_AUTHORITY = new PublicKey("DkFmzzzF89tYZu2JWR1uq3H13U5np9NU6MHF4E61gJmG");
const RESULT_AUTHORITY = new PublicKey("FaAegH2tBCyDzxXcXyz64qEFWsrvpX8M6SSFNx1Pt5KF");
const CORE_AUTHORITY = "FNRyxJLpcGuWwEUXYiXqjJMDsHAAs1gU8CnkjjBRL3ys";
const GAME_CONFIG = new PublicKey("FdA2EJ89Xtqn6Z1pxayBQnSJQ8pVpVAuEjaUW8qbU8nx");
const OWNERSHIP_CONFIG = new PublicKey("6Gj8af5cnLSKFWcdfFLr7fihhNmp97BdPRt76zWemPPZ");
const INITIALIZE_DISCRIMINATOR = Buffer.from([241, 13, 60, 90, 194, 86, 195, 199]);
const OWNERSHIP_DISCRIMINATOR = Buffer.from([227, 236, 116, 131, 86, 134, 101, 163]);
const GAME_CONFIG_DISCRIMINATOR = Buffer.from([45, 146, 146, 33, 170, 69, 96, 133]);
const SCHEMA_VERSION = 1;
const STAMP_RAM_COST = 75;
const APPROVED_BIND_RAM_COST = 250;
const GENERATOR_VERSION = 1;
const POOL_HASH_HEX = "6cea5ce56944a88486cafbb568a628b5aa5664c3db652a79cd90016427aaa4b4";
const COLLECTIONS = [
  { key: "moves", address: "AUQuvgpFDUekeVh2GHri99A59G4YY5LUWdGw9fKTB9wC", name: "Chainbound Moves (Devnet)", uri: "https://chainbound.local/devnet/collections/moves.json" },
  { key: "archives", address: "DP3rTx145BPfunueM9SHjYJXBDCK5GmrdwZjpCvgf6U", name: "Chainbound Archives (Devnet)", uri: "https://chainbound.local/devnet/collections/archives.json" },
  { key: "covers", address: "6wPnru8RDTsnCyujzPMq8v9U8pD6ynRa8es3ZXcesbNs", name: "Chainbound Covers (Devnet)", uri: "https://chainbound.local/devnet/collections/covers.json" },
  { key: "cosmetics", address: "76aVsw9xScewNG4wkiE75asdM5bbZaHS5CG78263yMcV", name: "Chainbound Cosmetics (Devnet)", uri: "https://chainbound.local/devnet/collections/cosmetics.json" },
];

function assert(condition, message) {
  if (!condition) throw new Error(message);
}

function sha256(bytes) {
  return createHash("sha256").update(bytes).digest("hex");
}

function requireBindRamCost() {
  const text = process.env.CHAINBOUND_BIND_RAM_COST;
  assert(text !== undefined && text !== "", "Set CHAINBOUND_BIND_RAM_COST explicitly; no default is permitted.");
  assert(/^\d+$/.test(text), "CHAINBOUND_BIND_RAM_COST must be a non-negative integer.");
  const value = Number(text);
  assert(Number.isSafeInteger(value) && value <= 0xffffffff, "CHAINBOUND_BIND_RAM_COST exceeds u32.");
  assert(value === APPROVED_BIND_RAM_COST, `CHAINBOUND_BIND_RAM_COST must equal the approved Bind pin (${APPROVED_BIND_RAM_COST}).`);
  return value;
}

async function rpcRequest(method, params) {
  const response = await fetch(RPC_URL, {
    method: "POST",
    headers: { "content-type": "application/json" },
    body: JSON.stringify({ jsonrpc: "2.0", id: 1, method, params }),
  });
  assert(response.ok, `${method} HTTP ${response.status}.`);
  const payload = await response.json();
  if (payload.error) throw new Error(`${method}: ${JSON.stringify(payload.error)}`);
  return payload.result;
}

function verifyGameConfig(account) {
  assert(account, "GameConfig is missing.");
  assert(account.owner.equals(PROGRAM_ID), "GameConfig owner mismatch.");
  assert(account.data.length === 89, "GameConfig data length mismatch.");
  assert(account.data.subarray(0, 8).equals(GAME_CONFIG_DISCRIMINATOR), "GameConfig discriminator mismatch.");
  assert(new PublicKey(account.data.subarray(8, 40)).equals(PROGRAM_AUTHORITY), "GameConfig program authority mismatch.");
  assert(new PublicKey(account.data.subarray(40, 72)).equals(RESULT_AUTHORITY), "GameConfig result authority mismatch.");
  assert(account.data.readUInt16LE(72) === 1, "GameConfig schema mismatch.");
  assert(account.data.readBigUInt64LE(80) === 0n, "GameConfig instruction flags are not OFF.");
}

function encodeInitialize(bindRamCost) {
  const data = Buffer.alloc(182);
  let offset = 0;
  INITIALIZE_DISCRIMINATOR.copy(data, offset); offset += 8;
  data.writeUInt16LE(SCHEMA_VERSION, offset); offset += 2;
  for (const collection of COLLECTIONS) {
    new PublicKey(collection.address).toBuffer().copy(data, offset); offset += 32;
  }
  data.writeUInt32LE(STAMP_RAM_COST, offset); offset += 4;
  data.writeUInt32LE(bindRamCost, offset); offset += 4;
  data.writeUInt32LE(GENERATOR_VERSION, offset); offset += 4;
  Buffer.from(POOL_HASH_HEX, "hex").copy(data, offset);
  return data;
}

function decodeSimulatedOwnership(account, expectedBump, bindRamCost) {
  assert(account, "Simulation did not return OwnershipConfig.");
  assert(account.owner === PROGRAM_ID.toBase58(), "Simulated OwnershipConfig owner mismatch.");
  const data = Buffer.from(account.data[0], "base64");
  assert(data.length === 183, `OwnershipConfig data length is ${data.length}, expected 183.`);
  assert(data.subarray(0, 8).equals(OWNERSHIP_DISCRIMINATOR), "OwnershipConfig discriminator mismatch.");
  const decoded = {
    schemaVersion: data.readUInt16LE(8),
    movesCollection: new PublicKey(data.subarray(10, 42)).toBase58(),
    archivesCollection: new PublicKey(data.subarray(42, 74)).toBase58(),
    coversCollection: new PublicKey(data.subarray(74, 106)).toBase58(),
    cosmeticsCollection: new PublicKey(data.subarray(106, 138)).toBase58(),
    stampRamCost: data.readUInt32LE(138),
    bindRamCost: data.readUInt32LE(142),
    generatorVersion: data.readUInt32LE(146),
    poolHashHex: data.subarray(150, 182).toString("hex"),
    bump: data[182],
  };
  assert(decoded.schemaVersion === SCHEMA_VERSION, "Simulated schema mismatch.");
  COLLECTIONS.forEach((item, index) => {
    const field = ["movesCollection", "archivesCollection", "coversCollection", "cosmeticsCollection"][index];
    assert(decoded[field] === item.address, `Simulated ${item.key} collection mismatch.`);
  });
  assert(decoded.stampRamCost === STAMP_RAM_COST, "Simulated Stamp cost mismatch.");
  assert(decoded.bindRamCost === bindRamCost, "Simulated Bind cost mismatch.");
  assert(decoded.generatorVersion === GENERATOR_VERSION, "Simulated generator version mismatch.");
  assert(decoded.poolHashHex === POOL_HASH_HEX, "Simulated pool hash mismatch.");
  assert(decoded.bump === expectedBump, "Simulated OwnershipConfig bump mismatch.");
  return decoded;
}

async function main() {
  const bindRamCost = requireBindRamCost();
  const connection = new Connection(RPC_URL, "confirmed");
  const umi = createUmi(RPC_URL).use(mplCore());
  const genesisHash = await connection.getGenesisHash();
  assert(genesisHash === DEVNET_GENESIS, `RPC is not Solana devnet (${genesisHash}).`);
  const [derivedOwnership, bump] = PublicKey.findProgramAddressSync([Buffer.from("ownership_config")], PROGRAM_ID);
  assert(derivedOwnership.equals(OWNERSHIP_CONFIG), `Derived OwnershipConfig changed to ${derivedOwnership}.`);

  const [gameConfig, ownershipConfig, payerBalance, blockhash] = await Promise.all([
    connection.getAccountInfo(GAME_CONFIG, "confirmed"),
    connection.getAccountInfo(OWNERSHIP_CONFIG, "confirmed"),
    connection.getBalance(PROGRAM_AUTHORITY, "confirmed"),
    connection.getLatestBlockhash("confirmed"),
  ]);
  verifyGameConfig(gameConfig);
  assert(ownershipConfig === null, "OwnershipConfig already exists; refusing to prepare initialize.");

  for (const expected of COLLECTIONS) {
    const decoded = await fetchCollectionV1(umi, expected.address, { commitment: "confirmed" });
    assert(decoded.name === expected.name, `${expected.key} collection name mismatch.`);
    assert(decoded.uri === expected.uri, `${expected.key} collection URI mismatch.`);
    assert(decoded.updateAuthority.toString() === CORE_AUTHORITY, `${expected.key} update authority mismatch.`);
  }

  const instructionData = encodeInitialize(bindRamCost);
  const instruction = new TransactionInstruction({
    programId: PROGRAM_ID,
    keys: [
      { pubkey: OWNERSHIP_CONFIG, isSigner: false, isWritable: true },
      { pubkey: GAME_CONFIG, isSigner: false, isWritable: false },
      { pubkey: PROGRAM_AUTHORITY, isSigner: true, isWritable: true },
      { pubkey: SystemProgram.programId, isSigner: false, isWritable: false },
    ],
    data: instructionData,
  });
  const transaction = new Transaction({ feePayer: PROGRAM_AUTHORITY, recentBlockhash: blockhash.blockhash }).add(instruction);
  const message = transaction.compileMessage();
  const fee = await connection.getFeeForMessage(message, "confirmed");
  assert(fee.value !== null, "RPC did not return a fee.");
  const encoded = transaction.serialize({ requireAllSignatures: false, verifySignatures: false }).toString("base64");
  const simulation = await rpcRequest("simulateTransaction", [encoded, {
    encoding: "base64",
    commitment: "confirmed",
    sigVerify: false,
    replaceRecentBlockhash: true,
    accounts: { encoding: "base64", addresses: [PROGRAM_AUTHORITY.toBase58(), OWNERSHIP_CONFIG.toBase58()] },
  }]);
  assert(simulation.value.err === null, `OwnershipConfig simulation failed: ${JSON.stringify(simulation.value.err)}.`);
  const postConfig = decodeSimulatedOwnership(simulation.value.accounts?.[1], bump, bindRamCost);
  const rentLamports = simulation.value.accounts[1].lamports;

  process.stdout.write(`${JSON.stringify({
    gate: "E",
    warning: "UNSIGNED OWNERSHIPCONFIG PLAN ONLY: no signer was loaded and nothing was submitted.",
    cluster: "devnet",
    rpcUrl: new URL(RPC_URL).origin,
    genesisHash,
    programId: PROGRAM_ID.toBase58(),
    gameConfig: GAME_CONFIG.toBase58(),
    ownershipConfig: OWNERSHIP_CONFIG.toBase58(),
    ownershipConfigBump: bump,
    programAuthorityAndPayer: PROGRAM_AUTHORITY.toBase58(),
    args: {
      schemaVersion: SCHEMA_VERSION,
      movesCollection: COLLECTIONS[0].address,
      archivesCollection: COLLECTIONS[1].address,
      coversCollection: COLLECTIONS[2].address,
      cosmeticsCollection: COLLECTIONS[3].address,
      stampRamCost: STAMP_RAM_COST,
      bindRamCost,
      generatorVersion: GENERATOR_VERSION,
      poolHashHex: POOL_HASH_HEX,
      poolHashBytes: [...Buffer.from(POOL_HASH_HEX, "hex")],
    },
    economics: {
      payerBalanceLamports: payerBalance,
      feeLamports: fee.value,
      rentLamports,
      estimatedTotalLamports: fee.value + rentLamports,
      projectedPayerBalanceLamports: payerBalance - fee.value - rentLamports,
    },
    instruction: {
      discriminatorHex: INITIALIZE_DISCRIMINATOR.toString("hex"),
      dataHex: instructionData.toString("hex"),
      dataSha256: sha256(instructionData),
      accounts: message.accountKeys.map((key, index) => ({
        index,
        pubkey: key.toBase58(),
        signer: message.isAccountSigner(index),
        writable: message.isAccountWritable(index),
      })),
    },
    unsignedTransaction: {
      base64: encoded,
      messageSha256: sha256(message.serialize()),
      recentBlockhash: blockhash.blockhash,
      lastValidBlockHeight: blockhash.lastValidBlockHeight,
      requiredSignatures: message.header.numRequiredSignatures,
    },
    simulation: {
      error: simulation.value.err,
      unitsConsumed: simulation.value.unitsConsumed,
      logs: simulation.value.logs,
      postConfig,
    },
  }, null, 2)}\n`);
}

main().catch((error) => {
  process.stderr.write(`${error instanceof Error ? error.stack || error.message : error}\n`);
  process.exitCode = 1;
});
