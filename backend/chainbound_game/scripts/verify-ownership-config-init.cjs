#!/usr/bin/env node
"use strict";

// Read-only Gate E verifier. It compares the finalized transaction instruction
// and live OwnershipConfig bytes against the exact approved proposal.

const bs58Module = require("bs58");
const bs58 = bs58Module.default || bs58Module;
const { Connection, PublicKey } = require("@solana/web3.js");

const RPC_URL = process.env.CHAINBOUND_SOLANA_RPC_URL || "https://api.devnet.solana.com";
const DEVNET_GENESIS = "EtWTRABZaYq6iMfeYKouRu166VU2xqa1wcaWoxPkrZBG";
const PROGRAM_ID = new PublicKey("9312ay7VrpvNYmyEx8TYCqhafbZ2Y5WGVbt6Qp772uUQ");
const PROGRAM_AUTHORITY = new PublicKey("DkFmzzzF89tYZu2JWR1uq3H13U5np9NU6MHF4E61gJmG");
const GAME_CONFIG = new PublicKey("FdA2EJ89Xtqn6Z1pxayBQnSJQ8pVpVAuEjaUW8qbU8nx");
const OWNERSHIP_CONFIG = new PublicKey("6Gj8af5cnLSKFWcdfFLr7fihhNmp97BdPRt76zWemPPZ");
const INITIALIZE_DISCRIMINATOR = Buffer.from([241, 13, 60, 90, 194, 86, 195, 199]);
const OWNERSHIP_DISCRIMINATOR = Buffer.from([227, 236, 116, 131, 86, 134, 101, 163]);
const COLLECTIONS = [
  "AUQuvgpFDUekeVh2GHri99A59G4YY5LUWdGw9fKTB9wC",
  "DP3rTx145BPfunueM9SHjYJXBDCK5GmrdwZjpCvgf6U",
  "6wPnru8RDTsnCyujzPMq8v9U8pD6ynRa8es3ZXcesbNs",
  "76aVsw9xScewNG4wkiE75asdM5bbZaHS5CG78263yMcV",
];
const POOL_HASH_HEX = "6cea5ce56944a88486cafbb568a628b5aa5664c3db652a79cd90016427aaa4b4";

function assert(condition, message) {
  if (!condition) throw new Error(message);
}

function expectedInstructionData() {
  const data = Buffer.alloc(182);
  let offset = 0;
  INITIALIZE_DISCRIMINATOR.copy(data, offset); offset += 8;
  data.writeUInt16LE(1, offset); offset += 2;
  for (const address of COLLECTIONS) {
    new PublicKey(address).toBuffer().copy(data, offset); offset += 32;
  }
  data.writeUInt32LE(75, offset); offset += 4;
  data.writeUInt32LE(250, offset); offset += 4;
  data.writeUInt32LE(1, offset); offset += 4;
  Buffer.from(POOL_HASH_HEX, "hex").copy(data, offset);
  return data;
}

function decodeConfig(account, expectedBump) {
  assert(account?.owner.equals(PROGRAM_ID), "OwnershipConfig missing or wrong owner.");
  const data = account.data;
  assert(data.length === 183, `OwnershipConfig data length is ${data.length}, expected 183.`);
  assert(data.subarray(0, 8).equals(OWNERSHIP_DISCRIMINATOR), "OwnershipConfig discriminator mismatch.");
  const decoded = {
    schemaVersion: data.readUInt16LE(8),
    movesCollection: new PublicKey(data.subarray(10, 42)).toBase58(),
    archivesCollection: new PublicKey(data.subarray(42, 74)).toBase58(),
    coversCollection: new PublicKey(data.subarray(74, 106)).toBase58(),
    cosmeticsCollection: new PublicKey(data.subarray(106, 138)).toBase58(),
    stampRamCost: data.readUInt32LE(138),
    bindRamCost: data.readUInt32LE(142),
    generatorVersion: data.readUInt32LE(146),
    poolHashHex: data.subarray(150, 182).toString("hex"),
    bump: data[182],
  };
  assert(decoded.schemaVersion === 1, "Schema mismatch.");
  const fields = ["movesCollection", "archivesCollection", "coversCollection", "cosmeticsCollection"];
  COLLECTIONS.forEach((address, index) => assert(decoded[fields[index]] === address, `${fields[index]} mismatch.`));
  assert(decoded.stampRamCost === 75, "Stamp cost mismatch.");
  assert(decoded.bindRamCost === 250, "Bind cost mismatch.");
  assert(decoded.generatorVersion === 1, "Generator version mismatch.");
  assert(decoded.poolHashHex === POOL_HASH_HEX, "Pool hash mismatch.");
  assert(decoded.bump === expectedBump, "Bump mismatch.");
  return decoded;
}

