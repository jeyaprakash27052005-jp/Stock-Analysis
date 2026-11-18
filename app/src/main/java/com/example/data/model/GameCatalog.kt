package com.example.data.model

import androidx.compose.ui.graphics.Color

object GameCatalog {
    val regions = listOf("IND", "SEA", "ME", "EU", "NA", "SA")

    val characters = listOf(
        CatalogItem(
            id = "char_kael",
            type = ItemType.CHARACTER,
            name = "Kael",
            rarity = "common",
            description = "Active – Sprint Surge: +30% move speed for 6 s (CD 45 s).",
            priceGold = null,
            priceDiamonds = null,
            inStore = false,
            accentColor = Color(0xFFFF8C1A),
            skillName = "Sprint Surge",
            skillActive = true,
            skillCooldownSec = 45f,
        ),
        CatalogItem(
            id = "char_mira",
            type = ItemType.CHARACTER,
            name = "Mira",
            rarity = "rare",
            description = "Passive – Field Medic: healing items work 30% faster and heal +15%.",
            priceGold = 8000,
            priceDiamonds = 399,
            accentColor = Color(0xFF2FBF5B),
            skillName = "Field Medic",
        ),
        CatalogItem(
            id = "char_torvin",
            type = ItemType.CHARACTER,
            name = "Torvin",
            rarity = "epic",
            description = "Active – Bastion Dome: shield absorbing 120 damage for 5 s (CD 60 s).",
            priceGold = null,
            priceDiamonds = 499,
            accentColor = Color(0xFF5AB8FF),
            skillName = "Bastion Dome",
            skillActive = true,
            skillCooldownSec = 60f,
        ),
        CatalogItem(
            id = "char_zara",
            type = ItemType.CHARACTER,
            name = "Zara",
            rarity = "rare",
            description = "Passive – Ghost Step: footsteps are silent while crouched or prone.",
            priceGold = 8000,
            priceDiamonds = 399,
            accentColor = Color(0xFFB07CFF),
            skillName = "Ghost Step",
        ),
        CatalogItem(
            id = "char_okoro",
            type = ItemType.CHARACTER,
            name = "Okoro",
            rarity = "epic",
            description = "Active – Healing Aura: heals you and allies within 5 m for 6 HP/s, 5 s (CD 50 s).",
            priceGold = null,
            priceDiamonds = 499,
            accentColor = Color(0xFF44D17A),
            skillName = "Healing Aura",
            skillActive = true,
            skillCooldownSec = 50f,
        ),
        CatalogItem(
            id = "char_lyra",
            type = ItemType.CHARACTER,
            name = "Lyra",
            rarity = "rare",
            description = "Passive – Eagle Eye: enemies you hit are marked on radar for 3 s.",
            priceGold = 8000,
            priceDiamonds = 399,
            accentColor = Color(0xFFFFD23F),
            skillName = "Eagle Eye",
        ),
        CatalogItem(
            id = "char_dante",
            type = ItemType.CHARACTER,
            name = "Dante",
            rarity = "rare",
            description = "Passive – Iron Skin: take 8% less damage from bullets.",
            priceGold = 8000,
            priceDiamonds = 399,
            accentColor = Color(0xFF8C9BAE),
            skillName = "Iron Skin",
        ),
        CatalogItem(
            id = "char_suki",
            type = ItemType.CHARACTER,
            name = "Suki",
            rarity = "epic",
            description = "Active – Blink Dash: dash 7 m in the move direction (CD 30 s).",
            priceGold = null,
            priceDiamonds = 499,
            accentColor = Color(0xFFFF5E8E),
            skillName = "Blink Dash",
            skillActive = true,
            skillCooldownSec = 30f,
        ),
        CatalogItem(
            id = "char_rook",
            type = ItemType.CHARACTER,
            name = "Rook",
            rarity = "common",
            description = "Passive – Scavenger: eliminating an enemy restores 25 HP.",
            priceGold = 6000,
            priceDiamonds = 299,
            accentColor = Color(0xFFD9822B),
            skillName = "Scavenger",
        ),
        CatalogItem(
            id = "char_nova",
            type = ItemType.CHARACTER,
            name = "Nova",
            rarity = "legendary",
            description = "Active – Pulse Scan: reveals enemies within 60 m for 4 s (CD 75 s).",
            priceGold = null,
            priceDiamonds = 699,
            accentColor = Color(0xFF4DF2FF),
            skillName = "Pulse Scan",
            skillActive = true,
            skillCooldownSec = 75f,
        ),
        CatalogItem(
            id = "char_bram",
            type = ItemType.CHARACTER,
            name = "Bram",
            rarity = "common",
            description = "Passive – Quick Hands: reload 20% faster.",
            priceGold = 6000,
            priceDiamonds = 299,
            accentColor = Color(0xFFCC9955),
            skillName = "Quick Hands",
        ),
        CatalogItem(
            id = "char_ivy",
            type = ItemType.CHARACTER,
            name = "Ivy",
            rarity = "rare",
            description = "Passive – Vigor Bloom: Vigor Shrooms give 50% more energy.",
            priceGold = 8000,
            priceDiamonds = 399,
            accentColor = Color(0xFFA0E65A),
            skillName = "Vigor Bloom",
        ),
    )

