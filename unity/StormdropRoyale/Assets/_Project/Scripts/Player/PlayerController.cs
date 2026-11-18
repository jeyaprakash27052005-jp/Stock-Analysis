using Unity.Netcode;
using UnityEngine;

namespace Stormdrop
{
    /// <summary>
    /// Movement for the local player: walk/sprint/crouch/prone/jump, plane → skydive → parachute,
    /// knocked crawling and riding vehicles. Owner-authoritative for responsiveness; the server
    /// checks speed every tick and pulls back players that move impossibly fast.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : NetworkBehaviour
    {
        [Header("Ground")]
        public float walkSpeed = 4.4f;
        public float sprintSpeed = 6.6f;
        public float crouchSpeed = 2.4f;
        public float proneSpeed = 1.1f;
        public float knockedSpeed = 0.9f;
        public float jumpHeight = 1.1f;
        public float gravity = -22f;

        [Header("Sky")]
        public float freefallSpeed = 48f;
        public float freefallForward = 24f;
        public float chuteFallSpeed = 6f;
        public float chuteForward = 15f;
        public float autoOpenHeight = 120f;

        public readonly NetworkVariable<Stance> StanceState =
            new NetworkVariable<Stance>(Stance.Standing, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        public readonly NetworkVariable<bool> Sprinting =
            new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        public readonly NetworkVariable<bool> ChuteOpen =
            new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        public VehicleController Vehicle { get; set; }
        public int VehicleSeat { get; set; } = -1;
        public float Height => StanceState.Value switch { Stance.Crouching => 1.2f, Stance.Prone => 0.55f, _ => 1.8f };
        public Vector3 Velocity { get; private set; }
        public bool IsMoving => Velocity.sqrMagnitude > 0.5f;

        CharacterController _cc;
        PlayerIdentity _id;
        Transform _body;
        float _vy;
        float _airborneFromY;
        bool _wasGrounded = true;

        // server-side validation
        Vector3 _lastValidPos;
        double _graceUntil;
        int _speedViolations;
        float _checkTimer;

        void Awake()
        {
            _cc = GetComponent<CharacterController>();
            _id = GetComponent<PlayerIdentity>();
            _body = transform.Find("Body");
        }

        public override void OnNetworkSpawn()
        {
            StanceState.OnValueChanged += (_, s) => ApplyStanceShape(s);
            ApplyStanceShape(StanceState.Value);
            if (IsOwner)
            {
                PlayerCamera.Create(transform);
                if (PlayerInputHandler.Instance == null) new GameObject("Input").AddComponent<PlayerInputHandler>();
            }
            _lastValidPos = transform.position;
            _graceUntil = NetworkManager.ServerTime.Time + 2;
        }

        void ApplyStanceShape(Stance s)
        {
            float h = s switch { Stance.Crouching => 1.2f, Stance.Prone => 0.55f, _ => 1.8f };
            _cc.height = h;
            _cc.center = new Vector3(0, h / 2f, 0);
            if (_body != null) { _body.localScale = new Vector3(0.8f, h / 2f, 0.8f); _body.localPosition = new Vector3(0, h / 2f, 0); }
        }

        // ------------------------------------------------------------------ owner update
        void Update()
        {
            if (!IsSpawned) return;
            if (IsServer) ServerTick();
            if (!IsOwner) return;

            var input = PlayerInputHandler.Instance;
            var cam = PlayerCamera.Instance;
            if (input == null || cam == null) return;
            cam.AddLook(input.Look);

            switch (_id.Health.Life.Value)
            {
                case PlayerLifeState.InPlane: UpdateInPlane(input); break;
                case PlayerLifeState.Parachuting: UpdateSky(input, cam); break;
                case PlayerLifeState.Alive:
                    if (Vehicle != null) UpdateInVehicle(input);
                    else UpdateGround(input, cam, false);
                    break;
                case PlayerLifeState.Knocked: UpdateGround(input, cam, true); break;
                default: Velocity = Vector3.zero; SetVisible(false); break;
            }
        }

        void UpdateInPlane(PlayerInputHandler input)
        {
            SetVisible(false);
            var plane = PlaneController.Instance;
            if (plane == null) return;
            _cc.enabled = false;
            transform.position = plane.CurrentPosition + Vector3.down * 4f;
            _cc.enabled = true;
            if (input.ConsumeJump() | input.ConsumeParachute()) JumpFromPlaneRpc();
        }

        void UpdateSky(PlayerInputHandler input, PlayerCamera cam)
        {
            SetVisible(true);
            float groundDist = Physics.Raycast(transform.position + Vector3.up, Vector3.down, out var hit, 3000f, GameConstants.EnvironmentMask)
                ? hit.distance - 1f : 999f;
            if (!ChuteOpen.Value && (groundDist < autoOpenHeight || input.ConsumeParachute())) ChuteOpen.Value = true;

            Vector3 fwd = Quaternion.Euler(0, cam.Yaw, 0) * new Vector3(input.Move.x, 0, input.Move.y);
            float down = ChuteOpen.Value ? chuteFallSpeed : freefallSpeed * Mathf.Lerp(0.7f, 1f, input.Move.y);   // push forward to dive
            float fwdSpeed = ChuteOpen.Value ? chuteForward : freefallForward;
            Velocity = fwd * fwdSpeed + Vector3.down * down;
            transform.rotation = Quaternion.Euler(0, cam.Yaw, 0);
            _cc.Move(Velocity * Time.deltaTime);
            if (_cc.isGrounded || groundDist < 0.4f)
            {
                ChuteOpen.Value = false;
                _vy = 0;
                _wasGrounded = true;
                LandedRpc();
            }
        }

        void UpdateGround(PlayerInputHandler input, PlayerCamera cam, bool knocked)
        {
            SetVisible(true);
            if (knocked) { if (StanceState.Value != Stance.Prone) StanceState.Value = Stance.Prone; }
            else
            {
                if (input.ConsumeCrouch()) StanceState.Value = StanceState.Value == Stance.Crouching ? Stance.Standing : Stance.Crouching;
                if (input.ConsumeProne()) StanceState.Value = StanceState.Value == Stance.Prone ? Stance.Standing : Stance.Prone;
            }

            bool sprint = !knocked && input.Sprint && input.Move.y > 0.3f && StanceState.Value == Stance.Standing
                          && (_id.Weapons == null || !_id.Weapons.Aiming.Value);
            if (sprint != Sprinting.Value) Sprinting.Value = sprint;

            float speed = knocked ? knockedSpeed : StanceState.Value switch
            {
                Stance.Crouching => crouchSpeed,
                Stance.Prone => proneSpeed,
                _ => sprint ? sprintSpeed : walkSpeed,
            };
            speed *= SpeedMultiplier();

            Quaternion yaw = Quaternion.Euler(0, cam.Yaw, 0);
            Vector3 move = yaw * new Vector3(input.Move.x, 0, input.Move.y) * speed;
            transform.rotation = yaw;

            if (_cc.isGrounded)
            {
                if (!_wasGrounded)
                {
                    float drop = _airborneFromY - transform.position.y;
                    if (drop > 6f) FallDamageRpc(drop);
                }
                _vy = -2f;
                if (!knocked && input.ConsumeJump())
                {
                    if (StanceState.Value != Stance.Standing) StanceState.Value = Stance.Standing;
                    else _vy = Mathf.Sqrt(jumpHeight * -2f * gravity);
                }
            }
            else if (_wasGrounded) _airborneFromY = transform.position.y;
            _wasGrounded = _cc.isGrounded;

            _vy += gravity * Time.deltaTime;
            if (transform.position.y < GameConstants.WaterLevel - 0.8f) _vy = Mathf.Max(_vy, 0.5f);   // swim: stay at surface
            Velocity = move + Vector3.up * _vy;
            _cc.Move(Velocity * Time.deltaTime);
        }

        void UpdateInVehicle(PlayerInputHandler input)
        {
            var seat = Vehicle.SeatTransform(VehicleSeat);
            _cc.enabled = false;
            transform.SetPositionAndRotation(seat.position, seat.rotation);
            SetVisible(VehicleSeat != 0);
            if (input.ConsumeInteract()) Vehicle.ExitRpc();
        }

        float SpeedMultiplier()
        {
            float m = 1f;
            if (_id.Skills != null) m *= _id.Skills.MoveSpeedMultiplier;
            var w = _id.Weapons != null ? _id.Weapons.CurrentWeapon : null;
            if (w != null) m *= w.moveSpeedMultiplier;
            return m;
        }

        bool _visible = true;
        void SetVisible(bool v)
        {
            if (_visible == v) return;
            _visible = v;
            foreach (var r in GetComponentsInChildren<Renderer>()) r.enabled = v;
        }

        void LateUpdate()
        {
            // Non-owners: hide players that are still in the plane
            if (!IsOwner && IsSpawned) SetVisible(_id.Health.Life.Value != PlayerLifeState.InPlane && _id.Health.Life.Value != PlayerLifeState.Dead);
            if (IsOwner && Vehicle == null && !_cc.enabled) _cc.enabled = true;
        }

        // ------------------------------------------------------------------ server
        /// <summary>Server: move this player (spawns, rounds, anti-cheat corrections).</summary>
        public void ServerTeleport(Vector3 pos)
        {
            _graceUntil = NetworkManager.ServerTime.Time + 1.5;
            _lastValidPos = pos;
            TeleportRpc(pos);
        }

        [Rpc(SendTo.Owner)]
        void TeleportRpc(Vector3 pos)
        {
            _cc.enabled = false;
            transform.position = pos;
            _cc.enabled = true;
            _vy = 0;
            _wasGrounded = true;
            if (StanceState.Value != Stance.Standing) StanceState.Value = Stance.Standing;
        }

        [Rpc(SendTo.Server)]
        void JumpFromPlaneRpc(RpcParams rpc = default)
        {
            if (rpc.Receive.SenderClientId != OwnerClientId || _id.Health.Life.Value != PlayerLifeState.InPlane) return;
            if (PlaneController.Instance == null || !PlaneController.Instance.CanJump) return;
            ServerJumpFromPlane();
        }

        public void ServerJumpFromPlane()
        {
            _graceUntil = NetworkManager.ServerTime.Time + 600;   // free-fall speeds are validated separately below
            _id.Health.ServerSetLife(PlayerLifeState.Parachuting);
        }

        [Rpc(SendTo.Server)]
        void LandedRpc(RpcParams rpc = default)
        {
            if (rpc.Receive.SenderClientId != OwnerClientId || _id.Health.Life.Value != PlayerLifeState.Parachuting) return;
            _id.Health.ServerSetLife(PlayerLifeState.Alive);
            _graceUntil = NetworkManager.ServerTime.Time + 1;
            _lastValidPos = transform.position;
        }

        [Rpc(SendTo.Server)]
        void FallDamageRpc(float drop, RpcParams rpc = default)
        {
            if (rpc.Receive.SenderClientId != OwnerClientId) return;
            float dmg = Mathf.Clamp((drop - 6f) * 9f, 0, 400);
            if (_id.Pet != null && _id.Pet.buff == PetBuff.SoftLanding) dmg *= 1f - _id.Pet.power;
            _id.Health.ApplyDamage(dmg, null, false, "Fall", DamageKind.Fall);
        }

        public void ServerSetGrace(float seconds)
        {
            _graceUntil = NetworkManager.ServerTime.Time + seconds;
            _lastValidPos = transform.position;
        }

        void ServerTick()
        {
            double now = NetworkManager.ServerTime.Time;
            LagCompensation.Record(NetworkObjectId, now, transform.position);

            _checkTimer += Time.deltaTime;
            if (_checkTimer < 0.5f) return;
            float dt = _checkTimer;
            _checkTimer = 0;

            var life = _id.Health.Life.Value;
            Vector3 delta = transform.position - _lastValidPos;
            if (now < _graceUntil && life != PlayerLifeState.Parachuting || Vehicle != null || life == PlayerLifeState.InPlane)
            {
                _lastValidPos = transform.position;
                return;
            }

            float horizontal = new Vector2(delta.x, delta.z).magnitude / dt;
            float allowed = life == PlayerLifeState.Parachuting ? freefallForward * 1.5f + 6f
                          : sprintSpeed * 1.9f * (_id.Skills != null ? _id.Skills.MoveSpeedMultiplier : 1f) + 2f;
            bool upTooFast = life != PlayerLifeState.Parachuting && delta.y / dt > 12f;

            if (horizontal > allowed || upTooFast)
            {
                _speedViolations++;
                ServerTeleport(_lastValidPos);
                if (_speedViolations >= 6) MatchManager.Instance?.FlagSuspicious(_id, $"speed {horizontal:0.0} m/s (allowed {allowed:0.0})");
                return;
            }
            _lastValidPos = transform.position;
        }
    }
}
