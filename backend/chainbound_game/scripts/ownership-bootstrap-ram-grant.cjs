#!/usr/bin/env node
"use strict";

// Operator-only Devnet smoke RAM grant. Prepare mode asks the authenticated
// Edge Function for the exact partially signed transaction, validates every
// pinned field, and simulates without signing or sending. Execute mode requires
// a separate approval token and loads the matching wallet keypair only locally.

const crypto = require("crypto");
const { mkdirSync, readFileSync, writeFileSync } = require("fs");
const { join, resolve } = require("path");
const {
  Connection,
  Keypair,
  PublicKey,
  SystemProgram,
  Transaction,
  VersionedTransaction,
} = require("@solana/web3.js");

const MODE = String(process.env.CHAINBOUND_GRANT_MODE || "prepare").trim();
const APPROVAL = "APPROVED_ONE_DEVNET_SMOKE_RAM_GRANT_100";
const RPC_URL = "https://api.devnet.solana.com";
const DEVNET_GENESIS = "EtWTRABZaYq6iMfeYKouRu166VU2xqa1wcaWoxPkrZBG";
const SUPABASE_URL = "https://jhzqcbrzkausrrtcavuu.supabase.co";
const PROGRAM_ID = new PublicKey("9312ay7VrpvNYmyEx8TYCqhafbZ2Y5WGVbt6Qp772uUQ");
const GAME_CONFIG = new PublicKey("FdA2EJ89Xtqn6Z1pxayBQnSJQ8pVpVAuEjaUW8qbU8nx");
const RESULT_AUTHORITY = new PublicKey("FaAegH2tBCyDzxXcXyz64qEFWSrvpX8M6SSFNx1Pt5KF");
const SMOKE_WALLET = new PublicKey("H68eGMwT3UCigd9qf4u1VLyqQh6jq57HD4wLWaQrbBWq");
const IDEMPOTENCY_KEY = "devnet-smoke-grant-20260920-1";
const RAM_AMOUNT = 100;
const GRANT_RAM_DISCRIMINATOR = Buffer.from([178, 154, 17, 238, 34, 122, 206, 163]);
const PLAYER_PROFILE_DISCRIMINATOR = Buffer.from([82, 226, 99, 87, 164, 130, 181, 80]);
const MOVE_VAULT_DISCRIMINATOR = Buffer.from([30, 75, 22, 221, 63, 126, 196, 29]);
const GAME_CONFIG_DISCRIMINATOR = Buffer.from([45, 146, 146, 33, 170, 69, 96, 133]);
const RAM_GRANT_RECEIPT_DISCRIMINATOR = Buffer.from([151, 111, 207, 251, 59, 36, 69, 29]);

function assert(condition, message) {
  if (!condition) throw new Error(message);
}

function env(name) {
  const value = String(process.env[name] || "").trim();
  assert(value, `missing_env:${name}`);
  return value;
}

function timestamp() {
  return new Date().toISOString().replace(/[-:]/g, "").replace(/\.\d{3}Z$/, "Z");
}

function sha256(value) {
  return crypto.createHash("sha256").update(value).digest();
}

function sha256Hex(value) {
  return sha256(value).toString("hex");
}

function u32(value) {
  const data = Buffer.alloc(4);
  data.writeUInt32LE(value);
  return data;
}

function toVersionedTransaction(transaction) {
  const message = transaction.compileMessage();
  const requiredSignerKeys = message.accountKeys.slice(0, message.header.numRequiredSignatures);
  const signaturesByKey = new Map(
    transaction.signatures.map(({ publicKey, signature }) => [publicKey.toBase58(), signature]),
  );
  const signatures = requiredSignerKeys.map((publicKey) => {
    const signature = signaturesByKey.get(publicKey.toBase58());
    return signature ? Uint8Array.from(signature) : new Uint8Array(64);
  });
  return new VersionedTransaction(message, signatures);
}

async function simulateLegacyTransaction(connection, transaction, config) {
  // web3.js 1.99 rejects a SimulateTransactionConfig when the first argument
  // is a legacy Transaction. Wrap its unchanged legacy Message/signatures in a
  // VersionedTransaction so the public config overload is selected.
  return connection.simulateTransaction(toVersionedTransaction(transaction), config);
}

