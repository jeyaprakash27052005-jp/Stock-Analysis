using UnityEngine;

namespace Stormdrop
{
    public enum SkillId : byte
    {
        None,
        // Active (button on HUD)
        SprintSurge, BastionDome, HealingAura, BlinkDash, PulseScan,
        // Passive
        FieldMedic, GhostStep, EagleEye, IronSkin, Scavenger, QuickHands, VigorBloom,
    }

    public enum PetBuff : byte { None, SoftLanding, GunfirePing, ArmorMend, QuickPaws }

    [CreateAssetMenu(menuName = "Stormdrop/Character")]
    public class CharacterData : ScriptableObject
    {
        [Tooltip("Must match the item id in the backend, e.g. char_kael")]
        public string id;
        public string displayName;
        [TextArea] public string bio;
        public Color placeholderColor = Color.white;
        public GameObject model;               // optional: your rigged character model
        public Sprite portrait;
        public SkillId skill;
        public bool IsActiveSkill => skill >= SkillId.SprintSurge && skill <= SkillId.PulseScan;
        public float cooldown = 45f;
        public float duration = 6f;
        public float power = 0.3f;             // meaning depends on skill (speed %, shield HP, heal/s …)
        [TextArea] public string skillDescription;
    }
}
