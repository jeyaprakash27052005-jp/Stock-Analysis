using UnityEngine;

namespace Stormdrop
{
    [CreateAssetMenu(menuName = "Stormdrop/Attachment")]
    public class AttachmentData : ItemDefinition
    {
        [Header("Attachment")]
        public AttachmentSlot slot;
        public float scopeZoom = 1f;            // 2, 4, 8 for scopes
        public float spreadMultiplier = 1f;     // < 1 = tighter
        public float recoilMultiplier = 1f;     // < 1 = less recoil
        public float rangeMultiplier = 1f;
        public float magazineBonus = 0f;        // +% magazine size (0.5 = +50%)
        public float reloadMultiplier = 1f;
        public bool suppressor;                 // hides shots from the minimap
        public WeaponCategory[] fitsCategories;

        public bool Fits(WeaponData w)
        {
            if (System.Array.IndexOf(w.allowedSlots, slot) < 0) return false;
            return fitsCategories == null || fitsCategories.Length == 0 || System.Array.IndexOf(fitsCategories, w.category) >= 0;
        }
    }
}
