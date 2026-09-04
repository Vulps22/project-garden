# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

Project Garden — a "Grow A Garden" clone for **Somnium Space VR**. Multiplayer VR: players buy seeds
from shop points, plant them in slots, wait for them to grow, then carry the grown plant to a sell
point for currency (**Thatch**).

- Unity **6000.3.5f2**, URP 17.3.0, XR Interaction Toolkit 3.3.1
- Networking: **Photon Fusion** (via the Somnium `NetworkBridge` wrapper), Photon 2.0.5 + addons
- Requires **Somnium ProSDK v2.1**; the SDK on this machine is the 3.1.x line

**Somnium Space supports neither its SDK nor its client on Linux, and this machine is Nobara.**
Making it work anyway is the point of the exercise, so every Linux-only defect is in an untested
upstream code path. Local workarounds are the deliverable — never propose filing upstream bugs,
waiting for a fix, or switching to Windows as the answer.

## Repo layout — read this before adding files

This repo is **not a full Unity project**. It is cloned *into* an existing Somnium ProSDK template
checkout, and `.gitignore` is an allowlist: it ignores everything (`/*`) and un-ignores only three
asset folders.

```
Assets/#User/GrowAGarden/   <- all game code, scene, prefabs, materials
Assets/Cartoon_Farm_Crops/  <- third-party crop art
Assets/GVOZDY/              <- third-party art
```

Everything else in the working tree (`ProjectSettings/`, `Packages/`, `Assets/Photon/`,
`Assets/SomniumSpace/`, `Assets/Urp/`, `Assets/XR*/`) is template-supplied and intentionally untracked.

**Consequence: a new top-level folder or root file will be silently ignored** until you add a matching
`!` rule to `.gitignore`. Check with `git check-ignore -v <path>` if a new file isn't showing in
`git status`.

Inside `Assets/#User/GrowAGarden/`:

```
GrowAGardenScene.unity      the one scene
prefab_buyables/            BuyPoint{,Carrot,Turnip,Pumpkin} — the shop slots
prefabs_sellables/          SellPoint
prefabs_crops/              Seed_/Plant_/Produce_{Carrot,Turnip,Pumpkin} — nine spawnable prefabs
Prefabs_world/, Materials/, sounds/
scripts/                    see "Where the code lives" below
```

### Two things that live outside this repo

**`Assets/#User/Community Modules/`** — template-supplied, untracked, and the source of the
`SomniumSpace.Network` assembly (`NetworkBridge`, the RPC transport everything is built on). If
`NetworkBridge` fails to resolve, the template is missing Community Modules; it is not a code bug.
See the vendoring rules below — they are the opposite of what they used to be.

**`Packages/com.somnilux.sdk/`** — "Somnilux", Vulps's own Editor-only local package
(github.com/Vulps22/somnilux), holding every Linux workaround for the ProSDK plus the NetLog HTTP
logger. Untracked here because `Packages/` is outside the allowlist. It is *not* part of the game and
never reaches an exported world (`includePlatforms: ["Editor"]`). Contents:

| File | What it works around |
|---|---|
| `BundleCaseFix` / `BundleCaseRestore` | SDK asks for an UPPERCASE sha256 bundle name; Unity always lowercases. Invisible on case-insensitive filesystems. |
| `BundleCompressionFix` | Forces LZMA or LZ4; see the client-version trap below. |
| `Preview360Fix` | `CRTWaitForUpdate` polls a CustomRenderTexture that was already destroyed, so the 360 preview stays empty and upload aborts. Blits the cubemap to an equirect JPEG instead. |
| `UploadRefreshGuard` | Holds off domain reloads during an upload. |
| `NetLog/` | Harmony-patches every managed HTTP stack the SDK uses and surfaces requests/responses in an Editor window. Built to debug export and login failures from the inside, since the ProSDK ships as opaque DLLs. |

