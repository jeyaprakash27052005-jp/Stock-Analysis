using UnityEngine;

namespace Stormdrop
{
    /// <summary>Pooled, client-only visual for one loot entry (not networked).</summary>
    public class LootVisual : MonoBehaviour
    {
        public int id;
        Renderer _renderer;
        GameObject _custom;

        static readonly Color[] RarityColors =
        {
            new Color(0.85f, 0.85f, 0.85f), new Color(0.3f, 0.6f, 1f), new Color(0.7f, 0.35f, 1f), new Color(1f, 0.75f, 0.2f),
        };

        public static LootVisual Create()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Loot";
            go.layer = GameConstants.LayerLoot;
            go.transform.localScale = new Vector3(0.45f, 0.2f, 0.3f);
            Destroy(go.GetComponent<Collider>());
            var v = go.AddComponent<LootVisual>();
            v._renderer = go.GetComponent<Renderer>();
            v._renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return v;
        }

        public void Show(LootEntry e, ItemDefinition def)
        {
            id = e.id;
            transform.position = e.position + Vector3.up * 0.1f;
            gameObject.SetActive(true);
            if (_custom) Destroy(_custom);
            if (def != null && def.worldModel != null)
            {
                _renderer.enabled = false;
                _custom = Instantiate(def.worldModel, transform.position, Quaternion.identity, transform);
            }
            else
            {
                _renderer.enabled = true;
                _renderer.material.color = def != null ? RarityColors[(int)def.rarity] : Color.white;
            }
        }

        void Update() => transform.Rotate(0, 40f * Time.deltaTime, 0, Space.World);
    }
}
