# Procgen islands

The step doc for [`roadmap.md`](roadmap.md)'s *Future — islands that generate themselves*. Vulps's
design; the engineering notes are mine and are flagged as such.

Written 2026-09-09 on `feature/procgen`, and **parked the same day** — see below.

---

## ⚠ Status: parked, blocked on flight

`feature/procgen` was merged to `main` rather than left standing, because everything on it is
finished and independently useful — the island scale box, and this document — and nothing on it is a
half-built system waiting to be resumed. A branch parked mid-design rots; a merged branch does not.

**Flight is the blocker, and it is a real one.** The world's usable radius is a function of flight
speed, so nothing here can be sized until flight exists (see *Radius* below). Work continues on
**`add-advanced-flight`**, cut from `main` at this commit, and is designed in
**[`flight.md`](flight.md)**.

That doc supplies the constraint this one is missing: **flap height x glide ratio = the island
spacing ceiling** — 240 m at the current starting numbers. Island spacing and flap height are one
decision, not two.

(An earlier version of this section said flight had no supported API behind it. That was wrong:
`ISomniumPlayer.References.Body` hands over `Root`, `Head` and both hands as real `Transform`s. See
`flight.md`.)

Resume this document when flight has a known speed and a known implementation.

---

## The design

**Islands are premade, not generated.** A palette of authored island prefabs, placed by the world
seed. The generator picks *which* island goes *where*, not what an island is made of. In the finished
game the definitions live on a server, authored either at `IslandCreator.vulps.co.uk` or — more
likely, and better — in a Somnium world built for the purpose. **For now everything stays local
Unity prefabs.**

**Players spawn in the Garden.** They always have.

**The seed is a timestamp.** The first time a master client joins, the moment is recorded, and that
number is the world seed from then until the last player leaves and the instance cleans itself up at
the end of its lifecycle. One number, one world, one instance.

**The seed places islands at coordinates within an X km range.** Flying out generates more islands as
they come into range.

**Every island has between 0 and 1 buy points.** That may change.

**The loop:** fly out, find a buy point, decide whether you want what it is offering or whether to
keep exploring, collect one or two seeds, and come home.

**Drop a seed and it is gone.** It falls into oblivion. More generally: **anything that hits the
world floor is destroyed.** The floor is a giant trash can and doubles as the cleanup mechanism for
everything that gets dropped, thrown or abandoned.

**Every player sees the same islands, in the same locations, with the same buy points, selling the
same items.**

**Rarity is weighted by distance.** A buy point rolls against a rarity factor on the seed and a
factor of its own, each cycle — and the further out the island, the higher the chance of rare,
high-value goods. Going further is how you find better things.

