---
aliases:
  - "Notifier"
tags:
  - interaction
---
# Notifier

Short messages to one player: a box just below the middle of their view for three seconds, with a chime. Any of our
code can raise one with `Notifier.Push("…")`, or `Notifier.Push("…", seconds)` for longer or shorter.

## How it works

**Status (2026-11-03):** built, compiles clean, untested in-world. Asked for by Vulps 2026-11-03.

- **`Notifications/Notifier`** (on its own `Notifier` object under SceneManager): `Notifier.Push(text)` shows a toast.
  - **Where:** a world-space canvas fixed to the eyes (`Camera.main`), 0.6 m in front and 6 cm below the middle
    (`_distance`, `_below`), 900 × 0.0002 m wide (18 cm). It draws over everything, the tablet and walls included
    (`ZTest Always`, render queue 4000).
  - **A 2D player** gets them as HUD toasts instead: a screen-space canvas, the column at the top centre of the
    screen, 60% of the size, the same boxes, fades and stacking (**Status (2026-11-11):** compiles clean, untested
    in-world; [[2d-players]]).
  - **The box:** dark, 88% opaque, white text 48 pt, wrapping. It fades in and out over 0.25 s and is gone after its
    duration, 3 s unless the caller says otherwise (2 s was too quick to read, Vulps). Up to four stack downwards; a
    fifth pushes the oldest out. The Notifier does not update at all while no toast is up.
  - **The chime:** `sounds/Notified 2.wav` (copied from the HintsStarsLite pack), played on an `AudioSource` that is in
    the scene from load, so Somnium routes it through the player's volume ([[tablet#Settings|tablet → Settings]]).
    **A push less than 5 s after the last one is silent** (Vulps); every push restarts that 5 s.
- **Off switch:** World Settings → **Show Notifications** ([[world-settings#The switches|world-settings → The switches]]), on by
  default, stored as `noNotifications` in `player_settings`. Off, `Push` only logs.
- **What pushes one:**
  - **A player joins:** "<name> has joined the world" ("A new player has joined the world" without a name). Not the
    player themselves, and not for anyone already there in their first 10 s, so a late joiner is not told about
    everyone in the room.
  - **A developer's notification** to everyone ([[dev-tools#Notify everyone|dev-tools → Notify everyone]]).
  - **"Something is ready to harvest"**: a fruit or crop ripened (`Produce.AnyRipened`, raised by each client as it
    sees ripeness reach 1) on a Plot this player owns or is a teammate on, the nearest of its Garden's four Plots. One
    notice per Plot every 10 s, so a plant bearing nine blooms is one notice. Produce that arrives already ripe (a late
    joiner, the Developer Market) is not news. Upgrades (Moonflower, Spotlight Tulip, popcorn) have nothing to harvest
    and say nothing.
  - **"You have been given Developer Access"**: this player's access went from off to on (`DeveloperAccess.Changed`);
    never to the owner, who always has it.

> [!info] Networking
> None. A toast is this player's own; whatever pushes one decides who sees it.
