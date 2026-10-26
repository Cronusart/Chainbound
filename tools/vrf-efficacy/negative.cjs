"use strict";
// Step 4 of MAGICBLOCK_VRF_EFFICACY_RUN_PLAN.md: negative cases by simulation.
// No keypair, no signatures, no spend.
//
// usage: node tools/vrf-efficacy/negative.cjs --pda <consumed request PDA> --owner <its owner>
//          --other <a different wallet> [--seeds <latency results.jsonl>] [--rpc <url>]
const fs = require("fs");
const crypto = require("crypto");
const {
  web3,
  DEFAULT_RPC,
  ERR,
  requestIx,
  consumeIx,
  fulfillIx,
  readRequest,
  simulateUnsigned,
  customErrorCode,
  parseArgs,
} = require("./common.cjs");

const { Connection, PublicKey } = web3;

function expectCode(name, result, expected) {
  const code = customErrorCode(result.err);
  const ok = code === expected;
  return { case: name, ok, expected, got: code ?? result.err, logs: tail(result.logs) };
}

function tail(logs) {
  return (logs || []).filter((l) => /Instruction:|Error|error|failed|already in use/i.test(l)).slice(-4);
}

async function main() {
  const args = parseArgs(process.argv.slice(2));
  if (!args.pda || !args.owner || !args.other) {
    throw new Error("--pda, --owner and --other are required");
  }
  const connection = new Connection(args.rpc || DEFAULT_RPC, "confirmed");
  const pda = new PublicKey(args.pda);
  const owner = new PublicKey(args.owner);
  const other = new PublicKey(args.other);

  const before = await readRequest(connection, pda);
  if (!before) throw new Error(`request PDA ${pda.toBase58()} not found`);
  if (before.statusName !== "Consumed") throw new Error(`expected a Consumed PDA, got ${before.statusName}`);

  const results = [];

  // N1: consume a request that is already Consumed.
  results.push(expectCode("N1 consume replay", await simulateUnsigned(connection, owner, [consumeIx(owner, pda)]),
    ERR.INVALID_ENCOUNTER_STATUS));

  // N2: a different wallet consumes someone else's request (PDA seeds include the owner).
  results.push(expectCode("N2 wrong owner", await simulateUnsigned(connection, other, [consumeIx(other, pda)]),
    ERR.CONSTRAINT_SEEDS));

  // N3: forged callback with an ordinary wallet as the "VRF identity".
  results.push(expectCode("N3 forged callback (wrong identity)",
    await simulateUnsigned(connection, owner, [fulfillIx(owner, pda, crypto.randomBytes(32))]),
    ERR.CONSTRAINT_ADDRESS));

  // N6: request again with a seed that already has a PDA (needs a latency results file).
  if (args.seeds) {
    const line = fs.readFileSync(args.seeds, "utf8").split(/\r?\n/).map((l) => l.trim()).filter(Boolean)
      .map((l) => JSON.parse(l)).find((r) => r.ok && r.seedHex && r.payer);
    if (!line) throw new Error(`no successful run with a seed in ${args.seeds}`);
    const payer = new PublicKey(line.payer);
    const { ix } = requestIx(payer, Buffer.from(line.seedHex, "hex"));
    const sim = await simulateUnsigned(connection, payer, [ix]);
    const alreadyInUse = (sim.logs || []).some((l) => /already in use/i.test(l));
    results.push({
      case: "N6 same seed twice",
      ok: sim.err !== null && alreadyInUse,
      expected: "system allocate: already in use",
      got: sim.err,
      logs: tail(sim.logs),
    });
  }

  const after = await readRequest(connection, pda);
  const unchanged = after.statusName === before.statusName && after.randomness === before.randomness;
  results.push({ case: "PDA unchanged after N1-N3", ok: unchanged, expected: before.statusName, got: after.statusName });

  for (const r of results) {
    console.log(`${r.ok ? "PASS" : "FAIL"}  ${r.case}  expected=${r.expected} got=${JSON.stringify(r.got)}`);
    for (const l of r.logs || []) console.log(`        ${l}`);
  }
  const failed = results.filter((r) => !r.ok).length;
  console.log(`\n${results.length - failed}/${results.length} passed`);
  process.exitCode = failed ? 1 : 0;
}

main().catch((error) => {
  console.error(`negative cases failed to run: ${error.message}`);
  process.exitCode = 2;
});
