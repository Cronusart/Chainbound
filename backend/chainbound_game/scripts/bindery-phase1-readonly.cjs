#!/usr/bin/env node
"use strict";

const crypto = require("crypto");
const fs = require("fs");
const path = require("path");
const { execFileSync } = require("child_process");
const { Connection, PublicKey } = require("@solana/web3.js");

const PROJECT_ROOT = path.resolve(__dirname, "../../..");
const MANIFEST_PATH = path.join(
  PROJECT_ROOT,
  "Docs/Specs/Economy/manifests/bindery-devnet-baseline-v2.json",
);
const PHASE1_DEPLOYMENT_PATH = path.join(
  PROJECT_ROOT,
  "Docs/Specs/Economy/manifests/bindery-phase1-deployment-v1.json",
);
const PHASE1_MIGRATION = "20261016045210";
const REQUIRED_SECRET_NAMES = [
  "CHAINBOUND_CORE_ARCHIVES_COLLECTION",
  "CHAINBOUND_CORE_COSMETICS_COLLECTION",
  "CHAINBOUND_CORE_COSMETICS_UPDATE_AUTHORITIES",
  "CHAINBOUND_CORE_COVERS_COLLECTION",
  "CHAINBOUND_CORE_MOVES_COLLECTION",
  "CHAINBOUND_CORE_MOVES_UPDATE_AUTHORITY",
  "CHAINBOUND_GAME_PROGRAM_ID",
  "CHAINBOUND_OWNERSHIP_BIND_RAM_COST",
  "CHAINBOUND_OWNERSHIP_GENERATOR_VERSION",
  "CHAINBOUND_OWNERSHIP_METADATA_URI_BASE",
  "CHAINBOUND_OWNERSHIP_MODIFIER_POOL_HASH",
  "CHAINBOUND_OWNERSHIP_SCHEMA_VERSION",
  "CHAINBOUND_OWNERSHIP_STAMP_RAM_COST",
  "CHAINBOUND_OWNERSHIP_TICKET_TTL_SECONDS",
  "CHAINBOUND_RESULT_AUTHORITY_SECRET",
  "CHAINBOUND_SOLANA_CLUSTER",
  "CHAINBOUND_SOLANA_RPC_ENDPOINT",
  "HELIUS_DAS_URL",
];

function invariant(value, message) {
  if (!value) throw new Error(message);
}

function manifest() {
  return JSON.parse(fs.readFileSync(MANIFEST_PATH, "utf8"));
}

function phase1Deployment() {
  return JSON.parse(fs.readFileSync(PHASE1_DEPLOYMENT_PATH, "utf8"));
}

function supabaseJson(args) {
  return JSON.parse(
    execFileSync("supabase", args, {
      cwd: path.join(PROJECT_ROOT, "Supabase"),
      encoding: "utf8",
      env: { ...process.env, SUPABASE_DISABLE_TELEMETRY: "1" },
      windowsHide: true,
    }),
  );
}

