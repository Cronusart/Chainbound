"use strict";
// Devnet Squads v4 multisig for the authority rehearsal (MAINNET_VRF_READINESS_PLAN.md section 1).
// Signs locally with keypair files; secret keys are only read into memory and never printed.
// Refuses to run anywhere but Devnet.
//
//   node tools/vrf-efficacy/squads.cjs create-multisig --payer <devnet keypair> --members <a,b,c>
//          [--threshold 2] [--time-lock 0] [--memo <text>] [--dry-run]
//   node tools/vrf-efficacy/squads.cjs show-multisig --multisig <address>
const fs = require("fs");
const multisig = require("@sqds/multisig");
const { Connection, Keypair, PublicKey, TransactionMessage, VersionedTransaction } = require("@solana/web3.js");
const { parseArgs } = require("./common.cjs");

const DEVNET_RPC = "https://api.devnet.solana.com";
const DEVNET_GENESIS = "EtWTRABZaYq6iMfeYKouRu166VU2xqa1wcaWoxPkrZBG";
const UPGRADE_AUTHORITY = "DkFmzzzF89tYZu2JWR1uq3H13U5np9NU6MHF4E61gJmG";
const { Permissions } = multisig.types;

function loadKeypair(file) {
  const bytes = JSON.parse(fs.readFileSync(file, "utf8"));
  if (!Array.isArray(bytes) || bytes.length !== 64) throw new Error("keypair file must be a 64-byte JSON array");
  return Keypair.fromSecretKey(Uint8Array.from(bytes));
}

async function requireDevnet(connection) {
  const genesis = await connection.getGenesisHash();
  if (genesis !== DEVNET_GENESIS) throw new Error(`refusing to run: genesis ${genesis} is not Devnet`);
}

async function describe(connection, multisigPda) {
  const ms = await multisig.accounts.Multisig.fromAccountAddress(connection, multisigPda);
  const [vault] = multisig.getVaultPda({ multisigPda, index: 0 });
  return {
    multisig: multisigPda.toBase58(),
    vault: vault.toBase58(),
    threshold: ms.threshold,
    timeLock: ms.timeLock,
    configAuthority: ms.configAuthority.equals(PublicKey.default) ? "none" : ms.configAuthority.toBase58(),
    members: ms.members.map((m) => ({ key: m.key.toBase58(), permissions: m.permissions.mask })),
  };
}

async function createMultisig(connection, args) {
  if (!args.payer) throw new Error("--payer <devnet keypair> is required");
  const members = String(args.members || "").split(",").map((s) => s.trim()).filter(Boolean).map((k) => new PublicKey(k));
  const threshold = Number(args.threshold || 2);
  const timeLock = Number(args["time-lock"] || 0);
  if (new Set(members.map((m) => m.toBase58())).size !== members.length) throw new Error("--members has duplicates");
  if (members.length < 1 || threshold < 1 || threshold > members.length) throw new Error("threshold must be 1..members");

  const payer = loadKeypair(args.payer);
  if (payer.publicKey.toBase58() === UPGRADE_AUTHORITY) throw new Error("refusing to use the program upgrade authority as payer");
  // The create key only seeds the multisig address; it has no authority afterwards.
  const createKey = Keypair.generate();
  const [multisigPda] = multisig.getMultisigPda({ createKey: createKey.publicKey });
  const [vault] = multisig.getVaultPda({ multisigPda, index: 0 });
  const [programConfigPda] = multisig.getProgramConfigPda({});
  const programConfig = await multisig.accounts.ProgramConfig.fromAccountAddress(connection, programConfigPda);

  const ix = multisig.instructions.multisigCreateV2({
    treasury: programConfig.treasury,
    creator: payer.publicKey,
    multisigPda,
    configAuthority: null,
    threshold,
    members: members.map((key) => ({ key, permissions: Permissions.all() })),
    timeLock,
    createKey: createKey.publicKey,
    rentCollector: null,
    memo: args.memo || "chainbound devnet authority rehearsal",
  });
  const { blockhash, lastValidBlockHeight } = await connection.getLatestBlockhash("confirmed");
  const message = new TransactionMessage({ payerKey: payer.publicKey, recentBlockhash: blockhash, instructions: [ix] })
    .compileToV0Message();
  const tx = new VersionedTransaction(message);
  tx.sign([payer, createKey]);

  const fee = Number(programConfig.multisigCreationFee);
  const balance = await connection.getBalance(payer.publicKey, "confirmed");
  console.log(`payer            ${payer.publicKey.toBase58()}  (${balance / 1e9} SOL)`);
  console.log(`multisig         ${multisigPda.toBase58()}`);
  console.log(`vault (index 0)  ${vault.toBase58()}   <- the address that becomes the authority`);
  console.log(`threshold        ${threshold} of ${members.length}, time lock ${timeLock} s, config authority none`);
  members.forEach((m, i) => console.log(`member ${i + 1}         ${m.toBase58()}  (initiate, vote, execute)`));
  console.log(`creation fee     ${fee / 1e9} SOL to Squads treasury ${programConfig.treasury.toBase58()}, plus rent`);

  if (args["dry-run"]) {
    const sim = await connection.simulateTransaction(tx, { commitment: "confirmed" });
    console.log(`dry run: err=${JSON.stringify(sim.value.err)} units=${sim.value.unitsConsumed}`);
    for (const l of (sim.value.logs || []).filter((l) => /Instruction|Error|failed/.test(l))) console.log(`  ${l}`);
    console.log("dry run only: nothing was sent. A real run uses a fresh create key, so its addresses will differ.");
    return;
  }
  const signature = await connection.sendTransaction(tx);
  const conf = await connection.confirmTransaction({ signature, blockhash, lastValidBlockHeight }, "confirmed");
  if (conf.value.err) throw new Error(`${signature} failed: ${JSON.stringify(conf.value.err)}`);
  console.log(`create tx ${signature}`);
  console.log(`read back: ${JSON.stringify(await describe(connection, multisigPda), null, 2)}`);
}

async function main() {
  const [command, ...rest] = process.argv.slice(2);
  const args = parseArgs(rest);
  const connection = new Connection(args.rpc || DEVNET_RPC, "confirmed");
  await requireDevnet(connection);
  switch (command) {
    case "create-multisig": return createMultisig(connection, args);
    case "show-multisig":
      if (!args.multisig) throw new Error("--multisig <address> is required");
      return console.log(JSON.stringify(await describe(connection, new PublicKey(args.multisig)), null, 2));
    default: throw new Error("command: create-multisig | show-multisig");
  }
}

main().catch((error) => {
  console.error(`squads tool failed: ${error.message}`);
  process.exitCode = 1;
});
