#!/usr/bin/env node

// Read-only verifier for the Phase 1 Devnet upload buffer. It validates the
// pinned program spine, parses the upgradeable-loader buffer metadata, hashes
// the uploaded payload, and summarizes the buffer transaction history. It
// never loads a signer, signs, or submits a transaction.

const { createHash } = require("crypto");
const { readFileSync } = require("fs");
const { resolve } = require("path");
const { Connection, PublicKey } = require("@solana/web3.js");

const PROGRAM_ID = new PublicKey(
  "9312ay7VrpvNYmyEx8TYCqhafbZ2Y5WGVbt6Qp772uUQ",
);
const PROGRAM_DATA = new PublicKey(
  "5wqBqX7qubopQnteX5RPTwBJrSFA6xnMs9Zbwz2BHfzD",
);
const AUTHORITY = new PublicKey("DkFmzzzF89tYZu2JWR1uq3H13U5np9NU6MHF4E61gJmG");
const UPGRADEABLE_LOADER = new PublicKey(
  "BPFLoaderUpgradeab1e11111111111111111111111",
);
const DEVNET_GENESIS = "EtWTRABZaYq6iMfeYKouRu166VU2xqa1wcaWoxPkrZBG";
const EXPECTED_ARTIFACT_BYTES = Number(
  process.env.EXPECTED_ARTIFACT_BYTES || "741248",
);
const EXPECTED_ARTIFACT_SHA256 = String(
  process.env.EXPECTED_ARTIFACT_SHA256 ||
    "588249aecc1c57956a1acf525af31ffa1e756145f5e26b65724f45d499751e87",
).toLowerCase();
const EXPECTED_PRE_UPGRADE_SLOT = Number(
  process.env.EXPECTED_PRE_UPGRADE_SLOT || "501449795",
);
const EXPECTED_BUFFER_ACCOUNT_BYTES = 741285;
const EXPECTED_BUFFER_LAMPORTS = 3766418680;
const EXPECTED_UPLOAD_TRANSACTIONS = 734; // create/initialize + 733 writes
const PROGRAM_DATA_METADATA_BYTES = 45;
const BUFFER_METADATA_BYTES = 37;

function sha256(bytes) {
  return createHash("sha256").update(bytes).digest("hex");
}

function assert(condition, message) {
  if (!condition) throw new Error(message);
}

