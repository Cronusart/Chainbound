#!/usr/bin/env node

// Read-only post-Upgrade verifier for the pinned Phase 1 Devnet program. It
// hashes the executable bytes directly from ProgramData, verifies authority and
// capacity, proves the Upgrade signature is the newest ProgramData transaction,
// and confirms that the loader atomically drained/closed the upload buffer.
// It never loads a signer, signs, or submits a transaction.

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
const PRE_UPGRADE_SLOT = Number(
  process.env.EXPECTED_PRE_UPGRADE_SLOT || "501449795",
);
const PROGRAM_DATA_METADATA_BYTES = 45;

function assert(condition, message) {
  if (!condition) throw new Error(message);
}

function sha256(bytes) {
  return createHash("sha256").update(bytes).digest("hex");
}

async function main() {
  const upgradeSignature = (
    process.env.UPGRADE_SIGNATURE ||
    process.argv[2] ||
    ""
  ).trim();
  const bufferText = (
    process.env.BUFFER_PUBKEY ||
    process.argv[3] ||
    ""
  ).trim();
  assert(upgradeSignature, "Set UPGRADE_SIGNATURE or pass it as argv[2].");
  assert(bufferText, "Set BUFFER_PUBKEY or pass it as argv[3].");

  const rpcUrl = process.env.SOLANA_RPC_URL || "https://api.devnet.solana.com";
  const artifactPath = resolve(
    process.argv[4] ||
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
  const [
    program,
    programData,
    buffer,
    payerBalance,
    signatureStatuses,
    transaction,
    newestProgramDataSignatures,
    genesisHash,
  ] = await Promise.all([
    connection.getAccountInfo(PROGRAM_ID, "confirmed"),
    connection.getAccountInfo(PROGRAM_DATA, "confirmed"),
    connection.getAccountInfo(bufferPublicKey, "confirmed"),
    connection.getBalance(AUTHORITY, "confirmed"),
    connection.getSignatureStatuses([upgradeSignature], {
      searchTransactionHistory: true,
    }),
    connection.getTransaction(upgradeSignature, {
      commitment: "confirmed",
      maxSupportedTransactionVersion: 0,
    }),
    connection.getSignaturesForAddress(PROGRAM_DATA, { limit: 1 }, "confirmed"),
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
  assert(
    programData.data.length >= PROGRAM_DATA_METADATA_BYTES,
    "ProgramData is truncated.",
  );
  assert(
    programData.data.readUInt32LE(0) === 3,
    "ProgramData state variant changed.",
  );
  assert(programData.data[12] === 1, "ProgramData became immutable.");
  const authority = new PublicKey(programData.data.subarray(13, 45));
  assert(authority.equals(AUTHORITY), "Upgrade authority changed.");
  const lastDeploySlot = Number(programData.data.readBigUInt64LE(4));
  assert(
    lastDeploySlot > PRE_UPGRADE_SLOT,
    `lastDeploySlot ${lastDeploySlot} did not advance past ${PRE_UPGRADE_SLOT}.`,
  );

  const deployedPayload = programData.data.subarray(
    PROGRAM_DATA_METADATA_BYTES,
  );
  const deployedSha256 = sha256(deployedPayload);
  assert(
    deployedPayload.length === EXPECTED_ARTIFACT_BYTES,
    `Deployed payload length is ${deployedPayload.length}.`,
  );
  assert(
    deployedSha256 === EXPECTED_ARTIFACT_SHA256,
    `Deployed SHA-256 mismatch: ${deployedSha256}.`,
  );
  assert(
    deployedPayload.equals(artifact),
    "Deployed payload differs by bytes.",
  );

  assert(
    buffer === null,
    "Buffer still exists; the Upgrade did not complete its atomic drain/close.",
  );
  const status = signatureStatuses.value[0];
  assert(status, "Upgrade signature was not found in transaction history.");
  assert(status.err === null, "Upgrade signature has an on-chain error.");
  assert(transaction, "Upgrade transaction details are unavailable.");
  assert(transaction.meta, "Upgrade transaction metadata is unavailable.");
  assert(transaction.meta.err === null, "Upgrade transaction failed.");
  assert(
    transaction.slot === lastDeploySlot,
    `Upgrade slot ${transaction.slot} differs from lastDeploySlot ${lastDeploySlot}.`,
  );
  assert(
    newestProgramDataSignatures[0]?.signature === upgradeSignature,
    "Upgrade signature is not the newest ProgramData transaction.",
  );

  process.stdout.write(
    `${JSON.stringify(
      {
        verified: true,
        cluster: "devnet",
        genesisHash,
        rpcUrl: new URL(rpcUrl).origin,
        programId: PROGRAM_ID.toBase58(),
        programData: PROGRAM_DATA.toBase58(),
        owner: program.owner.toBase58(),
        authority: authority.toBase58(),
        dataLen: deployedPayload.length,
        lastDeploySlot,
        artifact: {
          path: artifactPath,
          bytes: artifact.length,
          sha256: artifactSha256,
        },
        deployed: {
          bytes: deployedPayload.length,
          sha256: deployedSha256,
          exactByteMatch: deployedPayload.equals(artifact),
          method:
            "Fetched ProgramData at confirmed commitment, removed the 45-byte upgradeable-loader metadata header, then SHA-256 hashed and byte-compared the payload.",
        },
        upgrade: {
          signature: upgradeSignature,
          slot: transaction.slot,
          confirmationStatus: status.confirmationStatus,
          error: status.err,
          feeLamports: transaction.meta.fee,
        },
        buffer: {
          address: bufferPublicKey.toBase58(),
          existsAfterUpgrade: false,
          closeAndRefundSignature: upgradeSignature,
          note: "The upgradeable loader drains the buffer in the Upgrade instruction, so Upgrade and buffer close/refund share one signature.",
        },
        programDataLamports: programData.lamports,
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
