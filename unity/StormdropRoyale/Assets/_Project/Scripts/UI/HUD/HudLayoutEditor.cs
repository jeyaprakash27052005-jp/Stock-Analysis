using System;
using System.Collections.Generic;
using UnityEngine;

namespace Stormdrop
{
    /// <summary>Lets players drag HUD buttons anywhere; positions are saved in settings (and synced to the backend).</summary>
    public static class HudLayoutEditor
    {
        public static bool Editing { get; private set; }
        static Action _onClose;

        [Serializable] class Entry { public string id; public float x, y; }
        [Serializable] class Layout { public List<Entry> buttons = new List<Entry>(); }

        public static void Open(Action onClose)
        {
            _onClose = onClose;
            Editing = true;
            var hud = HUDController.Instance != null ? HUDController.Instance : HUDController.Create();
            hud.gameObject.SetActive(true);
            hud.ShowEditorBar(true);
        }

        public static void Close()
        {
            Editing = false;
            Save();
            if (HUDController.Instance != null)
            {
                HUDController.Instance.ShowEditorBar(false);
                if (PlayerIdentity.Local == null) HUDController.Instance.gameObject.SetActive(false);
            }
            _onClose?.Invoke();
        }

        public static void Save()
        {
            if (HUDController.Instance == null) return;
            var layout = new Layout();
            foreach (var b in HUDController.Instance.Buttons)
            {
                var p = ((RectTransform)b.transform).anchoredPosition;
                layout.buttons.Add(new Entry { id = b.layoutId, x = p.x, y = p.y });
            }
            GameSettings.Data.hudLayoutJson = JsonUtility.ToJson(layout);
            GameSettings.Save();
            if (Backend.Me != null) _ = Backend.SaveSettings(GameSettings.Data);
        }

        public static void Apply(IEnumerable<HudButton> buttons)
        {
            var json = GameSettings.Data.hudLayoutJson;
            if (string.IsNullOrEmpty(json)) return;
            var layout = JsonUtility.FromJson<Layout>(json);
            foreach (var b in buttons)
            {
                var e = layout.buttons.Find(x => x.id == b.layoutId);
                if (e != null) ((RectTransform)b.transform).anchoredPosition = new Vector2(e.x, e.y);
            }
        }

        public static void ResetLayout()
        {
            GameSettings.Data.hudLayoutJson = "";
            GameSettings.Save();
            HUDController.Instance?.ResetButtonPositions();
        }
    }
}
