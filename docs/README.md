---
aliases:
  - "Project Garden"
tags:
  - index
---
# Project Garden

A "Grow A Garden" world for **Somnium Space VR**. Multiplayer VR: players buy seeds from buy points, plant them,
wait for them to grow, then carry the produce to a sell point for **Thatch**.

**The core loop works in-world, on both crop shapes** (confirmed 2026-09-03, on Fusion 2.0). A rooted crop is
bought, planted, grows, becomes produce, is carried and sold. A bearing crop grows a vine, bears into a socket,
ripens, is harvested, withers, and frees its plot to be replanted. The Fusion 2.1 build was tested in-world
on 2026-10-31 and works.

## Documents

Every doc is tagged by the kinds of thing it defines; `Docs.base` lists them by kind. Read [[terminology]] first
(several words used to mean something else), then [[principles]], then the planning docs: [[todolist]] and
[[roadmap]].

- **Rules:** [[principles]], [[layers]], [[conventions]].
- **Crops:** [[crops]] (Seed, Plant, Produce, definitions, balance), [[weed]].
- **Mechanics:** [[shop]], [[economy]], [[plot-ownership]], [[flight]].
- **Interaction:** [[entity-components]], [[tutorial]].
- **Game:** [[procgen-islands]].
- **Tech:** [[networking]], [[community-modules]], [[runtime-spawn]], [[world-bridge]], [[testing]], [[tooling]].
- **Reference:** [[terminology]], [[packaging]], [[fractured-isles-tutorial-script]], [[tutorial-transcript-mapping]].

## Repo

**This repo is not a full Unity project.** It is cloned into a Somnium ProSDK template checkout at
`~/Documents/project-garden` (since 2026-09-11; the old `~/Documents/Somnium ProSDK V3` is stale), and `.gitignore`
is an allowlist: it ignores everything (`/*`) and un-ignores only what is ours.

```
Assets/#User/GrowAGarden/                             scene, prefabs, materials, sounds, islands
Assets/#User/Community Modules/Scripts/GrowAGarden/   all our code (see community-modules)
Assets/#User/Community Modules/Scripts/Vulps.GrowAGarden.asmdef
docs/  Audacity/  CLAUDE.md  README.md
```

Everything else (`ProjectSettings/`, `Packages/`, `Assets/Photon/`, `Assets/SomniumSpace/`, the rest of Community
Modules, the art packs in [[packaging]]) is template-supplied and intentionally untracked. **A new top-level folder
or root file is silently ignored** until `.gitignore` gets a matching `!` rule; `git check-ignore -v <path>` says
why a file isn't showing in `git status`.

Unity **6000.3.5f2**, URP 17.3.0, XR Interaction Toolkit 3.3.1, Photon Fusion 2.1.3, Community Modules 4.0.
The scene is `Assets/#User/GrowAGarden/GrowAGardenScene.unity`. Editor-only Linux workarounds: [[tooling#Somnilux|Somnilux]].
