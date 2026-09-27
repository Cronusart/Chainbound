#!/usr/bin/env node
"use strict";

// Devnet-only owner bootstrap for the pinned Ownership Spine smoke wallet.
// Prepare mode is keyless and never signs or sends. Execute mode requires a
// separate approval token and reads the matching keypair only from a local path.

const crypto = require("crypto");
const { mkdirSync, readFileSync, writeFileSync } = require("fs");
const { join, resolve } = require("path");
const {
  Connection,
  Keypair,
  PublicKey,
  SystemProgram,
  Transaction,
  TransactionInstruction,
} = require("@solana/web3.js");

const MODE = String(process.env.CHAINBOUND_BOOTSTRAP_MODE || "prepare").trim();
const APPROVAL = "APPROVED_DEVNET_SMOKE_PLAYER_BOOTSTRAP";
const RPC_URL = process.env.CHAINBOUND_SOLANA_RPC_URL || "https://api.devnet.solana.com";
const DEVNET_GENESIS = "EtWTRABZaYq6iMfeYKouRu166VU2xqa1wcaWoxPkrZBG";
const PROGRAM_ID = new PublicKey("9312ay7VrpvNYmyEx8TYCqhafbZ2Y5WGVbt6Qp772uUQ");
const SMOKE_WALLET = new PublicKey("H68eGMwT3UCigd9qf4u1VLyqQh6jq57HD4wLWaQrbBWq");
const CREATE_PROFILE = Buffer.from([225, 205, 234, 143, 17, 186, 50, 220]);
const CREATE_MOVE_VAULT = Buffer.from([13, 174, 181, 106, 193, 119, 14, 60]);
const PLAYER_PROFILE = Buffer.from([82, 226, 99, 87, 164, 130, 181, 80]);
const MOVE_VAULT = Buffer.from([30, 75, 22, 221, 63, 126, 196, 29]);
const PLAYER_PROFILE_BYTES = 91;
const MOVE_VAULT_BYTES = 2505;

function assert(condition, message) {
  if (!condition) throw new Error(message);
}

function timestamp() {
  return new Date().toISOString().replace(/[-:]/g, "").replace(/\.\d{3}Z$/, "Z");
}

function sha256Hex(value) {
  return crypto.createHash("sha256").update(value).digest("hex");
}

function writeEvidence(value) {
  const directory = resolve(__dirname, "../../../Logs");
  mkdirSync(directory, { recursive: true });
  const path = join(directory, `ownership-player-bootstrap-${timestamp()}.json`);
  writeFileSync(path, `${JSON.stringify(value, null, 2)}\n`, { encoding: "utf8", mode: 0o600 });
  return path;
}

function loadOwner(path) {
  const raw = JSON.parse(readFileSync(path, "utf8"));
  assert(
    Array.isArray(raw) && raw.length === 64 &&
      raw.every((value) => Number.isInteger(value) && value >= 0 && value <= 255),
    "invalid_owner_keypair_file",
  );
  const keypair = Keypair.fromSecretKey(Uint8Array.from(raw));
  assert(keypair.publicKey.equals(SMOKE_WALLET), "owner_keypair_does_not_match_smoke_wallet");
  return keypair;
}

function assertBootstrapInstruction(instruction, expectedData, expectedAccount, code) {
  assert(instruction.programId.equals(PROGRAM_ID), `${code}_program_mismatch`);
  assert(instruction.data.equals(expectedData), `${code}_data_mismatch`);
  const expectedKeys = [
    { pubkey: expectedAccount, isSigner: false, isWritable: true },
    { pubkey: SMOKE_WALLET, isSigner: true, isWritable: true },
    { pubkey: SystemProgram.programId, isSigner: false, isWritable: false },
  ];
  assert(instruction.keys.length === expectedKeys.length, `${code}_account_count_mismatch`);
  expectedKeys.forEach((expected, index) => {
    const actual = instruction.keys[index];
    assert(actual.pubkey.equals(expected.pubkey), `${code}_account_${index}_mismatch`);
    assert(actual.isSigner === expected.isSigner, `${code}_account_${index}_signer_mismatch`);
    assert(actual.isWritable === expected.isWritable, `${code}_account_${index}_writable_mismatch`);
  });
}

