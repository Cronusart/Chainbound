"use strict";
// Devnet tooling for on-chain VRF rarity tiers (KNOWN_RARITY_WEIGHTS_v1.md).
// Signs locally with keypair files; secret keys are only read into memory and never printed.
//
//   node tools/vrf-efficacy/roll.cjs simulate [--odds ultra] [--draws 1000000]
//   node tools/vrf-efficacy/roll.cjs show-odds
//   node tools/vrf-efficacy/roll.cjs set-odds --keypair <program authority> --kind known|stamp
//          --odds ultra|steep [--pool-hash <hex>] [--dry-run]
//   node tools/vrf-efficacy/roll.cjs run --keypair <result authority> [--kind known] [--runs 10]
//          [--gap 3] [--timeout 60] [--out vrf-roll-results.jsonl] [--dry-run]
//   node tools/vrf-efficacy/roll.cjs close --keypair <result authority> --results <file>
const fs = require("fs");
const crypto = require("crypto");
const { web3, DEFAULT_RPC, PROGRAM_ID, ORACLE_QUEUE, scopedVrfIdentity, simulateUnsigned, customErrorCode, parseArgs } =
  require("./common.cjs");

const { Connection, Keypair, PublicKey, SystemProgram, Transaction, TransactionInstruction } = web3;
const VRF_PROGRAM = new PublicKey("Vrf1RNUjXmQGjmQrQLvJHs9SNkvDJEsRVFPkfSQUwGz");
const SLOT_HASHES = new PublicKey("SysvarS1otHashes111111111111111111111111111");
const UPGRADE_AUTHORITY = "DkFmzzzF89tYZu2JWR1uq3H13U5np9NU6MHF4E61gJmG";

const TIERS = ["Common", "Uncommon", "Rare", "Epic", "Legendary", "Mythic"];
const KINDS = { known: 1, stamp: 2 };
// Weights in parts per million, Common..Mythic.
const ODDS = {
  ultra: [640_000, 250_000, 90_000, 17_000, 2_800, 200],
  steep: [620_000, 250_000, 100_000, 25_000, 4_500, 500],
};
// Placeholder until CombatServerTablesExporter generates the per-tier pools and their hash.
const DRAFT_POOL_HASH = sha256(Buffer.from("chainbound:roll-pools:draft-devnet-test"));
const ROLL_CLOSE_DELAY_SLOTS = 9_000;

function sha256(...parts) {
  const h = crypto.createHash("sha256");
  for (const p of parts) h.update(p);
  return h.digest();
}

const disc = (name) => sha256(Buffer.from(`global:${name}`)).subarray(0, 8);

function cumulative(weights) {
  let sum = 0;
  return weights.map((w) => (sum += w));
}

function u32s(values) {
  const b = Buffer.alloc(4 * values.length);
  values.forEach((v, i) => b.writeUInt32LE(v, 4 * i));
  return b;
}

// Same rule as roll_tier in lib.rs.
function rollTier(randomness, cumulativePpm) {
  const draw = Number(Buffer.from(randomness).readBigUInt64LE(0) % 1_000_000n);
  return cumulativePpm.findIndex((t) => draw < t);
}

const pda = (seeds) => PublicKey.findProgramAddressSync(seeds, PROGRAM_ID)[0];
const gameConfigPda = () => pda([Buffer.from("game_config")]);
const rollOddsPda = (kind) => pda([Buffer.from("roll_odds"), Buffer.from([kind])]);
const rollRequestPda = (kind, opHash) => pda([Buffer.from("roll_request"), Buffer.from([kind]), opHash]);
const programIdentity = () => pda([Buffer.from("identity")]);

// RollOdds: disc(8) kind(1) odds_version(4) cumulative_ppm(24) pool_hash(32) bump(1)
async function readOdds(connection, kind) {
  const info = await connection.getAccountInfo(rollOddsPda(kind), "confirmed");
  if (!info) return null;
  const d = info.data;
  return {
    kind: d[8],
    oddsVersion: d.readUInt32LE(9),
    cumulativePpm: [0, 1, 2, 3, 4, 5].map((i) => d.readUInt32LE(13 + 4 * i)),
    poolHash: d.subarray(37, 69).toString("hex"),
  };
}

// RollRequest: disc(8) status(1) tier(1) randomness(32) kind(1) operation_hash(32) subject_hash(32)
// odds_version(4) cumulative_ppm(24) pool_hash(32) requested(8) fulfilled(8) consumed(8) result_hash(32) bump(1)
async function readRoll(connection, address, commitment = "confirmed") {
  const info = await connection.getAccountInfo(address, commitment);
  if (!info) return null;
  const d = info.data;
  return {
    status: d[8],
    tier: d[9],
    randomness: d.subarray(10, 42),
    kind: d[42],
    operationHash: d.subarray(43, 75).toString("hex"),
    oddsVersion: d.readUInt32LE(107),
    cumulativePpm: [0, 1, 2, 3, 4, 5].map((i) => d.readUInt32LE(111 + 4 * i)),
    poolHash: d.subarray(135, 167).toString("hex"),
    requestedAtSlot: Number(d.readBigUInt64LE(167)),
    fulfilledAtSlot: Number(d.readBigUInt64LE(175)),
    consumedAtSlot: Number(d.readBigUInt64LE(183)),
  };
}

