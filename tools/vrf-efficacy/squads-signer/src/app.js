// Devnet-only signing page for the Squads vault that holds Chainbound's authorities
// (MAINNET_VRF_READINESS_PLAN.md section 1). It builds four kinds of vault transaction, each with
// exactly one inner instruction:
//   - accept_program_authority (take GameConfig.program_authority)
//   - update_roll_odds (change published odds)
//   - BPF loader Upgrade (deploy a buffer that the vault owns)
//   - BPF loader SetAuthority (hand the upgrade authority to another key: the recovery exit)
// Approvers see the stored instruction decoded from chain before they sign. The page refuses any
// cluster but Devnet. Wallets only sign; the page sends through a Devnet RPC itself.
import * as multisig from "@sqds/multisig";
import {
  Connection,
  PublicKey,
  SYSVAR_CLOCK_PUBKEY,
  SYSVAR_RENT_PUBKEY,
  TransactionMessage,
  VersionedTransaction,
} from "@solana/web3.js";
import { getWallets } from "@wallet-standard/app";
import {
  createDefaultAuthorizationCache,
  createDefaultChainSelector,
  createDefaultWalletNotFoundHandler,
  registerMwa,
} from "@solana-mobile/wallet-standard-mobile";
import {
  ACCEPT_HEX,
  AUTHORITY_TRANSFER,
  DEFAULT_POOL_HASH,
  GAME_CONFIG,
  KINDS,
  LOADER,
  LOADER_SET_AUTHORITY_HEX,
  LOADER_UPGRADE_HEX,
  MULTISIG,
  ODDS,
  PROGRAMDATA,
  PROGRAM_ID,
  TIERS,
  UPDATE_ODDS_HEX,
  VAULT,
  acceptInstruction,
  cumulative,
  decodeOddsData,
  readOdds,
  rollOddsPda,
  setOddsIx,
  setUpgradeAuthorityInstruction,
  upgradeInstruction,
  weightsFromCumulative,
} from "./instructions.mjs";

const RPC = "https://api.devnet.solana.com";
const DEVNET_GENESIS = "EtWTRABZaYq6iMfeYKouRu166VU2xqa1wcaWoxPkrZBG";
const CHAIN = "solana:devnet";
const DEFAULT_RECOVERY = "DkFmzzzF89tYZu2JWR1uq3H13U5np9NU6MHF4E61gJmG";
const BUFFER_HEADER_LEN = 37;

const connection = new Connection(RPC, "confirmed");

const $ = (id) => document.getElementById(id);
const state = { wallet: null, account: null, member: null, chain: null, shown: null, oddsSimulation: null };

function log(text, kind = "") {
  const line = document.createElement("div");
  line.className = `log ${kind}`;
  line.textContent = `${new Date().toLocaleTimeString()}  ${text}`;
  $("log").prepend(line);
}

const short = (key) => {
  const s = key.toBase58 ? key.toBase58() : String(key);
  return `${s.slice(0, 6)}…${s.slice(-4)}`;
};
const hex = (bytes) => Buffer.from(bytes).toString("hex");

async function sha256Hex(bytes) {
  return hex(new Uint8Array(await crypto.subtle.digest("SHA-256", bytes)));
}

// ---- chain reads

const PROGRAMDATA_HEADER_LEN = 45;

// ProgramData: enum(4) slot(8) Option<Pubkey>(1 + 32), then room for the program bytes.
async function readProgramData() {
  const info = await connection.getAccountInfo(PROGRAMDATA, "confirmed");
  return {
    authority: info && info.data[12] === 1 ? new PublicKey(info.data.subarray(13, PROGRAMDATA_HEADER_LEN)) : null,
    capacity: info ? info.data.length - PROGRAMDATA_HEADER_LEN : 0,
  };
}

// Extend is permissionless while the ExtendProgramChecked feature gate is inactive (checked
// 2026-10-27 on Devnet and Mainnet), so it runs from the CLI rather than as a vault proposal.
function extendHint(needed, capacity) {
  const extra = Math.max(10_240, needed - capacity);
  return `solana program extend ${PROGRAM_ID.toBase58()} ${extra} --url devnet --keypair <any funded payer>`;
}

