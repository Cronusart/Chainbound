#!/usr/bin/env bash
set -euo pipefail
set +x

# Operator-only Devnet bootstrap Gate B. This submits exactly one Upgrade
# transaction from the already verified loader buffer. It never creates or
# writes a buffer and never changes program authority or configuration.

EXPECTED_APPROVAL="${CHAINBOUND_EXPECTED_GATE_APPROVAL:-APPROVED_BOOTSTRAP_GATE_B_UPGRADE_B5W4}"
EXPECTED_GENESIS="EtWTRABZaYq6iMfeYKouRu166VU2xqa1wcaWoxPkrZBG"
EXPECTED_PROGRAM="9312ay7VrpvNYmyEx8TYCqhafbZ2Y5WGVbt6Qp772uUQ"
EXPECTED_PROGRAM_DATA="5wqBqX7qubopQnteX5RPTwBJrSFA6xnMs9Zbwz2BHfzD"
EXPECTED_PRE_UPGRADE_SLOT="${CHAINBOUND_EXPECTED_PRE_UPGRADE_SLOT:-501449795}"
EXPECTED_PAYER="DkFmzzzF89tYZu2JWR1uq3H13U5np9NU6MHF4E61gJmG"
EXPECTED_BUFFER="B5w4TUucVz6uw2NVAVUhjQf85ytLckSTW2DwTasEkdDY"
EXPECTED_ARTIFACT_BYTES="741248"
EXPECTED_ARTIFACT_SHA256="${CHAINBOUND_EXPECTED_ARTIFACT_SHA256:-588249aecc1c57956a1acf525af31ffa1e756145f5e26b65724f45d499751e87}"
EXPECTED_BUFFER_REFUND_LAMPORTS="3766418680"
RPC_URL="https://api.devnet.solana.com"
OPERATION="${CHAINBOUND_GATE_OPERATION:-bootstrap-gate-b-upgrade-only}"
LOG_STEM="${CHAINBOUND_GATE_LOG_STEM:-bootstrap-gate-b-upgrade}"

if [[ "${CHAINBOUND_GATE_APPROVAL:-}" != "$EXPECTED_APPROVAL" ]]; then
  printf 'Refusing Gate B: approval token is absent or incorrect.\n' >&2
  exit 64
fi

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
CHAIN_ROOT="$(cd -- "$SCRIPT_DIR/.." && pwd -P)"
PROJECT_ROOT="$(cd -- "$CHAIN_ROOT/../.." && pwd -P)"
ARTIFACT="$CHAIN_ROOT/target/deploy/chainbound_game.so"
PAYER_KEYPAIR="${CHAINBOUND_OPERATOR_KEYPAIR:-$HOME/.config/solana/id.json}"
SOLANA_BIN="${SOLANA_BIN:-$HOME/.local/share/solana/install/active_release/bin/solana}"
SOLANA_KEYGEN_BIN="${SOLANA_KEYGEN_BIN:-$HOME/.local/share/solana/install/active_release/bin/solana-keygen}"
STAMP="$(date -u +%Y%m%dT%H%M%SZ)"
LOG_DIR="${CHAINBOUND_LOG_DIR:-$PROJECT_ROOT/Logs}"
LOG="$LOG_DIR/$LOG_STEM-$STAMP.log"
VERIFY_LOG="$LOG_DIR/$LOG_STEM-verified-$STAMP.json"
PLAN_FILE="$(mktemp)"
RESULT_FILE="$(mktemp)"
trap 'rm -f -- "$PLAN_FILE" "$RESULT_FILE"' EXIT

mkdir -p -- "$LOG_DIR"
exec > >(tee -a "$LOG") 2>&1

printf 'operation=%s\ncluster=devnet\n' "$OPERATION"
printf 'buffer_creation_included=false\nbuffer_write_included=false\nprogram_upgrade_included=true\n'
printf 'artifact=%s\nprogram=%s\nbuffer=%s\npayer=%s\n' \
  "$ARTIFACT" "$EXPECTED_PROGRAM" "$EXPECTED_BUFFER" "$EXPECTED_PAYER"

for required in "$ARTIFACT" "$PAYER_KEYPAIR" "$SOLANA_BIN" "$SOLANA_KEYGEN_BIN"; do
  [[ -f "$required" ]] || { printf 'missing_required_file=%s\n' "$required" >&2; exit 65; }
done
command -v node >/dev/null || { printf 'node_not_found\n' >&2; exit 65; }

[[ "$(stat -c '%s' "$ARTIFACT")" == "$EXPECTED_ARTIFACT_BYTES" ]] || { printf 'artifact_size_mismatch\n' >&2; exit 66; }
[[ "$(sha256sum "$ARTIFACT" | awk '{print $1}')" == "$EXPECTED_ARTIFACT_SHA256" ]] || { printf 'artifact_sha256_mismatch\n' >&2; exit 66; }
[[ "$("$SOLANA_KEYGEN_BIN" pubkey "$PAYER_KEYPAIR")" == "$EXPECTED_PAYER" ]] || { printf 'payer_pubkey_mismatch\n' >&2; exit 66; }
[[ "$("$SOLANA_BIN" genesis-hash --url "$RPC_URL")" == "$EXPECTED_GENESIS" ]] || { printf 'devnet_genesis_mismatch\n' >&2; exit 66; }

BUFFER_PUBKEY="$EXPECTED_BUFFER" EXPECTED_ARTIFACT_BYTES="$EXPECTED_ARTIFACT_BYTES" EXPECTED_ARTIFACT_SHA256="$EXPECTED_ARTIFACT_SHA256" EXPECTED_PRE_UPGRADE_SLOT="$EXPECTED_PRE_UPGRADE_SLOT" SOLANA_RPC_URL="$RPC_URL" \
  node "$SCRIPT_DIR/prepare-program-upgrade-gate-b-unsigned.cjs" > "$PLAN_FILE"
