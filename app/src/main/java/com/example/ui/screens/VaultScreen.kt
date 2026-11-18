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
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.ScrollableTabRow
import androidx.compose.material3.Surface
import androidx.compose.material3.Tab
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.testTag
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import com.example.data.model.CatalogItem
import com.example.data.model.GameCatalog
import com.example.data.model.ItemType
import com.example.data.remote.OwnedItemDoc
import com.example.data.remote.UserProfileDoc
import com.example.ui.components.RarityChip
import com.example.ui.components.StormdropTopBar
import com.example.ui.theme.StormdropColors

@Composable
fun VaultScreen(
    profile: UserProfileDoc?,
    ownedItems: List<OwnedItemDoc>,
    onEquipItem: (CatalogItem) -> Unit,
    onBack: () -> Unit,
) {
    BackHandler(onBack = onBack)

    val tabs = listOf("Characters", "Pets", "Skins & Gear", "Consumables")
    var selectedTab by remember { mutableIntStateOf(0) }

    val resolvedOwned = remember(ownedItems, selectedTab) {
        ownedItems.mapNotNull { owned ->
            val cat = GameCatalog.itemById(owned.itemId) ?: return@mapNotNull null
            cat to owned.quantity
        }.filter { (item, _) ->
            when (selectedTab) {
                0 -> item.type == ItemType.CHARACTER
                1 -> item.type == ItemType.PET
                2 -> item.type in setOf(
                    ItemType.WEAPON_SKIN,
                    ItemType.PARACHUTE,
                    ItemType.OUTPIT,
                    ItemType.EMOTE,
                    ItemType.VEHICLE_SKIN,
                )
                else -> item.type in setOf(ItemType.CONSUMABLE, ItemType.AVATAR, ItemType.BANNER)
            }
        }
    }

    Column(
        modifier = Modifier
            .fillMaxSize()
            .background(StormdropColors.BgDeep),
    ) {
        StormdropTopBar(
            title = "Armory Vault",
            gold = profile?.gold ?: 0,
            diamonds = profile?.diamonds ?: 0,
            onBack = onBack,
        )

        ScrollableTabRow(
            selectedTabIndex = selectedTab,
            containerColor = StormdropColors.BgSurface,
            contentColor = StormdropColors.AccentCyan,
            edgePadding = 12.dp,
        ) {
            tabs.forEachIndexed { idx, label ->
                Tab(
                    selected = selectedTab == idx,
                    onClick = { selectedTab = idx },
                    text = {
                        Text(
                            label,
                            color = if (selectedTab == idx) StormdropColors.AccentCyan else StormdropColors.TextDim,
                        )
                    },
                    modifier = Modifier.testTag("vault_tab_$idx"),
                )
            }
        }

        if (resolvedOwned.isEmpty()) {
            Column(
                modifier = Modifier
                    .fillMaxSize()
                    .padding(32.dp),
                horizontalAlignment = Alignment.CenterHorizontally,
                verticalArrangement = Arrangement.Center,
            ) {
                Text(
                    text = "No items in this category yet.",
                    style = MaterialTheme.typography.titleMedium,
                    color = StormdropColors.TextDim,
                )
                Spacer(Modifier.height(6.dp))
                Text(
                    text = "Visit Armory Store or Storm Pass to unlock new gear!",
                    style = MaterialTheme.typography.bodySmall,
                    color = StormdropColors.TextMuted,
                )
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
                items(resolvedOwned, key = { it.first.id }) { (item, qty) ->
                    val isEquipped = item.id == profile?.equippedCharacterId ||
                        item.id == profile?.equippedPetId ||
                        item.id == profile?.equippedParachuteId ||
                        item.id == profile?.equippedWeaponSkinId

                    Surface(
                        color = if (isEquipped) StormdropColors.BgElevated else StormdropColors.BgSurface,
                        shape = RoundedCornerShape(16.dp),
                        border = BorderStroke(
                            width = if (isEquipped) 2.dp else 1.dp,
                            color = if (isEquipped) StormdropColors.AccentCyan else StormdropColors.Divider,
                        ),
                        modifier = Modifier.fillMaxWidth(),
                    ) {
                        Row(
                            modifier = Modifier.padding(14.dp),
                            verticalAlignment = Alignment.CenterVertically,
                        ) {
                            Column(modifier = Modifier.weight(1f)) {
                                Row(
                                    verticalAlignment = Alignment.CenterVertically,
                                    horizontalArrangement = Arrangement.spacedBy(8.dp),
                                ) {
                                    Text(
                                        text = if (qty > 1) "${item.name} x$qty" else item.name,
                                        style = MaterialTheme.typography.titleMedium,
                                        color = StormdropColors.TextMain,
                                    )
                                    RarityChip(item.rarity)
                                }
                                Spacer(Modifier.height(4.dp))
                                Text(
                                    text = item.description,
                                    style = MaterialTheme.typography.bodySmall,
                                    color = StormdropColors.TextDim,
                                )
                            }
                            if (item.type in setOf(
                                    ItemType.CHARACTER,
                                    ItemType.PET,
                                    ItemType.PARACHUTE,
                                    ItemType.WEAPON_SKIN,
                                )
                            ) {
                                Button(
                                    onClick = { onEquipItem(item) },
                                    enabled = !isEquipped,
                                    colors = ButtonDefaults.buttonColors(
                                        containerColor = StormdropColors.AccentCyan,
                                        contentColor = Color(0xFF001A2C),
                                    ),
                                    shape = RoundedCornerShape(10.dp),
                                    modifier = Modifier
                                        .height(48.dp)
                                        .testTag("equip_${item.id}"),
                                ) {
                                    Text(if (isEquipped) "Equipped" else "Equip", fontWeight = FontWeight.Bold)
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
