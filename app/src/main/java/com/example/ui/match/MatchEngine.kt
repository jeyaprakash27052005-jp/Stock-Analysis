package com.example.ui.match

import androidx.compose.ui.graphics.Color
import com.example.data.model.GameCatalog
import com.example.data.model.GameMode
import com.example.data.model.WeaponDef
import kotlin.math.PI
import kotlin.math.abs
import kotlin.math.atan2
import kotlin.math.cos
import kotlin.math.hypot
import kotlin.math.max
import kotlin.math.min
import kotlin.math.sin
import kotlin.random.Random

enum class FighterState { PLANE, SKY, GROUND }

enum class LootKind { WEAPON, AMMO, MED, SHROOM, VEST, HELMET, AIRDROP }

data class TownPoi(
    val name: String,
    val x: Float,
    val z: Float,
    val radius: Float,
    val color: Color,
)

data class BuildingBox(
    val x: Float,
    val z: Float,
    val w: Float,
    val d: Float,
    val color: Color,
)

data class TreeProp(
    val x: Float,
    val z: Float,
    val radius: Float,
)

data class LootItem(
    val uid: Int,
    val kind: LootKind,
    val id: String,
    val count: Int,
    val x: Float,
    val z: Float,
    val label: String,
    val color: Color,
)

data class TracerShot(
    val x1: Float,
    val z1: Float,
    val x2: Float,
    val z2: Float,
    val until: Float,
    val isPlayer: Boolean,
)

data class FoamWallObj(
    val id: Int,
    val x: Float,
    val z: Float,
    val angleRad: Float,
    var hp: Float,
    val expiresAt: Float,
)

data class Fighter(
    val id: Int,
    val name: String,
    val team: Int,
    val isPlayer: Boolean,
    val color: Color,
    var x: Float = 0f,
    var z: Float = 0f,
    var altitude: Float = 140f,
    var yaw: Float = 0f,
    var hp: Float = 200f,
    var shieldHp: Float = 0f,
    var shieldUntil: Float = 0f,
    var energy: Float = 0f,
    var vestLevel: Int = 0,
    var vestDur: Float = 0f,
    var helmetLevel: Int = 0,
    var helmetDur: Float = 0f,
    var state: FighterState = FighterState.PLANE,
    var chuteOpen: Boolean = false,
    var alive: Boolean = true,
    var crouch: Boolean = false,
    val weapons: Array<String?> = arrayOf("fists", null),
    val mags: IntArray = intArrayOf(0, 0),
    var activeSlot: Int = 0,
    val ammo: MutableMap<String, Int> = mutableMapOf(
        "light" to 0, "medium" to 0, "heavy" to 0, "shells" to 0, "sniper" to 0
    ),
    var meds: Int = 0,
    var foamWalls: Int = 2,
    var kills: Int = 0,
    var damageDealt: Int = 0,
    var headshots: Int = 0,
    var nextShotAt: Float = 0f,
    var reloadUntil: Float = 0f,
    var pendingReload: Boolean = false,
    var healUntil: Float = 0f,
    var jumpAt: Float = 0f,
    var targetId: Int? = null,
    var wanderX: Float = 0f,
    var wanderZ: Float = 0f,
    var hasWander: Boolean = false,
    var thinkTimer: Float = 0f,
    var skillLevel: Float = 0.45f,
    var placement: Int = 0,
    var revealUntil: Float = 0f,
    var sprintBoostUntil: Float = 0f,
    var skillCooldownUntil: Float = 0f,
)

data class ZoneState(
    var cx: Float = 0f,
    var cz: Float = 0f,
    var radius: Float = 320f,
    var fromX: Float = 0f,
    var fromZ: Float = 0f,
    var fromR: Float = 320f,
    var toX: Float = 0f,
    var toZ: Float = 0f,
    var toR: Float = 190f,
    var phase: Int = -1,
    var state: String = "wait", // wait, shrink, done
    var t0: Float = 20f,
    var t1: Float = 55f,
    var dps: Float = 2f,
    var tickTimer: Float = 0f,
)

data class ZonePhaseCfg(val wait: Float, val shrink: Float, val r: Float, val dps: Float)

