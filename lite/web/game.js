/* Stormdrop Royale Lite — HTML5 (three.js) version packaged in the Android app.
 * Online: guest login, profile, missions, store and daily reward from the Stormdrop backend.
 * Offline-capable solo battle royale against bots: plane drop, parachute, loot, weapons,
 * armor, med kits, shrinking storm, minimap, kill feed, results. */
(() => {
"use strict";
const API = "https://stormdrop-royal.vercel.app/api";
const SITE = API.replace(/\/api$/, "");
const $ = (id) => document.getElementById(id);
const store = {
  get(k, d) { try { const v = localStorage.getItem(k); return v === null ? d : JSON.parse(v); } catch { return d; } },
  set(k, v) { try { localStorage.setItem(k, JSON.stringify(v)); } catch {} },
};
const settings = Object.assign({ quality: "low", sens: 1 }, store.get("sd.settings", {}));

// ====================================================================== online
const net = {
  token: store.get("sd.token", null), me: null, online: false,
  async call(method, path, body) {
    const r = await fetch(API + path, {
      method, headers: { "content-type": "application/json", ...(this.token ? { authorization: "Bearer " + this.token } : {}) },
      body: body ? JSON.stringify(body) : undefined,
    });
    const j = await r.json().catch(() => ({}));
    if (!r.ok) throw new Error(j.error || ("HTTP " + r.status));
    return j;
  },
  deviceId() {
    let id = store.get("sd.device", null);
    if (!id) { id = Array.from(crypto.getRandomValues(new Uint8Array(20)), (b) => b.toString(16).padStart(2, "0")).join(""); store.set("sd.device", id); }
    return id;
  },
};

async function checkHealth() {
  try {
    const h = await (await fetch(API + "/health", { cache: "no-store" })).json();
    $("st-api").innerHTML = '<span class="ok">● online</span>';
    const dbOk = h.database === "connected";
    $("st-db").innerHTML = dbOk ? '<span class="ok">● connected</span>' : `<span class="bad">● ${h.database}</span>`;
    net.online = dbOk;
  } catch {
    $("st-api").innerHTML = '<span class="bad">● unreachable</span>';
    $("st-db").innerHTML = '<span class="bad">● offline</span>';
    net.online = false;
  }
}

async function guestLogin() {
  const b = $("btn-guest"); b.disabled = true; b.textContent = "Signing in…";
  try {
    if (net.token) { try { net.me = await net.call("GET", "/me"); } catch { net.token = null; } }
    if (!net.me) {
      const r = await net.call("POST", "/auth/guest", { deviceId: net.deviceId() });
      net.token = r.token; net.me = r.profile; store.set("sd.token", r.token);
    }
    showLobby();
  } catch (e) {
    toast("Online login failed: " + e.message + " — playing offline");
    showLobby();
  } finally { b.disabled = false; b.textContent = "Play as Guest"; }
}

function show(id) {
  for (const s of document.querySelectorAll(".screen")) s.classList.toggle("on", s.id === id);
  $("hud").classList.toggle("on", id === "hud");
}

function showLobby() {
  show("lobby");
  const m = net.me;
  $("p-name").textContent = m ? m.nickname : "Offline player";
  $("p-sub").textContent = m ? `ID ${m.player_tag} · Level ${m.level} · ${m.region}` : "Not signed in — progress isn't saved";
  $("p-gold").textContent = m ? Number(m.gold).toLocaleString() : "—";
  $("p-dia").textContent = m ? Number(m.diamonds).toLocaleString() : "—";
  $("p-rank").textContent = m && m.brRank ? `${m.brRank.label} · ${m.br_rank_points} RP` : "";
  $("net-note").textContent = m ? "Connected to the online database." : "Offline mode.";
}

async function refreshMe() { if (net.token) try { net.me = await net.call("GET", "/me"); showLobby(); } catch {} }

function panel(html) { $("panel").innerHTML = html; }
async function tab(name) {
  if (name === "settings") return renderSettings();
  if (!net.me) return panel('<p class="dim">Sign in online to use this.</p>');
  panel('<p class="dim">Loading…</p>');
  try {
    if (name === "missions") {
      const r = await net.call("GET", "/progression/missions");
      panel(r.missions.map((m) => `<div class="item"><span>[${m.period}] ${m.title}<br><span class="dim">+${m.reward_gold} Gold · ${Math.min(m.progress, m.target)}/${m.target}</span></span>
        ${m.claimed_at ? '<span class="ok">Claimed</span>' : m.complete ? `<button class="primary" data-claim="${m.id}">Claim</button>` : ""}</div>`).join(""));
    } else if (name === "store") {
      const [cat, inv] = await Promise.all([net.call("GET", "/store/catalog"), net.call("GET", "/store/inventory")]);
      const owned = new Set(inv.items.map((i) => i.item_id));
      const items = cat.items.filter((i) => i.in_store && (i.price_gold != null || i.price_diamonds != null));
      panel(items.map((i) => `<div class="item"><span>${i.name}<br><span class="dim">${i.type.replace("_", " ")} · ${i.rarity}</span></span><span class="row">
        ${owned.has(i.id) && i.type !== "consumable" ? '<span class="ok">Owned</span>' :
          (i.price_gold != null ? `<button data-buy="${i.id}" data-cur="gold">● ${i.price_gold}</button>` : "") +
          (i.price_diamonds != null ? `<button data-buy="${i.id}" data-cur="diamonds">◆ ${i.price_diamonds}</button>` : "")}</span></div>`).join(""));
    } else if (name === "reward") {
      const r = await net.call("GET", "/progression/login-rewards");
      panel(r.rewards.map((d) => `<div class="item"><span>Day ${d.day}: ${d.name}</span>${d.day <= r.streakDay ? '<span class="ok">✔</span>' : ""}</div>`).join("") +
        (r.canClaim ? '<p><button class="primary" id="claim-daily">Claim today\'s reward</button></p>' : '<p class="dim">Come back tomorrow!</p>'));
    }
  } catch (e) { panel(`<p class="bad">${e.message}</p>`); }
}

$("panel").addEventListener("click", async (e) => {
  const t = e.target;
  try {
    if (t.dataset.claim) { await net.call("POST", `/progression/missions/${t.dataset.claim}/claim`); toast("Claimed!"); await refreshMe(); tab("missions"); }
    else if (t.dataset.buy) { await net.call("POST", "/store/buy", { itemId: t.dataset.buy, currency: t.dataset.cur }); toast("Purchased!"); await refreshMe(); tab("store"); }
    else if (t.id === "claim-daily") { await net.call("POST", "/progression/login-rewards/claim"); toast("Reward claimed!"); await refreshMe(); tab("reward"); }
    else if (t.dataset.q) { settings.quality = t.dataset.q; store.set("sd.settings", settings); applyQuality(); renderSettings(); }
    else if (t.id === "signout") { net.token = null; net.me = null; store.set("sd.token", null); show("splash"); }
  } catch (err) { toast(err.message); }
});
$("panel").addEventListener("input", (e) => { if (e.target.id === "sens") { settings.sens = +e.target.value; store.set("sd.settings", settings); } });

function renderSettings() {
  panel(`<div class="item"><span>Graphics</span><span class="row">${["low", "high"].map((q) =>
      `<button data-q="${q}" class="${settings.quality === q ? "primary" : ""}">${q === "low" ? "Smooth" : "Ultra"}</button>`).join("")}</span></div>
    <div class="item"><span>Look sensitivity</span><input id="sens" type="range" min="0.3" max="3" step="0.1" value="${settings.sens}"></div>
    ${net.me ? '<div class="item"><span>Account</span><button id="signout">Sign out</button></div>' : ""}
    <p class="dim">Delete your account any time at ${SITE}/delete-account</p>`);
}

let toastTimer;
function toast(msg) {
  let t = $("toast");
  if (!t) { t = document.createElement("div"); t.id = "toast"; Object.assign(t.style, { position: "fixed", left: "50%", bottom: "24px", transform: "translateX(-50%)", background: "#000c", padding: "10px 18px", borderRadius: "10px", zIndex: 99, fontWeight: 700 }); document.body.appendChild(t); }
  t.textContent = msg; t.style.display = "block";
  clearTimeout(toastTimer); toastTimer = setTimeout(() => (t.style.display = "none"), 3000);
}

// ====================================================================== 3D world
const canvas = $("game");
const renderer = new THREE.WebGLRenderer({ canvas, antialias: false, powerPreference: "high-performance" });
const scene = new THREE.Scene();
scene.background = new THREE.Color(0x8fc3e8);
scene.fog = new THREE.Fog(0x8fc3e8, 120, 420);
const camera = new THREE.PerspectiveCamera(70, 1, 0.1, 900);
scene.add(new THREE.HemisphereLight(0xdfefff, 0x445533, 0.85));
const sun = new THREE.DirectionalLight(0xffffff, 0.75); sun.position.set(80, 160, 40); scene.add(sun);

function applyQuality() {
  const ratio = settings.quality === "high" ? Math.min(2, devicePixelRatio) : Math.min(1, devicePixelRatio * 0.75);
  renderer.setPixelRatio(ratio);
  resize();
}
function resize() {
  renderer.setSize(innerWidth, innerHeight, false);
  camera.aspect = innerWidth / innerHeight; camera.updateProjectionMatrix();
}
addEventListener("resize", resize);
applyQuality();

const HALF = 300;
function rng(seed) { let s = seed >>> 0 || 1; return () => ((s ^= s << 13, s ^= s >>> 17, s ^= s << 5) >>> 0) / 4294967296; }
const TOWNS = [
  { name: "Ironhaven City", x: -120, z: 60, r: 70, n: 16, color: 0xbfb9b0 },
  { name: "Fort Basalt", x: 130, z: -120, r: 55, n: 9, color: 0x5d6a4c },
  { name: "Goldmeadow Farms", x: -140, z: -140, r: 60, n: 6, color: 0xa8433a },
  { name: "Rustport", x: 30, z: 215, r: 50, n: 8, color: 0x3b6fb8 },
];
function height(x, z) {
  const d = Math.hypot(x, z) / 285;
  let h = 5 + 4 * Math.sin(x * 0.021) * Math.cos(z * 0.018) + 2 * Math.sin(x * 0.05 + z * 0.04);
  const dm = Math.hypot(x - 150, z - 140);
  h += 38 * Math.exp(-(dm * dm) / (2 * 55 * 55));
  for (const t of TOWNS) {
    const dt = Math.hypot(x - t.x, z - t.z);
    const k = 1 - Math.min(1, Math.max(0, (dt - t.r * 0.8) / (t.r * 0.6)));
    h = h * (1 - k) + 4.5 * k;
  }
  const riverZ = 20 + Math.sin(x / 45) * 25;
  const dr = Math.abs(z - riverZ);
  if (x < 120 && dr < 16) h = Math.min(h, -1 + (dr / 16) * 6);
  if (d > 0.82) h -= (d - 0.82) * 90;
  return h;
}

// Terrain
const terrain = (() => {
  const seg = 110;
  const g = new THREE.PlaneGeometry(HALF * 2, HALF * 2, seg, seg);
  g.rotateX(-Math.PI / 2);
  const pos = g.attributes.position, colors = [];
  const c = new THREE.Color();
  for (let i = 0; i < pos.count; i++) {
    const x = pos.getX(i), z = pos.getZ(i), y = height(x, z);
    pos.setY(i, y);
    if (y < 1.2) c.setHex(0xd8c79a); else if (y > 30) c.setHex(0xeef3f8); else if (y > 18) c.setHex(0x8a8a86); else c.setHex(0x5a8f3c);
    c.offsetHSL(0, 0, (Math.random() - 0.5) * 0.04);
    colors.push(c.r, c.g, c.b);
  }
  g.setAttribute("color", new THREE.Float32BufferAttribute(colors, 3));
  g.computeVertexNormals();
  const m = new THREE.Mesh(g, new THREE.MeshLambertMaterial({ vertexColors: true }));
  scene.add(m);
  const water = new THREE.Mesh(new THREE.PlaneGeometry(2000, 2000), new THREE.MeshLambertMaterial({ color: 0x2b6f9e, transparent: true, opacity: 0.85 }));
  water.rotation.x = -Math.PI / 2; water.position.y = 0.2; scene.add(water);
  return m;
})();
const groundY = (x, z) => Math.max(height(x, z), 0.2);

// Buildings (solid boxes used for collision + line of sight) and trees
const boxes = [];
const lootPoints = [];
(() => {
  const r = rng(42);
  const roof = new THREE.MeshLambertMaterial({ color: 0x6b3b30 });
  for (const t of TOWNS) {
    const mat = new THREE.MeshLambertMaterial({ color: t.color });
    for (let i = 0; i < t.n; i++) {
      const x = t.x + (r() * 2 - 1) * t.r * 0.8, z = t.z + (r() * 2 - 1) * t.r * 0.8;
      if (height(x, z) < 1.5) continue;
      const w = 7 + r() * 7, d = 7 + r() * 7, h = 5 + r() * 8, y = groundY(x, z);
      if (boxes.some((b) => Math.abs(b.x - x) < (b.w + w) / 2 + 4 && Math.abs(b.z - z) < (b.d + d) / 2 + 4)) continue;
      const m = new THREE.Mesh(new THREE.BoxGeometry(w, h, d), mat);
      m.position.set(x, y + h / 2 - 0.5, z); scene.add(m);
      const rf = new THREE.Mesh(new THREE.BoxGeometry(w + 0.6, 0.5, d + 0.6), roof);
      rf.position.set(x, y + h - 0.25, z); scene.add(rf);
      boxes.push({ x, z, w, d, y0: y - 1, y1: y + h });
      for (let k = 0; k < 3; k++) {
        const side = Math.floor(r() * 4);
        const off = (r() - 0.5) * 0.8;
        const lx = x + (side === 0 ? w / 2 + 1.5 : side === 1 ? -w / 2 - 1.5 : off * w);
        const lz = z + (side === 2 ? d / 2 + 1.5 : side === 3 ? -d / 2 - 1.5 : off * d);
        lootPoints.push({ x: lx, z: lz, tier: t.name === "Fort Basalt" ? 2 : 1 });
      }
    }
  }
  const trunk = new THREE.MeshLambertMaterial({ color: 0x6b4a2b }), leaves = new THREE.MeshLambertMaterial({ color: 0x2f6b2c });
  const tg = new THREE.CylinderGeometry(0.3, 0.4, 3, 6), lg = new THREE.ConeGeometry(2.2, 5, 7);
  for (let i = 0; i < 260; i++) {
    const x = (r() * 2 - 1) * 260, z = (r() * 2 - 1) * 260, y = height(x, z);
    if (y < 2 || y > 24 || TOWNS.some((t) => Math.hypot(x - t.x, z - t.z) < t.r)) { if (r() < 0.3) lootPoints.push({ x, z, tier: 0 }); continue; }
    const tr = new THREE.Mesh(tg, trunk); tr.position.set(x, y + 1.5, z); scene.add(tr);
    const lf = new THREE.Mesh(lg, leaves); lf.position.set(x, y + 5, z); scene.add(lf);
    if (r() < 0.25) lootPoints.push({ x: x + 2, z: z + 2, tier: 0 });
  }
})();

function rayBoxDist(o, d, b, maxD) {
  let tmin = 0, tmax = maxD;
  const mins = [b.x - b.w / 2, b.y0, b.z - b.d / 2], maxs = [b.x + b.w / 2, b.y1, b.z + b.d / 2];
  const oo = [o.x, o.y, o.z], dd = [d.x, d.y, d.z];
  for (let i = 0; i < 3; i++) {
    if (Math.abs(dd[i]) < 1e-8) { if (oo[i] < mins[i] || oo[i] > maxs[i]) return Infinity; continue; }
    let t1 = (mins[i] - oo[i]) / dd[i], t2 = (maxs[i] - oo[i]) / dd[i];
    if (t1 > t2) [t1, t2] = [t2, t1];
    tmin = Math.max(tmin, t1); tmax = Math.min(tmax, t2);
    if (tmin > tmax) return Infinity;
  }
  return tmin;
}
function blockedDist(o, d, maxD) {
  let best = maxD;
  for (const b of boxes) { const t = rayBoxDist(o, d, b, best); if (t < best) best = t; }
  // coarse terrain check
  for (let s = 4; s < best; s += 4) {
    const px = o.x + d.x * s, py = o.y + d.y * s, pz = o.z + d.z * s;
    if (py < height(px, pz) - 0.3) { best = s; break; }
  }
  return best;
}

// ====================================================================== items
const WEAPONS = {
  fists:   { name: "Fists", dmg: 18, rpm: 120, mag: 0, range: 2.6, spread: 0, auto: false, ammo: null, zoom: 1 },
  pistol:  { name: "Viper Pistol", dmg: 30, rpm: 300, mag: 12, range: 55, spread: 2.2, auto: false, ammo: "light", zoom: 1.3, reload: 1.3 },
  smg:     { name: "Wasp SMG", dmg: 22, rpm: 850, mag: 32, range: 65, spread: 3.2, auto: true, ammo: "light", zoom: 1.3, reload: 1.8 },
  ar:      { name: "Raptor-7", dmg: 32, rpm: 600, mag: 30, range: 130, spread: 2.2, auto: true, ammo: "medium", zoom: 2, reload: 2.2 },
  shotgun: { name: "Thunderclap", dmg: 15, pellets: 8, rpm: 75, mag: 6, range: 26, spread: 6, auto: false, ammo: "shells", zoom: 1.2, reload: 2.6 },
  sniper:  { name: "Longreach", dmg: 140, rpm: 45, mag: 5, range: 320, spread: 0.15, hipSpread: 5, auto: false, ammo: "sniper", zoom: 4, reload: 3.2, head: 2.2 },
  lmg:     { name: "Juggernaut LMG", dmg: 34, rpm: 650, mag: 80, range: 140, spread: 3, auto: true, ammo: "heavy", zoom: 2, reload: 4.5 },
};
const AMMO_FOR = { light: "Light Ammo", medium: "Medium Ammo", heavy: "Heavy Ammo", shells: "Shells", sniper: "Sniper Ammo" };
const ARMOR = [0, 0.2, 0.3, 0.4, 0.5], ARMOR_DUR = [0, 100, 150, 200, 260];
const LOOT_TABLES = [
  [["weapon", "pistol", 8], ["weapon", "smg", 6], ["weapon", "shotgun", 4], ["ammo", "light", 12], ["ammo", "shells", 5], ["ammo", "medium", 6], ["med", 1, 8], ["shroom", 1, 7], ["vest", 1, 6], ["helmet", 1, 5]],
  [["weapon", "ar", 8], ["weapon", "smg", 5], ["weapon", "shotgun", 4], ["weapon", "sniper", 2], ["ammo", "medium", 12], ["ammo", "light", 6], ["ammo", "shells", 4], ["ammo", "sniper", 3], ["med", 2, 9], ["shroom", 1, 5], ["vest", 2, 5], ["helmet", 2, 5]],
  [["weapon", "ar", 6], ["weapon", "lmg", 4], ["weapon", "sniper", 4], ["ammo", "medium", 10], ["ammo", "heavy", 6], ["ammo", "sniper", 5], ["med", 2, 8], ["vest", 3, 5], ["helmet", 3, 5]],
];
const LOOT_COLORS = { weapon: 0xffb347, ammo: 0xd8d8d8, med: 0x44d17a, shroom: 0xb07cff, vest: 0x4aa3ff, helmet: 0x4aa3ff };
function lootLabel(l) {
  if (l.kind === "weapon") return WEAPONS[l.id].name;
  if (l.kind === "ammo") return `${AMMO_FOR[l.id]} ×${l.n}`;
  if (l.kind === "med") return `Med Kit ×${l.n}`;
  if (l.kind === "shroom") return `Vigor Shroom ×${l.n}`;
  return `${l.kind === "vest" ? "Vest" : "Helmet"} Lv${l.id}`;
}

// ====================================================================== match state
let M = null;   // current match
const lootGeo = new THREE.BoxGeometry(0.7, 0.35, 0.5);
const lootMats = {};
const bodyGeo = new THREE.CylinderGeometry(0.42, 0.42, 1.35, 8), headGeo = new THREE.SphereGeometry(0.28, 8, 6);
const chuteGeo = new THREE.SphereGeometry(2.4, 10, 6, 0, Math.PI * 2, 0, Math.PI / 2);
const BOT_NAMES = ["Vex", "Kira", "Dusk", "Rho", "Juno", "Mako", "Nyx", "Tango", "Blaze", "Orin", "Zed", "Pike", "Lumen", "Sable", "Fen", "Quill", "Ash", "Brick", "Echo", "Wren"];

function makeFighter(name, color, isPlayer) {
  const g = new THREE.Group();
  const mat = new THREE.MeshLambertMaterial({ color });
  const body = new THREE.Mesh(bodyGeo, mat); body.position.y = 0.75; g.add(body);
  const head = new THREE.Mesh(headGeo, new THREE.MeshLambertMaterial({ color: 0xf0d2b0 })); head.position.y = 1.65; g.add(head);
  const visor = new THREE.Mesh(new THREE.BoxGeometry(0.4, 0.1, 0.12), new THREE.MeshLambertMaterial({ color: 0x111111 })); visor.position.set(0, 1.68, 0.24); g.add(visor);
  const chute = new THREE.Mesh(chuteGeo, new THREE.MeshLambertMaterial({ color: isPlayer ? 0xff8c1a : 0xdddddd, side: THREE.DoubleSide })); chute.position.y = 5; chute.visible = false; g.add(chute);
  g.visible = false;
  scene.add(g);
  return {
    name, isPlayer, mesh: g, chute, pos: new THREE.Vector3(), vy: 0, yaw: 0, hp: 200, energy: 0, vest: 0, vestDur: 0, helmet: 0, helmetDur: 0,
    state: "plane", chuteOpen: false, alive: true, crouch: false, weapons: ["fists", null], mags: [0, 0], active: 0,
    ammo: { light: 0, medium: 0, heavy: 0, shells: 0, sniper: 0 }, meds: 0, kills: 0, damage: 0, nextShot: 0, reloadUntil: 0,
    healUntil: 0, jumpAt: 0, target: null, wander: null, think: 0, skill: 0.35 + Math.random() * 0.35, placement: 0,
  };
}

function startMatch() {
  if (M) endCleanup();
  const r = Math.random;
  M = {
    t: 0, fighters: [], loot: [], lootId: 1, over: false, feed: [],
    zone: { cx: 0, cz: 0, r: 330, fromX: 0, fromZ: 0, fromR: 330, toX: 0, toZ: 0, toR: 330, phase: -1, state: "wait", t0: 0, t1: 25, dps: 1 },
    plane: null, aim: false, fireHeld: false, firePressed: false,
  };
  // loot
  for (const p of lootPoints) {
    const table = LOOT_TABLES[p.tier];
    if (r() < 0.25) continue;
    const total = table.reduce((s, e) => s + e[2], 0);
    let roll = r() * total, pick = table[0];
    for (const e of table) { roll -= e[2]; if (roll < 0) { pick = e; break; } }
    spawnLoot(p.x + (r() - 0.5) * 2, p.z + (r() - 0.5) * 2, lootFrom(pick));
  }
  // fighters
  const me = makeFighter(net.me ? net.me.nickname : "You", 0xff8c1a, true);
  M.me = me; M.fighters.push(me);
  for (let i = 0; i < 19; i++) M.fighters.push(makeFighter(BOT_NAMES[i], new THREE.Color().setHSL(r(), 0.5, 0.45).getHex(), false));
  // plane path
  const a = r() * Math.PI * 2, off = (r() - 0.5) * 160;
  const dir = new THREE.Vector3(Math.cos(a), 0, Math.sin(a)), side = new THREE.Vector3(-dir.z, 0, dir.x);
  M.plane = { from: dir.clone().multiplyScalar(-330).addScaledVector(side, off).setY(140), to: dir.clone().multiplyScalar(330).addScaledVector(side, off).setY(140), t: 0, dur: 18 };
  M.planeMesh = new THREE.Mesh(new THREE.BoxGeometry(3, 2.5, 14), new THREE.MeshLambertMaterial({ color: 0xdddddd }));
  const wings = new THREE.Mesh(new THREE.BoxGeometry(18, 0.4, 3), M.planeMesh.material); M.planeMesh.add(wings);
  scene.add(M.planeMesh);
  for (const f of M.fighters) if (!f.isPlayer) f.jumpAt = 2 + r() * 14;
  show("hud");
  banner("Tap JUMP to drop from the plane", 4);
  camYaw = Math.atan2(dir.x, dir.z) + Math.PI; camPitch = 0.25;
}

function lootFrom(e) {
  const [kind, id] = e;
  if (kind === "ammo") return { kind, id, n: { light: 30, medium: 30, heavy: 40, shells: 10, sniper: 8 }[id] };
  if (kind === "med") return { kind, id: "med", n: id };
  if (kind === "shroom") return { kind, id: "shroom", n: id };
  return { kind, id };
}
function spawnLoot(x, z, item) {
  const key = item.kind;
  lootMats[key] = lootMats[key] || new THREE.MeshLambertMaterial({ color: LOOT_COLORS[key], emissive: LOOT_COLORS[key], emissiveIntensity: 0.25 });
  const m = new THREE.Mesh(lootGeo, lootMats[key]);
  const y = groundY(x, z);
  m.position.set(x, y + 0.3, z); scene.add(m);
  M.loot.push(Object.assign({ uid: M.lootId++, x, y, z, mesh: m }, item));
}
function removeLoot(l) { scene.remove(l.mesh); M.loot.splice(M.loot.indexOf(l), 1); }

function endCleanup() {
  for (const f of M.fighters) scene.remove(f.mesh);
  for (const l of M.loot) scene.remove(l.mesh);
  if (M.planeMesh) scene.remove(M.planeMesh);
  for (const t of tracers) scene.remove(t.line);
  tracers.length = 0;
  M = null;
}

// ====================================================================== fighter logic
function weaponOf(f) { return WEAPONS[f.weapons[f.active] || "fists"]; }

function tryPickup(f, l) {
  if (l.kind === "weapon") {
    const slot = f.weapons[0] === "fists" || f.weapons[0] === null ? 0 : f.weapons[1] === null ? 1 : f.active;
    const old = f.weapons[slot];
    if (old && old !== "fists") spawnLoot(f.pos.x + 1, f.pos.z, { kind: "weapon", id: old });
    f.weapons[slot] = l.id; f.mags[slot] = WEAPONS[l.id].mag; f.active = slot;
  } else if (l.kind === "ammo") f.ammo[l.id] += l.n;
  else if (l.kind === "med") f.meds += l.n;
  else if (l.kind === "shroom") f.energy = Math.min(200, f.energy + 50 * l.n);
  else if (l.kind === "vest") { if (l.id < f.vest) return false; f.vest = l.id; f.vestDur = ARMOR_DUR[l.id]; }
  else if (l.kind === "helmet") { if (l.id < f.helmet) return false; f.helmet = l.id; f.helmetDur = ARMOR_DUR[l.id]; }
  removeLoot(l);
  return true;
}

function autoPickup(f) {
  for (const l of M.loot.slice()) {
    if (Math.abs(l.x - f.pos.x) > 1.8 || Math.abs(l.z - f.pos.z) > 1.8) continue;
    if (l.kind === "weapon") {
      if (!f.isPlayer) { const better = weaponScore(l.id) > weaponScore(f.weapons[0]) || f.weapons[1] === null; if (better) tryPickup(f, l); }
      else if (f.weapons[0] === "fists" || f.weapons[1] === null) { tryPickup(f, l); toast("Picked up " + WEAPONS[l.id].name); }
      continue;
    }
    if (l.kind === "vest" && l.id <= f.vest || l.kind === "helmet" && l.id <= f.helmet) continue;
    if (tryPickup(f, l) && f.isPlayer) toast("Picked up " + lootLabel(l));
  }
}
const weaponScore = (id) => ({ fists: 0, pistol: 1, smg: 3, shotgun: 3, ar: 4, sniper: 3, lmg: 4 })[id || "fists"] || 0;

function reload(f) {
  const w = weaponOf(f);
  if (!w.ammo || f.reloadUntil > M.t || f.mags[f.active] >= w.mag || f.ammo[w.ammo] <= 0) return;
  f.reloadUntil = M.t + w.reload;
  f.pendingReload = true;
}
function finishReload(f) {
  if (!f.pendingReload || M.t < f.reloadUntil) return;
  f.pendingReload = false;
  const w = weaponOf(f);
  if (!w.ammo) return;
  const take = Math.min(w.mag - f.mags[f.active], f.ammo[w.ammo]);
  f.mags[f.active] += take; f.ammo[w.ammo] -= take;
}

function damage(target, amount, attacker, headshot, source) {
  if (!target.alive || target.state === "plane") return;
  let dmg = amount;
  if (source !== "storm") {
    if (headshot && target.helmet) { dmg *= 1 - ARMOR[target.helmet]; target.helmetDur -= dmg * 0.6; if (target.helmetDur <= 0) target.helmet = 0; }
    else if (!headshot && target.vest) { dmg *= 1 - ARMOR[target.vest]; target.vestDur -= dmg * 0.6; if (target.vestDur <= 0) target.vest = 0; }
  }
  target.hp -= dmg;
  if (attacker) attacker.damage += dmg;
  if (target.isPlayer) { $("dmg").style.boxShadow = "inset 0 0 120px rgba(255,0,0,.55)"; setTimeout(() => ($("dmg").style.boxShadow = "inset 0 0 120px rgba(255,0,0,0)"), 180); }
  if (!target.isPlayer && attacker) target.target = attacker;   // bots fight back
  if (target.hp <= 0) kill(target, attacker, source, headshot);
}

function kill(f, killer, source, headshot) {
  f.alive = false; f.hp = 0;
  f.placement = M.fighters.filter((x) => x.alive).length + 1;
  f.mesh.visible = false;
  if (killer) killer.kills++;
  // death crate: drop everything
  for (const w of f.weapons) if (w && w !== "fists") spawnLoot(f.pos.x + (Math.random() - 0.5) * 2, f.pos.z + (Math.random() - 0.5) * 2, { kind: "weapon", id: w });
  for (const [k, n] of Object.entries(f.ammo)) if (n > 0) spawnLoot(f.pos.x + (Math.random() - 0.5) * 2, f.pos.z + (Math.random() - 0.5) * 2, { kind: "ammo", id: k, n });
  if (f.meds) spawnLoot(f.pos.x, f.pos.z + 1, { kind: "med", id: "med", n: f.meds });
  if (f.vest) spawnLoot(f.pos.x + 1, f.pos.z + 1, { kind: "vest", id: f.vest });
  feed(killer ? `${killer.name} eliminated ${f.name}${headshot ? " ◎" : ""} [${source}]` : `${f.name} was eliminated by the storm`);
  if (killer && killer.isPlayer) banner(headshot ? "HEADSHOT ELIMINATION" : "ELIMINATION", 1.4);
  const alive = M.fighters.filter((x) => x.alive);
  if (f.isPlayer) finish(false);
  else if (alive.length === 1 && alive[0].isPlayer) { M.me.placement = 1; finish(true); }
}

function feed(line) {
  M.feed.push(line); if (M.feed.length > 5) M.feed.shift();
  $("feed").innerHTML = M.feed.map((l) => l.replace(/[<>]/g, "")).join("<br>");
}

const tracers = [];
const tracerMat = new THREE.LineBasicMaterial({ color: 0xffe08a, transparent: true, opacity: 0.9 });
function tracer(a, b) {
  const g = new THREE.BufferGeometry().setFromPoints([a, b]);
  const line = new THREE.Line(g, tracerMat); scene.add(line);
  tracers.push({ line, until: M.t + 0.05 });
}

const _o = new THREE.Vector3(), _d = new THREE.Vector3(), _v = new THREE.Vector3();
function raySphere(o, d, c, r) {
  _v.subVectors(c, o); const t = _v.dot(d); if (t < 0) return Infinity;
  const d2 = _v.lengthSq() - t * t; return d2 <= r * r ? t - Math.sqrt(r * r - d2) : Infinity;
}

/** Fires one shot from origin along dir. Returns hit info. */
function shoot(f, origin, dir, spreadDeg) {
  const w = weaponOf(f);
  const melee = !w.ammo;
  if (M.t < f.nextShot || M.t < f.reloadUntil || M.t < f.healUntil) return false;
  if (!melee && f.mags[f.active] <= 0) { reload(f); return false; }
  f.nextShot = M.t + 60 / w.rpm;
  if (!melee) f.mags[f.active]--;
  let hitAny = false;
  for (let p = 0; p < (w.pellets || 1); p++) {
    _d.copy(dir);
    if (spreadDeg > 0) {
      const s = THREE.MathUtils.degToRad(spreadDeg);
      _d.x += (Math.random() - 0.5) * s * 2; _d.y += (Math.random() - 0.5) * s * 2; _d.z += (Math.random() - 0.5) * s * 2; _d.normalize();
    }
    const maxD = blockedDist(origin, _d, w.range);
    let best = null, bestT = maxD, head = false;
    for (const t of M.fighters) {
      if (t === f || !t.alive || t.state === "plane") continue;
      const crouchY = t.crouch ? -0.45 : 0;
      const tb = raySphere(origin, _d, _v.set(t.pos.x, t.pos.y + 0.85 + crouchY, t.pos.z), 0.55);
      const th = raySphere(origin, _d, _v.set(t.pos.x, t.pos.y + 1.62 + crouchY, t.pos.z), 0.3);
      if (th < bestT) { best = t; bestT = th; head = true; }
      if (tb < bestT) { best = t; bestT = tb; head = false; }
    }
    const end = origin.clone().addScaledVector(_d, best ? bestT : maxD);
    if (!melee && (f.isPlayer || origin.distanceTo(camera.position) < 120)) tracer(f.isPlayer ? f.pos.clone().setY(f.pos.y + 1.3) : origin.clone(), end);
    if (best) {
      const falloff = bestT > w.range * 0.4 ? 0.7 : 1;
      damage(best, w.dmg * (head ? (w.head || 1.8) : 1) * falloff, f, head, w.name);
      hitAny = true;
      if (f.isPlayer) { const h = $("hit"); h.style.borderColor = head ? "#ff4040" : "#fff"; h.style.opacity = 1; setTimeout(() => (h.style.opacity = 0), 120); }
    }
  }
  if (f.isPlayer && !melee) { camPitch -= 0.012 * (w.zoom > 2 ? 3 : 1); camYaw += (Math.random() - 0.5) * 0.008; }
  return hitAny;
}

function moveFighter(f, dx, dz, dt, speed) {
  const len = Math.hypot(dx, dz);
  if (len > 1e-3) { f.pos.x += dx / len * speed * dt * Math.min(1, len); f.pos.z += dz / len * speed * dt * Math.min(1, len); }
  // building collision (push out on the shallow axis)
  for (const b of boxes) {
    const px = f.pos.x - b.x, pz = f.pos.z - b.z, hw = b.w / 2 + 0.45, hd = b.d / 2 + 0.45;
    if (Math.abs(px) < hw && Math.abs(pz) < hd && f.pos.y < b.y1 - 0.3) {
      const ox = hw - Math.abs(px), oz = hd - Math.abs(pz);
      if (ox < oz) f.pos.x = b.x + Math.sign(px || 1) * hw; else f.pos.z = b.z + Math.sign(pz || 1) * hd;
    }
  }
  const lim = HALF - 15;
  f.pos.x = Math.max(-lim, Math.min(lim, f.pos.x)); f.pos.z = Math.max(-lim, Math.min(lim, f.pos.z));
}

function updateSky(f, dt, mx, mz) {
  const g = groundY(f.pos.x, f.pos.z);
  if (!f.chuteOpen && f.pos.y - g < 45) f.chuteOpen = true;
  f.chute.visible = f.chuteOpen;
  f.pos.y -= (f.chuteOpen ? 6 : 28) * dt;
  moveFighter(f, mx, mz, dt, f.chuteOpen ? 11 : 20);
  if (f.pos.y <= g) { f.pos.y = g; f.state = "ground"; f.chuteOpen = false; f.chute.visible = false; }
}

function updateGround(f, dt, mx, mz, sprint) {
  const w = weaponOf(f);
  const speed = (f.crouch ? 2.4 : sprint ? 6.4 : 4.6) * (w === WEAPONS.lmg ? 0.88 : 1) * (f.pos.y < 0.5 ? 0.6 : 1);
  moveFighter(f, mx, mz, dt, speed);
  const g = groundY(f.pos.x, f.pos.z);
  f.vy -= 22 * dt; f.pos.y += f.vy * dt;
  if (f.pos.y <= g) { f.pos.y = g; f.vy = 0; f.grounded = true; } else f.grounded = false;
  finishReload(f);
  if (f.healUntil && M.t >= f.healUntil) { f.healUntil = 0; f.hp = Math.min(200, f.hp + 75); }
  if (f.energy > 0 && f.hp < 200) { const a = Math.min(f.energy, 5 * dt); f.energy -= a; f.hp = Math.min(200, f.hp + a); }
  autoPickup(f);
}

function heal(f) {
  if (f.meds <= 0 || f.hp >= 200 || f.healUntil) return false;
  f.meds--; f.healUntil = M.t + 4; return true;
}

// ---------------------------------------------------------------- bots
function botUpdate(f, dt) {
  if (f.state === "plane") { if (M.plane.t > f.jumpAt || M.plane.t >= M.plane.dur) jump(f); return; }
  f.think -= dt;
  const z = M.zone;
  if (f.think <= 0) {
    f.think = 0.4 + Math.random() * 0.4;
    // pick target: nearest visible enemy within 70 m
    let best = null, bestD = 70;
    for (const o of M.fighters) {
      if (o === f || !o.alive || o.state === "plane") continue;
      const d = o.pos.distanceTo(f.pos);
      if (d < bestD) { bestD = d; best = o; }
    }
    f.target = best && (f.target === best || Math.random() < 0.8) ? best : f.target && f.target.alive ? f.target : null;
    if (f.hp < 90 && f.meds > 0 && !f.healUntil) heal(f);
    // wander towards loot or the zone
    const outside = Math.hypot(f.pos.x - z.toX, f.pos.z - z.toZ) > z.toR * 0.85;
    if (outside || !f.wander || f.pos.distanceTo(f.wander) < 3) {
      const nearLoot = !outside && M.loot.find((l) => Math.abs(l.x - f.pos.x) < 25 && Math.abs(l.z - f.pos.z) < 25 && (l.kind !== "weapon" || weaponScore(l.id) > weaponScore(f.weapons[f.active])));
      if (nearLoot) f.wander = new THREE.Vector3(nearLoot.x, 0, nearLoot.z);
      else {
        const a = Math.random() * Math.PI * 2, r = Math.random() * z.toR * 0.6;
        f.wander = new THREE.Vector3(z.toX + Math.cos(a) * r, 0, z.toZ + Math.sin(a) * r);
      }
    }
    if (weaponOf(f).ammo && f.mags[f.active] === 0) reload(f);
    if (f.weapons[1] && weaponScore(f.weapons[1]) > weaponScore(f.weapons[0]) && f.active === 0) f.active = 1;
  }
  let mx = 0, mz = 0;
  const t = f.target && f.target.alive ? f.target : null;
  if (f.state === "sky") { if (f.wander) { mx = f.wander.x - f.pos.x; mz = f.wander.z - f.pos.z; } updateSky(f, dt, mx, mz); return; }
  if (t) {
    const d = t.pos.distanceTo(f.pos), w = weaponOf(f);
    const want = w.ammo ? Math.min(w.range * 0.6, 35) : 1.5;
    mx = t.pos.x - f.pos.x; mz = t.pos.z - f.pos.z;
    if (d < want) { const s = Math.sin(M.t * 1.7 + f.skill * 10), ox = mx; mx = -mz * s; mz = ox * s; }   // strafe
    f.yaw = Math.atan2(t.pos.x - f.pos.x, t.pos.z - f.pos.z);
    if (d < w.range && M.t > (f.reactAt || 0)) {
      _o.set(f.pos.x, f.pos.y + 1.5, f.pos.z);
      _d.set(t.pos.x - _o.x, t.pos.y + 1.0 - _o.y, t.pos.z - _o.z).normalize();
      const inaccuracy = (w.spread || 1) * (1.4 - f.skill) * (1 + d / 60) + (t.isPlayer ? 0.8 : 0);
      shoot(f, _o, _d, inaccuracy);
    }
  } else if (f.wander) { mx = f.wander.x - f.pos.x; mz = f.wander.z - f.pos.z; f.yaw = Math.atan2(mx, mz); f.reactAt = M.t + 0.6; }
  updateGround(f, dt, mx, mz, !t);
}

function jump(f) {
  f.state = "sky";
  const p = planePos();
  f.pos.set(p.x + (Math.random() - 0.5) * 4, p.y - 3, p.z + (Math.random() - 0.5) * 4);
  f.mesh.visible = true;
  if (!f.isPlayer) {
    const target = lootPoints[Math.floor(Math.random() * lootPoints.length)];
    f.wander = new THREE.Vector3(target.x, 0, target.z);
  }
}
function planePos() { return M.plane.from.clone().lerp(M.plane.to, Math.min(1, M.plane.t / M.plane.dur)); }

// ---------------------------------------------------------------- storm
const ZONE_PHASES = [
  { wait: 45, shrink: 35, r: 190, dps: 2 }, { wait: 35, shrink: 30, r: 115, dps: 4 },
  { wait: 30, shrink: 25, r: 60, dps: 7 }, { wait: 25, shrink: 20, r: 25, dps: 11 }, { wait: 20, shrink: 20, r: 0, dps: 18 },
];
const zoneWall = new THREE.Mesh(new THREE.CylinderGeometry(1, 1, 300, 64, 1, true),
  new THREE.MeshBasicMaterial({ color: 0x6aa0ff, transparent: true, opacity: 0.22, side: THREE.DoubleSide, depthWrite: false }));
zoneWall.visible = false; scene.add(zoneWall);

function updateZone(dt) {
  const z = M.zone;
  if (M.t < 20) return;                      // storm starts after the drop
  zoneWall.visible = true;
  const now = M.t - 20;
  if (z.phase < 0 || (z.state === "shrink" && now >= z.t1)) {
    z.phase++;
    if (z.phase >= ZONE_PHASES.length) { z.state = "done"; }
    else {
      const ph = ZONE_PHASES[z.phase];
      z.fromX = z.cx; z.fromZ = z.cz; z.fromR = z.r;
      const a = Math.random() * Math.PI * 2, m = Math.random() * Math.max(0, z.r - ph.r) * 0.8;
      z.toX = Math.max(-200, Math.min(200, z.cx + Math.cos(a) * m)); z.toZ = Math.max(-200, Math.min(200, z.cz + Math.sin(a) * m)); z.toR = ph.r;
      z.state = "wait"; z.t0 = now + ph.wait; z.t1 = z.t0 + ph.shrink; z.dps = ph.dps;
      banner(z.phase === 0 ? "The storm is coming!" : "Storm moving — head to the safe zone", 2.5);
    }
  }
  if (z.state === "wait" && now >= z.t0) z.state = "shrink";
  if (z.state === "shrink") {
    const k = Math.min(1, (now - z.t0) / (z.t1 - z.t0));
    z.cx = z.fromX + (z.toX - z.fromX) * k; z.cz = z.fromZ + (z.toZ - z.fromZ) * k; z.r = z.fromR + (z.toR - z.fromR) * k;
  }
  zoneWall.position.set(z.cx, 100, z.cz); zoneWall.scale.set(Math.max(1, z.r), 1, Math.max(1, z.r));
  z.tick = (z.tick || 0) + dt;
  if (z.tick >= 1) {
    z.tick = 0;
    for (const f of M.fighters) if (f.alive && f.state !== "plane" && Math.hypot(f.pos.x - z.cx, f.pos.z - z.cz) > z.r) damage(f, z.dps, null, false, "storm");
  }
}

// ====================================================================== player input
let camYaw = 0, camPitch = 0.25;
const input = { mx: 0, mz: 0, sprint: false };
const keys = {};
addEventListener("keydown", (e) => { keys[e.code] = true; if (e.code === "Space") press("jump"); if (e.code === "KeyR") press("reload"); if (e.code === "KeyC") press("crouch"); if (e.code === "KeyH") press("heal"); if (e.code === "KeyF") press("pick"); if (e.code === "Digit1") switchSlot(0); if (e.code === "Digit2") switchSlot(1); });
addEventListener("keyup", (e) => { keys[e.code] = false; });

function bindHold(id, down, up) {
  const el = $(id);
  el.addEventListener("pointerdown", (e) => { e.preventDefault(); try { el.setPointerCapture(e.pointerId); } catch {} el.classList.add("on"); down(e); });
  const end = (e) => { el.classList.remove("on"); up && up(e); };
  el.addEventListener("pointerup", end); el.addEventListener("pointercancel", end);
  return el;
}
// Look pad + fire buttons that can also be dragged to aim
function lookDrag(el) {
  let last = null;
  el.addEventListener("pointerdown", (e) => { last = { x: e.clientX, y: e.clientY, id: e.pointerId }; });
  el.addEventListener("pointermove", (e) => {
    if (!last || e.pointerId !== last.id) return;
    const k = 0.0045 * settings.sens / (M && M.aim ? weaponOf(M.me).zoom : 1);
    camYaw -= (e.clientX - last.x) * k; camPitch += (e.clientY - last.y) * k;
    camPitch = Math.max(-0.9, Math.min(1.1, camPitch));
    last.x = e.clientX; last.y = e.clientY;
  });
  const end = (e) => { if (last && e.pointerId === last.id) last = null; };
  el.addEventListener("pointerup", end); el.addEventListener("pointercancel", end);
}
lookDrag($("look")); lookDrag($("b-fire")); lookDrag($("b-fire2"));
for (const id of ["b-fire", "b-fire2"]) bindHold(id, () => { if (M) { M.fireHeld = true; M.firePressed = true; } }, () => { if (M) M.fireHeld = false; });
bindHold("b-aim", () => press("aim"));
bindHold("b-jump", () => press("jump"));
bindHold("b-crouch", () => press("crouch"));
bindHold("b-reload", () => press("reload"));
bindHold("b-heal", () => press("heal"));
bindHold("b-pick", () => press("pick"));
let quitArmed = 0;   // WebView suppresses confirm(), so ask for a second tap instead
bindHold("b-quit", () => {
  if (!M || M.over) return;
  if (performance.now() - quitArmed > 2500) { quitArmed = performance.now(); banner("Tap QUIT again to leave", 2.5); return; }
  M.me.alive = false; M.me.placement = M.fighters.filter((x) => x.alive).length + 1; finish(false);
});
$("slot0").addEventListener("pointerdown", () => switchSlot(0));
$("slot1").addEventListener("pointerdown", () => switchSlot(1));


// Joystick
(() => {
  const zone = $("joy"), knob = $("joyKnob"), ring = $("joyRing");
  let id = null, cx = 0, cy = 0;
  zone.addEventListener("pointerdown", (e) => {
    id = e.pointerId; try { zone.setPointerCapture(id); } catch {}
    const r = zone.getBoundingClientRect(); cx = e.clientX - r.left; cy = e.clientY - r.top;
    ring.style.left = cx - 70 + "px"; ring.style.bottom = r.height - cy - 70 + "px";
    move(e);
  });
  function move(e) {
    if (e.pointerId !== id) return;
    const r = zone.getBoundingClientRect();
    let dx = e.clientX - r.left - cx, dy = e.clientY - r.top - cy;
    const len = Math.hypot(dx, dy), max = 60;
    if (len > max) { dx = dx / len * max; dy = dy / len * max; }
    knob.style.left = cx + dx - 30 + "px"; knob.style.bottom = r.height - (cy + dy) - 30 + "px";
    input.mx = dx / max; input.mz = dy / max;
    input.sprint = len > max * 1.25 && dy < -max * 0.7;
  }
  zone.addEventListener("pointermove", move);
  const end = (e) => { if (e.pointerId !== id) return; id = null; input.mx = input.mz = 0; input.sprint = false;
    knob.style.left = "110px"; knob.style.bottom = "100px"; ring.style.left = "70px"; ring.style.bottom = "60px"; };
  zone.addEventListener("pointerup", end); zone.addEventListener("pointercancel", end);
})();

function switchSlot(i) { if (M && M.me.weapons[i]) { M.me.active = i; M.me.reloadUntil = 0; M.me.pendingReload = false; } }

function press(action) {
  if (!M || M.over) return;
  const me = M.me;
  if (action === "jump") {
    if (me.state === "plane") { jump(me); banner("", 0); }
    else if (me.state === "sky") me.chuteOpen = true;
    else if (me.grounded) { if (me.crouch) me.crouch = false; else me.vy = 7; }
  } else if (action === "crouch") me.crouch = !me.crouch;
  else if (action === "reload") reload(me);
  else if (action === "aim") M.aim = !M.aim;
  else if (action === "heal") { if (!heal(me)) toast(me.meds ? "Already at full HP" : "No med kits"); }
  else if (action === "pick" && M.nearWeapon) { tryPickup(me, M.nearWeapon); toast("Picked up " + WEAPONS[me.weapons[me.active]].name); }
}

function updatePlayer(dt) {
  const me = M.me;
  if (!me.alive) return;
  let mx = input.mx, mz = input.mz;
  if (keys.KeyW) mz = -1; if (keys.KeyS) mz = 1; if (keys.KeyA) mx = -1; if (keys.KeyD) mx = 1;
  const sprint = input.sprint || keys.ShiftLeft;
  // camera-relative movement
  const sin = Math.sin(camYaw), cos = Math.cos(camYaw);
  const wx = mx * cos + mz * sin, wz = -mx * sin + mz * cos;
  if (me.state === "plane") { me.pos.copy(planePos()); return; }
  if (me.state === "sky") updateSky(me, dt, wx, wz);
  else updateGround(me, dt, wx, wz, sprint && !M.aim);
  me.yaw = camYaw + Math.PI;
  // shooting along the camera ray
  const w = weaponOf(me);
  const want = M.fireHeld && (w.auto || M.firePressed);
  M.firePressed = false;
  if (want && me.state === "ground") {
    camera.getWorldDirection(_d);
    const origin = camera.position.clone().addScaledVector(_d, M.aim && w.zoom >= 4 ? 0.5 : 3.6);
    const spread = M.aim ? (w.spread || 0) * 0.35 : (w.hipSpread || w.spread || 0) * (Math.hypot(mx, mz) > 0.1 ? 1.5 : 1) * (me.crouch ? 0.75 : 1);
    shoot(me, origin, _d.clone(), spread);
  }
  // nearby weapon to swap
  M.nearWeapon = me.state === "ground" ? M.loot.find((l) => l.kind === "weapon" && Math.abs(l.x - me.pos.x) < 2 && Math.abs(l.z - me.pos.z) < 2) : null;
}

// ====================================================================== HUD
let bannerUntil = 0;
function banner(text, secs) { $("banner").textContent = text; bannerUntil = performance.now() + secs * 1000; }
const mini = $("mini").getContext("2d");
function updateHud() {
  const me = M.me, z = M.zone;
  const alive = M.fighters.filter((f) => f.alive).length;
  let zoneTxt = "";
  if (z.state === "wait") zoneTxt = ` · Storm moves in ${Math.max(0, Math.ceil(z.t0 - (M.t - 20)))}s`;
  else if (z.state === "shrink") zoneTxt = ` · Storm closing ${Math.max(0, Math.ceil(z.t1 - (M.t - 20)))}s`;
  else if (M.t < 20) zoneTxt = " · Drop zone";
  const outside = Math.hypot(me.pos.x - z.cx, me.pos.z - z.cz) > z.r && M.t > 20;
  $("top").innerHTML = `Alive ${alive} · Kills ${me.kills}${zoneTxt}${outside ? ' · <span style="color:#ff6060">OUTSIDE ZONE</span>' : ""}`;
  const town = TOWNS.find((t) => Math.hypot(me.pos.x - t.x, me.pos.z - t.z) < t.r);
  $("hint").textContent = me.state === "plane" ? "" : me.healUntil ? "Healing…" : me.pendingReload ? "Reloading…" : town ? town.name : "";
  $("hpbar").firstChild.style.width = me.hp / 2 + "%";
  $("armbar").firstChild.style.width = (me.vest ? me.vestDur / ARMOR_DUR[me.vest] * 100 : 0) + "%";
  $("enbar").firstChild.style.width = me.energy / 2 + "%";
  $("armtxt").textContent = `HP ${Math.ceil(me.hp)} · Vest Lv${me.vest} · Helmet Lv${me.helmet} · Med ×${me.meds}`;
  const w = weaponOf(me);
  $("ammo").innerHTML = w.ammo ? `${me.mags[me.active]}<span style="font-size:16px"> / ${me.ammo[w.ammo]}</span>` : "";
  for (let i = 0; i < 2; i++) {
    const s = $("slot" + i);
    s.textContent = me.weapons[i] ? WEAPONS[me.weapons[i]].name : "—";
    s.classList.toggle("act", i === me.active);
  }
  const pick = $("b-pick");
  pick.style.display = M.nearWeapon ? "flex" : "none";
  if (M.nearWeapon) pick.textContent = "PICK " + WEAPONS[M.nearWeapon.id].name;
  $("b-jump").textContent = me.state === "plane" ? "JUMP" : me.state === "sky" && !me.chuteOpen ? "CHUTE" : "JUMP";
  $("b-aim").classList.toggle("on", M.aim);
  $("b-crouch").classList.toggle("on", me.crouch);
  if (performance.now() > bannerUntil) $("banner").textContent = "";

  // minimap (whole island)
  const s = 130 / (HALF * 2), cx = (x) => (x + HALF) * s, cz = (zz) => (zz + HALF) * s;
  mini.clearRect(0, 0, 130, 130);
  mini.fillStyle = "#2b6f9e"; mini.fillRect(0, 0, 130, 130);
  mini.fillStyle = "#5a8f3c"; mini.beginPath(); mini.arc(65, 65, 285 * s, 0, Math.PI * 2); mini.fill();
  mini.fillStyle = "#bdb6aa"; for (const t of TOWNS) { mini.beginPath(); mini.arc(cx(t.x), cz(t.z), t.r * s * 0.8, 0, 7); mini.fill(); }
  if (M.t > 20) {
    mini.strokeStyle = "#7fb0ff"; mini.lineWidth = 2; mini.beginPath(); mini.arc(cx(z.cx), cz(z.cz), z.r * s, 0, 7); mini.stroke();
    mini.strokeStyle = "#fff"; mini.lineWidth = 1; mini.beginPath(); mini.arc(cx(z.toX), cz(z.toZ), z.toR * s, 0, 7); mini.stroke();
  }
  if (me.state === "plane") { const p = M.plane; mini.strokeStyle = "#ffffffaa"; mini.beginPath(); mini.moveTo(cx(p.from.x), cz(p.from.z)); mini.lineTo(cx(p.to.x), cz(p.to.z)); mini.stroke(); }
  const px = cx(me.pos.x), pz = cz(me.pos.z);
  mini.save(); mini.translate(px, pz); mini.rotate(-camYaw + Math.PI);
  mini.fillStyle = "#ffd23f"; mini.beginPath(); mini.moveTo(0, -6); mini.lineTo(4, 4); mini.lineTo(-4, 4); mini.fill(); mini.restore();
}

// ====================================================================== results
function finish(won) {
  if (M.over) return;
  M.over = true;
  const me = M.me;
  if (won) me.placement = 1;
  banner(won ? "STORM CHAMPION!" : `ELIMINATED — #${me.placement}`, 3);
  setTimeout(() => {
    $("r-title").textContent = won ? "STORM CHAMPION!" : `#${me.placement} of 20`;
    $("r-stats").innerHTML = `<div class="item"><span>Eliminations</span><b>${me.kills}</b></div>
      <div class="item"><span>Damage</span><b>${Math.round(me.damage)}</b></div>
      <div class="item"><span>Survived</span><b>${Math.floor(M.t / 60)}:${String(Math.floor(M.t % 60)).padStart(2, "0")}</b></div>`;
    endCleanup();
    zoneWall.visible = false;
    show("results");
  }, 2600);
}

// ====================================================================== main loop
let lastT = performance.now();
const camOffset = new THREE.Vector3();
function frame(now) {
  requestAnimationFrame(frame);
  const dt = Math.min(0.05, (now - lastT) / 1000); lastT = now;
  if (!M) { // idle camera orbit behind menus
    const a = now * 0.00005; camera.position.set(Math.cos(a) * 260, 90, Math.sin(a) * 260); camera.lookAt(0, 10, 0);
    renderer.render(scene, camera); return;
  }
  step(dt);
  if (!M) return;
  renderMatch(dt);
}

function step(dt) {
  M.t += dt;
  if (!M.over) {
    M.plane.t += dt;
    if (M.plane.t >= M.plane.dur && M.me.state === "plane") jump(M.me);
    const pp = planePos(); M.planeMesh.position.copy(pp);
    M.planeMesh.lookAt(M.plane.to); M.planeMesh.visible = M.plane.t < M.plane.dur + 3;
    updatePlayer(dt);
    for (const f of M.fighters) if (!f.isPlayer && f.alive) botUpdate(f, dt);
    updateZone(dt);
    if (!M.over) {
      const alive = M.fighters.filter((f) => f.alive);
      if (alive.length === 1 && alive[0].isPlayer) finish(true);
    }
  }
}

function renderMatch(dt) {
  for (const f of M.fighters) {
    if (!f.alive || f.state === "plane") continue;
    f.mesh.position.copy(f.pos); f.mesh.rotation.y = f.yaw;
    f.mesh.scale.y = f.crouch && f.state === "ground" ? 0.7 : 1;
  }
  for (let i = tracers.length - 1; i >= 0; i--) if (M.t > tracers[i].until) { scene.remove(tracers[i].line); tracers[i].line.geometry.dispose(); tracers.splice(i, 1); }
  for (const l of M.loot) l.mesh.rotation.y += dt * 1.5;

  // camera
  const me = M.me;
  const w = weaponOf(me);
  const zoom = M.aim && me.state === "ground" ? w.zoom : 1;
  camera.fov += ((70 / zoom) - camera.fov) * Math.min(1, dt * 12); camera.updateProjectionMatrix();
  const focus = me.state === "plane" ? planePos() : me.pos;
  const dist = me.state === "plane" ? 22 : me.state === "sky" ? 9 : zoom >= 4 ? 0.01 : zoom > 1 ? 2.2 : 3.6;
  camOffset.set(Math.sin(camYaw) * Math.cos(camPitch), Math.sin(camPitch), Math.cos(camYaw) * Math.cos(camPitch)).multiplyScalar(dist);
  const shoulder = me.state === "ground" && zoom < 4 ? (zoom > 1 ? 0.7 : 1.05) : 0;
  camera.position.set(focus.x + camOffset.x + Math.cos(camYaw) * shoulder, focus.y + 2.0 + camOffset.y, focus.z + camOffset.z - Math.sin(camYaw) * shoulder);
  const minY = groundY(camera.position.x, camera.position.z) + 0.4;
  if (camera.position.y < minY) camera.position.y = minY;
  camera.rotation.order = "YXZ";
  camera.rotation.set(-camPitch, camYaw, 0);
  me.mesh.visible = me.state !== "plane" && me.alive && zoom < 4;
  if (!M.over) updateHud();
  renderer.render(scene, camera);
}
requestAnimationFrame(frame);

// ====================================================================== wiring
$("btn-guest").onclick = guestLogin;
$("btn-offline").onclick = () => { net.me = null; showLobby(); };
$("btn-play").onclick = () => startMatch();
$("btn-back").onclick = () => { refreshMe(); showLobby(); };
$("tab-missions").onclick = () => tab("missions");
$("tab-store").onclick = () => tab("store");
$("tab-reward").onclick = () => tab("reward");
$("tab-settings").onclick = () => tab("settings");
$("lnk-privacy").onclick = (e) => { e.preventDefault(); location.href = SITE + "/privacy"; };
$("lnk-terms").onclick = (e) => { e.preventDefault(); location.href = SITE + "/terms"; };
window.SD = { startMatch, press, get match() { return M; }, net,
  // test hook: advance the simulation without rendering
  simulate(seconds) { for (let t = 0; t < seconds && M && !M.over; t += 0.05) step(0.05); } };   // for automated tests
checkHealth().then(() => { if (net.token && net.online) guestLogin(); });
})();