// Buffer: enum(4) Option<Pubkey>(1 + 32) then the program bytes.
async function readBuffer(address) {
  const info = await connection.getAccountInfo(address, "confirmed");
  if (!info || !info.owner.equals(LOADER) || info.data.readUInt32LE(0) !== 1) return null;
  const authority = info.data[4] === 1 ? new PublicKey(info.data.subarray(5, 37)) : null;
  const program = info.data.subarray(BUFFER_HEADER_LEN);
  return { authority, length: program.length, sha256: await sha256Hex(program) };
}

async function readChain() {
  const genesis = await connection.getGenesisHash();
  if (genesis !== DEVNET_GENESIS) throw new Error(`RPC is not Devnet (genesis ${genesis})`);
  const config = await connection.getAccountInfo(GAME_CONFIG, "confirmed");
  const transferInfo = await connection.getAccountInfo(AUTHORITY_TRANSFER, "confirmed");
  const ms = await multisig.accounts.Multisig.fromAccountAddress(connection, MULTISIG);
  const programData = await readProgramData();
  return {
    programAuthority: new PublicKey(config.data.subarray(8, 40)),
    upgradeAuthority: programData.authority,
    capacity: programData.capacity,
    transfer: transferInfo && {
      current: new PublicKey(transferInfo.data.subarray(8, 40)),
      pending: new PublicKey(transferInfo.data.subarray(40, 72)),
    },
    odds: { known: await readOdds(connection, KINDS.known), stamp: await readOdds(connection, KINDS.stamp) },
    ms,
    latestIndex: BigInt(ms.transactionIndex.toString()),
  };
}

// ---- decoding a stored vault transaction, so approvers check what is on chain

const keysEqual = (actual, expected) =>
  actual.length === expected.length && actual.every((k, i) => k.equals(expected[i]));

async function decodeInner(ix, ixKeys, programId, transfer) {
  const data = Buffer.from(ix.data);
  const dataHex = hex(data);
  if (programId.equals(PROGRAM_ID) && dataHex === ACCEPT_HEX) {
    // After execution the transfer PDA is closed, so the old authority can only be checked while pending.
    const head = keysEqual(ixKeys.slice(0, 3), [AUTHORITY_TRANSFER, GAME_CONFIG, VAULT]);
    return {
      title: "accept_program_authority: the vault takes GameConfig.program_authority",
      details: [`previous authority: ${ixKeys[3] ? ixKeys[3].toBase58() : "?"}`],
      checks: [["accounts are transfer, config, vault, previous authority",
        ixKeys.length === 4 && head && (!transfer || ixKeys[3].equals(transfer.current))]],
    };
  }
  if (programId.equals(PROGRAM_ID) && dataHex.startsWith(UPDATE_ODDS_HEX) && data.length === 8 + 24 + 32) {
    const cum = [0, 1, 2, 3, 4, 5].map((i) => data.readUInt32LE(8 + 4 * i));
    const kindName = Object.keys(KINDS).find((k) => ixKeys[0] && ixKeys[0].equals(rollOddsPda(KINDS[k])));
    const valid = cum.every((t, i) => t > (i ? cum[i - 1] : 0)) && cum[5] === 1_000_000;
    const weights = cum.map((t, i) => t - (i ? cum[i - 1] : 0));
    return {
      title: `update_roll_odds: ${kindName ?? "unknown kind"}`,
      kindName,
      targetOdds: kindName ? {
        cumulativePpm: cum,
        poolHash: hex(data.subarray(32, 64)),
      } : null,
      details: [
        ...weights.map((w, i) => `${TIERS[i].padEnd(10)} ${(w / 10_000).toFixed(2)}%  (${w} ppm)`),
        `pool hash: ${hex(data.subarray(32, 64))}`,
      ],
      checks: [
        ["roll odds account is Known or Stamp", Boolean(kindName)],
        ["thresholds strictly increase and end at 1,000,000", valid],
        ["accounts are roll odds, config, vault", ixKeys.length === 3 && ixKeys[1].equals(GAME_CONFIG) && ixKeys[2].equals(VAULT)],
      ],
    };
  }
  if (programId.equals(LOADER) && dataHex === LOADER_UPGRADE_HEX) {
    const buffer = ixKeys[2] ? await readBuffer(ixKeys[2]) : null;
    return {
      title: "Upgrade: replace the Chainbound program with a buffer",
      details: [
        `buffer: ${ixKeys[2] ? ixKeys[2].toBase58() : "?"}`,
        buffer ? `buffer program bytes: ${buffer.length}, sha256 ${buffer.sha256}` : "buffer: already used or not found",
        buffer ? `buffer authority: ${buffer.authority ? buffer.authority.toBase58() : "none"}` : "",
        `spill (gets the buffer's rent back): ${ixKeys[3] ? ixKeys[3].toBase58() : "?"}`,
        "compare the sha256 with the build before approving",
      ].filter(Boolean),
      checks: [
        ["accounts are programdata, program, buffer, spill, rent, clock, vault",
          ixKeys.length === 7 && keysEqual([ixKeys[0], ixKeys[1]], [PROGRAMDATA, PROGRAM_ID]) &&
          keysEqual(ixKeys.slice(4), [SYSVAR_RENT_PUBKEY, SYSVAR_CLOCK_PUBKEY, VAULT])],
        // An executed upgrade closes its buffer, so the fit can only be checked while it exists.
        ...(buffer ? [[`buffer fits the program (${buffer.length} of ${state.chain.capacity} bytes)`,
          buffer.length <= state.chain.capacity]] : []),
      ],
    };
  }
  if (programId.equals(LOADER) && dataHex === LOADER_SET_AUTHORITY_HEX) {
    return {
      title: "SetAuthority: hand the upgrade authority to another key",
      details: [`new upgrade authority: ${ixKeys[2] ? ixKeys[2].toBase58() : "none (program becomes immutable!)"}`],
      checks: [
        ["accounts are programdata, vault, new authority",
          ixKeys.length === 3 && keysEqual([ixKeys[0], ixKeys[1]], [PROGRAMDATA, VAULT])],
      ],
    };
  }
  return { title: "unrecognized instruction", details: [`program ${programId.toBase58()}, data ${dataHex}`], checks: [["recognized instruction", false]] };
}

