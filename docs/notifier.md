---
aliases:
  - "Notifier"
tags:
  - interaction
---
# Notifier

Short messages to one player: a box just below the middle of their view for two seconds, with a chime. Any of our
code can raise one with `Notifier.Push("…")`.

## How it works

**Status (2026-11-03):** built, compiles clean, untested in-world. Asked for by Vulps 2026-11-03.

- **`Notifications/Notifier`** (on its own `Notifier` object under SceneManager): `Notifier.Push(text)` shows a toast.
  - **Where:** a world-space canvas fixed to the eyes (`Camera.main`), 0.6 m in front and 6 cm below the middle
    (`_distance`, `_below`), 900 × 0.0002 m wide (18 cm). It draws over everything, the tablet and walls included
    (`ZTest Always`, render queue 4000).
  - **The box:** dark, 88% opaque, white text 48 pt, wrapping. It fades in and out over 0.25 s and is gone after 2 s.
    Up to four stack downwards; a fifth pushes the oldest out.
  - **The chime:** `sounds/Notified 2.wav` (copied from the HintsStarsLite pack), played on an `AudioSource` that is in
    the scene from load, so Somnium routes it through the player's volume ([[tablet#Settings|tablet → Settings]]).
    **A push less than 5 s after the last one is silent** (Vulps); every push restarts that 5 s.
- **Off switch:** World Settings → **Show Notifications** ([[tablet#World Settings|tablet → World Settings]]), on by
  default, stored as `noNotifications` in `player_settings`. Off, `Push` only logs.
- **What pushes one:**
  - **A player joins:** "<name> has joined the world" ("A new player has joined the world" without a name). Not the
    player themselves, and not for anyone already there in their first 10 s, so a late joiner is not told about
    everyone in the room.

> [!info] Networking
> None. A toast is this player's own; whatever pushes one decides who sees it.
