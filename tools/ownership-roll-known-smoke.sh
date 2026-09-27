#!/usr/bin/env bash
# Devnet smoke: ownership_roll_known. Prefer files over paste to avoid truncation.
# Usage:
#   bash tools/ownership-roll-known-smoke.sh
# Or after writing secrets to /tmp:
#   ANON_FILE=/tmp/sb-anon.txt ACCESS_FILE=/tmp/sb-access.txt bash tools/ownership-roll-known-smoke.sh
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"
umask 077
mkdir -p Logs

URL="https://jhzqcbrzkausrrtcavuu.supabase.co/functions/v1/ownership_roll_known"
LOG="Logs/ownership-roll-known-smoke-$(date -u +%Y%m%dT%H%M%SZ).log"
IDEM="devnet-smoke-known-roll-$(date -u +%Y%m%d%H%M%S)"

if [[ -n "${ANON_FILE:-}" && -f "$ANON_FILE" ]]; then
  ANON="$(tr -d '\r\n' < "$ANON_FILE")"
else
  read -rsp "Supabase anon key: " ANON
  echo
fi
if [[ -n "${ACCESS_FILE:-}" && -f "$ACCESS_FILE" ]]; then
  ACCESS="$(tr -d '\r\n' < "$ACCESS_FILE")"
else
  read -rsp "Authenticated user access token: " ACCESS
  echo
fi

python3 - <<PY
import os,json,base64,time
anon=os.environ.get("ANON","")
# passed via env below
PY

# export for python checks without printing secrets
export _CHK_ANON="$ANON"
export _CHK_ACCESS="$ACCESS"
python3 - <<'PY'
import os,json,base64,time,sys
anon=os.environ["_CHK_ANON"].strip()
tok=os.environ["_CHK_ACCESS"].strip()
print("anon_len", len(anon), "anon_prefix", anon[:3])
print("token_len", len(tok), "token_prefix", tok[:3])
if tok == anon:
  print("FAIL: access token identical to anon key")
  sys.exit(1)
if not tok.startswith("eyJ"):
  print("FAIL: access token should start with eyJ")
  sys.exit(1)
parts=tok.split(".")
if len(parts)!=3:
  print("FAIL: token not a JWT (need 3 parts, got", len(parts), ")")
  sys.exit(1)
pad=lambda s: s + "="*((4-len(s)%4)%4)
try:
  payload=json.loads(base64.urlsafe_b64decode(pad(parts[1])))
except Exception as e:
  print("FAIL: cannot decode jwt payload", e)
  sys.exit(1)
print("jwt_role", payload.get("role"))
print("jwt_iss", payload.get("iss"))
exp=payload.get("exp")
now=int(time.time())
print("jwt_exp_in_sec", (int(exp)-now) if exp is not None else None)
if payload.get("role") != "authenticated":
  print("FAIL: role must be authenticated (got", payload.get("role"), ")")
  sys.exit(1)
if "jhzqcbrzkausrrtcavuu" not in str(payload.get("iss","")):
  print("FAIL: iss not this Supabase project")
  sys.exit(1)
if exp is None or int(exp) <= now+30:
  print("FAIL: token expired or expiring")
  sys.exit(1)
print("jwt_local_checks_ok")
PY

tmp="$(mktemp)"
http="$(curl -sS -X POST "$URL" \
  -H "Authorization: Bearer ${ACCESS}" \
  -H "apikey: ${ANON}" \
  -H "Content-Type: application/json" \
  --data "{\"idempotency_key\":\"${IDEM}\"}" \
  -o "$tmp" -w "%{http_code}")" || true

{
  echo "ownership_roll_known smoke"
  echo "utc=$(date -u +%Y%m%dT%H%M%SZ)"
  echo "idempotency_key=$IDEM"
  echo "http=$http"
  echo "raw:"
  head -c 2000 "$tmp"; echo
} | tee "$LOG"

python3 - <<PY
import json,sys
http=int("$http")
raw=open("$tmp",encoding="utf-8").read()
try:
  d=json.loads(raw)
except Exception:
  print("parse_error")
  sys.exit(1)
print("gateway_code", d.get("code"))
print("gateway_message", d.get("message"))
print("success", d.get("success"))
print("error", d.get("error"))
print("ramCost", d.get("ramCost"))
print("ramBalance", d.get("ramBalance"))
print("replayed", d.get("replayed"))
known=d.get("known") or {}
print("known.instanceId", known.get("instanceId"))
print("known.moveId", known.get("moveId"))
print("log", "$LOG")
if http!=200 or not d.get("success") or not known.get("instanceId"):
  sys.exit(2)
print("NEXT_EXPORT: export CHAINBOUND_SMOKE_KNOWN_ID='%s'" % known.get("instanceId"))
PY
status=$?
rm -f "$tmp"
unset ANON ACCESS _CHK_ANON _CHK_ACCESS
exit $status
