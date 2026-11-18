#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

namespace Stormdrop.EditorTools
{
    /// <summary>
    /// Release builds:
    ///   Stormdrop ▸ 2. Build Settings…              package name, version, keystore, optional SDK defines
    ///   Stormdrop ▸ 3. Build Android App Bundle     signed .aab for Google Play (IL2CPP, ARM64 + ARMv7)
    ///   Stormdrop ▸ 4. Build Dedicated Server        Linux headless server for matches
    ///   Stormdrop ▸ 5. Build iOS (Xcode project)
    /// Keystore passwords are never saved: they come from the window (session only) or the
    /// environment variables STORMDROP_KEYSTORE_PASS and STORMDROP_KEY_PASS (for CI).
    /// </summary>
    public class StormdropBuildTool : EditorWindow
    {
        const string P = "Stormdrop.Build.";
        static string PackageName { get => EditorPrefs.GetString(P + "package", "com.yourstudio.stormdrop"); set => EditorPrefs.SetString(P + "package", value); }
        static string Version { get => EditorPrefs.GetString(P + "version", GameConstants.Version); set => EditorPrefs.SetString(P + "version", value); }
        static int VersionCode { get => EditorPrefs.GetInt(P + "code", 1); set => EditorPrefs.SetInt(P + "code", value); }
        static string KeystorePath { get => EditorPrefs.GetString(P + "keystore", ""); set => EditorPrefs.SetString(P + "keystore", value); }
        static string KeyAlias { get => EditorPrefs.GetString(P + "alias", "stormdrop"); set => EditorPrefs.SetString(P + "alias", value); }
        static string _keystorePass = "", _keyPass = "";

        static readonly string[] OptionalDefines = { "STORMDROP_GPGS", "STORMDROP_FACEBOOK", "STORMDROP_IAP", "STORMDROP_FIREBASE", "STORMDROP_VIVOX" };
        static readonly string[] DefineHelp =
        {
            "Google Play Games plugin installed (Google login)",
            "Facebook SDK for Unity installed",
            "Unity In-App Purchasing installed (Diamond top-ups)",
            "Firebase Analytics + Crashlytics + Messaging imported",
            "Unity Vivox installed (voice chat)",
        };

        [MenuItem("Stormdrop/2. Build Settings…", priority = 20)]
        static void Open() => GetWindow<StormdropBuildTool>("Stormdrop Build");

