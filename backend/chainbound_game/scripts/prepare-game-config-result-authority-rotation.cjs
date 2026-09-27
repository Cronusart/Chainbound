#!/usr/bin/env node
"use strict";

// Builds and simulates the exact unsigned GameConfig result-authority rotation.
// This script loads no keypair, signs nothing, and submits nothing. It is pinned
// to the one-character Gate D correction approved for review: ...Wsrvp... to
// ...WSrvp.... Every other GameConfig field must remain unchanged.

const { createHash } = require("crypto");
const {
  Connection,
  PublicKey,
  Transaction,
  TransactionInstruction,
} = require("@solana/web3.js");

const RPC_URL =
  process.env.CHAINBOUND_SOLANA_RPC_URL || "https://api.devnet.solana.com";
const DEVNET_GENESIS = "EtWTRABZaYq6iMfeYKouRu166VU2xqa1wcaWoxPkrZBG";
const PROGRAM_ID = new PublicKey(
  "9312ay7VrpvNYmyEx8TYCqhafbZ2Y5WGVbt6Qp772uUQ"
);
const PROGRAM_DATA = new PublicKey(
  "5wqBqX7qubopQnteX5RPTwBJrSFA6xnMs9Zbwz2BHfzD"
);
const PROGRAM_AUTHORITY = new PublicKey(
  "DkFmzzzF89tYZu2JWR1uq3H13U5np9NU6MHF4E61gJmG"
);
const CURRENT_RESULT_AUTHORITY = new PublicKey(
  "FaAegH2tBCyDzxXcXyz64qEFWsrvpX8M6SSFNx1Pt5KF"
);
const TARGET_RESULT_AUTHORITY = new PublicKey(
  "FaAegH2tBCyDzxXcXyz64qEFWSrvpX8M6SSFNx1Pt5KF"
);
const GAME_CONFIG = new PublicKey(
  "FdA2EJ89Xtqn6Z1pxayBQnSJQ8pVpVAuEjaUW8qbU8nx"
);
const UPGRADEABLE_LOADER = new PublicKey(
  "BPFLoaderUpgradeab1e11111111111111111111111"
);
const EXPECTED_PROGRAM_BYTES = 714312;
const EXPECTED_PROGRAM_SHA256 =
  "58e78eb59c34b701c50729fdbdd77677232bec12bce1b6e9e0ae7e2a32d2a2ad";
const PROGRAM_DATA_METADATA_BYTES = 45;
const UPDATE_DISCRIMINATOR = Buffer.from([180, 82, 7, 205, 89, 182, 61, 128]);
const GAME_CONFIG_DISCRIMINATOR = Buffer.from([
  45, 146, 146, 33, 170, 69, 96, 133,
]);
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

function differingCharacterIndexes(left, right) {
  assert(
    left.length === right.length,
    "Authority strings have different lengths."
  );
  const indexes = [];
  for (let index = 0; index < left.length; index += 1) {
    if (left[index] !== right[index]) indexes.push(index);
  }
  return indexes;
}

function decodeConfigData(data, label) {
  assert(
    data.length === 89,
    `${label} data length is ${data.length}, expected 89.`
  );
  assert(
    data.subarray(0, 8).equals(GAME_CONFIG_DISCRIMINATOR),
    `${label} discriminator mismatch.`
  );
  return {
    programAuthority: new PublicKey(data.subarray(8, 40)).toBase58(),
    resultAuthority: new PublicKey(data.subarray(40, 72)).toBase58(),
    schemaVersion: data.readUInt16LE(72),
    ramDailyCap: data.readUInt16LE(74),
    ramPerBurn: data.readUInt32LE(76),
    instructionFlags: data.readBigUInt64LE(80),
    bump: data[88],
  };
}

