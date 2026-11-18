using System;
using System.Threading.Tasks;
using UnityEngine;
#if STORMDROP_GPGS
using GooglePlayGames;
using GooglePlayGames.BasicApi;
#endif
#if STORMDROP_FACEBOOK
using System.Collections.Generic;
using Facebook.Unity;
#endif

namespace Stormdrop
{
    /// <summary>
    /// Guest, Google (Play Games Services v2) and Facebook login. Third-party tokens are sent to the
    /// backend, which verifies them with Google/Facebook and returns our own session token.
    ///
    /// Optional SDKs are behind scripting define symbols so the project compiles without them:
    ///   STORMDROP_GPGS      — Google Play Games plugin for Unity (v11+)
    ///   STORMDROP_FACEBOOK  — Facebook SDK for Unity
    /// </summary>
    public static class AuthService
    {
        const string TokenKey = "stormdrop.session";
        const string DeviceKey = "stormdrop.device";

        public static bool HasSavedSession => !string.IsNullOrEmpty(PlayerPrefs.GetString(TokenKey, ""));

        static string DeviceId
        {
            get
            {
                var id = PlayerPrefs.GetString(DeviceKey, "");
                if (string.IsNullOrEmpty(id))
                {
                    id = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N").Substring(0, 8);
                    PlayerPrefs.SetString(DeviceKey, id);
                    PlayerPrefs.Save();
                }
                return id;
            }
        }

        public static async Task<bool> TryResume()
        {
            ApiClient.SessionToken = PlayerPrefs.GetString(TokenKey, "");
            if (string.IsNullOrEmpty(ApiClient.SessionToken)) return false;
            try { await Backend.RefreshProfile(); return true; }
            catch (ApiException e) when (e.Status == 401) { SignOut(); return false; }
        }

        public static Task SignInGuest() =>
            Complete(ApiClient.Post<AuthResponse>("/auth/guest", new { deviceId = DeviceId, region = GuessRegion() }));

        public static async Task SignInGoogle()
        {
            string code = await GetGoogleServerAuthCode();
            await Complete(ApiClient.Post<AuthResponse>("/auth/google", new { serverAuthCode = code, region = GuessRegion() }));
        }

        public static async Task SignInFacebook()
        {
            string token = await GetFacebookToken();
            await Complete(ApiClient.Post<AuthResponse>("/auth/facebook", new { accessToken = token, region = GuessRegion() }));
        }

        /// <summary>Bind Google/Facebook to the current guest account so progress survives a new phone.</summary>
        public static async Task Link(string provider)
        {
            if (provider == "google") await ApiClient.Post<OkResponse>("/auth/link", new { provider, serverAuthCode = await GetGoogleServerAuthCode() });
            else await ApiClient.Post<OkResponse>("/auth/link", new { provider, accessToken = await GetFacebookToken() });
            await Backend.RefreshProfile();
        }

        public static void SignOut()
        {
            ApiClient.SessionToken = null;
            PlayerPrefs.DeleteKey(TokenKey);
            PlayerPrefs.Save();
#if STORMDROP_FACEBOOK
            if (FB.IsLoggedIn) FB.LogOut();
#endif
        }

        static async Task Complete(Task<AuthResponse> call)
        {
            var r = await call;
            ApiClient.SessionToken = r.token;
            PlayerPrefs.SetString(TokenKey, r.token);
            PlayerPrefs.Save();
            await Backend.RefreshProfile();
            if (Backend.Me.settings != null && Backend.Me.settings.HasValues)
                GameSettings.Load(Backend.Me.settings.ToObject<GameSettingsData>());
            AnalyticsService.SetUser(Backend.Me.id);
            AnalyticsService.Log("login");
        }

        static string GuessRegion()
        {
            // Simple default from the device's time zone; players can change it in Settings.
            double h = TimeZoneInfo.Local.BaseUtcOffset.TotalHours;
            if (h >= 5 && h < 6.5) return "IND";
            if (h >= 6.5 && h <= 9) return "SEA";
            if (h >= 3 && h < 5) return "ME";
            if (h >= -1 && h < 3) return "EU";
            if (h <= -3 && h > -6) return "SA";
            if (h <= -6) return "NA";
            return "IND";
        }

        static Task<string> GetGoogleServerAuthCode()
        {
            var tcs = new TaskCompletionSource<string>();
#if STORMDROP_GPGS
            PlayGamesPlatform.Activate();
            PlayGamesPlatform.Instance.Authenticate(status =>
            {
                if (status != SignInStatus.Success) { PlayGamesPlatform.Instance.ManuallyAuthenticate(s2 => Request(s2)); return; }
                Request(status);
            });
            void Request(SignInStatus s)
            {
                if (s != SignInStatus.Success) { tcs.TrySetException(new Exception("Google sign-in was cancelled")); return; }
                PlayGamesPlatform.Instance.RequestServerSideAccess(false, code =>
                {
                    if (string.IsNullOrEmpty(code)) tcs.TrySetException(new Exception("Google did not return an auth code"));
                    else tcs.TrySetResult(code);
                });
            }
#else
            tcs.SetException(new Exception("Google login is not set up in this build (add STORMDROP_GPGS)."));
#endif
            return tcs.Task;
        }

        static Task<string> GetFacebookToken()
        {
            var tcs = new TaskCompletionSource<string>();
#if STORMDROP_FACEBOOK
            void Login()
            {
                FB.LogInWithReadPermissions(new List<string> { "public_profile" }, result =>
                {
                    if (result == null || result.Cancelled || !string.IsNullOrEmpty(result.Error))
                        tcs.TrySetException(new Exception(result?.Error ?? "Facebook login cancelled"));
                    else tcs.TrySetResult(AccessToken.CurrentAccessToken.TokenString);
                });
            }
            if (FB.IsInitialized) Login(); else FB.Init(() => { FB.ActivateApp(); Login(); });
#else
            tcs.SetException(new Exception("Facebook login is not set up in this build (add STORMDROP_FACEBOOK)."));
#endif
            return tcs.Task;
        }
    }
}
