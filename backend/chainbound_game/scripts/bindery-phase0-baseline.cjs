#!/usr/bin/env node
"use strict";

// Phase 0 Bindery release-baseline generator and verifier.
// - `write` records deterministic hashes for the hardened local candidate.
// - `check` verifies the checked-in manifest against local bytes and policy pins.
// - `verify-live` proves the pinned Devnet ProgramData and OwnershipConfig still
//   match while reporting (not hiding) candidate deployment requirements.
// - `verify-supabase` proves the pinned read-only Edge revisions and exact
//   local/remote migration version parity. No command loads a signer, deploys,
//   applies a migration, or submits a transaction.

const { createHash } = require("crypto");
const { execFileSync } = require("child_process");
const { existsSync, readFileSync, writeFileSync } = require("fs");
const { dirname, relative, resolve } = require("path");
let Connection;
let PublicKey;

const PROJECT_ROOT = resolve(__dirname, "../../..");
const MANIFEST_PATH = resolve(
  PROJECT_ROOT,
  "Docs/Specs/Economy/manifests/bindery-devnet-baseline-v2.json"
);
const CONTRACT_PATH = "Docs/Specs/Economy/BINDERY_PROTOCOL_CONTRACT_v1.md";
const PROGRAM_ID = "9312ay7VrpvNYmyEx8TYCqhafbZ2Y5WGVbt6Qp772uUQ";
const PROGRAM_DATA = "5wqBqX7qubopQnteX5RPTwBJrSFA6xnMs9Zbwz2BHfzD";
const UPGRADEABLE_LOADER = "BPFLoaderUpgradeab1e11111111111111111111111";
const UPGRADE_AUTHORITY = "DkFmzzzF89tYZu2JWR1uq3H13U5np9NU6MHF4E61gJmG";
const DEVNET_GENESIS = "EtWTRABZaYq6iMfeYKouRu166VU2xqa1wcaWoxPkrZBG";
const CORE_PROGRAM = "CoREENxT6tW1HoK8ypY1SxRMZTcVPm7R94rH4PZNhX7d";
const CORE_AUTHORITY = "FNRyxJLpcGuWwEUXYiXqjJMDsHAAs1gU8CnkjjBRL3ys";
const PROGRAM_DATA_HEADER_BYTES = 45;

const OBSERVED_PROGRAM = Object.freeze({
  lastDeploymentSlot: 511286785,
  observedAtUtc: "2026-10-15T08:20:47Z",
  payloadBytes: 899312,
  payloadSha256:
    "a79a218bcfd88b5430893de135d0ffea1306f2082e2beebe466e98237852c8fe",
});

const OBSERVED_CANDIDATE = Object.freeze({
  builtAtUtc: "2026-10-16T03:57:25.620Z",
  payloadBytes: 899376,
  payloadSha256:
    "25e88efb33b77e07a74ef51f1226e2fea1fb55b44e3f65e656caad2c600e7e64",
});

const OBSERVED_SUPABASE = Object.freeze({
  projectRef: "jhzqcbrzkausrrtcavuu",
  observedAtUtc: "2026-10-16T04:00:21Z",
  migrationCount: 106,
  latestMigrationVersion: "20261015042331",
  functions: Object.freeze({
    ownership_prepare_bind: Object.freeze({
      version: 6,
      ezbrSha256:
        "b8a2a6a1286d368779731b7ec5cde990cf30fd94e39ab8e83175d7cf94d1ecc3",
    }),
    ownership_confirm_bind: Object.freeze({
      version: 6,
      ezbrSha256:
        "72de45cf4b6992e32eca2c3dcf7983cad72456ddf68e4dbf030ed289a5ce8698",
    }),
    ownership_reconcile_bind: Object.freeze({
      version: 6,
      ezbrSha256:
        "ac6c7f36aa1db96f6d0f3cd9afd45ceb1174654391c30904f1ace742aee4c2d9",
    }),
  }),
  normalizedSourceSha256: Object.freeze({
    "Supabase/functions/ownership_prepare_bind/index.ts":
      "b4c1a9a6ff675348f2cba3c96493a569ef25b2c509957ac44a337e7848559c60",
    "Supabase/functions/ownership_confirm_bind/index.ts":
      "8d2514c943b67df141d0f86a7d308d149617ae8461f559cf9d5113fdaad9ce51",
    "Supabase/functions/ownership_reconcile_bind/index.ts":
      "d48067e896a3e0db2892d9c7f0c5d57a6562cd3f4a0f7b5abdf3604f18a8e952",
    "Supabase/functions/_shared/account_helpers.ts":
      "d04c5a44cbac76c965d978e6298fc123bd56c0c48b5a7a80de8fd5c309379e3f",
    "Supabase/functions/_shared/modifier_pool.generated.ts":
      "3b8f818eb24e0db91c67c7bbb4775c54a1417dd7448f6727fb95a1ab2485481f",
    "Supabase/functions/_shared/ownership_bind.ts":
      "d7c2762c3993f4b8618d62882a12b057dabdd7b2a7a7fc76819305fb94714f06",
    "Supabase/functions/_shared/ownership_das_proxy.ts":
      "a385ccd6a0c54ba0e5bbbf0ea7ae38e37a8e08fdb1e4765036218c9e2be6c826",
    "Supabase/functions/_shared/ownership_spine.ts":
      "88c48e8bc57021086906be7a4e6380fc97eb09d7b026e070e315831c36af1449",
  }),
});

