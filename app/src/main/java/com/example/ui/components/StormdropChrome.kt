package com.example.ui.components

import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.filled.CloudDone
import androidx.compose.material.icons.filled.CloudSync
import androidx.compose.material.icons.filled.Diamond
import androidx.compose.material.icons.filled.MonetizationOn
import androidx.compose.material.icons.filled.Shield
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.testTag
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.example.data.CloudHealthState
import com.example.data.model.RankCalculator
import com.example.ui.theme.JetBrainsMonoFamily
import com.example.ui.theme.StormdropColors

@Composable
fun CurrencyPill(
    gold: Int,
    diamonds: Int,
    modifier: Modifier = Modifier,
    onDiamondsClick: (() -> Unit)? = null,
) {
    Row(
        modifier = modifier,
        horizontalArrangement = Arrangement.spacedBy(8.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Surface(
            color = StormdropColors.BgElevated,
            shape = RoundedCornerShape(20.dp),
            border = BorderStroke(1.dp, StormdropColors.Gold.copy(alpha = 0.35f)),
        ) {
            Row(
                modifier = Modifier.padding(horizontal = 10.dp, vertical = 6.dp),
                verticalAlignment = Alignment.CenterVertically,
            ) {
                Icon(
                    imageVector = Icons.Filled.MonetizationOn,
                    contentDescription = "Gold",
                    tint = StormdropColors.Gold,
                    modifier = Modifier.size(16.dp),
                )
                Spacer(Modifier.width(4.dp))
                Text(
                    text = "%,d".format(gold),
                    style = MaterialTheme.typography.labelLarge.copy(
                        fontFamily = JetBrainsMonoFamily,
                        color = StormdropColors.Gold,
                    ),
                )
            }
        }
        Surface(
            color = StormdropColors.BgElevated,
            shape = RoundedCornerShape(20.dp),
            border = BorderStroke(1.dp, StormdropColors.Diamond.copy(alpha = 0.45f)),
            modifier = if (onDiamondsClick != null) {
                Modifier
                    .clip(RoundedCornerShape(20.dp))
                    .clickable(onClick = onDiamondsClick)
            } else {
                Modifier
            },
        ) {
            Row(
                modifier = Modifier.padding(horizontal = 10.dp, vertical = 6.dp),
                verticalAlignment = Alignment.CenterVertically,
            ) {
                Icon(
                    imageVector = Icons.Filled.Diamond,
                    contentDescription = "Diamonds",
                    tint = StormdropColors.Diamond,
                    modifier = Modifier.size(16.dp),
                )
                Spacer(Modifier.width(4.dp))
                Text(
                    text = "%,d".format(diamonds),
                    style = MaterialTheme.typography.labelLarge.copy(
                        fontFamily = JetBrainsMonoFamily,
                        color = StormdropColors.Diamond,
                    ),
                )
            }
        }
    }
}

@Composable
fun RankBadgePill(points: Int, prefix: String = "BR", modifier: Modifier = Modifier) {
    val info = RankCalculator.rankInfo(points)
    val tierColor = when (info.tier) {
        "Bronze" -> Color(0xFFCD7F32)
        "Silver" -> Color(0xFFC0C8D4)
        "Gold" -> StormdropColors.Gold
        "Platinum" -> Color(0xFF47E6B1)
        "Diamond" -> StormdropColors.Diamond
        "Heroic" -> StormdropColors.Crimson
        else -> StormdropColors.RarityMythic
    }
    Surface(
        modifier = modifier,
        color = tierColor.copy(alpha = 0.14f),
        shape = RoundedCornerShape(10.dp),
        border = BorderStroke(1.dp, tierColor.copy(alpha = 0.5f)),
    ) {
        Row(
            modifier = Modifier.padding(horizontal = 8.dp, vertical = 4.dp),
            verticalAlignment = Alignment.CenterVertically,
        ) {
            Icon(
                imageVector = Icons.Filled.Shield,
                contentDescription = "$prefix Rank",
                tint = tierColor,
                modifier = Modifier.size(14.dp),
            )
            Spacer(Modifier.width(4.dp))
            Text(
                text = "$prefix ${info.label}",
                style = MaterialTheme.typography.labelSmall.copy(
                    color = tierColor,
                    fontWeight = FontWeight.Bold,
                ),
            )
        }
    }
}

@Composable
fun StormdropTopBar(
    title: String,
    gold: Int,
    diamonds: Int,
    onBack: () -> Unit,
    modifier: Modifier = Modifier,
) {
    Row(
        modifier = modifier
            .fillMaxWidth()
            .background(StormdropColors.BgSurface)
            .padding(horizontal = 8.dp, vertical = 8.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        IconButton(
            onClick = onBack,
            modifier = Modifier
                .size(48.dp)
                .testTag("top_back_button"),
        ) {
            Icon(
                imageVector = Icons.AutoMirrored.Filled.ArrowBack,
                contentDescription = "Back",
                tint = StormdropColors.TextMain,
            )
        }
        Text(
            text = title,
            style = MaterialTheme.typography.titleLarge,
            color = StormdropColors.TextMain,
            maxLines = 1,
            overflow = TextOverflow.Ellipsis,
            modifier = Modifier.weight(1f),
        )
        CurrencyPill(gold = gold, diamonds = diamonds)
    }
}

@Composable
fun RarityChip(rarity: String, modifier: Modifier = Modifier) {
    val color = StormdropColors.rarityColor(rarity)
    Box(
        modifier = modifier
            .clip(RoundedCornerShape(6.dp))
            .background(color.copy(alpha = 0.18f))
            .border(1.dp, color.copy(alpha = 0.55f), RoundedCornerShape(6.dp))
            .padding(horizontal = 6.dp, vertical = 2.dp),
    ) {
        Text(
            text = rarity.uppercase(),
            fontSize = 10.sp,
            fontWeight = FontWeight.Bold,
            color = color,
        )
    }
}

@Composable
fun CloudSyncBadge(health: CloudHealthState, modifier: Modifier = Modifier) {
    val tint = if (health.firebaseConnected) StormdropColors.Emerald else StormdropColors.Gold
    Row(
        modifier = modifier
            .clip(RoundedCornerShape(12.dp))
            .background(tint.copy(alpha = 0.12f))
            .border(1.dp, tint.copy(alpha = 0.35f), RoundedCornerShape(12.dp))
            .padding(horizontal = 8.dp, vertical = 4.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Icon(
            imageVector = if (health.firebaseConnected) Icons.Filled.CloudDone else Icons.Filled.CloudSync,
            contentDescription = health.summary,
            tint = tint,
            modifier = Modifier.size(14.dp),
        )
        Spacer(Modifier.width(5.dp))
        Text(
            text = if (health.firebaseConnected) "Firebase Online" else "Syncing",
            style = MaterialTheme.typography.labelSmall.copy(color = tint, fontWeight = FontWeight.SemiBold),
        )
    }
}

@Composable
fun SectionHeader(title: String, subtitle: String? = null, modifier: Modifier = Modifier) {
    Column(modifier = modifier.fillMaxWidth()) {
        Text(
            text = title,
            style = MaterialTheme.typography.titleMedium,
            color = StormdropColors.TextMain,
        )
        if (!subtitle.isNullOrBlank()) {
            Spacer(Modifier.height(2.dp))
            Text(
                text = subtitle,
                style = MaterialTheme.typography.bodySmall,
                color = StormdropColors.TextDim,
            )
        }
    }
}
