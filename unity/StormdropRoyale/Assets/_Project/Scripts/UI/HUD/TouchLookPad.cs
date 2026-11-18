using UnityEngine;
using UnityEngine.EventSystems;

namespace Stormdrop
{
    /// <summary>Right side of the screen: drag to look. Each finger is tracked separately by the EventSystem.</summary>
    public class TouchLookPad : MonoBehaviour, IDragHandler
    {
        public void OnDrag(PointerEventData e)
        {
            if (HudLayoutEditor.Editing) return;
            var input = PlayerInputHandler.Instance;
            if (input != null) input.touchLookDelta += e.delta;
        }
    }
}
