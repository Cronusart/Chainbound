#!/usr/bin/env node
"use strict";

// Read-only verifier for the approved 26,936-byte bootstrap ProgramData
// extension. It never reads a signer, signs, or submits a transaction.

const { createHash } = require("crypto");
const { readFileSync } = require("fs");
const { resolve } = require("path");
const bs58 = require("bs58");
const { Connection, PublicKey } = require("@solana/web3.js");

const PROGRAM_ID = new PublicKey(
  "9312ay7VrpvNYmyEx8TYCqhafbZ2Y5WGVbt6Qp772uUQ"
);
const PROGRAM_DATA = new PublicKey(
  "5wqBqX7qubopQnteX5RPTwBJrSFA6xnMs9Zbwz2BHfzD"
);
const AUTHORITY = new PublicKey("DkFmzzzF89tYZu2JWR1uq3H13U5np9NU6MHF4E61gJmG");
const UPGRADEABLE_LOADER = new PublicKey(
  "BPFLoaderUpgradeab1e11111111111111111111111"
);
const DEVNET_GENESIS = "EtWTRABZaYq6iMfeYKouRu166VU2xqa1wcaWoxPkrZBG";
const EXPECTED_ARTIFACT_BYTES = 741248;
const EXPECTED_ARTIFACT_SHA256 =
  "588249aecc1c57956a1acf525af31ffa1e756145f5e26b65724f45d499751e87";
const PROGRAM_DATA_METADATA_BYTES = 45;
const EXPECTED_ACCOUNT_BYTES =
  EXPECTED_ARTIFACT_BYTES + PROGRAM_DATA_METADATA_BYTES;
const EXPECTED_EXTENSION_BYTES = 26936;
const EXPECTED_INSTRUCTION_HEX = "0600000038690000";

function assert(condition, message) {
  if (!condition) throw new Error(message);
}

function sha256(bytes) {
  return createHash("sha256").update(bytes).digest("hex");
}

function decodeInstructionData(value) {
  if (typeof value === "string") {
    const decoder =
      typeof bs58.decode === "function" ? bs58.decode : bs58.default.decode;
    return Buffer.from(decoder(value));
  }
  return Buffer.from(value || []);
}

