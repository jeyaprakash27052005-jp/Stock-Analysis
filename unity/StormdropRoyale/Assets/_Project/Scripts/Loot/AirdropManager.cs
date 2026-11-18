using Unity.Netcode;
using UnityEngine;

namespace Stormdrop
{
    /// <summary>Server: drops a crate with rare loot inside the upcoming safe zone.</summary>
    public class AirdropManager : NetworkBehaviour
    {
        public static AirdropManager Instance { get; private set; }
        public static event System.Action<Vector3> AirdropIncoming;   // local, for minimap + HUD

        public override void OnNetworkSpawn() => Instance = this;
        public override void OnNetworkDespawn() { if (Instance == this) Instance = null; }

        public void ServerDrop(Vector2 zoneCenter, float zoneRadius)
        {
            if (!IsServer || GameDatabase.Instance.airdropPrefab == null) return;
            Vector2 p = zoneCenter + Random.insideUnitCircle * Mathf.Max(10f, zoneRadius * 0.7f);
            var pos = new Vector3(p.x, 350f, p.y);
            var crate = Instantiate(GameDatabase.Instance.airdropPrefab, pos, Quaternion.identity);
            crate.GetComponent<NetworkObject>().Spawn();
            IncomingRpc(new Vector3(p.x, 0, p.y));
        }

        [Rpc(SendTo.ClientsAndHost)]
        void IncomingRpc(Vector3 groundPos) => AirdropIncoming?.Invoke(groundPos);
    }
}
