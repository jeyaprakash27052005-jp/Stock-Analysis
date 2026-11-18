package com.example.ui.screens

import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.Image
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.BoxWithConstraints
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
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.LazyRow
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.KeyboardArrowLeft
import androidx.compose.material.icons.automirrored.filled.KeyboardArrowRight
import androidx.compose.material.icons.filled.Backpack
import androidx.compose.material.icons.filled.CalendarMonth
import androidx.compose.material.icons.filled.CardGiftcard
import androidx.compose.material.icons.filled.EmojiEvents
import androidx.compose.material.icons.filled.FlashOn
import androidx.compose.material.icons.filled.Group
import androidx.compose.material.icons.filled.Leaderboard
import androidx.compose.material.icons.filled.PlayArrow
import androidx.compose.material.icons.filled.Settings
import androidx.compose.material.icons.filled.Shield
import androidx.compose.material.icons.filled.ShoppingBag
import androidx.compose.material.icons.filled.Star
import androidx.compose.material.icons.filled.TaskAlt
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.LinearProgressIndicator
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.Surface
import androidx.compose.material3.Switch
import androidx.compose.material3.SwitchDefaults
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.platform.testTag
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.example.R
import com.example.data.CloudHealthState
import com.example.data.model.GameCatalog
import com.example.data.model.GameMode
import com.example.data.model.RankCalculator
import com.example.data.remote.GuildDoc
import com.example.data.remote.UserProfileDoc
import com.example.ui.components.CloudSyncBadge
import com.example.ui.components.CurrencyPill
import com.example.ui.components.RankBadgePill
import com.example.ui.components.RarityChip
import com.example.ui.theme.JetBrainsMonoFamily
import com.example.ui.theme.StormdropColors

