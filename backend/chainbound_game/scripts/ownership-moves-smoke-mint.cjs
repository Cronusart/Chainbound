#!/usr/bin/env node
"use strict";

// Operator-only Devnet smoke Stamp. Prepare mode is keyless and never sends.
// Execute mode requires an explicit approval token, loads the linked wallet
// keypair locally, simulates with all signatures, and sends exactly one tx.
// JWTs, anon keys, and keypair bytes are never printed or written to evidence.

const crypto = require("crypto");
const { mkdirSync, readFileSync, writeFileSync } = require("fs");
const { homedir } = require("os");
const { join, resolve } = require("path");
const {
  Connection,
  Keypair,
  PublicKey,
  SystemProgram,
  Transaction,
} = require("@solana/web3.js");
const {
  decodeConfig,
  decodeProfile,
  decodeVault,
  simulateLegacyTransaction,
} = require("./ownership-bootstrap-ram-grant.cjs");

const MODE = process.env.CHAINBOUND_SMOKE_MODE || "prepare";
const APPROVAL = "APPROVED_ONE_DEVNET_MOVES_SMOKE_MINT";
const RPC_URL = "https://api.devnet.solana.com";
const DEVNET_GENESIS = "EtWTRABZaYq6iMfeYKouRu166VU2xqa1wcaWoxPkrZBG";
const SUPABASE_URL = "https://jhzqcbrzkausrrtcavuu.supabase.co";
const PROGRAM = "9312ay7VrpvNYmyEx8TYCqhafbZ2Y5WGVbt6Qp772uUQ";
const CORE_PROGRAM = "CoREENxT6tW1HoK8ypY1SxRMZTcVPm7R94rH4PZNhX7d";
const MOVES = "AUQuvgpFDUekeVh2GHri99A59G4YY5LUWdGw9fKTB9wC";
const CORE_AUTHORITY = "FNRyxJLpcGuWwEUXYiXqjJMDsHAAs1gU8CnkjjBRL3ys";
const RESULT_AUTHORITY = "FaAegH2tBCyDzxXcXyz64qEFWSrvpX8M6SSFNx1Pt5KF";
const PROGRAM_ID = new PublicKey(PROGRAM);
const MOVES_COLLECTION = new PublicKey(MOVES);
const CORE_PROGRAM_ID = new PublicKey(CORE_PROGRAM);
const RESULT_AUTHORITY_KEY = new PublicKey(RESULT_AUTHORITY);
const KNOWN_ROLL_RAM_COST = 5;
const STAMP_RAM_COST = 75;
const STARTING_RAM = 100;
const EXPECTED_RAM_AFTER = STARTING_RAM - KNOWN_ROLL_RAM_COST - STAMP_RAM_COST;
const KNOWN_MOVE_ID = 50410;
const KNOWN_ELEMENT = 0;
const KNOWN_BASE_POWER = 40;
const EXPECTED_METADATA_URI_BASE =
  "https://jhzqcbrzkausrrtcavuu.supabase.co/storage/v1/object/public/chainbound-ownership-metadata";
const EXPECTED_POOL_HASH = "6cea5ce56944a88486cafbb568a628b5aa5664c3db652a79cd90016427aaa4b4";
const ROLL_KNOWN_DISCRIMINATOR = Buffer.from([42, 140, 118, 198, 71, 101, 152, 152]);
const PREPARE_STAMP_DISCRIMINATOR = Buffer.from([244, 254, 92, 75, 125, 49, 21, 158]);
const STAMP_KNOWN_DISCRIMINATOR = Buffer.from([169, 191, 199, 46, 62, 34, 188, 181]);
const OWNERSHIP_CONFIG_DISCRIMINATOR = Buffer.from([227, 236, 116, 131, 86, 134, 101, 163]);
const DEFAULT_KNOWN_ID = "9293fc49-b5d0-48d9-b071-504bdc9b3b00";
const DEFAULT_IDEMPOTENCY_KEY = "moves-smoke-mint-20260919-1";

function assert(condition, message) {
  if (!condition) throw new Error(message);
}

