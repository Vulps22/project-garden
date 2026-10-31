---
aliases:
  - "Principles"
  - "The governing rule"
tags:
  - rules
---
# Principles

The design rules every multiplayer decision in the world follows. Read this before [[networking]], which holds
the mechanism, and [[layers]], which holds the code's shape.

## The governing rule: the master is the source of truth

**Status (2026-09-13):** the rule; some of the code does not honour it yet (see *Practical consequences*).

Every peer should be able to ask the master what something is and get back **facts, not inferences**. Almost
every multiplayer bug this project has had was a client inferring something it should have been told: the master
reading geometry to decide a sale, a shop slot reading `isSelected` to decide a purchase, a fresh authority
asserting a lifecycle flag it had guessed at.

Two things this rule is *not*:

- **It is not about peer-to-peer traffic.** Fusion relays everything through the Photon server; there is no
  peer-to-peer to eliminate. `ShopSlot.OnTriggerExit` once ran on every client and each decided the purchase for
  itself, all through the server, and still wrong. The axis is **authorship**: one client decides, everyone else
  observes.
- **It is not uniform.** At 200 ms, routing a held object's position through the master would make carrying
  anything unusable. The rule needs three tiers.

| Tier | Owner | Examples | How it travels |
|---|---|---|---|
| **Facts** | **master, always** | who owns what, who bought what, what is planted where, balances, what exists | RPC to all, from the master, at the moment it decides |
| **Derivations** | nobody, computed | growth completion, current phase, target scale | not sent at all; every client derives it from a fact |
| **Simulation** | the state authority | where a held object is this frame | Fusion transform replication |

**Derivations are the tier to reach for first.** `GetGrowthCompletion()` is the model: sync *"planted at T"* once
and every client computes the same scale every frame, forever, with no further messages. Anything computable from a
fact should be computed, not broadcast. A phase transition is the clock crossing a line that everyone can already
see, not an event needing an RPC.

**Optimistic locally, authoritative eventually.** The player takes the seed the instant they grab it; the master
confirms ~200 ms later and only intervenes if the answer was no. Without this the rule reads as "wait for the
master", which is exactly the flicker that produced the hover-transfer experiment ([[networking#Three notions of ownership|networking → Three notions of ownership]]). A decision may be slow. A hand must not be.

### Practical consequences

- A buy is **decided** by the master ([[shop]]). A sell must be **accepted** by the master, not observed by it
  ([[economy#Selling|economy → Selling]]).
- Planting, harvesting and despawning are facts the master decides, not events it learns about.
- An object no player is holding should be owned by the master. This is `IAuthoritySource.ShouldMasterOwn`, which
  encodes the rule correctly but does not always win.
- A pull channel ("master, what is this?") is a **repair** path for a client that suspects it is desynced, not the
  primary one. If the master announces every fact, peers already have them.

## Three more rules, learned the hard way

**Status (2026-09-01):** rules. They came out of the seed-physics rework and the 2026-09-01 multiplayer failure;
the third is the governing rule in the specific. Apply them to new code rather than re-deriving them.

**1. One owner per shared property.** ^one-owner
Every serious bug in this project was several components writing the same field from their own stale copy.
`KinematicController` exists to *own* `isKinematic` and `useGravity`, not to do a job. A new shared physical
property needs an owner ([[entity-components]]).

> [!warning] Fusion 2.1 writes `isKinematic` too
> With Forecast off, `NetworkTransform` makes a Rigidbody kinematic on every client that does not own it. That is a
> second writer of a property `KinematicController` owns. Open in [[networking#Fusion 2.1|networking → Fusion 2.1]].

**2. Idle means idle.** ^idle-means-idle
A component that drives the rigidbody every frame fights XRI, the hands and the solver forever.
`ReturnableEntity`, `AlignableEntity` and `HoveringEntity` write **nothing at all** until told to act. That removed
a whole class of bug rather than guarding against it. Related: a threshold says when to *start*, not where to
*stop*. Always land exactly on the target, or objects park "close enough" with nothing left to correct them.

**3. Local state must never drive a networked decision.** ^local-state
`XRGrabInteractable.isSelected` is true on exactly one machine and is not replicated. Every decision built on it
was made independently, and differently, on each client; that single property produced four symptoms that looked
like unrelated bugs. The replicated equivalent is **`IHeldObject.HolderId`**, synced via `PlantMessageType.grabber`.

- **A decision that must be the same everywhere is made in one place:** the master for economy and slot
  bookkeeping, the state authority for movement. Everything else observes.
- **A local suspension must never depend on remote state to be released.** Arrival is judged on every client;
  only movement is authority-gated; a timeout is the backstop.
- **Prefer an explicit request over inference** whenever the initiating client knows something no one else does.
  The purchase flow in [[shop]] is the worked example.

## Every function states its own rules

If a behaviour is master-only, that function guards, regardless of whether its caller already did. Not
duplication: the guard is part of what the behaviour *is*. You don't check the kitchen to see if the oven is on;
you check the oven. Guards are a silent `if (!PlayerManager.IsMaster) return;`, because `PlayerJoined` and friends
fire on every client and a proxy reaching one is the normal case.
