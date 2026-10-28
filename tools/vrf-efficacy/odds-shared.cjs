"use strict";

// Canonical roll-odds data and instruction builder shared by roll.cjs, the Squads signer,
// and simulation scripts. Keep the instruction encoding in this module only.
const { PublicKey, SystemProgram, TransactionInstruction } = require("@solana/web3.js");

const PROGRAM_ID = new PublicKey("9312ay7VrpvNYmyEx8TYCqhafbZ2Y5WGVbt6Qp772uUQ");
const UPDATE_ODDS_HEX = "fa19cbb40c73ade6";
const INITIALIZE_ODDS_HEX = "22ced12f11f2141d";
const DEFAULT_POOL_HASH = "a73693d8f57706f53b00fc0148ebe6cb073501c6cf13326c7ddb136a4c787067";

const KINDS = Object.freeze({ known: 1, stamp: 2 });
const TIERS = Object.freeze(["Common", "Uncommon", "Rare", "Epic", "Legendary", "Mythic"]);
const ODDS = Object.freeze({
  ultra: Object.freeze([640_000, 250_000, 90_000, 17_000, 2_800, 200]),
  steep: Object.freeze([620_000, 250_000, 100_000, 25_000, 4_500, 500]),
});

const pda = (seeds) => PublicKey.findProgramAddressSync(seeds, PROGRAM_ID)[0];
const gameConfigPda = () => pda([Buffer.from("game_config")]);
const rollOddsPda = (kind) => pda([Buffer.from("roll_odds"), Buffer.from([kind])]);

function cumulative(weights) {
  let sum = 0;
  const thresholds = weights.map((weight) => (sum += weight));
  if (thresholds.length !== TIERS.length || sum !== 1_000_000) {
    throw new Error("roll odds must contain six weights totaling 1,000,000 ppm");
  }
  return thresholds;
}

function weightsFromCumulative(thresholds) {
  return thresholds.map((threshold, index) => threshold - (index ? thresholds[index - 1] : 0));
}

function u32s(values) {
  const data = Buffer.alloc(4 * values.length);
  values.forEach((value, index) => data.writeUInt32LE(value, 4 * index));
  return data;
}

function normalizePoolHash(poolHash) {
  const bytes = Buffer.isBuffer(poolHash) ? poolHash : Buffer.from(poolHash);
  if (bytes.length !== 32) throw new Error("pool hash must be 32 bytes");
  return bytes;
}

function decodeOddsData(data) {
  const bytes = Buffer.from(data);
  if (bytes.length < 69) throw new Error(`roll odds account is too short (${bytes.length} bytes)`);
  return {
    kind: bytes[8],
    oddsVersion: bytes.readUInt32LE(9),
    cumulativePpm: [0, 1, 2, 3, 4, 5].map((index) => bytes.readUInt32LE(13 + 4 * index)),
    poolHash: bytes.subarray(37, 69).toString("hex"),
  };
}

async function readOdds(connection, kind) {
  const info = await connection.getAccountInfo(rollOddsPda(kind), "confirmed");
  return info ? decodeOddsData(info.data) : null;
}

function setOddsIx(authority, kind, cumulativePpm, poolHash, exists) {
  const hash = normalizePoolHash(poolHash);
  if (!Object.values(KINDS).includes(kind)) throw new Error(`unsupported roll kind ${kind}`);
  if (cumulativePpm.length !== TIERS.length || cumulativePpm[TIERS.length - 1] !== 1_000_000 ||
      cumulativePpm.some((threshold, index) => threshold <= (index ? cumulativePpm[index - 1] : 0))) {
    throw new Error("cumulative odds must strictly increase and end at 1,000,000 ppm");
  }

  const keys = exists
    ? [
        { pubkey: rollOddsPda(kind), isSigner: false, isWritable: true },
        { pubkey: gameConfigPda(), isSigner: false, isWritable: false },
        { pubkey: authority, isSigner: true, isWritable: false },
      ]
    : [
        { pubkey: rollOddsPda(kind), isSigner: false, isWritable: true },
        { pubkey: gameConfigPda(), isSigner: false, isWritable: false },
        { pubkey: authority, isSigner: true, isWritable: true },
        { pubkey: SystemProgram.programId, isSigner: false, isWritable: false },
      ];
  const data = exists
    ? Buffer.concat([Buffer.from(UPDATE_ODDS_HEX, "hex"), u32s(cumulativePpm), hash])
    : Buffer.concat([Buffer.from(INITIALIZE_ODDS_HEX, "hex"), Buffer.from([kind]), u32s(cumulativePpm), hash]);
  return new TransactionInstruction({ programId: PROGRAM_ID, keys, data });
}

module.exports = {
  DEFAULT_POOL_HASH,
  INITIALIZE_ODDS_HEX,
  KINDS,
  ODDS,
  PROGRAM_ID,
  TIERS,
  UPDATE_ODDS_HEX,
  cumulative,
  decodeOddsData,
  gameConfigPda,
  readOdds,
  rollOddsPda,
  setOddsIx,
  weightsFromCumulative,
};
