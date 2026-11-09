---
aliases:
  - "Plot ownership"
tags:
  - mechanic
  - game
---
# Plot ownership

How a Plot comes to belong to someone, who may use it, and what happens when they leave: roadmap step 1
([[roadmap]]). Vulps's design; the engineering notes are flagged as such. Words here follow [[terminology]] — a
**Plot** is the claimable patch, a **PlantSlot** is one square of dirt inside it, and 24 PlantSlots make a Plot.

## What it has to do

**Status (2026-11-04):** claiming, teammates and the planting check built; the harvest check and the pickup rule
designed, not built.

- A Plot can be **claimed**, and the claimant's id is stored on it.
- Only the claimant and their **teammates** may plant there (`PlotStateManager.CanBeUsedBy`). **Harvesting is not
  checked yet:** the master refuses a harvest only when the produce is unripe or already taken
  (`Produce.OnHarvestRequested`), so anyone who can reach it may pick it.
- **The planter is who counts, not the seed's buyer** (2026-11-03): the holder's client sends its own id with the plant
  request, and the master checks that against the Plot. A seed handed to someone, or spawned from the Developer Market
  and passed on, is theirs to plant in their own Plot.
- An **unclaimed** Plot allows no planting at all.
- The claimant can **add and remove** teammates.

## Ownership is a fact, not state authority

**Status (2026-11-04):** built.

Fusion's state authority is the obvious candidate for "whose is this" and is the wrong one:

