using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Stormdrop
{
    /// <summary>Store: cosmetics/characters/pets for Gold or Diamonds, Diamond top-ups, and Fortune Drop (lucky spin, odds shown).</summary>
    public class StoreScreen : UIScreen
    {
        RectTransform _content;
        HashSet<string> _owned = new HashSet<string>();
        string _tab = "featured";
        const string SpinEvent = "fd_phoenix";

        protected override void Build()
        {
            Header("Store");
            var tabs = UIFactory.Rect(Root, "Tabs").Place(0, 1, 1, 1, 30, -200, 30, 120);
            UIFactory.HStack(tabs, 12);
            foreach (var (id, label) in new[] { ("featured", "Featured"), ("character", "Characters"), ("pet", "Pets"), ("weapon_skin", "Weapon Skins"),
                                                ("outfit", "Outfits"), ("emote", "Emotes"), ("parachute", "Parachutes"), ("vehicle_skin", "Vehicles"),
                                                ("diamonds", "Top Up ◆"), ("spin", "Fortune Drop") })
            {
                var tabId = id;
                UIFactory.Button(tabs, label, () => { _tab = tabId; Render(); }, UIFactory.PanelLight, 24);
            }
            _content = UIFactory.Rect(Root, "Content").Place(0, 0, 1, 1, 30, 30, 30, 215);
            PurchaseService.PurchaseCompleted += _ => UI.Toast("Diamonds added!");
            PurchaseService.PurchaseFailed += msg => UI.Toast(msg);
        }

        protected override void OnShow() => Run(async () =>
        {
            await Backend.LoadCatalog();
            var inv = await Backend.Inventory();
            _owned = new HashSet<string>(inv.items.Select(i => i.item_id));
            Render();
        });

        void Render()
        {
            UIFactory.Clear(_content);
            if (_tab == "diamonds") { RenderTopUp(); return; }
            if (_tab == "spin") { RenderSpin(); return; }
            var items = Backend.Catalog.items.Where(i => i.in_store && (i.price_gold != null || i.price_diamonds != null)
                                                         && (_tab == "featured" ? i.rarity == "legendary" || i.rarity == "epic" : i.type == _tab)).ToList();
            var grid = UIFactory.ScrollList(_content, out _);
            grid.parent.GetComponent<RectTransform>().Place(0, 0, 1, 1);
            for (int i = 0; i < items.Count; i += 4)
            {
                var row = UIFactory.Row(grid, 330, new Color(0, 0, 0, 0));
                UIFactory.HStack(row, 16);
                foreach (var item in items.Skip(i).Take(4)) Card(row, item);
                for (int k = items.Skip(i).Take(4).Count(); k < 4; k++) UIFactory.Rect(row, "Spacer");
            }
        }

        void Card(Transform parent, CatalogItem item)
        {
            var card = UIFactory.Image(parent, UIFactory.Panel, "Card").rectTransform;
            UIFactory.Image(card, UIFactory.RarityColor(item.rarity), "Rarity").rectTransform.Place(0, 1, 1, 1, 0, -10, 0, 0);
            UIFactory.Text(card, item.name, 30, UIFactory.TextMain, TextAnchor.UpperCenter, FontStyle.Bold).rectTransform.Place(0, 0.62f, 1, 1, 10, 0, 10, 20);
            UIFactory.Text(card, item.description, 20, UIFactory.TextDim, TextAnchor.UpperCenter).rectTransform.Place(0, 0.3f, 1, 0.66f, 12, 0, 12, 0);
            if (_owned.Contains(item.id) && item.type != "consumable")
            {
                UIFactory.Text(card, "OWNED", 28, UIFactory.Good, TextAnchor.MiddleCenter, FontStyle.Bold).rectTransform.Place(0, 0, 1, 0.28f);
                return;
            }
            var btns = UIFactory.Rect(card, "Prices").Place(0, 0, 1, 0.28f, 10, 10, 10, 0);
            UIFactory.HStack(btns, 8);
            if (item.price_gold != null) UIFactory.Button(btns, $"● {item.price_gold:N0}", () => Buy(item, "gold"), new Color(0.35f, 0.28f, 0.08f), 24);
            if (item.price_diamonds != null) UIFactory.Button(btns, $"◆ {item.price_diamonds:N0}", () => Buy(item, "diamonds"), new Color(0.1f, 0.25f, 0.4f), 24);
        }

        void Buy(CatalogItem item, string currency)
        {
            int price = (currency == "gold" ? item.price_gold : item.price_diamonds) ?? 0;
            UI.Confirm("Confirm purchase", $"Buy {item.name} for {price:N0} {(currency == "gold" ? "Gold" : "Diamonds")}?", "Buy",
                () => Run(async () =>
                {
                    await Backend.Buy(item.id, currency);
                    _owned.Add(item.id);
                    UI.Toast($"{item.name} added to your Vault");
                    AnalyticsService.Log("store_buy", "item", item.id);
                    Render();
                }));
        }

        void RenderTopUp()
        {
            var grid = UIFactory.Rect(_content, "Packs").Place(0.05f, 0.1f, 0.95f, 0.95f);
            int i = 0;
            foreach (var kv in Backend.Catalog.diamondPacks.OrderBy(k => k.Value))
            {
                int col = i % 3, row = i / 3;
                var id = kv.Key;
                var b = UIFactory.Button(grid, $"◆ {kv.Value:N0}\n\n<size=30>{PurchaseService.LocalizedPrice(id)}</size>", () => PurchaseService.Buy(id), new Color(0.1f, 0.25f, 0.42f), 52);
                b.GetComponent<RectTransform>().Place(col / 3f, row == 0 ? 0.52f : 0f, (col + 1) / 3f, row == 0 ? 1f : 0.48f, 12, 12, 12, 12);
                i++;
            }
            UIFactory.Text(_content, "Purchases are processed by Google Play. Diamonds are a virtual currency with no cash value.", 22, UIFactory.TextDim, TextAnchor.MiddleCenter)
                .rectTransform.Place(0, 0, 1, 0.08f);
        }

        void RenderSpin()
        {
            UIFactory.Text(_content, "Phoenix Fortune Drop", 54, UIFactory.Gold, TextAnchor.MiddleCenter, FontStyle.Bold).rectTransform.Place(0, 0.75f, 1, 0.95f);
            UIFactory.Text(_content, "Grand prize: Phoenix Wings parachute. Duplicates convert to 1,000 Gold.", 28, UIFactory.TextDim, TextAnchor.MiddleCenter).rectTransform.Place(0, 0.65f, 1, 0.75f);
            UIFactory.Button(_content, "Spin ×1\n◆ 20", () => Spin(1), UIFactory.Accent, 40).GetComponent<RectTransform>().Place(0.2f, 0.3f, 0.45f, 0.58f);
            UIFactory.Button(_content, "Spin ×10\n◆ 180", () => Spin(10), UIFactory.Accent, 40).GetComponent<RectTransform>().Place(0.55f, 0.3f, 0.8f, 0.58f);
            UIFactory.Button(_content, "View odds", ShowOdds, UIFactory.PanelLight, 28).GetComponent<RectTransform>().Place(0.4f, 0.12f, 0.6f, 0.24f);
        }

        void Spin(int count) => Run(async () =>
        {
            var r = await Backend.Spin(SpinEvent, count);
            await Backend.RefreshProfile();
            var names = r.wins.Select(w => (Backend.Item(w.itemId)?.name ?? w.itemId) + (w.duplicate ? " (dup → 1,000 Gold)" : ""));
            UI.Popup("You got", c => UIFactory.Text(c, string.Join("\n", names), 32, UIFactory.TextMain, TextAnchor.MiddleCenter).rectTransform.Place(0, 0, 1, 1), 1000, 800);
        });

        void ShowOdds() => Run(async () =>
        {
            var r = await Backend.SpinOdds(SpinEvent);
            UI.Popup("Fortune Drop odds", c =>
            {
                var lines = r.odds.OrderBy(o => o.percent).Select(o => $"{o.name}  ×{o.quantity}   —   {o.percent:0.##}%");
                UIFactory.Text(c, string.Join("\n", lines), 30, UIFactory.TextMain, TextAnchor.MiddleCenter).rectTransform.Place(0, 0, 1, 1);
            }, 1000, 760);
        });
    }
}