async function verifyLive() {
  const baseline = manifest();
  const rpcUrl = process.env.CHAINBOUND_SOLANA_RPC_URL || "https://api.devnet.solana.com";
  const connection = new Connection(rpcUrl, "confirmed");
  const programKey = new PublicKey(baseline.program.programId);
  const programDataKey = new PublicKey(baseline.program.programData);
  const ownershipConfigKey = new PublicKey(baseline.ownershipConfig.address);
  const [genesisHash, program, programData, ownershipConfig] = await Promise.all([
    connection.getGenesisHash(),
    connection.getAccountInfo(programKey, "confirmed"),
    connection.getAccountInfo(programDataKey, "confirmed"),
    connection.getAccountInfo(ownershipConfigKey, "confirmed"),
  ]);
  invariant(genesisHash === baseline.cluster.genesisHash, "RPC genesis changed");
  invariant(program?.owner.toBase58() === baseline.program.loader, "program owner changed");
  invariant(programData?.owner.toBase58() === baseline.program.loader, "ProgramData owner changed");
  invariant(ownershipConfig?.owner.toBase58() === baseline.program.programId, "OwnershipConfig owner changed");
  invariant(ownershipConfig?.data.length === 183, "OwnershipConfig layout changed");

  const lastDeploymentSlot = Number(programData.data.readBigUInt64LE(4));
  const payload = programData.data.subarray(45);
  const payloadSha256 = crypto.createHash("sha256").update(payload).digest("hex");
  invariant(lastDeploymentSlot === baseline.program.live.lastDeploymentSlot, "deployment slot changed");
  invariant(payload.length === baseline.program.live.payloadBytes, "program payload size changed");
  invariant(payloadSha256 === baseline.program.live.payloadSha256, "program payload hash changed");
  const collections = [
    new PublicKey(ownershipConfig.data.subarray(10, 42)).toBase58(),
    new PublicKey(ownershipConfig.data.subarray(42, 74)).toBase58(),
    new PublicKey(ownershipConfig.data.subarray(74, 106)).toBase58(),
    new PublicKey(ownershipConfig.data.subarray(106, 138)).toBase58(),
  ];
  const expectedCollections = [
    baseline.ownershipConfig.movesCollection,
    baseline.ownershipConfig.archivesCollection,
    baseline.ownershipConfig.coversCollection,
    baseline.ownershipConfig.cosmeticsCollection,
  ];
  invariant(collections.join("|") === expectedCollections.join("|"), "ownership collections changed");
  return {
    verified: true,
    scope: "read_only_live_devnet",
    genesisHash,
    programId: baseline.program.programId,
    lastDeploymentSlot,
    payloadBytes: payload.length,
    payloadSha256,
    ownershipCollectionsUnchanged: true,
    mutationsPerformed: false,
  };
}