Menu: **Somnilux** in the Editor menu bar (LZMA/LZ4 toggle, "Fix 360 Preview Now", "Release Upload
Refresh Hold", "Log Patch Status").

Known noise: NetLog's bundled `0Harmony.dll` is Harmony 2.3.3, which ILMerges MonoMod.Core + Cecil.
Burst's `EntryPointMethodFinder` hashes every assembly's references, its metadata reader throws
`BadImageFormatException: Read out of bounds` on that merged DLL, and "Failed to find entry-points"
spams **every domain reload**. Harmless — the DLL is valid, Harmony works, exported worlds were never
affected. Swapping in the non-merged Harmony 2.2.2 build is the likely fix. Don't re-diagnose this.

### `_parked/` — kept, not deleted

Outside `Assets/`, git-ignored. `v2-community-modules/` (29 unreferenced V2 copies),
`vendored-SceneNetworking/` (a *stale* copy — read its README before touching it),
`stale-bundle-1930/`, `netlog-round2/`.

### Community Modules: vendor, do not reference

**The Somnium uploader validates your asmdef's references against a whitelist and rejects
`CommunityModules`** ("Unsupported Scripting References Detected"). It also compiles your sources
**server-side** against
`imprt.somnium.space/SDK/FilesStorage/ScriptingReferences.txt`; anything off that list is *skipped*,
so a `using` of it fails `CS0246` and the world silently never publishes — while the HTTP upload
still returns 200.

Accepted: `SomniumSpace.Network`, `AVProVideo.Runtime`, Unity/Fusion/Photon package assemblies.
Rejected: `CommunityModules`, `SomniumSpace.Avatar.Holders.Handlers`.

So the V2-era workflow is still necessary in V3, but the *discipline* around it is what matters:

- Vendor **one file at a time**, sourced from **V3's** `Community Modules/` — never an older copy.
- Re-namespace it to `GrowAGarden`.
- **Generate a fresh `.meta` GUID.** Copying the `.meta` alongside the `.cs` is what produced 39
  duplicate-GUID collisions, cleaned up 2026-08-25. Unity papers over those by assigning fresh GUIDs
  in `Library/` without rewriting the `.meta`, so bindings become unreproducible and re-roll on any
  `Library` wipe or fresh clone. **Duplicate GUIDs under `Assets/#User/` are currently 0 — keep it
  that way** (`find Assets/#User -name '*.meta' -exec grep -h '^guid:' {} \; | sort | uniq -d`).
- Also strip editor-only code. The server compile scans the assembly and an `#if UNITY_EDITOR` guard
  does **not** help, because the Editor-compiled assembly still carries the reference.

Currently vendored under `scripts/Networking Plugin/`: `SceneNetworking.cs`, `NetworkGrabbable.cs`,
`Utils/{BytesReader,BytesWriter}.cs`.

`SceneNetworking` has **one deliberate divergence** from CM V3 — `UnityEditor.PrefabUtility.
IsPartOfPrefabAsset(no)` replaced by the runtime-safe `!no.gameObject.scene.IsValid()`, for exactly
the reason above. Diff normalized for line endings and namespace before assuming a drift is
accidental. `BytesReader`/`BytesWriter` are older forks that back every RPC payload; swapping them for
CM's would be a wire-format change, so they are left alone.

### Where the code lives

```
scripts/scripts/                 game logic
  Plants/                        Plant, RootedPlant, BearingPlant, SingleHarvestPlant,
                                 MultiHarvestPlant, ProduceSlot
  Produce/                       Produce, RootedProduce
  economy/                       EconomyManager, PlayerBalance, BalanceDisplayManager
  Physics/                       KinematicController, ReturnableEntity, AlignableEntity,
                                 HoveringEntity, IKinematicSource, ILifecycleNotifier
  Ownership/                     AuthorityController, GardenLease, IAuthoritySource, IPlotOccupant
  Interaction/                   SingleHolderFilter, SellableEntity, IHeldObject,
                                 ISellValueSource, XRGrabInteractableRef
  Seed, ShopSlot, PlantSlot, SellPoint, SeedDefinition, IPlantable, Logger, ExceptionAlarm
scripts/Seeds/                   CarrotSeed, TurnipSeed, PumpkinSeed (SeedDefinition subclasses)
scripts/Networking Plugin/       vendored CM (see above)
scripts/{Interactions Plugin, ARS Additional, Networked Components, Interactions Examples}/
                                 GAG-only scripts with no CM counterpart
scripts/#Scripts Backup/         dead .txt snapshots from 2026-02-23. Ignore it; never edit it.
```

## Build / run / test

There is no test suite and no lint step. **Nothing is truly verified until it is uploaded and played
in-world** — this is a Somnium Space world, not a standalone game, so VR interaction, multiplayer, and
the Somnium runtime around it only exist once the world is deployed and joined from the Somnium client.
Editor Play mode has no Somnium game behind it.

The loop is five steps, and only the last two verify anything:

1. **Make the change.**
2. **Check for errors** — via the `unity` CLI against the running Editor (below). Fast, but this only
   proves it *compiles*. Never describe a change as working on the strength of step 2.
3. **Deploy** — export/upload to Somnium via the ProSDK menu. Not scriptable; a GUI action.
4. **Connect and test** — join the uploaded world from the Somnium client, in VR.
5. **Check the logs** for exceptions and data.

Steps 3–5 are yours to drive; they cannot be run from here. When you have only done steps 1–2, say so
plainly — "compiles clean, untested in-world" — rather than implying the behaviour is confirmed.

**One phase per upload.** An upload is a slow manual round trip, so don't stack unrelated changes into
one. If a fix is needed mid-phase, `git stash -u` the WIP, make the fix, wait for the test, then pop.

### Step 2: compile-checking from the CLI

The `unity` CLI talks to a *running* Editor over the `com.unity.pipeline` package (installed into the
untracked template-supplied `Packages/manifest.json`, so it never dirties this repo).

Setup, once per machine: `unity pipeline install`, then focus the Editor window so it resolves the
package. `unity status` should then show a port and `ready`. If `Server Reachable` is false in
`unity pipeline list`, the Editor just hasn't re-resolved the manifest yet — focus it.

```bash
unity command recompile                  # returns immediately; "up_to_date" if nothing changed
unity command recompile_status --json    # poll -> {"status":"completed","failed":bool,"errors":[...]}
unity command get_console_logs --json    # structured Editor console, with stack traces
```

Also useful: `eval` / `eval_file` run C# against the live Editor via Roslyn — the practical way to
inspect scene state (pools, shop slots, `EconomyManager`) without entering VR; plus `editor_play` /
`editor_stop`, `capture_game_view`, `find_gameobjects`, `get_scene_hierarchy`, `menu`. Full list:
`unity command`.

Three gotchas, all of which have cost real time:

- **Unity cannot compile while another window has focus.** The bridge still says `ready` and
  `recompile_status` sits on `triggered` forever. Vulps is usually in a browser, terminal or the
  Somnium client. If `triggered` comes back twice in a row, **stop polling, finish the rest of the
  work, and flag the unverified compile at the end** — report it as "written, not compile-checked",
  never as compiling clean. One `AssetDatabase.Refresh()` via `eval` is worth a single attempt.
- **Never trigger a recompile while an upload is running.** A domain reload mid-build can break the
  export. Check `Logs/network.log` for a recent `confirmUploadCdn` first.
- Command args are **positional**, not `k=v` (`unity command eval 'return 2+2;'`), and `data.result`
  is sometimes a nested object but sometimes a **JSON-encoded string** needing a second parse.

`unity test` and `unity build` spawn their *own* batch-mode Editor, which contends with the open Editor
for the project lock — prefer the `unity command` bridge. There are no test assemblies under
`Assets/#User/` anyway.

The Editor leaks memory until it OOMs. Watch RSS against a ~3.1 GB baseline and suggest a restart at a
natural break.

### Step 3: when the upload itself misbehaves

Restore/consult Somnilux's NetLog (see above). Also worth knowing:

**"Bundle file is Empty" is a compression mismatch, not a casing bug.** SDK 3.1.7 (2026-08-26) switched
asset bundles to LZ4; a client built before that cannot unpack them. Casing and cache corruption were
both chased first and both were wrong. Cheap check straight from `Logs/network.log`: each upload's JSON
declares `FileSize` (the zip) and `BundleFiles[0].FileSize` (the .unity3d) — LZMA bundles are dense so
zipping *adds* ~7 KB, LZ4 bundles compress further so the zip comes out ~1.3 MB *smaller*. The sign of
`bundle - zip` tells you the compression without opening anything. `BundleCompressionFix` is the lever.

### Step 5: what to look for in-world

Nothing in the Unity Editor console sees in-world behaviour. The client log is here:

```
/mnt/games/somnium-space-vr/drive_c/users/steamuser/AppData/LocalLow/Somnium Space Ltd/Somnium Space VR/Player.log
```

(`Player-prev.log` is the previous session; the `drive_c` path is because Somnium runs under Proton.)

**Check its size before reading it.** A per-frame throw took it to 647 MB / 11.6 M lines in one
session. Use `grep -n -m` and line-ranged `sed`, never a whole read. `grep -c` on a 600 MB file is
fine; `head`/`tail` are instant.

- `grep -n -m 15 "GrowAGarden" "$L"` — where the world assembly loads and the first project exception.
- `grep -o "[A-Za-z]*() '[^']*' — [^;]*" "$L" | sort | uniq -c` — turns the project's logging
  convention into a histogram of what actually went wrong.

**A session with a per-frame throw cannot be used to judge feel.** An exception plus a log write per
object per frame destroys frame time; anything "tested" in such a session has to be re-tested.

`ExceptionAlarm` turns off the sun on any exception with `GrowAGarden` in its stack trace, so a
pitch-black world reads as a crash but *is the alarm firing*. That is the signal, not a lighting bug.

Multi-client behaviour — master-client authority, RPC state sync, late-join — is only observable here,
with several real clients connected. A single client tells you nothing about any of it.

## Architecture

### Authority model

Master-client authoritative, with **three** distinct notions of ownership — do not conflate them:

- `SceneNetworking.IsMasterClient` — scene-wide owner. Gates economy mutation, spawning, planting
  decisions and slot bookkeeping. It resolves to the **first player to join** (confirmed empirically,
  though not yet in a busy multi-client session — treat as confirmed-but-provisional).
- `networkBridge.Object.HasStateAuthority` — **per-NetworkObject** Fusion authority: who *simulates*
  this object and who may despawn it. It transfers on **hover**, not on grab (`NetworkGrabbable`
  calls `RequestControl()` from `firstHoverEntered`), so **authority is not evidence of anything** —
  a player who waves a hand near a ripe pumpkin owns it, having harvested nothing.
- `OwnerId` on a plot or seed — **whose it is**. A replicated fact decided by the master, in the
  same shape as `IHeldObject.HolderId`. See "Ownership is a fact, not state authority" below.

A plant is simulated only by its state authority; every other client is a passive proxy applying RPC
state. Because authority transfers on hover, every decision that matters must be *announced* by the
master, never inferred from who happens to own an object.

### The governing rule: the master is the source of truth

Every peer should be able to ask the master what something is and get back **facts, not
inferences**. Almost every multiplayer bug this project has had was a client inferring something
it should have been told — the master reading geometry to decide a sale, a shop slot reading
`isSelected` to decide a purchase, a fresh authority asserting a lifecycle flag it had guessed at.

Two things this rule is *not*:

- **It is not about peer-to-peer traffic.** Fusion relays everything through the Photon server;
  there is no peer-to-peer to eliminate. `ShopSlot.OnTriggerExit` once ran on every client and each
  decided the purchase for itself — all through the server, and still wrong. The axis is
  **authorship**: one client decides, everyone else observes.
- **It is not uniform.** At 200 ms, routing a held object's position through the master would make
  carrying anything unusable. The rule needs three tiers.

| Tier | Owner | Examples | How it travels |
|---|---|---|---|
| **Facts** | **master, always** | who owns what, who bought what, what is planted where, balances, what exists | RPC to all, from the master, at the moment it decides |
| **Derivations** | nobody — computed | growth completion, current phase, target scale | not sent at all; every client derives it from a fact |
| **Simulation** | the state authority | where a held object is this frame | Fusion transform replication |

**Derivations are the tier to reach for first.** `GetGrowthCompletion()` is the model: sync
*"planted at T"* once and every client computes the same scale every frame, forever, with no
further messages. Anything computable from a fact should be computed, not broadcast — a phase
transition is the clock crossing a line that everyone can already see, not an event needing an RPC.

**And the clause that keeps it playable: optimistic locally, authoritative eventually.** The player
takes the seed the instant they grab it; the master confirms ~200 ms later and only intervenes if
the answer was no. Without this the rule reads as "wait for the master", which is exactly the
flicker the hover-authority fix removed. A decision may be slow. A hand must not be.

Practical consequences, all of which the code should honour and some of which it does not yet:

- A buy is **decided** by the master. A sell must be **accepted** by the master, not observed by it.
- Planting, harvesting and pooling are facts the master decides, not events it learns about.
- An object no player is holding should be owned by the master — this is `IAuthoritySource.ShouldMasterOwn`,
  which encodes the rule correctly but does not always win.
- A pull channel ("master, what is this?") is a **repair** path for a client that suspects it is
  desynced, not the primary one. If the master announces every fact, peers already have them.

### Three more rules, learned the hard way

These came out of the seed-physics rework and the 2026-09-01 multiplayer failure — the third is a
corollary of the governing rule above. Apply them to new code rather than re-deriving them.

**1. One owner per shared property.** Every serious bug in this project was several components writing
the same field from their own stale copy. `KinematicController` exists to *own* `isKinematic` and
`useGravity`, not to do a job. If you need a new shared physical property, give it an owner.

**2. Idle means idle.** A component that drives the rigidbody every frame fights XRI, the hands and
the solver forever. `ReturnableEntity`, `AlignableEntity` and `HoveringEntity` write **nothing at all**
until told to act. That removed a whole class of bug rather than guarding against it. Related: a
threshold says when to *start*, not where to *stop* — always land exactly on the target, or objects
park "close enough" with nothing left to correct them.

**3. Local state must never drive a networked decision.** (The governing rule, in the specific.) `XRGrabInteractable.isSelected` is true on
exactly one machine and is not replicated. Every decision built on it was made independently, and
differently, on each client — that single property produced four symptoms that looked like unrelated
bugs. The replicated equivalent is **`IHeldObject.HolderId`**, synced via `PlantMessageType.grabber`.
Two corollaries:

- **A decision that must be the same everywhere is made in one place** — the master for economy and
  slot bookkeeping, the state authority for movement. Everything else observes.
- **A local suspension must never depend on remote state to be released.** Arrival is judged on every
  client; only movement is authority-gated; a timeout is the backstop.
- **Prefer an explicit request over inference** whenever the initiating client knows something no one
  else does. The purchase flow below is the worked example.

### Behaviour lives in components on the prefab

`PlantSeed` used to own lifecycle, growth, serialization, planting, grab arbitration, physics mode and
ownership. The physical and interaction concerns are small components resolved through one-member
interfaces, so they know nothing about crops. They live on **`Seed` and `Produce` only** — the two
things a hand can reach:

| Component | Owns | Talks to |
|---|---|---|
| `KinematicController` | the *only* writer of `Rigidbody.isKinematic` / `useGravity` | `IKinematicSource` |
| `AuthorityController` | returning state authority to the master for unheld world-owned objects | `IAuthoritySource` |
| `SingleHolderFilter` | refusing a grab when someone else holds it (`IXRSelectFilter`) | `IHeldObject` |
| `ReturnableEntity` | travelling back to a target position when recalled | `IHeldObject` |
| `AlignableEntity` | turning back to a stored target rotation when realigned | `IHeldObject` |
| `HoveringEntity` | falling, landing, rising to hover, bobbing, spinning, righting | told explicitly |
| `SellableEntity` | value, whether it can be sold, and what a sale does to it | `ISellValueSource` |

They re-evaluate on `ILifecycleNotifier.LifecycleChanged`, which `Seed` and `Produce` raise after
every lifecycle transition and on every `grabber` RPC. The event deliberately carries **no payload** —
subscribers read what they need from the source, so a new behaviour needs no interface change.

`Seed` and `Produce` supply the answers: `ShouldBeKinematic`, `ShouldUseGravity`, `ShouldMasterOwn`,
`HolderId`, `IsHeld`. Each is *derived* rather than remembered.

**A produce hovers only once harvested.** Until a player takes it, it is attached to a plant or
sitting in the earth and must not drift — so `HoveringEntity` stays off. That is rule 2 applied at
the right seam, and it means a socket never fights a hovering body for position.

`ReturnableEntity` and `AlignableEntity` are deliberately separate: coming back to a place and facing
a particular way are different wants. `AlignableEntity`'s target is a `Quaternion` snapshot, not a live
`Transform` — it goes stale if whatever defined "upright" moves. `HoveringEntity` rights its own up
vector while spinning, because the spin is what creates the conflict; it uses up-vector alignment
rather than per-axis euler flags, since rotations don't decompose into independent axes past ~90°.

Hover/fall/settle values are serialized on the prefabs, so tuning feel needs no code change: hover
height 0.25, rise 0.6 m/s, fall gravity scale 0.35, max fall 2.5 m/s, spin 20°/s.

### Networking: hand-rolled byte packing

All sync goes through `NetworkBridge` RPCs carrying `(byte messageId, byte[] data)`, serialized manually
with `BytesWriter`/`BytesReader`. There is no Fusion `[Networked]` property anywhere.

- Each component defines its **own private `enum ...MessageType : byte`** — `SeedMessageType`,
  `PlantMessageType`, `ProduceMessageType`, `PlantSlotMessageType`, `SellMessageType`. IDs are scoped
  per NetworkBridge, so `0` in one is unrelated to `0` in another, and each starts at 0. **Append new
  values, never insert** — these are wire ids, and renumbering makes two builds disagree about what a
  message means.
- `BytesWriter` is **pre-sized** — you must compute the exact byte count up front. Adding a field to a
  payload means updating the size calculation too, or it will overflow. See
  `GetExtraBroadcastStateSize()` for how subclasses extend a parent's payload.
- Three delivery targets, chosen deliberately: `RPC_SendMessageToAll`, `RPC_SendMessageToProxies`
  (state sync — excludes the authority that already has the state), `OnMessageToController`.
- `long` timestamps are split into two `int`s (high/low); the low half must be re-read as `(uint)` to
  avoid sign extension.

### Late-join sync

New clients get state pushed, never pulled: `SceneNetworking.OnOtherPlayerJoined` → authority calls
`broadcastState()`. `EconomyManager` does the same on `OnBecomeWorldMaster` (deferred one frame via
`BroadcastNextFrame`). `Seed` also re-broadcasts on `OnStateAuthorityChanged`, because
`broadcastState()` is a no-op without authority — anything that changed while unowned was never sent.
This is the mechanism behind open issue #40.

### Three classes, three lifetimes

`PlantSeed` is gone. It used to be a seed, a growing crop and a fruit by turns, which is why it owned
lifecycle, growth, serialization, planting, grab arbitration, physics mode and ownership at once.
Those are now three objects with three lifetimes, and **only two of them can be touched.**

| | what it is | grabbable |
|---|---|---|
| `Seed` | shop stock, then a bought thing, then something you put in the ground | yes |
| `Plant` | spawned into a plot by the master, grows there, never reached by a hand | **no** |
| `Produce` | what a plant yields; the only thing that is ever sold | yes |

That a plant cannot be touched is what makes it small: no `XRGrabInteractable`, no
`NetworkGrabbable`, `SingleHolderFilter`, `ReturnableEntity`, `AlignableEntity`, `HoveringEntity` or
`KinematicController` — and no colliders at all. It keeps `AuthorityController`, because a plant does
belong to the world.

```
Plant (abstract)                   Produce
├── RootedPlant                    └── RootedProduce
└── BearingPlant (abstract)
    ├── SingleHarvestPlant
    └── MultiHarvestPlant  (#43, written, no crop uses it)
```

- **`RootedPlant`** (carrot, turnip) *is* the crop: on full growth it spawns a `RootedProduce` at its
  own transform and despawns itself, handing the plot over rather than freeing it.
- **`BearingPlant`** (pumpkin) keeps standing and hangs produce off sockets. `SingleHarvestPlant`
  withers once every socket has yielded; `MultiHarvestPlant` refills and carries on.
- **`RootedProduce`** earns its class by **owning the plot** until it is pulled — otherwise a player
  could sow a seed into ground that visibly still holds a carrot.

Growth and ripening are **derived, never sent**: every client compares now against a timestamp and
arrives at the same scale. Syncing one timestamp syncs the whole thing forever.

**A plant positions itself from its plot.** Fusion's spawn pose is local to the spawner and not
networked, so the plot's `NetworkId` rides in the state payload and each client places the plant from
it. A plant never moves, so paying for a `NetworkTransform` to replicate a value that changes once
would be absurd.

### Traits, not interfaces the class has to admit

`SellableEntity` is a component you add, in the same family as `KinematicController`,
`SingleHolderFilter`, `ReturnableEntity`, `AlignableEntity` and `HoveringEntity` — behaviour that
attaches to anything and knows nothing about crops. A produce is sellable; a watering can or a sword
could be.

It owns the object's half of a sale: `SellValue` (from an optional `ISellValueSource`, else a
serialized number), `CanBeSold` (its own `enabled` flag — off while unripe), `SetPending` (leave the
world when offered, come back if refused) and `OnSold()`, which **defaults** to despawning rather
than requiring it. `SellPoint` therefore has no idea what a crop is.