async function readProposal(index, transfer) {
  const [txPda] = multisig.getTransactionPda({ multisigPda: MULTISIG, index });
  const [proposalPda] = multisig.getProposalPda({ multisigPda: MULTISIG, transactionIndex: index });
  const out = {
    index, title: "", details: [], checks: [], ok: false, status: "none", approved: [], rejected: [],
    kindName: null, targetOdds: null,
  };
  try {
    const vtx = await multisig.accounts.VaultTransaction.fromAccountAddress(connection, txPda);
    const keys = vtx.message.accountKeys;
    const ixs = vtx.message.instructions;
    const ix = ixs[0];
    const ixKeys = ix ? Array.from(ix.accountIndexes).map((i) => keys[i]) : [];
    const decoded = ix
      ? await decodeInner(ix, ixKeys, keys[ix.programIdIndex], transfer)
      : { title: "empty", details: [], checks: [] };
    out.title = decoded.title;
    out.details = decoded.details;
    out.kindName = decoded.kindName || null;
    out.targetOdds = decoded.targetOdds || null;
    out.checks = [["exactly one instruction", ixs.length === 1], ...decoded.checks, ["vault index 0", vtx.vaultIndex === 0]];
    out.ok = out.checks.every(([, pass]) => pass);
  } catch {
    out.checks = [["vault transaction exists", false]];
  }
  try {
    const proposal = await multisig.accounts.Proposal.fromAccountAddress(connection, proposalPda);
    out.status = proposal.status.__kind;
    out.approved = proposal.approved.map((k) => k.toBase58());
    out.rejected = proposal.rejected.map((k) => k.toBase58());
  } catch {
    out.status = "no proposal";
  }
  return out;
}