@Composable
fun LobbyScreen(
    profile: UserProfileDoc?,
    guilds: List<GuildDoc>,
    selectedMode: GameMode,
    isRanked: Boolean,
    partyMembers: List<String>,
    cloudHealth: CloudHealthState,
    canClaimDailyLogin: Boolean,
    onCycleCharacter: (Int) -> Unit,
    onClaimDailyLogin: () -> Unit,
    onToggleRanked: () -> Unit,
    onOpenModeSelect: () -> Unit,
    onDeployMatch: () -> Unit,
    onLeaveParty: () -> Unit,
    onNavigateStore: () -> Unit,
    onNavigateVault: () -> Unit,
    onNavigatePass: () -> Unit,
    onNavigateMissions: () -> Unit,
    onNavigateSocial: () -> Unit,
    onNavigateLeaderboard: () -> Unit,
    onNavigateSettings: () -> Unit,
) {
    var showDailyDialog by remember { mutableStateOf(false) }

    val character = remember(profile?.equippedCharacterId) {
        GameCatalog.itemById(profile?.equippedCharacterId ?: "char_kael") ?: GameCatalog.characters.first()
    }
    val pet = remember(profile?.equippedPetId) {
        profile?.equippedPetId?.let { GameCatalog.itemById(it) }
    }
    val joinedGuild = remember(profile?.guildId, guilds) {
        profile?.guildId?.let { gid -> guilds.firstOrNull { it.id == gid } }
    }

    Column(
        modifier = Modifier
            .fillMaxSize()
            .background(StormdropColors.BgDeep),
    ) {
        // Top Header Bar
        Surface(
            color = StormdropColors.BgSurface,
            tonalElevation = 2.dp,
            modifier = Modifier.fillMaxWidth(),
        ) {
            Row(
                modifier = Modifier
                    .fillMaxWidth()
                    .padding(horizontal = 12.dp, vertical = 10.dp),
                verticalAlignment = Alignment.CenterVertically,
            ) {
                Box(
                    modifier = Modifier
                        .size(44.dp)
                        .clip(CircleShape)
                        .background(character.accentColor.copy(alpha = 0.22f))
                        .border(1.5.dp, character.accentColor, CircleShape),
                    contentAlignment = Alignment.Center,
                ) {
                    Text(
                        text = (profile?.nickname ?: "D").take(1).uppercase(),
                        style = MaterialTheme.typography.titleMedium.copy(
                            color = character.accentColor,
                            fontWeight = FontWeight.Bold,
                        ),
                    )
                }
                Spacer(Modifier.width(10.dp))
                Column(modifier = Modifier.weight(1f)) {
                    Row(verticalAlignment = Alignment.CenterVertically) {
                        if (joinedGuild != null) {
                            Text(
                                text = "[${joinedGuild.tag}] ",
                                style = MaterialTheme.typography.labelMedium,
                                color = StormdropColors.AccentCyan,
                            )
                        }
                        Text(
                            text = profile?.nickname ?: "Syncing...",
                            style = MaterialTheme.typography.titleMedium,
                            color = StormdropColors.TextMain,
                            maxLines = 1,
                            overflow = TextOverflow.Ellipsis,
                        )
                    }
                    Row(
                        verticalAlignment = Alignment.CenterVertically,
                        horizontalArrangement = Arrangement.spacedBy(8.dp),
                    ) {
                        val lvl = profile?.level ?: 1
                        val xp = profile?.xp ?: 0
                        val nextXp = RankCalculator.xpForNextLevel(lvl)
                        Text(
                            text = "Lv $lvl · ${profile?.region ?: "IND"}",
                            style = MaterialTheme.typography.labelSmall.copy(fontFamily = JetBrainsMonoFamily),
                            color = StormdropColors.TextDim,
                        )
                        LinearProgressIndicator(
                            progress = { (xp.toFloat() / nextXp).coerceIn(0f, 1f) },
                            modifier = Modifier
                                .width(58.dp)
                                .height(5.dp)
                                .clip(RoundedCornerShape(4.dp)),
                            color = StormdropColors.AccentOrange,
                            trackColor = StormdropColors.BgHighlight,
                        )
                    }
                }
                CurrencyPill(
                    gold = profile?.gold ?: 0,
                    diamonds = profile?.diamonds ?: 0,
                    onDiamondsClick = onNavigateStore,
                )
                IconButton(
                    onClick = onNavigateSettings,
                    modifier = Modifier
                        .size(48.dp)
                        .testTag("settings_button"),
                ) {
                    Icon(
                        imageVector = Icons.Filled.Settings,
                        contentDescription = "Settings",
                        tint = StormdropColors.TextMain,
                    )
                }
            }
        }

        // Responsive Lobby Content
        BoxWithConstraints(
            modifier = Modifier
                .weight(1f)
                .fillMaxWidth(),
        ) {
            val isWide = maxWidth >= 680.dp
            if (isWide) {
                Row(
                    modifier = Modifier
                        .fillMaxSize()
                        .padding(16.dp),
                    horizontalArrangement = Arrangement.spacedBy(16.dp),
                ) {
                    Column(
                        modifier = Modifier
                            .weight(1.15f)
                            .verticalScroll(rememberScrollState()),
                        verticalArrangement = Arrangement.spacedBy(12.dp),
                    ) {
                        CharacterShowcaseCard(
                            character = character,
                            pet = pet,
                            profile = profile,
                            partyMembers = partyMembers,
                            cloudHealth = cloudHealth,
                            onCycleCharacter = onCycleCharacter,
                            onLeaveParty = onLeaveParty,
                        )
                        QuickNavGrid(
                            onNavigateStore = onNavigateStore,
                            onNavigateVault = onNavigateVault,
                            onNavigatePass = onNavigatePass,
                            onNavigateMissions = onNavigateMissions,
                            onNavigateSocial = onNavigateSocial,
                            onNavigateLeaderboard = onNavigateLeaderboard,
                        )
                    }
                    Column(
                        modifier = Modifier
                            .weight(0.85f)
                            .verticalScroll(rememberScrollState()),
                        verticalArrangement = Arrangement.spacedBy(12.dp),
                    ) {
                        DailyLoginBanner(
                            streakDay = profile?.loginStreakDay ?: 0,
                            canClaim = canClaimDailyLogin,
                            onOpen = { showDailyDialog = true },
                        )
                        DeployPanel(
                            selectedMode = selectedMode,
                            isRanked = isRanked,
                            level = profile?.level ?: 1,
                            onToggleRanked = onToggleRanked,
                            onOpenModeSelect = onOpenModeSelect,
                            onDeployMatch = onDeployMatch,
                        )
                    }
                }
            } else {
                LazyColumn(
                    modifier = Modifier.fillMaxSize(),
                    contentPadding = PaddingValues(14.dp),
                    verticalArrangement = Arrangement.spacedBy(12.dp),
                ) {
                    item {
                        DailyLoginBanner(
                            streakDay = profile?.loginStreakDay ?: 0,
                            canClaim = canClaimDailyLogin,
                            onOpen = { showDailyDialog = true },
                        )
                    }
                    item {
                        CharacterShowcaseCard(
                            character = character,
                            pet = pet,
                            profile = profile,
                            partyMembers = partyMembers,
                            cloudHealth = cloudHealth,
                            onCycleCharacter = onCycleCharacter,
                            onLeaveParty = onLeaveParty,
                        )
                    }
                    item {
                        DeployPanel(
                            selectedMode = selectedMode,
                            isRanked = isRanked,
                            level = profile?.level ?: 1,
                            onToggleRanked = onToggleRanked,
                            onOpenModeSelect = onOpenModeSelect,
                            onDeployMatch = onDeployMatch,
                        )
                    }
                    item {
                        QuickNavGrid(
                            onNavigateStore = onNavigateStore,
                            onNavigateVault = onNavigateVault,
                            onNavigatePass = onNavigatePass,
                            onNavigateMissions = onNavigateMissions,
                            onNavigateSocial = onNavigateSocial,
                            onNavigateLeaderboard = onNavigateLeaderboard,
                        )
                    }
                }
            }
        }
    }

    if (showDailyDialog) {
        AlertDialog(
            onDismissRequest = { showDailyDialog = false },
            containerColor = StormdropColors.BgSurface,
            title = {
                Text("7-Day Login Rewards", color = StormdropColors.TextMain)
            },
            text = {
                val streak = profile?.loginStreakDay ?: 0
                Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                    Text(
                        text = "Current Streak: Day $streak / 7",
                        style = MaterialTheme.typography.bodySmall,
                        color = StormdropColors.AccentCyan,
                    )
                    GameCatalog.loginRewards.forEach { reward ->
                        val item = GameCatalog.itemById(reward.itemId)
                        val claimed = reward.day <= streak
                        val isNext = reward.day == (streak % 7) + 1 && canClaimDailyLogin
                        Surface(
                            color = if (isNext) StormdropColors.AccentOrange.copy(alpha = 0.16f) else StormdropColors.BgElevated,
                            shape = RoundedCornerShape(10.dp),
                            border = BorderStroke(
                                1.dp,
                                if (isNext) StormdropColors.AccentOrange else StormdropColors.Divider,
                            ),
                        ) {
                            Row(
                                modifier = Modifier
                                    .fillMaxWidth()
                                    .padding(horizontal = 12.dp, vertical = 8.dp),
                                verticalAlignment = Alignment.CenterVertically,
                            ) {
                                Text(
                                    text = "Day ${reward.day}",
                                    style = MaterialTheme.typography.labelLarge,
                                    color = StormdropColors.Gold,
                                    modifier = Modifier.width(54.dp),
                                )
                                Text(
                                    text = item?.name ?: reward.itemId,
                                    style = MaterialTheme.typography.bodyMedium,
                                    color = StormdropColors.TextMain,
                                    modifier = Modifier.weight(1f),
                                )
                                Text(
                                    text = when {
                                        claimed -> "Claimed"
                                        isNext -> "Ready"
                                        else -> "Locked"
                                    },
                                    style = MaterialTheme.typography.labelSmall,
                                    color = when {
                                        claimed -> StormdropColors.Emerald
                                        isNext -> StormdropColors.AccentOrange
                                        else -> StormdropColors.TextMuted
                                    },
                                )
                            }
                        }
                    }
                }
            },
            confirmButton = {
                Button(
                    onClick = {
                        onClaimDailyLogin()
                        showDailyDialog = false
                    },
                    enabled = canClaimDailyLogin,
                    colors = ButtonDefaults.buttonColors(
                        containerColor = StormdropColors.AccentOrange,
                        contentColor = Color(0xFF140B00),
                    ),
                    modifier = Modifier.testTag("claim_daily_button"),
                ) {
                    Text(if (canClaimDailyLogin) "Claim Today" else "Claimed Today")
                }
            },
            dismissButton = {
                TextButton(onClick = { showDailyDialog = false }) {
                    Text("Close", color = StormdropColors.TextDim)
                }
            },
        )
    }
}

