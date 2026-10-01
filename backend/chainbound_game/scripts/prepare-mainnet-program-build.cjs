#!/usr/bin/env node

/*
 * Prepare an isolated Chainbound Mainnet program workspace without changing
 * the canonical Devnet source tree or copying any program keypair.
 *
 * Preparation only:
 *   node scripts/prepare-mainnet-program-build.cjs --program-id <PUBKEY>
 *
 * Prepare and build a pinned SBF artifact:
 *   node scripts/prepare-mainnet-program-build.cjs --program-id <PUBKEY> --build
 */

const crypto = require("crypto");
const fs = require("fs");
const path = require("path");
const { spawnSync } = require("child_process");

const DEVNET_PROGRAM_ID = "9312ay7VrpvNYmyEx8TYCqhafbZ2Y5WGVbt6Qp772uUQ";
const PROGRAM_NAME = "chainbound_game";
const DEFAULT_TOOLS_VERSION = "v1.52";
const DEFAULT_ARCH = "v0";
const BASE58_ALPHABET = "123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz";

function fail(message) {
  console.error(`[prepare-mainnet-program-build] ${message}`);
  process.exit(1);
}

function parseArgs(argv) {
  const result = {
    build: false,
    help: false,
    programId: "",
    outputDir: "",
    toolsVersion: DEFAULT_TOOLS_VERSION,
    arch: DEFAULT_ARCH,
  };

  for (let index = 0; index < argv.length; index += 1) {
    const value = argv[index];
    if (value === "--build") result.build = true;
    else if (value === "--help" || value === "-h") result.help = true;
    else if (value === "--program-id") result.programId = argv[++index] ?? "";
    else if (value === "--output-dir") result.outputDir = argv[++index] ?? "";
    else if (value === "--tools-version") result.toolsVersion = argv[++index] ?? "";
    else if (value === "--arch") result.arch = argv[++index] ?? "";
    else fail(`unknown argument: ${value}`);
  }

  return result;
}

function printHelp() {
  console.log(`Usage:
  node scripts/prepare-mainnet-program-build.cjs --program-id <PUBKEY> [options]

Options:
  --build                    Run the pinned cargo build-sbf command.
  --output-dir <PATH>        New destination; must not already exist.
  --tools-version <VERSION>  Solana platform-tools version (default: ${DEFAULT_TOOLS_VERSION}).
  --arch <ARCH>              SBF architecture (default: ${DEFAULT_ARCH}).
  --help                     Show this help.

The script never reads, copies, creates, signs with, or deploys a keypair.`);
}

function decodeBase58(value) {
  let number = 0n;
  for (const character of value) {
    const digit = BASE58_ALPHABET.indexOf(character);
    if (digit < 0) throw new Error("invalid base58 character");
    number = number * 58n + BigInt(digit);
  }

  const decoded = [];
  while (number > 0n) {
    decoded.push(Number(number & 255n));
    number >>= 8n;
  }
  decoded.reverse();

  let leadingZeroes = 0;
  while (leadingZeroes < value.length && value[leadingZeroes] === "1") leadingZeroes += 1;
  return Buffer.concat([Buffer.alloc(leadingZeroes), Buffer.from(decoded)]);
}

function validateProgramId(value) {
  if (!value || decodeBase58(value).length !== 32) fail("--program-id must be a 32-byte Solana public key");
  if (value === DEVNET_PROGRAM_ID) fail("Mainnet program ID must differ from the Devnet program ID");
}

function sha256File(filePath) {
  return crypto.createHash("sha256").update(fs.readFileSync(filePath)).digest("hex");
}

function run(command, args, cwd, allowFailure = false) {
  const result = spawnSync(command, args, {
    cwd,
    encoding: "utf8",
    env: { ...process.env, NO_DNA: "1" },
    shell: false,
    stdio: allowFailure ? "pipe" : "inherit",
  });

  if (result.error) {
    if (allowFailure) return "";
    fail(`${command} failed to start: ${result.error.message}`);
  }
  if (result.status !== 0) {
    if (allowFailure) return "";
    fail(`${command} exited with code ${result.status}`);
  }
  return (result.stdout || "").trim();
}

function replaceOnce(filePath, searchValue, replacement, label) {
  const original = fs.readFileSync(filePath, "utf8");
  const first = original.indexOf(searchValue);
  if (first < 0 || original.indexOf(searchValue, first + searchValue.length) >= 0) {
    fail(`${label} did not contain exactly one expected identity marker`);
  }
  fs.writeFileSync(filePath, original.replace(searchValue, replacement), "utf8");
}

function patchAnchorToml(filePath, programId) {
  let text = fs.readFileSync(filePath, "utf8");
  if (/\[programs\.mainnet\]/.test(text)) fail("copied Anchor.toml already contains programs.mainnet");

  const devnetBlock = `[programs.devnet]\n${PROGRAM_NAME} = "${DEVNET_PROGRAM_ID}"`;
  if (!text.includes(devnetBlock)) fail("Anchor.toml Devnet block did not match the expected identity");
  text = text.replace(
    devnetBlock,
    `${devnetBlock}\n\n[programs.mainnet]\n${PROGRAM_NAME} = "${programId}"`,
  );
  text = text.replace(/(\[provider\]\s*\r?\ncluster\s*=\s*)"devnet"/, `$1"mainnet"`);
  fs.writeFileSync(filePath, text, "utf8");
}

