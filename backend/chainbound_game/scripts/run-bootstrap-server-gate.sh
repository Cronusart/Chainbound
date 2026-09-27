#!/usr/bin/env bash
set -euo pipefail
set +x

# Operator-only Phase 1 bootstrap server gate for linked Supabase Devnet.
# It applies exactly one pinned SQL file and deploys exactly three named Edge
# Functions. It refuses to run until the new program bytes are live.

EXPECTED_APPROVAL="APPROVED_BOOTSTRAP_SERVER_GATE_DEVNET"
PROJECT_REF="jhzqcbrzkausrrtcavuu"
EXPECTED_GENESIS="EtWTRABZaYq6iMfeYKouRu166VU2xqa1wcaWoxPkrZBG"
PROGRAM_ID="9312ay7VrpvNYmyEx8TYCqhafbZ2Y5WGVbt6Qp772uUQ"
EXPECTED_ARTIFACT_BYTES="741248"
EXPECTED_ARTIFACT_SHA256="588249aecc1c57956a1acf525af31ffa1e756145f5e26b65724f45d499751e87"
MIGRATION_VERSION="20260920010000"
EXPECTED_MIGRATION_SHA256="a6a65cd7887979505929ffb8ff4f33920641ec18ba3c54d2ccd9d8e62dc8a1ef"
EXPECTED_PREPARE_SHA256="4de7215597ba314529111798b3a05394a1429218093353c4cc62a35927aa0a2d"
EXPECTED_CONFIRM_SHA256="28a1aee893dd3497e08fe0ace797c937658250e244ee0d187bfa892fbef4051f"
EXPECTED_STAMP_SHA256="f3903ef9489f04f47ad7956813e36c9ed6631b4354555334994d3024570c08d2"
RPC_URL="https://api.devnet.solana.com"

if [[ "${CHAINBOUND_SERVER_GATE_APPROVAL:-}" != "$EXPECTED_APPROVAL" ]]; then
  printf 'Refusing server gate: approval token is absent or incorrect.\n' >&2
  exit 64
fi

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
CHAIN_ROOT="$(cd -- "$SCRIPT_DIR/.." && pwd -P)"
PROJECT_ROOT="$(cd -- "$CHAIN_ROOT/../.." && pwd -P)"
SUPABASE_ROOT="$PROJECT_ROOT/Supabase"
MIGRATION="$SUPABASE_ROOT/migrations/${MIGRATION_VERSION}_ownership_bootstrap_ram_grant.sql"
# `npx` resolves to the Windows Node shim in this WSL setup. Run it from the
# repository root so its hardcoded `supabase/{migrations,functions}` discovery
# selects this project's top-level `Supabase` directory (Windows paths are
# case-insensitive), and pass a relative path it can open.
MIGRATION_CLI_PATH="Supabase/migrations/${MIGRATION_VERSION}_ownership_bootstrap_ram_grant.sql"
PREPARE_FUNCTION="$SUPABASE_ROOT/functions/ownership_prepare_devnet_smoke_grant/index.ts"
CONFIRM_FUNCTION="$SUPABASE_ROOT/functions/ownership_confirm_devnet_smoke_grant/index.ts"
STAMP_FUNCTION="$SUPABASE_ROOT/functions/ownership_confirm_stamp/index.ts"
ARTIFACT="$CHAIN_ROOT/target/deploy/chainbound_game.so"
SOLANA_BIN="${SOLANA_BIN:-$HOME/.local/share/solana/install/active_release/bin/solana}"
STAMP="$(date -u +%Y%m%dT%H%M%SZ)"
LOG_DIR="${CHAINBOUND_LOG_DIR:-$PROJECT_ROOT/Logs}"
LOG="$LOG_DIR/bootstrap-server-gate-$STAMP.log"
FUNCTIONS_FILE="$(mktemp)"
SCHEMA_FILE="$(mktemp)"
MIGRATION_HISTORY_FILE="$(mktemp)"
DEPLOYED_DUMP="$(mktemp)"
trap 'rm -f -- "$FUNCTIONS_FILE" "$SCHEMA_FILE" "$MIGRATION_HISTORY_FILE" "$DEPLOYED_DUMP"' EXIT

mkdir -p -- "$LOG_DIR"
exec > >(tee -a "$LOG") 2>&1

printf 'operation=bootstrap-server-gate-exact-only\ncluster=devnet\nproject_ref=%s\n' "$PROJECT_REF"
printf 'broad_db_push_included=false\nunrelated_migrations_included=false\n'

for required in "$MIGRATION" "$PREPARE_FUNCTION" "$CONFIRM_FUNCTION" "$STAMP_FUNCTION" "$ARTIFACT" "$SOLANA_BIN"; do
  [[ -f "$required" ]] || { printf 'missing_required_file=%s\n' "$required" >&2; exit 65; }
