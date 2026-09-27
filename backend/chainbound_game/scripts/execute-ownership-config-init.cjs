#!/usr/bin/env node
"use strict";

// Operator-only Gate E executor. It is hard-pinned to the approved proposal:
// bind_ram_cost=250, Stamp=75, generator=1, the canonical pool hash, and the
// four verified Core collections. Codex does not invoke this signer-side file.

const { readFileSync } = require("fs");
const { Connection, Keypair, PublicKey, SystemProgram, Transaction, TransactionInstruction } = require("@solana/web3.js");
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
const APPROVAL_TOKEN = "APPROVED_GATE_E_INITIALIZE_OWNERSHIPCONFIG_BIND_250";
const SCHEMA_VERSION = 1;
const STAMP_RAM_COST = 75;
const BIND_RAM_COST = 250;
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

function loadOperator(path) {
  const values = JSON.parse(readFileSync(path, "utf8"));
  assert(Array.isArray(values) && values.length === 64 && values.every(Number.isInteger), "Operator keypair file is invalid.");
  const keypair = Keypair.fromSecretKey(Uint8Array.from(values));
  assert(keypair.publicKey.equals(PROGRAM_AUTHORITY), "Operator keypair does not match pinned authority.");
  return keypair;
}

function verifyGameConfig(account) {
  assert(account?.owner.equals(PROGRAM_ID), "GameConfig missing or wrong owner.");
  const data = account.data;
  assert(data.length === 89 && data.subarray(0, 8).equals(GAME_CONFIG_DISCRIMINATOR), "GameConfig layout mismatch.");
  assert(new PublicKey(data.subarray(8, 40)).equals(PROGRAM_AUTHORITY), "GameConfig program authority mismatch.");
  assert(new PublicKey(data.subarray(40, 72)).equals(RESULT_AUTHORITY), "GameConfig result authority mismatch.");
  assert(data.readUInt16LE(72) === 1, "GameConfig schema mismatch.");
  assert(data.readUInt16LE(74) === 100, "GameConfig daily cap mismatch.");
  assert(data.readUInt32LE(76) === 10, "GameConfig RAM per burn mismatch.");
  assert(data.readBigUInt64LE(80) === 0n, "GameConfig flags are not OFF.");
}

function encodeInitialize() {
  const data = Buffer.alloc(182);
  let offset = 0;
  INITIALIZE_DISCRIMINATOR.copy(data, offset); offset += 8;
  data.writeUInt16LE(SCHEMA_VERSION, offset); offset += 2;
  for (const item of COLLECTIONS) {
    new PublicKey(item.address).toBuffer().copy(data, offset); offset += 32;
  }
  data.writeUInt32LE(STAMP_RAM_COST, offset); offset += 4;
  data.writeUInt32LE(BIND_RAM_COST, offset); offset += 4;
  data.writeUInt32LE(GENERATOR_VERSION, offset); offset += 4;
  Buffer.from(POOL_HASH_HEX, "hex").copy(data, offset);
  return data;
}

function decodeAndVerify(account, expectedBump) {
  assert(account?.owner.equals(PROGRAM_ID), "OwnershipConfig missing or wrong owner.");
  const data = account.data;
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
  assert(decoded.schemaVersion === SCHEMA_VERSION, "OwnershipConfig schema mismatch.");
  const fields = ["movesCollection", "archivesCollection", "coversCollection", "cosmeticsCollection"];
  COLLECTIONS.forEach((item, index) => assert(decoded[fields[index]] === item.address, `${item.key} collection mismatch.`));
  assert(decoded.stampRamCost === STAMP_RAM_COST, "Stamp cost mismatch.");
  assert(decoded.bindRamCost === BIND_RAM_COST, "Bind cost mismatch.");
  assert(decoded.generatorVersion === GENERATOR_VERSION, "Generator version mismatch.");
  assert(decoded.poolHashHex === POOL_HASH_HEX, "Pool hash mismatch.");
  assert(decoded.bump === expectedBump, "OwnershipConfig bump mismatch.");
  return decoded;
}

