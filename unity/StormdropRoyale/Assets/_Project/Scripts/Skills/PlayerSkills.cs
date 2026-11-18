using System.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Stormdrop
{
    /// <summary>
    /// Character skill (one per character, active or passive) and pet buff.
    /// Passive skills are multipliers read by other systems; active skills run on the server.
    /// </summary>
    public class PlayerSkills : NetworkBehaviour
    {
        public readonly NetworkVariable<double> CooldownEndsAt = new NetworkVariable<double>(0);
        public readonly NetworkVariable<double> ActiveUntil = new NetworkVariable<double>(0);

        /// <summary>Local: enemy positions revealed by Pulse Scan / Eagle Eye / pet pings (for minimap + HUD).</summary>
        public static event System.Action<Vector3, float> EnemyRevealed;   // position, seconds

        PlayerIdentity _id;
        float _petTick;

        CharacterData Ch => _id.Character;
        SkillId Skill => Ch != null ? Ch.skill : SkillId.None;
        bool Active => IsSpawned && NetworkManager.ServerTime.Time < ActiveUntil.Value;
        public bool HasActiveSkill => Ch != null && Ch.IsActiveSkill;
        public float CooldownRemaining => IsSpawned ? Mathf.Max(0, (float)(CooldownEndsAt.Value - NetworkManager.ServerTime.Time)) : 0;

        void Awake() => _id = GetComponent<PlayerIdentity>();

        // ------------------------------------------------------------------ passive multipliers
        public float MoveSpeedMultiplier => Skill == SkillId.SprintSurge && Active ? 1f + Ch.power : 1f;
        public float DamageTakenMultiplier => Skill == SkillId.IronSkin ? 1f - Ch.power : 1f;
        public float ReloadTimeMultiplier => Skill == SkillId.QuickHands ? 1f - Ch.power : 1f;
        public float HealTimeMultiplier => Skill == SkillId.FieldMedic ? 1f - Ch.power : 1f;
        public float HealAmountMultiplier => Skill == SkillId.FieldMedic ? 1.15f : 1f;
        public float EnergyMultiplier => Skill == SkillId.VigorBloom ? 1f + Ch.power : 1f;
        public bool SilentFootsteps => Skill == SkillId.GhostStep && _id.Controller.StanceState.Value != Stance.Standing;

        // ------------------------------------------------------------------ active skill
        public void RequestUse() { if (IsOwner && HasActiveSkill && CooldownRemaining <= 0) UseSkillRpc(); }

        [Rpc(SendTo.Server)]
        void UseSkillRpc(RpcParams rpc = default)
        {
            if (rpc.Receive.SenderClientId != OwnerClientId || !_id.Health.IsAlive || !HasActiveSkill) return;
            double now = NetworkManager.ServerTime.Time;
            if (now < CooldownEndsAt.Value) return;
            CooldownEndsAt.Value = now + Ch.cooldown;
            ActiveUntil.Value = now + Ch.duration;

            switch (Skill)
            {
                case SkillId.BastionDome: _id.Health.ServerAddShield(Ch.power); StartCoroutine(ClearShieldAfter(Ch.duration)); break;
                case SkillId.HealingAura: StartCoroutine(HealingAura()); break;
                case SkillId.BlinkDash:
                    _id.Controller.ServerSetGrace(1f);
                    DashRpc(Ch.power);
                    break;
                case SkillId.PulseScan:
                    foreach (var p in PlayerIdentity.All)
                        if (p.Team.Value != _id.Team.Value && p.Health.Life.Value == PlayerLifeState.Alive
                            && Vector3.Distance(p.transform.position, transform.position) < 50f)
                            RevealToTeam(p.transform.position, Ch.duration);
                    break;
                // SprintSurge works through MoveSpeedMultiplier while ActiveUntil is in the future
            }
        }

        IEnumerator ClearShieldAfter(float s)
        {
            yield return new WaitForSeconds(s);
            _id.Health.Shield.Value = 0;
        }

        IEnumerator HealingAura()
        {
            float end = Time.time + Ch.duration;
            while (Time.time < end && _id.Health.IsAlive)
            {
                foreach (var p in PlayerIdentity.All)
                    if (p.Team.Value == _id.Team.Value && Vector3.Distance(p.transform.position, transform.position) < 5f)
                        p.Health.ServerHeal(Ch.power);
                yield return new WaitForSeconds(1f);
            }
        }

        [Rpc(SendTo.Owner)]
        void DashRpc(float meters)
        {
            var cc = GetComponent<CharacterController>();
            var input = PlayerInputHandler.Instance;
            Vector3 dir = input != null && input.Move.sqrMagnitude > 0.01f
                ? transform.rotation * new Vector3(input.Move.x, 0, input.Move.y).normalized
                : transform.forward;
            cc.Move(dir * meters);
        }

        // ------------------------------------------------------------------ hooks from other systems (server)
        public void ServerOnKnockedEnemy()
        {
            if (Skill == SkillId.Scavenger) _id.Health.ServerHeal(Ch.power);
        }

        public void ServerOnHitEnemy(PlayerIdentity target)
        {
            if (Skill == SkillId.EagleEye) RevealToTeam(target.transform.position, Ch.duration);
        }

        public void ServerOnDamaged(PlayerIdentity attacker) { _petTick = 0; }

        /// <summary>Called when someone fires an unsuppressed weapon: Byte the Owl pings them for nearby enemies.</summary>
        public static void ServerNotifyGunfire(PlayerIdentity shooter, Vector3 pos)
        {
            foreach (var p in PlayerIdentity.All)
            {
                if (p.Team.Value == shooter.Team.Value || p.Pet == null || p.Pet.buff != PetBuff.GunfirePing) continue;
                if (Vector3.Distance(p.transform.position, pos) < 30f) p.Skills.RevealRpc(pos, 2f, p.Skills.RpcTarget.Single(p.OwnerClientId, RpcTargetUse.Temp));
            }
        }

        void RevealToTeam(Vector3 pos, float seconds)
        {
            foreach (var p in PlayerIdentity.All)
                if (p.Team.Value == _id.Team.Value) RevealRpc(pos, seconds, RpcTarget.Single(p.OwnerClientId, RpcTargetUse.Temp));
        }

        [Rpc(SendTo.SpecifiedInParams)]
        void RevealRpc(Vector3 pos, float seconds, RpcParams rpc = default) => EnemyRevealed?.Invoke(pos, seconds);

        void Update()
        {
            if (!IsServer || !IsSpawned) return;
            // Pebble the Turtle: slowly mends the vest when out of combat
            var pet = _id.Pet;
            if (pet != null && pet.buff == PetBuff.ArmorMend && _id.Health.IsAlive)
            {
                _petTick += Time.deltaTime;
                if (_petTick >= 4f) { _petTick = 0; _id.Health.ServerRepairVest(1f + pet.power); }
            }
        }
    }
}
