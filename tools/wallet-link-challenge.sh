#!/usr/bin/env bash
# Step 1/2: login + wallet_auth_challenge (WSL-safe).
#   bash tools/wallet-link-challenge.sh
# Optional: EMAIL=you@x.com WALLET=<pubkey> bash tools/wallet-link-challenge.sh
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"
umask 077
mkdir -p Logs

PROJECT_URL="https://jhzqcbrzkausrrtcavuu.supabase.co"
WALLET="${WALLET:-23TQmb6Ss9KZp9ThpxFiGRPwBrjD2vqAFtTP6ZFeR7Bt}"
EMAIL="${EMAIL:-cronustitannft@gmail.com}"
OUT="Logs/wallet-link-challenge-latest.json"

read -rsp "Supabase anon key: " ANON
echo
read -rsp "Password for ${EMAIL}: " PASS
echo

curl -sS "${PROJECT_URL}/auth/v1/token?grant_type=password" \
  -H "apikey: ${ANON}" \
  -H "Authorization: Bearer ${ANON}" \
  -H "Content-Type: application/json" \
  -d "{\"email\":\"${EMAIL}\",\"password\":\"${PASS}\"}" \
  -o /tmp/cb-wallet-link-session.json \
  -w "login_http:%{http_code}\n"

python3 - <<'PY'
import json
d=json.load(open("/tmp/cb-wallet-link-session.json"))
t=d.get("access_token") or ""
print("login_ok", bool(t), "token_len", len(t), "prefix", t[:3] if t else "")
print("user_id", (d.get("user") or {}).get("id"))
print("error", d.get("error_description") or d.get("msg") or d.get("error"))
if not t:
  raise SystemExit(1)
PY

ACCESS="$(python3 -c 'import json; print(json.load(open("/tmp/cb-wallet-link-session.json"))["access_token"])')"
printf '%s' "$ANON" > /tmp/cb-wallet-link-anon.txt
printf '%s' "$ACCESS" > /tmp/cb-wallet-link-access.txt
unset PASS

curl -sS -X POST "${PROJECT_URL}/functions/v1/wallet_auth_challenge" \
  -H "Authorization: Bearer ${ACCESS}" \
  -H "apikey: ${ANON}" \
  -H "Content-Type: application/json" \
  -d "{\"wallet_public_key\":\"${WALLET}\",\"cluster\":\"devnet\",\"wallet_provider\":\"cli_smoke\"}" \
  -o "$OUT" \
  -w "challenge_http:%{http_code}\n"

unset ACCESS ANON

python3 - <<PY
import json
p="$OUT"
d=json.load(open(p))
print("challenge_success", d.get("success"))
print("nonce_id", d.get("nonce_id"))
print("wallet", d.get("wallet_public_key"))
print("error", d.get("error"))
msg=d.get("message") or ""
open("Logs/wallet-link-challenge-message.txt","w",encoding="utf-8").write(msg)
print("message_lines", len(msg.splitlines()))
print("message_file", "Logs/wallet-link-challenge-message.txt")
print("challenge_json", p)
if not d.get("success") or not d.get("nonce_id") or not msg:
  raise SystemExit(2)
print("Next: sign message, then: bash tools/wallet-link-verify.sh")
print("Auto-sign option: KEYPAIR=/path/to/id.json bash tools/wallet-link-verify.sh")
PY
