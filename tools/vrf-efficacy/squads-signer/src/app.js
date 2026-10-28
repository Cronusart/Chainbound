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
  rollOddsPda,
  setUpgradeAuthorityInstruction,
  updateOddsInstruction,
  upgradeInstruction,
} from "./instructions.mjs";

const RPC = "https://api.devnet.solana.com";
const DEVNET_GENESIS = "EtWTRABZaYq6iMfeYKouRu166VU2xqa1wcaWoxPkrZBG";
const CHAIN = "solana:devnet";
const DEFAULT_RECOVERY = "DkFmzzzF89tYZu2JWR1uq3H13U5np9NU6MHF4E61gJmG";
const BUFFER_HEADER_LEN = 37;

const connection = new Connection(RPC, "confirmed");

const $ = (id) => document.getElementById(id);
const state = { wallet: null, account: null, member: null, chain: null, shown: null };

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

async function readUpgradeAuthority() {
  const info = await connection.getAccountInfo(PROGRAMDATA, "confirmed");
  // ProgramData: enum(4) slot(8) Option<Pubkey>(1 + 32)
  return info && info.data[12] === 1 ? new PublicKey(info.data.subarray(13, 45)) : null;
}

async function readOdds(kind) {
  const info = await connection.getAccountInfo(rollOddsPda(kind), "confirmed");
  if (!info) return null;
  const d = info.data;
  return {
    oddsVersion: d.readUInt32LE(9),
    cumulativePpm: [0, 1, 2, 3, 4, 5].map((i) => d.readUInt32LE(13 + 4 * i)),
    poolHash: hex(d.subarray(37, 69)),
  };
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
  return {
    programAuthority: new PublicKey(config.data.subarray(8, 40)),
    upgradeAuthority: await readUpgradeAuthority(),
    transfer: transferInfo && {
      current: new PublicKey(transferInfo.data.subarray(8, 40)),
      pending: new PublicKey(transferInfo.data.subarray(40, 72)),
    },
    odds: { known: await readOdds(KINDS.known), stamp: await readOdds(KINDS.stamp) },
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
  const out = { index, title: "", details: [], checks: [], ok: false, status: "none", approved: [], rejected: [] };
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
    const weights = ODDS[$("odds-preset").value];
    const poolHex = ($("odds-pool").value || current.poolHash).trim();
    if (!/^[0-9a-fA-F]{64}$/.test(poolHex)) throw new Error("pool hash must be 64 hex characters");
    return {
      instruction: updateOddsInstruction(KINDS[kindName], cumulative(weights), Buffer.from(poolHex, "hex")),
      memo: `chainbound: update_roll_odds ${kindName} ${$("odds-preset").value}`,
      summary: `${kindName} odds to ${$("odds-preset").value}`,
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
  if (!$("odds-pool").value && chain.odds[$("odds-kind").value]) $("odds-pool").placeholder = chain.odds[$("odds-kind").value].poolHash;

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
  const { instruction, memo, summary } = await buildFromForm();
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
}

function guard(fn) {
  return async () => {
    for (const id of ["create", "approve", "reject", "execute"]) $(id).disabled = true;
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
$("approve").onclick = guard(() => vote("approve"));
$("reject").onclick = guard(() => vote("reject"));
$("execute").onclick = guard(execute);
$("refresh").onclick = () => render();
$("type").onchange = showForm;
$("odds-kind").onchange = () => render();
$("recovery-target").placeholder = DEFAULT_RECOVERY;
listWallets();
render();
