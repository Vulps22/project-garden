---
aliases:
  - "Shop"
  - "Purchase flow"
tags:
  - mechanic
  - interaction
---
# Shop

How a seed is stocked and bought. The balance a purchase draws on is [[economy]]; the seed itself is [[crops]].

A shop stall is a **`BuyPoint`** with a **`Socket`** beneath it. The socket holds the stock and knows nothing about
money: holding an object, recalling it, ignoring the prop it sits in, and judging whether it has really left. The
buy point decides what is sold, what it costs and whether a sale happened. `DispensingEntity` is the same shape for
free items, and `PlotLeaseManager` uses it for deeds. Both replaced `ShopSlot`, which did all of it at once.

## What a stall sells

**Status (2026-10-31):** built.

A stall rolls its next crop on the master (`Roll()`), weighted by rarity and its `_rarityMultiplier`; proxies are
not told what was rolled, only what the stock is. A stall with an `_override` stocks that crop once and never
cycles, so the shop can always be relied on to sell it. When the world cycles, `Offer()` swaps the stock, waiting for
authority over the outgoing seed before despawning it.

## The purchase flow

**Status (2026-10-31):** built and tested in-world. Names checked against `BuyPoint`, `Socket` and `Seed`.

Worth reading end to end before changing anything in the buy point. Nearly every step is there because the obvious
version was wrong.

1. **Stocking.** The master spawns a `Seed_` prefab and `PlaceInShop()`s it at the socket; the socket `Hold()`s it.
   Stock is **physical**, not kinematic; `ReturnableEntity` + `AlignableEntity` are armed with auto-recall so a nudge
   tidies itself up. Collisions with the prop are ignored per collider pair (the socket sits inside the barrow
   mesh), done pairwise rather than by layer, because the physics collision matrix lives in `ProjectSettings` and
   **does not travel inside an exported asset bundle**.
2. **Grabbing.** `SingleHolderFilter` refuses if someone else holds it. Affordability is deliberately **not**
   checked here: an unaffordable seed is still grabbable, so the player gets something to feel rather than a seed
   that silently refuses to move.
3. **Leaving the socket.** `OnTriggerExit` **cannot be trusted**: Unity re-creates the PhysX actor when
   `isKinematic`, `detectCollisions` or a collider's `enabled` changes, and fires exits for everything it was
   overlapping with nothing having moved. `Socket.HasLeft()` checks the geometry before raising `Leaving`.
4. **Asking.** In `BuyPoint.OnLeaving`, **the holder** calls `Seed.RequestPurchase()`, because the holder is the
   only client that knows its own hand is on the seed (`IsHeld` is local; see
   [[principles#^local-state|principles → local state]]). Sent to all; everyone but the master ignores it.
5. **Deciding.** The master's `OnPurchaseRequested` starts `ResolvePurchaseRequest`. The buyer's grabber message and
   Fusion's authority transfer arrive in no fixed order against the request, so it retries quietly for a short
   window rather than bouncing the seed out of a hand on the first miss. Then: known buyer? known state authority?
   a definition to price from? a balance row? affordable? Any **no** goes to `ReturnToSocket()`, which restores shop
   state and recalls the seed **from where it stands**, without charging anyone. Otherwise `Seed.Purchase()` →
   `EconomyManager.RemoveBalance()`. The price comes from the **seed's** definition, not the stall's, so a stall
   mid-cycle never sells a carrot at pumpkin prices.
6. **Shoved out.** If the seed leaves and nobody anywhere holds it (`HolderId` empty), a hand or another seed pushed
   it: `ReturnToSocket()`. Treating that as a sale handed out free seeds and restocked the stall, which is a
   straightforward way to print crops.
7. **Restocking.** `Purchase()` flips `InShop`, which releases the socket on every client; the socket reports itself
   empty (`OnLeft`) and the master's `Update()` spawns fresh stock. Retries are rate-limited: a failing per-frame
   spawn is hundreds of attempts and, when Fusion throws rather than refusing, hundreds of orphans
   ([[runtime-spawn]]).
8. **Latching.** `OnEntered` adopts a seed as stock only while the socket is empty and the seed is unbought and in
   shop state, so a bought seed carried past a stall is never taken as its stock.

Late joiners see stock through `AnnounceStock` ([[networking#Late-join sync|networking → Late-join sync]]).
