#!/usr/bin/env node
"use strict";

// Read-only Gate D verifier. It checks the confirmed transaction instruction
// and decodes the live GameConfig account without loading a signer.

const bs58Module = require("bs58");
const bs58 = bs58Module.default || bs58Module;
const { Connection, PublicKey } = require("@solana/web3.js");

const RPC_URL = process.env.CHAINBOUND_SOLANA_RPC_URL || "https://api.devnet.solana.com";
const DEVNET_GENESIS = "EtWTRABZaYq6iMfeYKouRu166VU2xqa1wcaWoxPkrZBG";
const PROGRAM_ID = new PublicKey("9312ay7VrpvNYmyEx8TYCqhafbZ2Y5WGVbt6Qp772uUQ");
const PROGRAM_AUTHORITY = new PublicKey("DkFmzzzF89tYZu2JWR1uq3H13U5np9NU6MHF4E61gJmG");
const RESULT_AUTHORITY = new PublicKey("FaAegH2tBCyDzxXcXyz64qEFWsrvpX8M6SSFNx1Pt5KF");
const GAME_CONFIG = new PublicKey("FdA2EJ89Xtqn6Z1pxayBQnSJQ8pVpVAuEjaUW8qbU8nx");
const INITIALIZE_DISCRIMINATOR = Buffer.from([175, 175, 109, 31, 13, 152, 155, 237]);
const GAME_CONFIG_DISCRIMINATOR = Buffer.from([45, 146, 146, 33, 170, 69, 96, 133]);
const SCHEMA_VERSION = 1;
const RAM_DAILY_CAP = 100;
const RAM_PER_BURN = 10;
const INSTRUCTION_FLAGS = 0n;

function assert(condition, message) {
  if (!condition) throw new Error(message);
}

function expectedInstructionData() {
  const data = Buffer.alloc(56);
  INITIALIZE_DISCRIMINATOR.copy(data, 0);
  RESULT_AUTHORITY.toBuffer().copy(data, 8);
  data.writeUInt16LE(SCHEMA_VERSION, 40);
  data.writeUInt16LE(RAM_DAILY_CAP, 42);
  data.writeUInt32LE(RAM_PER_BURN, 44);
  data.writeBigUInt64LE(INSTRUCTION_FLAGS, 48);
  return data;
}

function decodeConfig(account, expectedBump) {
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
  assert(decoded.programAuthority === PROGRAM_AUTHORITY.toBase58(), "Program authority mismatch.");
  assert(decoded.resultAuthority === RESULT_AUTHORITY.toBase58(), "Result authority mismatch.");
  assert(decoded.schemaVersion === SCHEMA_VERSION, "Schema version mismatch.");
  assert(decoded.ramDailyCap === RAM_DAILY_CAP, "Daily cap mismatch.");
  assert(decoded.ramPerBurn === RAM_PER_BURN, "RAM per burn mismatch.");
  assert(decoded.instructionFlags === INSTRUCTION_FLAGS, "Instruction flags mismatch.");
  assert(decoded.bump === expectedBump, "GameConfig bump mismatch.");
  return { ...decoded, instructionFlags: decoded.instructionFlags.toString() };
}

async function main() {
  const signature = (process.env.GAME_CONFIG_SIGNATURE || process.argv[2] || "").trim();
  assert(signature, "Set GAME_CONFIG_SIGNATURE or pass the signature as argv[2].");
  const connection = new Connection(RPC_URL, "confirmed");
  const genesisHash = await connection.getGenesisHash();
  assert(genesisHash === DEVNET_GENESIS, `RPC is not Solana devnet (${genesisHash}).`);
  const [derivedConfig, bump] = PublicKey.findProgramAddressSync([Buffer.from("game_config")], PROGRAM_ID);
  assert(derivedConfig.equals(GAME_CONFIG), `Derived GameConfig changed to ${derivedConfig}.`);

  const [account, statuses, transaction, newest, payerBalance] = await Promise.all([
    connection.getAccountInfo(GAME_CONFIG, "confirmed"),
    connection.getSignatureStatuses([signature], { searchTransactionHistory: true }),
    connection.getTransaction(signature, { commitment: "confirmed", maxSupportedTransactionVersion: 0 }),
    connection.getSignaturesForAddress(GAME_CONFIG, { limit: 1 }, "confirmed"),
    connection.getBalance(PROGRAM_AUTHORITY, "confirmed"),
  ]);
  const status = statuses.value[0];
  assert(status, "GameConfig signature was not found.");
  assert(status.err === null, "GameConfig signature has an on-chain error.");
  assert(transaction?.meta?.err === null, "GameConfig transaction failed.");
  assert(newest[0]?.signature === signature, "Signature is not the newest GameConfig transaction.");

  const message = transaction.transaction.message;
  const keys = message.accountKeys.map((key) => key.toBase58());
  assert(keys[0] === PROGRAM_AUTHORITY.toBase58(), "Pinned authority is not the fee payer.");
  assert(keys.includes(GAME_CONFIG.toBase58()), "Transaction omits GameConfig.");
  const programIndex = keys.indexOf(PROGRAM_ID.toBase58());
  const initializeIx = message.instructions.find((ix) => ix.programIdIndex === programIndex);
  assert(initializeIx, "Initialize instruction was not found.");
  assert(
    Buffer.from(bs58.decode(initializeIx.data)).equals(expectedInstructionData()),
    "Initialize instruction data differs from the approved Gate D payload.",
  );

  const decoded = decodeConfig(account, bump);
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
      slot: transaction.slot,
      confirmationStatus: status.confirmationStatus,
      error: status.err,
      feeLamports: transaction.meta.fee,
      accountLamports: account.lamports,
      payerBalanceLamports: payerBalance,
      account: decoded,
    }, null, 2)}\n`,
  );
}

main().catch((error) => {
  process.stderr.write(`${error instanceof Error ? error.stack || error.message : error}\n`);
  process.exitCode = 1;
});
