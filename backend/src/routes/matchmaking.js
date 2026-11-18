import { Router } from "express";
import { config } from "../config.js";
import { HttpError, one, query, tx } from "../db.js";
import { requirePlayer, signJoinTicket } from "../lib/auth.js";

const r = Router();
r.use(requirePlayer);

const MIN_OVERRIDE = Number(process.env.MM_MIN_PLAYERS || 0);   // set to 1 or 2 while testing

/**
 * Start matchmaking. A party leader queues the whole party; everyone gets a ticket.
 * Training mode never touches a server — the client runs it offline.
 */
r.post("/join", async (req, res) => {
  const { mode, ranked } = req.body;
  const cfg = config.modes[mode];
  if (!cfg) throw new HttpError(400, "Unknown mode");
  if (mode === "training") return res.json({ status: "offline" });
  const isRanked = !!ranked && ["br_solo", "br_duo", "br_squad", "clash_squad"].includes(mode);

  const ticket = await tx(async (c) => {
    const me = (await c.query("select region, br_rank_points, cs_rank_points, level from players where id=$1", [req.playerId])).rows[0];
    if (isRanked && me.level < 6) throw new HttpError(403, "Ranked unlocks at level 6");
    const party = (await c.query(
      "select pa.id, pa.leader_id from party_members pm join parties pa on pa.id=pm.party_id where pm.player_id=$1", [req.playerId])).rows[0];
    if (party && party.leader_id !== req.playerId) throw new HttpError(403, "Only the party leader can start");
    const members = party
      ? (await c.query("select player_id from party_members where party_id=$1", [party.id])).rows.map((m) => m.player_id)
      : [req.playerId];
    if (members.length > cfg.teamSize) throw new HttpError(400, `Party is too big for this mode (max ${cfg.teamSize})`);
    await c.query("update matchmaking_tickets set status='cancelled', updated_at=now() where player_id = any($1) and status='waiting'", [members]);
    const rp = mode === "clash_squad" ? me.cs_rank_points : me.br_rank_points;
    let mine;
    for (const pid of members) {
      const t = (await c.query(
        `insert into matchmaking_tickets (player_id, party_id, mode, region, is_ranked, rank_points)
         values ($1,$2,$3,$4,$5,$6) returning *`, [pid, party?.id || null, mode, me.region, isRanked, rp])).rows[0];
      if (pid === req.playerId) mine = t;
    }
    return mine;
  });
  res.json({ status: "waiting", ticketId: ticket.id });
});

/** Your current ticket (party members poll this after the leader starts). */
r.get("/current", async (req, res) => {
  const t = await one(
    `select * from matchmaking_tickets where player_id=$1
       and ((status='waiting' and updated_at > now() - interval '5 minutes')
         or (status='matched' and updated_at > now() - interval '60 seconds'))
     order by created_at desc limit 1`,
    [req.playerId]);
  res.json(t ? await ticketStatus(t, req.playerId) : { status: "none" });
});

r.get("/:ticketId", async (req, res) => {
  const t = await one("select * from matchmaking_tickets where id=$1 and player_id=$2", [req.params.ticketId, req.playerId]);
  if (!t) throw new HttpError(404, "Ticket not found");
  res.json(await ticketStatus(t, req.playerId));
});

r.delete("/:ticketId", async (req, res) => {
  const t = await one("select party_id from matchmaking_tickets where id=$1 and player_id=$2 and status='waiting'", [req.params.ticketId, req.playerId]);
  if (t) {
    // Cancelling cancels for the whole party
    await query(`update matchmaking_tickets set status='cancelled', updated_at=now()
                 where status='waiting' and (id=$1 or (party_id is not null and party_id=$2))`, [req.params.ticketId, t.party_id]);
  }
  res.json({ ok: true });
});

