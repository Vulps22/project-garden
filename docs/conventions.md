---
aliases:
  - "Conventions"
tags:
  - rules
---
# Conventions

How code, commits and these docs are written.

## Code

- **Namespaces follow folders.** Our code is `Assets/#User/Community Modules/Scripts/GrowAGarden/`. A script at its
  root is `Vulps.GrowAGarden`; a script in a sub-folder is `Vulps.GrowAGarden.<Folder>` (`Crops`, `Economy`,
  `Flight`, `Interaction`, `Modules`, `Motion`, `Orchestration`, `Ownership`, `Seeds`, `Settings`, `Tutorial`).
  Folders are PascalCase. **Never name a folder after a type in it or after a Unity type**: a namespace
  `Vulps.GrowAGarden.Produce` would hide the class `Produce` from every caller, and `Vulps.GrowAGarden.Physics`
  would hide `UnityEngine.Physics`. That is why produce lives in `Crops/` and the physics components in `Motion/`.
  Why our code sits inside Community Modules at all: [[community-modules]].
- **Logging:** always the project's `Logger` (`Log`/`Info`/`Warn`/`Error`), never `Debug.Log` directly. Messages
  lead with `Method() 'objectName' — ` context; the log histogram in [[testing#Reading the client log|testing]]
  depends on that shape.
- **Events:** an event we declare is named for **the fact** (`Sold`, `Harvested`, `PurchaseRequested`,
  `PlayerGone`) and a handler is `On` + that name, so it reads `Seed.PurchaseRequested += OnPurchaseRequested`. The
  `On`-prefixed *events* in the tree (`NetworkBridgeEvents.OnSpawned`, `SceneNetworking.OnOtherPlayerJoined`,
  `OnBecomeWorldMaster`) are Community Modules' or the SDK's, not ours. `EconomyManager.OnPlayerBalanceChanged` is
  the one event of ours out of step and should become `PlayerBalanceChanged`.
- **Fields** are `_camelCase` private + `[SerializeField]`; wire references in the Inspector, and use
  `OnValidate()` to auto-populate same-GameObject components.
- **Comments: one line per function signature.** Say what the function does. If it needs more than a line it has
  stopped being a "what does this do" comment and become history, justification, or a note to self. Never what
  changed, what it used to do, why the refactor happened, what a decision was weighed against, or why the code is
  arranged as it is. A line that would survive being moved into a commit message belongs there.
- **Wire ids** (`...MessageType : byte` enums) are appended, never inserted. See [[networking#Messaging|networking → Messaging]].

## Git

- **Commits:** `#<issue> <imperative description>`, e.g. `#42 Add null socket guard in BearingPlant.Awake`. Issue
  number first, present tense. Work without an issue drops the number and reads as a sentence about intent
  (`Let the holder ask to buy and the master decide`).
- **Branches:** `feature/<issue>-<slug>` or `feature/<slug>`, `wip/<slug>` for exploratory work.
- **One phase per upload.** An upload is a slow manual round trip, so unrelated changes are not stacked into one.
  If a fix is needed mid-phase, `git stash -u` the WIP, make the fix, wait for the test, then pop.

## These docs

- **`docs/` is an Obsidian vault.** One topic, one doc; text is moved, never copied. Anywhere else that needs it
  links there in a sentence at most. Links point from the specific to the general.
- Link as `[[crops]]`, a section as `[[networking#Messaging|networking → Messaging]]` (Obsidian drops `:` from
  heading anchors), a bullet by block id (`^one-owner`). **Inside a table, escape the alias pipe**
  (`[[crops#Balance\|balance]]`).
- **A doc holds today's state and the refined plan.** Each section carries its own status line,
  `**Status (date):** proposed / refined / built, untested in-world / tested`. Status is never doc-wide.
- Unsettled points are callouts: `> [!question] Open`, `> [!info] Proposed`, `> [!warning] Pinned / Untested /
  Stale`, `> [!info] Networking`.
- A new doc gets frontmatter `aliases` (its title) and `tags`, every kind it defines behaviour for, from `crop`,
  `mechanic`, `interaction`, `game`, `tech`, `rules`, `arc`, `tracking`, `reference`, `index`. `Docs.base` has a
  view per kind.
- To-do items are `- [ ]` in [[todolist]], one line each, pointing at their doc.
- **A step doc starts as a design and becomes the record of what was built.** [[runtime-spawn]] is that whole arc.
  Do not create one until the step has real design in it.
