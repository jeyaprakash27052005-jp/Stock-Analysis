using System.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Stormdrop
{
    public enum ZoneState : byte { Inactive, Waiting, Shrinking, Final }

    /// <summary>
    /// Shrinking safe zone. The server picks each next circle inside the current one and deals
    /// damage outside it every second. Clients interpolate the radius from shared server time,
    /// so only a few values are ever sent over the network.
    /// </summary>
    public class SafeZoneManager : NetworkBehaviour
    {
        public static SafeZoneManager Instance { get; private set; }

        public readonly NetworkVariable<ZoneState> State = new NetworkVariable<ZoneState>(ZoneState.Inactive);
        public readonly NetworkVariable<int> PhaseIndex = new NetworkVariable<int>(-1);
        public readonly NetworkVariable<Vector2> FromCenter = new NetworkVariable<Vector2>();
        public readonly NetworkVariable<float> FromRadius = new NetworkVariable<float>(5000f);
        public readonly NetworkVariable<Vector2> ToCenter = new NetworkVariable<Vector2>();
        public readonly NetworkVariable<float> ToRadius = new NetworkVariable<float>(5000f);
        public readonly NetworkVariable<double> ShrinkStart = new NetworkVariable<double>(0);
        public readonly NetworkVariable<double> ShrinkEnd = new NetworkVariable<double>(0);
        public readonly NetworkVariable<float> DamagePerSecond = new NetworkVariable<float>(0);

        ZoneConfig _cfg;
        Coroutine _run;

        public override void OnNetworkSpawn()
        {
            Instance = this;
            _cfg = GameDatabase.Instance.zoneConfig;
        }

        public override void OnNetworkDespawn() { if (Instance == this) Instance = null; }

        double Now => NetworkManager.ServerTime.Time;

        /// <summary>Current circle (interpolated during shrink).</summary>
        public void Current(out Vector2 center, out float radius)
        {
            if (!IsSpawned || State.Value == ZoneState.Inactive) { center = Vector2.zero; radius = 99999f; return; }
            float t = ShrinkEnd.Value > ShrinkStart.Value
                ? Mathf.Clamp01((float)((Now - ShrinkStart.Value) / (ShrinkEnd.Value - ShrinkStart.Value))) : 0f;
            if (State.Value == ZoneState.Waiting) t = 0;
            center = Vector2.Lerp(FromCenter.Value, ToCenter.Value, t);
            radius = Mathf.Lerp(FromRadius.Value, ToRadius.Value, t);
        }

        public bool IsInside(Vector3 pos)
        {
            Current(out var c, out var r);
            return Vector2.Distance(new Vector2(pos.x, pos.z), c) <= r;
        }

        /// <summary>Seconds until the next change (for the HUD timer).</summary>
        public float SecondsToNextEvent =>
            State.Value == ZoneState.Waiting ? Mathf.Max(0, (float)(ShrinkStart.Value - Now))
            : State.Value == ZoneState.Shrinking ? Mathf.Max(0, (float)(ShrinkEnd.Value - Now)) : 0f;

        // ------------------------------------------------------------------ server
        public void ServerBegin()
        {
            if (!IsServer || _run != null) return;
            _run = StartCoroutine(Run());
            StartCoroutine(DamageLoop());
        }

        public void ServerStop()
        {
            if (_run != null) StopCoroutine(_run);
            _run = null;
        }

        IEnumerator Run()
        {
            var center = Vector2.zero;
            float radius = _cfg.startRadius;
            FromCenter.Value = ToCenter.Value = center;
            FromRadius.Value = ToRadius.Value = radius;
            State.Value = ZoneState.Waiting;
            ShrinkStart.Value = ShrinkEnd.Value = Now + _cfg.firstPhaseDelay;
            yield return new WaitForSeconds(_cfg.firstPhaseDelay);

            for (int i = 0; i < _cfg.phases.Length; i++)
            {
                var ph = _cfg.phases[i];
                PhaseIndex.Value = i;
                // Next circle lies fully inside the current one
                Vector2 next = center + Random.insideUnitCircle * Mathf.Max(0, radius - ph.targetRadius);
                next = ClampToLand(next, ph.targetRadius);
                FromCenter.Value = center; FromRadius.Value = radius;
                ToCenter.Value = next; ToRadius.Value = ph.targetRadius;
                DamagePerSecond.Value = ph.damagePerSecond;
                State.Value = ZoneState.Waiting;
                ShrinkStart.Value = Now + ph.waitSeconds;
                ShrinkEnd.Value = ShrinkStart.Value + ph.shrinkSeconds;
                if (System.Array.IndexOf(_cfg.airdropPhases, i) >= 0) AirdropManager.Instance?.ServerDrop(next, ph.targetRadius);

                yield return new WaitForSeconds(ph.waitSeconds);
                State.Value = ZoneState.Shrinking;
                yield return new WaitForSeconds(ph.shrinkSeconds);
                center = next;
                radius = ph.targetRadius;
            }
            State.Value = ZoneState.Final;
        }

        static Vector2 ClampToLand(Vector2 c, float r)
        {
            float half = GameConstants.MapSize / 2f - Mathf.Min(r, 300f);
            return new Vector2(Mathf.Clamp(c.x, -half, half), Mathf.Clamp(c.y, -half, half));
        }

        IEnumerator DamageLoop()
        {
            var wait = new WaitForSeconds(1f);
            while (true)
            {
                yield return wait;
                if (State.Value == ZoneState.Inactive) continue;
                float dps = Mathf.Max(1f, DamagePerSecond.Value);
                foreach (var p in PlayerIdentity.All)
                {
                    var life = p.Health.Life.Value;
                    if ((life == PlayerLifeState.Alive || life == PlayerLifeState.Knocked) && !IsInside(p.transform.position))
                        p.Health.ApplyDamage(dps, null, false, "Storm", DamageKind.Zone);
                }
            }
        }
    }
}
