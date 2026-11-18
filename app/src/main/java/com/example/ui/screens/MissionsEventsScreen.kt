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
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.widthIn
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.LinearProgressIndicator
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Surface
import androidx.compose.material3.Tab
import androidx.compose.material3.TabRow
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.testTag
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import com.example.data.model.GameCatalog
import com.example.data.remote.AchievementProgressDoc
import com.example.data.remote.MissionProgressDoc
import com.example.ui.components.StormdropTopBar
import com.example.ui.theme.JetBrainsMonoFamily
import com.example.ui.theme.StormdropColors

@Composable
fun MissionsEventsScreen(
    gold: Int,
    diamonds: Int,
    missions: List<MissionProgressDoc>,
    achievements: List<AchievementProgressDoc>,
    onClaimMission: (String) -> Unit,
    onClaimAchievement: (String) -> Unit,
    onBack: () -> Unit,
) {
    BackHandler(onBack = onBack)

    var selectedTab by remember { mutableIntStateOf(0) }
    val missionMap = remember(missions) { missions.associateBy { it.missionId } }
    val achMap = remember(achievements) { achievements.associateBy { it.achievementId } }

    Column(
        modifier = Modifier
            .fillMaxSize()
            .background(StormdropColors.BgDeep),
    ) {
        StormdropTopBar(
            title = "Missions & Events",
            gold = gold,
            diamonds = diamonds,
            onBack = onBack,
        )

        TabRow(
            selectedTabIndex = selectedTab,
            containerColor = StormdropColors.BgSurface,
            contentColor = StormdropColors.Emerald,
        ) {
            listOf("Missions", "Achievements", "Events").forEachIndexed { idx, label ->
                Tab(
                    selected = selectedTab == idx,
                    onClick = { selectedTab = idx },
                    text = {
                        Text(
                            label,
                            color = if (selectedTab == idx) StormdropColors.Emerald else StormdropColors.TextDim,
                        )
                    },
                    modifier = Modifier.testTag("missions_tab_$idx"),
                )
            }
        }

        LazyColumn(
            modifier = Modifier
                .fillMaxSize()
                .widthIn(max = 760.dp)
                .align(Alignment.CenterHorizontally),
            contentPadding = PaddingValues(16.dp),
            verticalArrangement = Arrangement.spacedBy(12.dp),
        ) {
            when (selectedTab) {
                0 -> {
                    items(GameCatalog.missions, key = { it.id }) { def ->
                        val prog = missionMap[def.id]
                        val cur = (prog?.progress ?: 0).coerceAtMost(def.target)
                        val claimed = prog?.claimed == true
                        val ready = cur >= def.target && !claimed
                        Surface(
                            color = StormdropColors.BgSurface,
                            shape = RoundedCornerShape(16.dp),
                            border = BorderStroke(
                                1.dp,
                                if (ready) StormdropColors.Emerald.copy(alpha = 0.6f) else StormdropColors.Divider,
                            ),
                        ) {
                            Column(
                                modifier = Modifier.padding(14.dp),
                                verticalArrangement = Arrangement.spacedBy(8.dp),
                            ) {
                                Row(
                                    modifier = Modifier.fillMaxWidth(),
                                    horizontalArrangement = Arrangement.SpaceBetween,
                                ) {
                                    Column(modifier = Modifier.weight(1f)) {
                                        Text(
                                            text = "${def.period.uppercase()} · ${def.title}",
                                            style = MaterialTheme.typography.titleSmall,
                                            color = StormdropColors.TextMain,
                                        )
                                        Text(
                                            text = "+${def.rewardGold} Gold · +${def.rewardXp} XP" +
                                                if (def.rewardDiamonds > 0) " · +${def.rewardDiamonds} Dia" else "",
                                            style = MaterialTheme.typography.labelSmall,
                                            color = StormdropColors.Gold,
                                        )
                                    }
                                    Text(
                                        text = "$cur / ${def.target}",
                                        style = MaterialTheme.typography.labelLarge.copy(fontFamily = JetBrainsMonoFamily),
                                        color = StormdropColors.AccentCyan,
                                    )
                                }
                                LinearProgressIndicator(
                                    progress = { (cur.toFloat() / def.target).coerceIn(0f, 1f) },
                                    modifier = Modifier
                                        .fillMaxWidth()
                                        .height(6.dp)
                                        .clip(RoundedCornerShape(4.dp)),
                                    color = StormdropColors.Emerald,
                                    trackColor = StormdropColors.BgHighlight,
                                )
                                Button(
                                    onClick = { onClaimMission(def.id) },
                                    enabled = ready,
                                    colors = ButtonDefaults.buttonColors(
                                        containerColor = StormdropColors.Emerald,
                                        contentColor = Color(0xFF001C0B),
                                    ),
                                    shape = RoundedCornerShape(10.dp),
                                    modifier = Modifier
                                        .fillMaxWidth()
                                        .height(48.dp)
                                        .testTag("claim_mission_${def.id}"),
                                ) {
                                    Text(if (claimed) "Claimed" else if (ready) "Claim Reward" else "In Progress", fontWeight = FontWeight.Bold)
                                }
                            }
                        }
                    }
                }

                1 -> {
                    items(GameCatalog.achievements, key = { it.id }) { def ->
                        val prog = achMap[def.id]
                        val cur = (prog?.progress ?: 0).coerceAtMost(def.target)
                        val claimed = prog?.claimed == true
                        val ready = cur >= def.target && !claimed
                        Surface(
                            color = StormdropColors.BgSurface,
                            shape = RoundedCornerShape(16.dp),
                            border = BorderStroke(1.dp, StormdropColors.Divider),
                        ) {
                            Column(
                                modifier = Modifier.padding(14.dp),
                                verticalArrangement = Arrangement.spacedBy(8.dp),
                            ) {
                                Row(
                                    modifier = Modifier.fillMaxWidth(),
                                    horizontalArrangement = Arrangement.SpaceBetween,
                                ) {
                                    Text(def.title, style = MaterialTheme.typography.titleSmall, color = StormdropColors.TextMain)
                                    Text(
                                        "$cur / ${def.target}",
                                        style = MaterialTheme.typography.labelLarge.copy(fontFamily = JetBrainsMonoFamily),
                                        color = StormdropColors.Gold,
                                    )
                                }
                                Text(
                                    text = "Reward: +${def.rewardGold} Gold · +${def.rewardDiamonds} Diamonds",
                                    style = MaterialTheme.typography.labelSmall,
                                    color = StormdropColors.Diamond,
                                )
                                LinearProgressIndicator(
                                    progress = { (cur.toFloat() / def.target).coerceIn(0f, 1f) },
                                    modifier = Modifier
                                        .fillMaxWidth()
                                        .height(6.dp)
                                        .clip(RoundedCornerShape(4.dp)),
                                    color = StormdropColors.Gold,
                                    trackColor = StormdropColors.BgHighlight,
                                )
                                Button(
                                    onClick = { onClaimAchievement(def.id) },
                                    enabled = ready,
                                    colors = ButtonDefaults.buttonColors(
                                        containerColor = StormdropColors.Gold,
                                        contentColor = Color(0xFF140B00),
                                    ),
                                    shape = RoundedCornerShape(10.dp),
                                    modifier = Modifier.fillMaxWidth().height(48.dp),
                                ) {
                                    Text(if (claimed) "Claimed" else if (ready) "Claim" else "Locked", fontWeight = FontWeight.Bold)
                                }
                            }
                        }
                    }
                }

                else -> {
                    items(GameCatalog.events, key = { it.id }) { ev ->
                        Surface(
                            color = StormdropColors.BgSurface,
                            shape = RoundedCornerShape(16.dp),
                            border = BorderStroke(1.dp, StormdropColors.AccentOrange.copy(alpha = 0.4f)),
                        ) {
                            Column(modifier = Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(6.dp)) {
                                Row(
                                    modifier = Modifier.fillMaxWidth(),
                                    horizontalArrangement = Arrangement.SpaceBetween,
                                ) {
                                    Text(ev.title, style = MaterialTheme.typography.titleMedium, color = StormdropColors.AccentOrange)
                                    Text(ev.dateRange, style = MaterialTheme.typography.labelSmall, color = StormdropColors.AccentCyan)
                                }
                                Text(ev.description, style = MaterialTheme.typography.bodyMedium, color = StormdropColors.TextDim)
                            }
                        }
                    }
                }
            }
        }
    }
}