function setOddsIx(authority, kind, cumulativePpm, poolHash, exists) {
  const keys = exists
    ? [
        { pubkey: rollOddsPda(kind), isSigner: false, isWritable: true },
        { pubkey: gameConfigPda(), isSigner: false, isWritable: false },
        { pubkey: authority, isSigner: true, isWritable: false },
      ]
    : [
        { pubkey: rollOddsPda(kind), isSigner: false, isWritable: true },
        { pubkey: gameConfigPda(), isSigner: false, isWritable: false },
        { pubkey: authority, isSigner: true, isWritable: true },
        { pubkey: SystemProgram.programId, isSigner: false, isWritable: false },
      ];
  const data = exists
    ? Buffer.concat([disc("update_roll_odds"), u32s(cumulativePpm), poolHash])
    : Buffer.concat([disc("initialize_roll_odds"), Buffer.from([kind]), u32s(cumulativePpm), poolHash]);
  return new TransactionInstruction({ programId: PROGRAM_ID, keys, data });
}

function requestRollIx(authority, kind, opHash, subjectHash) {
  return new TransactionInstruction({
    programId: PROGRAM_ID,
    keys: [
      { pubkey: rollRequestPda(kind, opHash), isSigner: false, isWritable: true },
      { pubkey: rollOddsPda(kind), isSigner: false, isWritable: false },
      { pubkey: gameConfigPda(), isSigner: false, isWritable: false },
      { pubkey: authority, isSigner: true, isWritable: true },
      { pubkey: ORACLE_QUEUE, isSigner: false, isWritable: true },
      { pubkey: programIdentity(), isSigner: false, isWritable: false },
      { pubkey: VRF_PROGRAM, isSigner: false, isWritable: false },
      { pubkey: SLOT_HASHES, isSigner: false, isWritable: false },
      { pubkey: SystemProgram.programId, isSigner: false, isWritable: false },
    ],
    data: Buffer.concat([disc("request_roll"), Buffer.from([kind]), opHash, subjectHash]),
  });
}

function consumeRollIx(authority, address, resultHash) {
  return new TransactionInstruction({
    programId: PROGRAM_ID,
    keys: [
      { pubkey: address, isSigner: false, isWritable: true },
      { pubkey: gameConfigPda(), isSigner: false, isWritable: false },
      { pubkey: authority, isSigner: true, isWritable: false },
    ],
    data: Buffer.concat([disc("consume_roll"), resultHash]),
  });
}

function closeRollIx(authority, address) {
  return new TransactionInstruction({
    programId: PROGRAM_ID,
    keys: [
      { pubkey: address, isSigner: false, isWritable: true },
      { pubkey: gameConfigPda(), isSigner: false, isWritable: false },
      { pubkey: authority, isSigner: true, isWritable: true },
    ],
    data: disc("close_roll"),
  });
}

function loadKeypair(file) {
  const bytes = JSON.parse(fs.readFileSync(file, "utf8"));
  if (!Array.isArray(bytes) || bytes.length !== 64) throw new Error("keypair file must be a 64-byte JSON array");
  return Keypair.fromSecretKey(Uint8Array.from(bytes));
}

async function send(connection, payer, ix) {
  const { blockhash, lastValidBlockHeight } = await connection.getLatestBlockhash("confirmed");
  const tx = new Transaction({ feePayer: payer.publicKey, recentBlockhash: blockhash }).add(ix);
  tx.sign(payer);
  const signature = await connection.sendRawTransaction(tx.serialize(), { preflightCommitment: "confirmed" });
  const conf = await connection.confirmTransaction({ signature, blockhash, lastValidBlockHeight }, "confirmed");
  if (conf.value.err) throw new Error(`${signature} failed: ${JSON.stringify(conf.value.err)}`);
  return signature;
}

async function readAuthorities(connection) {
  const info = await connection.getAccountInfo(gameConfigPda(), "confirmed");
  if (!info) throw new Error("GameConfig not found");
  return {
    programAuthority: new PublicKey(info.data.subarray(8, 40)).toBase58(),
    resultAuthority: new PublicKey(info.data.subarray(40, 72)).toBase58(),
  };
}

const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

function histogram(tiers) {
  const counts = TIERS.map(() => 0);
  for (const t of tiers) counts[t]++;
  return counts;
}