const OWNERSHIP_CONFIG = Object.freeze({
  address: "6Gj8af5cnLSKFWcdfFLr7fihhNmp97BdPRt76zWemPPZ",
  schemaVersion: 1,
  movesCollection: "AUQuvgpFDUekeVh2GHri99A59G4YY5LUWdGw9fKTB9wC",
  archivesCollection: "DP3rTx145BPfunueM9SHjYJXBDCK5GmrdwZjpCvgf6U",
  coversCollection: "6wPnru8RDTsnCyujzPMq8v9U8pD6ynRa8es3ZXcesbNs",
  cosmeticsCollection: "76aVsw9xScewNG4wkiE75asdM5bbZaHS5CG78263yMcV",
  stampRamCost: 75,
  bindRamCost: 250,
  generatorVersion: 1,
  poolHashHex:
    "6cea5ce56944a88486cafbb568a628b5aa5664c3db652a79cd90016427aaa4b4",
});

const RELEASE_INPUTS = Object.freeze([
  [CONTRACT_PATH, "frozen Bindery protocol contract"],
  [
    "backend/chainbound_game/scripts/bindery-phase0-baseline.cjs",
    "Phase 0 baseline verifier",
  ],
  [
    "backend/chainbound_game/programs/chainbound_game/src/lib.rs",
    "Anchor program source",
  ],
  [
    "backend/chainbound_game/programs/chainbound_game/Cargo.toml",
    "Anchor program dependency contract",
  ],
  ["backend/chainbound_game/Cargo.toml", "Anchor workspace build profile"],
  ["backend/chainbound_game/Cargo.lock", "locked Rust dependency graph"],
  ["backend/chainbound_game/rust-toolchain.toml", "pinned Rust toolchain"],
  [
    "backend/chainbound_game/Anchor.toml",
    "pinned Anchor toolchain and program registry",
  ],
  [
    "backend/chainbound_game/target/deploy/chainbound_game.so",
    "local deploy artifact",
  ],
  ["backend/chainbound_game/idl/chainbound_game.json", "canonical Anchor IDL"],
  [
    "Assets/Scripts/Solana/Programs/Generated/ChainboundGameProgramContract.Generated.cs",
    "generated Unity contract",
  ],
  ["Assets/Data/Solana/SolanaConfig.asset", "Unity Devnet configuration"],
]);

const EDGE_ENTRYPOINTS = Object.freeze([
  "Supabase/functions/ownership_prepare_bind/index.ts",
  "Supabase/functions/ownership_confirm_bind/index.ts",
  "Supabase/functions/ownership_reconcile_bind/index.ts",
]);

const MIGRATION_PATHS = Object.freeze([
  "Supabase/migrations/20260903010000_authoritative_trading_trace_capture.sql",
  "Supabase/migrations/20261001190000_authoritative_capture_capacity_retirement.sql",
  "Supabase/migrations/20261006110459_ram_spend_coordinator_settlement.sql",
  "Supabase/migrations/20261006110715_ownership_bind_coordination.sql",
  "Supabase/migrations/20261008120337_retire_press_charge_catalog_row.sql",
  "Supabase/migrations/20261009010622_ram_v2_postgres_canonical_earn.sql",
  "Supabase/migrations/20261013234258_unified_move_inventory.sql",
]);

function invariant(condition, message) {
  if (!condition) throw new Error(message);
}

function debug(message) {
  if (process.env.BINDERY_BASELINE_DEBUG === "1")
    process.stderr.write(`[bindery-baseline] ${message}\n`);
}

function absolute(projectPath) {
  const path = resolve(PROJECT_ROOT, projectPath);
  invariant(
    existsSync(path),
    `Required baseline input is missing: ${projectPath}`
  );
  return path;
}

function sha256(bytes) {
  return createHash("sha256").update(bytes).digest("hex");
}

