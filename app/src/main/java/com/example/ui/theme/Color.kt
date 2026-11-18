package com.example.ui.theme

import androidx.compose.ui.graphics.Color

object StormdropColors {
    val BgDeep = Color(0xFF0B0E15)
    val BgSurface = Color(0xFF121620)
    val BgElevated = Color(0xFF1B2232)
    val BgHighlight = Color(0xFF252E42)

    val AccentOrange = Color(0xFFFF8C1A)
    val AccentOrangeDark = Color(0xFFD96A00)
    val AccentCyan = Color(0xFF5AB8FF)
    val AccentCyanDim = Color(0xFF2B6F9E)

    val Gold = Color(0xFFFFD23F)
    val Diamond = Color(0xFF66D1FF)
    val VigorPurple = Color(0xFFB07CFF)
    val Emerald = Color(0xFF2FBF5B)
    val Crimson = Color(0xFFE5484D)

    val TextMain = Color(0xFFEEF1F6)
    val TextDim = Color(0xFF9AA4B5)
    val TextMuted = Color(0xFF636D7E)
    val Divider = Color(0x1FFFFFFF)

    val RarityCommon = Color(0xFFB2B8C2)
    val RarityRare = Color(0xFF4D99FF)
    val RarityEpic = Color(0xFFB366FF)
    val RarityLegendary = Color(0xFFFFB333)
    val RarityMythic = Color(0xFFFF4D66)

    fun rarityColor(rarity: String): Color = when (rarity.lowercase()) {
        "rare" -> RarityRare
        "epic" -> RarityEpic
        "legendary" -> RarityLegendary
        "mythic" -> RarityMythic
        else -> RarityCommon
    }
}
