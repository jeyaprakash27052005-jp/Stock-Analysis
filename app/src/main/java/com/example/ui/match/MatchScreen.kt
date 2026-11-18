package com.example.ui.match

import androidx.activity.compose.BackHandler
import androidx.compose.foundation.Canvas
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.gestures.detectDragGestures
import androidx.compose.foundation.gestures.detectTapGestures
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.WindowInsets
import androidx.compose.foundation.layout.fillMaxHeight
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.safeDrawing
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.layout.windowInsetsPadding
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.LinearProgressIndicator
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableFloatStateOf
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.geometry.Size
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.Path
import androidx.compose.ui.graphics.PathEffect
import androidx.compose.ui.graphics.drawscope.Stroke
import androidx.compose.ui.graphics.drawscope.rotate
import androidx.compose.ui.input.pointer.pointerInput
import androidx.compose.ui.platform.testTag
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.example.data.local.PlayerSettingsState
import com.example.data.model.GameCatalog
import com.example.data.model.GameMode
import com.example.data.remote.UserProfileDoc
import com.example.ui.theme.JetBrainsMonoFamily
import com.example.ui.theme.StormdropColors
import kotlinx.coroutines.delay
import kotlin.math.PI
import kotlin.math.atan2
import kotlin.math.ceil
import kotlin.math.cos
import kotlin.math.hypot
import kotlin.math.sin