// Validates the form for the chosen proposal type and returns { instruction, memo, summary }.
async function buildFromForm() {
  const { transfer, programAuthority, upgradeAuthority, odds } = state.chain;
  const type = $("type").value;
  if (type === "accept") {
    if (!transfer || !transfer.pending.equals(VAULT)) throw new Error("no pending handoff to the vault");
    return { instruction: acceptInstruction(transfer.current), memo: "chainbound: accept_program_authority", summary: "accept program authority" };
  }
  if (type === "odds") {
    if (!programAuthority.equals(VAULT)) throw new Error("the vault is not GameConfig.program_authority");
    const kindName = $("odds-kind").value;
    const current = odds[kindName];
    if (!current) throw new Error(`${kindName} odds are not initialized; initialize_roll_odds is not offered here`);
    const preset = $("odds-preset").value;
    const weights = ODDS[preset];
    if (!weights) throw new Error(`unsupported odds preset ${preset}`);
    const cumulativePpm = cumulative(weights);
    const poolHex = ($("odds-pool").value || DEFAULT_POOL_HASH).trim();
    if (!/^[0-9a-fA-F]{64}$/.test(poolHex)) throw new Error("pool hash must be 64 hex characters");
    const instruction = setOddsIx(VAULT, KINDS[kindName], cumulativePpm, Buffer.from(poolHex, "hex"), true);
    return {
      instruction,
      memo: `chainbound: update_roll_odds ${kindName} ${preset}`,
      summary: `${kindName} odds to ${preset}`,
      oddsTarget: { kindName, preset, weights, cumulativePpm, poolHash: poolHex.toLowerCase() },
    };
  }
  if (type === "upgrade") {
    if (!upgradeAuthority || !upgradeAuthority.equals(VAULT)) throw new Error("the vault is not the upgrade authority yet");
    const buffer = new PublicKey($("upgrade-buffer").value.trim());
    const info = await readBuffer(buffer);
    if (!info) throw new Error("that address is not an upgradeable-loader buffer");
    if (!info.authority || !info.authority.equals(VAULT)) {
      throw new Error(`buffer authority is ${info.authority ? info.authority.toBase58() : "none"}; run set-buffer-authority to the vault first`);
    }
    if (info.length > state.chain.capacity) {
      throw new Error(`buffer is ${info.length} bytes but the program has room for ${state.chain.capacity}; extend first: ${extendHint(info.length, state.chain.capacity)}`);
    }
    const spill = new PublicKey(($("upgrade-spill").value || state.account.address).trim());
    log(`buffer ${buffer.toBase58()}: ${info.length} bytes, sha256 ${info.sha256}`);
    return { instruction: upgradeInstruction(buffer, spill), memo: "chainbound: program upgrade", summary: `upgrade from ${short(buffer)}` };
  }
  if (type === "recovery") {
    if (!upgradeAuthority || !upgradeAuthority.equals(VAULT)) throw new Error("the vault is not the upgrade authority");
    const target = new PublicKey(($("recovery-target").value || DEFAULT_RECOVERY).trim());
    if (target.equals(VAULT)) throw new Error("the target is already the vault");
    return { instruction: setUpgradeAuthorityInstruction(target), memo: "chainbound: upgrade authority recovery", summary: `upgrade authority to ${short(target)}` };
  }
  throw new Error(`unknown proposal type ${type}`);
}

// ---- rendering

function row(parent, text, cls = "") {
  const div = document.createElement("div");
  if (cls) div.className = cls;
  div.textContent = text;
  parent.append(div);
}

function formatOddsResult(oddsByKind) {
  return Object.entries(oddsByKind).map(([kindName, odds]) => {
    if (!odds) return `${kindName}: not initialized`;
    const weights = weightsFromCumulative(odds.cumulativePpm);
    return [
      `${kindName}:`,
      `  odds_version ${odds.oddsVersion}`,
      `  ppm ${weights.join("/")}`,
      `  cumulative_ppm ${odds.cumulativePpm.join("/")}`,
      `  pool_hash ${odds.poolHash}`,
    ].join("\n");
  }).join("\n");
}

function renderOddsSimulation() {
  const box = $("odds-simulation");
  const simulation = state.oddsSimulation;
  if (!simulation) {
    box.className = "detail";
    box.textContent = "Not simulated yet. Creation always repeats this simulation before opening the wallet.";
    return;
  }
  const { target, err, unitsConsumed, logs } = simulation;
  box.className = err ? "detail fail" : "detail pass";
  box.textContent = [
    `${err ? "FAIL" : "PASS"} vault-signer simulation: err=${JSON.stringify(err)} units=${unitsConsumed}`,
    `${target.kindName} ${target.preset}: odds_version ${target.nextOddsVersion}`,
    `ppm ${target.weights.join("/")}`,
    `pool_hash ${target.poolHash}`,
    ...(logs || []).filter((line) => /Instruction|Error|failed|consumed/.test(line)).slice(-5),
  ].join("\n");
}