function normalizedTextSha256(projectPath) {
  const text = readFileSync(absolute(projectPath), "utf8").replaceAll(
    "\r\n",
    "\n"
  );
  return sha256(Buffer.from(text, "utf8"));
}

function fileRecord(projectPath, role) {
  const bytes = readFileSync(absolute(projectPath));
  return {
    path: projectPath.replaceAll("\\", "/"),
    role,
    bytes: bytes.length,
    sha256: sha256(bytes),
  };
}

function localImportSpecifiers(source) {
  const matches = [];
  const pattern = /(?:from\s+|import\s*)["']([^"']+)["']/g;
  for (let match = pattern.exec(source); match; match = pattern.exec(source)) {
    if (match[1].startsWith(".")) matches.push(match[1]);
  }
  return matches;
}

function edgeDependencyClosure() {
  debug("walking Edge dependency closure");
  const pending = [...EDGE_ENTRYPOINTS];
  const found = new Set();
  while (pending.length > 0) {
    const projectPath = pending.pop().replaceAll("\\", "/");
    if (found.has(projectPath)) continue;
    found.add(projectPath);
    const source = readFileSync(absolute(projectPath), "utf8");
    const parent = dirname(projectPath);
    for (const specifier of localImportSpecifiers(source)) {
      let imported = relative(
        PROJECT_ROOT,
        resolve(PROJECT_ROOT, parent, specifier)
      ).replaceAll("\\", "/");
      if (!imported.endsWith(".ts")) imported += ".ts";
      pending.push(imported);
    }
  }
  debug(`Edge dependency closure contains ${found.size} files`);
  return [...found].sort();
}

function fileSetHash(paths) {
  const hash = createHash("sha256");
  for (const projectPath of [...paths].sort()) {
    hash.update(Buffer.from(projectPath.replaceAll("\\", "/"), "utf8"));
    hash.update(Buffer.from([0]));
    hash.update(readFileSync(absolute(projectPath)));
    hash.update(Buffer.from([0]));
  }
  return hash.digest("hex");
}

function gitRaw(args) {
  return execFileSync("git", args, { cwd: PROJECT_ROOT, encoding: "utf8" });
}

function git(args) {
  return gitRaw(args).trim();
}

function yamlScalar(source, key) {
  const match = source.match(new RegExp(`^[ \\t]*${key}:[ \\t]*(.*)$`, "m"));
  return match ? match[1].trim() : null;
}