function env(name) {
  const value = String(process.env[name] || "").trim();
  assert(value, `missing_env:${name}`);
  return value;
}

function decodeJwtPayload(token, errorCode) {
  const parts = token.split(".");
  assert(parts.length === 3, errorCode);
  try {
    const payload = JSON.parse(
      Buffer.from(parts[1], "base64url").toString("utf8"),
    );
    assert(payload && typeof payload === "object", errorCode);
    return payload;
  } catch {
    throw new Error(errorCode);
  }
}

function validateLocalCredentials() {
  const anonKey = env("CHAINBOUND_SUPABASE_ANON_KEY");
  const accessToken = env("CHAINBOUND_USER_ACCESS_TOKEN");
  assert(accessToken !== anonKey, "user_access_token_is_anon_key");

  if (anonKey.startsWith("eyJ")) {
    const anonPayload = decodeJwtPayload(anonKey, "invalid_supabase_anon_key");
    assert(anonPayload.role === "anon", "supabase_key_role_not_anon");
  }

  const payload = decodeJwtPayload(
    accessToken,
    "invalid_user_access_token_format",
  );
  assert(
    payload.iss === `${SUPABASE_URL}/auth/v1`,
    "user_access_token_wrong_project",
  );
  assert(
    payload.role === "authenticated",
    "user_access_token_role_not_authenticated",
  );
  assert(
    typeof payload.sub === "string" && payload.sub.length > 0,
    "user_access_token_missing_subject",
  );
  assert(
    Number.isFinite(Number(payload.exp)) &&
      Number(payload.exp) > Math.floor(Date.now() / 1000) + 30,
    "user_access_token_expired_or_expiring",
  );
}

function publicKey(value, name) {
  try {
    return new PublicKey(value);
  } catch {
    throw new Error(`invalid_public_key:${name}`);
  }
}

function sha256Hex(bytes) {
  return crypto.createHash("sha256").update(bytes).digest("hex");
}

function sha256Bytes(value) {
  return crypto.createHash("sha256").update(value).digest();
}

function u16(value) {
  const data = Buffer.alloc(2);
  data.writeUInt16LE(value);
  return data;
}

function readBorshString(data, offset, code) {
  assert(offset + 4 <= data.length, `${code}_length_missing`);
  const length = data.readUInt32LE(offset);
  const start = offset + 4;
  const end = start + length;
  assert(end <= data.length, `${code}_truncated`);
  return { value: data.subarray(start, end).toString("utf8"), offset: end };
}

function assertInstructionKeys(instruction, expected, code) {
  assert(instruction.keys.length === expected.length, `${code}_account_count_mismatch`);
  expected.forEach((item, index) => {
    const actual = instruction.keys[index];
    assert(actual.pubkey.equals(item.pubkey), `${code}_account_${index}_mismatch`);
    assert(actual.isSigner === item.isSigner, `${code}_account_${index}_signer_mismatch`);
    assert(actual.isWritable === item.isWritable, `${code}_account_${index}_writable_mismatch`);
  });
}

function key(pubkey, isSigner, isWritable) {
  return { pubkey, isSigner, isWritable };
}

function decodeOwnershipConfig(account) {
  assert(account && account.owner.equals(PROGRAM_ID), "ownership_config_missing_or_wrong_owner");
  const data = account.data;
  assert(data.length === 183, "ownership_config_layout_mismatch");
  assert(data.subarray(0, 8).equals(OWNERSHIP_CONFIG_DISCRIMINATOR), "ownership_config_discriminator_mismatch");
  const decoded = {
    movesCollection: new PublicKey(data.subarray(10, 42)).toBase58(),
    stampRamCost: data.readUInt32LE(138),
    bindRamCost: data.readUInt32LE(142),
    generatorVersion: data.readUInt32LE(146),
    poolHash: data.subarray(150, 182).toString("hex"),
  };
  assert(decoded.movesCollection === MOVES, "ownership_config_moves_collection_mismatch");
  assert(decoded.stampRamCost === STAMP_RAM_COST, "ownership_config_stamp_cost_mismatch");
  assert(decoded.bindRamCost === 250, "ownership_config_bind_cost_mismatch");
  assert(decoded.generatorVersion === 1, "ownership_config_generator_version_mismatch");
  assert(decoded.poolHash === EXPECTED_POOL_HASH, "ownership_config_pool_hash_mismatch");
  return decoded;
}

