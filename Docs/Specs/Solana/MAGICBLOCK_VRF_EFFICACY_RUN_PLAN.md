# MagicBlock VRF Devnet efficacy run — plan

> **Status:** plan only, not run. Drafted 2026-10-26. Results go in the "MagicBlock VRF efficacy track" section of [OWNERSHIP_SPINE_PHASE1_DEVNET.md](../Economy/OWNERSHIP_SPINE_PHASE1_DEVNET.md). The follow-on design is [ROLL_REQUEST_VRF_DESIGN_v1.md](../Economy/ROLL_REQUEST_VRF_DESIGN_v1.md).

## Goal

Get the first successful MagicBlock VRF round trip on Devnet through the existing encounter path, and measure what the roll_request design needs: latency, cost, and failure behavior. No program upgrade, no Edge Function change, no economy change.

The run passes when all of these hold:

1. `request_encounter` finalizes and its logs show the CPI into the VRF program.
2. A `fulfill_encounter` transaction signed by MagicBlock's VRF identity lands on the same request PDA.
3. The PDA reads `status = Fulfilled` with 32 non-zero bytes.
4. `consume_encounter_randomness` finalizes and the PDA reads `status = Consumed`.
5. A second consume on the same PDA fails with `InvalidEncounterStatus`.

## Preconditions (checked read-only on 2026-10-26)

| Item | State |
|---|---|
| Chainbound program `9312ay7VrpvNYmyEx8TYCqhafbZ2Y5WGVbt6Qp772uUQ` | Live, 899,357-byte ProgramData, last deploy slot 511,286,785. Contains `RequestEncounter`, `FulfillEncounter`, `ConsumeEncounterRandomness`. |
| MagicBlock VRF program `Vrf1RNUjXmQGjmQrQLvJHs9SNkvDJEsRVFPkfSQUwGz` | Executable, upgradeable loader. |
| Base-layer oracle queue `Cuj97ggrhhidhbu39TijNVqE74xvKJ69gDervRUXAxGh` | Exists, owned by the VRF program, 30,000 bytes. |
| Unity `VrfService` request accounts | Match the IDL exactly: request PDA, payer, queue, program identity, VRF program, slot hashes, system program. |
| Local `target/deploy/chainbound_game.so` | Built 2026-10-15; does **not** match the deployed bytes (Oct 19 deploy). Not needed for this run; rebuild before any upgrade. |

The deploy record's "Current Phase 1 + MagicBlock artifact" table (714,312 bytes, `58e78eb5…`) is out of date. Refresh it from Devnet when recording results.

## Wallet and cost

- Use a dedicated Devnet test wallet. Never the upgrade authority `DkFm…gJmG`.
- Fund it with about 0.05 SOL from the Devnet faucet.
- Expected cost per round trip: two signed transactions from the test wallet (request, consume) plus rent for the `EncounterRequest` PDA, which is 122 bytes, 1,270,000 lamports (measured). Whether MagicBlock charges the payer anything else is not documented; measure it.
- `EncounterRequest` has no close instruction, so that rent is locked for good. Record this; roll_request adds a close path.

## Steps

### 0. Free preflight (no signature, no spend)

Build the `request_encounter` transaction for the funded test wallet and send it to `simulateTransaction` with `sigVerify: false` and `replaceRecentBlockhash: true`. Pass when `err` is null and the logs show `Instruction: RequestEncounter` followed by the CPI into `Vrf1RNUj…`. Record compute units.

This catches account or queue mistakes before any SOL moves.

**Result, 2026-10-26: PASS.** Test wallet `7oBf57qGZRZvNNcRjKiLMdxQ2TeMeA9ue8LtiwG5M5Pj` (10 SOL on Devnet, not the upgrade authority).

- `err: null`, 38,350 of 200,000 compute units.
- Logs: `Instruction: RequestEncounter` → system program (PDA init) → `Vrf1RNUj…` invoke (`Idx: 16`, one nested system call, 11,675 CUs) → success.
- Measured `EncounterRequest` rent: 1,270,000 lamports (0.00127 SOL).
- What the nested system call inside the VRF CPI pays for (a fee or a request account) only shows in a real run; compare the payer balance in step 3.

### 1. Editor setup

1. Open `Bootstrap` in the live Editor.
2. Set `useDeterministicVrfInEditor = false` on `Assets/Data/Solana/SolanaConfig.asset`. **Do not commit this change.**
3. Enter Play Mode and connect the test wallet through the desktop wallet bridge (Phantom or Solflare on Devnet).

### 2. Run the harness

Select **Chainbound → Solana → Run MagicBlock VRF Devnet Efficacy**. The menu requests, polls every 2 s for up to 60 s, then consumes. Copy these log lines:

- `[MagicBlock VRF] request PDA=… tx=…`
- `[MagicBlock VRF] fulfilled PDA=… randomness=… latencyMs=…`
- `[MagicBlock VRF] consumed PDA=… tx=… totalLatencyMs=…`

The menu waits 60 s, while the deploy record says 30 s. Keep 60 s for this run, and record the real latency to set the product timeout.

### 3. Verify independently from RPC

Don't rely on the Unity logs alone.

1. `getTransaction(request tx)`: finalized, no error, logs include the VRF CPI.
2. `getSignaturesForAddress(request PDA)`: find the fulfill transaction. Its logs show `Instruction: FulfillEncounter`, and the VRF program identity signed it.
3. `getAccountInfo(request PDA)`: owner is the Chainbound program; the Anchor discriminator matches `EncounterRequest`; status byte is Consumed; randomness equals the logged hex.
4. Payer balance before and after: total lamports spent, split into fees and rent.

### 4. Negative cases

| Case | How | Expected |
|---|---|---|
| Consume replay | Call consume again on the same PDA | Fails, `InvalidEncounterStatus` |
| Same seed twice | Request again with the same seed context | Fails at PDA `init` (account exists) |
| Wrong owner | Consume from a second wallet | Fails, `InvalidAuthority` (seed/has_one mismatch) |
| Forged callback | Call `fulfill_encounter` directly from the test wallet | Fails: missing VRF identity signer |

### 5. Latency sample

Repeat step 2 ten times with fresh seed contexts. Record request → fulfilled and request → consumed times (min, median, max) and whether any request never fulfilled. These numbers set the roll_request timeout and how long the Candy Machine reveal must cover.

### 6. Clean up and record

1. Set `useDeterministicVrfInEditor` back to `true`, and confirm `git status` doesn't show the asset.
2. Update the deploy record: the refreshed program hash and size, the first successful request, fulfill and consume signatures, the latency table, the cost per round trip, the negative-case results, and the rent-lock finding.

## Failure triage

| Symptom | Likely cause | Next check |
|---|---|---|
| Preflight `err` mentions an account | Account order or writability drift | Compare against the IDL `request_encounter` accounts |
| Preflight fails inside the VRF CPI | Queue address or VRF program changed | Re-read the queue owner; check MagicBlock's current `DEFAULT_QUEUE` |
| Request lands, never fulfills | Oracle not serving this queue on Devnet | Check MagicBlock status or Discord; retry later; record the time |
| Fulfill lands, callback fails | Callback account metas or discriminator mismatch | Fulfill transaction logs; the request ix must pass the request PDA as the writable callback account |
| Wallet rejects in the browser bridge | Wrong cluster in the wallet | Switch the wallet to Devnet |
