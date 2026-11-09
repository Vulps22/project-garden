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
`PlotLeaseManager` uses it for deeds. A buy point holding a seed with a voiceover plays it to a player walking up:
[[tutorial#Seed voiceovers]].

## Stall ids

**Status (2026-11-01):** played alone in-world: the Garden's and the islands' stall ids resolve, island ones with
their seeded rarity. Multiplayer untested.

A `StallId` names a stall the same way on every client with nothing sent: **where** (an island's grid cell, or for a
Garden where it stands, in whole metres) and **which** (its index among the buy points on that island or Garden, in
hierarchy order). Garden 0's three stalls are `Garden 2,7 #0–2`. Not by sector: several of the 14 Gardens share one
([[towns]]). Island stalls get theirs from the cell `IslandField` records on the
island when it places it.

## What a stall sells

**Status (2026-11-03):** the master-owned stock table and event-driven stalls (phase 2): compiles clean, untested
in-world. The shared clock and restock timer: compiles clean, untested in-world.

- **A stall asks for stock** when its stall changes in the table and the master has nothing there: on load, when its
  stock is sold or goes, when the world cycles, when this client receives the master's table. Not on a timer: Fusion's
  messages are reliable. A client asks only once it has the master's table (`ShopKeeper.HasTable`), so a late
  joiner's stalls do not all ask before it knows what is stocked. An ask that cannot be sent yet (a late joiner's
  messenger still attaching) is kept, one per stall, and sent on the messenger's `OnSpawned`. It carries the socket's
  pose, its `_rarityMultiplier` and its pinned crop, since the master may not have the stall.
- **The master stocks it** unless the table already has it or it is sold out (never true of a pinned stall). An ask
  that reaches a master which cannot spawn yet is served on `WorldBridge.NetworkReady`. A stall with an `_override` always gets that crop, looked up by `seedId` in
  `WorldManager`'s buyables; otherwise it rolls, weighted by rarity and the multiplier (an island stall's is 1–100 from the world seed, skewed low: `1 + 99·u⁴`, so half are x7 or below). A crop
  with `spawnWeight` w under 1 weighs w × multiplier^(1 − w), so the rarer the crop the more a rare stall favours it;
  a flat multiplier would give a barely-rare crop the same boost as the rarest. What that comes to across all island
  stalls is in [[crops#Balance|crops → Balance]]. The seed is spawned at the pose (`Seed.PlaceInShop`).
- **Which seed is a stall's stock is the master's fact**, `ShopKeeper`'s table of stall → seed network id, sent on
  SceneManager's messenger (message ids 16–19):
  - **stocked** (16): stall, seed id and the seed's item id ("seed.tomato"), when the master stocks it;
  - **emptied** (17): stall and seed id, when it is sold or its seed goes; applied only if that seed is still the
    stall's, so a late one never empties the stall's next stock;
  - **table** (18): the whole table, item ids included, to a late joiner (after the sold-out list) and from a new master;
  - **table request** (19): a joiner asks once its own messenger can talk, because the master's push on the join can
    arrive before the joiner's scene objects are attached.

  Every client mirrors it and decides nothing from it. A seed and its "stocked" message arrive in either order;
  whichever comes second completes the match (`ShopKeeper.OnSeedSpawned`, `Seed.ApplyStall`). Every seed is kept by
  id from its spawn until it is destroyed (`ShopKeeper._arrived`) and looked up there before the runner, which may not
  find a seed from inside its own spawn. A stall takes a seed only if it is the kind the table names
  (`Interaction/ItemIdentity`, [[persistence]]).
- **A left-over seed ends itself** (2026-11-05, built, untested; [[playtest-2026-11-04]] items 7–8). A seed whose stall
  the table has moved on to a different seed (an old cycle's stock the master could not find to discard, or a stall
  restocked while its island was unloaded) is left over. Every client can tell from the table it mirrors, so nothing is
  sent: the client simulating it despawns it when the table changes, when it takes over simulating it, when its state
  arrives or when a hand lets go (`Seed.CheckSuperseded`, `ShopKeeper.IsSuperseded`). One nobody simulates goes the
  moment someone in its sector does. An empty stall alone is not enough (a new seed can arrive before the table names
  it), and a held seed is left alone (a purchase may be under way). If a purchase still reaches the master for one, it
  is discarded, not sold; a seed whose id names the stall's stock but whose kind is wrong is discarded and the stall
  emptied, so it asks for its real stock. A seed that has just
  spawned on a proxy asks its owner for its state (`SeedMessageType.stateRequest`), so nothing is re-sent on a timer.
- **Each stall listens to its own stall** (`ShopKeeper.Watch`): it adopts what the table says and asks when empty. No
  per-frame lookup. The Almanac's stall count listens to `ShopKeeper.StockChanged`.
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
- **Everyone knows when the next cycle is.** The master sends the seconds left (message id 11 on SceneManager's
  messenger) at every cycle and to each late joiner, and every client sets its own deadline from it, so a new master
  carries the cycle on where the old one left it.
- **The restock timer** (`RestockTimer`, on SceneManager): a button on the tablet's sidebar under World Spawn,
  "Restock 4:32", counting down to that deadline; "Restock --:--" until the master has said. Built through
  `TabletManager.AddQuickAction` ([[tablet#The sidebar|tablet → The sidebar]]). Pressing it does nothing.

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
   authority transfer arrive in no fixed order against the request, so it decides at once if both are known, else when
   the seed's lifecycle next changes (the grabber message arriving), and refuses after 1.5 s, rather than bouncing the
   seed out of a hand on the first miss. A request for a seed the master does not list as that
   stall's stock is refused and logged, so the seed is never left in the buyer's hand unbought and unplantable. Then: known buyer? known state authority? a definition
   to price from? a balance row? affordable? Any **no** restores shop state on every client without charging
   anyone, and each client's stall recalls the seed **from where it stands** (`Socket.RecallHere`; whoever
   simulates it moves it). Otherwise `Seed.Purchase()` → `EconomyManager.RemoveBalance()`, the stall is sold
   out until the next cycle (not a pinned one), and the master says the stall is emptied. The price comes from the **seed's** definition, so a cycle mid-sale never sells a
   carrot at pumpkin prices.
6. **Shoved out.** If the seed leaves and nobody holds it, a hand or another seed pushed it: not a sale. Auto-recall
   brings it back. Treating that as a sale handed out free seeds and restocked the stall, which is a
   straightforward way to print crops.
7. **After a sale.** The master's "emptied" takes the seed off the table on every client; each stall lets go of it. A
   pinned stall asks again at once; any other stays empty until the cycle reopens it.

Late joiners get the stock table and the sold-out list from the master ([[networking#Late-join sync|networking → Late-join sync]]).
