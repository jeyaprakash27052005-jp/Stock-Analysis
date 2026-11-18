using Unity.Netcode;
using UnityEngine;

namespace Stormdrop
{
    /// <summary>Frag, smoke or flash grenade. Simulated on the server, replicated with a NetworkTransform.</summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Grenade : NetworkBehaviour
    {
        public ThrowableType type;
        [HideInInspector] public PlayerIdentity thrower;
        public float fuse = 3f;
        public float fragRadius = 7f;
        public float fragDamage = 160f;
        public float flashRadius = 14f;
        public GameObject smokeEffectPrefab;     // optional particle system

        readonly NetworkVariable<ThrowableType> _type = new NetworkVariable<ThrowableType>();
        Rigidbody _rb;
        float _explodeAt;

        void Awake() => _rb = GetComponent<Rigidbody>();

        public override void OnNetworkSpawn()
        {
            _rb.isKinematic = !IsServer;
        }

        public void ServerLaunch(Vector3 velocity, ThrowableType t)
        {
            _type.Value = t;
            _rb.linearVelocity = velocity;
            _explodeAt = Time.time + fuse;
        }

        void Update()
        {
            if (!IsServer || !IsSpawned || Time.time < _explodeAt || _explodeAt == 0) return;
            _explodeAt = 0;
            Explode();
        }

        void Explode()
        {
            Vector3 p = transform.position + Vector3.up * 0.3f;
            switch (_type.Value)
            {
                case ThrowableType.Frag:
                    foreach (var pl in PlayerIdentity.All)
                    {
                        Vector3 target = pl.transform.position + Vector3.up;
                        float d = Vector3.Distance(p, target);
                        if (d > fragRadius || Physics.Linecast(p, target, GameConstants.EnvironmentMask, QueryTriggerInteraction.Ignore)) continue;
                        pl.Health.ApplyDamage(fragDamage * (1f - d / fragRadius), thrower, false, "Frag Grenade", DamageKind.Explosion);
                    }
                    foreach (var v in FindObjectsByType<VehicleController>(FindObjectsSortMode.None))
                        if (Vector3.Distance(v.transform.position, p) < fragRadius) v.ServerDamage(fragDamage, thrower);
                    break;
                case ThrowableType.Flash:
                    foreach (var pl in PlayerIdentity.All)
                    {
                        Vector3 eye = pl.transform.position + Vector3.up * 1.6f;
                        float d = Vector3.Distance(p, eye);
                        if (d > flashRadius || Physics.Linecast(p, eye, GameConstants.EnvironmentMask, QueryTriggerInteraction.Ignore)) continue;
                        float facing = Vector3.Dot(pl.transform.forward, (p - eye).normalized);
                        FlashRpc(Mathf.Lerp(1.5f, 4f, (facing + 1f) / 2f) * (1f - d / flashRadius), RpcTarget.Single(pl.OwnerClientId, RpcTargetUse.Temp));
                    }
                    break;
            }
            EffectRpc(p, _type.Value);
            Invoke(nameof(Despawn), _type.Value == ThrowableType.Smoke ? 0.2f : 0.1f);
        }

        void Despawn() { if (IsSpawned) NetworkObject.Despawn(true); }

        [Rpc(SendTo.ClientsAndHost)]
        void EffectRpc(Vector3 pos, ThrowableType t)
        {
            if (t == ThrowableType.Smoke)
            {
                var smoke = smokeEffectPrefab != null ? Instantiate(smokeEffectPrefab, pos, Quaternion.identity) : MakePlaceholderSmoke(pos);
                Destroy(smoke, 18f);
            }
            else if (t == ThrowableType.Frag) HUDController.Instance?.ShowExplosion(pos);
        }

        [Rpc(SendTo.SpecifiedInParams)]
        void FlashRpc(float seconds, RpcParams rpc = default) => HUDController.Instance?.Flash(seconds);

        static GameObject MakePlaceholderSmoke(Vector3 pos)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(go.GetComponent<Collider>());
            go.transform.position = pos;
            go.transform.localScale = Vector3.one * 9f;
            var r = go.GetComponent<Renderer>();
            r.material = new Material(Shader.Find("Sprites/Default")) { color = new Color(0.85f, 0.85f, 0.85f, 0.85f) };
            return go;
        }
    }
}
