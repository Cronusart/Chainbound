import { assertEquals, assertThrows } from "https://deno.land/std@0.224.0/assert/mod.ts";
import { decodePublicKeyBytes, derivePlayerProfilePda, findProgramAddress, isOnCurve } from "./solana_pda.ts";

Deno.test("findProgramAddress matches Solana web3.js talking-squirrels vector", () => {
  const programId = decodePublicKeyBytes("BPFLoaderUpgradeab1e11111111111111111111111");
  const derived = findProgramAddress(
    [new TextEncoder().encode("Talking"), new TextEncoder().encode("Squirrels")],
    programId,
  );
  assertEquals(derived.address, "HTqKuCuTUMwRJV4ceegG2CwYRxub4qjpj9DEg3nz1NGF");
  assertEquals(derived.bump, 255);
  assertEquals(isOnCurve(decodePublicKeyBytes(derived.address)), false);
});

Deno.test("derivePlayerProfilePda is stable for a fixed wallet and program", () => {
  const system = "11111111111111111111111111111111";
  const first = derivePlayerProfilePda(system, system);
  const second = derivePlayerProfilePda(system, system);
  assertEquals(first, second);
  assertEquals(decodePublicKeyBytes(first).length, 32);
  assertEquals(isOnCurve(decodePublicKeyBytes(first)), false);
});

Deno.test("decodePublicKeyBytes rejects invalid keys", () => {
  assertThrows(() => decodePublicKeyBytes("not-a-key"), Error, "invalid_public_key");
});

const CHAINBOUND_GAME_PROGRAM_ID = "9312ay7VrpvNYmyEx8TYCqhafbZ2Y5WGVbt6Qp772uUQ";

// Expected values computed with @solana/web3.js@1.95.3 PublicKey.findProgramAddressSync(
//   [utf8("player_profile"), wallet.toBytes()], CHAINBOUND_GAME_PROGRAM_ID).
// Wallets: the system program id and Keypair.fromSeed(new Uint8Array(32).fill(n)) for n = 1, 5, 6, 39.
const PLAYER_PROFILE_KNOWN_ANSWERS: { wallet: string; pda: string; bump: number }[] = [
  { wallet: "11111111111111111111111111111111", pda: "FsXfXMuRBCffAWjjCoJ9pijCBRnmaQRLdk7qU6HCkLCa", bump: 253 },
  {
    wallet: "AKnL4NNf3DGWZJS6cPknBuEGnVsV4A4m5tgebLHaRSZ9",
    pda: "AGv2ixDKvttZTzXv7HZHUeDfXheV1fvikHW5CrgQgvUs",
    bump: 255,
  },
  {
    wallet: "8SFqwqnq4whPhs8icwHA2hQg3hUoN1qrCLK1SBx3WKwe",
    pda: "Biqv785JaMGeMgtZzxoGwvEM9cWBxcHHoqdc8wu2MVWq",
    bump: 254,
  },
  {
    wallet: "AKkzLhjhyFtM9j7WAhbaqYpFe49cXeJBg2kzLRC2PnNa",
    pda: "Bimq7v3LTWUQLNUHFJGeHmjzAN8fAkNpnBhG9db3tS2V",
    bump: 252,
  },
  {
    wallet: "H37sgaQuqcbs5GAP3WkurghsnPuMkFYcs2BSR6By2B2E",
    pda: "2BHamqUksKXG1sHHuF2yVPrKksvaS9cgzQErSBT5WAC2",
    bump: 251,
  },
];

Deno.test("derivePlayerProfilePda matches web3.js known answers for the Chainbound game program", () => {
  const programId = decodePublicKeyBytes(CHAINBOUND_GAME_PROGRAM_ID);
  for (const vector of PLAYER_PROFILE_KNOWN_ANSWERS) {
    assertEquals(derivePlayerProfilePda(vector.wallet, CHAINBOUND_GAME_PROGRAM_ID), vector.pda);
    const derived = findProgramAddress(
      [new TextEncoder().encode("player_profile"), decodePublicKeyBytes(vector.wallet)],
      programId,
    );
    assertEquals(derived.address, vector.pda);
    assertEquals(derived.bump, vector.bump);
  }
});

Deno.test("findProgramAddress rejects seeds longer than 32 bytes", () => {
  const programId = decodePublicKeyBytes(CHAINBOUND_GAME_PROGRAM_ID);
  findProgramAddress([new Uint8Array(32)], programId);
  assertThrows(() => findProgramAddress([new Uint8Array(33)], programId), Error, "max_seed_length_exceeded");
});

Deno.test("findProgramAddress rejects more than 16 seeds", () => {
  const programId = decodePublicKeyBytes(CHAINBOUND_GAME_PROGRAM_ID);
  findProgramAddress(Array.from({ length: 16 }, () => new Uint8Array(1)), programId);
  assertThrows(
    () => findProgramAddress(Array.from({ length: 17 }, () => new Uint8Array(1)), programId),
    Error,
    "max_seeds_exceeded",
  );
});

Deno.test("derivePlayerProfilePda distinguishes invalid wallet and program ids", () => {
  assertThrows(() => derivePlayerProfilePda("not-a-key", CHAINBOUND_GAME_PROGRAM_ID), Error, "invalid_wallet");
  assertThrows(
    () => derivePlayerProfilePda("11111111111111111111111111111111", "not-a-key"),
    Error,
    "invalid_program_id",
  );
});
