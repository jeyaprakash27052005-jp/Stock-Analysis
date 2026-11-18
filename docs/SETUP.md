# Step-by-step setup

From an empty machine to a game running on your phone, a live backend, dedicated match servers and a
Play Store build. Follow the steps in order; each ends with a check so you know it worked.

---

## 0. What you need

| Tool | Version | Used for |
|---|---|---|
| [Unity Hub](https://unity.com/download) + **Unity 6 LTS** (6000.0.38f1 or newer) | with modules **Android Build Support** (incl. OpenJDK and Android SDK & NDK Tools), **Linux Dedicated Server Build Support**, and on a Mac **iOS Build Support** | The game |
| Node.js | 20 or newer | Backend (local runs and the database setup script) |
| Git | any | Version control |
| Accounts | Supabase (you already have the project from the old app), Vercel, Google Play Console, Google Cloud, optionally Facebook for Developers and Firebase | Hosting, store, login, analytics |
| A Linux VM per region | 2 vCPU / 2 GB RAM minimum (e.g. Mumbai region for India) | Dedicated match servers |

---

## 1. Database (Supabase — same project as before)

The old Stock Analyzer tables (`watchlist`, `alerts`, `price_history`, `signal_history`) are replaced by the game
schema. The new tables are created next to them; nothing is dropped automatically.

**Option A – script (recommended)**
```bash
cd backend
cp .env.example .env          # put your Supabase Transaction-pooler DATABASE_URL in it
npm install
npm run db:setup              # runs db/schema.sql then db/seed.sql
```
**Option B – Supabase dashboard:** SQL Editor → paste `backend/db/schema.sql` → Run, then `backend/db/seed.sql` → Run.

To remove the old stock tables once you don't need that data, uncomment the `drop table …` line at the top of
`schema.sql` and run it again (this permanently deletes the old data).

✅ **Check:** Supabase → Table Editor shows `players`, `items` (≈50 rows), `matches`, `guilds` … See `docs/DATABASE.md`.

---

## 2. Backend API (Vercel — same account as before)

1. Vercel → your existing project (or **Add New → Project** and import this repo).
2. **Settings → General → Root Directory = `backend`**. Framework preset: **Other**.
3. **Settings → Environment Variables** (Production + Preview):

   | Name | Value |
   |---|---|
   | `DATABASE_URL` | Supabase → Connect → **Transaction pooler** string (port 6543) with your DB password |
   | `JWT_SECRET` | long random string (`openssl rand -hex 32`) |
   | `SERVER_SECRET` | another long random string — also given to your dedicated servers |
   | `CRON_SECRET` | another random string (Vercel Cron sends it automatically) |
   | `GOOGLE_WEB_CLIENT_ID`, `GOOGLE_WEB_CLIENT_SECRET` | step 6a |
   | `FACEBOOK_APP_ID`, `FACEBOOK_APP_SECRET` | step 6b (optional) |
   | `ANDROID_PACKAGE_NAME` | e.g. `com.yourstudio.stormdrop` |
   | `GOOGLE_PLAY_SERVICE_ACCOUNT_JSON` | step 6c |
   | `SUPPORT_EMAIL` | your support address |

   Remove the old variables `APP_PASSWORD`, `DATA_MODE`, `TELEGRAM_*` — they are no longer used.
4. Redeploy. Edit `backend/public/privacy.html` and `terms.html` (studio name, email) before going public.

✅ **Check:** `https://<your-project>.vercel.app/api/health` → `{"status":"ok","database":"connected"}` and
`/privacy`, `/terms`, `/delete-account` open.

**Run locally instead:** `cd backend && npm run dev` → http://localhost:3000. While testing matchmaking alone set
`MM_MIN_PLAYERS=1` and `MM_FILL_SECONDS=5` in `.env` so a match starts with just you.

---

## 3. Open the Unity project

1. Unity Hub → **Add → Add project from disk** → select `unity/StormdropRoyale`. If Hub asks for an editor version,
   pick your installed Unity 6 LTS.
2. First open takes a few minutes while packages download (Netcode for GameObjects, Unity Transport, URP, Newtonsoft JSON).
3. Menu **Stormdrop ▸ 1. Run Setup Wizard**. It creates:
   - all weapons, attachments, items, 12 characters, 4 pets, loot tables and the zone config (`Assets/_Project/Data`)
   - network prefabs: player, match systems, plane, airdrop, grenade, foam wall, 4 vehicles (`Assets/_Project/Prefabs`)
   - the **Bootstrap** scene and the **IsleVerdant** scene — a 4×4 km island with a city, military base, farmland,
     port, mountains and a river, ~900 loot points, vehicle spawns and team spawns
   - layers and *Active Input Handling = Both*
4. **Restart the Unity editor once** (needed for the input-handling change).
5. Open `Assets/_Project/Scenes/Bootstrap.unity`, select **GameBootstrap**, set **Api Base Url** to
   `https://<your-project>.vercel.app/api` (or `http://localhost:3000/api`).

✅ **Check:** press **Play** → Splash → Login → **Play as Guest** → Lobby shows your nickname, Gold and Diamonds.
Choose **Mode → Training Ground → START**: you spawn on the island with a rifle. Controls in the editor:
WASD move, mouse look (click to lock, Esc to unlock), left click fire, right click aim, R reload, Space jump,
C crouch, Z prone, Shift sprint, F interact, Q skill, Tab switch weapon.

---

## 4. Dedicated match servers

Matches run on servers you control so the server can validate every shot (anti-cheat).

1. Unity: **Stormdrop ▸ 4. Build Dedicated Server (Linux)** → `unity/StormdropRoyale/Builds/Server/`.
2. On your Linux VM (Docker installed), from the repository root:
   ```bash
   docker build -f server/Dockerfile -t stormdrop-server .
   cp server/.env.example server/.env    # set STORMDROP_SECRET (= SERVER_SECRET), STORMDROP_API, STORMDROP_HOST (VM public IP), STORMDROP_REGION
   docker compose -f server/docker-compose.yml up -d
   ```
3. Open the firewall for **UDP 7777–7779** (cloud security group / `ufw allow 7777:7779/udp`).
4. Each container registers with the backend, waits for a match, hosts it, reports results, exits, and Docker restarts it
   fresh for the next match. Add more services (ports) to host more simultaneous matches.

✅ **Check:** Supabase → `game_servers` shows your servers with `status = ready` and a recent `last_heartbeat`.
Then in the editor pick **Battle Royale – Solo** → START (with `MM_MIN_PLAYERS=1`): you connect, ride the plane,
jump, and the storm starts.

To test two players on one PC, build an Android or desktop client and run it next to the editor, or use the
*Multiplayer Play Mode* package (Window ▸ Package Manager → com.unity.multiplayer.playmode).

---

## 5. Run on your Android phone

1. Phone: Settings → About → tap *Build number* 7× → Developer options → **USB debugging** on. Connect via USB.
2. Unity: **File ▸ Build Profiles** → Android → **Switch Platform**.
3. Quick test build: Build Profiles → Android → *Build And Run* (APK is fine for testing).

✅ **Check:** the game opens in landscape, guest login works, Training Ground runs at 30+ FPS on *Smooth*.

---

## 6. Optional SDKs (each one is switched on with a checkbox in **Stormdrop ▸ 2. Build Settings…**)

The code for each SDK is already written behind a scripting define, so the project compiles without them.
Install the SDK first, then tick its box.

### 6a. Google login — Google Play Games Services v2 (`STORMDROP_GPGS`)
1. Play Console → your app → **Play Games Services → Setup** → create a game project.
2. Google Cloud console (same project) → **Credentials**: an **Android** OAuth client (package name + SHA-1 of your
   *app-signing* key from Play Console → App integrity) and a **Web application** OAuth client.
3. Put the Web client ID/secret in Vercel (`GOOGLE_WEB_CLIENT_ID`, `GOOGLE_WEB_CLIENT_SECRET`).
4. Import the *Google Play Games plugin for Unity* (v11+, from its GitHub releases) → Window ▸ Google Play Games ▸ Setup
   → Android setup → paste resources from Play Console and the **Web client ID**.
5. Tick `STORMDROP_GPGS`.

### 6b. Facebook login (`STORMDROP_FACEBOOK`)
developers.facebook.com → create a *Consumer* app → add **Facebook Login** → set package name, class name
`com.unity3d.player.UnityPlayerActivity` and key hashes. Import the *Facebook SDK for Unity*, set App ID/Client Token in
Facebook ▸ Edit Settings, put `FACEBOOK_APP_ID`/`FACEBOOK_APP_SECRET` in Vercel, tick `STORMDROP_FACEBOOK`.

### 6c. In-app purchases (`STORMDROP_IAP`)
Package Manager → **In-App Purchasing** (latest). Create the products and the service account in
`play-store/IAP_PRODUCTS.md`. Tick `STORMDROP_IAP`.

### 6d. Analytics, crash reporting, push (`STORMDROP_FIREBASE`)
Firebase console → add Android app (package name) → download `google-services.json` into `Assets/` (git-ignored).
Import FirebaseAnalytics, FirebaseCrashlytics and FirebaseMessaging `.unitypackage`s, run *Assets ▸ External Dependency
Manager ▸ Android Resolver ▸ Force Resolve*, tick `STORMDROP_FIREBASE`.

### 6e. Voice chat (`STORMDROP_VIVOX`)
Unity Cloud dashboard → link the project → enable **Vivox**. Package Manager → `com.unity.services.vivox`.
Edit ▸ Project Settings ▸ Services → link project. Tick `STORMDROP_VIVOX`.

---

## 7. Release build for Google Play

1. Create the upload key (`play-store/RELEASE_CHECKLIST.md` §2).
2. **Stormdrop ▸ 2. Build Settings…** → package name, version, version code, keystore file, alias, passwords
   (passwords are kept only for this editor session; for CI use `STORMDROP_KEYSTORE_PASS` / `STORMDROP_KEY_PASS`).
3. **Build Android App Bundle (.aab)** → `unity/StormdropRoyale/Builds/Android/stormdrop-<version>-<code>.aab`.
4. Upload to **Play Console → Testing → Internal testing → Create release**.
5. Continue with `play-store/RELEASE_CHECKLIST.md` (content rating, data safety, store listing, closed test, production).

---

## 8. Replace the placeholder art

Everything visual is a placeholder so the game is playable today: capsule characters tinted per character,
box buildings, sphere trees, a procedural island and code-built UI.
- **Characters:** assign a rigged model to `CharacterData.model` and parent it under the player's *Body*.
- **Weapons/loot:** set `ItemDefinition.worldModel` — `LootVisual` uses it automatically.
- **Map:** sculpt/replace `IsleVerdant` (keep `LootSpawnPoint`, `VehicleSpawnPoint`, `SpawnPoint`, `MapLocation`
  components). Use LOD Groups and texture atlases, and keep total scene memory under ~600 MB for 2 GB phones.
- **UI:** the UI is built in code (`Scripts/UI`), so restyle via `UIFactory` colours/fonts or replace screens one by one.
