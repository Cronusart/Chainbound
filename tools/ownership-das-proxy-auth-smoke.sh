#!/usr/bin/env bash
# Ownership DAS proxy authenticated smokes — safe for WSL (no multi-line paste).
# Usage: bash tools/ownership-das-proxy-auth-smoke.sh
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"
umask 077
mkdir -p Logs

URL="https://jhzqcbrzkausrrtcavuu.supabase.co/functions/v1/ownership_das_proxy"
MOVES="AUQuvgpFDUekeVh2GHri99A59G4YY5LUWdGw9fKTB9wC"
WRONG="DP3rTx145BPfunueM9SHjYJXBDCK5GmrdwZjpCvgf6U"
LOG="Logs/ownership-das-proxy-auth-smoke-$(date -u +%Y%m%dT%H%M%SZ).log"

command -v curl >/dev/null || { echo "curl required" >&2; exit 1; }
HAVE_JQ=0
command -v jq >/dev/null && HAVE_JQ=1

read -rsp "Supabase anon key: " ANON
echo
read -rsp "Authenticated user access token: " ACCESS_TOKEN
echo

if [[ -z "${ANON}" || -z "${ACCESS_TOKEN}" ]]; then
  echo "Both anon key and access token are required." >&2
  exit 1
fi

# Quick local sanity (never prints secrets)
python3 - <<PY
anon, tok = """${ANON}""", """${ACCESS_TOKEN}"""
print(f"anon_len={len(anon)} anon_prefix={anon[:3]!r}")
print(f"token_len={len(tok)} token_prefix={tok[:3]!r}")
if not tok.startswith("eyJ"):
    print("WARN: access token should usually start with eyJ (JWT). UID UUIDs will 401.")
if tok == anon:
    print("WARN: access token looks identical to anon key — use the user login JWT instead.")
PY

{
  echo "ownership_das_proxy auth smoke"
  echo "utc=$(date -u +%Y%m%dT%H%M%SZ)"
  echo "url=$URL"
  echo "moves=$MOVES"
} | tee "$LOG"

smoke() {
  local label="$1" payload="$2" filter="$3" tmp http
  tmp="$(mktemp)"
  http="$(curl -sS -X POST "$URL" \
    -H "Authorization: Bearer ${ACCESS_TOKEN}" \
    -H "apikey: ${ANON}" \
    -H "Content-Type: application/json" \
    --data "$payload" \
    -o "$tmp" -w "%{http_code}")" || true

  {
    printf '\n[%s] HTTP:%s\n' "$label" "$http"
    echo -n "raw: "
    head -c 600 "$tmp"; echo
    if [[ "$HAVE_JQ" -eq 1 ]]; then
      jq -c "$filter" "$tmp" 2>/dev/null || true
    fi
  } | tee -a "$LOG"
  rm -f -- "$tmp"
}

smoke "getAsset Moves" \
  "{\"method\":\"getAsset\",\"params\":{\"id\":\"${MOVES}\"}}" \
  '{success,error,msg,message,code,id:.result.id,interface:.result.interface}'

smoke "getAssetsByGroup Moves limit clamp" \
  "{\"method\":\"getAssetsByGroup\",\"params\":{\"groupKey\":\"collection\",\"groupValue\":\"${MOVES}\",\"page\":1,\"limit\":999}}" \
  '{success,error,msg,message,code,total:.result.total,limit:.result.limit,itemCount:(.result.items|length)}'

smoke "disallowed sendTransaction" \
  '{"method":"sendTransaction","params":{}}' \
  '{success,error,msg,message,code}'

smoke "wrong collection" \
  "{\"method\":\"getAssetsByGroup\",\"params\":{\"groupKey\":\"collection\",\"groupValue\":\"${WRONG}\",\"page\":1,\"limit\":10}}" \
  '{success,error,msg,message,code}'

unset ACCESS_TOKEN ANON
printf '\nRedacted evidence: %s\n' "$LOG"