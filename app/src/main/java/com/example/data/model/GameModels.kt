package com.example.data.model

import androidx.compose.ui.graphics.Color
import kotlinx.serialization.Serializable

@Serializable
enum class GameMode(
    val apiId: String,
    val displayName: String,
    val shortName: String,
    val description: String,
    val maxPlayers: Int,
    val teamSize: Int,
    val isBattleRoyale: Boolean,
) {
    BR_SOLO(
        apiId = "br_solo",
        displayName = "Battle Royale – Solo",
        shortName = "BR Solo",
        description = "Drop onto Isle Verdant, loot up, outlast 19 rival fighters & the shrinking storm.",
        maxPlayers = 20,
        teamSize = 1,
        isBattleRoyale = true,
    ),
    BR_DUO(
        apiId = "br_duo",
        displayName = "Battle Royale – Duo",
        shortName = "BR Duo",
        description = "2-player squads fighting across a 4 km island. Last squad standing is STORM CHAMPION!",
        maxPlayers = 20,
        teamSize = 2,
        isBattleRoyale = true,
    ),
    BR_SQUAD(
        apiId = "br_squad",
        displayName = "Battle Royale – Squad",
        shortName = "BR Squad",
        description = "4-player squad warfare across Ironhaven City, Fort Basalt & Serpent River.",
        maxPlayers = 20,
        teamSize = 4,
        isBattleRoyale = true,
    ),
    CLASH_SQUAD(
        apiId = "clash_squad",
        displayName = "Clash Squad 4v4",
        shortName = "Clash 4v4",
        description = "Fast 4v4 Ironhaven arena rounds with escalating tactical loadouts. First to 4 wins.",
        maxPlayers = 8,
        teamSize = 4,
        isBattleRoyale = false,
    ),
    TEAM_DEATHMATCH(
        apiId = "tdm",
        displayName = "Team Deathmatch",
        shortName = "TDM 4v4",
        description = "High-tempo 4v4 combat with 3s respawns. First team to 25 eliminations wins.",
        maxPlayers = 8,
        teamSize = 4,
        isBattleRoyale = false,
    ),
    TRAINING(
        apiId = "training",
        displayName = "Training Ground",
        shortName = "Training",
        description = "Offline practice range with high-tier weapons, targets & infinite respawn.",
        maxPlayers = 1,
        teamSize = 1,
        isBattleRoyale = false,
    );

    companion object {
        fun fromApiId(id: String): GameMode = entries.firstOrNull { it.apiId == id } ?: BR_SOLO
    }
}

enum class GraphicsPreset(val label: String) {
    SMOOTH("Smooth"),
    STANDARD("Standard"),
    ULTRA("Ultra"),
}

enum class ItemType(val label: String) {
    CHARACTER("Characters"),
    PET("Pets"),
    WEAPON_SKIN("Weapon Skins"),
    OUTPIT("Outfits"),
    EMOTE("Emotes"),
    PARACHUTE("Parachutes"),
    VEHICLE_SKIN("Vehicles"),
    AVATAR("Avatars"),
    BANNER("Banners"),
    CONSUMABLE("Consumables"),
    CURRENCY("Currency"),
    PASS_XP("Pass XP"),
}

data class CatalogItem(
    val id: String,
    val type: ItemType,
    val name: String,
    val rarity: String,
    val description: String,
    val priceGold: Int? = null,
    val priceDiamonds: Int? = null,
    val inStore: Boolean = true,
    val accentColor: Color = Color(0xFFFF8C1A),
    val skillName: String = "",
    val skillActive: Boolean = false,
    val skillCooldownSec: Float = 0f,
    val metaAmount: Int = 0,
    val metaCurrency: String = "",
)

data class WeaponDef(
    val id: String,
    val name: String,
    val category: String,
    val ammoType: String?,
    val damage: Float,
    val pellets: Int = 1,
    val rpm: Float,
    val magSize: Int,
    val rangeMeters: Float,
    val spreadDeg: Float,
    val autoFire: Boolean,
    val zoom: Float,
    val reloadSec: Float,
    val headMultiplier: Float = 1.8f,
    val score: Int = 1,
)