async function render() {
  $("vault").textContent = VAULT.toBase58();
  $("multisig").textContent = MULTISIG.toBase58();
  let chain;
  try {
    chain = await readChain();
  } catch (error) {
    $("status").textContent = `Can't read Devnet: ${error.message}`;
    return;
  }
  state.chain = chain;
  const { programAuthority, upgradeAuthority, transfer, ms, latestIndex } = chain;
  const mark = (key) => (key && key.equals(VAULT) ? "  ✅ the vault" : "");
  $("authority").textContent = `${programAuthority.toBase58()}${mark(programAuthority)}`;
  $("upgrade-authority").textContent = upgradeAuthority ? `${upgradeAuthority.toBase58()}${mark(upgradeAuthority)}` : "none (immutable)";
  $("pending").textContent = transfer
    ? `${transfer.pending.toBase58()}${transfer.pending.equals(VAULT) ? "  (the vault)" : "  ⚠ NOT the vault"}`
    : "none";
  $("threshold").textContent = `${ms.threshold} of ${ms.members.length}`;
  $("odds-result").textContent = formatOddsResult(chain.odds);
  renderOddsSimulation();

  const memberKeys = ms.members.map((m) => m.key.toBase58());
  const me = state.account ? state.account.address : null;
  state.member = me && memberKeys.includes(me) ? me : null;
  $("who").textContent = me ? `${me}${state.member ? "  (member)" : "  ⚠ not a member of this multisig"}` : "not connected";

  const wanted = $("index").value ? BigInt($("index").value) : latestIndex;
  const shown = wanted > 0n && wanted <= latestIndex ? await readProposal(wanted, transfer) : null;
  state.shown = shown;
  const box = $("proposal");
  box.innerHTML = "";
  if (shown) {
    row(box, `Multisig transaction #${shown.index}${shown.index === latestIndex ? " (latest)" : ""}: ${shown.status}, ` +
      `approvals ${shown.approved.length} of ${ms.threshold}${shown.rejected.length ? `, rejections ${shown.rejected.length}` : ""}`);
    row(box, shown.title, "title");
    for (const d of shown.details) row(box, d, "detail");
    for (const [label, pass] of shown.checks) row(box, `${pass ? "✔" : "✘"} ${label}`, pass ? "pass" : "fail");
    for (const k of shown.approved) row(box, `approved by ${short(k)}`);
    for (const k of shown.rejected) row(box, `rejected by ${short(k)}`);
  } else {
    box.textContent = latestIndex > 0n ? "No transaction with that number." : "No multisig transactions yet.";
  }

  const active = shown && shown.status === "Active";
  const approved = shown && shown.ok && shown.status === "Approved";
  const voted = shown && me && (shown.approved.includes(me) || shown.rejected.includes(me));
  const lastApproval = active && shown.ok && !voted && shown.approved.length + 1 >= ms.threshold;
  $("create").disabled = !state.member;
  $("simulate-odds").disabled = !(state.member && $("type").value === "odds");
  $("approve").disabled = !(state.member && active && shown.ok && !voted);
  $("reject").disabled = !(state.member && active && !voted);
  $("execute").disabled = !(state.member && (approved || lastApproval));
  $("execute").textContent = lastApproval ? "Approve and execute" : "Execute";
  $("status").textContent = shown && active && !shown.ok
    ? "The shown proposal failed a check: reject it, don't approve."
    : "Ready.";
  showForm();
}

function showForm() {
  for (const t of ["accept", "odds", "upgrade", "recovery"]) $(`form-${t}`).hidden = $("type").value !== t;
}

// ---- wallets and sending

async function connectWallet(wallet) {
  const { accounts } = await wallet.features["standard:connect"].connect();
  const account = accounts.find((a) => a.chains.includes(CHAIN)) || accounts[0];
  if (!account) throw new Error("wallet returned no account");
  state.wallet = wallet;
  state.account = account;
  log(`connected ${wallet.name}: ${account.address}`);
  await render();
}

