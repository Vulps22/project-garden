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
> `[GrowAGarden] Logging Session Started 2026-11-05 00:31:07 build 1`.
> **The build number is `Logger.BUILD`**, raised by one each time Vulps says a build was uploaded (from 2026-11-05).
> A banner with the old number, or none, means the client is not running the upload being judged.
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

## Frame time benchmark

**Status (2026-11-03):** built, compiles clean, untested in-world. Asked for by Vulps.

`DevTools/FrameTimeLog` (on SceneManager) writes one line a minute to every client's log:

```
Report() 'SceneManager' — PERF 5400 frames: avg=11.2 p95=12.0 p99=16.5 max=40.2 ms, over=3.1% of 11.1 ms @90Hz cpu=8.3 gpu=9.1 | night=no bubble=Town_Capital@2,7 players=3 draw=1200m tripping=no at=(4,1,9)
```

- **Over budget** is the share of frames slower than the headset's frame (5% slack). It is the first number to read:
  in VR the frame rate is locked to the refresh rate, so the average sits at the budget until frames start missing.
  `p95`/`p99` are the slowest 5% and 1%; `max` the worst single frame (load hitches land here).
- **`cpu`/`gpu`** are Unity's own frame times (`FrameTimingManager`), present only if the client has frame timing
  stats on; the first PERF report says whether it does. They show headroom shrinking before any frame is missed.
- **The context after `|`** is what makes two lines comparable: day or night, which Garden's bubble, players in the
  world, draw distance, tripping, where. Compare like with like only.
- **The benchmark:** a change that raises `over`, `p99` or `cpu`/`gpu` in the same context is a regression. Stand in the
  same spot for a few minutes before and after.

Read it from the last session banner:

```bash
START=$(grep -n "Logging Session Started" "$L" | tail -1 | cut -d: -f1)
tail -n +"$START" "$L" | grep -o "PERF .*"
```

## Debug aids

**Status (2026-10-31):** `ExceptionAlarm` is in the scene.

- **`ExceptionAlarm`** turns off the sun on any exception with `GrowAGarden` in its stack trace. A pitch-black world
  reads as a crash but *is the alarm firing*: that is the signal, not a lighting bug.
- **Master-client probe:** `BalanceDisplayManager.Set`'s `Logger.Log($"...IsMasterClient={...}")`, read from the
  client log. No side effects.
- `Debug_EconomyBoost` and `Debug_MasterClientIndicator` were **deleted**. The indicator forced scene ambient light
  via `SomniumBridge.Environment` and was itself the cause of an unrelated "black sky" report. Removing the booster
  left a dangling `Debug_EconomyBoost.Instance` dereference in `PumpkinSeed.Update()` that threw every frame on
  every pumpkin: the log-size note above.
