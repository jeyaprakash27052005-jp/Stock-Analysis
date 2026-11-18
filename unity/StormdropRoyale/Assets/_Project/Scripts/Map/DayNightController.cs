using UnityEngine;

namespace Stormdrop
{
    /// <summary>Switches lighting between the day and night variant chosen by the server for this match.</summary>
    public class DayNightController : MonoBehaviour
    {
        public Light sun;
        public Color dayAmbient = new Color(0.62f, 0.66f, 0.72f);
        public Color nightAmbient = new Color(0.12f, 0.14f, 0.24f);
        public Color dayFog = new Color(0.7f, 0.8f, 0.9f);
        public Color nightFog = new Color(0.05f, 0.07f, 0.12f);
        bool? _applied;

        void Update()
        {
            var mm = MatchManager.Instance;
            bool night = mm != null && mm.Night.Value;
            if (_applied == night) return;
            _applied = night;
            if (sun == null) sun = FindFirstObjectByType<Light>();
            if (sun != null)
            {
                sun.intensity = night ? 0.25f : 1.1f;
                sun.color = night ? new Color(0.6f, 0.7f, 1f) : new Color(1f, 0.96f, 0.88f);
                sun.transform.rotation = Quaternion.Euler(night ? 20f : 50f, -30f, 0);
            }
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = night ? nightAmbient : dayAmbient;
            RenderSettings.fog = true;
            RenderSettings.fogColor = night ? nightFog : dayFog;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = night ? 40f : 250f;
            RenderSettings.fogEndDistance = night ? 260f : 1100f;
        }
    }
}
