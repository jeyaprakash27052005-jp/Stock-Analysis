using Unity.Netcode;
using UnityEngine;

namespace Stormdrop
{
    public enum VehicleType : byte { Jeep, Motorbike, Buggy, Boat }

    /// <summary>
    /// Arcade vehicle (cheap enough for 2 GB phones: one Rigidbody, raycast ground check, no wheel colliders).
    /// The server hands ownership to the driver so driving feels instant; fuel and health stay server-side.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class VehicleController : NetworkBehaviour
    {
        public VehicleType type = VehicleType.Jeep;
        public Transform[] seats;                    // 0 = driver
        public float maxSpeed = 28f;
        public float acceleration = 14f;
        public float turnSpeed = 70f;
        public float maxHealth = 1000f;
        public float maxFuel = 100f;
        public float fuelPerSecond = 0.6f;

        public readonly NetworkVariable<float> Health = new NetworkVariable<float>(1000f);
        public readonly NetworkVariable<float> Fuel = new NetworkVariable<float>(100f);
        public NetworkList<ulong> Occupants;          // NetworkObjectId per seat, 0 = empty

        Rigidbody _rb;
        bool IsBoat => type == VehicleType.Boat;

        void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            Occupants = new NetworkList<ulong>();
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                Health.Value = maxHealth;
                Fuel.Value = Random.Range(maxFuel * 0.4f, maxFuel);
                for (int i = 0; i < seats.Length; i++) Occupants.Add(0);
            }
            Occupants.OnListChanged += _ => RefreshLocalSeat();
            OnGainedOwnership();
        }

        public override void OnGainedOwnership() { if (_rb) _rb.isKinematic = !IsOwner; }
        public override void OnLostOwnership() { if (_rb) _rb.isKinematic = !IsOwner; }

        public Transform SeatTransform(int i) => seats != null && i >= 0 && i < seats.Length ? seats[i] : transform;

        public int SeatOf(ulong playerObjectId)
        {
            for (int i = 0; i < Occupants.Count; i++) if (Occupants[i] == playerObjectId) return i;
            return -1;
        }

        void RefreshLocalSeat()
        {
            var me = PlayerIdentity.Local;
            if (me == null) return;
            int seat = SeatOf(me.NetworkObjectId);
            if (seat >= 0) { me.Controller.Vehicle = this; me.Controller.VehicleSeat = seat; }
            else if (me.Controller.Vehicle == this) { me.Controller.Vehicle = null; me.Controller.VehicleSeat = -1; }
        }

        // ------------------------------------------------------------------ enter / exit
        [Rpc(SendTo.Server)]
        public void EnterRpc(RpcParams rpc = default)
        {
            var player = PlayerOf(rpc.Receive.SenderClientId);
            if (player == null || !player.Health.IsAlive || player.Controller.Vehicle != null || Health.Value <= 0) return;
            if (Vector3.Distance(player.transform.position, transform.position) > 5f) return;
            for (int i = 0; i < Occupants.Count; i++)
            {
                if (Occupants[i] != 0) continue;
                Occupants[i] = player.NetworkObjectId;
                player.Controller.Vehicle = this;
                player.Controller.VehicleSeat = i;
                if (i == 0) NetworkObject.ChangeOwnership(player.OwnerClientId);
                return;
            }
        }

        [Rpc(SendTo.Server)]
        public void ExitRpc(RpcParams rpc = default)
        {
            var player = PlayerOf(rpc.Receive.SenderClientId);
            if (player != null) ServerEject(player);
        }

        public void ServerEject(PlayerIdentity player)
        {
            int seat = SeatOf(player.NetworkObjectId);
            if (seat < 0) return;
            Occupants[seat] = 0;
            player.Controller.Vehicle = null;
            player.Controller.VehicleSeat = -1;
            player.Controller.ServerTeleport(transform.position + transform.right * (seat % 2 == 0 ? -2.2f : 2.2f) + Vector3.up);
            if (seat == 0) NetworkObject.RemoveOwnership();
        }

        PlayerIdentity PlayerOf(ulong clientId) =>
            NetworkManager.ConnectedClients.TryGetValue(clientId, out var c) && c.PlayerObject ? c.PlayerObject.GetComponent<PlayerIdentity>() : null;

        // ------------------------------------------------------------------ driving (owner = driver)
        void FixedUpdate()
        {
            if (!IsSpawned) return;
            if (IsServer)
            {
                bool driven = Occupants.Count > 0 && Occupants[0] != 0;
                if (driven && _rb.linearVelocity.sqrMagnitude > 1f) Fuel.Value = Mathf.Max(0, Fuel.Value - fuelPerSecond * Time.fixedDeltaTime);
                if (!IsBoat && transform.position.y < GameConstants.WaterLevel - 1.5f) ServerDamage(50f * Time.fixedDeltaTime, null);
            }
            if (!IsOwner || _rb.isKinematic) return;

            var me = PlayerIdentity.Local;
            var input = PlayerInputHandler.Instance;
            bool driving = me != null && me.Controller.Vehicle == this && me.Controller.VehicleSeat == 0 && input != null;
            Vector2 move = driving && Fuel.Value > 0 && Health.Value > 0 ? input.Move : Vector2.zero;

            bool grounded = IsBoat ? transform.position.y < GameConstants.WaterLevel + 0.6f
                                   : Physics.Raycast(transform.position + Vector3.up * 0.5f, Vector3.down, 1.4f, GameConstants.EnvironmentMask);
            if (IsBoat)
            {
                float depth = GameConstants.WaterLevel - transform.position.y + 0.3f;
                if (depth > 0) _rb.AddForce(Vector3.up * depth * 25f, ForceMode.Acceleration);
                _rb.linearDamping = depth > 0 ? 1.2f : 0.1f;
            }
            if (!grounded) return;

            Vector3 fwd = transform.forward;
            float speed = Vector3.Dot(_rb.linearVelocity, fwd);
            float target = move.y * maxSpeed * (move.y < 0 ? 0.4f : 1f);
            float accel = Mathf.Clamp(target - speed, -acceleration, acceleration);
            _rb.AddForce(fwd * accel, ForceMode.Acceleration);
            float steer = move.x * turnSpeed * Mathf.Clamp01(Mathf.Abs(speed) / 4f) * Mathf.Sign(speed == 0 ? 1 : speed);
            _rb.MoveRotation(_rb.rotation * Quaternion.Euler(0, steer * Time.fixedDeltaTime, 0));
            // Kill sideways slide for arcade handling
            Vector3 side = Vector3.Project(_rb.linearVelocity, transform.right);
            _rb.AddForce(-side * (type == VehicleType.Motorbike ? 6f : 3.5f), ForceMode.Acceleration);
        }

        // ------------------------------------------------------------------ damage (server)
        public void ServerDamage(float dmg, PlayerIdentity attacker)
        {
            if (!IsServer || Health.Value <= 0) return;
            Health.Value = Mathf.Max(0, Health.Value - dmg);
            if (Health.Value > 0) return;
            for (int i = 0; i < Occupants.Count; i++)
            {
                if (Occupants[i] == 0 || !NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(Occupants[i], out var o)) continue;
                var p = o.GetComponent<PlayerIdentity>();
                ServerEject(p);
                p.Health.ApplyDamage(120f, attacker, false, "Vehicle explosion", DamageKind.Explosion);
            }
            Invoke(nameof(Despawn), 8f);
        }

        void Despawn() { if (IsSpawned) NetworkObject.Despawn(true); }

        /// <summary>Running people over (server checks the driver's vehicle speed).</summary>
        void OnCollisionEnter(Collision c)
        {
            if (!IsServer) return;
            var victim = c.collider.GetComponentInParent<PlayerIdentity>();
            float speed = c.relativeVelocity.magnitude;
            if (victim == null || speed < 8f || SeatOf(victim.NetworkObjectId) >= 0) return;
            PlayerIdentity driver = null;
            if (Occupants.Count > 0 && Occupants[0] != 0 && NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(Occupants[0], out var d))
                driver = d.GetComponent<PlayerIdentity>();
            victim.Health.ApplyDamage(speed * 4f, driver, false, "Vehicle", DamageKind.Vehicle);
        }
    }
}
