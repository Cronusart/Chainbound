#!/usr/bin/env bash
set -euo pipefail

# Interactive operator wrapper for the keyless prepare/review gate.
# Secret-bearing values are read silently, exported only to the child process,
# and removed from this shell when the wrapper exits. This script never signs
# or sends a transaction.
set +x

EXPECTED_WALLET="H68eGMwT3UCigd9qf4u1VLyqQh6jq57HD4wLWaQrbBWq"
EXPECTED_KNOWN_ID="9293fc49-b5d0-48d9-b071-504bdc9b3b00"
EXPECTED_IDEMPOTENCY_KEY="moves-smoke-mint-20260919-1"

cleanup() {
  unset CHAINBOUND_SUPABASE_ANON_KEY
  unset CHAINBOUND_USER_ACCESS_TOKEN
  unset CHAINBOUND_SMOKE_WALLET
  unset CHAINBOUND_SMOKE_KNOWN_ID
  unset CHAINBOUND_SMOKE_IDEMPOTENCY_KEY
  unset CHAINBOUND_SMOKE_MODE
}
trap cleanup EXIT HUP INT TERM

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd -- "${script_dir}/../../.." && pwd)"
cd -- "${repo_root}"

read -r -s -p "Supabase anon key: " CHAINBOUND_SUPABASE_ANON_KEY
printf '\n'
read -r -s -p "Authenticated user access token: " CHAINBOUND_USER_ACCESS_TOKEN
printf '\n'
CHAINBOUND_SMOKE_WALLET="$EXPECTED_WALLET"
CHAINBOUND_SMOKE_KNOWN_ID="$EXPECTED_KNOWN_ID"
CHAINBOUND_SMOKE_IDEMPOTENCY_KEY="$EXPECTED_IDEMPOTENCY_KEY"

if [[ -z "${CHAINBOUND_SUPABASE_ANON_KEY}" ]]; then
  printf '%s\n' "missing_input:Supabase anon key" >&2
  exit 1
fi
if [[ -z "${CHAINBOUND_USER_ACCESS_TOKEN}" ]]; then
  printf '%s\n' "missing_input:Authenticated user access token" >&2
  exit 1
fi
export CHAINBOUND_SUPABASE_ANON_KEY
export CHAINBOUND_USER_ACCESS_TOKEN
export CHAINBOUND_SMOKE_WALLET
export CHAINBOUND_SMOKE_KNOWN_ID
export CHAINBOUND_SMOKE_IDEMPOTENCY_KEY
export CHAINBOUND_SMOKE_MODE=prepare

printf 'operator_review_gate=one-devnet-moves-smoke-prepare\n'
printf 'cluster=devnet\nwallet=%s\nknown_instance_id=%s\nidempotency_key=%s\nknown_ram_cost=5\nfirst_stamp_ram_cost=75\ntotal_ram_cost=80\nexpected_ram_after=20\nsigning=false\nsending=false\n' \
  "$EXPECTED_WALLET" "$EXPECTED_KNOWN_ID" "$EXPECTED_IDEMPOTENCY_KEY"
node backend/chainbound_game/scripts/ownership-moves-smoke-mint.cjs
