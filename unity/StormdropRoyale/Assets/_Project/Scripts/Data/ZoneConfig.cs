using System;
using UnityEngine;

namespace Stormdrop
{
    /// <summary>Safe-zone phases. Default values give a ~11 minute match on a 4 km map.</summary>
    [CreateAssetMenu(menuName = "Stormdrop/Zone Config")]
    public class ZoneConfig : ScriptableObject
    {
        [Serializable]
        public struct Phase
        {
            public float waitSeconds;     // zone holds, next circle is shown
            public float shrinkSeconds;   // zone shrinks to targetRadius
            public float targetRadius;
            public float damagePerSecond; // damage outside the zone during this phase
        }

        public float startRadius = 2900f;
        public float firstPhaseDelay = 60f;     // after the plane leaves
        public Phase[] phases =
        {
            new Phase { waitSeconds = 90, shrinkSeconds = 60, targetRadius = 1400, damagePerSecond = 1 },
            new Phase { waitSeconds = 60, shrinkSeconds = 50, targetRadius = 900,  damagePerSecond = 2 },
            new Phase { waitSeconds = 50, shrinkSeconds = 40, targetRadius = 550,  damagePerSecond = 4 },
            new Phase { waitSeconds = 45, shrinkSeconds = 35, targetRadius = 320,  damagePerSecond = 6 },
            new Phase { waitSeconds = 40, shrinkSeconds = 30, targetRadius = 160,  damagePerSecond = 9 },
            new Phase { waitSeconds = 30, shrinkSeconds = 25, targetRadius = 70,   damagePerSecond = 12 },
            new Phase { waitSeconds = 20, shrinkSeconds = 20, targetRadius = 20,   damagePerSecond = 16 },
            new Phase { waitSeconds = 15, shrinkSeconds = 15, targetRadius = 0,    damagePerSecond = 25 },
        };
        [Tooltip("Airdrops arrive at the start of these phase indexes")]
        public int[] airdropPhases = { 1, 3, 5 };
    }
}