function buildManifest() {
  debug("building manifest");
  const edgePaths = edgeDependencyClosure();
  debug("hashing Edge inputs");
  const edgeFiles = edgePaths.map((path) =>
    fileRecord(path, "canonical Bind Edge bundle input")
  );
  const migrationFiles = MIGRATION_PATHS.map((path) =>
    fileRecord(path, "Bindery authority/migration input")
  );
  const releaseFiles = RELEASE_INPUTS.map(([path, role]) =>
    fileRecord(path, role)
  );
  debug("reading git release-input state");
  const trackedPaths = [
    ...new Set([
      ...RELEASE_INPUTS.map(([path]) => path),
      ...edgePaths,
      ...MIGRATION_PATHS,
    ]),
  ];
  const dirty = gitRaw(["status", "--porcelain=v1", "--", ...trackedPaths])
    .split(/\r?\n/)
    .filter(Boolean);
  const unityConfigText = readFileSync(
    absolute("Assets/Data/Solana/SolanaConfig.asset"),
    "utf8"
  );
  debug("enumerating migrations");
  const allMigrationNames = require("fs")
    .readdirSync(absolute("Supabase/migrations"))
    .filter((name) => /^\d{14}_.+\.sql$/.test(name))
    .sort();
  const candidateArtifact = releaseFiles.find((item) =>
    item.path.endsWith("chainbound_game.so")
  );
  invariant(
    candidateArtifact.bytes === OBSERVED_CANDIDATE.payloadBytes &&
      candidateArtifact.sha256 === OBSERVED_CANDIDATE.payloadSha256,
    "Local program artifact is not the reviewed Phase 0 hardened candidate."
  );
  const remoteSourceComparisons = edgePaths.map((path) => {
    const remoteSha256 = OBSERVED_SUPABASE.normalizedSourceSha256[path] || null;
    const localSha256 = normalizedTextSha256(path);
    return {
      path,
      hashNormalization: "UTF-8 with CRLF normalized to LF",
      localSha256,
      remoteSha256,
      exactMatch: remoteSha256 === localSha256,
    };
  });
  const remoteSourceMismatches = remoteSourceComparisons
    .filter((item) => !item.exactMatch)
    .map((item) => item.path);
  const unityCollections = {
    moves: yamlScalar(unityConfigText, "coreMovesCollectionId"),
    archives: yamlScalar(unityConfigText, "coreArchivesCollectionId"),
    covers: yamlScalar(unityConfigText, "coreCoversCollectionId"),
    cosmetics: yamlScalar(unityConfigText, "coreCosmeticsCollectionId"),
  };
  const binderyConfigurationComplete =
    unityCollections.moves === OWNERSHIP_CONFIG.movesCollection &&
    unityCollections.archives === OWNERSHIP_CONFIG.archivesCollection &&
    unityCollections.covers === OWNERSHIP_CONFIG.coversCollection &&
    unityCollections.cosmetics === OWNERSHIP_CONFIG.cosmeticsCollection;

  debug("assembling manifest object");
  return {
    schemaVersion: 2,
    baselineId: "bindery-devnet-2026-10-15-v2",
    supersedesBaselineId: "bindery-devnet-2026-10-15-v1",
    status: "phase0_hardened_non_player",
    contractPath: CONTRACT_PATH,
    sourceControl: {
      capturedBaseCommit: git(["rev-parse", "HEAD"]),
      capturedBranch: git(["branch", "--show-current"]),
      releaseInputsClean: dirty.length === 0,
      releaseInputChanges: dirty,
      note: "The base commit identifies the parent revision. Exact release-input bytes are authoritative when the capture is dirty.",
    },
    cluster: { name: "devnet", genesisHash: DEVNET_GENESIS },
    program: {
      programId: PROGRAM_ID,
      programData: PROGRAM_DATA,
      loader: UPGRADEABLE_LOADER,
      upgradeAuthority: UPGRADE_AUTHORITY,
      live: {
        lastDeploymentSlot: OBSERVED_PROGRAM.lastDeploymentSlot,
        observedAtUtc: OBSERVED_PROGRAM.observedAtUtc,
        payloadBytes: OBSERVED_PROGRAM.payloadBytes,
        payloadCapacityBytes: OBSERVED_PROGRAM.payloadBytes,
        payloadSha256: OBSERVED_PROGRAM.payloadSha256,
      },
      candidate: {
        builtAtUtc: OBSERVED_CANDIDATE.builtAtUtc,
        payloadBytes: candidateArtifact.bytes,
        payloadSha256: candidateArtifact.sha256,
        exactLiveMatch:
          candidateArtifact.sha256 === OBSERVED_PROGRAM.payloadSha256,
        fitsCurrentCapacity:
          candidateArtifact.bytes <= OBSERVED_PROGRAM.payloadBytes,
        requiredProgramDataExtensionBytes: Math.max(
          0,
          candidateArtifact.bytes - OBSERVED_PROGRAM.payloadBytes
        ),
        canonicalMetadataSourceVerified: true,
        capturedPinnedBuildSucceeded: true,
        buildWarnings: [
          "mpl_core::hooked::plugin::registry_records_to_plugin_list reports an estimated 4224-byte frame, 128 bytes above the 4096-byte SBF warning threshold; review before upgrade and exercise archive CreateV2 in Phase 2.",
        ],
      },
    },
    buildToolchain: {
      anchorCli: "0.32.1",
      rust: "1.89.0",
      solanaCliUsedForAudit: "2.3.0",
      artifactProfile:
        "release with overflow checks and one codegen unit; fat LTO requested, but cargo-build-sbf warns the dual cdylib/lib crate types preclude LTO",
    },
    metaplexCore: { programId: CORE_PROGRAM, authorityPda: CORE_AUTHORITY },
    ownershipConfig: { ...OWNERSHIP_CONFIG },
    metadataContract: {
      archiveKind: "chainling_archive",
      stampedComponentKind: "stamped_move",
      cosmeticComponentKind: "cosmetic",
      retiredArchiveKind: "bound_chainling",
      retiredCosmeticComponentKind: "cosmetic_skin",
      boundMoveTierExists: false,
      pressChargeRequired: false,
    },
    stateAuthority: {
      model: "hybrid_with_explicit_reconciliation",
      chainlingAndCapture:
        "BattleService and Postgres; result-authority-attested on-chain Bind projection",
      ram: "Postgres canonical ledger; on-chain v1 reservation/profile is an execution guard and reconciled mirror",
      coreComponentsAndArchive:
        "Metaplex Core accounts plus chainbound_game validation",
      offchainProjection:
        "receipt-verified Supabase confirm/reconcile settlement",
    },
    unityConfig: {
      path: "Assets/Data/Solana/SolanaConfig.asset",
      gameProgramId: yamlScalar(unityConfigText, "gameProgramId"),
      coreProgramId: yamlScalar(unityConfigText, "metaplexCoreProgramId"),
      cluster: yamlScalar(unityConfigText, "ownershipCluster"),
      expectedGenesisHash: yamlScalar(unityConfigText, "expectedGenesisHash"),
      movesCollection: unityCollections.moves,
      coreAuthority: yamlScalar(unityConfigText, "coreMovesUpdateAuthority"),
      archivesCollection: unityCollections.archives,
      coversCollection: unityCollections.covers,
      cosmeticsCollection: unityCollections.cosmetics,
      binderyConfigurationComplete,
    },
    releaseInputs: releaseFiles,
    edge: {
      canonicalSourceRoot: "Supabase/functions",
      excludedSourceRoot: "Supabase/supabase/functions",
      hashAlgorithm:
        "sha256(path-utf8 + NUL + exact-file-bytes + NUL), paths sorted ordinal",
      bundleSha256: fileSetHash(edgePaths),
      files: edgeFiles,
      remoteDeployment: {
        projectRef: OBSERVED_SUPABASE.projectRef,
        observedAtUtc: OBSERVED_SUPABASE.observedAtUtc,
        revisionVerified: true,
        functions: OBSERVED_SUPABASE.functions,
        sourceComparisons: remoteSourceComparisons,
        exactLocalSourceParity: remoteSourceMismatches.length === 0,
        localSourceMismatches: remoteSourceMismatches,
      },
    },
    migrations: {
      canonicalSourceRoot: "Supabase/migrations",
      latestLocalVersion: allMigrationNames.at(-1).slice(0, 14),
      localMigrationCount: allMigrationNames.length,
      remoteHistoryVerified: true,
      remoteObservedAtUtc: OBSERVED_SUPABASE.observedAtUtc,
      remoteMigrationCount: OBSERVED_SUPABASE.migrationCount,
      remoteLatestVersion: OBSERVED_SUPABASE.latestMigrationVersion,
      exactLocalRemoteVersionParity:
        allMigrationNames.length === OBSERVED_SUPABASE.migrationCount &&
        allMigrationNames.at(-1).slice(0, 14) ===
          OBSERVED_SUPABASE.latestMigrationVersion,
      files: migrationFiles,
    },
    featureGates: {
      playerBindery: "disabled",
      bindSourceDefault: "disabled",
      deploymentAuthorizedByThisManifest: false,
      transactionAuthorizedByThisManifest: false,
    },
    knownDeltas: [
      `The live program remains the immutable v1 baseline. The canonical metadata fix is in the local candidate and requires an explicitly approved same-program upgrade plus ${Math.max(
        0,
        candidateArtifact.bytes - OBSERVED_PROGRAM.payloadBytes
      )} bytes of ProgramData extension before durable Bind proof.`,
      "The deployed Bind Edge entrypoints are pinned at version 6. Six of eight normalized source inputs match; modifier_pool.generated.ts and ownership_spine.ts are newer locally and require a reviewed deployment/rebase decision.",
      "The candidate SBF build retains the mpl-core 4224-byte frame-size warning; review and Devnet archive CreateV2 proof are required before upgrade approval.",
      "Relevant release inputs were captured from a dirty worktree; use their exact hashes, not the base commit alone.",
    ],
  };
}

