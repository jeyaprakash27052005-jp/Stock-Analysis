-- =====================================================================
-- Stormdrop Royale — starter content. Safe to re-run (upserts).
-- Character/pet ids here must match CharacterData/PetData ids in Unity.
-- =====================================================================

-- ---------------------------------------------------------------- characters (12)
insert into items (id, type, name, rarity, description, price_gold, price_diamonds, metadata, sort_order) values
 ('char_kael',  'character','Kael',  'common',   'Active – Sprint Surge: +30% move speed for 6 s (CD 45 s).',            null, null, '{"skill":"sprint_surge","default":true}', 1),
 ('char_mira',  'character','Mira',  'rare',     'Passive – Field Medic: healing items work 30% faster and heal +15%.',  8000,  399, '{"skill":"field_medic"}', 2),
 ('char_torvin','character','Torvin','epic',     'Active – Bastion Dome: shield absorbing 120 damage for 5 s (CD 60 s).',null,  499, '{"skill":"bastion_dome"}', 3),
 ('char_zara',  'character','Zara',  'rare',     'Passive – Ghost Step: footsteps are silent while crouched or prone.',  8000,  399, '{"skill":"ghost_step"}', 4),
 ('char_okoro', 'character','Okoro', 'epic',     'Active – Healing Aura: heals you and allies within 5 m for 6 HP/s, 5 s (CD 50 s).', null, 499, '{"skill":"healing_aura"}', 5),
 ('char_lyra',  'character','Lyra',  'rare',     'Passive – Eagle Eye: enemies you hit are marked for your squad for 3 s.', 8000, 399, '{"skill":"eagle_eye"}', 6),
 ('char_dante', 'character','Dante', 'rare',     'Passive – Iron Skin: take 8% less damage from bullets.',                8000,  399, '{"skill":"iron_skin"}', 7),
 ('char_suki',  'character','Suki',  'epic',     'Active – Blink Dash: dash 7 m in the move direction (CD 30 s).',        null,  499, '{"skill":"blink_dash"}', 8),
 ('char_rook',  'character','Rook',  'common',   'Passive – Scavenger: knocking an enemy restores 25 HP.',                6000,  299, '{"skill":"scavenger"}', 9),
 ('char_nova',  'character','Nova',  'legendary','Active – Pulse Scan: reveals enemies within 50 m for 3 s (CD 75 s).',  null,  699, '{"skill":"pulse_scan"}', 10),
 ('char_bram',  'character','Bram',  'common',   'Passive – Quick Hands: reload 20% faster.',                             6000,  299, '{"skill":"quick_hands"}', 11),
 ('char_ivy',   'character','Ivy',   'rare',     'Passive – Vigor Bloom: Vigor Shrooms give 50% more energy.',           8000,  399, '{"skill":"vigor_bloom"}', 12)
on conflict (id) do update set name=excluded.name, rarity=excluded.rarity, description=excluded.description,
  price_gold=excluded.price_gold, price_diamonds=excluded.price_diamonds, metadata=excluded.metadata, sort_order=excluded.sort_order;

-- ---------------------------------------------------------------- pets (4)
insert into items (id, type, name, rarity, description, price_gold, price_diamonds, metadata, sort_order) values
 ('pet_ember', 'pet','Ember the Fox',     'rare', 'Takes 50% less fall and landing damage.',            null, 299, '{"buff":"soft_landing"}', 20),
 ('pet_byte',  'pet','Byte the Owl',      'epic', 'Pings enemies who shoot within 30 m of you.',        null, 499, '{"buff":"gunfire_ping"}', 21),
 ('pet_pebble','pet','Pebble the Turtle', 'rare', 'Repairs 1 vest durability every 4 s out of combat.', null, 299, '{"buff":"armor_mend"}', 22),
 ('pet_dash',  'pet','Dash the Ferret',   'common','Loot pickup and door use 20% faster.',              5000, 199, '{"buff":"quick_paws"}', 23)
on conflict (id) do update set name=excluded.name, description=excluded.description, price_gold=excluded.price_gold,
  price_diamonds=excluded.price_diamonds, metadata=excluded.metadata;

