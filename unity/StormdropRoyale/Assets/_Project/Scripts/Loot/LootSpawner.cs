using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Stormdrop
{
    public struct LootEntry
    {
        public int id;
        public string itemId;
        public int amount;
        public Vector3 position;
    }

    public struct LootDrop : INetworkSerializeByMemcpy, IEquatable<LootDrop>
    {
        public int id;
        public FixedString32Bytes itemId;
        public int amount;
        public Vector3 position;
        public bool Equals(LootDrop o) => id == o.id && amount == o.amount;
    }

    /// <summary>
    /// Bandwidth-friendly loot: every machine generates the same world loot from one shared seed,
    /// so only "taken" ids and dropped items are networked (no NetworkObject per item).
    /// Clients only create visuals for loot within a short radius of the local player.
    /// </summary>
    public class LootSpawner : NetworkBehaviour
    {
        public static LootSpawner Instance { get; private set; }

        public float visualRadius = 45f;
        public readonly NetworkVariable<int> Seed = new NetworkVariable<int>(0);
        NetworkList<int> _taken;
        NetworkList<LootDrop> _drops;

        readonly Dictionary<int, LootEntry> _loot = new Dictionary<int, LootEntry>();
        readonly Dictionary<int, LootVisual> _visuals = new Dictionary<int, LootVisual>();
        readonly Stack<LootVisual> _pool = new Stack<LootVisual>();
        int _nextDropId = 1_000_000;
        float _refreshTimer;

        public IEnumerable<LootEntry> All => _loot.Values;

        void Awake()
        {
            _taken = new NetworkList<int>();
            _drops = new NetworkList<LootDrop>();
        }

        public override void OnNetworkSpawn()
        {
            Instance = this;
            if (IsServer) Seed.Value = UnityEngine.Random.Range(1, int.MaxValue);
            Generate(Seed.Value);
            Seed.OnValueChanged += (_, s) => Generate(s);
            foreach (var id in _taken) Remove(id);
            foreach (var d in _drops) AddLocal(d);
            _taken.OnListChanged += e => { if (e.Type == NetworkListEvent<int>.EventType.Add) Remove(e.Value); };
            _drops.OnListChanged += e =>
            {
                if (e.Type == NetworkListEvent<LootDrop>.EventType.Add) AddLocal(e.Value);
            };
        }

        public override void OnNetworkDespawn() { if (Instance == this) Instance = null; }

        void Generate(int seed)
        {
            if (seed == 0) return;
            foreach (var v in _visuals.Values) if (v) Destroy(v.gameObject);
            _visuals.Clear();
            _loot.Clear();
            var db = GameDatabase.Instance;
            // MatchManager lives on the same NetworkObject; on the server its Mode may not be written yet.
            var mode = IsServer ? NetworkBootstrap.Instance.Allocation.Mode : GetComponent<MatchManager>().Mode.Value;
            if (mode == GameMode.ClashSquad || mode == GameMode.TeamDeathmatch) return;   // fixed loadouts, no world loot

            // Same order on every machine: sort spawn points by position
            var points = FindObjectsByType<LootSpawnPoint>(FindObjectsSortMode.None)
                .OrderBy(p => p.transform.position.x).ThenBy(p => p.transform.position.z).ThenBy(p => p.transform.position.y).ToArray();
            var rng = new DeterministicRandom(seed);
            int id = 1;
            foreach (var p in points)
            {
                var table = db.Table(mode == GameMode.Training ? LootTier.High : p.tier);
                for (int i = 0; i < p.items; i++)
                {
                    bool got = table.Roll(ref rng, out var item, out var amount);
                    float ox = rng.Range(-p.scatter, p.scatter), oz = rng.Range(-p.scatter, p.scatter);
                    if (!got) { id++; continue; }
                    _loot[id] = new LootEntry { id = id, itemId = item.id, amount = amount, position = p.transform.position + new Vector3(ox, 0.1f, oz) };
                    id++;
                }
            }
        }

        void AddLocal(LootDrop d)
        {
            if (_taken.Contains(d.id)) return;
            _loot[d.id] = new LootEntry { id = d.id, itemId = d.itemId.ToString(), amount = d.amount, position = d.position };
        }

        void Remove(int id)
        {
            _loot.Remove(id);
            if (_visuals.TryGetValue(id, out var v)) { Release(v); _visuals.Remove(id); }
        }

        // ------------------------------------------------------------------ server
        public bool ServerTryTake(int id, Vector3 playerPos, float range, out LootEntry entry)
        {
            entry = default;
            if (!IsServer || !_loot.TryGetValue(id, out entry)) return false;
            if (Vector3.Distance(playerPos, entry.position) > range) return false;
            _taken.Add(id);
            Remove(id);
            return true;
        }

        public void ServerAddDrop(ItemDefinition item, int amount, Vector3 pos)
        {
            if (!IsServer || item == null || amount <= 0) return;
            var p = pos + new Vector3(UnityEngine.Random.Range(-1f, 1f), 0, UnityEngine.Random.Range(-1f, 1f));
            if (Physics.Raycast(p + Vector3.up * 3f, Vector3.down, out var hit, 20f, GameConstants.EnvironmentMask)) p = hit.point + Vector3.up * 0.1f;
            _drops.Add(new LootDrop { id = _nextDropId++, itemId = GameConstants.Fs32(item.id), amount = amount, position = p });
        }

        // ------------------------------------------------------------------ client visuals
        public LootEntry? Nearest(Vector3 pos, float range)
        {
            LootEntry? best = null;
            float bestD = range;
            foreach (var v in _visuals.Values)
            {
                if (!v || !_loot.TryGetValue(v.id, out var e)) continue;
                float d = Vector3.Distance(pos, e.position);
                if (d < bestD) { bestD = d; best = e; }
            }
            return best;
        }

        public List<LootEntry> NearbyEntries(Vector3 pos, float range) =>
            _visuals.Keys.Where(_loot.ContainsKey).Select(k => _loot[k]).Where(e => Vector3.Distance(pos, e.position) <= range).ToList();

        void Update()
        {
            if (GameBootstrap.IsDedicatedServer || PlayerIdentity.Local == null) return;
            _refreshTimer -= Time.deltaTime;
            if (_refreshTimer > 0) return;
            _refreshTimer = 0.5f;
            Vector3 me = PlayerIdentity.Local.transform.position;
            float r2 = visualRadius * visualRadius;

            var toRemove = new List<int>();
            foreach (var kv in _visuals)
                if (!_loot.TryGetValue(kv.Key, out var e) || (e.position - me).sqrMagnitude > r2 * 1.3f) toRemove.Add(kv.Key);
            foreach (var id in toRemove) { Release(_visuals[id]); _visuals.Remove(id); }

            foreach (var e in _loot.Values)
            {
                if (_visuals.ContainsKey(e.id) || (e.position - me).sqrMagnitude > r2) continue;
                var v = _pool.Count > 0 ? _pool.Pop() : LootVisual.Create();
                v.Show(e, GameDatabase.Instance.Item(e.itemId));
                _visuals[e.id] = v;
            }
        }

        void Release(LootVisual v)
        {
            if (!v) return;
            v.gameObject.SetActive(false);
            _pool.Push(v);
        }
    }
}