function assertRecord(record) {
  const actual = fileRecord(record.path, record.role);
  invariant(
    actual.bytes === record.bytes,
    `${record.path} byte length changed (${actual.bytes} != ${record.bytes}).`
  );
  invariant(
    actual.sha256 === record.sha256,
    `${record.path} SHA-256 changed (${actual.sha256} != ${record.sha256}).`
  );
}

function validatePolicy(manifest) {
  invariant(
    manifest.schemaVersion === 2 &&
      manifest.status === "phase0_hardened_non_player",
    "Baseline status is not fail-closed."
  );
  invariant(
    manifest.featureGates.playerBindery === "disabled",
    "Player Bindery gate must remain disabled."
  );
  invariant(
    manifest.featureGates.bindSourceDefault === "disabled",
    "Bind sources must default disabled."
  );
  invariant(
    manifest.metadataContract.archiveKind === "chainling_archive",
    "Archive kind drifted."
  );
  invariant(
    manifest.metadataContract.cosmeticComponentKind === "cosmetic",
    "Cosmetic component kind drifted."
  );
  invariant(
    manifest.metadataContract.boundMoveTierExists === false,
    "Bound move tier must not exist."
  );
  invariant(
    manifest.metadataContract.pressChargeRequired === false,
    "Press Charge must not be required."
  );
  invariant(
    manifest.ownershipConfig.bindRamCost === 250,
    "Bind RAM cost drifted from 250."
  );
  invariant(
    manifest.edge.canonicalSourceRoot === "Supabase/functions",
    "Canonical Edge root drifted."
  );
  invariant(
    manifest.unityConfig.binderyConfigurationComplete === true,
    "Unity Bindery collection configuration is incomplete."
  );
  invariant(
    manifest.program.candidate.requiredProgramDataExtensionBytes ===
      Math.max(
        0,
        manifest.program.candidate.payloadBytes -
          manifest.program.live.payloadCapacityBytes
      ),
    "Candidate ProgramData extension calculation drifted."
  );
  invariant(
    manifest.edge.remoteDeployment.revisionVerified === true,
    "Supabase Bind revisions have not been captured."
  );
  invariant(
    manifest.migrations.remoteHistoryVerified === true &&
      manifest.migrations.exactLocalRemoteVersionParity === true,
    "Supabase migration parity has not been captured."
  );

  const contract = readFileSync(absolute(manifest.contractPath), "utf8");
  invariant(
    contract.includes("cb.kind=chainling_archive"),
    "Contract omits canonical archive metadata."
  );
  invariant(
    contract.includes("cb.bind_component_kind=cosmetic"),
    "Contract omits canonical cosmetic metadata."
  );
  invariant(
    contract.includes("Postgres is canonical"),
    "Contract omits the canonical RAM decision."
  );
  const programSource = readFileSync(
    absolute("backend/chainbound_game/programs/chainbound_game/src/lib.rs"),
    "utf8"
  );
  invariant(
    /attribute\("cb\.kind",\s*"chainling_archive"\.to_string\(\)\)/.test(
      programSource
    ),
    "Program source does not emit the canonical archive kind."
  );
  invariant(
    !/attribute\("cb\.kind",\s*"bound_chainling"\.to_string\(\)\)/.test(
      programSource
    ),
    "Program source still emits the retired archive kind."
  );
  invariant(
    /"cb\.bind_component_kind"[\s\S]{0,180}"cosmetic"\.to_string\(\)/.test(
      programSource
    ),
    "Program source does not emit the canonical cosmetic component kind."
  );
  const allowlist = readFileSync(
    absolute("Docs/Release/BETA_FEATURE_ALLOWLIST.md"),
    "utf8"
  );
  invariant(
    /Bindery press \/ Core consume[^\n]+\*\*Gate\*\*/.test(allowlist),
    "Release allowlist no longer gates Bindery/Core consume."
  );
  const forbiddenActiveTerm =
    /\bpress_charge\b|\bbound_move\b|\bcosmetic_skin\b|\bbound_chainling\b/i;
  for (const record of manifest.edge.files) {
    const source = readFileSync(absolute(record.path), "utf8");
    invariant(
      !forbiddenActiveTerm.test(source),
      `Retired Bindery vocabulary is selected by ${record.path}.`
    );
  }
}

