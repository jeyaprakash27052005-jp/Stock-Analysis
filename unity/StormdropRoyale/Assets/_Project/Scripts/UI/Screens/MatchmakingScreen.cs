using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Stormdrop
{
    /// <summary>Queues with the backend, polls until matched, then connects to the dedicated server.</summary>
    public class MatchmakingScreen : UIScreen
    {
        Text _status, _timer;
        string _ticket, _followTicket;
        bool _connecting;
        float _start;

        protected override void Build()
        {
            UIFactory.Text(Root, "MATCHMAKING", 70, UIFactory.Accent, TextAnchor.MiddleCenter, FontStyle.Bold).rectTransform.Place(0, 0.62f, 1, 0.8f);
            _status = UIFactory.Text(Root, "", 36, UIFactory.TextMain, TextAnchor.MiddleCenter);
            _status.rectTransform.Place(0, 0.45f, 1, 0.6f);
            _timer = UIFactory.Text(Root, "", 60, UIFactory.TextDim, TextAnchor.MiddleCenter);
            _timer.rectTransform.Place(0, 0.32f, 1, 0.45f);
            UIFactory.Button(Root, "Cancel", Cancel, UIFactory.Danger, 34).GetComponent<RectTransform>().At(0.5f, 0.15f, 0, 0, 420, 100);
        }

        public void FollowTicket(string ticketId) => _followTicket = ticketId;

        protected override void OnShow()
        {
            _connecting = false;
            _start = Time.time;
            _status.text = $"{ModeSelectScreen.ModeName(UI.SelectedMode)} · {Backend.Me?.region}";
            StartCoroutine(Flow());
        }

        protected override void OnHide() => StopAllCoroutines();

        void Update()
        {
            if (!_connecting && Root.gameObject.activeSelf) _timer.text = System.TimeSpan.FromSeconds(Time.time - _start).ToString(@"mm\:ss");
        }

        IEnumerator Flow()
        {
            if (UI.SelectedMode == GameMode.Training && _followTicket == null)
            {
                StartTraining();
                yield break;
            }

            if (_followTicket != null) { _ticket = _followTicket; _followTicket = null; }
            else
            {
                var join = Backend.JoinQueue(UI.SelectedMode, UI.Ranked);
                yield return new WaitUntil(() => join.IsCompleted);
                if (join.IsFaulted) { Fail(join.Exception.GetBaseException().Message); yield break; }
                _ticket = join.Result.ticketId;
            }

            while (true)
            {
                yield return new WaitForSeconds(2f);
                var st = Backend.QueueStatus(_ticket);
                yield return new WaitUntil(() => st.IsCompleted);
                if (st.IsFaulted) { _status.text = "Reconnecting…"; continue; }
                var s = st.Result;
                if (s.status == "matched")
                {
                    Connect(s);
                    yield break;
                }
                if (s.status != "waiting") { Fail("Matchmaking " + s.status); yield break; }
                _status.text = $"Finding players… {s.playersInQueue} in queue";
                if (Time.time - _start > 300) { Cancel(); UI.Toast("No match found. Try another mode or region."); yield break; }
            }
        }

        void Connect(MatchmakingStatus s)
        {
            _connecting = true;
            _status.text = "Match found! Connecting…";
            _timer.text = "";
            UI.LastResult = null;
            var mode = GameConstants.ParseMode(s.match.mode);
            NetworkBootstrap.Instance.StartClient(s.match.host, (ushort)s.match.port, s.joinTicket, mode);
            _ = VoiceChatService.JoinTeamChannel(s.match.id, s.match.team);
            AnalyticsService.Log("match_join", "mode", s.match.mode);
        }

        void StartTraining()
        {
            _connecting = true;
            _status.text = "Loading training ground…";
            UI.LastResult = null;
            var me = Backend.Me;
            NetworkBootstrap.Instance.StartTraining(me?.nickname ?? "Player", me?.character_id ?? "char_kael", me?.pet_id ?? "");
        }

        void Cancel()
        {
            if (!string.IsNullOrEmpty(_ticket)) _ = Backend.CancelQueue(_ticket);
            _ticket = null;
            if (_connecting) NetworkBootstrap.Instance.Leave();
            UI.Show<LobbyScreen>(clearStack: true);
        }

        void Fail(string message)
        {
            UI.Toast(message);
            UI.Show<LobbyScreen>(clearStack: true);
        }
    }
}
