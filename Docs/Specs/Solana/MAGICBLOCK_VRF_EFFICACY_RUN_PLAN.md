# MagicBlock VRF Devnet efficacy run — plan

> **Status:** first round trip **PASSED** on 2026-10-26 (steps 0–3; see "Run 1 results"). Steps 4–5, the negative cases and the latency sample, are still to run. Results go in the "MagicBlock VRF efficacy track" section of [OWNERSHIP_SPINE_PHASE1_DEVNET.md](../Economy/OWNERSHIP_SPINE_PHASE1_DEVNET.md). The follow-on design is [ROLL_REQUEST_VRF_DESIGN_v1.md](../Economy/ROLL_REQUEST_VRF_DESIGN_v1.md).

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

Detailed plan, 2026-10-26. Most of these cases cost nothing: they run through `simulateTransaction` against PDA `57BTELf5…` from run 1, which is already Consumed. They need no signature and spend nothing. The one case that needs a known seed runs after step 5, using a seed the latency script recorded.

Facts these checks rely on (from the IDL and the pinned SDK at `60761a7`):

- `InvalidAuthority` = **6012**, `InvalidEncounterStatus` = **6036**.
- `consume_encounter_randomness` derives the PDA from `["encounter_request", owner, request_seed]` with `has_one = owner`, so a different signer fails the **seeds** constraint first (Anchor 2006).
- `#[vrf_callback]` injects `#[account(address = scoped_vrf_identity(&crate::ID))] vrf_program_identity: Signer`. The IDL can't show this address because it's computed by a function. For this program it is `DYXgVktMgrs4x5Xft9KnbtEtmjHqzGUdvQyK5HWAwCDS` (seeds `["identity", program]` under `Vrf1RNUj…`). Run 1's fulfill passed exactly this account to `FulfillEncounter`.

| # | Case | How | Expected | Cost |
|---|---|---|---|---|
| N1 | Consume replay | Simulate `consume_encounter_randomness` on `57BTELf5…` with owner `GcrG…3qbx` | `Custom(6036)` `InvalidEncounterStatus` | free |
| N2 | Wrong owner | Same, signed as a different wallet (`7oBf…M5Pj`) | Anchor `2006 ConstraintSeeds`; if the PDA were passed with a matching seed, `6012` instead | free |
| N3 | Forged callback, wrong identity | Simulate `fulfill_encounter` on `57BTELf5…` with `vrf_program_identity` = the test wallet (a real signer) | Anchor `2012 ConstraintAddress`; the handler never runs | free |
| N4 | Forged callback, right identity | `DYXg…` as signer without the VRF program | Can't be built: nobody holds a key for a PDA. **Don't simulate it with `sigVerify: false`**, which skips signature checks and would falsely "pass". Covered by the runtime and by run 1 | n/a |
| N5 | Duplicate fulfill | Already observed: 3 oracle resubmissions in run 1 failed inside `Vrf1RNUj…` (`0x1`) and never reached the program | Recorded, no rerun | free |
| N6 | Same seed twice | After step 5, simulate `request_encounter` with a seed from a finished run | System program `allocate` fails, account already in use | free |
| N7 | Consume before fulfill (optional) | During step 5, simulate consume right after the request confirms, before the oracle answers | `Custom(6036)`; skip if the oracle has already answered (it took about 1 s in run 1) | free |

Pass when each simulated case returns exactly its expected error, the program's logs never show a handler running past its constraints, and N1–N3 leave `57BTELf5…` unchanged (status Consumed, same bytes).

How to run: one small script, `tools/vrf-efficacy/negative.cjs`, alongside the existing `vrf_preflight.cjs` logic. It builds each transaction, calls `simulateTransaction` with `sigVerify: false` and `replaceRecentBlockhash: true`, and asserts the error code. It needs no keypair, so I can run it directly.

**Results, 2026-10-26** (`node tools/vrf-efficacy/negative.cjs --pda 57BTELf5… --owner GcrG… --other 7oBf…`): **4/4 PASS**

| Case | Result |
|---|---|
| N1 consume replay | `6036 InvalidEncounterStatus`, raised at `lib.rs:2142` |
| N2 wrong owner | `2006 ConstraintSeeds` on `encounter_request` |
| N3 forged callback, wrong identity | `2012 ConstraintAddress` on `vrf_program_identity`; the handler never ran |
| PDA after N1–N3 | Unchanged: Consumed, same bytes |

N6 runs after step 5 (`--seeds <latency results file>`). N7 is recorded per run by the latency script.

### 5. Latency sample

Detailed plan, 2026-10-26.

**Approach: a CLI loop, not ten Editor runs.** Ten harness runs mean 20 Phantom approvals, and the human approval time swamps the oracle time. Run 1 already measured the Editor path (10.7 s to fulfilled, 14.4 s total). A CLI loop signs locally and measures the chain and the oracle cleanly.

- Script: `tools/vrf-efficacy/latency.cjs` (`@solana/web3.js` from `backend/chainbound_game/node_modules`).
- Signer: the `7oBf…M5Pj` keypair file, passed by path. **You run it**, so the secret key never leaves your machine or enters this chat.
- RPC: public `https://api.devnet.solana.com`. This is operator tooling, not the game client, so it doesn't go through the authenticated proxy, and no provider key is involved.
- Cost: about 1,780,000 lamports per run (5,000 + rent 1,270,000 + MagicBlock 500,000 + 5,000; no priority fee). Ten runs ≈ 0.018 SOL; the wallet has 10 SOL. The rent stays locked (no close instruction).