function writeEvidence(prefix, value) {
  const directory = resolve(__dirname, "../../../Logs");
  mkdirSync(directory, { recursive: true });
  const path = join(directory, `${prefix}-${timestamp()}.json`);
  writeFileSync(path, `${JSON.stringify(value, null, 2)}\n`, { encoding: "utf8", mode: 0o600 });
  return path;
}

function decodeJwtPayload(token, code) {
  const parts = token.split(".");
  assert(parts.length === 3, code);
  try {
    const value = JSON.parse(Buffer.from(parts[1], "base64url").toString("utf8"));
    assert(value && typeof value === "object", code);
    return value;
  } catch {
    throw new Error(code);
  }
}

function validateCredentials() {
  const anon = env("CHAINBOUND_SUPABASE_ANON_KEY");
  const token = env("CHAINBOUND_USER_ACCESS_TOKEN");
  assert(anon !== token, "user_access_token_is_anon_key");
  if (anon.startsWith("eyJ")) {
    assert(decodeJwtPayload(anon, "invalid_supabase_anon_key").role === "anon", "supabase_key_role_not_anon");
  }
  const payload = decodeJwtPayload(token, "invalid_user_access_token_format");
  assert(payload.iss === `${SUPABASE_URL}/auth/v1`, "user_access_token_wrong_project");
  assert(payload.role === "authenticated", "user_access_token_role_not_authenticated");
  assert(Number(payload.exp) > Math.floor(Date.now() / 1000) + 30, "user_access_token_expired_or_expiring");
}

function stableEdgeError(status, payload) {
  if (status === 401) return "authentication_rejected";
  const code = payload && typeof payload.error === "string" ? payload.error : `http_${status}`;
  return code.replace(/[^a-zA-Z0-9_:-]/g, "_").slice(0, 120);
}

async function edge(name, body) {
  const response = await fetch(`${SUPABASE_URL}/functions/v1/${name}`, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      apikey: env("CHAINBOUND_SUPABASE_ANON_KEY"),
      Authorization: `Bearer ${env("CHAINBOUND_USER_ACCESS_TOKEN")}`,
    },
    body: JSON.stringify(body),
  });
  let payload = null;
  try { payload = await response.json(); } catch { /* redact upstream bodies */ }
  if (!response.ok || !payload || payload.success !== true) {
    throw new Error(`${name}:${stableEdgeError(response.status, payload)}`);
  }
  return payload;
}

function requireProgramAccount(account, length, discriminator, code) {
  assert(account && account.owner.equals(PROGRAM_ID), `${code}_missing_or_wrong_owner`);
  assert(account.data.length === length, `${code}_layout_mismatch`);
  assert(account.data.subarray(0, 8).equals(discriminator), `${code}_discriminator_mismatch`);
}

function decodeProfile(account) {
  requireProgramAccount(account, 91, PLAYER_PROFILE_DISCRIMINATOR, "player_profile");
  const data = account.data;
  return {
    owner: new PublicKey(data.subarray(8, 40)).toBase58(),
    ramBalance: data.readUInt32LE(68),
    dailyRamEarned: data.readUInt16LE(72),
    lifetimeRamEarned: data.readBigUInt64LE(82).toString(),
  };
}

function decodeVault(account) {
  requireProgramAccount(account, 2505, MOVE_VAULT_DISCRIMINATOR, "move_vault");
  const data = account.data;
  const vectorCount = data.readUInt32LE(42);
  return {
    owner: new PublicKey(data.subarray(8, 40)).toBase58(),
    knownCapacity: data[40],
    knownCount: data[41],
    vectorCount,
    nextInstanceId: data.readBigUInt64LE(46 + vectorCount * 49).toString(),
  };
}

function decodeConfig(account) {
  requireProgramAccount(account, 89, GAME_CONFIG_DISCRIMINATOR, "game_config");
  const data = account.data;
  return {
    resultAuthority: new PublicKey(data.subarray(40, 72)).toBase58(),
    ramDailyCap: data.readUInt16LE(74),
    instructionFlags: data.readBigUInt64LE(80).toString(),
  };
}

