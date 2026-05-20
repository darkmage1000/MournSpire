using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using MournSpire.Player;
using MournSpire.Enemy;
using MournSpire.Items;
using MournSpire.Core;

namespace MournSpire.UI
{
    /// <summary>
    /// Manages all HUD elements: stats bars, combat log, boss bar,
    /// interact prompt, inventory panel, shop panel, win/lose overlay.
    /// Assign UI element refs in the Inspector.
    /// </summary>
    public class HUDController : MonoBehaviour
    {
        // ── Stats panel ────────────────────────────────────────────────────────
        [Header("Stats")]
        public Slider   hpBar;
        public TMP_Text hpText;
        public Slider   spBar;
        public TMP_Text spText;
        public Slider   xpBar;
        public TMP_Text xpText;
        public TMP_Text goldText;
        public TMP_Text levelText;
        public TMP_Text blockIndicator;

        // ── Enemy counter ──────────────────────────────────────────────────────
        [Header("Enemy Counter")]
        public TMP_Text enemyCountText;

        // ── Combat log ─────────────────────────────────────────────────────────
        [Header("Combat Log")]
        public Transform logContainer;
        public GameObject logEntryPrefab;
        public int        maxLogEntries = 5;
        private readonly Queue<GameObject> _logEntries = new();

        // ── Boss bar ───────────────────────────────────────────────────────────
        [Header("Boss Bar")]
        public GameObject bossBarRoot;
        public Slider     bossHpSlider;
        public TMP_Text   bossHpText;

        // ── Interact prompt ────────────────────────────────────────────────────
        [Header("Interact Prompt")]
        public GameObject interactPromptRoot;
        public TMP_Text   interactPromptText;

        // ── Inventory panel ────────────────────────────────────────────────────
        [Header("Inventory")]
        public GameObject inventoryPanel;
        public TMP_Text   invWoodText;
        public TMP_Text   invStoneText;
        public TMP_Text   invIronText;
        public TMP_Text   invPotionsText;
        public TMP_Text   invTonicsText;
        private bool      _inventoryOpen;

        // ── Shop panel ─────────────────────────────────────────────────────────
        [Header("Shop")]
        public GameObject shopPanel;

        // ── Message overlay ────────────────────────────────────────────────────
        [Header("Message")]
        public GameObject overlayRoot;
        public TMP_Text   overlayTitle;
        public TMP_Text   overlaySub;

        // ── Level-up ───────────────────────────────────────────────────────────
        [Header("Level-Up")]
        public GameObject levelUpBanner;

        // ── Zone label ─────────────────────────────────────────────────────────
        [Header("Zone")]
        public TMP_Text zoneLabel;

        // ── Private refs ───────────────────────────────────────────────────────
        private PlayerStats _stats;
        private BossController _boss;

        // ── Init ───────────────────────────────────────────────────────────────

        public void Init(PlayerStats stats, Inventory inv, int enemyCount, BossController boss)
        {
            _stats = stats;
            _boss  = boss;

            RefreshInventory(inv);
            UpdateEnemyCount(enemyCount);
            if (bossBarRoot) bossBarRoot.SetActive(false);
            if (overlayRoot) overlayRoot.SetActive(false);
            if (inventoryPanel) inventoryPanel.SetActive(false);
            if (shopPanel)      shopPanel.SetActive(false);
            if (levelUpBanner)  levelUpBanner.SetActive(false);
        }

        // ── Per-frame update ───────────────────────────────────────────────────

        void Update()
        {
            if (_stats == null) return;
            RefreshStats();
            if (_boss != null) RefreshBossBar();

            // B key toggles inventory
            if (UnityEngine.InputSystem.Keyboard.current.bKey.wasPressedThisFrame)
                ToggleInventory();
        }

        void RefreshStats()
        {
            if (hpBar)   hpBar.value   = (float)_stats.hp / _stats.maxHp;
            if (hpText)  hpText.text   = $"{_stats.hp}/{_stats.maxHp}";
            if (spBar)   spBar.value   = _stats.stamina / _stats.maxStamina;
            if (spText)  spText.text   = $"{Mathf.CeilToInt(_stats.stamina)}/{Mathf.CeilToInt(_stats.maxStamina)}";
            if (xpBar)   xpBar.value   = (float)_stats.xp / _stats.xpToNext;
            if (xpText)  xpText.text   = $"{_stats.xp}/{_stats.xpToNext}";
            if (goldText) goldText.text = $"Gold: {_stats.gold}";
            if (levelText) levelText.text = $"Lv.{_stats.level} | ATK {_stats.atk} | DEF {_stats.def}";
        }

