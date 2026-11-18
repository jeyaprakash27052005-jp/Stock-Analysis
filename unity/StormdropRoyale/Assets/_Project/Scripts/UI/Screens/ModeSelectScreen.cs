using UnityEngine;

namespace Stormdrop
{
    public class ModeSelectScreen : UIScreen
    {
        public static string ModeName(GameMode m) => m switch
        {
            GameMode.BrSolo => "Battle Royale – Solo",
            GameMode.BrDuo => "Battle Royale – Duo",
            GameMode.BrSquad => "Battle Royale – Squad",
            GameMode.ClashSquad => "Clash Squad 4v4",
            GameMode.TeamDeathmatch => "Team Deathmatch",
            _ => "Training Ground",
        };

        static string Describe(GameMode m) => m switch
        {
            GameMode.ClashSquad => "Rounds of 4v4. First team to win 4 rounds.",
            GameMode.TeamDeathmatch => "Respawn fights. First team to 30 eliminations.",
            GameMode.Training => "Practice weapons and movement offline.",
            _ => "50 players, 4 km island, shrinking storm. Last one standing wins.",
        };

        protected override void Build()
        {
            Header("Select Mode");
            var grid = UIFactory.Rect(Root, "Modes").Place(0.08f, 0.2f, 0.92f, 0.85f);
            var modes = new[] { GameMode.BrSolo, GameMode.BrDuo, GameMode.BrSquad, GameMode.ClashSquad, GameMode.TeamDeathmatch, GameMode.Training };
            for (int i = 0; i < modes.Length; i++)
            {
                var m = modes[i];
                int col = i % 3, row = i / 3;
                var b = UIFactory.Button(grid, ModeName(m) + "\n\n<size=24>" + Describe(m) + "</size>", () => { UI.SelectedMode = m; UI.Back(); },
                    GameConstants.IsBattleRoyale(m) ? new Color(0.25f, 0.3f, 0.45f) : UIFactory.PanelLight, 34);
                b.GetComponent<RectTransform>().Place(col / 3f, row == 0 ? 0.52f : 0f, (col + 1) / 3f, row == 0 ? 1f : 0.48f, 10, 10, 10, 10);
            }
            var ranked = UIFactory.Button(Root, "", null, UIFactory.PanelLight, 32);
            ranked.GetComponent<RectTransform>().Place(0.35f, 0.05f, 0.65f, 0.14f);
            void Label() => ranked.SetLabel(UI.Ranked ? "Ranked: ON  (level 6+)" : "Ranked: OFF  (casual)");
            ranked.onClick.AddListener(() => { UI.Ranked = !UI.Ranked; Label(); });
            Label();
        }
    }
}
