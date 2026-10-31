---
aliases:
  - "Testing"
  - "The loop"
tags:
  - tech
  - rules
---
# Testing

How a change is checked, and what counts as checked. The tools behind each step are in [[tooling]].

## The five steps

**Status:** the rule.

There is no test suite and no lint step. **Nothing is truly verified until it is uploaded and played in-world.**
This is a Somnium Space world, not a standalone game, so VR interaction, multiplayer, and the Somnium runtime around
it only exist once the world is deployed and joined from the Somnium client. Editor Play mode has no Somnium game
behind it.

1. **Make the change.**
2. **Check for errors** via the `unity` CLI against the running Editor ([[tooling#Compile-checking from the CLI|tooling]]). This only proves it *compiles*. Never describe a change as working on the strength of step 2.
3. **Deploy:** export and upload through the ProSDK menu. Not scriptable; a GUI action.
4. **Connect and test:** join the uploaded world from the Somnium client, in VR.
5. **Check the logs** for exceptions and data.

Steps 3–5 are Vulps's to drive. When only steps 1–2 are done, say so plainly: "compiles clean, untested in-world".

**One phase per upload** ([[conventions#Git|conventions → Git]]). Multi-client behaviour is only observable with
several real clients connected; a single client tells you nothing about any of it.

## Reading the client log

**Status:** the procedure.

Nothing in the Unity Editor console sees in-world behaviour. The client log is:

```
/mnt/games/somnium-space-vr/drive_c/users/steamuser/AppData/LocalLow/Somnium Space Ltd/Somnium Space VR/Player.log
```

(`Player-prev.log` is the previous session; the `drive_c` path is because Somnium runs under Proton.)

> [!warning] Find the session banner first. Never read the log without it.
> `Logger.BeginSession()` prints one line from `WorldManager.Awake()` at the start of every run:
> `[GrowAGarden] Logging Session Started 2026-09-05 00:31:07`.
> The client log is **append-only across runs**, so a histogram or a `grep -c` over the whole file counts every
> session it remembers, including the build before the fix being judged. Read only from the last banner:
> ```bash
> START=$(grep -n "Logging Session Started" "$L" | tail -1 | cut -d: -f1)
> tail -n +"$START" "$L" | grep -c "ReturnToSocket"
> ```
> **No banner means the run predates it, or the world never loaded**: say which, and do not analyse it anyway.
> Check the banner's timestamp against when Vulps actually tested. Quoting an older session's numbers as if they
> described the current build has happened twice, once while reporting a bug as fixed that had never been re-run.

**Check its size before reading it.** A per-frame throw took it to 647 MB / 11.6 M lines in one session. Use
`grep -n -m` and line-ranged `sed`, never a whole read. `grep -c` on a 600 MB file is fine; `head`/`tail` are
instant.

- `grep -n -m 15 "GrowAGarden" "$L"`: where the world assembly loads and the first project exception.
- `grep -o "[A-Za-z]*() '[^']*' — [^;]*" "$L" | sort | uniq -c`: turns the logging convention
  ([[conventions#Code|conventions → Code]]) into a histogram of what actually went wrong.

**A session with a per-frame throw cannot be used to judge feel.** An exception plus a log write per object per
frame destroys frame time; anything "tested" in such a session has to be re-tested.

## Debug aids

**Status (2026-09-13):** `ExceptionAlarm` is in the scene; `ReportPlacementOnce` is temporary.

- **`ExceptionAlarm`** turns off the sun on any exception with `GrowAGarden` in its stack trace. A pitch-black world
  reads as a crash but *is the alarm firing*: that is the signal, not a lighting bug.
- **Master-client probe:** `BalanceDisplayManager.Set`'s `Logger.Log($"...IsMasterClient={...}")`, read from the
  client log. No side effects.
- **`Produce.ReportPlacementOnce()`** is temporary instrumentation: one line per produce, a second after it is born,
  saying where it actually is and what it is worth. It exists because the logs used to report the position we
  *asked for* rather than the one the object got, and that cost two uploads. Delete it once the 2.1 build is
  tested ([[todolist]]).
- `Debug_EconomyBoost` and `Debug_MasterClientIndicator` were **deleted**. The indicator forced scene ambient light
  via `SomniumBridge.Environment` and was itself the cause of an unrelated "black sky" report. Removing the booster
  left a dangling `Debug_EconomyBoost.Instance` dereference in `PumpkinSeed.Update()` that threw every frame on
  every pumpkin: the log-size note above.
