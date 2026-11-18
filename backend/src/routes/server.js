// Endpoints used only by dedicated game servers (authenticated with X-Server-Secret).
import { Router } from "express";
import { config } from "../config.js";
import { HttpError, one, query, tx } from "../db.js";
import { requireServer } from "../lib/auth.js";
import { addPassXp, addXp, changeCurrency } from "../lib/economy.js";
import { periodKey } from "../lib/periods.js";
import { brRankDelta, csRankDelta } from "../lib/ranks.js";

const r = Router();
r.use(requireServer);

r.post("/register", async (req, res) => {
  const { region, host, port, version, maxPlayers } = req.body;
  if (!config.regions.includes(region) || !host || !port) throw new HttpError(400, "region, host, port required");
  const s = await one(
    `insert into game_servers (region, host, port, version, max_players, status, last_heartbeat, current_match_id)
     values ($1,$2,$3,$4,$5,'ready',now(),null)
     on conflict (host, port) do update set region=excluded.region, version=excluded.version, max_players=excluded.max_players,
       status='ready', last_heartbeat=now(), current_match_id=null
     returning id`, [region, host, Number(port), version || "1.0.0", Number(maxPlayers) || 50]);
  res.json({ serverId: s.id });
});

/** Heartbeat. Returns the match this server has been allocated (if any) so it can prepare. */
r.post("/heartbeat", async (req, res) => {
  const s = await one("update game_servers set last_heartbeat=now() where id=$1 returning *", [req.body.serverId]);
  if (!s) throw new HttpError(404, "Unknown server, register again");
  let allocation = null;
  if (s.current_match_id) {
    const m = await one("select * from matches where id=$1", [s.current_match_id]);
    const players = (await query(
      "select player_id, team from matchmaking_tickets where match_id=$1 and status='matched'", [m.id])).rows;
    allocation = { matchId: m.id, mode: m.mode, isRanked: m.is_ranked, mapId: m.map_id, timeOfDay: m.time_of_day, players };
  }
  res.json({ status: s.status, allocation });
});

r.post("/match-start", async (req, res) => {
  await query("update matches set status='running', started_at=now() where id=$1", [req.body.matchId]);
  await query("update game_servers set status='in_match' where current_match_id=$1", [req.body.matchId]);
  res.json({ ok: true });
});

const METRICS = ["matches", "kills", "damage", "top10", "wins", "revives", "headshots", "survival_minutes"];

/**
 * Final results from the authoritative server. Rewards, rank points, stats, missions and
 * achievements are all calculated here — never trusted from a game client.
 */