-- ---------------------------------------------------------------- cosmetics
insert into items (id, type, name, rarity, description, price_gold, price_diamonds, metadata, sort_order) values
 ('skin_raptor_neon',   'weapon_skin','Raptor-7 Neon Pulse',     'epic',     'Glowing AR finish.',              null, 299, '{"weapon":"ar_raptor7"}', 30),
 ('skin_wasp_tiger',    'weapon_skin','Wasp SMG Tiger Stripe',   'rare',     'Orange camo for the Wasp.',       12000, 199, '{"weapon":"smg_wasp"}', 31),
 ('skin_longreach_frost','weapon_skin','Longreach Frostbite',    'legendary','Ice-crystal sniper skin.',        null, 599, '{"weapon":"sr_longreach"}', 32),
 ('skin_thunder_gold',  'weapon_skin','Thunderclap Gilded',      'epic',     'Gold-plated shotgun.',            null, 349, '{"weapon":"sg_thunderclap"}', 33),
 ('outfit_desert_ops',  'outfit','Desert Ops Set',              'rare',     'Sand-coloured tactical outfit.',   15000, 249, '{"slot":"full"}', 40),
 ('outfit_neon_runner', 'outfit','Neon Runner Set',             'legendary','Light-strip streetwear.',          null, 799, '{"slot":"full"}', 41),
 ('outfit_cap_storm',   'outfit','Storm Cap',                   'common',   'Basic cap.',                       3000, null, '{"slot":"head"}', 42),
 ('emote_victory_flip', 'emote','Victory Flip',                 'epic',     'Backflip celebration.',            null, 299, '{}', 50),
 ('emote_salute',       'emote','Salute',                       'common',   'Classic salute.',                  4000, 99, '{}', 51),
 ('emote_dance_wave',   'emote','Wave Dance',                   'rare',     'Smooth wave dance.',               null, 199, '{}', 52),
 ('chute_phoenix',      'parachute','Phoenix Wings',            'legendary','Fiery parachute trail.',           null, 499, '{}', 60),
 ('chute_classic_blue', 'parachute','Classic Blue',             'common',   'Simple blue canopy.',              5000, null, '{}', 61),
 ('veh_jeep_jungle',    'vehicle_skin','Jungle Jeep',           'rare',     'Camo jeep paint.',                 null, 249, '{"vehicle":"jeep"}', 70),
 ('veh_bike_chrome',    'vehicle_skin','Chrome Bike',           'epic',     'Mirror-finish motorbike.',         null, 349, '{"vehicle":"motorbike"}', 71),
 ('avatar_default',     'avatar','Rookie',                      'common',   '',                                 null, null, '{"default":true}', 80),
 ('banner_default',     'banner','Storm Grey',                  'common',   '',                                 null, null, '{"default":true}', 81)
on conflict (id) do update set name=excluded.name, rarity=excluded.rarity, description=excluded.description,
  price_gold=excluded.price_gold, price_diamonds=excluded.price_diamonds, metadata=excluded.metadata;

-- ---------------------------------------------------------------- currencies / consumables (rewards only)
insert into items (id, type, name, rarity, description, in_store, metadata, sort_order) values
 ('cur_gold_100',     'currency','100 Gold',        'common','', false, '{"currency":"gold","amount":100}', 90),
 ('cur_gold_500',     'currency','500 Gold',        'common','', false, '{"currency":"gold","amount":500}', 91),
 ('cur_gold_2000',    'currency','2,000 Gold',      'rare',  '', false, '{"currency":"gold","amount":2000}', 92),
 ('cur_diamonds_10',  'currency','10 Diamonds',     'rare',  '', false, '{"currency":"diamonds","amount":10}', 93),
 ('cur_diamonds_50',  'currency','50 Diamonds',     'epic',  '', false, '{"currency":"diamonds","amount":50}', 94),
 ('card_xp_double',   'consumable','Double XP Card (1 h)', 'rare','', false, '{"effect":"double_xp","minutes":60}', 95),
 ('card_name_change', 'consumable','Name Change Card','rare','', true,  '{"effect":"rename"}', 96),
 ('pass_xp_500',      'pass_xp','500 Pass XP',      'common','', false, '{"amount":500}', 97)
on conflict (id) do nothing;
update items set price_diamonds = 99 where id = 'card_name_change';

-- ---------------------------------------------------------------- missions
insert into mission_defs (id, period, title, metric, target, reward_xp, reward_gold, reward_diamonds, reward_pass_xp) values
 ('d_play_3',      'daily', 'Play 3 matches',                 'matches',          3,   300, 200, 0, 200),
 ('d_kills_5',     'daily', 'Eliminate 5 enemies',            'kills',            5,   300, 250, 0, 200),
 ('d_damage_1000', 'daily', 'Deal 1,000 damage',              'damage',        1000,   300, 250, 0, 200),
 ('d_survive_20',  'daily', 'Survive 20 minutes in total',    'survival_minutes',20,   200, 150, 0, 150),
 ('w_top10_10',    'weekly','Finish Top 10 ten times',         'top10',           10,  1500,1000, 10, 800),
 ('w_win_2',       'weekly','Become Storm Champion twice',     'wins',             2,  2000,1500, 20,1000),
 ('w_revive_10',   'weekly','Revive teammates 10 times',       'revives',         10,  1000, 800,  0, 600),
 ('w_head_25',     'weekly','Land 25 headshot eliminations',   'headshots',       25,  1500,1000, 10, 800)
