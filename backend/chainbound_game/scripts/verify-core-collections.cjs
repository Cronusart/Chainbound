#!/usr/bin/env node
"use strict";

// Read-only verifier for the four pinned Phase 1 Devnet Core collections.
// It never loads a signer, signs, or submits a transaction.

const { Connection, PublicKey } = require("@solana/web3.js");
const { createUmi } = require("@metaplex-foundation/umi-bundle-defaults");
const { fetchCollectionV1, mplCore } = require("@metaplex-foundation/mpl-core");

const RPC_URL = process.env.CHAINBOUND_SOLANA_RPC_URL || "https://api.devnet.solana.com";
const DEVNET_GENESIS = "EtWTRABZaYq6iMfeYKouRu166VU2xqa1wcaWoxPkrZBG";
const PAYER = "DkFmzzzF89tYZu2JWR1uq3H13U5np9NU6MHF4E61gJmG";
const UPDATE_AUTHORITY = "FNRyxJLpcGuWwEUXYiXqjJMDsHAAs1gU8CnkjjBRL3ys";
const MPL_CORE_PROGRAM = "CoREENxT6tW1HoK8ypY1SxRMZTcVPm7R94rH4PZNhX7d";

const COLLECTIONS = [
  {
    key: "moves",
    address: "AUQuvgpFDUekeVh2GHri99A59G4YY5LUWdGw9fKTB9wC",
    signature: "2Zoq66iWT1qEFNcyPQQAd1465GTr9ZgpKCsL8MNWuebY4byHujaEYwhaPwfwuVnRXPmx1KLj5YXkJh84gkEvstdq",
    name: "Chainbound Moves (Devnet)",
    uri: "https://chainbound.local/devnet/collections/moves.json",
  },
  {
    key: "archives",
    address: "DP3rTx145BPfunueM9SHjYJXBDCK5GmrdwZjpCvgf6U",
    signature: "3Pu3feqqPz6Gv8FZ2n5qoH5gqoyeRsZtpq3xJUgqVqswfwZi4FrmmyUTRdiVHw7SSps1swENEdKVExrx7NpSro8a",
    name: "Chainbound Archives (Devnet)",
    uri: "https://chainbound.local/devnet/collections/archives.json",
  },
  {
    key: "covers",
    address: "6wPnru8RDTsnCyujzPMq8v9U8pD6ynRa8es3ZXcesbNs",
    signature: "5XrCkvvZirQKH98W7fZWAYXNJynAs3LoeJCYrSbWjRh47rjY4i1JcCsXL7QNtL3qQd4HtqwoBcLYyNjenTDMsRzm",
    name: "Chainbound Covers (Devnet)",
    uri: "https://chainbound.local/devnet/collections/covers.json",
  },
  {
    key: "cosmetics",
    address: "76aVsw9xScewNG4wkiE75asdM5bbZaHS5CG78263yMcV",
    signature: "7P8MRHtrvtuPPRAMRExAwGRFSRwaei7zabndVGKSeXHAU4iymB9C1Dm1YLCTWpCbzna9jsNm2iUEiN9nUrY8Sev",
    name: "Chainbound Cosmetics (Devnet)",
    uri: "https://chainbound.local/devnet/collections/cosmetics.json",
  },
];

function assert(condition, message) {
  if (!condition) throw new Error(message);
}

async function main() {
  assert(
    new Set(COLLECTIONS.map((item) => item.address)).size === COLLECTIONS.length,
    "Collection addresses are not distinct.",
  );
  const connection = new Connection(RPC_URL, "confirmed");
  const umi = createUmi(RPC_URL).use(mplCore());
  const genesisHash = await connection.getGenesisHash();
  assert(genesisHash === DEVNET_GENESIS, `RPC is not Solana devnet (${genesisHash}).`);

  const addresses = COLLECTIONS.map((item) => new PublicKey(item.address));
  const [rawAccounts, statuses, transactions] = await Promise.all([
    connection.getMultipleAccountsInfo(addresses, "confirmed"),
    connection.getSignatureStatuses(
      COLLECTIONS.map((item) => item.signature),
      { searchTransactionHistory: true },
    ),
    Promise.all(
      COLLECTIONS.map((item) =>
        connection.getTransaction(item.signature, {
          commitment: "confirmed",
          maxSupportedTransactionVersion: 0,
        }),
      ),
    ),
  ]);

  const verified = [];
  for (let index = 0; index < COLLECTIONS.length; index += 1) {
    const expected = COLLECTIONS[index];
    const raw = rawAccounts[index];
    const status = statuses.value[index];
    const transaction = transactions[index];
    assert(raw, `${expected.key} collection account is missing.`);
    assert(raw.owner.toBase58() === MPL_CORE_PROGRAM, `${expected.key} owner is not MPL Core.`);
    assert(status, `${expected.key} create signature was not found.`);
    assert(status.err === null, `${expected.key} create signature failed.`);
    assert(transaction?.meta?.err === null, `${expected.key} create transaction failed.`);

    const accountKeys = transaction.transaction.message
      .getAccountKeys()
      .staticAccountKeys.map((key) => key.toBase58());
    assert(accountKeys.includes(PAYER), `${expected.key} transaction omits pinned payer.`);
    assert(accountKeys.includes(expected.address), `${expected.key} transaction omits collection.`);
    assert(accountKeys.includes(MPL_CORE_PROGRAM), `${expected.key} transaction omits MPL Core.`);

    const decoded = await fetchCollectionV1(umi, expected.address, {
      commitment: "confirmed",
    });
    assert(decoded.name === expected.name, `${expected.key} name mismatch.`);
    assert(decoded.uri === expected.uri, `${expected.key} URI mismatch.`);
    assert(
      decoded.updateAuthority.toString() === UPDATE_AUTHORITY,
      `${expected.key} update authority mismatch.`,
    );
    verified.push({
      key: expected.key,
      address: expected.address,
      owner: raw.owner.toBase58(),
      lamports: raw.lamports,
      dataBytes: raw.data.length,
      name: decoded.name,
      uri: decoded.uri,
      updateAuthority: decoded.updateAuthority.toString(),
      numMinted: decoded.numMinted,
      currentSize: decoded.currentSize,
      signature: expected.signature,
      slot: transaction.slot,
      confirmationStatus: status.confirmationStatus,
      error: status.err,
    });
  }

  process.stdout.write(
    `${JSON.stringify(
      {
        verified: true,
        cluster: "devnet",
        rpcUrl: new URL(RPC_URL).origin,
        genesisHash,
        payer: PAYER,
        updateAuthority: UPDATE_AUTHORITY,
        mplCoreProgram: MPL_CORE_PROGRAM,
        collections: verified,
      },
      (_key, value) => (typeof value === "bigint" ? value.toString() : value),
      2,
    )}\n`,
  );
}

main().catch((error) => {
  process.stderr.write(`${error instanceof Error ? error.stack || error.message : error}\n`);
  process.exitCode = 1;
});
