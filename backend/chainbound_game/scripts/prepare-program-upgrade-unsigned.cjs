#!/usr/bin/env node

// Builds the exact unsigned buffer-create, representative loader-write, and
// pinned-program Upgrade messages for the current artifact. BUFFER_PUBKEY may
// supply an operator-generated public key; this script never reads its secret.
// Without BUFFER_PUBKEY it generates a throwaway public key in memory. It never
// signs and never submits a transaction.

const { createHash } = require("crypto");
const { readFileSync } = require("fs");
const { resolve } = require("path");
const bs58Module = require("bs58");
const bs58 = bs58Module.default || bs58Module;
const {
  Connection,
  Keypair,
  PublicKey,
  SystemProgram,
  SYSVAR_CLOCK_PUBKEY,
  SYSVAR_RENT_PUBKEY,
  Transaction,
  TransactionInstruction,
} = require("@solana/web3.js");

const PROGRAM_ID = new PublicKey(
  "9312ay7VrpvNYmyEx8TYCqhafbZ2Y5WGVbt6Qp772uUQ",
);
const EXPECTED_PROGRAM_DATA = new PublicKey(
  "5wqBqX7qubopQnteX5RPTwBJrSFA6xnMs9Zbwz2BHfzD",
);
const PAYER = new PublicKey("DkFmzzzF89tYZu2JWR1uq3H13U5np9NU6MHF4E61gJmG");
const DEVNET_GENESIS = "EtWTRABZaYq6iMfeYKouRu166VU2xqa1wcaWoxPkrZBG";
const EXPECTED_PRE_UPGRADE_SLOT = Number(
  process.env.EXPECTED_PRE_UPGRADE_SLOT || "501449795",
);
const UPGRADEABLE_LOADER = new PublicKey(
  "BPFLoaderUpgradeab1e11111111111111111111111",
);
const BUFFER_HEADER_BYTES = 37;
// Agave 2.3 `solana program write-buffer` deliberately calculates the buffer
// funding amount with `size_of_programdata(max_len)` (45-byte metadata), even
// though `create_buffer` allocates `size_of_buffer(program_len)` (37 bytes).
// Mirror the CLI exactly so the operator approval uses the real temporary
// outflow rather than only the account's minimum rent-exempt balance.
const PROGRAM_DATA_HEADER_BYTES = 45;
const PACKET_DATA_SIZE = 1232;

function bincodeVariant(index) {
  const data = Buffer.alloc(4);
  data.writeUInt32LE(index, 0);
  return data;
}

function encodeWrite(offset, bytes) {
  const data = Buffer.alloc(16 + bytes.length);
  data.writeUInt32LE(1, 0); // UpgradeableLoaderInstruction::Write
  data.writeUInt32LE(offset, 4);
  data.writeBigUInt64LE(BigInt(bytes.length), 8);
  bytes.copy(data, 16);
  return data;
}