        void RefreshBossBar()
        {
            if (!bossBarRoot || !_boss.Triggered || _boss._stats().IsDead) return;
            float pct = (float)_boss._stats().Hp / _boss._stats().MaxHp;
            if (bossHpSlider) bossHpSlider.value = pct;
            if (bossHpText)   bossHpText.text     = $"{_boss._stats().Hp} / {_boss._stats().MaxHp}";
        }

        // ── Public API ─────────────────────────────────────────────────────────

        public void Log(string msg)
        {
            if (logEntryPrefab == null || logContainer == null) return;

            var go  = Instantiate(logEntryPrefab, logContainer);
            var tmp = go.GetComponentInChildren<TMP_Text>();
            if (tmp) tmp.text = msg;

            _logEntries.Enqueue(go);
            if (_logEntries.Count > maxLogEntries)
                Destroy(_logEntries.Dequeue());

            StartCoroutine(FadeOut(go, 3.5f));
        }

        public void UpdateEnemyCount(int alive)
        {
            if (enemyCountText) enemyCountText.text = $"Enemies: {alive}";
        }

        public void ShowBossBar(bool visible)
        {
            if (bossBarRoot) bossBarRoot.SetActive(visible);
        }

        public void ShowInteractPrompt(string text)
        {
            if (interactPromptRoot) interactPromptRoot.SetActive(true);
            if (interactPromptText) interactPromptText.text = text;
        }

        public void HideInteractPrompt()
        {
            if (interactPromptRoot) interactPromptRoot.SetActive(false);
        }

        public void ShowMessage(string title, string sub, bool defeat = false)
        {
            if (!overlayRoot) return;
            overlayRoot.SetActive(true);
            if (overlayTitle) overlayTitle.text = title;
            if (overlaySub)   overlaySub.text   = sub;
        }

        public void FlashLevelUp()
        {
            if (levelUpBanner) StartCoroutine(ShowBannerFor(levelUpBanner, 1.8f));
        }

        public void RefreshInventory(Inventory inv)
        {
            if (invWoodText)   invWoodText.text   = inv.wood.ToString();
            if (invStoneText)  invStoneText.text  = inv.stone.ToString();
            if (invIronText)   invIronText.text   = inv.iron.ToString();
            if (invPotionsText) invPotionsText.text = inv.potions.ToString();
            if (invTonicsText)  invTonicsText.text  = inv.tonics.ToString();
        }

        public void OpenShop(MerchantController merchant, PlayerStats stats, Inventory inv)
        {
            if (shopPanel) shopPanel.SetActive(true);
            // Actual button wiring is done via a ShopController component on the shopPanel GO
            var sc = shopPanel?.GetComponent<ShopController>();
            sc?.Open(merchant, stats, inv, this);
        }

        public void CloseShop()
        {
            if (shopPanel) shopPanel.SetActive(false);
        }

        // ── Inventory toggle ───────────────────────────────────────────────────

        void ToggleInventory()
        {
            _inventoryOpen = !_inventoryOpen;
            if (inventoryPanel) inventoryPanel.SetActive(_inventoryOpen);
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        IEnumerator FadeOut(GameObject go, float delay)
        {
            yield return new WaitForSeconds(delay);
            float t = 0f;
            var cg  = go.GetComponent<CanvasGroup>() ?? go.AddComponent<CanvasGroup>();
            while (t < 1f)
            {
                t += Time.deltaTime / 0.8f;
                cg.alpha = 1f - t;
                yield return null;
            }
            Destroy(go);
            _logEntries.TryDequeue(out _);
        }

        IEnumerator ShowBannerFor(GameObject go, float dur)
        {
            go.SetActive(true);
            yield return new WaitForSeconds(dur);
            go.SetActive(false);
        }
    }

    // Extension so GameManager can call _stats() on EnemyController
    public static class EnemyExtensions
    {
        public static MournSpire.Enemy.EnemyStats _stats(this MournSpire.Enemy.EnemyController e)
            => e.GetComponent<MournSpire.Enemy.EnemyStats>();
    }
}
