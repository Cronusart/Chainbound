#!/usr/bin/env node
"use strict";

// Builds and simulates the exact unsigned GameConfig initialization. It loads
// no signer and submits nothing.

const { createHash } = require("crypto");
const bs58Module = require("bs58");
const bs58 = bs58Module.default || bs58Module;
const {
  Connection,
  PublicKey,
  SystemProgram,
  Transaction,
  TransactionInstruction,
} = require("@solana/web3.js");

const RPC_URL = process.env.CHAINBOUND_SOLANA_RPC_URL || "https://api.devnet.solana.com";
const DEVNET_GENESIS = "EtWTRABZaYq6iMfeYKouRu166VU2xqa1wcaWoxPkrZBG";
const PROGRAM_ID = new PublicKey("9312ay7VrpvNYmyEx8TYCqhafbZ2Y5WGVbt6Qp772uUQ");
const PROGRAM_DATA = new PublicKey("5wqBqX7qubopQnteX5RPTwBJrSFA6xnMs9Zbwz2BHfzD");
const PROGRAM_AUTHORITY = new PublicKey("DkFmzzzF89tYZu2JWR1uq3H13U5np9NU6MHF4E61gJmG");
const RESULT_AUTHORITY = new PublicKey("FaAegH2tBCyDzxXcXyz64qEFWsrvpX8M6SSFNx1Pt5KF");
const EXPECTED_CONFIG = new PublicKey("FdA2EJ89Xtqn6Z1pxayBQnSJQ8pVpVAuEjaUW8qbU8nx");
const UPGRADEABLE_LOADER = new PublicKey("BPFLoaderUpgradeab1e11111111111111111111111");
const EXPECTED_PROGRAM_BYTES = 714312;
const EXPECTED_PROGRAM_SHA256 = "58e78eb59c34b701c50729fdbdd77677232bec12bce1b6e9e0ae7e2a32d2a2ad";
const PROGRAM_DATA_METADATA_BYTES = 45;
const INITIALIZE_DISCRIMINATOR = Buffer.from([175, 175, 109, 31, 13, 152, 155, 237]);
const GAME_CONFIG_ACCOUNT_DISCRIMINATOR = Buffer.from([45, 146, 146, 33, 170, 69, 96, 133]);
const SCHEMA_VERSION = 1;
const RAM_DAILY_CAP = 100;
const RAM_PER_BURN = 10;
const INSTRUCTION_FLAGS = 0n;

function assert(condition, message) {
  if (!condition) throw new Error(message);
}

