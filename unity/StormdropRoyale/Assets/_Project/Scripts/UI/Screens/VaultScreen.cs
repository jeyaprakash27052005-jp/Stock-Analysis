using System.Linq;
using UnityEngine;

namespace Stormdrop
{
    /// <summary>Owned items. Tap a character or pet to equip it.</summary>
    public class VaultScreen : UIScreen
    {
        RectTransform _list;

        protected override void Build()
        {
            Header("Vault");
            _list = UIFactory.ScrollList(Root, out _);
            _list.parent.GetComponent<RectTransform>().Place(0, 0, 1, 1, 40, 30, 40, 130);
        }

        protected override void OnShow() => Run(async () =>
        {
            await Backend.LoadCatalog();
            var inv = await Backend.Inventory();
            UIFactory.Clear(_list);
            foreach (var group in inv.items.GroupBy(i => i.type))
            {
                UIFactory.Text(UIFactory.Row(_list, 60, new Color(0, 0, 0, 0)), group.Key.Replace('_', ' ').ToUpper(), 30, UIFactory.Accent, TextAnchor.MiddleLeft, FontStyle.Bold)
                    .rectTransform.Place(0, 0, 1, 1, 10, 0, 0, 0);
                foreach (var it in group)
                {
                    var row = UIFactory.Row(_list, 90);
                    UIFactory.Image(row, UIFactory.RarityColor(it.rarity), "Rarity").rectTransform.Place(0, 0, 0, 1, 0, 0, -10, 0);
                    UIFactory.Text(row, it.name + (it.quantity > 1 ? $"  ×{it.quantity}" : ""), 30).rectTransform.Place(0, 0, 0.7f, 1, 30, 0, 0, 0);
                    bool equipped = it.item_id == Backend.Me.character_id || it.item_id == Backend.Me.pet_id;
                    if (it.type == "character" || it.type == "pet")
                    {
                        var id = it.item_id; var type = it.type;
                        if (equipped) UIFactory.Text(row, "EQUIPPED", 26, UIFactory.Good, TextAnchor.MiddleCenter).rectTransform.Place(0.75f, 0, 1, 1);
                        else UIFactory.Button(row, "Equip", () => Run(async () =>
                        {
                            if (type == "character") await Backend.SetLoadout(id, Backend.Me.pet_id);
                            else await Backend.SetLoadout(Backend.Me.character_id, id);
                            OnShow();
                        }), UIFactory.Accent, 26).GetComponent<RectTransform>().Place(0.78f, 0.15f, 0.98f, 0.85f);
                    }
                }
            }
        });
    }
}
