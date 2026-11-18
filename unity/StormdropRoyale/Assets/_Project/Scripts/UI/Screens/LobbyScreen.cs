using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Stormdrop
{
    /// <summary>Main lobby: profile, character showcase, party, mode + START, and menu shortcuts.</summary>
    public class LobbyScreen : UIScreen
    {
        Text _name, _level, _rank, _charName, _charSkill, _modeLabel, _party;
        Image _xpFill, _showcase;
        List<string> _ownedCharacters = new List<string>();
        int _charIndex;
        bool _loginRewardChecked;

        protected override void Build()
        {
            // top bar: profile + currencies
            var top = UIFactory.Image(Root, UIFactory.Panel, "Top").rectTransform.Place(0, 1, 1, 1, 0, -120, 0, 0);
            _name = UIFactory.Text(top, "", 38, UIFactory.TextMain, TextAnchor.UpperLeft, FontStyle.Bold);
            _name.rectTransform.Place(0, 0.5f, 0.4f, 1, 30, 0, 0, 12);
            _level = UIFactory.Text(top, "", 26, UIFactory.TextDim, TextAnchor.LowerLeft);
            _level.rectTransform.Place(0, 0, 0.4f, 0.5f, 30, 20, 0, 0);
            var xpBg = UIFactory.Image(top, new Color(0, 0, 0, 0.5f), "XP").rectTransform.Place(0.12f, 0, 0.38f, 0, 0, 14, 0, -26);
            _xpFill = UIFactory.Image(xpBg, UIFactory.Accent2, "Fill");
            _xpFill.rectTransform.Place(0, 0, 0, 1);
            _rank = UIFactory.Text(top, "", 30, UIFactory.Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
            _rank.rectTransform.Place(0.4f, 0, 0.62f, 1);
            UI.CurrencyBar(top);

            // left menu
            var menu = UIFactory.Rect(Root, "Menu").Place(0, 0.12f, 0, 0.86f, 20, 0, -280, 0);
            var v = menu.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = 12; v.childControlHeight = true; v.childForceExpandHeight = true; v.childControlWidth = true;
            UIFactory.Button(menu, "Store", () => UI.Show<StoreScreen>());
            UIFactory.Button(menu, "Vault", () => UI.Show<VaultScreen>());
            UIFactory.Button(menu, "Storm Pass", () => UI.Show<PassScreen>());
            UIFactory.Button(menu, "Missions", ShowMissions);
            UIFactory.Button(menu, "Events", ShowEvents);
            UIFactory.Button(menu, "Guild", () => UI.Show<GuildScreen>());
            UIFactory.Button(menu, "Friends", ShowFriends);
            UIFactory.Button(menu, "Leaderboard", ShowLeaderboard);
            UIFactory.Button(menu, "Settings", () => UI.Show<SettingsScreen>());

            // character showcase (placeholder art: coloured silhouette)
            _showcase = UIFactory.Image(Root, Color.white, "Showcase");
            _showcase.rectTransform.At(0.5f, 0.55f, 0, 0, 360, 560);
            _charName = UIFactory.Text(Root, "", 52, UIFactory.TextMain, TextAnchor.MiddleCenter, FontStyle.Bold);
            _charName.rectTransform.At(0.5f, 0.2f, 0, 20, 900, 70);
            _charSkill = UIFactory.Text(Root, "", 26, UIFactory.TextDim, TextAnchor.UpperCenter);
            _charSkill.rectTransform.At(0.5f, 0.12f, 0, 0, 900, 90);
            UIFactory.Button(Root, "◀", () => CycleCharacter(-1)).GetComponent<RectTransform>().At(0.5f, 0.55f, -300, 0, 90, 120);
            UIFactory.Button(Root, "▶", () => CycleCharacter(1)).GetComponent<RectTransform>().At(0.5f, 0.55f, 300, 0, 90, 120);

            // party
            _party = UIFactory.Text(Root, "", 26, UIFactory.TextMain, TextAnchor.UpperLeft);
            _party.rectTransform.Place(0.72f, 0.45f, 1, 0.84f, 0, 0, 30, 0);
            UIFactory.Button(Root, "Invite by ID", InviteFriend, UIFactory.PanelLight, 26).GetComponent<RectTransform>().Place(0.72f, 0.38f, 1, 0.44f, 0, 0, 30, 0);

            // mode + start
            var modeBtn = UIFactory.Button(Root, "", () => UI.Show<ModeSelectScreen>(), UIFactory.PanelLight, 30);
            modeBtn.GetComponent<RectTransform>().Place(0.72f, 0.2f, 1, 0.3f, 0, 0, 30, 0);
            _modeLabel = modeBtn.GetComponentInChildren<Text>();
            UIFactory.Button(Root, "START", () => UI.Show<MatchmakingScreen>(), UIFactory.Accent, 56).GetComponent<RectTransform>().Place(0.72f, 0.04f, 1, 0.18f, 0, 0, 30, 0);
        }

        protected override void OnShow()
        {
            Backend.ProfileChanged -= Refresh;
            Backend.ProfileChanged += Refresh;
            Refresh();
            Run(async () =>
            {
                await Backend.LoadCatalog();
                await Backend.RefreshProfile();
                var inv = await Backend.Inventory();
                _ownedCharacters = inv.items.Where(i => i.type == "character").Select(i => i.item_id).ToList();
                _charIndex = Mathf.Max(0, _ownedCharacters.IndexOf(Backend.Me.character_id));
                RefreshCharacter();
                await RefreshParty();
                if (!_loginRewardChecked) { _loginRewardChecked = true; await MaybeShowLoginReward(); }
            }, spinner: false);
            StartCoroutine(FollowPartyLeader());
        }

        protected override void OnHide() { Backend.ProfileChanged -= Refresh; StopAllCoroutines(); }

        void Refresh()
        {
            var me = Backend.Me;
            if (me == null) return;
            _name.text = $"{me.nickname}  <size=24><color=#9AA4B5>ID {me.player_tag}</color></size>";
            _name.supportRichText = true;
            _level.text = $"Lv {me.level}" + (string.IsNullOrEmpty(me.guild_tag) ? "" : $"   [{me.guild_tag}] {me.guild_name}");
            _xpFill.rectTransform.anchorMax = new Vector2(me.xpToNext > 0 ? (float)me.xp / me.xpToNext : 0, 1);
            _rank.text = me.brRank != null ? $"{me.brRank.label}  ·  {me.br_rank_points} RP" : "";
            _modeLabel.text = $"Mode: {ModeSelectScreen.ModeName(UI.SelectedMode)}{(UI.Ranked ? " (Ranked)" : "")}  ▸";
            RefreshCharacter();
        }

        void RefreshCharacter()
        {
            if (Backend.Me == null) return;
            var id = _ownedCharacters.Count > 0 ? _ownedCharacters[Mathf.Clamp(_charIndex, 0, _ownedCharacters.Count - 1)] : Backend.Me.character_id;
            var data = GameDatabase.Instance.Character(id);
            var item = Backend.Item(id);
            _charName.text = (item?.name ?? data?.displayName ?? id) + (id == Backend.Me.character_id ? "  ✔" : "");
            _charSkill.text = item?.description ?? data?.skillDescription ?? "";
            _showcase.color = data != null ? data.placeholderColor : Color.gray;
        }

        void CycleCharacter(int dir)
        {
            if (_ownedCharacters.Count == 0) return;
            _charIndex = (_charIndex + dir + _ownedCharacters.Count) % _ownedCharacters.Count;
            var id = _ownedCharacters[_charIndex];
            RefreshCharacter();
            Run(() => Backend.SetLoadout(id, Backend.Me.pet_id), spinner: false);
        }

        async System.Threading.Tasks.Task RefreshParty()
        {
            var p = await Backend.Party();
            var lines = new List<string> { "<b>PARTY</b>" };
            if (p.party == null) lines.Add($"• {Backend.Me.nickname} (you)");
            else foreach (var m in p.party.members) lines.Add($"• {m.nickname}  Lv{m.level}{(m.leader ? "  ★" : "")}");
            _party.supportRichText = true;
            _party.text = string.Join("\n", lines);
            foreach (var inv in p.invites)
                UI.Confirm("Party invite", $"{inv.from_nickname} invited you to their squad.", "Join",
                    () => Run(async () => { await Backend.JoinParty(inv.party_id); await RefreshParty(); }));
        }

        /// <summary>Party members automatically follow the leader into matchmaking.</summary>
        IEnumerator FollowPartyLeader()
        {
            while (true)
            {
                yield return new WaitForSeconds(3f);
                var t = Backend.CurrentTicket();
                yield return new WaitUntil(() => t.IsCompleted);
                if (t.Status == System.Threading.Tasks.TaskStatus.RanToCompletion && t.Result.status is "waiting" or "matched")
                {
                    UI.Get<MatchmakingScreen>().FollowTicket(t.Result.ticketId);
                    UI.Show<MatchmakingScreen>();
                    yield break;
                }
                var p = RefreshParty();
                yield return new WaitUntil(() => p.IsCompleted);
            }
        }

        void InviteFriend()
        {
            InputField field = null;
            System.Action close = null;
            close = UI.Popup("Invite to party", c =>
            {
                field = UIFactory.Input(c, "Player ID (10 digits)");
                field.contentType = InputField.ContentType.IntegerNumber;
                field.GetComponent<RectTransform>().Place(0, 0.5f, 1, 0.8f);
                UIFactory.Button(c, "Send invite", () => Run(async () => { await Backend.InviteToParty(field.text); close(); UI.Toast("Invite sent"); }), UIFactory.Accent)
                    .GetComponent<RectTransform>().Place(0.25f, 0.1f, 0.75f, 0.35f);
            }, 900, 450);
        }

        async System.Threading.Tasks.Task MaybeShowLoginReward()
        {
            var r = await Backend.LoginRewards();
            if (!r.canClaim) return;
            System.Action close = null;
            close = UI.Popup("Daily Login Reward", c =>
            {
                var row = UIFactory.Rect(c, "Days").Place(0, 0.35f, 1, 1);
                UIFactory.HStack(row, 10);
                foreach (var d in r.rewards)
                {
                    bool next = d.day == (r.streakDay % 7) + 1;
                    var cell = UIFactory.Image(row, next ? UIFactory.Accent : d.day <= r.streakDay ? UIFactory.Good : UIFactory.PanelLight, "Day");
                    UIFactory.Text(cell.transform, $"Day {d.day}\n\n{d.name}", 24, UIFactory.TextMain, TextAnchor.MiddleCenter).rectTransform.Place(0, 0, 1, 1, 6, 6, 6, 6);
                }
                UIFactory.Button(c, "Claim", () => Run(async () => { await Backend.ClaimLoginReward(); await Backend.RefreshProfile(); close(); UI.Toast("Reward claimed!"); }), UIFactory.Accent, 36)
                    .GetComponent<RectTransform>().Place(0.3f, 0.02f, 0.7f, 0.25f);
            }, 1500, 650);
        }

        void ShowMissions() => Run(async () =>
        {
            var r = await Backend.Missions();
            UI.Popup("Missions", c =>
            {
                var list = UIFactory.ScrollList(c, out _);
                list.parent.GetComponent<RectTransform>().Place(0, 0, 1, 1);
                foreach (var m in r.missions)
                {
                    var row = UIFactory.Row(list, 110);
                    UIFactory.Text(row, $"[{m.period.ToUpper()}] {m.title}\n<size=22><color=#9AA4B5>+{m.reward_xp} XP  +{m.reward_gold} Gold{(m.reward_diamonds > 0 ? $"  +{m.reward_diamonds} Diamonds" : "")}</color></size>", 28)
                        .rectTransform.Place(0, 0, 0.62f, 1, 20, 0, 0, 0);
                    UIFactory.Text(row, $"{Mathf.Min(m.progress, m.target)}/{m.target}", 28, UIFactory.TextDim, TextAnchor.MiddleCenter).rectTransform.Place(0.62f, 0, 0.78f, 1);
                    if (m.claimed_at != null) UIFactory.Text(row, "Claimed", 26, UIFactory.Good, TextAnchor.MiddleCenter).rectTransform.Place(0.78f, 0, 1, 1);
                    else if (m.complete)
                    {
                        var id = m.id;
                        UIFactory.Button(row, "Claim", () => Run(async () => { await Backend.ClaimMission(id); await Backend.RefreshProfile(); UI.Toast("Claimed!"); }), UIFactory.Accent)
                            .GetComponent<RectTransform>().Place(0.8f, 0.15f, 0.98f, 0.85f);
                    }
                }
            }, 1500, 900);
        });

        void ShowEvents() => Run(async () =>
        {
            var r = await Backend.Events();
            UI.Popup("Events", c =>
            {
                var list = UIFactory.ScrollList(c, out _);
                list.parent.GetComponent<RectTransform>().Place(0, 0, 1, 1);
                foreach (var e in r.events)
                {
                    var row = UIFactory.Row(list, 130);
                    UIFactory.Text(row, $"<b>{e.title}</b>\n<size=24>{e.description}</size>\n<size=20><color=#9AA4B5>{e.starts_at?.Substring(0, 10)} → {e.ends_at?.Substring(0, 10)}</color></size>", 30)
                        .rectTransform.Place(0, 0, 1, 1, 20, 0, 20, 0);
                }
            }, 1400, 850);
        });

        void ShowFriends() => Run(async () =>
        {
            var r = await Backend.Friends();
            UI.Popup("Friends", c =>
            {
                var add = UIFactory.Input(c, "Add friend by Player ID");
                add.GetComponent<RectTransform>().Place(0, 0.88f, 0.7f, 1);
                UIFactory.Button(c, "Add", () => Run(async () => { await Backend.AddFriend(add.text); UI.Toast("Request sent"); }), UIFactory.Accent)
                    .GetComponent<RectTransform>().Place(0.72f, 0.88f, 1, 1);
                var list = UIFactory.ScrollList(c, out _);
                list.parent.GetComponent<RectTransform>().Place(0, 0, 1, 0.85f);
                foreach (var f in r.friends)
                {
                    var row = UIFactory.Row(list, 90);
                    UIFactory.Text(row, $"{(f.online ? "<color=#4CD964>●</color>" : "<color=#666>●</color>")} {f.nickname}  Lv{f.level}  <size=22>{f.brRank?.label}</size>", 28)
                        .rectTransform.Place(0, 0, 0.55f, 1, 20, 0, 0, 0);
                    var tag = f.player_tag;
                    if (f.status == "incoming")
                    {
                        UIFactory.Button(row, "Accept", () => Run(async () => { await Backend.RespondFriend(tag, true); UI.Toast("Friend added"); }), UIFactory.Good, 24).GetComponent<RectTransform>().Place(0.58f, 0.15f, 0.77f, 0.85f);
                        UIFactory.Button(row, "Decline", () => Run(() => Backend.RespondFriend(tag, false)), UIFactory.Danger, 24).GetComponent<RectTransform>().Place(0.79f, 0.15f, 0.98f, 0.85f);
                    }
                    else if (f.status == "pending") UIFactory.Text(row, "Pending", 24, UIFactory.TextDim, TextAnchor.MiddleCenter).rectTransform.Place(0.6f, 0, 1, 1);
                    else UIFactory.Button(row, "Invite", () => Run(async () => { await Backend.InviteToParty(tag); UI.Toast("Invite sent"); }), UIFactory.Accent2, 24).GetComponent<RectTransform>().Place(0.79f, 0.15f, 0.98f, 0.85f);
                }
            }, 1300, 900);
        });

        void ShowLeaderboard() => Run(async () =>
        {
            var r = await Backend.Leaderboard(Backend.Me.region);
            UI.Popup($"Leaderboard — {r.region}", c =>
            {
                var list = UIFactory.ScrollList(c, out _, 4);
                list.parent.GetComponent<RectTransform>().Place(0, 0, 1, 1);
                foreach (var p in r.players)
                {
                    var row = UIFactory.Row(list, 70, p.player_tag == Backend.Me.player_tag ? new Color(1f, 0.55f, 0.1f, 0.35f) : (Color?)null);
                    UIFactory.Text(row, $"#{p.region_rank}   {p.nickname} {(string.IsNullOrEmpty(p.guild_tag) ? "" : $"[{p.guild_tag}]")}", 28).rectTransform.Place(0, 0, 0.6f, 1, 20, 0, 0, 0);
                    UIFactory.Text(row, $"{p.rank?.label}  {p.br_rank_points}", 26, UIFactory.Gold, TextAnchor.MiddleRight).rectTransform.Place(0.6f, 0, 1, 1, 0, 0, 20, 0);
                }
            }, 1300, 900);
        });
    }
}
