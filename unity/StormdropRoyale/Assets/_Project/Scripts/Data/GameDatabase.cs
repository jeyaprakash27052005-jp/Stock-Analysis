using System.Collections.Generic;
using UnityEngine;

namespace Stormdrop
{
    /// <summary>Registry of all game content. Lives at Resources/GameDatabase (created by the setup wizard).</summary>
    [CreateAssetMenu(menuName = "Stormdrop/Game Database")]
    public class GameDatabase : ScriptableObject
    {
        public static GameDatabase Instance;

        public ItemDefinition[] items;            // includes WeaponData and AttachmentData
        public CharacterData[] characters;
        public PetData[] pets;
        public LootTable[] lootTables;            // indexed by LootTier
        public ZoneConfig zoneConfig;

        [Header("Network prefabs")]
        public GameObject playerPrefab;
        public GameObject matchSystemsPrefab;     // MatchManager + SafeZone + Loot + Airdrops
        public GameObject planePrefab;
        public GameObject airdropPrefab;
        public GameObject grenadePrefab;
        public GameObject foamWallPrefab;
        public GameObject[] vehiclePrefabs;       // jeep, motorbike, buggy, boat

        [Header("Clash Squad round loadouts (by round, last repeats)")]
        public WeaponData[] clashSquadRoundWeapons;
        public WeaponData trainingRifle;

        Dictionary<string, ItemDefinition> _items;
        Dictionary<string, CharacterData> _chars;
        Dictionary<string, PetData> _pets;

        void Build()
        {
            _items = new Dictionary<string, ItemDefinition>();
            foreach (var i in items) if (i != null) _items[i.id] = i;
            _chars = new Dictionary<string, CharacterData>();
            foreach (var c in characters) if (c != null) _chars[c.id] = c;
            _pets = new Dictionary<string, PetData>();
            foreach (var p in pets) if (p != null) _pets[p.id] = p;
        }

        public ItemDefinition Item(string id)
        {
            if (_items == null) Build();
            return id != null && _items.TryGetValue(id, out var i) ? i : null;
        }

        public CharacterData Character(string id)
        {
            if (_chars == null) Build();
            return id != null && _chars.TryGetValue(id, out var c) ? c : (characters.Length > 0 ? characters[0] : null);
        }

        public PetData Pet(string id)
        {
            if (_pets == null) Build();
            return id != null && _pets.TryGetValue(id, out var p) ? p : null;
        }

        public LootTable Table(LootTier tier)
        {
            foreach (var t in lootTables) if (t != null && t.tier == tier) return t;
            return lootTables.Length > 0 ? lootTables[0] : null;
        }

        public IEnumerable<GameObject> AllNetworkPrefabs()
        {
            if (playerPrefab) yield return playerPrefab;
            if (matchSystemsPrefab) yield return matchSystemsPrefab;
            if (planePrefab) yield return planePrefab;
            if (airdropPrefab) yield return airdropPrefab;
            if (grenadePrefab) yield return grenadePrefab;
            if (foamWallPrefab) yield return foamWallPrefab;
            if (vehiclePrefabs != null) foreach (var v in vehiclePrefabs) if (v) yield return v;
        }
    }
}