function assertConfig(decoded, expectedResultAuthority, expectedBump, label) {
  assert(
    decoded.programAuthority === PROGRAM_AUTHORITY.toBase58(),
    `${label} program authority mismatch.`
  );
  assert(
    decoded.resultAuthority === expectedResultAuthority.toBase58(),
    `${label} result authority mismatch.`
  );
  assert(
    decoded.schemaVersion === SCHEMA_VERSION,
    `${label} schema version mismatch.`
  );
  assert(decoded.ramDailyCap === RAM_DAILY_CAP, `${label} daily cap mismatch.`);
  assert(
    decoded.ramPerBurn === RAM_PER_BURN,
    `${label} RAM per burn mismatch.`
  );
  assert(
    decoded.instructionFlags === INSTRUCTION_FLAGS,
    `${label} instruction flags mismatch.`
  );
  assert(decoded.bump === expectedBump, `${label} bump mismatch.`);
}

function printableConfig(decoded) {
  return { ...decoded, instructionFlags: decoded.instructionFlags.toString() };
}

function encodeUpdate() {
  const data = Buffer.alloc(8 + 32 + 2 + 2 + 4 + 8);
  let offset = 0;
  UPDATE_DISCRIMINATOR.copy(data, offset);
  offset += 8;
  TARGET_RESULT_AUTHORITY.toBuffer().copy(data, offset);
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

async function rpcRequest(method, params) {
  const response = await fetch(RPC_URL, {
    method: "POST",
    headers: { "content-type": "application/json" },
    body: JSON.stringify({ jsonrpc: "2.0", id: 1, method, params }),
  });
  assert(response.ok, `${method} HTTP ${response.status}.`);
  const payload = await response.json();
  if (payload.error)
    throw new Error(`${method}: ${JSON.stringify(payload.error)}`);
  return payload.result;
}

async function main() {
  const currentText = CURRENT_RESULT_AUTHORITY.toBase58();
  const targetText = TARGET_RESULT_AUTHORITY.toBase58();
  const differingIndexes = differingCharacterIndexes(currentText, targetText);
  assert(
    differingIndexes.length === 1 && differingIndexes[0] === 25,
    "Pinned authority correction is not index 25 only."
  );
  assert(
    currentText.toLowerCase() === targetText.toLowerCase(),
    "Pinned authorities are not casefold-equal."
  );

  const connection = new Connection(RPC_URL, "confirmed");
  const genesisHash = await connection.getGenesisHash();
  assert(
    genesisHash === DEVNET_GENESIS,
    `RPC is not Solana devnet (${genesisHash}).`
  );
  const [derivedConfig, configBump] = PublicKey.findProgramAddressSync(
    [Buffer.from("game_config")],
    PROGRAM_ID
  );
  assert(
    derivedConfig.equals(GAME_CONFIG),
    `Derived GameConfig changed to ${derivedConfig}.`
  );

  const [program, programData, config, payerBalance, blockhash] =
    await Promise.all([
      connection.getAccountInfo(PROGRAM_ID, "confirmed"),
      connection.getAccountInfo(PROGRAM_DATA, "confirmed"),
      connection.getAccountInfo(GAME_CONFIG, "confirmed"),
      connection.getBalance(PROGRAM_AUTHORITY, "confirmed"),
      connection.getLatestBlockhash("confirmed"),
    ]);
  assert(program?.owner.equals(UPGRADEABLE_LOADER), "Program owner mismatch.");
  assert(
    program.data.readUInt32LE(0) === 2,
    "Program account variant mismatch."
  );
  assert(
    new PublicKey(program.data.subarray(4, 36)).equals(PROGRAM_DATA),
    "ProgramData link mismatch."
  );
  assert(
    programData?.owner.equals(UPGRADEABLE_LOADER),
    "ProgramData owner mismatch."
  );
  assert(
    programData.data.readUInt32LE(0) === 3,
    "ProgramData variant mismatch."
  );
  assert(programData.data[12] === 1, "ProgramData is immutable.");
  assert(
    new PublicKey(programData.data.subarray(13, 45)).equals(PROGRAM_AUTHORITY),
    "Upgrade authority mismatch."
  );
  const payload = programData.data.subarray(PROGRAM_DATA_METADATA_BYTES);
  assert(
    payload.length === EXPECTED_PROGRAM_BYTES,
    `Program payload length changed to ${payload.length}.`
  );
  assert(
    sha256(payload) === EXPECTED_PROGRAM_SHA256,
    "Deployed program hash mismatch."
  );

  assert(
    config?.owner.equals(PROGRAM_ID),
    "GameConfig missing or wrong owner."
  );
  const beforeConfig = decodeConfigData(config.data, "Live GameConfig");
  assertConfig(
    beforeConfig,
    CURRENT_RESULT_AUTHORITY,
    configBump,
    "Live GameConfig"
  );

  const instructionData = encodeUpdate();
  const instruction = new TransactionInstruction({
    programId: PROGRAM_ID,
    keys: [
      { pubkey: GAME_CONFIG, isSigner: false, isWritable: true },
      { pubkey: PROGRAM_AUTHORITY, isSigner: true, isWritable: false },
    ],
    data: instructionData,
  });
  const transaction = new Transaction({
    feePayer: PROGRAM_AUTHORITY,
    recentBlockhash: blockhash.blockhash,
  }).add(instruction);
  const message = transaction.compileMessage();
  const fee = await connection.getFeeForMessage(message, "confirmed");
  assert(fee.value !== null, "RPC did not return the rotation fee.");
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
      accounts: { encoding: "base64", addresses: [GAME_CONFIG.toBase58()] },
    },
  ]);
  assert(
    simulation.value.err === null,
    `Rotation simulation failed: ${JSON.stringify(simulation.value.err)}.`
  );
  const simulatedAccount = simulation.value.accounts?.[0];
  assert(simulatedAccount, "Simulation did not return GameConfig post-state.");
  assert(
    simulatedAccount.owner === PROGRAM_ID.toBase58(),
    "Simulated GameConfig owner mismatch."
  );
  assert(
    simulatedAccount.lamports === config.lamports,
    "Rotation simulation changed GameConfig lamports."
  );
  const afterConfig = decodeConfigData(
    Buffer.from(simulatedAccount.data[0], "base64"),
    "Simulated GameConfig"
  );
  assertConfig(
    afterConfig,
    TARGET_RESULT_AUTHORITY,
    configBump,
    "Simulated GameConfig"
  );

  process.stdout.write(
    `${JSON.stringify(
      {
        gate: "GameConfig result-authority rotation PREP",
        warning:
          "UNSIGNED PLAN ONLY: no keypair was loaded, nothing was signed, and nothing was submitted.",
        cluster: "devnet",
        rpcUrl: new URL(RPC_URL).origin,
        genesisHash,
        programId: PROGRAM_ID.toBase58(),
        programData: PROGRAM_DATA.toBase58(),
        deployedProgramSha256: sha256(payload),
        gameConfig: GAME_CONFIG.toBase58(),
        programAuthorityAndPayer: PROGRAM_AUTHORITY.toBase58(),
        authorityRotation: {
          from: currentText,
          to: targetText,
          differingCharacterIndexesZeroBased: differingIndexes,
          casefoldEqual: currentText.toLowerCase() === targetText.toLowerCase(),
        },
        args: {
          resultAuthority: targetText,
          schemaVersion: SCHEMA_VERSION,
          ramDailyCap: RAM_DAILY_CAP,
          ramPerBurn: RAM_PER_BURN,
          instructionFlags: INSTRUCTION_FLAGS.toString(),
        },
        economics: {
          payerBalanceLamports: payerBalance,
          feeLamports: fee.value,
          rentLamports: 0,
          totalOutflowLamports: fee.value,
          projectedPayerBalanceLamports: payerBalance - fee.value,
          gameConfigLamportsBeforeAndAfter: config.lamports,
        },
        instruction: {
          name: "update_game_config",
          discriminatorHex: UPDATE_DISCRIMINATOR.toString("hex"),
          dataHex: instructionData.toString("hex"),
          dataSha256: sha256(instructionData),
          accounts: message.accountKeys.map((key, index) => ({
            index,
            pubkey: key.toBase58(),
            signer: message.isAccountSigner(index),
            writable: message.isAccountWritable(index),
          })),
        },
        liveConfigBefore: printableConfig(beforeConfig),
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
          postConfig: printableConfig(afterConfig),
        },
      },
      null,
      2
    )}\n`
  );
}

main().catch((error) => {
  process.stderr.write(
    `${error instanceof Error ? error.stack || error.message : error}\n`
  );
  process.exitCode = 1;
});
