#!/usr/bin/env node
"use strict";

// Read-only verifier for the approved GameConfig result-authority correction.

const bs58Module = require("bs58");
const bs58 = bs58Module.default || bs58Module;
const { createHash } = require("crypto");
const { Connection, PublicKey } = require("@solana/web3.js");

const RPC_URL =
  process.env.CHAINBOUND_SOLANA_RPC_URL || "https://api.devnet.solana.com";
const DEVNET_GENESIS = "EtWTRABZaYq6iMfeYKouRu166VU2xqa1wcaWoxPkrZBG";
const PROGRAM_ID = new PublicKey(
  "9312ay7VrpvNYmyEx8TYCqhafbZ2Y5WGVbt6Qp772uUQ"
);
const PROGRAM_AUTHORITY = new PublicKey(
  "DkFmzzzF89tYZu2JWR1uq3H13U5np9NU6MHF4E61gJmG"
);
const TARGET_RESULT_AUTHORITY = new PublicKey(
  "FaAegH2tBCyDzxXcXyz64qEFWSrvpX8M6SSFNx1Pt5KF"
);
const GAME_CONFIG = new PublicKey(
  "FdA2EJ89Xtqn6Z1pxayBQnSJQ8pVpVAuEjaUW8qbU8nx"
);
const UPDATE_DISCRIMINATOR = Buffer.from([180, 82, 7, 205, 89, 182, 61, 128]);
const GAME_CONFIG_DISCRIMINATOR = Buffer.from([
  45, 146, 146, 33, 170, 69, 96, 133,
]);
const EXPECTED_FEE_LAMPORTS = 5000;

function assert(condition, message) {
  if (!condition) throw new Error(message);
}

function sha256(bytes) {
  return createHash("sha256").update(bytes).digest("hex");
}

function expectedInstructionData() {
  const data = Buffer.alloc(56);
  UPDATE_DISCRIMINATOR.copy(data, 0);
  TARGET_RESULT_AUTHORITY.toBuffer().copy(data, 8);
  data.writeUInt16LE(1, 40);
  data.writeUInt16LE(100, 42);
  data.writeUInt32LE(10, 44);
  data.writeBigUInt64LE(0n, 48);
  return data;
}

function decodeAndVerify(account, expectedBump) {
  assert(
    account?.owner.equals(PROGRAM_ID),
    "GameConfig missing or wrong owner."
  );
  const data = account.data;
  assert(
    data.length === 89,
    `GameConfig data length is ${data.length}, expected 89.`
  );
  assert(
    data.subarray(0, 8).equals(GAME_CONFIG_DISCRIMINATOR),
    "GameConfig discriminator mismatch."
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
  assert(
    decoded.programAuthority === PROGRAM_AUTHORITY.toBase58(),
    "Program authority changed."
  );
  assert(
    decoded.resultAuthority === TARGET_RESULT_AUTHORITY.toBase58(),
    "Result authority mismatch."
  );
  assert(decoded.schemaVersion === 1, "Schema changed.");
  assert(decoded.ramDailyCap === 100, "Daily cap changed.");
  assert(decoded.ramPerBurn === 10, "RAM per burn changed.");
  assert(decoded.instructionFlags === 0n, "Instruction flags changed.");
  assert(decoded.bump === expectedBump, "Bump changed.");
  return { ...decoded, instructionFlags: decoded.instructionFlags.toString() };
}

async function main() {
  const signature = (
    process.env.GAME_CONFIG_ROTATION_SIGNATURE ||
    process.argv[2] ||
    ""
  ).trim();
  assert(
    signature,
    "Set GAME_CONFIG_ROTATION_SIGNATURE or pass the signature as argv[2]."
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

  const [account, statuses, transaction, history, payerBalance] =
    await Promise.all([
      connection.getAccountInfo(GAME_CONFIG, "finalized"),
      connection.getSignatureStatuses([signature], {
        searchTransactionHistory: true,
      }),
      connection.getTransaction(signature, {
        commitment: "finalized",
        maxSupportedTransactionVersion: 0,
      }),
      connection.getSignaturesForAddress(
        GAME_CONFIG,
        { limit: 100 },
        "finalized"
      ),
      connection.getBalance(PROGRAM_AUTHORITY, "finalized"),
    ]);
  const status = statuses.value[0];
  assert(status?.err === null, "Rotation signature missing or failed.");
  assert(
    status.confirmationStatus === "finalized",
    "Rotation is not finalized."
  );
  assert(transaction?.meta?.err === null, "Rotation transaction failed.");
  assert(
    transaction.meta.fee === EXPECTED_FEE_LAMPORTS,
    "Rotation fee mismatch."
  );
  const historyIndex = history.findIndex((item) => item.signature === signature);
  assert(historyIndex >= 0, "Rotation signature missing from recent GameConfig history.");

  const message = transaction.transaction.message;
  const keys = message.accountKeys.map((key) => key.toBase58());
  assert(
    keys.length === 3,
    `Rotation has ${keys.length} message accounts, expected 3.`
  );
  assert(
    keys[0] === PROGRAM_AUTHORITY.toBase58(),
    "DkFm is not the fee payer/signer."
  );
  assert(
    keys[1] === GAME_CONFIG.toBase58(),
    "GameConfig account index changed."
  );
  assert(keys[2] === PROGRAM_ID.toBase58(), "Program account index changed.");
  assert(
    message.header.numRequiredSignatures === 1,
    "Rotation required signer count changed."
  );
  const instructions = message.instructions.filter(
    (item) => item.programIdIndex === 2
  );
  assert(
    instructions.length === 1,
    `Found ${instructions.length} game-program instructions, expected 1.`
  );
  const instruction = instructions[0];
  assert(
    JSON.stringify(instruction.accounts) === JSON.stringify([1, 0]),
    "Instruction account indices changed."
  );
  const expectedData = expectedInstructionData();
  assert(
    Buffer.from(bs58.decode(instruction.data)).equals(expectedData),
    "Rotation instruction bytes mismatch."
  );
  assert(
    transaction.meta.preBalances[1] === transaction.meta.postBalances[1],
    "GameConfig lamports changed."
  );
  const decoded = decodeAndVerify(account, bump);

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
        slot: transaction.slot,
        confirmationStatus: status.confirmationStatus,
        error: status.err,
        feeLamports: transaction.meta.fee,
        newerGameConfigTransactionCount: historyIndex,
        instructionDataSha256: sha256(expectedData),
        gameConfigLamports: account.lamports,
        payerBalanceLamports: payerBalance,
        account: decoded,
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
