---
aliases:
  - "Warehouse"
  - "The warehouse seed"
tags:
  - mechanic
  - game
---
# The warehouse

A private store on the Garden, opened for good by a rare seed: what a player carries in is kept between sessions.
Where the stored data lives (accounts, the warehouse table): [[persistence]].

## The design

**Status (2026-11-04):** designed with Vulps, not built. Waits on Thatch persistence (accounts), which it extends
by one flag and a list ([[persistence]]).

A **warehouse** stands on the Garden island from the start, shut and derelict. A **warehouse seed**, found out in
the world, opens it **for good**: the unlock is recorded on the player's account, and from then on the doors stand
open for them in every session. A one-time purchase priced as a gut punch, on a scoreboard that ranks current Thatch
([[scoreboard]]).

**Planting it.** The seed is used up when it is fully planted, so it never stands. It folds in on itself: the tip of
the plant is pulled down into the ground and the outside follows after it. Vines then sprout around the warehouse
door and force it open.

**Without an account,** planting is refused with a toast: "You need an account to plant this seed. You can sell it
without planting for your trouble." The seed sells for a little more than it cost, a carrot- or turnip-sized profit,
so buying it by mistake costs nothing.

**What it holds.** Anything a player carries in is kept between sessions, on shelves, where they put it.

- **Upgrades go back to the warehouse.** At the end of a session a standing upgrade returns to its owner's
  warehouse, and each session the player chooses which to plant and where. A Moonflower that replanted itself
  would force night at that Garden every session; choosing keeps night a choice.
- **Buffs are lost with the crops.** A buff dies when the session ends, so quitting early to keep one only loses it.
- What persists is small: Thatch, the unlock, and the warehouse's contents. Plots and crops do not.

**The warehouse is private.** The doors are open only to their owner; everyone else sees the derelict building.
Something carried inside stops rendering for every other player, a pocket dimension in the fiction. Carry it back
out and it leaves persistence until it is returned.

## Engineering notes

**Status (2026-11-04):** proposed, not built.

- **The open door is per viewer.** Every client knows from the account record whether its own player has the
  unlock, so the doors are a local rendering of one fact; nothing about them goes on the wire. The fold-and-vines
  moment is the one exception: other players watching the planting should see it.
- **Storing is the sale transaction with a different ending.** `SellableEntity` already models
  "leave the world when offered, come back if refused" (`SetPending`) and defaults `OnSold()` to
  despawning ([[crops]] §2a). Putting something
  on a shelf is the same movement — an object leaves the world, a fact is recorded — so the warehouse likely wants a
  trait beside `SellableEntity` rather than a new subsystem, and the door works like `SellPoint`: it accepts, it does
  not observe.
- **Prefer despawning to hiding.** Hiding a stored item leaves a live `NetworkObject` nobody but the owner can see,
  which other players can still walk into, grab, and take state authority over. If storage despawns and retrieval
  spawns from persistence, the privacy is structural rather than enforced.
- **A stored item is a stable string id**, required on every storable thing (`tomato`, `yggdrasil`), never its
  position in `SceneNetworking._networkPrefabs`, which a reorder would remap. The category stays out of the id so
  re-deciding buff or upgrade never breaks a warehouse; an id is never renamed once shipped; an Editor check fails
  on a duplicate or empty one before upload.
- **Shelves are an array of sockets, and a stored item is its socket's index**, one item per socket, so the socket
  decides the pose. Sockets can be moved but are only ever appended, never inserted or reordered. How that is
  stored: [[persistence#The warehouse table|persistence → The warehouse table]].
- **Private to render is not private to the master.** Whoever decides what is in a warehouse is the
  master, so the master's client knows every player's inventory. Privacy here is a rendering decision,
  a veil rather than a secret.
- **A crash ends a session early.** Buffs die with the session, and Somnium crashes on world changes. The departure
  grace ([[plot-ownership#Departures|plot-ownership → Departures]]) could cover it: back within grace keeps your buffs.

## Open questions

**Status (2026-11-04):** open.

- Whether the unlock is per player even with teammates (proposed: yes, the warehouse is private), and which
  warehouse opens when there are 14 Gardens (proposed: every Garden's, so changing Garden never strands a
  collection).
- Whether an upgrade can be pulled back to the warehouse mid-session (a Moonflower as a day/night switch).
- Whether anything is *too large* to store, and whether shelves have capacity at all — limits are
  much easier to add before the first thing is stored than after.
