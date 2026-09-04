# Roadmap — from a working loop to a game

Written 2026-09-03, the night the core loop first ran end to end. Vulps's design; the engineering
notes under each step are mine and are flagged as such.

Order matters here and is not arbitrary: each step is the thing that makes the next one mean
something. A fence upgrade is pointless without a plot to fence; a rarity roll is pointless without
upgrades worth rolling for.

---

## Terminology

**World / Island / Garden / Plot / PlantSlot**, locked 2026-09-03 and recorded in
[`terminology.md`](terminology.md), along with the renames the code still owes it. Read that first if
any of the words below feel ambiguous — several of them used to mean something else.

---

## 1. Plot ownership

Only your Plot. Plant in it, harvest from it, and nobody else can do either — unless you invited
them.

This is first because every step after it assumes a Plot belongs to somebody. An upgrade is bought
for a Plot, a rare find matters because what it grows into is yours, and a world that re-rolls is
only tolerable if what you have claimed survives the re-roll.

Claiming, teammates, and the check on **harvest** — currently open to anyone who can reach the
produce — are designed in **[`plot-ownership.md`](plot-ownership.md)**: a deed you carry, an
application scroll the applicant issues, and a shed where every ownership change is committed.

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

---

## Future — islands that generate themselves

Beyond the four steps: **stop authoring the map.** Keep a list of explorable prefabs and let the game
drop them into the void procedurally as players fly out to meet them. Exploration stops having an
edge, and the world stops being a fixed amount of content.

The seed is **the timestamp the instance was spawned** — the moment the first master client joins. So
every instance is a different world, every player in that instance is in the same one, and the whole
thing costs one number.

**Engineering notes.**

- **The seed is a fact; the map is a derivation.** One `long`, decided by the master and replicated
  the way `_plantedTimestamp` already is, and every client computes the same islands forever with no
  further messages. This is `GetGrowthCompletion()` at world scale, and it is the property to protect
  above all others — the moment any part of the layout has to be *announced*, infinite exploration
  stops being free.
- **Generation must be pure and index-addressable.** The same seed and the same coordinate must give
  the same island on every client, in any order, whoever arrived first — so a late joiner can compute
  island #47 without having computed the forty-six before it. That rules out `UnityEngine.Random`,
  which is global mutable state shared with everything else in the process. Hash the seed with the
  island's coordinate into a local `System.Random` instead.
- **Purity is also what makes unloading safe.** Islands have to despawn behind the player or the
  NetworkObject count grows without bound (§4c — Somnium may have a per-world limit nobody has hit
  yet). Despawning a place a player might return to is only acceptable if it regenerates
  *identically*, which is exactly what determinism buys. It is not a nicety here; it is the thing
  that lets the world be endless.
- **The real ceiling is `SceneNetworking._networkPrefabs`.** Every explorable has to be registered
  there by hand, and §3 already flags that array as the pressure point. Generation is unbounded; the
  *vocabulary* is not. Infinite exploration is infinite arrangements of a finite prefab list, which
  is fine, and worth being honest about when it starts to feel repetitive.
- Rarity from §3 applies per island — the multiplier becomes a property of where you are.

**And the good problem: getting it home.** A seed found six islands out has to be carried back through
0G, and that is the best thing about it. Every find becomes a commitment rather than a pickup, and the
journey is where it can go wrong. A carry container would soften exactly the tension that makes it
worth doing, and should be resisted for as long as it stays bearable.

The interesting version is the **ender chest on some islands** — put a thing in, it appears at home.
That is nearly free: `SellableEntity` with a different `OnSold()`, crediting an inventory instead of
thatch, and `SellPoint` already proves the shape. Keeping it **rare** leaves the 0G carry as the
default and makes the chest a relief you are pleased to find, which is a better feeling than a bigger
backpack.

---

## Future — buying more garden

A **Plot Seed**, found in the random drops out in the world. It is only *visible* to a player with
more than **5,000 × the number of plots they already own** — everyone else sees an empty socket.
Plant it and it grows something you can pick up and carry to an available space.

The Garden island moves to a **grid** of plot-and-road chunks to support this (the grid serves a
second feature as well — to be written up separately). Carry the thing the Plot Seed grew and you
see **green ghosts** of the free grid cells adjacent to the plots you already hold; place it in one
and a new Plot spawns there, merging with yours into a single larger garden.

The escalation is the good part: the price of *seeing* the next expansion rises with every expansion
you have already bought, so it stays a distant thing that comes into view exactly once you can
plausibly reach it.

**Engineering notes.**

- **The visibility gate is a derivation, not a message.** Balances are already broadcast to everyone
  and plot ownership will be, so every client can compute `5000 × plots` for itself and decide
  whether to render. Nothing new goes on the wire. Re-evaluate on balance change and on plot-count
  change — both are already events — rather than per frame.
- **Hide the collider with the renderer.** The object exists on the network for everyone regardless
  of who can see it, so a player below the threshold will otherwise walk into something invisible.
  Related: this is a veil, not a secret. A modified client could see it. That is fine for "you cannot
  afford this yet" and would not be fine for anything that must be enforced.
- **A grid gives plots an identity worth having.** A coordinate is a better id than a scene
  reference, and it makes *adjacent* computable — which is the whole ghost preview, for free and
  entirely locally. No networking in the preview at all; the master only decides the placement.
- **The grid collides with `GardenIsland` being one prefab.** If the garden is spawned chunk by
  chunk, the prefab is the *chunk*, not the island. Worth settling before the `GardenIsland` prefab
  is made, or it gets made twice.
