package com.example.data.local

import android.content.Context
import androidx.datastore.preferences.core.booleanPreferencesKey
import androidx.datastore.preferences.core.edit
import androidx.datastore.preferences.core.floatPreferencesKey
import androidx.datastore.preferences.core.stringPreferencesKey
import androidx.datastore.preferences.preferencesDataStore
import com.example.data.model.GraphicsPreset
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.map

private val Context.settingsStore by preferencesDataStore(name = "stormdrop_settings")

data class PlayerSettingsState(
    val graphics: GraphicsPreset = GraphicsPreset.STANDARD,
    val highFrameRate: Boolean = true,
    val lookSensitivity: Float = 1.0f,
    val scopeSensitivity: Float = 0.7f,
    val gyroscopeAim: Boolean = false,
    val autoFire: Boolean = false,
    val aimAssist: Boolean = true,
    val masterVolume: Float = 0.8f,
    val voiceChat: Boolean = true,
    val hudScale: Float = 1.0f,
)

class SettingsDataStore(private val context: Context) {
    private object Keys {
        val GRAPHICS = stringPreferencesKey("graphics")
        val HIGH_FPS = booleanPreferencesKey("high_fps")
        val LOOK_SENS = floatPreferencesKey("look_sens")
        val SCOPE_SENS = floatPreferencesKey("scope_sens")
        val GYRO = booleanPreferencesKey("gyro")
        val AUTO_FIRE = booleanPreferencesKey("auto_fire")
        val AIM_ASSIST = booleanPreferencesKey("aim_assist")
        val VOLUME = floatPreferencesKey("volume")
        val VOICE_CHAT = booleanPreferencesKey("voice_chat")
        val HUD_SCALE = floatPreferencesKey("hud_scale")
    }

    val settingsFlow: Flow<PlayerSettingsState> = context.settingsStore.data.map { prefs ->
        PlayerSettingsState(
            graphics = runCatching {
                GraphicsPreset.valueOf(prefs[Keys.GRAPHICS] ?: GraphicsPreset.STANDARD.name)
            }.getOrDefault(GraphicsPreset.STANDARD),
            highFrameRate = prefs[Keys.HIGH_FPS] ?: true,
            lookSensitivity = prefs[Keys.LOOK_SENS] ?: 1.0f,
            scopeSensitivity = prefs[Keys.SCOPE_SENS] ?: 0.7f,
            gyroscopeAim = prefs[Keys.GYRO] ?: false,
            autoFire = prefs[Keys.AUTO_FIRE] ?: false,
            aimAssist = prefs[Keys.AIM_ASSIST] ?: true,
            masterVolume = prefs[Keys.VOLUME] ?: 0.8f,
            voiceChat = prefs[Keys.VOICE_CHAT] ?: true,
            hudScale = prefs[Keys.HUD_SCALE] ?: 1.0f,
        )
    }

    suspend fun update(transform: (PlayerSettingsState) -> PlayerSettingsState) {
        context.settingsStore.edit { prefs ->
            val cur = PlayerSettingsState(
                graphics = runCatching {
                    GraphicsPreset.valueOf(prefs[Keys.GRAPHICS] ?: GraphicsPreset.STANDARD.name)
                }.getOrDefault(GraphicsPreset.STANDARD),
                highFrameRate = prefs[Keys.HIGH_FPS] ?: true,
                lookSensitivity = prefs[Keys.LOOK_SENS] ?: 1.0f,
                scopeSensitivity = prefs[Keys.SCOPE_SENS] ?: 0.7f,
                gyroscopeAim = prefs[Keys.GYRO] ?: false,
                autoFire = prefs[Keys.AUTO_FIRE] ?: false,
                aimAssist = prefs[Keys.AIM_ASSIST] ?: true,
                masterVolume = prefs[Keys.VOLUME] ?: 0.8f,
                voiceChat = prefs[Keys.VOICE_CHAT] ?: true,
                hudScale = prefs[Keys.HUD_SCALE] ?: 1.0f,
            )
            val next = transform(cur)
            prefs[Keys.GRAPHICS] = next.graphics.name
            prefs[Keys.HIGH_FPS] = next.highFrameRate
            prefs[Keys.LOOK_SENS] = next.lookSensitivity
            prefs[Keys.SCOPE_SENS] = next.scopeSensitivity
            prefs[Keys.GYRO] = next.gyroscopeAim
            prefs[Keys.AUTO_FIRE] = next.autoFire
            prefs[Keys.AIM_ASSIST] = next.aimAssist
            prefs[Keys.VOLUME] = next.masterVolume
            prefs[Keys.VOICE_CHAT] = next.voiceChat
            prefs[Keys.HUD_SCALE] = next.hudScale
        }
    }
}