### Ownership is a fact, not state authority

Plots carry `OwnerId` — a Somnium player id, decided by the master and replicated, exactly as
`IHeldObject.HolderId` is one level down. Seeds carry their own, because they leave the garden.

**Do not reach for Fusion state authority for this.** `NetworkGrabbable` calls `RequestControl()` on
`firstHoverEntered`, so authority transfers to anyone whose hand comes *near* a seed or produce —
ownership by proximity, without a grab. Authority is also the master's channel for acting on objects
at all (`SellPoint`, `ShopSlot` and `AuthorityController` all take it as routine business), and it
vanishes the moment a player disconnects.

`GardenLease` holds a departed player's garden for **120 seconds** before freeing their plots and
despawning what stands in them — a dropout is indistinguishable from leaving, and
`DestroyWhenStateAuthorityLeaves` cannot wait. Their `HolderId`, by contrast, is cleared
**immediately**: that is a correction, not cleanup, and leaving it set makes `SingleHolderFilter`
refuse the object to everyone for the rest of the session.

### Runtime spawn — nothing is pooled

`SceneNetworking._networkPrefabs` registers the nine crop prefabs into Fusion's `PrefabTable`;
`ShopSlot`, `PlantSlot` and `BearingPlant` spawn from it and despawn when done. Always with
`NetworkSpawnFlags.SharedModeStateAuthMasterClient`, so **the client that decides a thing exists is
the client that owns it** — which is what closed the gap behind most of the 2026-09-02 bugs, since
`broadcastState()` self-gates on `HasStateAuthority`.

