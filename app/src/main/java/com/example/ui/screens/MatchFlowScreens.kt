package com.example.ui.screens

import androidx.activity.compose.BackHandler
import androidx.compose.animation.core.RepeatMode
import androidx.compose.animation.core.animateFloat
import androidx.compose.animation.core.infiniteRepeatable
import androidx.compose.animation.core.rememberInfiniteTransition
import androidx.compose.animation.core.tween
import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.layout.widthIn
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.CheckCircle
import androidx.compose.material.icons.filled.EmojiEvents
import androidx.compose.material.icons.filled.FlightTakeoff
import androidx.compose.material.icons.filled.Groups
import androidx.compose.material.icons.filled.Radar
import androidx.compose.material.icons.filled.Replay
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.alpha
import androidx.compose.ui.draw.clip
import androidx.compose.ui.draw.scale
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.testTag
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.example.data.model.GameMode
import com.example.data.model.MatchResultSummary
import com.example.ui.components.StormdropTopBar
import com.example.ui.theme.JetBrainsMonoFamily
import com.example.ui.theme.StormdropColors
import kotlinx.coroutines.delay

@Composable
fun ModeSelectScreen(
    selectedMode: GameMode,
    gold: Int,
    diamonds: Int,
    onSelectMode: (GameMode) -> Unit,
    onBack: () -> Unit,
) {
    BackHandler(onBack = onBack)

    Column(
        modifier = Modifier
            .fillMaxSize()
            .background(StormdropColors.BgDeep),
    ) {
        StormdropTopBar(
            title = "Select Mode",
            gold = gold,
            diamonds = diamonds,
            onBack = onBack,
        )
        LazyColumn(
            modifier = Modifier
                .fillMaxSize()
                .widthIn(max = 720.dp)
                .align(Alignment.CenterHorizontally),
            contentPadding = PaddingValues(16.dp),
            verticalArrangement = Arrangement.spacedBy(12.dp),
        ) {
            items(GameMode.entries) { mode ->
                val isSelected = mode == selectedMode
                Surface(
                    color = if (isSelected) StormdropColors.BgElevated else StormdropColors.BgSurface,
                    shape = RoundedCornerShape(16.dp),
                    border = BorderStroke(
                        width = if (isSelected) 2.dp else 1.dp,
                        color = if (isSelected) StormdropColors.AccentOrange else StormdropColors.Divider,
                    ),
                    modifier = Modifier
                        .fillMaxWidth()
                        .clip(RoundedCornerShape(16.dp))
                        .clickable {
                            onSelectMode(mode)
                            onBack()
                        }
                        .testTag("mode_card_${mode.apiId}"),
                ) {
                    Row(
                        modifier = Modifier.padding(16.dp),
                        verticalAlignment = Alignment.CenterVertically,
                    ) {
                        Column(modifier = Modifier.weight(1f)) {
                            Row(
                                verticalAlignment = Alignment.CenterVertically,
                                horizontalArrangement = Arrangement.spacedBy(8.dp),
                            ) {
                                Text(
                                    text = mode.displayName,
                                    style = MaterialTheme.typography.titleMedium,
                                    color = StormdropColors.TextMain,
                                )
                                if (mode.isBattleRoyale) {
                                    Text(
                                        text = "ISLE VERDANT",
                                        fontSize = 10.sp,
                                        fontWeight = FontWeight.Bold,
                                        color = StormdropColors.AccentCyan,
                                    )
                                }
                            }
                            Spacer(Modifier.height(4.dp))
                            Text(
                                text = mode.description,
                                style = MaterialTheme.typography.bodySmall,
                                color = StormdropColors.TextDim,
                            )
                            Spacer(Modifier.height(8.dp))
                            Row(horizontalArrangement = Arrangement.spacedBy(14.dp)) {
                                Row(verticalAlignment = Alignment.CenterVertically) {
                                    Icon(
                                        imageVector = Icons.Filled.Groups,
                                        contentDescription = null,
                                        tint = StormdropColors.AccentOrange,
                                        modifier = Modifier.size(14.dp),
                                    )
                                    Spacer(Modifier.width(4.dp))
                                    Text(
                                        text = "${mode.maxPlayers} Players",
                                        style = MaterialTheme.typography.labelSmall,
                                        color = StormdropColors.TextDim,
                                    )
                                }
                                Text(
                                    text = "Squad size: ${mode.teamSize}",
                                    style = MaterialTheme.typography.labelSmall,
                                    color = StormdropColors.TextDim,
                                )
                            }
                        }
                        if (isSelected) {
                            Icon(
                                imageVector = Icons.Filled.CheckCircle,
                                contentDescription = "Selected",
                                tint = StormdropColors.AccentOrange,
                                modifier = Modifier.size(24.dp),
                            )
                        }
                    }
                }
            }
        }
    }
}

