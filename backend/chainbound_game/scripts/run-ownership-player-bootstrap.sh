#!/usr/bin/env bash
set -euo pipefail
set +x
umask 077

EXPECTED_APPROVAL="APPROVED_DEVNET_SMOKE_PLAYER_BOOTSTRAP"
EXPECTED_WALLET="H68eGMwT3UCigd9qf4u1VLyqQh6jq57HD4wLWaQrbBWq"

if [[ "${CHAINBOUND_BOOTSTRAP_APPROVAL:-}" != "$EXPECTED_APPROVAL" ]]; then
  printf 'Refusing player bootstrap: approval token is absent or incorrect.\n' >&2
  exit 64
fi

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
CLIENT="$SCRIPT_DIR/ownership-bootstrap-player.cjs"
[[ -f "$CLIENT" ]] || { printf 'missing_bootstrap_client\n' >&2; exit 65; }
command -v node >/dev/null || { printf 'node_not_found\n' >&2; exit 65; }

if [[ "${CHAINBOUND_BOOTSTRAP_WRAPPER_VALIDATE_ONLY:-0}" == "1" ]]; then
  CHAINBOUND_BOOTSTRAP_MODE=prepare node "$CLIENT"
  exit 0
fi

KEYPAIR_INPUT="${CHAINBOUND_SMOKE_WALLET_KEYPAIR:-}"
if [[ -z "$KEYPAIR_INPUT" ]]; then
  read -rsp "Local smoke-wallet keypair path (hidden; never logged): " KEYPAIR_INPUT
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
[[ "$ACTUAL_WALLET" == "$EXPECTED_WALLET" ]] || {
  printf 'smoke_wallet_pubkey_mismatch expected=%s actual=%s\n' "$EXPECTED_WALLET" "$ACTUAL_WALLET" >&2
  exit 66
}

printf 'operator_sign_gate=devnet-smoke-player-bootstrap\n'
printf 'cluster=devnet\nwallet=%s\ninstructions=create_profile,create_move_vault\n' "$EXPECTED_WALLET"
CHAINBOUND_BOOTSTRAP_MODE=execute \
CHAINBOUND_BOOTSTRAP_APPROVAL="$EXPECTED_APPROVAL" \
CHAINBOUND_SMOKE_WALLET_KEYPAIR="$KEYPAIR_PATH" \
node "$CLIENT"