done
command -v node >/dev/null || { printf 'node_not_found\n' >&2; exit 65; }
command -v npx >/dev/null || { printf 'npx_not_found\n' >&2; exit 65; }

[[ "$(tr -d '\r\n' < "$SUPABASE_ROOT/.temp/project-ref")" == "$PROJECT_REF" ]] || { printf 'supabase_project_ref_mismatch\n' >&2; exit 66; }
[[ "$(sha256sum "$MIGRATION" | awk '{print $1}')" == "$EXPECTED_MIGRATION_SHA256" ]] || { printf 'migration_sha256_mismatch\n' >&2; exit 66; }
[[ "$(sha256sum "$PREPARE_FUNCTION" | awk '{print $1}')" == "$EXPECTED_PREPARE_SHA256" ]] || { printf 'prepare_function_sha256_mismatch\n' >&2; exit 66; }
[[ "$(sha256sum "$CONFIRM_FUNCTION" | awk '{print $1}')" == "$EXPECTED_CONFIRM_SHA256" ]] || { printf 'confirm_function_sha256_mismatch\n' >&2; exit 66; }
[[ "$(sha256sum "$STAMP_FUNCTION" | awk '{print $1}')" == "$EXPECTED_STAMP_SHA256" ]] || { printf 'stamp_function_sha256_mismatch\n' >&2; exit 66; }
[[ "$(stat -c '%s' "$ARTIFACT")" == "$EXPECTED_ARTIFACT_BYTES" ]] || { printf 'artifact_size_mismatch\n' >&2; exit 66; }
[[ "$(sha256sum "$ARTIFACT" | awk '{print $1}')" == "$EXPECTED_ARTIFACT_SHA256" ]] || { printf 'artifact_sha256_mismatch\n' >&2; exit 66; }
[[ "$(NO_DNA=1 "$SOLANA_BIN" genesis-hash --url "$RPC_URL")" == "$EXPECTED_GENESIS" ]] || { printf 'devnet_genesis_mismatch\n' >&2; exit 67; }
NO_DNA=1 "$SOLANA_BIN" program dump "$PROGRAM_ID" "$DEPLOYED_DUMP" --url "$RPC_URL"
[[ "$(stat -c '%s' "$DEPLOYED_DUMP")" == "$EXPECTED_ARTIFACT_BYTES" ]] || { printf 'live_program_size_mismatch\n' >&2; exit 67; }
[[ "$(sha256sum "$DEPLOYED_DUMP" | awk '{print $1}')" == "$EXPECTED_ARTIFACT_SHA256" ]] || { printf 'gate_b_not_verified_live_program_hash_mismatch\n' >&2; exit 67; }

cd "$PROJECT_ROOT"
npx supabase functions list --project-ref "$PROJECT_REF" --output json > "$FUNCTIONS_FILE"
npx supabase db query --linked --agent=yes --output json \
  "SELECT to_regclass('public.ownership_ram_grant_operations')::text AS grant_table, to_regprocedure('public.ownership_confirm_stamp_atomic(uuid,text,uuid,text,jsonb,numeric,integer)')::text AS atomic_stamp_function;" > "$SCHEMA_FILE"
node - "$FUNCTIONS_FILE" "$SCHEMA_FILE" <<'NODE'
const fs = require("fs");
const functions = JSON.parse(fs.readFileSync(process.argv[2], "utf8"));
const schema = JSON.parse(fs.readFileSync(process.argv[3], "utf8"));
const byName = new Map(functions.map((item) => [item.name, item]));
const row = schema.rows?.[0] || {};
process.stdout.write(JSON.stringify({
  preflightVerified: true,
  projectRef: "jhzqcbrzkausrrtcavuu",
  remoteBefore: {
    grantTable: row.grant_table ?? null,
    atomicStampFunction: row.atomic_stamp_function ?? null,
    prepareSmokeGrant: byName.has("ownership_prepare_devnet_smoke_grant") ? "ACTIVE" : "ABSENT",
    confirmSmokeGrant: byName.has("ownership_confirm_devnet_smoke_grant") ? "ACTIVE" : "ABSENT",
    confirmStampVersion: byName.get("ownership_confirm_stamp")?.version ?? null
  },
  exactMigrationVersion: "20260920010000",
  exactFunctions: [
    "ownership_prepare_devnet_smoke_grant",
    "ownership_confirm_devnet_smoke_grant",
    "ownership_confirm_stamp"
  ],
  liveProgram: {
    programId: "9312ay7VrpvNYmyEx8TYCqhafbZ2Y5WGVbt6Qp772uUQ",
    bytes: 741248,
    sha256: "588249aecc1c57956a1acf525af31ffa1e756145f5e26b65724f45d499751e87",
    exactArtifactMatch: true
  },
  broadDbPushIncluded: false,
  unrelatedMigrationsIncluded: false,
  mutationsPerformed: false
}, null, 2) + "\n");
NODE

