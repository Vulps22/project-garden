# The tutorial — a narrator, not a UI

Started 2026-09-09, first pass wired 2026-09-10. A recorded voice that plays one line the first
time the player reaches the thing it describes, remembers what it has said between sessions, and
never says it twice. There is no tutorial UI, no arrows, no checklist — a headset is a bad place
to read.

**Entirely local.** No RPCs, no master decisions, nothing replicated: every client hears its own
tutorial on its own schedule, which is why none of the networking rules in `CLAUDE.md` apply to any
of it. Two players in the same world can be at different points in the script and neither is wrong.

Three files, all under `scripts/scripts/Tutorial/`:

| File | Owns |
|---|---|
| `TutorialManager` | the script, the order, the queue, persistence, suppression |
| `TutorialTriggers` | **all** the coupling — listens to the game and says "this just happened" |
| `LocalPlayerMotionProbe` | the one signal the game does not already announce: the player moved |

`TutorialTriggers` exists so the narrator is not a dependency of the economy. Each system announces
a fact named for itself — `DeedClaimZone.DeedCarriedIn`, `SellPoint.SaleCompleted`,
`Produce.AnyLifecycleChanged`, `FlightController.Launched` — and knows nothing about narration.
Deleting the tutorial later means deleting these three files, not editing six unrelated classes.

---

## Pass 1 — the linear script

Ten lines, in order, each gated on the player having reached it.

| # | Id | Clip | What fires it |
|---|---|---|---|
| 0 | `BetaNotice` | `tutorial_beta` | 1 m of horizontal movement from the spawn anchor |
| 1 | `Welcome` | `tutorial_Welcome` | follow-up, 1 s after §0 finishes |
| 2 | `Hut` | `tutorial_hut` | the deed carried through the hut door |
| 3 | `Planting` | `tutorial_planting` | follow-up, 3 s after §2 finishes |
| 4 | `Selling` | `tutorial_selling` | the local player harvests a produce |
| 5 | `SellingBasics` | `tutorial_first_100` | the local player completes a sale |
| 6 | `ExplorationIntro` | `tutorial_first_flight` | balance reaches 100 Thatch |
| 7 | `FlightControls` | `tutorial_flight_1` | launch |
| 8 | `TroughShotTechnique` | `tutorial_flight_2` | 5 s continuously airborne |
| 9 | `FlyingWrapUp` | `tutorial_final` | landing after a flight of 10 s or more |

Four decisions in there that were each arrived at the hard way, and should not be undone casually:

- **Ids are strings, not an enum.** They are persisted by name; a reordered enum would silently
  replay everything the player has already heard — the same class of mistake as renumbering a wire
  id.
- **Clips are matched by asset name, not array position.** A mis-drag in the Inspector would
  otherwise give the wrong narration with nothing to notice, and the order is invisible once the
  scene is saved.
- **Skipped steps stay skipped.** Triggering §5 while the player is on §3 plays §5 and leaves §3
  and §4 unheard forever. A line delivered out of its context is worse than no line.
- **Two lines hang off another line, not off an event** (§1 and §3). Their natural trigger is the
  same instant as the line before them, so timing them from that clip *finishing* is the only way
  they do not talk over each other.

**Suppressed entirely when the world has no plot for this player.** There are four plots; every
line assumes the player is getting one, and flight is gated behind §6. A fifth player would be
talked at about a garden they cannot have and then locked out of flying for the session. Silent by
design — there is no "sorry, the world is full" line, and inventing one out of a suppression rule
would be worse than saying nothing.

### Status

Written, wired into the scene, and **walked as far as §3 in-world** (client session 2026-09-10
02:25). That run ended in the `_handPitchOffset: 55` plummet, which is fixed but was fixed *after*
the session — so **§4 through §9 have never been heard**. That walk is the next thing the tutorial
needs, and it needs an upload rather than any more code.

---

## Parked for pass 2

### The tips

Five short lines that are **not** part of the linear script and are not gated on it — a tip
describes a thing the player has just met, whenever they meet it. Only one is wired:

| Tip | Clip | State |
|---|---|---|
| `Falling` | `tip_falling` | **wired** — `WorldFloor` plays it after a rescue |
| `Climbing` | `tip_climbing` | parked: needs a "player is climbing" signal that nothing announces yet |
| `Upgrades` | `tip_upgrades` | parked: upgrades do not exist (roadmap step 2) |
| `Buffing` | `tip_buffing` | parked: buffs do not exist |
| `Teams` | `tutorial_teams` | parked: teammates exist on the plot, but nothing announces joining one |

Three of the four are parked because **the thing they describe is not built**, and a tip for a
feature the player cannot reach is a line that will never play. `Climbing` is the only real gap:
the mechanic is there, the signal is not.

Wiring one is small — a fact announced by whatever system gains the feature, and one handler in
`TutorialTriggers`. Do it as part of building that feature, not as a tutorial task.

`TipsAfterTutorial` holds `Falling` and `Climbing` back until the linear script has finished,
because both describe falling and falling is exactly what happens while the flight lesson is still
being delivered. One session had the 20.6-second falling tip queued between the flight controls and
the trough-shot line. Any new tip that only makes sense once the player can fly belongs in that set;
an upgrade or a buff can be found at any point and reads fine on its own.

### Other pass-2 debt

- **`SettingsPanel` is out of the scene.** With the panel in the hut, Somnium's own tablet would not
  open. Not understood; the panel holds the flight trim capture and the two narration switches
  (including `SkipTutorial`, the player's own off switch). Every consumer null-guards and fails
  open, so its absence changes only that the player cannot turn the narrator off or trim their
  flight. See the commit message on `bcdc62a` for the trim design.
- **The script is a first cut.** Nothing has been re-recorded since the walk, and §4–§9 have not
  been heard in context at all. Expect line-level edits once they have.

---

Related: [`flight.md`](flight.md) — §6 unlocks flight, and §7–§9 teach it.
[`plot-ownership.md`](plot-ownership.md) — the deed §2 describes.