- **⚠ Spawning plots removes PlantSlots from the only authority sweep there is.** `Plant_Slot` is a
  scene object today, which is why `SceneNetworking.ReassignNullObjectsAuthority` reaches it — that
  method only ever walks `_sceneNetworkObjects`. Make plots spawnable and their 24 PlantSlots stop
  being scene objects, and nothing reclaims their authority when an owner leaves. That gap already
  exists for crops; this would widen it to the ground itself.
- **Prefer "you own two Plots" over "two Plots become one".** A claim is per-Plot and a player can
  already hold several, so expansion needs no data-model change — merging is roads and borders, a
  visual concern. Making a composite Plot means a second kind of claimable thing, and
  `plot-ownership.md` would need reopening.
- **Placement is a master decision with an optimistic ghost.** The client draws the preview
  immediately from the grid it can already see; the master confirms what actually spawns. Standard
  shape — a decision may be slow, a hand must not be.
- **The price only makes sense after step 2.** 5,000 thatch is roughly 333 carrots at today's
  numbers. This is endgame, and it is endgame *because* upgrades and rarity are supposed to change
  what an hour is worth. It should be designed against that economy, not this one.

**Open:** the thing the Plot Seed grows has no name yet — not a hoe. And it is worth deciding whether
the ghosts appear while carrying the seed, the grown object, or both; the sketch says "plot seed" but
the flow plants it first.

---

## Future — the world grows a plot at a time

Four plots, ten-plus players in every test. Teaming up absorbs some of that; the rest needs more
ground.

**When the last plot is claimed, a new island is appended to the end of the chain**, joined *sell
point to buy point* — the existing island's sell point backs onto the new island's buy point, so the
seam reads as a street rather than a border.

Rejected on the way: a permanent rain cloud by the sell point that teleports you to a new island. It
was going to get messy at scale, and the reason is worth keeping — **a teleport makes instances,
instances split the player base, and the whole value of a multiplayer garden is other people being in
it.** It would also break the physical continuity that makes carrying anything meaningful.

**Engineering notes.**

- **The chain works because every segment is self-sufficient.** Each Garden already has its own sell
  point and three buy points, so a player on island three never walks back to island one — they buy,
  plant, grow and sell where they stand. The chain is walked exactly once, to find free ground. A
  linear world with a central shop would be unplayable by island five; this one is a long street
  where you only care about your stretch of it.
- **Write the trigger as an invariant, not an event:** *there is always at least one free plot in the
  world*. The event version fires once and hands you one island when ten players claim at once; the
  invariant is checked after every claim and every release and spawns as many as it needs. It also
  answers what happens when a lease expires and plots come back — nothing, because it already holds.
  A small buffer (fewer than two free) stops an arriving player waiting on a spawn.
- **Only the count goes on the wire.** Island N sits at `origin + N × length`; sync "there are three
  islands" and every client places them identically. Same derivation as the procgen seed — a
  replicated transform for a value that changes once would be absurd.
- **It never shrinks, deliberately.** Un-appending is hard — someone may be standing on it, crops may
  be growing — and worlds do not persist across instances, so sprawl is bounded by *peak concurrent*
  demand rather than cumulative. Recorded as a non-goal so nobody builds shrink logic later.
- **This is the grid's second customer.** The Plot Seed grows a garden *within* an island; this
  appends whole islands. Different axes, one foundation, and both need the garden authored as
  spawnable chunks. Build the grid once.
- **⚠ Object count.** Each island is four Plots of 24 PlantSlots, and every PlantSlot carries a
  NetworkObject and a NetworkBridge — 96 NetworkObjects per island before a single crop exists.
  Three islands is 288, just for dirt. `runtime-spawn.md` §4c notes Somnium may have a per-world
  limit nobody has found yet; appending islands is the thing most likely to find it. Worth measuring
  before it is built.

### Prerequisite: the scene has to be tidied first

Measured 2026-09-04: **30 root objects**, of which 16 are `Fence_4_Dark (n)`, four are
`BorderedPlotWithRoad` (not even contiguous — `SceneManager` sits between two of them), four are the
buy and sell points, and two are loose `Grass`. `Environment` exists and contains only the light,
floor, terrain and ambience.

So an island is not an object. It is twenty-six root objects that happen to sit near each other, and
**nothing can append one until it is a single thing.** The target:

```
World (scene)
├── SceneManager          managers only
├── #World Uploader       SDK
├── PostProcessing        SDK
├── Environment           light, floor, ambience
└── GardenIsland          ← one prefab, everything else inside
    ├── Plots/            BorderedPlotWithRoad ×4
    ├── Shop/             BuyPointCarrot / Turnip / Pumpkin
    ├── SellPoint
    └── Scenery/          fences, grass
```

The same tidy is the moment to make **one Plot one NetworkObject with 24 sockets** instead of 24
separate ones — that is the structural half of step 1's ownership change, and
[`plot-ownership.md`](plot-ownership.md) carries it. It takes an island from 96 NetworkObjects to 4,
which is what makes appending islands affordable at all.

**Do this in two steps, and only the second one is dangerous.** Grouping the island into a prefab
that is still *placed* in the scene changes nothing about networking — its PlantSlots stay scene
objects and keep the SDK's authority sweep. Spawning that prefab at runtime is what takes them out of
`SceneNetworking.ReassignNullObjectsAuthority`, which only ever walks `_sceneNetworkObjects`. Step one
is free tidying and can happen now; step two should wait until orphaned authority is solved for crops,
because it extends that same gap to the ground itself.
