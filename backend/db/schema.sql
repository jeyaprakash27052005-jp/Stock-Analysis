-- =====================================================================
-- Stormdrop Royale — database schema (Supabase / Postgres)
-- Run once:  npm run db:setup   (or paste into Supabase → SQL Editor)
-- Safe to re-run: every statement is idempotent.
--
-- Security model (same as before): Row Level Security is ON for every
-- table with NO public policies, so only the backend (which connects
-- with the database password) can read or write. Game clients never
-- talk to the database directly — they go through the API.
-- =====================================================================

create extension if not exists pgcrypto;

-- Old Stock Analyzer tables. Uncomment to remove them once you no longer need the data.
-- drop table if exists watchlist, alerts, price_history, signal_history cascade;

-- ---------------------------------------------------------------- accounts
create table if not exists players (
  id               uuid primary key default gen_random_uuid(),
  player_tag       text unique not null,                 -- public numeric ID shown in game
  nickname         text not null,
  avatar_id        text not null default 'avatar_default',
  banner_id        text not null default 'banner_default',
  region           text not null default 'IND',          -- matchmaking region
  level            int  not null default 1,
  xp               int  not null default 0,
  gold             bigint not null default 0 check (gold >= 0),
  diamonds         bigint not null default 0 check (diamonds >= 0),
  br_rank_points   int  not null default 0,              -- battle royale ranked
  cs_rank_points   int  not null default 0,              -- clash squad ranked
  br_season_best   int  not null default 0,
  guild_id         uuid,
  is_banned        boolean not null default false,
  ban_reason       text,
  created_at       timestamptz not null default now(),
  last_login_at    timestamptz not null default now(),
  deleted_at       timestamptz
);
create index if not exists players_br_rank_idx on players (region, br_rank_points desc) where deleted_at is null;
create index if not exists players_nickname_idx on players (lower(nickname));

-- One player can be linked to several login providers (guest, google, facebook)
create table if not exists auth_identities (
  provider      text not null check (provider in ('guest','google','facebook','apple')),
  provider_uid  text not null,
  player_id     uuid not null references players(id) on delete cascade,
  created_at    timestamptz not null default now(),
  primary key (provider, provider_uid)
);
create index if not exists auth_identities_player_idx on auth_identities (player_id);

create table if not exists player_settings (
  player_id   uuid primary key references players(id) on delete cascade,
  data        jsonb not null default '{}'::jsonb,       -- HUD layout, sensitivity, graphics
  updated_at  timestamptz not null default now()
);

create table if not exists push_tokens (
  token       text primary key,
  player_id   uuid not null references players(id) on delete cascade,
  platform    text not null default 'android',
  updated_at  timestamptz not null default now()
);

-- ---------------------------------------------------------------- catalog & inventory
create table if not exists items (
  id              text primary key,                     -- e.g. char_kael, skin_raptor_neon
  type            text not null check (type in ('character','pet','weapon_skin','outfit','emote',
                                                 'parachute','vehicle_skin','avatar','banner',
                                                 'consumable','currency','pass_xp')),
  name            text not null,
  rarity          text not null default 'common' check (rarity in ('common','rare','epic','legendary','mythic')),
  description     text not null default '',
  price_gold      int,                                  -- null = not buyable with gold
  price_diamonds  int,                                  -- null = not buyable with diamonds
  in_store        boolean not null default true,
  metadata        jsonb not null default '{}'::jsonb,   -- skill id, weapon id for skins, etc.
  sort_order      int not null default 0,
  created_at      timestamptz not null default now()
);

create table if not exists player_items (
  player_id    uuid not null references players(id) on delete cascade,
  item_id      text not null references items(id),
  quantity     int  not null default 1 check (quantity >= 0),
  acquired_at  timestamptz not null default now(),
  expires_at   timestamptz,                             -- null = permanent
  primary key (player_id, item_id)
);

create table if not exists loadouts (
  player_id     uuid primary key references players(id) on delete cascade,
  character_id  text not null default 'char_kael' references items(id),
  pet_id        text references items(id),
  parachute_id  text references items(id),
  outfit        jsonb not null default '{}'::jsonb,     -- slot -> item id
  weapon_skins  jsonb not null default '{}'::jsonb,     -- weapon id -> skin item id
  emotes        jsonb not null default '[]'::jsonb,
  updated_at    timestamptz not null default now()
);

