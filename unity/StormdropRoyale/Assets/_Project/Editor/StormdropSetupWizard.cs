#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Stormdrop.EditorTools
{
    /// <summary>
    /// One-click project setup: layers, all game content (weapons, items, characters, pets, loot tables,
    /// zone), network prefabs, a playable 4 km test island with all six zone types, and the two scenes.
    /// Safe to run again — existing assets are updated in place.
    /// Menu: Stormdrop ▸ 1. Run Setup Wizard
    /// </summary>
    public static class StormdropSetupWizard
    {
        const string Root = "Assets/_Project";
        const string DataDir = Root + "/Data";
        const string PrefabDir = Root + "/Prefabs";
        const string SceneDir = Root + "/Scenes";
        const string ArtDir = Root + "/Art";

        static readonly Dictionary<string, ItemDefinition> Items = new Dictionary<string, ItemDefinition>();

        [MenuItem("Stormdrop/1. Run Setup Wizard", priority = 0)]
        public static void Run()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            foreach (var d in new[] { DataDir, DataDir + "/Items", DataDir + "/Weapons", DataDir + "/Characters", DataDir + "/Pets", DataDir + "/Loot",
                                      PrefabDir, SceneDir, ArtDir, Root + "/Resources" })
                Directory.CreateDirectory(d);

            Items.Clear();
            SetupLayers();
            EditorUtility.DisplayProgressBar("Stormdrop", "Creating items and weapons…", 0.1f);
            CreateItems();
            CreateWeapons();
            CreateAttachments();
            EditorUtility.DisplayProgressBar("Stormdrop", "Creating characters and pets…", 0.3f);
            var characters = CreateCharacters();
            var pets = CreatePets();
            var tables = CreateLootTables();
            var zone = Asset<ZoneConfig>(DataDir + "/ZoneConfig.asset");

            var db = Asset<GameDatabase>(Root + "/Resources/GameDatabase.asset");
            db.items = new List<ItemDefinition>(Items.Values).ToArray();
            db.characters = characters;
            db.pets = pets;
            db.lootTables = tables;
            db.zoneConfig = zone;
            db.clashSquadRoundWeapons = new[] { W("pt_viper"), W("smg_wasp"), W("sg_thunderclap"), W("ar_raptor7"), W("ar_halberd"), W("sr_falcon"), W("ar_raptor7") };
            db.trainingRifle = W("ar_raptor7");

            EditorUtility.DisplayProgressBar("Stormdrop", "Creating network prefabs…", 0.5f);
            db.playerPrefab = CreatePlayerPrefab();
            db.matchSystemsPrefab = CreateMatchSystemsPrefab();
            db.planePrefab = CreatePlanePrefab();
            db.airdropPrefab = CreateAirdropPrefab();
            db.grenadePrefab = CreateGrenadePrefab();
            db.foamWallPrefab = CreateFoamWallPrefab();
            db.vehiclePrefabs = new[]
            {
                CreateVehiclePrefab("Jeep", VehicleType.Jeep, new Vector3(2.2f, 1.4f, 4.2f), 4, 28f, new Color(0.35f, 0.45f, 0.25f)),
                CreateVehiclePrefab("Motorbike", VehicleType.Motorbike, new Vector3(0.8f, 1.1f, 2.2f), 2, 34f, new Color(0.15f, 0.15f, 0.18f)),
                CreateVehiclePrefab("Buggy", VehicleType.Buggy, new Vector3(1.8f, 1.1f, 3.2f), 2, 31f, new Color(0.85f, 0.55f, 0.15f)),
                CreateVehiclePrefab("Boat", VehicleType.Boat, new Vector3(2.4f, 1f, 5f), 4, 24f, new Color(0.9f, 0.9f, 0.95f)),
            };
            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();

            EditorUtility.DisplayProgressBar("Stormdrop", "Building the island (this takes a moment)…", 0.7f);
            CreateGameScene();
            CreateBootstrapScene(db);
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(SceneDir + "/" + GameConstants.BootstrapScene + ".unity", true),
                new EditorBuildSettingsScene(SceneDir + "/" + GameConstants.GameScene + ".unity", true),
            };
            EditorUtility.ClearProgressBar();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorSceneManager.OpenScene(SceneDir + "/" + GameConstants.BootstrapScene + ".unity");
            if (!Application.isBatchMode) EditorUtility.DisplayDialog("Stormdrop", "Setup complete!\n\n1. Select the GameBootstrap object and set your API URL.\n" +
                "2. Press Play, choose Training Ground and START to test offline.\n\nSee docs/SETUP.md for multiplayer and Play Store builds.", "OK");
        }

        // ------------------------------------------------------------------ helpers
        static T Asset<T>(string path) where T : ScriptableObject
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            if (a == null) { a = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(a, path); }
            EditorUtility.SetDirty(a);
            return a;
        }

        static WeaponData W(string id) => Items.TryGetValue(id, out var i) ? i as WeaponData : null;

        static Material Mat(string name, Color color)
        {
            string path = $"{ArtDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            m.color = color;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            EditorUtility.SetDirty(m);
            return m;
        }

        static void SetLayer(GameObject go, int layer)
        {
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
        }

        static GameObject Primitive(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 scale, Material mat, bool keepCollider)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            if (mat) go.GetComponent<Renderer>().sharedMaterial = mat;
            if (!keepCollider) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        static GameObject SavePrefab(GameObject go, string name)
        {
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{PrefabDir}/{name}.prefab");
            Object.DestroyImmediate(go);
            return prefab;
        }

        static void SetupLayers()
        {
            var tm = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tm.FindProperty("layers");
            layers.GetArrayElementAtIndex(GameConstants.LayerPlayer).stringValue = "Player";
            layers.GetArrayElementAtIndex(GameConstants.LayerVehicle).stringValue = "Vehicle";
            layers.GetArrayElementAtIndex(GameConstants.LayerLoot).stringValue = "Loot";
            tm.ApplyModifiedProperties();

            // Active Input Handling = Both (legacy Input is used for keyboard/mouse/gyro, UI works with either).
            var ps = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
            var input = ps.FindProperty("activeInputHandler");
            if (input != null && input.intValue != 2)
            {
                input.intValue = 2;
                ps.ApplyModifiedProperties();
                Debug.LogWarning("[Stormdrop] Active Input Handling set to 'Both'. Restart the Unity editor once for it to take effect.");
            }
        }

        // ------------------------------------------------------------------ content
        static ItemDefinition Item(string id, string name, ItemKind kind, ItemRarity rarity, int weight, int maxStack, int pickup,
                                   int level = 0, float amount = 0, float useSeconds = 0, AmmoType ammo = AmmoType.None, ThrowableType thr = ThrowableType.Frag)
        {
            var i = Asset<ItemDefinition>($"{DataDir}/Items/{id}.asset");
            i.id = id; i.displayName = name; i.kind = kind; i.rarity = rarity; i.weight = weight; i.maxStack = maxStack; i.pickupAmount = pickup;
            i.level = level; i.amount = amount; i.useSeconds = useSeconds; i.ammoType = ammo; i.throwable = thr;
            Items[id] = i;
            return i;
        }

        static void CreateItems()
        {
            Item("ammo_light", "Light Ammo", ItemKind.Ammo, ItemRarity.Common, 1, 120, 40, ammo: AmmoType.Light);
            Item("ammo_medium", "Medium Ammo", ItemKind.Ammo, ItemRarity.Common, 1, 120, 30, ammo: AmmoType.Medium);
            Item("ammo_heavy", "Heavy Ammo", ItemKind.Ammo, ItemRarity.Common, 1, 120, 30, ammo: AmmoType.Heavy);
            Item("ammo_shells", "Shotgun Shells", ItemKind.Ammo, ItemRarity.Common, 2, 40, 12, ammo: AmmoType.Shells);
            Item("ammo_sniper", "Sniper Ammo", ItemKind.Ammo, ItemRarity.Rare, 2, 40, 10, ammo: AmmoType.Sniper);
            Item("med_kit", "Med Kit", ItemKind.Medkit, ItemRarity.Common, 10, 6, 1, amount: 75, useSeconds: 5f);
            Item("vigor_shroom", "Vigor Shroom", ItemKind.VigorShroom, ItemRarity.Common, 5, 10, 1, amount: 50, useSeconds: 2.5f);
            Item("armor_repair", "Armor Repair Kit", ItemKind.ArmorRepairKit, ItemRarity.Rare, 10, 3, 1, amount: 60, useSeconds: 4f);
            for (int l = 1; l <= 4; l++)
            {
                var rarity = (ItemRarity)Mathf.Min(l - 1, 3);
                Item($"vest_{l}", $"Vest Lv{l}", ItemKind.ArmorVest, rarity, 0, 1, 1, level: l);
                Item($"helmet_{l}", $"Helmet Lv{l}", ItemKind.Helmet, rarity, 0, 1, 1, level: l);
            }
            for (int l = 1; l <= 3; l++) Item($"backpack_{l}", $"Backpack Lv{l}", ItemKind.Backpack, (ItemRarity)(l - 1), 0, 1, 1, level: l);
            Item("nade_frag", "Frag Grenade", ItemKind.Throwable, ItemRarity.Rare, 8, 5, 1, thr: ThrowableType.Frag);
            Item("nade_smoke", "Smoke Grenade", ItemKind.Throwable, ItemRarity.Common, 8, 5, 1, thr: ThrowableType.Smoke);
            Item("nade_flash", "Flash Grenade", ItemKind.Throwable, ItemRarity.Common, 8, 5, 1, thr: ThrowableType.Flash);
            Item("foam_wall", "Foam Wall", ItemKind.FoamWall, ItemRarity.Common, 6, 10, 2);
        }

        static WeaponData Weapon(string id, string name, WeaponCategory cat, ItemRarity rarity, AmmoType ammo, float dmg, float rpm, int mag, float reload,
                                 float range, bool auto, float hip, float ads, float recoil, int pellets = 1, float head = 1.8f, float move = 1f, float zoom = 1.3f)
        {
            var w = Asset<WeaponData>($"{DataDir}/Weapons/{id}.asset");
            w.id = id; w.displayName = name; w.kind = ItemKind.Weapon; w.rarity = rarity; w.weight = 0; w.maxStack = 1; w.pickupAmount = 1;
            w.category = cat; w.ammo = ammo; w.damage = dmg; w.roundsPerMinute = rpm; w.magazineSize = mag; w.reloadSeconds = reload;
            w.range = range; w.falloffStart = range * 0.35f; w.automatic = auto; w.hipSpread = hip; w.adsSpread = ads;
            w.recoilVertical = recoil; w.recoilHorizontal = recoil * 0.35f; w.pellets = pellets; w.headshotMultiplier = head;
            w.moveSpeedMultiplier = move; w.adsZoom = zoom;
            w.allowedSlots = cat switch
            {
                WeaponCategory.AssaultRifle => new[] { AttachmentSlot.Scope, AttachmentSlot.Muzzle, AttachmentSlot.Foregrip, AttachmentSlot.Magazine, AttachmentSlot.Stock },
                WeaponCategory.SMG => new[] { AttachmentSlot.Scope, AttachmentSlot.Muzzle, AttachmentSlot.Foregrip, AttachmentSlot.Magazine, AttachmentSlot.Stock },
                WeaponCategory.Shotgun => new[] { AttachmentSlot.Magazine, AttachmentSlot.Stock },
                WeaponCategory.Sniper => new[] { AttachmentSlot.Scope, AttachmentSlot.Muzzle, AttachmentSlot.Magazine },
                WeaponCategory.LMG => new[] { AttachmentSlot.Scope, AttachmentSlot.Muzzle, AttachmentSlot.Foregrip, AttachmentSlot.Magazine },
                WeaponCategory.Pistol => new[] { AttachmentSlot.Muzzle, AttachmentSlot.Magazine },
                _ => new AttachmentSlot[0],
            };
            Items[id] = w;
            return w;
        }

        static void CreateWeapons()
        {
            //       id                 name              category                     rarity            ammo              dmg  rpm  mag reload range auto  hip  ads  recoil
            Weapon("ar_raptor7",    "Raptor-7",        WeaponCategory.AssaultRifle, ItemRarity.Rare,   AmmoType.Medium, 38, 600, 30, 2.3f, 180, true, 3.2f, 0.7f, 1.1f);
            Weapon("ar_halberd",    "Halberd AR",      WeaponCategory.AssaultRifle, ItemRarity.Epic,   AmmoType.Medium, 45, 480, 30, 2.5f, 200, true, 3.0f, 0.6f, 1.4f);
            Weapon("smg_wasp",      "Wasp SMG",        WeaponCategory.SMG,          ItemRarity.Common, AmmoType.Light,  26, 900, 32, 1.8f, 70,  true, 3.5f, 1.4f, 0.6f, move: 1.05f);
            Weapon("smg_hornet",    "Hornet SMG",      WeaponCategory.SMG,          ItemRarity.Rare,   AmmoType.Light,  30, 780, 25, 1.7f, 80,  true, 3.2f, 1.2f, 0.7f, move: 1.05f);
            Weapon("sg_thunderclap","Thunderclap",     WeaponCategory.Shotgun,      ItemRarity.Rare,   AmmoType.Shells, 19, 75,  6,  3.0f, 25,  false, 5.5f, 4.5f, 3f, pellets: 8, head: 1.4f);
            Weapon("sg_boar",       "Boar Pump",       WeaponCategory.Shotgun,      ItemRarity.Common, AmmoType.Shells, 23, 140, 4,  2.6f, 22,  false, 6f, 5f, 3.5f, pellets: 6, head: 1.4f);
            Weapon("sr_longreach",  "Longreach",       WeaponCategory.Sniper,       ItemRarity.Legendary, AmmoType.Sniper, 170, 40, 5, 3.6f, 650, false, 8f, 0.05f, 4f, head: 2.4f, move: 0.92f, zoom: 2f);
            Weapon("sr_falcon",     "Falcon DMR",      WeaponCategory.Sniper,       ItemRarity.Epic,   AmmoType.Sniper, 72,  200, 10, 2.8f, 420, false, 5f, 0.2f, 2.2f, head: 2f, move: 0.95f, zoom: 1.8f);
            Weapon("lmg_juggernaut","Juggernaut LMG",  WeaponCategory.LMG,          ItemRarity.Epic,   AmmoType.Heavy,  40,  650, 80, 5f,   200, true, 4f, 1f, 1.3f, move: 0.88f);
            Weapon("pt_viper",      "Viper Pistol",    WeaponCategory.Pistol,       ItemRarity.Common, AmmoType.Light,  34,  320, 12, 1.4f, 50,  false, 2.5f, 1.2f, 1.2f, move: 1.08f);
            Weapon("pt_sidekick",   "Sidekick Revolver", WeaponCategory.Pistol,     ItemRarity.Rare,   AmmoType.Heavy,  72,  120, 6,  2.2f, 60,  false, 3f, 0.8f, 3f, move: 1.06f);
            Weapon("melee_fists",   "Fists",           WeaponCategory.Melee,        ItemRarity.Common, AmmoType.None,   18,  140, 0,  0,    2,   false, 0, 0, 0, move: 1.1f);
            Weapon("melee_machete", "Machete",         WeaponCategory.Melee,        ItemRarity.Common, AmmoType.None,   70,  85,  0,  0,    2.2f, false, 0, 0, 0, move: 1.1f);
            Weapon("melee_crowbar", "Crowbar",         WeaponCategory.Melee,        ItemRarity.Common, AmmoType.None,   60,  95,  0,  0,    2.2f, false, 0, 0, 0, move: 1.1f);
        }

        static void Attachment(string id, string name, AttachmentSlot slot, ItemRarity rarity, float zoom = 1, float spread = 1, float recoil = 1,
                               float range = 1, float mag = 0, float reload = 1, bool suppressor = false, params WeaponCategory[] fits)
        {
            var a = Asset<AttachmentData>($"{DataDir}/Items/{id}.asset");
            a.id = id; a.displayName = name; a.kind = ItemKind.Attachment; a.rarity = rarity; a.weight = 2; a.maxStack = 1; a.pickupAmount = 1;
            a.slot = slot; a.scopeZoom = zoom; a.spreadMultiplier = spread; a.recoilMultiplier = recoil; a.rangeMultiplier = range;
            a.magazineBonus = mag; a.reloadMultiplier = reload; a.suppressor = suppressor; a.fitsCategories = fits;
            Items[id] = a;
        }

        static void CreateAttachments()
        {
            var ar = WeaponCategory.AssaultRifle; var smg = WeaponCategory.SMG; var sr = WeaponCategory.Sniper; var lmg = WeaponCategory.LMG; var pt = WeaponCategory.Pistol;
            Attachment("scope_2x", "2x Scope", AttachmentSlot.Scope, ItemRarity.Common, zoom: 2, spread: 0.9f, fits: new[] { ar, smg, lmg, sr });
            Attachment("scope_4x", "4x Scope", AttachmentSlot.Scope, ItemRarity.Rare, zoom: 4, spread: 0.85f, fits: new[] { ar, lmg, sr });
            Attachment("scope_8x", "8x Scope", AttachmentSlot.Scope, ItemRarity.Epic, zoom: 8, spread: 0.8f, fits: new[] { sr });
            Attachment("muzzle_suppressor", "Suppressor", AttachmentSlot.Muzzle, ItemRarity.Rare, recoil: 0.95f, suppressor: true, fits: new[] { ar, smg, sr, pt });
            Attachment("muzzle_compensator", "Compensator", AttachmentSlot.Muzzle, ItemRarity.Common, recoil: 0.75f, fits: new[] { ar, smg, lmg, sr });
            Attachment("grip_vertical", "Vertical Foregrip", AttachmentSlot.Foregrip, ItemRarity.Common, spread: 0.9f, recoil: 0.8f);
            Attachment("mag_extended", "Extended Magazine", AttachmentSlot.Magazine, ItemRarity.Rare, mag: 0.5f);
            Attachment("mag_quick", "Quickdraw Magazine", AttachmentSlot.Magazine, ItemRarity.Common, mag: 0.2f, reload: 0.7f);
            Attachment("stock_tactical", "Tactical Stock", AttachmentSlot.Stock, ItemRarity.Common, spread: 0.85f, recoil: 0.85f);
        }

        static CharacterData[] CreateCharacters()
        {
            var list = new List<CharacterData>();
            void C(string id, string name, SkillId skill, Color color, float cd, float dur, float power, string desc)
            {
                var c = Asset<CharacterData>($"{DataDir}/Characters/{id}.asset");
                c.id = id; c.displayName = name; c.skill = skill; c.placeholderColor = color; c.cooldown = cd; c.duration = dur; c.power = power;
                c.skillDescription = desc;
                list.Add(c);
            }
            C("char_kael", "Kael", SkillId.SprintSurge, new Color(0.95f, 0.55f, 0.15f), 45, 6, 0.3f, "Active – Sprint Surge: +30% move speed for 6 s.");
            C("char_mira", "Mira", SkillId.FieldMedic, new Color(0.95f, 0.4f, 0.6f), 0, 0, 0.3f, "Passive – Field Medic: healing is 30% faster and 15% stronger.");
            C("char_torvin", "Torvin", SkillId.BastionDome, new Color(0.4f, 0.5f, 0.7f), 60, 5, 120, "Active – Bastion Dome: absorb 120 damage for 5 s.");
            C("char_zara", "Zara", SkillId.GhostStep, new Color(0.3f, 0.3f, 0.35f), 0, 0, 0, "Passive – Ghost Step: silent while crouched or prone.");
            C("char_okoro", "Okoro", SkillId.HealingAura, new Color(0.3f, 0.8f, 0.45f), 50, 5, 6, "Active – Healing Aura: 6 HP/s to allies within 5 m.");
            C("char_lyra", "Lyra", SkillId.EagleEye, new Color(0.65f, 0.4f, 0.9f), 0, 3, 0, "Passive – Eagle Eye: enemies you hit are revealed to your squad.");
            C("char_dante", "Dante", SkillId.IronSkin, new Color(0.6f, 0.2f, 0.2f), 0, 0, 0.08f, "Passive – Iron Skin: 8% less bullet damage.");
            C("char_suki", "Suki", SkillId.BlinkDash, new Color(0.2f, 0.75f, 0.95f), 30, 0.2f, 7, "Active – Blink Dash: dash 7 m.");
            C("char_rook", "Rook", SkillId.Scavenger, new Color(0.55f, 0.45f, 0.3f), 0, 0, 25, "Passive – Scavenger: knocking an enemy restores 25 HP.");
            C("char_nova", "Nova", SkillId.PulseScan, new Color(1f, 0.85f, 0.3f), 75, 3, 0, "Active – Pulse Scan: reveal enemies within 50 m.");
            C("char_bram", "Bram", SkillId.QuickHands, new Color(0.45f, 0.6f, 0.3f), 0, 0, 0.2f, "Passive – Quick Hands: reload 20% faster.");
            C("char_ivy", "Ivy", SkillId.VigorBloom, new Color(0.5f, 0.9f, 0.6f), 0, 0, 0.5f, "Passive – Vigor Bloom: +50% energy from Vigor Shrooms.");
            return list.ToArray();
        }

        static PetData[] CreatePets()
        {
            var list = new List<PetData>();
            void P(string id, string name, PetBuff buff, float power, string desc)
            {
                var p = Asset<PetData>($"{DataDir}/Pets/{id}.asset");
                p.id = id; p.displayName = name; p.buff = buff; p.power = power; p.description = desc;
                list.Add(p);
            }
            P("pet_ember", "Ember the Fox", PetBuff.SoftLanding, 0.5f, "50% less fall damage.");
            P("pet_byte", "Byte the Owl", PetBuff.GunfirePing, 0, "Pings enemies who shoot within 30 m.");
            P("pet_pebble", "Pebble the Turtle", PetBuff.ArmorMend, 0, "Slowly repairs your vest.");
            P("pet_dash", "Dash the Ferret", PetBuff.QuickPaws, 0.3f, "Larger pickup range.");
            return list.ToArray();
        }

        static LootTable[] CreateLootTables()
        {
            LootTable T(LootTier tier, float empty, params (string id, int weight, int min, int max)[] entries)
            {
                var t = Asset<LootTable>($"{DataDir}/Loot/Loot_{tier}.asset");
                t.tier = tier; t.emptyChance = empty;
                var list = new List<LootTable.Entry>();
                foreach (var e in entries)
                    if (Items.TryGetValue(e.id, out var item)) list.Add(new LootTable.Entry { item = item, weight = e.weight, minAmount = e.min, maxAmount = e.max });
                t.entries = list.ToArray();
                return t;
            }
            var low = T(LootTier.Low, 0.3f,
                ("pt_viper", 8, 0, 0), ("smg_wasp", 6, 0, 0), ("sg_boar", 5, 0, 0), ("melee_machete", 3, 0, 0), ("melee_crowbar", 2, 0, 0),
                ("ammo_light", 14, 20, 40), ("ammo_shells", 8, 6, 12), ("ammo_medium", 6, 15, 30), ("med_kit", 8, 1, 1), ("vigor_shroom", 10, 1, 2),
                ("vest_1", 6, 0, 0), ("helmet_1", 6, 0, 0), ("backpack_1", 5, 0, 0), ("scope_2x", 4, 0, 0), ("foam_wall", 5, 1, 2), ("nade_smoke", 3, 0, 0));
            var med = T(LootTier.Medium, 0.25f,
                ("ar_raptor7", 7, 0, 0), ("smg_hornet", 5, 0, 0), ("sg_thunderclap", 4, 0, 0), ("sr_falcon", 2, 0, 0), ("pt_sidekick", 3, 0, 0),
                ("ammo_medium", 14, 20, 40), ("ammo_light", 8, 20, 40), ("ammo_shells", 5, 6, 12), ("ammo_sniper", 3, 5, 10), ("med_kit", 10, 1, 2),
                ("vigor_shroom", 8, 1, 2), ("vest_2", 5, 0, 0), ("helmet_2", 5, 0, 0), ("backpack_2", 4, 0, 0), ("scope_2x", 4, 0, 0), ("scope_4x", 2, 0, 0),
                ("muzzle_compensator", 3, 0, 0), ("grip_vertical", 3, 0, 0), ("mag_quick", 3, 0, 0), ("stock_tactical", 2, 0, 0),
                ("nade_frag", 4, 0, 0), ("nade_flash", 2, 0, 0), ("foam_wall", 6, 2, 3), ("armor_repair", 2, 0, 0));
            var high = T(LootTier.High, 0.15f,
                ("ar_halberd", 6, 0, 0), ("ar_raptor7", 5, 0, 0), ("lmg_juggernaut", 3, 0, 0), ("sr_falcon", 3, 0, 0), ("sr_longreach", 1, 0, 0),
                ("ammo_medium", 12, 30, 60), ("ammo_heavy", 5, 30, 60), ("ammo_sniper", 5, 5, 10), ("med_kit", 10, 1, 3), ("vest_3", 4, 0, 0),
                ("helmet_3", 4, 0, 0), ("backpack_3", 3, 0, 0), ("scope_4x", 4, 0, 0), ("scope_8x", 1, 0, 0), ("muzzle_suppressor", 3, 0, 0),
                ("mag_extended", 3, 0, 0), ("nade_frag", 5, 1, 2), ("foam_wall", 6, 2, 4), ("armor_repair", 3, 0, 0));
            var drop = T(LootTier.Airdrop, 0f,
                ("sr_longreach", 5, 0, 0), ("lmg_juggernaut", 4, 0, 0), ("vest_4", 5, 0, 0), ("helmet_4", 5, 0, 0), ("scope_8x", 4, 0, 0),
                ("mag_extended", 4, 0, 0), ("muzzle_suppressor", 4, 0, 0), ("med_kit", 6, 3, 5), ("ammo_sniper", 4, 10, 20), ("ammo_heavy", 3, 60, 90),
                ("foam_wall", 4, 4, 6), ("nade_frag", 3, 2, 3));
            return new[] { low, med, high, drop };
        }

        // ------------------------------------------------------------------ prefabs
        static GameObject CreatePlayerPrefab()
        {
            var go = new GameObject("Player");
            go.AddComponent<NetworkObject>();
            var nt = go.AddComponent<ClientNetworkTransform>();
            nt.SyncRotAngleX = false; nt.SyncRotAngleZ = false;
            nt.SyncScaleX = nt.SyncScaleY = nt.SyncScaleZ = false;
            var cc = go.AddComponent<CharacterController>();
            cc.height = 1.8f; cc.radius = 0.4f; cc.center = new Vector3(0, 0.9f, 0); cc.stepOffset = 0.4f; cc.slopeLimit = 50;
            go.AddComponent<PlayerIdentity>();     // RequireComponent adds controller, health, inventory
            go.AddComponent<WeaponSystem>();
            go.AddComponent<PlayerSkills>();
            go.AddComponent<PingSystem>();
            var body = Primitive(PrimitiveType.Capsule, "Body", go.transform, new Vector3(0, 0.9f, 0), new Vector3(0.8f, 0.9f, 0.8f), Mat("PlayerBody", Color.white), false);
            Primitive(PrimitiveType.Cube, "Visor", body.transform, new Vector3(0, 0.6f, 0.4f), new Vector3(0.6f, 0.15f, 0.2f), Mat("Visor", new Color(0.1f, 0.1f, 0.12f)), false);
            SetLayer(go, GameConstants.LayerPlayer);
            return SavePrefab(go, "Player");
        }

        static GameObject CreateMatchSystemsPrefab()
        {
            var go = new GameObject("MatchSystems");
            go.AddComponent<NetworkObject>();
            go.AddComponent<MatchManager>();
            go.AddComponent<SafeZoneManager>();
            go.AddComponent<LootSpawner>();
            go.AddComponent<AirdropManager>();
            return SavePrefab(go, "MatchSystems");
        }

        static GameObject CreatePlanePrefab()
        {
            var go = new GameObject("DropPlane");
            go.AddComponent<NetworkObject>();
            go.AddComponent<PlaneController>();
            var mat = Mat("Plane", new Color(0.8f, 0.82f, 0.85f));
            Primitive(PrimitiveType.Capsule, "Fuselage", go.transform, Vector3.zero, new Vector3(5, 5, 30), mat, false).transform.localRotation = Quaternion.Euler(90, 0, 0);
            Primitive(PrimitiveType.Cube, "Wings", go.transform, new Vector3(0, 0, 2), new Vector3(40, 0.8f, 6), mat, false);
            Primitive(PrimitiveType.Cube, "Tail", go.transform, new Vector3(0, 4, -26), new Vector3(0.8f, 8, 4), mat, false);
            return SavePrefab(go, "DropPlane");
        }

        static GameObject CreateAirdropPrefab()
        {
            var go = new GameObject("Airdrop");
            go.AddComponent<NetworkObject>();
            go.AddComponent<NetworkTransform>();
            go.AddComponent<AirdropCrate>();
            var box = go.AddComponent<BoxCollider>();
            box.center = new Vector3(0, 0.6f, 0); box.size = new Vector3(1.6f, 1.2f, 1.6f);
            Primitive(PrimitiveType.Cube, "Crate", go.transform, new Vector3(0, 0.6f, 0), new Vector3(1.6f, 1.2f, 1.6f), Mat("Airdrop", new Color(0.9f, 0.3f, 0.2f)), false);
            Primitive(PrimitiveType.Sphere, "Chute", go.transform, new Vector3(0, 6, 0), new Vector3(6, 2, 6), Mat("Chute", new Color(1f, 0.85f, 0.2f)), false);
            return SavePrefab(go, "Airdrop");
        }

        static GameObject CreateGrenadePrefab()
        {
            var go = Primitive(PrimitiveType.Sphere, "Grenade", null, Vector3.zero, Vector3.one * 0.22f, Mat("Grenade", new Color(0.2f, 0.3f, 0.15f)), true);
            go.AddComponent<NetworkObject>();
            go.AddComponent<NetworkTransform>();
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 0.4f;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            go.AddComponent<Grenade>();
            return SavePrefab(go, "Grenade");
        }

        static GameObject CreateFoamWallPrefab()
        {
            var go = new GameObject("FoamWall");
            go.AddComponent<NetworkObject>();
            go.AddComponent<FoamWall>();
            var box = go.AddComponent<BoxCollider>();
            box.center = new Vector3(0, 1.1f, 0); box.size = new Vector3(3.2f, 2.2f, 0.5f);
            Primitive(PrimitiveType.Cube, "Wall", go.transform, new Vector3(0, 1.1f, 0), new Vector3(3.2f, 2.2f, 0.5f), Mat("FoamWall", new Color(0.5f, 0.85f, 1f)), false);
            return SavePrefab(go, "FoamWall");
        }

        static GameObject CreateVehiclePrefab(string name, VehicleType type, Vector3 size, int seats, float maxSpeed, Color color)
        {
            var go = new GameObject(name);
            go.AddComponent<NetworkObject>();
            go.AddComponent<ClientNetworkTransform>();
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = type == VehicleType.Motorbike ? 250 : 1200;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.centerOfMass = new Vector3(0, 0.3f, 0);
            var box = go.AddComponent<BoxCollider>();
            box.center = new Vector3(0, size.y / 2f, 0); box.size = size;
            var v = go.AddComponent<VehicleController>();
            v.type = type; v.maxSpeed = maxSpeed;
            Primitive(PrimitiveType.Cube, "Body", go.transform, new Vector3(0, size.y / 2f, 0), size, Mat("Vehicle_" + name, color), false);
            var seatList = new List<Transform>();
            for (int i = 0; i < seats; i++)
            {
                var s = new GameObject("Seat" + i).transform;
                s.SetParent(go.transform, false);
                float x = seats == 2 ? 0 : (i % 2 == 0 ? -0.5f : 0.5f);
                float z = seats == 2 ? (i == 0 ? 0.2f : -0.6f) : (i < 2 ? 0.5f : -0.9f);
                s.localPosition = new Vector3(x, size.y * 0.55f, z);
                seatList.Add(s);
            }
            v.seats = seatList.ToArray();
            SetLayer(go, GameConstants.LayerVehicle);
            return SavePrefab(go, "Vehicle_" + name);
        }

        // ------------------------------------------------------------------ scenes
        static void CreateBootstrapScene(GameDatabase db)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cam = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
            cam.tag = "MainCamera";
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.06f, 0.09f);
            var boot = new GameObject("GameBootstrap").AddComponent<GameBootstrap>();
            boot.database = db;
            EditorSceneManager.SaveScene(scene, SceneDir + "/" + GameConstants.BootstrapScene + ".unity");
        }

        static readonly (string name, MapZoneType type, Vector2 pos, float radius)[] Locations =
        {
            ("Ironhaven City", MapZoneType.City, new Vector2(-750, 450), 320),
            ("Fort Basalt", MapZoneType.MilitaryBase, new Vector2(900, -700), 260),
            ("Goldmeadow Farms", MapZoneType.Farmland, new Vector2(-950, -950), 380),
            ("Rustport", MapZoneType.Port, new Vector2(150, -1650), 260),
            ("Frostspine Peaks", MapZoneType.Mountains, new Vector2(1050, 1050), 450),
            ("Serpent River", MapZoneType.River, new Vector2(0, 150), 250),
        };

        static float HeightAt(float x, float z)
        {
            // Returns normalised height (0–1) of a 300 m tall terrain; water is at 4 m (≈0.0133).
            float r = Mathf.Max(Mathf.Abs(x), Mathf.Abs(z)) / 2000f;
            float island = 1f - Mathf.SmoothStep(0.82f, 0.98f, r);
            float h = 0.03f + Mathf.PerlinNoise(x * 0.0015f + 13, z * 0.0015f + 7) * 0.05f + Mathf.PerlinNoise(x * 0.01f, z * 0.01f) * 0.006f;

            // Mountains (north-east)
            float dm = Vector2.Distance(new Vector2(x, z), new Vector2(1050, 1050));
            h += Mathf.Exp(-dm * dm / (2 * 380f * 380f)) * 0.55f * (0.8f + 0.4f * Mathf.PerlinNoise(x * 0.004f, z * 0.004f));

            // Flatten towns
            foreach (var l in Locations)
            {
                if (l.type == MapZoneType.Mountains || l.type == MapZoneType.River) continue;
                float d = Vector2.Distance(new Vector2(x, z), l.pos);
                float target = l.type == MapZoneType.Port ? 0.02f : 0.035f;
                float t = 1f - Mathf.SmoothStep(l.radius * 0.8f, l.radius * 1.3f, d);
                h = Mathf.Lerp(h, target, t);
            }

            // River: winding west → east channel below water level
            float riverZ = 150 + Mathf.Sin(x / 450f) * 220f;
            float dr = Mathf.Abs(z - riverZ);
            if (x < 1700) h = Mathf.Lerp(0.004f, h, Mathf.SmoothStep(18f, 70f, dr));

            return Mathf.Lerp(0.0f, h, island);
        }

        static void CreateGameScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Terrain
            const int res = 513;
            var td = new TerrainData { heightmapResolution = res };
            td.size = new Vector3(GameConstants.MapSize, 300, GameConstants.MapSize);
            var heights = new float[res, res];
            for (int zi = 0; zi < res; zi++)
                for (int xi = 0; xi < res; xi++)
                {
                    float x = xi / (float)(res - 1) * GameConstants.MapSize - GameConstants.MapSize / 2f;
                    float z = zi / (float)(res - 1) * GameConstants.MapSize - GameConstants.MapSize / 2f;
                    heights[zi, xi] = HeightAt(x, z);
                }
            td.SetHeights(0, 0, heights);
            PaintTerrain(td);
            string tdPath = SceneDir + "/IsleVerdant_Terrain.asset";
            AssetDatabase.DeleteAsset(tdPath);
            AssetDatabase.CreateAsset(td, tdPath);
            var terrainGo = Terrain.CreateTerrainGameObject(td);
            terrainGo.name = "Island";
            terrainGo.transform.position = new Vector3(-GameConstants.MapSize / 2f, 0, -GameConstants.MapSize / 2f);
            var terrain = terrainGo.GetComponent<Terrain>();
            terrain.heightmapPixelError = 8;      // cheaper on mobile
            terrain.basemapDistance = 400;
            terrain.drawInstanced = true;

            float Ground(float x, float z) => terrain.SampleHeight(new Vector3(x, 0, z)) + terrain.transform.position.y;

            // Water
            var water = Primitive(PrimitiveType.Plane, "Water", null, new Vector3(0, GameConstants.WaterLevel, 0), new Vector3(700, 1, 700), Mat("Water", new Color(0.15f, 0.45f, 0.65f)), false);
            water.isStatic = true;

            // Light + systems
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.Hard;
            sun.transform.rotation = Quaternion.Euler(50, -30, 0);
            var env = new GameObject("Environment");
            env.AddComponent<DayNightController>().sun = sun;
            env.AddComponent<SafeZoneVisual>();
            var cam = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cam.tag = "MainCamera";
            cam.transform.SetPositionAndRotation(new Vector3(0, 300, -600), Quaternion.Euler(25, 0, 0));

            // Towns
            var rng = new System.Random(42);
            var wallMat = Mat("Building", new Color(0.75f, 0.72f, 0.68f));
            var roofMat = Mat("Roof", new Color(0.55f, 0.3f, 0.25f));
            var milMat = Mat("Military", new Color(0.35f, 0.4f, 0.3f));
            var farmMat = Mat("Barn", new Color(0.65f, 0.25f, 0.2f));
            var portMat = Mat("Container", new Color(0.2f, 0.4f, 0.7f));
            foreach (var l in Locations)
            {
                var parent = new GameObject(l.name).transform;
                var loc = parent.gameObject.AddComponent<MapLocation>();
                loc.displayName = l.name; loc.zoneType = l.type; loc.radius = l.radius;
                parent.position = new Vector3(l.pos.x, Ground(l.pos.x, l.pos.y), l.pos.y);

                int count = l.type switch { MapZoneType.City => 36, MapZoneType.MilitaryBase => 14, MapZoneType.Farmland => 10, MapZoneType.Port => 16, MapZoneType.Mountains => 6, _ => 4 };
                LootTier tier = l.type switch { MapZoneType.MilitaryBase => LootTier.High, MapZoneType.City => LootTier.Medium, MapZoneType.Port => LootTier.Medium, _ => LootTier.Low };
                Material mat = l.type switch { MapZoneType.MilitaryBase => milMat, MapZoneType.Farmland => farmMat, MapZoneType.Port => portMat, _ => wallMat };
                for (int i = 0; i < count; i++)
                {
                    float spread = l.radius * 0.75f;
                    float x = l.pos.x + (float)(rng.NextDouble() * 2 - 1) * spread;
                    float z = l.pos.y + (float)(rng.NextDouble() * 2 - 1) * spread;
                    if (l.type == MapZoneType.River) z = 150 + Mathf.Sin(x / 450f) * 220f + (i % 2 == 0 ? 90 : -90);
                    float y = Ground(x, z);
                    if (y < GameConstants.WaterLevel + 0.5f) continue;
                    float w = l.type == MapZoneType.City ? 14 + (float)rng.NextDouble() * 10 : 10 + (float)rng.NextDouble() * 6;
                    float h = l.type == MapZoneType.City ? 5 + (float)rng.NextDouble() * 4 : 4.5f;
                    Shelter(parent, new Vector3(x, y, z), w, h, mat, roofMat, tier, (float)rng.NextDouble() * 90f);
                }
                if (l.type == MapZoneType.Mountains || l.type == MapZoneType.River)
                    for (int i = 0; i < 25; i++)
                    {
                        float x = l.pos.x + (float)(rng.NextDouble() * 2 - 1) * l.radius;
                        float z = l.pos.y + (float)(rng.NextDouble() * 2 - 1) * l.radius;
                        float y = Ground(x, z);
                        if (y < GameConstants.WaterLevel + 0.5f) continue;
                        LootPoint(parent, new Vector3(x, y, z), LootTier.Low);
                    }
            }

            // Scattered loot + trees over the whole island
            var treeMat = Mat("Tree", new Color(0.2f, 0.45f, 0.2f));
            var trunkMat = Mat("Trunk", new Color(0.4f, 0.28f, 0.18f));
            var wild = new GameObject("Wilderness").transform;
            for (int i = 0; i < 900; i++)
            {
                float x = (float)(rng.NextDouble() * 2 - 1) * 1750, z = (float)(rng.NextDouble() * 2 - 1) * 1750;
                float y = Ground(x, z);
                if (y < GameConstants.WaterLevel + 1 || y > 140) continue;
                if (i % 3 == 0) LootPoint(wild, new Vector3(x, y, z), LootTier.Low);
                else
                {
                    var tree = new GameObject("Tree").transform;
                    tree.SetParent(wild); tree.position = new Vector3(x, y, z);
                    Primitive(PrimitiveType.Cylinder, "Trunk", tree, new Vector3(0, 2, 0), new Vector3(0.6f, 2, 0.6f), trunkMat, true);
                    Primitive(PrimitiveType.Sphere, "Leaves", tree, new Vector3(0, 5.5f, 0), new Vector3(4, 4, 4), treeMat, false);
                    tree.gameObject.isStatic = true;
                }
            }

            // Vehicles
            var vehicles = new GameObject("VehicleSpawns").transform;
            foreach (var l in Locations)
            {
                if (l.type == MapZoneType.Mountains) continue;
                for (int i = 0; i < 4; i++)
                {
                    float a = i * Mathf.PI / 2f;
                    float x = l.pos.x + Mathf.Cos(a) * (l.radius + 40), z = l.pos.y + Mathf.Sin(a) * (l.radius + 40);
                    float y = Ground(x, z);
                    var sp = new GameObject("VehicleSpawn").AddComponent<VehicleSpawnPoint>();
                    sp.transform.SetParent(vehicles);
                    if (y < GameConstants.WaterLevel) { sp.type = VehicleType.Boat; y = GameConstants.WaterLevel; }
                    else sp.type = (VehicleType)(i % 3);
                    sp.transform.position = new Vector3(x, y, z);
                }
            }
            // Boats at the port and on the river
            foreach (var p in new[] { new Vector2(150, -1850), new Vector2(250, -1860), new Vector2(-400, 150 + Mathf.Sin(-400 / 450f) * 220f), new Vector2(600, 150 + Mathf.Sin(600 / 450f) * 220f) })
            {
                var sp = new GameObject("BoatSpawn").AddComponent<VehicleSpawnPoint>();
                sp.transform.SetParent(vehicles);
                sp.type = VehicleType.Boat; sp.chance = 1;
                sp.transform.position = new Vector3(p.x, GameConstants.WaterLevel, p.y);
            }

            // Spawn points: waiting area (beach near the centre) and Clash Squad / TDM bases in the city
            var spawns = new GameObject("SpawnPoints").transform;
            for (int i = 0; i < 12; i++)
            {
                float a = i / 12f * Mathf.PI * 2;
                AddSpawn(spawns, -1, new Vector3(-300 + Mathf.Cos(a) * 25, 0, -300 + Mathf.Sin(a) * 25), Ground);
            }
            var city = Locations[0].pos;
            for (int i = 0; i < 4; i++)
            {
                AddSpawn(spawns, 0, new Vector3(city.x - 120, 0, city.y - 10 + i * 6), Ground);
                AddSpawn(spawns, 1, new Vector3(city.x + 120, 0, city.y - 10 + i * 6), Ground);
            }

            EditorSceneManager.SaveScene(scene, SceneDir + "/" + GameConstants.GameScene + ".unity");
        }

        static void AddSpawn(Transform parent, int team, Vector3 p, System.Func<float, float, float> ground)
        {
            var sp = new GameObject($"Spawn_T{team}").AddComponent<SpawnPoint>();
            sp.team = team;
            sp.transform.SetParent(parent);
            sp.transform.position = new Vector3(p.x, ground(p.x, p.z) + 0.2f, p.z);
        }

        /// <summary>Open building: floor, 4 pillars, 3 walls and a roof, with loot inside.</summary>
        static void Shelter(Transform parent, Vector3 pos, float size, float height, Material wall, Material roof, LootTier tier, float yaw)
        {
            var b = new GameObject("Building").transform;
            b.SetParent(parent);
            b.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
            float half = size / 2f;
            Primitive(PrimitiveType.Cube, "Floor", b, new Vector3(0, 0.1f, 0), new Vector3(size, 0.4f, size), wall, true);
            Primitive(PrimitiveType.Cube, "Roof", b, new Vector3(0, height, 0), new Vector3(size + 0.6f, 0.4f, size + 0.6f), roof, true);
            Primitive(PrimitiveType.Cube, "WallBack", b, new Vector3(0, height / 2f, -half), new Vector3(size, height, 0.4f), wall, true);
            Primitive(PrimitiveType.Cube, "WallLeft", b, new Vector3(-half, height / 2f, 0), new Vector3(0.4f, height, size), wall, true);
            Primitive(PrimitiveType.Cube, "WallRight", b, new Vector3(half, height / 2f, -half / 2f), new Vector3(0.4f, height, size / 2f), wall, true);
            Primitive(PrimitiveType.Cube, "Pillar", b, new Vector3(half, height / 2f, half), new Vector3(0.5f, height, 0.5f), wall, true);
            Primitive(PrimitiveType.Cube, "Pillar", b, new Vector3(-half, height / 2f, half), new Vector3(0.5f, height, 0.5f), wall, true);
            b.gameObject.isStatic = true;
            foreach (var t in b.GetComponentsInChildren<Transform>()) t.gameObject.isStatic = true;
            int points = Mathf.Clamp(Mathf.RoundToInt(size / 6f), 1, 4);
            for (int i = 0; i < points; i++)
                LootPoint(b, b.TransformPoint(new Vector3((i % 2 == 0 ? -1 : 1) * half * 0.45f, 0.35f, (i < 2 ? -1 : 1) * half * 0.4f)), tier);
        }

        static void LootPoint(Transform parent, Vector3 pos, LootTier tier)
        {
            var lp = new GameObject("Loot").AddComponent<LootSpawnPoint>();
            lp.transform.SetParent(parent);
            lp.transform.position = pos;
            lp.tier = tier;
            lp.items = tier == LootTier.High ? 3 : 2;
        }

        static void PaintTerrain(TerrainData td)
        {
            TerrainLayer Layer(string name, Color c)
            {
                string texPath = $"{ArtDir}/{name}.png";
                var tex = new Texture2D(64, 64);
                var px = new Color[64 * 64];
                var r = new System.Random(name.GetHashCode());
                for (int i = 0; i < px.Length; i++) { float n = 0.9f + (float)r.NextDouble() * 0.2f; px[i] = new Color(c.r * n, c.g * n, c.b * n); }
                tex.SetPixels(px); tex.Apply();
                File.WriteAllBytes(texPath, tex.EncodeToPNG());
                AssetDatabase.ImportAsset(texPath);
                string layerPath = $"{ArtDir}/{name}.terrainlayer";
                var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(layerPath);
                if (layer == null) { layer = new TerrainLayer(); AssetDatabase.CreateAsset(layer, layerPath); }
                layer.diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
                layer.tileSize = new Vector2(8, 8);
                EditorUtility.SetDirty(layer);
                return layer;
            }
            td.terrainLayers = new[] { Layer("Grass", new Color(0.33f, 0.52f, 0.24f)), Layer("Sand", new Color(0.82f, 0.75f, 0.55f)), Layer("Rock", new Color(0.45f, 0.44f, 0.42f)), Layer("Snow", new Color(0.92f, 0.94f, 0.97f)) };
            int ar = 256;
            td.alphamapResolution = ar;
            var maps = new float[ar, ar, 4];
            for (int y = 0; y < ar; y++)
                for (int x = 0; x < ar; x++)
                {
                    float nx = x / (float)(ar - 1), ny = y / (float)(ar - 1);
                    float h = td.GetInterpolatedHeight(nx, ny);
                    float steep = td.GetSteepness(nx, ny);
                    int layer = h < GameConstants.WaterLevel + 2.5f ? 1 : h > 150 ? 3 : steep > 30 ? 2 : 0;
                    maps[y, x, layer] = 1;
                }
            td.SetAlphamaps(0, 0, maps);
        }
    }
}
#endif