Per run, sequentially, with a 5 s gap between runs:

1. Fresh random 32-byte seed; build and sign `request_encounter`; send; record `t0`, the signature and the slot.
2. Subscribe to the PDA (`accountSubscribe`, confirmed) with a 250 ms polling fallback. Record `t_fulfilled_confirmed` when the status byte becomes Fulfilled, then `t_fulfilled_finalized`.
3. From `getSignaturesForAddress(PDA)`: the fulfill signature, its slot, its signer (must be a MagicBlock oracle), and the count of duplicate oracle submissions.
4. Send `consume_encounter_randomness`; record `t_consumed`; read the account and assert Consumed with non-zero bytes.
5. Write one JSON line per run. On a 60 s timeout, record `timeout` and continue; stop after 3 timeouts in a row.

Output: `results.jsonl` plus a summary table. Paste it here and I'll verify a sample of the signatures from RPC and record it.

| Metric | Proposed pass bar |
|---|---|
| Runs fulfilled | 10 / 10 |
| Request slot → fulfill slot | p90 ≤ 8 slots (about 3 s) |
| Send → fulfilled, confirmed | p90 ≤ 5 s |
| Send → fulfilled, finalized | recorded only (sets the Unity polling choice) |
| Consume | 10 / 10 Consumed, bytes non-zero |
| Fulfill signer | always a MagicBlock oracle; the callback identity is always `DYXg…` |

What the numbers decide:

- **roll_request timeout:** `max(10 s, 3 × p90 confirmed latency)` before showing "still rolling".
- **Unity polling:** if confirmed is reliably much faster than finalized, poll at confirmed (or subscribe) and settle at finalized. The current 2 s poll at finalized is most of run 1's 10.7 s.
- **Candy Machine reveal length:** sized to cover the confirmed p90.

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

## Run 1 results (2026-10-26, verified from Devnet RPC)

Payer `GcrGhPrxRS9y1hKCtXU4cVbMbeAtw96xdqdg7LKY3qbx`, connected through the Editor browser bridge (Phantom, sign-and-send).

| Item | Value |
|---|---|
| Request PDA | `57BTELf5oweTjMCS12tE1LofmyZya1T8LCK7XGpyfXod` |
| Request tx | `3wsH59XYpkLKiHJB7WB9uoosfftXM133147eLsS2Sh7BBJxCqiGjVjk7trbfoMqx2a3dtPqJ53XixLWfrYxiNKTk`, slot 515,481,760, finalized, no error, 43,148 CUs; logs show `RequestEncounter` → system (PDA init) → `Vrf1RNUj…` → system transfer |
| Fulfill tx | `2xWD6kTemxrnCqfeY1wffidWYFbfe6MQ5yVEygSYxRPhdkwQpUFa726nsvhKKSihjLsG8Tdra6Nt1qP1GQNbNRXD`, slot 515,481,763 (+3 slots, about 1 s), signed by MagicBlock oracle `vrfkfM4uoisXZQPrFiS2brY4oMkU9EWjyvmvqaFd5AS`, `Vrf1RNUj…` → `FulfillEncounter`, 32,423 CUs |
| Consume tx | `4pUfX8nst7d8pYYP8SgAH6Qq1AaKU5WqPssDk11nZ67Yo5nT46SWhFdQaDBCQoDSaRZcUtxx74q25eJa5aZeq3S3`, slot 515,481,788, `ConsumeEncounterRandomness`, 3,618 CUs |
| Account state | Owner = Chainbound program, `EncounterRequest` discriminator correct, status **Consumed**, 32 non-zero bytes equal to the logged `a86c95f4…f83ebe32` |
| Duplicate oracle submissions | 3 more oracle transactions one slot after the fulfill, all `custom program error: 0x1` inside `Vrf1RNUj…`; they never reached the Chainbound program, and the oracle paid their fees |
| Latency (Unity) | request → fulfilled 10,713 ms; request → consumed 14,403 ms. On-chain the fulfill landed about 1 s after the request; the rest is the 2 s poll interval, finalized commitment, and the second wallet approval |

Cost to the payer (balance 15,000,000,000 → 14,998,070,000 lamports, total **1,930,000**):

| Part | Lamports |
|---|---:|
| Request fee (Phantom adds a priority fee) | 80,000 |
| `EncounterRequest` rent (locked: there is no close instruction) | 1,270,000 |
| MagicBlock VRF request charge (the system transfer inside the VRF CPI) | 500,000 |
| Consume fee | 80,000 |

Fixes needed to get here, all on `sync/local-20261020`:

- `70bb590d`: unscoped transactions were labelled `legacy-configured` and the cluster guard rejected all of them.
- `79f22ccc`: the Editor bridge was sign-only and sent through the RPC proxy, which correctly refuses `sendTransaction`; it now uses the wallet's sign-and-send.
- Editor setup, not code: the stale Supabase dev session was cleared, and the test wallet was added to `beta_allowlist`.
