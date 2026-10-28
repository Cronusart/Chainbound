// Devnet smoke test for the vault's inner instructions. Simulation only, with signature checks off
// so the vault can be listed as a signer without signing; nothing is sent.
//
//   node tools/vrf-efficacy/squads-signer/simulate.mjs
import { Connection, PublicKey, TransactionMessage, VersionedTransaction } from "@solana/web3.js";
import {
  KINDS,
  ODDS,
  VAULT,
  cumulative,
  rollOddsPda,
  setUpgradeAuthorityInstruction,
  updateOddsInstruction,
} from "./src/instructions.mjs";

const connection = new Connection("https://api.devnet.solana.com", "confirmed");
const FEE_PAYER = new PublicKey("AxrH7pwxL1kdfMDHBRTt6YhuNCWpcNkqjJJ633gVoCLu");
const DKFM = new PublicKey("DkFmzzzF89tYZu2JWR1uq3H13U5np9NU6MHF4E61gJmG");
const INVALID_AUTHORITY = 6012;

async function simulate(label, instructions, expectCustom = null) {
  const { blockhash } = await connection.getLatestBlockhash("confirmed");
  const message = new TransactionMessage({ payerKey: FEE_PAYER, recentBlockhash: blockhash, instructions }).compileToV0Message();
  const sim = await connection.simulateTransaction(new VersionedTransaction(message), { sigVerify: false, replaceRecentBlockhash: true });
  const err = sim.value.err;
  const custom = err && err.InstructionError && err.InstructionError[1] && err.InstructionError[1].Custom;
  const pass = expectCustom === null ? err === null : custom === expectCustom;
  console.log(`${pass ? "PASS" : "FAIL"}  ${label}: err=${JSON.stringify(err)} units=${sim.value.unitsConsumed}`);
  return pass;
}

const odds = await connection.getAccountInfo(rollOddsPda(KINDS.known), "confirmed");
const poolHash = Buffer.from(odds.data.subarray(37, 69));
const ultra = cumulative(ODDS.ultra);

const results = [
  await simulate("vault updates Known odds (Ultra, same pool)", [updateOddsInstruction(KINDS.known, ultra, poolHash)]),
  await simulate("DkFm can no longer update odds", [updateOddsInstruction(KINDS.known, ultra, poolHash, DKFM)], INVALID_AUTHORITY),
  await simulate("DkFm hands the upgrade authority to the vault (the CLI step)", [setUpgradeAuthorityInstruction(VAULT, DKFM)]),
  await simulate("...then the vault can hand it back (the recovery proposal)", [
    setUpgradeAuthorityInstruction(VAULT, DKFM),
    setUpgradeAuthorityInstruction(DKFM, VAULT),
  ]),
];
if (results.some((r) => !r)) process.exitCode = 1;