r.post("/match-result", async (req, res) => {
  const { matchId, winnerTeam, players } = req.body;
  if (!Array.isArray(players)) throw new HttpError(400, "players[] required");
  const summary = await tx(async (c) => {
    const m = (await c.query("select * from matches where id=$1 for update", [matchId])).rows[0];
    if (!m) throw new HttpError(404, "Unknown match");
    if (m.status === "finished") throw new HttpError(409, "Result already submitted");
    const expected = new Set((await c.query("select player_id from matchmaking_tickets where match_id=$1", [matchId])).rows.map((x) => x.player_id));
    const totalTeams = new Set(players.map((p) => p.team)).size;
    const isBR = m.mode.startsWith("br_");
    const doubleXp = (await c.query("select 1 from game_events where type='double_xp' and now() between starts_at and ends_at")).rowCount > 0;
    const out = [];

    for (const p of players) {
      if (!expected.has(p.playerId)) continue;   // ignore anyone the backend didn't send to this server
      const kills = clamp(p.kills, 0, 60), damage = clamp(p.damage, 0, 20000), headshots = clamp(p.headshots, 0, kills);
      const placement = clamp(p.placement, 1, 60), revives = clamp(p.revives, 0, 50), assists = clamp(p.assists, 0, 60);
      const survival = clamp(p.survivalSeconds, 0, 3600);
      const won = isBR ? placement === 1 : p.team === winnerTeam;
      const suspicious = !!p.suspicious;

      let xp = 60 + kills * 25 + Math.floor(survival / 60) * 12 + (isBR ? Math.max(0, 11 - placement) * 15 : won ? 100 : 30);
      let gold = Math.min(400, 25 + kills * 12 + (won ? 120 : 0) + (isBR && placement <= 10 ? 40 : 0));
      if (doubleXp) xp *= 2;
      let rankDelta = 0;
      if (m.is_ranked) {
        const pl = (await c.query("select br_rank_points, cs_rank_points from players where id=$1", [p.playerId])).rows[0];
        rankDelta = isBR
          ? brRankDelta({ points: pl.br_rank_points, placement, totalTeams, kills, assists })
          : csRankDelta({ points: pl.cs_rank_points, won, kills });
      }
      if (suspicious) { xp = 0; gold = 0; rankDelta = Math.min(0, rankDelta); }

      await c.query(
        `insert into match_players (match_id, player_id, team, character_id, placement, kills, assists, damage, headshots,
           revives, survival_seconds, rank_delta, xp_gained, gold_gained, suspicious)
         values ($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,$15) on conflict do nothing`,
        [matchId, p.playerId, p.team, p.characterId || null, placement, kills, assists, damage, headshots, revives, survival,
         rankDelta, xp, gold, suspicious]);

      if (rankDelta) {
        const col = isBR ? "br_rank_points" : "cs_rank_points";
        await c.query(`update players set ${col} = greatest(0, ${col} + $2),
                       br_season_best = greatest(br_season_best, br_rank_points + case when $3 then $2 else 0 end) where id=$1`,
          [p.playerId, rankDelta, isBR]);
      }
      if (gold) await changeCurrency(c, p.playerId, "gold", gold, "match", matchId);
      const lvl = xp ? await addXp(c, p.playerId, xp) : { levelsGained: [] };
      if (!suspicious) await addPassXp(c, p.playerId, 100 + kills * 20);

      await c.query(
        `insert into player_stats (player_id, mode, matches, wins, top10, kills, deaths, damage, headshots)
         values ($1,$2,1,$3,$4,$5,$6,$7,$8)
         on conflict (player_id, mode) do update set matches=player_stats.matches+1, wins=player_stats.wins+excluded.wins,
           top10=player_stats.top10+excluded.top10, kills=player_stats.kills+excluded.kills, deaths=player_stats.deaths+excluded.deaths,
           damage=player_stats.damage+excluded.damage, headshots=player_stats.headshots+excluded.headshots`,
        [p.playerId, m.mode, won ? 1 : 0, isBR && placement <= 10 ? 1 : 0, kills, won ? 0 : 1, damage, headshots]);

      if (!suspicious && m.mode !== "training") {
        const values = { matches: 1, kills, damage, top10: isBR && placement <= 10 ? 1 : 0, wins: won ? 1 : 0, revives, headshots,
                         survival_minutes: Math.floor(survival / 60) };
        await c.query(
          `insert into player_missions (player_id, mission_id, period_key, progress)
           select $1, d.id, case d.period when 'daily' then $2 else $3 end, least(d.target, ($4::jsonb ->> d.metric)::int)
           from mission_defs d where d.active and ($4::jsonb ->> d.metric)::int > 0
           on conflict (player_id, mission_id, period_key) do update
             set progress = least((select target from mission_defs where id = excluded.mission_id), player_missions.progress + excluded.progress)`,
          [p.playerId, periodKey("daily"), periodKey("weekly"), JSON.stringify(values)]);
        await c.query(
          `insert into player_achievements (player_id, achievement_id, progress)
           select $1, d.id, ($2::jsonb ->> d.metric)::int from achievement_defs d where ($2::jsonb ->> d.metric)::int > 0
           on conflict (player_id, achievement_id) do update set progress = player_achievements.progress + excluded.progress`,
          [p.playerId, JSON.stringify(values)]);
        await c.query(
          `update player_achievements pa set completed_at = now() from achievement_defs d
           where pa.achievement_id = d.id and pa.player_id = $1 and pa.completed_at is null and pa.progress >= d.target`, [p.playerId]);
      }
      if (suspicious)
        await c.query("insert into reports (reported_id, match_id, reason, details, source) values ($1,$2,'cheating',$3,'anticheat')",
          [p.playerId, matchId, String(p.suspicionReason || "server validation").slice(0, 500)]);

      out.push({ playerId: p.playerId, xp, gold, rankDelta, levelsGained: lvl.levelsGained });
    }

    // Guild glory from wins/kills
    await c.query(
      `update guilds g set glory = glory + x.pts from (
         select pl.guild_id, sum(mp.kills * 2 + case when mp.placement = 1 then 20 else 0 end) pts
         from match_players mp join players pl on pl.id = mp.player_id
         where mp.match_id = $1 and pl.guild_id is not null and not mp.suspicious group by pl.guild_id) x
       where g.id = x.guild_id`, [matchId]);
    await c.query("update matches set status='finished', ended_at=now(), winner_team=$2 where id=$1", [matchId, winnerTeam ?? null]);
    await c.query("update game_servers set status='ready', current_match_id=null where current_match_id=$1", [matchId]);
    return out;
  });
  res.json({ ok: true, players: summary });
});

r.post("/match-abandon", async (req, res) => {
  await query("update matches set status='abandoned', ended_at=now() where id=$1 and status <> 'finished'", [req.body.matchId]);
  await query("update game_servers set status='ready', current_match_id=null where current_match_id=$1", [req.body.matchId]);
  res.json({ ok: true });
});

function clamp(v, lo, hi) {
  const n = Math.floor(Number(v) || 0);
  return Math.max(lo, Math.min(hi, n));
}

export default r;
