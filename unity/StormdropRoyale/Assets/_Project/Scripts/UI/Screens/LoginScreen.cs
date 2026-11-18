using UnityEngine;

namespace Stormdrop
{
    public class LoginScreen : UIScreen
    {
        protected override void Build()
        {
            UIFactory.Text(Root, GameConstants.GameName.ToUpper(), 90, UIFactory.Accent, TextAnchor.MiddleCenter, FontStyle.Bold).rectTransform.Place(0, 0.68f, 1, 0.9f);
            var col = UIFactory.Rect(Root, "Buttons").At(0.5f, 0.42f, 0, 0, 700, 420);
            UIFactory.Button(col, "Continue with Google", () => Login(AuthService.SignInGoogle), new Color(0.26f, 0.52f, 0.96f), 34).GetComponent<RectTransform>().Place(0, 0.68f, 1, 1);
            UIFactory.Button(col, "Continue with Facebook", () => Login(AuthService.SignInFacebook), new Color(0.23f, 0.35f, 0.6f), 34).GetComponent<RectTransform>().Place(0, 0.34f, 1, 0.64f);
            UIFactory.Button(col, "Play as Guest", () => Login(AuthService.SignInGuest), UIFactory.PanelLight, 34).GetComponent<RectTransform>().Place(0, 0, 1, 0.3f);

            UIFactory.Text(Root, "Guest progress is saved to this device. Accounts not used for 30 days (e.g. after uninstalling) are deleted.\n" +
                                 "By continuing you agree to the Terms of Service and Privacy Policy. Ages 13+.",
                           24, UIFactory.TextDim, TextAnchor.MiddleCenter).rectTransform.Place(0.1f, 0.08f, 0.9f, 0.2f);
            UIFactory.Button(Root, "Privacy Policy", () => Application.OpenURL(LegalUrl("privacy")), new Color(0, 0, 0, 0), 26).GetComponent<RectTransform>().Place(0.3f, 0.02f, 0.48f, 0.08f);
            UIFactory.Button(Root, "Terms of Service", () => Application.OpenURL(LegalUrl("terms")), new Color(0, 0, 0, 0), 26).GetComponent<RectTransform>().Place(0.52f, 0.02f, 0.7f, 0.08f);
        }

        public static string LegalUrl(string page) => ApiClient.BaseUrl.Replace("/api", "") + "/" + page;

        void Login(System.Func<System.Threading.Tasks.Task> method) =>
            Run(async () => { await method(); UI.Show<LobbyScreen>(clearStack: true); });
    }
}
