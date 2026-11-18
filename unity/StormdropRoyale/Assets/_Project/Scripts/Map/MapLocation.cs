using System.Collections.Generic;
using UnityEngine;

namespace Stormdrop
{
    public enum MapZoneType { City, MilitaryBase, Farmland, Port, Mountains, River }

    /// <summary>Named area shown on the minimap and in the HUD ("You are in: Rustport").</summary>
    public class MapLocation : MonoBehaviour
    {
        public string displayName = "Rustport";
        public MapZoneType zoneType;
        public float radius = 250f;
        public static readonly List<MapLocation> All = new List<MapLocation>();

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        public static MapLocation At(Vector3 pos)
        {
            MapLocation best = null;
            float bestD = float.MaxValue;
            foreach (var l in All)
            {
                float d = Vector2.Distance(new Vector2(pos.x, pos.z), new Vector2(l.transform.position.x, l.transform.position.z));
                if (d < l.radius && d < bestD) { best = l; bestD = d; }
            }
            return best;
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1, 1, 0, 0.25f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
