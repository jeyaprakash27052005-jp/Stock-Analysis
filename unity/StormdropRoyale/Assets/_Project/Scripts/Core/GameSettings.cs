using System;
using UnityEngine;

namespace Stormdrop
{
    public enum GraphicsPreset { Smooth, Standard, Ultra }

    /// <summary>Player-facing settings. Saved locally and synced to the backend (/api/me/settings).</summary>
    [Serializable]
    public class GameSettingsData
    {
        public GraphicsPreset graphics = GraphicsPreset.Standard;
        public bool highFrameRate = false;      // 60 FPS (Standard/Ultra only)
        public float lookSensitivity = 1f;
        public float scopeSensitivity = 0.6f;
        public bool gyroscopeAim = false;
        public float gyroSensitivity = 1f;
        public bool autoFire = false;            // fire automatically when the crosshair is on an enemy
        public bool aimAssist = true;
        public float masterVolume = 1f;
        public bool voiceChat = true;
        public bool quickChatOnly = false;
        public string hudLayoutJson = "";        // written by HudLayoutEditor
        public string language = "en";
    }

    public static class GameSettings
    {
        const string Key = "stormdrop.settings";
        static GameSettingsData _data;
        public static event Action Changed;

        public static GameSettingsData Data
        {
            get
            {
                if (_data == null)
                {
                    var json = PlayerPrefs.GetString(Key, "");
                    _data = string.IsNullOrEmpty(json) ? DefaultsForDevice() : JsonUtility.FromJson<GameSettingsData>(json);
                }
                return _data;
            }
        }

        /// <summary>Picks Smooth on 2–3 GB phones so low-end devices hold 30 FPS.</summary>
        static GameSettingsData DefaultsForDevice()
        {
            var d = new GameSettingsData();
            int ram = SystemInfo.systemMemorySize;
            d.graphics = ram <= 3072 ? GraphicsPreset.Smooth : ram <= 6144 ? GraphicsPreset.Standard : GraphicsPreset.Ultra;
            d.highFrameRate = ram > 4096;
            return d;
        }

        public static void Save()
        {
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(Data));
            PlayerPrefs.Save();
            Apply();
            Changed?.Invoke();
        }

        public static void Load(GameSettingsData d)
        {
            if (d == null) return;
            _data = d;
            Save();
        }

        static int _nativeW, _nativeH;

        public static void Apply()
        {
            var d = Data;
            if (_nativeW == 0) { _nativeW = Screen.currentResolution.width; _nativeH = Screen.currentResolution.height; }

            // Map presets onto the project's quality levels: lowest, middle, highest.
            int count = QualitySettings.names.Length;
            int level = d.graphics == GraphicsPreset.Smooth ? 0 : d.graphics == GraphicsPreset.Ultra ? count - 1 : count / 2;
            QualitySettings.SetQualityLevel(level, true);
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = d.graphics == GraphicsPreset.Smooth ? 30 : d.highFrameRate ? 60 : 40;

            // Render-resolution scale: biggest single win on low-end GPUs.
            float scale = d.graphics switch { GraphicsPreset.Smooth => 0.65f, GraphicsPreset.Standard => 0.8f, _ => 1f };
#if UNITY_ANDROID || UNITY_IOS
            if (_nativeW > 0) Screen.SetResolution(Mathf.RoundToInt(_nativeW * scale), Mathf.RoundToInt(_nativeH * scale), true);
#endif
            QualitySettings.shadows = d.graphics == GraphicsPreset.Smooth ? ShadowQuality.Disable : ShadowQuality.HardOnly;
            QualitySettings.shadowDistance = d.graphics == GraphicsPreset.Ultra ? 80 : 40;
            QualitySettings.lodBias = d.graphics switch { GraphicsPreset.Smooth => 0.5f, GraphicsPreset.Standard => 0.9f, _ => 1.5f };
            AudioListener.volume = d.masterVolume;
#if ENABLE_LEGACY_INPUT_MANAGER
            Input.gyro.enabled = d.gyroscopeAim && SystemInfo.supportsGyroscope;
#endif
        }
    }
}
