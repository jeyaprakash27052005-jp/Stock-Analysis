import { Router } from "express";
import { config } from "../config.js";
import { HttpError, one, query, tx } from "../db.js";
import { requirePlayer } from "../lib/auth.js";
import { changeCurrency } from "../lib/economy.js";
import { rankInfo } from "../lib/ranks.js";

const r = Router();
r.use(requirePlayer);

const byTag = async (tag) => {
  const p = await one("select id, nickname from players where player_tag=$1 and deleted_at is null", [String(tag || "")]);
  if (!p) throw new HttpError(404, "No player with that ID");
  return p;
};

// ------------------------------------------------------------ friends
r.get("/friends", async (req, res) => {
  const rows = (await query(
    `select p.player_tag, p.nickname, p.avatar_id, p.level, p.br_rank_points, p.last_login_at,
            case when f.player_id = $1 then f.status else case f.status when 'pending' then 'incoming' else f.status end end as status
     from friendships f join players p on p.id = case when f.player_id=$1 then f.friend_id else f.player_id end
     where (f.player_id=$1 or f.friend_id=$1) and p.deleted_at is null
     order by p.last_login_at desc`, [req.playerId])).rows;
  res.json({ friends: rows.map((f) => ({ ...f, brRank: rankInfo(f.br_rank_points),
    online: Date.now() - new Date(f.last_login_at).getTime() < 15 * 60000 })) });
});

r.post("/friends/request", async (req, res) => {
  const other = await byTag(req.body.playerTag);
  if (other.id === req.playerId) throw new HttpError(400, "That's you!");
  await tx(async (c) => {
    const count = (await c.query("select count(*)::int n from friendships where (player_id=$1 or friend_id=$1) and status='accepted'", [req.playerId])).rows[0].n;
    if (count >= 100) throw new HttpError(400, "Friend list is full (100)");
    // If they already asked us, accept instead.
    const theirs = await c.query("update friendships set status='accepted' where player_id=$2 and friend_id=$1 returning 1", [req.playerId, other.id]);
    if (!theirs.rowCount)
      await c.query("insert into friendships (player_id, friend_id, status) values ($1,$2,'pending') on conflict do nothing", [req.playerId, other.id]);
  });
  res.json({ ok: true });
});

r.post("/friends/respond", async (req, res) => {
  const other = await byTag(req.body.playerTag);
  if (req.body.accept)
    await query("update friendships set status='accepted' where player_id=$2 and friend_id=$1 and status='pending'", [req.playerId, other.id]);
  else
    await query("delete from friendships where player_id=$2 and friend_id=$1 and status='pending'", [req.playerId, other.id]);
  res.json({ ok: true });
});

r.delete("/friends/:tag", async (req, res) => {
  const other = await byTag(req.params.tag);
  await query("delete from friendships where (player_id=$1 and friend_id=$2) or (player_id=$2 and friend_id=$1)", [req.playerId, other.id]);
  res.json({ ok: true });
});

// ------------------------------------------------------------ parties (lobby squads)
async function partyOf(c, playerId) {
  return (await c.query(
    `select pa.* from party_members pm join parties pa on pa.id = pm.party_id where pm.player_id=$1`, [playerId])).rows[0] || null;
}

r.get("/party", async (req, res) => {
  const party = await partyOf({ query }, req.playerId);
  const invites = (await query(
    `select pi.party_id, p.nickname as from_nickname, p.player_tag as from_tag from party_invites pi
     join players p on p.id = pi.invited_by where pi.player_id=$1 and pi.created_at > now() - interval '10 minutes'`, [req.playerId])).rows;
  if (!party) return res.json({ party: null, invites });
  const members = (await query(
    `select p.player_tag, p.nickname, p.avatar_id, p.level, l.character_id, (p.id = $2) as leader
     from party_members pm join players p on p.id = pm.player_id left join loadouts l on l.player_id = p.id
     where pm.party_id=$1 order by pm.joined_at`, [party.id, party.leader_id])).rows;
  res.json({ party: { id: party.id, mode: party.mode, members }, invites });
});

