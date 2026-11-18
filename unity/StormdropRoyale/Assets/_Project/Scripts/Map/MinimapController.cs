using UnityEngine;
using UnityEngine.UI;

namespace Stormdrop
{
    /// <summary>
    /// Minimap: the island is rendered ONCE from above into a texture (no per-frame camera cost),
    /// then markers for you, teammates, pings, zone and revealed enemies are drawn as UI on top.
    /// </summary>
    public class MinimapController : MonoBehaviour
    {
        public static Texture MapTexture { get; private set; }

        public RawImage mapImage;
        public RectTransform markerLayer;
        public float zoomMeters = 350f;       // half-width of the visible area
        public bool fullMap;

        Image _me;
        readonly System.Collections.Generic.List<Image> _pool = new System.Collections.Generic.List<Image>();
        int _used;
        readonly System.Collections.Generic.List<(Vector3 pos, float until)> _revealed = new System.Collections.Generic.List<(Vector3, float)>();

        void OnEnable() => PlayerSkills.EnemyRevealed += OnReveal;
        void OnDisable() => PlayerSkills.EnemyRevealed -= OnReveal;
        void OnReveal(Vector3 p, float s) => _revealed.Add((p, Time.time + s));

        public static void RenderMapOnce()
        {
            if (MapTexture != null) return;
            var go = new GameObject("MinimapCapture");
            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = GameConstants.MapSize / 2f;
            cam.transform.SetPositionAndRotation(new Vector3(0, 800, 0), Quaternion.Euler(90, 0, 0));
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.18f, 0.4f, 0.6f);
            cam.farClipPlane = 1500;
            var rt = new RenderTexture(512, 512, 16);
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = null;
            Destroy(go);
            MapTexture = rt;
        }

        void LateUpdate()
        {
            var me = PlayerIdentity.Local;
            if (me == null || mapImage == null) return;
            if (MapTexture == null) RenderMapOnce();
            mapImage.texture = MapTexture;

            float half = fullMap ? GameConstants.MapSize / 2f : zoomMeters;
            Vector3 c = fullMap ? Vector3.zero : me.transform.position;
            float size = GameConstants.MapSize;
            mapImage.uvRect = new Rect((c.x - half + size / 2f) / size, (c.z - half + size / 2f) / size, half * 2f / size, half * 2f / size);

            _used = 0;
            Rect r = markerLayer.rect;
            Vector2 ToUi(Vector3 w) => new Vector2((w.x - c.x) / half * r.width / 2f, (w.z - c.z) / half * r.height / 2f);
            bool InView(Vector2 ui) => Mathf.Abs(ui.x) <= r.width / 2f && Mathf.Abs(ui.y) <= r.height / 2f;

            Marker(ToUi(me.transform.position), Color.yellow, 10f, me.transform.eulerAngles.y);
            foreach (var p in PlayerIdentity.All)
                if (p.IsTeammateOfLocal && p.Health.Life.Value != PlayerLifeState.Dead) Marker(ToUi(p.transform.position), Color.cyan, 8f, 0);
            foreach (var ping in PingSystem.Active)
            {
                var ui = ToUi(ping.position);
                if (InView(ui)) Marker(ui, ping.type == PingType.Enemy ? Color.red : Color.white, 9f, 45f);
            }
            _revealed.RemoveAll(x => x.until < Time.time);
            foreach (var e in _revealed) { var ui = ToUi(e.pos); if (InView(ui)) Marker(ui, new Color(1f, 0.3f, 0.2f), 9f, 0); }

            var zone = SafeZoneManager.Instance;
            if (zone != null && zone.State.Value != ZoneState.Inactive)
            {
                var nc = ToUi(new Vector3(zone.ToCenter.Value.x, 0, zone.ToCenter.Value.y));
                if (InView(nc)) Marker(nc, Color.white, 6f, 45f);
            }
            for (int i = _used; i < _pool.Count; i++) _pool[i].enabled = false;
        }

        void Marker(Vector2 pos, Color color, float size, float rotation)
        {
            Image img;
            if (_used < _pool.Count) img = _pool[_used];
            else
            {
                var go = new GameObject("Marker", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(markerLayer, false);
                img = go.GetComponent<Image>();
                img.raycastTarget = false;
                _pool.Add(img);
            }
            _used++;
            img.enabled = true;
            img.color = color;
            var rt = img.rectTransform;
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(size, size);
            rt.localRotation = Quaternion.Euler(0, 0, -rotation);
        }
    }
}