if [[ "${CHAINBOUND_SERVER_GATE_VALIDATE_ONLY:-0}" == "1" ]]; then
  printf 'validate_only=true\nmutations_performed=false\nLOG=%s\n' "$LOG"
  exit 0
fi

printf 'server_gate_mutation_start_utc=%s\n' "$(date -u +%Y-%m-%dT%H:%M:%SZ)"
SCHEMA_STATE="$(node - "$SCHEMA_FILE" <<'NODE'
const fs = require("fs");
const schema = JSON.parse(fs.readFileSync(process.argv[2], "utf8"));
const row = schema.rows?.[0] || {};
const hasTable = row.grant_table === "ownership_ram_grant_operations";
const hasFunction = String(row.atomic_stamp_function || "").startsWith("ownership_confirm_stamp_atomic(");
if (hasTable !== hasFunction) throw new Error("partial_target_schema_state_refusing_resume");
process.stdout.write(hasTable ? "present" : "absent");
NODE
)"
if [[ "$SCHEMA_STATE" == "absent" ]]; then
  npx supabase db query --linked --agent=yes --output json --file "$MIGRATION_CLI_PATH"
  printf 'migration_sql_applied=true\n'
else
  printf 'migration_sql_already_present=true\nmigration_sql_reapplied=false\n'
fi
npx supabase db query --linked --agent=yes --output json \
  "SELECT to_regclass('public.ownership_ram_grant_operations')::text AS grant_table, to_regprocedure('public.ownership_confirm_stamp_atomic(uuid,text,uuid,text,jsonb,numeric,integer)')::text AS atomic_stamp_function;" > "$SCHEMA_FILE"
node - "$SCHEMA_FILE" <<'NODE'
const fs = require("fs");
const schema = JSON.parse(fs.readFileSync(process.argv[2], "utf8"));
const row = schema.rows?.[0] || {};
if (row.grant_table !== "ownership_ram_grant_operations") throw new Error("grant_table_missing_after_migration");
if (!String(row.atomic_stamp_function || "").startsWith("ownership_confirm_stamp_atomic(")) throw new Error("atomic_stamp_function_missing_after_migration");
NODE
npx supabase migration repair "$MIGRATION_VERSION" --linked --status applied --yes
npx supabase functions deploy ownership_prepare_devnet_smoke_grant --project-ref "$PROJECT_REF" --use-api
npx supabase functions deploy ownership_confirm_devnet_smoke_grant --project-ref "$PROJECT_REF" --use-api
npx supabase functions deploy ownership_confirm_stamp --project-ref "$PROJECT_REF" --use-api
npx supabase db query --linked --agent=yes --output json \
  "SELECT version FROM supabase_migrations.schema_migrations WHERE version = '${MIGRATION_VERSION}';" > "$MIGRATION_HISTORY_FILE"
npx supabase functions list --project-ref "$PROJECT_REF" --output json > "$FUNCTIONS_FILE"
node - "$FUNCTIONS_FILE" "$MIGRATION_HISTORY_FILE" <<'NODE'
const fs = require("fs");
const functions = JSON.parse(fs.readFileSync(process.argv[2], "utf8"));
const migrationHistory = JSON.parse(fs.readFileSync(process.argv[3], "utf8"));
const byName = new Map(functions.map((item) => [item.name, item]));
const migrationRows = migrationHistory.rows || [];
if (migrationRows.length !== 1 || String(migrationRows[0].version) !== "20260920010000") {
  throw new Error("migration_history_not_repaired");
}
for (const name of ["ownership_prepare_devnet_smoke_grant", "ownership_confirm_devnet_smoke_grant", "ownership_confirm_stamp"]) {
  const item = byName.get(name);
  if (!item || item.status !== "ACTIVE") throw new Error(`${name}_not_active`);
  if (item.verify_jwt !== true) throw new Error(`${name}_jwt_not_enforced`);
}
if (Number(byName.get("ownership_confirm_stamp").version) <= 6) throw new Error("ownership_confirm_stamp_not_redeployed");
process.stdout.write(JSON.stringify({
  verified: true,
  projectRef: "jhzqcbrzkausrrtcavuu",
  migrationVersionApplied: "20260920010000",
  migrationHistoryVerified: true,
  functions: [
    { name: "ownership_prepare_devnet_smoke_grant", version: byName.get("ownership_prepare_devnet_smoke_grant").version, verifyJwt: true },
    { name: "ownership_confirm_devnet_smoke_grant", version: byName.get("ownership_confirm_devnet_smoke_grant").version, verifyJwt: true },
    { name: "ownership_confirm_stamp", version: byName.get("ownership_confirm_stamp").version, verifyJwt: true }
  ],
  broadDbPushIncluded: false,
  unrelatedMigrationsIncluded: false
}, null, 2) + "\n");
NODE
printf 'server_gate_mutation_complete_utc=%s\nLOG=%s\n' "$(date -u +%Y-%m-%dT%H:%M:%SZ)" "$LOG"
