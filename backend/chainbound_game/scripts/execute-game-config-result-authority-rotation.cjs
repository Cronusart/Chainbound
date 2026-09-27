#!/usr/bin/env node
"use strict";

// Operator-only executor for the explicitly approved one-character GameConfig
// result-authority correction. Codex never invokes this file: it loads the
// DkFm operator keypair locally, signs, and submits on Devnet.

const { createHash } = require("crypto");
const { readFileSync } = require("fs");
const {
  Connection,
  Keypair,
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
const APPROVAL_TOKEN =
  "APPROVED_GAMECONFIG_RESULT_AUTHORITY_ROTATION_INDEX25_LOWER_S_TO_UPPER_S";
const SCHEMA_VERSION = 1;
const RAM_DAILY_CAP = 100;
const RAM_PER_BURN = 10;
const INSTRUCTION_FLAGS = 0n;
const EXPECTED_FEE_LAMPORTS = 5000;

function assert(condition, message) {
  if (!condition) throw new Error(message);
}

function sha256(bytes) {
  return createHash("sha256").update(bytes).digest("hex");
}

function loadOperator(path) {
  const values = JSON.parse(readFileSync(path.trim(), "utf8"));
  assert(
    Array.isArray(values) &&
      values.length === 64 &&
      values.every(
        (value) => Number.isInteger(value) && value >= 0 && value <= 255
      ),
    "Operator keypair file is invalid."
  );
  const keypair = Keypair.fromSecretKey(Uint8Array.from(values));
  assert(
    keypair.publicKey.equals(PROGRAM_AUTHORITY),
    "Operator keypair does not match DkFm authority."
  );
  return keypair;
}

function decodeConfig(account, expectedBump, label) {
  assert(account?.owner.equals(PROGRAM_ID), `${label} missing or wrong owner.`);
  const data = account.data;
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
    lamports: account.lamports,
    expectedBump,
  };
}

function assertConfig(decoded, resultAuthority, expectedBump, label) {
  assert(
    decoded.programAuthority === PROGRAM_AUTHORITY.toBase58(),
    `${label} program authority mismatch.`
  );
  assert(
    decoded.resultAuthority === resultAuthority.toBase58(),
    `${label} result authority mismatch.`
  );
  assert(decoded.schemaVersion === SCHEMA_VERSION, `${label} schema mismatch.`);
  assert(decoded.ramDailyCap === RAM_DAILY_CAP, `${label} daily cap mismatch.`);
  assert(
    decoded.ramPerBurn === RAM_PER_BURN,
    `${label} RAM per burn mismatch.`
  );
  assert(
    decoded.instructionFlags === INSTRUCTION_FLAGS,
    `${label} flags changed.`
  );
  assert(decoded.bump === expectedBump, `${label} bump mismatch.`);
}

function printableConfig(decoded) {
  const { expectedBump: _expectedBump, ...rest } = decoded;
  return { ...rest, instructionFlags: decoded.instructionFlags.toString() };
}

