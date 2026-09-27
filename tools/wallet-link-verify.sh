#!/usr/bin/env bash
# Step 2/2: wallet_connect_verified
#   bash tools/wallet-link-verify.sh
#   KEYPAIR=/path/to/solana-id.json bash tools/wallet-link-verify.sh
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"
umask 077
mkdir -p Logs

PROJECT_URL="https://jhzqcbrzkausrrtcavuu.supabase.co"
CHAL="Logs/wallet-link-challenge-latest.json"
MSG="Logs/wallet-link-challenge-message.txt"
LOG="Logs/wallet-link-verify-$(date -u +%Y%m%dT%H%M%SZ).log"

[[ -f "$CHAL" && -f "$MSG" ]] || { echo "Run bash tools/wallet-link-challenge.sh first." >&2; exit 1; }
[[ -f /tmp/cb-wallet-link-anon.txt && -f /tmp/cb-wallet-link-access.txt ]] || { echo "Session expired; re-run challenge." >&2; exit 1; }

SIG=""
if [[ -n "${KEYPAIR:-}" ]]; then
  [[ -f "$KEYPAIR" ]] || { echo "KEYPAIR not found: $KEYPAIR" >&2; exit 1; }
  SIG="$(node tools/wallet-link-sign.mjs "$KEYPAIR" "$MSG")"
  echo "signed_with_keypair=1 sig_len=${#SIG}"
else
  echo "Open Logs/wallet-link-challenge-message.txt"
  echo "Sign that EXACT text with the challenge wallet (see wallet field in the challenge JSON)."
  echo "Paste a base58 (preferred) or base64 signature."
  read -rsp "Signature: " SIG
  echo
fi
[[ -n "$SIG" ]] || { echo "Empty signature" >&2; exit 1; }

printf '%s' "$SIG" > /tmp/cb-wallet-link-sig.txt
unset SIG

python3 - <<PY
import json, urllib.request, os
chal=json.load(open("$CHAL"))
sig=open("/tmp/cb-wallet-link-sig.txt").read().strip()
payload={
  "wallet_public_key": chal["wallet_public_key"],
  "nonce_id": chal["nonce_id"],
  "signed_message": open("$MSG",encoding="utf-8").read(),
  "signature": sig,
  "cluster": "devnet",
  "wallet_provider": "cli_smoke",
}
body=json.dumps(payload).encode()
req=urllib.request.Request(
  "$PROJECT_URL/functions/v1/wallet_connect_verified",
  data=body,
  headers={
    "Authorization": "Bearer " + open("/tmp/cb-wallet-link-access.txt").read().strip(),
    "apikey": open("/tmp/cb-wallet-link-anon.txt").read().strip(),
    "Content-Type": "application/json",
  },
  method="POST",
)
try:
  with urllib.request.urlopen(req, timeout=60) as r:
    raw=r.read().decode(); code=r.status
except Exception as e:
  raw=e.read().decode() if hasattr(e,"read") else str(e)
  code=getattr(e,"code",0)
open("$LOG","w",encoding="utf-8").write(f"http={code}\n{raw}\n")
print(f"verify_http:{code}")
d=json.loads(raw) if raw.strip().startswith("{") else {}
print("success", d.get("success"))
print("error", d.get("error"))
print("log", "$LOG")
for p in ("/tmp/cb-wallet-link-anon.txt","/tmp/cb-wallet-link-access.txt","/tmp/cb-wallet-link-sig.txt"):
  try: os.remove(p)
  except FileNotFoundError: pass
if not d.get("success"):
  raise SystemExit(2)
print("Linked. Confirm with the auth.users SQL join.")
PY