-- Every change to gold/diamonds is written here (audit trail, refunds, fraud checks)
create table if not exists currency_ledger (
  id          bigserial primary key,
  player_id   uuid not null references players(id) on delete cascade,
  currency    text not null check (currency in ('gold','diamonds')),
  delta       bigint not null,
  reason      text not null,
  ref         text,
  created_at  timestamptz not null default now()
);
create index if not exists currency_ledger_player_idx on currency_ledger (player_id, created_at desc);

-- Real-money purchases (Google Play / App Store)
create table if not exists purchases (
  id              uuid primary key default gen_random_uuid(),
  player_id       uuid not null references players(id) on delete cascade,
  platform        text not null check (platform in ('google_play','app_store')),
  product_id      text not null,
  order_id        text,
  purchase_token  text not null unique,
  diamonds        int  not null default 0,
  status          text not null default 'credited' check (status in ('credited','refunded','rejected')),
  raw             jsonb,
  created_at      timestamptz not null default now()
);

-- ---------------------------------------------------------------- social
create table if not exists friendships (
  player_id   uuid not null references players(id) on delete cascade,
  friend_id   uuid not null references players(id) on delete cascade,
  status      text not null check (status in ('pending','accepted')),   -- pending = player_id asked friend_id
  created_at  timestamptz not null default now(),
  primary key (player_id, friend_id),
  check (player_id <> friend_id)
);

create table if not exists parties (
  id          uuid primary key default gen_random_uuid(),
  leader_id   uuid not null references players(id) on delete cascade,
  mode        text not null default 'br_squad',
  created_at  timestamptz not null default now()
);
create table if not exists party_members (
  player_id   uuid primary key references players(id) on delete cascade,   -- a player is in at most one party
  party_id    uuid not null references parties(id) on delete cascade,
  joined_at   timestamptz not null default now()
);
create table if not exists party_invites (
  party_id    uuid not null references parties(id) on delete cascade,
  player_id   uuid not null references players(id) on delete cascade,
  invited_by  uuid not null references players(id) on delete cascade,
  created_at  timestamptz not null default now(),
  primary key (party_id, player_id)
);

create table if not exists guilds (
  id           uuid primary key default gen_random_uuid(),
  name         text not null unique,
  tag          text not null unique check (char_length(tag) between 2 and 5),
  description  text not null default '',
  leader_id    uuid not null references players(id),
  level        int  not null default 1,
  glory        bigint not null default 0,
  max_members  int  not null default 30,
  min_level    int  not null default 1,
  created_at   timestamptz not null default now()
);
create table if not exists guild_members (
  player_id   uuid primary key references players(id) on delete cascade,
  guild_id    uuid not null references guilds(id) on delete cascade,
  role        text not null default 'member' check (role in ('leader','officer','member')),
  joined_at   timestamptz not null default now()
);
create index if not exists guild_members_guild_idx on guild_members (guild_id);

-- Quick-chat and text messages (party / guild). Voice goes through the voice service, not the DB.
create table if not exists chat_messages (
  id          bigserial primary key,
  channel     text not null,                -- 'guild:<id>' or 'party:<id>'
  player_id   uuid not null references players(id) on delete cascade,
  body        text not null check (char_length(body) <= 200),
  created_at  timestamptz not null default now()
);
create index if not exists chat_messages_channel_idx on chat_messages (channel, id desc);

-- ---------------------------------------------------------------- matchmaking & matches
create table if not exists game_servers (
  id               uuid primary key default gen_random_uuid(),
  region           text not null,
  host             text not null,
  port             int  not null,
  version          text not null default '1.0.0',
  status           text not null default 'ready' check (status in ('ready','allocated','in_match','offline')),
  current_match_id uuid,
  max_players      int  not null default 50,
  last_heartbeat   timestamptz not null default now(),
  unique (host, port)
);

