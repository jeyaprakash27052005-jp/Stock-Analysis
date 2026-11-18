using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Stormdrop
{
    /// <summary>
    /// Owns the menu canvas and screen stack:
    /// Splash → Login → Lobby → Mode select → Matchmaking → (match HUD) → Results,
    /// plus Store, Vault, Pass, Guild and Settings. Also toasts, popups and the loading spinner.
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        public static UIManager Instance { get; private set; }

        public Canvas Canvas { get; private set; }
        readonly Dictionary<Type, UIScreen> _screens = new Dictionary<Type, UIScreen>();
        readonly Stack<UIScreen> _stack = new Stack<UIScreen>();
        GameObject _loading;
        Text _toast;
        CanvasGroup _toastGroup;
        RectTransform _popupLayer;
        HUDController _hud;

        // Shared state between screens
        public GameMode SelectedMode = GameMode.BrSquad;
        public bool Ranked;
        public MatchResultMessage? LastResult;

        public static void Create()
        {
            if (Instance != null) return;
            var go = new GameObject("UIManager");
            DontDestroyOnLoad(go);
            go.AddComponent<UIManager>();
        }

        void Awake()
        {
            Instance = this;
            Canvas = UIFactory.CreateCanvas("MenuCanvas", 10);
            Add<SplashScreen>(); Add<LoginScreen>(); Add<LobbyScreen>(); Add<ModeSelectScreen>(); Add<MatchmakingScreen>();
            Add<ResultsScreen>(); Add<StoreScreen>(); Add<VaultScreen>(); Add<PassScreen>(); Add<GuildScreen>(); Add<SettingsScreen>();

            _popupLayer = UIFactory.Rect(Canvas.transform, "Popups").Place(0, 0, 1, 1);
            BuildLoading();
            BuildToast();

            PlayerIdentity.LocalSpawned += OnLocalPlayerSpawned;
            Show<SplashScreen>(clearStack: true);
        }

        void Start()
        {
            var net = NetworkBootstrap.Instance;
            if (net != null) net.ClientDisconnected += reason => OnMatchLeft(reason);
        }

        void Add<T>() where T : UIScreen
        {
            var root = UIFactory.Image(Canvas.transform, UIFactory.Bg, typeof(T).Name).rectTransform.Place(0, 0, 1, 1);
            root.gameObject.SetActive(false);
            var s = root.gameObject.AddComponent<T>();
            s.Init(root);
            _screens[typeof(T)] = s;
        }

        public T Get<T>() where T : UIScreen => (T)_screens[typeof(T)];

        public void Show<T>(bool clearStack = false) where T : UIScreen
        {
            if (_stack.Count > 0) _stack.Peek().Hide();
            if (clearStack) { while (_stack.Count > 0) _stack.Pop().Hide(); }
            var s = Get<T>();
            _stack.Push(s);
            s.Show();
        }

        public void Back()
        {
            if (_stack.Count <= 1) return;
            _stack.Pop().Hide();
            _stack.Peek().Show();
        }

        public void HideAllMenus()
        {
            while (_stack.Count > 0) _stack.Pop().Hide();
        }

        // ------------------------------------------------------------------ match lifecycle
        void OnLocalPlayerSpawned(PlayerIdentity me)
        {
            HideAllMenus();
            if (_hud == null) _hud = HUDController.Create();
            _hud.gameObject.SetActive(true);
            _hud.Bind(me);
        }

        public void OnMatchResults(MatchResultMessage msg)
        {
            LastResult = msg;
            if (_hud != null) _hud.ShowEliminatedBanner(msg);
            StartCoroutine(ShowResultsSoon());
        }

        IEnumerator ShowResultsSoon()
        {
            yield return new WaitForSeconds(3f);
            if (_hud != null) _hud.gameObject.SetActive(false);
            Show<ResultsScreen>(clearStack: true);
        }

        void OnMatchLeft(string reason)
        {
            if (_hud != null) _hud.gameObject.SetActive(false);
            if (LastResult == null && reason != null && Backend.Me != null)
            {
                Show<LobbyScreen>(clearStack: true);
                Toast(reason);
            }
        }

        public void LeaveMatchToLobby()
        {
            LastResult = null;
            _ = VoiceChatService.Leave();
            NetworkBootstrap.Instance.Leave();
            if (_hud != null) _hud.gameObject.SetActive(false);
            Show<LobbyScreen>(clearStack: true);
        }

        // ------------------------------------------------------------------ shared widgets
        public void CurrencyBar(RectTransform parent)
        {
            var box = UIFactory.Rect(parent, "Currency").Place(1, 0, 1, 1, -520, 18, 20, 18);
            UIFactory.HStack(box, 12);
            var gold = UIFactory.Button(box, "", () => { }, new Color(0, 0, 0, 0.4f), 28);
            var dia = UIFactory.Button(box, "", () => Show<StoreScreen>(), new Color(0, 0, 0, 0.4f), 28);
            void Refresh()
            {
                if (Backend.Me == null || gold == null) return;
                gold.SetLabel($"<color=#FFD040>●</color> {UIFactory.Money(Backend.Me.gold)}");
                dia.SetLabel($"<color=#8CD9FF>◆</color> {UIFactory.Money(Backend.Me.diamonds)}  +");
            }
            gold.GetComponentInChildren<Text>().supportRichText = true;
            dia.GetComponentInChildren<Text>().supportRichText = true;
            Backend.ProfileChanged += Refresh;
            Refresh();
        }

        public void SetLoading(bool on) => _loading.SetActive(on);

        public void Toast(string message)
        {
            StopCoroutine(nameof(ToastRoutine));
            _toast.text = message;
            StartCoroutine(nameof(ToastRoutine));
        }

        IEnumerator ToastRoutine()
        {
            _toastGroup.gameObject.SetActive(true);
            _toastGroup.alpha = 1;
            yield return new WaitForSeconds(3f);
            for (float a = 1; a > 0; a -= Time.deltaTime * 2) { _toastGroup.alpha = a; yield return null; }
            _toastGroup.gameObject.SetActive(false);
        }

        /// <summary>Modal popup; fill content with the callback. Returns a close action.</summary>
        public Action Popup(string title, Action<RectTransform> fill, float width = 1200, float height = 800)
        {
            var dim = UIFactory.Image(_popupLayer, new Color(0, 0, 0, 0.65f), "Popup");
            dim.rectTransform.Place(0, 0, 1, 1);
            var box = UIFactory.Image(dim.transform, UIFactory.Panel, "Box").rectTransform.At(0.5f, 0.5f, 0, 0, width, height);
            UIFactory.Text(box, title, 40, UIFactory.TextMain, TextAnchor.MiddleCenter, FontStyle.Bold).rectTransform.Place(0, 1, 1, 1, 0, -80, 0, 0);
            Action close = () => Destroy(dim.gameObject);
            UIFactory.Button(box, "✕", close, UIFactory.Danger).GetComponent<RectTransform>().At(1, 1, -45, -40, 70, 60);
            var content = UIFactory.Rect(box, "Content").Place(0, 0, 1, 1, 30, 30, 30, 95);
            fill(content);
            return close;
        }

        public void Confirm(string title, string message, string okLabel, Action onOk)
        {
            Action close = null;
            close = Popup(title, c =>
            {
                UIFactory.Text(c, message, 32, UIFactory.TextMain, TextAnchor.MiddleCenter).rectTransform.Place(0, 0.3f, 1, 1);
                UIFactory.Button(c, okLabel, () => { close(); onOk(); }, UIFactory.Accent).GetComponent<RectTransform>().Place(0.55f, 0, 0.95f, 0.22f);
                UIFactory.Button(c, "Cancel", () => close(), UIFactory.PanelLight).GetComponent<RectTransform>().Place(0.05f, 0, 0.45f, 0.22f);
            }, 1000, 520);
        }

        void BuildLoading()
        {
            var img = UIFactory.Image(Canvas.transform, new Color(0, 0, 0, 0.55f), "Loading");
            img.rectTransform.Place(0, 0, 1, 1);
            UIFactory.Text(img.transform, "Loading…", 40, UIFactory.TextMain, TextAnchor.MiddleCenter).rectTransform.Place(0, 0, 1, 1);
            _loading = img.gameObject;
            _loading.SetActive(false);
        }

        void BuildToast()
        {
            var img = UIFactory.Image(Canvas.transform, new Color(0, 0, 0, 0.85f), "Toast");
            img.rectTransform.At(0.5f, 0, 0, 140, 1300, 90);
            img.raycastTarget = false;
            _toastGroup = img.gameObject.AddComponent<CanvasGroup>();
            _toastGroup.blocksRaycasts = false;
            _toast = UIFactory.Text(img.transform, "", 32, UIFactory.TextMain, TextAnchor.MiddleCenter);
            _toast.rectTransform.Place(0, 0, 1, 1, 20, 0, 20, 0);
            img.gameObject.SetActive(false);
        }
    }
}