function decodeProfile(account) {
  assert(account && account.owner.equals(PROGRAM_ID), "player_profile_missing_or_wrong_owner");
  const data = account.data;
  assert(data.length === PLAYER_PROFILE_BYTES, "player_profile_layout_mismatch");
  assert(data.subarray(0, 8).equals(PLAYER_PROFILE), "player_profile_discriminator_mismatch");
  const decoded = {
    owner: new PublicKey(data.subarray(8, 40)).toBase58(),
    ramBalance: data.readUInt32LE(68),
    dailyRamEarned: data.readUInt16LE(72),
    lastRamResetTimestamp: data.readBigInt64LE(74).toString(),
    lifetimeRamEarned: data.readBigUInt64LE(82).toString(),
    bump: data[90],
  };
  assert(decoded.owner === SMOKE_WALLET.toBase58(), "player_profile_owner_mismatch");
  assert(decoded.ramBalance === 0, "fresh_player_profile_ram_not_zero");
  assert(decoded.dailyRamEarned === 0, "fresh_player_profile_daily_ram_not_zero");
  assert(decoded.lifetimeRamEarned === "0", "fresh_player_profile_lifetime_ram_not_zero");
  return decoded;
}

function decodeVault(account) {
  assert(account && account.owner.equals(PROGRAM_ID), "move_vault_missing_or_wrong_owner");
  const data = account.data;
  assert(data.length === MOVE_VAULT_BYTES, "move_vault_layout_mismatch");
  assert(data.subarray(0, 8).equals(MOVE_VAULT), "move_vault_discriminator_mismatch");
  const count = data[41];
  const vectorCount = data.readUInt32LE(42);
  const nextInstanceOffset = 46 + vectorCount * 49;
  const decoded = {
    owner: new PublicKey(data.subarray(8, 40)).toBase58(),
    knownCapacity: data[40],
    knownCount: count,
    vectorCount,
    nextInstanceId: data.readBigUInt64LE(nextInstanceOffset).toString(),
    bump: data[nextInstanceOffset + 8],
  };
  assert(decoded.owner === SMOKE_WALLET.toBase58(), "move_vault_owner_mismatch");
  assert(decoded.knownCapacity === 50, "move_vault_capacity_mismatch");
  assert(decoded.knownCount === 0 && decoded.vectorCount === 0, "fresh_move_vault_not_empty");
  assert(decoded.nextInstanceId === "1", "fresh_move_vault_next_instance_mismatch");
  return decoded;
}

