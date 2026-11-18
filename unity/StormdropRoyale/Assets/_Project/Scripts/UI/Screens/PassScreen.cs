using UnityEngine;
using UnityEngine.UI;

namespace Stormdrop
{
    /// <summary>Storm Pass: free + premium reward tracks, levelled by Pass XP from matches and missions.</summary>
    public class PassScreen : UIScreen
    {
        Text _info;
        RectTransform _list;
        Button _premium;

        protected override void Build()
        {
            Header("Storm Pass");
            _info = UIFactory.Text(Root, "", 34, UIFactory.TextMain, TextAnchor.MiddleLeft);
            _info.rectTransform.Place(0, 1, 0.7f, 1, 40, -230, 0, 125);
            _premium = UIFactory.Button(Root, "", null, UIFactory.Gold, 30);
            _premium.GetComponent<RectTransform>().Place(0.7f, 1, 1, 1, 0, -225, 40, 130);
            _premium.GetComponentInChildren<Text>().color = Color.black;
            _list = UIFactory.ScrollList(Root, out _, 6);
            _list.parent.GetComponent<RectTransform>().Place(0, 0, 1, 1, 40, 30, 40, 240);
        }

        protected override void OnShow() => Run(async () =>
        {
            var p = await Backend.Pass();
            UIFactory.Clear(_list);
            if (p.season == null) { _info.text = "No active season"; return; }
            _info.text = $"{p.season.name}\nLevel {p.level}/{p.season.max_level}  ·  {p.passXp % p.season.xp_per_level}/{p.season.xp_per_level} XP  ·  ends {p.season.ends_at?.Substring(0, 10)}";
            _premium.onClick.RemoveAllListeners();
            if (p.premium) { _premium.SetLabel("PREMIUM ACTIVE"); _premium.interactable = false; }
            else
            {
                _premium.SetLabel($"Unlock Premium  ◆ {p.season.premium_price_diamonds}");
                _premium.onClick.AddListener(() => UI.Confirm("Storm Pass Premium", $"Unlock the premium track for {p.season.premium_price_diamonds} Diamonds?", "Unlock",
                    () => Run(async () => { await Backend.BuyPremiumPass(); await Backend.RefreshProfile(); OnShow(); })));
            }

            for (int lvl = 1; lvl <= p.season.max_level; lvl++)
            {
                var row = UIFactory.Row(_list, 90, lvl <= p.level ? UIFactory.PanelLight : UIFactory.Panel);
                UIFactory.Text(row, $"Lv {lvl}", 30, UIFactory.TextMain, TextAnchor.MiddleCenter, FontStyle.Bold).rectTransform.Place(0, 0, 0.1f, 1);
                Cell(row, p, lvl, "free", 0.1f, 0.55f);
                Cell(row, p, lvl, "premium", 0.55f, 1f);
            }
        });

        void Cell(RectTransform row, PassResponse p, int lvl, string track, float x0, float x1)
        {
            var reward = p.rewards.Find(r => r.level == lvl && r.track == track);
            if (reward == null) return;
            var cell = UIFactory.Rect(row, track).Place(x0, 0, x1, 1, 10, 8, 10, 8);
            UIFactory.Text(cell, (track == "premium" ? "★ " : "") + reward.name, 26, UIFactory.RarityColor(reward.rarity)).rectTransform.Place(0, 0, 0.65f, 1, 10, 0, 0, 0);
            string key = $"{lvl}:{track}";
            if (p.claimed.Contains(key)) UIFactory.Text(cell, "✔", 32, UIFactory.Good, TextAnchor.MiddleCenter).rectTransform.Place(0.65f, 0, 1, 1);
            else if (lvl <= p.level && (track == "free" || p.premium))
                UIFactory.Button(cell, "Claim", () => Run(async () => { await Backend.ClaimPass(lvl, track); await Backend.RefreshProfile(); OnShow(); }), UIFactory.Accent, 24)
                    .GetComponent<RectTransform>().Place(0.67f, 0.1f, 1, 0.9f);
            else UIFactory.Text(cell, "Locked", 22, UIFactory.TextDim, TextAnchor.MiddleCenter).rectTransform.Place(0.65f, 0, 1, 1);
        }
    }
}
