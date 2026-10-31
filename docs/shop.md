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

## The purchase flow

**Status (2026-09-13):** built and tested in-world as `ShopSlot`.

> [!warning] Stale names
> `ShopSlot` has since been split: its jobs now live in `BuyPoint` and `DispensingEntity`. The steps below are the
> design and still describe the rules, but method and class names have not been re-checked against the split
> (`HasLeftSlot()`, for one, no longer exists by that name). Verify against the code before relying on a name.

Worth reading end to end before changing anything in the buy point. Nearly every step is there because the obvious
version was wrong.

1. The master spawns a `Seed_` prefab, `PlaceInShop()`, `AssignSlot()`. Stock is **physical**, not kinematic;
   `ReturnableEntity` + `AlignableEntity` are armed with auto-recall so a nudge tidies itself up. Prop collisions are
   ignored per collider pair (the slot anchor sits inside the barrow mesh), done pairwise rather than by layer,
   because the physics collision matrix lives in `ProjectSettings` and **does not travel inside an exported asset
   bundle**.
2. A player grabs it. `SingleHolderFilter` refuses if someone else holds it. Affordability is deliberately **not**
   checked here: an unaffordable seed is still grabbable, so the player gets something to feel rather than a seed
   that silently refuses to move.
3. The seed crosses the slot trigger. `OnTriggerExit` **cannot be trusted**: Unity re-creates the PhysX actor when
   `isKinematic`, `detectCollisions` or a collider's `enabled` changes, and fires exits for everything it was
   overlapping with nothing having moved. Leaving the slot is therefore judged from the geometry, not the event.
4. **The holder** calls `RequestPurchase()`. The holder is the only client that knows its own hand is on the seed.
   Sent to all; everyone but the master ignores it.
5. **The master** decides in `OnPurchaseRequested`: known buyer? known state authority? affordable? Then release the
   slot → `Purchase()` → `RemoveBalance()` → restock. Anything else returns the seed to the slot or rejects the
   purchase, putting the seed back **where it stands** rather than teleporting it, and keeping the slot's current
   seed so nothing restocks.
6. If nobody anywhere holds it (`HolderId` empty), it was shoved out by a hand or another seed: return it to the
   slot. Treating that as a sale handed out free seeds and restocked the slot, which is a straightforward way to
   print crops.
7. The master re-stocks whenever the slot is empty. A sold or planted seed is despawned, so a Unity-null current seed
   is the only empty state there is. Retries are rate-limited to twice a second: a failing per-frame spawn is
   hundreds of attempts and, when Fusion throws rather than refusing, hundreds of orphans ([[runtime-spawn]]).
8. `OnTriggerEnter` latches only seeds whose `seedId` matches the slot's. Without that, a carrot carried past the
   pumpkin buy point on the way to the sell point became that slot's stock.

Late joiners see stock through `AnnounceStock` ([[networking#Late-join sync|networking → Late-join sync]]).
