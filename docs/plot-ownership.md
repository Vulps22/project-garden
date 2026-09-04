# Plot ownership

The mechanism behind **roadmap step 1, "Plot ownership"**. Designed 2026-09-03, not yet built.
Vulps's design; the engineering notes are mine and are flagged as such. Words here follow
[`terminology.md`](terminology.md) — a **Plot** is the claimable patch, a **PlantSlot** is one square
of dirt inside it, and 24 PlantSlots make a Plot.

## What it has to do

- A Plot can be **claimed**, and the claimant's id is stored on it.
- Only the claimant and their **teammates** may plant or harvest there.
- An **unclaimed** Plot allows no planting at all.
- The claimant can **add and remove** teammates.

## The shape: everything is an object you carry

There is no UI anywhere in this design. Every ownership change is a physical object moved from one
place to another, and the vocabulary is one verb learned once:

> **Into the shed commits it. Out of the shed undoes it.**

Claim a Plot — carry its deed in. Accept a teammate — carry their scroll in. Remove one — drag their
scroll out. The shed is the single place where facts are committed, which means one trigger volume
and one decision point for the master.

## The shed

**One shed in the Garden**, physically. What is *inside* it depends on who is looking:

| Viewer | Sees |
|---|---|
| anyone | the **barrel**, with a scroll hovering above it |
| a Plot owner | their **deed**, and their **teammate list** |
| everyone else | nothing else |

The barrel being visible to everybody is what makes the whole thing bootstrap: a player who owns
nothing can still take a scroll and ask for something.

## Claiming

An unclaimed Plot has its **deed** sitting at the centre, on the same rig shop stock uses —
`ReturnableEntity` + `AlignableEntity` with auto-recall — except the grab is **not** killed, so it can
be carried away. Take it into the shed and the Plot is yours. The deed then lives in your shed, and
dragging it back out relinquishes the Plot.

That last part is why the deed belongs in the shed rather than staying on the Plot: "the deed is in my
shed" *is* the claim, and there is nothing lying in the open for someone to walk off with.

## Joining someone's Plot

The token is an **application**, issued by the person who wants in — not a grant issued by the owner.
A owns a Plot; B wants to team up:

1. B takes a scroll from the barrel. The master stamps **B's id** onto it.
2. B drops it **in A's Plot**.
3. Only A can pick it up (see the pickup rule below), so it sits there as a private message.
4. **Accept** — A carries it into the shed. B joins A's Plot.
5. **Reject** — A drops it anywhere that is not the shed. Anyone can destroy it by putting it in the
   SellPoint.

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

> Anything dropped in a Plot can only be picked up by someone with access to that Plot.

This is load-bearing twice over: it stops produce being stolen out of someone's Plot, and it is what
makes the application inbox *private*. Worth building even if the rest of this design changes.

**Engineering note.** One more `IXRSelectFilter` beside `SingleHolderFilter`, asking "am I inside a
Plot, and does this player have access?". It reads a replicated fact, so a local filter is legitimate
here — rule 3 forbids local state driving the decision, not local evaluation of a replicated one.

## What is networked, and what is not

This is the part that keeps it cheap.

| Networked | Local, derived |
|---|---|
| the claim table — `plotId → claimantId + teammateIds` | the deed shown in the shed |
| application scrolls dispensed by the barrel | the teammate list shown in the shed |
| the deed while it sits on an unclaimed Plot | everything else inside the shed |

Two players standing in the shed together see different things in the same space, and that costs
nothing, because none of it is real. Dragging a local scroll out is a local gesture that sends a
request; the master's next broadcast is what actually changes anything.

**Engineering notes.**

- The claim table is **the `EconomyManager` pattern verbatim**: master-owned, master-mutated, whole
  table rebroadcast on change, everyone rebuilding from scratch. Consider putting it beside
  `EconomyManager` rather than in a new singleton.
- The shed interior is a **derivation** in the CLAUDE.md tier sense — computed from a fact every peer
  already holds, never sent. Do not be tempted to network the scrolls inside it; they look like the
  application scrolls and behave nothing like them, and if they ever share a prefab someone will
  network the wrong one.
- `PlantSlot` should **stop holding its own `OwnerId`** and ask its Plot. Two levels each holding an
  owner is two sources of truth for "may I plant here" — rule 1, one owner per shared property.
- `GardenLease` becomes `PlotLease` and holds a **Plot claim** rather than a set of PlantSlots. Its
  120-second hold was inherited; a claim is a much larger thing to lose than a slot, so that number
  should be re-judged rather than carried over.

## Almost all of this is already built

