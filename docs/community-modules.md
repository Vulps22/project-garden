---
aliases:
  - "Community Modules"
tags:
  - tech
  - rules
---
# Community Modules

Where our code lives and why. Community Modules (CM) is the community package of scripts, example scenes and the
Somnium network stubs, installed at `Assets/#User/Community Modules/`. Since 4.0 the world's own code lives
**inside** it and shares its assembly.

## Layout

**Status (2026-10-31):** built, compiles clean; untested in-world.

```
Assets/#User/Community Modules/
  Scripts/
    Vulps.GrowAGarden.asmdef      CM's asmdef, renamed: the world's one assembly
    GrowAGarden/                  all our code, namespace Vulps.GrowAGarden.*
    Networking Plugin/ ...        CM's own scripts, also namespace Vulps.GrowAGarden
  Plugins/SomniumSpace/Networking/  the network stubs, asmdef SomniumSpace.Network
Assets/#User/GrowAGarden/         scene, prefabs, materials, sounds: everything but code
```

- **One assembly, `Vulps.GrowAGarden`.** The Namespace Helper renamed CM's asmdef and the namespace of every script
  under it, CM's and ours. Assembly and namespace must both be unique across every uploaded Somnium world, and
  every public type must sit in the root namespace or below it. Our sub-namespaces follow folders
  ([[conventions#Code|conventions → Code]]).
- **Git tracks only our part of CM**: `Scripts/GrowAGarden/` and `Scripts/Vulps.GrowAGarden.asmdef`. The rest of CM
  is template-supplied, like `Packages/` and `Assets/Photon/`, and a fresh clone needs CM 4.0 installed first.
  The Namespace Helper's rename of CM's own scripts is therefore not in git either; rerun it after reinstalling CM.
- **CM's files are edited in place, never deleted** without asking Vulps.
- **`SceneNetworking`, `NetworkSpawn` and the network stubs are never modified**, apart from the namespace line the
  Namespace Helper sets. The stubs have the same assembly, namespace and class names as the components inside the
  Somnium client, which swaps its own in; any difference breaks the match.
- **Scripts are moved with their `.meta`, never duplicated**, so every scene and prefab reference survives.
- The world's rules for agents, from Somnium: `WorldCreatorContext.md` in the project root (v4.0, 2026-10-20). It
  outranks anything here about the platform.

## Why not a separate assembly

**Status (2026-10-31):** settled.

Until CM 4.0 our code was its own asmdef, `GrowAGarden`, and CM was a separate assembly. The Somnium uploader
validates asmdef references against a whitelist and **rejects `CommunityModules`** ("Unsupported Scripting
References Detected"), and it compiles sources server-side, skipping anything off the list, so a `using` of it
fails `CS0246` and the world silently never publishes while the HTTP upload still returns 200. The workaround was
to vendor CM files into our assembly one at a time with fresh GUIDs, which produced 39 duplicate-GUID collisions
(cleaned up 2026-08-25) and copies that drifted from upstream.

Making CM's assembly *be* ours removes the reference altogether. SpaceScape (`~/Documents/project-spacescape-somnium`)
was the worked example.

## Migration to 4.0 and Fusion 2.1

**Status (2026-10-31):** done, compiles clean; untested in-world. What it changed for networking:
[[networking#Fusion 2.1|networking → Fusion 2.1]].

Somnium's networking update of 2026-10-20 broke every networked world. The migration ran from the command line
with Somnium's tool (`Somnium Fusion 2.1 Migration Tool V1.0.unitypackage`, guide in
`Fusion_2.1_Migration_Guide.md`, both in the project root):

1. Backup commit `ebeaca2`, and a full copy at `~/Documents/project-garden-backup-2026-10-31-pre-fusion21`.
2. `MigrationAutomation.RunUnattended -migrationTask Full` in batch mode: Fusion 2.1.3 and CM 4.0 in, every
   `NetworkRigidbody3D` replaced. Exit 3, every step done, compile errors only in our vendored CM copies.
3. Our code moved into `Scripts/GrowAGarden/`; the vendored `SceneNetworking`, `NetworkGrabbable`, `BytesReader` and
   `BytesWriter` deleted and the scene and prefabs re-pointed at CM 4.0's GUIDs. `BytesReader`/`BytesWriter` were
   identical to 4.0, so the wire format did not change. 4.0's `SceneNetworking` guards its own `PrefabUtility` call,
   which our copy had swapped out.
4. Namespace Helper → `Vulps.GrowAGarden`; sub-namespaces per folder.
5. The tool moved out of `Assets/` to `~/Documents/project-garden-migration-leftovers/`. `MigrationBackups/` in the
   project root holds its per-step zips.

> [!warning] Left over
> - `Community Modules/Prefabs/Networking/Networking Components Examples/Networked Script Enable Object Example.prefab`
>   has a missing script. The guide calls it safe to delete; it is CM's, so that is Vulps's call.
> - The assembly name changed: re-run the SDK's assembly-name availability check before the next upload, and accept
>   the SDK's own update if it offers one.
