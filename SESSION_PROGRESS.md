# Session Progress: Tutorial Branch & Flight Fix

**Date:** 2026-09-09  
**Branch:** `tutorial` (merged from `add-advanced-flight` → `main`)  
**Status:** In progress — TutorialManager built, flight bug fixed, ready for integration

---

## What Was Accomplished

### 1. Audio Setup & Mapping
- **14 MP3 files copied** to `Assets/#User/GrowAGarden/sounds/`
  - 10 tutorial files (linear progression)
  - 4 context-triggered tips (falling, climbing, upgrades, buffing, teams)
- **Mono → Stereo conversion** applied to all files (VR requirement)
- **Transcript mapping created** (`tutorial-transcript-mapping.md`)
  - Maps each audio file to script section and trigger conditions
  - Documents durations, trigger events, implementation notes

### 2. TutorialManager Implementation
**Location:** `Assets/#User/GrowAGarden/scripts/scripts/Tutorial/TutorialManager.cs`

**Architecture:**
```csharp
TutorialManager.Tooltips.Tutorial.Welcome    // Step constants
TutorialManager.Tooltips.Tips.Falling        // Tip constants
TutorialManager.GetInstance().TriggerTooltip(id)  // Public API
```

**Features:**
- Singleton with `GetInstance()` (SOLID design)
- Persistence via `SomniumBridge.LocalStorage` (sanctioned Somnium API)
- Tracks played tooltips in JSON: `{ "played": ["Welcome", "Hut", ...] }`
- Fires on `PlayerManager.LocalPlayerJoined` → triggers Welcome
- No persistence in memory — survives rejoin

**Key Decisions:**
- Initial complex version (try-catch, Debug.Log, custom serialization) triggered Somnium SDK "non-standard logic" filter
- Stripped to minimal, then added only `SomniumBridge.LocalStorage` (official API)
- Used simple `[Serializable]` TooltipProgress class with string array (no Dictionary serialization issues)

### 3. Flight Bug Fix
**Issue:** Plummeting during glide (inverted sink curve)

**Root Cause:** Vertical descent rate was linear in speed — faster flight = steeper sink. Should be inverse: faster = shallower descent.

**Fix:** Line 227-229 in `FlightModel.cs`
```csharp
float sinkRatio = _s.BestGlideSpeed / Mathf.Max(_s.BestGlideSpeed, Speed);
float vertical = Speed * Mathf.Sin(pathRad) * sinkRatio;
float forward = Speed * Mathf.Cos(pathRad);
```
- At BestGlideSpeed (18 m/s): sinkRatio = 1.0 (normal)
- At 2x BestGlideSpeed (36 m/s): sinkRatio = 0.5 (half sink, better glide)
- At low speed: sinkRatio clamped (forces steeper descent)

---

## Current State

✅ **Complete:**
- Audio files in place (stereo, VR-ready)
- Audio-to-script mapping documented
- TutorialManager compiles and uploads
- Persistence system in place (SomniumBridge.LocalStorage)
- Welcome tooltip wired to player spawn
- Flight sink curve inverted (fixes plummet bug)

⏳ **Ready for next phase:**
- Audio clip assignments to TutorialManager (Inspector drag-and-drop)
- Trigger wiring to game systems (SellPoint, EconomyManager, FlightController, etc.)
- Settings menu toggle for tutorial disable
- In-world testing

---

## What's Left

### Phase 1: Wire Triggers
Connect TutorialManager.TriggerTooltip() calls to game events:

| Trigger | Location | Method Call |
|---------|----------|-------------|
| Welcome | ✅ Done (OnLocalPlayerJoined) | `TutorialManager.GetInstance().TriggerTooltip(Tooltips.Tutorial.Welcome)` |
| Hut | Hut.cs | `OnPlayerEntered()` |
| Planting | PlantSlot.cs | `OnPlantPlaced()` |
| Selling | SellPoint.cs | `OnSaleCompleted()` |
| SellingBasics | EconomyManager.cs | `OnBalanceChanged()` when >= 100 |
| ExplorationIntro | FlightController.cs | `OnFirstFlight()` |
| FlyingOopsMoment | FlightController.cs | `OnFlightStarted()` |
| FlightControls | FlightController.cs | `OnFlightDuration(10s)` |
| TroughShotTechnique | FlightController.cs | `OnFlightDuration(10s)` |
| FlyingWrapUp | FlightController.cs | `OnFlightEnd()` |
| Falling (tip) | Fall detector | `Y < -50` |
| Climbing (tip) | Collision detector | `OnIslandCollision()` |
| Upgrades (tip) | CollectibleEntity.cs | `OnUpgradeCollected()` |
| Buffing (tip) | CollectibleEntity.cs | `OnBuffCollected()` |
| Teams (tip) | GardenLease.cs | `OnPlayerAddedToPlot()` |

### Phase 2: Audio Assignment
Inspector workflow:
1. Open TutorialManager in Inspector
2. Set `_audioSource` to scene AudioSource
3. Add 14 entries to `_tips` List
4. Drag MP3s from `Assets/sounds/` to `clip` field

### Phase 3: Settings Menu
- Add toggle for `tutorial_disabled` to hut settings
- Gate tutorial triggers with `if (TutorialManager.GetInstance().IsTutorialDisabled()) return;` (if added to API)

---

## Key Git Commits

| Commit | Description |
|--------|-------------|
| `4c5249c` | Merge add-advanced-flight → main |
| `5209931` | Add tutorial audio + mapping |
| `b9e1c4c` | Add TutorialManager singleton |
| `3a35512` | Use SomniumBridge.LocalStorage |
| `a69842b` | Fix inverted sink curve (flight) |

---

## Testing Checklist (In-World)

- [ ] Welcome plays on join
- [ ] Hut entry triggers Hut tooltip
- [ ] First plant placement triggers Planting tooltip
- [ ] First harvest triggers Selling tooltip
- [ ] Reaching 100 Thatch triggers SellingBasics + ExplorationIntro
- [ ] Flight launch triggers Flying sequence (Oops Moment, Controls, Trough-Shot, Wrap-Up)
- [ ] Falling below floor triggers Falling tip
- [ ] Finding upgrade triggers Upgrades tip
- [ ] Finding buff triggers Buffing tip
- [ ] Adding player to deed triggers Teams tip
- [ ] Tooltips don't replay on rejoin (persistence works)
- [ ] Settings toggle disables all tutorials

---

## Blockers / Open Issues

1. **Audio clip assignment** — needs manual Inspector work
2. **Trigger wiring** — needs calls added to ~12 game systems
3. **Flight fix verification** — untested in-world (needs player to test)
4. **Settings menu** — not yet wired up

---

## Notes

- **SomniumBridge.LocalStorage** is the sanctioned persistence API — use it, not PlayerPrefs
- **TutorialManager is minimal** — no error handling, no logging, just core functionality (Somnium SDK restrictions)
- **Audio files are stereo 44.1kHz MP3** — ready for VR spatial audio
- **In-order progression locked** — players can't skip ahead (designed to prevent confusion)