node - "$PLAN_FILE" "$EXPECTED_PRE_UPGRADE_SLOT" "$EXPECTED_ARTIFACT_SHA256" <<'NODE'
const fs = require("fs");
const plan = JSON.parse(fs.readFileSync(process.argv[2], "utf8"));
const expectedPreUpgradeSlot = Number(process.argv[3]);
const expectedSha256 = process.argv[4];
function check(value, message) { if (!value) throw new Error(message); }
check(plan.cluster === "devnet", "plan_cluster_mismatch");
check(plan.genesisHash === "EtWTRABZaYq6iMfeYKouRu166VU2xqa1wcaWoxPkrZBG", "plan_genesis_mismatch");
check(plan.programId === "9312ay7VrpvNYmyEx8TYCqhafbZ2Y5WGVbt6Qp772uUQ", "plan_program_mismatch");
check(plan.programData === "5wqBqX7qubopQnteX5RPTwBJrSFA6xnMs9Zbwz2BHfzD", "plan_programdata_mismatch");
check(plan.previousDeploySlot === expectedPreUpgradeSlot, "plan_pre_upgrade_slot_mismatch");
check(plan.buffer === "B5w4TUucVz6uw2NVAVUhjQf85ytLckSTW2DwTasEkdDY", "plan_buffer_mismatch");
check(plan.payerSpillAndUpgradeAuthority === "DkFmzzzF89tYZu2JWR1uq3H13U5np9NU6MHF4E61gJmG", "plan_authority_mismatch");
check(plan.programDataCapacityBytes === 741248, "plan_capacity_mismatch");
check(plan.artifact.bytes === 741248, "plan_artifact_size_mismatch");
check(plan.artifact.sha256 === expectedSha256, "plan_artifact_hash_mismatch");
check(plan.bufferPayloadSha256 === plan.artifact.sha256, "plan_buffer_hash_mismatch");
check(plan.bufferRefundLamports === 3766418680, "plan_buffer_refund_mismatch");
check(plan.feeLamports === 5000, "plan_fee_mismatch");
check(plan.message.requiredSignatures === 1, "plan_signer_count_mismatch");
check(plan.message.instruction.dataHex === "03000000", "plan_instruction_mismatch");
check(plan.simulation.err === null, "plan_simulation_failed");
process.stdout.write(JSON.stringify({
  gateBPreflightVerified: true,
  cluster: plan.cluster,
  programId: plan.programId,
  programData: plan.programData,
  previousDeploySlot: plan.previousDeploySlot,
  buffer: plan.buffer,
  payerSpillAndUpgradeAuthority: plan.payerSpillAndUpgradeAuthority,
  artifactBytes: plan.artifact.bytes,
  artifactSha256: plan.artifact.sha256,
  bufferPayloadSha256: plan.bufferPayloadSha256,
  bufferRefundLamports: plan.bufferRefundLamports,
  feeLamports: plan.feeLamports,
  messageSha256: plan.message.sha256,
  requiredSignatures: plan.message.requiredSignatures,
  instructionDataHex: plan.message.instruction.dataHex,
  simulationError: plan.simulation.err,
  simulationUnitsConsumed: plan.simulation.unitsConsumed,
  signingPerformed: false,
  sendingPerformed: false
}, null, 2) + "\n");
NODE

if [[ "${CHAINBOUND_GATE_VALIDATE_ONLY:-0}" == "1" ]]; then
  printf 'validate_only=true\nsigning_performed=false\nsending_performed=false\nLOG=%s\n' "$LOG"
  exit 0
fi

printf 'gate_b_send_start_utc=%s\n' "$(date -u +%Y-%m-%dT%H:%M:%SZ)"
NO_DNA=1 "$SOLANA_BIN" program upgrade "$EXPECTED_BUFFER" "$EXPECTED_PROGRAM" \
  --upgrade-authority "$PAYER_KEYPAIR" \
  --fee-payer "$PAYER_KEYPAIR" \
  --keypair "$PAYER_KEYPAIR" \
  --url "$RPC_URL" \
  --commitment confirmed \
  --use-quic \
  --output json | tee "$RESULT_FILE"
printf 'gate_b_send_complete_utc=%s\n' "$(date -u +%Y-%m-%dT%H:%M:%SZ)"

UPGRADE_SIGNATURE="$(node - "$RESULT_FILE" <<'NODE'
const fs = require("fs");
const result = JSON.parse(fs.readFileSync(process.argv[2], "utf8"));
if (result.programId !== "9312ay7VrpvNYmyEx8TYCqhafbZ2Y5WGVbt6Qp772uUQ") throw new Error("upgrade_result_program_mismatch");
if (typeof result.signature !== "string" || result.signature.length < 80) throw new Error("upgrade_signature_missing");
process.stdout.write(result.signature);
NODE
)"

UPGRADE_SIGNATURE="$UPGRADE_SIGNATURE" BUFFER_PUBKEY="$EXPECTED_BUFFER" EXPECTED_ARTIFACT_BYTES="$EXPECTED_ARTIFACT_BYTES" EXPECTED_ARTIFACT_SHA256="$EXPECTED_ARTIFACT_SHA256" EXPECTED_PRE_UPGRADE_SLOT="$EXPECTED_PRE_UPGRADE_SLOT" SOLANA_RPC_URL="$RPC_URL" \
  node "$SCRIPT_DIR/verify-program-upgrade.cjs" | tee "$VERIFY_LOG"
printf 'LOG=%s\nVERIFY_LOG=%s\n' "$LOG" "$VERIFY_LOG"
