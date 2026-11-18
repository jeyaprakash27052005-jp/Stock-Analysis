using UnityEngine;

namespace Stormdrop
{
    /// <summary>
    /// Entry point in the Bootstrap scene. Decides whether this process is a dedicated
    /// server (Linux server build or "-server" argument) or a phone client.
    /// </summary>
    public class GameBootstrap : MonoBehaviour
    {
        [Tooltip("Base URL of the backend, e.g. https://your-project.vercel.app/api")]
        public string apiBaseUrl = "https://your-project.vercel.app/api";
        public GameDatabase database;

        public static GameBootstrap Instance { get; private set; }
        public static bool IsDedicatedServer { get; private set; }

        void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            GameDatabase.Instance = database != null ? database : Resources.Load<GameDatabase>("GameDatabase");
            ApiClient.BaseUrl = CommandLine.Get("-api", apiBaseUrl);

#if UNITY_SERVER
            IsDedicatedServer = true;
#else
            IsDedicatedServer = CommandLine.Has("-server");
#endif
            var net = gameObject.AddComponent<NetworkBootstrap>();

            if (IsDedicatedServer)
            {
                Application.targetFrameRate = GameConstants.TickRate;
                QualitySettings.vSyncCount = 0;
                AudioListener.volume = 0;
                gameObject.AddComponent<DedicatedServerAgent>().Begin(net);
                return;
            }

            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            GameSettings.Apply();
            AnalyticsService.Initialize();
            PushNotificationService.Initialize();
            PurchaseService.Initialize();
            UIManager.Create();
        }
    }

    public static class CommandLine
    {
        public static bool Has(string flag) => System.Array.IndexOf(System.Environment.GetCommandLineArgs(), flag) >= 0;

        public static string Get(string flag, string fallback)
        {
            var args = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(args, flag);
            if (i >= 0 && i + 1 < args.Length) return args[i + 1];
            var env = System.Environment.GetEnvironmentVariable("STORMDROP_" + flag.TrimStart('-').ToUpperInvariant());
            return string.IsNullOrEmpty(env) ? fallback : env;
        }
    }
}
