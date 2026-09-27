#!/usr/bin/env bash
set -euo pipefail
set +x
umask 077

# Approval-gated server-only hotfix. Deploys exactly the two confirmation
# functions affected by the transaction-signature format validator. It applies
# no migrations, changes no secrets/config/economics, and performs no chain tx.

EXPECTED_APPROVAL="APPROVED_DEVNET_CONFIRM_SIGNATURE_VALIDATOR_HOTFIX"
PROJECT_REF="jhzqcbrzkausrrtcavuu"
EXPECTED_SHARED_SHA256="0c10f2da79c3354f6dd0e5910f7268eada42de0a1c91e37f5aca1660ec44d51e"
EXPECTED_GRANT_CONFIRM_SHA256="73591e34beb75babb84c56f3cf25fe8412f8579b4fedc96b53f059e721d3e677"
EXPECTED_STAMP_CONFIRM_SHA256="cbb8d07316d0ca90745868f3a616241689476270d4d7ab9892f2741a96ff3c39"
EXPECTED_TEST_SHA256="e8e6a203704cd2f5d53e69a4b81fe5b05bad154776ed783341f62785924ce88d"
EXPECTED_GRANT_CONFIRM_VERSION="2"
EXPECTED_STAMP_CONFIRM_VERSION="8"
VALIDATE_ONLY="${CHAINBOUND_HOTFIX_VALIDATE_ONLY:-0}"
DENO_BIN="${DENO_BIN:-$(command -v deno || true)}"

if [[ "$VALIDATE_ONLY" != "1" && "${CHAINBOUND_HOTFIX_APPROVAL:-}" != "$EXPECTED_APPROVAL" ]]; then
  printf 'Refusing confirmation hotfix: approval token is absent or incorrect.\n' >&2
  exit 64
fi

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
CHAIN_ROOT="$(cd -- "$SCRIPT_DIR/.." && pwd -P)"
PROJECT_ROOT="$(cd -- "$CHAIN_ROOT/../.." && pwd -P)"
SUPABASE_ROOT="$PROJECT_ROOT/Supabase"
SHARED="$SUPABASE_ROOT/functions/_shared/ownership_spine.ts"
GRANT_CONFIRM="$SUPABASE_ROOT/functions/ownership_confirm_devnet_smoke_grant/index.ts"
STAMP_CONFIRM="$SUPABASE_ROOT/functions/ownership_confirm_stamp/index.ts"
TEST="$SUPABASE_ROOT/functions/_shared/ownership_spine_test.ts"
STATE_VERIFIER="$SCRIPT_DIR/verify-ownership-bootstrap-ram-grant.cjs"
STAMP="$(date -u +%Y%m%dT%H%M%SZ)"
LOG_DIR="${CHAINBOUND_LOG_DIR:-$PROJECT_ROOT/Logs}"
LOG="$LOG_DIR/ownership-confirm-signature-hotfix-$STAMP.log"
FUNCTIONS_BEFORE="$(mktemp)"
FUNCTIONS_AFTER="$(mktemp)"
GRANT_ROW="$(mktemp)"
trap 'rm -f -- "$FUNCTIONS_BEFORE" "$FUNCTIONS_AFTER" "$GRANT_ROW"' EXIT

mkdir -p -- "$LOG_DIR"
exec > >(tee -a "$LOG") 2>&1

printf 'operation=devnet-confirm-signature-validator-hotfix\n'
printf 'project_ref=%s\nfunctions=ownership_confirm_devnet_smoke_grant,ownership_confirm_stamp\n' "$PROJECT_REF"
printf 'migrations_included=false\nsecret_changes_included=false\nchain_transactions_included=false\n'

for required in "$SHARED" "$GRANT_CONFIRM" "$STAMP_CONFIRM" "$TEST" "$STATE_VERIFIER"; do
  [[ -f "$required" ]] || { printf 'missing_required_file=%s\n' "$required" >&2; exit 65; }
done
[[ -n "$DENO_BIN" && -x "$DENO_BIN" ]] || { printf 'deno_not_found\n' >&2; exit 65; }
command -v node >/dev/null || { printf 'node_not_found\n' >&2; exit 65; }
command -v npx >/dev/null || { printf 'npx_not_found\n' >&2; exit 65; }

[[ "$(tr -d '\r\n' < "$SUPABASE_ROOT/.temp/project-ref")" == "$PROJECT_REF" ]] || { printf 'supabase_project_ref_mismatch\n' >&2; exit 66; }
[[ "$(sha256sum "$SHARED" | awk '{print $1}')" == "$EXPECTED_SHARED_SHA256" ]] || { printf 'shared_source_sha256_mismatch\n' >&2; exit 66; }
[[ "$(sha256sum "$GRANT_CONFIRM" | awk '{print $1}')" == "$EXPECTED_GRANT_CONFIRM_SHA256" ]] || { printf 'grant_confirm_source_sha256_mismatch\n' >&2; exit 66; }
[[ "$(sha256sum "$STAMP_CONFIRM" | awk '{print $1}')" == "$EXPECTED_STAMP_CONFIRM_SHA256" ]] || { printf 'stamp_confirm_source_sha256_mismatch\n' >&2; exit 66; }
[[ "$(sha256sum "$TEST" | awk '{print $1}')" == "$EXPECTED_TEST_SHA256" ]] || { printf 'test_source_sha256_mismatch\n' >&2; exit 66; }

