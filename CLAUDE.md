# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

Project Garden — a "Grow A Garden" clone for **Somnium Space VR**. Multiplayer VR: players buy seeds
from shop points, plant them in slots, wait for them to grow, then carry the grown plant to a sell
point for currency (**Thatch**).

- Unity **6000.3.5f2**, URP 17.3.0, XR Interaction Toolkit 3.3.1
- Networking: **Photon Fusion** (via the Somnium `NetworkBridge` wrapper), Photon 2.0.5 + addons
- Requires **Somnium ProSDK v2.1**

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

### The untracked dependency you must know about

`Assets/#User/Community Modules/` is **not in this repo** but the game code depends on it. It supplies
the `SomniumSpace.Network` assembly — `NetworkBridge`, the RPC transport everything is built on. If
`NetworkBridge` fails to resolve, the template is missing Community Modules, not a code bug.

**Never vendor Community Modules into this project.** Under the V2 ProSDK the accepted workflow was to
copy Community Modules into your own scripts folder and fold them into your assembly. That is not how V3
works: add `CommunityModules` to `GrowAGarden.asmdef`'s `references` and use the types directly. A copy
under `Assets/#User/GrowAGarden/` is by definition a stale V2 snapshot — **if a script exists in V3's
`Community Modules/`, the Community Modules copy is the newer one.**

That old workflow left 31 vendored duplicates here, and copying the `.cs` also copied its `.meta`, so
each shared a GUID with its original. Unity papered over the 39 collisions by assigning fresh GUIDs in
`Library/` without rewriting the `.meta` files — meaning bindings were unreproducible and would re-roll
on any `Library` wipe or fresh clone. One had already broken: the scene's `SceneNetworking` pointed at a
GUID that resolved to nothing, so `GrowAGarden.SceneNetworking` was never driven and `IsMasterClient` /
`IsNetworkReady` were permanently false — silently disabling shop spawning, economy mutation, late-join
sync and balance rebroadcast.

Cleaned up 2026-08-25: 29 unreferenced V2 copies parked in `_parked/v2-community-modules/`, the game
repointed at `CommunityModules.SceneNetworking`, and every remaining GUID regenerated. **Duplicate GUIDs
are now 0** — keep it that way. Only `scripts/Networking Plugin/Utils/{BytesReader,BytesWriter}.cs`
remain as deliberate local forks (they back every RPC payload; swapping them for CM's is a wire-format
change, so they were left alone and only their GUIDs regenerated).

Game logic lives in `scripts/scripts/` and `scripts/Seeds/`. A handful of GAG-only scripts with no CM
counterpart also live under `scripts/{Interactions Plugin,ARS Additional,Networked Components,
Interactions Examples}/`.

`scripts/#Scripts Backup/` is dead `.txt` snapshots from 2026-02-23. Ignore it; never edit it.

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

### Step 2: compile-checking from the CLI

The `unity` CLI talks to a *running* Editor over the `com.unity.pipeline` package (installed
2026-08-25 into the untracked template-supplied `Packages/manifest.json`, so it never dirties this repo).

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

Two CLI gotchas: command args are **positional**, not `k=v` (`unity command eval 'return 2+2;'`), and
`data.result` is sometimes a nested object but sometimes a **JSON-encoded string** needing a second
parse — handle both when scripting against `--json`.

`unity test` and `unity build` spawn their *own* batch-mode Editor, which contends with the open Editor
for the project lock — prefer the `unity command` bridge while the GUI Editor is running. There are no
test assemblies under `Assets/#User/` anyway, so `unity test` would run zero tests.

### Step 3: when the upload itself misbehaves

The ProSDK ships as precompiled DLLs, so its HTTP flow is opaque. **NetLog** Harmony-patches every managed
HTTP stack the SDK uses and surfaces requests/responses in an Editor window — built to debug export and
login failures from the inside. It is Editor-only (`includePlatforms:["Editor"]`), so it never reaches an
exported world.

It is currently **parked in `_parked/` (outside `Assets/`, git-ignored), not deleted** — see
`_parked/README.md` for the one-line restore. Restore it when an upload misbehaves; leave it parked
otherwise, because its bundled `0Harmony.dll` (Harmony 2.3.3, which ILMerges MonoMod.Core + Cecil) trips a
Burst bug: `EntryPointMethodFinder` hashes every assembly's references, including Editor-only ones, and
its metadata reader throws `BadImageFormatException: Read out of bounds` on that merged DLL, spamming
"Failed to find entry-points" on **every domain reload**. Harmless — the DLL is valid, Harmony works, and
exported worlds were never affected — but noisy. If you restore NetLog and want it quiet, swap in the
non-merged Harmony 2.2.2 build. Don't re-diagnose this from scratch.

### Step 5: what to look for in-world

The in-world signals come from the probes under "Debug utilities in the scene" below — chiefly
`ExceptionAlarm` (the sun goes out on any `GrowAGarden` exception) and `Debug_MasterClientIndicator`.

Multi-client behaviour — master-client authority, RPC state sync, late-join — is only observable here,
with several real clients connected. A single client tells you nothing about any of it.

## Architecture

### Authority model

Master-client authoritative, with two distinct notions of authority — do not conflate them:

- `SceneNetworking.IsMasterClient` — scene-wide owner. Gates economy mutation and shop spawning.
- `networkBridge.Object.HasStateAuthority` — **per-NetworkObject** Fusion authority. Gates growth
  simulation and state broadcast for one plant. Authority migrates (see `RequestAuthorityAndReturn`).

A single plant is simulated only by its state authority; every other client is a passive proxy applying
RPC state.

### Networking: hand-rolled byte packing

