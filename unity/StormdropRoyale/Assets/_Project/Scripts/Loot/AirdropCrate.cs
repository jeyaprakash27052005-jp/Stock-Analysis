using Unity.Netcode;
using UnityEngine;

namespace Stormdrop
{
    /// <summary>Falls slowly on a parachute; any player can open it once it lands.</summary>
    public class AirdropCrate : NetworkBehaviour
    {
        public float fallSpeed = 9f;
        public int rolls = 5;
        public readonly NetworkVariable<bool> Landed = new NetworkVariable<bool>(false);

        void Update()
        {
            if (!IsServer || !IsSpawned || Landed.Value) return;
            transform.position += Vector3.down * fallSpeed * Time.deltaTime;
            if (Physics.Raycast(transform.position + Vector3.up, Vector3.down, out var hit, 1.6f, GameConstants.EnvironmentMask))
            {
                transform.position = hit.point;
                Landed.Value = true;
            }
        }

        [Rpc(SendTo.Server)]
        public void OpenRpc(RpcParams rpc = default)
        {
            if (!Landed.Value) return;
            var client = NetworkManager.ConnectedClients.TryGetValue(rpc.Receive.SenderClientId, out var c) ? c : null;
            if (client?.PlayerObject == null) return;
            var player = client.PlayerObject.GetComponent<PlayerIdentity>();
            if (!player.Health.IsAlive || Vector3.Distance(player.transform.position, transform.position) > 3.5f) return;

            var table = GameDatabase.Instance.Table(LootTier.Airdrop);
            var rng = new DeterministicRandom(Random.Range(1, int.MaxValue));
            for (int i = 0; i < rolls; i++)
                if (table.Roll(ref rng, out var item, out var amount)) LootSpawner.Instance.ServerAddDrop(item, amount, transform.position);
            NetworkObject.Despawn(true);
        }
    }
}
