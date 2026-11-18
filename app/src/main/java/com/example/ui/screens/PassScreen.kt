package com.example.ui.screens

import androidx.activity.compose.BackHandler
import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.Image
import androidx.compose.foundation.background
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
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.layout.widthIn
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.LinearProgressIndicator
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.remember
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.platform.testTag
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import com.example.R
import com.example.data.model.GameCatalog
import com.example.data.remote.PassProgressDoc
import com.example.ui.components.StormdropTopBar
import com.example.ui.theme.JetBrainsMonoFamily
import com.example.ui.theme.StormdropColors

@Composable
fun PassScreen(
    gold: Int,
    diamonds: Int,
    passProgress: PassProgressDoc?,
    onUnlockPremium: () -> Unit,
    onClaimReward: (Int, String) -> Unit,
    onBack: () -> Unit,
) {
    BackHandler(onBack = onBack)

    val passXp = passProgress?.passXp ?: 0
    val currentLevel = (passXp / GameCatalog.passXpPerLevel).coerceAtMost(GameCatalog.passMaxLevel)
    val levelProgress = (passXp % GameCatalog.passXpPerLevel) / GameCatalog.passXpPerLevel.toFloat()
    val premiumUnlocked = passProgress?.premiumUnlocked == true
    val claimedSet = remember(passProgress?.claimedKeys) {
        (passProgress?.claimedKeys ?: "").split(",").filter { it.isNotBlank() }.toSet()
    }
    val levels = remember { (1..GameCatalog.passMaxLevel).toList() }

    Column(
        modifier = Modifier
            .fillMaxSize()
            .background(StormdropColors.BgDeep),
    ) {
        StormdropTopBar(
            title = "Storm Pass",
            gold = gold,
            diamonds = diamonds,
            onBack = onBack,
        )

        LazyColumn(
            modifier = Modifier
                .fillMaxSize()
                .widthIn(max = 780.dp)
                .align(Alignment.CenterHorizontally),
            contentPadding = PaddingValues(16.dp),
            verticalArrangement = Arrangement.spacedBy(12.dp),
        ) {
            item {
                Surface(
                    shape = RoundedCornerShape(20.dp),
                    color = StormdropColors.BgSurface,
                    border = BorderStroke(1.dp, StormdropColors.Gold.copy(alpha = 0.45f)),
                ) {
                    Box(modifier = Modifier.fillMaxWidth()) {
                        Image(
                            painter = painterResource(id = R.drawable.img_storm_pass),
                            contentDescription = "Storm Pass Season 1",
                            contentScale = ContentScale.Crop,
                            modifier = Modifier
                                .fillMaxWidth()
                                .height(160.dp),
                            alpha = 0.45f,
                        )
                        Box(
                            modifier = Modifier
                                .fillMaxWidth()
                                .height(160.dp)
                                .background(
                                    Brush.verticalGradient(
                                        listOf(Color.Transparent, StormdropColors.BgSurface)
                                    )
                                ),
                        )
                        Column(
                            modifier = Modifier
                                .fillMaxWidth()
                                .padding(16.dp),
                            verticalArrangement = Arrangement.spacedBy(8.dp),
                        ) {
                            Text(
                                text = GameCatalog.passSeasonName,
                                style = MaterialTheme.typography.titleLarge,
                                color = StormdropColors.Gold,
                            )
                            Text(
                                text = "Tier $currentLevel / ${GameCatalog.passMaxLevel}  ·  $passXp XP",
                                style = MaterialTheme.typography.bodyMedium.copy(fontFamily = JetBrainsMonoFamily),
                                color = StormdropColors.TextMain,
                            )
                            LinearProgressIndicator(
                                progress = { levelProgress.coerceIn(0f, 1f) },
                                modifier = Modifier
                                    .fillMaxWidth()
                                    .height(8.dp)
                                    .clip(RoundedCornerShape(4.dp)),
                                color = StormdropColors.Gold,
                                trackColor = StormdropColors.BgHighlight,
                            )
                            if (!premiumUnlocked) {
                                Button(
                                    onClick = onUnlockPremium,
                                    colors = ButtonDefaults.buttonColors(
                                        containerColor = StormdropColors.Gold,
                                        contentColor = Color(0xFF1A1200),
                                    ),
                                    shape = RoundedCornerShape(12.dp),
                                    modifier = Modifier
                                        .fillMaxWidth()
                                        .height(48.dp)
                                        .testTag("unlock_premium_pass_button"),
                                ) {
                                    Text(
                                        "Unlock Premium Track · ${GameCatalog.passPremiumPriceDiamonds} Diamonds",
                                        fontWeight = FontWeight.Bold,
                                    )
                                }
                            } else {
                                Text(
                                    text = "PREMIUM TRACK ACTIVE",
                                    style = MaterialTheme.typography.labelLarge,
                                    color = StormdropColors.Emerald,
                                )
                            }
                        }
                    }
                }
            }

            items(levels, key = { it }) { lvl ->
                val freeDef = GameCatalog.passRewards.first { it.level == lvl && it.track == "free" }
                val premDef = GameCatalog.passRewards.first { it.level == lvl && it.track == "premium" }
                val freeName = GameCatalog.itemById(freeDef.itemId)?.name ?: freeDef.itemId
                val premName = GameCatalog.itemById(premDef.itemId)?.name ?: premDef.itemId

                val unlocked = currentLevel >= lvl
                val freeClaimed = "$lvl:free" in claimedSet
                val premClaimed = "$lvl:premium" in claimedSet

                Surface(
                    color = StormdropColors.BgSurface,
                    shape = RoundedCornerShape(14.dp),
                    border = BorderStroke(
                        1.dp,
                        if (unlocked) StormdropColors.AccentOrange.copy(alpha = 0.45f) else StormdropColors.Divider,
                    ),
                ) {
                    Row(
                        modifier = Modifier
                            .fillMaxWidth()
                            .padding(12.dp),
                        verticalAlignment = Alignment.CenterVertically,
                        horizontalArrangement = Arrangement.spacedBy(10.dp),
                    ) {
                        Text(
                            text = "Lv $lvl",
                            style = MaterialTheme.typography.titleMedium.copy(fontFamily = JetBrainsMonoFamily),
                            color = if (unlocked) StormdropColors.Gold else StormdropColors.TextMuted,
                            modifier = Modifier.width(50.dp),
                        )
                        Column(modifier = Modifier.weight(1f)) {
                            Text("Free: $freeName", style = MaterialTheme.typography.bodySmall, color = StormdropColors.TextMain)
                            Text("Prem: $premName", style = MaterialTheme.typography.bodySmall, color = StormdropColors.Gold)
                        }
                        Column(verticalArrangement = Arrangement.spacedBy(6.dp)) {
                            Button(
                                onClick = { onClaimReward(lvl, "free") },
                                enabled = unlocked && !freeClaimed,
                                shape = RoundedCornerShape(8.dp),
                                modifier = Modifier.height(48.dp),
                            ) {
                                Text(if (freeClaimed) "Claimed" else "Free")
                            }
                            Button(
                                onClick = { onClaimReward(lvl, "premium") },
                                enabled = unlocked && premiumUnlocked && !premClaimed,
                                colors = ButtonDefaults.buttonColors(
                                    containerColor = StormdropColors.Gold,
                                    contentColor = Color(0xFF140B00),
                                ),
                                shape = RoundedCornerShape(8.dp),
                                modifier = Modifier.height(48.dp),
                            ) {
                                Text(if (premClaimed) "Claimed" else "Prem")
                            }
                        }
                    }
                }
            }
        }
    }
}