@Composable
fun MatchScreen(
    mode: GameMode,
    isRanked: Boolean,
    profile: UserProfileDoc?,
    settings: PlayerSettingsState,
    onMatchFinished: (
        won: Boolean,
        placement: Int,
        totalTeams: Int,
        kills: Int,
        damage: Int,
        headshots: Int,
        survivalSeconds: Int,
    ) -> Unit,
) {
    val sim = remember(mode) {
        MatchSimulation(
            mode = mode,
            playerName = profile?.nickname ?: "You",
            characterId = profile?.equippedCharacterId ?: "char_kael",
            petId = profile?.equippedPetId,
        ).apply {
            autoFireEnabled = settings.autoFire
        }
    }

    var tick by remember { mutableIntStateOf(0) }
    var joyX by remember { mutableFloatStateOf(0f) }
    var joyY by remember { mutableFloatStateOf(0f) }
    var joyKnobOffset by remember { mutableStateOf(Offset.Zero) }
    var fireHeld by remember { mutableStateOf(false) }
    var hudToast by remember { mutableStateOf<String?>(null) }
    var showQuitConfirm by remember { mutableStateOf(false) }
    var settled by remember { mutableStateOf(false) }

    BackHandler { showQuitConfirm = true }

    LaunchedEffect(hudToast) {
        if (hudToast != null) {
            delay(2000)
            hudToast = null
        }
    }

    LaunchedEffect(sim) {
        var lastNs = System.nanoTime()
        while (!sim.over) {
            val nowNs = System.nanoTime()
            val dt = ((nowNs - lastNs) / 1_000_000_000f).coerceIn(0.008f, 0.05f)
            lastNs = nowNs
            sim.step(dt, joyX, joyY, fireHeld)
            tick++
            delay(16L)
        }
        if (!settled) {
            settled = true
            val me = sim.player
            val totalTeams = if (mode.isBattleRoyale) sim.fighters.map { it.team }.distinct().size else 2
            onMatchFinished(
                sim.won,
                me.placement.coerceAtLeast(1),
                totalTeams,
                me.kills,
                me.damageDealt,
                me.headshots,
                sim.elapsed.toInt(),
            )
        }
    }

    // Recompose-tracked state reads
    @Suppress("UNUSED_EXPRESSION")
    tick
    val me = sim.player
    val weapon = sim.weaponOf(me)
    val nearLoot = sim.nearbyPickableLoot()
    val aliveCount = sim.fighters.count { it.alive }
    val charDef = GameCatalog.itemById(sim.characterId)
    val skillCdRemaining = (me.skillCooldownUntil - sim.elapsed).coerceAtLeast(0f)
    val outsideZone = mode.isBattleRoyale && sim.elapsed > 18f &&
        hypot(me.x - sim.zone.cx, me.z - sim.zone.cz) > sim.zone.radius

    Box(
        modifier = Modifier
            .fillMaxSize()
            .background(Color(0xFF1A4B6E))
    ) {
        // 1. Tactical World Canvas
        Canvas(modifier = Modifier.fillMaxSize()) {
            val zoomScale = when {
                me.state == FighterState.PLANE -> 3.0f
                me.state == FighterState.SKY -> 4.2f
                sim.aimMode -> 7.5f * (weapon.zoom.coerceAtMost(2.5f) * 0.55f)
                else -> 6.8f
            }
            val cx = size.width / 2f
            val cy = size.height / 2f
            val camX = me.x
            val camZ = me.z

            fun toScreen(wx: Float, wz: Float): Offset =
                Offset(cx + (wx - camX) * zoomScale, cy + (wz - camZ) * zoomScale)

            // Ocean
            drawRect(if (sim.isNight) Color(0xFF0E2338) else Color(0xFF2B6F9E))

            // Isle Verdant island silhouette
            val islandCenter = toScreen(0f, 0f)
            drawCircle(
                color = if (sim.isNight) Color(0xFF3E4536) else Color(0xFFD8C79A),
                radius = 290f * zoomScale,
                center = islandCenter,
            )
            drawCircle(
                color = if (sim.isNight) Color(0xFF233824) else Color(0xFF4F8235),
                radius = 276f * zoomScale,
                center = islandCenter,
            )

            // Frostspine Peaks elevation
            val peakPos = toScreen(150f, 140f)
            drawCircle(
                color = if (sim.isNight) Color(0xFF4F5966) else Color(0xFF8A8A86),
                radius = 60f * zoomScale,
                center = peakPos,
            )
            drawCircle(
                color = if (sim.isNight) Color(0xFF8899AA) else Color(0xFFEEF3F8),
                radius = 30f * zoomScale,
                center = peakPos,
            )

            // Serpent River
            val riverPath = Path().apply {
                for (rx in -260..140 step 20) {
                    val rz = 20f + sin(rx / 45f) * 25f
                    val pt = toScreen(rx.toFloat(), rz)
                    if (rx == -260) moveTo(pt.x, pt.y) else lineTo(pt.x, pt.y)
                }
            }
            drawPath(
                path = riverPath,
                color = if (sim.isNight) Color(0xFF143552) else Color(0xFF2F7EB5),
                style = Stroke(width = 18f * zoomScale),
            )

            // Town pads
            for (town in sim.towns) {
                val tp = toScreen(town.x, town.z)
                drawCircle(
                    color = town.color.copy(alpha = 0.25f),
                    radius = town.radius * zoomScale,
                    center = tp,
                )
            }

            // Buildings
            for (b in sim.buildings) {
                val topLeft = toScreen(b.x - b.w / 2f, b.z - b.d / 2f)
                val bw = b.w * zoomScale
                val bd = b.d * zoomScale
                if (topLeft.x + bw < 0 || topLeft.x > size.width || topLeft.y + bd < 0 || topLeft.y > size.height) continue
                drawRect(
                    color = if (sim.isNight) b.color.copy(alpha = 0.65f) else b.color,
                    topLeft = topLeft,
                    size = Size(bw, bd),
                )
                drawRect(
                    color = Color(0xFF3E2620),
                    topLeft = Offset(topLeft.x + bw * 0.1f, topLeft.y + bd * 0.1f),
                    size = Size(bw * 0.8f, bd * 0.8f),
                )
            }

            // Foam Walls
            for (fw in sim.foamWalls) {
                val p = toScreen(fw.x, fw.z)
                rotate(degrees = fw.angleRad * 180f / PI.toFloat(), pivot = p) {
                    drawRect(
                        color = StormdropColors.AccentCyan,
                        topLeft = Offset(p.x - 2.6f * zoomScale, p.y - 0.6f * zoomScale),
                        size = Size(5.2f * zoomScale, 1.2f * zoomScale),
                    )
                }
            }

            // Trees
            for (t in sim.trees) {
                val tp = toScreen(t.x, t.z)
                if (tp.x < -30 || tp.x > size.width + 30 || tp.y < -30 || tp.y > size.height + 30) continue
                drawCircle(
                    color = if (sim.isNight) Color(0xFF1A421B) else Color(0xFF2D6A2E),
                    radius = t.radius * zoomScale,
                    center = tp,
                )
            }

            // Loot items
            for (l in sim.loot) {
                val lp = toScreen(l.x, l.z)
                if (lp.x < -20 || lp.x > size.width + 20 || lp.y < -20 || lp.y > size.height + 20) continue
                val r = (if (l.kind == LootKind.AIRDROP) 2.2f else 1.0f) * zoomScale
                drawCircle(color = l.color.copy(alpha = 0.35f), radius = r * 1.8f, center = lp)
                drawCircle(color = l.color, radius = r, center = lp)
            }

            // Player sight line
            if (me.alive && me.state == FighterState.GROUND) {
                val mp = toScreen(me.x, me.z)
                val endX = mp.x + sin(me.yaw) * weapon.rangeMeters * zoomScale * 0.55f
                val endY = mp.y - cos(me.yaw) * weapon.rangeMeters * zoomScale * 0.55f
                drawLine(
                    color = Color.White.copy(alpha = if (sim.aimMode) 0.5f else 0.25f),
                    start = mp,
                    end = Offset(endX, endY),
                    strokeWidth = if (sim.aimMode) 3f else 2f,
                    pathEffect = PathEffect.dashPathEffect(floatArrayOf(12f, 10f)),
                )
            }

            // Tracers
            for (tr in sim.tracers) {
                val p1 = toScreen(tr.x1, tr.z1)
                val p2 = toScreen(tr.x2, tr.z2)
                drawLine(
                    color = if (tr.isPlayer) Color(0xFFFFE08A) else Color(0xFFFF6B4A),
                    start = p1,
                    end = p2,
                    strokeWidth = 3f,
                )
            }

            // Fighters
            for (f in sim.fighters) {
                if (!f.alive || f.state == FighterState.PLANE) continue
                val fp = toScreen(f.x, f.z)
                if (!f.isPlayer && f.team != me.team && hypot(f.x - me.x, f.z - me.z) > 120f && sim.elapsed > f.revealUntil) continue

                if (f.state == FighterState.SKY && f.chuteOpen) {
                    drawCircle(
                        color = if (f.isPlayer) StormdropColors.AccentOrange else Color.White.copy(alpha = 0.85f),
                        radius = 3.2f * zoomScale,
                        center = fp,
                    )
                }

                // Shield dome
                if (f.shieldHp > 0f) {
                    drawCircle(
                        color = StormdropColors.AccentCyan.copy(alpha = 0.28f),
                        radius = 3.2f * zoomScale,
                        center = fp,
                    )
                    drawCircle(
                        color = StormdropColors.AccentCyan,
                        radius = 3.2f * zoomScale,
                        center = fp,
                        style = Stroke(width = 3f),
                    )
                }

                // Reveal ring
                if (!f.isPlayer && sim.elapsed < f.revealUntil) {
                    drawCircle(
                        color = StormdropColors.Crimson,
                        radius = 3.6f * zoomScale,
                        center = fp,
                        style = Stroke(width = 3f),
                    )
                }

                // Body & facing barrel
                val rad = (if (f.crouch) 1.15f else 1.45f) * zoomScale
                drawCircle(color = Color.Black.copy(alpha = 0.4f), radius = rad + 2f, center = fp)
                drawCircle(color = f.color, radius = rad, center = fp)
                val barrelEnd = Offset(fp.x + sin(f.yaw) * rad * 1.6f, fp.y - cos(f.yaw) * rad * 1.6f)
                drawLine(color = Color.White, start = fp, end = barrelEnd, strokeWidth = 4f)
            }

            // Drop Plane
            if (mode.isBattleRoyale && sim.planeT < sim.planeDur + 2f) {
                val (px, pz) = sim.planePos()
                val pp = toScreen(px, pz)
                val pAng = atan2(sim.planeToX - sim.planeFromX, -(sim.planeToZ - sim.planeFromZ)) * 180f / PI.toFloat()
                rotate(degrees = pAng, pivot = pp) {
                    drawRect(
                        color = Color(0xFFE6ECF5),
                        topLeft = Offset(pp.x - 18f, pp.y - 34f),
                        size = Size(36f, 68f),
                    )
                    drawRect(
                        color = StormdropColors.AccentOrange,
                        topLeft = Offset(pp.x - 56f, pp.y - 8f),
                        size = Size(112f, 16f),
                    )
                }
            }

            // Storm Wall
            if (mode.isBattleRoyale && sim.elapsed >= 18f) {
                val zc = toScreen(sim.zone.cx, sim.zone.cz)
                drawCircle(
                    color = StormdropColors.AccentCyan,
                    radius = sim.zone.radius * zoomScale,
                    center = zc,
                    style = Stroke(width = 5f),
                )
                val zNext = toScreen(sim.zone.toX, sim.zone.toZ)
                drawCircle(
                    color = Color.White.copy(alpha = 0.7f),
                    radius = sim.zone.toR * zoomScale,
                    center = zNext,
                    style = Stroke(width = 2f, pathEffect = PathEffect.dashPathEffect(floatArrayOf(12f, 12f))),
                )
            }
        }

        // Damage vignette
        if (sim.elapsed < sim.damageFlashUntil || outsideZone) {
            Box(
                modifier = Modifier
                    .fillMaxSize()
                    .border(
                        width = if (sim.elapsed < sim.damageFlashUntil) 12.dp else 5.dp,
                        color = StormdropColors.Crimson.copy(alpha = if (sim.elapsed < sim.damageFlashUntil) 0.7f else 0.45f),
                    )
            )
        }

        // Right-half drag-to-aim look area
        Box(
            modifier = Modifier
                .fillMaxHeight()
                .fillMaxWidth(0.55f)
                .align(Alignment.CenterEnd)
                .pointerInput(Unit) {
                    detectDragGestures { change, dragAmount ->
                        change.consume()
                        val sens = 0.008f * settings.lookSensitivity
                        me.yaw = (me.yaw + dragAmount.x * sens)
                    }
                }
        )

        // Left-half virtual joystick
        Box(
            modifier = Modifier
                .fillMaxHeight(0.65f)
                .fillMaxWidth(0.45f)
                .align(Alignment.BottomStart)
                .pointerInput(Unit) {
                    detectDragGestures(
                        onDragStart = { },
                        onDragEnd = {
                            joyX = 0f; joyY = 0f; joyKnobOffset = Offset.Zero
                        },
                        onDragCancel = {
                            joyX = 0f; joyY = 0f; joyKnobOffset = Offset.Zero
                        },
                        onDrag = { change, dragAmount ->
                            change.consume()
                            val maxR = 110f
                            val next = joyKnobOffset + dragAmount
                            val dist = hypot(next.x, next.y)
                            val clamped = if (dist > maxR) next * (maxR / dist) else next
                            joyKnobOffset = clamped
                            joyX = (clamped.x / maxR).coerceIn(-1f, 1f)
                            joyY = (clamped.y / maxR).coerceIn(-1f, 1f)
                        },
                    )
                }
        ) {
            // Visual Joystick ring
            Box(
                modifier = Modifier
                    .align(Alignment.BottomStart)
                    .padding(start = 36.dp, bottom = 36.dp)
                    .size(116.dp)
                    .border(2.dp, Color.White.copy(alpha = 0.35f), CircleShape)
                    .background(Color.Black.copy(alpha = 0.25f), CircleShape),
                contentAlignment = Alignment.Center,
            ) {
                Box(
                    modifier = Modifier
                        .size(48.dp)
                        .background(StormdropColors.AccentOrange.copy(alpha = 0.85f), CircleShape)
                )
            }
        }

        // HUD Overlay
        Box(
            modifier = Modifier
                .fillMaxSize()
                .windowInsetsPadding(WindowInsets.safeDrawing)
                .padding(10.dp)
        ) {
            // Top Left: Kill Feed
            Column(
                modifier = Modifier.align(Alignment.TopStart),
                verticalArrangement = Arrangement.spacedBy(2.dp),
            ) {
                for (line in sim.feed) {
                    Text(
                        text = line,
                        style = MaterialTheme.typography.labelSmall,
                        color = Color.White,
                        modifier = Modifier
                            .background(Color.Black.copy(alpha = 0.55f), RoundedCornerShape(6.dp))
                            .padding(horizontal = 8.dp, vertical = 3.dp),
                    )
                }
            }

            // Top Center: Match Status Pill
            val zoneText = when {
                !mode.isBattleRoyale -> mode.shortName
                sim.elapsed < 18f -> "Drop Zone"
                sim.zone.state == "wait" -> "Storm in ${ceil((sim.zone.t0 - (sim.elapsed - 18f)).coerceAtLeast(0f)).toInt()}s"
                sim.zone.state == "shrink" -> "Closing ${ceil((sim.zone.t1 - (sim.elapsed - 18f)).coerceAtLeast(0f)).toInt()}s"
                else -> "Final Zone"
            }
            Row(
                modifier = Modifier
                    .align(Alignment.TopCenter)
                    .background(Color.Black.copy(alpha = 0.65f), RoundedCornerShape(20.dp))
                    .padding(horizontal = 14.dp, vertical = 6.dp),
                horizontalArrangement = Arrangement.spacedBy(10.dp),
                verticalAlignment = Alignment.CenterVertically,
            ) {
                Text("ALIVE $aliveCount", style = MaterialTheme.typography.labelLarge, color = StormdropColors.AccentOrange)
                Text("KILLS ${me.kills}", style = MaterialTheme.typography.labelLarge, color = Color.White)
                Text(zoneText, style = MaterialTheme.typography.labelLarge, color = if (outsideZone) StormdropColors.Crimson else StormdropColors.AccentCyan)
            }

            // Top Right: Minimap + Quit
            Row(
                modifier = Modifier.align(Alignment.TopEnd),
                horizontalArrangement = Arrangement.spacedBy(8.dp),
                verticalAlignment = Alignment.Top,
            ) {
                Box(
                    modifier = Modifier
                        .size(44.dp)
                        .clip(CircleShape)
                        .background(StormdropColors.Crimson.copy(alpha = 0.85f))
                        .clickable { showQuitConfirm = true }
                        .testTag("match_quit_button"),
                    contentAlignment = Alignment.Center,
                ) {
                    Text("EXIT", style = MaterialTheme.typography.labelSmall, color = Color.White, fontWeight = FontWeight.Bold)
                }
                Canvas(
                    modifier = Modifier
                        .size(96.dp)
                        .clip(RoundedCornerShape(10.dp))
                        .border(1.5.dp, Color.White.copy(alpha = 0.4f), RoundedCornerShape(10.dp))
                ) {
                    drawRect(Color(0xFF1D4E73))
                    val s = size.width / (MatchSimulation.HALF * 2f)
                    fun mx(x: Float) = (x + MatchSimulation.HALF) * s
                    fun mz(z: Float) = (z + MatchSimulation.HALF) * s
                    drawCircle(Color(0xFF4F8235), radius = 280f * s, center = Offset(size.width / 2f, size.height / 2f))
                    for (t in sim.towns) {
                        drawCircle(Color(0xFFBDB6AA), radius = t.radius * s * 0.7f, center = Offset(mx(t.x), mz(t.z)))
                    }
                    if (mode.isBattleRoyale && sim.elapsed >= 18f) {
                        drawCircle(StormdropColors.AccentCyan, radius = sim.zone.radius * s, center = Offset(mx(sim.zone.cx), mz(sim.zone.cz)), style = Stroke(2f))
                        drawCircle(Color.White, radius = sim.zone.toR * s, center = Offset(mx(sim.zone.toX), mz(sim.zone.toZ)), style = Stroke(1f))
                    }
                    for (f in sim.fighters) {
                        if (!f.isPlayer && f.alive && sim.elapsed < f.revealUntil) {
                            drawCircle(StormdropColors.Crimson, radius = 3.5f, center = Offset(mx(f.x), mz(f.z)))
                        }
                    }
                    drawCircle(StormdropColors.Gold, radius = 4f, center = Offset(mx(me.x), mz(me.z)))
                }
            }

            // Center Banner & Hitmarker
            if (sim.elapsed < sim.bannerUntil && sim.bannerText.isNotBlank()) {
                Text(
                    text = sim.bannerText,
                    style = MaterialTheme.typography.headlineMedium,
                    color = StormdropColors.AccentOrange,
                    textAlign = TextAlign.Center,
                    modifier = Modifier
                        .align(Alignment.TopCenter)
                        .padding(top = 54.dp)
                        .background(Color.Black.copy(alpha = 0.6f), RoundedCornerShape(10.dp))
                        .padding(horizontal = 16.dp, vertical = 6.dp),
                )
            }
            if (sim.elapsed < sim.hitFlashUntil) {
                Box(
                    modifier = Modifier
                        .align(Alignment.Center)
                        .size(22.dp)
                        .border(2.5.dp, if (sim.hitWasHeadshot) StormdropColors.Crimson else Color.White, RoundedCornerShape(4.dp))
                )
            }
            if (hudToast != null) {
                Text(
                    text = hudToast!!,
                    style = MaterialTheme.typography.bodyMedium,
                    color = Color.White,
                    modifier = Modifier
                        .align(Alignment.Center)
                        .padding(top = 64.dp)
                        .background(Color.Black.copy(alpha = 0.75f), RoundedCornerShape(10.dp))
                        .padding(horizontal = 12.dp, vertical = 6.dp),
                )
            }

            // Pick up weapon/airdrop prompt
            if (nearLoot != null) {
                Button(
                    onClick = { sim.playerPickUpNearby()?.let { hudToast = it } },
                    colors = ButtonDefaults.buttonColors(containerColor = StormdropColors.AccentOrange),
                    modifier = Modifier
                        .align(Alignment.Center)
                        .padding(top = 100.dp)
                        .testTag("match_pickup_button"),
                ) {
                    Text("PICK ${nearLoot.label}", color = Color.Black, fontWeight = FontWeight.Bold)
                }
            }

            // Left utility buttons (Med, Wall, Character Skill)
            Column(
                modifier = Modifier
                    .align(Alignment.CenterStart)
                    .padding(start = 6.dp),
                verticalArrangement = Arrangement.spacedBy(8.dp),
            ) {
                HudCircleButton("MED\n×${me.meds}", StormdropColors.Emerald, 52.dp, "match_med_button") {
                    sim.playerHeal()?.let { hudToast = it }
                }
                HudCircleButton("WALL\n×${me.foamWalls}", StormdropColors.AccentCyan, 52.dp, "match_wall_button") {
                    hudToast = sim.playerDeployFoamWall()
                }
                val skillLabel = if (charDef?.skillActive == true) {
                    if (skillCdRemaining > 0f) "${skillCdRemaining.toInt()}s" else "SKILL"
                } else "PASSIVE"
                HudCircleButton(skillLabel, StormdropColors.VigorPurple, 54.dp, "match_skill_button") {
                    hudToast = sim.playerActivateSkill()
                }
            }

            // Bottom Center: Weapon Slots + HP/Vest/Vigor Bars
            Column(
                modifier = Modifier
                    .align(Alignment.BottomCenter)
                    .width(260.dp),
                horizontalAlignment = Alignment.CenterHorizontally,
            ) {
                if (me.healUntil > sim.elapsed || me.pendingReload) {
                    Text(
                        text = if (me.healUntil > sim.elapsed) "Healing…" else "Reloading…",
                        style = MaterialTheme.typography.labelSmall,
                        color = StormdropColors.Gold,
                    )
                }
                Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                    for (slot in 0..1) {
                        val wId = me.weapons[slot]
                        val wName = wId?.let { GameCatalog.weapons[it]?.name } ?: "Empty"
                        val active = me.activeSlot == slot
                        Box(
                            modifier = Modifier
                                .weight(1f)
                                .clip(RoundedCornerShape(8.dp))
                                .background(if (active) StormdropColors.AccentOrange else Color.Black.copy(alpha = 0.65f))
                                .clickable { sim.playerSwitchSlot(slot) }
                                .padding(vertical = 6.dp, horizontal = 8.dp),
                            contentAlignment = Alignment.Center,
                        ) {
                            Text(
                                text = wName,
                                style = MaterialTheme.typography.labelSmall,
                                color = if (active) Color.Black else Color.White,
                                maxLines = 1,
                            )
                        }
                    }
                }
                Spacer(Modifier.height(4.dp))
                Text(
                    text = "HP ${me.hp.toInt()} · Vest Lv${me.vestLevel} · Helm Lv${me.helmetLevel}" +
                        if (weapon.ammoType != null) " · ${me.mags[me.activeSlot]}/${me.ammo[weapon.ammoType] ?: 0}" else "",
                    style = MaterialTheme.typography.labelSmall,
                    fontFamily = JetBrainsMonoFamily,
                    color = Color.White,
                )
                LinearProgressIndicator(
                    progress = { (me.hp / 200f).coerceIn(0f, 1f) },
                    modifier = Modifier.fillMaxWidth().height(7.dp).clip(RoundedCornerShape(4.dp)),
                    color = Color.White,
                    trackColor = Color.Black.copy(alpha = 0.6f),
                )
                Spacer(Modifier.height(3.dp))
                LinearProgressIndicator(
                    progress = { (me.energy / 200f).coerceIn(0f, 1f) },
                    modifier = Modifier.fillMaxWidth().height(4.dp).clip(RoundedCornerShape(4.dp)),
                    color = StormdropColors.VigorPurple,
                    trackColor = Color.Black.copy(alpha = 0.5f),
                )
            }

            // Bottom Right: Action Cluster (Fire, Aim, Reload, Crouch, Jump/Chute)
            Box(
                modifier = Modifier
                    .align(Alignment.BottomEnd)
                    .size(210.dp)
            ) {
                // Main FIRE button (hold or tap)
                Box(
                    modifier = Modifier
                        .align(Alignment.BottomEnd)
                        .padding(end = 10.dp, bottom = 14.dp)
                        .size(84.dp)
                        .clip(CircleShape)
                        .background(if (fireHeld) StormdropColors.AccentOrange else StormdropColors.AccentOrange.copy(alpha = 0.85f))
                        .border(2.5.dp, Color.White, CircleShape)
                        .pointerInput(Unit) {
                            detectTapGestures(
                                onPress = {
                                    fireHeld = true
                                    tryAwaitRelease()
                                    fireHeld = false
                                }
                            )
                        }
                        .testTag("match_fire_button"),
                    contentAlignment = Alignment.Center,
                ) {
                    Text("FIRE", style = MaterialTheme.typography.titleLarge, color = Color.Black, fontWeight = FontWeight.Bold)
                }

                // AIM button
                Box(
                    modifier = Modifier
                        .align(Alignment.BottomStart)
                        .padding(start = 18.dp, bottom = 10.dp)
                        .size(56.dp)
                        .clip(CircleShape)
                        .background(if (sim.aimMode) StormdropColors.AccentOrange else Color.Black.copy(alpha = 0.65f))
                        .border(1.5.dp, Color.White.copy(alpha = 0.5f), CircleShape)
                        .clickable { sim.aimMode = !sim.aimMode }
                        .testTag("match_aim_button"),
                    contentAlignment = Alignment.Center,
                ) {
                    Text("AIM", style = MaterialTheme.typography.labelSmall, color = Color.White, fontWeight = FontWeight.Bold)
                }

                // RELOAD button
                Box(
                    modifier = Modifier
                        .align(Alignment.CenterStart)
                        .padding(start = 16.dp, bottom = 20.dp)
                        .size(52.dp)
                        .clip(CircleShape)
                        .background(Color.Black.copy(alpha = 0.65f))
                        .border(1.5.dp, Color.White.copy(alpha = 0.45f), CircleShape)
                        .clickable { sim.playerReload() }
                        .testTag("match_reload_button"),
                    contentAlignment = Alignment.Center,
                ) {
                    Text("RLD", style = MaterialTheme.typography.labelSmall, color = Color.White)
                }

                // CROUCH button
                Box(
                    modifier = Modifier
                        .align(Alignment.TopCenter)
                        .padding(top = 18.dp, end = 22.dp)
                        .size(52.dp)
                        .clip(CircleShape)
                        .background(if (me.crouch) StormdropColors.AccentOrange else Color.Black.copy(alpha = 0.65f))
                        .border(1.5.dp, Color.White.copy(alpha = 0.45f), CircleShape)
                        .clickable { sim.playerToggleCrouch() }
                        .testTag("match_crouch_button"),
                    contentAlignment = Alignment.Center,
                ) {
                    Text("CRCH", style = MaterialTheme.typography.labelSmall, color = Color.White)
                }

                // JUMP / CHUTE button
                val jumpText = when {
                    me.state == FighterState.PLANE -> "JUMP"
                    me.state == FighterState.SKY && !me.chuteOpen -> "CHUTE"
                    else -> "UP"
                }
                Box(
                    modifier = Modifier
                        .align(Alignment.TopEnd)
                        .padding(top = 18.dp, end = 8.dp)
                        .size(58.dp)
                        .clip(CircleShape)
                        .background(if (me.state != FighterState.GROUND) StormdropColors.AccentOrange else Color.Black.copy(alpha = 0.65f))
                        .border(1.5.dp, Color.White.copy(alpha = 0.55f), CircleShape)
                        .clickable { sim.playerJumpOrChute() }
                        .testTag("match_jump_button"),
                    contentAlignment = Alignment.Center,
                ) {
                    Text(
                        text = jumpText,
                        style = MaterialTheme.typography.labelSmall,
                        color = if (me.state != FighterState.GROUND) Color.Black else Color.White,
                        fontWeight = FontWeight.Bold,
                    )
                }
            }
        }
    }

    if (showQuitConfirm) {
        AlertDialog(
            onDismissRequest = { showQuitConfirm = false },
            title = { Text("Leave Match?") },
            text = { Text("Leaving early counts as your current placement (${aliveCount} alive).") },
            confirmButton = {
                Button(
                    onClick = {
                        showQuitConfirm = false
                        sim.quitMatch()
                    },
                    colors = ButtonDefaults.buttonColors(containerColor = StormdropColors.Crimson),
                ) { Text("Leave Match") }
            },
            dismissButton = {
                TextButton(onClick = { showQuitConfirm = false }) { Text("Resume") }
            },
        )
    }
}

@Composable
private fun HudCircleButton(
    label: String,
    accent: Color,
    size: androidx.compose.ui.unit.Dp,
    tag: String,
    onClick: () -> Unit,
) {
    Box(
        modifier = Modifier
            .size(size)
            .clip(CircleShape)
            .background(Color.Black.copy(alpha = 0.62f))
            .border(1.5.dp, accent.copy(alpha = 0.85f), CircleShape)
            .clickable(onClick = onClick)
            .testTag(tag),
        contentAlignment = Alignment.Center,
    ) {
        Text(
            text = label,
            style = MaterialTheme.typography.labelSmall.copy(fontSize = 10.sp, lineHeight = 12.sp),
            color = Color.White,
            textAlign = TextAlign.Center,
            fontWeight = FontWeight.Bold,
        )
    }
}