        void OnGUI()
        {
            EditorGUILayout.LabelField("Store identity", EditorStyles.boldLabel);
            PackageName = EditorGUILayout.TextField("Package name", PackageName);
            Version = EditorGUILayout.TextField("Version name", Version);
            VersionCode = EditorGUILayout.IntField("Version code", VersionCode);
            EditorGUILayout.HelpBox("Version code must go up by at least 1 for every upload to Google Play. The package name can never change after the first upload.", MessageType.Info);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Upload key (keep the .keystore file and passwords safe — never commit them)", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                KeystorePath = EditorGUILayout.TextField("Keystore file", KeystorePath);
                if (GUILayout.Button("…", GUILayout.Width(30)))
                {
                    var p = EditorUtility.OpenFilePanel("Keystore", "", "keystore,jks");
                    if (!string.IsNullOrEmpty(p)) KeystorePath = p;
                }
            }
            KeyAlias = EditorGUILayout.TextField("Key alias", KeyAlias);
            _keystorePass = EditorGUILayout.PasswordField("Keystore password", _keystorePass);
            _keyPass = EditorGUILayout.PasswordField("Key password", _keyPass);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Optional SDKs (tick only after importing the SDK)", EditorStyles.boldLabel);
            var defines = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Android).Split(';').Where(d => d.Length > 0).ToList();
            bool changed = false;
            for (int i = 0; i < OptionalDefines.Length; i++)
            {
                bool on = defines.Contains(OptionalDefines[i]);
                bool now = EditorGUILayout.ToggleLeft($"{OptionalDefines[i]}  —  {DefineHelp[i]}", on);
                if (now == on) continue;
                changed = true;
                if (now) defines.Add(OptionalDefines[i]); else defines.Remove(OptionalDefines[i]);
            }
            if (changed)
            {
                var joined = string.Join(";", defines);
                PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.Android, joined);
                PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.iOS, joined);
            }

            EditorGUILayout.Space();
            if (GUILayout.Button("Apply Player Settings", GUILayout.Height(28))) ApplyPlayerSettings();
            if (GUILayout.Button("Build Android App Bundle (.aab)", GUILayout.Height(36))) BuildAndroid();
            if (GUILayout.Button("Build Dedicated Server (Linux)", GUILayout.Height(28))) BuildServer();
        }

        // ------------------------------------------------------------------ settings
        public static void ApplyPlayerSettings()
        {
            PlayerSettings.companyName = PlayerSettings.companyName == "DefaultCompany" ? "Your Studio" : PlayerSettings.companyName;
            PlayerSettings.productName = GameConstants.GameName;
            PlayerSettings.bundleVersion = Version;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, PackageName);
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, PackageName);

            // Landscape only
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.runInBackground = false;

            // Android: Google Play requires 64-bit and a recent target API level
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64 | AndroidArchitecture.ARMv7;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;   // = highest installed SDK
            PlayerSettings.Android.bundleVersionCode = VersionCode;
            PlayerSettings.Android.forceInternetPermission = true;
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Android, ManagedStrippingLevel.Low);
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan, GraphicsDeviceType.OpenGLES3 });
            PlayerSettings.SetIl2CppCodeGeneration(NamedBuildTarget.Android, Il2CppCodeGeneration.OptimizeSize);
            PlayerSettings.stripEngineCode = true;

            // iOS
            PlayerSettings.iOS.targetOSVersionString = "13.0";
            PlayerSettings.iOS.buildNumber = VersionCode.ToString();
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.iOS, ScriptingImplementation.IL2CPP);
            Debug.Log($"[Stormdrop] Player settings applied for {PackageName} {Version} ({VersionCode})");
        }

        static string[] Scenes() => EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();

        static bool Check()
        {
            if (Scenes().Length < 2) { EditorUtility.DisplayDialog("Stormdrop", "Run Stormdrop ▸ 1. Run Setup Wizard first.", "OK"); return false; }
            var boot = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Player.prefab");
            if (boot == null) { EditorUtility.DisplayDialog("Stormdrop", "Player prefab missing — run the setup wizard.", "OK"); return false; }
            return true;
        }

        // ------------------------------------------------------------------ builds
        [MenuItem("Stormdrop/3. Build Android App Bundle (Play Store)", priority = 21)]
        public static void BuildAndroid()
        {
            if (!Check()) return;
            ApplyPlayerSettings();
            string ksPass = !string.IsNullOrEmpty(_keystorePass) ? _keystorePass : Environment.GetEnvironmentVariable("STORMDROP_KEYSTORE_PASS");
            string keyPass = !string.IsNullOrEmpty(_keyPass) ? _keyPass : Environment.GetEnvironmentVariable("STORMDROP_KEY_PASS");
            if (string.IsNullOrEmpty(KeystorePath) || !File.Exists(KeystorePath) || string.IsNullOrEmpty(ksPass))
            {
                EditorUtility.DisplayDialog("Stormdrop", "Set your upload keystore and passwords in Stormdrop ▸ 2. Build Settings… first.\n" +
                    "See play-store/RELEASE_CHECKLIST.md → 'Create the upload key'.", "OK");
                Open();
                return;
            }
            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystoreName = KeystorePath;
            PlayerSettings.Android.keystorePass = ksPass;
            PlayerSettings.Android.keyaliasName = KeyAlias;
            PlayerSettings.Android.keyaliasPass = string.IsNullOrEmpty(keyPass) ? ksPass : keyPass;
            EditorUserBuildSettings.buildAppBundle = true;
            EditorUserBuildSettings.development = false;

            Directory.CreateDirectory("Builds/Android");
            string path = $"Builds/Android/stormdrop-{Version}-{VersionCode}.aab";
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = Scenes(), locationPathName = path, target = BuildTarget.Android, options = BuildOptions.None,
            });
            Finish(report, path);
            if (report.summary.result == BuildResult.Succeeded) VersionCode++;   // ready for the next upload
        }

        /// <summary>
        /// CI entry point (GitHub Actions / game-ci): runs the setup wizard if the content doesn't exist yet,
        /// then builds an installable APK to build/Android/stormdrop.apk. Signed with the release keystore when
        /// STORMDROP_KEYSTORE_BASE64 / STORMDROP_KEYSTORE_PASS / STORMDROP_KEY_ALIAS are set, otherwise with the debug key.
        /// </summary>
        public static void BuildApkCI()
        {
            if (AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/_Project/Resources/GameDatabase.asset") == null || Scenes().Length < 2)
                StormdropSetupWizard.Run();
            var api = Environment.GetEnvironmentVariable("STORMDROP_API");
            if (!string.IsNullOrEmpty(api))
            {
                var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(Scenes()[0]);
                var boot = UnityEngine.Object.FindFirstObjectByType<GameBootstrap>();
                if (boot != null) { boot.apiBaseUrl = api; EditorUtility.SetDirty(boot); UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene); }
            }
            var code = Environment.GetEnvironmentVariable("STORMDROP_VERSION_CODE");
            if (int.TryParse(code, out var vc)) VersionCode = vc;
            ApplyPlayerSettings();

            var ks64 = Environment.GetEnvironmentVariable("STORMDROP_KEYSTORE_BASE64");
            if (!string.IsNullOrEmpty(ks64))
            {
                var ksPath = Path.GetFullPath("upload.keystore");
                File.WriteAllBytes(ksPath, Convert.FromBase64String(ks64));
                var pass = Environment.GetEnvironmentVariable("STORMDROP_KEYSTORE_PASS");
                PlayerSettings.Android.useCustomKeystore = true;
                PlayerSettings.Android.keystoreName = ksPath;
                PlayerSettings.Android.keystorePass = pass;
                PlayerSettings.Android.keyaliasName = Environment.GetEnvironmentVariable("STORMDROP_KEY_ALIAS") ?? "stormdrop";
                PlayerSettings.Android.keyaliasPass = Environment.GetEnvironmentVariable("STORMDROP_KEY_PASS") ?? pass;
            }
            else PlayerSettings.Android.useCustomKeystore = false;

            EditorUserBuildSettings.buildAppBundle = Environment.GetEnvironmentVariable("STORMDROP_AAB") == "1";
            Directory.CreateDirectory("build/Android");
            string path = EditorUserBuildSettings.buildAppBundle ? "build/Android/stormdrop.aab" : "build/Android/stormdrop.apk";
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = Scenes(), locationPathName = path, target = BuildTarget.Android });
            Debug.Log($"[Stormdrop] CI build {report.summary.result}: {path}");
            if (Application.isBatchMode) EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
        }

        [MenuItem("Stormdrop/4. Build Dedicated Server (Linux)", priority = 22)]
        public static void BuildServer()
        {
            if (!Check()) return;
            Directory.CreateDirectory("Builds/Server");
            string path = "Builds/Server/StormdropServer.x86_64";
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = Scenes(), locationPathName = path, target = BuildTarget.StandaloneLinux64,
                subtarget = (int)StandaloneBuildSubtarget.Server, options = BuildOptions.None,
            });
            Finish(report, path);
        }

        [MenuItem("Stormdrop/5. Build iOS (Xcode project)", priority = 23)]
        public static void BuildIOS()
        {
            if (!Check()) return;
            ApplyPlayerSettings();
            Directory.CreateDirectory("Builds/iOS");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = Scenes(), locationPathName = "Builds/iOS", target = BuildTarget.iOS, options = BuildOptions.None,
            });
            Finish(report, "Builds/iOS");
        }

        static void Finish(BuildReport report, string path)
        {
            var s = report.summary;
            string msg = s.result == BuildResult.Succeeded
                ? $"Build succeeded: {path}\nSize: {s.totalSize / (1024 * 1024)} MB, {s.totalTime.TotalMinutes:0.0} min"
                : $"Build {s.result}. {s.totalErrors} errors — see the Console.";
            Debug.Log("[Stormdrop] " + msg);
            if (!Application.isBatchMode) EditorUtility.DisplayDialog("Stormdrop", msg, "OK");
            if (s.result == BuildResult.Succeeded) EditorUtility.RevealInFinder(path);
        }
    }
}
#endif