function parseAuthority(data, expectedVariant, metadataBytes, label) {
  assert(data.length >= metadataBytes, `${label} account data is truncated.`);
  assert(
    data.readUInt32LE(0) === expectedVariant,
    `${label} has the wrong upgradeable-loader state variant.`,
  );
  assert(data[expectedVariant === 1 ? 4 : 12] === 1, `${label} is immutable.`);
  const authorityOffset = expectedVariant === 1 ? 5 : 13;
  return new PublicKey(data.subarray(authorityOffset, authorityOffset + 32));
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

  const rpcUrl = process.env.SOLANA_RPC_URL || "https://api.devnet.solana.com";
  const artifactPath = resolve(
    process.argv[3] ||
      resolve(__dirname, "../target/deploy/chainbound_game.so"),
  );
  const bufferPublicKey = new PublicKey(bufferText);
  const artifact = readFileSync(artifactPath);
  const artifactSha256 = sha256(artifact);
  assert(
    artifact.length === EXPECTED_ARTIFACT_BYTES,
    `Artifact length changed: ${artifact.length}.`,
  );
  assert(
    artifactSha256 === EXPECTED_ARTIFACT_SHA256,
    `Artifact SHA-256 changed: ${artifactSha256}.`,
  );

  const connection = new Connection(rpcUrl, "confirmed");
  const [program, programData, buffer, payerBalance, signatures, genesisHash] =
    await Promise.all([
      connection.getAccountInfo(PROGRAM_ID, "confirmed"),
      connection.getAccountInfo(PROGRAM_DATA, "confirmed"),
      connection.getAccountInfo(bufferPublicKey, "confirmed"),
      connection.getBalance(AUTHORITY, "confirmed"),
      connection.getSignaturesForAddress(
        bufferPublicKey,
        { limit: 1000 },
        "confirmed",
      ),
      connection.getGenesisHash(),
    ]);
  assert(
    genesisHash === DEVNET_GENESIS,
    `RPC is not Solana Devnet (${genesisHash}).`,
  );

  assert(program, "Pinned Program account is missing.");
  assert(program.owner.equals(UPGRADEABLE_LOADER), "Program owner changed.");
  assert(program.data.length >= 36, "Program account data is truncated.");
  assert(program.data.readUInt32LE(0) === 2, "Program state variant changed.");
  const linkedProgramData = new PublicKey(program.data.subarray(4, 36));
  assert(
    linkedProgramData.equals(PROGRAM_DATA),
    `Program links to unexpected ProgramData ${linkedProgramData.toBase58()}.`,
  );

  assert(programData, "Pinned ProgramData account is missing.");
  assert(
    programData.owner.equals(UPGRADEABLE_LOADER),
    "ProgramData owner changed.",
  );
  const programAuthority = parseAuthority(
    programData.data,
    3,
    PROGRAM_DATA_METADATA_BYTES,
    "ProgramData",
  );
  const lastDeploySlot = Number(programData.data.readBigUInt64LE(4));
  assert(programAuthority.equals(AUTHORITY), "Program authority changed.");
  assert(
    lastDeploySlot === EXPECTED_PRE_UPGRADE_SLOT,
    `Program was unexpectedly changed at slot ${lastDeploySlot}.`,
  );
  assert(
    programData.data.length - PROGRAM_DATA_METADATA_BYTES ===
      EXPECTED_ARTIFACT_BYTES,
    "ProgramData capacity changed.",
  );

  assert(buffer, "Upload buffer account is missing.");
  assert(buffer.owner.equals(UPGRADEABLE_LOADER), "Buffer owner is invalid.");
  assert(
    buffer.data.length === EXPECTED_BUFFER_ACCOUNT_BYTES,
    `Buffer account length is ${buffer.data.length}; expected ${EXPECTED_BUFFER_ACCOUNT_BYTES}.`,
  );
  assert(
    buffer.lamports === EXPECTED_BUFFER_LAMPORTS,
    `Buffer has ${buffer.lamports} lamports; expected ${EXPECTED_BUFFER_LAMPORTS}.`,
  );
  const bufferAuthority = parseAuthority(
    buffer.data,
    1,
    BUFFER_METADATA_BYTES,
    "Buffer",
  );
  assert(bufferAuthority.equals(AUTHORITY), "Buffer authority is invalid.");
  const payload = buffer.data.subarray(BUFFER_METADATA_BYTES);
  const payloadSha256 = sha256(payload);
  assert(
    payload.length === EXPECTED_ARTIFACT_BYTES,
    `Buffer payload length is ${payload.length}.`,
  );
  assert(
    payloadSha256 === EXPECTED_ARTIFACT_SHA256,
    `Buffer payload SHA-256 mismatch: ${payloadSha256}.`,
  );

  const currentUploadSignatures = signatures.filter(
    (entry) => entry.slot > EXPECTED_PRE_UPGRADE_SLOT,
  );
  const failedSignatures = currentUploadSignatures.filter(
    (entry) => entry.err !== null,
  );
  assert(
    currentUploadSignatures.length === EXPECTED_UPLOAD_TRANSACTIONS,
    `Found ${currentUploadSignatures.length} post-extension buffer transactions; expected ${EXPECTED_UPLOAD_TRANSACTIONS}.`,
  );
  assert(
    failedSignatures.length === 0,
    `${failedSignatures.length} buffer transactions failed.`,
  );
  const oldest = currentUploadSignatures.at(-1);
  const newest = currentUploadSignatures.at(0);

  process.stdout.write(
    `${JSON.stringify(
      {
        verified: true,
        cluster: "devnet",
        genesisHash,
        rpcUrl: new URL(rpcUrl).origin,
        program: {
          programId: PROGRAM_ID.toBase58(),
          programData: PROGRAM_DATA.toBase58(),
          owner: program.owner.toBase58(),
          authority: programAuthority.toBase58(),
          capacityBytes: programData.data.length - PROGRAM_DATA_METADATA_BYTES,
          lastDeploySlot,
          lamports: programData.lamports,
        },
        buffer: {
          address: bufferPublicKey.toBase58(),
          owner: buffer.owner.toBase58(),
          authority: bufferAuthority.toBase58(),
          accountBytes: buffer.data.length,
          payloadBytes: payload.length,
          lamports: buffer.lamports,
          payloadSha256,
          matchesArtifact: payload.equals(artifact),
        },
        uploadTransactions: {
          cutoffSlotExclusive: EXPECTED_PRE_UPGRADE_SLOT,
          count: currentUploadSignatures.length,
          successful: currentUploadSignatures.length - failedSignatures.length,
          failed: failedSignatures.length,
          createSignature: oldest?.signature || null,
          latestWriteSignature: newest?.signature || null,
          firstSlot: oldest?.slot || null,
          latestSlot: newest?.slot || null,
          olderHistoryIgnored:
            signatures.length - currentUploadSignatures.length,
        },
        payerBalanceLamports: payerBalance,
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
