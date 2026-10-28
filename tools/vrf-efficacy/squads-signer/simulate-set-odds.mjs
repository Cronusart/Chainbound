// Devnet-only set_odds simulation for the Squads vault. This never loads a keypair, signs, or sends.
// Signature verification is disabled so the vault can be represented as the program authority.
//
//   node tools/vrf-efficacy/squads-signer/simulate-set-odds.mjs \
//     --kind known --odds ultra \
//     --pool-hash a73693d8f57706f53b00fc0148ebe6cb073501c6cf13326c7ddb136a4c787067
import { Connection, PublicKey, TransactionMessage, VersionedTransaction } from "@solana/web3.js";
import {
  DEFAULT_POOL_HASH,
  GAME_CONFIG,
  KINDS,
  ODDS,
  VAULT,
  cumulative,
  decodeOddsData,
  readOdds,
  rollOddsPda,
  setOddsIx,
  weightsFromCumulative,
} from "./src/instructions.mjs";

const RPC = "https://api.devnet.solana.com";
const DEVNET_GENESIS = "EtWTRABZaYq6iMfeYKouRu166VU2xqa1wcaWoxPkrZBG";
const FEE_PAYER = new PublicKey("AxrH7pwxL1kdfMDHBRTt6YhuNCWpcNkqjJJ633gVoCLu");

function argsFrom(argv) {
  const result = {};
  for (let index = 0; index < argv.length; index += 2) {
    const key = argv[index];
    const value = argv[index + 1];
    if (!key?.startsWith("--") || !value) throw new Error(`expected --name value, got ${key || "end of input"}`);
    result[key.slice(2)] = value;
  }
  return result;
}

function decodeSimulatedAccount(account) {
  if (!account) throw new Error("Devnet simulation did not return the roll-odds account");
  const data = Array.isArray(account.data) ? Buffer.from(account.data[0], "base64") : Buffer.from(account.data);
  return decodeOddsData(data);
}

const args = argsFrom(process.argv.slice(2));
const kindName = args.kind || "known";
const preset = args.odds || "ultra";
const kind = KINDS[kindName];
const weights = ODDS[preset];
const poolHashHex = (args["pool-hash"] || DEFAULT_POOL_HASH).toLowerCase();
if (!kind) throw new Error("--kind must be known or stamp");
if (preset !== "ultra" || !weights) throw new Error("--odds must be ultra");
if (!/^[0-9a-f]{64}$/.test(poolHashHex)) throw new Error("--pool-hash must be 64 hex characters");

const connection = new Connection(RPC, "confirmed");
const genesis = await connection.getGenesisHash();
if (genesis !== DEVNET_GENESIS) throw new Error(`RPC is not Devnet (genesis ${genesis})`);
console.log(`PASS Devnet genesis ${genesis}`);

const config = await connection.getAccountInfo(GAME_CONFIG, "confirmed");
if (!config) throw new Error(`GameConfig ${GAME_CONFIG.toBase58()} does not exist`);
const authority = new PublicKey(config.data.subarray(8, 40));
if (!authority.equals(VAULT)) {
  throw new Error(`GameConfig.program_authority is ${authority.toBase58()}, expected vault ${VAULT.toBase58()}`);
}
console.log(`PASS GameConfig.program_authority ${authority.toBase58()} (Squads vault)`);

const current = await readOdds(connection, kind);
if (!current) throw new Error(`${kindName} roll odds are not initialized`);
const cumulativePpm = cumulative(weights);
const ix = setOddsIx(VAULT, kind, cumulativePpm, Buffer.from(poolHashHex, "hex"), true);
const { blockhash } = await connection.getLatestBlockhash("confirmed");
const message = new TransactionMessage({
  payerKey: FEE_PAYER,
  recentBlockhash: blockhash,
  instructions: [ix],
}).compileToV0Message();
const sim = await connection.simulateTransaction(new VersionedTransaction(message), {
  sigVerify: false,
  replaceRecentBlockhash: true,
  accounts: { encoding: "base64", addresses: [rollOddsPda(kind).toBase58()] },
});
if (sim.value.err) {
  console.log(`FAIL set_odds ${kindName} ${preset}: err=${JSON.stringify(sim.value.err)} units=${sim.value.unitsConsumed}`);
  for (const line of (sim.value.logs || []).slice(-8)) console.log(`  ${line}`);
  process.exitCode = 1;
} else {
  const simulated = decodeSimulatedAccount(sim.value.accounts && sim.value.accounts[0]);
  const simulatedWeights = weightsFromCumulative(simulated.cumulativePpm);
  const matches = simulatedWeights.join(",") === weights.join(",") && simulated.poolHash === poolHashHex &&
    simulated.oddsVersion === current.oddsVersion + 1;
  console.log(`${matches ? "PASS" : "FAIL"} set_odds ${kindName} ${preset}: err=null units=${sim.value.unitsConsumed}`);
  console.log(`  vault signer ${VAULT.toBase58()}`);
  console.log(`  current odds_version ${current.oddsVersion}`);
  console.log(`  simulated odds_version ${simulated.oddsVersion}`);
  console.log(`  ppm ${simulatedWeights.join("/")}`);
  console.log(`  cumulative_ppm ${simulated.cumulativePpm.join("/")}`);
  console.log(`  pool_hash ${simulated.poolHash}`);
  if (!matches) process.exitCode = 1;
}
