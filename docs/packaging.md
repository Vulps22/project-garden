# Packaging — GAG as a drop-in

Status: **note only, nothing built.** Written 2026-09-11 on `refactor-to-community-modules`, while
the manager-layer refactor was being scoped.

The goal: `Assets/#User/GrowAGarden/` should drop into a fresh ProSDK checkout and slot in with
minimal work. Two things stand between here and there, and only the first one is the obvious one.

---

## 1. The README manifest

**Make `README.md` list every asset-store package the scene actually uses**, so a reinstall can
re-acquire them from the store and the repo can stop carrying copies of the art.

Record per package: exact store name, publisher, store URL, **version imported**, and what in GAG
depends on it.

**Version is the load-bearing field, and it is easy to leave out.** Unity resolves a prefab's
reference to a mesh or material by the GUID held in *the dependency's* `.meta`. Asset Store
`.unitypackage` files ship their `.meta` files, so re-importing the *same version* restores the same
GUIDs and GAG's prefabs re-link silently. A different version — or a manual copy that lets Unity
regenerate metas — produces new GUIDs, and every reference in GAG becomes a Missing. So: pin the
version, and always re-import from the package, never by copying loose files in.

Today's `README.md` is 208 bytes. It names ProSDK v2.1 and Photon 2.0.5 and lists no art at all.
`.gitignore` already allowlists it, so it is tracked and ready to be filled in.

## 2. What the vendored art actually costs

Measured 2026-09-11:

| tree | tracked files | on disk | assets GAG references |
|---|---|---|---|
| `Assets/#User/GrowAGarden` | 452 | 9.7 MB | — |
| `Assets/Cartoon_Farm_Crops` | 125 | 9.5 MB | **10** |
| `Assets/GVOZDY` (Garden Wooden Fence) | 30 | 16 MB | **2** |

25.5 MB and 155 files carried for **12 assets that anything actually uses** — roughly 72% of the
repo's tracked asset bytes, for art that is one store download away.

**Licence is the second reason and may be the stronger one.** The Asset Store EULA generally permits
redistribution *as part of a project* but not as standalone art, so a public repo containing the raw
packages may already be outside it. Worth checking before deciding, because it argues for removing
them rather than trimming them.

Two ways to do it, and the choice should be deliberate:

- **(a) Remove both trees entirely**, README manifest restores them. Smallest repo, licence-clean.
  Costs store access and the exact version at reinstall time, and is unrecoverable if a package is
  ever delisted.
- **(b) Keep only the 12 referenced assets.** Stays self-contained, but it forks the package, loses
  the "just reimport it" story, and is the option the licence is least likely to allow.

Leaning **(a)** — with a private snapshot of each `.unitypackage` kept somewhere outside the repo, so
a delisting is survivable without putting the art back in git.

## 3. GAG is not self-contained yet — CM points *into* it

This is the one that actually blocks the drop-in, and it was not visible until the GUIDs were chased.

Three scripts live under GAG but carry Community Modules' original `.meta` GUIDs, and **CM's own
assets still reference them**:

| script, in GAG | referenced by, in CM |
|---|---|
| `scripts/Networked Components/NetworkEnableObject.cs` | `Prefabs/Networking/…/Networked Script Enable Object Example.prefab`, Networking `_Plugin Descriptor.asset` |
| `scripts/Networked Components/NetworkObjectsSwitcher.cs` | Networking `_Plugin Descriptor.asset` |
| `scripts/Interactions Plugin/PullForce.cs` | Interactions `_Plugin Descriptor.asset` |

CM V3 does not contain these files. They were **relocated** into GAG with their `.meta` intact, so
the dependency now runs **CM → GAG**, which is exactly backwards for something meant to be droppable.
Drop GAG into a fresh SDK checkout today and CM's example prefab comes up with a missing script;
remove GAG from this one and the same thing happens here.

This is not a duplicate-GUID collision — the invariant from CLAUDE.md still holds, 0 duplicates under
`Assets/#User/`. It is a single copy that moved and left its referrers behind. The fix is to decide
which side owns each file and repair the other side's references, not to re-roll GUIDs.

## 4. Dead weight — candidates, not decisions

Seven scripts across four folders are referenced by **nothing**: not the scene, not any prefab
anywhere under `Assets/`, not any other `.cs`.

- `Interactions Examples/SomniumTriggerExample.cs`
- `ARS Additional/AudioSourceUI.cs`
- `Interactions Plugin/PlayerPresenceTrigger.cs`, `StepManager.cs`
- `Interactions Plugin/PullForce.cs` — except the CM descriptor in §3
- `Networked Components/NetworkEnableObject.cs`, `NetworkObjectsSwitcher.cs` — except the CM
  references in §3

Plus `scripts/#Scripts Backup/`: 484 KB of dead `.txt` snapshots from 2026-02-23, which CLAUDE.md
already describes as "ignore it; never edit it".

**Verify each one before deleting.** The scan matched GUID references and identifier text; it would
not catch an `AddComponent` by string or anything wired up only at runtime. The "decorative and
unreachable" assumption has been wrong in this project before, and cost a floor players fell through.

**Bonus for the messaging refactor:** `NetworkEnableObject` and `NetworkObjectsSwitcher` are the only
users of `NetworkBridge.SyncByteArray`, which has **no `NetworkBridgeDataX` equivalent** — the new
classes expose `NetworkArray<int>` only. If those two scripts are dead, that migration problem is
deleted rather than solved.

---

## Not in scope

Whether to de-vendor Community Modules. That is a separate decision that sits on top of a finished
manager layer; see [`world-bridge.md`](world-bridge.md).
