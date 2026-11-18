using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Stormdrop
{
    public enum MatchPhase : byte { Waiting, Plane, InProgress, RoundIntermission, Ended }

    /// <summary>Sent to each player when their match is over (eliminated or won).</summary>
    public struct MatchResultMessage : INetworkSerializable
    {
        public FixedString64Bytes matchId;
        public byte mode;
        public bool won;
        public int placement, totalTeams, kills, damage, headshots, revives, survivalSeconds;

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref matchId); s.SerializeValue(ref mode); s.SerializeValue(ref won);
            s.SerializeValue(ref placement); s.SerializeValue(ref totalTeams); s.SerializeValue(ref kills);
            s.SerializeValue(ref damage); s.SerializeValue(ref headshots); s.SerializeValue(ref revives);
            s.SerializeValue(ref survivalSeconds);
        }
    }

    /// <summary>Server-only bookkeeping per player.</summary>
    public class PlayerMatchStats
    {
        public string playerId, nickname, characterId;
        public int team, kills, assists, damage, headshots, revives, placement;
        public double joinTime, deathTime = -1;
        public bool suspicious, resultsSent;
        public string suspicionReason;
        public readonly HashSet<ulong> damagedBy = new HashSet<ulong>();   // for assists
    }

    /// <summary>
    /// Authoritative match flow for every mode: waiting room → plane → zone phases → results
    /// (Battle Royale), rounds (Clash Squad), respawns (Team Deathmatch) or free play (Training).
    /// </summary>
    public class MatchManager : NetworkBehaviour
    {
        public static MatchManager Instance { get; private set; }

        public readonly NetworkVariable<MatchPhase> Phase = new NetworkVariable<MatchPhase>(MatchPhase.Waiting);
        public readonly NetworkVariable<GameMode> Mode = new NetworkVariable<GameMode>(GameMode.Training);
        public readonly NetworkVariable<bool> Night = new NetworkVariable<bool>(false);
        public readonly NetworkVariable<int> PlayersAlive = new NetworkVariable<int>(0);
        public readonly NetworkVariable<int> TeamsAlive = new NetworkVariable<int>(0);
        public readonly NetworkVariable<double> PhaseEndsAt = new NetworkVariable<double>(0);
        public readonly NetworkVariable<int> ScoreA = new NetworkVariable<int>(0);
        public readonly NetworkVariable<int> ScoreB = new NetworkVariable<int>(0);
        public readonly NetworkVariable<int> Round = new NetworkVariable<int>(0);
        public readonly NetworkVariable<FixedString64Bytes> MatchId = new NetworkVariable<FixedString64Bytes>();

        public System.Action<string, string, string, bool, bool> KillFeed;   // killer, victim, weapon, headshot, knock
        public System.Action<MatchResultMessage> LocalResults;

        [Header("Rules")]
        public float waitForPlayersSeconds = 60f;
        public int clashSquadRoundsToWin = 4;
        public float clashSquadRoundSeconds = 100f;
        public int tdmKillsToWin = 30;
        public float tdmMinutes = 8f;
        public float tdmRespawnSeconds = 3f;

        readonly Dictionary<ulong, PlayerMatchStats> _stats = new Dictionary<ulong, PlayerMatchStats>();   // key: player NetworkObjectId
        readonly Dictionary<int, int> _teamPlacement = new Dictionary<int, int>();
        double _matchStart;
        int _totalTeams;
        bool _planeDone;

        public bool IsRespawnMode => Mode.Value == GameMode.TeamDeathmatch || Mode.Value == GameMode.Training;
        double Now => NetworkManager.ServerTime.Time;

        public override void OnNetworkSpawn()
        {
            Instance = this;
            if (!IsServer) return;
            var alloc = NetworkBootstrap.Instance.Allocation;
            Mode.Value = alloc.Mode;
            Night.Value = alloc.Night;
            MatchId.Value = alloc.MatchId;
            StartCoroutine(ServerFlow());
            NetworkBootstrap.Instance.ServerSpawnMissingPlayers();
        }

        public override void OnNetworkDespawn()
        {
            if (Instance == this) Instance = null;
        }

        public bool AcceptingPlayers(string playerId) =>
            Phase.Value == MatchPhase.Waiting || IsRespawnMode;

        // ------------------------------------------------------------------ registration (server)
        public void RegisterPlayer(PlayerIdentity p)
        {
            if (!IsServer) return;
            _stats[p.NetworkObjectId] = new PlayerMatchStats
            {
                playerId = p.BackendPlayerId, nickname = p.Nickname.Value.ToString(), team = p.Team.Value,
                characterId = p.CharacterId.Value.ToString(), joinTime = Now,
            };
            if (Phase.Value == MatchPhase.Waiting)
            {
                p.Controller.ServerTeleport(WaitingAreaPosition(p.Team.Value));
                p.Health.ServerSetLife(PlayerLifeState.Alive);
                p.Health.ServerSetInvulnerable(true);
            }
            else if (IsRespawnMode) StartCoroutine(Respawn(p, 0.5f));
            RecountAlive();
        }

        public void OnPlayerRemoved(PlayerIdentity p)
        {
            if (!IsServer || !_stats.TryGetValue(p.NetworkObjectId, out var s)) return;
            if (Phase.Value != MatchPhase.Ended && s.deathTime < 0 && !IsRespawnMode)
            {
                s.deathTime = Now;   // disconnect counts as elimination
                CheckTeamEliminated(s.team);
            }
            RecountAlive();
        }

        public void OnClientLeft(ulong clientId) { /* player object despawn → OnPlayerRemoved handles it */ }

        public PlayerMatchStats StatsFor(PlayerIdentity p) => p != null && _stats.TryGetValue(p.NetworkObjectId, out var s) ? s : null;

        public void FlagSuspicious(PlayerIdentity p, string reason)
        {
            var s = StatsFor(p);
            if (s == null || s.suspicious) return;
            s.suspicious = true;
            s.suspicionReason = reason;
            Debug.LogWarning($"[AntiCheat] {s.nickname}: {reason}");
        }

        public void RecordDamage(PlayerIdentity attacker, PlayerIdentity victim, float amount)
        {
            var a = StatsFor(attacker);
            if (a != null && attacker != victim) a.damage += Mathf.RoundToInt(amount);
            var v = StatsFor(victim);
            if (v != null && attacker != null) v.damagedBy.Add(attacker.NetworkObjectId);
        }

        public void RecordRevive(PlayerIdentity reviver) { var s = StatsFor(reviver); if (s != null) s.revives++; }

        // ------------------------------------------------------------------ flow (server)
        IEnumerator ServerFlow()
        {
            var nb = NetworkBootstrap.Instance;
            double deadline = Now + (Mode.Value == GameMode.Training ? 0.3 : waitForPlayersSeconds);
            PhaseEndsAt.Value = deadline;
            while (Now < deadline)
            {
                int expected = nb.Allocation.ExpectedPlayers.Count;
                if (expected > 0 && _stats.Count >= expected) break;
                yield return new WaitForSeconds(0.5f);
            }
            if (_stats.Count == 0)
            {
                Debug.LogWarning("[Server] nobody joined — abandoning match");
                DedicatedServerAgent.Instance?.ReportAbandoned();
                yield break;
            }

            _matchStart = Now;
            _totalTeams = PlayerIdentity.All.Select(p => p.Team.Value).Distinct().Count();
            TeamsAlive.Value = _totalTeams;
            DedicatedServerAgent.Instance?.ReportStarted();
            foreach (var p in PlayerIdentity.All) p.Health.ServerSetInvulnerable(false);

            switch (Mode.Value)
            {
                case GameMode.BrSolo: case GameMode.BrDuo: case GameMode.BrSquad:
                    yield return BattleRoyaleFlow(); break;
                case GameMode.ClashSquad:
                    yield return ClashSquadFlow(); break;
                case GameMode.TeamDeathmatch:
                    yield return DeathmatchFlow(); break;
                default:
                    Phase.Value = MatchPhase.InProgress;
                    foreach (var p in PlayerIdentity.All) StartCoroutine(Respawn(p, 0f));
                    break;
            }
        }

        IEnumerator BattleRoyaleFlow()
        {
            Phase.Value = MatchPhase.Plane;
            VehicleSpawnPoint.ServerSpawnAll();
            var plane = Instantiate(GameDatabase.Instance.planePrefab).GetComponent<PlaneController>();
            plane.GetComponent<NetworkObject>().Spawn();
            plane.ServerBeginFlight(Random.Range(0, 1000));
            foreach (var p in PlayerIdentity.All)
            {
                p.Health.ServerResetForSpawn();
                p.Health.ServerSetLife(PlayerLifeState.InPlane);
            }
            RecountAlive();
            while (!_planeDone) yield return null;
            Phase.Value = MatchPhase.InProgress;
            SafeZoneManager.Instance.ServerBegin();
        }

        public void OnPlaneFinished() => _planeDone = true;

        IEnumerator ClashSquadFlow()
        {
            var weapons = GameDatabase.Instance.clashSquadRoundWeapons;
            while (ScoreA.Value < clashSquadRoundsToWin && ScoreB.Value < clashSquadRoundsToWin)
            {
                Round.Value++;
                Phase.Value = MatchPhase.RoundIntermission;
                var weapon = weapons != null && weapons.Length > 0 ? weapons[Mathf.Min(Round.Value - 1, weapons.Length - 1)] : null;
                foreach (var p in PlayerIdentity.All)
                {
                    p.Health.ServerResetForSpawn();
                    p.Controller.ServerTeleport(SpawnPoint.Pick(p.Team.Value).position);
                    p.Inventory.ServerResetLoadout(weapon);
                    p.Health.ServerSetInvulnerable(true);
                }
                PhaseEndsAt.Value = Now + 5;
                yield return new WaitForSeconds(5f);
                foreach (var p in PlayerIdentity.All) p.Health.ServerSetInvulnerable(false);
                Phase.Value = MatchPhase.InProgress;
                PhaseEndsAt.Value = Now + clashSquadRoundSeconds;

                int winner = -1;
                while (winner < 0)
                {
                    bool aAlive = AnyStanding(0), bAlive = AnyStanding(1);
                    if (!aAlive || !bAlive) winner = aAlive ? 0 : bAlive ? 1 : 2;
                    else if (Now >= PhaseEndsAt.Value) winner = TeamHealth(0) >= TeamHealth(1) ? 0 : 1;
                    yield return new WaitForSeconds(0.25f);
                }
                if (winner == 0) ScoreA.Value++; else if (winner == 1) ScoreB.Value++;
                yield return new WaitForSeconds(3f);
            }
            EndMatch(ScoreA.Value > ScoreB.Value ? 0 : 1);
        }

        IEnumerator DeathmatchFlow()
        {
            Phase.Value = MatchPhase.InProgress;
            PhaseEndsAt.Value = Now + tdmMinutes * 60;
            foreach (var p in PlayerIdentity.All) StartCoroutine(Respawn(p, 0f));
            while (Now < PhaseEndsAt.Value && ScoreA.Value < tdmKillsToWin && ScoreB.Value < tdmKillsToWin)
                yield return new WaitForSeconds(0.5f);
            EndMatch(ScoreA.Value >= ScoreB.Value ? 0 : 1);
        }

        IEnumerator Respawn(PlayerIdentity p, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (p == null || !p.IsSpawned || Phase.Value == MatchPhase.Ended) yield break;
            p.Health.ServerResetForSpawn();
            p.Inventory.ServerResetLoadout(Mode.Value == GameMode.Training ? GameDatabase.Instance.trainingRifle
                                                                           : GameDatabase.Instance.clashSquadRoundWeapons?.LastOrDefault());
            p.Controller.ServerTeleport(SpawnPoint.Pick(Mode.Value == GameMode.Training ? -1 : p.Team.Value).position);
            p.Health.ServerSetLife(PlayerLifeState.Alive);
            RecountAlive();
        }

        // ------------------------------------------------------------------ combat events (server)
        public void OnPlayerKnocked(PlayerIdentity victim, PlayerIdentity attacker, string weapon)
        {
            AnnounceRpc(attacker ? attacker.Nickname.Value : default, victim.Nickname.Value, weapon, false, true);
            if (attacker != null) attacker.Skills.ServerOnKnockedEnemy();
            // If no teammate is still standing, the whole team bleeds out now.
            if (!AnyStanding(victim.Team.Value))
                foreach (var mate in PlayerIdentity.All.Where(x => x.Team.Value == victim.Team.Value && x.Health.Life.Value == PlayerLifeState.Knocked).ToList())
                    mate.Health.ServerKill(null, "bleed-out", false);
        }

        public void OnPlayerKilled(PlayerIdentity victim, PlayerIdentity killer, string weapon, bool headshot)
        {
            var vs = StatsFor(victim);
            var ks = StatsFor(killer);
            if (ks != null && killer != victim)
            {
                ks.kills++;
                if (headshot) ks.headshots++;
                if (Mode.Value == GameMode.TeamDeathmatch) { if (killer.Team.Value == 0) ScoreA.Value++; else ScoreB.Value++; }
            }
            if (vs != null)
                foreach (var id in vs.damagedBy)
                    if (killer == null || id != killer.NetworkObjectId)
                        if (_stats.TryGetValue(id, out var a)) a.assists++;
            vs?.damagedBy.Clear();

            AnnounceRpc(killer ? killer.Nickname.Value : default, victim.Nickname.Value, weapon, headshot, false);
            if (GameConstants.IsBattleRoyale(Mode.Value)) victim.Inventory.ServerDropAll(victim.transform.position);

            if (IsRespawnMode) { StartCoroutine(Respawn(victim, tdmRespawnSeconds)); RecountAlive(); return; }
            if (vs != null) vs.deathTime = Now;
            RecountAlive();
            if (GameConstants.IsBattleRoyale(Mode.Value)) CheckTeamEliminated(victim.Team.Value);
        }

        bool AnyStanding(int team) => PlayerIdentity.All.Any(p => p.Team.Value == team &&
            (p.Health.Life.Value == PlayerLifeState.Alive || p.Health.Life.Value == PlayerLifeState.InPlane || p.Health.Life.Value == PlayerLifeState.Parachuting));

        public bool TeamHasStanding(int team, PlayerIdentity except) => PlayerIdentity.All.Any(p => p != except && p.Team.Value == team &&
            (p.Health.Life.Value == PlayerLifeState.Alive || p.Health.Life.Value == PlayerLifeState.Parachuting));

        float TeamHealth(int team) => PlayerIdentity.All.Where(p => p.Team.Value == team && p.Health.Life.Value == PlayerLifeState.Alive).Sum(p => p.Health.Health.Value);

        void RecountAlive()
        {
            if (!IsServer) return;
            PlayersAlive.Value = PlayerIdentity.All.Count(p => p.Health.Life.Value != PlayerLifeState.Dead && p.Health.Life.Value != PlayerLifeState.Spectating);
        }

        void CheckTeamEliminated(int team)
        {
            if (Phase.Value == MatchPhase.Ended || _teamPlacement.ContainsKey(team)) return;
            bool anyLeft = PlayerIdentity.All.Any(p => p.Team.Value == team && p.Health.Life.Value != PlayerLifeState.Dead && p.Health.Life.Value != PlayerLifeState.Spectating);
            if (anyLeft) return;
            _teamPlacement[team] = TeamsAlive.Value;
            TeamsAlive.Value = Mathf.Max(0, TeamsAlive.Value - 1);
            SendResultsToTeam(team, false, _teamPlacement[team]);

            if (TeamsAlive.Value <= 1)
            {
                var winner = PlayerIdentity.All.Select(p => p.Team.Value).FirstOrDefault(t => !_teamPlacement.ContainsKey(t));
                EndMatch(winner);
            }
        }

        public void EndMatch(int winnerTeam)
        {
            if (Phase.Value == MatchPhase.Ended) return;
            Phase.Value = MatchPhase.Ended;
            if (GameConstants.IsBattleRoyale(Mode.Value))
            {
                _teamPlacement[winnerTeam] = 1;
                SendResultsToTeam(winnerTeam, true, 1);
            }
            else
            {
                SendResultsToTeam(0, winnerTeam == 0, winnerTeam == 0 ? 1 : 2);
                SendResultsToTeam(1, winnerTeam == 1, winnerTeam == 1 ? 1 : 2);
            }
            SafeZoneManager.Instance?.ServerStop();

            var report = _stats.Values.Where(s => !string.IsNullOrEmpty(s.playerId) && s.playerId != "local").Select(s => new MatchReportPlayer
            {
                playerId = s.playerId, team = s.team, characterId = s.characterId,
                placement = s.placement > 0 ? s.placement : _teamPlacement.TryGetValue(s.team, out var pl) ? pl : _totalTeams,
                kills = s.kills, assists = s.assists, damage = s.damage, headshots = s.headshots, revives = s.revives,
                survivalSeconds = Mathf.RoundToInt((float)((s.deathTime >= 0 ? s.deathTime : Now) - Mathf.Max((float)_matchStart, (float)s.joinTime))),
                suspicious = s.suspicious, suspicionReason = s.suspicionReason,
            }).ToList();
            DedicatedServerAgent.Instance?.ReportResults(winnerTeam, report);
        }

        void SendResultsToTeam(int team, bool won, int placement)
        {
            foreach (var p in PlayerIdentity.All.Where(x => x.Team.Value == team))
            {
                var s = StatsFor(p);
                if (s == null || s.resultsSent) continue;
                s.resultsSent = true;
                s.placement = placement;
                var msg = new MatchResultMessage
                {
                    matchId = MatchId.Value, mode = (byte)Mode.Value, won = won, placement = placement, totalTeams = _totalTeams,
                    kills = s.kills, damage = s.damage, headshots = s.headshots, revives = s.revives,
                    survivalSeconds = Mathf.RoundToInt((float)((s.deathTime >= 0 ? s.deathTime : Now) - _matchStart)),
                };
                ResultsRpc(msg, RpcTarget.Single(p.OwnerClientId, RpcTargetUse.Temp));
            }
        }

        // ------------------------------------------------------------------ RPCs to clients
        [Rpc(SendTo.ClientsAndHost)]
        void AnnounceRpc(FixedString32Bytes killer, FixedString32Bytes victim, FixedString32Bytes weapon, bool headshot, bool knock)
        {
            KillFeed?.Invoke(killer.ToString(), victim.ToString(), weapon.ToString(), headshot, knock);
        }

        [Rpc(SendTo.SpecifiedInParams)]
        void ResultsRpc(MatchResultMessage msg, RpcParams rpcParams = default)
        {
            LocalResults?.Invoke(msg);
            AnalyticsService.LogMatchEnd(msg);
        }

        Vector3 WaitingAreaPosition(int team)
        {
            var sp = SpawnPoint.Pick(-1);
            return sp.position + new Vector3(Random.Range(-15f, 15f), 0, Random.Range(-15f, 15f));
        }
    }
}