function timestamp() {
  return new Date()
    .toISOString()
    .replace(/[-:]/g, "")
    .replace(/\.\d{3}Z$/, "Z");
}

function writeEvidence(prefix, value) {
  const directory = resolve(__dirname, "../../../Logs");
  mkdirSync(directory, { recursive: true });
  const path = join(directory, `${prefix}-${timestamp()}.json`);
  writeFileSync(path, `${JSON.stringify(value, null, 2)}\n`, {
    encoding: "utf8",
    mode: 0o600,
  });
  return path;
}

function stableEdgeError(status, payload) {
  if (status === 401) {
    const message = String(
      (payload &&
        (payload.message || payload.msg || payload.error_description)) ||
        "",
    ).toLowerCase();
    if (message.includes("missing") && message.includes("authorization"))
      return "missing_authorization_header";
    if (message.includes("jwt") || message.includes("token"))
      return "user_access_token_rejected";
    return "authentication_rejected";
  }
  const code =
    payload && typeof payload.error === "string"
      ? payload.error
      : `http_${status}`;
  return code.replace(/[^a-zA-Z0-9_:-]/g, "_").slice(0, 120);
}

async function edge(functionName, body) {
  const response = await fetch(`${SUPABASE_URL}/functions/v1/${functionName}`, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      apikey: env("CHAINBOUND_SUPABASE_ANON_KEY"),
      Authorization: `Bearer ${env("CHAINBOUND_USER_ACCESS_TOKEN")}`,
    },
    body: JSON.stringify(body),
  });
  let payload = null;
  try {
    payload = await response.json();
  } catch {
    // Return only a stable local code; never echo an upstream body.
  }
  if (!response.ok || !payload || payload.success !== true)
    throw new Error(
      `${functionName}:${stableEdgeError(response.status, payload)}`,
    );
  return payload;
}

async function prepareStamp(wallet, knownId, idempotencyKey) {
  return await edge("ownership_prepare_stamp", {
    wallet_address: wallet,
    known_instance_id: knownId,
    modifier_count: 1,
    idempotency_key: idempotencyKey,
  });
}

function verifyReview(payload, wallet, knownId) {
  const review = payload.review || {};
  assert(review.cluster === "devnet", "review_cluster_mismatch");
  assert(review.feePayer === wallet, "review_fee_payer_mismatch");
  assert(review.chainboundProgram === PROGRAM, "review_program_mismatch");
  assert(review.coreProgram === CORE_PROGRAM, "review_core_program_mismatch");
  assert(review.outputCollection === MOVES, "review_collection_mismatch");
  assert(review.knownInstanceId === knownId, "review_known_instance_mismatch");
  assert(
    Number(review.ramCost) === STAMP_RAM_COST,
    "review_stamp_ram_cost_mismatch",
  );
  publicKey(payload.ticket_address, "ticket_address");
  publicKey(payload.stamped_asset_address, "stamped_asset_address");
  assert(
    typeof payload.transaction_base64 === "string" &&
      payload.transaction_base64.length > 0,
    "missing_transaction",
  );
  return review;
}