1. **It follows a grab.** Seeds and produce pass authority to whoever last held them, so authority-as-ownership
   would make anything you pick up yours
   ([[networking#Three notions of ownership|networking → Three notions of ownership]]).
2. **Authority is the master's action channel.** `SellPoint`, the stalls and `AuthorityController` take authority
   as routine business; if authority meant ownership, each of those would be the shop confiscating something.
3. **`DestroyWhenStateAuthorityLeaves` cannot wait** for a player to come back ([[#Departures]]).

Instead, **`OwnerId` is a Somnium player id, decided by the master and replicated as a fact**, the same shape as
`IHeldObject.HolderId` one level up:

- **The Plot owns, not the plant.** `PlotLeaseManager` holds the Plot's owner and teammates; anything standing in a
  Plot is that player's by transitivity, so plants and produce are never stamped. Ownership is checked once, when
  the master decides a plant request.
- **A seed carries its own `OwnerId`**, its buyer, because it is the one thing that leaves the Garden.

## The shape: everything is an object you carry

**Status (2026-11-04):** Vulps's design; built for claiming, the teammate wall and removing a teammate.

There is no UI anywhere in this design. Every ownership change is a physical object moved from one
place to another, and the vocabulary is one verb learned once:

> **Into the shed commits it. Out of the shed undoes it.**

Claim a Plot — carry its deed in. Accept a teammate — carry their scroll in. Remove one — drag their
scroll out. The shed is the single place where facts are committed, which means one trigger volume
and one decision point for the master.

## The shed

**Status (2026-11-04):** built (`DeedClaimZone`, `ApplicationAcceptZone`, `PlotTeammateRoster`,
`PlotClaimSignage`).

**One shed per Garden**, physically: its deed hut, answering for its own four Plots ([[towns]], 14 Gardens since
2026-11-03). What is *inside* it depends on who is looking:

| Viewer | Sees |
|---|---|
| anyone | the **barrel**, with a scroll hovering above it (switched off: *Joining someone's Plot*) |
| a Plot owner | their **deed**, and their **teammate list** |
| everyone else | nothing else |

The barrel being visible to everybody is what makes the whole thing bootstrap: a player who owns
nothing can still take a scroll and ask for something.

## Claiming

**Status (2026-11-04):** built.

An unclaimed Plot has its **deed** sitting at the centre, on the same rig shop stock uses —
`ReturnableEntity` + `AlignableEntity` with auto-recall — except the grab is **not** killed, so it can
be carried away. Take it into the shed and the Plot is yours. The deed then lives in your shed, and
dragging it back out relinquishes the Plot.

That last part is why the deed belongs in the shed rather than staying on the Plot: "the deed is in my
shed" *is* the claim, and there is nothing lying in the open for someone to walk off with.

## Joining someone's Plot

**Status (2026-11-02):** switched off. Joining does not work yet and players kept taking the blank application
scroll, so the hut's dispenser (`DeedHut/ItemDispenser`) has its `DispensingEntity` disabled: no scroll is stocked, and
the crate stays as scenery. Tick the component back on to bring it back.

The token is an **application**, issued by the person who wants in — not a grant issued by the owner.
A owns a Plot; B wants to team up:

1. B takes a scroll from the barrel. The master stamps **B's id** onto it.
2. B drops it **in A's Plot**.
3. Only A can pick it up (see the pickup rule below), so it sits there as a private message.
4. **Accept** — A carries it into the shed. B joins A's Plot.
5. **Reject** — A drops it anywhere that is not the shed. Destroying it in the SellPoint is designed, not built: the
   scroll carries no `SellableEntity`.

**Why the applicant issues it, and not the owner.** The token names its *beneficiary*, and the
beneficiary is fixed the moment it leaves the barrel. So stealing one gains a thief nothing: carry
B's scroll wherever you like and the only person it can ever admit is B. The reverse design — the
owner issuing a grant — makes the scroll a bearer token, and then any unattended moment on the ground
is an interception window.

**Why not a gesture.** Pressing hands to add someone was the first idea and it cannot work: the
gesture is symmetric but the result is asymmetric, so it cannot say who is inviting whom; remote
avatar hands are networked approximations, so two clients will disagree about whether a touch
happened while the master — who must decide — may be across the map; and the false-positive failure
mode is "I accidentally gave a stranger my garden", which is the worst one available.

**What the object version buys.** It is **asynchronous**. A does not need to be present, online or
paying attention when B applies. B drops the scroll and leaves; A finds it next time they tend their
Plot. The Plot becomes an inbox.

## The pickup rule

**Status (2026-11-04):** designed, not built.

> Anything dropped in a Plot can only be picked up by someone with access to that Plot.

This is load-bearing twice over: it stops produce being stolen out of someone's Plot, and it is what
makes the application inbox *private*. Worth building even if the rest of this design changes.

**Engineering note.** One more `IXRSelectFilter` beside `SingleHolderFilter`, asking "am I inside a
Plot, and does this player have access?". It reads a replicated fact, so a local filter is legitimate
here — rule 3 forbids local state driving the decision, not local evaluation of a replicated one.

## What is networked, and what is not

**Status (2026-11-04):** built.

| Networked | Local, derived |
|---|---|
| each Plot's claim — owner and teammate ids, on `PlotLeaseManager` | the deed and name shown in the shed |
| application scrolls dispensed by the barrel | the teammate list shown in the shed |
| the deed while it sits on an unclaimed Plot | everything else inside the shed |
| which of the Plot's 24 PlantSlots are occupied, on `PlotStateManager` | |

Two players standing in the shed together see different things in the same space, and that costs
nothing, because none of it is real. Dragging a teammate's parchment out is a local gesture that sends a
request; the master's next broadcast is what actually changes anything.

**One Plot, one NetworkObject.** A Plot's managers share its single `NetworkMessenger`, and so one id space,
`PlotMessageType`: `OwnerChanged` 0, `TeammatesChanged` 5 and `TeammateRemoveRequest` 6 are `PlotLeaseManager`'s;
`SlotOccupancyChanged` 1, `PlantRequest` 2, `Planted` 3 and `FullStateSync` 4 (all 24 slots as a 3-byte mask, for a
late joiner) are `PlotStateManager`'s. **A `PlantSlot` holds no state and no messenger**: it is a marker that
forwards a plant request to its Plot, which knows the slot's index. A plant anchors to (Plot `NetworkId`, slot
index).

**Engineering notes.**

- The shed interior is a **derivation** in the [[principles]] tier sense — computed from a fact every peer
  already holds, never sent. Do not network the parchments inside it (`TeammateSlotDisplay` is a plain
  `XRGrabInteractable`); they look like the application scrolls and behave nothing like them.
- The departure grace (180 s, [[#Departures]]) was set when it held PlantSlots; a claim is a much larger thing to
  lose than a slot, so that number should be re-judged rather than carried over.

## How it is built

**Status (2026-11-04):** built; joining switched off.

- **The barrel is a `DispensingEntity`** (`DeedHut/ItemDispenser`): spawns one free scroll at a time, hovers it,
  restocks when it is taken, the master deciding the take. The same holder-requests, master-decides shape as a stall
  ([[shop]]).
- **An application scroll is a `CollectibleEntity`** (`Collectible_BlankDeedScroll`, `collectibleId`
  `blank_scroll`) with a `PlotApplication` beside it: which Plot it was dropped on, and the accept when carried into
  the shed (message ids 5–7 on the scroll's messenger).
- **The deed is a `CollectibleEntity`** placed beside its Plot in the Garden ([[#The deed is part of the Garden]]);
  carried into the shed, `DeedClaimZone` asks the master to claim.
- **The teammate wall** (`PlotTeammateRoster`, `TeammateSlotDisplay`) shows the owner their teammates;
  `PlotTeammateRevokeZone` asks to remove one whose parchment is carried out of the hut.
- **The named deed** over the fireplace (`PlotClaimSignage`) shows the local player's name if they own one of the
  Garden's Plots.

## Departures

**Status (2026-09-13):** built and tested in-world (a player leaving, the master leaving).

Departures are owned by **`PlayerManager`**: one clock, `_graceSeconds = 180`, `OnPlayerRemoved` → grace →
`PlayerDestroyed`. A dropout is indistinguishable from leaving, so a departed player's garden is held for the grace
period before anything is freed; Fusion's `DestroyWhenStateAuthorityLeaves` cannot wait. On `PlayerDestroyed`,
`PlayerLifecycle` has `PlotCleanup` free the player's plots and despawn what stands in them, then drops their
balance row ([[economy]]). Plots first, so cleanup never runs against a player the economy has already forgotten.

Their `HolderId`, by contrast, is cleared **immediately**: that is a correction, not cleanup, and leaving it set makes
`SingleHolderFilter` refuse the object to everyone for the rest of the session.

## One seed, one plant

**Status (2026-11-04):** built, untested in-world.

A seed carried across two neighbouring slots touches both triggers, and each sends a plant request before the
master's "planted" reply has come back to the seed. The master used to accept both: two seeds grew four turnips.
`PlotStateManager` now remembers every seed it has planted until that seed is despawned, and refuses a second
request for it. Test: sweep a seed across the line between two slots; one plant grows.

## The deed is part of the Garden

**Status (2026-11-04):** built (Editor compile clean, prefab and scene saved), untested in-world.

Each Plot's deed is a scene object, not spawned: a `Deed` instance beside the `Plot` in `BorderedPlot.prefab`, at
the deed socket, wired to `PlotLeaseManager._deed` (Vulps, 2026-11-04). A sibling rather than a child, so one network
object never sits inside another; `NetworkGrabbable` moves it to the scene root at load, and the reference holds.

- **Every client has it from load**, already in its socket (`CollectibleEntity._startsInDispenser`), so nothing is
  sent to say so, and no master ever spawns a second.
- **Claimed, it is hidden; relinquished, shown.** Every client applies the replicated owner (`PlotLeaseManager.ShowDeed`
  on `OwnerChanged`, and at start): hidden, its grab is off (a hand holding it lets go), its renderers and colliders are off,
  it is kinematic, and the client simulating it puts it straight back in its socket (`Socket.SnapHere`). If nobody
  simulates it at that moment, auto-recall brings it home unseen once someone does. Shown again, it is held at its
  socket and called back if it is away. Nothing is despawned, so who holds it never matters.
- Claiming is unchanged: the holder carries it into the hut, `DeedClaimZone` asks, the master announces the owner.

Test: two players. The second claims a Plot (the deed vanishes for both, is back in its socket when shown, and no
"recall did not arrive" warning follows); the master leaves (no new deeds); the claimant leaves for good (their deed reappears in its socket).

## Discarding a scroll

**Status (2026-11-04):** built, compiles clean, untested in-world.

An accepted scroll is ended by the master, but the player holding it has its state authority, and only authority can
despawn. `CollectibleEntity.Discard()` therefore announces it (`discard`, message 9 on the collectible's bridge):
whoever has authority despawns it, and if nobody does, the master takes authority and does, as `Seed.RequestDiscard`
does. The scroll dispenser adopts its stock on every client when it hears the scroll is stock
(`CollectibleEntity.AnyInDispenser`), so a new master does not stock a second. Test: the owner accepts a scroll; it
is gone for everyone; the master leaves and the barrel keeps one scroll.

## Open questions

**Status (2026-11-04):** open.

**1. Does a teammate see anything?** As specified, "don't own a Plot, see nothing" leaves an accepted
teammate staring at an empty room: no confirmation they were accepted, no list of Plots they are on,
and no way to leave one. Three options —

- teammates see nothing, and access is communicated socially, out of band;
- **teammates see the Plots they are on, and dragging one out is *leaving*** — symmetric with the
  owner dragging a scroll out to *remove*, and it closes the loop on an application that otherwise
  has no confirmation step at all;
- teammates see the full deed of any Plot they are on — too much, and it blurs owning with being
  invited.

The middle one is the recommendation, and it preserves the one-verb rule in both directions.

**2. What happens to abandoned scrolls?** Passive rejection leaves a scroll lying around, and every
ignored application is another one. Give them a lifetime, or `ReturnableEntity` auto-recall
back to the barrel, which reads nicely as *the application returns to sender*.

**3. The scroll has to be readable.** A picks one up and must know whose it is before deciding, so it
needs a name rendered on it, legible at hand distance in a headset. It is the one piece of actual UI
in the design and the easiest thing here to get wrong.

**4. Repeat applications.** Nothing stops B applying twice. Simplest answer is to make accepting an
already-accepted teammate a no-op and let question 2's lifetime rule clear the litter, rather than
tracking pending applications.

**5. How does a claim end?** Dragging the deed out relinquishes it deliberately, and the departure grace
([[#Departures]]) covers a disconnect. Neither covers a player who simply stops playing while still connected, nor a
Garden whose four Plots are all claimed by people who will not be back.
