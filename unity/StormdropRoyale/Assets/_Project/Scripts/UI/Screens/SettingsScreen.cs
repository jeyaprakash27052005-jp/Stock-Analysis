using UnityEngine;
using UnityEngine.UI;

namespace Stormdrop
{
    /// <summary>Graphics presets, controls, audio, HUD editor, account linking, legal links and account deletion.</summary>
    public class SettingsScreen : UIScreen
    {
        RectTransform _list;

        protected override void Build()
        {
            Header("Settings");
            _list = UIFactory.ScrollList(Root, out _, 8);
            _list.parent.GetComponent<RectTransform>().Place(0.1f, 0, 0.9f, 1, 0, 30, 0, 130);
        }

        protected override void OnShow()
        {
            UIFactory.Clear(_list);
            var d = GameSettings.Data;

            Section("GRAPHICS");
            var gRow = Row("Quality");
            foreach (GraphicsPreset p in System.Enum.GetValues(typeof(GraphicsPreset)))
            {
                var preset = p;
                UIFactory.Button(gRow, p.ToString(), () => { d.graphics = preset; Save(); OnShow(); }, d.graphics == p ? UIFactory.Accent : UIFactory.PanelLight, 26);
            }
            Toggle("High frame rate (60 FPS)", d.highFrameRate, v => d.highFrameRate = v);

            Section("CONTROLS");
            SliderRow("Look sensitivity", 0.2f, 3f, d.lookSensitivity, v => d.lookSensitivity = v);
            SliderRow("Scope sensitivity", 0.1f, 2f, d.scopeSensitivity, v => d.scopeSensitivity = v);
            Toggle("Gyroscope aim", d.gyroscopeAim, v => d.gyroscopeAim = v);
            SliderRow("Gyroscope sensitivity", 0.2f, 3f, d.gyroSensitivity, v => d.gyroSensitivity = v);
            Toggle("Auto fire when aiming at enemy", d.autoFire, v => d.autoFire = v);
            Toggle("Aim assist", d.aimAssist, v => d.aimAssist = v);
            var hud = Row("HUD layout");
            UIFactory.Button(hud, "Customize HUD", () => { UI.HideAllMenus(); HudLayoutEditor.Open(() => UI.Show<SettingsScreen>()); }, UIFactory.PanelLight, 26);

            Section("AUDIO & CHAT");
            SliderRow("Master volume", 0, 1, d.masterVolume, v => d.masterVolume = v);
            Toggle("Voice chat", d.voiceChat, v => d.voiceChat = v);

            Section("ACCOUNT");
            var me = Backend.Me;
            if (me != null)
            {
                var region = Row($"Region: {me.region}");
                foreach (var r in new[] { "IND", "SEA", "ME", "EU", "NA", "SA" })
                {
                    var rr = r;
                    UIFactory.Button(region, r, () => Run(async () => { await ApiClient.Patch<Profile>("/me", new { region = rr }); await Backend.RefreshProfile(); OnShow(); }),
                        me.region == r ? UIFactory.Accent : UIFactory.PanelLight, 22);
                }
                var link = Row("Linked: " + (me.linked != null ? string.Join(", ", me.linked) : "guest"));
                if (me.linked == null || !me.linked.Contains("google"))
                    UIFactory.Button(link, "Link Google", () => Run(async () => { await AuthService.Link("google"); OnShow(); }), new Color(0.26f, 0.52f, 0.96f), 24);
                if (me.linked == null || !me.linked.Contains("facebook"))
                    UIFactory.Button(link, "Link Facebook", () => Run(async () => { await AuthService.Link("facebook"); OnShow(); }), new Color(0.23f, 0.35f, 0.6f), 24);
                var rename = Row("Nickname: " + me.nickname);
                UIFactory.Button(rename, "Change (Name Card or ◆99)", Rename, UIFactory.PanelLight, 22);
            }
            var legal = Row("Legal");
            UIFactory.Button(legal, "Privacy Policy", () => Application.OpenURL(LoginScreen.LegalUrl("privacy")), UIFactory.PanelLight, 24);
            UIFactory.Button(legal, "Terms of Service", () => Application.OpenURL(LoginScreen.LegalUrl("terms")), UIFactory.PanelLight, 24);
            var acct = Row("Session");
            UIFactory.Button(acct, "Sign out", () => { AuthService.SignOut(); UI.Show<LoginScreen>(clearStack: true); }, UIFactory.PanelLight, 24);
            UIFactory.Button(acct, "Delete account", ConfirmDelete, UIFactory.Danger, 24);
            UIFactory.Text(UIFactory.Row(_list, 60, new Color(0, 0, 0, 0)), $"{GameConstants.GameName} v{GameConstants.Version}", 22, UIFactory.TextDim, TextAnchor.MiddleCenter).rectTransform.Place(0, 0, 1, 1);
        }

        protected override void OnHide() => Save();

        void Save()
        {
            GameSettings.Save();
            if (Backend.Me != null) _ = Backend.SaveSettings(GameSettings.Data);
        }

        void Section(string title) =>
            UIFactory.Text(UIFactory.Row(_list, 70, new Color(0, 0, 0, 0)), title, 30, UIFactory.Accent, TextAnchor.LowerLeft, FontStyle.Bold).rectTransform.Place(0, 0, 1, 1, 10, 0, 0, 0);

        RectTransform Row(string label)
        {
            var row = UIFactory.Row(_list, 90);
            UIFactory.Text(row, label, 28).rectTransform.Place(0, 0, 0.4f, 1, 20, 0, 0, 0);
            var right = UIFactory.Rect(row, "Controls").Place(0.4f, 0, 1, 1, 0, 12, 12, 12);
            UIFactory.HStack(right, 10);
            return right;
        }

        void Toggle(string label, bool value, System.Action<bool> set)
        {
            var r = Row(label);
            var b = UIFactory.Button(r, value ? "ON" : "OFF", null, value ? UIFactory.Good : UIFactory.PanelLight, 26);
            b.onClick.AddListener(() => { set(!value); Save(); OnShow(); });
        }

        void SliderRow(string label, float min, float max, float value, System.Action<float> set)
        {
            var r = Row($"{label}");
            UIFactory.Slider(r, min, max, value, v => set(v));
        }

        void Rename()
        {
            System.Action close = null;
            close = UI.Popup("Change nickname", c =>
            {
                var f = UIFactory.Input(c, "New nickname (3–16)");
                f.characterLimit = 16;
                f.GetComponent<RectTransform>().Place(0, 0.5f, 1, 0.8f);
                UIFactory.Button(c, "Save", () => Run(async () => { await Backend.Rename(f.text); close(); OnShow(); }), UIFactory.Accent)
                    .GetComponent<RectTransform>().Place(0.3f, 0.1f, 0.7f, 0.35f);
            }, 900, 450);
        }

        /// <summary>Google Play requires in-app account deletion for apps that let users create accounts.</summary>
        void ConfirmDelete() =>
            UI.Confirm("Delete account", "This permanently deletes your account, items, Diamonds and progress. This cannot be undone.", "Delete forever",
                () => Run(async () =>
                {
                    await Backend.DeleteAccount();
                    AuthService.SignOut();
                    PlayerPrefs.DeleteAll();
                    UI.Show<LoginScreen>(clearStack: true);
                    UI.Toast("Your account has been deleted.");
                }));
    }
}