// ---- commands

function simulate(args) {
  const weights = ODDS[args.odds || "ultra"];
  if (!weights) throw new Error(`unknown --odds ${args.odds}`);
  const cum = cumulative(weights);
  const draws = Number(args.draws || 1_000_000);
  const counts = TIERS.map(() => 0);
  for (let i = 0; i < draws; i++) counts[rollTier(crypto.randomBytes(8), cum)]++;
  console.log(`odds ${args.odds || "ultra"}  draws ${draws}  (crypto.randomBytes standing in for VRF bytes)`);
  TIERS.forEach((name, i) => {
    const expected = (weights[i] / 1_000_000) * draws;
    const sd = Math.sqrt(expected * (1 - weights[i] / 1_000_000));
    const z = sd ? (counts[i] - expected) / sd : 0;
    console.log(`${name.padEnd(10)} expected ${expected.toFixed(0).padStart(8)}  got ${String(counts[i]).padStart(8)}  z ${z.toFixed(2)}`);
  });
}

async function showOdds(connection) {
  for (const [name, kind] of Object.entries(KINDS)) {
    const odds = await readOdds(connection, kind);
    console.log(`${name}: ${odds ? JSON.stringify(odds) : "not initialized"}  (PDA ${rollOddsPda(kind).toBase58()})`);
  }
}

async function setOdds(connection, args) {
  const kind = KINDS[args.kind];
  if (!kind) throw new Error("--kind known|stamp is required");
  const weights = ODDS[args.odds];
  if (!weights) throw new Error("--odds ultra|steep is required");
  const cum = cumulative(weights);
  const poolHash = args["pool-hash"] ? Buffer.from(args["pool-hash"], "hex") : DRAFT_POOL_HASH;
  if (poolHash.length !== 32) throw new Error("--pool-hash must be 32 bytes of hex");

  const signer = loadKeypair(args.keypair);
  const { programAuthority } = await readAuthorities(connection);
  if (signer.publicKey.toBase58() !== programAuthority) {
    throw new Error(`keypair is ${signer.publicKey.toBase58()}, but GameConfig.program_authority is ${programAuthority}`);
  }
  const before = await readOdds(connection, kind);
  console.log(`before: ${before ? JSON.stringify(before) : "not initialized"}`);
  console.log(`after:  cumulative_ppm ${JSON.stringify(cum)}  pool_hash ${poolHash.toString("hex")}`);
  const ix = setOddsIx(signer.publicKey, kind, cum, poolHash, Boolean(before));
  if (args["dry-run"]) {
    const sim = await simulateUnsigned(connection, signer.publicKey, [ix]);
    console.log(`dry run: err=${JSON.stringify(sim.err)} units=${sim.unitsConsumed}`);
    for (const l of (sim.logs || []).filter((l) => /Instruction|Error|failed/.test(l))) console.log(`  ${l}`);
    return;
  }
  const sig = await send(connection, signer, ix);
  console.log(`set-odds tx ${sig}`);
  console.log(`now:    ${JSON.stringify(await readOdds(connection, kind))}`);
}

async function waitFulfilled(connection, address, deadline) {
  while (Date.now() < deadline) {
    const roll = await readRoll(connection, address).catch(() => null);
    if (roll && roll.status >= 1) return roll;
    await sleep(250);
  }
  return null;
}

