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

A shop stall is a **`BuyPoint`** with a **`Socket`** beneath it, on whichever clients have it loaded. The socket
holds the stock and knows nothing about money. The buy point holds and shows its stock, asks for more, and reports a
hand taking a seed. **`ShopKeeper`** (on `SceneManager`) is the master's half of every stall: it rolls, spawns,
sells and recycles **by stall id**, so the master never needs the stall loaded. `DispensingEntity` is the older
single-component shape for free items, kept because a dispenser stands in the scene and every master has it;
`PlotLeaseManager` uses it for deeds.

## Stall ids

**Status (2026-11-01):** played alone in-world: the Garden's and the islands' stall ids resolve, island ones with
their seeded rarity. Multiplayer untested.

A `StallId` names a stall the same way on every client with nothing sent: **where** (an island's grid cell, or for a
Garden its sector) and **which** (its index among the buy points on that island or Garden, in hierarchy order). The
Garden's three stalls are `Garden 0,0 #0–2`. Island stalls get theirs from the cell `IslandField` records on the
island when it places it.

## What a stall sells

**Status (2026-11-01):** played alone in-world, including the pinned stalls restocking after a sale. Multiplayer
untested.

- **A stall asks for stock** whenever it is loaded, empty and not sold out: on load, when its stock goes, when the
  world cycles, and every 5 s while still empty in case a request was lost. It sends the socket's pose, its
  `_rarityMultiplier` and its pinned crop with the request, since the master may not have the stall.
- **The master stocks it** unless it is stocked, sold out (never true of a pinned stall), or was stocked within the last second (several clients
  asking at once spawn once). A stall with an `_override` always gets that crop, looked up by `seedId` in
  `WorldManager`'s buyables; otherwise it rolls, weighted by rarity and the multiplier (an island stall's is 1–100 from the world seed). A crop
  with `spawnWeight` w under 1 weighs w × multiplier^(1 − w), so the rarer the crop the more a rare stall favours it
  (2026-11-01; a flat multiplier gave the barely-rare turnip the same boost as the rarest crop, and island stalls
  sold almost nothing but turnips). At x20: carrot 14%, turnip 27%, pumpkin 33%, weed 26% before the new crops;
  at x100 the rarest crops take most rolls. The seed is spawned at the
  pose and told its stall id (`Seed.PlaceInShop`), which every client learns, and late joiners get it in the seed's
  state.
- **Which seed is a stall's stock** is a table every client keeps from the seeds themselves (`ShopKeeper.StockAt`).
  A stall shows whatever the table says, so no trigger has to catch a seed arriving. Two seeds claiming one stall
  (two masters racing) keeps the first and discards the second. **A discarded seed leaves the table as soon as the
  discard message arrives**, not at its despawn (2026-11-02): a peer often received a cycle's new stock before the
  old seed's despawn, kept the old one and turned the new one away, so the stall never offered it for sale. Session 1
  of 2026-11-01 logged 514 such stalls on a peer.
- **The sign** (`StallDisplay`, on the `BuyPoint` prefab, so every Garden and island stall) shows what is on the
  shelf: name, then since 2026-11-02 a **tagline** in small italics under it, the crop's `SeedDefinition.tagline`,
  saying what makes it worth buying ("grows a cinema screen", "fruits three times"), then `B:` buy price and `S:` sell
  value (`S: -` for something that is not sold, like popcorn). It follows the seed in the socket, not the roll, so
  every client agrees; an unstocked stall shows the prefab's placeholder text (carrot's).
- **One sale per stall per cycle.** A sale marks the stall sold out on every client until the next cycle; the list
  is sent whole to late joiners, so a new master already has it.
- **A pinned stall restocks after every sale.** A stall with an `_override` (the Garden's carrot, turnip and
  pumpkin) is never marked sold out: its stock leaves the table when bought, the stall asks again, and the master
  spawns another of the same crop.
- **The cycle** (`WorldManager`, every 300 s, master): every unsold seed is discarded by whoever simulates it, the
  sold-out list is cleared, and every stall asks again.

## The purchase flow

**Status (2026-11-01):** reworked for `ShopKeeper` and sector authority; bought from alone in-world (carrots and
weed), multiplayer untested. The 2026-10-31 version was tested in-world.

Worth reading end to end before changing anything in the buy point. Nearly every step is there because the obvious
version was wrong.

1. **Stocking.** The master spawns a `Seed_` prefab at the stall's pose and tells it its stall id. Every client's
   stall sees it in the stock table and `Hold()`s it. Stock is **physical**, not kinematic; `ReturnableEntity` +
   `AlignableEntity` are armed with auto-recall **on every client**, so whichever client simulates it (a player in
   its sector, [[networking#Authority by sector|networking → Authority by sector]]) tidies a nudge. Collisions with
   the prop are ignored per collider pair (the socket sits inside the barrow mesh), done pairwise rather than by
   layer, because the physics collision matrix lives in `ProjectSettings` and **does not travel inside an exported
   asset bundle**.
2. **Grabbing.** `SingleHolderFilter` refuses if someone else holds it. Affordability is deliberately **not**
   checked here: an unaffordable seed is still grabbable, so the player gets something to feel rather than a seed
   that silently refuses to move.
3. **Leaving the socket.** `OnTriggerExit` **cannot be trusted**: Unity re-creates the PhysX actor when
   `isKinematic`, `detectCollisions` or a collider's `enabled` changes, and fires exits for everything it was
   overlapping with nothing having moved. `Socket.HasLeft()` checks the geometry before raising `Leaving`.
4. **Asking.** In `BuyPoint.OnLeaving`, **the holder** calls `Seed.RequestPurchase()`, because the holder is the
   only client that knows its own hand is on the seed (`IsHeld` is local; see
   [[principles#^local-state|principles → local state]]). Sent to all; `ShopKeeper` acts on the master only. The
   stall asks for any unsold seed with its stall id, not only the one it has adopted, since a peer's stall can lag a
   restock.
5. **Deciding.** `ShopKeeper.ResolvePurchaseRequest` on the master. The buyer's grabber message and Fusion's
   authority transfer arrive in no fixed order against the request, so it retries quietly for a short window rather
   than bouncing the seed out of a hand on the first miss. A request for a seed the master does not list as that
   stall's stock is refused and logged (2026-11-02); it used to be dropped silently, which left the seed in the
   buyer's hand unbought and unplantable. Then: known buyer? known state authority? a definition
   to price from? a balance row? affordable? Any **no** restores shop state on every client without charging
   anyone, and each client's stall recalls the seed **from where it stands** (`Socket.RecallHere`; whoever
   simulates it moves it). Otherwise `Seed.Purchase()` → `EconomyManager.RemoveBalance()`, and the stall is sold
   out until the next cycle. The price comes from the **seed's** definition, so a cycle mid-sale never sells a
   carrot at pumpkin prices.
6. **Shoved out.** If the seed leaves and nobody holds it, a hand or another seed pushed it: not a sale. Auto-recall
   brings it back. Treating that as a sale handed out free seeds and restocked the stall, which is a
   straightforward way to print crops.
7. **After a sale.** `Purchase()` flips `InShop`, which drops the seed from the stock table on every client; each
   stall lets go of it. The stall stays empty until the cycle reopens it.

Late joiners see stock through `AnnounceStock`, now in `ShopKeeper`
([[networking#Late-join sync|networking → Late-join sync]]).