create table if not exists matches (
  id           uuid primary key default gen_random_uuid(),
  mode         text not null check (mode in ('br_solo','br_duo','br_squad','clash_squad','tdm','training')),
  region       text not null,
  is_ranked    boolean not null default false,
  map_id       text not null default 'isle_verdant',
  time_of_day  text not null default 'day' check (time_of_day in ('day','night')),
  server_id    uuid references game_servers(id) on delete set null,
  status       text not null default 'allocated' check (status in ('allocated','running','finished','abandoned')),
  winner_team  int,
  created_at   timestamptz not null default now(),
  started_at   timestamptz,
  ended_at     timestamptz
);

create table if not exists matchmaking_tickets (
  id           uuid primary key default gen_random_uuid(),
  player_id    uuid not null references players(id) on delete cascade,
  party_id     uuid,
  mode         text not null,
  region       text not null,
  is_ranked    boolean not null default false,
  rank_points  int  not null default 0,
  status       text not null default 'waiting' check (status in ('waiting','matched','cancelled','expired')),
  match_id     uuid references matches(id) on delete set null,
  team         int,
  created_at   timestamptz not null default now(),
  updated_at   timestamptz not null default now()
);
create index if not exists mm_waiting_idx on matchmaking_tickets (mode, region, is_ranked, created_at) where status = 'waiting';
create unique index if not exists mm_one_active_ticket on matchmaking_tickets (player_id) where status = 'waiting';

create table if not exists match_players (
  match_id          uuid not null references matches(id) on delete cascade,
  player_id         uuid not null references players(id) on delete cascade,
  team              int  not null,
  character_id      text,
  placement         int,
  kills             int not null default 0,
  assists           int not null default 0,
  damage            int not null default 0,
  headshots         int not null default 0,
  revives           int not null default 0,
  survival_seconds  int not null default 0,
  rank_delta        int not null default 0,
  xp_gained         int not null default 0,
  gold_gained       int not null default 0,
  suspicious        boolean not null default false,  -- flagged by server-side anti-cheat
  primary key (match_id, player_id)
);
create index if not exists match_players_player_idx on match_players (player_id);

create table if not exists player_stats (
  player_id   uuid not null references players(id) on delete cascade,
  mode        text not null,
  matches     int not null default 0,
  wins        int not null default 0,
  top10       int not null default 0,
  kills       int not null default 0,
  deaths      int not null default 0,
  damage      bigint not null default 0,
  headshots   int not null default 0,
  primary key (player_id, mode)
);

create table if not exists reports (
  id           uuid primary key default gen_random_uuid(),
  reporter_id  uuid references players(id) on delete set null,
  reported_id  uuid not null references players(id) on delete cascade,
  match_id     uuid references matches(id) on delete set null,
  reason       text not null check (reason in ('cheating','teaming','abuse','name','afk','other')),
  details      text,
  source       text not null default 'player' check (source in ('player','anticheat')),
  status       text not null default 'open' check (status in ('open','reviewed','actioned','dismissed')),
  created_at   timestamptz not null default now()
);

-- ---------------------------------------------------------------- progression
create table if not exists mission_defs (
  id              text primary key,
  period          text not null check (period in ('daily','weekly')),
  title           text not null,
  metric          text not null check (metric in ('matches','kills','damage','top10','wins','revives','headshots','survival_minutes')),
  target          int  not null,
  reward_xp       int  not null default 0,
  reward_gold     int  not null default 0,
  reward_diamonds int  not null default 0,
  reward_pass_xp  int  not null default 0,
  active          boolean not null default true
);
create table if not exists player_missions (
  player_id    uuid not null references players(id) on delete cascade,
  mission_id   text not null references mission_defs(id),
  period_key   text not null,               -- '2026-11-18' for daily, '2026-W47' for weekly
  progress     int  not null default 0,
  claimed_at   timestamptz,
  primary key (player_id, mission_id, period_key)
);

create table if not exists achievement_defs (
  id               text primary key,
  title            text not null,
  metric           text not null,
  target           int  not null,
  reward_gold      int  not null default 0,
  reward_diamonds  int  not null default 0
);
create table if not exists player_achievements (
  player_id       uuid not null references players(id) on delete cascade,
  achievement_id  text not null references achievement_defs(id),
  progress        int  not null default 0,
  completed_at    timestamptz,
  claimed_at      timestamptz,
  primary key (player_id, achievement_id)
);

