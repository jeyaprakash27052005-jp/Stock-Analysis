using UnityEngine;

namespace Stormdrop
{
    /// <summary>
    /// Draws the zone as a tall, semi-transparent cylinder wall, plus a thin ring for the next circle.
    /// Placed in the game scene; costs a single draw call so it is fine on low-end phones.
    /// </summary>
    public class SafeZoneVisual : MonoBehaviour
    {
        public Color wallColor = new Color(0.35f, 0.55f, 1f, 0.35f);
        public Color nextColor = new Color(1f, 1f, 1f, 0.9f);
        public float wallHeight = 600f;

        Transform _wall;
        LineRenderer _nextRing;
        const int Segments = 96;

        void Start()
        {
            var cyl = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(cyl.GetComponent<Collider>());
            cyl.name = "ZoneWall";
            _wall = cyl.transform;
            _wall.SetParent(transform);
            var mat = new Material(Shader.Find("Sprites/Default")) { color = wallColor };
            var r = cyl.GetComponent<Renderer>();
            r.material = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            // Render the inside faces too by flipping a copy of the mesh
            var mesh = Instantiate(cyl.GetComponent<MeshFilter>().sharedMesh);
            var tris = mesh.triangles;
            var both = new int[tris.Length * 2];
            for (int i = 0; i < tris.Length; i += 3)
            {
                both[i] = tris[i]; both[i + 1] = tris[i + 1]; both[i + 2] = tris[i + 2];
                both[tris.Length + i] = tris[i]; both[tris.Length + i + 1] = tris[i + 2]; both[tris.Length + i + 2] = tris[i + 1];
            }
            mesh.triangles = both;
            cyl.GetComponent<MeshFilter>().sharedMesh = mesh;

            var ringGo = new GameObject("NextZoneRing");
            ringGo.transform.SetParent(transform);
            _nextRing = ringGo.AddComponent<LineRenderer>();
            _nextRing.positionCount = Segments;
            _nextRing.loop = true;
            _nextRing.widthMultiplier = 3f;
            _nextRing.material = new Material(Shader.Find("Sprites/Default"));
            _nextRing.startColor = _nextRing.endColor = nextColor;
        }

        void LateUpdate()
        {
            var z = SafeZoneManager.Instance;
            bool show = z != null && z.State.Value != ZoneState.Inactive;
            _wall.gameObject.SetActive(show);
            _nextRing.enabled = show && z.State.Value != ZoneState.Final;
            if (!show) return;

            z.Current(out var c, out var r);
            _wall.position = new Vector3(c.x, wallHeight / 2f - 50f, c.y);
            _wall.localScale = new Vector3(r * 2f, wallHeight / 2f, r * 2f);

            Vector2 nc = z.ToCenter.Value;
            float nr = z.ToRadius.Value;
            for (int i = 0; i < Segments; i++)
            {
                float a = i * Mathf.PI * 2f / Segments;
                float x = nc.x + Mathf.Cos(a) * nr, y = nc.y + Mathf.Sin(a) * nr;
                float h = Terrain.activeTerrain ? Terrain.activeTerrain.SampleHeight(new Vector3(x, 0, y)) + 2f : 5f;
                _nextRing.SetPosition(i, new Vector3(x, h, y));
            }
        }
    }
}
