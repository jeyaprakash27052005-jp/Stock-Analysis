using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Stormdrop
{
    [Serializable]
    public class MatchReportPlayer
    {
        public string playerId, characterId, suspicionReason;
        public int team, placement, kills, assists, damage, headshots, revives, survivalSeconds;
        public bool suspicious;
    }

    /// <summary>
    /// Runs on the dedicated server: registers with the backend, heartbeats, waits for an
    /// allocation, hosts exactly one match, reports results, then exits (the container restarts it fresh).
    ///
    /// Configure with arguments or environment variables:
    ///   -secret / STORMDROP_SECRET    shared SERVER_SECRET (same value as the backend)
    ///   -region / STORMDROP_REGION    IND, SEA, ME, EU, NA, SA
    ///   -host   / STORMDROP_HOST      public IP or DNS players connect to
    ///   -port   / STORMDROP_PORT      UDP port (default 7777)
    ///   -api    / STORMDROP_API       backend base URL
    /// </summary>
    public class DedicatedServerAgent : MonoBehaviour
    {
        public static DedicatedServerAgent Instance { get; private set; }

        NetworkBootstrap _net;
        string _serverId, _matchId;
        bool _started;

        public void Begin(NetworkBootstrap net)
        {
            Instance = this;
            _net = net;
            _net.ServerSecret = CommandLine.Get("-secret", "");
            ApiClient.ServerSecret = _net.ServerSecret;
            StartCoroutine(Loop());
        }

        ushort Port => ushort.TryParse(CommandLine.Get("-port", GameConstants.DefaultServerPort.ToString()), out var p) ? p : GameConstants.DefaultServerPort;

        IEnumerator Loop()
        {
            while (_serverId == null)
            {
                var t = ApiClient.Post<ServerRegisterResponse>("/server/register", new
                {
                    region = CommandLine.Get("-region", "IND"),
                    host = CommandLine.Get("-host", "127.0.0.1"),
                    port = Port,
                    version = GameConstants.Version,
                    maxPlayers = GameConstants.MaxPlayers,
                });
                yield return new WaitUntil(() => t.IsCompleted);
                if (t.Status == TaskStatus.RanToCompletion) _serverId = t.Result.serverId;
                else { Debug.LogWarning("[Server] register failed, retrying: " + t.Exception?.GetBaseException().Message); yield return new WaitForSeconds(5); }
            }
            Debug.Log("[Server] registered as " + _serverId);

            while (true)
            {
                var t = ApiClient.Post<ServerHeartbeatResponse>("/server/heartbeat", new { serverId = _serverId });
                yield return new WaitUntil(() => t.IsCompleted);
                if (t.Status == TaskStatus.RanToCompletion && t.Result.allocation != null && !_started)
                {
                    var a = t.Result.allocation;
                    _started = true;
                    _matchId = a.matchId;
                    var alloc = new MatchAllocation { MatchId = a.matchId, Mode = GameConstants.ParseMode(a.mode), IsRanked = a.isRanked, Night = a.timeOfDay == "night" };
                    foreach (var p in a.players) alloc.ExpectedPlayers.Add(p.player_id);
                    _net.StartDedicatedServer(Port, alloc);
                }
                else if (t.Status != TaskStatus.RanToCompletion)
                {
                    var msg = t.Exception?.GetBaseException().Message ?? "";
                    if (msg.Contains("404")) _serverId = null;   // backend forgot us → re-register
                }
                if (_serverId == null) { StartCoroutine(Loop()); yield break; }
                yield return new WaitForSeconds(5);
            }
        }

        public async void ReportStarted()
        {
            if (_matchId == null) return;
            try { await ApiClient.Post<OkResponse>("/server/match-start", new { matchId = _matchId }); }
            catch (Exception e) { Debug.LogWarning("[Server] match-start: " + e.Message); }
        }

        public async void ReportResults(int winnerTeam, List<MatchReportPlayer> players)
        {
            if (_matchId == null) { QuitSoon(); return; }
            for (int attempt = 0; attempt < 5; attempt++)
            {
                try
                {
                    await ApiClient.Post<OkResponse>("/server/match-result", new { matchId = _matchId, winnerTeam, players });
                    Debug.Log("[Server] results submitted");
                    break;
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[Server] results attempt {attempt + 1} failed: {e.Message}");
                    if (e.Message.Contains("409")) break;
                    await Task.Delay(3000);
                }
            }
            QuitSoon();
        }

        public async void ReportAbandoned()
        {
            if (_matchId != null)
                try { await ApiClient.Post<OkResponse>("/server/match-abandon", new { matchId = _matchId }); } catch { }
            QuitSoon();
        }

        void QuitSoon() => StartCoroutine(QuitAfter(20f));   // give clients time to see results

        IEnumerator QuitAfter(float s)
        {
            yield return new WaitForSeconds(s);
            _net.Manager.Shutdown();
            Application.Quit(0);
        }
    }
}
