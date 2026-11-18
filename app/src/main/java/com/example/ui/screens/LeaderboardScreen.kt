package com.example.ui.screens

import androidx.activity.compose.BackHandler
import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.layout.widthIn
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.LazyRow
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.FilterChip
import androidx.compose.material3.FilterChipDefaults
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Surface
import androidx.compose.material3.Tab
import androidx.compose.material3.TabRow
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.testTag
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import com.example.data.model.GameCatalog
import com.example.data.model.GameMode
import com.example.data.model.LeaderboardEntry
import com.example.data.remote.MatchHistoryDoc
import com.example.ui.components.RankBadgePill
import com.example.ui.components.StormdropTopBar
import com.example.ui.theme.JetBrainsMonoFamily
import com.example.ui.theme.StormdropColors

@Composable
fun LeaderboardScreen(
    gold: Int,
    diamonds: Int,
    buildLeaderboard: (String) -> List<LeaderboardEntry>,
    matchHistory: List<MatchHistoryDoc>,
    onBack: () -> Unit,
) {
    BackHandler(onBack = onBack)

    var selectedTab by remember { mutableIntStateOf(0) }
    var selectedRegion by remember { mutableStateOf("ALL") }
    val entries = remember(selectedRegion, buildLeaderboard) { buildLeaderboard(selectedRegion) }

    Column(
        modifier = Modifier
            .fillMaxSize()
            .background(StormdropColors.BgDeep),
    ) {
        StormdropTopBar(
            title = "Rankings & History",
            gold = gold,
            diamonds = diamonds,
            onBack = onBack,
        )

        TabRow(
            selectedTabIndex = selectedTab,
            containerColor = StormdropColors.BgSurface,
            contentColor = StormdropColors.AccentCyan,
        ) {
            listOf("Leaderboard", "Match History").forEachIndexed { idx, label ->
                Tab(
                    selected = selectedTab == idx,
                    onClick = { selectedTab = idx },
                    text = {
                        Text(
                            label,
                            color = if (selectedTab == idx) StormdropColors.AccentCyan else StormdropColors.TextDim,
                        )
                    },
                    modifier = Modifier.testTag("leaderboard_tab_$idx"),
                )
            }
        }

        if (selectedTab == 0) {
            LazyRow(
                contentPadding = PaddingValues(horizontal = 16.dp, vertical = 10.dp),
                horizontalArrangement = Arrangement.spacedBy(8.dp),
            ) {
                items(listOf("ALL") + GameCatalog.regions) { reg ->
                    FilterChip(
                        selected = selectedRegion == reg,
                        onClick = { selectedRegion = reg },
                        label = { Text(reg) },
                        colors = FilterChipDefaults.filterChipColors(
                            selectedContainerColor = StormdropColors.AccentCyan.copy(alpha = 0.22f),
                            selectedLabelColor = StormdropColors.AccentCyan,
                        ),
                    )
                }
            }

            LazyColumn(
                modifier = Modifier
                    .fillMaxSize()
                    .widthIn(max = 760.dp)
                    .align(Alignment.CenterHorizontally),
                contentPadding = PaddingValues(horizontal = 16.dp, vertical = 8.dp),
                verticalArrangement = Arrangement.spacedBy(8.dp),
            ) {
                items(entries, key = { it.playerTag }) { entry ->
                    Surface(
                        color = if (entry.isSelf) StormdropColors.BgElevated else StormdropColors.BgSurface,
                        shape = RoundedCornerShape(14.dp),
                        border = BorderStroke(
                            1.dp,
                            if (entry.isSelf) StormdropColors.AccentOrange else StormdropColors.Divider,
                        ),
                    ) {
                        Row(
                            modifier = Modifier
                                .fillMaxWidth()
                                .padding(12.dp),
                            verticalAlignment = Alignment.CenterVertically,
                        ) {
                            Text(
                                text = "#${entry.rank}",
                                style = MaterialTheme.typography.titleMedium.copy(fontFamily = JetBrainsMonoFamily),
                                color = when (entry.rank) {
                                    1 -> StormdropColors.Gold
                                    2 -> StormdropColors.AccentCyan
                                    3 -> StormdropColors.AccentOrange
                                    else -> StormdropColors.TextDim
                                },
                                modifier = Modifier.width(42.dp),
                            )
                            Column(modifier = Modifier.weight(1f)) {
                                Row(
                                    verticalAlignment = Alignment.CenterVertically,
                                    horizontalArrangement = Arrangement.spacedBy(6.dp),
                                ) {
                                    if (!entry.guildTag.isNullOrBlank()) {
                                        Text(
                                            text = "[${entry.guildTag}]",
                                            style = MaterialTheme.typography.labelMedium,
                                            color = StormdropColors.AccentCyan,
                                        )
                                    }
                                    Text(
                                        text = if (entry.isSelf) "${entry.nickname} (YOU)" else entry.nickname,
                                        style = MaterialTheme.typography.titleSmall.copy(
                                            fontWeight = if (entry.isSelf) FontWeight.Bold else FontWeight.SemiBold,
                                        ),
                                        color = StormdropColors.TextMain,
                                    )
                                }
                                Text(
                                    text = "Lv ${entry.level} · ${entry.region} · ${entry.brRankPoints} RP",
                                    style = MaterialTheme.typography.labelSmall,
                                    color = StormdropColors.TextDim,
                                )
                            }
                            RankBadgePill(points = entry.brRankPoints, prefix = "BR")
                        }
                    }
                }
            }
        } else {
            if (matchHistory.isEmpty()) {
                Column(
                    modifier = Modifier
                        .fillMaxSize()
                        .padding(32.dp),
                    horizontalAlignment = Alignment.CenterHorizontally,
                    verticalArrangement = Arrangement.Center,
                ) {
                    Text("No matches recorded yet.", color = StormdropColors.TextDim)
                }
            } else {
                LazyColumn(
                    modifier = Modifier
                        .fillMaxSize()
                        .widthIn(max = 760.dp)
                        .align(Alignment.CenterHorizontally),
                    contentPadding = PaddingValues(16.dp),
                    verticalArrangement = Arrangement.spacedBy(10.dp),
                ) {
                    items(matchHistory, key = { it.id }) { m ->
                        val mode = GameMode.fromApiId(m.modeId)
                        Surface(
                            color = StormdropColors.BgSurface,
                            shape = RoundedCornerShape(14.dp),
                            border = BorderStroke(
                                1.dp,
                                if (m.won) StormdropColors.Gold.copy(alpha = 0.55f) else StormdropColors.Divider,
                            ),
                        ) {
                            Row(
                                modifier = Modifier
                                    .fillMaxWidth()
                                    .padding(14.dp),
                                verticalAlignment = Alignment.CenterVertically,
                            ) {
                                Column(modifier = Modifier.weight(1f)) {
                                    Text(
                                        text = "${mode.shortName}${if (m.ranked) " (Ranked)" else ""} · ${if (m.won) "CHAMPION" else "#${m.placement}/${m.totalTeams}"}",
                                        style = MaterialTheme.typography.titleSmall,
                                        color = if (m.won) StormdropColors.Gold else StormdropColors.TextMain,
                                    )
                                    Text(
                                        text = "${m.kills} Kills · ${m.damage} Dmg · ${m.headshots} HS · ${m.survivalSeconds}s",
                                        style = MaterialTheme.typography.bodySmall,
                                        color = StormdropColors.TextDim,
                                    )
                                }
                                Column(horizontalAlignment = Alignment.End) {
                                    Text(
                                        text = "+${m.goldGained}G  +${m.xpGained}XP",
                                        style = MaterialTheme.typography.labelMedium.copy(fontFamily = JetBrainsMonoFamily),
                                        color = StormdropColors.Gold,
                                    )
                                    if (m.ranked) {
                                        val sign = if (m.rankDelta >= 0) "+" else ""
                                        Text(
                                            text = "$sign${m.rankDelta} RP",
                                            style = MaterialTheme.typography.labelSmall.copy(fontFamily = JetBrainsMonoFamily),
                                            color = if (m.rankDelta >= 0) StormdropColors.Emerald else StormdropColors.Crimson,
                                        )
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
