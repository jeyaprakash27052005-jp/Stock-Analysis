using UnityEngine;

namespace Stormdrop
{
    /// <summary>Place these in buildings and around the map. Tier controls the loot table used.</summary>
    public class LootSpawnPoint : MonoBehaviour
    {
        public LootTier tier = LootTier.Medium;
        [Range(1, 4)] public int items = 2;
        public float scatter = 1.2f;

        void OnDrawGizmos()
        {
            Gizmos.color = tier switch { LootTier.Low => Color.white, LootTier.Medium => Color.cyan, LootTier.High => Color.magenta, _ => Color.yellow };
            Gizmos.DrawWireCube(transform.position + Vector3.up * 0.25f, new Vector3(0.5f, 0.5f, 0.5f));
        }
    }
}