async function runRolls(connection, args) {
  const kind = KINDS[args.kind || "known"];
  if (!kind) throw new Error("--kind known|stamp");
  const runs = Number(args.runs || 10);
  const gapMs = Number(args.gap || 3) * 1000;
  const timeoutMs = Number(args.timeout || 60) * 1000;
  const out = args.out || "vrf-roll-results.jsonl";

  const signer = loadKeypair(args.keypair);
  const address = signer.publicKey.toBase58();
  if (address === UPGRADE_AUTHORITY) throw new Error("refusing to use the program upgrade authority");
  const { resultAuthority } = await readAuthorities(connection);
  if (address !== resultAuthority) throw new Error(`keypair is ${address}, but GameConfig.result_authority is ${resultAuthority}`);
  const odds = await readOdds(connection, kind);
  if (!odds) throw new Error(`RollOdds for ${args.kind || "known"} is not initialized; run set-odds first`);
  console.log(`result authority ${address}  odds v${odds.oddsVersion} ${JSON.stringify(odds.cumulativePpm)}`);

  const subjectHash = sha256(Buffer.from("chainbound:devnet-roll-test"));
  if (args["dry-run"]) {
    const ix = requestRollIx(signer.publicKey, kind, crypto.randomBytes(32), subjectHash);
    const sim = await simulateUnsigned(connection, signer.publicKey, [ix]);
    console.log(`dry run: request_roll err=${JSON.stringify(sim.err)} units=${sim.unitsConsumed}`);
    for (const l of (sim.logs || []).filter((l) => /Instruction|Error|failed/.test(l))) console.log(`  ${l}`);
    return;
  }

  const records = [];
  for (let n = 1; n <= runs; n++) {
    const opHash = crypto.randomBytes(32);
    const rollPda = rollRequestPda(kind, opHash);
    const rec = { run: n, kind, pda: rollPda.toBase58(), operationHashHex: opHash.toString("hex"), ok: false };
    try {
      const t0 = Date.now();
      rec.requestSig = await send(connection, signer, requestRollIx(signer.publicKey, kind, opHash, subjectHash));
      const roll = await waitFulfilled(connection, rollPda, t0 + timeoutMs);
      if (!roll) throw new Error("timeout waiting for fulfill");
      rec.msFulfilled = Date.now() - t0;
      rec.tier = roll.tier;
      rec.tierName = TIERS[roll.tier];
      rec.oddsVersion = roll.oddsVersion;
      rec.randomnessHex = Buffer.from(roll.randomness).toString("hex");
      rec.recomputedTier = rollTier(roll.randomness, roll.cumulativePpm);
      rec.slotDelta = roll.fulfilledAtSlot - roll.requestedAtSlot;
      const resultHash = sha256(Buffer.from("chainbound:devnet-roll-test-result"), opHash, Buffer.from([roll.tier]));
      rec.consumeSig = await send(connection, signer, consumeRollIx(signer.publicKey, rollPda, resultHash));
      const after = await readRoll(connection, rollPda);
      rec.consumedAtSlot = after.consumedAtSlot;
      rec.ok = after.status === 2 && rec.recomputedTier === rec.tier;
      if (!rec.ok) rec.error = "post-check failed";
    } catch (error) {
      rec.error = error.message;
    }
    records.push(rec);
    fs.appendFileSync(out, JSON.stringify(rec) + "\n");
    console.log(`run ${n}: ${rec.ok ? "ok" : "FAIL"}  tier ${rec.tierName ?? "-"}  recomputed ${TIERS[rec.recomputedTier] ?? "-"}  ` +
      `${rec.msFulfilled ?? "-"} ms  slots ${rec.slotDelta ?? "-"}  ${rec.error ?? ""}`);
    if (n < runs) await sleep(gapMs);
  }

  const ok = records.filter((r) => r.ok);
  console.log("\n=== summary");
  console.log(`runs ok: ${ok.length}/${records.length}  (on-chain tier == tier recomputed from bytes: ${ok.length})`);
  const counts = histogram(ok.map((r) => r.tier));
  TIERS.forEach((name, i) => console.log(`${name.padEnd(10)} ${counts[i]}`));
  console.log(`results written to ${out}; close the PDAs after ${ROLL_CLOSE_DELAY_SLOTS} slots (~1 h) with the close command`);
}

async function closeRolls(connection, args) {
  if (!args.results) throw new Error("--results <file> is required");
  const signer = loadKeypair(args.keypair);
  if (signer.publicKey.toBase58() === UPGRADE_AUTHORITY) throw new Error("refusing to use the program upgrade authority");
  const slot = await connection.getSlot("confirmed");
  const rows = fs.readFileSync(args.results, "utf8").split(/\r?\n/).filter(Boolean).map((l) => JSON.parse(l));
  let closed = 0;
  for (const r of rows.filter((x) => x.pda)) {
    const address = new PublicKey(r.pda);
    const roll = await readRoll(connection, address);
    if (!roll) continue;
    if (roll.status !== 2 || slot < roll.consumedAtSlot + ROLL_CLOSE_DELAY_SLOTS) {
      console.log(`skip ${r.pda}: status ${roll.status}, opens at slot ${roll.consumedAtSlot + ROLL_CLOSE_DELAY_SLOTS} (now ${slot})`);
      continue;
    }
    console.log(`closed ${r.pda}: ${await send(connection, signer, closeRollIx(signer.publicKey, address))}`);
    closed++;
  }
  console.log(`${closed} closed`);
}

async function main() {
  const [command, ...rest] = process.argv.slice(2);
  const args = parseArgs(rest);
  const connection = new Connection(args.rpc || DEFAULT_RPC, "confirmed");
  switch (command) {
    case "simulate": return simulate(args);
    case "show-odds": return showOdds(connection);
    case "set-odds": return setOdds(connection, args);
    case "run": return runRolls(connection, args);
    case "close": return closeRolls(connection, args);
    default: throw new Error("command: simulate | show-odds | set-odds | run | close");
  }
}

module.exports = { rollTier, cumulative, ODDS, scopedVrfIdentity, customErrorCode };

if (require.main === module) {
  main().catch((error) => {
    console.error(`roll tool failed: ${error.message}`);
    process.exitCode = 1;
  });
}