- **The barrel is `ShopSlot` at price zero.** Spawns stock, hovers it, restocks when empty, master
  decides the take and stamps an id on the way out. That includes the parts that cost uploads to get
  right — `HasLeftSlot()` checking geometry rather than trusting `OnTriggerExit`, and the holder
  *requesting* rather than the slot inferring.
- **An application scroll is a `Seed` with a different payload**: `NetworkBridge` +
  `NetworkGrabbable`, a replicated id stamped by the master, `SingleHolderFilter` so two people
  cannot hold it, `HoveringEntity` for the barrel hover, `ReturnableEntity` so a stray one tidies
  itself.
- **Destroying one in the SellPoint is free.** `SellableEntity` with a serialized value of 0;
  `OnSold()` already *defaults* to despawning. `SellPoint` never learns what a scroll is.
- **Keep the barrel to one scroll at a time**, restocking after the previous is taken — `ShopSlot`
  already has exactly this shape in `_currentSeed`. Otherwise a player can carpet the Garden in
  scrolls.

## The structural half: one Plot, one NetworkObject, 24 sockets

Moving `OwnerId` up from the PlantSlot to the Plot is not only a correctness fix. It is what makes
the slot's `NetworkObject` pointless, and removing 24 network identities per Plot is what makes the
rest of the roadmap affordable. **These are one refactor, and splitting them would hide that.**

Look at what a `PlantSlot` actually replicates today:

```csharp
public bool          IsOccupied { get; private set; }
public string        OwnerId    { get; private set; }
public IPlotOccupant Occupant   { get; private set; }   // local reference, never synced
```

`Occupant` is not networked — a plant positions itself from the slot's NetworkId. So the *only*
reason a patch of dirt carries a `NetworkObject` **and** a `NetworkBridge` is `OwnerId`, and this
document has already decided `OwnerId` does not belong there. Take it away and a socket's remaining
networked state is **one bit**: occupied, or not. Twenty-four of those is three bytes as a mask.

| | now | after |
|---|---:|---:|
| NetworkObjects per island | 96 | 4 |
| authority reassignments per master transfer | 96 | 4 |
| three chained islands (see roadmap) | 288 | 12 |

The ~100 lines of `ReassignNullObjectsAuthority Plant_Slot` at every master transfer in the
2026-09-03 logs are this problem printing itself, once per slot.

**Two traps, both already paid for elsewhere.**

- **Author the sockets as a plain `Transform[]`.** Arrays of custom `[Serializable]` classes arrive
  empty from the bundle export — the `ProduceSlot` defect, invisible by every means the Editor
  offers and only visible in-world. Build richer per-socket objects at `Awake`.
- **`BytesWriter` is pre-sized**, so the Plot's payload calculation now covers 24 sockets. Small, but
  it is exactly the shape of "add a field, forget the size, overflow".

**One consequence to design deliberately.** A plant currently anchors to its slot's NetworkId; after
this it anchors to **(plot NetworkId, socket index)**. That is better rather than worse — stable,
meaningful, and it survives the socket not being a network object at all. `PlantSlot`'s
`RequestPlant` comment already says it is "sent on the plot's bridge rather than the seed's", which
is terminology debt today and becomes literally true afterwards.

**Sequencing.** This depends on the scene tidy in `roadmap.md` — an island cannot be one prefab until
it is one object, and a Plot cannot be one NetworkObject until the hierarchy says which sockets are
its. Do it as part of that tidy, while the island is still *placed* in the scene rather than spawned:
that step changes nothing about networking, so a Plot losing 24 NetworkObjects can be tested on its
own before anything starts spawning islands at runtime.

## Open questions

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

**2. What happens to abandoned scrolls?** Passive rejection leaves a live `NetworkObject` lying
around, and every ignored application is another one — `runtime-spawn.md` §4c notes Somnium may have
a per-world object limit nobody has hit yet. Give them a lifetime, or `ReturnableEntity` auto-recall
back to the barrel, which reads nicely as *the application returns to sender*.

**3. The scroll has to be readable.** A picks one up and must know whose it is before deciding, so it
needs a name rendered on it, legible at hand distance in a headset. It is the one piece of actual UI
in the design and the easiest thing here to get wrong.

**4. Repeat applications.** Nothing stops B applying twice. Simplest answer is to make accepting an
already-accepted teammate a no-op and let question 2's lifetime rule clear the litter, rather than
tracking pending applications.

**5. How does a claim end?** Dragging the deed out relinquishes it deliberately, and `PlotLease`
covers a disconnect. Neither covers a player who simply stops playing while still connected, nor a
Garden whose four Plots are all claimed by people who will not be back — which is the question the
old §1 engineering note raised and this design still does not answer.
