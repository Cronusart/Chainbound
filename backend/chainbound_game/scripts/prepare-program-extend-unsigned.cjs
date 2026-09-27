#!/usr/bin/env node

// Builds and simulates the exact unsigned upgradeable-loader ExtendProgram message.
// It never reads a keypair and never signs or submits a transaction.

const { createHash } = require("crypto");
const { readFileSync } = require("fs");
const { resolve } = require("path");
const {
  Connection,
  PublicKey,
  SystemProgram,
  Transaction,
  TransactionInstruction,
} = require("@solana/web3.js");

const PROGRAM_ID = new PublicKey("9312ay7VrpvNYmyEx8TYCqhafbZ2Y5WGVbt6Qp772uUQ");
const EXPECTED_PROGRAM_DATA = new PublicKey("5wqBqX7qubopQnteX5RPTwBJrSFA6xnMs9Zbwz2BHfzD");
const PAYER = new PublicKey("DkFmzzzF89tYZu2JWR1uq3H13U5np9NU6MHF4E61gJmG");
const UPGRADEABLE_LOADER = new PublicKey("BPFLoaderUpgradeab1e11111111111111111111111");
const PROGRAM_DATA_HEADER_BYTES = 45;

function encodeExtendProgram(additionalBytes) {
  if (!Number.isSafeInteger(additionalBytes) || additionalBytes <= 0 || additionalBytes > 0xffffffff)
    throw new Error(`invalid additional byte count: ${additionalBytes}`);
  const data = Buffer.alloc(8);
  data.writeUInt32LE(6, 0); // UpgradeableLoaderInstruction::ExtendProgram bincode variant.
  data.writeUInt32LE(additionalBytes, 4);
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
  if (payload.error) throw new Error(`${method}: ${JSON.stringify(payload.error)}`);
  return payload.result;
}

