# Roadmap — from a working loop to a game

Written 2026-09-03, the night the core loop first ran end to end. Vulps's design; the engineering
notes under each step are mine and are flagged as such.

Order matters here and is not arbitrary: each step is the thing that makes the next one mean
something. A fence upgrade is pointless without a plot to fence; a rarity roll is pointless without
upgrades worth rolling for.

---

## 1. Plot claiming

Only your plot. Plant in it, harvest from it, and nobody else can do either.

**Mostly built already.** `PlantSlot.OwnerId` exists, is replicated, and `CanBeUsedBy(playerId)` is
already checked when the master decides a planting. What is missing:

- a claim gesture — what makes an unowned plot yours in the first place
- the same check on **harvest**, which is currently open to anyone who can reach the produce
- something visual for whose plot is whose

**Engineering note.** The interesting question is not the claim, it is what happens when the world is
full of plots held by people who are not coming back. `GardenLease`'s 120-second hold is the
beginning of an answer and its duration should be judged against how a real dropout feels, not
chosen in the abstract.

## 2. Upgrades — sell the seed, or sell the produce

The pattern, and the best idea in this document:

> **Sell the seed for money, or plant it and sell the *produce* for the upgrade.**

A found fence seed is worth 500 thatch on the spot. Or plant it, grow it, sell what it yields, and
get fence colliders round your plot so you stop falling off the edge. A raindrop grows into a cloud
that waters your garden — faster growth, bigger plants, and bigger plants sell for more.

It is a decision with **no UI**: take the money now, or spend the growth time on something permanent.
Every rare find becomes a small gamble instead of a pickup. It is also the same shape as an ordinary
carrot with the stakes raised, which is why it will read as natural rather than bolted on.

**Upgrades last for the session only.** That removes persistence from the problem entirely.

**Engineering notes.**

- Selling a seed is free: `SellableEntity` is a trait, so drop it on the seed prefab with a value.
  The money-printer caveat does not apply to a seed nobody bought.
- Selling produce *for an upgrade* is a new trait beside the existing six — the produce answers 0
  through `ISellValueSource`, and a `GrantsUpgradeOnSale` component listens to `SellableEntity.Sold`.
  `SellPoint` continues to have no idea what a crop is.
- Session-scoped upgrades are **the `EconomyManager` pattern verbatim**: a master-owned
  `playerId → upgrades` dictionary, mutated only by the master, whole table rebroadcast on every
  change, everyone rebuilding from scratch. Consider whether it belongs beside `EconomyManager`
  rather than in a new singleton.
- **Growth-rate modifiers fight derived growth.** Completion is `(now − plantedAt) / duration` on
  every client. Change `duration` mid-growth and completion *jumps* — a plant visibly snaps forward
  or back when a cloud drifts over. Fix it the way phases already did: **restamp `_plantedTimestamp`
  when the modifier changes** so completion stays continuous. Invisible until it happens in front of
  a player.
- "Bigger plants sell for more" is nearly free — `ISellValueSource` was written to answer *what is
  this worth right now*. Note `SellPoint` captures the value at the moment of offer, deliberately,
  because announcing a sale clears the holder and there would be nobody left to pay.

## 3. Generalised spawn slots with rarity

Stop having a carrot slot. Have a **slot**, with a spawn table and a **rarity multiplier**.

- Garden slots: common crops, rare items at well under 1%.
- Exploration slots, out in the floating void: the same table with the multiplier turned up — call it
  10% — so wandering is how you find the strange things.
- Exploration slots may be **empty**; a garden slot always fills. One serialized toggle.

Rejected on the way: a subtype filter list ("only trees", "only plantables"). A rarity multiplier
does the same work with one number instead of a taxonomy, and taxonomies of game objects have a way
of becoming a second type system.

**Engineering notes.**

- The roll is a **master decision** and the spawned object *is* its announcement. Do not build a
  "what is in this slot" sync; the object arriving is the message.
- Price already lives on `SeedDefinition.buyPrice`, so a generalised slot needs no new pricing
  concept — only the table.
- **The pressure point is `SceneNetworking._networkPrefabs`.** Every spawnable must be registered
  there by hand, and its `OnValidate` re-rolls every GUID whenever the array length changes. Fine
  within one build — all clients share the scene — but it means two builds with different lists can
  never talk to each other, and it will get tiresome somewhere north of thirty prefabs. Worth a
  tooling pass then, not now.

## 4. The world cycles

Every N seconds — five minutes, say — **all slots globally re-roll** and spawn newly randomised
stock. Exploration areas stop being one-and-done: the map is worth re-walking because it is a
different map now. The world gets a heartbeat.

**Engineering notes.**

- **Cycling takes stock out of a player's hands, deliberately.** Holding is not owning. An earlier
  draft of this document said a cycle should skip held stock; that is wrong, and wrong in a way this
  project has a name for — `HolderId` says who is holding, `OwnerId` says whose it is, and only the
  second is a claim. Treating a hand as a claim lets a player with no thatch grab the rarest thing
  on the map and squat on it until the timer flips.
- **The scramble is the point.** Reaching the shop with a second left on the clock is free drama.
  Build for it rather than around it.
- **Bought stock is already safe by construction.** `ShopSlot` drops its reference to a seed the
  moment it stops being `InShop`, so the only thing a cycle can ever despawn is unsold stock. No
  extra ownership check is needed — the existing structure already draws the line in the right place.
- **What does need care: a despawn must end the grab first.** `Runner.Despawn` destroys the object on
  every client, and XRI left holding a destroyed object is its own bug. This is the same rule as
  `runtime-spawn.md` §4d and it applies to every despawn path — cycling, `GardenLease` clearing a
  departed player's seeds, and anything else that removes something a hand might be on.
- **Design question worth settling before building it:** a cycle can destroy a sub-1% find before the
  player who spotted it gets there. Options — rare items are exempt from cycling; a slot only
  re-rolls if nobody has been near it; or the cycle is the price of hesitating, which is a defensible
  answer and the one most consistent with the scramble above. Decide deliberately rather than
  discovering it in a playtest.
- The master ticks the cycle and acts on it. There is no need to derive the schedule on every client:
  nothing but the master does anything with it, and the spawns speak for themselves.

---

## Where this leaves the game

Plots you own, crops you grow, strange things you find, upgrades that change how your garden works,
and a world that reshuffles often enough to be worth exploring again. That is a complete loop with a
reason to keep playing, and every piece of it is now a small addition to an architecture that already
does the hard parts — spawn, ownership, derived growth, master-decided facts, and traits that attach
to anything.

Nothing here needs a new networking concept. That is the good sign.
