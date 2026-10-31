# Project Garden

A "Grow A Garden" world for **Somnium Space VR**: buy seeds, plant them, grow them, carry the produce to a sell
point for Thatch. Multiplayer, master-client authoritative, on Photon Fusion 2.1.3 and Community Modules 4.0.

**Somnium Space supports neither its SDK nor its client on Linux, and this machine is Nobara.** Making it work
anyway is the point of the exercise, so every Linux-only defect is in an untested upstream code path. Local
workarounds are the deliverable; never propose filing upstream bugs, waiting for a fix, or switching to Windows.

## Where things are

- **`docs/` is an Obsidian vault and the source of truth.** Start at `docs/README.md` (every doc, grouped by kind),
  then `docs/terminology.md` and `docs/principles.md`. What is left to do: `docs/todolist.md`; milestones:
  `docs/roadmap.md`.
- **Read the doc for a topic before touching it**, and update it in the same change. One topic, one doc; text is
  moved, never copied; each section carries its own `**Status (date):**` line. How the docs are written:
  `docs/conventions.md` → These docs.
- Code: `Assets/#User/Community Modules/Scripts/GrowAGarden/`, namespaces `Vulps.GrowAGarden.<Folder>`, in Community
  Modules' asmdef renamed `Vulps.GrowAGarden` (`docs/community-modules.md`). Scene, prefabs and art:
  `Assets/#User/GrowAGarden/`.
- **`.gitignore` is an allowlist.** Only our folders are tracked; a new top-level folder or file is silently ignored
  until it gets a `!` rule (`docs/README.md` → Repo).
- Somnium's own rules for agents: `WorldCreatorContext.md` in the project root (v4.0). It outranks these docs about
  the platform. Fusion 2.1 migration guide: `Fusion_2.1_Migration_Guide.md`.

## Rules that apply to every change

- **Networking is decided before building.** Who owns a thing's state, how it reaches other players and late
  joiners, and what a non-master's hand does to it (`docs/principles.md`, `docs/networking.md`).
- **The layers** (`docs/layers.md`): components → managers → bridges → anything not ours. No skipping a layer, no
  calls sideways.
- **Community Modules:** move scripts with their `.meta`, never duplicate them; never modify `SceneNetworking`,
  `NetworkSpawn` or the network stubs; never delete a CM file without asking.
- **Code style** (`docs/conventions.md`): `Logger`, never `Debug.Log`; one-line comments; wire ids appended, never
  inserted; no folder named after a type in it or after a Unity type.

## Checking a change

The loop is in `docs/testing.md`; the tools in `docs/tooling.md`.

- **Nothing is verified until it is uploaded and played in-world.** A clean compile is step 2 of 5. Say "compiles
  clean, untested in-world" when that is all that is true.
- Compile-check with the `unity` CLI against the running Editor. **Never recompile during an upload**, and never
  enter Play mode or tick a running scene: Vulps drives Play mode.
- **Read the client log only from the last session banner**, and check its size first (`docs/testing.md` →
  Reading the client log).
- **One phase per upload.**

## Current state

**Fusion 2.1 / Community Modules 4.0 migration done 2026-10-31**, compiles clean, untested in-world. Branch
`refactor-to-community-modules`, PR #48. What is left before it can be uploaded is the first section of
`docs/todolist.md`.

**Late join, a player leaving and the master leaving were tested with real clients on Fusion 2.0 and work.** If a
multiplayer path looks unverified, a doc is probably stale; ask before asserting it.
