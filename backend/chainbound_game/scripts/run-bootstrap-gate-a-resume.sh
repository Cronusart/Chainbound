#!/usr/bin/env bash
set -euo pipefail
set +x

# Operator-only continuation of the already approved Devnet Gate A upload.
# Requires an existing loader buffer and sends only diffed Write instructions.
# It never creates a replacement buffer and never invokes Upgrade.

EXPECTED_APPROVAL="${CHAINBOUND_EXPECTED_GATE_APPROVAL:-APPROVED_BOOTSTRAP_GATE_A_CREATE_UPLOAD_B5W4}"
EXPECTED_GENESIS="EtWTRABZaYq6iMfeYKouRu166VU2xqa1wcaWoxPkrZBG"
EXPECTED_PAYER="DkFmzzzF89tYZu2JWR1uq3H13U5np9NU6MHF4E61gJmG"
EXPECTED_BUFFER="B5w4TUucVz6uw2NVAVUhjQf85ytLckSTW2DwTasEkdDY"
EXPECTED_ARTIFACT_BYTES="741248"
EXPECTED_ARTIFACT_SHA256="${CHAINBOUND_EXPECTED_ARTIFACT_SHA256:-588249aecc1c57956a1acf525af31ffa1e756145f5e26b65724f45d499751e87}"
EXPECTED_PROGRAM_DATA_SLOT="${CHAINBOUND_EXPECTED_PRE_UPGRADE_SLOT:-501449795}"
OPERATION="${CHAINBOUND_GATE_OPERATION:-bootstrap-gate-a-resume-existing-buffer}"
LOG_STEM="${CHAINBOUND_GATE_LOG_STEM:-bootstrap-gate-a-resume}"
RPC_URL="https://api.devnet.solana.com"

if [[ "${CHAINBOUND_GATE_APPROVAL:-}" != "$EXPECTED_APPROVAL" ]]; then
  printf 'Refusing Gate A resume: approval token is absent or incorrect.\n' >&2
  exit 64
fi

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
CHAIN_ROOT="$(cd -- "$SCRIPT_DIR/.." && pwd -P)"
PROJECT_ROOT="$(cd -- "$CHAIN_ROOT/../.." && pwd -P)"
ARTIFACT="$CHAIN_ROOT/target/deploy/chainbound_game.so"
PAYER_KEYPAIR="${CHAINBOUND_OPERATOR_KEYPAIR:-$HOME/.config/solana/id.json}"
BUFFER_KEYPAIR="${CHAINBOUND_BUFFER_KEYPAIR:-$HOME/.config/solana/buffers/$EXPECTED_BUFFER.json}"
SOLANA_BIN="${SOLANA_BIN:-$HOME/.local/share/solana/install/active_release/bin/solana}"
SOLANA_KEYGEN_BIN="${SOLANA_KEYGEN_BIN:-$HOME/.local/share/solana/install/active_release/bin/solana-keygen}"
STAMP="$(date -u +%Y%m%dT%H%M%SZ)"
LOG_DIR="${CHAINBOUND_LOG_DIR:-$PROJECT_ROOT/Logs}"
LOG="$LOG_DIR/$LOG_STEM-$STAMP.log"
VERIFY_LOG="$LOG_DIR/$LOG_STEM-buffer-verified-$STAMP.json"
PLAN_FILE="$(mktemp)"
trap 'rm -f -- "$PLAN_FILE"' EXIT

mkdir -p -- "$LOG_DIR"
exec > >(tee -a "$LOG") 2>&1

printf 'operation=%s\ncluster=devnet\n' "$OPERATION"
printf 'buffer_creation_included=false\nprogram_upgrade_included=false\n'
printf 'artifact=%s\nbuffer=%s\npayer=%s\n' "$ARTIFACT" "$EXPECTED_BUFFER" "$EXPECTED_PAYER"

for required in "$ARTIFACT" "$PAYER_KEYPAIR" "$BUFFER_KEYPAIR" "$SOLANA_BIN" "$SOLANA_KEYGEN_BIN"; do
  [[ -f "$required" ]] || { printf 'missing_required_file=%s\n' "$required" >&2; exit 65; }
done
command -v node >/dev/null || { printf 'node_not_found\n' >&2; exit 65; }

[[ "$(stat -c '%s' "$ARTIFACT")" == "$EXPECTED_ARTIFACT_BYTES" ]] || { printf 'artifact_size_mismatch\n' >&2; exit 66; }
[[ "$(sha256sum "$ARTIFACT" | awk '{print $1}')" == "$EXPECTED_ARTIFACT_SHA256" ]] || { printf 'artifact_sha256_mismatch\n' >&2; exit 66; }
[[ "$("$SOLANA_KEYGEN_BIN" pubkey "$PAYER_KEYPAIR")" == "$EXPECTED_PAYER" ]] || { printf 'payer_pubkey_mismatch\n' >&2; exit 66; }
[[ "$("$SOLANA_KEYGEN_BIN" pubkey "$BUFFER_KEYPAIR")" == "$EXPECTED_BUFFER" ]] || { printf 'buffer_pubkey_mismatch\n' >&2; exit 66; }
[[ "$("$SOLANA_BIN" genesis-hash --url "$RPC_URL")" == "$EXPECTED_GENESIS" ]] || { printf 'devnet_genesis_mismatch\n' >&2; exit 66; }