function listWallets() {
  const { get, on } = getWallets();
  const draw = () => {
    const list = $("wallets");
    list.innerHTML = "";
    for (const w of get().filter((w) => w.features["standard:connect"] &&
      (w.features["solana:signTransaction"] || w.features["solana:signAndSendTransaction"]))) {
      const b = document.createElement("button");
      b.textContent = `Connect ${w.name}`;
      b.onclick = () => connectWallet(w).catch((e) => log(`connect failed: ${e.message}`, "fail"));
      list.append(b);
    }
    if (!list.children.length) list.textContent = "No Solana wallet found in this browser.";
  };
  on("register", draw);
  draw();
}

function bs58(bytes) {
  const A = "123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz";
  let n = BigInt("0x" + hex(bytes));
  let s = "";
  while (n > 0n) { s = A[Number(n % 58n)] + s; n /= 58n; }
  for (const b of bytes) { if (b === 0) s = "1" + s; else break; }
  return s;
}

function decodeSimulatedAccount(account) {
  if (!account) return null;
  const data = Array.isArray(account.data) ? Buffer.from(account.data[0], "base64") : Buffer.from(account.data);
  return decodeOddsData(data);
}

// Simulates the actual program instruction before a Squads proposal is created. Signature checks
// are disabled so the vault is represented as the required signer without any wallet signature.
async function simulateSetOdds(built = null) {
  const proposal = built || await buildFromForm();
  if (!proposal.oddsTarget) throw new Error("select Set odds before running the vault simulation");
  const payer = new PublicKey(state.account.address);
  const { blockhash } = await connection.getLatestBlockhash("confirmed");
  const message = new TransactionMessage({
    payerKey: payer,
    recentBlockhash: blockhash,
    instructions: [proposal.instruction],
  }).compileToV0Message();
  const address = rollOddsPda(KINDS[proposal.oddsTarget.kindName]).toBase58();
  const sim = await connection.simulateTransaction(new VersionedTransaction(message), {
    sigVerify: false,
    replaceRecentBlockhash: true,
    accounts: { encoding: "base64", addresses: [address] },
  });
  const simulatedOdds = decodeSimulatedAccount(sim.value.accounts && sim.value.accounts[0]);
  const current = state.chain.odds[proposal.oddsTarget.kindName];
  const target = {
    ...proposal.oddsTarget,
    nextOddsVersion: simulatedOdds ? simulatedOdds.oddsVersion : current.oddsVersion + 1,
  };
  let err = sim.value.err;
  if (!err && !simulatedOdds) err = "simulation did not return the roll-odds account";
  if (!err && (simulatedOdds.poolHash !== target.poolHash ||
      simulatedOdds.cumulativePpm.join(",") !== target.cumulativePpm.join(","))) {
    err = "simulated account does not match the requested odds/hash";
  }
  state.oddsSimulation = {
    err,
    unitsConsumed: sim.value.unitsConsumed,
    logs: sim.value.logs || [],
    simulatedOdds,
    target,
  };
  renderOddsSimulation();
  if (err) throw new Error(`vault set_odds simulation failed: ${JSON.stringify(err)}`);
  log(`vault set_odds simulation passed for ${target.kindName}: odds_version ${target.nextOddsVersion}, ppm ${target.weights.join("/")}`, "pass");
  return proposal;
}

// Simulates, has the connected wallet sign, and sends through the Devnet RPC, so the wallet's own
// network setting can't redirect the transaction.
async function signAndSend(instructions, label) {
  const payer = new PublicKey(state.account.address);
  const { blockhash, lastValidBlockHeight } = await connection.getLatestBlockhash("confirmed");
  const message = new TransactionMessage({ payerKey: payer, recentBlockhash: blockhash, instructions }).compileToV0Message();
  const tx = new VersionedTransaction(message);
  const sim = await connection.simulateTransaction(tx, { sigVerify: false, replaceRecentBlockhash: true });
  if (sim.value.err) throw new Error(`${label} simulation failed: ${JSON.stringify(sim.value.err)} ${(sim.value.logs || []).slice(-3).join(" | ")}`);
  log(`${label}: simulation ok, asking the wallet to sign…`);

  const features = state.wallet.features;
  let signature;
  if (features["solana:signTransaction"]) {
    const [{ signedTransaction }] = await features["solana:signTransaction"].signTransaction({
      account: state.account, chain: CHAIN, transaction: tx.serialize(),
    });
    signature = await connection.sendRawTransaction(signedTransaction, { preflightCommitment: "confirmed" });
  } else {
    const [{ signature: raw }] = await features["solana:signAndSendTransaction"].signAndSendTransaction({
      account: state.account, chain: CHAIN, transaction: tx.serialize(),
    });
    signature = bs58(raw);
  }
  const conf = await connection.confirmTransaction({ signature, blockhash, lastValidBlockHeight }, "confirmed");
  if (conf.value.err) throw new Error(`${label} failed on chain: ${signature} ${JSON.stringify(conf.value.err)}`);
  log(`${label}: ${signature}`, "pass");
  return signature;
}

