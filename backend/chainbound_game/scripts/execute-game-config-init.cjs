#!/usr/bin/env node
"use strict";

// Operator-only Gate D executor. Codex does not invoke this file because it
// loads the local operator keypair, signs, and submits the approved transaction.

const { readFileSync } = require("fs");
const {
  Connection,
  Keypair,
  PublicKey,
  SystemProgram,
  Transaction,
  TransactionInstruction,
} = require("@solana/web3.js");

const RPC_URL = process.env.CHAINBOUND_SOLANA_RPC_URL || "https://api.devnet.solana.com";
const DEVNET_GENESIS = "EtWTRABZaYq6iMfeYKouRu166VU2xqa1wcaWoxPkrZBG";
const PROGRAM_ID = new PublicKey("9312ay7VrpvNYmyEx8TYCqhafbZ2Y5WGVbt6Qp772uUQ");
const PROGRAM_AUTHORITY = new PublicKey("DkFmzzzF89tYZu2JWR1uq3H13U5np9NU6MHF4E61gJmG");
const RESULT_AUTHORITY = new PublicKey("FaAegH2tBCyDzxXcXyz64qEFWsrvpX8M6SSFNx1Pt5KF");
const GAME_CONFIG = new PublicKey("FdA2EJ89Xtqn6Z1pxayBQnSJQ8pVpVAuEjaUW8qbU8nx");
const INITIALIZE_DISCRIMINATOR = Buffer.from([175, 175, 109, 31, 13, 152, 155, 237]);
const GAME_CONFIG_DISCRIMINATOR = Buffer.from([45, 146, 146, 33, 170, 69, 96, 133]);
const APPROVAL_TOKEN = "APPROVED_GATE_D_INITIALIZE_GAMECONFIG";
const SCHEMA_VERSION = 1;
const RAM_DAILY_CAP = 100;
const RAM_PER_BURN = 10;
const INSTRUCTION_FLAGS = 0n;

function assert(condition, message) {
  if (!condition) throw new Error(message);
}

function loadOperator(path) {
  const values = JSON.parse(readFileSync(path, "utf8"));
  assert(
    Array.isArray(values) && values.length === 64 && values.every(Number.isInteger),
    "Operator keypair file is invalid.",
  );
  const keypair = Keypair.fromSecretKey(Uint8Array.from(values));
  assert(keypair.publicKey.equals(PROGRAM_AUTHORITY), "Operator keypair does not match pinned authority.");
  return keypair;
}

function encodeInitialize() {
  const data = Buffer.alloc(56);
  INITIALIZE_DISCRIMINATOR.copy(data, 0);
  RESULT_AUTHORITY.toBuffer().copy(data, 8);
  data.writeUInt16LE(SCHEMA_VERSION, 40);
  data.writeUInt16LE(RAM_DAILY_CAP, 42);
  data.writeUInt32LE(RAM_PER_BURN, 44);
  data.writeBigUInt64LE(INSTRUCTION_FLAGS, 48);
  return data;
}

function decodeAndVerify(account, expectedBump) {
  assert(account, "GameConfig account is missing.");
  assert(account.owner.equals(PROGRAM_ID), "GameConfig owner mismatch.");
  const data = account.data;
  assert(data.length === 89, `GameConfig data length is ${data.length}, expected 89.`);
  assert(data.subarray(0, 8).equals(GAME_CONFIG_DISCRIMINATOR), "GameConfig discriminator mismatch.");
  const decoded = {
    programAuthority: new PublicKey(data.subarray(8, 40)).toBase58(),
    resultAuthority: new PublicKey(data.subarray(40, 72)).toBase58(),
    schemaVersion: data.readUInt16LE(72),
    ramDailyCap: data.readUInt16LE(74),
    ramPerBurn: data.readUInt32LE(76),
    instructionFlags: data.readBigUInt64LE(80),
    bump: data[88],
  };
  assert(decoded.programAuthority === PROGRAM_AUTHORITY.toBase58(), "GameConfig program authority mismatch.");
  assert(decoded.resultAuthority === RESULT_AUTHORITY.toBase58(), "GameConfig result authority mismatch.");
  assert(decoded.schemaVersion === SCHEMA_VERSION, "GameConfig schema version mismatch.");
  assert(decoded.ramDailyCap === RAM_DAILY_CAP, "GameConfig daily cap mismatch.");
  assert(decoded.ramPerBurn === RAM_PER_BURN, "GameConfig RAM per burn mismatch.");
  assert(decoded.instructionFlags === INSTRUCTION_FLAGS, "GameConfig instruction flags mismatch.");
  assert(decoded.bump === expectedBump, "GameConfig bump mismatch.");
  return { ...decoded, instructionFlags: decoded.instructionFlags.toString() };
}

