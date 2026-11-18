using UnityEngine;

namespace Stormdrop
{
    [CreateAssetMenu(menuName = "Stormdrop/Pet")]
    public class PetData : ScriptableObject
    {
        public string id;
        public string displayName;
        public PetBuff buff;
        public float power = 0.5f;
        public GameObject model;
        [TextArea] public string description;
    }
}