function checkLocal(manifest) {
  for (const record of manifest.releaseInputs) assertRecord(record);
  for (const record of manifest.edge.files) assertRecord(record);
  for (const record of manifest.migrations.files) assertRecord(record);
  invariant(
    edgeDependencyClosure().join("\n") ===
      manifest.edge.files.map((item) => item.path).join("\n"),
    "Bind Edge dependency closure changed."
  );
  invariant(
    fileSetHash(manifest.edge.files.map((item) => item.path)) ===
      manifest.edge.bundleSha256,
    "Bind Edge bundle hash changed."
  );
  for (const comparison of manifest.edge.remoteDeployment.sourceComparisons) {
    invariant(
      normalizedTextSha256(comparison.path) === comparison.localSha256,
      `${comparison.path} normalized source hash changed.`
    );
  }
  validatePolicy(manifest);
  return {
    verified: true,
    scope: "local",
    baselineId: manifest.baselineId,
    releaseInputCount: manifest.releaseInputs.length,
    edgeFileCount: manifest.edge.files.length,
    migrationCount: manifest.migrations.files.length,
    unityBinderyConfigurationComplete:
      manifest.unityConfig.binderyConfigurationComplete,
    candidateProgramDataExtensionBytes:
      manifest.program.candidate.requiredProgramDataExtensionBytes,
    remoteEdgeExactLocalSourceParity:
      manifest.edge.remoteDeployment.exactLocalSourceParity,
    playerBindery: manifest.featureGates.playerBindery,
  };
}

