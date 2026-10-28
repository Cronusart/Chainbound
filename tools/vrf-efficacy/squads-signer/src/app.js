// Devnet-only signing page for the program-authority handoff (MAINNET_VRF_READINESS_PLAN.md
// section 1). It builds exactly one inner instruction, accept_program_authority, and refuses to run
// on any cluster but Devnet. Wallets only sign; the page sends through a Devnet RPC itself.
import * as multisig from "@sqds/multisig";
import {
  Connection,
  PublicKey,
  TransactionInstruction,
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

const RPC = "https://api.devnet.solana.com";
const DEVNET_GENESIS = "EtWTRABZaYq6iMfeYKouRu166VU2xqa1wcaWoxPkrZBG";
const CHAIN = "solana:devnet";
const PROGRAM_ID = new PublicKey("9312ay7VrpvNYmyEx8TYCqhafbZ2Y5WGVbt6Qp772uUQ");
const MULTISIG = new PublicKey("9kcMg2WKwF2XdUBuKnpHnso4PRuVNPVgg1VuGtbjAv9m");
// sha256("global:accept_program_authority")[0..8]
const ACCEPT_DISCRIMINATOR_HEX = "e0c165c3f3220a6c";

const connection = new Connection(RPC, "confirmed");
const [VAULT] = multisig.getVaultPda({ multisigPda: MULTISIG, index: 0 });
const [GAME_CONFIG] = PublicKey.findProgramAddressSync([Buffer.from("game_config")], PROGRAM_ID);
const [AUTHORITY_TRANSFER] = PublicKey.findProgramAddressSync([Buffer.from("authority_transfer")], PROGRAM_ID);

const $ = (id) => document.getElementById(id);
const state = { wallet: null, account: null, member: null, chain: null };

function log(text, kind = "") {
  const line = document.createElement("div");
  line.className = `log ${kind}`;
  line.textContent = `${new Date().toLocaleTimeString()}  ${text}`;
  $("log").prepend(line);
}

function short(key) {
  const s = key.toBase58 ? key.toBase58() : String(key);
  return `${s.slice(0, 6)}…${s.slice(-4)}`;
}

async function readChain() {
  const genesis = await connection.getGenesisHash();
  if (genesis !== DEVNET_GENESIS) throw new Error(`RPC is not Devnet (genesis ${genesis})`);

  const config = await connection.getAccountInfo(GAME_CONFIG, "confirmed");
  const programAuthority = new PublicKey(config.data.subarray(8, 40));
  const transferInfo = await connection.getAccountInfo(AUTHORITY_TRANSFER, "confirmed");
  const transfer = transferInfo && {
    current: new PublicKey(transferInfo.data.subarray(8, 40)),
    pending: new PublicKey(transferInfo.data.subarray(40, 72)),
  };
  const ms = await multisig.accounts.Multisig.fromAccountAddress(connection, MULTISIG);
  const latestIndex = BigInt(ms.transactionIndex.toString());
  let latest = null;
  if (latestIndex > 0n) latest = await readProposal(latestIndex, transfer);
  return { programAuthority, transfer, ms, latestIndex, latest };
}

// Reads the vault transaction and proposal at `index` and checks that the stored inner
// instruction is exactly the expected accept, so approvers verify on-chain content.
async function readProposal(index, transfer) {
  const [txPda] = multisig.getTransactionPda({ multisigPda: MULTISIG, index });
  const [proposalPda] = multisig.getProposalPda({ multisigPda: MULTISIG, transactionIndex: index });
  const out = { index, checks: [], ok: false, status: "none", approved: [] };
  try {
    const vtx = await multisig.accounts.VaultTransaction.fromAccountAddress(connection, txPda);
    const keys = vtx.message.accountKeys;
    const ixs = vtx.message.instructions;
    const ix = ixs[0];
    const ixKeys = ix ? Array.from(ix.accountIndexes).map((i) => keys[i]) : [];
    // Once the accept has executed the proposal account is closed, so the current authority can
    // only be checked while a handoff is pending.
    const expected = [AUTHORITY_TRANSFER, GAME_CONFIG, VAULT];
    const accountsMatch = ixKeys.length === 4 && expected.every((k, i) => ixKeys[i].equals(k)) &&
      (!transfer || ixKeys[3].equals(transfer.current));
    const accountsLabel = transfer
      ? "accounts are transfer, config, vault, current authority"
      : `accounts are transfer, config, vault (current authority was ${ixKeys[3] ? short(ixKeys[3]) : "?"})`;
    out.checks = [
      ["exactly one instruction", ixs.length === 1],
      ["targets the Chainbound program", Boolean(ix) && keys[ix.programIdIndex].equals(PROGRAM_ID)],
      ["is accept_program_authority", Boolean(ix) && Buffer.from(ix.data).toString("hex") === ACCEPT_DISCRIMINATOR_HEX],
      [accountsLabel, accountsMatch],
      ["vault index 0", vtx.vaultIndex === 0],
    ];
    out.ok = out.checks.every(([, pass]) => pass);
  } catch {
    out.checks = [["vault transaction exists", false]];
  }
  try {
    const proposal = await multisig.accounts.Proposal.fromAccountAddress(connection, proposalPda);
    out.status = proposal.status.__kind;
    out.approved = proposal.approved.map((k) => k.toBase58());
  } catch {
    out.status = "no proposal";
  }
  return out;
}

function acceptInstruction(transfer) {
  return new TransactionInstruction({
    programId: PROGRAM_ID,
    keys: [
      { pubkey: AUTHORITY_TRANSFER, isSigner: false, isWritable: true },
      { pubkey: GAME_CONFIG, isSigner: false, isWritable: true },
      { pubkey: VAULT, isSigner: true, isWritable: false },
      { pubkey: transfer.current, isSigner: false, isWritable: true },
    ],
    data: Buffer.from(ACCEPT_DISCRIMINATOR_HEX, "hex"),
  });
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
  const { programAuthority, transfer, ms, latest } = chain;
  const done = programAuthority.equals(VAULT);
  $("authority").textContent = `${programAuthority.toBase58()}${done ? "  ✅ the vault" : ""}`;
  $("pending").textContent = transfer
    ? `${transfer.pending.toBase58()}${transfer.pending.equals(VAULT) ? "  (the vault)" : "  ⚠ NOT the vault"}`
    : "none";
  $("threshold").textContent = `${ms.threshold} of ${ms.members.length}`;

  const memberKeys = ms.members.map((m) => m.key.toBase58());
  const me = state.account ? state.account.address : null;
  state.member = me && memberKeys.includes(me) ? me : null;
  $("who").textContent = me ? `${me}${state.member ? "  (member)" : "  ⚠ not a member of this multisig"}` : "not connected";

  const box = $("proposal");
  box.innerHTML = "";
  if (latest) {
    const head = document.createElement("div");
    head.textContent = `Latest multisig transaction #${latest.index}: ${latest.status}, ` +
      `approvals ${latest.approved.length} of ${ms.threshold}`;
    box.append(head);
    for (const [label, pass] of latest.checks) {
      const row = document.createElement("div");
      row.className = pass ? "pass" : "fail";
      row.textContent = `${pass ? "✔" : "✘"} ${label}`;
      box.append(row);
    }
    for (const k of latest.approved) {
      const row = document.createElement("div");
      row.textContent = `approved by ${short(k)}`;
      box.append(row);
    }
  } else {
    box.textContent = "No multisig transactions yet.";
  }

  const canCreate = Boolean(state.member && transfer && transfer.pending.equals(VAULT) && !done);
  const active = latest && latest.ok && latest.status === "Active";
  const approved = latest && latest.ok && latest.status === "Approved";
  const iApproved = latest && me && latest.approved.includes(me);
  $("create").disabled = !canCreate || Boolean(active || approved);
  $("approve").disabled = !(state.member && active && !iApproved);
  $("execute").disabled = !(state.member && (approved || (active && !iApproved && latest.approved.length + 1 >= ms.threshold)));
  $("execute").textContent = active && !iApproved ? "Approve and execute" : "Execute";
  $("status").textContent = done
    ? "Done: the vault is GameConfig.program_authority."
    : !transfer
      ? "Waiting for DkFm to run propose-authority."
      : "Ready.";
}

async function connectWallet(wallet) {
  const connect = wallet.features["standard:connect"];
  const { accounts } = await connect.connect();
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

// Signs with the connected wallet and sends through the Devnet RPC, so the wallet's own network
// setting can't redirect the transaction.
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

function bs58(bytes) {
  const A = "123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz";
  let n = BigInt("0x" + Buffer.from(bytes).toString("hex"));
  let s = "";
  while (n > 0n) { s = A[Number(n % 58n)] + s; n /= 58n; }
  for (const b of bytes) { if (b === 0) s = "1" + s; else break; }
  return s;
}

async function createProposal() {
  const { transfer, latestIndex } = state.chain;
  const member = new PublicKey(state.account.address);
  const index = latestIndex + 1n;
  const inner = new TransactionMessage({
    payerKey: VAULT,
    recentBlockhash: PublicKey.default.toBase58(),
    instructions: [acceptInstruction(transfer)],
  });
  await signAndSend([
    multisig.instructions.vaultTransactionCreate({
      multisigPda: MULTISIG, transactionIndex: index, creator: member, rentPayer: member,
      vaultIndex: 0, ephemeralSigners: 0, transactionMessage: inner,
      memo: "chainbound: accept_program_authority",
    }),
    multisig.instructions.proposalCreate({ multisigPda: MULTISIG, transactionIndex: index, creator: member, rentPayer: member }),
    multisig.instructions.proposalApprove({ multisigPda: MULTISIG, transactionIndex: index, member }),
  ], `create + approve #${index}`);
}

async function approve() {
  const member = new PublicKey(state.account.address);
  const { latest } = state.chain;
  await signAndSend([multisig.instructions.proposalApprove({ multisigPda: MULTISIG, transactionIndex: latest.index, member })],
    `approve #${latest.index}`);
}

async function execute() {
  const member = new PublicKey(state.account.address);
  const { latest } = state.chain;
  const ixs = [];
  if (latest.status === "Active") {
    ixs.push(multisig.instructions.proposalApprove({ multisigPda: MULTISIG, transactionIndex: latest.index, member }));
  }
  const { instruction } = await multisig.instructions.vaultTransactionExecute({
    connection, multisigPda: MULTISIG, transactionIndex: latest.index, member,
  });
  ixs.push(instruction);
  await signAndSend(ixs, `execute #${latest.index}`);
}

function guard(fn) {
  return async () => {
    for (const id of ["create", "approve", "execute"]) $(id).disabled = true;
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
$("approve").onclick = guard(approve);
$("execute").onclick = guard(execute);
$("refresh").onclick = () => render();
listWallets();
render();