**Nothing is ever planted out there.** `GardenIsland` is the only place a player can plant or
harvest. Exploration islands are places you *find* things, not places you farm. Later, `GardenIsland`
may start spawning additional gardens to accommodate a larger playerbase — that is a separate
mechanism from this one (see [`roadmap.md`](roadmap.md)'s *the world grows a plot at a time*).

## Flight

Somnium's own flight and glide are **disabled** — the bridge supports this — and replaced with a
hand-driven system: the hands are front-facing airplane flaps. Tilt a hand up and rotate up, tilt it
down and rotate down.

An **optional advanced flight toggle**, enabled at the hut, unlocks the full range: loops, and
barrel rolls by tilting the hands in opposite directions.

---

## Engineering notes

These are mine, not design decisions.

### The map is a derivation, and nothing about it goes on the wire

Seed plus coordinate gives island prefab, position, and buy-point slot, identically on every client.
"Generating as they come into range" is *instantiation*, not decision. There is no island manifest to
sync, no late-join path, and no repair channel. This is `GetGrowthCompletion()` at world scale and it
is the property to protect above every other — the moment any part of the layout has to be
*announced*, the design stops being free.

**Prefer a bounded field over an unbounded one.** With a fixed radius, every client can compute the
*entire* manifest once at startup — every island's position, prefab and slot, as one array — and
proximity becomes instantiate/destroy against a fixed list rather than generation. That removes
streaming logic entirely, gives the trash-can floor a defined extent, and turns the precision ceiling
below into a bound you deliberately stayed inside rather than a bug waiting to happen.

### ⚠ The rarity roll cannot be a master decision

[`roadmap.md`](roadmap.md) §3 says *"the roll is a master decision and the spawned object is its
announcement"*. That holds only while the master has the slot loaded, and at 2 km out it does not.

So **exploration slots derive their roll** — `hash(seed, islandId, cycleIndex)` against a
distance-weighted table — while **garden slots stay master-rolled**. Identical on every client, works
at any distance, needs no master presence, survives an unload and a reload. The split is defensible
precisely because distance is what breaks §3's premise, and it should be written into the tier table
rather than left as an exception someone later "corrects".

Note this also means the roadmap's rule about push-not-pull needs a carve-out. Announcing every
island's stock to everyone is spam about places nobody will visit; for streamed content,
**pull-on-arrival is the primary channel**, and the current rule reads as absolute enough to be cited
against that.

### What spawns, and when — the one real trade

If the roll is derived, every client would try to spawn the stock and you would get N copies. Two
answers:

- **Wild stock is not a NetworkObject until it is bought.** The displayed seed is a local, derived
  visual; the purchase is the moment it becomes real, with the master taking the Thatch and spawning
  one actual `Seed`. No networked objects at distance, no duplicate spawn, no ownership question, and
  object count bounded by what is in hands.
- **Or the master holds fixtures for any island any player is near**, keeping §3 intact at the cost
  of master work scaling with player spread.

**The cost of the first is feel, and it is not small.** Today an unaffordable seed is still grabbable
— deliberately, so a player gets something to hold rather than a seed that silently refuses to move.
A ghost you cannot pick up until you have paid is a worse moment, and it is the exact moment the
exploration loop is built around. Decide this deliberately.

### The dupe, and the only far-world state the master needs

Determinism regenerates stock that has already been taken: buy the rare seed, fly home, let the
island unload, fly back, and it is there again. So the master must hold one small fact — **which
slots have been emptied this cycle** — as a set of ids cleared on every `WorldManager` cycle. That is
the *only* thing about the far world the master has to remember; it is not a world model.

It also means wild buy points must be **one-shot per cycle**, not instant-restock. `BuyPoint.Update()`
refills whenever its socket is empty, which out in the void would be a farm rather than a discovery.
One serialized flag.

### ⚠ Radius is bounded twice, and the tighter bound is not the one you expect

**By float precision.** Unity is float32; positional precision degrades as `distance / 2²³`.

| distance from origin | precision |
|---|---|
| 1 km | 0.12 mm |
| 10 km | 1.2 mm |
| 50 km | 6 mm |
| 100 km | 12 mm |

In VR, held objects and hand tracking begin to visibly jitter in the 1–10 km band, and physics solves
degrade before rendering does. Without a floating-origin system — which cannot work here anyway,
since every client would need a different offset while everything networked lives in world space —
**~5 km is a realistic outer bound.**

**By cycle time, which binds much tighter.** `WorldManager._recycleSeconds` is 300. The furthest
*useful* island is the one you can reach and return from inside one cycle, because anything beyond
that has re-rolled before you arrive. Effective radius is `flightSpeed × 150 s`:

| flight speed | effective radius |
|---|---|
| 10 m/s | 1.5 km |
| 20 m/s | 3 km |
| 50 m/s | 7.5 km |

**So flight speed and cycle length are the two knobs, and the radius falls out of them.** A 5 km
radius disc is 78.5 km² — comparable to GTA V or Breath of the Wild by area, though that flatters it
badly, since those are walkable everywhere and this is a void with islands in it. At 300 m spacing it
would hold ~870 island slots, of which a player on an hour-long session might visit thirty. **2 km is
my read of the right starting size**, with density tuned to make the trip feel right.

If a genuinely larger world is wanted, exploration slots should cycle on a slower clock than garden
slots. They serve different purposes and there is no reason they share a heartbeat.

### ⚠ Flight has no supported API, and this is the spike

`ISomniumPlayerMotion` exposes:

```
DoTeleportToPoint          SetGravity          SetCollisionDisableState
SetMovementSpeed           SetJump             SetFlyModeDisableState
SetGlideSpeed              SetTeleportation    SetGlideDisableState
```

with `SomniumPlayerFlyRules.SetDisableFlyModeState`, `SomniumPlayerGlideRules.SetDisableGlideState` /
`SetGlobalSpeed`, and per-area variants of all of it — so **disabling Somnium's flight and glide is
fully supported, including per-zone.** Flight could be off inside the Garden and on in the void
without touching anything else.

**There is no velocity, no force, and no move-the-rig call.** The only positional control on the
entire interface is `DoTeleportToPoint`. Three ways forward, in the order I would try them:

1. **Find out what Somnium's glide actually is.** If it is already arm-driven, `SetGlideSpeed` plus
   disabled fly may deliver most of the mechanic without owning locomotion at all. Ten minutes, and
   it could remove the whole problem.
2. **`DoTeleportToPoint` at frame rate.** It is a teleport API and probably does fades, collision
   resolution or a network sync per call, so it is likely unusable — but it is an afternoon to
   settle.
3. **Write to Somnium's player rig transform directly.** Realistically the answer. Off the supported
   surface, will fight their character controller unless `SetCollisionDisableState` and `SetGravity`
   get it out of the way, and exactly the kind of thing an SDK update breaks silently. Squarely the
   sort of local workaround this project exists to do.

**Motion sickness is the design constraint, not the mechanic.** Continuous locomotion with pitch and
roll is the most nausea-inducing thing VR can do, and loops and barrel rolls are its worst case.
Making advanced flight an opt-in toggle is already the right answer to that; the thing worth adding
is that basic flight should keep the horizon stable enough that nobody needs the toggle to enjoy the
world.

### The trash-can floor

`Runner.Despawn` destroys the object on every client and requires state authority, so the floor is a
**master decision**, not a local trigger — and it must `ForceRelease()` before despawning or XRI is
left holding a destroyed object ([`runtime-spawn.md`](runtime-spawn.md) §4d). The floor is global
geometry so the master always has it, which is what makes this work where the buy points do not.

Open: what happens to a *player* who falls past it.

### Where the ground actually is, as of 2026-09-09

- The scene tidy the roadmap listed as a prerequisite is **done**: 6 root objects, **7 scene
  NetworkObjects**, down from the 30 roots and 99 NOs that section was written against.
- `GardenIsland` is one prefab: 4× `BorderedPlotWithRoad`, `SellZone`, `BuyZone`, `RockFormation`.
  The scenery/fixtures separation this design wants **already exists structurally** — `RockFormation`
  is scenery, the rest is fixtures.
- `DeedHut` is a **root object, outside `GardenIsland`** — ownership is a home-island concept, which
  is consistent with nothing being plantable out in the void.
- The four `Plot` NetworkObjects carry `Flags = V1` **only** — no `AllowStateAuthorityOverride`,
  unlike every crop prefab (`524289`), and no `MasterClientObject`, unlike `SellPoint`,
  `SceneManager` and `BalanceBoard`. Worth settling before Plots are ever spawned rather than placed.
- `SceneNetworking._networkPrefabs` is at **11**. The roadmap flags it as getting tiresome north of
  thirty; a palette of island prefabs does not touch it, since islands are not networked — but
  anything a buy point can offer does.
- `BuyPoint.prefab` has **no `NetworkObject` and no `NetworkBridge`**. It is a plain fixture, and only
  the seeds it spawns are networked. This is what makes locally-instantiated wild buy points possible
  at all.
- `IslandFragmentGenerator` exists, is deliberately Editor-free, and carries a *"unsure if we're
  keeping this — don't build on top of this until that's settled"* header. **It no longer gates this
  design**, because islands are premade: it authors fragments, not placement.
- `Islands/Island_A.prefab` is the first island, empty, built against the scale box.

### Island scale

`GardenIsland` measures 42.5 × 57.1 × 38.4 by renderer bounds, spanning y −49.0 to +8.1 — most of
that height is the rocky underside; its colliders are only 20.6 × 22.6 × 16.7. Rounded, the working
envelope for a new island is **40–60 on every axis**, and **Grow A Garden ▸ Island Scale Box** draws
it at the origin in any prefab whose filename starts with `Island`.

---

## Open questions

1. **Flight** — what it is, and how fast. Everything else is downstream. See the spike above.
2. **Cycle length for exploration slots** — shared with garden slots, or its own slower clock.
3. **Radius and island spacing**, which fall out of 1 and 2.
4. **Grabbable wild stock, or buy-then-spawn** — the feel trade above.
5. **What happens to a player who falls past the floor.**
6. **Whether `Plot`'s flags need changing** before gardens are ever spawned.
