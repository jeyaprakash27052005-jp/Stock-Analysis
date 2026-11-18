package com.example.ui.theme

import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Shapes
import androidx.compose.material3.darkColorScheme
import androidx.compose.runtime.Composable
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.unit.dp

private val StormdropColorScheme = darkColorScheme(
    primary = StormdropColors.AccentOrange,
    onPrimary = Color(0xFF140B00),
    primaryContainer = StormdropColors.AccentOrangeDark,
    onPrimaryContainer = StormdropColors.TextMain,
    secondary = StormdropColors.AccentCyan,
    onSecondary = Color(0xFF001A2C),
    secondaryContainer = StormdropColors.AccentCyanDim,
    onSecondaryContainer = StormdropColors.TextMain,
    tertiary = StormdropColors.VigorPurple,
    onTertiary = Color(0xFF1A0736),
    background = StormdropColors.BgDeep,
    onBackground = StormdropColors.TextMain,
    surface = StormdropColors.BgSurface,
    onSurface = StormdropColors.TextMain,
    surfaceVariant = StormdropColors.BgElevated,
    onSurfaceVariant = StormdropColors.TextDim,
    outline = StormdropColors.Divider,
    error = StormdropColors.Crimson,
    onError = StormdropColors.TextMain,
)

private val StormdropShapes = Shapes(
    small = RoundedCornerShape(8.dp),
    medium = RoundedCornerShape(14.dp),
    large = RoundedCornerShape(18.dp),
)

@Composable
fun StormdropTheme(content: @Composable () -> Unit) {
    MaterialTheme(
        colorScheme = StormdropColorScheme,
        typography = StormdropTypography,
        shapes = StormdropShapes,
        content = content,
    )
}