function encodeUpdate() {
  const data = Buffer.alloc(56);
  UPDATE_DISCRIMINATOR.copy(data, 0);
  TARGET_RESULT_AUTHORITY.toBuffer().copy(data, 8);
  data.writeUInt16LE(SCHEMA_VERSION, 40);
  data.writeUInt16LE(RAM_DAILY_CAP, 42);
  data.writeUInt32LE(RAM_PER_BURN, 44);
  data.writeBigUInt64LE(INSTRUCTION_FLAGS, 48);
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
  assert(
    process.env.CHAINBOUND_GAMECONFIG_ROTATION_APPROVAL === APPROVAL_TOKEN,
    `Refusing to sign: set CHAINBOUND_GAMECONFIG_ROTATION_APPROVAL=${APPROVAL_TOKEN}.`
  );
  assert(
    process.env.CHAINBOUND_OPERATOR_KEYPAIR?.trim(),
    "CHAINBOUND_OPERATOR_KEYPAIR is required."
  );

  const connection = new Connection(RPC_URL, "confirmed");
  const genesisHash = await connection.getGenesisHash();
  assert(
    genesisHash === DEVNET_GENESIS,
    `RPC is not Solana devnet (${genesisHash}).`
  );
  const [derivedConfig, bump] = PublicKey.findProgramAddressSync(
    [Buffer.from("game_config")],
    PROGRAM_ID
  );
  assert(
    derivedConfig.equals(GAME_CONFIG),
    `Derived GameConfig changed to ${derivedConfig}.`
  );

  const [program, programData, beforeAccount] = await Promise.all([
    connection.getAccountInfo(PROGRAM_ID, "confirmed"),
    connection.getAccountInfo(PROGRAM_DATA, "confirmed"),
    connection.getAccountInfo(GAME_CONFIG, "confirmed"),
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
  assert(programData.data[12] === 1, "ProgramData has no upgrade authority.");
  assert(
    new PublicKey(programData.data.subarray(13, 45)).equals(PROGRAM_AUTHORITY),
    "Upgrade authority mismatch."
  );
  const payload = programData.data.subarray(PROGRAM_DATA_METADATA_BYTES);
  assert(
    payload.length === EXPECTED_PROGRAM_BYTES,
    "Program payload length changed."
  );
  assert(
    sha256(payload) === EXPECTED_PROGRAM_SHA256,
    "Deployed program hash changed."
  );

  const before = decodeConfig(beforeAccount, bump, "Live GameConfig");
  if (before.resultAuthority === TARGET_RESULT_AUTHORITY.toBase58()) {
    assertConfig(before, TARGET_RESULT_AUTHORITY, bump, "Existing GameConfig");
    process.stdout.write(
      `${JSON.stringify(
        {
          verified: true,
          operation: "GameConfig result-authority rotation",
          cluster: "devnet",
          gameConfig: GAME_CONFIG.toBase58(),
          signature: null,
          resumedExistingVerifiedRotation: true,
          account: printableConfig(before),
        },
        null,
        2
      )}\n`
    );
    return;
  }
  assertConfig(
    before,
    CURRENT_RESULT_AUTHORITY,
    bump,
    "Pre-rotation GameConfig"
  );

  const operator = loadOperator(process.env.CHAINBOUND_OPERATOR_KEYPAIR);
  const instructionData = encodeUpdate();
  const instruction = new TransactionInstruction({
    programId: PROGRAM_ID,
    keys: [
      { pubkey: GAME_CONFIG, isSigner: false, isWritable: true },
      { pubkey: PROGRAM_AUTHORITY, isSigner: true, isWritable: false },
    ],
    data: instructionData,
  });
  const latest = await connection.getLatestBlockhash("finalized");
  const transaction = new Transaction({
    feePayer: PROGRAM_AUTHORITY,
    recentBlockhash: latest.blockhash,
  }).add(instruction);
  const fee = await connection.getFeeForMessage(
    transaction.compileMessage(),
    "confirmed"
  );
  assert(
    fee.value === EXPECTED_FEE_LAMPORTS,
    `Rotation fee changed to ${fee.value}.`
  );
  transaction.sign(operator);
  const serialized = transaction.serialize();

  const simulation = await rpcRequest("simulateTransaction", [
    serialized.toString("base64"),
    {
      encoding: "base64",
      commitment: "confirmed",
      sigVerify: true,
      replaceRecentBlockhash: false,
      accounts: { encoding: "base64", addresses: [GAME_CONFIG.toBase58()] },
    },
  ]);
  assert(
    simulation.value.err === null,
    `Signed rotation simulation failed: ${JSON.stringify(
      simulation.value.err
    )}.`
  );
  const simulatedAccount = simulation.value.accounts?.[0];
  assert(
    simulatedAccount?.owner === PROGRAM_ID.toBase58(),
    "Simulated GameConfig owner mismatch."
  );
  assert(
    simulatedAccount.lamports === before.lamports,
    "Simulation changed GameConfig lamports."
  );
  const simulated = decodeConfig(
    {
      owner: PROGRAM_ID,
      data: Buffer.from(simulatedAccount.data[0], "base64"),
      lamports: simulatedAccount.lamports,
    },
    bump,
    "Simulated GameConfig"
  );
  assertConfig(
    simulated,
    TARGET_RESULT_AUTHORITY,
    bump,
    "Simulated GameConfig"
  );

  process.stderr.write(
    "Signed simulation passed; sending the exact simulated bytes to Devnet.\n"
  );
  const signature = await connection.sendRawTransaction(serialized, {
    skipPreflight: false,
    preflightCommitment: "confirmed",
    maxRetries: 15,
  });
  process.stderr.write(`Rotation submitted: ${signature}\n`);
  const confirmation = await connection.confirmTransaction(
    {
      signature,
      blockhash: latest.blockhash,
      lastValidBlockHeight: latest.lastValidBlockHeight,
    },
    "finalized"
  );
  assert(confirmation.value.err === null, "Rotation transaction failed.");

  const [afterAccount, record, payerBalance] = await Promise.all([
    connection.getAccountInfo(GAME_CONFIG, "finalized"),
    connection.getTransaction(signature, {
      commitment: "finalized",
      maxSupportedTransactionVersion: 0,
    }),
    connection.getBalance(PROGRAM_AUTHORITY, "finalized"),
  ]);
  assert(record?.meta?.err === null, "Finalized rotation record failed.");
  assert(record.meta.fee === EXPECTED_FEE_LAMPORTS, "Finalized fee mismatch.");
  const after = decodeConfig(afterAccount, bump, "Finalized GameConfig");
  assertConfig(after, TARGET_RESULT_AUTHORITY, bump, "Finalized GameConfig");
  assert(
    after.lamports === before.lamports,
    "Finalized rotation changed GameConfig lamports."
  );

  process.stdout.write(
    `${JSON.stringify(
      {
        verified: true,
        operation: "GameConfig result-authority rotation",
        cluster: "devnet",
        rpcUrl: new URL(RPC_URL).origin,
        genesisHash,
        programId: PROGRAM_ID.toBase58(),
        gameConfig: GAME_CONFIG.toBase58(),
        signature,
        slot: record.slot,
        confirmationStatus: "finalized",
        feeLamports: record.meta.fee,
        instructionDataSha256: sha256(instructionData),
        signedMessageSha256: sha256(transaction.serializeMessage()),
        simulationUnitsConsumed: simulation.value.unitsConsumed,
        payerBalanceLamports: payerBalance,
        before: printableConfig(before),
        after: printableConfig(after),
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
