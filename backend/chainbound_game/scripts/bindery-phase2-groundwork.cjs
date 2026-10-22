#!/usr/bin/env node
"use strict";

// Intentionally local-only. Do not add RPC, HTTP, wallet, signer, deployment,
// Supabase, child-process, or environment-secret dependencies to this file.

const fs = require("node:fs");
const path = require("node:path");

const EXPECTED_PROGRAM_ID = "9312ay7VrpvNYmyEx8TYCqhafbZ2Y5WGVbt6Qp772uUQ";
const EXPECTED_PROJECT_REF = "jhzqcbrzkausrrtcavuu";
const LIVE_FLAG = "--execute-live";
const LIVE_EXECUTION_IMPLEMENTED = false;

const repoRoot = path.resolve(__dirname, "..", "..", "..");
const manifestRoot = path.join(repoRoot, "Docs", "Specs", "Economy", "manifests");
const catalogPath = path.join(manifestRoot, "bindery-phase2-case-catalog-v1.json");
const schemaPath = path.join(manifestRoot, "bindery-phase2-evidence-bundle.schema.json");
const emptyBundlePath = path.join(manifestRoot, "bindery-phase2-evidence-bundle.empty.json");

function readJson(filePath) {
  return JSON.parse(fs.readFileSync(filePath, "utf8"));
}

function invariant(condition, message) {
  if (!condition) {
    throw new Error(`Safety invariant failed: ${message}`);
  }
}

function sameStrings(left, right) {
  return left.length === right.length && left.every((value, index) => value === right[index]);
}

function validateGroundwork(catalog, schema, bundle) {
  invariant(catalog.artifact_kind === "bindery_phase2_case_catalog", "unexpected case catalog");
  invariant(catalog.status === "groundwork_only", "catalog is not groundwork-only");
  invariant(catalog.live_execution_flag === LIVE_FLAG, "live opt-in flag changed");
  invariant(catalog.live_execution_implemented === false, "catalog claims live execution exists");
  invariant(LIVE_EXECUTION_IMPLEMENTED === false, "runner live implementation must remain disabled");
  invariant(catalog.target.cluster === "devnet", "target cluster is not Devnet");
  invariant(catalog.target.program_id === EXPECTED_PROGRAM_ID, "program id changed");
  invariant(catalog.target.supabase_project_ref === EXPECTED_PROJECT_REF, "Supabase project changed");

  const gate = catalog.phase1_gate;
  invariant(gate.live_inspection_run === false, "catalog claims the Phase 1 inspection ran");
  invariant(gate.inspection_result === null, "catalog contains a fabricated inspection result");
  invariant(gate.selection_trusted === null, "selection_trusted must be unknown in groundwork");
  invariant(gate.source_enabled === null, "no source exists to report as enabled or disabled");
  invariant(gate.prepare_eligible === false, "prepare_eligible must remain false");
  invariant(gate.live_cases_blocked === true, "live cases must remain blocked");

  invariant(Array.isArray(catalog.cases) && catalog.cases.length === 20, "expected 20 matrix cases");
  const catalogIds = catalog.cases.map((testCase) => testCase.id);
  invariant(new Set(catalogIds).size === catalogIds.length, "case ids must be unique");
  invariant(catalog.cases.every((testCase) => testCase.status === "blocked_phase1_inspection"), "every live case must be blocked");
  invariant(catalog.cases.every((testCase) => Array.isArray(testCase.targets) && testCase.targets.length > 0), "every case needs a target");
  invariant(catalog.cases.every((testCase) => Array.isArray(testCase.required_disposable_inputs) && testCase.required_disposable_inputs.length > 0), "every case needs disposable inputs");
  invariant(catalog.cases.every((testCase) => typeof testCase.expected_terminal_state === "string" && testCase.expected_terminal_state.length > 0), "every case needs a terminal state");
  invariant(catalog.cases.every((testCase) => Array.isArray(testCase.evidence_fields) && testCase.evidence_fields.length > 0), "every case needs evidence fields");
  invariant(catalog.cases.some((testCase) => testCase.component_paths.includes(0)), "Stamped move path is missing");
  invariant(catalog.cases.some((testCase) => testCase.component_paths.includes(1)), "Cosmetic path is missing");

  invariant(schema.$schema === "https://json-schema.org/draft/2020-12/schema", "unexpected evidence schema dialect");
  invariant(schema.properties.phase1_inspection_checkpoint.properties.prepare_eligible.const === false, "schema must pin the disabled-source checkpoint to prepare_eligible=false");
  invariant(schema.properties.cases.minItems === 20 && schema.properties.cases.maxItems === 20, "schema case count differs from catalog");

  invariant(bundle.artifact_kind === "bindery_phase2_evidence_bundle", "unexpected evidence bundle");
  invariant(bundle.bundle_status === "empty_groundwork", "evidence bundle is not empty groundwork");
  invariant(bundle.created_at === null && bundle.completed_at === null, "empty bundle contains execution timestamps");
  invariant(bundle.target.program_id === EXPECTED_PROGRAM_ID, "bundle program id changed");
  invariant(bundle.target.supabase_project_ref === EXPECTED_PROJECT_REF, "bundle Supabase project changed");
  invariant(bundle.phase1_inspection_checkpoint.live_inspection_run === false, "bundle claims the Phase 1 inspection ran");
  invariant(bundle.phase1_inspection_checkpoint.selection_trusted === null, "bundle fabricates selection trust");
  invariant(bundle.phase1_inspection_checkpoint.source_enabled === null, "bundle fabricates source enablement");
  invariant(bundle.phase1_inspection_checkpoint.prepare_eligible === false, "bundle prepare_eligible must remain false");
  invariant(bundle.execution_authorization.approved === false, "bundle contains live approval");
  invariant(bundle.execution_authorization.live_flag_used === false, "bundle claims the live flag was used");
  invariant(bundle.redaction.secrets_included === false, "bundle claims secrets are included");
  invariant(Array.isArray(bundle.cases) && bundle.cases.length === catalog.cases.length, "bundle and catalog case counts differ");
  invariant(sameStrings(bundle.cases.map((item) => item.case_id), catalogIds), "bundle cases must match catalog order");
  invariant(bundle.cases.every((item) => item.status === "blocked"), "empty bundle must mark every live case blocked");
  invariant(bundle.cases.every((item) => Object.keys(item).every((key) => ["case_id", "component_kind", "status"].includes(key))), "empty bundle contains observed case evidence");

  return {
    case_count: catalog.cases.length,
    component_paths: catalog.component_paths,
    live_inspection_run: false,
    prepare_eligible: false,
    live_execution_implemented: false,
    network_clients_loaded: false
  };
}

