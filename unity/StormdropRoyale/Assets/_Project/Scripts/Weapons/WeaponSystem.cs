using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Stormdrop
{
    /// <summary>A hit the shooter's client believes it made on a player.</summary>
    public struct HitClaim : INetworkSerializable
    {
        public ulong targetId;
        public Vector3 point;
        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref targetId);
            s.SerializeValue(ref point);
        }
    }

    /// <summary>Weapon stats after attachments.</summary>
    public struct EffectiveWeapon
    {
        public WeaponData data;
        public float spreadHip, spreadAds, recoilV, recoilH, range, zoom, reload;
        public int magSize;
        public bool suppressed;
    }

    /// <summary>
    /// Shooting. The owner aims, applies spread/recoil and raycasts locally for instant feedback,
    /// then sends the shot to the server. The server is the only one that deals damage and it
    /// re-checks everything: ammo, fire rate, shooter position, range, angle, line of sight and
    /// the target's lag-compensated position. Repeated failures flag the player for review.
    /// </summary>
    public class WeaponSystem : NetworkBehaviour
    {
        public readonly NetworkVariable<bool> Aiming =
            new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        public readonly NetworkVariable<bool> Reloading = new NetworkVariable<bool>(false);

        public event System.Action<bool, float> HitConfirmed;   // headshot, damage (local shooter)
        public event System.Action Fired;

        public WeaponData CurrentWeapon => _inv != null ? _inv.WeaponIn(_inv.ActiveSlot.Value) : null;
        public int PredictedMag => _predictedMag;

        PlayerIdentity _id;
        PlayerInventory _inv;
        LineRenderer _tracer;

        // owner state
        float _nextFireTime;
        int _predictedMag = -1;
        float _bloom;
        bool _firePrev;
        readonly RaycastHit[] _hits = new RaycastHit[16];

        // server state
        double _lastServerShot;
        readonly Queue<double> _recentShots = new Queue<double>();
        int _violations;
        Coroutine _reload;

        void Awake()
        {
            _id = GetComponent<PlayerIdentity>();
            _inv = GetComponent<PlayerInventory>();
        }

        public override void OnNetworkSpawn()
        {
            _inv.Changed += () => _predictedMag = -1;
        }

        public static EffectiveWeapon Effective(WeaponSlotState st)
        {
            var w = GameDatabase.Instance.Item(st.weaponId.ToString()) as WeaponData;
            var e = new EffectiveWeapon { data = w };
            if (w == null) return e;
            e.spreadHip = w.hipSpread; e.spreadAds = w.adsSpread; e.recoilV = w.recoilVertical; e.recoilH = w.recoilHorizontal;
            e.range = w.range; e.zoom = w.adsZoom; e.reload = w.reloadSeconds; e.magSize = w.magazineSize;
            foreach (AttachmentSlot slot in System.Enum.GetValues(typeof(AttachmentSlot)))
            {
                if (!(GameDatabase.Instance.Item(st.Get(slot).ToString()) is AttachmentData a)) continue;
                e.spreadHip *= a.spreadMultiplier; e.spreadAds *= a.spreadMultiplier;
                e.recoilV *= a.recoilMultiplier; e.recoilH *= a.recoilMultiplier;
                e.range *= a.rangeMultiplier; e.reload *= a.reloadMultiplier;
                e.magSize = Mathf.RoundToInt(e.magSize * (1f + a.magazineBonus));
                if (a.slot == AttachmentSlot.Scope) e.zoom = a.scopeZoom;
                e.suppressed |= a.suppressor;
            }
            return e;
        }

        // ------------------------------------------------------------------ owner
        void Update()
        {
            if (!IsOwner || !IsSpawned) return;
            var input = PlayerInputHandler.Instance;
            var cam = PlayerCamera.Instance;
            if (input == null || cam == null) return;

            bool canShoot = _id.Health.IsAlive && _id.Controller.Vehicle == null && _inv.UsingItem.Value.Length == 0
                            && _id.Health.Life.Value == PlayerLifeState.Alive;
            var st = _inv.ActiveWeaponState;
            var e = Effective(st);

            if (input.ConsumeSwitchWeapon()) CycleWeapon();
            if (input.ConsumeAimToggle() && canShoot && e.data != null && e.data.category != WeaponCategory.Melee) Aiming.Value = !Aiming.Value;
            if (!canShoot && Aiming.Value) Aiming.Value = false;
            cam.Zoom = Aiming.Value && e.data != null ? Mathf.Max(1.1f, e.zoom) : 1f;

            if (_predictedMag < 0) _predictedMag = st.mag;
            if (input.ConsumeReload() && e.data != null && e.magSize > 0 && _predictedMag < e.magSize) ReloadRpc();

            _bloom = Mathf.MoveTowards(_bloom, 0, Time.deltaTime * 4f);
            bool want = input.FireHeld || (GameSettings.Data.autoFire && CrosshairOnEnemy(cam, e.range));
            bool pressed = want && !_firePrev;
            _firePrev = want;
            if (!canShoot || e.data == null || Reloading.Value || !want) return;
            if (!e.data.automatic && !pressed) return;
            if (Time.time < _nextFireTime) return;

            bool melee = e.data.category == WeaponCategory.Melee;
            if (!melee && _predictedMag <= 0)
            {
                if (pressed && _inv.AmmoReserve(e.data.ammo) > 0) ReloadRpc();
                return;
            }
            FireOnce(cam, e, melee);
        }

        void CycleWeapon()
        {
            for (int k = 1; k <= 4; k++)
            {
                byte s = (byte)((_inv.ActiveSlot.Value + k) % 4);
                if (!_inv.Slot(s).IsEmpty) { _inv.SwitchSlotRpc(s); Aiming.Value = false; return; }
            }
        }

        bool CrosshairOnEnemy(PlayerCamera cam, float range)
        {
            var ray = cam.AimRay();
            if (!Physics.SphereCast(ray, 0.25f, out var hit, Mathf.Max(range, 10f), GameConstants.ShootableMask, QueryTriggerInteraction.Ignore)) return false;
            var p = hit.collider.GetComponentInParent<PlayerIdentity>();
            return p != null && p != _id && p.Team.Value != _id.Team.Value && p.Health.Life.Value != PlayerLifeState.Dead;
        }

        void FireOnce(PlayerCamera cam, EffectiveWeapon e, bool melee)
        {
            var w = e.data;
            _nextFireTime = Time.time + w.SecondsPerShot;
            if (!melee) _predictedMag--;

            float spread = (Aiming.Value ? e.spreadAds : e.spreadHip) + _bloom;
            if (_id.Controller.IsMoving) spread *= 1.5f;
            if (_id.Controller.StanceState.Value == Stance.Crouching) spread *= 0.8f;
            if (_id.Controller.StanceState.Value == Stance.Prone) spread *= 0.6f;

            var ray = cam.AimRay();
            float range = melee ? 2.2f : e.range;
            int pellets = Mathf.Max(1, w.pellets);
            var dirs = new Vector3[pellets];
            var claims = new List<HitClaim>();
            Vector3 lastEnd = ray.origin + ray.direction * range;

            for (int i = 0; i < pellets; i++)
            {
                Vector2 r = Random.insideUnitCircle * spread;
                Vector3 dir = Quaternion.AngleAxis(r.x, cam.transform.up) * Quaternion.AngleAxis(r.y, cam.transform.right) * ray.direction;
                dirs[i] = dir;
                if (NearestHit(ray.origin, dir, range, out var hit))
                {
                    lastEnd = hit.point;
                    var target = hit.collider.GetComponentInParent<PlayerIdentity>();
                    if (target != null) claims.Add(new HitClaim { targetId = target.NetworkObjectId, point = hit.point });
                }
            }

            FireRpc(ray.origin, dirs, claims.ToArray());
            ShowTracer(transform.position + Vector3.up * 1.4f + transform.forward * 0.5f, lastEnd, melee);
            if (!melee) cam.AddRecoil(e.recoilV * (Aiming.Value ? 0.6f : 1f), Random.Range(-e.recoilH, e.recoilH));
            _bloom = Mathf.Min(_bloom + 0.35f, 4f);
            Fired?.Invoke();
        }

        bool NearestHit(Vector3 origin, Vector3 dir, float range, out RaycastHit best)
        {
            best = default;
            int n = Physics.RaycastNonAlloc(origin, dir, _hits, range, GameConstants.ShootableMask, QueryTriggerInteraction.Ignore);
            float bestDist = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                if (_hits[i].collider.transform.IsChildOf(transform)) continue;   // ignore self
                if (_hits[i].distance < bestDist) { bestDist = _hits[i].distance; best = _hits[i]; }
            }
            return bestDist < float.MaxValue;
        }

        void ShowTracer(Vector3 from, Vector3 to, bool melee)
        {
            if (melee) return;
            if (_tracer == null)
            {
                var go = new GameObject("Tracer");
                _tracer = go.AddComponent<LineRenderer>();
                _tracer.widthMultiplier = 0.03f;
                _tracer.material = new Material(Shader.Find("Sprites/Default"));
                _tracer.startColor = new Color(1f, 0.9f, 0.5f, 0.9f);
                _tracer.endColor = new Color(1f, 0.9f, 0.5f, 0f);
            }
            _tracer.SetPosition(0, from);
            _tracer.SetPosition(1, to);
            _tracer.enabled = true;
            CancelInvoke(nameof(HideTracer));
            Invoke(nameof(HideTracer), 0.04f);
        }

        void HideTracer() { if (_tracer) _tracer.enabled = false; }

        // ------------------------------------------------------------------ server
        [Rpc(SendTo.Server)]
        void FireRpc(Vector3 origin, Vector3[] dirs, HitClaim[] claims, RpcParams rpc = default)
        {
            if (rpc.Receive.SenderClientId != OwnerClientId) return;
            if (_id.Health.Life.Value != PlayerLifeState.Alive || _id.Controller.Vehicle != null) return;
            int slot = _inv.ActiveSlot.Value;
            var st = _inv.Slot(slot);
            var e = Effective(st);
            var w = e.data;
            if (w == null || Reloading.Value) return;
            bool melee = w.category == WeaponCategory.Melee;
            double now = NetworkManager.ServerTime.Time;

            // 1. ammo
            if (!melee && st.mag <= 0) { Violation("fired with empty magazine"); return; }
            // 2. fire rate (allow network jitter on single shots, enforce the average over 1 s)
            if (now - _lastServerShot < w.SecondsPerShot * 0.5) { Violation("fire rate"); return; }
            while (_recentShots.Count > 0 && now - _recentShots.Peek() > 1.0) _recentShots.Dequeue();
            if (_recentShots.Count > w.roundsPerMinute / 60f * 1.3f + 2) { Violation("sustained fire rate"); return; }
            _recentShots.Enqueue(now);
            _lastServerShot = now;
            // 3. shape of the request
            int pellets = Mathf.Max(1, w.pellets);
            if (dirs == null || dirs.Length != pellets || claims == null || claims.Length > pellets) { Violation("bad shot packet"); return; }
            // 4. shooter position (camera sits up to ~3.5 m behind/above the player)
            if (Vector3.Distance(origin, transform.position + Vector3.up * 1.6f) > 4.5f) { Violation("shot origin"); return; }

            if (!melee) { st.mag--; _inv.ServerSetSlot(slot, st); }

            float rangeLimit = (melee ? 2.5f : e.range) * 1.1f;
            Vector3 fxEnd = origin + dirs[0].normalized * Mathf.Min(rangeLimit, 300f);

            // Non-player targets (vehicles, foam walls) are raycast by the server itself.
            foreach (var d in dirs)
            {
                if (Physics.Raycast(origin, d, out var hit, rangeLimit, GameConstants.ShootableMask, QueryTriggerInteraction.Ignore))
                {
                    if (hit.collider.GetComponentInParent<PlayerIdentity>() != null) continue;
                    fxEnd = hit.point;
                    float dmg = w.damage * Falloff(w, hit.distance);
                    hit.collider.GetComponentInParent<FoamWall>()?.ServerDamage(dmg);
                    hit.collider.GetComponentInParent<VehicleController>()?.ServerDamage(dmg * 0.6f, _id);
                }
            }

            // Player hits: validate each claim against lag-compensated history.
            float rtt = NetworkManager.NetworkConfig.NetworkTransport.GetCurrentRtt(OwnerClientId) / 1000f;
            double rewindTime = now - Mathf.Clamp(rtt * 0.5f + 0.1f, 0f, (float)LagCompensation.MaxRewind);
            var damagedThisShot = new HashSet<ulong>();
            foreach (var c in claims)
            {
                if (!NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(c.targetId, out var obj)) continue;
                var target = obj.GetComponent<PlayerIdentity>();
                if (target == null || target == _id) continue;
                var life = target.Health.Life.Value;
                if (life != PlayerLifeState.Alive && life != PlayerLifeState.Knocked && life != PlayerLifeState.Parachuting) continue;

                float dist = Vector3.Distance(origin, c.point);
                if (dist > rangeLimit) { Violation("hit beyond range"); continue; }
                Vector3 toPoint = (c.point - origin).normalized;
                bool angleOk = false;
                foreach (var d in dirs) if (Vector3.Angle(d, toPoint) < 6f) { angleOk = true; break; }
                if (!angleOk) { Violation("hit off aim"); continue; }
                if (Physics.Linecast(origin, c.point - toPoint * 0.35f, out var block, GameConstants.EnvironmentMask, QueryTriggerInteraction.Ignore)
                    && block.collider.GetComponentInParent<PlayerIdentity>() == null) { Violation("hit through wall"); continue; }

                Vector3 basePos = LagCompensation.TryGet(target.NetworkObjectId, rewindTime, out var past) ? past : target.transform.position;
                float h = target.Controller.Height;
                bool near = HorizontalDistance(basePos, c.point) < 1.3f || HorizontalDistance(target.transform.position, c.point) < 1.3f;
                float rel = (c.point.y - basePos.y) / h;
                if (!near || rel < -0.35f || rel > 1.35f) { Violation("hit position mismatch"); continue; }

                bool headshot = rel >= 0.8f && target.Controller.StanceState.Value != Stance.Prone;
                float mult = headshot ? w.headshotMultiplier : rel < 0.35f ? w.limbMultiplier : 1f;
                float dmgDealt = target.Health.ApplyDamage(w.damage * mult * Falloff(w, dist), _id, headshot, w.displayName,
                                                           melee ? DamageKind.Melee : DamageKind.Bullet);
                if (dmgDealt > 0)
                {
                    HitConfirmRpc(headshot, dmgDealt);
                    if (damagedThisShot.Add(target.NetworkObjectId)) _id.Skills?.ServerOnHitEnemy(target);
                }
                fxEnd = c.point;
            }

            if (!melee)
            {
                ShotFxRpc(origin, fxEnd);
                if (!e.suppressed) PlayerSkills.ServerNotifyGunfire(_id, transform.position);
            }
        }

        static float HorizontalDistance(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;

        static float Falloff(WeaponData w, float dist)
        {
            if (dist <= w.falloffStart) return 1f;
            float t = Mathf.InverseLerp(w.falloffStart, w.range, dist);
            return Mathf.Lerp(1f, w.minFalloffDamage, t);
        }

        void Violation(string what)
        {
            _violations++;
            if (_violations >= 12) MatchManager.Instance?.FlagSuspicious(_id, "shot validation: " + what);
        }

        [Rpc(SendTo.Server)]
        void ReloadRpc(RpcParams rpc = default)
        {
            if (rpc.Receive.SenderClientId != OwnerClientId || Reloading.Value || _id.Health.Life.Value != PlayerLifeState.Alive) return;
            int slot = _inv.ActiveSlot.Value;
            var e = Effective(_inv.Slot(slot));
            if (e.data == null || e.magSize <= 0 || _inv.Slot(slot).mag >= e.magSize || _inv.AmmoReserve(e.data.ammo) <= 0) return;
            _reload = StartCoroutine(ReloadRoutine(slot, e));
        }

        IEnumerator ReloadRoutine(int slot, EffectiveWeapon e)
        {
            Reloading.Value = true;
            float mult = _id.Skills != null ? _id.Skills.ReloadTimeMultiplier : 1f;
            yield return new WaitForSeconds(e.reload * mult);
            Reloading.Value = false;
            _reload = null;
            if (_inv.ActiveSlot.Value != slot) yield break;
            var st = _inv.Slot(slot);
            var ammo = PlayerInventory.AmmoItem(e.data.ammo);
            if (ammo == null) yield break;
            int need = e.magSize - st.mag;
            int take = Mathf.Min(need, _inv.Count(ammo.id));
            if (take <= 0 || !_inv.ServerRemove(ammo.id, take)) yield break;
            st.mag += take;
            _inv.ServerSetSlot(slot, st);
        }

        public void ServerCancelReload()
        {
            if (_reload != null) StopCoroutine(_reload);
            _reload = null;
            Reloading.Value = false;
        }

        [Rpc(SendTo.Owner)]
        void HitConfirmRpc(bool headshot, float damage) => HitConfirmed?.Invoke(headshot, damage);

        [Rpc(SendTo.NotOwner)]
        void ShotFxRpc(Vector3 from, Vector3 to)
        {
            if (IsServer && !IsHost) return;
            ShowTracer(transform.position + Vector3.up * 1.4f, to, false);
        }
    }
}