    val pets = listOf(
        CatalogItem("pet_ember", ItemType.PET, "Ember the Fox", "rare", "Takes 50% less fall and landing damage.", null, 299, accentColor = Color(0xFFFF7A33)),
        CatalogItem("pet_byte", ItemType.PET, "Byte the Owl", "epic", "Pings enemies who shoot within 30 m of you.", null, 499, accentColor = Color(0xFF66D1FF)),
        CatalogItem("pet_pebble", ItemType.PET, "Pebble the Turtle", "rare", "Repairs 1 vest durability every 4 s out of combat.", null, 299, accentColor = Color(0xFF4CD964)),
        CatalogItem("pet_dash", ItemType.PET, "Dash the Ferret", "common", "Loot pickup range and speed +20%.", 5000, 199, accentColor = Color(0xFFE0B866)),
    )

    val cosmeticsAndConsumables = listOf(
        CatalogItem("skin_raptor_neon", ItemType.WEAPON_SKIN, "Raptor-7 Neon Pulse", "epic", "Glowing AR finish.", null, 299, accentColor = Color(0xFF33E6FF)),
        CatalogItem("skin_wasp_tiger", ItemType.WEAPON_SKIN, "Wasp SMG Tiger Stripe", "rare", "Orange camo for the Wasp.", 12000, 199, accentColor = Color(0xFFFF8C1A)),
        CatalogItem("skin_longreach_frost", ItemType.WEAPON_SKIN, "Longreach Frostbite", "legendary", "Ice-crystal sniper skin.", null, 599, accentColor = Color(0xFF8CE6FF)),
        CatalogItem("skin_thunder_gold", ItemType.WEAPON_SKIN, "Thunderclap Gilded", "epic", "Gold-plated shotgun.", null, 349, accentColor = Color(0xFFFFD23F)),
        CatalogItem("outfit_desert_ops", ItemType.OUTPIT, "Desert Ops Set", "rare", "Sand-coloured tactical outfit.", 15000, 249, accentColor = Color(0xFFD6B87C)),
        CatalogItem("outfit_neon_runner", ItemType.OUTPIT, "Neon Runner Set", "legendary", "Light-strip streetwear.", null, 799, accentColor = Color(0xFFFF4D94)),
        CatalogItem("outfit_cap_storm", ItemType.OUTPIT, "Storm Cap", "common", "Basic tactical cap.", 3000, null, accentColor = Color(0xFF8C9BAE)),
        CatalogItem("emote_victory_flip", ItemType.EMOTE, "Victory Flip", "epic", "Backflip celebration.", null, 299, accentColor = Color(0xFFB07CFF)),
        CatalogItem("emote_salute", ItemType.EMOTE, "Salute", "common", "Classic salute.", 4000, 99, accentColor = Color(0xFFB2B8C2)),
        CatalogItem("emote_dance_wave", ItemType.EMOTE, "Wave Dance", "rare", "Smooth wave dance.", null, 199, accentColor = Color(0xFF5AB8FF)),
        CatalogItem("chute_phoenix", ItemType.PARACHUTE, "Phoenix Wings", "legendary", "Fiery parachute trail.", null, 499, accentColor = Color(0xFFFF661A)),
        CatalogItem("chute_classic_blue", ItemType.PARACHUTE, "Classic Blue", "common", "Simple blue canopy.", 5000, null, accentColor = Color(0xFF4D99FF)),
        CatalogItem("veh_jeep_jungle", ItemType.VEHICLE_SKIN, "Jungle Jeep", "rare", "Camo jeep paint.", null, 249, accentColor = Color(0xFF4E8C44)),
        CatalogItem("veh_bike_chrome", ItemType.VEHICLE_SKIN, "Chrome Bike", "epic", "Mirror-finish motorbike.", null, 349, accentColor = Color(0xFFC2D4E8)),
        CatalogItem("avatar_default", ItemType.AVATAR, "Rookie", "common", "Starter avatar badge.", null, null, inStore = false),
        CatalogItem("banner_default", ItemType.BANNER, "Storm Grey", "common", "Starter profile banner.", null, null, inStore = false),
        CatalogItem("cur_gold_100", ItemType.CURRENCY, "100 Gold", "common", "Gold currency pack.", inStore = false, metaAmount = 100, metaCurrency = "gold"),
        CatalogItem("cur_gold_500", ItemType.CURRENCY, "500 Gold", "common", "Gold currency pack.", inStore = false, metaAmount = 500, metaCurrency = "gold"),
        CatalogItem("cur_gold_2000", ItemType.CURRENCY, "2,000 Gold", "rare", "Gold currency stash.", inStore = false, metaAmount = 2000, metaCurrency = "gold"),
        CatalogItem("cur_diamonds_10", ItemType.CURRENCY, "10 Diamonds", "rare", "Diamond reward.", inStore = false, metaAmount = 10, metaCurrency = "diamonds"),
        CatalogItem("cur_diamonds_50", ItemType.CURRENCY, "50 Diamonds", "epic", "Diamond reward.", inStore = false, metaAmount = 50, metaCurrency = "diamonds"),
        CatalogItem("card_xp_double", ItemType.CONSUMABLE, "Double XP Card (1 h)", "rare", "Doubles match XP for 60 minutes.", inStore = false),
        CatalogItem("card_name_change", ItemType.CONSUMABLE, "Name Change Card", "rare", "Lets you rename your callsign once for free.", priceDiamonds = 99, inStore = true),
    )

