#!/usr/bin/env bash
set -euo pipefail
set +x
umask 077

# Recovery-only path for the narrow case where the approved 100-RAM grant is
# already verified on-chain but Edge confirmation was interrupted. It never
# loads a keypair, signs, or sends. It validates the exact receipt transaction
# before completing only the idempotent server confirmation.

EXPECTED_APPROVAL="APPROVED_ONE_DEVNET_SMOKE_RAM_GRANT_100"

cleanup() {
  unset CHAINBOUND_SUPABASE_ANON_KEY
  unset CHAINBOUND_USER_ACCESS_TOKEN
  unset CHAINBOUND_GRANT_MODE
  unset CHAINBOUND_GRANT_SIGNATURE
}
trap cleanup EXIT HUP INT TERM

if [[ "${CHAINBOUND_GRANT_APPROVAL:-}" != "$EXPECTED_APPROVAL" ]]; then
  printf 'Refusing RAM-grant recovery: approval token is absent or incorrect.\n' >&2
  exit 64
fi

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
CLIENT="$SCRIPT_DIR/ownership-bootstrap-ram-grant.cjs"
[[ -f "$CLIENT" ]] || { printf 'missing_ram_grant_client\n' >&2; exit 65; }
command -v node >/dev/null || { printf 'node_not_found\n' >&2; exit 65; }

read -r -s -p "Supabase anon key: " CHAINBOUND_SUPABASE_ANON_KEY
printf '\n'
read -r -s -p "Authenticated user access token: " CHAINBOUND_USER_ACCESS_TOKEN
printf '\n'

[[ -n "$CHAINBOUND_SUPABASE_ANON_KEY" ]] || { printf 'missing_input:Supabase anon key\n' >&2; exit 65; }
[[ -n "$CHAINBOUND_USER_ACCESS_TOKEN" ]] || { printf 'missing_input:Authenticated user access token\n' >&2; exit 65; }

export CHAINBOUND_SUPABASE_ANON_KEY
export CHAINBOUND_USER_ACCESS_TOKEN
export CHAINBOUND_GRANT_MODE=recover

printf 'operator_recovery_gate=one-devnet-smoke-ram-grant\n'
printf 'cluster=devnet\nwallet=H68eGMwT3UCigd9qf4u1VLyqQh6jq57HD4wLWaQrbBWq\namount=100\nsigning=false\nsending=false\n'
node "$CLIENT"
