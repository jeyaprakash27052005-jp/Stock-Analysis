using System;
using System.Collections;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Stormdrop
{
    public struct InventoryStack : INetworkSerializeByMemcpy, IEquatable<InventoryStack>
    {
        public FixedString32Bytes itemId;
        public int count;
        public bool Equals(InventoryStack o) => itemId.Equals(o.itemId) && count == o.count;
    }

    /// <summary>A weapon in one of the 4 slots, with its loaded magazine and attachments.</summary>
    public struct WeaponSlotState : INetworkSerializeByMemcpy, IEquatable<WeaponSlotState>
    {
        public FixedString32Bytes weaponId, scope, muzzle, foregrip, magazine, stock;
        public int mag;

        public bool IsEmpty => weaponId.Length == 0;

        public FixedString32Bytes Get(AttachmentSlot s) => s switch
        {
            AttachmentSlot.Scope => scope, AttachmentSlot.Muzzle => muzzle, AttachmentSlot.Foregrip => foregrip,
            AttachmentSlot.Magazine => magazine, _ => stock,
        };

        public void Set(AttachmentSlot s, FixedString32Bytes id)
        {
            switch (s)
            {
                case AttachmentSlot.Scope: scope = id; break;
                case AttachmentSlot.Muzzle: muzzle = id; break;
                case AttachmentSlot.Foregrip: foregrip = id; break;
                case AttachmentSlot.Magazine: magazine = id; break;
                default: stock = id; break;
            }
        }

        public bool Equals(WeaponSlotState o) => weaponId.Equals(o.weaponId) && mag == o.mag && scope.Equals(o.scope)
            && muzzle.Equals(o.muzzle) && foregrip.Equals(o.foregrip) && magazine.Equals(o.magazine) && stock.Equals(o.stock);
    }

    /// <summary>
    /// Server-authoritative backpack and weapon slots. Clients request actions with RPCs
    /// (pick up, drop, switch, use, throw, deploy) and the server validates each one.
    /// </summary>
    public class PlayerInventory : NetworkBehaviour
    {
        public static readonly int[] BackpackCapacity = { 120, 220, 290, 380 };   // level 0–3

        public readonly NetworkVariable<WeaponSlotState> Slot0 = new NetworkVariable<WeaponSlotState>();
        public readonly NetworkVariable<WeaponSlotState> Slot1 = new NetworkVariable<WeaponSlotState>();
        public readonly NetworkVariable<WeaponSlotState> Slot2 = new NetworkVariable<WeaponSlotState>();
        public readonly NetworkVariable<WeaponSlotState> Slot3 = new NetworkVariable<WeaponSlotState>();
        public readonly NetworkVariable<byte> ActiveSlot = new NetworkVariable<byte>(0);
        public readonly NetworkVariable<byte> BackpackLevel = new NetworkVariable<byte>(0);
        public readonly NetworkVariable<FixedString32Bytes> UsingItem = new NetworkVariable<FixedString32Bytes>();
        public readonly NetworkVariable<double> UsingUntil = new NetworkVariable<double>(0);
        public NetworkList<InventoryStack> Items;

        public event Action Changed;

        PlayerIdentity _id;
        Coroutine _useRoutine;
        static GameDatabase Db => GameDatabase.Instance;

        void Awake()
        {
            _id = GetComponent<PlayerIdentity>();
            Items = new NetworkList<InventoryStack>();
        }

        public override void OnNetworkSpawn()
        {
            Items.OnListChanged += _ => Changed?.Invoke();
            Slot0.OnValueChanged += (_, __) => Changed?.Invoke();
            Slot1.OnValueChanged += (_, __) => Changed?.Invoke();
            Slot2.OnValueChanged += (_, __) => Changed?.Invoke();
            Slot3.OnValueChanged += (_, __) => Changed?.Invoke();
            ActiveSlot.OnValueChanged += (_, __) => Changed?.Invoke();
            if (IsServer) ServerResetLoadout(null);
        }

        // ------------------------------------------------------------------ queries (everyone)
        public NetworkVariable<WeaponSlotState> SlotVar(int i) => i switch { 0 => Slot0, 1 => Slot1, 2 => Slot2, _ => Slot3 };
        public WeaponSlotState Slot(int i) => SlotVar(i).Value;
        public WeaponSlotState ActiveWeaponState => Slot(ActiveSlot.Value);
        public WeaponData WeaponIn(int slot) => Db.Item(Slot(slot).weaponId.ToString()) as WeaponData;

        public int Count(string itemId)
        {
            int n = 0;
            foreach (var s in Items) if (s.itemId.ToString() == itemId) n += s.count;
            return n;
        }

        public int UsedCapacity
        {
            get
            {
                int w = 0;
                foreach (var s in Items) { var d = Db.Item(s.itemId.ToString()); if (d != null) w += d.weight * s.count; }
                return w;
            }
        }

        public int Capacity => BackpackCapacity[Mathf.Clamp(BackpackLevel.Value, 0, 3)];

        public static ItemDefinition AmmoItem(AmmoType t)
        {
            foreach (var i in Db.items) if (i != null && i.kind == ItemKind.Ammo && i.ammoType == t) return i;
            return null;
        }

        public int AmmoReserve(AmmoType t) { var a = AmmoItem(t); return a == null ? 0 : Count(a.id); }

        // ------------------------------------------------------------------ server mutations
        /// <summary>Adds an item; returns how many were actually taken.</summary>
        public int ServerTryAdd(ItemDefinition def, int amount)
        {
            if (!IsServer || def == null || amount <= 0) return 0;
            switch (def.kind)
            {
                case ItemKind.Weapon: return ServerGiveWeapon((WeaponData)def, -1) ? amount : 0;
                case ItemKind.ArmorVest:
                case ItemKind.Helmet: return _id.Health.ServerEquipArmor(def) ? amount : 0;
                case ItemKind.Backpack:
                    if (def.level <= BackpackLevel.Value) return 0;
                    BackpackLevel.Value = (byte)Mathf.Clamp(def.level, 1, 3);
                    return amount;
                case ItemKind.Attachment:
                    if (ServerAutoAttach((AttachmentData)def)) return amount;
                    break;
            }
            int free = Capacity - UsedCapacity;
            int fit = def.weight <= 0 ? amount : Mathf.Min(amount, free / def.weight);
            if (fit <= 0) return 0;
            AddStack(def.id, fit, def.maxStack);
            return fit;
        }

        void AddStack(string id, int amount, int maxStack)
        {
            var fs = GameConstants.Fs32(id);
            for (int i = 0; i < Items.Count && amount > 0; i++)
            {
                var s = Items[i];
                if (!s.itemId.Equals(fs) || s.count >= maxStack) continue;
                int add = Mathf.Min(amount, maxStack - s.count);
                s.count += add;
                Items[i] = s;
                amount -= add;
            }
            while (amount > 0)
            {
                int add = Mathf.Min(amount, Mathf.Max(1, maxStack));
                Items.Add(new InventoryStack { itemId = fs, count = add });
                amount -= add;
            }
        }

        public bool ServerRemove(string id, int amount)
        {
            if (Count(id) < amount) return false;
            var fs = GameConstants.Fs32(id);
            for (int i = Items.Count - 1; i >= 0 && amount > 0; i--)
            {
                var s = Items[i];
                if (!s.itemId.Equals(fs)) continue;
                int take = Mathf.Min(amount, s.count);
                s.count -= take;
                amount -= take;
                if (s.count <= 0) Items.RemoveAt(i); else Items[i] = s;
            }
            return true;
        }

        public bool ServerGiveWeapon(WeaponData w, int forceSlot)
        {
            int slot = forceSlot;
            if (slot < 0)
            {
                if (w.category == WeaponCategory.Pistol) slot = 2;
                else if (w.category == WeaponCategory.Melee) slot = 3;
                else if (Slot0.Value.IsEmpty) slot = 0;
                else if (Slot1.Value.IsEmpty) slot = 1;
                else
                {
                    slot = ActiveSlot.Value <= 1 ? ActiveSlot.Value : 0;
                    ServerDropSlot(slot);
                }
            }
            if (!Slot(slot).IsEmpty && forceSlot < 0 && slot >= 2) ServerDropSlot(slot);
            SlotVar(slot).Value = new WeaponSlotState { weaponId = GameConstants.Fs32(w.id), mag = w.magazineSize };
            if (Slot(ActiveSlot.Value).IsEmpty || forceSlot >= 0) ActiveSlot.Value = (byte)slot;
            return true;
        }

        bool ServerAutoAttach(AttachmentData a)
        {
            for (int k = 0; k < 2; k++)
            {
                int slot = k == 0 ? ActiveSlot.Value : (ActiveSlot.Value == 0 ? 1 : 0);
                if (slot > 2) continue;
                var st = Slot(slot);
                var w = WeaponIn(slot);
                if (w == null || !a.Fits(w)) continue;
                var current = Db.Item(st.Get(a.slot).ToString());
                if (current != null && current.rarity >= a.rarity) continue;
                if (current != null) LootSpawner.Instance?.ServerAddDrop(current, 1, transform.position);
                st.Set(a.slot, GameConstants.Fs32(a.id));
                SlotVar(slot).Value = st;
                return true;
            }
            return false;
        }

        public void ServerSetSlot(int slot, WeaponSlotState s) => SlotVar(slot).Value = s;

        void ServerDropSlot(int slot)
        {
            var st = Slot(slot);
            if (st.IsEmpty) return;
            if (st.weaponId.ToString() == "melee_fists") { SlotVar(slot).Value = default; return; }
            var spawner = LootSpawner.Instance;
            var pos = transform.position + transform.forward;
            spawner?.ServerAddDrop(Db.Item(st.weaponId.ToString()), 1, pos);
            foreach (AttachmentSlot a in Enum.GetValues(typeof(AttachmentSlot)))
                if (st.Get(a).Length > 0) spawner?.ServerAddDrop(Db.Item(st.Get(a).ToString()), 1, pos);
            SlotVar(slot).Value = default;
        }

        /// <summary>Death crate: everything is scattered on the ground for others to loot.</summary>
        public void ServerDropAll(Vector3 pos)
        {
            for (int i = 0; i < 3; i++) ServerDropSlot(i);
            var spawner = LootSpawner.Instance;
            foreach (var s in Items) spawner?.ServerAddDrop(Db.Item(s.itemId.ToString()), s.count, pos);
            Items.Clear();
        }

        /// <summary>Fresh loadout: fists only (BR) or a given weapon + ammo + armor (Clash Squad, TDM, Training).</summary>
        public void ServerResetLoadout(WeaponData weapon)
        {
            Items.Clear();
            Slot0.Value = Slot1.Value = Slot2.Value = default;
            BackpackLevel.Value = 0;
            var fists = Db.Item("melee_fists") as WeaponData;
            Slot3.Value = fists != null ? new WeaponSlotState { weaponId = GameConstants.Fs32(fists.id) } : default;
            ActiveSlot.Value = 3;
            if (weapon == null) return;
            ServerGiveWeapon(weapon, 0);
            BackpackLevel.Value = 2;
            var ammo = AmmoItem(weapon.ammo);
            if (ammo != null) AddStack(ammo.id, weapon.magazineSize * 4, ammo.maxStack);
            var med = Db.Item("med_kit");
            if (med != null) AddStack(med.id, 3, med.maxStack);
            var wall = Db.Item("foam_wall");
            if (wall != null) AddStack(wall.id, 2, wall.maxStack);
            _id.Health.ServerSetArmor(2, 2);
        }

        public void ServerCancelUse()
        {
            if (_useRoutine != null) StopCoroutine(_useRoutine);
            _useRoutine = null;
            UsingItem.Value = default;
            UsingUntil.Value = 0;
        }

        bool SenderOk(RpcParams rpc) => rpc.Receive.SenderClientId == OwnerClientId;
        bool CanAct => _id.Health.Life.Value == PlayerLifeState.Alive;

        // ------------------------------------------------------------------ client requests
        [Rpc(SendTo.Server)]
        public void PickupRpc(int lootId, RpcParams rpc = default)
        {
            if (!SenderOk(rpc) || !CanAct || LootSpawner.Instance == null) return;
            float range = 3.2f * (_id.Pet != null && _id.Pet.buff == PetBuff.QuickPaws ? 1.3f : 1f);
            if (!LootSpawner.Instance.ServerTryTake(lootId, transform.position, range, out var entry)) return;
            var def = Db.Item(entry.itemId);
            int taken = ServerTryAdd(def, entry.amount);
            if (taken < entry.amount) LootSpawner.Instance.ServerAddDrop(def, entry.amount - taken, entry.position);
        }

        [Rpc(SendTo.Server)]
        public void DropRpc(FixedString32Bytes itemId, int amount, RpcParams rpc = default)
        {
            if (!SenderOk(rpc) || !CanAct) return;
            var def = Db.Item(itemId.ToString());
            amount = Mathf.Min(amount, Count(itemId.ToString()));
            if (def == null || amount <= 0) return;
            ServerRemove(def.id, amount);
            LootSpawner.Instance?.ServerAddDrop(def, amount, transform.position + transform.forward);
        }

        [Rpc(SendTo.Server)]
        public void DropWeaponRpc(byte slot, RpcParams rpc = default)
        {
            if (SenderOk(rpc) && CanAct && slot < 3) ServerDropSlot(slot);
        }

        [Rpc(SendTo.Server)]
        public void SwitchSlotRpc(byte slot, RpcParams rpc = default)
        {
            if (!SenderOk(rpc) || slot > 3 || Slot(slot).IsEmpty) return;
            ActiveSlot.Value = slot;
            _id.Weapons?.ServerCancelReload();
        }

        /// <summary>Med kit, Vigor Shroom or armor repair kit — takes time; cancelled by knock/death.</summary>
        [Rpc(SendTo.Server)]
        public void UseItemRpc(FixedString32Bytes itemId, RpcParams rpc = default)
        {
            if (!SenderOk(rpc) || !CanAct || _useRoutine != null) return;
            var def = Db.Item(itemId.ToString());
            if (def == null || Count(def.id) <= 0) return;
            if (def.kind != ItemKind.Medkit && def.kind != ItemKind.VigorShroom && def.kind != ItemKind.ArmorRepairKit) return;
            if (def.kind == ItemKind.Medkit && _id.Health.Health.Value >= GameConstants.MaxHealth) return;
            _useRoutine = StartCoroutine(UseRoutine(def));
        }

        IEnumerator UseRoutine(ItemDefinition def)
        {
            float time = def.useSeconds;
            if (def.kind == ItemKind.Medkit && _id.Skills != null) time *= _id.Skills.HealTimeMultiplier;
            UsingItem.Value = GameConstants.Fs32(def.id);
            UsingUntil.Value = NetworkManager.ServerTime.Time + time;
            yield return new WaitForSeconds(time);
            _useRoutine = null;
            UsingItem.Value = default;
            if (!CanAct || !ServerRemove(def.id, 1)) yield break;
            switch (def.kind)
            {
                case ItemKind.Medkit:
                    _id.Health.ServerHeal(def.amount * (_id.Skills != null ? _id.Skills.HealAmountMultiplier : 1f)); break;
                case ItemKind.VigorShroom:
                    _id.Health.ServerAddEnergy(def.amount * (_id.Skills != null ? _id.Skills.EnergyMultiplier : 1f)); break;
                case ItemKind.ArmorRepairKit:
                    _id.Health.ServerRepairVest(def.amount); break;
            }
        }

        [Rpc(SendTo.Server)]
        public void ThrowRpc(FixedString32Bytes itemId, Vector3 origin, Vector3 velocity, RpcParams rpc = default)
        {
            if (!SenderOk(rpc) || !CanAct) return;
            var def = Db.Item(itemId.ToString());
            if (def == null || def.kind != ItemKind.Throwable || !ServerRemove(def.id, 1)) return;
            if (Vector3.Distance(origin, transform.position + Vector3.up * 1.5f) > 2.5f) origin = transform.position + Vector3.up * 1.5f;
            velocity = Vector3.ClampMagnitude(velocity, 28f);
            var go = Instantiate(Db.grenadePrefab, origin, Quaternion.identity);
            var g = go.GetComponent<Grenade>();
            g.type = def.throwable;
            g.thrower = _id;
            go.GetComponent<NetworkObject>().Spawn();
            g.ServerLaunch(velocity, def.throwable);
        }

        [Rpc(SendTo.Server)]
        public void DeployWallRpc(Vector3 position, float yaw, RpcParams rpc = default)
        {
            if (!SenderOk(rpc) || !CanAct || Vector3.Distance(position, transform.position) > 6f) return;
            var def = Db.Item("foam_wall");
            if (def == null || !ServerRemove(def.id, 1)) return;
            var go = Instantiate(Db.foamWallPrefab, position, Quaternion.Euler(0, yaw, 0));
            go.GetComponent<NetworkObject>().Spawn();
        }
    }
}
