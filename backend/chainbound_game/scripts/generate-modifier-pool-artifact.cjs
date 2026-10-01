#!/usr/bin/env node

const { spawnSync } = require("child_process");
const { readFileSync, writeFileSync } = require("fs");
const { resolve } = require("path");

const hashScript = resolve(__dirname, "compute-modifier-pool-hash.cjs");
const csvPath = resolve(__dirname, "../../../Docs/Specs/Combat/Data/Modifiers.csv");
const hash = spawnSync(process.execPath, [hashScript, csvPath], { encoding: "utf8" });
if (hash.status !== 0) {
  process.stderr.write(hash.stderr || hash.stdout || "hash failed\n");
  process.exit(hash.status || 1);
}
const report = JSON.parse(hash.stdout);
const EXPECTED = "6cea5ce56944a88486cafbb568a628b5aa5664c3db652a79cd90016427aaa4b4";
const poolHashMatches = report.poolHashHex === EXPECTED && report.rowCount === 104;
if (!poolHashMatches) {
  process.stderr.write(
    `WARN: catalog mechanical hash/row mismatch. got hash=${report.poolHashHex} rows=${report.rowCount}; keeping pinned pool IDs ${EXPECTED} and writing effect bounds only.\n`
  );
}
function parseCsv(text) {
  const records = [];
  let record = [];
  let field = "";
  let quoted = false;
  for (let index = 0; index < text.length; index += 1) {
    const char = text[index];
    if (quoted) {
      if (char === '"' && text[index + 1] === '"') {
        field += '"';
        index += 1;
      } else if (char === '"') quoted = false;
      else field += char;
    } else if (char === '"') quoted = true;
    else if (char === ",") {
      record.push(field);
      field = "";
    } else if (char === "\n") {
      record.push(field);
      records.push(record);
      record = [];
      field = "";
    } else if (char !== "\r") field += char;
  }
  if (field.length > 0 || record.length > 0) {
    record.push(field);
    records.push(record);
  }
  return records;
}

const source = readFileSync(csvPath, "utf8").replace(/^\uFEFF/, "");
const canonical = source
  .split(/\r?\n/)
  .filter((line) => line.trim() && !line.trimStart().startsWith("#"))
  .join("\n");
const records = parseCsv(canonical);
const headers = records[0].map((value) => String(value ?? "").trim());
const sectionIndex = headers.indexOf("SectionId");
const minIndex = headers.indexOf("EffectMin");
const maxIndex = headers.indexOf("EffectMax");
const effectIndex = headers.indexOf("EffectDescription");
if (sectionIndex < 0) throw new Error("SectionId missing");
if (minIndex < 0 || maxIndex < 0 || effectIndex < 0) throw new Error("EffectMin/EffectMax/EffectDescription missing");
const sectionIds = records.slice(1).map((row) => String(row[sectionIndex] ?? "").trim());
if (sectionIds.length !== 104) throw new Error(`expected 104 sectionIds, got ${sectionIds.length}`);

function catalogNumber(value) {
  const parsed = Number(String(value ?? "").trim());
  return Number.isFinite(parsed) ? parsed : 0;
}

const quoted = sectionIds.map((id) => `  ${JSON.stringify(id)},`).join("\n");
const bounds = records.slice(1).map((row) => {
  const id = String(row[sectionIndex] ?? "").trim();
  return `  ${JSON.stringify(id)}: { min: ${catalogNumber(row[minIndex])}, max: ${catalogNumber(row[maxIndex])}, effect: ${JSON.stringify(String(row[effectIndex] ?? "").trim())} },`;
}).join("\n");
const contents = `// Generated from Docs/Specs/Combat/Data/Modifiers.csv. Do not edit by hand.
// sourceSha256=${report.sourceSha256}
// poolHash=${EXPECTED}
// catalogMechanicalHash=${report.poolHashHex}
// rowCount=${report.rowCount}

export const EXPECTED_MODIFIER_POOL_ROW_COUNT = 104 as const;
export const EXPECTED_MODIFIER_POOL_HASH =
  "6cea5ce56944a88486cafbb568a628b5aa5664c3db652a79cd90016427aaa4b4" as const;

export const MODIFIER_POOL = [
${quoted}
] as const;

export const MODIFIER_EFFECT_BOUNDS = {
${bounds}
} as const;

export type ModifierPoolSectionId = typeof MODIFIER_POOL[number];
`;

const targets = [
  resolve(__dirname, "../../../Supabase/functions/_shared/modifier_pool.generated.ts"),
  resolve(__dirname, "../../../Supabase/supabase/functions/_shared/modifier_pool.generated.ts"),
];
const existingIds = [...readFileSync(targets[0], "utf8").matchAll(/^\s+"([^"]+)",$/gm)].map((match) => match[1]);
if (existingIds.join("\0") !== sectionIds.join("\0")) {
  process.stderr.write("STOP: CSV section ID order differs from the pinned MODIFIER_POOL.\n");
  process.exit(2);
}
for (const target of targets) writeFileSync(target, contents, "utf8");
process.stdout.write(`wrote ${sectionIds.length} ids and effect bounds to ${targets.length} files\n`);
