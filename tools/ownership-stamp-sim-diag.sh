#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"
umask 077
mkdir -p Logs

export WALLET="${CHAINBOUND_SMOKE_WALLET:-H68eGMwT3UCigd9qf4u1VLyqQh6jq57HD4wLWaQrbBWq}"
export KNOWN_ID="${CHAINBOUND_SMOKE_KNOWN_ID:-}"
export IDEM="${CHAINBOUND_SMOKE_IDEMPOTENCY_KEY:-moves-smoke-mint-20260921-diag-1}"
export RPC="https://api.devnet.solana.com"
export PROGRAM="9312ay7VrpvNYmyEx8TYCqhafbZ2Y5WGVbt6Qp772uUQ"
export FN="https://jhzqcbrzkausrrtcavuu.supabase.co/functions/v1/ownership_prepare_stamp"
export OUT="Logs/ownership-stamp-sim-diag-$(date -u +%Y%m%dT%H%M%SZ).json"

[[ -n "$KNOWN_ID" ]] || { echo "export CHAINBOUND_SMOKE_KNOWN_ID=... first" >&2; exit 1; }

if [[ -n "${ANON_FILE:-}" && -f "$ANON_FILE" ]]; then export ANON="$(tr -d '\r\n' <"$ANON_FILE")"; else read -rsp "anon: " ANON; echo; export ANON; fi
if [[ -n "${ACCESS_FILE:-}" && -f "$ACCESS_FILE" ]]; then export ACCESS="$(tr -d '\r\n' <"$ACCESS_FILE")"; else read -rsp "access: " ACCESS; echo; export ACCESS; fi

# Resolve @solana/web3.js from backend package (tools/ has no node_modules)
export NODE_PATH="$ROOT/backend/chainbound_game/node_modules${NODE_PATH:+:$NODE_PATH}"
node tools/ownership-stamp-sim-diag.cjs
unset ANON ACCESS
