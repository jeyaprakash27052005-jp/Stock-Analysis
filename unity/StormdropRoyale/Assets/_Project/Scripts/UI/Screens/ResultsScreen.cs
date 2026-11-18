using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Stormdrop
{
    /// <summary>End of match: placement, kills, damage, survival time, then XP / Gold / rank points from the backend.</summary>
    public class ResultsScreen : UIScreen
    {
        Text _title, _stats, _rewards;

        protected override void Build()
        {
            _title = UIFactory.Text(Root, "", 100, UIFactory.Accent, TextAnchor.MiddleCenter, FontStyle.Bold);
            _title.rectTransform.Place(0, 0.7f, 1, 0.92f);
            _stats = UIFactory.Text(Root, "", 40, UIFactory.TextMain, TextAnchor.MiddleCenter);
            _stats.rectTransform.Place(0.1f, 0.38f, 0.9f, 0.68f);
            _rewards = UIFactory.Text(Root, "", 36, UIFactory.Gold, TextAnchor.MiddleCenter);
            _rewards.rectTransform.Place(0.1f, 0.22f, 0.9f, 0.38f);
            UIFactory.Button(Root, "Back to Lobby", () => UI.LeaveMatchToLobby(), UIFactory.Accent, 40).GetComponent<RectTransform>().At(0.5f, 0.1f, 0, 0, 520, 110);
        }

        protected override void OnShow()
        {
            if (UI.LastResult == null) return;
            var r = UI.LastResult.Value;
            var mode = (GameMode)r.mode;
            bool br = GameConstants.IsBattleRoyale(mode);
            _title.text = r.won ? GameConstants.VictoryTitle : br ? $"#{r.placement} of {r.totalTeams}" : "DEFEAT";
            _title.color = r.won ? UIFactory.Gold : UIFactory.TextMain;
            var t = System.TimeSpan.FromSeconds(r.survivalSeconds);
            _stats.text = $"Eliminations  {r.kills}\nDamage  {r.damage}\nHeadshots  {r.headshots}\nRevives  {r.revives}\nSurvived  {t:mm\\:ss}";
            _rewards.text = "Calculating rewards…";
            if (mode != GameMode.Training) StartCoroutine(FetchRewards(r.matchId.ToString()));
            else _rewards.text = "Training — no rewards";
        }

        protected override void OnHide() => StopAllCoroutines();

        /// <summary>The server reports results when the whole match ends, so poll history for a while.</summary>
        IEnumerator FetchRewards(string matchId)
        {
            for (int i = 0; i < 40; i++)
            {
                var h = Backend.MatchHistory();
                yield return new WaitUntil(() => h.IsCompleted);
                var row = h.Status == System.Threading.Tasks.TaskStatus.RanToCompletion ? h.Result.matches.FirstOrDefault(m => m.id == matchId) : null;
                if (row != null)
                {
                    string rp = row.is_ranked ? $"   {(row.rank_delta >= 0 ? "+" : "")}{row.rank_delta} RP" : "";
                    _rewards.text = $"+{row.xp_gained} XP   +{row.gold_gained} Gold{rp}";
                    _ = Backend.RefreshProfile();
                    yield break;
                }
                _rewards.text = "Rewards arrive when the match ends…";
                yield return new WaitForSeconds(5f);
            }
        }
    }
}
