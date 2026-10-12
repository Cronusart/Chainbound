# Daily crate — Toby's shipment

Concept for the reward that follows **The Professor's Order**. Not built. The coffee handoff completes the quest and stops. This crate is the next patch's convenience reward, not a quest that starts by itself.

## Player fantasy

Toby thanks the dog for the order by shipping a small crate once a day: **10 RAM** and **1 Signal Orb**. It looks like a wrapped Christmas gift under or beside `XmasTree` in `player_room`. Opening it grants both, then the gift is gone until 24 hours after that claim.

The same claim is also on the main-menu tree. That tree already opens the events panel (news, ongoing events, upcoming events, update notes). A claim control lives in that panel so a player can take the gift without walking home, and so the menu stays the place they check what is happening.

## What already exists

| Piece | Today |
|---|---|
| Quest item | `tobys_delivery` ("Toby's Delivery") is granted when Reynard brews and consumed when Toby accepts the handoff. |
| Quest end | Completing `deliver_to_toby` finishes `coffee_and_beer`. Nothing is queued after it. |
| House tree | `player_room` object `XmasTree` is an interact. During the intro its profile plays "no presents yet." |
| Menu tree | `MainMenuSequenceController2.OpenEventsPanel` opens `EventsPanelController` from the xmas-tree hotspot. The panel shows `MainMenuEventsFeed` title and body. No claim button. |
| Bag grants | `profile_apply_profile_item_delta` can grant or consume profile-stack items such as `signal_orb`. A world pickup is a **one-time** spawn (`world.pickup.*` plus a SQL claim). It cannot come back tomorrow. |
| World flags | Profile flags are booleans. They can remember "Toby unlocked the shipment." They cannot remember a timestamp. |
| RAM | The live balance only moves when the server returns a new balance version. A client-side add disappears on the next profile read. Battle RAM grants are a separate track and stay off until that work ships. |

## Rules

- The shipment unlocks when Toby accepts the parcel (`world.coffee.toby_delivery_accepted`). No crate before that.
- One open crate at a time. The grant is **10 RAM and 1 Signal Orb together**.
- The next crate is ready **24 hours after the successful claim**. A missed day does not stack a second crate.
- The clock is the server claim time, not the player's machine.
- Retrying the same claim does not pay twice.
- Either surface can claim it. Claiming in the house hides the menu button, and claiming in the menu hides the house gift.

## Server claim

One receipt per profile, not a new bag item and not a `world.pickup` spawn.

Suggested shape:

- Unlock flag is the narrative gate. The server still checks it (or an equivalent profile fact) before paying.
- Row keyed by profile: `unlocked_at`, `last_claimed_at`, `next_claim_at`.
- `claim_toby_daily_shipment` (name flexible) returns `available`, `next_claim_at`, and on success the new RAM balance version plus the orb stack.
- RAM and the orb commit in the same transaction. A failure leaves both unspent.
- Idempotency key is the profile plus the claim window (`last_claimed_at` or `next_claim_at`), so a double tap cannot mint a second orb.

The client reads availability. It does not decide that the timer elapsed.

## House gift

- A wrapped-present object parented under or beside `XmasTree` in `player_room`. The tree itself keeps the intro line. The present is a separate interact.
- Hidden until the shipment is unlocked **and** the server says a claim is ready.
- Check on entering `player_room`, and again on a timer while that scene stays loaded, so the present can appear without a reload if the player is already home when the window opens.
- Interact plays an open, then the existing item-obtain presentation for the orb, with the RAM amount in the same beat. Hide the present when the claim succeeds.
- If the claim fails, leave the present in place and say the shipment did not land.

## Menu claim

- Keep the events feed as the news / upcoming / patch-notes body.
- Add a claim section on that same panel: ready state, or "back in …" when `next_claim_at` is in the future.
- The button calls the same claim as the house present.
- A player who has not finished the coffee handoff sees the news and no claim. The panel does not start a quest.

## Open choices

- Exact present mesh and open animation. None authored yet.
- Whether the RAM line uses the post-battle ticker copy or a shorter "10 RAM" obtain line.
- Whether `next_claim_at` is shown as a countdown or as a clock time.
- Whether a failed orb grant with a successful RAM write is impossible by construction (preferred: one transaction) or needs a repair receipt.

## Out of scope for this note

- Building the SQL, the edge function, the present prefab, or the menu button.
- A species journal or the incinerator trip. Toby's handoff only mentions them.
- Changing battle-reward mode. This stipend is its own faucet, paced at 10 RAM a day, and does not replace battle RAM.