Four traps, each of which cost an upload. `docs/runtime-spawn.md` §4 has the full list.

- **`Spawned()` runs before `Start()`.** Subscribe to `NetworkBridge.OnSpawned` in `Awake`.
- **Never gate a spawn on our own statics.** `IsSharedModeMasterClient` goes true as soon as the peer
  is in a room, and `IsNetworkReady` is a static that outlives its scene. Ask the runner:
  `runner.IsRunning && runner.LocalPlayer.IsRealPlayer`. Spawning early makes Fusion instantiate the
  prefab and *then* throw from `Simulation.GetNextId()`, leaving orphans nothing will ever clean up.
  Rate-limit retries for the same reason.
- **A kinematic `NetworkRigidbody3D` ignores `transform.position`** — use `Teleport()`. Non-kinematic
  bodies are physics-driven and don't show this, which is why seeds worked and produce didn't.
- **Arrays of custom `[Serializable]` classes arrive empty from the bundle export.** Plain
  `Transform[]` survives. The Editor reports the array as correct by every available means, so this
  is only visible in-world. Author plain arrays and build richer objects at `Awake`.

### Deleting a class hands its job to the prefabs, silently

Every failure in landing the split was this shape. `PlaceInShop` used to write the pose after a
spawn; `OnGrowthUpdated` used to overwrite the root scale every frame; `UpdateVisuals` used to enable
the renderer; the growth phases used to overwrite the body scale. All four deletions were correct,
and all four left a prefab holding a value that had never been meaningful and was suddenly the only
source of truth.