async function main() {
  assert(
    process.env.CHAINBOUND_GATE_D_APPROVAL === APPROVAL_TOKEN,
    `Refusing to sign: set CHAINBOUND_GATE_D_APPROVAL=${APPROVAL_TOKEN} only after explicit Gate D approval.`,
  );
  assert(process.env.CHAINBOUND_OPERATOR_KEYPAIR, "CHAINBOUND_OPERATOR_KEYPAIR is required.");
  const operator = loadOperator(process.env.CHAINBOUND_OPERATOR_KEYPAIR);
  const connection = new Connection(RPC_URL, "confirmed");
  const genesisHash = await connection.getGenesisHash();
  assert(genesisHash === DEVNET_GENESIS, `RPC is not Solana devnet (${genesisHash}).`);
  const [derivedConfig, bump] = PublicKey.findProgramAddressSync([Buffer.from("game_config")], PROGRAM_ID);
  assert(derivedConfig.equals(GAME_CONFIG), `Derived GameConfig changed to ${derivedConfig}.`);

  const existing = await connection.getAccountInfo(GAME_CONFIG, "confirmed");
  if (existing) {
    const decoded = decodeAndVerify(existing, bump);
    process.stdout.write(
      `${JSON.stringify({
        verified: true,
        gate: "D",
        cluster: "devnet",
        gameConfig: GAME_CONFIG.toBase58(),
        signature: null,
        resumedExistingVerifiedConfig: true,
        account: decoded,
      }, null, 2)}\n`,
    );
    return;
  }

  const instruction = new TransactionInstruction({
    programId: PROGRAM_ID,
    keys: [
      { pubkey: GAME_CONFIG, isSigner: false, isWritable: true },
      { pubkey: PROGRAM_AUTHORITY, isSigner: true, isWritable: true },
      { pubkey: SystemProgram.programId, isSigner: false, isWritable: false },
    ],
    data: encodeInitialize(),
  });
  const maxAttempts = Number(process.env.CHAINBOUND_GATE_D_BLOCKHASH_ATTEMPTS || 8);
  let signature = null;
  let confirmation = null;
  let simulationUnitsConsumed = null;

  // web3.js >=1.95 rejects simulateTransaction(legacyTx, {sigVerify}) with
  // "Invalid arguments". Prefer send+confirm with skipPreflight; optionally
  // simulate with no config object when CHAINBOUND_GATE_D_SKIP_PREFLIGHT!=1.
  const skipPreflight =
    process.env.CHAINBOUND_GATE_D_SKIP_PREFLIGHT !== "0"; // default true on flaky devnet

  for (let attempt = 1; attempt <= maxAttempts; attempt += 1) {
    const latest = await connection.getLatestBlockhash("finalized");
    const transaction = new Transaction({
      feePayer: PROGRAM_AUTHORITY,
      recentBlockhash: latest.blockhash,
    }).add(instruction);
    transaction.sign(operator);

    if (!skipPreflight) {
      try {
        const simulation = await connection.simulateTransaction(transaction);
        if (simulation.value.err) {
          const text = JSON.stringify(simulation.value.err);
          if (text.includes("BlockhashNotFound") && attempt < maxAttempts) {
            await new Promise((r) => setTimeout(r, 500 * attempt));
            continue;
          }
          throw new Error(
            `Signed GameConfig simulation failed: ${JSON.stringify({
              err: simulation.value.err,
              logs: simulation.value.logs,
            })}`,
          );
        }
        simulationUnitsConsumed = simulation.value.unitsConsumed ?? null;
      } catch (simErr) {
        const msg = simErr instanceof Error ? simErr.message : String(simErr);
        if (msg.includes("Invalid arguments") || msg.includes("BlockhashNotFound")) {
          process.stderr.write(
            `Gate D: simulate unavailable (${msg}); falling back to skipPreflight send (attempt ${attempt}/${maxAttempts})\n`,
          );
        } else {
          throw simErr;
        }
      }
    } else {
      process.stderr.write(
        `Gate D: skipPreflight send (attempt ${attempt}/${maxAttempts})\n`,
      );
    }

    try {
      signature = await connection.sendRawTransaction(transaction.serialize(), {
        skipPreflight: true,
        preflightCommitment: "confirmed",
        maxRetries: 15,
      });
      confirmation = await connection.confirmTransaction(
        {
          signature,
          blockhash: latest.blockhash,
          lastValidBlockHeight: latest.lastValidBlockHeight,
        },
        "confirmed",
      );
      assert(confirmation.value.err === null, "GameConfig transaction failed.");
      break;
    } catch (error) {
      const msg = String(error);
      if (
        (msg.includes("BlockhashNotFound") || msg.includes("block height exceeded")) &&
        attempt < maxAttempts
      ) {
        await new Promise((r) => setTimeout(r, 500 * attempt));
        continue;
      }
      throw error;
    }
  }
  assert(signature && confirmation, `GameConfig send failed after ${maxAttempts} attempts.`);

  const created = await connection.getAccountInfo(GAME_CONFIG, "confirmed");
  const decoded = decodeAndVerify(created, bump);
  const transactionRecord = await connection.getTransaction(signature, {
    commitment: "confirmed",
    maxSupportedTransactionVersion: 0,
  });
  assert(transactionRecord?.meta?.err === null, "Confirmed GameConfig transaction record failed.");
  process.stdout.write(
    `${JSON.stringify({
      verified: true,
      gate: "D",
      cluster: "devnet",
      rpcUrl: new URL(RPC_URL).origin,
      genesisHash,
      programId: PROGRAM_ID.toBase58(),
      gameConfig: GAME_CONFIG.toBase58(),
      signature,
      slot: transactionRecord.slot,
      feeLamports: transactionRecord.meta.fee,
      simulationUnitsConsumed,
      resumedExistingVerifiedConfig: false,
      account: decoded,
    }, null, 2)}\n`,
  );
}

main().catch((error) => {
  process.stderr.write(`${error instanceof Error ? error.stack || error.message : error}\n`);
  process.exitCode = 1;
});