function verifySupabase() {
  const baseline = manifest();
  const deployment = phase1Deployment();
  const projectRef = baseline.edge.remoteDeployment.projectRef;
  invariant(projectRef === deployment.environment.supabaseProjectRef, "Phase 1 project ref changed");
  const functions = supabaseJson(["functions", "list", "--project-ref", projectRef, "--output", "json"]);
  const bySlug = new Map(functions.map((item) => [item.slug, item]));
  const verifiedFunctions = {};
  for (const [slug, expected] of Object.entries(deployment.functions)) {
    const actual = bySlug.get(slug);
    invariant(actual?.status === "ACTIVE", `${slug} is not ACTIVE`);
    invariant(actual.version === expected.version, `${slug} version changed`);
    invariant(actual.ezbr_sha256 === expected.ezbrSha256, `${slug} bundle hash changed`);
    verifiedFunctions[slug] = { status: actual.status, version: actual.version, ezbrSha256: actual.ezbr_sha256 };
  }
  const migrationResult = supabaseJson(["migration", "list", "--linked"]);
  const remoteVersions = (migrationResult.migrations || []).map((item) => item.remote).filter(Boolean).sort();
  invariant(
    remoteVersions.length === deployment.migration.remoteMigrationCount,
    "remote migration count changed",
  );
  invariant(remoteVersions.at(-1) === deployment.migration.remoteLatestVersion, "remote migration head changed");
  invariant(deployment.migration.remoteLatestVersion === PHASE1_MIGRATION, "Phase 1 evidence head is invalid");
  const localVersions = fs.readdirSync(path.join(PROJECT_ROOT, "Supabase/migrations"))
    .filter((name) => /^\d{14}_.+\.sql$/.test(name)).map((name) => name.slice(0, 14)).sort();
  const pendingLocalVersions = localVersions.filter((version) => !remoteVersions.includes(version));
  invariant(pendingLocalVersions.length === 0, `unexpected pending migrations: ${pendingLocalVersions.join(",")}`);
  const schemaResult = supabaseJson([
    "db",
    "query",
    "--linked",
    "--output-format",
    "json",
    `select jsonb_build_object(
      'phase1_columns', (select count(*) from information_schema.columns
        where table_schema = 'public' and table_name = 'ownership_bind_sources'
          and column_name in ('source_version','dossier_provenance','dossier_snapshot_hash',
            'archive_metadata_hash','expires_at','revoked_at','revoked_reason','created_by',
            'last_state_reason','last_inspected_at','last_inspection_digest','last_inspection_trusted')),
      'phase1_triggers', (select count(*) from pg_trigger
        where tgrelid = 'public.ownership_bind_sources'::regclass
          and tgname in ('trg_ownership_bind_source_immutable','trg_ownership_bind_source_audit')),
      'anon_create_execute', has_function_privilege('anon',
        'public.ownership_create_bind_source_service(uuid,uuid,text,text,numeric,text,text,smallint,text,text,text,jsonb,text,timestamptz,text)', 'EXECUTE'),
      'authenticated_create_execute', has_function_privilege('authenticated',
        'public.ownership_create_bind_source_service(uuid,uuid,text,text,numeric,text,text,smallint,text,text,text,jsonb,text,timestamptz,text)', 'EXECUTE'),
      'service_create_execute', has_function_privilege('service_role',
        'public.ownership_create_bind_source_service(uuid,uuid,text,text,numeric,text,text,smallint,text,text,text,jsonb,text,timestamptz,text)', 'EXECUTE'),
      'bind_sources', (select count(*) from public.ownership_bind_sources),
      'enabled_sources', (select count(*) from public.ownership_bind_sources where enabled = true),
      'trusted_sources', (select count(*) from public.ownership_bind_sources where last_inspection_trusted = true),
      'bindings', (select count(*) from public.ownership_chainling_bindings),
      'chainlings', (select count(*) from public.chainlings)
    ) as phase1_state;`,
  ]);
  const phase1State = schemaResult.rows?.[0]?.phase1_state;
  invariant(
    phase1State?.phase1_columns === deployment.databaseBoundary.phase1SourceColumns,
    "Phase 1 source columns changed",
  );
  invariant(
    phase1State?.phase1_triggers === deployment.databaseBoundary.phase1SourceTriggers,
    "Phase 1 source triggers changed",
  );
  invariant(
    phase1State?.anon_create_execute === deployment.databaseBoundary.anonCanCreateSource,
    "anon source-create privilege changed",
  );
  invariant(
    phase1State?.authenticated_create_execute === deployment.databaseBoundary.authenticatedCanCreateSource,
    "authenticated source-create privilege changed",
  );
  invariant(
    phase1State?.service_create_execute === deployment.databaseBoundary.serviceRoleCanCreateSource,
    "service source-create privilege changed",
  );
  const secrets = supabaseJson(["secrets", "list", "--project-ref", projectRef, "--output", "json"]);
  const secretNames = new Set(secrets.map((secret) => secret.name));
  const missingSecretNames = REQUIRED_SECRET_NAMES.filter((name) => !secretNames.has(name));
  invariant(missingSecretNames.length === 0, `missing required secrets: ${missingSecretNames.join(",")}`);
  return {
    verified: true,
    scope: "read_only_supabase_devnet",
    projectRef,
    evidenceCapturedAtUtc: deployment.capturedAtUtc,
    functions: verifiedFunctions,
    remoteMigrationCount: remoteVersions.length,
    remoteLatestMigrationVersion: remoteVersions.at(-1),
    pendingLocalVersions,
    phase1State,
    requiredSecretNamesPresent: REQUIRED_SECRET_NAMES.length,
    secretValuesRead: false,
    mutationsPerformed: false,
  };
}

async function main() {
  const command = process.argv[2];
  const result = command === "verify-live"
    ? await verifyLive()
    : command === "verify-supabase"
    ? verifySupabase()
    : null;
  invariant(result, "Usage: bindery-phase1-readonly.cjs <verify-live|verify-supabase>");
  process.stdout.write(`${JSON.stringify(result, null, 2)}\n`);
}

main().catch((error) => {
  console.error(error instanceof Error ? error.message : error);
  process.exitCode = 1;
});