function decodeOwnershipConfig(account, expectedBump) {
  invariant(
    account && account.owner.equals(new PublicKey(PROGRAM_ID)),
    "OwnershipConfig missing or has the wrong owner."
  );
  const data = account.data;
  invariant(
    data.length === 183,
    `OwnershipConfig length is ${data.length}, expected 183.`
  );
  const decoded = {
    schemaVersion: data.readUInt16LE(8),
    movesCollection: new PublicKey(data.subarray(10, 42)).toBase58(),
    archivesCollection: new PublicKey(data.subarray(42, 74)).toBase58(),
    coversCollection: new PublicKey(data.subarray(74, 106)).toBase58(),
    cosmeticsCollection: new PublicKey(data.subarray(106, 138)).toBase58(),
    stampRamCost: data.readUInt32LE(138),
    bindRamCost: data.readUInt32LE(142),
    generatorVersion: data.readUInt32LE(146),
    poolHashHex: data.subarray(150, 182).toString("hex"),
    bump: data[182],
  };
  for (const [key, value] of Object.entries(OWNERSHIP_CONFIG)) {
    if (key === "address") continue;
    invariant(
      decoded[key] === value,
      `OwnershipConfig ${key} changed (${decoded[key]} != ${value}).`
    );
  }
  invariant(decoded.bump === expectedBump, "OwnershipConfig bump changed.");
  return decoded;
}

async function verifyLive(manifest) {
  checkLocal(manifest);
  ({ Connection, PublicKey } = require("@solana/web3.js"));
  const rpcUrl =
    process.env.CHAINBOUND_SOLANA_RPC_URL || "https://api.devnet.solana.com";
  const connection = new Connection(rpcUrl, "confirmed");
  const programKey = new PublicKey(PROGRAM_ID);
  const [derivedOwnershipConfig, ownershipBump] =
    PublicKey.findProgramAddressSync(
      [Buffer.from("ownership_config")],
      programKey
    );
  const [derivedCoreAuthority] = PublicKey.findProgramAddressSync(
    [Buffer.from("core_authority")],
    programKey
  );
  invariant(
    derivedOwnershipConfig.toBase58() === OWNERSHIP_CONFIG.address,
    "OwnershipConfig PDA changed."
  );
  invariant(
    derivedCoreAuthority.toBase58() === CORE_AUTHORITY,
    "Core authority PDA changed."
  );

  const [genesisHash, program, programData, ownershipAccount] =
    await Promise.all([
      connection.getGenesisHash(),
      connection.getAccountInfo(programKey, "confirmed"),
      connection.getAccountInfo(new PublicKey(PROGRAM_DATA), "confirmed"),
      connection.getAccountInfo(derivedOwnershipConfig, "confirmed"),
    ]);
  invariant(
    genesisHash === DEVNET_GENESIS,
    `RPC is not Devnet (${genesisHash}).`
  );
  invariant(
    program && program.owner.toBase58() === UPGRADEABLE_LOADER,
    "Program account missing or loader changed."
  );
  invariant(
    program.data.readUInt32LE(0) === 2,
    "Program account state variant changed."
  );
  invariant(
    new PublicKey(program.data.subarray(4, 36)).toBase58() === PROGRAM_DATA,
    "ProgramData link changed."
  );
  invariant(
    programData && programData.owner.toBase58() === UPGRADEABLE_LOADER,
    "ProgramData missing or loader changed."
  );
  invariant(
    programData.data.readUInt32LE(0) === 3,
    "ProgramData state variant changed."
  );
  invariant(
    programData.data[12] === 1,
    "ProgramData unexpectedly became immutable."
  );
  invariant(
    new PublicKey(programData.data.subarray(13, 45)).toBase58() ===
      UPGRADE_AUTHORITY,
    "Upgrade authority changed."
  );

  const lastDeploymentSlot = Number(programData.data.readBigUInt64LE(4));
  const payload = programData.data.subarray(PROGRAM_DATA_HEADER_BYTES);
  const payloadSha256 = sha256(payload);
  invariant(
    lastDeploymentSlot === manifest.program.live.lastDeploymentSlot,
    `Program deployment slot changed (${lastDeploymentSlot}).`
  );
  invariant(
    payload.length === manifest.program.live.payloadBytes,
    `Program payload size changed (${payload.length}).`
  );
  invariant(
    payloadSha256 === manifest.program.live.payloadSha256,
    `Program payload hash changed (${payloadSha256}).`
  );
  const decodedOwnershipConfig = decodeOwnershipConfig(
    ownershipAccount,
    ownershipBump
  );

  return {
    verified: true,
    scope: "local_and_live_devnet",
    baselineId: manifest.baselineId,
    rpcOrigin: new URL(rpcUrl).origin,
    genesisHash,
    programId: PROGRAM_ID,
    programData: PROGRAM_DATA,
    lastDeploymentSlot,
    payloadBytes: payload.length,
    payloadSha256,
    candidatePayloadBytes: manifest.program.candidate.payloadBytes,
    candidatePayloadSha256: manifest.program.candidate.payloadSha256,
    candidateExactLiveMatch: manifest.program.candidate.exactLiveMatch,
    requiredProgramDataExtensionBytes:
      manifest.program.candidate.requiredProgramDataExtensionBytes,
    sameProgramUpgradeRequired: !manifest.program.candidate.exactLiveMatch,
    ownershipConfig: decodedOwnershipConfig,
    mutationsPerformed: false,
  };
}

