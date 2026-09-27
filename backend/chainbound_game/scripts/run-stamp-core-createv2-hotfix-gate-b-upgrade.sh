#!/usr/bin/env bash
set -euo pipefail
set +x

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
export CHAINBOUND_EXPECTED_GATE_APPROVAL="APPROVED_STAMP_CORE_CREATEV2_HOTFIX_GATE_B_UPGRADE_B5W4"
export CHAINBOUND_EXPECTED_ARTIFACT_SHA256="5741a024e981c3f7558752726564466cb6beabeefffe61254a620b915b8a6e5b"
export CHAINBOUND_EXPECTED_PRE_UPGRADE_SLOT="501530147"
export CHAINBOUND_GATE_OPERATION="stamp-core-createv2-hotfix-gate-b-upgrade-only"
export CHAINBOUND_GATE_LOG_STEM="stamp-core-createv2-hotfix-gate-b-upgrade"
exec bash "$SCRIPT_DIR/run-bootstrap-gate-b-upgrade.sh"
