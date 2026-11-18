using System.Collections.Generic;
using UnityEngine;

namespace Stormdrop
{
    /// <summary>
    /// Server-side position history for every player, so hits can be checked against
    /// where the target was when the shooter fired (rewinding by the shooter's latency).
    /// </summary>
    public static class LagCompensation
    {
        const int Capacity = 32;                // ~1 s at 30 Hz
        public const double MaxRewind = 0.35;   // seconds

        struct Sample { public double time; public Vector3 pos; }

        class History
        {
            public readonly Sample[] buf = new Sample[Capacity];
            public int head, count;
        }

        static readonly Dictionary<ulong, History> _histories = new Dictionary<ulong, History>();

        public static void Record(ulong objectId, double time, Vector3 pos)
        {
            if (!_histories.TryGetValue(objectId, out var h)) _histories[objectId] = h = new History();
            h.buf[h.head] = new Sample { time = time, pos = pos };
            h.head = (h.head + 1) % Capacity;
            if (h.count < Capacity) h.count++;
        }

        public static void Forget(ulong objectId) => _histories.Remove(objectId);

        /// <summary>Interpolated position at a past time (clamped to the history window).</summary>
        public static bool TryGet(ulong objectId, double time, out Vector3 pos)
        {
            pos = default;
            if (!_histories.TryGetValue(objectId, out var h) || h.count == 0) return false;
            Sample newer = h.buf[(h.head - 1 + Capacity) % Capacity];
            if (time >= newer.time) { pos = newer.pos; return true; }
            for (int i = 1; i < h.count; i++)
            {
                Sample older = h.buf[(h.head - 1 - i + Capacity * 2) % Capacity];
                if (older.time <= time)
                {
                    float t = (float)((time - older.time) / System.Math.Max(1e-6, newer.time - older.time));
                    pos = Vector3.Lerp(older.pos, newer.pos, t);
                    return true;
                }
                newer = older;
            }
            pos = newer.pos;
            return true;
        }
    }
}