function decodeReceipt(account) {
  requireProgramAccount(account, 121, RAM_GRANT_RECEIPT_DISCRIMINATOR, "ram_grant_receipt");
  const data = account.data;
  return {
    owner: new PublicKey(data.subarray(8, 40)).toBase58(),
    idempotencyHash: data.subarray(40, 72).toString("hex"),
    amount: data.readUInt32LE(72),
    balanceAfter: data.readUInt32LE(76),
    utcDay: data.readBigInt64LE(80).toString(),
    resultAuthority: new PublicKey(data.subarray(88, 120)).toBase58(),
    bump: data[120],
  };
}

function assertFresh(profile, vault, config) {
  assert(profile.owner === SMOKE_WALLET.toBase58(), "player_profile_owner_mismatch");
  assert(profile.ramBalance === 0, "fresh_profile_ram_not_zero");
  assert(profile.dailyRamEarned === 0, "fresh_profile_daily_ram_not_zero");
  assert(profile.lifetimeRamEarned === "0", "fresh_profile_lifetime_ram_not_zero");
  assert(vault.owner === SMOKE_WALLET.toBase58(), "move_vault_owner_mismatch");
  assert(vault.knownCapacity === 50 && vault.knownCount === 0 && vault.vectorCount === 0, "move_vault_not_empty");
  assert(vault.nextInstanceId === "1", "move_vault_next_instance_mismatch");
  assert(config.resultAuthority === RESULT_AUTHORITY.toBase58(), "result_authority_mismatch");
  assert(config.ramDailyCap === RAM_AMOUNT, "ram_daily_cap_mismatch");
  assert(config.instructionFlags === "0", "ownership_flags_not_off");
}

function loadWallet(path) {
  const raw = JSON.parse(readFileSync(path, "utf8"));
  assert(
    Array.isArray(raw) && raw.length === 64 &&
      raw.every((value) => Number.isInteger(value) && value >= 0 && value <= 255),
    "invalid_wallet_keypair_file",
  );
  const signer = Keypair.fromSecretKey(Uint8Array.from(raw));
  assert(signer.publicKey.equals(SMOKE_WALLET), "wallet_keypair_public_key_mismatch");
  return signer;
}

function validateGrantTransaction(transaction, profileAddress, receiptAddress, idempotencyHash) {
  assert(transaction.feePayer && transaction.feePayer.equals(SMOKE_WALLET), "transaction_fee_payer_mismatch");
  assert(transaction.instructions.length === 1, "unexpected_instruction_count");
  const instruction = transaction.instructions[0];
  assert(instruction.programId.equals(PROGRAM_ID), "unexpected_instruction_program");
  const expectedKeys = [receiptAddress, profileAddress, SMOKE_WALLET, GAME_CONFIG, RESULT_AUTHORITY, SystemProgram.programId];
  // Message-level privileges promote the owner to writable signer because it
  // is the fee payer, even though the GrantRam owner account is unchecked.
  const expectedMeta = [
    { isSigner: false, isWritable: true },
    { isSigner: false, isWritable: true },
    { isSigner: true, isWritable: true },
    { isSigner: false, isWritable: false },
    { isSigner: true, isWritable: true },
    { isSigner: false, isWritable: false },
  ];
  assert(instruction.keys.length === expectedKeys.length, "unexpected_instruction_account_count");
  expectedKeys.forEach((key, index) => {
    const actual = instruction.keys[index];
    assert(actual.pubkey.equals(key), `instruction_account_${index}_mismatch`);
    assert(actual.isSigner === expectedMeta[index].isSigner, `instruction_account_${index}_signer_mismatch`);
    assert(actual.isWritable === expectedMeta[index].isWritable, `instruction_account_${index}_writable_mismatch`);
  });
  const expectedData = Buffer.concat([GRANT_RAM_DISCRIMINATOR, idempotencyHash, u32(RAM_AMOUNT)]);
  assert(instruction.data.equals(expectedData), "grant_instruction_data_mismatch");

  const signerKeys = transaction.signatures.map((item) => item.publicKey.toBase58());
  assert(signerKeys.length === 2, "unexpected_required_signer_count");
  assert(signerKeys.includes(SMOKE_WALLET.toBase58()), "wallet_not_required_signer");
  assert(signerKeys.includes(RESULT_AUTHORITY.toBase58()), "result_authority_not_required_signer");
  return {
    signerKeys,
    resultSignature: transaction.signatures.find((item) => item.publicKey.equals(RESULT_AUTHORITY)),
    walletSignature: transaction.signatures.find((item) => item.publicKey.equals(SMOKE_WALLET)),
  };
}

