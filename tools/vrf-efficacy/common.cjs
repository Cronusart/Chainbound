"use strict";
// Shared helpers for the MagicBlock VRF efficacy tools.
// See Docs/Specs/Solana/MAGICBLOCK_VRF_EFFICACY_RUN_PLAN.md (steps 4 and 5).
const path = require("path");

function loadWeb3() {
  try {
    return require("@solana/web3.js");
  } catch {
    return require(path.join(__dirname, "../../backend/chainbound_game/node_modules/@solana/web3.js"));
  }
}

const web3 = loadWeb3();
const { PublicKey, SystemProgram, TransactionInstruction, Transaction } = web3;

const PROGRAM_ID = new PublicKey("9312ay7VrpvNYmyEx8TYCqhafbZ2Y5WGVbt6Qp772uUQ");
const VRF_PROGRAM = new PublicKey("Vrf1RNUjXmQGjmQrQLvJHs9SNkvDJEsRVFPkfSQUwGz");
const ORACLE_QUEUE = new PublicKey("Cuj97ggrhhidhbu39TijNVqE74xvKJ69gDervRUXAxGh");
const SLOT_HASHES = new PublicKey("SysvarS1otHashes111111111111111111111111111");
const UPGRADE_AUTHORITY = "DkFmzzzF89tYZu2JWR1uq3H13U5np9NU6MHF4E61gJmG";
const DEFAULT_RPC = "https://api.devnet.solana.com";

// Anchor discriminators from backend/chainbound_game/idl/chainbound_game.json.
const DISC = {
  request: Buffer.from([224, 239, 0, 38, 149, 200, 66, 156]),
  fulfill: Buffer.from([179, 121, 75, 29, 254, 142, 143, 171]),
  consume: Buffer.from([7, 114, 169, 20, 163, 95, 27, 181]),
};

const ERR = {
  INVALID_AUTHORITY: 6012,
  INVALID_ENCOUNTER_STATUS: 6036,
  CONSTRAINT_SEEDS: 2006,
  CONSTRAINT_ADDRESS: 2012,
};

const STATUS = { 0: "Pending", 1: "Fulfilled", 2: "Consumed" };

function requestPda(owner, seed) {
  return PublicKey.findProgramAddressSync([Buffer.from("encounter_request"), owner.toBuffer(), seed], PROGRAM_ID)[0];
}

function programIdentity() {
  return PublicKey.findProgramAddressSync([Buffer.from("identity")], PROGRAM_ID)[0];
}

function scopedVrfIdentity() {
  return PublicKey.findProgramAddressSync([Buffer.from("identity"), PROGRAM_ID.toBuffer()], VRF_PROGRAM)[0];
}

function requestIx(payer, seed) {
  const pda = requestPda(payer, seed);
  const ix = new TransactionInstruction({
    programId: PROGRAM_ID,
    keys: [
      { pubkey: pda, isSigner: false, isWritable: true },
      { pubkey: payer, isSigner: true, isWritable: true },
      { pubkey: ORACLE_QUEUE, isSigner: false, isWritable: true },
      { pubkey: programIdentity(), isSigner: false, isWritable: false },
      { pubkey: VRF_PROGRAM, isSigner: false, isWritable: false },
      { pubkey: SLOT_HASHES, isSigner: false, isWritable: false },
      { pubkey: SystemProgram.programId, isSigner: false, isWritable: false },
    ],
    data: Buffer.concat([DISC.request, seed]),
  });
  return { ix, pda };
}

function consumeIx(owner, pda) {
  return new TransactionInstruction({
    programId: PROGRAM_ID,
    keys: [
      { pubkey: pda, isSigner: false, isWritable: true },
      { pubkey: owner, isSigner: true, isWritable: false },
    ],
    data: DISC.consume,
  });
}

function fulfillIx(identity, pda, randomness) {
  return new TransactionInstruction({
    programId: PROGRAM_ID,
    keys: [
      { pubkey: identity, isSigner: true, isWritable: false },
      { pubkey: pda, isSigner: false, isWritable: true },
    ],
    data: Buffer.concat([DISC.fulfill, randomness]),
  });
}

// EncounterRequest: disc(8) status(1) randomness(32) owner(32) request_seed(32)
// requested_at_slot(8) fulfilled_at_slot(8) bump(1)
async function readRequest(connection, pda, commitment = "confirmed") {
  const info = await connection.getAccountInfo(pda, commitment);
  if (!info) return null;
  const d = info.data;
  return {
    owner: info.owner.toBase58(),
    status: d[8],
    statusName: STATUS[d[8]] ?? String(d[8]),
    randomness: Buffer.from(d.subarray(9, 41)).toString("hex"),
    requestOwner: new PublicKey(d.subarray(41, 73)).toBase58(),
    requestSeed: Buffer.from(d.subarray(73, 105)).toString("hex"),
    requestedAtSlot: Number(d.readBigUInt64LE(105)),
    fulfilledAtSlot: Number(d.readBigUInt64LE(113)),
  };
}

// Simulates without signatures. Only for cases where a real signer could sign;
// sigVerify:false skips signature checks entirely.
async function simulateUnsigned(connection, feePayer, instructions) {
  const { blockhash } = await connection.getLatestBlockhash("confirmed");
  const tx = new Transaction({ feePayer, recentBlockhash: blockhash });
  for (const ix of instructions) tx.add(ix);
  const wire = tx.serialize({ requireAllSignatures: false, verifySignatures: false }).toString("base64");
  const res = await connection._rpcRequest("simulateTransaction", [
    wire,
    { encoding: "base64", sigVerify: false, replaceRecentBlockhash: true, commitment: "confirmed" },
  ]);
  if (res.error) throw new Error(`simulateTransaction: ${JSON.stringify(res.error)}`);
  return res.result.value;
}

function customErrorCode(err) {
  const inner = err && err.InstructionError && err.InstructionError[1];
  return inner && typeof inner === "object" && "Custom" in inner ? inner.Custom : null;
}

function parseArgs(argv) {
  const out = {};
  for (let i = 0; i < argv.length; i++) {
    if (!argv[i].startsWith("--")) continue;
    const key = argv[i].slice(2);
    const next = argv[i + 1];
    out[key] = next && !next.startsWith("--") ? (i++, next) : true;
  }
  return out;
}

module.exports = {
  web3,
  PROGRAM_ID,
  VRF_PROGRAM,
  ORACLE_QUEUE,
  UPGRADE_AUTHORITY,
  DEFAULT_RPC,
  ERR,
  STATUS,
  requestPda,
  scopedVrfIdentity,
  requestIx,
  consumeIx,
  fulfillIx,
  readRequest,
  simulateUnsigned,
  customErrorCode,
  parseArgs,
};
