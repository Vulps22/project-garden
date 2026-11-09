---
aliases:
  - "Roadmap — from a working loop to a game"
tags:
  - arc
  - tracking
---
# Roadmap — from a working loop to a game

Written 2026-09-03, the night the core loop first ran end to end. Vulps's design; the engineering
notes under each step are mine and are flagged as such.

Order matters here and is not arbitrary: each step is the thing that makes the next one mean
something. A fence upgrade is pointless without a plot to fence; a rarity roll is pointless without
upgrades worth rolling for.

---

## Terminology

**World / Island / Garden / Plot / PlantSlot**, locked 2026-09-03 and recorded in
[[terminology]], along with the renames the code still owes it. Read that first if
any of the words below feel ambiguous — several of them used to mean something else.

---

## 1. Plot ownership

**Status (2026-11-04):** claiming, teammates and the planting check built; the check on harvest not built, so anyone
who can reach the produce may pick it.

Only your Plot. Plant in it, harvest from it, and nobody else can do either — unless you invited
them.

This is first because every step after it assumes a Plot belongs to somebody. An upgrade is bought
for a Plot, a rare find matters because what it grows into is yours, and a world that re-rolls is
only tolerable if what you have claimed survives the re-roll.

Claiming, teammates and the check on **harvest** are in **[[plot-ownership]]**: a deed you carry, an application
scroll the applicant issues, and a shed where every ownership change is committed.

## 2. Upgrades — sell the seed, or grow it and keep it standing

The pattern, and the best idea in this document:

> **Sell the seed for money, or plant it and keep the grown thing in the ground for what it does.**

A found fence seed is worth 500 thatch on the spot. Or plant it, let it reach maturity, and fence
colliders spawn round your plot so you stop falling off the edge. A raincloud tree bears clouds you
can harvest and throw in the air — or just drop on a plot — to speed growth and swell the final size
and value. A weed plant's bloom, planted, sprays white powder over everyone in range until someone
harvests it.

It is a decision with **no UI**: take the money now, or spend the growing time and then keep spending
the ground. Every rare find becomes a small gamble instead of a pickup, and it is the same shape as
an ordinary carrot with the stakes raised, which is why it will read as natural rather than bolted on.

