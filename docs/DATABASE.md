# Database (Supabase Postgres)

Same Supabase project as the old Stock Analyzer. The game schema lives in `backend/db/schema.sql`,
starter content in `backend/db/seed.sql`. Row Level Security is enabled on every table with **no public policies**,
so the anon/public keys can read nothing — only the backend (database password via `DATABASE_URL`) has access.

## Old Stock Analyzer tables

`watchlist`, `alerts`, `price_history` and `signal_history` are no longer used by any code. They are left untouched
so no data is lost; uncomment the `drop table` line at the top of `schema.sql` and run it to delete them.

## Tables

| Group | Table | Purpose |
|---|---|---|
| Accounts | `players` | Profile, level/XP, Gold, Diamonds, BR & CS rank points, region, ban flag, soft-delete |
| | `auth_identities` | Login providers per player (guest device id, Google, Facebook) |
| | `player_settings` | HUD layout, sensitivity, graphics preset (JSON) |
| | `push_tokens` | FCM tokens |
| Catalog | `items` | Every character, pet, skin, outfit, emote, parachute, vehicle skin, currency pack, consumable |
| | `player_items` | What each player owns (the Vault) |
| | `loadouts` | Equipped character, pet, parachute, outfit, weapon skins, emotes |
| Money | `currency_ledger` | Every Gold/Diamond change with reason (audit trail) |
| | `purchases` | Google Play purchases (unique purchase token → credited once) |
| Social | `friendships` | Friend requests and friends |
| | `parties`, `party_members`, `party_invites` | Lobby squads |
| | `guilds`, `guild_members` | Guilds (clans), roles, glory |
| | `chat_messages` | Guild / party text chat (pruned after 30 days) |
| Matches | `game_servers` | Registered dedicated servers, status, heartbeat |
| | `matchmaking_tickets` | Queue entries per player (with party, rank, region) |
| | `matches` | One row per match (mode, ranked, server, day/night, winner) |
| | `match_players` | Per-player results, rewards, anti-cheat flag |
| | `player_stats` | Lifetime stats per mode |
| | `reports` | Player reports and automatic anti-cheat flags |
| Progression | `mission_defs`, `player_missions` | Daily/weekly missions and progress per period |
| | `achievement_defs`, `player_achievements` | Achievements |
| | `pass_seasons`, `pass_rewards`, `player_pass` | Storm Pass free/premium tracks |
| | `spin_events`, `spin_rewards`, `spin_history` | Fortune Drop with public odds |
| | `login_reward_defs`, `player_login_rewards` | 7-day login calendar |
| | `game_events` | Events calendar |
| Compliance | `deletion_requests` | Requests from the public account-deletion page |
| View | `leaderboard_br` | Regional BR ranking (security_invoker, respects RLS) |

## Common admin queries

```sql
-- Ban a cheater
update players set is_banned = true, ban_reason = 'Cheating' where player_tag = '1234567890';

-- Review anti-cheat flags from the last day
select p.nickname, r.details, r.created_at from reports r join players p on p.id = r.reported_id
where r.source = 'anticheat' and r.created_at > now() - interval '1 day' order by r.created_at desc;

-- Grant compensation to everyone
insert into currency_ledger (player_id, currency, delta, reason)
select id, 'diamonds', 50, 'server_maintenance' from players where deleted_at is null;
update players set diamonds = diamonds + 50 where deleted_at is null;

-- Live servers
select region, status, count(*) from game_servers where last_heartbeat > now() - interval '1 minute' group by 1, 2;
```
