using System.Threading.Tasks;
using UnityEngine;

namespace Stormdrop
{
    public class SplashScreen : UIScreen
    {
        protected override void Build()
        {
            UIFactory.Text(Root, GameConstants.GameName.ToUpper(), 110, UIFactory.Accent, TextAnchor.MiddleCenter, FontStyle.Bold).rectTransform.Place(0, 0.45f, 1, 0.75f);
            UIFactory.Text(Root, "Drop in. Loot up. Outlast the storm.", 36, UIFactory.TextDim, TextAnchor.MiddleCenter).rectTransform.Place(0, 0.35f, 1, 0.45f);
            UIFactory.Text(Root, "v" + GameConstants.Version, 24, UIFactory.TextDim, TextAnchor.LowerRight).rectTransform.Place(0, 0, 1, 0, 0, 20, 30, -60);
        }

        protected override async void OnShow()
        {
            await Task.Delay(1200);
            bool resumed = false;
            try { resumed = await AuthService.TryResume(); } catch (System.Exception e) { UI.Toast(e.Message); }
            if (resumed) UI.Show<LobbyScreen>(clearStack: true);
            else UI.Show<LoginScreen>(clearStack: true);
        }
    }
}