All sync goes through `NetworkBridge` RPCs carrying `(byte messageId, byte[] data)`, serialized manually
with `BytesWriter`/`BytesReader`. There is no Fusion `[Networked]` property anywhere.

- Each component defines its **own private `enum ...MessageType : byte`** (`PlantMessageType`,
  `PlantSlotMessageType`, `PoolMessageType`). IDs are scoped per NetworkBridge, so `0` in one component
  is unrelated to `0` in another.
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
`BroadcastNextFrame`). This is the mechanism behind open issue #40.

### `PlantSeed` — the core template-method class

`PlantSeed` (abstract) drives the seed → planted → grown → sold lifecycle. Subclasses override hooks
rather than reimplementing the loop:

| Hook | Purpose |
|---|---|
| `UpdateVisuals(bool isSeed)` | **abstract** — swap models/colliders for seed vs plant |
| `OnGrowthUpdated(completion, targetScale)` | per-frame scale; default scales the root transform |
| `OnFullyGrown()` | phase complete; default enables grab + broadcasts |
| `OnWillUpdate()` | runs on **all** clients, before the authority-only guard |
| `On{Write,Read}BroadcastState` + `GetExtraBroadcastStateSize` | extend the state payload |

Two implementations: `RootedPlantSeed` (simple show/hide, e.g. carrot, turnip) and `ViningPlantSeed`
(multi-phase vine → fruit → decay, e.g. pumpkin; anchors the vine in world space via `LateUpdate` while
the fruit root moves freely).

Growth is **timestamp-derived, not accumulated**: `GetGrowthCompletion()` compares
`DateTimeOffset.UtcNow` against `_plantedTimestamp` and the current phase's `duration`. This is why
syncing a timestamp is sufficient to sync growth, and why phase transitions reset `_plantedTimestamp`.

`PlantSeed` implements `IXRSelectFilter` — `Process()` runs *before* a grab commits and is where both
steal-prevention (`_grabber` already set) and affordability checks live.

### Seed definitions are MonoBehaviours, not ScriptableObjects

`SeedDefinition` is an abstract `MonoBehaviour` whose concrete subclasses (`CarrotSeed`, `TurnipSeed`,
`PumpkinSeed`) **hardcode** id/prices/phases in `Init()`, called from both `OnEnable` and `OnValidate`.
Balance changes are code changes, not Inspector edits. To add a crop: new `SeedDefinition` subclass +
a `PlantSeed` subclass choice + prefab + a `UnifiedPool` under `PoolManager` + a `ShopSlot`.

### Pooling — nothing is instantiated at runtime

Fusion NetworkObjects are pre-placed in the scene and recycled. `PoolManager` (singleton) maps
`seedId → UnifiedPool`; pools `Claim()` and `Return()` existing `PlantSeed` instances. `Restore()`
requires state authority, so pool returns may need `RequestAuthorityAndReturn` (20s timeout), and
`Awake`-time restores that fail are retried each frame from `_pendingRestore`.

An exhausted pool returns `null` — callers must handle it; empty shops are the symptom.

### Economy

`EconomyManager` (singleton) holds `Dictionary<playerId, PlayerBalance>` and is the only writer.
Mutations are master-only; every change re-broadcasts the **entire** balance table to all clients, which
rebuild their dictionary from scratch and refresh `BalanceDisplayManager` (a sorted scoreboard).
`OnPlayerBalanceChanged` fires locally afterward.

Note `RemoveBalance` deliberately applies locally on non-master clients before the broadcast arrives
(optimistic deduction, from #39) — `AddBalance` does not.

## Conventions

- **Commits:** `#<issue> <imperative description>` — e.g. `#42 Add null SeedId guard in PoolManager.Awake`.
  Issue number first, present tense.
- **Branches:** `feature/<issue>-<slug>` (e.g. `feature/46-dependency-inversion`), `wip/<slug>` for
  exploratory work.
- **Logging:** always the project's `GrowAGarden.Logger` (`Log`/`Info`/`Warn`/`Error`), never `Debug.Log`
  directly. Messages conventionally lead with `Method() 'objectName' — ` context.
- Fields are `_camelCase` private + `[SerializeField]`; wire references in the Inspector, and use
  `OnValidate()` to auto-populate same-GameObject components.

## Debug utilities in the scene

- `ExceptionAlarm` — **turns off the sun** when any exception with `GrowAGarden` in its stack trace is
  logged. A pitch-black scene in VR means an exception, not a lighting bug.
- `Debug_EconomyBoost` — a grabbable that grants 10,000,000 Thatch once. Grabbing it also flips
  `PumpkinSeed` into `debug` mode, collapsing growth durations from 360s to 10s.
- `Debug_MasterClientIndicator` — colour-codes master-client state: green = local client is master,
  red = not, grey = not joined yet, blue = offline. Written to answer whether a non-player holds master
  client (it does not — it is the first joiner). Keep it in the scene until that is re-confirmed across
  several real clients.

## Current state

`main` is at the post-MVP pumpkin/vining work (tags: `MVP`, `add-turnip`).
`feature/46-dependency-inversion` is 4 commits ahead and unmerged — it makes `PlantSeed` lifecycle flags
read-only to external callers and adds action methods; the public mutable flags (`IsSeed`, `InShop`,
`IsBought`, `IsInPool`) described above are what `main` still has. Other `feature/*` branches are fully
merged and stale.

Open issues worth knowing: **#40** (bug — scoreboard desync after master client transfer), **#45**
(extract grab filtering out of `PlantSeed`), **#43** (`RegrowablePlantSeed`; `RegrowableFruit.cs` is an
empty stub reserved for it).