**After deleting anything that wrote to a component every frame, audit what the prefab now says about
it.** The prefabs are now authoritative for appearance, scale and physical presence, and nothing at
runtime will second-guess them.

### The shop purchase flow

Worth reading end to end before changing anything in `ShopSlot` — nearly every step is there because
the obvious version was wrong.

1. Master spawns a `Seed_` prefab, `PlaceInShop()`, `AssignSlot()`. Stock is **physical**, not
   kinematic; `ReturnableEntity` + `AlignableEntity` are armed with auto-recall so a nudge tidies
   itself up. Prop collisions are ignored per collider pair (the slot anchor sits inside the barrow
   mesh) — done pairwise rather than by layer, because the physics collision matrix lives in
   `ProjectSettings` and **does not travel inside an exported asset bundle**.
2. A player grabs it. `SingleHolderFilter` refuses if someone else holds it. Affordability is
   deliberately **not** checked here — an unaffordable seed is still grabbable, so the player gets
   something to feel rather than a seed that silently refuses to move.
3. The seed crosses the slot trigger. `OnTriggerExit` **cannot be trusted**: Unity re-creates the
   PhysX actor when `isKinematic`, `detectCollisions` or a collider's `enabled` changes, and fires
   exits for everything it was overlapping with nothing having moved. `HasLeftSlot()` therefore checks
   the geometry, not the event.
