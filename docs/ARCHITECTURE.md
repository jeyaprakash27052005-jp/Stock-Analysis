# Architecture

```
 ┌──────────────── Phone (Unity client) ────────────────┐        ┌──────── Vercel (Node.js API) ────────┐
 │ UI screens ─ Backend.cs ─ ApiClient (HTTPS/JSON) ─────┼──────▶ │ /api/auth  /me  /store  /progression │
 │                                                       │        │ /social  /matchmaking  /leaderboard  │
 │ Netcode client ◀──── UDP (Unity Transport) ─────┐     │        │ /server/* (dedicated servers only)   │
 └─────────────────────────────────────────────────┼─────┘        └───────────────┬──────────────────────┘
                                                   │                              │ SQL (pooler :6543)
 ┌────────── Dedicated server (Linux, Docker) ─────┴──────┐        ┌──────────────▼──────────────────────┐
 │ Same Unity project, headless. Authoritative:            │ HTTPS  │ Supabase Postgres (RLS on, no public │
 │ damage, hits, loot, zone, match flow, results           ├──────▶ │ policies — only the API can access)  │
 └─────────────────────────────────────────────────────────┘        └─────────────────────────────────────┘
```

## Match lifecycle

1. **Queue** – the phone calls `POST /api/matchmaking/join` (party leaders queue the whole party). Members' lobbies poll
   `/matchmaking/current` and follow automatically.
2. **Form** – every poll tries to build a match: tickets with the same mode, region and (ranked) rank band
   (±300 RP, widening 100 RP every 15 s). Parties stay together; solos fill squads. A lobby starts when full, or after
   45 s with the mode's minimum.
3. **Allocate** – a `ready` server in that region (heartbeat < 30 s) is locked with `FOR UPDATE SKIP LOCKED`, a `matches`
   row is created and every ticket gets a team.
4. **Join ticket** – each player receives a 10-minute HS256 JWT (`sub`, `mid`, `team`, character, pet) signed with
   `SERVER_SECRET`. The server's connection approval verifies the signature, the match id and the expected player list,
   so nobody can join a match they weren't put in or choose their own team.
5. **Play** – the server's `MatchManager` waits for everyone (≤ 60 s), then runs the mode: plane → parachute →
   8 storm phases (Battle Royale), rounds (Clash Squad), respawns (Team Deathmatch).
6. **Results** – eliminated players get their result immediately; at the end the server posts all stats to
   `/api/server/match-result`. The **backend** computes XP, Gold, rank points, mission/achievement progress, Pass XP and
   guild glory — clients never report their own rewards. The server process then exits and Docker starts a fresh one.

## Netcode model (Unity Netcode for GameObjects)

| What | Authority | Why |
|---|---|---|
| Player movement | Owner (`ClientNetworkTransform`) | Instant response on 100–200 ms mobile networks |
| Movement validation | Server (`PlayerController.ServerTick`) | Max speed per state checked every 0.5 s; violations are pulled back and flagged |
| Shooting / damage | Server (`WeaponSystem.FireRpc`) | Ammo, fire-rate (per shot and per second), shot origin, range, aim angle, line of sight and **lag-compensated** target position (`LagCompensation`, ≤ 350 ms rewind) are verified before any damage |
| HP, armor, knock, revive | Server (`PlayerHealth`) | Network variables written only by the server |
| Inventory and loot | Server (`PlayerInventory`, `LootSpawner`) | Every pickup/drop/use is an RPC the server validates |
| Zone | Server picks circles; clients interpolate from server time | Only ~9 values sent per phase |
| Plane | Position computed from start time on every machine | Zero bandwidth after spawn |
| Vehicles | Ownership handed to the driver; fuel/health server-side | Responsive driving |

Players flagged by the anti-cheat get no rewards for that match and a `reports` row with `source = 'anticheat'`.

### Loot with almost no bandwidth
World loot is not made of NetworkObjects. The server shares one random **seed**; every machine sorts the
`LootSpawnPoint`s the same way and rolls the same loot with a deterministic xorshift RNG. Only *taken* loot ids and
*dropped* items are networked (`NetworkList`). Clients render only loot within 45 m from a small object pool.

## Performance budget for 2 GB phones (30 FPS)

- `GameSettings` picks **Smooth** automatically on ≤ 3 GB RAM: lowest quality level, no shadows, 65 % render
  resolution, 30 FPS cap, lower LOD bias.
- Terrain pixel error 8, instanced terrain, static batching for buildings, shadows disabled on effects.
- The minimap is rendered **once** into a texture (no second camera per frame).
- The safe-zone wall is one mesh; tracers reuse one LineRenderer per player.
- Server tick rate 30 Hz; owner-auth transforms sync position + Y rotation only.
- Guidelines for real art: ≤ 150k triangles on screen, ≤ 120 draw calls, ASTC textures, ≤ 600 MB RAM total.

## Backend

Node.js 20 + Express 5 on Vercel serverless (`backend/api/index.js`). Each request opens connections through
Supabase's transaction pooler. All money changes go through `changeCurrency()`, which refuses to go negative and writes
`currency_ledger`. Purchases are verified with Google (`purchases.products.get`) and credited once per purchase token.
A daily Vercel Cron (`/api/cron/cleanup`) expires stale tickets, marks dead servers offline and prunes chat.

## Scaling notes

- API: stateless; Vercel scales it. For > ~2k concurrent players move the matchmaker into a long-running worker
  (same SQL) so it isn't triggered by polling, and add Redis for presence/party invites.
- Match servers: one process per match, ~1 vCPU / 700 MB each. Run them per region (Mumbai for India, Singapore for SEA, …)
  or use a fleet manager such as Unity Multiplay, Agones (Kubernetes) or Edgegap — they only need to start the container
  with the same environment variables.
- Database: indexes cover leaderboards, waiting tickets and ledgers. Enable Supabase backups before launch.

## Folder structure

```
backend/                     Node.js API (deploy this folder to Vercel)
  api/index.js               Vercel entrypoint
  src/app.js                 Express app and routes
  src/routes/                auth, profile, store, progression, social, matchmaking, server, misc
  src/lib/                   auth/JWT, economy, ranks, providers (Google/Facebook/Play), periods
  db/schema.sql, seed.sql    database tables + starter content
  public/                    website, privacy policy, terms, account-deletion form
server/                      Dockerfile + docker-compose for dedicated match servers
play-store/                  store listing, data safety, content rating, IAP, release checklist, graphics
docs/                        setup, architecture, game design, database
unity/StormdropRoyale/       Unity 6 project
  Assets/_Project/Scripts/
    Core/        bootstrap, constants, settings (graphics presets)
    Data/        ScriptableObjects: weapons, attachments, items, characters, pets, loot tables, zone
    Networking/  network bootstrap, join tickets, match manager, dedicated-server agent, lag compensation
    Player/      controller, input, camera, health/armor/knock/revive, inventory, identity
    Weapons/     weapon system (server hit validation), grenades, foam wall
    Zone/        safe zone manager + visual
    Loot/        deterministic loot spawner, visuals, airdrops
    Skills/      character skills + pet buffs
    Vehicles/    vehicle controller + spawn points
    Map/         plane, minimap, pings + quick chat, day/night, spawn points, named locations
    Backend/     API client, models, auth (guest/Google/Facebook), purchases
    Services/    analytics + crash reporting, push notifications, voice chat
    UI/          UI framework, screens (splash → results), HUD, joystick, HUD layout editor
  Assets/_Project/Editor/    setup wizard, build tool
```