r.post("/party/invite", async (req, res) => {
  const other = await byTag(req.body.playerTag);
  await tx(async (c) => {
    let party = await partyOf(c, req.playerId);
    if (!party) {
      party = (await c.query("insert into parties (leader_id) values ($1) returning *", [req.playerId])).rows[0];
      await c.query("insert into party_members (player_id, party_id) values ($1,$2)", [req.playerId, party.id]);
    }
    const n = (await c.query("select count(*)::int n from party_members where party_id=$1", [party.id])).rows[0].n;
    if (n >= 4) throw new HttpError(400, "Party is full");
    await c.query("insert into party_invites (party_id, player_id, invited_by) values ($1,$2,$3) on conflict (party_id, player_id) do update set created_at=now()",
      [party.id, other.id, req.playerId]);
  });
  res.json({ ok: true });
});

r.post("/party/join", async (req, res) => {
  await tx(async (c) => {
    const inv = (await c.query("delete from party_invites where party_id=$1 and player_id=$2 returning 1", [req.body.partyId, req.playerId])).rowCount;
    if (!inv) throw new HttpError(404, "Invite expired");
    const n = (await c.query("select count(*)::int n from party_members where party_id=$1", [req.body.partyId])).rows[0].n;
    if (n >= 4) throw new HttpError(400, "Party is full");
    await leaveParty(c, req.playerId);
    await c.query("insert into party_members (player_id, party_id) values ($1,$2)", [req.playerId, req.body.partyId]);
  });
  res.json({ ok: true });
});

async function leaveParty(c, playerId) {
  const party = await partyOf(c, playerId);
  if (!party) return;
  await c.query("delete from party_members where player_id=$1", [playerId]);
  const next = (await c.query("select player_id from party_members where party_id=$1 order by joined_at limit 1", [party.id])).rows[0];
  if (!next) await c.query("delete from parties where id=$1", [party.id]);
  else if (party.leader_id === playerId) await c.query("update parties set leader_id=$2 where id=$1", [party.id, next.player_id]);
}

r.post("/party/leave", async (req, res) => {
  await tx((c) => leaveParty(c, req.playerId));
  res.json({ ok: true });
});

// ------------------------------------------------------------ guilds
r.get("/guilds", async (req, res) => {
  const q = `%${String(req.query.search || "").slice(0, 30)}%`;
  const rows = (await query(
    `select g.id, g.name, g.tag, g.description, g.level, g.glory, g.max_members, g.min_level,
            (select count(*)::int from guild_members m where m.guild_id = g.id) as members
     from guilds g where g.name ilike $1 or g.tag ilike $1 order by g.glory desc limit 50`, [q])).rows;
  res.json({ guilds: rows });
});

r.get("/guilds/mine", async (req, res) => {
  const g = await one(
    `select g.* from guild_members m join guilds g on g.id = m.guild_id where m.player_id=$1`, [req.playerId]);
  if (!g) return res.json({ guild: null });
  const members = (await query(
    `select p.player_tag, p.nickname, p.level, p.br_rank_points, m.role, p.last_login_at
     from guild_members m join players p on p.id = m.player_id where m.guild_id=$1
     order by case m.role when 'leader' then 0 when 'officer' then 1 else 2 end, p.level desc`, [g.id])).rows;
  const chat = (await query(
    `select c.id, c.body, c.created_at, p.nickname from chat_messages c join players p on p.id = c.player_id
     where c.channel=$1 order by c.id desc limit 50`, [`guild:${g.id}`])).rows.reverse();
  res.json({ guild: { ...g, glory: Number(g.glory) }, members, chat });
});