// ---- actions

async function createProposal() {
  let built = await buildFromForm();
  if (built.oddsTarget) built = await simulateSetOdds(built);
  const { instruction, memo, summary } = built;
  const member = new PublicKey(state.account.address);
  const index = state.chain.latestIndex + 1n;
  const inner = new TransactionMessage({ payerKey: VAULT, recentBlockhash: PublicKey.default.toBase58(), instructions: [instruction] });
  await signAndSend([
    multisig.instructions.vaultTransactionCreate({
      multisigPda: MULTISIG, transactionIndex: index, creator: member, rentPayer: member,
      vaultIndex: 0, ephemeralSigners: 0, transactionMessage: inner, memo,
    }),
    multisig.instructions.proposalCreate({ multisigPda: MULTISIG, transactionIndex: index, creator: member, rentPayer: member }),
    multisig.instructions.proposalApprove({ multisigPda: MULTISIG, transactionIndex: index, member }),
  ], `create + approve #${index} (${summary})`);
  $("index").value = "";
}

async function vote(kind) {
  const member = new PublicKey(state.account.address);
  const index = state.shown.index;
  const ix = kind === "approve"
    ? multisig.instructions.proposalApprove({ multisigPda: MULTISIG, transactionIndex: index, member })
    : multisig.instructions.proposalReject({ multisigPda: MULTISIG, transactionIndex: index, member });
  await signAndSend([ix], `${kind} #${index}`);
}

async function execute() {
  const member = new PublicKey(state.account.address);
  const shown = state.shown;
  const ixs = [];
  if (shown.status === "Active") {
    ixs.push(multisig.instructions.proposalApprove({ multisigPda: MULTISIG, transactionIndex: shown.index, member }));
  }
  const { instruction } = await multisig.instructions.vaultTransactionExecute({
    connection, multisigPda: MULTISIG, transactionIndex: shown.index, member,
  });
  ixs.push(instruction);
  await signAndSend(ixs, `execute #${shown.index}`);
  if (shown.kindName) {
    const odds = await readOdds(connection, KINDS[shown.kindName]);
    const weights = weightsFromCumulative(odds.cumulativePpm);
    log(`set_odds result ${shown.kindName}: odds_version ${odds.oddsVersion}, ppm ${weights.join("/")}, pool_hash ${odds.poolHash}`, "pass");
  }
}

function guard(fn) {
  return async () => {
    for (const id of ["create", "simulate-odds", "approve", "reject", "execute"]) $(id).disabled = true;
    try {
      await render();
      await fn();
    } catch (error) {
      log(error.message, "fail");
    }
    await render();
  };
}

registerMwa({
  appIdentity: { name: "Chainbound Devnet authority signer", uri: window.location.origin },
  authorizationCache: createDefaultAuthorizationCache(),
  chains: [CHAIN],
  chainSelector: createDefaultChainSelector(),
  onWalletNotFound: createDefaultWalletNotFoundHandler(),
});

$("create").onclick = guard(createProposal);
$("simulate-odds").onclick = guard(simulateSetOdds);
$("approve").onclick = guard(() => vote("approve"));
$("reject").onclick = guard(() => vote("reject"));
$("execute").onclick = guard(execute);
$("refresh").onclick = () => render();
$("type").onchange = () => {
  state.oddsSimulation = null;
  showForm();
  renderOddsSimulation();
  render().catch((error) => log(error.message, "fail"));
};
for (const id of ["odds-kind", "odds-preset", "odds-pool"]) {
  $(id).oninput = () => { state.oddsSimulation = null; renderOddsSimulation(); };
}
$("recovery-target").placeholder = DEFAULT_RECOVERY;
listWallets();
render();