async function main() {
  assert(
    process.env.CHAINBOUND_GATE_E_APPROVAL === APPROVAL_TOKEN,
    `Refusing to sign: set CHAINBOUND_GATE_E_APPROVAL=${APPROVAL_TOKEN} only after explicit Gate E approval.`,
  );
  assert(process.env.CHAINBOUND_OPERATOR_KEYPAIR, "CHAINBOUND_OPERATOR_KEYPAIR is required.");
  const operator = loadOperator(process.env.CHAINBOUND_OPERATOR_KEYPAIR);
  const connection = new Connection(RPC_URL, "confirmed");
  const umi = createUmi(RPC_URL).use(mplCore());
  const genesisHash = await connection.getGenesisHash();
  assert(genesisHash === DEVNET_GENESIS, `RPC is not Solana devnet (${genesisHash}).`);
  const [derived, bump] = PublicKey.findProgramAddressSync([Buffer.from("ownership_config")], PROGRAM_ID);
  assert(derived.equals(OWNERSHIP_CONFIG), `Derived OwnershipConfig changed to ${derived}.`);
  verifyGameConfig(await connection.getAccountInfo(GAME_CONFIG, "confirmed"));

  for (const expected of COLLECTIONS) {
    const decoded = await fetchCollectionV1(umi, expected.address, { commitment: "confirmed" });
    assert(decoded.name === expected.name && decoded.uri === expected.uri, `${expected.key} metadata mismatch.`);
    assert(decoded.updateAuthority.toString() === CORE_AUTHORITY, `${expected.key} update authority mismatch.`);
  }

  const existing = await connection.getAccountInfo(OWNERSHIP_CONFIG, "confirmed");
  if (existing) {
    const decoded = decodeAndVerify(existing, bump);
    process.stdout.write(`${JSON.stringify({
      verified: true,
      gate: "E",
      cluster: "devnet",
      ownershipConfig: OWNERSHIP_CONFIG.toBase58(),
      signature: null,
      resumedExistingVerifiedConfig: true,
      account: decoded,
    }, null, 2)}\n`);
    return;
  }

  const instruction = new TransactionInstruction({
    programId: PROGRAM_ID,
    keys: [
      { pubkey: OWNERSHIP_CONFIG, isSigner: false, isWritable: true },
      { pubkey: GAME_CONFIG, isSigner: false, isWritable: false },
      { pubkey: PROGRAM_AUTHORITY, isSigner: true, isWritable: true },
      { pubkey: SystemProgram.programId, isSigner: false, isWritable: false },
    ],
    data: encodeInitialize(),
  });
  const maxAttempts = Number(process.env.CHAINBOUND_GATE_E_BLOCKHASH_ATTEMPTS || 10);
  let signature = null;
  let confirmation = null;
  for (let attempt = 1; attempt <= maxAttempts; attempt += 1) {
    const latest = await connection.getLatestBlockhash("finalized");
    const transaction = new Transaction({ feePayer: PROGRAM_AUTHORITY, recentBlockhash: latest.blockhash }).add(instruction);
    transaction.sign(operator);
    process.stderr.write(`Gate E: skipPreflight send (attempt ${attempt}/${maxAttempts})\n`);
    try {
      signature = await connection.sendRawTransaction(transaction.serialize(), {
        skipPreflight: true,
        preflightCommitment: "confirmed",
        maxRetries: 15,
      });
      confirmation = await connection.confirmTransaction({
        signature,
        blockhash: latest.blockhash,
        lastValidBlockHeight: latest.lastValidBlockHeight,
      }, "confirmed");
      assert(confirmation.value.err === null, "OwnershipConfig transaction failed.");
      break;
    } catch (error) {
      const message = String(error);
      if ((message.includes("BlockhashNotFound") || message.includes("block height exceeded")) && attempt < maxAttempts) {
        await new Promise((resolve) => setTimeout(resolve, 500 * attempt));
        continue;
      }
      throw error;
    }
  }
  assert(signature && confirmation, `OwnershipConfig send failed after ${maxAttempts} attempts.`);
  const account = await connection.getAccountInfo(OWNERSHIP_CONFIG, "confirmed");
  const decoded = decodeAndVerify(account, bump);
  const record = await connection.getTransaction(signature, { commitment: "confirmed", maxSupportedTransactionVersion: 0 });
  assert(record?.meta?.err === null, "Confirmed OwnershipConfig transaction record failed.");
  process.stdout.write(`${JSON.stringify({
    verified: true,
    gate: "E",
    cluster: "devnet",
    rpcUrl: new URL(RPC_URL).origin,
    genesisHash,
    programId: PROGRAM_ID.toBase58(),
    gameConfig: GAME_CONFIG.toBase58(),
    ownershipConfig: OWNERSHIP_CONFIG.toBase58(),
    signature,
    slot: record.slot,
    feeLamports: record.meta.fee,
    resumedExistingVerifiedConfig: false,
    account: decoded,
  }, null, 2)}\n`);
}

main().catch((error) => {
  process.stderr.write(`${error instanceof Error ? error.stack || error.message : error}\n`);
  process.exitCode = 1;
});