function sha256(bytes) {
  return createHash("sha256").update(bytes).digest("hex");
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

function encodeInitialize() {
  const data = Buffer.alloc(8 + 32 + 2 + 2 + 4 + 8);
  let offset = 0;
  INITIALIZE_DISCRIMINATOR.copy(data, offset);
  offset += 8;
  RESULT_AUTHORITY.toBuffer().copy(data, offset);
  offset += 32;
  data.writeUInt16LE(SCHEMA_VERSION, offset);
  offset += 2;
  data.writeUInt16LE(RAM_DAILY_CAP, offset);
  offset += 2;
  data.writeUInt32LE(RAM_PER_BURN, offset);
  offset += 4;
  data.writeBigUInt64LE(INSTRUCTION_FLAGS, offset);
  return data;
}

function decodeSimulatedConfig(account, expectedBump) {
  assert(account, "Simulation did not return the GameConfig account.");
  assert(account.owner === PROGRAM_ID.toBase58(), "Simulated GameConfig owner mismatch.");
  const data = Buffer.from(account.data[0], "base64");
  assert(data.length === 89, `Simulated GameConfig data length is ${data.length}, expected 89.`);
  assert(
    data.subarray(0, 8).equals(GAME_CONFIG_ACCOUNT_DISCRIMINATOR),
    "Simulated GameConfig discriminator mismatch.",
  );
  const decoded = {
    programAuthority: new PublicKey(data.subarray(8, 40)).toBase58(),
    resultAuthority: new PublicKey(data.subarray(40, 72)).toBase58(),
    schemaVersion: data.readUInt16LE(72),
    ramDailyCap: data.readUInt16LE(74),
    ramPerBurn: data.readUInt32LE(76),
    instructionFlags: data.readBigUInt64LE(80),
    bump: data[88],
  };
  assert(decoded.programAuthority === PROGRAM_AUTHORITY.toBase58(), "Simulated program authority mismatch.");
  assert(decoded.resultAuthority === RESULT_AUTHORITY.toBase58(), "Simulated result authority mismatch.");
  assert(decoded.schemaVersion === SCHEMA_VERSION, "Simulated schema version mismatch.");
  assert(decoded.ramDailyCap === RAM_DAILY_CAP, "Simulated daily cap mismatch.");
  assert(decoded.ramPerBurn === RAM_PER_BURN, "Simulated RAM per burn mismatch.");
  assert(decoded.instructionFlags === INSTRUCTION_FLAGS, "Simulated instruction flags mismatch.");
  assert(decoded.bump === expectedBump, "Simulated GameConfig bump mismatch.");
  return { ...decoded, instructionFlags: decoded.instructionFlags.toString() };
}

async function main() {
  const connection = new Connection(RPC_URL, "confirmed");
  const genesisHash = await connection.getGenesisHash();
  assert(genesisHash === DEVNET_GENESIS, `RPC is not Solana devnet (${genesisHash}).`);
  const [derivedConfig, configBump] = PublicKey.findProgramAddressSync(
    [Buffer.from("game_config")],
    PROGRAM_ID,
  );
  assert(derivedConfig.equals(EXPECTED_CONFIG), `Derived GameConfig changed to ${derivedConfig}.`);

  const [program, programData, config, payerBalance, blockhash] = await Promise.all([
    connection.getAccountInfo(PROGRAM_ID, "confirmed"),
    connection.getAccountInfo(PROGRAM_DATA, "confirmed"),
    connection.getAccountInfo(EXPECTED_CONFIG, "confirmed"),
    connection.getBalance(PROGRAM_AUTHORITY, "confirmed"),
    connection.getLatestBlockhash("confirmed"),
  ]);
  assert(config === null, "GameConfig already exists; refusing to prepare initialize.");
  assert(program?.owner.equals(UPGRADEABLE_LOADER), "Program owner mismatch.");
  assert(program.data.readUInt32LE(0) === 2, "Program account variant mismatch.");
  assert(new PublicKey(program.data.subarray(4, 36)).equals(PROGRAM_DATA), "ProgramData link mismatch.");
  assert(programData?.owner.equals(UPGRADEABLE_LOADER), "ProgramData owner mismatch.");
  assert(programData.data.readUInt32LE(0) === 3, "ProgramData variant mismatch.");
  assert(programData.data[12] === 1, "ProgramData is immutable.");
  assert(
    new PublicKey(programData.data.subarray(13, 45)).equals(PROGRAM_AUTHORITY),
    "Program upgrade authority mismatch.",
  );
  const payload = programData.data.subarray(PROGRAM_DATA_METADATA_BYTES);
  assert(payload.length === EXPECTED_PROGRAM_BYTES, `Program capacity changed to ${payload.length}.`);
  assert(sha256(payload) === EXPECTED_PROGRAM_SHA256, "Deployed program hash mismatch.");

  const instructionData = encodeInitialize();
  const instruction = new TransactionInstruction({
    programId: PROGRAM_ID,
    keys: [
      { pubkey: EXPECTED_CONFIG, isSigner: false, isWritable: true },
      { pubkey: PROGRAM_AUTHORITY, isSigner: true, isWritable: true },
      { pubkey: SystemProgram.programId, isSigner: false, isWritable: false },
    ],
    data: instructionData,
  });
  const transaction = new Transaction({
    feePayer: PROGRAM_AUTHORITY,
    recentBlockhash: blockhash.blockhash,
  }).add(instruction);
  const message = transaction.compileMessage();
  const fee = await connection.getFeeForMessage(message, "confirmed");
  assert(fee.value !== null, "RPC did not return the initialization fee.");
  const encoded = transaction
    .serialize({ requireAllSignatures: false, verifySignatures: false })
    .toString("base64");
  const simulation = await rpcRequest("simulateTransaction", [
    encoded,
    {
      encoding: "base64",
      commitment: "confirmed",
      sigVerify: false,
      replaceRecentBlockhash: true,
      accounts: {
        encoding: "base64",
        addresses: [PROGRAM_AUTHORITY.toBase58(), EXPECTED_CONFIG.toBase58()],
      },
    },
  ]);
  assert(simulation.value.err === null, `GameConfig simulation failed: ${JSON.stringify(simulation.value.err)}.`);
  const simulatedConfig = decodeSimulatedConfig(simulation.value.accounts?.[1], configBump);
  const rentLamports = simulation.value.accounts[1].lamports;

  process.stdout.write(
    `${JSON.stringify(
      {
        gate: "D",
        warning: "UNSIGNED GAMECONFIG PLAN ONLY: no signer was loaded and nothing was submitted.",
        cluster: "devnet",
        rpcUrl: new URL(RPC_URL).origin,
        genesisHash,
        programId: PROGRAM_ID.toBase58(),
        programData: PROGRAM_DATA.toBase58(),
        deployedProgramSha256: sha256(payload),
        gameConfig: EXPECTED_CONFIG.toBase58(),
        gameConfigBump: configBump,
        programAuthorityAndPayer: PROGRAM_AUTHORITY.toBase58(),
        args: {
          resultAuthority: RESULT_AUTHORITY.toBase58(),
          schemaVersion: SCHEMA_VERSION,
          ramDailyCap: RAM_DAILY_CAP,
          ramPerBurn: RAM_PER_BURN,
          instructionFlags: INSTRUCTION_FLAGS.toString(),
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
          postConfig: simulatedConfig,
        },
      },
      null,
      2,
    )}\n`,
  );
}

main().catch((error) => {
  process.stderr.write(`${error instanceof Error ? error.stack || error.message : error}\n`);
  process.exitCode = 1;
});
