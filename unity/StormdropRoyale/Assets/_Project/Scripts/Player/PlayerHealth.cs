using Unity.Netcode;
using UnityEngine;

namespace Stormdrop
{
    public enum DamageKind : byte { Bullet, Melee, Explosion, Zone, Fall, Vehicle }

    /// <summary>
    /// HP (200), Vigor energy, vest/helmet (levels 1–4 with durability), skill shield,
    /// knock-down and revive. All values are written only by the server.
    /// </summary>
    public class PlayerHealth : NetworkBehaviour
    {
        public static readonly float[] ArmorReduction = { 0f, 0.20f, 0.30f, 0.40f, 0.50f };
        public static readonly float[] ArmorDurability = { 0f, 100f, 150f, 200f, 260f };

        public readonly NetworkVariable<float> Health = new NetworkVariable<float>(GameConstants.MaxHealth);
        public readonly NetworkVariable<float> Energy = new NetworkVariable<float>(0);
        public readonly NetworkVariable<float> KnockHealth = new NetworkVariable<float>(0);
        public readonly NetworkVariable<float> Shield = new NetworkVariable<float>(0);
        public readonly NetworkVariable<PlayerLifeState> Life = new NetworkVariable<PlayerLifeState>(PlayerLifeState.Alive);
        public readonly NetworkVariable<byte> VestLevel = new NetworkVariable<byte>(0);
        public readonly NetworkVariable<float> VestDurability = new NetworkVariable<float>(0);
        public readonly NetworkVariable<byte> HelmetLevel = new NetworkVariable<byte>(0);
        public readonly NetworkVariable<float> HelmetDurability = new NetworkVariable<float>(0);
        public readonly NetworkVariable<float> ReviveProgress = new NetworkVariable<float>(0);
        public readonly NetworkVariable<bool> Invulnerable = new NetworkVariable<bool>(false);

        /// <summary>Local: called with the attacker's position so the HUD can show a damage arrow.</summary>
        public event System.Action<Vector3> DamagedFrom;

        public bool IsAlive => Life.Value == PlayerLifeState.Alive;

        PlayerIdentity _id;
        PlayerIdentity _knockedBy;
        string _knockedWeapon;
        PlayerHealth _reviver;
        Vector3 _reviverStart;
        float _energyTick;

        void Awake() => _id = GetComponent<PlayerIdentity>();

        // ------------------------------------------------------------------ server API
        public void ServerSetLife(PlayerLifeState s) { if (IsServer) Life.Value = s; }
        public void ServerSetInvulnerable(bool v) { if (IsServer) Invulnerable.Value = v; }

        public void ServerResetForSpawn()
        {
            Health.Value = GameConstants.MaxHealth;
            Energy.Value = 0;
            KnockHealth.Value = 0;
            Shield.Value = 0;
            ReviveProgress.Value = 0;
            _reviver = null;
            _knockedBy = null;
            Life.Value = PlayerLifeState.Alive;
        }

        public void ServerSetArmor(int vest, int helmet)
        {
            VestLevel.Value = (byte)vest; VestDurability.Value = ArmorDurability[vest];
            HelmetLevel.Value = (byte)helmet; HelmetDurability.Value = ArmorDurability[helmet];
        }

        /// <summary>Equips a vest/helmet if it is better than the current one.</summary>
        public bool ServerEquipArmor(ItemDefinition item)
        {
            int lvl = Mathf.Clamp(item.level, 1, 4);
            if (item.kind == ItemKind.ArmorVest)
            {
                if (lvl < VestLevel.Value || (lvl == VestLevel.Value && VestDurability.Value >= ArmorDurability[lvl] * 0.9f)) return false;
                VestLevel.Value = (byte)lvl; VestDurability.Value = ArmorDurability[lvl];
                return true;
            }
            if (item.kind == ItemKind.Helmet)
            {
                if (lvl < HelmetLevel.Value || (lvl == HelmetLevel.Value && HelmetDurability.Value >= ArmorDurability[lvl] * 0.9f)) return false;
                HelmetLevel.Value = (byte)lvl; HelmetDurability.Value = ArmorDurability[lvl];
                return true;
            }
            return false;
        }

        public void ServerRepairVest(float amount)
        {
            if (VestLevel.Value == 0) return;
            VestDurability.Value = Mathf.Min(ArmorDurability[VestLevel.Value], VestDurability.Value + amount);
        }

        public void ServerHeal(float hp)
        {
            if (Life.Value != PlayerLifeState.Alive) return;
            Health.Value = Mathf.Min(GameConstants.MaxHealth, Health.Value + hp);
        }

        public void ServerAddEnergy(float e) => Energy.Value = Mathf.Min(GameConstants.MaxEnergy, Energy.Value + e);

        public void ServerAddShield(float amount) => Shield.Value = Mathf.Max(Shield.Value, amount);

