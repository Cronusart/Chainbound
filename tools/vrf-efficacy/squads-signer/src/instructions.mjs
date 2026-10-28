// Inner instructions the Devnet vault can sign. Shared by the signing page and the Node smoke test
// (simulate.cjs) so both build byte-identical instructions.
import * as multisig from "@sqds/multisig";
import { PublicKey, SYSVAR_CLOCK_PUBKEY, SYSVAR_RENT_PUBKEY, SystemProgram, TransactionInstruction } from "@solana/web3.js";

export const PROGRAM_ID = new PublicKey("9312ay7VrpvNYmyEx8TYCqhafbZ2Y5WGVbt6Qp772uUQ");
export const MULTISIG = new PublicKey("9kcMg2WKwF2XdUBuKnpHnso4PRuVNPVgg1VuGtbjAv9m");
export const LOADER = new PublicKey("BPFLoaderUpgradeab1e11111111111111111111111");
// sha256("global:<name>")[0..8]
export const ACCEPT_HEX = "e0c165c3f3220a6c";
export const UPDATE_ODDS_HEX = "fa19cbb40c73ade6";
// UpgradeableLoaderInstruction variants, u32 little-endian.
export const LOADER_UPGRADE_HEX = "03000000";
export const LOADER_SET_AUTHORITY_HEX = "04000000";
export const KINDS = { known: 1, stamp: 2 };
export const TIERS = ["Common", "Uncommon", "Rare", "Epic", "Legendary", "Mythic"];
// Weights in parts per million, Common..Mythic; same table as roll.cjs.
export const ODDS = {
  ultra: [640_000, 250_000, 90_000, 17_000, 2_800, 200],
  steep: [620_000, 250_000, 100_000, 25_000, 4_500, 500],
};

export const [VAULT] = multisig.getVaultPda({ multisigPda: MULTISIG, index: 0 });
const pda = (seeds, program = PROGRAM_ID) => PublicKey.findProgramAddressSync(seeds, program)[0];
export const GAME_CONFIG = pda([Buffer.from("game_config")]);
export const AUTHORITY_TRANSFER = pda([Buffer.from("authority_transfer")]);
export const PROGRAMDATA = pda([PROGRAM_ID.toBuffer()], LOADER);
export const rollOddsPda = (kind) => pda([Buffer.from("roll_odds"), Buffer.from([kind])]);

export const cumulative = (weights) => { let sum = 0; return weights.map((w) => (sum += w)); };
const u32s = (values) => { const b = Buffer.alloc(4 * values.length); values.forEach((v, i) => b.writeUInt32LE(v, 4 * i)); return b; };

export function acceptInstruction(previousAuthority) {
  return new TransactionInstruction({
    programId: PROGRAM_ID,
    keys: [
      { pubkey: AUTHORITY_TRANSFER, isSigner: false, isWritable: true },
      { pubkey: GAME_CONFIG, isSigner: false, isWritable: true },
      { pubkey: VAULT, isSigner: true, isWritable: false },
      { pubkey: previousAuthority, isSigner: false, isWritable: true },
    ],
    data: Buffer.from(ACCEPT_HEX, "hex"),
  });
}

export function updateOddsInstruction(kind, cumulativePpm, poolHash, authority = VAULT) {
  return new TransactionInstruction({
    programId: PROGRAM_ID,
    keys: [
      { pubkey: rollOddsPda(kind), isSigner: false, isWritable: true },
      { pubkey: GAME_CONFIG, isSigner: false, isWritable: false },
      { pubkey: authority, isSigner: true, isWritable: false },
    ],
    data: Buffer.concat([Buffer.from(UPDATE_ODDS_HEX, "hex"), u32s(cumulativePpm), poolHash]),
  });
}

export function upgradeInstruction(buffer, spill, authority = VAULT) {
  return new TransactionInstruction({
    programId: LOADER,
    keys: [
      { pubkey: PROGRAMDATA, isSigner: false, isWritable: true },
      { pubkey: PROGRAM_ID, isSigner: false, isWritable: true },
      { pubkey: buffer, isSigner: false, isWritable: true },
      { pubkey: spill, isSigner: false, isWritable: true },
      { pubkey: SYSVAR_RENT_PUBKEY, isSigner: false, isWritable: false },
      { pubkey: SYSVAR_CLOCK_PUBKEY, isSigner: false, isWritable: false },
      { pubkey: authority, isSigner: true, isWritable: false },
    ],
    data: Buffer.from(LOADER_UPGRADE_HEX, "hex"),
  });
}

const extendData = (variant, additionalBytes) => {
  const b = Buffer.alloc(8);
  b.writeUInt32LE(variant, 0);
  b.writeUInt32LE(additionalBytes, 4);
  return b;
};

// ExtendProgram (variant 6): no authority, any payer. The loader requires at least 10,240 bytes.
export function extendInstruction(additionalBytes, payer) {
  return new TransactionInstruction({
    programId: LOADER,
    keys: [
      { pubkey: PROGRAMDATA, isSigner: false, isWritable: true },
      { pubkey: PROGRAM_ID, isSigner: false, isWritable: true },
      { pubkey: SystemProgram.programId, isSigner: false, isWritable: false },
      { pubkey: payer, isSigner: true, isWritable: true },
    ],
    data: extendData(6, additionalBytes),
  });
}

// ExtendProgramChecked (variant 9): needs the upgrade authority. Inactive behind feature gate
// 2oMRZEDWT2tqtYMofhmmfQ8SsjqUFzT6sYXppQDavxwz on Devnet and Mainnet as of 2026-10-27.
export function extendCheckedInstruction(additionalBytes, payer, authority = VAULT) {
  return new TransactionInstruction({
    programId: LOADER,
    keys: [
      { pubkey: PROGRAMDATA, isSigner: false, isWritable: true },
      { pubkey: PROGRAM_ID, isSigner: false, isWritable: true },
      { pubkey: authority, isSigner: true, isWritable: false },
      { pubkey: SystemProgram.programId, isSigner: false, isWritable: false },
      { pubkey: payer, isSigner: true, isWritable: true },
    ],
    data: extendData(9, additionalBytes),
  });
}

export function setUpgradeAuthorityInstruction(newAuthority, authority = VAULT) {
  return new TransactionInstruction({
    programId: LOADER,
    keys: [
      { pubkey: PROGRAMDATA, isSigner: false, isWritable: true },
      { pubkey: authority, isSigner: true, isWritable: false },
      { pubkey: newAuthority, isSigner: false, isWritable: false },
    ],
    data: Buffer.from(LOADER_SET_AUTHORITY_HEX, "hex"),
  });
}
