// Inner instructions the Devnet vault can sign. Shared by the signing page and Node simulations.
import * as multisig from "@sqds/multisig";
import { PublicKey, SYSVAR_CLOCK_PUBKEY, SYSVAR_RENT_PUBKEY, SystemProgram, TransactionInstruction } from "@solana/web3.js";
import oddsShared from "../../odds-shared.cjs";

export const PROGRAM_ID = oddsShared.PROGRAM_ID;
export const MULTISIG = new PublicKey("9kcMg2WKwF2XdUBuKnpHnso4PRuVNPVgg1VuGtbjAv9m");
export const LOADER = new PublicKey("BPFLoaderUpgradeab1e11111111111111111111111");
// sha256("global:<name>")[0..8]
export const ACCEPT_HEX = "e0c165c3f3220a6c";
export const UPDATE_ODDS_HEX = oddsShared.UPDATE_ODDS_HEX;
// UpgradeableLoaderInstruction variants, u32 little-endian.
export const LOADER_UPGRADE_HEX = "03000000";
export const LOADER_SET_AUTHORITY_HEX = "04000000";
export const DEFAULT_POOL_HASH = oddsShared.DEFAULT_POOL_HASH;
export const KINDS = oddsShared.KINDS;
export const TIERS = oddsShared.TIERS;
export const ODDS = oddsShared.ODDS;
export const cumulative = oddsShared.cumulative;
export const decodeOddsData = oddsShared.decodeOddsData;
export const readOdds = oddsShared.readOdds;
export const rollOddsPda = oddsShared.rollOddsPda;
export const setOddsIx = oddsShared.setOddsIx;
export const weightsFromCumulative = oddsShared.weightsFromCumulative;

export const [VAULT] = multisig.getVaultPda({ multisigPda: MULTISIG, index: 0 });
const pda = (seeds, program = PROGRAM_ID) => PublicKey.findProgramAddressSync(seeds, program)[0];
export const GAME_CONFIG = oddsShared.gameConfigPda();
export const AUTHORITY_TRANSFER = pda([Buffer.from("authority_transfer")]);
export const PROGRAMDATA = pda([PROGRAM_ID.toBuffer()], LOADER);

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