async function main() {
  const rpcUrl = process.env.SOLANA_RPC_URL || "https://api.devnet.solana.com";
  const artifactPath = resolve(
    process.argv[2] || resolve(__dirname, "../target/deploy/chainbound_game.so"),
  );
  const artifact = readFileSync(artifactPath);
  const artifactSha256 = createHash("sha256").update(artifact).digest("hex");
  const connection = new Connection(rpcUrl, "confirmed");

  const [derivedProgramData] = PublicKey.findProgramAddressSync(
    [PROGRAM_ID.toBuffer()],
    UPGRADEABLE_LOADER,
  );
  if (!derivedProgramData.equals(EXPECTED_PROGRAM_DATA))
    throw new Error(`ProgramData derivation mismatch: ${derivedProgramData.toBase58()}`);

  const [programAccount, programDataAccount, payerBalance, latestBlockhash] = await Promise.all([
    connection.getAccountInfo(PROGRAM_ID, "confirmed"),
    connection.getAccountInfo(EXPECTED_PROGRAM_DATA, "confirmed"),
    connection.getBalance(PAYER, "confirmed"),
    connection.getLatestBlockhash("confirmed"),
  ]);
  if (!programAccount || !programAccount.owner.equals(UPGRADEABLE_LOADER))
    throw new Error("Pinned Program account is missing or has the wrong owner.");
  if (!programDataAccount || !programDataAccount.owner.equals(UPGRADEABLE_LOADER))
    throw new Error("Pinned ProgramData account is missing or has the wrong owner.");

  const targetAllocation = artifact.length + PROGRAM_DATA_HEADER_BYTES;
  const additionalBytes = targetAllocation - programDataAccount.data.length;
  if (additionalBytes <= 0)
    throw new Error(`ProgramData already has enough space (${programDataAccount.data.length}).`);
  const expectedArtifactSha256 = String(process.env.EXPECTED_ARTIFACT_SHA256 || "").trim().toLowerCase();
  if (expectedArtifactSha256 && artifactSha256 !== expectedArtifactSha256)
    throw new Error(`Artifact SHA-256 ${artifactSha256} does not match the approved pin.`);
  const expectedAdditionalBytes = Number(process.env.EXPECTED_EXTENSION_BYTES || 0);
  if (expectedAdditionalBytes && additionalBytes !== expectedAdditionalBytes)
    throw new Error(`Extension delta ${additionalBytes} does not match approved ${expectedAdditionalBytes}.`);
  const targetRent = await connection.getMinimumBalanceForRentExemption(targetAllocation, "confirmed");
  const rentTopUp = Math.max(0, targetRent - programDataAccount.lamports);

  const instruction = new TransactionInstruction({
    programId: UPGRADEABLE_LOADER,
    keys: [
      { pubkey: EXPECTED_PROGRAM_DATA, isSigner: false, isWritable: true },
      { pubkey: PROGRAM_ID, isSigner: false, isWritable: true },
      { pubkey: SystemProgram.programId, isSigner: false, isWritable: false },
      { pubkey: PAYER, isSigner: true, isWritable: true },
    ],
    data: encodeExtendProgram(additionalBytes),
  });
  const transaction = new Transaction({
    feePayer: PAYER,
    recentBlockhash: latestBlockhash.blockhash,
  }).add(instruction);
  const message = transaction.compileMessage();
  const messageBytes = message.serialize();
  const feeResponse = await connection.getFeeForMessage(message, "confirmed");
  if (feeResponse.value == null) throw new Error("RPC did not return a fee for the message.");

  const wireBytes = transaction.serialize({
    requireAllSignatures: false,
    verifySignatures: false,
  });
  const wireBase64 = wireBytes.toString("base64");
  const simulation = await rpcRequest(rpcUrl, "simulateTransaction", [
    wireBase64,
    {
      encoding: "base64",
      commitment: "confirmed",
      sigVerify: false,
      replaceRecentBlockhash: false,
    },
  ]);

  const accountKeys = message.accountKeys.map((key, index) => ({
    index,
    pubkey: key.toBase58(),
    signer: message.isAccountSigner(index),
    writable: message.isAccountWritable(index),
  }));
  const compiledInstruction = message.instructions[0];
  const summary = {
    cluster: "devnet",
    rpcUrl: new URL(rpcUrl).origin,
    programId: PROGRAM_ID.toBase58(),
    programData: EXPECTED_PROGRAM_DATA.toBase58(),
    payer: PAYER.toBase58(),
    artifact: {
      path: artifactPath,
      bytes: artifact.length,
      sha256: artifactSha256,
    },
    allocation: {
      currentBytes: programDataAccount.data.length,
      targetBytes: targetAllocation,
      additionalBytes,
    },
    lamports: {
      currentProgramData: programDataAccount.lamports,
      targetRent,
      rentTopUp,
      payerBalance,
      messageFee: feeResponse.value,
      payerAfterRentAndFee: payerBalance - rentTopUp - feeResponse.value,
    },
    instruction: {
      programId: UPGRADEABLE_LOADER.toBase58(),
      dataHex: instruction.data.toString("hex"),
      decoded: { variant: "ExtendProgram", bincodeVariantIndex: 6, additionalBytes },
      accountIndices: compiledInstruction.accounts,
    },
    message: {
      recentBlockhash: latestBlockhash.blockhash,
      lastValidBlockHeight: latestBlockhash.lastValidBlockHeight,
      bytes: messageBytes.length,
      sha256: createHash("sha256").update(messageBytes).digest("hex"),
      accountKeys,
    },
    unsignedTransaction: {
      bytes: wireBytes.length,
      base64: wireBase64,
    },
    simulation: {
      err: simulation.value.err,
      unitsConsumed: simulation.value.unitsConsumed,
      logs: simulation.value.logs,
    },
  };
  process.stdout.write(`${JSON.stringify(summary, null, 2)}\n`);
}

main().catch((error) => {
  process.stderr.write(`${error instanceof Error ? error.stack : String(error)}\n`);
  process.exitCode = 1;
});