@Composable
private fun DailyLoginBanner(
    streakDay: Int,
    canClaim: Boolean,
    onOpen: () -> Unit,
) {
    Surface(
        color = if (canClaim) StormdropColors.AccentOrange.copy(alpha = 0.14f) else StormdropColors.BgSurface,
        shape = RoundedCornerShape(16.dp),
        border = BorderStroke(
            1.dp,
            if (canClaim) StormdropColors.AccentOrange.copy(alpha = 0.6f) else StormdropColors.Divider,
        ),
        modifier = Modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(16.dp))
            .clickable(onClick = onOpen)
            .testTag("daily_login_banner"),
    ) {
        Row(
            modifier = Modifier.padding(horizontal = 14.dp, vertical = 10.dp),
            verticalAlignment = Alignment.CenterVertically,
        ) {
            Icon(
                imageVector = Icons.Filled.CardGiftcard,
                contentDescription = "Daily Login Reward",
                tint = if (canClaim) StormdropColors.AccentOrange else StormdropColors.Gold,
                modifier = Modifier.size(24.dp),
            )
            Spacer(Modifier.width(12.dp))
            Column(modifier = Modifier.weight(1f)) {
                Text(
                    text = if (canClaim) "Daily Login Drop Available!" else "Daily Login Streak: Day $streakDay / 7",
                    style = MaterialTheme.typography.titleSmall,
                    color = StormdropColors.TextMain,
                )
                Text(
                    text = if (canClaim) "Tap to claim Day ${(streakDay % 7) + 1} reward" else "Come back tomorrow for your next reward",
                    style = MaterialTheme.typography.bodySmall,
                    color = StormdropColors.TextDim,
                )
            }
            Icon(
                imageVector = Icons.AutoMirrored.Filled.KeyboardArrowRight,
                contentDescription = null,
                tint = StormdropColors.TextDim,
            )
        }
    }
}

