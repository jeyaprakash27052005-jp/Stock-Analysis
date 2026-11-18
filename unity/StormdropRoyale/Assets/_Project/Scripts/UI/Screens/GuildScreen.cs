using UnityEngine;
using UnityEngine.UI;

namespace Stormdrop
{
    /// <summary>Guild (clan): view members and chat, or search / create / join one.</summary>
    public class GuildScreen : UIScreen
    {
        RectTransform _body;

        protected override void Build()
        {
            Header("Guild");
            _body = UIFactory.Rect(Root, "Body").Place(0, 0, 1, 1, 40, 30, 40, 130);
        }

        protected override void OnShow() => Run(async () =>
        {
            var mine = await Backend.MyGuild();
            UIFactory.Clear(_body);
            if (mine.guild != null) RenderMine(mine); else await RenderBrowse("");
        });

        void RenderMine(MyGuildResponse g)
        {
            UIFactory.Text(_body, $"<b>[{g.guild.tag}] {g.guild.name}</b>   Lv {g.guild.level}   ·   Glory {g.guild.glory:N0}\n<size=24>{g.guild.description}</size>", 38)
                .rectTransform.Place(0, 0.85f, 0.75f, 1);
            UIFactory.Button(_body, "Leave guild", () => UI.Confirm("Leave guild", "Are you sure?", "Leave", () => Run(async () => { await Backend.LeaveGuild(); await Backend.RefreshProfile(); OnShow(); })), UIFactory.Danger, 26)
                .GetComponent<RectTransform>().Place(0.8f, 0.88f, 1, 0.98f);

            var members = UIFactory.ScrollList(_body, out _, 4);
            members.parent.GetComponent<RectTransform>().Place(0, 0, 0.45f, 0.82f);
            foreach (var m in g.members)
                UIFactory.Text(UIFactory.Row(members, 70), $"{(m.role == "leader" ? "♛ " : m.role == "officer" ? "★ " : "")}{m.nickname}   Lv{m.level}", 28).rectTransform.Place(0, 0, 1, 1, 16, 0, 0, 0);

            var chat = UIFactory.ScrollList(_body, out var scroll, 4);
            chat.parent.GetComponent<RectTransform>().Place(0.47f, 0.12f, 1, 0.82f);
            foreach (var c in g.chat)
                UIFactory.Text(UIFactory.Row(chat, 60, new Color(0, 0, 0, 0)), $"<color=#FF9A40>{c.nickname}:</color> {c.body}", 26).rectTransform.Place(0, 0, 1, 1, 10, 0, 10, 0);
            Canvas.ForceUpdateCanvases();
            scroll.verticalNormalizedPosition = 0;
            var input = UIFactory.Input(_body, "Message your guild");
            input.characterLimit = 200;
            input.GetComponent<RectTransform>().Place(0.47f, 0, 0.85f, 0.1f);
            UIFactory.Button(_body, "Send", () => Run(async () => { await Backend.SendChat("guild", input.text); OnShow(); }), UIFactory.Accent, 28)
                .GetComponent<RectTransform>().Place(0.86f, 0, 1, 0.1f);
        }

        async System.Threading.Tasks.Task RenderBrowse(string search)
        {
            UIFactory.Clear(_body);
            var r = await Backend.SearchGuilds(search);
            var input = UIFactory.Input(_body, "Search guilds");
            input.text = search;
            input.GetComponent<RectTransform>().Place(0, 0.9f, 0.5f, 1);
            UIFactory.Button(_body, "Search", () => Run(() => RenderBrowse(input.text)), UIFactory.PanelLight, 28).GetComponent<RectTransform>().Place(0.52f, 0.9f, 0.66f, 1);
            UIFactory.Button(_body, "Create guild (2,000 Gold)", CreateGuild, UIFactory.Accent, 28).GetComponent<RectTransform>().Place(0.7f, 0.9f, 1, 1);
            var list = UIFactory.ScrollList(_body, out _, 6);
            list.parent.GetComponent<RectTransform>().Place(0, 0, 1, 0.87f);
            foreach (var g in r.guilds)
            {
                var row = UIFactory.Row(list, 100);
                UIFactory.Text(row, $"<b>[{g.tag}] {g.name}</b>  Lv{g.level}  ·  {g.members}/{g.max_members} members  ·  Glory {g.glory:N0}\n<size=22>{g.description}</size>", 28)
                    .rectTransform.Place(0, 0, 0.8f, 1, 20, 0, 0, 0);
                var id = g.id;
                UIFactory.Button(row, "Join", () => Run(async () => { await Backend.JoinGuild(id); await Backend.RefreshProfile(); OnShow(); }), UIFactory.Accent, 26)
                    .GetComponent<RectTransform>().Place(0.82f, 0.15f, 0.98f, 0.85f);
            }
        }

        void CreateGuild()
        {
            System.Action close = null;
            close = UI.Popup("Create guild", c =>
            {
                var name = UIFactory.Input(c, "Guild name (3–20)");
                name.GetComponent<RectTransform>().Place(0, 0.72f, 1, 0.9f);
                var tag = UIFactory.Input(c, "Tag (2–5 letters)");
                tag.characterLimit = 5;
                tag.GetComponent<RectTransform>().Place(0, 0.5f, 0.4f, 0.68f);
                var desc = UIFactory.Input(c, "Description");
                desc.GetComponent<RectTransform>().Place(0, 0.28f, 1, 0.46f);
                UIFactory.Button(c, "Create", () => Run(async () =>
                {
                    await Backend.CreateGuild(name.text, tag.text, desc.text);
                    await Backend.RefreshProfile();
                    close();
                    OnShow();
                }), UIFactory.Accent, 32).GetComponent<RectTransform>().Place(0.3f, 0.02f, 0.7f, 0.2f);
            }, 1000, 650);
        }
    }
}
