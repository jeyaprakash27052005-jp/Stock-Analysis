using System.Collections.Generic;
using UnityEngine;

namespace Stormdrop
{
    /// <summary>Spawn location for the waiting area, Clash Squad / TDM teams (0, 1) and Training (-1 = any).</summary>
    public class SpawnPoint : MonoBehaviour
    {
        public int team = -1;
        static readonly List<SpawnPoint> All = new List<SpawnPoint>();

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        public static Transform Pick(int team)
        {
            var options = All.FindAll(s => team < 0 || s.team == team || s.team < 0);
            if (options.Count == 0) options = All;
            if (options.Count == 0) { var t = new GameObject("FallbackSpawn").transform; t.position = new Vector3(0, 60, 0); return t; }
            return options[Random.Range(0, options.Count)].transform;
        }

        void OnDrawGizmos()
        {
            Gizmos.color = team == 0 ? Color.blue : team == 1 ? Color.red : Color.white;
            Gizmos.DrawSphere(transform.position + Vector3.up, 0.5f);
        }
    }
}
