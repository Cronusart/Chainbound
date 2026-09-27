#!/usr/bin/env node

// Builds and simulates the exact unsigned Gate B Upgrade transaction for the
// verified Phase 1 Devnet buffer. It validates all participating accounts and
// payload bytes. It never loads a signer, signs, or submits a transaction.

const { createHash } = require("crypto");
const { readFileSync } = require("fs");
const { resolve } = require("path");
const bs58Module = require("bs58");
const bs58 = bs58Module.default || bs58Module;
const {
  Connection,
  PublicKey,
  SYSVAR_CLOCK_PUBKEY,
  SYSVAR_RENT_PUBKEY,
  Transaction,
  TransactionInstruction,
} = require("@solana/web3.js");

const PROGRAM_ID = new PublicKey(
  "9312ay7VrpvNYmyEx8TYCqhafbZ2Y5WGVbt6Qp772uUQ",
);
const PROGRAM_DATA = new PublicKey(
  "5wqBqX7qubopQnteX5RPTwBJrSFA6xnMs9Zbwz2BHfzD",
);
const AUTHORITY = new PublicKey("DkFmzzzF89tYZu2JWR1uq3H13U5np9NU6MHF4E61gJmG");
const LOADER = new PublicKey("BPFLoaderUpgradeab1e11111111111111111111111");
const DEVNET_GENESIS = "EtWTRABZaYq6iMfeYKouRu166VU2xqa1wcaWoxPkrZBG";
const EXPECTED_ARTIFACT_BYTES = Number(
  process.env.EXPECTED_ARTIFACT_BYTES || "741248",
);
const EXPECTED_SHA256 = String(
  process.env.EXPECTED_ARTIFACT_SHA256 ||
    "588249aecc1c57956a1acf525af31ffa1e756145f5e26b65724f45d499751e87",
).toLowerCase();
const EXPECTED_PRE_UPGRADE_SLOT = Number(
  process.env.EXPECTED_PRE_UPGRADE_SLOT || "501449795",
);
const EXPECTED_BUFFER_LAMPORTS = 3766418680;
const PROGRAM_DATA_METADATA_BYTES = 45;
const BUFFER_METADATA_BYTES = 37;

function assert(condition, message) {
  if (!condition) throw new Error(message);
}

function sha256(bytes) {
  return createHash("sha256").update(bytes).digest("hex");
}

function variant(index) {
  const data = Buffer.alloc(4);
  data.writeUInt32LE(index, 0);
  return data;
}

async function rpcRequest(endpoint, method, params) {
  const response = await fetch(endpoint, {
    method: "POST",
    headers: { "content-type": "application/json" },
    body: JSON.stringify({ jsonrpc: "2.0", id: 1, method, params }),
  });
  if (!response.ok) throw new Error(`${method} HTTP ${response.status}`);
  const payload = await response.json();
  if (payload.error)
    throw new Error(`${method}: ${JSON.stringify(payload.error)}`);
  return payload.result;
}

