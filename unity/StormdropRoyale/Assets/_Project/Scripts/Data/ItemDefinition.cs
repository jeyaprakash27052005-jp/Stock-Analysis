using UnityEngine;

namespace Stormdrop
{
    public enum ItemKind : byte
    {
        Weapon, Ammo, Attachment, Medkit, VigorShroom, ArmorVest, Helmet, Backpack, Throwable, FoamWall, ArmorRepairKit
    }

    public enum ItemRarity : byte { Common, Rare, Epic, Legendary }

    public enum AmmoType : byte { None, Light, Medium, Heavy, Shells, Sniper }

    public enum ThrowableType : byte { Frag, Smoke, Flash }

    /// <summary>Anything that can lie on the ground and go into a backpack.</summary>
    [CreateAssetMenu(menuName = "Stormdrop/Item")]
    public class ItemDefinition : ScriptableObject
    {
        [Tooltip("Stable id used over the network and in loot tables, e.g. med_kit")]
        public string id;
        public string displayName;
        public ItemKind kind;
        public ItemRarity rarity;
        public Sprite icon;
        public GameObject worldModel;          // optional; a coloured cube is used if empty
        [Tooltip("Backpack space per unit")] public int weight = 1;
        public int maxStack = 1;
        public int pickupAmount = 1;

        [Header("Kind-specific values")]
        public int level;                      // armor / helmet / backpack level (1–4)
        public float amount;                   // heal HP, energy, armor durability, backpack capacity
        public float useSeconds;               // time to use a medkit / shroom
        public AmmoType ammoType;              // for Ammo items
        public ThrowableType throwable;        // for Throwable items
    }
}