@Composable
private fun CharacterShowcaseCard(
    character: com.example.data.model.CatalogItem,
    pet: com.example.data.model.CatalogItem?,
    profile: UserProfileDoc?,
    partyMembers: List<String>,
    cloudHealth: CloudHealthState,
    onCycleCharacter: (Int) -> Unit,
    onLeaveParty: () -> Unit,
) {
    Surface(
        shape = RoundedCornerShape(20.dp),
        color = StormdropColors.BgSurface,
        border = BorderStroke(1.dp, StormdropColors.Divider),
        modifier = Modifier.fillMaxWidth(),
    ) {
        Box(modifier = Modifier.fillMaxWidth()) {
            Image(
                painter = painterResource(id = R.drawable.img_hero_island),
                contentDescription = "Isle Verdant backdrop",
                contentScale = ContentScale.Crop,
                modifier = Modifier
                    .fillMaxWidth()
                    .height(230.dp),
                alpha = 0.36f,
            )
            Box(
                modifier = Modifier
                    .fillMaxWidth()
                    .height(230.dp)
                    .background(
                        Brush.verticalGradient(
                            listOf(
                                StormdropColors.BgDeep.copy(alpha = 0.2f),
                                StormdropColors.BgSurface.copy(alpha = 0.96f),
                            )
                        )
                    ),
            )

            Column(
                modifier = Modifier
                    .fillMaxWidth()
                    .padding(16.dp),
            ) {
                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.SpaceBetween,
                    verticalAlignment = Alignment.CenterVertically,
                ) {
                    CloudSyncBadge(health = cloudHealth)
                    Row(horizontalArrangement = Arrangement.spacedBy(6.dp)) {
                        RankBadgePill(points = profile?.brRankPoints ?: 0, prefix = "BR")
                        RankBadgePill(points = profile?.csRankPoints ?: 0, prefix = "CS")
                    }
                }

                Spacer(Modifier.height(14.dp))

                Row(
                    modifier = Modifier.fillMaxWidth(),
                    verticalAlignment = Alignment.CenterVertically,
                ) {
                    IconButton(
                        onClick = { onCycleCharacter(-1) },
                        modifier = Modifier
                            .size(48.dp)
                            .testTag("prev_character_button"),
                    ) {
                        Icon(
                            imageVector = Icons.AutoMirrored.Filled.KeyboardArrowLeft,
                            contentDescription = "Previous Character",
                            tint = StormdropColors.TextMain,
                        )
                    }

                    Column(
                        modifier = Modifier.weight(1f),
                        horizontalAlignment = Alignment.CenterHorizontally,
                    ) {
                        Box(
                            modifier = Modifier
                                .size(76.dp)
                                .clip(CircleShape)
                                .background(character.accentColor.copy(alpha = 0.22f))
                                .border(2.dp, character.accentColor, CircleShape),
                            contentAlignment = Alignment.Center,
                        ) {
                            Text(
                                text = character.name.take(2).uppercase(),
                                fontSize = 26.sp,
                                fontWeight = FontWeight.ExtraBold,
                                color = character.accentColor,
                            )
                        }
                        Spacer(Modifier.height(8.dp))
                        Row(
                            verticalAlignment = Alignment.CenterVertically,
                            horizontalArrangement = Arrangement.spacedBy(8.dp),
                        ) {
                            Text(
                                text = character.name,
                                style = MaterialTheme.typography.titleLarge,
                                color = StormdropColors.TextMain,
                            )
                            RarityChip(rarity = character.rarity)
                        }
                        Spacer(Modifier.height(4.dp))
                        Row(verticalAlignment = Alignment.CenterVertically) {
                            Icon(
                                imageVector = Icons.Filled.FlashOn,
                                contentDescription = "Character Skill",
                                tint = character.accentColor,
                                modifier = Modifier.size(14.dp),
                            )
                            Spacer(Modifier.width(4.dp))
                            Text(
                                text = character.skillName,
                                style = MaterialTheme.typography.labelMedium,
                                color = character.accentColor,
                            )
                            if (pet != null) {
                                Text(
                                    text = "  ·  Pet: ${pet.name}",
                                    style = MaterialTheme.typography.labelSmall,
                                    color = StormdropColors.AccentCyan,
                                )
                            }
                        }
                        Spacer(Modifier.height(4.dp))
                        Text(
                            text = character.description,
                            style = MaterialTheme.typography.bodySmall,
                            color = StormdropColors.TextDim,
                            maxLines = 2,
                            overflow = TextOverflow.Ellipsis,
                        )
                    }

                    IconButton(
                        onClick = { onCycleCharacter(1) },
                        modifier = Modifier
                            .size(48.dp)
                            .testTag("next_character_button"),
                    ) {
                        Icon(
                            imageVector = Icons.AutoMirrored.Filled.KeyboardArrowRight,
                            contentDescription = "Next Character",
                            tint = StormdropColors.TextMain,
                        )
                    }
                }

                Spacer(Modifier.height(12.dp))

                // Squad Party Bar
                Row(
                    modifier = Modifier.fillMaxWidth(),
                    verticalAlignment = Alignment.CenterVertically,
                    horizontalArrangement = Arrangement.SpaceBetween,
                ) {
                    LazyRow(horizontalArrangement = Arrangement.spacedBy(6.dp)) {
                        item {
                            PartyChip(name = profile?.nickname ?: "You", isLeader = true)
                        }
                        items(partyMembers) { mate ->
                            PartyChip(name = mate, isLeader = false)
                        }
                    }
                    if (partyMembers.isNotEmpty()) {
                        TextButton(onClick = onLeaveParty) {
                            Text("Leave Squad", color = StormdropColors.Crimson, fontSize = 12.sp)
                        }
                    }
                }
            }
        }
    }
}