async function main() {
  const bufferText = (
    process.env.BUFFER_PUBKEY ||
    process.argv[2] ||
    ""
  ).trim();
  assert(
    bufferText,
    "Set BUFFER_PUBKEY or pass the buffer public key as argv[2].",
  );
  const bufferPublicKey = new PublicKey(bufferText);
  const rpcUrl = process.env.SOLANA_RPC_URL || "https://api.devnet.solana.com";
  const artifactPath = resolve(
    process.argv[3] ||
      resolve(__dirname, "../target/deploy/chainbound_game.so"),
  );
  const artifact = readFileSync(artifactPath);
  const artifactSha256 = sha256(artifact);
  assert(
    artifact.length === EXPECTED_ARTIFACT_BYTES,
    `Artifact length changed: ${artifact.length}.`,
  );
  assert(
    artifactSha256 === EXPECTED_SHA256,
    `Artifact SHA-256 changed: ${artifactSha256}.`,
  );

  const connection = new Connection(rpcUrl, "confirmed");
  const [program, programData, buffer, payerBalance, blockhash, genesisHash] =
    await Promise.all([
      connection.getAccountInfo(PROGRAM_ID, "confirmed"),
      connection.getAccountInfo(PROGRAM_DATA, "confirmed"),
      connection.getAccountInfo(bufferPublicKey, "confirmed"),
      connection.getBalance(AUTHORITY, "confirmed"),
      connection.getLatestBlockhash("confirmed"),
      connection.getGenesisHash(),
    ]);
  assert(
    genesisHash === DEVNET_GENESIS,
    `RPC is not Solana Devnet (${genesisHash}).`,
  );

  assert(program, "Pinned Program is missing.");
  assert(program.owner.equals(LOADER), "Program owner is invalid.");
  assert(
    program.data.readUInt32LE(0) === 2,
    "Program state variant is invalid.",
  );
  assert(
    new PublicKey(program.data.subarray(4, 36)).equals(PROGRAM_DATA),
    "ProgramData link changed.",
  );

  assert(programData, "Pinned ProgramData is missing.");
  assert(programData.owner.equals(LOADER), "ProgramData owner is invalid.");
  assert(
    programData.data.readUInt32LE(0) === 3,
    "ProgramData state variant is invalid.",
  );
  assert(programData.data[12] === 1, "ProgramData is immutable.");
  const programAuthority = new PublicKey(programData.data.subarray(13, 45));
  assert(programAuthority.equals(AUTHORITY), "Upgrade authority changed.");
  const previousDeploySlot = Number(programData.data.readBigUInt64LE(4));
  assert(
    previousDeploySlot === EXPECTED_PRE_UPGRADE_SLOT,
    `Unexpected pre-Upgrade slot ${previousDeploySlot}.`,
  );
  const currentPayload = programData.data.subarray(PROGRAM_DATA_METADATA_BYTES);
  assert(
    currentPayload.length === artifact.length,
    "ProgramData capacity changed.",
  );

  assert(buffer, "Verified upload buffer is missing.");
  assert(buffer.owner.equals(LOADER), "Buffer owner is invalid.");
  assert(buffer.data.readUInt32LE(0) === 1, "Buffer state variant is invalid.");
  assert(buffer.data[4] === 1, "Buffer is immutable.");
  const bufferAuthority = new PublicKey(buffer.data.subarray(5, 37));
  assert(bufferAuthority.equals(AUTHORITY), "Buffer authority is invalid.");
  assert(
    buffer.lamports === EXPECTED_BUFFER_LAMPORTS,
    `Buffer funding changed: ${buffer.lamports}.`,
  );
  const bufferPayload = buffer.data.subarray(BUFFER_METADATA_BYTES);
  assert(
    bufferPayload.length === artifact.length,
    "Buffer payload length changed.",
  );
  assert(
    bufferPayload.equals(artifact),
    "Buffer payload differs from artifact.",
  );

  const instruction = new TransactionInstruction({
    programId: LOADER,
    keys: [
      { pubkey: PROGRAM_DATA, isSigner: false, isWritable: true },
      { pubkey: PROGRAM_ID, isSigner: false, isWritable: true },
      { pubkey: bufferPublicKey, isSigner: false, isWritable: true },
      { pubkey: AUTHORITY, isSigner: false, isWritable: true },
      { pubkey: SYSVAR_RENT_PUBKEY, isSigner: false, isWritable: false },
      { pubkey: SYSVAR_CLOCK_PUBKEY, isSigner: false, isWritable: false },
      { pubkey: AUTHORITY, isSigner: true, isWritable: false },
    ],
    data: variant(3),
  });
  const transaction = new Transaction({
    feePayer: AUTHORITY,
    recentBlockhash: blockhash.blockhash,
  }).add(instruction);
  const message = transaction.compileMessage();
  const fee = await connection.getFeeForMessage(message, "confirmed");
  assert(fee.value !== null, "RPC did not return the Upgrade fee.");
  const encoded = transaction
    .serialize({ requireAllSignatures: false, verifySignatures: false })
    .toString("base64");
  const simulation = await rpcRequest(rpcUrl, "simulateTransaction", [
    encoded,
    {
      encoding: "base64",
      commitment: "confirmed",
      sigVerify: false,
      replaceRecentBlockhash: false,
    },
  ]);
  assert(simulation.value.err === null, "Upgrade simulation failed.");

  process.stdout.write(
    `${JSON.stringify(
      {
        warning:
          "UNSIGNED GATE B PLAN ONLY: no signer was loaded and nothing was submitted.",
        cluster: "devnet",
        genesisHash,
        rpcUrl: new URL(rpcUrl).origin,
        programId: PROGRAM_ID.toBase58(),
        programData: PROGRAM_DATA.toBase58(),
        buffer: bufferPublicKey.toBase58(),
        payerSpillAndUpgradeAuthority: AUTHORITY.toBase58(),
        previousDeploySlot,
        programDataCapacityBytes: currentPayload.length,
        currentDeployedSha256: sha256(currentPayload),
        artifact: {
          path: artifactPath,
          bytes: artifact.length,
          sha256: artifactSha256,
        },
        bufferPayloadSha256: sha256(bufferPayload),
        bufferRefundLamports: buffer.lamports,
        feeLamports: fee.value,
        payerBalanceBeforeLamports: payerBalance,
        projectedPayerBalanceAfterLamports:
          payerBalance + buffer.lamports - fee.value,
        message: {
          bytes: message.serialize().length,
          sha256: sha256(message.serialize()),
          requiredSignatures: message.header.numRequiredSignatures,
          accountKeys: message.accountKeys.map((key, index) => ({
            index,
            pubkey: key.toBase58(),
            signer: message.isAccountSigner(index),
            writable: message.isAccountWritable(index),
          })),
          instruction: {
            programId: LOADER.toBase58(),
            accountIndices: message.instructions[0].accounts,
            dataHex: Buffer.from(
              bs58.decode(message.instructions[0].data),
            ).toString("hex"),
          },
        },
        simulation: simulation.value,
        blockhash,
      },
      null,
      2,
    )}\n`,
  );
}

main().catch((error) => {
  process.stderr.write(
    `${error instanceof Error ? error.stack : String(error)}\n`,
  );
  process.exitCode = 1;
});
