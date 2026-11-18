# Stormdrop Royale

A mobile-first 3D battle royale for Android (and iOS) built with **Unity 6 + Netcode for GameObjects**, a
**Node.js API on Vercel** and **Supabase Postgres** — the same Vercel + Supabase setup the old Stock Analyzer used.
All branding, characters, items and names are original.

- 50 players parachute onto a 4 × 4 km island; 8 shrinking storm phases; 10–15 minute matches; last squad standing is **STORM CHAMPION!**
- Modes: Solo, Duo, Squad, Clash Squad 4v4, Team Deathmatch, Training Ground, Ranked (Bronze → Grandmaster)
- Virtual joystick, fire/aim/scope/jump/crouch/prone/reload/sprint, customisable HUD, auto-fire, gyroscope aim
- HP 200, vest/helmet levels 1–4, backpacks 1–3, knock-down and revive
- 11 guns across AR/SMG/Shotgun/Sniper/LMG/Pistol, melee, grenades, deployable Foam Wall, 5 attachment slots, airdrops
- 12 characters with skills, 4 pets, jeep/motorbike/buggy/boat with fuel and damage
- Guest / Google / Facebook login, friends, parties, guilds, quick chat + voice chat, regional ranked matchmaking
- Server-side hit validation with lag compensation, server-checked movement, server-calculated rewards
- Gold + Diamonds, store, Storm Pass (free/premium), Fortune Drop with public odds, missions, achievements, login calendar, events
- Smooth / Standard / Ultra graphics presets, tuned for 2 GB RAM phones at 30 FPS
- Analytics + crash reporting (Firebase), push notifications, Google Play Billing with server verification

## Repository

| Folder | What it is |
|---|---|
| `unity/StormdropRoyale/` | The Unity project (open it with Unity Hub) |
| `backend/` | Node.js API + database schema + privacy/terms/account-deletion pages (deploy to Vercel) |
| `server/` | Docker files for dedicated match servers |
| `play-store/` | Everything Google Play asks for: listing text, data safety, content rating, IAP products, release checklist, icon and feature graphic |
| `docs/` | [Setup](docs/SETUP.md) · [Architecture](docs/ARCHITECTURE.md) · [Game design](docs/GAME_DESIGN.md) · [Database](docs/DATABASE.md) |

## Quick start

1. **Database:** `cd backend && cp .env.example .env` (add your Supabase `DATABASE_URL`) → `npm install && npm run db:setup`
2. **API:** Vercel → Root Directory `backend` → add the env vars from `.env.example` → deploy → open `/api/health`
3. **Game:** Unity Hub → *Add project from disk* → `unity/StormdropRoyale` → **Stormdrop ▸ 1. Run Setup Wizard** →
   restart the editor → set the API URL on *GameBootstrap* → Play → *Play as Guest* → *Training Ground* → START
4. **Matches:** build the Linux server and run it with Docker (`docs/SETUP.md` §4)
5. **Publish:** `play-store/RELEASE_CHECKLIST.md`

Full instructions: **[docs/SETUP.md](docs/SETUP.md)**.

## Tests

```bash
cd backend && npm run check     # loads every module + rank/period unit checks
```
The full API flow (accounts, store, missions, spin, friends, party, guild, matchmaking, dedicated-server results,
rank points, account deletion) has been exercised end-to-end against Postgres 16.

## Status and what's placeholder

The code is complete enough to play: Training Ground runs offline in the editor, and online matches work once a
dedicated server is running. Art is placeholder (capsule characters, box buildings, procedural island, code-built UI) —
see `docs/SETUP.md` §8 for swapping in real models. Google login, Facebook login, purchases, Firebase and Vivox each
need their SDK imported and a checkbox ticked (`docs/SETUP.md` §6).
