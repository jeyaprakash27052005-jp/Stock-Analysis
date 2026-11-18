using UnityEngine;

namespace Stormdrop
{
    public enum WeaponCategory : byte { AssaultRifle, SMG, Shotgun, Sniper, LMG, Pistol, Melee }

    public enum AttachmentSlot : byte { Scope, Muzzle, Foregrip, Magazine, Stock }

    public enum WeaponSlot : byte { Primary1 = 0, Primary2 = 1, Pistol = 2, Melee = 3 }

    [CreateAssetMenu(menuName = "Stormdrop/Weapon")]
    public class WeaponData : ItemDefinition
    {
        [Header("Weapon")]
        public WeaponCategory category;
        public AmmoType ammo;
        public float damage = 30f;              // per bullet / pellet, before body-part multiplier
        public float headshotMultiplier = 1.8f;
        public float limbMultiplier = 0.85f;
        public float roundsPerMinute = 600f;
        public bool automatic = true;
        public int magazineSize = 30;
        public float reloadSeconds = 2.2f;
        public float range = 150f;              // max effective distance (server rejects hits beyond range * 1.1)
        public float falloffStart = 50f;
        [Range(0, 1)] public float minFalloffDamage = 0.5f;
        public float hipSpread = 3f;            // degrees
        public float adsSpread = 0.6f;
        public float recoilVertical = 1.2f;
        public float recoilHorizontal = 0.4f;
        public int pellets = 1;                 // shotguns > 1
        public float moveSpeedMultiplier = 1f;
        public float adsZoom = 1.3f;            // iron-sight zoom (scope overrides)
        public AttachmentSlot[] allowedSlots = { AttachmentSlot.Scope, AttachmentSlot.Muzzle, AttachmentSlot.Magazine };

        public float SecondsPerShot => 60f / Mathf.Max(1f, roundsPerMinute);
        public WeaponSlot DefaultSlot => category == WeaponCategory.Pistol ? WeaponSlot.Pistol
                                        : category == WeaponCategory.Melee ? WeaponSlot.Melee : WeaponSlot.Primary1;
    }
}