@Composable
private fun PartyChip(name: String, isLeader: Boolean) {
    Surface(
        color = StormdropColors.BgElevated,
        shape = RoundedCornerShape(10.dp),
        border = BorderStroke(
            1.dp,
            if (isLeader) StormdropColors.AccentOrange.copy(alpha = 0.55f) else StormdropColors.Divider,
        ),
    ) {
        Row(
            modifier = Modifier.padding(horizontal = 8.dp, vertical = 4.dp),
            verticalAlignment = Alignment.CenterVertically,
        ) {
            if (isLeader) {
                Icon(
                    imageVector = Icons.Filled.Star,
                    contentDescription = "Squad Leader",
                    tint = StormdropColors.Gold,
                    modifier = Modifier.size(12.dp),
                )
                Spacer(Modifier.width(4.dp))
            }
            Text(
                text = name,
                style = MaterialTheme.typography.labelSmall,
                color = StormdropColors.TextMain,
            )
        }
    }
}

@Composable
private fun DeployPanel(
    selectedMode: GameMode,
    isRanked: Boolean,
    level: Int,
    onToggleRanked: () -> Unit,
    onOpenModeSelect: () -> Unit,
    onDeployMatch: () -> Unit,
) {
    Surface(
        shape = RoundedCornerShape(20.dp),
        color = StormdropColors.BgSurface,
        border = BorderStroke(1.dp, StormdropColors.Divider),
        modifier = Modifier.fillMaxWidth(),
    ) {
        Column(
            modifier = Modifier.padding(16.dp),
            verticalArrangement = Arrangement.spacedBy(12.dp),
        ) {
            Row(
                modifier = Modifier.fillMaxWidth(),
                verticalAlignment = Alignment.CenterVertically,
                horizontalArrangement = Arrangement.SpaceBetween,
            ) {
                Column(modifier = Modifier.weight(1f)) {
                    Text(
                        text = "SELECTED MODE",
                        style = MaterialTheme.typography.labelSmall,
                        color = StormdropColors.TextMuted,
                    )
                    Spacer(Modifier.height(2.dp))
                    Text(
                        text = selectedMode.displayName,
                        style = MaterialTheme.typography.titleLarge,
                        color = StormdropColors.TextMain,
                    )
                    Text(
                        text = "${selectedMode.maxPlayers} players · Squad ${selectedMode.teamSize}",
                        style = MaterialTheme.typography.bodySmall,
                        color = StormdropColors.AccentCyan,
                    )
                }
                OutlinedButton(
                    onClick = onOpenModeSelect,
                    border = BorderStroke(1.dp, StormdropColors.AccentCyan.copy(alpha = 0.6f)),
                    shape = RoundedCornerShape(12.dp),
                    modifier = Modifier
                        .height(48.dp)
                        .testTag("change_mode_button"),
                ) {
                    Text("Change", color = StormdropColors.AccentCyan)
                }
            }

            Surface(
                color = StormdropColors.BgElevated,
                shape = RoundedCornerShape(12.dp),
                modifier = Modifier.fillMaxWidth(),
            ) {
                Row(
                    modifier = Modifier
                        .fillMaxWidth()
                        .padding(horizontal = 12.dp, vertical = 6.dp),
                    verticalAlignment = Alignment.CenterVertically,
                    horizontalArrangement = Arrangement.SpaceBetween,
                ) {
                    Row(verticalAlignment = Alignment.CenterVertically) {
                        Icon(
                            imageVector = Icons.Filled.Shield,
                            contentDescription = "Ranked Mode",
                            tint = if (isRanked) StormdropColors.Gold else StormdropColors.TextDim,
                            modifier = Modifier.size(18.dp),
                        )
                        Spacer(Modifier.width(8.dp))
                        Column {
                            Text(
                                text = "Ranked Match",
                                style = MaterialTheme.typography.titleSmall,
                                color = StormdropColors.TextMain,
                            )
                            Text(
                                text = if (level < 6) "Unlocks at Lv 6 (Now Lv $level)" else "Earn RP & Climb Tiers",
                                style = MaterialTheme.typography.labelSmall,
                                color = StormdropColors.TextDim,
                            )
                        }
                    }
                    Switch(
                        checked = isRanked,
                        onCheckedChange = { onToggleRanked() },
                        colors = SwitchDefaults.colors(
                            checkedThumbColor = StormdropColors.BgDeep,
                            checkedTrackColor = StormdropColors.Gold,
                        ),
                        modifier = Modifier.testTag("ranked_toggle"),
                    )
                }
            }

            Button(
                onClick = onDeployMatch,
                colors = ButtonDefaults.buttonColors(
                    containerColor = StormdropColors.AccentOrange,
                    contentColor = Color(0xFF140B00),
                ),
                shape = RoundedCornerShape(16.dp),
                modifier = Modifier
                    .fillMaxWidth()
                    .height(58.dp)
                    .testTag("drop_in_button"),
            ) {
                Icon(
                    imageVector = Icons.Filled.PlayArrow,
                    contentDescription = "Drop In",
                    modifier = Modifier.size(26.dp),
                )
                Spacer(Modifier.width(8.dp))
                Text(
                    text = if (selectedMode == GameMode.TRAINING) "ENTER TRAINING" else "DROP IN NOW",
                    style = MaterialTheme.typography.titleMedium.copy(
                        fontWeight = FontWeight.ExtraBold,
                        letterSpacing = 1.sp,
                    ),
                )
            }
        }
    }
}

