using UnityEngine;
#if STORMDROP_FIREBASE
using Firebase;
using Firebase.Analytics;
using Firebase.Crashlytics;
using Firebase.Extensions;
#endif

namespace Stormdrop
{
    /// <summary>
    /// Analytics + crash reporting through Firebase (define STORMDROP_FIREBASE after importing
    /// FirebaseAnalytics.unitypackage, FirebaseCrashlytics.unitypackage and google-services.json).
    /// Without Firebase, events are only logged to the console.
    /// </summary>
    public static class AnalyticsService
    {
        static bool _ready;

        public static void Initialize()
        {
#if STORMDROP_FIREBASE
            FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(t =>
            {
                if (t.Result != DependencyStatus.Available) { Debug.LogWarning("Firebase unavailable: " + t.Result); return; }
                FirebaseAnalytics.SetAnalyticsCollectionEnabled(true);
                Crashlytics.ReportUncaughtExceptionsAsFatal = true;
                _ready = true;
            });
#endif
            Application.logMessageReceived += (msg, stack, type) =>
            {
                if (type == LogType.Exception) Log("client_exception", "message", msg.Length > 90 ? msg.Substring(0, 90) : msg);
            };
        }

        public static void SetUser(string playerId)
        {
#if STORMDROP_FIREBASE
            if (_ready) { FirebaseAnalytics.SetUserId(playerId); Crashlytics.SetUserId(playerId); }
#endif
        }

        public static void Log(string evt, string key = null, string value = null)
        {
#if STORMDROP_FIREBASE
            if (_ready)
            {
                if (key == null) FirebaseAnalytics.LogEvent(evt);
                else FirebaseAnalytics.LogEvent(evt, key, value);
                return;
            }
#endif
            if (Debug.isDebugBuild) Debug.Log($"[analytics] {evt} {key}={value}");
        }

        public static void LogMatchEnd(MatchResultMessage m)
        {
#if STORMDROP_FIREBASE
            if (_ready)
            {
                FirebaseAnalytics.LogEvent("match_end",
                    new Parameter("mode", m.mode), new Parameter("placement", m.placement),
                    new Parameter("kills", m.kills), new Parameter("won", m.won ? 1 : 0));
                return;
            }
#endif
            Log("match_end", "placement", m.placement.ToString());
        }
    }
}
