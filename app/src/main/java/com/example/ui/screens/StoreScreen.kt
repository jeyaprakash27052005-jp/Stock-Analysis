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
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.layout.widthIn
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.AutoAwesome
import androidx.compose.material.icons.filled.Casino
import androidx.compose.material.icons.filled.Check
import androidx.compose.material.icons.filled.Diamond
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.ScrollableTabRow
import androidx.compose.material3.Surface
import androidx.compose.material3.Tab
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.platform.testTag
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import com.example.R
import com.example.data.SpinWinResult
import com.example.data.model.CatalogItem
import com.example.data.model.DiamondPack
import com.example.data.model.GameCatalog
import com.example.data.model.ItemType
import com.example.data.remote.OwnedItemDoc
import com.example.ui.components.RarityChip
import com.example.ui.components.StormdropTopBar
import com.example.ui.theme.JetBrainsMonoFamily
import com.example.ui.theme.StormdropColors

@Composable
fun StoreScreen(
    gold: Int,
    diamonds: Int,
    ownedItems: List<OwnedItemDoc>,
    spinResults: List<SpinWinResult>?,
    onBuyItem: (CatalogItem, String) -> Unit,
    onSpinFortune: (Int) -> Unit,
    onDismissSpinResults: () -> Unit,
    onPurchaseDiamonds: (DiamondPack) -> Unit,
    onBack: () -> Unit,
) {
    BackHandler(onBack = onBack)

    val tabs = listOf("Characters", "Pets", "Skins & Gear", "Fortune Drop", "Diamonds")
    var selectedTab by remember { mutableIntStateOf(0) }
    val ownedIds = remember(ownedItems) { ownedItems.map { it.itemId }.toSet() }

    Column(
        modifier = Modifier
            .fillMaxSize()
            .background(StormdropColors.BgDeep),
    ) {
        StormdropTopBar(
            title = "Armory Store",
            gold = gold,
            diamonds = diamonds,
            onBack = onBack,
        )

        ScrollableTabRow(
            selectedTabIndex = selectedTab,
            containerColor = StormdropColors.BgSurface,
            contentColor = StormdropColors.AccentOrange,
            edgePadding = 12.dp,
        ) {
            tabs.forEachIndexed { idx, label ->
                Tab(
                    selected = selectedTab == idx,
                    onClick = { selectedTab = idx },
                    text = {
                        Text(
                            text = label,
                            color = if (selectedTab == idx) StormdropColors.AccentOrange else StormdropColors.TextDim,
                        )
                    },
                    modifier = Modifier.testTag("store_tab_$idx"),
                )
            }
        }

        when (selectedTab) {
            0, 1, 2 -> {
                val filtered = remember(selectedTab) {
                    GameCatalog.allItems.filter { item ->
                        item.inStore && when (selectedTab) {
                            0 -> item.type == ItemType.CHARACTER
                            1 -> item.type == ItemType.PET
                            else -> item.type in setOf(
                                ItemType.WEAPON_SKIN,
                                ItemType.OUTPIT,
                                ItemType.EMOTE,
                                ItemType.PARACHUTE,
                                ItemType.VEHICLE_SKIN,
                                ItemType.CONSUMABLE,
                            )
                        }
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
                    items(filtered, key = { it.id }) { item ->
                        StoreItemCard(
                            item = item,
                            isOwned = item.id in ownedIds && item.type != ItemType.CONSUMABLE,
                            onBuy = { currency -> onBuyItem(item, currency) },
                        )
                    }
                }
            }

            3 -> {
                FortuneDropTab(
                    onSpin1 = { onSpinFortune(1) },
                    onSpin10 = { onSpinFortune(10) },
                )
            }

            4 -> {
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
                            color = StormdropColors.AccentCyan.copy(alpha = 0.12f),
                            shape = RoundedCornerShape(14.dp),
                            border = BorderStroke(1.dp, StormdropColors.AccentCyan.copy(alpha = 0.45f)),
                        ) {
                            Text(
                                text = "Diamond top-ups use Google Play Billing in production release builds.",
                                style = MaterialTheme.typography.bodySmall,
                                color = StormdropColors.AccentCyan,
                                modifier = Modifier.padding(12.dp),
                            )
                        }
                    }
                    items(GameCatalog.diamondPacks, key = { it.productId }) { pack ->
                        Surface(
                            color = StormdropColors.BgSurface,
                            shape = RoundedCornerShape(16.dp),
                            border = BorderStroke(1.dp, StormdropColors.Diamond.copy(alpha = 0.35f)),
                            modifier = Modifier.fillMaxWidth(),
                        ) {
                            Row(
                                modifier = Modifier.padding(16.dp),
                                verticalAlignment = Alignment.CenterVertically,
                            ) {
                                Icon(
                                    imageVector = Icons.Filled.Diamond,
                                    contentDescription = "Diamonds",
                                    tint = StormdropColors.Diamond,
                                    modifier = Modifier.size(32.dp),
                                )
                                Spacer(Modifier.width(12.dp))
                                Column(modifier = Modifier.weight(1f)) {
                                    Text(
                                        text = "${pack.diamonds} Diamonds",
                                        style = MaterialTheme.typography.titleMedium,
                                        color = StormdropColors.TextMain,
                                    )
                                    Text(
                                        text = pack.bonusLabel,
                                        style = MaterialTheme.typography.labelSmall,
                                        color = StormdropColors.Gold,
                                    )
                                }
                                Button(
                                    onClick = { onPurchaseDiamonds(pack) },
                                    colors = ButtonDefaults.buttonColors(
                                        containerColor = StormdropColors.Diamond,
                                        contentColor = Color(0xFF001A2C),
                                    ),
                                    shape = RoundedCornerShape(12.dp),
                                    modifier = Modifier.height(48.dp),
                                ) {
                                    Text(pack.priceDisplay, fontWeight = FontWeight.Bold)
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    if (spinResults != null) {
        AlertDialog(
            onDismissRequest = onDismissSpinResults,
            containerColor = StormdropColors.BgSurface,
            title = { Text("Phoenix Fortune Drop!", color = StormdropColors.Gold) },
            text = {
                Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                    spinResults.forEach { res ->
                        Row(
                            modifier = Modifier.fillMaxWidth(),
                            horizontalArrangement = Arrangement.SpaceBetween,
                            verticalAlignment = Alignment.CenterVertically,
                        ) {
                            Text(res.item.name, color = StormdropColors.TextMain)
                            if (res.duplicateConvertedGold > 0) {
                                Text("+${res.duplicateConvertedGold} Gold", color = StormdropColors.Gold)
                            } else {
                                RarityChip(res.item.rarity)
                            }
                        }
                    }
                }
            },
            confirmButton = {
                TextButton(onClick = onDismissSpinResults) {
                    Text("Collect", color = StormdropColors.AccentOrange)
                }
            },
        )
    }
}

@Composable
private fun StoreItemCard(
    item: CatalogItem,
    isOwned: Boolean,
    onBuy: (String) -> Unit,
) {
    Surface(
        color = StormdropColors.BgSurface,
        shape = RoundedCornerShape(16.dp),
        border = BorderStroke(1.dp, StormdropColors.rarityColor(item.rarity).copy(alpha = 0.35f)),
        modifier = Modifier.fillMaxWidth(),
    ) {
        Column(modifier = Modifier.padding(14.dp), verticalArrangement = Arrangement.spacedBy(8.dp)) {
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically,
            ) {
                Column(modifier = Modifier.weight(1f)) {
                    Row(
                        verticalAlignment = Alignment.CenterVertically,
                        horizontalArrangement = Arrangement.spacedBy(8.dp),
                    ) {
                        Text(
                            text = item.name,
                            style = MaterialTheme.typography.titleMedium,
                            color = StormdropColors.TextMain,
                        )
                        RarityChip(rarity = item.rarity)
                    }
                    Spacer(Modifier.height(4.dp))
                    Text(
                        text = item.description,
                        style = MaterialTheme.typography.bodySmall,
                        color = StormdropColors.TextDim,
                    )
                }
            }

            if (isOwned) {
                Row(
                    verticalAlignment = Alignment.CenterVertically,
                    horizontalArrangement = Arrangement.spacedBy(4.dp),
                ) {
                    Icon(
                        imageVector = Icons.Filled.Check,
                        contentDescription = "Owned",
                        tint = StormdropColors.Emerald,
                        modifier = Modifier.size(16.dp),
                    )
                    Text("Owned in Vault", style = MaterialTheme.typography.labelMedium, color = StormdropColors.Emerald)
                }
            } else {
                Row(horizontalArrangement = Arrangement.spacedBy(10.dp)) {
                    if (item.priceGold != null) {
                        OutlinedButton(
                            onClick = { onBuy("gold") },
                            border = BorderStroke(1.dp, StormdropColors.Gold.copy(alpha = 0.6f)),
                            shape = RoundedCornerShape(10.dp),
                            modifier = Modifier
                                .height(48.dp)
                                .testTag("buy_gold_${item.id}"),
                        ) {
                            Text(
                                text = "%,d Gold".format(item.priceGold),
                                color = StormdropColors.Gold,
                                fontFamily = JetBrainsMonoFamily,
                            )
                        }
                    }
                    if (item.priceDiamonds != null) {
                        Button(
                            onClick = { onBuy("diamonds") },
                            colors = ButtonDefaults.buttonColors(
                                containerColor = StormdropColors.Diamond,
                                contentColor = Color(0xFF001A2C),
                            ),
                            shape = RoundedCornerShape(10.dp),
                            modifier = Modifier
                                .height(48.dp)
                                .testTag("buy_dia_${item.id}"),
                        ) {
                            Text(
                                text = "%,d Dia".format(item.priceDiamonds),
                                fontFamily = JetBrainsMonoFamily,
                                fontWeight = FontWeight.Bold,
                            )
                        }
                    }
                }
            }
        }
    }
}

@Composable
private fun FortuneDropTab(
    onSpin1: () -> Unit,
    onSpin10: () -> Unit,
) {
    LazyColumn(
        modifier = Modifier.fillMaxSize(),
        contentPadding = PaddingValues(16.dp),
        verticalArrangement = Arrangement.spacedBy(14.dp),
    ) {
        item {
            Surface(
                shape = RoundedCornerShape(20.dp),
                color = StormdropColors.BgSurface,
                border = BorderStroke(1.dp, StormdropColors.AccentOrange.copy(alpha = 0.55f)),
            ) {
                Box(modifier = Modifier.fillMaxWidth()) {
                    Image(
                        painter = painterResource(id = R.drawable.img_fortune_drop),
                        contentDescription = "Phoenix Fortune Drop crate",
                        contentScale = ContentScale.Crop,
                        modifier = Modifier
                            .fillMaxWidth()
                            .height(190.dp),
                        alpha = 0.55f,
                    )
                    Box(
                        modifier = Modifier
                            .fillMaxWidth()
                            .height(190.dp)
                            .background(
                                Brush.verticalGradient(
                                    listOf(Color.Transparent, StormdropColors.BgSurface)
                                )
                            ),
                    )
                    Column(
                        modifier = Modifier
                            .align(Alignment.BottomStart)
                            .padding(16.dp),
                    ) {
                        Row(verticalAlignment = Alignment.CenterVertically) {
                            Icon(
                                imageVector = Icons.Filled.AutoAwesome,
                                contentDescription = null,
                                tint = StormdropColors.Gold,
                                modifier = Modifier.size(20.dp),
                            )
                            Spacer(Modifier.width(6.dp))
                            Text(
                                text = "PHOENIX FORTUNE DROP",
                                style = MaterialTheme.typography.titleLarge,
                                color = StormdropColors.Gold,
                            )
                        }
                        Text(
                            text = "Spin for Phoenix Wings, Thunderclap Gilded, Victory Flip, or Diamonds. Duplicates convert to +1,000 Gold.",
                            style = MaterialTheme.typography.bodySmall,
                            color = StormdropColors.TextDim,
                        )
                    }
                }
            }
        }

        item {
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.spacedBy(12.dp),
            ) {
                Button(
                    onClick = onSpin1,
                    colors = ButtonDefaults.buttonColors(
                        containerColor = StormdropColors.AccentCyan,
                        contentColor = Color(0xFF001A2C),
                    ),
                    shape = RoundedCornerShape(14.dp),
                    modifier = Modifier
                        .weight(1f)
                        .height(54.dp)
                        .testTag("spin_1x_button"),
                ) {
                    Icon(Icons.Filled.Casino, contentDescription = null, modifier = Modifier.size(18.dp))
                    Spacer(Modifier.width(6.dp))
                    Text("1x Spin · 20 Dia", fontWeight = FontWeight.Bold)
                }
                Button(
                    onClick = onSpin10,
                    colors = ButtonDefaults.buttonColors(
                        containerColor = StormdropColors.AccentOrange,
                        contentColor = Color(0xFF140B00),
                    ),
                    shape = RoundedCornerShape(14.dp),
                    modifier = Modifier
                        .weight(1f)
                        .height(54.dp)
                        .testTag("spin_10x_button"),
                ) {
                    Icon(Icons.Filled.Casino, contentDescription = null, modifier = Modifier.size(18.dp))
                    Spacer(Modifier.width(6.dp))
                    Text("10x Spin · 180 Dia", fontWeight = FontWeight.Bold)
                }
            }
        }
    }
}