async function main() {
  const signature = String(
    process.env.EXTEND_SIGNATURE || process.argv[2] || ""
  ).trim();
  assert(
    signature,
    "Set EXTEND_SIGNATURE or pass the public extension signature as argv[2]."
  );
  const rpcUrl = process.env.SOLANA_RPC_URL || "https://api.devnet.solana.com";
  const artifactPath = resolve(
    process.argv[3] || resolve(__dirname, "../target/deploy/chainbound_game.so")
  );
  const artifact = readFileSync(artifactPath);
  assert(
    artifact.length === EXPECTED_ARTIFACT_BYTES,
    `artifact_length_changed:${artifact.length}`
  );
  assert(
    sha256(artifact) === EXPECTED_ARTIFACT_SHA256,
    `artifact_sha256_changed:${sha256(artifact)}`
  );

  const connection = new Connection(rpcUrl, "confirmed");
  const [
    genesisHash,
    program,
    programData,
    payerBalance,
    statuses,
    transaction,
    newest,
    targetRent,
  ] = await Promise.all([
    connection.getGenesisHash(),
    connection.getAccountInfo(PROGRAM_ID, "confirmed"),
    connection.getAccountInfo(PROGRAM_DATA, "confirmed"),
    connection.getBalance(AUTHORITY, "confirmed"),
    connection.getSignatureStatuses([signature], {
      searchTransactionHistory: true,
    }),
    connection.getTransaction(signature, {
      commitment: "confirmed",
      maxSupportedTransactionVersion: 0,
    }),
    connection.getSignaturesForAddress(PROGRAM_DATA, { limit: 1 }, "confirmed"),
    connection.getMinimumBalanceForRentExemption(
      EXPECTED_ACCOUNT_BYTES,
      "confirmed"
    ),
  ]);

  assert(genesisHash === DEVNET_GENESIS, `rpc_is_not_devnet:${genesisHash}`);
  assert(
    program && program.owner.equals(UPGRADEABLE_LOADER),
    "program_missing_or_wrong_owner"
  );
  assert(
    program.data.length === 36 && program.data.readUInt32LE(0) === 2,
    "program_layout_changed"
  );
  assert(
    new PublicKey(program.data.subarray(4, 36)).equals(PROGRAM_DATA),
    "programdata_pointer_changed"
  );
  assert(
    programData && programData.owner.equals(UPGRADEABLE_LOADER),
    "programdata_missing_or_wrong_owner"
  );
  assert(
    programData.data.length === EXPECTED_ACCOUNT_BYTES,
    `programdata_length_is_${programData.data.length}_expected_${EXPECTED_ACCOUNT_BYTES}`
  );
  assert(
    programData.data.readUInt32LE(0) === 3,
    "programdata_state_variant_changed"
  );
  assert(programData.data[12] === 1, "program_became_immutable");
  const authority = new PublicKey(programData.data.subarray(13, 45));
  const programDataSlot = Number(programData.data.readBigUInt64LE(4));
  assert(
    authority.equals(AUTHORITY),
    `upgrade_authority_changed:${authority.toBase58()}`
  );
  assert(
    programData.lamports >= targetRent,
    "programdata_not_rent_exempt_at_target_size"
  );

  const deployedPrefix = programData.data.subarray(
    PROGRAM_DATA_METADATA_BYTES,
    EXPECTED_ACCOUNT_BYTES
  );
  assert(
    deployedPrefix.length === EXPECTED_ARTIFACT_BYTES,
    "programdata_payload_capacity_mismatch"
  );
  // ExtendProgram preserves the existing executable prefix and zero-fills only
  // the new tail. Exact artifact comparison belongs to the later Upgrade gate.
  const oldPayloadBytes = EXPECTED_ARTIFACT_BYTES - EXPECTED_EXTENSION_BYTES;
  const newTail = deployedPrefix.subarray(oldPayloadBytes);
  assert(
    newTail.every((value) => value === 0),
    "new_programdata_tail_is_not_zero_filled"
  );

  const status = statuses.value[0];
  assert(
    status && status.err === null,
    "extension_signature_missing_or_failed"
  );
  assert(
    transaction && transaction.meta && transaction.meta.err === null,
    "extension_transaction_failed"
  );
  assert(
    programDataSlot === transaction.slot,
    `programdata_slot_${programDataSlot}_does_not_match_extension_${transaction.slot}`
  );
  assert(
    newest[0]?.signature === signature,
    "extension_is_not_newest_programdata_transaction"
  );
  const message = transaction.transaction.message;
  const keys = message.staticAccountKeys || message.accountKeys;
  const instructions = message.compiledInstructions || message.instructions;
  assert(
    Array.isArray(keys) && Array.isArray(instructions),
    "unsupported_transaction_message_shape"
  );
  const keyStrings = keys.map((key) => key.toBase58());
  assert(keyStrings[0] === AUTHORITY.toBase58(), "unexpected_fee_payer");
  for (const required of [
    PROGRAM_DATA,
    PROGRAM_ID,
    new PublicKey("11111111111111111111111111111111"),
    UPGRADEABLE_LOADER,
  ]) {
    assert(
      keyStrings.includes(required.toBase58()),
      `missing_transaction_account:${required.toBase58()}`
    );
  }
  const loaderIndex = keyStrings.indexOf(UPGRADEABLE_LOADER.toBase58());
  const extensionInstruction = instructions.find(
    (ix) => ix.programIdIndex === loaderIndex
  );
  assert(extensionInstruction, "extend_instruction_not_found");
  const instructionData = decodeInstructionData(extensionInstruction.data);
  assert(
    instructionData.toString("hex") === EXPECTED_INSTRUCTION_HEX,
    `extend_instruction_changed:${instructionData.toString("hex")}`
  );
  assert(
    (transaction.meta.logMessages || []).includes(
      `Extended ProgramData account by ${EXPECTED_EXTENSION_BYTES} bytes`
    ),
    "expected_extension_log_missing"
  );

  process.stdout.write(
    `${JSON.stringify(
      {
        verified: true,
        operation: "bootstrap-programdata-extension",
        cluster: "devnet",
        rpcUrl: new URL(rpcUrl).origin,
        genesisHash,
        programId: PROGRAM_ID.toBase58(),
        programData: PROGRAM_DATA.toBase58(),
        owner: program.owner.toBase58(),
        authority: authority.toBase58(),
        signature,
        slot: transaction.slot,
        programDataSlot,
        confirmationStatus: status.confirmationStatus,
        feeLamports: transaction.meta.fee,
        instructionDataHex: instructionData.toString("hex"),
        additionalBytes: EXPECTED_EXTENSION_BYTES,
        accountBytes: programData.data.length,
        payloadCapacityBytes:
          programData.data.length - PROGRAM_DATA_METADATA_BYTES,
        programDataLamports: programData.lamports,
        targetRentLamports: targetRent,
        zeroFilledTailBytes: newTail.length,
        payerBalanceLamports: payerBalance,
        artifact: {
          path: artifactPath,
          bytes: artifact.length,
          sha256: sha256(artifact),
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