4. **The holder** calls `RequestPurchase()` — the holder is the only client that knows its own hand is
   on the seed. Sent to all; everyone but the master ignores it.
5. **The master** decides in `OnPurchaseRequested`: known buyer? known state authority? affordable?
   Then `ReleaseSlot()` → `Purchase()` → `RemoveBalance()` → restock. Anything else routes to
   `ReturnToSlot()` or `RejectPurchase()`, which put the seed back **where it stands** rather than
   teleporting it, and keep the slot's `_currentSeed` so nothing restocks.
6. If nobody anywhere holds it (`HolderId` empty), it was shoved out by a hand or another seed —
   `ReturnToSlot()`. Treating that as a sale handed out free seeds and restocked the slot, which is a
   straightforward way to print crops.
7. `ShopSlot.Update()` (master only) re-stocks whenever the slot is empty — a sold or planted seed
   is despawned, so a Unity-null `_currentSeed` is the only empty state there is. Retries are
   rate-limited to twice a second: a failing per-frame spawn is hundreds of attempts and, when
   Fusion throws rather than refusing, hundreds of orphans.
8. `OnTriggerEnter` latches only seeds whose `seedId` matches the slot's. Without that, a carrot
   carried past the pumpkin buy point en route to the sell point became that slot's stock.

### Seed definitions are MonoBehaviours, not ScriptableObjects