BUFFER_PUBKEY="$EXPECTED_BUFFER" EXPECTED_ARTIFACT_BYTES="$EXPECTED_ARTIFACT_BYTES" EXPECTED_ARTIFACT_SHA256="$EXPECTED_ARTIFACT_SHA256" SOLANA_RPC_URL="$RPC_URL" node "$SCRIPT_DIR/prepare-program-buffer-resume-unsigned.cjs" > "$PLAN_FILE"
node - "$PLAN_FILE" "$EXPECTED_ARTIFACT_SHA256" <<'NODE'
const fs = require("fs");
const plan = JSON.parse(fs.readFileSync(process.argv[2], "utf8"));
const expectedSha256 = process.argv[3];
function check(value, message) { if (!value) throw new Error(message); }
check(plan.cluster === "devnet", "plan_cluster_mismatch");
check(plan.buffer.address === "B5w4TUucVz6uw2NVAVUhjQf85ytLckSTW2DwTasEkdDY", "plan_buffer_mismatch");
check(plan.buffer.owner === "BPFLoaderUpgradeab1e11111111111111111111111", "plan_buffer_owner_mismatch");
check(plan.buffer.authority === "DkFmzzzF89tYZu2JWR1uq3H13U5np9NU6MHF4E61gJmG", "plan_buffer_authority_mismatch");
check(plan.buffer.accountBytes === 741285, "plan_buffer_size_mismatch");
check(plan.buffer.lamports === 3766418680, "plan_buffer_funding_mismatch");
check(plan.artifact.bytes === 741248, "plan_artifact_size_mismatch");
check(plan.artifact.sha256 === expectedSha256, "plan_artifact_hash_mismatch");
check(plan.writes.remainingChunks > 0, "no_remaining_writes");
check(plan.simulations.firstRemaining?.err === null, "first_remaining_simulation_failed");
check(plan.simulations.lastRemaining?.err === null, "last_remaining_simulation_failed");
process.stdout.write(JSON.stringify({
  resumePreflightVerified: true,
  bufferPublicKey: plan.buffer.address,
  bufferAccountExists: true,
  bufferCreationIncluded: false,
  programUpgradeIncluded: false,
  artifactBytes: plan.artifact.bytes,
  artifactSha256: plan.artifact.sha256,
  completedChunks: plan.writes.completedChunks,
  remainingChunks: plan.writes.remainingChunks,
  firstRemainingOffset: plan.writes.firstRemainingOffset,
  lastRemainingOffset: plan.writes.lastRemainingOffset,
  projectedRemainingFeesLamports: plan.writes.projectedRemainingFeesLamports,
  firstRemainingSimulationError: plan.simulations.firstRemaining.err,
  lastRemainingSimulationError: plan.simulations.lastRemaining.err,
  signingPerformed: false,
  sendingPerformed: false
}, null, 2) + "\n");
NODE

if [[ "${CHAINBOUND_GATE_VALIDATE_ONLY:-0}" == "1" ]]; then
  printf 'validate_only=true\nsigning_performed=false\nsending_performed=false\nLOG=%s\n' "$LOG"
  exit 0
fi

printf 'resume_send_start_utc=%s\n' "$(date -u +%Y-%m-%dT%H:%M:%SZ)"
NO_DNA=1 "$SOLANA_BIN" program write-buffer "$ARTIFACT" --buffer "$BUFFER_KEYPAIR" --buffer-authority "$PAYER_KEYPAIR" --fee-payer "$PAYER_KEYPAIR" --keypair "$PAYER_KEYPAIR" --url "$RPC_URL" --commitment confirmed --use-quic --max-sign-attempts 10 --output json
printf 'resume_send_complete_utc=%s\n' "$(date -u +%Y-%m-%dT%H:%M:%SZ)"

BUFFER_PUBKEY="$EXPECTED_BUFFER" EXPECTED_ARTIFACT_BYTES="$EXPECTED_ARTIFACT_BYTES" EXPECTED_ARTIFACT_SHA256="$EXPECTED_ARTIFACT_SHA256" EXPECTED_PRE_UPGRADE_SLOT="$EXPECTED_PROGRAM_DATA_SLOT" SOLANA_RPC_URL="$RPC_URL" node "$SCRIPT_DIR/verify-program-buffer.cjs" | tee "$VERIFY_LOG"
printf 'LOG=%s\nVERIFY_LOG=%s\n' "$LOG" "$VERIFY_LOG"