function assertCompletedGrant(profile, vault, config, receipt, idempotencyHash) {
  assert(profile.owner === SMOKE_WALLET.toBase58(), "player_profile_owner_mismatch");
  assert(profile.ramBalance === RAM_AMOUNT, "completed_profile_ram_mismatch");
  assert(profile.dailyRamEarned === RAM_AMOUNT, "completed_profile_daily_ram_mismatch");
  assert(profile.lifetimeRamEarned === String(RAM_AMOUNT), "completed_profile_lifetime_ram_mismatch");
  assert(vault.owner === SMOKE_WALLET.toBase58(), "move_vault_owner_mismatch");
  assert(vault.knownCapacity === 50 && vault.knownCount === 0 && vault.vectorCount === 0, "move_vault_not_empty");
  assert(vault.nextInstanceId === "1", "move_vault_next_instance_mismatch");
  assert(config.resultAuthority === RESULT_AUTHORITY.toBase58(), "result_authority_mismatch");
  assert(config.ramDailyCap === RAM_AMOUNT, "ram_daily_cap_mismatch");
  assert(config.instructionFlags === "0", "ownership_flags_not_off");
  assert(receipt.owner === SMOKE_WALLET.toBase58(), "receipt_owner_mismatch");
  assert(receipt.idempotencyHash === idempotencyHash.toString("hex"), "receipt_idempotency_hash_mismatch");
  assert(receipt.amount === RAM_AMOUNT && receipt.balanceAfter === RAM_AMOUNT, "receipt_amount_mismatch");
  assert(receipt.resultAuthority === RESULT_AUTHORITY.toBase58(), "receipt_result_authority_mismatch");
}

async function findExistingGrantTransaction(connection, receiptAddress, profileAddress, idempotencyHash) {
  const explicitSignature = String(process.env.CHAINBOUND_GRANT_SIGNATURE || "").trim();
  const candidates = explicitSignature
    ? [{ signature: explicitSignature, err: null }]
    : await connection.getSignaturesForAddress(receiptAddress, { limit: 20 }, "confirmed");
  for (const candidate of candidates) {
    if (!candidate.signature || candidate.err) continue;
    const record = await connection.getTransaction(candidate.signature, {
      commitment: "confirmed",
      maxSupportedTransactionVersion: 0,
    });
    if (!record || record.meta?.err) continue;
    try {
      const transaction = Transaction.populate(record.transaction.message, record.transaction.signatures);
      const signatures = validateGrantTransaction(transaction, profileAddress, receiptAddress, idempotencyHash);
      assert(signatures.resultSignature?.signature, "recovered_result_authority_signature_missing");
      assert(signatures.walletSignature?.signature, "recovered_wallet_signature_missing");
      assert(transaction.verifySignatures(true), "recovered_signature_verification_failed");
      return { signature: candidate.signature, record };
    } catch {
      // Ignore unrelated address-history entries; only the exact pinned grant
      // transaction is eligible to repair the database confirmation.
    }
  }
  throw new Error("existing_grant_transaction_not_found");
}