-- Storm Pass (battle pass): free + premium track
create table if not exists pass_seasons (
  id                      text primary key,
  name                    text not null,
  starts_at               timestamptz not null,
  ends_at                 timestamptz not null,
  premium_price_diamonds  int not null default 499,
  xp_per_level            int not null default 1000,
  max_level               int not null default 50
);
create table if not exists pass_rewards (
  season_id  text not null references pass_seasons(id) on delete cascade,
  level      int  not null,
  track      text not null check (track in ('free','premium')),
  item_id    text not null references items(id),
  quantity   int  not null default 1,
  primary key (season_id, level, track)
);
create table if not exists player_pass (
  player_id   uuid not null references players(id) on delete cascade,
  season_id   text not null references pass_seasons(id) on delete cascade,
  pass_xp     int  not null default 0,
  premium     boolean not null default false,
  claimed     jsonb not null default '[]'::jsonb,       -- ["1:free","1:premium",...]
  primary key (player_id, season_id)
);

-- Fortune Drop (lucky spin). Odds are public — Google Play requires disclosing them.
create table if not exists spin_events (
  id               text primary key,
  name             text not null,
  starts_at        timestamptz not null,
  ends_at          timestamptz not null,
  cost_single      int not null default 20,      -- diamonds
  cost_ten         int not null default 180,
  active           boolean not null default true
);
create table if not exists spin_rewards (
  event_id   text not null references spin_events(id) on delete cascade,
  item_id    text not null references items(id),
  quantity   int  not null default 1,
  weight     int  not null check (weight > 0),
  primary key (event_id, item_id)
);
create table if not exists spin_history (
  id          bigserial primary key,
  player_id   uuid not null references players(id) on delete cascade,
  event_id    text not null references spin_events(id) on delete cascade,
  item_id     text not null references items(id),
  quantity    int  not null,
  created_at  timestamptz not null default now()
);

create table if not exists login_reward_defs (
  day       int primary key check (day between 1 and 7),
  item_id   text not null references items(id),
  quantity  int  not null default 1
);
create table if not exists player_login_rewards (
  player_id        uuid primary key references players(id) on delete cascade,
  streak_day       int  not null default 0,
  last_claim_date  date
);

create table if not exists game_events (
  id           text primary key,
  title        text not null,
  description  text not null default '',
  type         text not null default 'news' check (type in ('news','double_xp','top_up','mode','spin','tournament')),
  banner_url   text,
  starts_at    timestamptz not null,
  ends_at      timestamptz not null,
  data         jsonb not null default '{}'::jsonb
);

-- Account deletion requests from the public web form (Google Play requirement)
create table if not exists deletion_requests (
  id             uuid primary key default gen_random_uuid(),
  player_tag     text not null,
  contact_email  text not null,
  reason         text,
  status         text not null default 'open' check (status in ('open','done','rejected')),
  created_at     timestamptz not null default now()
);

-- ---------------------------------------------------------------- leaderboard view
create or replace view leaderboard_br as
  select p.id as player_id, p.player_tag, p.nickname, p.avatar_id, p.region, p.level,
         p.br_rank_points, g.tag as guild_tag,
         rank() over (partition by p.region order by p.br_rank_points desc) as region_rank
  from players p
  left join guilds g on g.id = p.guild_id
  where p.deleted_at is null and p.is_banned = false;

-- ---------------------------------------------------------------- row level security
do $$
declare t text;
begin
  foreach t in array array[
    'players','auth_identities','player_settings','push_tokens','items','player_items','loadouts',
    'currency_ledger','purchases','friendships','parties','party_members','party_invites','guilds',
    'guild_members','chat_messages','game_servers','matches','matchmaking_tickets','match_players',
    'player_stats','reports','mission_defs','player_missions','achievement_defs','player_achievements',
    'pass_seasons','pass_rewards','player_pass','spin_events','spin_rewards','spin_history',
    'login_reward_defs','player_login_rewards','game_events','deletion_requests']
  loop
    execute format('alter table %I enable row level security', t);
  end loop;
end $$;

-- The view runs with the caller's rights so RLS still applies through it
alter view leaderboard_br set (security_invoker = true);