data class RankTierInfo(
    val tier: String,
    val division: Int?,
    val label: String,
    val points: Int,
    val nextTierAt: Int?,
    val tierIndex: Int,
)

object RankCalculator {
    private val tiers = listOf(
        "Bronze" to 0,
        "Silver" to 1000,
        "Gold" to 1300,
        "Platinum" to 1700,
        "Diamond" to 2200,
        "Heroic" to 2800,
        "Grandmaster" to 3400,
    )
    private val roman = listOf("", "I", "II", "III", "IV")

    fun rankInfo(points: Int): RankTierInfo {
        var idx = tiers.lastIndex
        while (idx > 0 && points < tiers[idx].second) idx--
        val (name, minPts) = tiers[idx]
        val next = tiers.getOrNull(idx + 1)?.second
        var div: Int? = null
        if (next != null && idx < 5) {
            val step = (next - minPts) / 4f
            div = 4 - ((points - minPts) / step).toInt().coerceIn(0, 3)
        }
        val label = if (div != null) "$name ${roman[div]}" else name
        return RankTierInfo(name, div, label, points, next, idx)
    }

    fun brRankDelta(points: Int, placement: Int, totalTeams: Int, kills: Int, assists: Int = 0): Int {
        val tierIdx = rankInfo(points).tierIndex
        val entryCost = listOf(0, 4, 8, 12, 16, 20, 24)[tierIdx]
        val pct = if (totalTeams > 1) 1f - (placement - 1).toFloat() / (totalTeams - 1) else 1f
        val placementPts = kotlin.math.round(pct * pct * 40f).toInt() + (if (placement == 1) 15 else 0)
        val killPts = kills.coerceAtMost(10) * 3 + assists.coerceAtMost(10)
        val delta = placementPts + killPts - entryCost
        return if (points + delta < 0) -points else delta
    }

    fun csRankDelta(points: Int, won: Boolean, kills: Int): Int {
        val tierIdx = rankInfo(points).tierIndex
        val delta = (if (won) 20 else -12) + kills.coerceAtMost(8) - tierIdx
        return if (points + delta < 0) -points else delta
    }

    fun xpForNextLevel(level: Int): Int = 400 + level * 150
}

data class MissionDef(
    val id: String,
    val period: String,
    val title: String,
    val metric: String,
    val target: Int,
    val rewardXp: Int,
    val rewardGold: Int,
    val rewardDiamonds: Int,
    val rewardPassXp: Int,
)

data class AchievementDef(
    val id: String,
    val title: String,
    val metric: String,
    val target: Int,
    val rewardGold: Int,
    val rewardDiamonds: Int,
)

data class PassRewardDef(
    val level: Int,
    val track: String,
    val itemId: String,
    val quantity: Int = 1,
)

data class SpinRewardDef(
    val itemId: String,
    val quantity: Int,
    val weight: Int,
)

data class LoginRewardDef(
    val day: Int,
    val itemId: String,
    val quantity: Int = 1,
)

data class GameEventDef(
    val id: String,
    val title: String,
    val description: String,
    val type: String,
    val dateRange: String,
)

data class DiamondPack(
    val productId: String,
    val diamonds: Int,
    val priceDisplay: String,
    val bonusLabel: String,
)

data class LeaderboardEntry(
    val rank: Int,
    val playerTag: String,
    val nickname: String,
    val level: Int,
    val brRankPoints: Int,
    val guildTag: String?,
    val region: String,
    val isSelf: Boolean = false,
)

data class MatchResultSummary(
    val mode: GameMode,
    val isRanked: Boolean,
    val won: Boolean,
    val placement: Int,
    val totalTeams: Int,
    val kills: Int,
    val damage: Int,
    val headshots: Int,
    val survivalSeconds: Int,
    val xpGained: Int,
    val goldGained: Int,
    val passXpGained: Int,
    val rankDelta: Int,
    val levelsGained: List<Int>,
)