async function main() {
  assert(MODE === "prepare" || MODE === "execute", "CHAINBOUND_BOOTSTRAP_MODE_must_be_prepare_or_execute");
  const connection = new Connection(RPC_URL, "confirmed");
  const genesisHash = await connection.getGenesisHash();
  assert(genesisHash === DEVNET_GENESIS, `rpc_is_not_devnet:${genesisHash}`);

  const [profile] = PublicKey.findProgramAddressSync(
    [Buffer.from("player_profile"), SMOKE_WALLET.toBuffer()],
    PROGRAM_ID,
  );
  const [vault] = PublicKey.findProgramAddressSync(
    [Buffer.from("move_vault"), SMOKE_WALLET.toBuffer()],
    PROGRAM_ID,
  );
  const before = await connection.getMultipleAccountsInfo([profile, vault], "confirmed");
  const instructions = [];
  if (before[0]) decodeProfile(before[0]);
  else {
    instructions.push(new TransactionInstruction({
      programId: PROGRAM_ID,
      keys: [
        { pubkey: profile, isSigner: false, isWritable: true },
        { pubkey: SMOKE_WALLET, isSigner: true, isWritable: true },
        { pubkey: SystemProgram.programId, isSigner: false, isWritable: false },
      ],
      data: CREATE_PROFILE,
    }));
  }
  if (before[1]) decodeVault(before[1]);
  else {
    instructions.push(new TransactionInstruction({
      programId: PROGRAM_ID,
      keys: [
        { pubkey: vault, isSigner: false, isWritable: true },
        { pubkey: SMOKE_WALLET, isSigner: true, isWritable: true },
        { pubkey: SystemProgram.programId, isSigner: false, isWritable: false },
      ],
      data: CREATE_MOVE_VAULT,
    }));
  }
  let instructionIndex = 0;
  if (!before[0]) {
    assertBootstrapInstruction(instructions[instructionIndex], CREATE_PROFILE, profile, "create_profile");
    instructionIndex += 1;
  }
  if (!before[1]) {
    assertBootstrapInstruction(instructions[instructionIndex], CREATE_MOVE_VAULT, vault, "create_move_vault");
    instructionIndex += 1;
  }
  assert(instructionIndex === instructions.length, "unexpected_bootstrap_instruction");

  const balanceBefore = await connection.getBalance(SMOKE_WALLET, "confirmed");
  const summary = {
    operation: "devnet-smoke-player-bootstrap",
    mode: MODE,
    cluster: "devnet",
    rpcOrigin: new URL(RPC_URL).origin,
    genesisHash,
    programId: PROGRAM_ID.toBase58(),
    wallet: SMOKE_WALLET.toBase58(),
    playerProfile: profile.toBase58(),
    moveVault: vault.toBase58(),
    accountsBefore: { playerProfileExists: !!before[0], moveVaultExists: !!before[1] },
    walletBalanceLamportsBefore: balanceBefore,
    instructions: instructions.map((ix) =>
      ix.data.equals(CREATE_PROFILE) ? "create_profile" : "create_move_vault"
    ),
    requiredSignerPublicKeys: instructions.length ? [SMOKE_WALLET.toBase58()] : [],
    signingPerformed: false,
    sendingPerformed: false,
  };

  if (instructions.length === 0) {
    const verified = { playerProfile: decodeProfile(before[0]), moveVault: decodeVault(before[1]) };
    const evidencePath = writeEvidence({ ...summary, verified, alreadyInitialized: true });
    process.stdout.write(`${JSON.stringify({ ...summary, verified, alreadyInitialized: true, evidencePath }, null, 2)}\n`);
    return;
  }

  const latest = await connection.getLatestBlockhash("confirmed");
  const transaction = new Transaction({ feePayer: SMOKE_WALLET, recentBlockhash: latest.blockhash }).add(...instructions);
  const fee = await connection.getFeeForMessage(transaction.compileMessage(), "confirmed");
  const rent = (before[0] ? 0 : await connection.getMinimumBalanceForRentExemption(PLAYER_PROFILE_BYTES, "confirmed")) +
    (before[1] ? 0 : await connection.getMinimumBalanceForRentExemption(MOVE_VAULT_BYTES, "confirmed"));
  const projectedOutflow = rent + (fee.value || 0);
  let simulation = null;
  if (balanceBefore >= projectedOutflow) {
    simulation = await connection.simulateTransaction(transaction);
    assert(simulation.value.err === null, `unsigned_simulation_failed:${JSON.stringify(simulation.value.err)}`);
  }
  Object.assign(summary, {
    estimatedNetworkFeeLamports: fee.value || 0,
    estimatedRentLamports: rent,
    projectedOutflowLamports: projectedOutflow,
    fundingShortfallLamports: Math.max(0, projectedOutflow - balanceBefore),
    messageSha256: sha256Hex(transaction.serializeMessage()),
    simulation: simulation
      ? { succeeded: true, unitsConsumed: simulation.value.unitsConsumed || null }
      : { succeeded: false, skipped: "wallet_requires_devnet_faucet_funding" },
  });

  if (MODE === "prepare") {
    const evidencePath = writeEvidence(summary);
    process.stdout.write(`${JSON.stringify({ ...summary, evidencePath }, null, 2)}\n`);
    return;
  }

  assert(process.env.CHAINBOUND_BOOTSTRAP_APPROVAL === APPROVAL,
    `missing_approval:set CHAINBOUND_BOOTSTRAP_APPROVAL=${APPROVAL}`);
  assert(
    !before[0] && !before[1] && instructions.length === 2 &&
      instructions[0].data.equals(CREATE_PROFILE) && instructions[1].data.equals(CREATE_MOVE_VAULT),
    "bootstrap_execute_requires_both_reviewed_pdas_absent",
  );
  assert(process.env.CHAINBOUND_SMOKE_WALLET_KEYPAIR, "missing_env:CHAINBOUND_SMOKE_WALLET_KEYPAIR");
  assert(balanceBefore >= projectedOutflow, `insufficient_devnet_lamports:${projectedOutflow - balanceBefore}`);
  const owner = loadOwner(process.env.CHAINBOUND_SMOKE_WALLET_KEYPAIR);
  transaction.sign(owner);
  assert(transaction.verifySignatures(true), "owner_signature_verification_failed");
  const signedSimulation = await connection.simulateTransaction(transaction);
  assert(signedSimulation.value.err === null, `signed_simulation_failed:${JSON.stringify(signedSimulation.value.err)}`);
  const signature = await connection.sendRawTransaction(transaction.serialize(), {
    skipPreflight: false,
    preflightCommitment: "confirmed",
    maxRetries: 15,
  });
  const confirmation = await connection.confirmTransaction({ signature, ...latest }, "confirmed");
  assert(confirmation.value.err === null, "bootstrap_transaction_failed");
  const after = await connection.getMultipleAccountsInfo([profile, vault], "confirmed");
  const verified = { playerProfile: decodeProfile(after[0]), moveVault: decodeVault(after[1]) };
  const record = await connection.getTransaction(signature, { commitment: "confirmed", maxSupportedTransactionVersion: 0 });
  const evidence = {
    ...summary,
    signingPerformed: true,
    sendingPerformed: true,
    signature,
    slot: record?.slot || null,
    feeLamports: record?.meta?.fee || null,
    verified,
  };
  const evidencePath = writeEvidence(evidence);
  process.stdout.write(`${JSON.stringify({ ...evidence, evidencePath }, null, 2)}\n`);
}

main().catch((error) => {
  process.stderr.write(`${error instanceof Error ? error.stack || error.message : error}\n`);
  process.exitCode = 1;
});
