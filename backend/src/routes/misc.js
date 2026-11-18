import { Router } from "express";
import { config } from "../config.js";
import { HttpError, one, query } from "../db.js";
import { requireCron, requirePlayer } from "../lib/auth.js";
import { rankInfo } from "../lib/ranks.js";

const r = Router();

r.get("/health", async (_req, res) => {
  let database = "not configured";
  try { await one("select 1"); database = "connected"; } catch (e) { database = `error: ${e.message}`; }
  res.json({ status: "ok", service: "stormdrop-backend", database });
});

r.get("/leaderboard", async (req, res) => {
  const region = config.regions.includes(req.query.region) ? req.query.region : "IND";
  const rows = (await query(
    "select player_tag, nickname, avatar_id, level, br_rank_points, guild_tag, region_rank from leaderboard_br where region=$1 order by region_rank limit 100",
    [region])).rows;
  res.set("Cache-Control", "public, max-age=60");
  res.json({ region, players: rows.map((p) => ({ ...p, rank: rankInfo(p.br_rank_points) })) });
});

r.get("/leaderboard/guilds", async (_req, res) => {
  const rows = (await query(
    `select g.name, g.tag, g.level, g.glory, (select count(*)::int from guild_members m where m.guild_id=g.id) members
     from guilds g order by glory desc limit 100`)).rows;
  res.json({ guilds: rows });
});

r.get("/match-history", requirePlayer, async (req, res) => {
  const rows = (await query(
    `select m.id, m.mode, m.is_ranked, m.ended_at, mp.placement, mp.kills, mp.damage, mp.rank_delta, mp.xp_gained, mp.gold_gained
     from match_players mp join matches m on m.id = mp.match_id where mp.player_id=$1 order by m.ended_at desc nulls last limit 20`,
    [req.playerId])).rows;
  res.json({ matches: rows });
});

r.post("/reports", requirePlayer, async (req, res) => {
  const { playerTag, matchId, reason, details } = req.body;
  const target = await one("select id from players where player_tag=$1", [String(playerTag || "")]);
  if (!target) throw new HttpError(404, "Player not found");
  await query("insert into reports (reporter_id, reported_id, match_id, reason, details) values ($1,$2,$3,$4,$5)",
    [req.playerId, target.id, matchId || null, reason, String(details || "").slice(0, 500)]);
  res.json({ ok: true });
});

/** Public account-deletion request (linked from the Play Store listing / website). */
r.post("/deletion-request", async (req, res) => {
  const tag = String(req.body.playerTag || "").trim(), email = String(req.body.email || "").trim();
  if (!/^\d{6,12}$/.test(tag) || !/^\S+@\S+\.\S+$/.test(email)) throw new HttpError(400, "Enter your Player ID and a valid email");
  await query("insert into deletion_requests (player_tag, contact_email, reason) values ($1,$2,$3)",
    [tag, email, String(req.body.reason || "").slice(0, 500)]);
  res.json({ ok: true });
});

/** Daily housekeeping (Vercel Cron). */
r.get("/cron/cleanup", requireCron, async (_req, res) => {
  const out = {};
  out.tickets = (await query("update matchmaking_tickets set status='expired' where status='waiting' and updated_at < now() - interval '10 minutes'")).rowCount;
  out.oldTickets = (await query("delete from matchmaking_tickets where created_at < now() - interval '7 days'")).rowCount;
  out.servers = (await query("update game_servers set status='offline' where last_heartbeat < now() - interval '2 minutes' and status <> 'offline'")).rowCount;
  out.matches = (await query("update matches set status='abandoned' where status in ('allocated','running') and created_at < now() - interval '2 hours'")).rowCount;
  out.invites = (await query("delete from party_invites where created_at < now() - interval '1 day'")).rowCount;
  out.chat = (await query("delete from chat_messages where created_at < now() - interval '30 days'")).rowCount;
  res.json({ ok: true, ...out });
});

export default r;
