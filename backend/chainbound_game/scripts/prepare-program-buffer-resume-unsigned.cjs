#!/usr/bin/env node

// Read-only planner for resuming a partially uploaded Phase 1 buffer. It
// validates the existing buffer, compares every loader-write chunk with the
// approved artifact, and simulates the first and last remaining writes. It
// never loads a signer, signs, or submits a transaction.

const { createHash } = require("crypto");
const { readFileSync } = require("fs");
const { resolve } = require("path");
const {
  Connection,
  PublicKey,
  Transaction,
  TransactionInstruction,
} = require("@solana/web3.js");

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
const EXPECTED_BUFFER_ACCOUNT_BYTES = 741285;
const EXPECTED_BUFFER_LAMPORTS = 3766418680;
const BUFFER_METADATA_BYTES = 37;
const CHUNK_BYTES = 1012;

function assert(condition, message) {
  if (!condition) throw new Error(message);
}

function sha256(bytes) {
  return createHash("sha256").update(bytes).digest("hex");
}

function encodeWrite(offset, bytes) {
  const data = Buffer.alloc(16 + bytes.length);
  data.writeUInt32LE(1, 0);
  data.writeUInt32LE(offset, 4);
  data.writeBigUInt64LE(BigInt(bytes.length), 8);
  bytes.copy(data, 16);
  return data;
}

function makeWriteTransaction(bufferPublicKey, offset, bytes, blockhash) {
  return new Transaction({
    feePayer: AUTHORITY,
    recentBlockhash: blockhash,
  }).add(
    new TransactionInstruction({
      programId: LOADER,
      keys: [
        { pubkey: bufferPublicKey, isSigner: false, isWritable: true },
        { pubkey: AUTHORITY, isSigner: true, isWritable: false },
      ],
      data: encodeWrite(offset, bytes),
    }),
  );
}

async function simulate(connection, transaction) {
  const result = await connection.simulateTransaction(transaction, undefined, [
    AUTHORITY,
  ]);
  return {
    err: result.value.err,
    unitsConsumed: result.value.unitsConsumed,
    logs: result.value.logs,
  };
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
  const [account, blockhash, payerBalance, genesisHash] = await Promise.all([
    connection.getAccountInfo(bufferPublicKey, "confirmed"),
    connection.getLatestBlockhash("confirmed"),
    connection.getBalance(AUTHORITY, "confirmed"),
    connection.getGenesisHash(),
  ]);
  assert(
    genesisHash === DEVNET_GENESIS,
    `RPC is not Solana Devnet (${genesisHash}).`,
  );
  assert(account, "Approved buffer does not exist.");
  assert(account.owner.equals(LOADER), "Buffer owner is invalid.");
  assert(
    account.data.length === EXPECTED_BUFFER_ACCOUNT_BYTES,
    `Buffer account length is ${account.data.length}.`,
  );
  assert(
    account.lamports === EXPECTED_BUFFER_LAMPORTS,
    `Buffer funding is ${account.lamports}.`,
  );
  assert(account.data.readUInt32LE(0) === 1, "Account is not a loader Buffer.");
  assert(account.data[4] === 1, "Buffer is immutable.");
  const authority = new PublicKey(account.data.subarray(5, 37));
  assert(authority.equals(AUTHORITY), "Buffer authority is invalid.");

  const payload = account.data.subarray(BUFFER_METADATA_BYTES);
  const remaining = [];
  for (let offset = 0; offset < artifact.length; offset += CHUNK_BYTES) {
    const expected = artifact.subarray(offset, offset + CHUNK_BYTES);
    const actual = payload.subarray(offset, offset + expected.length);
    if (!actual.equals(expected)) {
      remaining.push({ offset, bytes: expected.length, expected });
    }
  }

  const first = remaining[0];
  const last = remaining.at(-1);
  let writeFeeLamports = 0;
  let firstSimulation = null;
  let lastSimulation = null;
  if (first) {
    const firstTransaction = makeWriteTransaction(
      bufferPublicKey,
      first.offset,
      first.expected,
      blockhash.blockhash,
    );
    const lastTransaction = makeWriteTransaction(
      bufferPublicKey,
      last.offset,
      last.expected,
      blockhash.blockhash,
    );
    const [fee, firstResult, lastResult] = await Promise.all([
      connection.getFeeForMessage(
        firstTransaction.compileMessage(),
        "confirmed",
      ),
      simulate(connection, firstTransaction),
      simulate(connection, lastTransaction),
    ]);
    assert(fee.value !== null, "RPC did not return the write fee.");
    writeFeeLamports = fee.value;
    firstSimulation = firstResult;
    lastSimulation = lastResult;
    assert(
      firstResult.err === null,
      "First remaining write simulation failed.",
    );
    assert(lastResult.err === null, "Last remaining write simulation failed.");
  }

  process.stdout.write(
    `${JSON.stringify(
      {
        warning:
          "UNSIGNED RESUME PLAN ONLY: no signer was loaded and nothing was submitted.",
        cluster: "devnet",
        rpcUrl: new URL(rpcUrl).origin,
        buffer: {
          address: bufferPublicKey.toBase58(),
          owner: account.owner.toBase58(),
          authority: authority.toBase58(),
          accountBytes: account.data.length,
          payloadBytes: payload.length,
          lamports: account.lamports,
          currentPayloadSha256: sha256(payload),
        },
        artifact: {
          path: artifactPath,
          bytes: artifact.length,
          sha256: artifactSha256,
        },
        writes: {
          totalChunks: Math.ceil(artifact.length / CHUNK_BYTES),
          completedChunks:
            Math.ceil(artifact.length / CHUNK_BYTES) - remaining.length,
          remainingChunks: remaining.length,
          firstRemainingOffset: first?.offset ?? null,
          firstRemainingBytes: first?.bytes ?? null,
          lastRemainingOffset: last?.offset ?? null,
          lastRemainingBytes: last?.bytes ?? null,
          writeFeeLamports,
          projectedRemainingFeesLamports: writeFeeLamports * remaining.length,
        },
        simulations: {
          firstRemaining: firstSimulation,
          lastRemaining: lastSimulation,
        },
        payerBalanceLamports: payerBalance,
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
