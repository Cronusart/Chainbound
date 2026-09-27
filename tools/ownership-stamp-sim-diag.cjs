"use strict";
const fs = require("fs");
const { Connection, PublicKey, Transaction } = require("@solana/web3.js");

const wallet = process.env.WALLET;
const knownId = process.env.KNOWN_ID;
const idem = process.env.IDEM;
const anon = process.env.ANON;
const access = process.env.ACCESS;
const rpc = process.env.RPC;
const program = new PublicKey(process.env.PROGRAM);
const fn = process.env.FN;
const out = process.env.OUT;

function pda(seeds) {
  return PublicKey.findProgramAddressSync(
    seeds.map((s) => (typeof s === "string" ? Buffer.from(s) : s)),
    program,
  )[0];
}

(async () => {
  const connection = new Connection(rpc, "confirmed");
  const owner = new PublicKey(wallet);
  const profile = pda(["player_profile", owner.toBytes()]);
  const vault = pda(["move_vault", owner.toBytes()]);
  const [profileInfo, vaultInfo, bal] = await Promise.all([
    connection.getAccountInfo(profile, "confirmed"),
    connection.getAccountInfo(vault, "confirmed"),
    connection.getBalance(owner, "confirmed"),
  ]);

  let ramBalance = null;
  if (profileInfo && profileInfo.data.length >= 48) {
    // PlayerProfile after 8-byte disc + 32-byte owner => ram u32 often follows; try common offset 40
    ramBalance = profileInfo.data.readUInt32LE(40);
  }
  let knownCount = null;
  let nextInstanceId = null;
  if (vaultInfo && vaultInfo.data.length >= 48) {
    // best-effort: many layouts put counts near head after disc+owner
    knownCount = vaultInfo.data.readUInt32LE(40);
    try { nextInstanceId = vaultInfo.data.readBigUInt64LE(44).toString(); } catch {}
  }

  const prepRes = await fetch(fn, {
    method: "POST",
    headers: {
      Authorization: `Bearer ${access}`,
      apikey: anon,
      "Content-Type": "application/json",
    },
    body: JSON.stringify({
      wallet_address: wallet,
      known_instance_id: knownId,
      modifier_count: 1,
      idempotency_key: idem,
    }),
  });
  const prepJson = await prepRes.json().catch(() => ({}));
  const summary = {
    prepareHttp: prepRes.status,
    prepareSuccess: prepJson.success === true,
    prepareError: prepJson.error || prepJson.message || prepJson.code || null,
    wallet,
    knownId,
    idempotencyKey: idem,
    walletLamports: bal,
    profileExists: !!profileInfo,
    vaultExists: !!vaultInfo,
    profileRamBalanceGuess: ramBalance,
    vaultKnownCountGuess: knownCount,
    vaultNextInstanceIdGuess: nextInstanceId,
  };

  if (!prepJson.success || !prepJson.transaction_base64) {
    fs.writeFileSync(out, JSON.stringify(summary, null, 2));
    console.log(JSON.stringify(summary, null, 2));
    process.exit(2);
  }

  const tx = Transaction.from(Buffer.from(prepJson.transaction_base64, "base64"));
  summary.instructionCount = tx.instructions.length;
  summary.instructionPrograms = tx.instructions.map((ix) => ix.programId.toBase58());
  summary.feePayer = tx.feePayer ? tx.feePayer.toBase58() : null;
  summary.predictedAsset = prepJson.stamped_asset_address || null;
  summary.ticket = prepJson.ticket_address || null;

  const sim = await connection.simulateTransaction(tx, {
    sigVerify: false,
    replaceRecentBlockhash: true,
    commitment: "confirmed",
  });
  summary.simulationError = sim.value.err;
  summary.unitsConsumed = sim.value.unitsConsumed || null;
  summary.simulationLogs = (sim.value.logs || []).slice(-50);
  fs.writeFileSync(out, JSON.stringify(summary, null, 2));
  console.log(JSON.stringify({
    prepareHttp: summary.prepareHttp,
    prepareSuccess: summary.prepareSuccess,
    walletLamports: summary.walletLamports,
    profileExists: summary.profileExists,
    vaultExists: summary.vaultExists,
    profileRamBalanceGuess: summary.profileRamBalanceGuess,
    vaultKnownCountGuess: summary.vaultKnownCountGuess,
    instructionCount: summary.instructionCount,
    simulationError: summary.simulationError,
    logTail: summary.simulationLogs,
    evidence: out,
  }, null, 2));
})().catch((e) => {
  console.error(String((e && e.message) || e));
  process.exit(1);
});
