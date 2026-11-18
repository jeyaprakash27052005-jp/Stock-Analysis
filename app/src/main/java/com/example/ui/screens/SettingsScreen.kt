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
import androidx.compose.foundation.lazy.LazyRow
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.FilterChip
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Slider
import androidx.compose.material3.Surface
import androidx.compose.material3.Switch
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.testTag
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import com.example.data.CloudHealthState
import com.example.data.local.PlayerSettingsState
import com.example.data.model.GameCatalog
import com.example.data.model.GraphicsPreset
import com.example.data.remote.UserProfileDoc
import com.example.ui.components.CloudSyncBadge
import com.example.ui.components.StormdropTopBar
import com.example.ui.theme.StormdropColors

@Composable
fun SettingsScreen(
    profile: UserProfileDoc?,
    settings: PlayerSettingsState,
    cloudHealth: CloudHealthState,
    onRenameNickname: (String) -> Unit,
    onSetRegion: (String) -> Unit,
    onUpdateSettings: ((PlayerSettingsState) -> PlayerSettingsState) -> Unit,
    onSignOut: () -> Unit,
    onDeleteAccount: () -> Unit,
    onBack: () -> Unit,
) {
    BackHandler(onBack = onBack)

    var nicknameInput by remember(profile?.nickname) { mutableStateOf(profile?.nickname ?: "") }
    var showDeleteConfirm by remember { mutableStateOf(false) }

    Column(
        modifier = Modifier
            .fillMaxSize()
            .background(StormdropColors.BgDeep),
    ) {
        StormdropTopBar(
            title = "Settings",
            gold = profile?.gold ?: 0,
            diamonds = profile?.diamonds ?: 0,
            onBack = onBack,
        )

        LazyColumn(
            modifier = Modifier
                .fillMaxSize()
                .widthIn(max = 760.dp)
                .align(Alignment.CenterHorizontally),
            contentPadding = PaddingValues(16.dp),
            verticalArrangement = Arrangement.spacedBy(14.dp),
        ) {
            item {
                Surface(
                    color = StormdropColors.BgSurface,
                    shape = RoundedCornerShape(16.dp),
                    border = BorderStroke(1.dp, StormdropColors.Divider),
                ) {
                    Column(
                        modifier = Modifier.padding(16.dp),
                        verticalArrangement = Arrangement.spacedBy(12.dp),
                    ) {
                        Row(
                            modifier = Modifier.fillMaxWidth(),
                            horizontalArrangement = Arrangement.SpaceBetween,
                            verticalAlignment = Alignment.CenterVertically,
                        ) {
                            Text("Account & Region", style = MaterialTheme.typography.titleMedium, color = StormdropColors.TextMain)
                            CloudSyncBadge(cloudHealth)
                        }

                        Row(
                            horizontalArrangement = Arrangement.spacedBy(8.dp),
                            verticalAlignment = Alignment.CenterVertically,
                        ) {
                            OutlinedTextField(
                                value = nicknameInput,
                                onValueChange = { nicknameInput = it },
                                label = { Text("Nickname (3-16 chars)") },
                                singleLine = true,
                                modifier = Modifier
                                    .weight(1f)
                                    .testTag("rename_nickname_input"),
                            )
                            Button(
                                onClick = { onRenameNickname(nicknameInput) },
                                shape = RoundedCornerShape(12.dp),
                                modifier = Modifier
                                    .height(54.dp)
                                    .testTag("rename_nickname_button"),
                            ) {
                                Text("Save")
                            }
                        }

                        Text("Server Region", style = MaterialTheme.typography.labelMedium, color = StormdropColors.TextDim)
                        LazyRow(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                            items(GameCatalog.regions) { reg ->
                                FilterChip(
                                    selected = profile?.region == reg,
                                    onClick = { onSetRegion(reg) },
                                    label = { Text(reg) },
                                )
                            }
                        }

                        Row(horizontalArrangement = Arrangement.spacedBy(10.dp)) {
                            OutlinedButton(
                                onClick = onSignOut,
                                border = BorderStroke(1.dp, StormdropColors.AccentCyan.copy(alpha = 0.6f)),
                                modifier = Modifier
                                    .weight(1f)
                                    .height(48.dp)
                                    .testTag("sign_out_button"),
                            ) {
                                Text("Sign Out", color = StormdropColors.AccentCyan)
                            }
                            Button(
                                onClick = { showDeleteConfirm = true },
                                colors = ButtonDefaults.buttonColors(
                                    containerColor = StormdropColors.Crimson,
                                    contentColor = Color.White,
                                ),
                                modifier = Modifier
                                    .weight(1f)
                                    .height(48.dp)
                                    .testTag("delete_account_button"),
                            ) {
                                Text("Delete Account", fontWeight = FontWeight.Bold)
                            }
                        }
                    }
                }
            }

            item {
                Surface(
                    color = StormdropColors.BgSurface,
                    shape = RoundedCornerShape(16.dp),
                    border = BorderStroke(1.dp, StormdropColors.Divider),
                ) {
                    Column(
                        modifier = Modifier.padding(16.dp),
                        verticalArrangement = Arrangement.spacedBy(10.dp),
                    ) {
                        Text("Graphics & Performance", style = MaterialTheme.typography.titleMedium, color = StormdropColors.TextMain)
                        Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                            GraphicsPreset.entries.forEach { preset ->
                                FilterChip(
                                    selected = settings.graphics == preset,
                                    onClick = { onUpdateSettings { it.copy(graphics = preset) } },
                                    label = { Text(preset.label) },
                                )
                            }
                        }
                        SettingSwitchRow(
                            title = "High Frame Rate (60 FPS)",
                            checked = settings.highFrameRate,
                            onToggle = { v -> onUpdateSettings { it.copy(highFrameRate = v) } },
                        )
                    }
                }
            }

            item {
                Surface(
                    color = StormdropColors.BgSurface,
                    shape = RoundedCornerShape(16.dp),
                    border = BorderStroke(1.dp, StormdropColors.Divider),
                ) {
                    Column(
                        modifier = Modifier.padding(16.dp),
                        verticalArrangement = Arrangement.spacedBy(10.dp),
                    ) {
                        Text("Controls & Audio", style = MaterialTheme.typography.titleMedium, color = StormdropColors.TextMain)
                        Text("Look Sensitivity: ${"%.2f".format(settings.lookSensitivity)}x", style = MaterialTheme.typography.bodySmall, color = StormdropColors.TextDim)
                        Slider(
                            value = settings.lookSensitivity,
                            onValueChange = { v -> onUpdateSettings { it.copy(lookSensitivity = v) } },
                            valueRange = 0.4f..2.0f,
                        )
                        Text("Master Volume: ${(settings.masterVolume * 100).toInt()}%", style = MaterialTheme.typography.bodySmall, color = StormdropColors.TextDim)
                        Slider(
                            value = settings.masterVolume,
                            onValueChange = { v -> onUpdateSettings { it.copy(masterVolume = v) } },
                            valueRange = 0f..1f,
                        )
                        SettingSwitchRow(
                            title = "Auto-Fire When Locked",
                            checked = settings.autoFire,
                            onToggle = { v -> onUpdateSettings { it.copy(autoFire = v) } },
                        )
                        SettingSwitchRow(
                            title = "Aim Assist",
                            checked = settings.aimAssist,
                            onToggle = { v -> onUpdateSettings { it.copy(aimAssist = v) } },
                        )
                        SettingSwitchRow(
                            title = "Squad Voice Chat",
                            checked = settings.voiceChat,
                            onToggle = { v -> onUpdateSettings { it.copy(voiceChat = v) } },
                        )
                    }
                }
            }
        }
    }

    if (showDeleteConfirm) {
        AlertDialog(
            onDismissRequest = { showDeleteConfirm = false },
            containerColor = StormdropColors.BgSurface,
            title = { Text("Delete Firebase Account Data?", color = StormdropColors.Crimson) },
            text = {
                Text(
                    "This permanently deletes your player profile, rank points, Vault items, Storm Pass, friends, and match history from Firebase.",
                    color = StormdropColors.TextDim,
                )
            },
            confirmButton = {
                Button(
                    onClick = {
                        showDeleteConfirm = false
                        onDeleteAccount()
                    },
                    colors = ButtonDefaults.buttonColors(containerColor = StormdropColors.Crimson),
                ) {
                    Text("Delete Permanently", color = Color.White)
                }
            },
            dismissButton = {
                TextButton(onClick = { showDeleteConfirm = false }) {
                    Text("Cancel", color = StormdropColors.TextMain)
                }
            },
        )
    }
}

@Composable
private fun SettingSwitchRow(
    title: String,
    checked: Boolean,
    onToggle: (Boolean) -> Unit,
) {
    Row(
        modifier = Modifier.fillMaxWidth(),
        verticalAlignment = Alignment.CenterVertically,
        horizontalArrangement = Arrangement.SpaceBetween,
    ) {
        Text(title, style = MaterialTheme.typography.bodyMedium, color = StormdropColors.TextMain)
        Switch(checked = checked, onCheckedChange = onToggle)
    }
}