function compileUnsigned(instructions, recentBlockhash) {
  const transaction = new Transaction({ feePayer: PAYER, recentBlockhash });
  transaction.add(...instructions);
  const message = transaction.compileMessage();
  const messageBytes = message.serialize();
  return {
    transaction,
    message,
    summary: {
      messageBytes: messageBytes.length,
      messageSha256: createHash("sha256").update(messageBytes).digest("hex"),
      requiredSignatures: message.header.numRequiredSignatures,
      accountKeys: message.accountKeys.map((key, index) => ({
        index,
        pubkey: key.toBase58(),
        signer: message.isAccountSigner(index),
        writable: message.isAccountWritable(index),
      })),
      instructions: message.instructions.map((instruction) => ({
        programIdIndex: instruction.programIdIndex,
        accountIndices: instruction.accounts,
        dataHex: Buffer.from(bs58.decode(instruction.data)).toString("hex"),
      })),
    },
  };
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
  const rpcUrl = process.env.SOLANA_RPC_URL || "https://api.devnet.solana.com";
  const artifactPath = resolve(
    process.argv[2] ||
      resolve(__dirname, "../target/deploy/chainbound_game.so"),
  );
  const artifact = readFileSync(artifactPath);
  const artifactSha256 = createHash("sha256").update(artifact).digest("hex");
  const expectedArtifactSha256 = String(
    process.env.EXPECTED_ARTIFACT_SHA256 || "",
  )
    .trim()
    .toLowerCase();
  if (expectedArtifactSha256 && artifactSha256 !== expectedArtifactSha256)
    throw new Error(
      `Artifact SHA-256 ${artifactSha256} does not match the approved pin.`,
    );
  const connection = new Connection(rpcUrl, "confirmed");
  const suppliedBufferPublicKey = process.env.BUFFER_PUBKEY?.trim();
  const bufferPublicKey = suppliedBufferPublicKey
    ? new PublicKey(suppliedBufferPublicKey)
    : Keypair.generate().publicKey; // Secret is never retained, printed, or used.

  const [derivedProgramData] = PublicKey.findProgramAddressSync(
    [PROGRAM_ID.toBuffer()],
    UPGRADEABLE_LOADER,
  );
  if (!derivedProgramData.equals(EXPECTED_PROGRAM_DATA))
    throw new Error(
      `ProgramData derivation mismatch: ${derivedProgramData.toBase58()}`,
    );

  const [
    programAccount,
    programDataAccount,
    bufferAccount,
    payerBalance,
    latestBlockhash,
    genesisHash,
  ] = await Promise.all([
    connection.getAccountInfo(PROGRAM_ID, "confirmed"),
    connection.getAccountInfo(EXPECTED_PROGRAM_DATA, "confirmed"),
    connection.getAccountInfo(bufferPublicKey, "confirmed"),
    connection.getBalance(PAYER, "confirmed"),
    connection.getLatestBlockhash("confirmed"),
    connection.getGenesisHash(),
  ]);
  if (genesisHash !== DEVNET_GENESIS)
    throw new Error(`RPC is not Solana Devnet (${genesisHash}).`);
  if (!programAccount || !programAccount.owner.equals(UPGRADEABLE_LOADER))
    throw new Error(
      "Pinned Program account is missing or has the wrong owner.",
    );
  if (
    !programDataAccount ||
    !programDataAccount.owner.equals(UPGRADEABLE_LOADER)
  )
    throw new Error(
      "Pinned ProgramData account is missing or has the wrong owner.",
    );
  if (
    programDataAccount.data.length <
    artifact.length + PROGRAM_DATA_HEADER_BYTES
  )
    throw new Error(
      `ProgramData capacity ${
        programDataAccount.data.length - PROGRAM_DATA_HEADER_BYTES
      } is below artifact ${
        artifact.length
      }; complete the approved extension first.`,
    );
  if (
    programDataAccount.data.readUInt32LE(0) !== 3 ||
    programDataAccount.data[12] !== 1 ||
    !new PublicKey(programDataAccount.data.subarray(13, 45)).equals(PAYER)
  )
    throw new Error("Pinned ProgramData authority or layout changed.");
  const programDataSlot = Number(programDataAccount.data.readBigUInt64LE(4));
  if (programDataSlot !== EXPECTED_PRE_UPGRADE_SLOT)
    throw new Error(
      `ProgramData slot ${programDataSlot} does not match the approved extension slot ${EXPECTED_PRE_UPGRADE_SLOT}.`,
    );
  if (bufferAccount)
    throw new Error("Selected buffer public key already exists on Devnet.");

  const bufferBytes = artifact.length + BUFFER_HEADER_BYTES;
  const bufferRentFundingBasisBytes =
    artifact.length + PROGRAM_DATA_HEADER_BYTES;
  const bufferFundingLamports =
    await connection.getMinimumBalanceForRentExemption(
      bufferRentFundingBasisBytes,
      "confirmed",
    );

  const createInstructions = [
    SystemProgram.createAccount({
      fromPubkey: PAYER,
      newAccountPubkey: bufferPublicKey,
      lamports: bufferFundingLamports,
      space: bufferBytes,
      programId: UPGRADEABLE_LOADER,
    }),
    new TransactionInstruction({
      programId: UPGRADEABLE_LOADER,
      keys: [
        { pubkey: bufferPublicKey, isSigner: false, isWritable: true },
        { pubkey: PAYER, isSigner: false, isWritable: false },
      ],
      data: bincodeVariant(0), // InitializeBuffer
    }),
  ];
  const create = compileUnsigned(createInstructions, latestBlockhash.blockhash);

  const emptyWrite = compileUnsigned(
    [
      new TransactionInstruction({
        programId: UPGRADEABLE_LOADER,
        keys: [
          { pubkey: bufferPublicKey, isSigner: false, isWritable: true },
          { pubkey: PAYER, isSigner: true, isWritable: false },
        ],
        data: encodeWrite(0, Buffer.alloc(0)),
      }),
    ],
    latestBlockhash.blockhash,
  );
  const emptyWriteWireBytes = emptyWrite.transaction.serialize({
    requireAllSignatures: false,
    verifySignatures: false,
  }).length;
  const chunkBytes = PACKET_DATA_SIZE - emptyWriteWireBytes - 1;
  const writeCount = Math.ceil(artifact.length / chunkBytes);
  const finalOffset = (writeCount - 1) * chunkBytes;
  const firstWrite = compileUnsigned(
    [
      new TransactionInstruction({
        programId: UPGRADEABLE_LOADER,
        keys: [
          { pubkey: bufferPublicKey, isSigner: false, isWritable: true },
          { pubkey: PAYER, isSigner: true, isWritable: false },
        ],
        data: encodeWrite(0, artifact.subarray(0, chunkBytes)),
      }),
    ],
    latestBlockhash.blockhash,
  );
  const lastWrite = compileUnsigned(
    [
      new TransactionInstruction({
        programId: UPGRADEABLE_LOADER,
        keys: [
          { pubkey: bufferPublicKey, isSigner: false, isWritable: true },
          { pubkey: PAYER, isSigner: true, isWritable: false },
        ],
        data: encodeWrite(finalOffset, artifact.subarray(finalOffset)),
      }),
    ],
    latestBlockhash.blockhash,
  );

  const upgrade = compileUnsigned(
    [
      new TransactionInstruction({
        programId: UPGRADEABLE_LOADER,
        keys: [
          { pubkey: EXPECTED_PROGRAM_DATA, isSigner: false, isWritable: true },
          { pubkey: PROGRAM_ID, isSigner: false, isWritable: true },
          { pubkey: bufferPublicKey, isSigner: false, isWritable: true },
          { pubkey: PAYER, isSigner: false, isWritable: true }, // spill
          { pubkey: SYSVAR_RENT_PUBKEY, isSigner: false, isWritable: false },
          { pubkey: SYSVAR_CLOCK_PUBKEY, isSigner: false, isWritable: false },
          { pubkey: PAYER, isSigner: true, isWritable: false }, // upgrade authority
        ],
        data: bincodeVariant(3), // Upgrade
      }),
    ],
    latestBlockhash.blockhash,
  );

  const [createFee, writeFee, upgradeFee] = await Promise.all([
    connection.getFeeForMessage(create.message, "confirmed"),
    connection.getFeeForMessage(firstWrite.message, "confirmed"),
    connection.getFeeForMessage(upgrade.message, "confirmed"),
  ]);
  if (
    createFee.value == null ||
    writeFee.value == null ||
    upgradeFee.value == null
  )
    throw new Error("RPC did not return all message fees.");

  const createWireBase64 = create.transaction
    .serialize({ requireAllSignatures: false, verifySignatures: false })
    .toString("base64");
  const createSimulation = await rpcRequest(rpcUrl, "simulateTransaction", [
    createWireBase64,
    {
      encoding: "base64",
      commitment: "confirmed",
      sigVerify: false,
      replaceRecentBlockhash: false,
    },
  ]);

  const uploadFees =
    createFee.value + writeFee.value * writeCount + upgradeFee.value;
  const summary = {
    warning: suppliedBufferPublicKey
      ? "UNSIGNED PLAN ONLY: the supplied buffer public key is included, but no secret was read. Blockhashes expire; rerun immediately before approval. Nothing was signed or sent."
      : "SAMPLE ONLY: blockhash and throwaway buffer public key expire/change; rerun immediately before approval. No secret was retained and nothing was signed or sent.",
    cluster: "devnet",
    genesisHash,
    rpcUrl: new URL(rpcUrl).origin,
    programId: PROGRAM_ID.toBase58(),
    programData: EXPECTED_PROGRAM_DATA.toBase58(),
    programDataSlot,
    payerAndUpgradeAuthority: PAYER.toBase58(),
    bufferPublicKey: bufferPublicKey.toBase58(),
    bufferPublicKeySource: suppliedBufferPublicKey
      ? "BUFFER_PUBKEY"
      : "throwaway-in-memory",
    artifact: {
      path: artifactPath,
      bytes: artifact.length,
      sha256: artifactSha256,
    },
    buffer: {
      allocationBytes: bufferBytes,
      rentFundingBasisBytes: bufferRentFundingBasisBytes,
      fundingLamports: bufferFundingLamports,
      note: `Matches Agave 2.3 write-buffer: the ${bufferBytes}-byte buffer account is temporarily funded using the ${bufferRentFundingBasisBytes}-byte ProgramData rent basis.`,
    },
    writes: {
      packetDataBytes: PACKET_DATA_SIZE,
      emptyWriteWireBytes,
      chunkBytes,
      transactionCount: writeCount,
      finalOffset,
      finalChunkBytes: artifact.length - finalOffset,
    },
    fees: {
      createBuffer: createFee.value,
      eachWrite: writeFee.value,
      allWrites: writeFee.value * writeCount,
      upgrade: upgradeFee.value,
      uploadAndUpgradeTotal: uploadFees,
    },
    balance: {
      payerLamports: payerBalance,
      transientUploadOutflowBeforeRefund: bufferFundingLamports + uploadFees,
      note: "Upgrade drains the buffer; when ProgramData is already rent-exempt, buffer rent spills back to this payer.",
    },
    blockhash: latestBlockhash,
    messages: {
      createBuffer: { ...create.summary, simulation: createSimulation.value },
      firstWrite: firstWrite.summary,
      lastWrite: lastWrite.summary,
      upgrade: upgrade.summary,
    },
  };
  process.stdout.write(`${JSON.stringify(summary, null, 2)}\n`);
}

main().catch((error) => {
  process.stderr.write(
    `${error instanceof Error ? error.stack : String(error)}\n`,
  );
  process.exitCode = 1;
});
