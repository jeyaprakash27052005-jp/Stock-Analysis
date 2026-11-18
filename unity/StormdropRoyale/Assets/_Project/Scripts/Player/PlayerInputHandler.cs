using UnityEngine;

namespace Stormdrop
{
    /// <summary>
    /// Collects input from the touch HUD (joystick, look pad, buttons, gyroscope) and, in the
    /// editor/PC, from keyboard and mouse. Gameplay scripts read only from here.
    /// Requires Player Settings ▸ Active Input Handling = "Both" (or "Input Manager (Old)").
    /// </summary>
    public class PlayerInputHandler : MonoBehaviour
    {
        public static PlayerInputHandler Instance { get; private set; }

        // Written by HUD controls
        [HideInInspector] public Vector2 touchMove;
        [HideInInspector] public Vector2 touchLookDelta;
        [HideInInspector] public bool touchFireHeld, touchSprintHeld;

        public Vector2 Move { get; private set; }
        public Vector2 Look { get; private set; }
        public bool FireHeld { get; private set; }
        public bool Sprint { get; private set; }

        // One-frame presses (consumed by the reader)
        bool _jump, _crouch, _prone, _reload, _aim, _interact, _skill, _switchNext, _parachute;
        public bool ConsumeJump() => Consume(ref _jump);
        public bool ConsumeCrouch() => Consume(ref _crouch);
        public bool ConsumeProne() => Consume(ref _prone);
        public bool ConsumeReload() => Consume(ref _reload);
        public bool ConsumeAimToggle() => Consume(ref _aim);
        public bool ConsumeInteract() => Consume(ref _interact);
        public bool ConsumeSkill() => Consume(ref _skill);
        public bool ConsumeSwitchWeapon() => Consume(ref _switchNext);
        public bool ConsumeParachute() => Consume(ref _parachute);

        public void PressJump() => _jump = true;
        public void PressCrouch() => _crouch = true;
        public void PressProne() => _prone = true;
        public void PressReload() => _reload = true;
        public void PressAim() => _aim = true;
        public void PressInteract() => _interact = true;
        public void PressSkill() => _skill = true;
        public void PressSwitchWeapon() => _switchNext = true;
        public void PressParachute() => _parachute = true;

        static bool Consume(ref bool b) { bool v = b; b = false; return v; }

        void Awake() => Instance = this;

        void Update()
        {
            var s = GameSettings.Data;
            Vector2 move = touchMove;
            Vector2 look = touchLookDelta * 0.12f * s.lookSensitivity;
            touchLookDelta = Vector2.zero;
            bool fire = touchFireHeld, sprint = touchSprintHeld;

#if ENABLE_LEGACY_INPUT_MANAGER
            if (s.gyroscopeAim && SystemInfo.supportsGyroscope)
            {
                Vector3 r = Input.gyro.rotationRateUnbiased;
                look += new Vector2(-r.y, r.x) * s.gyroSensitivity * 2f;
            }
#if UNITY_EDITOR || UNITY_STANDALONE
            var kb = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            if (kb.sqrMagnitude > 0) move = kb;
            if (Cursor.lockState == CursorLockMode.Locked)
                look += new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")) * 2f * s.lookSensitivity;
            fire |= Input.GetMouseButton(0) && Cursor.lockState == CursorLockMode.Locked;
            sprint |= Input.GetKey(KeyCode.LeftShift);
            if (Input.GetKeyDown(KeyCode.Space)) { _jump = true; _parachute = true; }
            if (Input.GetKeyDown(KeyCode.C)) _crouch = true;
            if (Input.GetKeyDown(KeyCode.Z)) _prone = true;
            if (Input.GetKeyDown(KeyCode.R)) _reload = true;
            if (Input.GetMouseButtonDown(1)) _aim = true;
            if (Input.GetKeyDown(KeyCode.F)) _interact = true;
            if (Input.GetKeyDown(KeyCode.Q)) _skill = true;
            if (Input.GetKeyDown(KeyCode.Tab)) _switchNext = true;
            if (Input.GetKeyDown(KeyCode.Escape)) Cursor.lockState = CursorLockMode.None;
            if (Input.GetMouseButtonDown(0) && (UnityEngine.EventSystems.EventSystem.current == null || !UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()))
                Cursor.lockState = CursorLockMode.Locked;
#endif
#endif
            Move = Vector2.ClampMagnitude(move, 1f);
            Look = look;
            FireHeld = fire;
            Sprint = sprint;
        }
    }
}
