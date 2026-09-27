#!/usr/bin/env bash
set -euo pipefail
set +x
umask 077

# Keyless prepare/review gate for exactly one 100-RAM Devnet smoke grant.
# Credentials are read silently, exist only in this process environment, and
# are removed on exit. The delegated client validates and simulates but cannot
# sign or send in prepare mode.

cleanup() {
  unset CHAINBOUND_SUPABASE_ANON_KEY
  unset CHAINBOUND_USER_ACCESS_TOKEN
  unset CHAINBOUND_GRANT_MODE
}
trap cleanup EXIT HUP INT TERM

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
export CHAINBOUND_GRANT_MODE=prepare

printf 'operator_review_gate=one-devnet-smoke-ram-grant-prepare\n'
printf 'cluster=devnet\nwallet=H68eGMwT3UCigd9qf4u1VLyqQh6jq57HD4wLWaQrbBWq\namount=100\nsigning=false\nsending=false\n'
node "$CLIENT"
