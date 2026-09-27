#!/usr/bin/env node

// Deterministic Phase 1 modifier-pool hash. Comments and display-only fields
// are excluded; all mechanical fields are encoded with explicit lengths so
// delimiters or empty trailing CSV columns cannot make the layout ambiguous.

const { createHash } = require("crypto");
const { readFileSync } = require("fs");
const { resolve } = require("path");

const FORMAT_MAGIC = Buffer.from("chainbound.modifier_pool\0", "utf8");
const FORMAT_VERSION = 1;
const GENERATOR_VERSION = 1;
const MECHANICAL_FIELDS = [
  "SectionId",
  "Category",
  "Scope",
  "MinRarity",
  "EffectMin",
  "EffectMax",
  "AllowedCategories",
  "ForbiddenCombos",
  "SlotCost",
  "IsTypeInfusion",
  "IsStatSub",
  "SubStat",
  "TriggerCondition",
  "TriggerChance",
  "PowerCap",
  "APCostCap",
  "ForbiddenTags",
  "SlotType",
  "Weight",
];
const EXCLUDED_DISPLAY_FIELDS = ["DisplayName", "EffectDescription"];

function assert(condition, message) {
  if (!condition) throw new Error(message);
}

function u16(value) {
  const bytes = Buffer.alloc(2);
  bytes.writeUInt16LE(value);
  return bytes;
}

function u32(value) {
  const bytes = Buffer.alloc(4);
  bytes.writeUInt32LE(value);
  return bytes;
}

function lengthPrefixed(value, lengthBytes) {
  const bytes = Buffer.from(value, "utf8");
  return Buffer.concat([lengthBytes === 2 ? u16(bytes.length) : u32(bytes.length), bytes]);
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
      } else if (char === '"') {
        quoted = false;
      } else {
        field += char;
      }
    } else if (char === '"') {
      quoted = true;
    } else if (char === ",") {
      record.push(field);
      field = "";
    } else if (char === "\n") {
      record.push(field);
      records.push(record);
      record = [];
      field = "";
    } else if (char !== "\r") {
      field += char;
    }
  }
  assert(!quoted, "CSV ends inside a quoted field.");
  if (field.length > 0 || record.length > 0) {
    record.push(field);
    records.push(record);
  }
  return records;
}

function normalize(value) {
  return String(value ?? "").trim().normalize("NFC");
}

function main() {
  const csvPath = resolve(
    process.argv[2] ||
      resolve(__dirname, "../../../Docs/Specs/Combat/Data/Modifiers.csv")
  );
  const source = readFileSync(csvPath, "utf8").replace(/^\uFEFF/, "");
  const sourceLines = source.split(/\r?\n/);
  const canonicalCsvText = sourceLines
    .filter((line) => line.trim() && !line.trimStart().startsWith("#"))
    .join("\n");
  const records = parseCsv(canonicalCsvText);
  assert(records.length >= 2, "Modifier CSV has no data rows.");

  const headers = records[0].map(normalize);
  assert(new Set(headers).size === headers.length, "Modifier CSV has duplicate headers.");
  for (const field of [...MECHANICAL_FIELDS, ...EXCLUDED_DISPLAY_FIELDS]) {
    assert(headers.includes(field), `Modifier CSV is missing ${field}.`);
  }

  const rows = records.slice(1).map((values, rowIndex) => {
    assert(
      values.length <= headers.length,
      `CSV row ${rowIndex + 2} has more fields than the header.`
    );
    const row = Object.fromEntries(
      headers.map((header, index) => [header, normalize(values[index])])
    );
    assert(row.SectionId, `CSV row ${rowIndex + 2} has no SectionId.`);
    return row;
  });
  const sectionIds = rows.map((row) => row.SectionId);
  assert(
    new Set(sectionIds).size === sectionIds.length,
    "Modifier CSV has duplicate SectionId values."
  );
  assert(sectionIds.includes("1.3-a"), "Pass A row 1.3-a is missing.");
  assert(sectionIds.some((id) => id.startsWith("6.8-")), "Pass A section 6.8 is missing.");

  const parts = [
    FORMAT_MAGIC,
    u32(FORMAT_VERSION),
    u32(GENERATOR_VERSION),
    u16(MECHANICAL_FIELDS.length),
    ...MECHANICAL_FIELDS.map((field) => lengthPrefixed(field, 2)),
    u32(rows.length),
  ];
  for (const row of rows) {
    for (const field of MECHANICAL_FIELDS) {
      parts.push(lengthPrefixed(row[field], 4));
    }
  }
  const canonicalBytes = Buffer.concat(parts);
  const digest = createHash("sha256").update(canonicalBytes).digest();

  process.stdout.write(
    `${JSON.stringify(
      {
        generatorVersion: GENERATOR_VERSION,
        formatVersion: FORMAT_VERSION,
        source: csvPath,
        sourceSha256: createHash("sha256").update(source, "utf8").digest("hex"),
        commentRule: "Exclude blank lines and lines whose first non-whitespace character is #.",
        rowOrder: "Preserve non-comment CSV source order.",
        stringNormalization: "Trim outer whitespace, normalize Unicode to NFC, encode UTF-8.",
        binaryLayout: [
          'UTF-8 bytes for "chainbound.modifier_pool\\0"',
          "format_version u32 little-endian",
          "generator_version u32 little-endian",
          "field_count u16 little-endian",
          "each field name: byte_length u16 little-endian + UTF-8 bytes",
          "row_count u32 little-endian",
          "for each row and fixed-order field: byte_length u32 little-endian + UTF-8 bytes",
        ],
        includedFields: MECHANICAL_FIELDS,
        excludedDisplayFields: EXCLUDED_DISPLAY_FIELDS,
        rowCount: rows.length,
        firstSectionId: rows[0].SectionId,
        lastSectionId: rows.at(-1).SectionId,
        canonicalByteLength: canonicalBytes.length,
        poolHashHex: digest.toString("hex"),
        poolHashBytes: [...digest],
      },
      null,
      2
    )}\n`
  );
}

try {
  main();
} catch (error) {
  process.stderr.write(`${error instanceof Error ? error.stack : String(error)}\n`);
  process.exitCode = 1;
}