async function inspectPrepared(connection, payload, wallet, knownId) {
  const review = verifyReview(payload, wallet, knownId);
  const owner = publicKey(wallet, "wallet");
  const idempotencyKey =
    process.env.CHAINBOUND_SMOKE_IDEMPOTENCY_KEY || DEFAULT_IDEMPOTENCY_KEY;
  const operationHash = sha256Bytes(idempotencyKey);
  const materializeHash = sha256Bytes(`known:${knownId}`);
  const [profile] = PublicKey.findProgramAddressSync(
    [Buffer.from("player_profile"), owner.toBuffer()],
    PROGRAM_ID,
  );
  const [vault] = PublicKey.findProgramAddressSync(
    [Buffer.from("move_vault"), owner.toBuffer()],
    PROGRAM_ID,
  );
  const [gameConfig] = PublicKey.findProgramAddressSync(
    [Buffer.from("game_config")],
    PROGRAM_ID,
  );
  const [ownershipConfig] = PublicKey.findProgramAddressSync(
    [Buffer.from("ownership_config")],
    PROGRAM_ID,
  );
  const [coreAuthority] = PublicKey.findProgramAddressSync(
    [Buffer.from("core_authority")],
    PROGRAM_ID,
  );
  const [knownReceipt] = PublicKey.findProgramAddressSync(
    [Buffer.from("known_roll_receipt"), owner.toBuffer(), materializeHash],
    PROGRAM_ID,
  );
  const [ticket] = PublicKey.findProgramAddressSync(
    [Buffer.from("stamp_ticket"), owner.toBuffer(), operationHash],
    PROGRAM_ID,
  );
  const [stampedAsset] = PublicKey.findProgramAddressSync(
    [Buffer.from("stamped_asset"), owner.toBuffer(), operationHash],
    PROGRAM_ID,
  );
  assert(payload.ticket_address === ticket.toBase58(), "ticket_pda_mismatch");
  assert(payload.stamped_asset_address === stampedAsset.toBase58(), "stamped_asset_pda_mismatch");

  const stateAccounts = await connection.getMultipleAccountsInfo(
    [profile, vault, gameConfig, ownershipConfig, knownReceipt, ticket, stampedAsset],
    "confirmed",
  );
  const profileState = decodeProfile(stateAccounts[0]);
  const vaultState = decodeVault(stateAccounts[1]);
  const gameState = decodeConfig(stateAccounts[2]);
  const ownershipState = decodeOwnershipConfig(stateAccounts[3]);
  assert(profileState.owner === wallet, "profile_owner_mismatch");
  assert(profileState.ramBalance === STARTING_RAM, "profile_ram_not_100_before_stamp");
  assert(profileState.dailyRamEarned === STARTING_RAM, "profile_daily_ram_not_100_before_stamp");
  assert(profileState.lifetimeRamEarned === String(STARTING_RAM), "profile_lifetime_ram_not_100_before_stamp");
  assert(vaultState.owner === wallet, "vault_owner_mismatch");
  assert(vaultState.knownCount === 0 && vaultState.vectorCount === 0, "vault_not_empty_before_materialization");
  assert(vaultState.nextInstanceId === "1", "unexpected_next_instance_id");
  assert(gameState.resultAuthority === RESULT_AUTHORITY, "game_config_result_authority_mismatch");
  assert(gameState.ramDailyCap === STARTING_RAM, "game_config_daily_cap_mismatch");
  assert(gameState.instructionFlags === "0", "ownership_flags_must_remain_off");
  assert(stateAccounts[4] === null, "known_materialization_receipt_already_exists");
  assert(stateAccounts[5] === null, "stamp_ticket_already_exists");
  assert(stateAccounts[6] === null, "stamped_asset_already_exists_refusing_second_mint");

  const transaction = Transaction.from(
    Buffer.from(payload.transaction_base64, "base64"),
  );
  assert(
    transaction.feePayer && transaction.feePayer.toBase58() === wallet,
    "transaction_fee_payer_mismatch",
  );
  assert(transaction.instructions.length === 3, "expected_materialize_prepare_stamp_instruction_count");
  assert(
    transaction.instructions.every((ix) => ix.programId.toBase58() === PROGRAM),
    "unexpected_outer_program",
  );

  const requiredSigners = transaction.signatures.map((entry) =>
    entry.publicKey.toBase58(),
  );
  assert(
    requiredSigners.length === 2 &&
      requiredSigners[0] === wallet &&
      requiredSigners[1] === RESULT_AUTHORITY,
    "unexpected_required_signer_set",
  );
  assert(requiredSigners.includes(wallet), "wallet_not_required_signer");
  assert(
    requiredSigners.includes(RESULT_AUTHORITY),
    "result_authority_not_required_signer",
  );
  const resultSignature = transaction.signatures.find(
    (entry) => entry.publicKey.toBase58() === RESULT_AUTHORITY,
  );
  const walletSignature = transaction.signatures.find(
    (entry) => entry.publicKey.toBase58() === wallet,
  );
  assert(
    resultSignature && resultSignature.signature,
    "result_authority_signature_missing",
  );
  assert(
    walletSignature && !walletSignature.signature,
    "wallet_signature_should_be_absent_during_prepare",
  );
  assert(
    transaction.verifySignatures(false),
    "partial_signature_verification_failed",
  );

  const [rollKnown, prepareStampInstruction, stampKnown] = transaction.instructions;
  assertInstructionKeys(rollKnown, [
    key(vault, false, true),
    key(owner, true, true),
    key(profile, false, true),
    key(ownershipConfig, false, false),
    key(knownReceipt, false, true),
    key(gameConfig, false, false),
    key(RESULT_AUTHORITY_KEY, true, true),
    key(SystemProgram.programId, false, false),
  ], "roll_known");
  const expectedRollData = Buffer.concat([
    ROLL_KNOWN_DISCRIMINATOR,
    materializeHash,
    u16(KNOWN_MOVE_ID),
    Buffer.from([KNOWN_ELEMENT]),
    u16(KNOWN_BASE_POWER),
  ]);
  assert(rollKnown.data.equals(expectedRollData), "roll_known_instruction_data_mismatch");

  assertInstructionKeys(prepareStampInstruction, [
    key(ticket, false, true),
    key(vault, false, true),
    key(ownershipConfig, false, false),
    key(owner, true, true),
    key(gameConfig, false, false),
    key(RESULT_AUTHORITY_KEY, true, true),
    key(SystemProgram.programId, false, false),
  ], "prepare_stamp");
  const prepareData = prepareStampInstruction.data;
  assert(prepareData.subarray(0, 8).equals(PREPARE_STAMP_DISCRIMINATOR), "prepare_stamp_discriminator_mismatch");
  assert(prepareData.subarray(8, 40).equals(operationHash), "prepare_stamp_operation_hash_mismatch");
  assert(prepareData.readBigUInt64LE(40) === 1n, "prepare_stamp_instance_id_mismatch");
  assert(prepareData.readUInt32LE(48) === 1, "prepare_stamp_modifier_count_mismatch");
  let parsed = readBorshString(prepareData, 52, "prepare_stamp_modifier");
  const modifierId = parsed.value;
  assert(/^\d+\.\d+-[a-z]$/.test(modifierId), "prepare_stamp_modifier_id_invalid");
  assert(parsed.offset < prepareData.length, "prepare_stamp_rarity_missing");
  const rarity = prepareData[parsed.offset];
  assert(rarity <= 5, "prepare_stamp_rarity_invalid");
  parsed = readBorshString(prepareData, parsed.offset + 1, "prepare_stamp_asset_name");
  assert(parsed.value === `Chainbound Stamped ${KNOWN_MOVE_ID}`, "prepare_stamp_asset_name_mismatch");
  parsed = readBorshString(prepareData, parsed.offset, "prepare_stamp_asset_uri");
  assert(
    parsed.value === `${EXPECTED_METADATA_URI_BASE}/stamped/${stampedAsset.toBase58()}.json`,
    "prepare_stamp_asset_uri_mismatch",
  );
  assert(parsed.offset + 8 === prepareData.length, "prepare_stamp_trailing_data_mismatch");
  const expiresAtUnix = prepareData.readBigInt64LE(parsed.offset);
  assert(
    expiresAtUnix === BigInt(Math.floor(new Date(payload.expires_at).getTime() / 1000)),
    "prepare_stamp_expiry_mismatch",
  );

  assertInstructionKeys(stampKnown, [
    key(owner, true, true),
    key(profile, false, true),
    key(vault, false, true),
    key(ticket, false, true),
    key(ownershipConfig, false, false),
    key(coreAuthority, false, false),
    key(stampedAsset, false, true),
    key(MOVES_COLLECTION, false, true),
    key(CORE_PROGRAM_ID, false, false),
    key(SystemProgram.programId, false, false),
  ], "stamp_known");
  assert(
    stampKnown.data.equals(Buffer.concat([STAMP_KNOWN_DISCRIMINATOR, operationHash])),
    "stamp_known_instruction_data_mismatch",
  );

  const accountKeys = new Set();
  for (const instruction of transaction.instructions) {
    for (const key of instruction.keys) accountKeys.add(key.pubkey.toBase58());
  }
  assert(accountKeys.has(MOVES), "moves_collection_missing_from_transaction");
  assert(
    accountKeys.has(CORE_PROGRAM),
    "core_program_missing_from_transaction",
  );
  assert(
    accountKeys.has(payload.stamped_asset_address),
    "stamped_asset_missing_from_transaction",
  );

  const simulation = await simulateLegacyTransaction(connection, transaction, {
    commitment: "confirmed",
    sigVerify: false,
    replaceRecentBlockhash: true,
  });
  if (simulation.value.err !== null) {
    const logs = (simulation.value.logs || [])
      .slice(-12)
      .map((line) => String(line).replace(/[\u0000-\u0008\u000b\u000c\u000e-\u001f\u007f]/g, ""));
    throw new Error(
      `unsigned_simulation_failed:${JSON.stringify({ error: simulation.value.err, logs })}`,
    );
  }

  return {
    transaction,
    summary: {
      operation: "one-devnet-moves-smoke-stamp",
      signingPerformed: false,
      sendingPerformed: false,
      cluster: "devnet",
      rpcOrigin: new URL(RPC_URL).origin,
      supabaseProjectHost: new URL(SUPABASE_URL).hostname,
      wallet,
      knownInstanceId: knownId,
      idempotencyKey,
      outputCollection: MOVES,
      expectedUpdateAuthority: CORE_AUTHORITY,
      predictedAsset: payload.stamped_asset_address,
      ticket: payload.ticket_address,
      ramCost: STAMP_RAM_COST,
      materializeKnownRamCost: KNOWN_ROLL_RAM_COST,
      totalRamCost: KNOWN_ROLL_RAM_COST + STAMP_RAM_COST,
      startingRam: STARTING_RAM,
      expectedRamAfter: EXPECTED_RAM_AFTER,
      onchainInstanceId: "1",
      modifierId,
      rarity,
      ownershipConfig: ownershipState,
      estimatedNetworkFeeLamports: Number(
        review.estimatedNetworkFeeLamports || 0,
      ),
      requiredSignerPublicKeys: requiredSigners,
      resultAuthoritySignaturePresent: true,
      walletSignaturePresent: false,
      instructionCount: transaction.instructions.length,
      messageSha256: sha256Hex(transaction.serializeMessage()),
      expiresAt: payload.expires_at,
      simulation: {
        error: simulation.value.err,
        unitsConsumed: simulation.value.unitsConsumed || null,
      },
      credentialMaterial: "REDACTED/not written",
    },
  };
}