        /// <summary>Single entry point for all damage. Returns damage actually dealt.</summary>
        public float ApplyDamage(float raw, PlayerIdentity attacker, bool headshot, string source, DamageKind kind)
        {
            if (!IsServer || raw <= 0 || Invulnerable.Value) return 0;
            var life = Life.Value;
            if (life == PlayerLifeState.Dead || life == PlayerLifeState.InPlane || life == PlayerLifeState.Spectating) return 0;
            bool friendly = attacker != null && attacker != _id && attacker.Team.Value == _id.Team.Value;
            if (friendly && kind != DamageKind.Explosion) return 0;
            if (friendly) raw *= 0.25f;

            float dmg = raw;
            if (kind == DamageKind.Bullet || kind == DamageKind.Melee) dmg *= _id.Skills != null ? _id.Skills.DamageTakenMultiplier : 1f;

            if (kind != DamageKind.Zone && kind != DamageKind.Fall && Shield.Value > 0)
            {
                float absorbed = Mathf.Min(Shield.Value, dmg);
                Shield.Value -= absorbed;
                dmg -= absorbed;
                if (dmg <= 0) return absorbed;
            }

            if (kind == DamageKind.Bullet || kind == DamageKind.Melee || kind == DamageKind.Explosion)
            {
                if (headshot && HelmetLevel.Value > 0)
                {
                    dmg *= 1f - ArmorReduction[HelmetLevel.Value];
                    HelmetDurability.Value -= dmg * 0.6f;
                    if (HelmetDurability.Value <= 0) { HelmetLevel.Value = 0; HelmetDurability.Value = 0; }
                }
                else if (!headshot && VestLevel.Value > 0)
                {
                    dmg *= 1f - ArmorReduction[VestLevel.Value];
                    VestDurability.Value -= dmg * 0.6f;
                    if (VestDurability.Value <= 0) { VestLevel.Value = 0; VestDurability.Value = 0; }
                }
            }

            MatchManager.Instance?.RecordDamage(attacker, _id, dmg);
            if (attacker != null) DamagedRpc(attacker.transform.position);
            _id.Skills?.ServerOnDamaged(attacker);

            if (life == PlayerLifeState.Knocked)
            {
                KnockHealth.Value -= dmg;
                if (KnockHealth.Value <= 0) ServerKill(attacker ?? _knockedBy, source, headshot);
                return dmg;
            }

            Health.Value -= dmg;
            if (Health.Value > 0) return dmg;
            Health.Value = 0;

            var mm = MatchManager.Instance;
            bool canKnock = mm != null && GameConstants.UsesKnockdown(mm.Mode.Value) && life == PlayerLifeState.Alive
                            && mm.TeamHasStanding(_id.Team.Value, _id) && kind != DamageKind.Zone;
            if (canKnock) Knock(attacker, source);
            else ServerKill(attacker, source, headshot);
            return dmg;
        }

        void Knock(PlayerIdentity attacker, string source)
        {
            Life.Value = PlayerLifeState.Knocked;
            KnockHealth.Value = GameConstants.KnockHealth;
            _knockedBy = attacker;
            _knockedWeapon = source;
            _id.Inventory?.ServerCancelUse();
            MatchManager.Instance?.OnPlayerKnocked(_id, attacker, source);
        }

        public void ServerKill(PlayerIdentity killer, string source, bool headshot)
        {
            if (Life.Value == PlayerLifeState.Dead) return;
            Life.Value = PlayerLifeState.Dead;
            Health.Value = 0;
            KnockHealth.Value = 0;
            ReviveProgress.Value = 0;
            _reviver = null;
            var credit = killer ?? _knockedBy;
            _id.Inventory?.ServerCancelUse();
            MatchManager.Instance?.OnPlayerKilled(_id, credit, credit == _knockedBy && killer == null ? _knockedWeapon : source, headshot);
        }

        // ------------------------------------------------------------------ revive
        /// <summary>Owner of the reviver asks to start reviving a knocked teammate.</summary>
        [Rpc(SendTo.Server)]
        public void StartReviveRpc(ulong targetObjectId, RpcParams rpc = default)
        {
            if (rpc.Receive.SenderClientId != OwnerClientId || Life.Value != PlayerLifeState.Alive) return;
            if (!NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(targetObjectId, out var obj)) return;
            var target = obj.GetComponent<PlayerHealth>();
            if (target == null || target == this || target.Life.Value != PlayerLifeState.Knocked) return;
            if (target._id.Team.Value != _id.Team.Value) return;
            if (Vector3.Distance(target.transform.position, transform.position) > 3f) return;
            target._reviver = this;
            target._reviverStart = transform.position;
        }

        void Update()
        {
            if (!IsServer || !IsSpawned) return;
            float dt = Time.deltaTime;

            if (Life.Value == PlayerLifeState.Knocked)
            {
                if (_reviver != null)
                {
                    bool valid = _reviver.Life.Value == PlayerLifeState.Alive
                                 && Vector3.Distance(_reviver.transform.position, _reviverStart) < 1.5f
                                 && Vector3.Distance(_reviver.transform.position, transform.position) < 3.5f;
                    if (!valid) { _reviver = null; ReviveProgress.Value = 0; }
                    else
                    {
                        ReviveProgress.Value += dt / GameConstants.ReviveSeconds;
                        if (ReviveProgress.Value >= 1f)
                        {
                            Life.Value = PlayerLifeState.Alive;
                            Health.Value = 60f;
                            ReviveProgress.Value = 0;
                            MatchManager.Instance?.RecordRevive(_reviver._id);
                            _reviver = null;
                            _knockedBy = null;
                        }
                    }
                }
                else
                {
                    KnockHealth.Value -= 4f * dt;   // bleed out in ~25 s
                    if (KnockHealth.Value <= 0) ServerKill(_knockedBy, _knockedWeapon, false);
                }
            }

            // Vigor energy converts into HP (like a slow regen) while alive
            if (Life.Value == PlayerLifeState.Alive && Energy.Value > 0 && Health.Value < GameConstants.MaxHealth)
            {
                _energyTick += dt;
                if (_energyTick >= 0.5f)
                {
                    float amount = Mathf.Min(Energy.Value, 2.5f);
                    Energy.Value -= amount;
                    Health.Value = Mathf.Min(GameConstants.MaxHealth, Health.Value + amount);
                    _energyTick = 0;
                }
            }
        }

        [Rpc(SendTo.Owner)]
        void DamagedRpc(Vector3 from) => DamagedFrom?.Invoke(from);
    }
}