`SeedDefinition` is an abstract `MonoBehaviour` whose concrete subclasses (`CarrotSeed`,
`TurnipSeed`, `PumpkinSeed`) **hardcode** id, prices and durations in `Init()`, called from both
`OnEnable` and `OnValidate`. Balance changes are code changes, not Inspector edits.

**One definition per crop, referenced by all three of its prefabs** — seed, plant and produce — so
there is one place per crop to read and to change. The multi-phase `phases` list is gone: the plant
owns `growthDuration`, the produce owns `ripenDuration`, and a bearing plant owns `witherDuration`.

A produce **must not** be handed the plant's definition component: a `RootedPlant` despawns itself
the statement after it bears, so the produce would spend its life pointing at a component on a
destroyed GameObject. Unity's overloaded null then makes every `!= null` read false, which is how a
carrot came to sell for nothing while its ripening still looked right by coincidence.

To add a crop: a `SeedDefinition` subclass, three prefabs, all three registered on `SceneNetworking`,
and a `ShopSlot` pointed at the seed.

Current balance: carrot 10/15, grows 10 s. Turnip 18/25, 15 s. Pumpkin 60/110 — vine 60 s, fruit
ripens 60 s, withers 30 s.

### Economy

`EconomyManager` (singleton) holds `Dictionary<playerId, PlayerBalance>` and is the only writer.
Mutations are master-only; every change re-broadcasts the **entire** balance table to all clients, which
rebuild their dictionary from scratch and refresh `BalanceDisplayManager` (a sorted scoreboard).
`OnPlayerBalanceChanged` fires locally afterward.

