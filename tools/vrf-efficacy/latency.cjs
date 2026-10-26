"use strict";
// Step 5 of MAGICBLOCK_VRF_EFFICACY_RUN_PLAN.md: sequential request -> fulfill -> consume
// round trips on Devnet, timed from the chain. Signs locally with a keypair file;
// the secret key is only read into memory and never printed.
//
// usage: node tools/vrf-efficacy/latency.cjs --keypair <path to keypair.json>
//          [--runs 10] [--gap 5] [--timeout 60] [--out vrf-latency-results.jsonl] [--rpc <url>] [--dry-run]
const fs = require("fs");
const crypto = require("crypto");
const {
  web3,
  DEFAULT_RPC,
  ERR,
  UPGRADE_AUTHORITY,
  scopedVrfIdentity,
  requestIx,
  consumeIx,
  readRequest,
  simulateUnsigned,
  customErrorCode,
  parseArgs,
} = require("./common.cjs");

const { Connection, Keypair, Transaction } = web3;
const COST_PER_RUN = 1_780_000; // 2 fees + 1,270,000 rent + 500,000 MagicBlock charge

const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

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

// Resolves with the first time the request reaches `minStatus` at `commitment`.
async function waitForStatus(connection, pda, minStatus, commitment, deadline) {
  let subId = null;
  try {
    const seen = new Promise((resolve) => {
      subId = connection.onAccountChange(pda, (info) => {
        if (info.data[8] >= minStatus) resolve(Date.now());
      }, commitment);
    });
    const polled = (async () => {
      while (Date.now() < deadline) {
        const req = await readRequest(connection, pda, commitment).catch(() => null);
        if (req && req.status >= minStatus) return Date.now();
        await sleep(250);
      }
      return null;
    })();
    return await Promise.race([seen, polled]);
  } finally {
    if (subId !== null) await connection.removeAccountChangeListener(subId).catch(() => {});
  }
}

async function findFulfill(connection, pda, requestSig, consumeSig) {
  const history = await connection.getSignaturesForAddress(pda, { limit: 20 }, "confirmed");
  const others = history.filter((h) => h.signature !== requestSig && h.signature !== consumeSig);
  const winner = others.find((h) => h.err === null);
  if (!winner) return { duplicates: others.length };
  const tx = await connection.getTransaction(winner.signature, { commitment: "confirmed", maxSupportedTransactionVersion: 0 });
  const keys = tx.transaction.message.staticAccountKeys || tx.transaction.message.accountKeys;
  return {
    fulfillSig: winner.signature,
    fulfillSlot: tx.slot,
    fulfillSigner: keys[0].toBase58(),
    scopedIdentityPresent: keys.some((k) => k.toBase58() === scopedVrfIdentity().toBase58()),
    duplicates: others.filter((h) => h.err !== null).length,
  };
}

async function runOnce(connection, payer, n, timeoutMs) {
  const rec = { run: n, payer: payer.publicKey.toBase58(), ok: false };
  const seed = crypto.randomBytes(32);
  rec.seedHex = seed.toString("hex");
  const { ix, pda } = requestIx(payer.publicKey, seed);
  rec.pda = pda.toBase58();

  const t0 = Date.now();
  const deadline = t0 + timeoutMs;
  const fulfilledConfirmed = waitForStatus(connection, pda, 1, "confirmed", deadline);
  rec.requestSig = await send(connection, payer, ix);
  rec.msRequestConfirmed = Date.now() - t0;
  rec.requestSlot = (await connection.getSignatureStatus(rec.requestSig)).value?.slot ?? null;

  // N7: consume before the oracle answers, if we're early enough.
  const early = await readRequest(connection, pda, "confirmed");
  if (early && early.status === 0) {
    const sim = await simulateUnsigned(connection, payer.publicKey, [consumeIx(payer.publicKey, pda)]);
    rec.n7ConsumeBeforeFulfill = customErrorCode(sim.err) === ERR.INVALID_ENCOUNTER_STATUS ? "rejected 6036" : JSON.stringify(sim.err);
  } else {
    rec.n7ConsumeBeforeFulfill = "skipped: already fulfilled";
  }

  const tConfirmed = await fulfilledConfirmed;
  if (!tConfirmed) {
    rec.error = "timeout waiting for fulfill (confirmed)";
    return rec;
  }
  rec.msFulfilledConfirmed = tConfirmed - t0;
  const tFinalized = await waitForStatus(connection, pda, 1, "finalized", Date.now() + timeoutMs);
  rec.msFulfilledFinalized = tFinalized ? tFinalized - t0 : null;

  rec.consumeSig = await send(connection, payer, consumeIx(payer.publicKey, pda));
  rec.msConsumed = Date.now() - t0;

  Object.assign(rec, await findFulfill(connection, pda, rec.requestSig, rec.consumeSig));
  if (rec.fulfillSlot && rec.requestSlot) rec.slotDelta = rec.fulfillSlot - rec.requestSlot;

  const after = await readRequest(connection, pda, "confirmed");
  rec.statusAfter = after?.statusName;
  rec.randomnessHex = after?.randomness;
  rec.ok = after?.statusName === "Consumed" && /[1-9a-f]/.test(after.randomness) && rec.scopedIdentityPresent === true;
  if (!rec.ok && !rec.error) rec.error = "post-check failed";
  return rec;
}

