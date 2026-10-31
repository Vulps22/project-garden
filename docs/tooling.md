---
aliases:
  - "Tooling"
  - "Somnilux"
tags:
  - tech
  - reference
---
# Tooling

The Editor bridge, the Linux workarounds and the upload. Why each exists: Somnium supports neither its SDK nor its
client on Linux, and this machine is Nobara. **Making it work anyway is the point**, so every Linux-only defect is
in an untested upstream code path and a local workaround is the deliverable, never an upstream bug report, a wait
for a fix, or Windows.

## Compile-checking from the CLI

**Status:** in daily use.

The `unity` CLI talks to a *running* Editor over the `com.unity.pipeline` package (installed into the untracked
`Packages/manifest.json`, so it never dirties the repo). Setup, once per machine: `unity pipeline install`, then
focus the Editor so it resolves the package. `unity status` should show a port and `ready`. If `Server Reachable` is
false in `unity pipeline list`, the Editor hasn't re-resolved the manifest yet: focus it.

```bash
unity command recompile                  # returns immediately; "up_to_date" if nothing changed
unity command recompile_status --json    # poll -> {"status":"completed","failed":bool,"errors":[...]}
unity command console --json             # structured Editor console, with stack traces
unity command clear_console              # so the next read shows only what you caused
```

Also: `eval` / `eval_file` run C# against the live Editor via Roslyn (the practical way to inspect scene state
without VR), `editor_play` / `editor_stop`, `capture_game_view`, `find_gameobjects`, `get_scene_hierarchy`, `menu`.
Full list: `unity command`. **Play mode is Vulps's to drive**: never enter it or tick a running scene.

Gotchas, each of which has cost real time:

- **Unity cannot compile while another window has focus.** The bridge still says `ready` and `recompile_status`
  sits on `triggered` forever. If `triggered` comes back twice in a row, stop polling, finish the rest of the work,
  and report it as "written, not compile-checked". One `AssetDatabase.Refresh()` via `eval` is worth a single try.
- **Never trigger a recompile while an upload is running.** A domain reload mid-build can break the export. Check
  `Logs/network.log` for a recent `confirmUploadCdn` first.
- **A case-only folder rename leaves ghost paths.** Unity keeps the old spelling in its script list
  (`CS2001: Source file '.../economy/...' could not be found`), and neither refresh, reimport nor a round-trip move
  clears it. Restart the Editor (expected to clear it; unconfirmed). To check the code meanwhile, run Unity's Roslyn on the assembly's response file
  (`Library/Bee/artifacts/*.dag/Vulps.GrowAGarden.rsp`) with the ghost lines removed.
- Command args are **positional**, not `k=v` (`unity command eval 'return 2+2;'`), and `data.result` is sometimes a
  nested object and sometimes a **JSON-encoded string** needing a second parse.
- `unity test` and `unity build` spawn their *own* batch-mode Editor, which contends with the open one for the
  project lock. With the Editor closed, batch mode is fine:
  `~/Unity/Hub/Editor/6000.3.5f2/Editor/Unity -batchmode -quit -projectPath "$PWD" -logFile verify.log`, then look
  for `error CS` and `Tundra build success`.

**The Editor leaks memory until it OOMs.** Watch RSS against a ~3.1 GB baseline and suggest a restart at a natural
break. If Unity dies at startup, check `df -h /tmp` first: `/tmp` is a 16 GB tmpfs and a full one kills Hub's
licensing client.

## Somnilux

**Status:** in use. Repo: github.com/Vulps22/somnilux. Backup and REAPPLY.md at `~/Desktop/somnium/somniluxSDK/`.

`Packages/com.somnilux.sdk/` is Vulps's own Editor-only local package holding every Linux workaround for the ProSDK
plus the NetLog HTTP logger. It is untracked here, is *not* part of the game, and never reaches an exported world
(`includePlatforms: ["Editor"]`). Menu: **Somnilux** in the Editor menu bar.

| File | What it works around |
|---|---|
| `BundleCaseFix` / `BundleCaseRestore` | The SDK asks for an UPPERCASE sha256 bundle name; Unity always lowercases. Invisible on case-insensitive filesystems. |
| `BundleCompressionFix` | Forces LZMA or LZ4; see *Uploads* below. |
| `Preview360Fix` | `CRTWaitForUpdate` polls a CustomRenderTexture that was already destroyed, so the 360 preview stays empty and upload aborts. Blits the cubemap to an equirect JPEG instead. |
| `UploadRefreshGuard` | Holds off domain reloads during an upload. |
| `NetLog/` | Harmony-patches every managed HTTP stack the SDK uses and shows requests and responses in an Editor window. Built to debug export and login failures from the inside, since the ProSDK ships as opaque DLLs. |

> [!info] Known noise, don't re-diagnose
> NetLog's bundled `0Harmony.dll` is Harmony 2.3.3, which ILMerges MonoMod.Core + Cecil. Burst's
> `EntryPointMethodFinder` throws `BadImageFormatException: Read out of bounds` on that merged DLL, and "Failed to
> find entry-points" spams every domain reload. Harmless: Harmony works and exported worlds were never affected.
> Swapping in the non-merged Harmony 2.2.2 build is the likely fix.

## Uploads

**Status:** the procedure.

Deploying is the ProSDK's export/upload menu, a GUI action. When it misbehaves, restore and consult NetLog.

**"Bundle file is Empty" is a compression mismatch, not a casing bug.** SDK 3.1.7 (2026-08-26) switched asset
bundles to LZ4; a client built before that cannot unpack them. Casing and cache corruption were both chased first
and both were wrong. Cheap check from `Logs/network.log`: each upload's JSON declares `FileSize` (the zip) and
`BundleFiles[0].FileSize` (the .unity3d). LZMA bundles are dense, so zipping *adds* ~7 KB; LZ4 bundles compress
further, so the zip comes out ~1.3 MB *smaller*. The sign of `bundle - zip` gives the compression without opening
anything. `BundleCompressionFix` is the lever.

**The uploader compiles our sources server-side** against
`imprt.somnium.space/SDK/FilesStorage/ScriptingReferences.txt`. Anything off that list is skipped, so a `using` of
it fails and the world silently never publishes while the HTTP upload still returns 200. Allowed assembly
references are listed in `WorldCreatorContext.md` section 3; see also [[community-modules]].

## Parked material

`_parked/` in the project root, git-ignored, kept rather than deleted: `v2-community-modules/`,
`vendored-SceneNetworking/` (a *stale* copy; read its README before touching it), `stale-bundle-1930/`,
`netlog-round2/`.
