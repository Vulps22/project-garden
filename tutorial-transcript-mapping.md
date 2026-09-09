# Tutorial Script → MP3 File Mapping

## Tutorials (linear progression)

| Step | Script Section | MP3 File | Duration | Trigger |
|---|---|---|---|---|
| 1 | Welcome | `tutorial_Welcome.mp3` | 10.9s | World load, player spawn |
| 2 | Hut Intro / Deed Scroll | `tutorial_hut.mp3` | 17.9s | First time entering hut |
| 3 | First Thatch (Planting) | `tutorial_planting.mp3` | 8.3s | After hut, ready to plant |
| 4 | First Harvest (Selling) | `tutorial_selling.mp3` | 7.2s | Plant harvested / produce spawned |
| 5 | Selling Basics / Practice Goal | `tutorial_first_100.mp3` | 15.4s | First produce sold |
| 6 | Exploration Intro | `tutorial_first_flight.mp3` | 25.3s | Player reaches 100 Thatch |
| 7–8 | Flying (Oops Moment) + Flight Controls | `tutorial_flight_1.mp3` | 17.1s | Player launches into air |
| 9 | Trough-Shot Technique | `tutorial_flight_2.mp3` | 20.8s | Player in flight, or on first airborne interval |
| 10 | Flying Wrap-Up | `tutorial_final.mp3` | 10.3s | Flight phase completed |

## Tips (context-triggered)

| Trigger Event | Script Section | MP3 File | Duration | Condition |
|---|---|---|---|---|
| Fall below world floor | First Fall (World Floor) | `tip_falling.mp3` | 20.5s | Y < -50 (approx) → teleport + audio |
| Bounce off island surface | Falling Off an Island | `tip_climbing.mp3` | 7.8s | Collision while falling → mentions rock grab |
| Collect upgrade seed | Upgrade Seed Found | `tip_upgrades.mp3` | 18.7s | First upgrade acquired |
| Collect buffing seed | Buffing Seed Found | `tip_buffing.mp3` | 13.3s | First buff/feature seed acquired |
| Player joins deed | Player Added to Deed List | `tutorial_teams.mp3` | 17.1s | Second player added to plot ownership |

## Status

✅ **COMPLETE** — All 10 steps have audio:
- 9 tutorial_* files (one combines steps 7–8)
- 5 tip/event-triggered files
- **Total: 15 of 15 script sections covered**

Ready to wire into the scene.

## Implementation Notes

### Audio Sequencing
- **Linear tutorials** play in order only. Track `CurrentTutorialStep` (0–9); only advance when trigger hits AND player ≥ that step. Skip steps stay skipped.
- **Tips** can play multiple times; gate by "first occurrence" or "once per session"
- Narrator voice should have consistent character: helpful, witty, occasionally comedic
- Use Somnium's spatial audio where applicable (narrator centered in world space)

### Skipped Steps
If a player somehow bypasses a step (e.g., debug warp to 100 Thatch), they don't hear earlier tutorials. This is acceptable because:
- Normal gameplay flow ensures sequential progression
- Context-free playback of out-of-order tutorials breaks narrative
- Skipped steps are an edge case, not a design path

### File Locations
All audio files live in: `Assets/#User/GrowAGarden/sounds/`

### Audio Properties
- **Format:** MP3 (stereo, 44.1 kHz)
- **Bitrate:** ~90–98 kbps
- **Total linear tutorial duration:** ~133 seconds (~2:13)
- **Total tip duration:** ~77.4 seconds (~1:17)

### Progression Gates
- **Flight disabled until Step 7 triggered** — prevents skipping linear progression
- Other steps gated by in-world milestones (plant, harvest, Thatch earned, etc.)

### Persistent Storage (Somnium Bridge)
Store tutorial progress as JSON in local storage:
```json
{
  "tutorialDisabled": false,
  "currentStep": 2,
  "stepsPlayed": [0, 1],
  "tipsPlayed": ["falling", "upgrades"]
}
```
- **tutorialDisabled:** Master toggle (user setting at end of branch)
- **currentStep:** Next step to play (0–9)
- **stepsPlayed:** Array of completed step indices (for skip detection)
- **tipsPlayed:** Array of tip keys that have triggered (prevents spam)

### Next Steps
1. Create `TutorialManager` component to read/write storage
2. Wire AudioSource components to trigger points
3. Implement event listeners for milestones
4. Lock flight behind Step 7 trigger
5. Add Settings menu toggle for tutorial disable

## Conversion Status
✓ All 14 MP3s converted from **mono → stereo** for VR audio (2026-09-09)
✓ All files are 44.1kHz, ready for Unity import
✓ All triggers and durations documented