Note `RemoveBalance` deliberately applies locally on non-master clients before the broadcast arrives
(optimistic deduction, from #39) — `AddBalance` does not.

Because the table is cleared and rebuilt on every broadcast, `GetLocalPlayer()` can be **transiently
null**. Refuse rather than guess when it is; the next broadcast restores it.

## Conventions

- **Commits:** `#<issue> <imperative description>` — e.g. `#42 Add null socket guard in BearingPlant.Awake`.
  Issue number first, present tense. Recent work without an issue drops the number and reads as a
  sentence about intent (`Let the holder ask to buy and the master decide`).
- **Branches:** `feature/<issue>-<slug>` or `feature/<slug>`, `wip/<slug>` for exploratory work.
- **Logging:** always the project's `GrowAGarden.Logger` (`Log`/`Info`/`Warn`/`Error`), never `Debug.Log`
  directly. Messages conventionally lead with `Method() 'objectName' — ` context; the log histogram in
  step 5 depends on that shape.
- **Events:** an event we declare is named for **the fact** — `Sold`, `Harvested`, `PurchaseRequested`,
  `PlayerGone` — and a handler is `On` + that name, so it reads `Seed.PurchaseRequested +=
  OnPurchaseRequested`. The `On`-prefixed *events* in the tree (`NetworkBridge.OnSpawned`,
  `SceneNetworking.OnOtherPlayerJoined`, `OnBecomeWorldMaster`) are **vendored or SDK, not ours** —
  that is the whole of the apparent inconsistency. `EconomyManager.OnPlayerBalanceChanged` is the one
  event of ours out of step and should become `PlayerBalanceChanged`.
- Fields are `_camelCase` private + `[SerializeField]`; wire references in the Inspector, and use
  `OnValidate()` to auto-populate same-GameObject components.
- **Comments explain why, not what.** The existing docstrings record the bug each guard exists for.
  Preserve that when editing; a guard with no rationale gets removed by the next person.

## Debug utilities

`ExceptionAlarm` is the only one left in the scene — it turns off the sun when any exception with
`GrowAGarden` in its stack trace is logged. A pitch-black scene in VR means an exception.

`Debug_EconomyBoost` and `Debug_MasterClientIndicator` were **deleted**. The indicator forced scene
ambient light via `SomniumBridge.Environment` and was itself the cause of an unrelated "black sky"
report. Removing the booster left a dangling `Debug_EconomyBoost.Instance` dereference in
`PumpkinSeed.Update()` that threw every frame on every pumpkin — see the log-size note above.

The master-client probe going forward is `BalanceDisplayManager.Set`'s existing
`Logger.Log($"...IsMasterClient={...}")`, read from the client log — no side effects.

`Produce.ReportPlacementOnce()` is **temporary instrumentation**, not a debug utility: one line per
produce, a second after it is born, saying where it actually is and what it is worth. It exists
because the logs used to report the position we *asked for* rather than the one the object got, and
that cost two uploads. Delete it once late-join, player-leave and master-leave are tested.

## Current state

Branch **`feature/runtime-spawn`**, well ahead of `main`; `main` is ahead of `origin/main` and
unpushed. Tags `MVP`, `add-turnip` are historical.

**The core loop works in-world, on both crop shapes** (confirmed 2026-09-03). A rooted crop is
bought, planted, grows, becomes produce, is carried and sold. A bearing crop grows a vine, bears into
a socket, ripens, is harvested, withers, and frees its plot to be replanted. No project exceptions in
the session; the only exceptions in the log are Somnium's own avatar system.

Design docs in `docs/`. **The roadmap holds milestones only; mechanism lives in a doc per step,
named after that step.** A step doc starts as a design and becomes the record of what was built —
`runtime-spawn.md` is that whole arc — so there is no separate "how it works" document to write
later. Link both ways, and do not create a step doc until the step has real design in it.

- `terminology.md` — **read this first.** World / Island / Garden / Plot / PlantSlot, and the renames
  the code still owes it. Several of these words used to mean something else, and the scripts still
  use "plot" to mean PlantSlot.
- `roadmap.md` — **start here after that.** Plot ownership, upgrades, generalised spawn slots, world
  cycling, and a future section on procedurally generated islands.
- `plot-ownership.md` — step 1's mechanism: the deed, the application scroll, the shed as the one
  place ownership changes are committed. Designed, not built.
- `runtime-spawn.md` — what replaced pooling, and the four traps.
- `bearing-plants-and-produce.md` — the Seed/Plant/Produce design.
- `world-bridge.md` — a deferred refactor; see below.

### ⚠ Reset before this is anything but a test build

**`EconomyManager._startingBalance` is back to 10** (2026-09-03). It had been raised to 500 so the
pumpkin could be reached without ten manual carrot loops in VR — a testing tax, not a design
decision. If it is ever raised again for testing, it is one serialized field on `SceneManager` in the
scene, and putting it back is the last thing to do before an upload that is not a test build.

Still temporary: **`Produce.ReportPlacementOnce()`** logs a produce's real position, scale, ripeness
and value one second after it is born. It exists because two uploads were spent on "the produce is
missing" when the only position in the log was the one we *asked for*. Delete it once the remaining
tests below are green.

### What still needs testing, and what to learn from each

None of these can be answered by one player standing in a world alone.

**A second client joining a running world.** The one unevaluated guess left on the branch:
`ShopSlot.AnnounceStock` re-broadcasts stock state three times (0.25 s, 1 s, 2 s after spawning),
because a spawned object reaches other clients a few ticks after the spawner and an RPC about an
object a client does not have yet is silently dropped. **Learn:** does a late joiner see shop stock,
growing plants at the right size, and ripe produce? If yes, collapse `AnnounceStock` to a single
broadcast and delete the loop. If they see nothing, the answer is not more broadcasts — it is putting
birth state in the spawn snapshot, for which `NetworkBridge` already exposes `[Networked]
SyncByteArray`. Watch for whether growth *scale* is right, not just presence: scale is derived from a
timestamp, so a wrong size means the timestamp never arrived.

**A player leaving.** `GardenLease` should clear their `HolderId` immediately and hold their plots for
120 seconds. **Learn:** can another player pick up a seed the leaver was carrying (immediately, not
after 120 s)? Does the garden survive a rejoin inside the window? Does it actually get cleared after
it — plots freed, plants and unharvested produce despawned, their loose seeds gone? The 120 s figure
is a guess and should be judged against how a real dropout feels.

**The master client leaving.** The least understood path in the project and the one most likely to
bite. **Learn:** does the new master take over shop restocking, growth simulation and sale
acceptance? `AuthorityController` should reclaim plants and unheld stock on `OnBecomeWorldMaster`,
but `SceneNetworking.ReassignNullObjectsAuthority` only ever sweeps *scene* objects and every crop is
now spawned — so `AuthorityController` is the only thing doing it. Also the standing suspect for
**#40** (scoreboard desync after master transfer).

### Known open problems

- **#40** — scoreboard desync after master client transfer. Untested since the split.
- **Growth pivots.** Everything scales about its mesh centre, so a pumpkin inflates through its vine
  and a carrot grows out of the soil as much as into it. The fix is an empty `ScalePivot` parent
  between root and mesh, with the mesh offset so the stem or soil line sits at the pivot's origin,
  and `_bodyToScale` pointed at the pivot. No code change — `_bodyToScale` is just a transform.
- **`MultiHarvestPlant` has no ending** (#43). With plants ungrabbable there is no uproot gesture at
  all, so an apple tree holds its plot until its owner's lease expires. Needs a real answer — a tool,
  a hold-to-remove on the plot, or a lifespan — before a crop uses it.
- **`WorldBridge` refactor**, agreed and deferred: one seam over the SDK so churn touches one file
  rather than every script *and every prefab*. `docs/world-bridge.md`. Blocked on nothing now except
  the tests above; start with spawn/despawn/take-authority, which are pure de-duplication.
- **#44**, **#47** are seed ideas. `PlantableEntity` — planting a sword to grow an auto-harvester — is
  a future idea, not a plan; the architecture already allows it, since what grows is a prefab
  reference on the thing being planted.

**Closed by the split, do not re-diagnose:** plants landing in the wrong slot (the master now spawns
the plant *at* the plot it chose, so there is no trigger to disagree about); the pumpkin's oversized
vine seed and its collider offset; `unified_pumpkin` missing `NetworkGrabbable`; shop return targets
surviving a planting.