r.post("/guilds", async (req, res) => {
  const name = String(req.body.name || "").trim(), tag = String(req.body.tag || "").trim().toUpperCase();
  if (!/^[\p{L}\p{N} _-]{3,20}$/u.test(name)) throw new HttpError(400, "Guild name must be 3–20 characters");
  if (!/^[A-Z0-9]{2,5}$/.test(tag)) throw new HttpError(400, "Tag must be 2–5 letters/numbers");
  const g = await tx(async (c) => {
    if ((await c.query("select 1 from guild_members where player_id=$1", [req.playerId])).rowCount)
      throw new HttpError(409, "Leave your current guild first");
    await changeCurrency(c, req.playerId, "gold", -config.guildCreateCostGold, "guild_create");
    const g = (await c.query(
      "insert into guilds (name, tag, description, leader_id) values ($1,$2,$3,$4) on conflict do nothing returning *",
      [name, tag, String(req.body.description || "").slice(0, 200), req.playerId])).rows[0];
    if (!g) throw new HttpError(409, "Name or tag already taken");
    await c.query("insert into guild_members (player_id, guild_id, role) values ($1,$2,'leader')", [req.playerId, g.id]);
    await c.query("update players set guild_id=$2 where id=$1", [req.playerId, g.id]);
    return g;
  });
  res.json({ ok: true, guild: g });
});

r.post("/guilds/:id/join", async (req, res) => {
  await tx(async (c) => {
    const g = (await c.query("select * from guilds where id=$1 for update", [req.params.id])).rows[0];
    if (!g) throw new HttpError(404, "Guild not found");
    const me = (await c.query("select level from players where id=$1", [req.playerId])).rows[0];
    if (me.level < g.min_level) throw new HttpError(403, `Requires level ${g.min_level}`);
    const n = (await c.query("select count(*)::int n from guild_members where guild_id=$1", [g.id])).rows[0].n;
    if (n >= g.max_members) throw new HttpError(400, "Guild is full");
    const ins = await c.query("insert into guild_members (player_id, guild_id) values ($1,$2) on conflict do nothing returning 1", [req.playerId, g.id]);
    if (!ins.rowCount) throw new HttpError(409, "Leave your current guild first");
    await c.query("update players set guild_id=$2 where id=$1", [req.playerId, g.id]);
  });
  res.json({ ok: true });
});

r.post("/guilds/leave", async (req, res) => {
  await tx(async (c) => {
    const m = (await c.query("delete from guild_members where player_id=$1 returning guild_id, role", [req.playerId])).rows[0];
    if (!m) return;
    await c.query("update players set guild_id=null where id=$1", [req.playerId]);
    if (m.role === "leader") {
      const next = (await c.query(
        "select player_id from guild_members where guild_id=$1 order by case role when 'officer' then 0 else 1 end, joined_at limit 1", [m.guild_id])).rows[0];
      if (next) {
        await c.query("update guild_members set role='leader' where player_id=$1", [next.player_id]);
        await c.query("update guilds set leader_id=$2 where id=$1", [m.guild_id, next.player_id]);
      } else {
        await c.query("delete from guilds where id=$1", [m.guild_id]);
      }
    }
  });
  res.json({ ok: true });
});

// Guild / party text chat. Quick-chat inside matches goes through the game server, not here.
r.post("/chat", async (req, res) => {
  const body = String(req.body.body || "").trim().slice(0, 200);
  if (!body) throw new HttpError(400, "Empty message");
  const m = await one("select guild_id from guild_members where player_id=$1", [req.playerId]);
  const p = await one("select party_id from party_members where player_id=$1", [req.playerId]);
  const channel = req.body.channel === "party" ? (p && `party:${p.party_id}`) : (m && `guild:${m.guild_id}`);
  if (!channel) throw new HttpError(400, "You're not in that channel");
  await query("insert into chat_messages (channel, player_id, body) values ($1,$2,$3)", [channel, req.playerId, body]);
  res.json({ ok: true });
});

export default r;
