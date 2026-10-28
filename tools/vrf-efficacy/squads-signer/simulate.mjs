// Devnet smoke test for the vault's inner instructions, against the current state: the vault holds
// both GameConfig.program_authority and the upgrade authority. Simulation only, with signature
// checks off so the vault can be listed as a signer without signing; nothing is sent.
//
//   node tools/vrf-efficacy/squads-signer/simulate.mjs
import { Connection, PublicKey, TransactionMessage, VersionedTransaction } from "@solana/web3.js";
import {
  KINDS,
  ODDS,
  VAULT,
  cumulative,
  extendCheckedInstruction,
  extendInstruction,
  rollOddsPda,
  setUpgradeAuthorityInstruction,
  updateOddsInstruction,
} from "./src/instructions.mjs";

const connection = new Connection("https://api.devnet.solana.com", "confirmed");
const FEE_PAYER = new PublicKey("AxrH7pwxL1kdfMDHBRTt6YhuNCWpcNkqjJJ633gVoCLu");
const DKFM = new PublicKey("DkFmzzzF89tYZu2JWR1uq3H13U5np9NU6MHF4E61gJmG");
const INVALID_AUTHORITY = 6012;
const MIN_EXTEND = 10_240;
const ANY_ERROR = "any error";

async function simulate(label, instructions, expect = null) {
  const { blockhash } = await connection.getLatestBlockhash("confirmed");
  const message = new TransactionMessage({ payerKey: FEE_PAYER, recentBlockhash: blockhash, instructions }).compileToV0Message();
  const sim = await connection.simulateTransaction(new VersionedTransaction(message), { sigVerify: false, replaceRecentBlockhash: true });
  const err = sim.value.err;
  const custom = err && err.InstructionError && err.InstructionError[1] && err.InstructionError[1].Custom;
  const pass = expect === null ? err === null : expect === ANY_ERROR ? err !== null : custom === expect;
  console.log(`${pass ? "PASS" : "FAIL"}  ${label}: err=${JSON.stringify(err)} units=${sim.value.unitsConsumed}`);
  return pass;
}

const odds = await connection.getAccountInfo(rollOddsPda(KINDS.known), "confirmed");
const poolHash = Buffer.from(odds.data.subarray(37, 69));
const ultra = cumulative(ODDS.ultra);

const results = [
  await simulate("vault updates Known odds (Ultra, same pool)", [updateOddsInstruction(KINDS.known, ultra, poolHash)]),
  await simulate("DkFm can no longer update odds", [updateOddsInstruction(KINDS.known, ultra, poolHash, DKFM)], INVALID_AUTHORITY),
  await simulate("vault can hand the upgrade authority back (recovery proposal)", [setUpgradeAuthorityInstruction(DKFM, VAULT)]),
  await simulate("DkFm can no longer move the upgrade authority", [setUpgradeAuthorityInstruction(DKFM, DKFM)], ANY_ERROR),
  await simulate(`ExtendProgram by any payer (${MIN_EXTEND} bytes, no authority)`, [extendInstruction(MIN_EXTEND, FEE_PAYER)]),
  // While gate 2oMRZEDWT2tqtYMofhmmfQ8SsjqUFzT6sYXppQDavxwz is inactive this is rejected; when it
  // starts passing, extend must move to a vault proposal.
  await simulate("ExtendProgramChecked still inactive", [extendCheckedInstruction(MIN_EXTEND, FEE_PAYER)], ANY_ERROR),
];
if (results.some((r) => !r)) process.exitCode = 1;
