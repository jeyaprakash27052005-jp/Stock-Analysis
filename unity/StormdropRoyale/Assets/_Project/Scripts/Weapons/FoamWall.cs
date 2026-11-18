using Unity.Netcode;
using UnityEngine;

namespace Stormdrop
{
    /// <summary>Deployable cover: blocks bullets (it is on the Environment layer) until destroyed or expired.</summary>
    public class FoamWall : NetworkBehaviour
    {
        public float maxHealth = 800f;
        public float lifetime = 25f;
        public readonly NetworkVariable<float> Health = new NetworkVariable<float>(800f);

        float _expires;

        public override void OnNetworkSpawn()
        {
            if (!IsServer) return;
            Health.Value = maxHealth;
            _expires = Time.time + lifetime;
        }

        public void ServerDamage(float dmg)
        {
            if (!IsServer) return;
            Health.Value -= dmg;
            if (Health.Value <= 0) NetworkObject.Despawn(true);
        }

        void Update()
        {
            if (IsServer && IsSpawned && Time.time > _expires) NetworkObject.Despawn(true);
            if (!IsServer && IsSpawned)
            {
                var r = GetComponentInChildren<Renderer>();
                if (r) r.material.color = Color.Lerp(new Color(1f, 0.4f, 0.3f, 0.8f), new Color(0.5f, 0.85f, 1f, 0.8f), Health.Value / maxHealth);
            }
        }
    }
}