    val allItems: List<CatalogItem> = characters + pets + cosmeticsAndConsumables
    private val itemMap: Map<String, CatalogItem> = allItems.associateBy { it.id }

    fun itemById(id: String): CatalogItem? = itemMap[id]

    val weapons: Map<String, WeaponDef> = listOf(
        WeaponDef("fists", "Fists", "Melee", null, 18f, 1, 120f, 0, 3.0f, 0f, false, 1f, 0f, 1.5f, 0),
        WeaponDef("pistol", "Viper Pistol", "Pistol", "light", 34f, 1, 320f, 12, 55f, 2.2f, false, 1.3f, 1.3f, 1.8f, 1),
        WeaponDef("revolver", "Sidekick Revolver", "Pistol", "heavy", 72f, 1, 140f, 6, 65f, 1.5f, false, 1.4f, 1.9f, 2.0f, 2),
        WeaponDef("smg", "Wasp SMG", "SMG", "light", 26f, 1, 900f, 32, 70f, 3.2f, true, 1.3f, 1.8f, 1.7f, 3),
        WeaponDef("hornet", "Hornet SMG", "SMG", "light", 30f, 1, 780f, 25, 80f, 2.6f, true, 1.4f, 1.7f, 1.75f, 3),
        WeaponDef("shotgun", "Thunderclap", "Shotgun", "shells", 19f, 8, 75f, 6, 28f, 7.5f, false, 1.2f, 2.5f, 1.6f, 3),
        WeaponDef("ar", "Raptor-7", "AR", "medium", 38f, 1, 600f, 30, 145f, 2.1f, true, 1.8f, 2.2f, 1.85f, 4),
        WeaponDef("halberd", "Halberd AR", "AR", "medium", 45f, 1, 480f, 30, 165f, 1.8f, true, 2.0f, 2.3f, 1.9f, 5),
        WeaponDef("sniper", "Longreach", "Sniper", "sniper", 170f, 1, 45f, 5, 290f, 0.25f, false, 3.5f, 3.1f, 2.2f, 4),
        WeaponDef("dmr", "Falcon DMR", "Sniper", "sniper", 72f, 1, 200f, 10, 220f, 0.9f, false, 2.5f, 2.5f, 2.0f, 4),
        WeaponDef("lmg", "Juggernaut LMG", "LMG", "heavy", 40f, 1, 650f, 80, 155f, 2.9f, true, 1.8f, 4.2f, 1.8f, 5),
    ).associateBy { it.id }

    val ammoNames = mapOf(
        "light" to "Light Ammo",
        "medium" to "Medium Ammo",
        "heavy" to "Heavy Ammo",
        "shells" to "Shells",
        "sniper" to "Sniper Ammo",
    )

    val missions = listOf(
        MissionDef("d_play_3", "daily", "Play 3 matches", "matches", 3, 300, 200, 0, 200),
        MissionDef("d_kills_5", "daily", "Eliminate 5 enemies", "kills", 5, 300, 250, 0, 200),
        MissionDef("d_damage_1000", "daily", "Deal 1,000 damage", "damage", 1000, 300, 250, 0, 200),
        MissionDef("d_survive_20", "daily", "Survive 20 minutes in total", "survival_minutes", 20, 200, 150, 0, 150),
        MissionDef("w_top10_10", "weekly", "Finish Top 10 ten times", "top10", 10, 1500, 1000, 10, 800),
        MissionDef("w_win_2", "weekly", "Become Storm Champion twice", "wins", 2, 2000, 1500, 20, 1000),
        MissionDef("w_head_25", "weekly", "Land 25 headshot eliminations", "headshots", 25, 1500, 1000, 10, 800),
    )