**An upgrade is the standing crop, not a thing you were granted.** It works while a fully grown
upgrade plant is in place, and the moment it is removed the effect is gone until another seed is found. The
built upgrades are `StandingPlant`s, never harvested, read as working once grown
([[economy#Upgrade bonus|economy → Upgrade bonus]]). That is the whole rule, and it has two consequences worth stating
plainly:

- **An upgrade consumes a PlantSlot for as long as you want it.** Not a one-off cost in growing time
  — a standing rent in ground. Four buffs is four fewer slots for crops, out of 24 per Plot, which is
  what makes running them a real choice and what makes *buying more garden* worth more than it looks.
- **A standing upgrade lasts the session.** Once a player has a warehouse, their upgrades return to it at the
  end of a session and they choose which to plant next time; buffs are lost with the crops ([[warehouse]]).

**Each upgrade and buff has its own bonus, larger the rarer the seed** (Vulps, 2026-11-04), on profit; built for
upgrades ([[economy#Upgrade bonus|economy → Upgrade bonus]]). Buffs multiply on top until they are lost.

**What each upgrade affects is its own business.** The fence is plot-scoped, a thrown cloud lands on
whichever plot it lands on, and the weed reaches every player in range. There is no single rule for
the target, only for the condition.

**Engineering notes.**

- **The grant condition is a derivation, and that deletes a subsystem.** "Is there a ripe fence plant
  in this plot" is computable by every client from facts already replicated — what is planted where,
  and when it was planted. So there is **no upgrade table**: no `playerId → upgrades` dictionary, no
  broadcast, no rebuild-from-scratch. The plant *is* the record. This is the same move
  `GetGrowthCompletion()` made, and it is the reason to prefer standing-crop upgrades over granted
  ones even setting the design aside.
- **Losing an upgrade needs no mechanism either.** Harvesting is an existing gesture that already
  raises `LifecycleChanged`; the effect re-evaluates off the same event every other trait uses. There
  is nothing to revoke.
- **`BehaviourModule` is exactly this, already built.** A fence that fences, a cloud that waters, a
  bloom that fogs — the per-crop layer was written for the rare crop that does something particular,
  reads the produce's condition and owns nothing. These are its first real users. Keep its rule: a
  module reads, it does not write what already has an owner.
- Selling a seed is free: `SellableEntity` is a trait, so drop it on the seed prefab with a value.
  The money-printer caveat does not apply to a seed nobody bought.
- **A carried buff is a decision, so the master makes it.** A cloud thrown in the air or dropped on a
  plot has to resolve *which* plot, and that answer must be the same everywhere. Optimistic locally —
  the cloud leaves the hand at once — master-confirmed a couple of hundred milliseconds later.
- **Growth-rate modifiers fight derived growth.** Completion is `(now − plantedAt) / duration` on
  every client. Change `duration` mid-growth and completion *jumps* — a plant visibly snaps forward
  or back when a cloud drifts over. Fix it the way phases already did: **restamp `_plantedTimestamp`
  when the modifier changes** so completion stays continuous. Invisible until it happens in front of
  a player. The raincloud is the first thing that will do this.
- **A player-range effect is a derivation too.** Every client knows where the bloom is and where it
  is standing, so who is in range needs no message — and a screen effect is local by nature. Nothing
  about "everyone in range" has to travel.
- "Bigger plants sell for more" is nearly free — `ISellValueSource` was written to answer *what is
  this worth right now*. Note `SellPoint` captures the value at the moment of offer, deliberately,
  because announcing a sale clears the holder and there would be nobody left to pay.
- **A plant that stands** is `StandingPlant`: it grows, then stands for the session, never harvested ([[crops]]).

## 3. Generalised spawn slots with rarity

**Status (2026-11-04):** built: every stall rolls from one spawn table with a rarity multiplier ([[shop]]). Still an
idea: a tooling pass over `SceneNetworking._networkPrefabs`, registered by hand, once it passes about thirty prefabs.

## 4. The world cycles

**Status (2026-11-04):** built: every 300 s the stalls re-roll, and every client knows when ([[shop]]). Still open: a
cycle can discard a rare find before the player who spotted it gets there; whether that is the price of hesitating is
undecided.

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

**Status (2026-11-04):** built: premade island prefabs placed by the world seed, with stalls on them
([[procgen-islands]]). Still ideas: the two below.

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
  [[plot-ownership]] would need reopening.
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

**Status (2026-11-03):** superseded by [[towns]]: 56 Plots in three towns of Gardens placed in the scene, which covers
the foreseeable player count without appending islands at runtime.

Rejected on the way, and the reason is worth keeping: a permanent rain cloud by the sell point that teleports you to
a new island. **A teleport makes instances, instances split the player base, and the whole value of a multiplayer
garden is other people being in it.** It would also break the physical continuity that makes carrying anything
meaningful.

## Future — the warehouse seed

**Status (2026-11-04):** designed, not built: [[warehouse]].

## Future — Yggdrasil, the god seed

**Status (2026-11-06):** built, Developer Market only, untested in-world: [[yggdrasil]].

## Future — the portal seed

Two hands is an **early-game limit**, by design. A very rare **portal seed**, after the warehouse, is the way past
it. Not designed yet. Until then the economy is balanced around trips: what a crop earns per thing carried
([[crops#Balance|crops → Balance]]).

---

## Future — the game is a tournament format

**How many plots can you get in four hours.** Every player starts at zero in a world nobody has seen
before, buys, grows, sells and explores, and the board at the end is the result. It is worth writing
down now, while it is still free, because three things already built for other reasons turn out to be
competition infrastructure — and one thing in the architecture would have to change before there is
ever anything at stake.

**The escalating Plot Seed price is already a scoring function.** 5,000 thatch × plots owned, merely
to *see* the next one, means N plots cost a cumulative 5,000 × N(N+1)/2 — ~275,000 for ten, with the
tenth alone costing nine times the first. That curve compresses the score at the top: a player twice
as effective does not get twice the plots, they get roughly forty per cent more. The field stays
within sight of each other and the last hour stays live. Most competitive games bolt that on
afterwards as rubber-banding and it always feels like an apology; here it falls out of a mechanic
added purely for pacing. **Do not flatten this curve for balance reasons without noticing what else
it was holding up.**

**Instances do not persist, and the map is seeded by the instance timestamp.** That makes an instance
a *match* rather than a save file — everyone starts equal, and a pinned seed gives identical ground
to every heat. Fresh-start-every-time is usually the hard part of tournament design and it is already
how the world works.

**The balance board is already a live leaderboard**, and `EconomyManager` rebroadcasts the entire
table on every change, so final standings are a snapshot of a thing that exists.

### Exploring is what makes it a format rather than an optimisation

As specified, *first to ten plots* has **no player interaction at all**. The append invariant —
always at least one free plot — deliberately guarantees ground is never scarce, so nobody ever
competes *over* a plot. It is a speedrun against the clock and the market, run in parallel lanes.
That is a real genre and it is fine, but it is not rivalry, and the whole argument for a multiplayer
garden is other people being in it.

The rivalry has to come from exploring, and this is the strongest argument yet for the found-seed
half of the design. The moment good seeds are **found rather than bought**, every minute is a real
decision: another carrot cycle at a known rate, or go looking. Safe compounding against variance —
and crucially, the right answer moves with how far behind you are and how much time is left. That is
not solvable in advance, which is exactly what stops a format collapsing into one line everyone runs.
Without it, someone optimises the loop once and the tournament is a typing test.

The 0G carry home is what gives that risk teeth, and the note against a carry container in the
procgen section applies doubly here: softening the journey removes the only thing making a find a
commitment rather than a pickup.

### ⚠ The master client is a player

This is the one that needs an architectural answer rather than a design one. The master decides
purchases, sales, spawning, planting and the entire economy. In a friendly race that is fine. The
moment anything is at stake, **the referee is also competing** — and it is not even about cheating,
since a master's own client is also the one whose lag or crash takes the match with it.

The fix is cheap *if* it is anticipated: a non-playing host joins first and stays.
`SceneNetworking.IsMasterClient` resolves to the first player to join, so this needs no new
mechanism — only that nothing in the world ever assumes the master is a participant. That assumption
is easy to introduce accidentally and hard to remove later, so the note is here rather than in a
step doc.

### The one thing worth protecting now

`BalanceDisplayManager` sorts on balance because that is the only number there has ever been. Have it
sort on a **supplied** metric instead of a hard-coded one and a plot-count board, a match score or a
personal-best board is a parameter rather than a rewrite. That is a small change today and an
irritating one once the shed and the deed exist.

Everything else here should wait. Format, duration, whether it is teams — [[plot-ownership]]'s
application scroll already makes a team a real thing, and "the plots are yours but she grew half of
them" is a good problem — are all decisions to make against an economy that does not exist yet, for
the same reason the Plot Seed's price cannot be set against today's numbers.

**Open:** whether a match needs a *stop* condition in-world at all, or whether the world simply keeps
running and the tournament is a thing agreed outside it. The second is free and should be tried
first.