function loadWallet(path, expectedWallet) {
  const bytes = JSON.parse(readFileSync(path, "utf8"));
  assert(
    Array.isArray(bytes) &&
      bytes.length === 64 &&
      bytes.every(Number.isInteger),
    "invalid_wallet_keypair_file",
  );
  const signer = Keypair.fromSecretKey(Uint8Array.from(bytes));
  assert(
    signer.publicKey.toBase58() === expectedWallet,
    "wallet_keypair_public_key_mismatch",
  );
  return signer;
}

function authorityMatches(asset) {
  return (
    Array.isArray(asset && asset.authorities) &&
    asset.authorities.some((entry) => entry && entry.address === CORE_AUTHORITY)
  );
}

function collectionMatches(asset) {
  return (
    Array.isArray(asset && asset.grouping) &&
    asset.grouping.some(
      (entry) =>
        entry &&
        entry.group_key === "collection" &&
        entry.group_value === MOVES,
    )
  );
}

async function waitForDas(assetId, wallet) {
  let lastCode = "not_indexed";
  for (let attempt = 1; attempt <= 24; attempt += 1) {
    try {
      const assetResponse = await edge("ownership_das_proxy", {
        method: "getAsset",
        params: { id: assetId },
      });
      const groupResponse = await edge("ownership_das_proxy", {
        method: "getAssetsByGroup",
        params: {
          groupKey: "collection",
          groupValue: MOVES,
          page: 1,
          limit: 50,
        },
      });
      const asset = assetResponse.result;
      const group = groupResponse.result;
      const items = Array.isArray(group && group.items) ? group.items : [];
      if (
        asset &&
        asset.id === assetId &&
        asset.interface === "MplCoreAsset" &&
        asset.ownership &&
        asset.ownership.owner === wallet &&
        authorityMatches(asset) &&
        collectionMatches(asset) &&
        Number(group.total) >= 1 &&
        items.some((item) => item && item.id === assetId)
      ) {
        return { asset, group };
      }
      lastCode = "asset_or_group_not_indexed";
    } catch (error) {
      lastCode =
        error instanceof Error
          ? error.message.split(":").slice(-1)[0]
          : "proxy_retry_failed";
    }
    await new Promise((resolveDelay) => setTimeout(resolveDelay, 2500));
  }
  throw new Error(`das_index_timeout:${lastCode}`);
}

