---
aliases:
  - "Persistence"
  - "Accounts"
tags:
  - mechanic
---
# Persistence

What a player keeps between sessions: today Thatch ([[economy]]); later the warehouse ([[warehouse]]). Kept by our
own backend, behind an opt-in account.

## The backend

**Status (2026-11-04):** prototype built and tested locally, not deployed; the world side not built.

- **`project-garden-backend`**, in `~/Documents/project-garden-backend`; its README has the API. TypeScript on
  `@vulps22/pathfinder`, the VPS's Postgres, Docker in `/opt/somnium/project-garden`, served at `garden.vulps.co.uk`
  behind Cloudflare. The world talks to it with `UnityWebRequest`.
- **It holds only a player's own data.** What other players see still travels by Fusion, the master loading a
  joiner's saved state into the networked state.
- **Each client writes its own progress:** once the master approves a Thatch change, the client reports it as a
  change with its own id, so a retry never counts twice and a change can never take the balance below zero. A token
  stops impersonation, not a player cheating on their own account.
- **No earning cap yet**, while seeds are still being added and rebalanced; anyone with the API can inflate their own
  balance until there is one.

## Accounts

**Status (2026-11-04):** decided by Vulps; built in the backend, not in the world.

- **Saving is opt-in.** A player who never asks for an account plays exactly as today, and loses nothing this session.
- **An account is keyed by Somnium id.** On first request the server assigns our own id to the Somnium id, and returns
  a bearer token (kept in `LocalStorage`) and a recovery code shown once. The account starts at the Thatch the client
  reports at that moment.
- **Recovery is the code alone.** The server hashes the requesting Somnium id, the id it assigned and the code, and
  compares; no passwords, so nothing a player reused elsewhere is ever stored. The code is long and random, attempts
  are rate-limited, and it is served over HTTPS.
- **The code is shown with each block's third character in a different colour** (`AB`**`C`**`DE-…`), so a player
  copying it can tell five characters from four at a glance.
- **A Somnium id is claimed by whoever asks first.** Any client can send any id, so someone could claim a player's
  before they do; that is fixed by hand on the server.

## The warehouse table

**Status (2026-11-04):** designed, not built ([[warehouse]]).

`warehouse: player_id, socket_index, item_id`: one item per shelf socket, the item by its stable string id, and the
unlock a flag on the account.