function copyWorkspace(source, destination) {
  const excluded = new Set(["target", "node_modules", ".anchor", "test-ledger"]);
  fs.cpSync(source, destination, {
    recursive: true,
    errorOnExist: true,
    force: false,
    filter: (entry) => {
      const relative = path.relative(source, entry);
      if (!relative) return true;
      return !relative.split(path.sep).some((segment) => excluded.has(segment));
    },
  });
}

function toolVersion(command, args, cwd) {
  return run(command, args, cwd, true) || "unavailable";
}

const options = parseArgs(process.argv.slice(2));
if (options.help) {
  printHelp();
  process.exit(0);
}

try {
  validateProgramId(options.programId);
} catch (error) {
  fail(error.message);
}

if (!options.toolsVersion) fail("--tools-version cannot be empty");
if (!/^v[0-9]+$/.test(options.arch)) fail("--arch must look like v0, v1, v2, or v3");

const workspaceRoot = path.resolve(__dirname, "..");
const repositoryRoot = path.resolve(workspaceRoot, "..", "..");
const timestamp = new Date().toISOString().replace(/[:.]/g, "-");
const destination = options.outputDir
  ? path.resolve(options.outputDir)
  : path.join(repositoryRoot, ".utmp", "mainnet-program-build", `${timestamp}-${options.programId.slice(0, 8)}`);

if (fs.existsSync(destination)) fail(`destination already exists: ${destination}`);
fs.mkdirSync(path.dirname(destination), { recursive: true });

const canonicalLib = path.join(workspaceRoot, "programs", PROGRAM_NAME, "src", "lib.rs");
const canonicalLibHashBefore = sha256File(canonicalLib);
copyWorkspace(workspaceRoot, destination);

const copiedLib = path.join(destination, "programs", PROGRAM_NAME, "src", "lib.rs");
replaceOnce(
  copiedLib,
  `declare_id!("${DEVNET_PROGRAM_ID}");`,
  `declare_id!("${options.programId}");`,
  "copied lib.rs",
);
patchAnchorToml(path.join(destination, "Anchor.toml"), options.programId);

const copiedIdl = path.join(destination, "idl", `${PROGRAM_NAME}.json`);
if (fs.existsSync(copiedIdl)) {
  const idl = JSON.parse(fs.readFileSync(copiedIdl, "utf8"));
  if (idl.address !== DEVNET_PROGRAM_ID) fail("copied IDL address did not match the Devnet program ID");
  idl.address = options.programId;
  fs.writeFileSync(copiedIdl, `${JSON.stringify(idl, null, 2)}\n`, "utf8");
}

if (sha256File(canonicalLib) !== canonicalLibHashBefore) fail("canonical Devnet lib.rs changed during preparation");

const manifest = {
  schemaVersion: 1,
  preparedAtUtc: new Date().toISOString(),
  sourceWorkspace: path.relative(repositoryRoot, workspaceRoot).replaceAll(path.sep, "/"),
  isolatedWorkspace: path.relative(repositoryRoot, destination).replaceAll(path.sep, "/"),
  devnetProgramId: DEVNET_PROGRAM_ID,
  mainnetProgramId: options.programId,
  keypairCopied: false,
  deployAttempted: false,
  build: {
    requested: options.build,
    command: `cargo build-sbf --tools-version ${options.toolsVersion} --arch ${options.arch} --manifest-path programs/${PROGRAM_NAME}/Cargo.toml --sbf-out-dir target/deploy -- --locked`,
    toolsVersion: options.toolsVersion,
    arch: options.arch,
    artifactPath: null,
    artifactBytes: null,
    artifactSha256: null,
  },
  source: {
    gitCommit: toolVersion("git", ["rev-parse", "HEAD"], repositoryRoot),
    gitStatus: toolVersion("git", ["status", "--short", "--", "backend/chainbound_game"], repositoryRoot),
    cargoLockSha256: sha256File(path.join(workspaceRoot, "Cargo.lock")),
    canonicalLibSha256: canonicalLibHashBefore,
    isolatedLibSha256: sha256File(copiedLib),
  },
  toolchain: {
    anchor: toolVersion("anchor", ["--version"], workspaceRoot),
    solana: toolVersion("solana", ["--version"], workspaceRoot),
    rustc: toolVersion("rustc", ["--version"], workspaceRoot),
    cargo: toolVersion("cargo", ["--version"], workspaceRoot),
  },
};

if (options.build) {
  const outputDir = path.join(destination, "target", "deploy");
  fs.mkdirSync(outputDir, { recursive: true });
  run(
    "cargo",
    [
      "build-sbf",
      "--tools-version",
      options.toolsVersion,
      "--arch",
      options.arch,
      "--manifest-path",
      path.join("programs", PROGRAM_NAME, "Cargo.toml"),
      "--sbf-out-dir",
      outputDir,
      "--",
      "--locked",
    ],
    destination,
  );

  const artifact = path.join(outputDir, `${PROGRAM_NAME}.so`);
  if (!fs.existsSync(artifact)) fail(`expected artifact was not produced: ${artifact}`);
  manifest.build.artifactPath = path.relative(repositoryRoot, artifact).replaceAll(path.sep, "/");
  manifest.build.artifactBytes = fs.statSync(artifact).size;
  manifest.build.artifactSha256 = sha256File(artifact);
}

const manifestPath = path.join(destination, "mainnet-build-manifest.json");
fs.writeFileSync(manifestPath, `${JSON.stringify(manifest, null, 2)}\n`, "utf8");

console.log(`[prepare-mainnet-program-build] prepared: ${destination}`);
console.log(`[prepare-mainnet-program-build] manifest: ${manifestPath}`);
if (!options.build) console.log("[prepare-mainnet-program-build] no build requested; no artifact was produced");
