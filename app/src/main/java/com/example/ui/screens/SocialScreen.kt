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
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.layout.widthIn
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.Send
import androidx.compose.material.icons.filled.Add
import androidx.compose.material.icons.filled.GroupAdd
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Surface
import androidx.compose.material3.Tab
import androidx.compose.material3.TabRow
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.testTag
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import com.example.data.remote.FriendDoc
import com.example.data.remote.GuildDoc
import com.example.data.remote.GuildMessageDoc
import com.example.data.remote.UserProfileDoc
import com.example.ui.components.RankBadgePill
import com.example.ui.components.StormdropTopBar
import com.example.ui.theme.JetBrainsMonoFamily
import com.example.ui.theme.StormdropColors

@Composable
fun SocialScreen(
    profile: UserProfileDoc?,
    friends: List<FriendDoc>,
    guilds: List<GuildDoc>,
    guildChat: List<GuildMessageDoc>,
    onAddFriend: (String) -> Unit,
    onRespondFriend: (String, Boolean) -> Unit,
    onInviteParty: (String) -> Unit,
    onCreateGuild: (String, String, String) -> Unit,
    onJoinGuild: (String) -> Unit,
    onLeaveGuild: () -> Unit,
    onSendGuildMessage: (String) -> Unit,
    onBack: () -> Unit,
) {
    BackHandler(onBack = onBack)

    var selectedTab by remember { mutableIntStateOf(0) }
    var friendTagInput by remember { mutableStateOf("") }
    var chatInput by remember { mutableStateOf("") }
    var showCreateGuild by remember { mutableStateOf(false) }

    val currentGuild = remember(profile?.guildId, guilds) {
        profile?.guildId?.let { gid -> guilds.firstOrNull { it.id == gid } }
    }

    Column(
        modifier = Modifier
            .fillMaxSize()
            .background(StormdropColors.BgDeep),
    ) {
        StormdropTopBar(
            title = "Social & Guilds",
            gold = profile?.gold ?: 0,
            diamonds = profile?.diamonds ?: 0,
            onBack = onBack,
        )

        TabRow(
            selectedTabIndex = selectedTab,
            containerColor = StormdropColors.BgSurface,
            contentColor = StormdropColors.VigorPurple,
        ) {
            listOf("Friends & Squad", "Guilds & Chat").forEachIndexed { idx, label ->
                Tab(
                    selected = selectedTab == idx,
                    onClick = { selectedTab = idx },
                    text = {
                        Text(
                            label,
                            color = if (selectedTab == idx) StormdropColors.VigorPurple else StormdropColors.TextDim,
                        )
                    },
                    modifier = Modifier.testTag("social_tab_$idx"),
                )
            }
        }

        if (selectedTab == 0) {
            LazyColumn(
                modifier = Modifier
                    .fillMaxSize()
                    .widthIn(max = 760.dp)
                    .align(Alignment.CenterHorizontally),
                contentPadding = PaddingValues(16.dp),
                verticalArrangement = Arrangement.spacedBy(12.dp),
            ) {
                item {
                    Surface(
                        color = StormdropColors.BgSurface,
                        shape = RoundedCornerShape(16.dp),
                        border = BorderStroke(1.dp, StormdropColors.Divider),
                    ) {
                        Column(
                            modifier = Modifier.padding(14.dp),
                            verticalArrangement = Arrangement.spacedBy(8.dp),
                        ) {
                            Text(
                                text = "Your Player ID: ${profile?.playerTag ?: "—"}",
                                style = MaterialTheme.typography.labelLarge.copy(fontFamily = JetBrainsMonoFamily),
                                color = StormdropColors.AccentCyan,
                            )
                            Row(
                                verticalAlignment = Alignment.CenterVertically,
                                horizontalArrangement = Arrangement.spacedBy(8.dp),
                            ) {
                                OutlinedTextField(
                                    value = friendTagInput,
                                    onValueChange = { friendTagInput = it },
                                    label = { Text("Add by Player ID") },
                                    singleLine = true,
                                    modifier = Modifier
                                        .weight(1f)
                                        .testTag("friend_id_input"),
                                )
                                Button(
                                    onClick = {
                                        onAddFriend(friendTagInput)
                                        friendTagInput = ""
                                    },
                                    shape = RoundedCornerShape(12.dp),
                                    modifier = Modifier
                                        .height(54.dp)
                                        .testTag("add_friend_button"),
                                ) {
                                    Icon(Icons.Filled.GroupAdd, contentDescription = "Add Friend")
                                }
                            }
                        }
                    }
                }

                items(friends, key = { it.playerTag }) { f ->
                    Surface(
                        color = StormdropColors.BgSurface,
                        shape = RoundedCornerShape(14.dp),
                        border = BorderStroke(1.dp, StormdropColors.Divider),
                    ) {
                        Row(
                            modifier = Modifier
                                .fillMaxWidth()
                                .padding(12.dp),
                            verticalAlignment = Alignment.CenterVertically,
                        ) {
                            Column(modifier = Modifier.weight(1f)) {
                                Row(
                                    verticalAlignment = Alignment.CenterVertically,
                                    horizontalArrangement = Arrangement.spacedBy(8.dp),
                                ) {
                                    Text(f.nickname, style = MaterialTheme.typography.titleSmall, color = StormdropColors.TextMain)
                                    Text(
                                        text = if (f.online) "Online" else "Offline",
                                        style = MaterialTheme.typography.labelSmall,
                                        color = if (f.online) StormdropColors.Emerald else StormdropColors.TextMuted,
                                    )
                                }
                                Spacer(Modifier.height(4.dp))
                                RankBadgePill(points = f.brRankPoints, prefix = "Lv ${f.level} · BR")
                            }
                            if (f.status == "incoming") {
                                Row(horizontalArrangement = Arrangement.spacedBy(6.dp)) {
                                    Button(
                                        onClick = { onRespondFriend(f.playerTag, true) },
                                        modifier = Modifier.height(48.dp),
                                    ) {
                                        Text("Accept")
                                    }
                                    OutlinedButton(
                                        onClick = { onRespondFriend(f.playerTag, false) },
                                        modifier = Modifier.height(48.dp),
                                    ) {
                                        Text("Decline", color = StormdropColors.Crimson)
                                    }
                                }
                            } else {
                                Button(
                                    onClick = { onInviteParty(f.nickname) },
                                    colors = ButtonDefaults.buttonColors(
                                        containerColor = StormdropColors.AccentCyan,
                                        contentColor = Color(0xFF001A2C),
                                    ),
                                    shape = RoundedCornerShape(10.dp),
                                    modifier = Modifier.height(48.dp),
                                ) {
                                    Text("Invite", fontWeight = FontWeight.Bold)
                                }
                            }
                        }
                    }
                }
            }
        } else {
            Column(
                modifier = Modifier
                    .fillMaxSize()
                    .widthIn(max = 760.dp)
                    .align(Alignment.CenterHorizontally),
            ) {
                if (currentGuild != null) {
                    Surface(
                        color = StormdropColors.BgSurface,
                        shape = RoundedCornerShape(16.dp),
                        border = BorderStroke(1.dp, StormdropColors.VigorPurple.copy(alpha = 0.55f)),
                        modifier = Modifier.padding(14.dp),
                    ) {
                        Row(
                            modifier = Modifier
                                .fillMaxWidth()
                                .padding(14.dp),
                            verticalAlignment = Alignment.CenterVertically,
                        ) {
                            Column(modifier = Modifier.weight(1f)) {
                                Text(
                                    text = "[${currentGuild.tag}] ${currentGuild.name}",
                                    style = MaterialTheme.typography.titleMedium,
                                    color = StormdropColors.VigorPurple,
                                )
                                Text(
                                    text = "Lv ${currentGuild.level} · Glory ${currentGuild.glory} · ${currentGuild.memberCount}/${currentGuild.maxMembers} Members",
                                    style = MaterialTheme.typography.bodySmall,
                                    color = StormdropColors.TextDim,
                                )
                            }
                            OutlinedButton(
                                onClick = onLeaveGuild,
                                border = BorderStroke(1.dp, StormdropColors.Crimson.copy(alpha = 0.6f)),
                                modifier = Modifier.height(48.dp),
                            ) {
                                Text("Leave", color = StormdropColors.Crimson)
                            }
                        }
                    }

                    LazyColumn(
                        modifier = Modifier
                            .weight(1f)
                            .fillMaxWidth(),
                        contentPadding = PaddingValues(horizontal = 14.dp, vertical = 8.dp),
                        verticalArrangement = Arrangement.spacedBy(8.dp),
                    ) {
                        items(guildChat, key = { it.id }) { msg ->
                            Surface(
                                color = StormdropColors.BgSurface,
                                shape = RoundedCornerShape(12.dp),
                            ) {
                                Column(
                                    modifier = Modifier
                                        .fillMaxWidth()
                                        .padding(10.dp),
                                ) {
                                    Text(
                                        text = msg.senderName,
                                        style = MaterialTheme.typography.labelMedium,
                                        color = StormdropColors.AccentCyan,
                                    )
                                    Text(
                                        text = msg.body,
                                        style = MaterialTheme.typography.bodyMedium,
                                        color = StormdropColors.TextMain,
                                    )
                                }
                            }
                        }
                    }

                    Row(
                        modifier = Modifier
                            .fillMaxWidth()
                            .background(StormdropColors.BgSurface)
                            .padding(12.dp),
                        verticalAlignment = Alignment.CenterVertically,
                        horizontalArrangement = Arrangement.spacedBy(8.dp),
                    ) {
                        OutlinedTextField(
                            value = chatInput,
                            onValueChange = { chatInput = it },
                            placeholder = { Text("Message guild...") },
                            singleLine = true,
                            modifier = Modifier
                                .weight(1f)
                                .testTag("guild_chat_input"),
                        )
                        IconButton(
                            onClick = {
                                onSendGuildMessage(chatInput)
                                chatInput = ""
                            },
                            modifier = Modifier
                                .size(48.dp)
                                .testTag("send_guild_chat_button"),
                        ) {
                            Icon(
                                imageVector = Icons.AutoMirrored.Filled.Send,
                                contentDescription = "Send message",
                                tint = StormdropColors.AccentOrange,
                            )
                        }
                    }
                } else {
                    LazyColumn(
                        modifier = Modifier.fillMaxSize(),
                        contentPadding = PaddingValues(16.dp),
                        verticalArrangement = Arrangement.spacedBy(12.dp),
                    ) {
                        item {
                            Button(
                                onClick = { showCreateGuild = true },
                                colors = ButtonDefaults.buttonColors(
                                    containerColor = StormdropColors.VigorPurple,
                                    contentColor = Color.White,
                                ),
                                shape = RoundedCornerShape(14.dp),
                                modifier = Modifier
                                    .fillMaxWidth()
                                    .height(52.dp)
                                    .testTag("open_create_guild_button"),
                            ) {
                                Icon(Icons.Filled.Add, contentDescription = null, modifier = Modifier.size(18.dp))
                                Spacer(Modifier.width(6.dp))
                                Text("Create New Guild · 2,000 Gold", fontWeight = FontWeight.Bold)
                            }
                        }

                        if (guilds.isEmpty()) {
                            item {
                                Text(
                                    text = "No public guilds found yet. Found the first guild above!",
                                    style = MaterialTheme.typography.bodyMedium,
                                    color = StormdropColors.TextDim,
                                )
                            }
                        } else {
                            items(guilds, key = { it.id }) { g ->
                                Surface(
                                    color = StormdropColors.BgSurface,
                                    shape = RoundedCornerShape(16.dp),
                                    border = BorderStroke(1.dp, StormdropColors.Divider),
                                ) {
                                    Row(
                                        modifier = Modifier
                                            .fillMaxWidth()
                                            .padding(14.dp),
                                        verticalAlignment = Alignment.CenterVertically,
                                    ) {
                                        Column(modifier = Modifier.weight(1f)) {
                                            Text(
                                                text = "[${g.tag}] ${g.name}",
                                                style = MaterialTheme.typography.titleMedium,
                                                color = StormdropColors.TextMain,
                                            )
                                            Text(
                                                text = g.description,
                                                style = MaterialTheme.typography.bodySmall,
                                                color = StormdropColors.TextDim,
                                            )
                                            Text(
                                                text = "Lv ${g.level} · ${g.memberCount}/${g.maxMembers} Members · Glory ${g.glory}",
                                                style = MaterialTheme.typography.labelSmall,
                                                color = StormdropColors.AccentCyan,
                                            )
                                        }
                                        Button(
                                            onClick = { onJoinGuild(g.id) },
                                            shape = RoundedCornerShape(10.dp),
                                            modifier = Modifier.height(48.dp),
                                        ) {
                                            Text("Join")
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

    if (showCreateGuild) {
        var name by remember { mutableStateOf("") }
        var tag by remember { mutableStateOf("") }
        var desc by remember { mutableStateOf("") }
        AlertDialog(
            onDismissRequest = { showCreateGuild = false },
            containerColor = StormdropColors.BgSurface,
            title = { Text("Create Guild (2,000 Gold)", color = StormdropColors.TextMain) },
            text = {
                Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                    OutlinedTextField(value = name, onValueChange = { name = it }, label = { Text("Guild Name (3-20 chars)") }, singleLine = true)
                    OutlinedTextField(value = tag, onValueChange = { tag = it.uppercase() }, label = { Text("Tag (2-5 chars, e.g. APEX)") }, singleLine = true)
                    OutlinedTextField(value = desc, onValueChange = { desc = it }, label = { Text("Description") }, maxLines = 2)
                }
            },
            confirmButton = {
                Button(
                    onClick = {
                        onCreateGuild(name, tag, desc)
                        showCreateGuild = false
                    },
                ) {
                    Text("Create")
                }
            },
            dismissButton = {
                TextButton(onClick = { showCreateGuild = false }) {
                    Text("Cancel", color = StormdropColors.TextDim)
                }
            },
        )
    }
}
