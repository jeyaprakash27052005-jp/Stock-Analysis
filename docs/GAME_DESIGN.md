# Game design reference — Stormdrop Royale

Original IP: all names, characters and items here are original. The victory screen reads **STORM CHAMPION!**

## Modes

| Mode | Players | Rules | Length |
|---|---|---|---|
| Battle Royale – Solo / Duo / Squad | 50 (48 in squads) | Plane drop, loot, 8 storm phases, last team alive wins. Knock-down & revive in Duo/Squad | ~12 min |
| Clash Squad | 4 v 4 | Rounds with a fixed loadout that upgrades each round, 100 s per round, first to 4 | ~8 min |
| Team Deathmatch | 4 v 4 | Respawn after 3 s, first to 30 eliminations or 8 min | 8 min |
| Training Ground | 1 (offline host) | High-tier loot everywhere, instant respawn, no rewards | — |
| Ranked | BR or Clash Squad | Unlocks at level 6, matched by rank band | — |

## Ranks (`backend/src/lib/ranks.js`)

| Tier | From RP | Divisions |
|---|---|---|
| Bronze | 0 | IV → I |
| Silver | 1000 | IV → I |
| Gold | 1300 | IV → I |
| Platinum | 1700 | IV → I |
| Diamond | 2200 | IV → I |
| Heroic | 2800 | – |
| Grandmaster | 3400 | – |

BR rank change = placement points (0–40, +15 for a win) + 3 per kill (max 10) + 1 per assist − entry cost
(0, 4, 8 … 24 by tier). Clash Squad: +20 win / −12 loss + kills − tier.

## Player

HP 200 · Vigor energy up to 200 (converts into HP at 5 HP/s) · Vest & helmet levels 1–4
(20/30/40/50 % reduction, 100/150/200/260 durability) · Backpack levels 0–3 (120/220/290/380 capacity)
· Knocked: 100 bleed HP draining 4/s, revive takes 5 s and restores 60 HP.

## Weapons (`StormdropSetupWizard.CreateWeapons`)

| Weapon | Type | Ammo | Damage | RPM | Mag | Range |
|---|---|---|---|---|---|---|
| Raptor-7 | AR | Medium | 38 | 600 | 30 | 180 m |
| Halberd AR | AR | Medium | 45 | 480 | 30 | 200 m |
| Wasp SMG | SMG | Light | 26 | 900 | 32 | 70 m |
| Hornet SMG | SMG | Light | 30 | 780 | 25 | 80 m |
| Thunderclap | Shotgun | Shells | 19 × 8 | 75 | 6 | 25 m |
| Boar Pump | Shotgun | Shells | 23 × 6 | 140 | 4 | 22 m |
| Longreach | Sniper | Sniper | 170 | 40 | 5 | 650 m |
| Falcon DMR | Sniper | Sniper | 72 | 200 | 10 | 420 m |
| Juggernaut LMG | LMG | Heavy | 40 | 650 | 80 | 200 m |
| Viper Pistol | Pistol | Light | 34 | 320 | 12 | 50 m |
| Sidekick Revolver | Pistol | Heavy | 72 | 120 | 6 | 60 m |
| Fists / Machete / Crowbar | Melee | – | 18 / 70 / 60 | – | – | 2 m |

Throwables: Frag (160 dmg, 7 m), Smoke, Flash. Deployable **Foam Wall** (800 HP, 25 s).
Attachments: 2x / 4x / 8x scopes, Suppressor (hidden from gunfire pings), Compensator, Vertical Foregrip,
Extended Magazine (+50 %), Quickdraw Magazine (+20 %, 30 % faster reload), Tactical Stock.

## Characters

| Character | Skill | Type | Effect |
|---|---|---|---|
| Kael (starter) | Sprint Surge | Active | +30 % speed 6 s, CD 45 s |
| Mira | Field Medic | Passive | Heals 30 % faster, +15 % |
| Torvin | Bastion Dome | Active | 120 shield for 5 s, CD 60 s |
| Zara | Ghost Step | Passive | Silent while crouched/prone |
| Okoro | Healing Aura | Active | 6 HP/s to allies in 5 m for 5 s, CD 50 s |
| Lyra | Eagle Eye | Passive | Enemies you hit are revealed 3 s |
| Dante | Iron Skin | Passive | −8 % bullet damage |
| Suki | Blink Dash | Active | 7 m dash, CD 30 s |
| Rook | Scavenger | Passive | +25 HP on knocking an enemy |
| Nova | Pulse Scan | Active | Reveal enemies in 50 m, CD 75 s |
| Bram | Quick Hands | Passive | Reload 20 % faster |
| Ivy | Vigor Bloom | Passive | +50 % Vigor energy |

Pets: Ember the Fox (−50 % fall damage), Byte the Owl (pings nearby shooters), Pebble the Turtle (vest repair),
Dash the Ferret (larger pickup range).

## Vehicles
Jeep (4 seats), Motorbike (2), Buggy (2), Boat (4, water only). Fuel drains while driving; vehicles explode at 0 HP
(120 damage to occupants); running players over deals speed × 4 damage.

## Map — Isle Verdant (4 × 4 km)
Ironhaven City (medium loot, Clash Squad/TDM arena), Fort Basalt military base (high loot), Goldmeadow Farms,
Rustport (port, boats), Frostspine Peaks (mountains), Serpent River. 30 % of matches are at night.

## Storm phases (`ZoneConfig`)

| Phase | Wait | Shrink | Radius | DPS |
|---|---|---|---|---|
| start | 60 s after the plane | – | 2900 m | – |
| 1 | 90 s | 60 s | 1400 m | 1 |
| 2 | 60 s | 50 s | 900 m | 2 |
| 3 | 50 s | 40 s | 550 m | 4 |
| 4 | 45 s | 35 s | 320 m | 6 |
| 5 | 40 s | 30 s | 160 m | 9 |
| 6 | 30 s | 25 s | 70 m | 12 |
| 7 | 20 s | 20 s | 20 m | 16 |
| 8 | 15 s | 15 s | 0 m | 25 |

Airdrops at the start of phases 2, 4 and 6.

## Economy

| Source | Gold | Diamonds |
|---|---|---|
| New account | 5,000 | 50 |
| Match | 25 + 12/kill + 120 win + 40 top 10 (max 400) | – |
| Level up | 200 per level | 10 every 5 levels |
| Daily / weekly missions | 150–1,500 | up to 20 |
| 7-day login | 500 – 2,000 | 10 – 50 |
| Storm Pass (free track) | 100 per level | 10 every 10 levels |
| Purchase (Google Play) | – | 100 – 5,600 packs |

Sinks: characters, pets and cosmetics, guild creation (2,000 Gold), rename (Name Card or 99 Diamonds),
Storm Pass Premium (499 Diamonds), Fortune Drop (20 / 180 Diamonds).

**Fortune Drop odds** (shown in game): Phoenix Wings 1 %, Thunderclap Gilded 2 %, Victory Flip 4 %, 10 Diamonds 13 %,
Double XP Card 20 %, 500 Gold 30 %, 100 Gold 30 %. Duplicate cosmetics convert to 1,000 Gold.
