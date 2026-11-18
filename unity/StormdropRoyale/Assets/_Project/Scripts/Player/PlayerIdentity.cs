using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Stormdrop
{
    /// <summary>Who this player is (from the verified join ticket) and shortcuts to its other components.</summary>
    [RequireComponent(typeof(PlayerController), typeof(PlayerHealth), typeof(PlayerInventory))]
    public class PlayerIdentity : NetworkBehaviour
    {
        public static readonly List<PlayerIdentity> All = new List<PlayerIdentity>();
        public static PlayerIdentity Local { get; private set; }
        public static event System.Action<PlayerIdentity> LocalSpawned;

        public readonly NetworkVariable<FixedString32Bytes> Nickname = new NetworkVariable<FixedString32Bytes>();
        public readonly NetworkVariable<int> Team = new NetworkVariable<int>();
        public readonly NetworkVariable<FixedString32Bytes> CharacterId = new NetworkVariable<FixedString32Bytes>();
        public readonly NetworkVariable<FixedString32Bytes> PetId = new NetworkVariable<FixedString32Bytes>();

        /// <summary>Server only: backend player UUID.</summary>
        public string BackendPlayerId { get; private set; }

        public PlayerController Controller { get; private set; }
        public PlayerHealth Health { get; private set; }
        public PlayerInventory Inventory { get; private set; }
        public WeaponSystem Weapons { get; private set; }
        public PlayerSkills Skills { get; private set; }
        public PingSystem Pings { get; private set; }

        public CharacterData Character => GameDatabase.Instance.Character(CharacterId.Value.ToString());
        public PetData Pet => GameDatabase.Instance.Pet(PetId.Value.ToString());
        public bool IsTeammateOfLocal => Local != null && Local != this && Local.Team.Value == Team.Value;

        void Awake()
        {
            Controller = GetComponent<PlayerController>();
            Health = GetComponent<PlayerHealth>();
            Inventory = GetComponent<PlayerInventory>();
            Weapons = GetComponent<WeaponSystem>();
            Skills = GetComponent<PlayerSkills>();
            Pings = GetComponent<PingSystem>();
        }

        public override void OnNetworkSpawn()
        {
            All.Add(this);
            if (IsServer && NetworkBootstrap.Instance.Tickets.TryGetValue(OwnerClientId, out var t))
            {
                BackendPlayerId = t.PlayerId;
                Nickname.Value = GameConstants.Fs32(Truncate(t.Nickname, 16));
                Team.Value = t.Team;
                CharacterId.Value = GameConstants.Fs32(t.CharacterId ?? "char_kael");
                PetId.Value = GameConstants.Fs32(t.PetId ?? "");
                MatchManager.Instance?.RegisterPlayer(this);
            }
            if (IsOwner)
            {
                Local = this;
                LocalSpawned?.Invoke(this);
            }
            ApplyPlaceholderLook();
            CharacterId.OnValueChanged += (_, __) => ApplyPlaceholderLook();
        }

        public override void OnNetworkDespawn()
        {
            All.Remove(this);
            if (IsServer)
            {
                MatchManager.Instance?.OnPlayerRemoved(this);
                LagCompensation.Forget(NetworkObjectId);
            }
            if (Local == this) Local = null;
        }

        /// <summary>Until real character models are added, tint the capsule with the character's colour.</summary>
        void ApplyPlaceholderLook()
        {
            var c = Character;
            if (c == null) return;
            foreach (var r in GetComponentsInChildren<Renderer>())
                if (r.gameObject.name == "Body") r.material.color = c.placeholderColor;
        }

        static string Truncate(string s, int n) => string.IsNullOrEmpty(s) ? "Player" : s.Length <= n ? s : s.Substring(0, n);
    }
}