class MatchSimulation(
    val mode: GameMode,
    val playerName: String,
    val characterId: String,
    val petId: String?,
) {
    companion object {
        const val HALF = 300f
        val ARMOR_REDUCTION = floatArrayOf(0f, 0.2f, 0.3f, 0.4f, 0.5f)
        val ARMOR_DURABILITY = floatArrayOf(0f, 100f, 150f, 200f, 260f)
        val ZONE_PHASES = listOf(
            ZonePhaseCfg(35f, 28f, 190f, 2f),
            ZonePhaseCfg(28f, 24f, 115f, 4f),
            ZonePhaseCfg(22f, 20f, 60f, 7f),
            ZonePhaseCfg(18f, 16f, 24f, 11f),
            ZonePhaseCfg(15f, 15f, 0f, 18f),
        )
        private val BOT_NAMES = listOf(
            "Vex", "Kira", "Dusk", "Rho", "Juno", "Mako", "Nyx", "Tango", "Blaze", "Orin",
            "Zed", "Pike", "Lumen", "Sable", "Fen", "Quill", "Ash", "Brick", "Echo", "Wren"
        )
    }

    val isNight = Random.nextFloat() < 0.30f
    val towns = listOf(
        TownPoi("Ironhaven City", -120f, 60f, 68f, Color(0xFF8C867D)),
        TownPoi("Fort Basalt", 130f, -120f, 55f, Color(0xFF5D6A4C)),
        TownPoi("Goldmeadow Farms", -140f, -140f, 58f, Color(0xFFA8433A)),
        TownPoi("Rustport", 30f, 210f, 48f, Color(0xFF3B6FB8)),
        TownPoi("Frostspine Peaks", 150f, 140f, 52f, Color(0xFFD8E4F0)),
    )

    val buildings = mutableListOf<BuildingBox>()
    val trees = mutableListOf<TreeProp>()
    val loot = mutableListOf<LootItem>()
    val fighters = mutableListOf<Fighter>()
    val tracers = mutableListOf<TracerShot>()
    val foamWalls = mutableListOf<FoamWallObj>()
    val feed = mutableListOf<String>()

    val zone = ZoneState()
    var elapsed = 0f
    var over = false
    var won = false
    var bannerText = ""
    var bannerUntil = 0f
    var hitFlashUntil = 0f
    var hitWasHeadshot = false
    var damageFlashUntil = 0f
    var aimMode = false
    var autoFireEnabled = false

    // Plane trajectory
    var planeFromX = -290f
    var planeFromZ = -80f
    var planeToX = 290f
    var planeToZ = 80f
    var planeT = 0f
    val planeDur = 16f

    private var nextLootUid = 1
    private var nextWallId = 1
    private var teamScores = intArrayOf(0, 0)
    private val spawnedAirdropPhases = mutableSetOf<Int>()

    val player: Fighter
        get() = fighters.first { it.isPlayer }

    init {
        generateWorld()
        setupMatch()
    }

    private fun generateWorld() {
        val rng = Random(42)
        for (town in towns) {
            val count = when (town.name) {
                "Ironhaven City" -> 12
                "Fort Basalt" -> 8
                else -> 6
            }
            repeat(count) {
                val bx = town.x + (rng.nextFloat() * 2f - 1f) * town.radius * 0.72f
                val bz = town.z + (rng.nextFloat() * 2f - 1f) * town.radius * 0.72f
                val bw = 10f + rng.nextFloat() * 9f
                val bd = 10f + rng.nextFloat() * 9f
                val overlaps = buildings.any { b ->
                    abs(b.x - bx) < (b.w + bw) / 2f + 4f && abs(b.z - bz) < (b.d + bd) / 2f + 4f
                }
                if (!overlaps) {
                    buildings.add(BuildingBox(bx, bz, bw, bd, town.color))
                    val tier = if (town.name == "Fort Basalt") 2 else 1
                    spawnLootNear(bx + (rng.nextFloat() - 0.5f) * 14f, bz + (rng.nextFloat() - 0.5f) * 14f, tier, rng)
                }
            }
        }
        repeat(110) {
            val tx = (rng.nextFloat() * 2f - 1f) * 255f
            val tz = (rng.nextFloat() * 2f - 1f) * 255f
            if (hypot(tx, tz) < 260f && towns.none { hypot(tx - it.x, tz - it.z) < it.radius * 0.85f }) {
                trees.add(TreeProp(tx, tz, 2.2f + rng.nextFloat() * 1.4f))
                if (rng.nextFloat() < 0.25f) spawnLootNear(tx + 3f, tz + 3f, 0, rng)
            }
        }
    }

    private fun spawnLootNear(x: Float, z: Float, tier: Int, rng: Random) {
        val roll = rng.nextFloat()
        val item = when {
            roll < 0.34f -> {
                val wId = when (tier) {
                    2 -> listOf("ar", "halberd", "lmg", "sniper", "dmr").random(rng)
                    1 -> listOf("ar", "smg", "hornet", "shotgun", "sniper").random(rng)
                    else -> listOf("pistol", "revolver", "smg", "shotgun").random(rng)
                }
                val def = GameCatalog.weapons[wId]!!
                LootItem(nextLootUid++, LootKind.WEAPON, wId, 1, x, z, def.name, Color(0xFFFFB347))
            }
            roll < 0.58f -> {
                val ammoKey = listOf("light", "medium", "heavy", "shells", "sniper").random(rng)
                val qty = mapOf("light" to 30, "medium" to 30, "heavy" to 40, "shells" to 12, "sniper" to 8)[ammoKey] ?: 20
                LootItem(nextLootUid++, LootKind.AMMO, ammoKey, qty, x, z, "${GameCatalog.ammoNames[ammoKey]} ×$qty", Color(0xFFD8D8D8))
            }
            roll < 0.72f -> LootItem(nextLootUid++, LootKind.MED, "med", 1, x, z, "Med Kit", Color(0xFF44D17A))
            roll < 0.84f -> LootItem(nextLootUid++, LootKind.SHROOM, "shroom", 1, x, z, "Vigor Shroom", Color(0xFFB07CFF))
            roll < 0.92f -> {
                val lv = (tier + 1).coerceIn(1, 3)
                LootItem(nextLootUid++, LootKind.VEST, lv.toString(), 1, x, z, "Vest Lv$lv", Color(0xFF4AA3FF))
            }
            else -> {
                val lv = (tier + 1).coerceIn(1, 3)
                LootItem(nextLootUid++, LootKind.HELMET, lv.toString(), 1, x, z, "Helmet Lv$lv", Color(0xFF4AA3FF))
            }
        }
        loot.add(item)
    }

    private fun setupMatch() {
        val me = Fighter(
            id = 0,
            name = playerName,
            team = 0,
            isPlayer = true,
            color = Color(0xFFFF8C1A),
            yaw = 0f,
        )
        if (characterId == "char_ivy") me.energy = 40f

        if (mode.isBattleRoyale) {
            val angle = Random.nextFloat() * (2f * PI.toFloat())
            val dx = cos(angle)
            val dz = sin(angle)
            val sideX = -dz
            val sideZ = dx
            val offset = (Random.nextFloat() - 0.5f) * 140f
            planeFromX = -dx * 290f + sideX * offset
            planeFromZ = -dz * 290f + sideZ * offset
            planeToX = dx * 290f + sideX * offset
            planeToZ = dz * 290f + sideZ * offset
            me.x = planeFromX
            me.z = planeFromZ
            me.yaw = atan2(dx, -dz)
            fighters.add(me)

            val total = mode.maxPlayers
            for (i in 1 until total) {
                val team = i / mode.teamSize
                val isTeammate = team == 0 && mode.teamSize > 1
                fighters.add(
                    Fighter(
                        id = i,
                        name = BOT_NAMES[(i - 1) % BOT_NAMES.size],
                        team = team,
                        isPlayer = false,
                        color = if (isTeammate) Color(0xFF5AB8FF) else Color.hsv((i * 37f) % 360f, 0.65f, 0.78f),
                        state = FighterState.PLANE,
                        jumpAt = 1.5f + Random.nextFloat() * 12f,
                        skillLevel = 0.35f + Random.nextFloat() * 0.4f,
                    )
                )
            }
            showBanner("Tap JUMP to drop from the plane!", 4f)
        } else if (mode == GameMode.TRAINING) {
            me.state = FighterState.GROUND
            me.altitude = 0f
            me.x = -120f
            me.z = 60f
            me.weapons[0] = "ar"
            me.mags[0] = 30
            me.weapons[1] = "sniper"
            me.mags[1] = 5
            me.ammo["medium"] = 180
            me.ammo["sniper"] = 40
            me.ammo["light"] = 120
            me.ammo["shells"] = 40
            me.ammo["heavy"] = 160
            me.meds = 5
            me.vestLevel = 2
            me.vestDur = ARMOR_DURABILITY[2]
            fighters.add(me)
            for (i in 1..6) {
                val ang = i * (2f * PI.toFloat() / 6f)
                fighters.add(
                    Fighter(
                        id = i,
                        name = "Target-${i}",
                        team = 1,
                        isPlayer = false,
                        color = Color(0xFFE5484D),
                        x = -120f + cos(ang) * 32f,
                        z = 60f + sin(ang) * 32f,
                        altitude = 0f,
                        state = FighterState.GROUND,
                        weapons = arrayOf("pistol", null),
                        mags = intArrayOf(12, 0),
                        skillLevel = 0.25f,
                    )
                )
            }
            showBanner("Training Ground — Practice Weapons & Skills", 3.5f)
        } else {
            // Clash Squad / Team Deathmatch in Ironhaven City
            me.state = FighterState.GROUND
            me.altitude = 0f
            me.x = -155f
            me.z = 60f
            me.weapons[0] = if (mode == GameMode.CLASH_SQUAD) "smg" else "ar"
            me.mags[0] = GameCatalog.weapons[me.weapons[0]]!!.magSize
            me.weapons[1] = "pistol"
            me.mags[1] = 12
            me.ammo["light"] = 90
            me.ammo["medium"] = 90
            me.meds = 2
            me.vestLevel = 1
            me.vestDur = ARMOR_DURABILITY[1]
            fighters.add(me)

            for (i in 1..7) {
                val team = if (i <= 3) 0 else 1
                val sx = if (team == 0) -155f else -85f
                val sz = 40f + (i % 4) * 14f
                fighters.add(
                    Fighter(
                        id = i,
                        name = if (team == 0) "Ally-${BOT_NAMES[i]}" else BOT_NAMES[i + 4],
                        team = team,
                        isPlayer = false,
                        color = if (team == 0) Color(0xFF5AB8FF) else Color(0xFFE5484D),
                        x = sx,
                        z = sz,
                        altitude = 0f,
                        state = FighterState.GROUND,
                        weapons = arrayOf(if (mode == GameMode.CLASH_SQUAD) "smg" else "ar", null),
                        mags = intArrayOf(30, 0),
                        ammo = mutableMapOf("light" to 90, "medium" to 90, "heavy" to 40, "shells" to 12, "sniper" to 8),
                        vestLevel = 1,
                        vestDur = ARMOR_DURABILITY[1],
                        skillLevel = 0.45f + Random.nextFloat() * 0.25f,
                    )
                )
            }
            zone.cx = -120f
            zone.cz = 60f
            zone.radius = 95f
            zone.toX = -120f
            zone.toZ = 60f
            zone.toR = 75f
            showBanner(if (mode == GameMode.CLASH_SQUAD) "CLASH SQUAD 4v4 — FIGHT!" else "TEAM DEATHMATCH — FIRST TO 25!", 3.5f)
        }
    }

    fun showBanner(text: String, secs: Float) {
        bannerText = text
        bannerUntil = elapsed + secs
    }

    fun weaponOf(f: Fighter): WeaponDef =
        GameCatalog.weapons[f.weapons[f.activeSlot] ?: "fists"] ?: GameCatalog.weapons["fists"]!!

    fun planePos(): Pair<Float, Float> {
        val k = (planeT / planeDur).coerceIn(0f, 1f)
        return (planeFromX + (planeToX - planeFromX) * k) to (planeFromZ + (planeToZ - planeFromZ) * k)
    }

    fun nearbyPickableLoot(): LootItem? {
        val p = player
        if (p.state != FighterState.GROUND) return null
        val pickupRange = if (petId == "pet_dash") 4.2f else 3.2f
        return loot.firstOrNull {
            (it.kind == LootKind.WEAPON || it.kind == LootKind.AIRDROP) &&
                hypot(it.x - p.x, it.z - p.z) < pickupRange
        }
    }

    fun playerJumpOrChute() {
        if (over) return
        val me = player
        when (me.state) {
            FighterState.PLANE -> {
                jumpFromPlane(me)
                bannerUntil = 0f
            }
            FighterState.SKY -> me.chuteOpen = true
            FighterState.GROUND -> me.crouch = false
        }
    }

    fun playerToggleCrouch() {
        if (!over && player.state == FighterState.GROUND) {
            player.crouch = !player.crouch
        }
    }

    fun playerSwitchSlot(slot: Int) {
        if (over) return
        val me = player
        if (me.weapons[slot] != null) {
            me.activeSlot = slot
            me.pendingReload = false
            me.reloadUntil = 0f
        }
    }

    fun playerReload() {
        if (!over) startReload(player)
    }

    fun playerHeal(): String? {
        val me = player
        if (me.meds <= 0) return "No Med Kits left"
        if (me.hp >= 200f) return "HP already full"
        if (me.healUntil > elapsed) return null
        me.meds--
        val healDur = if (characterId == "char_mira") 2.8f else 4.0f
        me.healUntil = elapsed + healDur
        return "Applying Med Kit…"
    }

    fun playerPickUpNearby(): String? {
        val item = nearbyPickableLoot() ?: return null
        return if (tryPickup(player, item)) "Picked up ${item.label}" else null
    }

    fun playerDeployFoamWall(): String {
        val me = player
        if (me.state != FighterState.GROUND) return "Land first to deploy cover"
        if (me.foamWalls <= 0) return "No Foam Walls left"
        me.foamWalls--
        val wx = me.x + sin(me.yaw) * 4.5f
        val wz = me.z - cos(me.yaw) * 4.5f
        foamWalls.add(FoamWallObj(nextWallId++, wx, wz, me.yaw, 800f, elapsed + 25f))
        return "Foam Wall deployed!"
    }

    fun playerActivateSkill(): String {
        val me = player
        val charDef = GameCatalog.itemById(characterId) ?: return "No skill"
        if (!charDef.skillActive) return "${charDef.skillName} (Passive Active)"
        if (elapsed < me.skillCooldownUntil) {
            val rem = (me.skillCooldownUntil - elapsed).toInt() + 1
            return "${charDef.skillName} ready in ${rem}s"
        }
        me.skillCooldownUntil = elapsed + charDef.skillCooldownSec
        when (characterId) {
            "char_kael" -> {
                me.sprintBoostUntil = elapsed + 6f
                showBanner("SPRINT SURGE +30% SPEED!", 1.8f)
            }
            "char_torvin" -> {
                me.shieldHp = 120f
                me.shieldUntil = elapsed + 5f
                showBanner("BASTION DOME (+120 SHIELD)!", 1.8f)
            }
            "char_okoro" -> {
                me.hp = (me.hp + 35f).coerceAtMost(200f)
                showBanner("HEALING AURA ACTIVATED!", 1.8f)
            }
            "char_suki" -> {
                moveFighter(me, sin(me.yaw), -cos(me.yaw), 1f, 7f)
                showBanner("BLINK DASH!", 1.2f)
            }
            "char_nova" -> {
                for (f in fighters) {
                    if (f.alive && f.team != me.team && hypot(f.x - me.x, f.z - me.z) <= 80f) {
                        f.revealUntil = elapsed + 5f
                    }
                }
                showBanner("PULSE SCAN — ENEMIES REVEALED!", 2.0f)
            }
        }
        return "${charDef.skillName} activated!"
    }

    private fun jumpFromPlane(f: Fighter) {
        f.state = FighterState.SKY
        val (px, pz) = planePos()
        f.x = px + (Random.nextFloat() - 0.5f) * 4f
        f.z = pz + (Random.nextFloat() - 0.5f) * 4f
        f.altitude = 135f
        if (!f.isPlayer) {
            val t = towns.random()
            f.wanderX = t.x + (Random.nextFloat() - 0.5f) * 35f
            f.wanderZ = t.z + (Random.nextFloat() - 0.5f) * 35f
            f.hasWander = true
        }
    }

    fun step(dt: Float, joyX: Float, joyY: Float, fireHeld: Boolean) {
        if (over) return
        elapsed += dt
        planeT += dt

        tracers.removeAll { elapsed > it.until }
        foamWalls.removeAll { elapsed > it.expiresAt || it.hp <= 0f }

        // Player update
        val me = player
        if (me.alive) {
            if (me.state == FighterState.PLANE) {
                val (px, pz) = planePos()
                me.x = px
                me.z = pz
                if (planeT >= planeDur) jumpFromPlane(me)
            } else if (me.state == FighterState.SKY) {
                me.altitude -= (if (me.chuteOpen) 12f else 32f) * dt
                if (me.altitude <= 42f) me.chuteOpen = true
                moveFighter(me, joyX, joyY, dt, if (me.chuteOpen) 13f else 22f)
                if (hypot(joyX, joyY) > 0.15f) me.yaw = atan2(joyX, -joyY)
                if (me.altitude <= 0f) {
                    me.altitude = 0f
                    me.state = FighterState.GROUND
                    me.chuteOpen = false
                }
            } else {
                val w = weaponOf(me)
                val sprintBoost = if (elapsed < me.sprintBoostUntil) 1.3f else 1.0f
                val speed = (if (me.crouch) 4.2f else if (aimMode) 5.4f else 7.6f) *
                    (if (w.id == "lmg") 0.88f else 1f) * sprintBoost
                moveFighter(me, joyX, joyY, dt, speed)
                if (hypot(joyX, joyY) > 0.15f && !fireHeld && !aimMode) {
                    me.yaw = atan2(joyX, -joyY)
                }
                finishReload(me)
                if (me.healUntil > 0f && elapsed >= me.healUntil) {
                    me.healUntil = 0f
                    val healBonus = if (characterId == "char_mira") 86f else 75f
                    me.hp = (me.hp + healBonus).coerceAtMost(200f)
                }
                if (me.energy > 0f && me.hp < 200f) {
                    val conv = min(me.energy, 5f * dt)
                    me.energy -= conv
                    me.hp = (me.hp + conv).coerceAtMost(200f)
                }
                if (me.shieldHp > 0f && elapsed > me.shieldUntil) me.shieldHp = 0f
                if (petId == "pet_pebble" && me.vestLevel > 0) {
                    me.vestDur = (me.vestDur + 0.25f * dt).coerceAtMost(ARMOR_DURABILITY[me.vestLevel])
                }
                autoPickup(me)

                // Aim assist / Auto-fire
                val nearestEnemy = findNearestVisibleEnemy(me, 95f)
                if (autoFireEnabled && nearestEnemy != null) {
                    me.yaw = atan2(nearestEnemy.x - me.x, -(nearestEnemy.z - me.z))
                    tryShoot(me, nearestEnemy)
                } else if (fireHeld) {
                    if (nearestEnemy != null && hypot(nearestEnemy.x - me.x, nearestEnemy.z - me.z) < w.rangeMeters * 0.85f) {
                        val targetAng = atan2(nearestEnemy.x - me.x, -(nearestEnemy.z - me.z))
                        val diff = angleDiff(me.yaw, targetAng)
                        if (abs(diff) < 0.45f) {
                            me.yaw += diff * 0.4f
                        }
                    }
                    tryShoot(me, null)
                }
            }
        }

        // Bots update
        for (bot in fighters) {
            if (!bot.isPlayer && bot.alive) updateBot(bot, dt)
        }

        // Zone update
        if (mode.isBattleRoyale) updateZone(dt)

        checkMatchEnd()
    }

    private fun updateBot(b: Fighter, dt: Float) {
        if (b.state == FighterState.PLANE) {
            if (planeT >= b.jumpAt || planeT >= planeDur) jumpFromPlane(b)
            return
        }
        if (b.state == FighterState.SKY) {
            b.altitude -= (if (b.chuteOpen) 12f else 32f) * dt
            if (b.altitude <= 42f) b.chuteOpen = true
            val dx = b.wanderX - b.x
            val dz = b.wanderZ - b.z
            val len = hypot(dx, dz).coerceAtLeast(1f)
            moveFighter(b, dx / len, dz / len, dt, if (b.chuteOpen) 12f else 20f)
            if (b.altitude <= 0f) {
                b.altitude = 0f
                b.state = FighterState.GROUND
                b.chuteOpen = false
            }
            return
        }

        b.thinkTimer -= dt
        if (b.thinkTimer <= 0f) {
            b.thinkTimer = 0.45f + Random.nextFloat() * 0.35f
            val target = findNearestVisibleEnemy(b, 65f)
            b.targetId = target?.id
            if (b.hp < 85f && b.meds > 0 && b.healUntil <= elapsed) {
                b.meds--
                b.healUntil = elapsed + 4f
            }
            val outside = hypot(b.x - zone.toX, b.z - zone.toZ) > zone.toR * 0.82f
            if (outside || !b.hasWander || hypot(b.x - b.wanderX, b.z - b.wanderZ) < 4f) {
                val nearLoot = if (!outside) loot.firstOrNull {
                    hypot(it.x - b.x, it.z - b.z) < 28f &&
                        (it.kind != LootKind.WEAPON || weaponScore(it.id) > weaponScore(b.weapons[0]))
                } else null
                if (nearLoot != null) {
                    b.wanderX = nearLoot.x
                    b.wanderZ = nearLoot.z
                } else {
                    val ang = Random.nextFloat() * (2f * PI.toFloat())
                    val rad = Random.nextFloat() * max(18f, zone.toR * 0.65f)
                    b.wanderX = (zone.toX + cos(ang) * rad).coerceIn(-HALF + 20f, HALF - 20f)
                    b.wanderZ = (zone.toZ + sin(ang) * rad).coerceIn(-HALF + 20f, HALF - 20f)
                }
                b.hasWander = true
            }
            if (weaponOf(b).ammoType != null && b.mags[b.activeSlot] <= 0) startReload(b)
            if (b.weapons[1] != null && weaponScore(b.weapons[1]) > weaponScore(b.weapons[0])) {
                b.activeSlot = 1
            }
        }

        val target = fighters.firstOrNull { it.id == b.targetId && it.alive && it.state == FighterState.GROUND }
        if (target != null) {
            val dx = target.x - b.x
            val dz = target.z - b.z
            val d = hypot(dx, dz).coerceAtLeast(0.1f)
            b.yaw = atan2(dx, -dz)
            val w = weaponOf(b)
            val desiredDist = if (w.ammoType != null) min(w.rangeMeters * 0.55f, 34f) else 2.2f
            val moveX: Float
            val moveZ: Float
            if (d < desiredDist) {
                val strafe = sin(elapsed * 1.8f + b.id)
                moveX = (-dz / d) * strafe
                moveZ = (dx / d) * strafe
            } else {
                moveX = dx / d
                moveZ = dz / d
            }
            moveFighter(b, moveX, moveZ, dt, 4.8f)
            if (d <= w.rangeMeters * 0.9f) {
                tryShoot(b, target)
            }
        } else if (b.hasWander) {
            val dx = b.wanderX - b.x
            val dz = b.wanderZ - b.z
            val d = hypot(dx, dz).coerceAtLeast(0.1f)
            b.yaw = atan2(dx, -dz)
            moveFighter(b, dx / d, dz / d, dt, 5.4f)
        }

        finishReload(b)
        if (b.healUntil > 0f && elapsed >= b.healUntil) {
            b.healUntil = 0f
            b.hp = (b.hp + 75f).coerceAtMost(200f)
        }
        autoPickup(b)
    }

    private fun moveFighter(f: Fighter, dx: Float, dz: Float, dt: Float, speed: Float) {
        val len = hypot(dx, dz)
        if (len > 0.01f) {
            val scale = min(1f, len)
            f.x += (dx / len) * speed * dt * scale
            f.z += (dz / len) * speed * dt * scale
        }
        for (b in buildings) {
            val px = f.x - b.x
            val pz = f.z - b.z
            val hw = b.w / 2f + 1.4f
            val hd = b.d / 2f + 1.4f
            if (abs(px) < hw && abs(pz) < hd) {
                val ox = hw - abs(px)
                val oz = hd - abs(pz)
                if (ox < oz) f.x = b.x + (if (px >= 0f) 1f else -1f) * hw
                else f.z = b.z + (if (pz >= 0f) 1f else -1f) * hd
            }
        }
        val lim = HALF - 16f
        f.x = f.x.coerceIn(-lim, lim)
        f.z = f.z.coerceIn(-lim, lim)
    }

    private fun startReload(f: Fighter) {
        val w = weaponOf(f)
        val ammoKey = w.ammoType ?: return
        if (f.reloadUntil > elapsed || f.mags[f.activeSlot] >= w.magSize) return
        if ((f.ammo[ammoKey] ?: 0) <= 0) return
        val speedMult = if (f.isPlayer && characterId == "char_bram") 0.8f else 1f
        f.reloadUntil = elapsed + w.reloadSec * speedMult
        f.pendingReload = true
    }

    private fun finishReload(f: Fighter) {
        if (!f.pendingReload || elapsed < f.reloadUntil) return
        f.pendingReload = false
        val w = weaponOf(f)
        val ammoKey = w.ammoType ?: return
        val avail = f.ammo[ammoKey] ?: 0
        val take = min(w.magSize - f.mags[f.activeSlot], avail)
        f.mags[f.activeSlot] += take
        f.ammo[ammoKey] = avail - take
    }

    private fun autoPickup(f: Fighter) {
        val range = if (f.isPlayer && petId == "pet_dash") 4.2f else 2.8f
        val snapshot = loot.toList()
        for (item in snapshot) {
            if (hypot(item.x - f.x, item.z - f.z) > range) continue
            when (item.kind) {
                LootKind.WEAPON -> {
                    if (!f.isPlayer) {
                        if (weaponScore(item.id) > weaponScore(f.weapons[0]) || f.weapons[1] == null) {
                            tryPickup(f, item)
                        }
                    } else if (f.weapons[0] == "fists" || f.weapons[1] == null) {
                        tryPickup(f, item)
                    }
                }
                LootKind.VEST -> {
                    val lv = item.id.toIntOrNull() ?: 1
                    if (lv > f.vestLevel) tryPickup(f, item)
                }
                LootKind.HELMET -> {
                    val lv = item.id.toIntOrNull() ?: 1
                    if (lv > f.helmetLevel) tryPickup(f, item)
                }
                else -> tryPickup(f, item)
            }
        }
    }

    private fun tryPickup(f: Fighter, item: LootItem): Boolean {
        when (item.kind) {
            LootKind.WEAPON -> {
                val slot = when {
                    f.weapons[0] == "fists" || f.weapons[0] == null -> 0
                    f.weapons[1] == null -> 1
                    else -> f.activeSlot
                }
                f.weapons[slot] = item.id
                f.mags[slot] = GameCatalog.weapons[item.id]?.magSize ?: 0
                f.activeSlot = slot
            }
            LootKind.AMMO -> f.ammo[item.id] = (f.ammo[item.id] ?: 0) + item.count
            LootKind.MED -> f.meds += item.count
            LootKind.SHROOM -> {
                val mult = if (f.isPlayer && characterId == "char_ivy") 1.5f else 1f
                f.energy = (f.energy + 50f * mult).coerceAtMost(200f)
            }
            LootKind.VEST -> {
                val lv = item.id.toIntOrNull() ?: 1
                if (lv <= f.vestLevel) return false
                f.vestLevel = lv
                f.vestDur = ARMOR_DURABILITY[lv]
            }
            LootKind.HELMET -> {
                val lv = item.id.toIntOrNull() ?: 1
                if (lv <= f.helmetLevel) return false
                f.helmetLevel = lv
                f.helmetDur = ARMOR_DURABILITY[lv]
            }
            LootKind.AIRDROP -> {
                f.weapons[1] = listOf("halberd", "lmg", "sniper").random()
                f.mags[1] = GameCatalog.weapons[f.weapons[1]]?.magSize ?: 30
                f.activeSlot = 1
                f.vestLevel = 3
                f.vestDur = ARMOR_DURABILITY[3]
                f.helmetLevel = 3
                f.helmetDur = ARMOR_DURABILITY[3]
                f.meds += 2
                f.ammo["medium"] = (f.ammo["medium"] ?: 0) + 60
                f.ammo["sniper"] = (f.ammo["sniper"] ?: 0) + 15
            }
        }
        loot.remove(item)
        return true
    }

    private fun tryShoot(shooter: Fighter, explicitTarget: Fighter?) {
        val w = weaponOf(shooter)
        val melee = w.ammoType == null
        if (elapsed < shooter.nextShotAt || elapsed < shooter.reloadUntil || elapsed < shooter.healUntil) return
        if (!melee && shooter.mags[shooter.activeSlot] <= 0) {
            startReload(shooter)
            return
        }
        shooter.nextShotAt = elapsed + 60f / w.rpm
        if (!melee) shooter.mags[shooter.activeSlot]--

        val dirX = sin(shooter.yaw)
        val dirZ = -cos(shooter.yaw)

        repeat(w.pellets) {
            val spreadMult = if (shooter.isPlayer && aimMode) 0.35f else if (shooter.crouch) 0.7f else 1f
            val jitter = (Random.nextFloat() - 0.5f) * 2f * (w.spreadDeg * spreadMult) * (PI.toFloat() / 180f)
            val ang = shooter.yaw + jitter
            val rx = sin(ang)
            val rz = -cos(ang)

            val maxDist = rayBlockedDist(shooter.x, shooter.z, rx, rz, w.rangeMeters)
            var hitTarget: Fighter? = null
            var hitDist = maxDist

            for (other in fighters) {
                if (other.id == shooter.id || other.team == shooter.team || !other.alive || other.state != FighterState.GROUND) continue
                val t = rayCircle(shooter.x, shooter.z, rx, rz, other.x, other.z, 2.1f)
                if (t < hitDist) {
                    hitDist = t
                    hitTarget = other
                }
            }

            if (!melee) {
                tracers.add(
                    TracerShot(
                        shooter.x + dirX * 1.4f,
                        shooter.z + dirZ * 1.4f,
                        shooter.x + rx * hitDist,
                        shooter.z + rz * hitDist,
                        elapsed + 0.08f,
                        shooter.isPlayer,
                    )
                )
            }

            if (hitTarget != null) {
                val headshot = Random.nextFloat() < (if (shooter.isPlayer && aimMode) 0.28f else 0.14f)
                val falloff = if (hitDist > w.rangeMeters * 0.5f) 0.75f else 1f
                val botAccuracyScale = if (!shooter.isPlayer) (0.42f + shooter.skillLevel * 0.45f) else 1f
                val dmg = w.damage * (if (headshot) w.headMultiplier else 1f) * falloff * botAccuracyScale
                applyDamage(hitTarget, dmg, shooter, headshot, w.name)
                if (shooter.isPlayer) {
                    hitFlashUntil = elapsed + 0.16f
                    hitWasHeadshot = headshot
                    if (characterId == "char_lyra") hitTarget.revealUntil = elapsed + 3f
                }
                if (hitTarget.isPlayer && petId == "pet_byte" && hitDist <= 35f) {
                    shooter.revealUntil = elapsed + 3f
                }
            } else if (explicitTarget != null && !shooter.isPlayer && Random.nextFloat() < 0.08f) {
                // occasional suppression ping
                if (petId == "pet_byte" && hypot(explicitTarget.x - shooter.x, explicitTarget.z - shooter.z) < 35f) {
                    shooter.revealUntil = elapsed + 2.5f
                }
            }
        }
    }

    private fun applyDamage(target: Fighter, rawAmount: Float, attacker: Fighter?, headshot: Boolean, source: String) {
        if (!target.alive) return
        var dmg = rawAmount
        if (source != "Storm") {
            if (target.isPlayer && characterId == "char_dante") dmg *= 0.92f
            if (headshot && target.helmetLevel > 0) {
                dmg *= (1f - ARMOR_REDUCTION[target.helmetLevel])
                target.helmetDur -= dmg * 0.55f
                if (target.helmetDur <= 0f) target.helmetLevel = 0
            } else if (!headshot && target.vestLevel > 0) {
                dmg *= (1f - ARMOR_REDUCTION[target.vestLevel])
                target.vestDur -= dmg * 0.55f
                if (target.vestDur <= 0f) target.vestLevel = 0
            }
            if (target.shieldHp > 0f) {
                val absorbed = min(target.shieldHp, dmg)
                target.shieldHp -= absorbed
                dmg -= absorbed
            }
        }
        target.hp -= dmg
        if (attacker != null) {
            attacker.damageDealt += dmg.toInt()
            if (!target.isPlayer) target.targetId = attacker.id
        }
        if (target.isPlayer) damageFlashUntil = elapsed + 0.2f

        if (target.hp <= 0f) {
            target.hp = 0f
            target.alive = false
            target.placement = fighters.count { it.alive } + 1
            if (attacker != null) {
                attacker.kills++
                if (headshot) attacker.headshots++
                if (attacker.isPlayer && characterId == "char_rook") {
                    attacker.hp = (attacker.hp + 25f).coerceAtMost(200f)
                }
            }
            // Drop crate
            val w1 = target.weapons[0]
            if (w1 != null && w1 != "fists") {
                val def = GameCatalog.weapons[w1]!!
                loot.add(LootItem(nextLootUid++, LootKind.WEAPON, w1, 1, target.x + 1f, target.z, def.name, Color(0xFFFFB347)))
            }
            loot.add(LootItem(nextLootUid++, LootKind.MED, "med", 1, target.x - 1f, target.z + 1f, "Med Kit", Color(0xFF44D17A)))
            val line = if (attacker != null) "${attacker.name} eliminated ${target.name}${if (headshot) " ◎" else ""} [$source]"
            else "${target.name} fell to the Storm"
            feed.add(0, line)
            if (feed.size > 4) feed.removeAt(feed.lastIndex)

            if (attacker != null && attacker.isPlayer) {
                showBanner(if (headshot) "HEADSHOT ELIMINATION!" else "ELIMINATION!", 1.3f)
            }

            // Respawn handling for TDM / Training
            if (mode == GameMode.TEAM_DEATHMATCH && !target.isPlayer) {
                teamScores[attacker?.team ?: 0]++
                respawnFighter(target)
            } else if (mode == GameMode.TRAINING && !target.isPlayer) {
                respawnFighter(target)
            }
        }
    }

    private fun respawnFighter(f: Fighter) {
        f.alive = true
        f.hp = 200f
        if (mode == GameMode.TRAINING) {
            val ang = Random.nextFloat() * 2f * PI.toFloat()
            f.x = -120f + cos(ang) * 34f
            f.z = 60f + sin(ang) * 34f
        } else {
            f.x = if (f.team == 0) -155f else -85f
            f.z = 40f + (f.id % 4) * 14f
        }
    }

    private fun updateZone(dt: Float) {
        if (elapsed < 18f) return
        val now = elapsed - 18f
        if (zone.phase < 0 || (zone.state == "shrink" && now >= zone.t1)) {
            zone.phase++
            if (zone.phase >= ZONE_PHASES.size) {
                zone.state = "done"
            } else {
                val ph = ZONE_PHASES[zone.phase]
                zone.fromX = zone.cx
                zone.fromZ = zone.cz
                zone.fromR = zone.radius
                val a = Random.nextFloat() * 2f * PI.toFloat()
                val m = Random.nextFloat() * max(0f, zone.radius - ph.r) * 0.75f
                zone.toX = (zone.cx + cos(a) * m).coerceIn(-180f, 180f)
                zone.toZ = (zone.cz + sin(a) * m).coerceIn(-180f, 180f)
                zone.toR = ph.r
                zone.state = "wait"
                zone.t0 = now + ph.wait
                zone.t1 = zone.t0 + ph.shrink
                zone.dps = ph.dps
                showBanner(if (zone.phase == 0) "The Storm is forming!" else "Storm moving — head to the safe zone!", 2.4f)
                if ((zone.phase == 1 || zone.phase == 3) && spawnedAirdropPhases.add(zone.phase)) {
                    val ax = zone.toX + (Random.nextFloat() - 0.5f) * 30f
                    val az = zone.toZ + (Random.nextFloat() - 0.5f) * 30f
                    loot.add(LootItem(nextLootUid++, LootKind.AIRDROP, "airdrop", 1, ax, az, "Airdrop Supply Crate", Color(0xFFFF4D66)))
                    showBanner("AIRDROP INCOMING!", 2.2f)
                }
            }
        }
        if (zone.state == "wait" && now >= zone.t0) zone.state = "shrink"
        if (zone.state == "shrink") {
            val k = ((now - zone.t0) / (zone.t1 - zone.t0)).coerceIn(0f, 1f)
            zone.cx = zone.fromX + (zone.toX - zone.fromX) * k
            zone.cz = zone.fromZ + (zone.toZ - zone.fromZ) * k
            zone.radius = zone.fromR + (zone.toR - zone.fromR) * k
        }
        zone.tickTimer += dt
        if (zone.tickTimer >= 1f) {
            zone.tickTimer = 0f
            for (f in fighters) {
                if (f.alive && f.state != FighterState.PLANE && hypot(f.x - zone.cx, f.z - zone.cz) > zone.radius) {
                    applyDamage(f, zone.dps, null, false, "Storm")
                }
            }
        }
    }

    private fun checkMatchEnd() {
        if (over) return
        val me = player
        if (mode == GameMode.TRAINING) return
        if (!me.alive) {
            over = true
            won = false
            return
        }
        if (mode == GameMode.TEAM_DEATHMATCH) {
            if (teamScores[0] >= 25) {
                over = true
                won = true
                me.placement = 1
            }
            return
        }
        val aliveTeams = fighters.filter { it.alive }.map { it.team }.distinct()
        if (aliveTeams.size <= 1 && aliveTeams.firstOrNull() == me.team) {
            over = true
            won = true
            me.placement = 1
        }
    }

    fun quitMatch() {
        val me = player
        if (mode != GameMode.TRAINING && me.alive) {
            me.alive = false
            me.placement = fighters.count { it.alive } + 1
        }
        over = true
        won = false
    }

    private fun findNearestVisibleEnemy(f: Fighter, maxDist: Float): Fighter? {
        var best: Fighter? = null
        var bestD = maxDist
        for (other in fighters) {
            if (other.id == f.id || other.team == f.team || !other.alive || other.state != FighterState.GROUND) continue
            val d = hypot(other.x - f.x, other.z - f.z)
            if (d < bestD && rayBlockedDist(f.x, f.z, (other.x - f.x) / d, (other.z - f.z) / d, d) >= d - 1.5f) {
                bestD = d
                best = other
            }
        }
        return best
    }

    private fun rayBlockedDist(ox: Float, oz: Float, dx: Float, dz: Float, maxD: Float): Float {
        var best = maxD
        for (b in buildings) {
            val t = rayBox2D(ox, oz, dx, dz, b.x - b.w / 2f, b.x + b.w / 2f, b.z - b.d / 2f, b.z + b.d / 2f, best)
            if (t < best) best = t
        }
        for (fw in foamWalls) {
            val t = rayCircle(ox, oz, dx, dz, fw.x, fw.z, 2.2f)
            if (t < best) best = t
        }
        return best
    }

    private fun rayBox2D(
        ox: Float, oz: Float, dx: Float, dz: Float,
        minX: Float, maxX: Float, minZ: Float, maxZ: Float, maxD: Float
    ): Float {
        var tMin = 0f
        var tMax = maxD
        if (abs(dx) < 1e-6f) {
            if (ox < minX || ox > maxX) return Float.POSITIVE_INFINITY
        } else {
            var t1 = (minX - ox) / dx
            var t2 = (maxX - ox) / dx
            if (t1 > t2) { val tmp = t1; t1 = t2; t2 = tmp }
            tMin = max(tMin, t1)
            tMax = min(tMax, t2)
            if (tMin > tMax) return Float.POSITIVE_INFINITY
        }
        if (abs(dz) < 1e-6f) {
            if (oz < minZ || oz > maxZ) return Float.POSITIVE_INFINITY
        } else {
            var t1 = (minZ - oz) / dz
            var t2 = (maxZ - oz) / dz
            if (t1 > t2) { val tmp = t1; t1 = t2; t2 = tmp }
            tMin = max(tMin, t1)
            tMax = min(tMax, t2)
            if (tMin > tMax) return Float.POSITIVE_INFINITY
        }
        return tMin
    }

    private fun rayCircle(ox: Float, oz: Float, dx: Float, dz: Float, cx: Float, cz: Float, r: Float): Float {
        val vx = cx - ox
        val vz = cz - oz
        val t = vx * dx + vz * dz
        if (t < 0f) return Float.POSITIVE_INFINITY
        val d2 = vx * vx + vz * vz - t * t
        return if (d2 <= r * r) max(0f, t - kotlin.math.sqrt(r * r - d2)) else Float.POSITIVE_INFINITY
    }

    private fun angleDiff(a: Float, b: Float): Float {
        var d = (b - a) % (2f * PI.toFloat())
        if (d > PI.toFloat()) d -= 2f * PI.toFloat()
        if (d < -PI.toFloat()) d += 2f * PI.toFloat()
        return d
    }

    private fun weaponScore(id: String?): Int = GameCatalog.weapons[id ?: "fists"]?.score ?: 0
}