async function prepare(connection, profileAddress, vaultAddress, receiptAddress, idempotencyHash) {
  const accounts = await connection.getMultipleAccountsInfo(
    [profileAddress, vaultAddress, GAME_CONFIG, receiptAddress], "confirmed",
  );
  const profile = decodeProfile(accounts[0]);
  const vault = decodeVault(accounts[1]);
  const config = decodeConfig(accounts[2]);
  assertFresh(profile, vault, config);
  assert(accounts[3] === null, "smoke_ram_grant_already_exists");

  const payload = await edge("ownership_prepare_devnet_smoke_grant", {
    wallet_address: SMOKE_WALLET.toBase58(),
    idempotency_key: IDEMPOTENCY_KEY,
  });
  const review = payload.review || {};
  assert(review.cluster === "devnet", "review_cluster_mismatch");
  assert(review.feePayer === SMOKE_WALLET.toBase58(), "review_fee_payer_mismatch");
  assert(review.chainboundProgram === PROGRAM_ID.toBase58(), "review_program_mismatch");
  assert(Number(review.amount) === RAM_AMOUNT && Number(review.dailyCap) === RAM_AMOUNT, "review_amount_mismatch");
  assert(Number(review.startingBalanceVerified) === 0, "review_starting_balance_mismatch");
  assert(payload.player_profile_address === profileAddress.toBase58(), "prepared_profile_mismatch");
  assert(payload.move_vault_address === vaultAddress.toBase58(), "prepared_vault_mismatch");
  assert(payload.receipt_address === receiptAddress.toBase58(), "prepared_receipt_mismatch");

  const transaction = Transaction.from(Buffer.from(payload.transaction_base64, "base64"));
  const { resultSignature, walletSignature } = validateGrantTransaction(
    transaction,
    profileAddress,
    receiptAddress,
    idempotencyHash,
  );
  assert(resultSignature && resultSignature.signature, "result_authority_signature_missing");
  assert(walletSignature && !walletSignature.signature, "wallet_signature_should_be_absent");
  assert(transaction.verifySignatures(false), "partial_signature_verification_failed");

  const simulation = await simulateLegacyTransaction(connection, transaction, {
    commitment: "confirmed", sigVerify: false, replaceRecentBlockhash: true,
  });
  assert(simulation.value.err === null, `unsigned_simulation_failed:${JSON.stringify(simulation.value.err)}`);
  return { transaction, payload, profile, vault, config, simulation };
}

async function confirmGrant(signature) {
  let last = "confirmation_failed";
  for (let attempt = 1; attempt <= 6; attempt += 1) {
    try {
      return await edge("ownership_confirm_devnet_smoke_grant", {
        wallet_address: SMOKE_WALLET.toBase58(),
        idempotency_key: IDEMPOTENCY_KEY,
        signature,
      });
    } catch (error) {
      last = error instanceof Error ? error.message.split(":").slice(-1)[0] : "confirmation_failed";
      if (!["transaction_not_confirmed", "ram_grant_not_verified", "ownership_confirm_devnet_smoke_grant_failed"].includes(last)) throw error;
      await new Promise((resolveDelay) => setTimeout(resolveDelay, 2000));
    }
  }
  throw new Error(`ownership_confirm_grant_timeout:${last}`);
}