@Composable
private fun QuickNavGrid(
    onNavigateStore: () -> Unit,
    onNavigateVault: () -> Unit,
    onNavigatePass: () -> Unit,
    onNavigateMissions: () -> Unit,
    onNavigateSocial: () -> Unit,
    onNavigateLeaderboard: () -> Unit,
) {
    val entries = listOf(
        Triple("Store", Icons.Filled.ShoppingBag, StormdropColors.AccentOrange to onNavigateStore),
        Triple("Vault", Icons.Filled.Backpack, StormdropColors.AccentCyan to onNavigateVault),
        Triple("Storm Pass", Icons.Filled.EmojiEvents, StormdropColors.Gold to onNavigatePass),
        Triple("Missions", Icons.Filled.TaskAlt, StormdropColors.Emerald to onNavigateMissions),
        Triple("Social", Icons.Filled.Group, StormdropColors.VigorPurple to onNavigateSocial),
        Triple("Ranks", Icons.Filled.Leaderboard, StormdropColors.Diamond to onNavigateLeaderboard),
    )

    Column(verticalArrangement = Arrangement.spacedBy(10.dp)) {
        entries.chunked(3).forEach { rowItems ->
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.spacedBy(10.dp),
            ) {
                rowItems.forEach { (label, icon, pair) ->
                    val (tint, action) = pair
                    LobbyNavTile(
                        label = label,
                        icon = icon,
                        tint = tint,
                        onClick = action,
                        modifier = Modifier.weight(1f),
                    )
                }
            }
        }
    }
}

@Composable
private fun LobbyNavTile(
    label: String,
    icon: ImageVector,
    tint: Color,
    onClick: () -> Unit,
    modifier: Modifier = Modifier,
) {
    Surface(
        color = StormdropColors.BgSurface,
        shape = RoundedCornerShape(16.dp),
        border = BorderStroke(1.dp, tint.copy(alpha = 0.28f)),
        modifier = modifier
            .height(82.dp)
            .clip(RoundedCornerShape(16.dp))
            .clickable(onClick = onClick)
            .testTag("nav_${label.lowercase().replace(" ", "_")}"),
    ) {
        Column(
            modifier = Modifier
                .fillMaxSize()
                .padding(10.dp),
            horizontalAlignment = Alignment.CenterHorizontally,
            verticalArrangement = Arrangement.Center,
        ) {
            Icon(
                imageVector = icon,
                contentDescription = label,
                tint = tint,
                modifier = Modifier.size(26.dp),
            )
            Spacer(Modifier.height(6.dp))
            Text(
                text = label,
                style = MaterialTheme.typography.labelLarge,
                color = StormdropColors.TextMain,
                maxLines = 1,
                overflow = TextOverflow.Ellipsis,
            )
        }
    }
}