function stats(values) {
  const v = values.filter((x) => typeof x === "number").sort((a, b) => a - b);
  if (!v.length) return "n/a";
  const pick = (q) => v[Math.min(v.length - 1, Math.ceil(q * v.length) - 1)];
  return `min ${v[0]}  median ${pick(0.5)}  p90 ${pick(0.9)}  max ${v[v.length - 1]}  (n=${v.length})`;
}

async function main() {
  const args = parseArgs(process.argv.slice(2));
  if (!args.keypair) throw new Error("--keypair <path> is required");
  const runs = Number(args.runs || 10);
  const gapMs = Number(args.gap || 5) * 1000;
  const timeoutMs = Number(args.timeout || 60) * 1000;
  const out = args.out || "vrf-latency-results.jsonl";
  const connection = new Connection(args.rpc || DEFAULT_RPC, "confirmed");

  const payer = loadKeypair(args.keypair);
  const address = payer.publicKey.toBase58();
  if (address === UPGRADE_AUTHORITY) throw new Error("refusing to use the program upgrade authority");
  const balance = await connection.getBalance(payer.publicKey, "confirmed");
  console.log(`payer ${address}  balance ${balance} lamports  runs ${runs}  est. cost ${runs * COST_PER_RUN}`);
  if (balance < runs * COST_PER_RUN + 10_000_000) throw new Error("balance too low for the requested runs");
  if (args["dry-run"]) {
    const { ix } = requestIx(payer.publicKey, crypto.randomBytes(32));
    const sim = await simulateUnsigned(connection, payer.publicKey, [ix]);
    console.log(`dry run: request simulation err=${JSON.stringify(sim.err)} units=${sim.unitsConsumed}`);
    return;
  }

  const records = [];
  let timeoutsInARow = 0;
  for (let n = 1; n <= runs; n++) {
    let rec;
    try {
      rec = await runOnce(connection, payer, n, timeoutMs);
    } catch (error) {
      rec = { run: n, payer: address, ok: false, error: error.message };
    }
    records.push(rec);
    fs.appendFileSync(out, JSON.stringify(rec) + "\n");
    console.log(`run ${n}: ${rec.ok ? "ok" : "FAIL"}  fulfilled(confirmed) ${rec.msFulfilledConfirmed ?? "-"} ms  ` +
      `finalized ${rec.msFulfilledFinalized ?? "-"} ms  slots ${rec.slotDelta ?? "-"}  ${rec.error ?? ""}`);
    timeoutsInARow = rec.error && rec.error.startsWith("timeout") ? timeoutsInARow + 1 : 0;
    if (timeoutsInARow >= 3) {
      console.log("stopping: 3 timeouts in a row");
      break;
    }
    if (n < runs) await sleep(gapMs);
  }

  const okRuns = records.filter((r) => r.ok);
  console.log("\n=== summary");
  console.log(`runs ok: ${okRuns.length}/${records.length}`);
  console.log(`request -> fulfilled (confirmed) ms: ${stats(okRuns.map((r) => r.msFulfilledConfirmed))}`);
  console.log(`request -> fulfilled (finalized) ms: ${stats(okRuns.map((r) => r.msFulfilledFinalized))}`);
  console.log(`request -> consumed ms:              ${stats(okRuns.map((r) => r.msConsumed))}`);
  console.log(`request slot -> fulfill slot:        ${stats(okRuns.map((r) => r.slotDelta))}`);
  console.log(`duplicate oracle submissions:        ${okRuns.reduce((s, r) => s + (r.duplicates || 0), 0)}`);
  console.log(`N7 consume-before-fulfill:           ${[...new Set(records.map((r) => r.n7ConsumeBeforeFulfill).filter(Boolean))].join(", ")}`);
  console.log(`results written to ${out}`);
}

// Set exitCode rather than calling process.exit(): exiting while fetch/websocket handles are
// still closing trips a libuv assertion on Windows with Node 24.
main().catch((error) => {
  console.error(`latency run failed: ${error.message}`);
  process.exitCode = 1;
});
