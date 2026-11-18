using UnityEngine;
#if STORMDROP_FIREBASE
using Firebase.Messaging;
#endif

namespace Stormdrop
{
    /// <summary>
    /// Push notifications via Firebase Cloud Messaging (define STORMDROP_FIREBASE).
    /// On Android 13+ the POST_NOTIFICATIONS permission is requested first.
    /// The device token is sent to the backend (push_tokens table) once the player is signed in.
    /// </summary>
    public static class PushNotificationService
    {
        static string _pendingToken;

        public static void Initialize()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            const string perm = "android.permission.POST_NOTIFICATIONS";
            if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(perm))
                UnityEngine.Android.Permission.RequestUserPermission(perm);
#endif
#if STORMDROP_FIREBASE
            FirebaseMessaging.TokenReceived += (_, e) => { _pendingToken = e.Token; TrySend(); };
            FirebaseMessaging.MessageReceived += (_, e) => Debug.Log("Push: " + e.Message?.Notification?.Title);
#endif
            Backend.ProfileChanged += TrySend;
        }

        static async void TrySend()
        {
            if (string.IsNullOrEmpty(_pendingToken) || string.IsNullOrEmpty(ApiClient.SessionToken)) return;
            var token = _pendingToken;
            _pendingToken = null;
            try { await Backend.RegisterPushToken(token, Application.platform == RuntimePlatform.IPhonePlayer ? "ios" : "android"); }
            catch { _pendingToken = token; }
        }
    }
}
