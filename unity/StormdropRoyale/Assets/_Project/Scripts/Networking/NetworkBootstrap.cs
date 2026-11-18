using System;
using System.Collections.Generic;
using System.Text;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Stormdrop
{
    /// <summary>What the dedicated server was told to host (from the backend allocation).</summary>
    public class MatchAllocation
    {
        public string MatchId = "training";
        public GameMode Mode = GameMode.Training;
        public bool IsRanked;
        public bool Night;
        public HashSet<string> ExpectedPlayers = new HashSet<string>();
    }

    /// <summary>
    /// Creates and configures the NetworkManager at runtime, and starts it as a dedicated
    /// server, a client (online match), or a host (offline training ground).
    /// </summary>
    public class NetworkBootstrap : MonoBehaviour
    {
        public static NetworkBootstrap Instance { get; private set; }
        public NetworkManager Manager { get; private set; }
        public MatchAllocation Allocation { get; private set; } = new MatchAllocation();

        /// <summary>Server only: verified ticket for each connected client.</summary>
        public readonly Dictionary<ulong, JoinTicket> Tickets = new Dictionary<ulong, JoinTicket>();

        public string ServerSecret { get; set; }
        public event Action<string> ClientDisconnected;   // reason
        public event Action ClientConnected;

        UnityTransport _transport;
        bool _systemsSpawned;

        void Awake()
        {
            Instance = this;
            var go = new GameObject("NetworkManager");
            DontDestroyOnLoad(go);
            _transport = go.AddComponent<UnityTransport>();
            Manager = go.AddComponent<NetworkManager>();
            if (Manager.NetworkConfig == null) Manager.NetworkConfig = new NetworkConfig();
            var cfg = Manager.NetworkConfig;
            cfg.NetworkTransport = _transport;
            cfg.ConnectionApproval = true;
            cfg.EnableSceneManagement = true;
            cfg.TickRate = GameConstants.TickRate;
            cfg.ClientConnectionBufferTimeout = 15;

            var db = GameDatabase.Instance;
            if (db == null) { Debug.LogError("GameDatabase missing — run Stormdrop ▸ Setup Wizard"); return; }
            // Player objects are spawned by us (ServerSpawnPlayer) once the match systems exist,
            // so the PlayerPrefab slot stays empty and every prefab is registered the same way.
            cfg.PlayerPrefab = null;
            foreach (var prefab in db.AllNetworkPrefabs()) Manager.AddNetworkPrefab(prefab);

            Manager.ConnectionApprovalCallback = ApprovalCheck;
            Manager.OnClientDisconnectCallback += OnDisconnect;
            Manager.OnClientConnectedCallback += id =>
            {
                if (Manager.IsServer && MatchManager.Instance != null) ServerSpawnPlayer(id);
                if (!Manager.IsServer || id == Manager.LocalClientId) ClientConnected?.Invoke();
            };
        }

        // ------------------------------------------------------------------ start modes
        public void StartDedicatedServer(ushort port, MatchAllocation allocation)
        {
            Allocation = allocation;
            _transport.SetConnectionData("0.0.0.0", port, "0.0.0.0");
            if (!Manager.StartServer()) { Debug.LogError("Server failed to start"); Application.Quit(1); return; }
            Debug.Log($"[Server] listening on {port} for match {allocation.MatchId} ({allocation.Mode})");
            LoadGameScene();
        }

        public void StartClient(string host, ushort port, string joinTicket, GameMode mode)
        {
            Allocation = new MatchAllocation { Mode = mode };
            _transport.SetConnectionData(host, port);
            Manager.NetworkConfig.ConnectionData = Encoding.UTF8.GetBytes(joinTicket);
            Manager.StartClient();
        }

        /// <summary>Training ground runs fully on the phone: this device is server + client.</summary>
        public void StartTraining(string nickname, string characterId, string petId)
        {
            Allocation = new MatchAllocation { Mode = GameMode.Training, MatchId = "training" };
            _transport.SetConnectionData("127.0.0.1", GameConstants.DefaultServerPort, "127.0.0.1");
            Manager.NetworkConfig.ConnectionData = Encoding.UTF8.GetBytes(JoinTicket.MakeOffline(nickname, characterId, petId));
            if (Manager.StartHost()) LoadGameScene();
        }

        public void Leave()
        {
            if (Manager.IsListening) Manager.Shutdown();
            _systemsSpawned = false;
            Tickets.Clear();
            if (SceneManager.GetActiveScene().name != GameConstants.BootstrapScene)
                SceneManager.LoadScene(GameConstants.BootstrapScene);
        }

        void LoadGameScene()
        {
            Manager.SceneManager.OnLoadEventCompleted += OnSceneLoaded;
            Manager.SceneManager.LoadScene(GameConstants.GameScene, LoadSceneMode.Single);
        }

        void OnSceneLoaded(string sceneName, LoadSceneMode mode, List<ulong> done, List<ulong> timedOut)
        {
            if (sceneName != GameConstants.GameScene || _systemsSpawned || !Manager.IsServer) return;
            _systemsSpawned = true;
            var systems = Instantiate(GameDatabase.Instance.matchSystemsPrefab);
            systems.GetComponent<NetworkObject>().Spawn();
        }

        // ------------------------------------------------------------------ approval (server)
        void ApprovalCheck(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            string raw = request.Payload != null ? Encoding.UTF8.GetString(request.Payload) : "";
            JoinTicket ticket = null;
            string error = null;

            bool ok = Allocation.Mode == GameMode.Training && Manager.IsHost && JoinTicket.TryParseOffline(raw, out ticket);
            if (!ok)
            {
                ok = !string.IsNullOrEmpty(ServerSecret) && JoinTicket.TryVerify(raw, ServerSecret, out ticket, out error);
                if (ok && ticket.MatchId != Allocation.MatchId) { ok = false; error = "wrong match"; }
                if (ok && Allocation.ExpectedPlayers.Count > 0 && !Allocation.ExpectedPlayers.Contains(ticket.PlayerId)) { ok = false; error = "not in match"; }
                if (ok && MatchManager.Instance != null && !MatchManager.Instance.AcceptingPlayers(ticket.PlayerId)) { ok = false; error = "match already started"; }
                if (ok) foreach (var t in Tickets.Values) if (t.PlayerId == ticket.PlayerId) { ok = false; error = "already connected"; }
            }

            response.Approved = ok;
            response.Reason = ok ? null : error ?? "rejected";
            response.CreatePlayerObject = false;   // spawned in ServerSpawnPlayer when the match is ready
            if (ok) Tickets[request.ClientNetworkId] = ticket;
            else Debug.LogWarning($"[Server] rejected client {request.ClientNetworkId}: {error}");
        }

        /// <summary>Server: create the player object for a connected, approved client (once).</summary>
        public void ServerSpawnPlayer(ulong clientId)
        {
            if (!Manager.IsServer || !Tickets.ContainsKey(clientId)) return;
            if (!Manager.ConnectedClients.TryGetValue(clientId, out var client) || client.PlayerObject != null) return;
            var go = Instantiate(GameDatabase.Instance.playerPrefab, new Vector3(0, 400, 0), Quaternion.identity);
            go.GetComponent<NetworkObject>().SpawnAsPlayerObject(clientId, true);
        }

        /// <summary>Server: called by MatchManager when it spawns, for clients that connected before it existed.</summary>
        public void ServerSpawnMissingPlayers()
        {
            foreach (var id in Manager.ConnectedClientsIds) ServerSpawnPlayer(id);
        }

        void OnDisconnect(ulong clientId)
        {
            if (Manager.IsServer && clientId != NetworkManager.ServerClientId)
            {
                MatchManager.Instance?.OnClientLeft(clientId);
                Tickets.Remove(clientId);
                return;
            }
            // We are a client and lost the server (or were rejected)
            var reason = Manager.DisconnectReason;
            ClientDisconnected?.Invoke(string.IsNullOrEmpty(reason) ? "Disconnected" : reason);
        }
    }
}