@Composable
fun MatchmakingScreen(
    mode: GameMode,
    isRanked: Boolean,
    region: String,
    partyCount: Int,
    onMatchReady: () -> Unit,
    onCancel: () -> Unit,
) {
    BackHandler(onBack = onCancel)

    var countdown by remember { mutableIntStateOf(3) }
    LaunchedEffect(Unit) {
        while (countdown > 0) {
            delay(650)
            countdown--
        }
        onMatchReady()
    }

    val pulse = rememberInfiniteTransition(label = "radar")
    val scale by pulse.animateFloat(
        initialValue = 0.85f,
        targetValue = 1.25f,
        animationSpec = infiniteRepeatable(tween(900), RepeatMode.Reverse),
        label = "pulse_scale",
    )

    Box(
        modifier = Modifier
            .fillMaxSize()
            .background(StormdropColors.BgDeep)
            .padding(24.dp),
        contentAlignment = Alignment.Center,
    ) {
        Column(
            modifier = Modifier.widthIn(max = 420.dp),
            horizontalAlignment = Alignment.CenterHorizontally,
        ) {
            Box(contentAlignment = Alignment.Center) {
                Box(
                    modifier = Modifier
                        .size(140.dp)
                        .scale(scale)
                        .alpha(0.3f)
                        .clip(CircleShape)
                        .background(StormdropColors.AccentOrange),
                )
                Box(
                    modifier = Modifier
                        .size(100.dp)
                        .clip(CircleShape)
                        .background(StormdropColors.BgElevated)
                        .border(2.dp, StormdropColors.AccentOrange, CircleShape),
                    contentAlignment = Alignment.Center,
                ) {
                    Icon(
                        imageVector = Icons.Filled.Radar,
                        contentDescription = "Searching Match",
                        tint = StormdropColors.AccentOrange,
                        modifier = Modifier.size(44.dp),
                    )
                }
            }

            Spacer(Modifier.height(28.dp))
            Text(
                text = "DEPLOYING TO ${mode.shortName.uppercase()}",
                style = MaterialTheme.typography.headlineSmall,
                color = StormdropColors.TextMain,
                textAlign = TextAlign.Center,
            )
            Spacer(Modifier.height(8.dp))
            Text(
                text = "${if (isRanked) "Ranked" else "Classic"} · Region $region · Squad ${partyCount + 1}/${mode.teamSize}",
                style = MaterialTheme.typography.bodyMedium,
                color = StormdropColors.AccentCyan,
                textAlign = TextAlign.Center,
            )
            Spacer(Modifier.height(16.dp))
            Row(verticalAlignment = Alignment.CenterVertically) {
                Icon(
                    imageVector = Icons.Filled.FlightTakeoff,
                    contentDescription = null,
                    tint = StormdropColors.Gold,
                    modifier = Modifier.size(18.dp),
                )
                Spacer(Modifier.width(6.dp))
                Text(
                    text = if (countdown > 0) "Boarding dropship in ${countdown}s..." else "Launching!",
                    style = MaterialTheme.typography.titleMedium.copy(fontFamily = JetBrainsMonoFamily),
                    color = StormdropColors.Gold,
                )
            }

            Spacer(Modifier.height(32.dp))
            OutlinedButton(
                onClick = onCancel,
                border = BorderStroke(1.dp, StormdropColors.Crimson.copy(alpha = 0.7f)),
                shape = RoundedCornerShape(14.dp),
                modifier = Modifier
                    .fillMaxWidth()
                    .height(50.dp)
                    .testTag("cancel_matchmaking_button"),
            ) {
                Text("Cancel Deployment", color = StormdropColors.Crimson)
            }
        }
    }
}