function supabaseJson(args) {
  return JSON.parse(
    execFileSync("supabase", args, {
      cwd: resolve(PROJECT_ROOT, "Supabase"),
      encoding: "utf8",
      env: { ...process.env, SUPABASE_DISABLE_TELEMETRY: "1" },
      windowsHide: true,
    })
  );
}

function verifySupabase(manifest) {
  checkLocal(manifest);
  const functions = supabaseJson([
    "functions",
    "list",
    "--project-ref",
    manifest.edge.remoteDeployment.projectRef,
    "--output",
    "json",
  ]);
  const bySlug = new Map(functions.map((item) => [item.slug, item]));
  const verifiedFunctions = {};
  for (const [slug, expected] of Object.entries(
    manifest.edge.remoteDeployment.functions
  )) {
    const actual = bySlug.get(slug);
    invariant(actual && actual.status === "ACTIVE", `${slug} is not ACTIVE.`);
    invariant(
      actual.version === expected.version,
      `${slug} version changed (${actual.version} != ${expected.version}).`
    );
    invariant(
      actual.ezbr_sha256 === expected.ezbrSha256,
      `${slug} bundle hash changed (${actual.ezbr_sha256} != ${expected.ezbrSha256}).`
    );
    verifiedFunctions[slug] = {
      status: actual.status,
      version: actual.version,
      ezbrSha256: actual.ezbr_sha256,
    };
  }

  const migrationResult = supabaseJson(["migration", "list", "--linked"]);
  const migrations = migrationResult.migrations || [];
  const localVersions = require("fs")
    .readdirSync(absolute("Supabase/migrations"))
    .filter((name) => /^\d{14}_.+\.sql$/.test(name))
    .map((name) => name.slice(0, 14))
    .sort();
  const remoteVersions = migrations
    .map((item) => item.remote)
    .filter(Boolean)
    .sort();
  invariant(
    localVersions.join("\n") === remoteVersions.join("\n"),
    "Local and remote Supabase migration versions differ."
  );
  invariant(
    remoteVersions.length === manifest.migrations.remoteMigrationCount &&
      remoteVersions.at(-1) === manifest.migrations.remoteLatestVersion,
    "Remote Supabase migration evidence changed."
  );

  return {
    verified: true,
    scope: "read_only_supabase_devnet",
    baselineId: manifest.baselineId,
    projectRef: manifest.edge.remoteDeployment.projectRef,
    functions: verifiedFunctions,
    migrationCount: remoteVersions.length,
    latestMigrationVersion: remoteVersions.at(-1),
    exactLocalRemoteMigrationVersionParity: true,
    exactLocalRemoteEdgeSourceParity:
      manifest.edge.remoteDeployment.exactLocalSourceParity,
    localEdgeSourceMismatches:
      manifest.edge.remoteDeployment.localSourceMismatches,
    mutationsPerformed: false,
  };
}

async function main() {
  const command = process.argv[2] || "check";
  if (command === "write") {
    const manifest = buildManifest();
    require("fs").mkdirSync(dirname(MANIFEST_PATH), { recursive: true });
    writeFileSync(
      MANIFEST_PATH,
      `${JSON.stringify(manifest, null, 2)}\n`,
      "utf8"
    );
    process.stdout.write(
      `${JSON.stringify(
        {
          written: relative(PROJECT_ROOT, MANIFEST_PATH).replaceAll("\\", "/"),
          baselineId: manifest.baselineId,
        },
        null,
        2
      )}\n`
    );
    return;
  }

  invariant(
    existsSync(MANIFEST_PATH),
    `Manifest does not exist. Run '${process.argv[1]} write'.`
  );
  const manifest = JSON.parse(readFileSync(MANIFEST_PATH, "utf8"));
  if (command === "check") {
    process.stdout.write(`${JSON.stringify(checkLocal(manifest), null, 2)}\n`);
    return;
  }
  if (command === "verify-live") {
    process.stdout.write(
      `${JSON.stringify(await verifyLive(manifest), null, 2)}\n`
    );
    return;
  }
  if (command === "verify-supabase") {
    process.stdout.write(
      `${JSON.stringify(verifySupabase(manifest), null, 2)}\n`
    );
    return;
  }
  throw new Error(
    "Usage: bindery-phase0-baseline.cjs <write|check|verify-live|verify-supabase>"
  );
}

main().catch((error) => {
  process.stderr.write(
    `${error instanceof Error ? error.stack || error.message : String(error)}\n`
  );
  process.exitCode = 1;
});
