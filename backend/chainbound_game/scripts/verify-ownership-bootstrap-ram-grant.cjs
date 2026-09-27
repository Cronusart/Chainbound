#!/usr/bin/env node
"use strict";

// Credential-free, read-only verifier for the one approved Devnet smoke RAM
// grant state. This script never calls Supabase, loads a keypair, signs, or
// sends. It accepts either the pristine pre-grant state or the exact completed
// grant receipt and writes only redacted public-state evidence.

const crypto = require("crypto");
const { mkdirSync, writeFileSync } = require("fs");
const { join, resolve } = require("path");
const { Connection, PublicKey } = require("@solana/web3.js");
const {
  assertCompletedGrant,
  assertFresh,
  decodeConfig,
  decodeProfile,
  decodeReceipt,
  decodeVault,
  findExistingGrantTransaction,
} = require("./ownership-bootstrap-ram-grant.cjs");

const RPC_URL = "https://api.devnet.solana.com";
const DEVNET_GENESIS = "EtWTRABZaYq6iMfeYKouRu166VU2xqa1wcaWoxPkrZBG";
const PROGRAM_ID = new PublicKey("9312ay7VrpvNYmyEx8TYCqhafbZ2Y5WGVbt6Qp772uUQ");
const GAME_CONFIG = new PublicKey("FdA2EJ89Xtqn6Z1pxayBQnSJQ8pVpVAuEjaUW8qbU8nx");
const SMOKE_WALLET = new PublicKey("H68eGMwT3UCigd9qf4u1VLyqQh6jq57HD4wLWaQrbBWq");
const IDEMPOTENCY_KEY = "devnet-smoke-grant-20260920-1";

function assert(condition, message) {
  if (!condition) throw new Error(message);
}

function timestamp() {
  return new Date().toISOString().replace(/[-:]/g, "").replace(/\.\d{3}Z$/, "Z");
}

function writeEvidence(value) {
  const directory = resolve(__dirname, "../../../Logs");
  mkdirSync(directory, { recursive: true });
  const path = join(directory, `ownership-bootstrap-ram-grant-state-${timestamp()}.json`);
  writeFileSync(path, `${JSON.stringify(value, null, 2)}\n`, { encoding: "utf8", mode: 0o600 });
  return path;
}

async function main() {
  const connection = new Connection(RPC_URL, "confirmed");
  const genesisHash = await connection.getGenesisHash();
  assert(genesisHash === DEVNET_GENESIS, `rpc_not_devnet:${genesisHash}`);

  const idempotencyHash = crypto.createHash("sha256").update(IDEMPOTENCY_KEY).digest();
  const [playerProfile] = PublicKey.findProgramAddressSync(
    [Buffer.from("player_profile"), SMOKE_WALLET.toBuffer()],
    PROGRAM_ID,
  );
  const [moveVault] = PublicKey.findProgramAddressSync(
    [Buffer.from("move_vault"), SMOKE_WALLET.toBuffer()],
    PROGRAM_ID,
  );
  const [receiptAddress] = PublicKey.findProgramAddressSync(
    [Buffer.from("ram_grant_receipt"), SMOKE_WALLET.toBuffer(), idempotencyHash],
    PROGRAM_ID,
  );

  const accounts = await connection.getMultipleAccountsInfo(
    [playerProfile, moveVault, GAME_CONFIG, receiptAddress],
    "confirmed",
  );
  const profile = decodeProfile(accounts[0]);
  const vault = decodeVault(accounts[1]);
  const config = decodeConfig(accounts[2]);
  const receipt = accounts[3] ? decodeReceipt(accounts[3]) : null;

  if (receipt) {
    assertCompletedGrant(profile, vault, config, receipt, idempotencyHash);
  } else {
    assertFresh(profile, vault, config);
  }

  const grantTransaction = receipt
    ? await findExistingGrantTransaction(
      connection,
      receiptAddress,
      playerProfile,
      idempotencyHash,
    )
    : null;

  const evidence = {
    verified: true,
    operation: "one-devnet-smoke-ram-grant-state-read",
    cluster: "devnet",
    rpcOrigin: new URL(RPC_URL).origin,
    genesisHash,
    programId: PROGRAM_ID.toBase58(),
    wallet: SMOKE_WALLET.toBase58(),
    playerProfile: playerProfile.toBase58(),
    moveVault: moveVault.toBase58(),
    receiptAddress: receiptAddress.toBase58(),
    idempotencyKey: IDEMPOTENCY_KEY,
    idempotencyHash: idempotencyHash.toString("hex"),
    stage: receipt ? "grant-executed" : "awaiting-grant",
    transaction: grantTransaction
      ? {
        signature: grantTransaction.signature,
        slot: grantTransaction.record.slot,
        confirmationStatus: "confirmed",
        error: grantTransaction.record.meta?.err ?? null,
        feeLamports: grantTransaction.record.meta?.fee ?? null,
      }
      : null,
    profile,
    vault,
    config,
    receipt,
    credentialsRead: false,
    signingPerformed: false,
    sendingPerformed: false,
    chainMutationPerformed: false,
  };
  const evidencePath = writeEvidence(evidence);
  process.stdout.write(`${JSON.stringify({ ...evidence, evidencePath }, null, 2)}\n`);
}

main().catch((error) => {
  const message = error instanceof Error ? error.message : "unknown_error";
  process.stderr.write(`${message.replace(/[\r\n]+/g, " ")}\n`);
  process.exitCode = 1;
});
