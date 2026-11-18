using Unity.Netcode;
using UnityEngine;

namespace Stormdrop
{
    /// <summary>Server spawns a vehicle here when the match systems start (BR modes only).</summary>
    public class VehicleSpawnPoint : MonoBehaviour
    {
        public VehicleType type = VehicleType.Jeep;
        [Range(0, 1)] public float chance = 0.7f;

        public static void ServerSpawnAll()
        {
            var db = GameDatabase.Instance;
            foreach (var sp in FindObjectsByType<VehicleSpawnPoint>(FindObjectsSortMode.None))
            {
                if (Random.value > sp.chance) continue;
                GameObject prefab = null;
                foreach (var v in db.vehiclePrefabs)
                    if (v && v.GetComponent<VehicleController>().type == sp.type) prefab = v;
                if (prefab == null) continue;
                var go = Instantiate(prefab, sp.transform.position + Vector3.up * 0.5f, sp.transform.rotation);
                go.GetComponent<NetworkObject>().Spawn();
            }
        }

        void OnDrawGizmos()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(transform.position + Vector3.up, new Vector3(2, 1.5f, 4));
        }
    }
}
