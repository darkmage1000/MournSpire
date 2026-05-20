using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using MournSpire.Core;
using MournSpire.Items;
using MournSpire.Player;

namespace MournSpire.UI
{
    /// <summary>
    /// Drives the shop UI panel.  Attach to the ShopPanel GameObject.
    /// HUDController.OpenShop() calls Open() here.
    /// Wire the Close button in Inspector to CloseShop().
    /// </summary>
    public class ShopController : MonoBehaviour
    {
        [Header("UI Refs")]
        public TMP_Text    titleText;
        public TMP_Text    infoText;
        public Transform   rowContainer;
        public GameObject  rowPrefab;    // prefab with TMP label + Button

        private MerchantController _merchant;
        private PlayerStats        _stats;
        private Inventory          _inv;
        private HUDController      _hud;

        // ── Resource prices ────────────────────────────────────────────────────
        static readonly Dictionary<string,int> ResourcePrices = new()
        {
            { "wood",  4 },
            { "stone", 6 },
            { "iron", 10 },
        };

        // ── Gear upgrades ──────────────────────────────────────────────────────
        record GearUpgrade(string Id, string Label, int Cost);
        static readonly GearUpgrade[] Upgrades = {
            new("atk",   "+3 Attack",   25),
            new("def",   "+2 Defense",  20),
            new("maxhp", "+25 Max HP",  30),
            new("maxsp", "+20 Max SP",  20),
        };

        const int PotionCost = 15;
        const int TonicCost  = 12;

        // ── Public ─────────────────────────────────────────────────────────────

        public void Open(MerchantController merchant, PlayerStats stats,
                         Inventory inv, HUDController hud)
        {
            _merchant = merchant;
            _stats    = stats;
            _inv      = inv;
            _hud      = hud;

            Rebuild();
        }

        public void CloseShop() => _hud?.CloseShop();

        // ── Build ───────────────────────────────────────────────────────────────

        void Rebuild()
        {
            foreach (Transform child in rowContainer) Destroy(child.gameObject);

            if (titleText) titleText.text = _merchant.RoleTitle;

            switch (_merchant.role)
            {
                case MerchantRole.ResourceBuyer:    BuildBuyer();       break;
                case MerchantRole.GearSeller:       BuildGear();        break;
                case MerchantRole.ConsumableSeller: BuildConsumables(); break;
            }
        }

        void BuildBuyer()
        {
            if (infoText) infoText.text = "Sell resources for gold.";
            foreach (var kv in ResourcePrices)
            {
                string res   = kv.Key;
                int    price = kv.Value;
                int    qty   = res == "wood" ? _inv.wood
                             : res == "stone" ? _inv.stone : _inv.iron;

                AddRow($"{Cap(res)} ×{qty}", $"Sell all ({qty * price}g)", qty > 0, () =>
                {
                    _stats.gold += qty * price;
                    if (res == "wood")  _inv.wood  = 0;
                    if (res == "stone") _inv.stone = 0;
                    if (res == "iron")  _inv.iron  = 0;
                    _inv.OnChanged?.Invoke();
                    _hud.Log($"Sold {qty} {res} for {qty * price}g");
                    Rebuild();
                });
            }
        }

        void BuildGear()
        {
            if (infoText) infoText.text = $"Gold: {_stats.gold}";
            foreach (var upg in Upgrades)
            {
                string id   = upg.Id;
                string lbl  = upg.Label;
                int    cost = upg.Cost;
                AddRow(lbl, $"{cost}g", _stats.gold >= cost, () =>
                {
                    _stats.gold -= cost;
                    switch (id)
                    {
                        case "atk":   _stats.atk       += 3;  break;
                        case "def":   _stats.def       += 2;  break;
                        case "maxhp": _stats.maxHp     += 25; _stats.Heal(25); break;
                        case "maxsp": _stats.maxStamina += 20f; break;
                    }
                    _hud.Log($"Purchased: {lbl}");
                    Rebuild();
                });
            }
        }

        void BuildConsumables()
        {
            if (infoText) infoText.text = $"Gold: {_stats.gold}  | Potions: {_inv.potions}  Tonics: {_inv.tonics}";
            AddRow("Health Potion (+50 HP)", $"{PotionCost}g", _stats.gold >= PotionCost, () =>
            {
                _stats.gold -= PotionCost;
                _inv.AddPotion();
                _hud.Log("Bought Health Potion");
                Rebuild();
            });
            AddRow("Stamina Tonic (+60 SP)", $"{TonicCost}g", _stats.gold >= TonicCost, () =>
            {
                _stats.gold -= TonicCost;
                _inv.AddTonic();
                _hud.Log("Bought Stamina Tonic");
                Rebuild();
            });
        }

        void AddRow(string label, string btnText, bool enabled, System.Action onClick)
        {
            if (rowPrefab == null || rowContainer == null) return;
            var go  = Instantiate(rowPrefab, rowContainer);
            var txts = go.GetComponentsInChildren<TMP_Text>();
            if (txts.Length >= 1) txts[0].text = label;
            if (txts.Length >= 2) txts[1].text = btnText;
            var btn = go.GetComponentInChildren<Button>();
            if (btn)
            {
                btn.interactable = enabled;
                btn.onClick.AddListener(() => onClick());
            }
        }

        static string Cap(string s) => s.Length == 0 ? s : char.ToUpper(s[0]) + s[1..];
    }
}
