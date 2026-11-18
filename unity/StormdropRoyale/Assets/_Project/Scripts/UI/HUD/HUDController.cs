using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Stormdrop
{
    /// <summary>
    /// In-match HUD: joystick, look pad, fire/aim/jump/crouch/prone/reload/interact/skill buttons,
    /// HP/energy/armor, ammo, weapon slots, minimap, zone timer, kill feed, loot list, pings,
    /// quick chat, hit markers, damage direction and knocked/revive progress.
    /// </summary>
    public class HUDController : MonoBehaviour
    {
        public static HUDController Instance { get; private set; }
        public readonly List<HudButton> Buttons = new List<HudButton>();

        Canvas _canvas;
        RectTransform _root;
        PlayerIdentity _me;
        VirtualJoystick _joystick;
        MinimapController _minimap;
        RectTransform _minimapRect;

        Text _top, _location, _ammo, _armor, _banner, _killFeed, _progressLabel, _quickChatLog;
        Image _hpFill, _energyFill, _shieldFill, _progressFill, _hitMarker, _damageArrow, _flash, _crosshair;
        RectTransform _progressBar, _lootList, _quickChatPanel, _editorBar;
        readonly Button[] _slotButtons = new Button[4];
        HudButton _fire, _fireLeft, _aim, _jump, _crouch, _prone, _reload, _interact, _skill, _heal, _shroom, _grenade, _wall, _ping, _chat, _mic, _map;
        readonly Dictionary<string, Vector2> _defaults = new Dictionary<string, Vector2>();
        readonly Queue<string> _feed = new Queue<string>();
        float _hitMarkerUntil, _damageUntil, _flashUntil, _bannerUntil, _refresh;
        System.Action _interactAction;
        MatchManager _boundMatch;

        public static HUDController Create()
        {
            var go = new GameObject("HUD");
            DontDestroyOnLoad(go);
            return go.AddComponent<HUDController>();
        }

        void Awake()
        {
            Instance = this;
            _canvas = UIFactory.CreateCanvas("HUDCanvas", 5);
            _canvas.transform.SetParent(transform, false);
            _root = (RectTransform)_canvas.transform;
            Build();
            HudLayoutEditor.Apply(Buttons);
        }

        void OnEnable() { if (_canvas) _canvas.gameObject.SetActive(true); }
        void OnDisable() { if (_canvas) _canvas.gameObject.SetActive(false); }

        // ------------------------------------------------------------------ build
        void Build()
        {
            // Look pad covers the right 60% of the screen (behind all buttons)
            var look = UIFactory.Image(_root, new Color(0, 0, 0, 0), "LookPad");
            look.rectTransform.Place(0.4f, 0, 1, 1);
            look.gameObject.AddComponent<TouchLookPad>();

            // Joystick zone: bottom-left
            var joyZone = UIFactory.Image(_root, new Color(0, 0, 0, 0), "JoystickZone");
            joyZone.rectTransform.Place(0, 0, 0.4f, 0.65f);
            var ring = UIFactory.Image(joyZone.transform, new Color(1, 1, 1, 0.15f), "Ring").rectTransform.At(0, 0, 260, 260, 240, 240);
            var knob = UIFactory.Image(joyZone.transform, new Color(1, 1, 1, 0.45f), "Knob").rectTransform.At(0, 0, 260, 260, 100, 100);
            ring.anchorMin = ring.anchorMax = knob.anchorMin = knob.anchorMax = new Vector2(0.5f, 0.5f);
            ring.anchoredPosition = knob.anchoredPosition = new Vector2(-120, -60);
            ring.GetComponent<Image>().raycastTarget = knob.GetComponent<Image>().raycastTarget = false;
            _joystick = joyZone.gameObject.AddComponent<VirtualJoystick>();
            _joystick.ring = ring; _joystick.knob = knob;
            var lockIcon = UIFactory.Image(joyZone.transform, UIFactory.Accent, "SprintLock");
            lockIcon.rectTransform.At(0.5f, 1, -120, -30, 60, 20);
            lockIcon.enabled = false;
            _joystick.sprintLockIcon = lockIcon;

            // Buttons (id, label, anchor, position, size)
            _fire = Btn("fire", "FIRE", 1, 0, -250, 260, 210, new Color(0.9f, 0.3f, 0.2f), true);
            _fire.onDown = () => SetFire(true); _fire.onUp = () => SetFire(false);
            _fireLeft = Btn("fireLeft", "FIRE", 0, 0.5f, 140, 120, 130, new Color(0.9f, 0.3f, 0.2f), true);
            _fireLeft.onDown = () => SetFire(true); _fireLeft.onUp = () => SetFire(false);
            _aim = Btn("aim", "AIM", 1, 0, -480, 330, 130, null); _aim.onDown = () => Input().PressAim();
            _jump = Btn("jump", "JUMP", 1, 0, -110, 470, 130, null); _jump.onDown = () => { Input().PressJump(); Input().PressParachute(); };
            _crouch = Btn("crouch", "CROUCH", 1, 0, -110, 120, 130, null); _crouch.onDown = () => Input().PressCrouch();
            _prone = Btn("prone", "PRONE", 1, 0, -270, 70, 130, null); _prone.onDown = () => Input().PressProne();
            _reload = Btn("reload", "RELOAD", 1, 0, -440, 110, 130, null); _reload.onDown = () => Input().PressReload();
            _skill = Btn("skill", "SKILL", 1, 0, -600, 200, 130, UIFactory.Accent2); _skill.onDown = () => _me?.Skills?.RequestUse();
            _heal = Btn("heal", "MED", 0.5f, 0, 330, 240, 120, UIFactory.Good); _heal.onDown = () => UseItem("med_kit");
            _shroom = Btn("shroom", "VIGOR", 0.5f, 0, 470, 240, 120, new Color(0.6f, 0.4f, 0.9f)); _shroom.onDown = () => UseItem("vigor_shroom");
            _grenade = Btn("grenade", "NADE", 1, 0, -620, 60, 120, null); _grenade.onDown = ThrowGrenade;
            _wall = Btn("wall", "WALL", 1, 0, -760, 60, 120, new Color(0.3f, 0.7f, 1f)); _wall.onDown = DeployWall;
            _interact = Btn("interact", "", 0.5f, 0.5f, 330, -60, 260, 100, UIFactory.Accent); _interact.onDown = () => _interactAction?.Invoke();
            _ping = Btn("ping", "PING", 1, 1, -460, -90, 110, 90, null); _ping.onDown = Ping;
            _chat = Btn("chat", "CHAT", 1, 1, -580, -90, 110, 90, null); _chat.onDown = () => _quickChatPanel.gameObject.SetActive(!_quickChatPanel.gameObject.activeSelf);
            _mic = Btn("mic", "MIC OFF", 1, 1, -700, -90, 120, 90, null);
            _mic.onDown = () => { VoiceChatService.SetMic(!VoiceChatService.MicOn); _mic.label.text = VoiceChatService.MicOn ? "MIC ON" : "MIC OFF"; };

            // Minimap (top-right)
            var mm = UIFactory.Image(_root, new Color(0, 0, 0, 0.6f), "Minimap");
            _minimapRect = mm.rectTransform.At(1, 1, -170, -170, 300, 300);
            var raw = UIFactory.Rect(mm.transform, "Map").Place(0, 0, 1, 1, 4, 4, 4, 4).gameObject.AddComponent<RawImage>();
            raw.raycastTarget = false;
            var markers = UIFactory.Rect(mm.transform, "Markers").Place(0, 0, 1, 1, 4, 4, 4, 4);
            mm.gameObject.AddComponent<RectMask2D>();
            _minimap = mm.gameObject.AddComponent<MinimapController>();
            _minimap.mapImage = raw; _minimap.markerLayer = markers;
            _map = Btn("map", "MAP", 1, 1, -170, -350, 140, 60, null);
            _map.onDown = ToggleFullMap;

            // Top info
            _top = UIFactory.Text(_root, "", 30, UIFactory.TextMain, TextAnchor.MiddleCenter, FontStyle.Bold);
            _top.rectTransform.Place(0.3f, 1, 0.7f, 1, 0, -70, 0, 10);
            _location = UIFactory.Text(_root, "", 24, UIFactory.TextDim, TextAnchor.MiddleCenter);
            _location.rectTransform.Place(0.3f, 1, 0.7f, 1, 0, -110, 0, 70);
            _killFeed = UIFactory.Text(_root, "", 24, UIFactory.TextMain, TextAnchor.UpperLeft);
            _killFeed.rectTransform.Place(0, 1, 0.3f, 1, 20, -260, 0, 20);

            // Health / energy / shield / armor (bottom centre)
            var bars = UIFactory.Rect(_root, "Bars").At(0.5f, 0, 0, 120, 620, 70);
            _hpFill = Bar(bars, "HP", 0.45f, 1f, new Color(0.95f, 0.95f, 0.95f));
            _shieldFill = Bar(bars, "Shield", 0.45f, 1f, new Color(0.4f, 0.8f, 1f, 0.8f));
            _energyFill = Bar(bars, "Energy", 0f, 0.35f, new Color(0.7f, 0.5f, 1f));
            _armor = UIFactory.Text(bars, "", 22, UIFactory.TextDim, TextAnchor.MiddleLeft);
            _armor.rectTransform.Place(0, 1, 1, 1, 0, 0, 0, -34);
            _ammo = UIFactory.Text(_root, "", 44, UIFactory.TextMain, TextAnchor.MiddleRight, FontStyle.Bold);
            _ammo.rectTransform.At(1, 0, -470, 470, 300, 60);

            // Weapon slots
            var slots = UIFactory.Rect(_root, "Slots").At(0.5f, 0, 0, 40, 800, 70);
            UIFactory.HStack(slots, 8);
            for (int i = 0; i < 4; i++)
            {
                byte s = (byte)i;
                _slotButtons[i] = UIFactory.Button(slots, "", () => _me?.Inventory.SwitchSlotRpc(s), new Color(0, 0, 0, 0.5f), 22);
            }

            // Centre overlays
            _crosshair = UIFactory.Image(_root, new Color(1, 1, 1, 0.85f), "Crosshair");
            _crosshair.rectTransform.At(0.5f, 0.5f, 0, 0, 8, 8);
            _crosshair.raycastTarget = false;
            _hitMarker = UIFactory.Image(_root, Color.white, "HitMarker");
            _hitMarker.rectTransform.At(0.5f, 0.5f, 0, 0, 34, 34);
            _hitMarker.rectTransform.localRotation = Quaternion.Euler(0, 0, 45);
            _hitMarker.raycastTarget = false;
            _damageArrow = UIFactory.Image(_root, new Color(1, 0.2f, 0.2f, 0.8f), "DamageArrow");
            _damageArrow.rectTransform.At(0.5f, 0.5f, 0, 0, 30, 120);
            _damageArrow.raycastTarget = false;
            _banner = UIFactory.Text(_root, "", 64, UIFactory.Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
            _banner.rectTransform.Place(0, 0.62f, 1, 0.78f);
            _progressBar = UIFactory.Image(_root, new Color(0, 0, 0, 0.6f), "Progress").rectTransform.At(0.5f, 0.35f, 0, 0, 500, 26);
            _progressFill = UIFactory.Image(_progressBar, UIFactory.Accent, "Fill");
            _progressFill.rectTransform.Place(0, 0, 0, 1);
            _progressLabel = UIFactory.Text(_progressBar, "", 26, UIFactory.TextMain, TextAnchor.LowerCenter);
            _progressLabel.rectTransform.Place(0, 1, 1, 1, 0, 0, 0, -40);
            _flash = UIFactory.Image(_root, new Color(1, 1, 1, 0), "Flash");
            _flash.rectTransform.Place(0, 0, 1, 1);
            _flash.raycastTarget = false;

            // Loot list (right-centre)
            _lootList = UIFactory.Rect(_root, "Loot").At(1, 0.5f, -600, 120, 360, 420);
            var lv = _lootList.gameObject.AddComponent<VerticalLayoutGroup>();
            lv.spacing = 6; lv.childControlHeight = true; lv.childForceExpandHeight = false; lv.childAlignment = TextAnchor.UpperCenter;

            // Quick chat panel
            _quickChatPanel = UIFactory.Image(_root, new Color(0, 0, 0, 0.7f), "QuickChat").rectTransform.At(1, 1, -640, -420, 420, 560);
            var qv = _quickChatPanel.gameObject.AddComponent<VerticalLayoutGroup>();
            qv.spacing = 4; qv.padding = new RectOffset(8, 8, 8, 8); qv.childControlHeight = true; qv.childForceExpandHeight = true;
            for (int i = 0; i < PingSystem.QuickChatLines.Length; i++)
            {
                int idx = i;
                UIFactory.Button(_quickChatPanel, PingSystem.QuickChatLines[i], () => { _me?.Pings.SendQuickChat(idx); _quickChatPanel.gameObject.SetActive(false); }, UIFactory.PanelLight, 22);
            }
            _quickChatPanel.gameObject.SetActive(false);
            _quickChatLog = UIFactory.Text(_root, "", 24, UIFactory.Accent2, TextAnchor.LowerLeft);
            _quickChatLog.rectTransform.Place(0, 0.65f, 0.35f, 0.85f, 20, 0, 0, 0);

            // HUD editor bar
            _editorBar = UIFactory.Image(_root, new Color(0, 0, 0, 0.8f), "EditorBar").rectTransform.At(0.5f, 1, 0, -200, 900, 110);
            UIFactory.HStack(_editorBar, 12, 12);
            UIFactory.Text(_editorBar, "Drag buttons to move them", 26, UIFactory.TextMain, TextAnchor.MiddleCenter);
            UIFactory.Button(_editorBar, "Reset", HudLayoutEditor.ResetLayout, UIFactory.PanelLight, 26);
            UIFactory.Button(_editorBar, "Save & Exit", HudLayoutEditor.Close, UIFactory.Accent, 26);
            _editorBar.gameObject.SetActive(false);

            foreach (var b in Buttons) _defaults[b.layoutId] = ((RectTransform)b.transform).anchoredPosition;
        }

        HudButton Btn(string id, string label, float ax, float ay, float x, float y, float size, Color? color, bool dragLooks = false) =>
            Btn(id, label, ax, ay, x, y, size, size, color, dragLooks);

        HudButton Btn(string id, string label, float ax, float ay, float x, float y, float w, float h, Color? color, bool dragLooks = false)
        {
            var c = color ?? new Color(1, 1, 1, 1);
            var img = UIFactory.Image(_root, new Color(c.r * 0.35f, c.g * 0.35f, c.b * 0.35f, 0.55f), "Btn_" + id);
            img.rectTransform.At(ax, ay, x, y, w, h);
            var b = img.gameObject.AddComponent<HudButton>();
            b.layoutId = id; b.image = img; b.dragLooks = dragLooks;
            b.label = UIFactory.Text(img.transform, label, 24, UIFactory.TextMain, TextAnchor.MiddleCenter, FontStyle.Bold);
            b.label.rectTransform.Place(0, 0, 1, 1);
            b.cooldownFill = UIFactory.Image(img.transform, new Color(0, 0, 0, 0.6f), "Cooldown");
            b.cooldownFill.rectTransform.Place(0, 0, 1, 0);
            b.cooldownFill.raycastTarget = false;
            Buttons.Add(b);
            return b;
        }

        static Image Bar(RectTransform parent, string name, float y0, float y1, Color color)
        {
            var bg = UIFactory.Image(parent, new Color(0, 0, 0, 0.5f), name).rectTransform.Place(0, y0, 1, y1);
            var fill = UIFactory.Image(bg, color, "Fill");
            fill.rectTransform.Place(0, 0, 1, 1);
            return fill;
        }

        public void ResetButtonPositions()
        {
            foreach (var b in Buttons) if (_defaults.TryGetValue(b.layoutId, out var p)) ((RectTransform)b.transform).anchoredPosition = p;
        }

        public void ShowEditorBar(bool on) => _editorBar.gameObject.SetActive(on);

        // ------------------------------------------------------------------ binding
        public void Bind(PlayerIdentity me)
        {
            _me = me;
            _feed.Clear();
            _banner.text = "";
            me.Weapons.HitConfirmed += (head, dmg) => { _hitMarkerUntil = Time.time + 0.2f; _hitMarker.color = head ? Color.red : Color.white; };
            me.Health.DamagedFrom += from => { _damageUntil = Time.time + 1f; PointDamageArrow(from); };
            PingSystem.QuickChatReceived -= OnQuickChat;
            PingSystem.QuickChatReceived += OnQuickChat;
            AirdropManager.AirdropIncoming -= OnAirdrop;
            AirdropManager.AirdropIncoming += OnAirdrop;
            _joystick.CancelSprintLock();
        }

        void BindMatch()
        {
            var mm = MatchManager.Instance;
            if (mm == null || mm == _boundMatch) return;
            _boundMatch = mm;
            mm.KillFeed += OnKillFeed;
            mm.LocalResults += msg => UIManager.Instance.OnMatchResults(msg);
        }

        void OnKillFeed(string killer, string victim, string weapon, bool headshot, bool knock)
        {
            string line = string.IsNullOrEmpty(killer)
                ? $"{victim} {(knock ? "was knocked" : "was eliminated")} ({weapon})"
                : $"{killer} {(knock ? "knocked" : "eliminated")} {victim}{(headshot ? " ◎" : "")}  [{weapon}]";
            _feed.Enqueue(line);
            while (_feed.Count > 5) _feed.Dequeue();
            _killFeed.text = string.Join("\n", _feed);
            if (_me != null && killer == _me.Nickname.Value.ToString() && !knock) Banner(headshot ? "HEADSHOT ELIMINATION" : "ELIMINATION", 1.5f);
        }

        void OnQuickChat(string from, string text) => StartCoroutine(ShowChatLine($"{from}: {text}"));

        IEnumerator ShowChatLine(string line)
        {
            _quickChatLog.text = line;
            yield return new WaitForSeconds(4f);
            if (_quickChatLog.text == line) _quickChatLog.text = "";
        }

        void OnAirdrop(Vector3 pos) => Banner("AIRDROP INCOMING", 2.5f);

        public void ShowEliminatedBanner(MatchResultMessage msg) =>
            Banner(msg.won ? GameConstants.VictoryTitle : GameConstants.IsBattleRoyale((GameMode)msg.mode) ? $"ELIMINATED — #{msg.placement}" : "MATCH OVER", 3f);

        void Banner(string text, float seconds) { _banner.text = text; _bannerUntil = Time.time + seconds; }

        public void Flash(float seconds) => _flashUntil = Time.time + seconds;

        public void ShowExplosion(Vector3 pos)
        {
            var cam = PlayerCamera.Instance;
            if (cam != null && Vector3.Distance(cam.transform.position, pos) < 25f) cam.AddRecoil(Random.Range(-2f, 2f), Random.Range(-2f, 2f));
        }

        // ------------------------------------------------------------------ actions
        static PlayerInputHandler Input() => PlayerInputHandler.Instance;

        void SetFire(bool on) { if (Input() != null) Input().touchFireHeld = on; }

        void UseItem(string id)
        {
            if (_me == null || _me.Inventory.Count(id) <= 0) { UIManager.Instance.Toast("You don't have any"); return; }
            _me.Inventory.UseItemRpc(GameConstants.Fs32(id));
        }

        void ThrowGrenade()
        {
            if (_me == null) return;
            var nade = _me.Inventory.Items.Cast<InventoryStack>().Select(s => GameDatabase.Instance.Item(s.itemId.ToString()))
                .FirstOrDefault(d => d != null && d.kind == ItemKind.Throwable);
            if (nade == null) { UIManager.Instance.Toast("No grenades"); return; }
            var cam = PlayerCamera.Instance.transform;
            _me.Inventory.ThrowRpc(GameConstants.Fs32(nade.id), _me.transform.position + Vector3.up * 1.6f + cam.forward * 0.6f, cam.forward * 20f + Vector3.up * 4f);
        }

        void DeployWall()
        {
            if (_me == null) return;
            var fwd = Quaternion.Euler(0, PlayerCamera.Instance.Yaw, 0) * Vector3.forward;
            _me.Inventory.DeployWallRpc(_me.transform.position + fwd * 2.2f, PlayerCamera.Instance.Yaw);
        }

        void Ping()
        {
            if (_me == null || PlayerCamera.Instance == null) return;
            var ray = PlayerCamera.Instance.AimRay();
            if (!Physics.Raycast(ray, out var hit, 600f, GameConstants.ShootableMask)) return;
            var enemy = hit.collider.GetComponentInParent<PlayerIdentity>();
            _me.Pings.SendPing(hit.point, enemy != null && enemy.Team.Value != _me.Team.Value ? PingType.Enemy : PingType.Location);
        }

        void ToggleFullMap()
        {
            _minimap.fullMap = !_minimap.fullMap;
            if (_minimap.fullMap) _minimapRect.At(0.5f, 0.5f, 0, 0, 900, 900);
            else _minimapRect.At(1, 1, -170, -170, 300, 300);
        }

        void PointDamageArrow(Vector3 from)
        {
            if (_me == null) return;
            Vector3 dir = from - _me.transform.position;
            float angle = Vector3.SignedAngle(Quaternion.Euler(0, PlayerCamera.Instance.Yaw, 0) * Vector3.forward, new Vector3(dir.x, 0, dir.z), Vector3.up);
            _damageArrow.rectTransform.localRotation = Quaternion.Euler(0, 0, -angle);
            _damageArrow.rectTransform.anchoredPosition = Quaternion.Euler(0, 0, -angle) * new Vector3(0, 220, 0);
        }

        // ------------------------------------------------------------------ per-frame
        void Update()
        {
            BindMatch();
            _hitMarker.enabled = Time.time < _hitMarkerUntil;
            _damageArrow.enabled = Time.time < _damageUntil;
            _flash.color = new Color(1, 1, 1, Mathf.Clamp01(_flashUntil - Time.time));
            if (Time.time > _bannerUntil) _banner.text = "";
            if (_me == null || !_me.IsSpawned) return;

            var input = Input();
            if (input != null)
            {
                if (input.ConsumeSkill()) _me.Skills?.RequestUse();
                if (_me.Controller.Vehicle == null && input.ConsumeInteract()) _interactAction?.Invoke();
            }

            _refresh -= Time.deltaTime;
            if (_refresh > 0) return;
            _refresh = 0.1f;
            RefreshStatus();
            RefreshContext();
            UpdateSpectate();
        }

        void RefreshStatus()
        {
            var h = _me.Health;
            var mm = MatchManager.Instance;
            var zone = SafeZoneManager.Instance;
            var life = h.Life.Value;

            _hpFill.rectTransform.anchorMax = new Vector2(life == PlayerLifeState.Knocked ? h.KnockHealth.Value / GameConstants.KnockHealth : h.Health.Value / GameConstants.MaxHealth, 1);
            _hpFill.color = life == PlayerLifeState.Knocked ? UIFactory.Danger : h.Health.Value < 60 ? new Color(1f, 0.5f, 0.4f) : new Color(0.95f, 0.95f, 0.95f);
            _shieldFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(h.Shield.Value / GameConstants.MaxHealth), 1);
            _energyFill.rectTransform.anchorMax = new Vector2(h.Energy.Value / GameConstants.MaxEnergy, 1);
            _armor.text = $"Vest Lv{h.VestLevel.Value} {(h.VestLevel.Value > 0 ? Mathf.CeilToInt(h.VestDurability.Value).ToString() : "")}   Helmet Lv{h.HelmetLevel.Value}   " +
                          $"Bag Lv{_me.Inventory.BackpackLevel.Value} ({_me.Inventory.UsedCapacity}/{_me.Inventory.Capacity})";

            var stats = mm != null ? mm.Mode.Value : GameMode.Training;
            string info = "";
            if (mm != null)
            {
                if (mm.Phase.Value == MatchPhase.Waiting) info = $"Waiting for players… {Mathf.CeilToInt((float)(mm.PhaseEndsAt.Value - NetworkTime()))}s";
                else if (GameConstants.IsBattleRoyale(stats)) info = $"Alive {mm.PlayersAlive.Value}   ·   Teams {mm.TeamsAlive.Value}";
                else if (stats == GameMode.ClashSquad) info = $"Round {mm.Round.Value}   ·   {mm.ScoreA.Value} : {mm.ScoreB.Value}";
                else if (stats == GameMode.TeamDeathmatch) info = $"{mm.ScoreA.Value} : {mm.ScoreB.Value}   ·   {Mathf.Max(0, Mathf.CeilToInt((float)(mm.PhaseEndsAt.Value - NetworkTime())))}s";
                else info = "Training Ground";
            }
            if (zone != null && zone.State.Value != ZoneState.Inactive)
            {
                info += zone.State.Value == ZoneState.Waiting ? $"   ·   Storm moves in {Mathf.CeilToInt(zone.SecondsToNextEvent)}s"
                      : zone.State.Value == ZoneState.Shrinking ? $"   ·   Storm closing {Mathf.CeilToInt(zone.SecondsToNextEvent)}s" : "";
                if (!zone.IsInside(_me.transform.position)) info += "   ·   <color=#FF6060>OUTSIDE SAFE ZONE</color>";
            }
            _top.supportRichText = true;
            _top.text = info;
            var loc = MapLocation.At(_me.transform.position);
            _location.text = loc != null ? loc.displayName : "";

            // Ammo + slots
            var st = _me.Inventory.ActiveWeaponState;
            var w = _me.Weapons.CurrentWeapon;
            _ammo.text = w == null || w.category == WeaponCategory.Melee ? "" :
                $"{Mathf.Max(0, _me.Weapons.PredictedMag)}<size=28> / {_me.Inventory.AmmoReserve(w.ammo)}</size>{(_me.Weapons.Reloading.Value ? "\n<size=24>RELOADING</size>" : "")}";
            _ammo.supportRichText = true;
            for (int i = 0; i < 4; i++)
            {
                var wd = _me.Inventory.WeaponIn(i);
                _slotButtons[i].SetLabel(wd != null ? wd.displayName : "—");
                _slotButtons[i].image.color = i == _me.Inventory.ActiveSlot.Value ? new Color(1f, 0.55f, 0.1f, 0.6f) : new Color(0, 0, 0, 0.5f);
            }

            // Buttons that depend on state
            bool sky = life == PlayerLifeState.InPlane || life == PlayerLifeState.Parachuting;
            _jump.label.text = life == PlayerLifeState.InPlane ? "JUMP" : life == PlayerLifeState.Parachuting && !_me.Controller.ChuteOpen.Value ? "CHUTE" : "JUMP";
            foreach (var b in new[] { _fire, _fireLeft, _aim, _reload, _crouch, _prone, _grenade, _wall, _heal, _shroom }) b.SetVisible(!sky && life != PlayerLifeState.Dead);
            _skill.SetVisible(_me.Skills != null && _me.Skills.HasActiveSkill && !sky);
            if (_me.Skills != null && _me.Character != null)
            {
                float cd = _me.Skills.CooldownRemaining;
                _skill.cooldownFill.rectTransform.anchorMax = new Vector2(1, Mathf.Clamp01(cd / Mathf.Max(1, _me.Character.cooldown)));
                _skill.label.text = cd > 0 ? Mathf.CeilToInt(cd).ToString() : "SKILL";
            }
            _heal.label.text = $"MED\n{_me.Inventory.Count("med_kit")}";
            _shroom.label.text = $"VIGOR\n{_me.Inventory.Count("vigor_shroom")}";
            _wall.label.text = $"WALL\n{_me.Inventory.Count("foam_wall")}";
            _crosshair.enabled = !sky;

            // Progress bar: using item / being revived / bleeding out
            float p = -1; string label = "";
            if (_me.Inventory.UsingItem.Value.Length > 0)
            {
                var def = GameDatabase.Instance.Item(_me.Inventory.UsingItem.Value.ToString());
                float total = def != null ? def.useSeconds : 1;
                p = 1f - Mathf.Clamp01((float)(_me.Inventory.UsingUntil.Value - NetworkTime()) / total);
                label = "Using " + (def != null ? def.displayName : "");
            }
            else if (life == PlayerLifeState.Knocked)
            {
                p = h.ReviveProgress.Value > 0 ? h.ReviveProgress.Value : h.KnockHealth.Value / GameConstants.KnockHealth;
                label = h.ReviveProgress.Value > 0 ? "Being revived…" : "KNOCKED — call for help!";
            }
            _progressBar.gameObject.SetActive(p >= 0);
            if (p >= 0) { _progressFill.rectTransform.anchorMax = new Vector2(p, 1); _progressLabel.text = label; }
        }

        void RefreshContext()
        {
            _interactAction = null;
            string label = null;
            Vector3 me = _me.transform.position;

            if (_me.Health.IsAlive && _me.Controller.Vehicle == null)
            {
                var mate = PlayerIdentity.All.FirstOrDefault(p => p.IsTeammateOfLocal && p.Health.Life.Value == PlayerLifeState.Knocked && Vector3.Distance(p.transform.position, me) < 3f);
                if (mate != null) { label = "REVIVE"; _interactAction = () => _me.Health.StartReviveRpc(mate.NetworkObjectId); }
                if (label == null)
                {
                    var crate = FindObjectsByType<AirdropCrate>(FindObjectsSortMode.None).FirstOrDefault(c => c.Landed.Value && Vector3.Distance(c.transform.position, me) < 3f);
                    if (crate != null) { label = "OPEN"; _interactAction = () => crate.OpenRpc(); }
                }
                if (label == null)
                {
                    var veh = FindObjectsByType<VehicleController>(FindObjectsSortMode.None).FirstOrDefault(v => Vector3.Distance(v.transform.position, me) < 4.5f && v.Health.Value > 0);
                    if (veh != null) { label = $"ENTER {veh.type.ToString().ToUpper()}"; _interactAction = () => veh.EnterRpc(); }
                }
            }
            else if (_me.Controller.Vehicle != null)
            {
                var v = _me.Controller.Vehicle;
                label = $"EXIT  ·  Fuel {Mathf.CeilToInt(v.Fuel.Value)}%";
                _interactAction = () => v.ExitRpc();
            }
            _interact.SetVisible(label != null);
            if (label != null) _interact.label.text = label;

            // Loot list
            UIFactory.Clear(_lootList);
            var spawner = LootSpawner.Instance;
            if (spawner == null || !_me.Health.IsAlive || _me.Controller.Vehicle != null) return;
            foreach (var e in spawner.NearbyEntries(me, 3f).Take(6))
            {
                var def = GameDatabase.Instance.Item(e.itemId);
                if (def == null) continue;
                int id = e.id;
                var b = UIFactory.Button(_lootList, $"{def.displayName}{(e.amount > 1 ? $" ×{e.amount}" : "")}", () => _me.Inventory.PickupRpc(id),
                    new Color(0, 0, 0, 0.65f), 24);
                b.gameObject.AddComponent<LayoutElement>().preferredHeight = 60;
                b.GetComponentInChildren<Text>().color = UIFactory.RarityColor(def.rarity.ToString().ToLower());
            }
        }

        /// <summary>After elimination, follow a living teammate.</summary>
        void UpdateSpectate()
        {
            var cam = PlayerCamera.Instance;
            if (cam == null) return;
            if (_me.Health.Life.Value != PlayerLifeState.Dead) { cam.target = _me.transform; return; }
            var mate = PlayerIdentity.All.FirstOrDefault(p => p.IsTeammateOfLocal && p.Health.Life.Value != PlayerLifeState.Dead);
            if (mate != null) cam.target = mate.transform;
        }

        double NetworkTime() => Unity.Netcode.NetworkManager.Singleton != null ? Unity.Netcode.NetworkManager.Singleton.ServerTime.Time : 0;
    }
}