on conflict (id) do update set title=excluded.title, target=excluded.target, reward_xp=excluded.reward_xp,
  reward_gold=excluded.reward_gold, reward_diamonds=excluded.reward_diamonds, reward_pass_xp=excluded.reward_pass_xp;

insert into achievement_defs (id, title, metric, target, reward_gold, reward_diamonds) values
 ('a_first_win',  'First Storm Champion', 'wins',     1,   1000, 20),
 ('a_kills_100',  'Centurion',            'kills',  100,   2000, 30),
 ('a_kills_1000', 'Storm Legend',         'kills', 1000,  10000, 100),
 ('a_matches_50', 'Veteran Dropper',      'matches', 50,   2000, 20),
 ('a_revive_50',  'Guardian Angel',       'revives', 50,   3000, 30)
on conflict (id) do nothing;

-- ---------------------------------------------------------------- Storm Pass season 1
insert into pass_seasons (id, name, starts_at, ends_at, premium_price_diamonds, xp_per_level, max_level)
values ('s1', 'Season 1 – First Landing', '2026-11-01', '2027-01-31', 499, 1000, 50)
on conflict (id) do nothing;

insert into pass_rewards (season_id, level, track, item_id, quantity)
select 's1', lvl, 'free',
       case when lvl % 10 = 0 then 'cur_diamonds_10' when lvl % 5 = 0 then 'card_xp_double' else 'cur_gold_100' end, 1
from generate_series(1, 50) as lvl
on conflict do nothing;

insert into pass_rewards (season_id, level, track, item_id, quantity)
select 's1', lvl, 'premium',
       case lvl when 1 then 'outfit_neon_runner' when 15 then 'skin_raptor_neon' when 30 then 'chute_phoenix'
                when 50 then 'skin_longreach_frost'
                else case when lvl % 4 = 0 then 'cur_diamonds_10' else 'cur_gold_500' end end, 1
from generate_series(1, 50) as lvl
on conflict do nothing;

-- ---------------------------------------------------------------- Fortune Drop (odds shown in game)
insert into spin_events (id, name, starts_at, ends_at, cost_single, cost_ten)
values ('fd_phoenix', 'Phoenix Fortune Drop', '2026-11-01', '2027-01-31', 20, 180)
on conflict (id) do nothing;

insert into spin_rewards (event_id, item_id, quantity, weight) values
 ('fd_phoenix','chute_phoenix',      1,   10),   -- 1.0%
 ('fd_phoenix','skin_thunder_gold',  1,   20),   -- 2.0%
 ('fd_phoenix','emote_victory_flip', 1,   40),   -- 4.0%
 ('fd_phoenix','cur_diamonds_10',    1,  130),   -- 13.0%
 ('fd_phoenix','card_xp_double',     1,  200),   -- 20.0%
 ('fd_phoenix','cur_gold_500',       1,  300),   -- 30.0%
 ('fd_phoenix','cur_gold_100',       1,  300)    -- 30.0%
on conflict do nothing;

-- ---------------------------------------------------------------- 7-day login rewards
insert into login_reward_defs (day, item_id, quantity) values
 (1,'cur_gold_500',1),(2,'card_xp_double',1),(3,'cur_gold_500',1),(4,'cur_diamonds_10',1),
 (5,'cur_gold_2000',1),(6,'emote_salute',1),(7,'cur_diamonds_50',1)
on conflict (day) do update set item_id=excluded.item_id, quantity=excluded.quantity;

-- ---------------------------------------------------------------- events calendar
insert into game_events (id, title, description, type, starts_at, ends_at) values
 ('ev_launch',     'Launch Celebration', 'Log in for 7 days to claim 50 Diamonds.',   'news',      '2026-11-01','2026-12-31'),
 ('ev_double_xp',  'Double XP Weekend',   'All matches give 2x XP.',                  'double_xp', '2026-11-28','2026-11-30'),
 ('ev_night_mode', 'Night Raid',          'Night-time Isle Verdant in Squad mode.',   'mode',      '2026-12-05','2026-12-12')
on conflict (id) do nothing;