async function confirmStamp(wallet, knownId, idempotencyKey, signature) {
  let lastError = "confirmation_failed";
  for (let attempt = 1; attempt <= 6; attempt += 1) {
    try {
      return await edge("ownership_confirm_stamp", {
        wallet_address: wallet,
        known_instance_id: knownId,
        idempotency_key: idempotencyKey,
        signature,
      });
    } catch (error) {
      lastError =
        error instanceof Error
          ? error.message.split(":").slice(-1)[0]
          : "confirmation_failed";
      if (
        ![
          "stamped_asset_not_verified",
          "transaction_not_confirmed",
          "ownership_confirm_stamp_failed",
        ].includes(lastError)
      )
        throw error;
      await new Promise((resolveDelay) => setTimeout(resolveDelay, 2000));
    }
  }
  throw new Error(`ownership_confirm_timeout:${lastError}`);
}

async function main() {
  assert(
    MODE === "prepare" || MODE === "execute",
    "CHAINBOUND_SMOKE_MODE_must_be_prepare_or_execute",
  );
  const wallet = env("CHAINBOUND_SMOKE_WALLET");
  publicKey(wallet, "CHAINBOUND_SMOKE_WALLET");
  validateLocalCredentials();
  const knownId = String(
    process.env.CHAINBOUND_SMOKE_KNOWN_ID || DEFAULT_KNOWN_ID,
  ).trim();
  assert(
    /^[0-9a-f]{8}-[0-9a-f-]{27}$/i.test(knownId),
    "invalid_known_instance_id",
  );
  assert(
    knownId === DEFAULT_KNOWN_ID,
    "unexpected_known_instance_id_refusing_unpinned_move",
  );
  const idempotencyKey = String(
    process.env.CHAINBOUND_SMOKE_IDEMPOTENCY_KEY || DEFAULT_IDEMPOTENCY_KEY,
  ).trim();
  assert(
    idempotencyKey === DEFAULT_IDEMPOTENCY_KEY,
    "unexpected_idempotency_key_refusing_unpinned_asset",
  );

  const connection = new Connection(RPC_URL, "confirmed");
  const genesisHash = await connection.getGenesisHash();
  assert(genesisHash === DEVNET_GENESIS, `rpc_not_devnet:${genesisHash}`);
  const collection = await connection.getAccountInfo(
    publicKey(MOVES, "moves_collection"),
    "confirmed",
  );
  assert(
    collection && collection.owner.toBase58() === CORE_PROGRAM,
    "moves_collection_owner_mismatch",
  );

  const payload = await prepareStamp(wallet, knownId, idempotencyKey);
  const inspected = await inspectPrepared(connection, payload, wallet, knownId);
  inspected.summary.genesisHash = genesisHash;
  inspected.summary.walletBalanceLamports = await connection.getBalance(
    publicKey(wallet, "wallet"),
    "confirmed",
  );

  if (MODE === "prepare") {
    const evidencePath = writeEvidence(
      "ownership-moves-smoke-mint-prepare",
      inspected.summary,
    );
    process.stdout.write(
      `${JSON.stringify({ ...inspected.summary, evidencePath }, null, 2)}\n`,
    );
    return;
  }

  assert(
    process.env.CHAINBOUND_SMOKE_APPROVAL === APPROVAL,
    `refusing_to_sign:set_CHAINBOUND_SMOKE_APPROVAL_${APPROVAL}`,
  );
  const signerPath = env("CHAINBOUND_SMOKE_WALLET_KEYPAIR");
  const signer = loadWallet(signerPath, wallet);
  const transaction = inspected.transaction;
  transaction.partialSign(signer);
  assert(
    transaction.verifySignatures(true),
    "fully_signed_transaction_verification_failed",
  );

  const signedSimulation = await simulateLegacyTransaction(connection, transaction, {
    commitment: "confirmed",
    sigVerify: true,
    replaceRecentBlockhash: false,
  });
  assert(
    signedSimulation.value.err === null,
    `signed_simulation_failed:${JSON.stringify(signedSimulation.value.err)}`,
  );

  const signature = await connection.sendRawTransaction(
    transaction.serialize(),
    {
      skipPreflight: false,
      preflightCommitment: "confirmed",
      maxRetries: 15,
    },
  );
  const confirmation = await connection.confirmTransaction(
    signature,
    "confirmed",
  );
  assert(
    confirmation.value.err === null,
    `transaction_failed:${JSON.stringify(confirmation.value.err)}`,
  );

  const status = await connection.getSignatureStatus(signature, {
    searchTransactionHistory: true,
  });
  const confirmedTransaction = await connection.getTransaction(signature, {
    commitment: "confirmed",
    maxSupportedTransactionVersion: 0,
  });
  assert(confirmedTransaction, "confirmed_transaction_not_found");
  const assetAccount = await connection.getAccountInfo(
    publicKey(payload.stamped_asset_address, "stamped_asset_address"),
    "confirmed",
  );
  assert(
    assetAccount && assetAccount.owner.toBase58() === CORE_PROGRAM,
    "minted_asset_owner_mismatch",
  );

  const indexed = await waitForDas(payload.stamped_asset_address, wallet);
  const databaseConfirmation = await confirmStamp(
    wallet,
    knownId,
    idempotencyKey,
    signature,
  );
  const evidence = {
    verified: true,
    operation: "one-devnet-moves-smoke-stamp",
    cluster: "devnet",
    genesisHash,
    wallet,
    knownInstanceId: knownId,
    idempotencyKey,
    collection: MOVES,
    expectedUpdateAuthority: CORE_AUTHORITY,
    asset: payload.stamped_asset_address,
    assetInterface: indexed.asset.interface,
    signature,
    slot: confirmedTransaction.slot,
    confirmationStatus:
      status && status.value ? status.value.confirmationStatus : "confirmed",
    signedSimulationUnitsConsumed: signedSimulation.value.unitsConsumed || null,
    proxyInventoryTotal: Number(indexed.group.total),
    proxyInventoryLimit: Number(indexed.group.limit),
    proxyContainsAsset: true,
    ownerMatched: indexed.asset.ownership.owner === wallet,
    collectionMatched: collectionMatches(indexed.asset),
    updateAuthorityMatched: authorityMatches(indexed.asset),
    ownershipDatabaseConfirmed: databaseConfirmation.confirmed === true,
    credentialMaterial: "REDACTED/not written",
  };
  const evidencePath = writeEvidence("ownership-moves-smoke-mint", evidence);
  process.stdout.write(
    `${JSON.stringify({ ...evidence, evidencePath }, null, 2)}\n`,
  );
}

main().catch((error) => {
  const message = error instanceof Error ? error.message : "unknown_error";
  process.stderr.write(`${message.replace(/[\r\n]+/g, " ")}\n`);
  process.exitCode = 1;
});
