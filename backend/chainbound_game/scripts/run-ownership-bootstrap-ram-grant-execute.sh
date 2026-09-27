#!/usr/bin/env bash
set -euo pipefail
set +x
umask 077

# Operator-only execute gate for exactly one 100-RAM Devnet smoke grant.
# This wrapper cannot proceed without the separately approved token, proves the
# local signer pubkey before loading it, and never prints credential/key bytes.

EXPECTED_APPROVAL="APPROVED_ONE_DEVNET_SMOKE_RAM_GRANT_100"
EXPECTED_WALLET="H68eGMwT3UCigd9qf4u1VLyqQh6jq57HD4wLWaQrbBWq"

cleanup() {
  unset CHAINBOUND_SUPABASE_ANON_KEY
  unset CHAINBOUND_USER_ACCESS_TOKEN
  unset CHAINBOUND_SMOKE_WALLET_KEYPAIR
  unset CHAINBOUND_GRANT_MODE
}
trap cleanup EXIT HUP INT TERM

if [[ "${CHAINBOUND_GRANT_APPROVAL:-}" != "$EXPECTED_APPROVAL" ]]; then
  printf 'Refusing RAM grant: approval token is absent or incorrect.\n' >&2
  exit 64
fi

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
CLIENT="$SCRIPT_DIR/ownership-bootstrap-ram-grant.cjs"
[[ -f "$CLIENT" ]] || { printf 'missing_ram_grant_client\n' >&2; exit 65; }
command -v node >/dev/null || { printf 'node_not_found\n' >&2; exit 65; }

KEYPAIR_INPUT="${CHAINBOUND_SMOKE_WALLET_KEYPAIR:-}"
if [[ -z "$KEYPAIR_INPUT" ]]; then
  read -r -s -p "Local smoke-wallet keypair path (hidden; never logged): " KEYPAIR_INPUT
  printf '\n'
fi
[[ -n "$KEYPAIR_INPUT" ]] || { printf 'missing_local_keypair_path\n' >&2; exit 65; }
if [[ "$KEYPAIR_INPUT" == "~/"* ]]; then
  KEYPAIR_INPUT="$HOME/${KEYPAIR_INPUT:2}"
fi
KEYPAIR_PATH="$(realpath -e -- "$KEYPAIR_INPUT")"
[[ -f "$KEYPAIR_PATH" ]] || { printf 'local_keypair_not_a_file\n' >&2; exit 65; }

SOLANA_KEYGEN_BIN="${SOLANA_KEYGEN_BIN:-$HOME/.local/share/solana/install/active_release/bin/solana-keygen}"
if [[ ! -x "$SOLANA_KEYGEN_BIN" ]]; then
  SOLANA_KEYGEN_BIN="$(command -v solana-keygen || true)"
fi
[[ -n "$SOLANA_KEYGEN_BIN" && -x "$SOLANA_KEYGEN_BIN" ]] || { printf 'solana_keygen_not_found\n' >&2; exit 65; }

ACTUAL_WALLET="$(NO_DNA=1 "$SOLANA_KEYGEN_BIN" pubkey "$KEYPAIR_PATH")"
if [[ "$ACTUAL_WALLET" != "$EXPECTED_WALLET" ]]; then
  printf 'smoke_wallet_pubkey_mismatch expected=%s actual=%s\n' "$EXPECTED_WALLET" "$ACTUAL_WALLET" >&2
  exit 66
fi

read -r -s -p "Supabase anon key: " CHAINBOUND_SUPABASE_ANON_KEY
printf '\n'
read -r -s -p "Authenticated user access token: " CHAINBOUND_USER_ACCESS_TOKEN
printf '\n'

[[ -n "$CHAINBOUND_SUPABASE_ANON_KEY" ]] || { printf 'missing_input:Supabase anon key\n' >&2; exit 65; }
[[ -n "$CHAINBOUND_USER_ACCESS_TOKEN" ]] || { printf 'missing_input:Authenticated user access token\n' >&2; exit 65; }

export CHAINBOUND_SUPABASE_ANON_KEY
export CHAINBOUND_USER_ACCESS_TOKEN
export CHAINBOUND_SMOKE_WALLET_KEYPAIR="$KEYPAIR_PATH"
export CHAINBOUND_GRANT_MODE=execute

printf 'operator_sign_gate=one-devnet-smoke-ram-grant\n'
printf 'cluster=devnet\nwallet=%s\namount=100\nresult_authority_cosigner=FaAegH2tBCyDzxXcXyz64qEFWSrvpX8M6SSFNx1Pt5KF\n' "$EXPECTED_WALLET"
node "$CLIENT"
