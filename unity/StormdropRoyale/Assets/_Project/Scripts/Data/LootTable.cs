using System;
using UnityEngine;

namespace Stormdrop
{
    public enum LootTier : byte { Low, Medium, High, Airdrop }

    [CreateAssetMenu(menuName = "Stormdrop/Loot Table")]
    public class LootTable : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public ItemDefinition item;
            public int weight;
            public int minAmount, maxAmount;   // 0 = use item.pickupAmount
        }

        public LootTier tier;
        public Entry[] entries;
        [Range(0, 1)] public float emptyChance = 0.25f;
        public int rollsPerPoint = 1;

        int _total = -1;

        public bool Roll(ref DeterministicRandom rng, out ItemDefinition item, out int amount)
        {
            item = null; amount = 0;
            if (entries == null || entries.Length == 0) return false;
            if (rng.Value() < emptyChance) return false;
            if (_total < 0) { _total = 0; foreach (var e in entries) _total += Mathf.Max(0, e.weight); }
            int roll = rng.Range(0, Mathf.Max(1, _total));
            foreach (var e in entries)
            {
                roll -= Mathf.Max(0, e.weight);
                if (roll >= 0) continue;
                item = e.item;
                amount = e.maxAmount > 0 ? rng.Range(e.minAmount, e.maxAmount + 1) : e.item.pickupAmount;
                return item != null;
            }
            return false;
        }
    }
}