cd "$PROJECT_ROOT"
"$DENO_BIN" test --allow-env --allow-read Supabase/functions/_shared/ownership_spine_test.ts
node "$STATE_VERIFIER"
npx supabase functions list --project-ref "$PROJECT_REF" --output json > "$FUNCTIONS_BEFORE"
npx supabase db query --linked --agent=yes --output json \
  "SELECT idempotency_key, status, signature, amount, balance_after FROM public.ownership_ram_grant_operations WHERE wallet_address = 'H68eGMwT3UCigd9qf4u1VLyqQh6jq57HD4wLWaQrbBWq' AND idempotency_key = 'devnet-smoke-grant-20260920-1';" > "$GRANT_ROW"
node - "$FUNCTIONS_BEFORE" "$GRANT_ROW" "$EXPECTED_GRANT_CONFIRM_VERSION" "$EXPECTED_STAMP_CONFIRM_VERSION" <<'NODE'
const fs = require("fs");
const rows = JSON.parse(fs.readFileSync(process.argv[2], "utf8"));
const grantQuery = JSON.parse(fs.readFileSync(process.argv[3], "utf8"));
const byName = new Map(rows.map((row) => [row.name, row]));
for (const [name, expectedVersion] of [
  ["ownership_confirm_devnet_smoke_grant", Number(process.argv[4])],
  ["ownership_confirm_stamp", Number(process.argv[5])],
]) {
  const row = byName.get(name);
  if (!row || row.status !== "ACTIVE" || row.verify_jwt !== true) throw new Error(`${name}_preflight_invalid`);
  if (Number(row.version) !== expectedVersion) throw new Error(`${name}_unexpected_remote_version`);
}
const grantRows = grantQuery.rows || [];
if (grantRows.length !== 1) throw new Error("expected_exactly_one_grant_operation");
const grant = grantRows[0];
if (grant.idempotency_key !== "devnet-smoke-grant-20260920-1" || grant.status !== "prepared") {
  throw new Error("grant_operation_not_prepared");
}
if (grant.signature !== null || Number(grant.amount) !== 100 || grant.balance_after !== null) {
  throw new Error("grant_operation_unexpected_values");
}
process.stdout.write(JSON.stringify({
  preflightVerified: true,
  remoteBefore: {
    ownership_confirm_devnet_smoke_grant: Number(byName.get("ownership_confirm_devnet_smoke_grant").version),
    ownership_confirm_stamp: Number(byName.get("ownership_confirm_stamp").version),
  },
  databaseBefore: {
    idempotencyKey: grant.idempotency_key,
    status: grant.status,
    signature: null,
    amount: Number(grant.amount),
    balanceAfter: null,
  },
  migrationsIncluded: false,
  chainTransactionsIncluded: false,
  mutationsPerformed: false,
}, null, 2) + "\n");
NODE

if [[ "$VALIDATE_ONLY" == "1" ]]; then
  printf 'validate_only=true\nmutations_performed=false\nLOG=%s\n' "$LOG"
  exit 0
fi

printf 'hotfix_mutation_start_utc=%s\n' "$(date -u +%Y-%m-%dT%H:%M:%SZ)"
npx supabase functions deploy ownership_confirm_devnet_smoke_grant --project-ref "$PROJECT_REF" --use-api
npx supabase functions deploy ownership_confirm_stamp --project-ref "$PROJECT_REF" --use-api
npx supabase functions list --project-ref "$PROJECT_REF" --output json > "$FUNCTIONS_AFTER"
node - "$FUNCTIONS_AFTER" "$EXPECTED_GRANT_CONFIRM_VERSION" "$EXPECTED_STAMP_CONFIRM_VERSION" <<'NODE'
const fs = require("fs");
const rows = JSON.parse(fs.readFileSync(process.argv[2], "utf8"));
const byName = new Map(rows.map((row) => [row.name, row]));
for (const [name, beforeVersion] of [
  ["ownership_confirm_devnet_smoke_grant", Number(process.argv[3])],
  ["ownership_confirm_stamp", Number(process.argv[4])],
]) {
  const row = byName.get(name);
  if (!row || row.status !== "ACTIVE" || row.verify_jwt !== true) throw new Error(`${name}_postdeploy_invalid`);
  if (Number(row.version) <= beforeVersion) throw new Error(`${name}_version_not_advanced`);
}
process.stdout.write(JSON.stringify({
  verified: true,
  functions: [
    { name: "ownership_confirm_devnet_smoke_grant", version: Number(byName.get("ownership_confirm_devnet_smoke_grant").version), verifyJwt: true },
    { name: "ownership_confirm_stamp", version: Number(byName.get("ownership_confirm_stamp").version), verifyJwt: true },
  ],
  migrationsApplied: false,
  secretsChanged: false,
  chainTransactionsPerformed: false,
}, null, 2) + "\n");
NODE
printf 'hotfix_mutation_complete_utc=%s\nLOG=%s\n' "$(date -u +%Y-%m-%dT%H:%M:%SZ)" "$LOG"
