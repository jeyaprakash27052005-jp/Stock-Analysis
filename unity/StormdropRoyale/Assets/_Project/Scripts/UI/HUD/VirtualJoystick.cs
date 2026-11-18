using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Stormdrop
{
    /// <summary>Floating left-thumb joystick. Pushing it to the top edge locks sprint.</summary>
    public class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public RectTransform knob;
        public RectTransform ring;
        public float radius = 110f;
        public Image sprintLockIcon;
        Vector2 _origin;
        bool _sprintLock;

        public void OnPointerDown(PointerEventData e)
        {
            if (HudLayoutEditor.Editing) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform, e.position, e.pressEventCamera, out _origin);
            ring.anchoredPosition = _origin;
            knob.anchoredPosition = _origin;
            OnDrag(e);
        }

        public void OnDrag(PointerEventData e)
        {
            if (HudLayoutEditor.Editing) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform, e.position, e.pressEventCamera, out var p);
            Vector2 d = Vector2.ClampMagnitude(p - _origin, radius * 1.4f);
            knob.anchoredPosition = _origin + Vector2.ClampMagnitude(d, radius);
            Vector2 v = d / radius;
            if (v.y > 1.25f) _sprintLock = true;
            else if (v.y < 0.6f) _sprintLock = false;
            var input = PlayerInputHandler.Instance;
            if (input == null) return;
            input.touchMove = Vector2.ClampMagnitude(v, 1f);
            input.touchSprintHeld = _sprintLock || v.magnitude > 0.98f && v.y > 0.7f;
            if (sprintLockIcon) sprintLockIcon.enabled = _sprintLock;
        }

        public void OnPointerUp(PointerEventData e)
        {
            knob.anchoredPosition = ring.anchoredPosition;
            var input = PlayerInputHandler.Instance;
            if (input == null) return;
            if (_sprintLock) { input.touchMove = Vector2.up; input.touchSprintHeld = true; return; }   // keep running hands-free
            input.touchMove = Vector2.zero;
            input.touchSprintHeld = false;
        }

        public void CancelSprintLock()
        {
            _sprintLock = false;
            if (sprintLockIcon) sprintLockIcon.enabled = false;
            var input = PlayerInputHandler.Instance;
            if (input != null) { input.touchMove = Vector2.zero; input.touchSprintHeld = false; }
        }
    }
}