async function ticketStatus(t, playerId) {
  if (t.status === "waiting") {
    await query("update matchmaking_tickets set updated_at=now() where id=$1", [t.id]);
    await tryFormMatch(t.mode, t.region, t.is_ranked);
    t = await one("select * from matchmaking_tickets where id=$1", [t.id]);
  }
  const waitedSec = Math.round((Date.now() - new Date(t.created_at).getTime()) / 1000);
  if (t.status !== "matched") {
    const queued = await one("select count(*)::int n from matchmaking_tickets where status='waiting' and mode=$1 and region=$2", [t.mode, t.region]);
    return { status: t.status, ticketId: t.id, waitedSec, playersInQueue: queued.n };
  }
  const m = await one(
    `select m.id, m.mode, m.map_id, m.time_of_day, s.host, s.port from matches m join game_servers s on s.id = m.server_id where m.id=$1`, [t.match_id]);
  const p = await one(
    "select p.nickname, l.character_id, l.pet_id from players p left join loadouts l on l.player_id=p.id where p.id=$1", [playerId]);
  return {
    status: "matched", ticketId: t.id, waitedSec,
    match: { id: m.id, mode: m.mode, mapId: m.map_id, timeOfDay: m.time_of_day, host: m.host, port: m.port, team: t.team },
    joinTicket: signJoinTicket({ playerId, matchId: m.id, team: t.team, nickname: p.nickname, characterId: p.character_id, petId: p.pet_id }),
  };
}

/** Groups waiting tickets into a match when the lobby is full or has waited long enough. */
async function tryFormMatch(mode, region, isRanked) {
  const cfg = config.modes[mode];
  await tx(async (c) => {
    const tickets = (await c.query(
      `select * from matchmaking_tickets where status='waiting' and mode=$1 and region=$2 and is_ranked=$3
         and updated_at > now() - interval '5 minutes'
       order by created_at for update skip locked limit $4`, [mode, region, isRanked, cfg.maxPlayers * 2])).rows;
    if (!tickets.length) return;

    // Ranked: only players near the oldest ticket's rank; band widens by 100 points every 15 s of waiting.
    const oldest = tickets[0];
    const waited = (Date.now() - new Date(oldest.created_at).getTime()) / 1000;
    const band = isRanked ? 300 + Math.floor(waited / 15) * 100 : Infinity;
    const eligible = tickets.filter((t) => Math.abs(t.rank_points - oldest.rank_points) <= band);

    // Keep parties together
    const groups = new Map();
    for (const t of eligible) {
      const key = t.party_id || t.id;
      if (!groups.has(key)) groups.set(key, []);
      groups.get(key).push(t);
    }
    const chosen = [];
    let count = 0;
    for (const g of [...groups.values()].sort((a, b) => b.length - a.length)) {
      if (count + g.length > cfg.maxPlayers) continue;
      chosen.push(g);
      count += g.length;
    }
    const minPlayers = MIN_OVERRIDE || cfg.minPlayers;
    const full = count >= cfg.maxPlayers;
    if (!full && !(waited >= config.matchmakingFillSeconds && count >= minPlayers)) return;

    const server = (await c.query(
      `update game_servers set status='allocated' where id = (
         select id from game_servers where status='ready' and region=$1 and last_heartbeat > now() - interval '30 seconds'
           and max_players >= $2
         order by random() limit 1 for update skip locked) returning *`, [region, count])).rows[0];
    if (!server) return;   // keep waiting; the client shows "Finding server…"

    const timeOfDay = Math.random() < 0.3 ? "night" : "day";
    const match = (await c.query(
      `insert into matches (mode, region, is_ranked, server_id, time_of_day) values ($1,$2,$3,$4,$5) returning *`,
      [mode, region, isRanked, server.id, timeOfDay])).rows[0];
    await c.query("update game_servers set current_match_id=$2 where id=$1", [server.id, match.id]);

    // Team assignment
    const teamOf = new Map();
    if (mode === "clash_squad" || mode === "tdm") {
      const sizes = [0, 0];
      for (const g of chosen) {
        const team = sizes[0] <= sizes[1] ? 0 : 1;
        sizes[team] += g.length;
        g.forEach((t) => teamOf.set(t.id, team));
      }
    } else {
      const teams = [];   // [{id, size}]
      for (const g of chosen) {
        let team = g[0].party_id ? null : teams.find((t) => t.size + g.length <= cfg.teamSize);
        if (!team) { team = { id: teams.length, size: 0 }; teams.push(team); }
        team.size += g.length;
        g.forEach((t) => teamOf.set(t.id, team.id));
      }
    }
    for (const g of chosen) for (const t of g)
      await c.query("update matchmaking_tickets set status='matched', match_id=$2, team=$3, updated_at=now() where id=$1",
        [t.id, match.id, teamOf.get(t.id)]);
  });
}

export default r;
