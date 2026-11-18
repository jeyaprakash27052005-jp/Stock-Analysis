using Unity.Netcode;
using UnityEngine;

namespace Stormdrop
{
    /// <summary>
    /// The drop plane flies a straight line across the island. Its position is computed from
    /// shared server time, so it costs no bandwidth after spawn. At the end everyone left is ejected.
    /// </summary>
    public class PlaneController : NetworkBehaviour
    {
        public static PlaneController Instance { get; private set; }

        public float altitude = 600f;
        public float speed = 90f;          // m/s → ~60 s to cross 5.6 km
        public float jumpWindowStart = 0.06f;

        public readonly NetworkVariable<Vector3> Start = new NetworkVariable<Vector3>();
        public readonly NetworkVariable<Vector3> End = new NetworkVariable<Vector3>();
        public readonly NetworkVariable<double> StartTime = new NetworkVariable<double>();

        bool _finished;

        public float Progress
        {
            get
            {
                if (!IsSpawned) return 0;
                float len = Vector3.Distance(Start.Value, End.Value);
                return Mathf.Clamp01((float)(NetworkManager.ServerTime.Time - StartTime.Value) * speed / Mathf.Max(1, len));
            }
        }

        public Vector3 CurrentPosition => Vector3.Lerp(Start.Value, End.Value, Progress);
        public bool CanJump => Progress >= jumpWindowStart;
        public float SecondsLeft => (1f - Progress) * Vector3.Distance(Start.Value, End.Value) / speed;

        public override void OnNetworkSpawn() => Instance = this;
        public override void OnNetworkDespawn() { if (Instance == this) Instance = null; }

        public void ServerBeginFlight(int seed)
        {
            var rng = new DeterministicRandom(seed);
            float angle = rng.Range(0f, Mathf.PI * 2f);
            float half = GameConstants.MapSize * 0.7f;
            Vector3 dir = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
            Vector3 offset = Vector3.Cross(dir, Vector3.up) * rng.Range(-600f, 600f);
            Start.Value = -dir * half + offset + Vector3.up * altitude;
            End.Value = dir * half + offset + Vector3.up * altitude;
            StartTime.Value = NetworkManager.ServerTime.Time + 3;
        }

        void Update()
        {
            if (!IsSpawned) return;
            transform.position = CurrentPosition;
            if (End.Value != Start.Value) transform.rotation = Quaternion.LookRotation(End.Value - Start.Value);
            if (!IsServer || _finished || Progress < 1f) return;
            _finished = true;
            foreach (var p in PlayerIdentity.All)
                if (p.Health.Life.Value == PlayerLifeState.InPlane) p.Controller.ServerJumpFromPlane();
            MatchManager.Instance?.OnPlaneFinished();
            Invoke(nameof(Despawn), 5f);
        }

        void Despawn() { if (IsSpawned) NetworkObject.Despawn(true); }
    }
}
