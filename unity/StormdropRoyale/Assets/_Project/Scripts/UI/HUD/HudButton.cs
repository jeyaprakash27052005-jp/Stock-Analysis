using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Stormdrop
{
    /// <summary>
    /// Touch button for the match HUD. Supports tap, hold, and (for fire buttons) dragging to aim
    /// while shooting. In HUD edit mode it can be dragged to a new position instead.
    /// </summary>
    public class HudButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
    {
        public string layoutId;
        public bool dragLooks;
        public Action onDown, onUp;
        public Image image;
        public Text label;
        public Image cooldownFill;

        public void OnPointerDown(PointerEventData e)
        {
            if (HudLayoutEditor.Editing) return;
            image.color = new Color(image.color.r, image.color.g, image.color.b, 0.95f);
            onDown?.Invoke();
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (HudLayoutEditor.Editing) { HudLayoutEditor.Save(); return; }
            image.color = new Color(image.color.r, image.color.g, image.color.b, 0.55f);
            onUp?.Invoke();
        }

        public void OnDrag(PointerEventData e)
        {
            var rt = (RectTransform)transform;
            if (HudLayoutEditor.Editing)
            {
                rt.anchoredPosition += e.delta / GetComponentInParent<Canvas>().scaleFactor;
                return;
            }
            if (dragLooks && PlayerInputHandler.Instance != null) PlayerInputHandler.Instance.touchLookDelta += e.delta;
        }

        public void SetVisible(bool v) { if (gameObject.activeSelf != v) gameObject.SetActive(v); }
    }
}