    val achievements = listOf(
        AchievementDef("a_first_win", "First Storm Champion", "wins", 1, 1000, 20),
        AchievementDef("a_kills_100", "Centurion", "kills", 100, 2000, 30),
        AchievementDef("a_kills_1000", "Storm Legend", "kills", 1000, 10000, 100),
        AchievementDef("a_matches_50", "Veteran Dropper", "matches", 50, 2000, 20),
    )

    val passSeasonId = "s1"
    val passSeasonName = "Season 1 – First Landing"
    const val passPremiumPriceDiamonds = 499
    const val passXpPerLevel = 1000
    const val passMaxLevel = 50

    val passRewards: List<PassRewardDef> = (1..passMaxLevel).flatMap { lvl ->
        val freeItem = when {
            lvl % 10 == 0 -> "cur_diamonds_10"
            lvl % 5 == 0 -> "card_xp_double"
            else -> "cur_gold_100"
        }
        val premItem = when (lvl) {
            1 -> "outfit_neon_runner"
            15 -> "skin_raptor_neon"
            30 -> "chute_phoenix"
            50 -> "skin_longreach_frost"
            else -> if (lvl % 4 == 0) "cur_diamonds_10" else "cur_gold_500"
        }
        listOf(
            PassRewardDef(lvl, "free", freeItem),
            PassRewardDef(lvl, "premium", premItem),
        )
    }

    val spinRewards = listOf(
        SpinRewardDef("chute_phoenix", 1, 10),
        SpinRewardDef("skin_thunder_gold", 1, 20),
        SpinRewardDef("emote_victory_flip", 1, 40),
        SpinRewardDef("cur_diamonds_10", 1, 130),
        SpinRewardDef("card_xp_double", 1, 200),
        SpinRewardDef("cur_gold_500", 1, 300),
        SpinRewardDef("cur_gold_100", 1, 300),
    )

    val loginRewards = listOf(
        LoginRewardDef(1, "cur_gold_500"),
        LoginRewardDef(2, "card_xp_double"),
        LoginRewardDef(3, "cur_gold_500"),
        LoginRewardDef(4, "cur_diamonds_10"),
        LoginRewardDef(5, "cur_gold_2000"),
        LoginRewardDef(6, "emote_salute"),
        LoginRewardDef(7, "cur_diamonds_50"),
    )

    val events = listOf(
        GameEventDef("ev_launch", "Launch Celebration", "Log in for 7 consecutive days to claim 50 Diamonds.", "news", "Nov 01 – Dec 31"),
        GameEventDef("ev_double_xp", "Double XP Weekend", "All matches grant 2x XP toward Level & Storm Pass.", "double_xp", "Nov 28 – Nov 30"),
        GameEventDef("ev_night_mode", "Night Raid", "30% of Isle Verdant matches drop under nightfall lighting.", "mode", "Dec 05 – Dec 12"),
    )

    val diamondPacks = listOf(
        DiamondPack("diamonds_100", 100, "$0.99", "Starter"),
        DiamondPack("diamonds_310", 310, "$2.99", "+10 Bonus"),
        DiamondPack("diamonds_520", 520, "$4.99", "+20 Bonus"),
        DiamondPack("diamonds_1060", 1060, "$9.99", "Popular"),
        DiamondPack("diamonds_2180", 2180, "$19.99", "+180 Bonus"),
        DiamondPack("diamonds_5600", 5600, "$49.99", "Best Value"),
    )

    val defaultBotsLeaderboard = listOf(
        LeaderboardEntry(1, "9012481201", "StormValkyrie", 42, 3520, "APEX", "IND"),
        LeaderboardEntry(2, "9012481202", "KaelMain_99", 37, 2940, "VORTX", "IND"),
        LeaderboardEntry(3, "9012481203", "BasaltKing", 31, 2410, "APEX", "IND"),
        LeaderboardEntry(4, "9012481204", "NyxSniper", 28, 1980, "NIGHT", "SEA"),
        LeaderboardEntry(5, "9012481205", "ThunderMako", 24, 1580, "BOLT", "SEA"),
        LeaderboardEntry(6, "9012481206", "DuskRunner", 19, 1340, "NIGHT", "EU"),
        LeaderboardEntry(7, "9012481207", "RustportAce", 15, 1120, null, "NA"),
        LeaderboardEntry(8, "9012481208", "FrostEcho", 12, 840, "BOLT", "ME"),
        LeaderboardEntry(9, "9012481209", "JunoDrop", 8, 490, null, "SA"),
    )
}
