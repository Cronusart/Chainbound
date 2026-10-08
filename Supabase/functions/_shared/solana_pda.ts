import { sha256 } from "npm:@noble/hashes@1.8.0/sha2";
import { ed25519 } from "npm:@noble/curves@1.9.7/ed25519";
import bs58 from "npm:bs58@5.0.0";

const PDA_MARKER = new TextEncoder().encode("ProgramDerivedAddress");
// Mirrors @solana/web3.js PublicKey limits (MAX_SEED_LENGTH = 32) and the runtime MAX_SEEDS = 16.
const MAX_SEED_LENGTH = 32;
const MAX_SEEDS = 16;

function concat(chunks: Uint8Array[]): Uint8Array {
  let total = 0;
  for (let i = 0; i < chunks.length; i++) total += chunks[i].length;
  const out = new Uint8Array(total);
  let offset = 0;
  for (let i = 0; i < chunks.length; i++) {
    out.set(chunks[i], offset);
    offset += chunks[i].length;
  }
  return out;
}

function toHex(bytes: Uint8Array): string {
  let hex = "";
  for (let i = 0; i < bytes.length; i++) {
    hex += bytes[i].toString(16).padStart(2, "0");
  }
  return hex;
}

export function decodePublicKeyBytes(base58Key: string): Uint8Array {
  let bytes: Uint8Array;
  try {
    bytes = bs58.decode(base58Key);
  } catch {
    throw new Error("invalid_public_key");
  }
  if (bytes.length !== 32) throw new Error("invalid_public_key");
  return bytes;
}

export function encodePublicKey(bytes: Uint8Array): string {
  if (bytes.length !== 32) throw new Error("invalid_public_key");
  return bs58.encode(bytes);
}

export function isOnCurve(bytes: Uint8Array): boolean {
  if (bytes.length !== 32) return false;
  try {
    ed25519.ExtendedPoint.fromHex(toHex(bytes));
    return true;
  } catch {
    return false;
  }
}

export function findProgramAddress(
  seeds: Uint8Array[],
  programId: Uint8Array,
): { address: string; bump: number } {
  if (programId.length !== 32) throw new Error("invalid_public_key");
  if (seeds.length > MAX_SEEDS) throw new Error("max_seeds_exceeded");
  for (let i = 0; i < seeds.length; i++) {
    if (seeds[i].length > MAX_SEED_LENGTH) throw new Error("max_seed_length_exceeded");
  }
  // Same search order as web3.js findProgramAddressSync: bump 255 down to 1.
  for (let bump = 255; bump > 0; bump--) {
    const hash = sha256(concat([...seeds, Uint8Array.of(bump), programId, PDA_MARKER]));
    if (!isOnCurve(hash)) {
      return { address: encodePublicKey(hash), bump };
    }
  }
  throw new Error("unable_to_find_program_address");
}

export function derivePlayerProfilePda(walletPublicKey: string, programId: string): string {
  let wallet: Uint8Array;
  let program: Uint8Array;
  try {
    wallet = decodePublicKeyBytes(walletPublicKey);
  } catch {
    throw new Error("invalid_wallet");
  }
  try {
    program = decodePublicKeyBytes(programId);
  } catch {
    throw new Error("invalid_program_id");
  }
  return findProgramAddress(
    [new TextEncoder().encode("player_profile"), wallet],
    program,
  ).address;
}
