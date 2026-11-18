using UnityEngine;

namespace Stormdrop
{
    /// <summary>Third-person over-the-shoulder camera with scope zoom. Created for the local player only.</summary>
    public class PlayerCamera : MonoBehaviour
    {
        public static PlayerCamera Instance { get; private set; }
        public Camera Cam { get; private set; }

        public Transform target;
        public Vector3 shoulderOffset = new Vector3(0.6f, 1.65f, 0f);
        public float distance = 3.2f;
        public float baseFov = 65f;
        public float pitchMin = -70f, pitchMax = 75f;

        public float Yaw { get; set; }
        public float Pitch { get; set; }
        public float Zoom { get; set; } = 1f;      // 1 = hip, 2/4/8 = scopes
        public bool FirstPersonScope => Zoom >= 2f;

        float _currentFov, _currentDistance;

        public static PlayerCamera Create(Transform target)
        {
            if (Instance != null) { Instance.target = target; return Instance; }
            var cam = Camera.main;
            GameObject go;
            if (cam == null) { go = new GameObject("PlayerCamera"); cam = go.AddComponent<Camera>(); go.AddComponent<AudioListener>(); go.tag = "MainCamera"; }
            else go = cam.gameObject;
            var pc = go.AddComponent<PlayerCamera>();
            pc.Cam = cam;
            pc.target = target;
            pc.Yaw = target.eulerAngles.y;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 1200f;
            return pc;
        }

        void Awake() { Instance = this; Cam = GetComponent<Camera>(); _currentFov = baseFov; _currentDistance = distance; }
        void OnDestroy() { if (Instance == this) Instance = null; }

        public void AddLook(Vector2 delta)
        {
            float mult = Zoom > 1f ? GameSettings.Data.scopeSensitivity / Mathf.Sqrt(Zoom) : 1f;
            Yaw += delta.x * mult;
            Pitch = Mathf.Clamp(Pitch - delta.y * mult, pitchMin, pitchMax);
        }

        public void AddRecoil(float vertical, float horizontal)
        {
            Pitch = Mathf.Clamp(Pitch - vertical, pitchMin, pitchMax);
            Yaw += horizontal;
        }

        void LateUpdate()
        {
            if (target == null) return;
            var rot = Quaternion.Euler(Pitch, Yaw, 0);
            float wantDist = FirstPersonScope ? 0f : Zoom > 1f ? distance * 0.55f : distance;
            _currentDistance = Mathf.Lerp(_currentDistance, wantDist, Time.deltaTime * 12f);
            _currentFov = Mathf.Lerp(_currentFov, baseFov / Zoom, Time.deltaTime * 14f);
            Cam.fieldOfView = _currentFov;

            Vector3 pivot = target.position + Quaternion.Euler(0, Yaw, 0) * shoulderOffset;
            Vector3 wanted = pivot - rot * Vector3.forward * _currentDistance;
            // Keep the camera out of walls
            if (_currentDistance > 0.1f && Physics.SphereCast(pivot, 0.2f, wanted - pivot, out var hit, _currentDistance, GameConstants.EnvironmentMask))
                wanted = pivot + (wanted - pivot).normalized * (hit.distance - 0.1f);
            transform.SetPositionAndRotation(wanted, rot);
        }

        /// <summary>The ray bullets travel along: from the camera through the crosshair.</summary>
        public Ray AimRay() => new Ray(transform.position + transform.forward * (_currentDistance + 0.3f), transform.forward);
    }
}
