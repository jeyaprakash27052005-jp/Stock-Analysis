using UnityEngine;

namespace Stormdrop
{
    /// <summary>Base class for menu screens. Build() runs once; OnShow() each time the screen opens.</summary>
    public abstract class UIScreen : MonoBehaviour
    {
        public RectTransform Root { get; private set; }
        protected UIManager UI => UIManager.Instance;
        bool _built;

        public void Init(RectTransform root) { Root = root; }

        public void Show()
        {
            if (!_built) { _built = true; Build(); }
            Root.gameObject.SetActive(true);
            OnShow();
        }

        public void Hide()
        {
            if (Root.gameObject.activeSelf) OnHide();
            Root.gameObject.SetActive(false);
        }

        protected abstract void Build();
        protected virtual void OnShow() { }
        protected virtual void OnHide() { }

        /// <summary>Runs an async backend call with a spinner and shows errors as a toast.</summary>
        protected async void Run(System.Func<System.Threading.Tasks.Task> action, bool spinner = true)
        {
            if (spinner) UI.SetLoading(true);
            try { await action(); }
            catch (System.Exception e) { UI.Toast(e.Message); }
            finally { if (spinner) UI.SetLoading(false); }
        }

        /// <summary>Standard screen header with a back button and title.</summary>
        protected RectTransform Header(string title, bool back = true)
        {
            var bar = UIFactory.Image(Root, UIFactory.Panel, "Header").rectTransform.Place(0, 1, 1, 1, 0, -110, 0, 0);
            if (back) UIFactory.Button(bar, "◀ Back", () => UI.Back(), UIFactory.PanelLight).GetComponent<RectTransform>().Place(0, 0, 0, 1, 20, 15, -200, 15);
            UIFactory.Text(bar, title, 44, UIFactory.TextMain, TextAnchor.MiddleCenter, FontStyle.Bold).rectTransform.Place(0, 0, 1, 1);
            UI.CurrencyBar(bar);
            return bar;
        }
    }
}