async function main() {
  const signature = (process.env.OWNERSHIP_CONFIG_SIGNATURE || process.argv[2] || "").trim();
  assert(signature, "Set OWNERSHIP_CONFIG_SIGNATURE or pass it as argv[2].");
  const connection = new Connection(RPC_URL, "confirmed");
  const genesisHash = await connection.getGenesisHash();
  assert(genesisHash === DEVNET_GENESIS, `RPC is not Solana devnet (${genesisHash}).`);
  const [derived, bump] = PublicKey.findProgramAddressSync([Buffer.from("ownership_config")], PROGRAM_ID);
  assert(derived.equals(OWNERSHIP_CONFIG), `Derived OwnershipConfig changed to ${derived}.`);
  const [account, statuses, transaction, newest, payerBalance] = await Promise.all([
    connection.getAccountInfo(OWNERSHIP_CONFIG, "confirmed"),
    connection.getSignatureStatuses([signature], { searchTransactionHistory: true }),
    connection.getTransaction(signature, { commitment: "confirmed", maxSupportedTransactionVersion: 0 }),
    connection.getSignaturesForAddress(OWNERSHIP_CONFIG, { limit: 1 }, "confirmed"),
    connection.getBalance(PROGRAM_AUTHORITY, "confirmed"),
  ]);
  const status = statuses.value[0];
  assert(status?.err === null, "OwnershipConfig signature missing or failed.");
  assert(transaction?.meta?.err === null, "OwnershipConfig transaction failed.");
  assert(newest[0]?.signature === signature, "Signature is not the newest OwnershipConfig transaction.");
  const message = transaction.transaction.message;
  const keys = message.accountKeys.map((key) => key.toBase58());
  assert(keys[0] === PROGRAM_AUTHORITY.toBase58(), "Pinned authority is not the fee payer.");
  assert(keys.includes(OWNERSHIP_CONFIG.toBase58()), "Transaction omits OwnershipConfig.");
  assert(keys.includes(GAME_CONFIG.toBase58()), "Transaction omits GameConfig.");
  const programIndex = keys.indexOf(PROGRAM_ID.toBase58());
  const instruction = message.instructions.find((item) => item.programIdIndex === programIndex);
  assert(instruction, "InitializeOwnershipConfig instruction was not found.");
  assert(
    Buffer.from(bs58.decode(instruction.data)).equals(expectedInstructionData()),
    "Instruction bytes differ from approved Gate E payload.",
  );
  const decoded = decodeConfig(account, bump);
  process.stdout.write(`${JSON.stringify({
    verified: true,
    gate: "E",
    cluster: "devnet",
    rpcUrl: new URL(RPC_URL).origin,
    genesisHash,
    programId: PROGRAM_ID.toBase58(),
    gameConfig: GAME_CONFIG.toBase58(),
    ownershipConfig: OWNERSHIP_CONFIG.toBase58(),
    signature,
    slot: transaction.slot,
    confirmationStatus: status.confirmationStatus,
    error: status.err,
    feeLamports: transaction.meta.fee,
    accountLamports: account.lamports,
    payerBalanceLamports: payerBalance,
    account: decoded,
  }, null, 2)}\n`);
}

main().catch((error) => {
  process.stderr.write(`${error instanceof Error ? error.stack || error.message : error}\n`);
  process.exitCode = 1;
});