@Composable
fun ResultsScreen(
    summary: MatchResultSummary?,
    onPlayAgain: () -> Unit,
    onReturnLobby: () -> Unit,
) {
    BackHandler(onBack = onReturnLobby)

    val won = summary?.won == true
    val bannerColor = if (won) StormdropColors.Gold else StormdropColors.AccentCyan

    Column(
        modifier = Modifier
            .fillMaxSize()
            .background(StormdropColors.BgDeep)
            .verticalScroll(rememberScrollState())
            .padding(20.dp),
        horizontalAlignment = Alignment.CenterHorizontally,
    ) {
        Spacer(Modifier.height(18.dp))
        Surface(
            color = bannerColor.copy(alpha = 0.14f),
            shape = RoundedCornerShape(20.dp),
            border = BorderStroke(1.5.dp, bannerColor.copy(alpha = 0.6f)),
            modifier = Modifier.widthIn(max = 520.dp).fillMaxWidth(),
        ) {
            Column(
                modifier = Modifier.padding(24.dp),
                horizontalAlignment = Alignment.CenterHorizontally,
            ) {
                Icon(
                    imageVector = Icons.Filled.EmojiEvents,
                    contentDescription = "Match Result",
                    tint = bannerColor,
                    modifier = Modifier.size(52.dp),
                )
                Spacer(Modifier.height(8.dp))
                Text(
                    text = if (won) "STORM CHAMPION!" else "ELIMINATED · #${summary?.placement ?: 1}",
                    style = MaterialTheme.typography.headlineMedium,
                    color = bannerColor,
                    textAlign = TextAlign.Center,
                )
                Spacer(Modifier.height(4.dp))
                Text(
                    text = "${summary?.mode?.displayName ?: "Battle Royale"} · Placement #${summary?.placement ?: 1} / ${summary?.totalTeams ?: 20}",
                    style = MaterialTheme.typography.bodyMedium,
                    color = StormdropColors.TextDim,
                    textAlign = TextAlign.Center,
                )
            }
        }

        Spacer(Modifier.height(16.dp))

        if (summary != null) {
            Surface(
                color = StormdropColors.BgSurface,
                shape = RoundedCornerShape(18.dp),
                border = BorderStroke(1.dp, StormdropColors.Divider),
                modifier = Modifier.widthIn(max = 520.dp).fillMaxWidth(),
            ) {
                Column(
                    modifier = Modifier.padding(18.dp),
                    verticalArrangement = Arrangement.spacedBy(12.dp),
                ) {
                    Text(
                        text = "COMBAT REPORT",
                        style = MaterialTheme.typography.labelMedium,
                        color = StormdropColors.TextMuted,
                    )
                    Row(
                        modifier = Modifier.fillMaxWidth(),
                        horizontalArrangement = Arrangement.SpaceBetween,
                    ) {
                        StatCell("Kills", summary.kills.toString(), StormdropColors.AccentOrange)
                        StatCell("Damage", summary.damage.toString(), StormdropColors.AccentCyan)
                        StatCell("Headshots", summary.headshots.toString(), StormdropColors.Crimson)
                        StatCell(
                            "Survived",
                            "${summary.survivalSeconds / 60}m ${summary.survivalSeconds % 60}s",
                            StormdropColors.Emerald,
                        )
                    }
                }
            }

            Spacer(Modifier.height(14.dp))

            Surface(
                color = StormdropColors.BgSurface,
                shape = RoundedCornerShape(18.dp),
                border = BorderStroke(1.dp, StormdropColors.Divider),
                modifier = Modifier.widthIn(max = 520.dp).fillMaxWidth(),
            ) {
                Column(
                    modifier = Modifier.padding(18.dp),
                    verticalArrangement = Arrangement.spacedBy(10.dp),
                ) {
                    Text(
                        text = "REWARDS SYNCED TO FIREBASE",
                        style = MaterialTheme.typography.labelMedium,
                        color = StormdropColors.TextMuted,
                    )
                    RewardRow("Account XP", "+${summary.xpGained} XP", StormdropColors.AccentCyan)
                    RewardRow("Gold Earned", "+${summary.goldGained} Gold", StormdropColors.Gold)
                    RewardRow("Storm Pass XP", "+${summary.passXpGained} Pass XP", StormdropColors.VigorPurple)
                    if (summary.isRanked) {
                        val sign = if (summary.rankDelta >= 0) "+" else ""
                        RewardRow(
                            "Rank Points",
                            "$sign${summary.rankDelta} RP",
                            if (summary.rankDelta >= 0) StormdropColors.Emerald else StormdropColors.Crimson,
                        )
                    }
                    if (summary.levelsGained.isNotEmpty()) {
                        Text(
                            text = "LEVEL UP! Reached Lv ${summary.levelsGained.last()} (+Gold & Diamonds bonus!)",
                            style = MaterialTheme.typography.titleSmall,
                            color = StormdropColors.Gold,
                        )
                    }
                }
            }
        }

        Spacer(Modifier.height(22.dp))

        Row(
            modifier = Modifier.widthIn(max = 520.dp).fillMaxWidth(),
            horizontalArrangement = Arrangement.spacedBy(12.dp),
        ) {
            OutlinedButton(
                onClick = onReturnLobby,
                border = BorderStroke(1.dp, StormdropColors.Divider),
                shape = RoundedCornerShape(14.dp),
                modifier = Modifier
                    .weight(1f)
                    .height(54.dp)
                    .testTag("results_lobby_button"),
            ) {
                Text("Lobby", color = StormdropColors.TextMain)
            }
            Button(
                onClick = onPlayAgain,
                colors = ButtonDefaults.buttonColors(
                    containerColor = StormdropColors.AccentOrange,
                    contentColor = Color(0xFF140B00),
                ),
                shape = RoundedCornerShape(14.dp),
                modifier = Modifier
                    .weight(1f)
                    .height(54.dp)
                    .testTag("results_play_again_button"),
            ) {
                Icon(Icons.Filled.Replay, contentDescription = "Play Again", modifier = Modifier.size(18.dp))
                Spacer(Modifier.width(6.dp))
                Text("Play Again", fontWeight = FontWeight.Bold)
            }
        }
    }
}

@Composable
private fun StatCell(label: String, value: String, color: Color) {
    Column(horizontalAlignment = Alignment.CenterHorizontally) {
        Text(
            text = value,
            style = MaterialTheme.typography.titleLarge.copy(
                fontFamily = JetBrainsMonoFamily,
                color = color,
            ),
        )
        Text(
            text = label,
            style = MaterialTheme.typography.labelSmall,
            color = StormdropColors.TextDim,
        )
    }
}

@Composable
private fun RewardRow(label: String, value: String, color: Color) {
    Row(
        modifier = Modifier.fillMaxWidth(),
        horizontalArrangement = Arrangement.SpaceBetween,
    ) {
        Text(label, style = MaterialTheme.typography.bodyMedium, color = StormdropColors.TextDim)
        Text(
            value,
            style = MaterialTheme.typography.titleSmall.copy(fontFamily = JetBrainsMonoFamily),
            color = color,
        )
    }
}
