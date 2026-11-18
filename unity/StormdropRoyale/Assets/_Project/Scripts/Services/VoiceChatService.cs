using System.Threading.Tasks;
using UnityEngine;
#if STORMDROP_VIVOX
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Vivox;
#endif

namespace Stormdrop
{
    /// <summary>
    /// Team voice chat through Unity Vivox (package com.unity.services.vivox, define STORMDROP_VIVOX).
    /// Each team gets its own channel per match: "m_{matchId}_t{team}". Quick chat (text lines)
    /// works without Vivox — see PingSystem.
    /// </summary>
    public static class VoiceChatService
    {
        static string _channel;
        public static bool MicOn { get; private set; }

        public static async Task JoinTeamChannel(string matchId, int team)
        {
            if (!GameSettings.Data.voiceChat) return;
#if STORMDROP_VIVOX
            try
            {
                if (UnityServices.State != ServicesInitializationState.Initialized) await UnityServices.InitializeAsync();
                if (!AuthenticationService.Instance.IsSignedIn) await AuthenticationService.Instance.SignInAnonymouslyAsync();
                await VivoxService.Instance.InitializeAsync();
                if (!VivoxService.Instance.IsLoggedIn) await VivoxService.Instance.LoginAsync();
                _channel = $"m_{matchId}_t{team}";
                await VivoxService.Instance.JoinGroupChannelAsync(_channel, ChatCapability.AudioOnly);
                SetMic(false);
            }
            catch (System.Exception e) { Debug.LogWarning("Voice chat unavailable: " + e.Message); }
#else
            await Task.CompletedTask;
#endif
        }

        public static void SetMic(bool on)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            // Ask for the microphone only when the player actually turns it on (Play policy best practice).
            if (on && !UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone))
            {
                UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Microphone);
                on = false;
            }
#endif
            MicOn = on;
#if STORMDROP_VIVOX
            if (on) VivoxService.Instance.UnmuteInputDevice(); else VivoxService.Instance.MuteInputDevice();
#endif
        }

        public static async Task Leave()
        {
#if STORMDROP_VIVOX
            if (_channel != null) try { await VivoxService.Instance.LeaveChannelAsync(_channel); } catch { }
#endif
            _channel = null;
            await Task.CompletedTask;
        }
    }
}