function parseArgs(argv) {
  const options = { format: "human", assertSafe: false, executeLive: false, help: false };
  for (let index = 0; index < argv.length; index += 1) {
    const arg = argv[index];
    if (arg === "--format") {
      const value = argv[index + 1];
      if (value !== "human" && value !== "json") {
        throw new Error("--format must be 'human' or 'json'");
      }
      options.format = value;
      index += 1;
    } else if (arg === "--assert-safe") {
      options.assertSafe = true;
    } else if (arg === LIVE_FLAG) {
      options.executeLive = true;
    } else if (arg === "--help" || arg === "-h") {
      options.help = true;
    } else {
      throw new Error(`Unknown argument: ${arg}`);
    }
  }
  return options;
}

function printHelp() {
  console.log("Usage: node backend/chainbound_game/scripts/bindery-phase2-groundwork.cjs [options]");
  console.log("");
  console.log("  --format human|json  List the local case catalog (default: human)");
  console.log("  --assert-safe        Validate groundwork gates and empty evidence placeholders");
  console.log(`  ${LIVE_FLAG}       Reserved explicit live opt-in; always refuses in this groundwork`);
  console.log("  --help               Show this help");
}

function main() {
  const options = parseArgs(process.argv.slice(2));
  if (options.help) {
    printHelp();
    return;
  }

  const catalog = readJson(catalogPath);
  const schema = readJson(schemaPath);
  const bundle = readJson(emptyBundlePath);
  const safety = validateGroundwork(catalog, schema, bundle);

  if (options.executeLive) {
    const refusal = {
      ok: false,
      code: "LIVE_EXECUTION_NOT_IMPLEMENTED",
      message: "Refusing live execution: the Phase 1 inspection has not run, all Phase 2 cases are blocked, and this local harness has no RPC, signer, deployment, or transaction adapter.",
      required_future_flag: LIVE_FLAG,
      phase1_required_checkpoint: "selection_trusted=true, source.enabled=false, prepare_eligible=false",
      mutations_attempted: 0
    };
    if (options.format === "json") {
      console.error(JSON.stringify(refusal, null, 2));
    } else {
      console.error(`${refusal.code}: ${refusal.message}`);
      console.error(`Required checkpoint: ${refusal.phase1_required_checkpoint}`);
      console.error("Devnet mutations attempted: 0");
    }
    process.exitCode = 2;
    return;
  }

  if (options.format === "json") {
    console.log(JSON.stringify({
      ok: true,
      mode: options.assertSafe ? "assert-safe" : "list",
      status: catalog.status,
      target: catalog.target,
      phase1_gate: catalog.phase1_gate,
      safety,
      cases: catalog.cases.map((testCase) => ({
        id: testCase.id,
        title: testCase.title,
        component_paths: testCase.component_paths,
        targets: testCase.targets,
        status: testCase.status
      })),
      mutations_attempted: 0
    }, null, 2));
    return;
  }

  console.log("Bindery Phase 2 Devnet proof groundwork");
  console.log(`Program: ${catalog.target.program_id}`);
  console.log(`Cases: ${safety.case_count} (all BLOCKED_PHASE1_INSPECTION)`);
  console.log("Phase 1 live inspection: NOT RUN");
  console.log("prepare_eligible: false");
  console.log("Live execution adapter: NOT IMPLEMENTED");
  console.log("");
  for (const testCase of catalog.cases) {
    const paths = testCase.component_paths.length === 0
      ? "invalid-kind"
      : testCase.component_paths.map((kind) => kind === 0 ? "Stamped move (0)" : "Cosmetic (1)").join(", ");
    console.log(`${testCase.id} | ${paths} | ${testCase.title}`);
    console.log(`  targets: ${testCase.targets.join(" -> ")}`);
    console.log(`  status: ${testCase.status}`);
  }
  console.log("");
  console.log("Devnet mutations attempted: 0");
  if (options.assertSafe) {
    console.log("Safety assertions: PASS");
  }
}

try {
  main();
} catch (error) {
  console.error(error instanceof Error ? error.message : String(error));
  process.exitCode = 2;
}