async function main() {
  assert(
    MODE === "prepare" || MODE === "execute" || MODE === "recover",
    "CHAINBOUND_GRANT_MODE_must_be_prepare_execute_or_recover",
  );
  validateCredentials();
  const connection = new Connection(RPC_URL, "confirmed");
  const genesisHash = await connection.getGenesisHash();
  assert(genesisHash === DEVNET_GENESIS, `rpc_not_devnet:${genesisHash}`);
  const idempotencyHash = sha256(IDEMPOTENCY_KEY);
  const [profileAddress] = PublicKey.findProgramAddressSync([Buffer.from("player_profile"), SMOKE_WALLET.toBuffer()], PROGRAM_ID);
  const [vaultAddress] = PublicKey.findProgramAddressSync([Buffer.from("move_vault"), SMOKE_WALLET.toBuffer()], PROGRAM_ID);
  const [receiptAddress] = PublicKey.findProgramAddressSync(
    [Buffer.from("ram_grant_receipt"), SMOKE_WALLET.toBuffer(), idempotencyHash], PROGRAM_ID,
  );
  const baseSummary = {
    operation: "one-devnet-smoke-ram-grant",
    mode: MODE,
    cluster: "devnet",
    genesisHash,
    rpcOrigin: new URL(RPC_URL).origin,
    supabaseProjectHost: new URL(SUPABASE_URL).hostname,
    wallet: SMOKE_WALLET.toBase58(),
    playerProfile: profileAddress.toBase58(),
    moveVault: vaultAddress.toBase58(),
    receipt: receiptAddress.toBase58(),
    idempotencyKey: IDEMPOTENCY_KEY,
    idempotencyHash: idempotencyHash.toString("hex"),
    amount: RAM_AMOUNT,
    credentialMaterial: "REDACTED/not written",
  };

  const existingAccounts = await connection.getMultipleAccountsInfo(
    [profileAddress, vaultAddress, GAME_CONFIG, receiptAddress],
    "confirmed",
  );
  if (existingAccounts[3]) {
    const profile = decodeProfile(existingAccounts[0]);
    const vault = decodeVault(existingAccounts[1]);
    const config = decodeConfig(existingAccounts[2]);
    const receipt = decodeReceipt(existingAccounts[3]);
    assertCompletedGrant(profile, vault, config, receipt, idempotencyHash);
    const recovered = await findExistingGrantTransaction(
      connection,
      receiptAddress,
      profileAddress,
      idempotencyHash,
    );
    const recoveredSummary = {
      ...baseSummary,
      verifiedOnChain: true,
      alreadyExecutedOnChain: true,
      recoveryTransactionFound: true,
      signature: recovered.signature,
      slot: recovered.record.slot,
      feeLamports: recovered.record.meta?.fee || null,
      profile,
      receipt,
      dailyCap: config.ramDailyCap,
      instructionFlags: config.instructionFlags,
      signingPerformed: false,
      sendingPerformed: false,
    };
    if (MODE === "prepare") {
      const evidencePath = writeEvidence("ownership-bootstrap-ram-grant-recovery-prepare", {
        ...recoveredSummary,
        databaseConfirmationPerformed: false,
        nextMode: "recover",
      });
      process.stdout.write(`${JSON.stringify({
        ...recoveredSummary,
        databaseConfirmationPerformed: false,
        nextMode: "recover",
        evidencePath,
      }, null, 2)}\n`);
      return;
    }
    assert(
      process.env.CHAINBOUND_GRANT_APPROVAL === APPROVAL,
      `refusing_to_recover:set_CHAINBOUND_GRANT_APPROVAL_${APPROVAL}`,
    );
    const database = await confirmGrant(recovered.signature);
    assert(database.confirmed === true, "database_grant_not_confirmed");
    assert(Number(database.amount) === RAM_AMOUNT && Number(database.balance_after) === RAM_AMOUNT, "database_grant_amount_mismatch");
    assert(!database.signature || database.signature === recovered.signature, "database_grant_signature_mismatch");
    const evidence = {
      ...recoveredSummary,
      verified: true,
      recovered: true,
      databaseConfirmed: true,
      databaseIdempotent: database.idempotent === true,
    };
    const evidencePath = writeEvidence("ownership-bootstrap-ram-grant-recovered", evidence);
    process.stdout.write(`${JSON.stringify({ ...evidence, evidencePath }, null, 2)}\n`);
    return;
  }
  assert(MODE !== "recover", "no_existing_grant_to_recover");

  const prepared = await prepare(connection, profileAddress, vaultAddress, receiptAddress, idempotencyHash);
  const summary = {
    ...baseSummary,
    startingBalance: prepared.profile.ramBalance,
    dailyCap: prepared.config.ramDailyCap,
    instructionFlags: prepared.config.instructionFlags,
    estimatedNetworkFeeLamports: Number(prepared.payload.review.estimatedNetworkFeeLamports || 0),
    requiredSignerPublicKeys: prepared.transaction.signatures.map((item) => item.publicKey.toBase58()),
    resultAuthoritySignaturePresent: true,
    walletSignaturePresent: false,
    messageSha256: sha256Hex(prepared.transaction.serializeMessage()),
    unsignedSimulation: { error: null, unitsConsumed: prepared.simulation.value.unitsConsumed || null },
    signingPerformed: false,
    sendingPerformed: false,
  };

  if (MODE === "prepare") {
    const evidencePath = writeEvidence("ownership-bootstrap-ram-grant-prepare", summary);
    process.stdout.write(`${JSON.stringify({ ...summary, evidencePath }, null, 2)}\n`);
    return;
  }

  assert(process.env.CHAINBOUND_GRANT_APPROVAL === APPROVAL, `refusing_to_sign:set_CHAINBOUND_GRANT_APPROVAL_${APPROVAL}`);
  const signer = loadWallet(env("CHAINBOUND_SMOKE_WALLET_KEYPAIR"));
  prepared.transaction.partialSign(signer);
  assert(prepared.transaction.verifySignatures(true), "fully_signed_transaction_verification_failed");
  const signedSimulation = await simulateLegacyTransaction(connection, prepared.transaction, {
    commitment: "confirmed", sigVerify: true, replaceRecentBlockhash: false,
  });
  assert(signedSimulation.value.err === null, `signed_simulation_failed:${JSON.stringify(signedSimulation.value.err)}`);
  const signature = await connection.sendRawTransaction(prepared.transaction.serialize(), {
    skipPreflight: false, preflightCommitment: "confirmed", maxRetries: 15,
  });
  const confirmation = await connection.confirmTransaction(signature, "confirmed");
  assert(confirmation.value.err === null, `transaction_failed:${JSON.stringify(confirmation.value.err)}`);

  const [profileAccount, receiptAccount] = await connection.getMultipleAccountsInfo([profileAddress, receiptAddress], "confirmed");
  const profile = decodeProfile(profileAccount);
  const receipt = decodeReceipt(receiptAccount);
  assert(profile.owner === SMOKE_WALLET.toBase58(), "final_profile_owner_mismatch");
  assert(profile.ramBalance === RAM_AMOUNT && profile.dailyRamEarned === RAM_AMOUNT, "final_profile_ram_mismatch");
  assert(profile.lifetimeRamEarned === String(RAM_AMOUNT), "final_profile_lifetime_ram_mismatch");
  assert(receipt.owner === SMOKE_WALLET.toBase58(), "receipt_owner_mismatch");
  assert(receipt.idempotencyHash === idempotencyHash.toString("hex"), "receipt_idempotency_hash_mismatch");
  assert(receipt.amount === RAM_AMOUNT && receipt.balanceAfter === RAM_AMOUNT, "receipt_amount_mismatch");
  assert(receipt.resultAuthority === RESULT_AUTHORITY.toBase58(), "receipt_result_authority_mismatch");
  const database = await confirmGrant(signature);
  assert(database.confirmed === true, "database_grant_not_confirmed");
  assert(Number(database.amount) === RAM_AMOUNT && Number(database.balance_after) === RAM_AMOUNT, "database_grant_amount_mismatch");
  const record = await connection.getTransaction(signature, { commitment: "confirmed", maxSupportedTransactionVersion: 0 });
  const evidence = {
    ...summary,
    verified: true,
    signingPerformed: true,
    sendingPerformed: true,
    walletSignaturePresent: true,
    signature,
    slot: record?.slot || null,
    feeLamports: record?.meta?.fee || null,
    signedSimulationUnitsConsumed: signedSimulation.value.unitsConsumed || null,
    profile,
    receipt,
    databaseConfirmed: true,
    databaseIdempotent: database.idempotent === true,
  };
  const evidencePath = writeEvidence("ownership-bootstrap-ram-grant", evidence);
  process.stdout.write(`${JSON.stringify({ ...evidence, evidencePath }, null, 2)}\n`);
}

module.exports = {
  assertCompletedGrant,
  assertFresh,
  decodeConfig,
  decodeProfile,
  decodeReceipt,
  decodeVault,
  findExistingGrantTransaction,
  simulateLegacyTransaction,
  toVersionedTransaction,
};

if (require.main === module) {
  main().catch((error) => {
    const message = error instanceof Error ? error.message : "unknown_error";
    process.stderr.write(`${message.replace(/[\r\n]+/g, " ")}\n`);
    process.exitCode = 1;
  });
}
