using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using MournSpire.World;
using MournSpire.Player;
using MournSpire.Enemy;
using MournSpire.Items;
using MournSpire.UI;

namespace MournSpire.Core
{
    /// <summary>
    /// Central game coordinator.
    ///  - Wires up world, player, enemies, and UI.
    ///  - Handles zone transitions (Overworld ↔ Dungeon).
    ///  - Routes combat events to the HUD log.
    ///
    /// Place one GameManager in each scene (Overworld, Dungeon).
    /// The Inventory and PlayerStats persist via DontDestroyOnLoad.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public enum Zone { Overworld, Dungeon }

        [Header("Scene Config")]
        public Zone currentZone = Zone.Overworld;

        [Header("Scene Names")]
        public string overworldScene = "Overworld";
        public string dungeonScene   = "Dungeon";

        [Header("Refs — assign in Inspector")]
        public WorldBuilder     worldBuilder;
        public PlayerController playerController;
        public PlayerStats      playerStats;
        public Inventory        inventory;
        public HUDController    hud;
        public BossController   boss;           // null in Overworld scene

        // ── Enemy tracking ─────────────────────────────────────────────────────
        private readonly List<EnemyController> _enemies = new();

        // ── Lifecycle ───────────────────────────────────────────────────────────

        void Start()
        {
            // Init player at map start tile
            playerController.Init(worldBuilder, worldBuilder.Data.PlayerStart);
            playerController.OnMovedTo    += HandlePlayerMoved;
            playerController.OnBumpAttack += HandleBumpAttack;
            playerController.OnAttackSwing += HandleAttackSwing;
            playerController.OnInteract   += HandleInteract;

            playerStats.OnLevelUp.AddListener(() =>
            {
                hud.FlashLevelUp();
                hud.Log("✦ <color=#ffd700>LEVEL UP!</color>");
            });
            playerStats.OnDeath.AddListener(HandlePlayerDeath);

            SpawnEnemies();
            if (boss != null) SetupBoss();

            hud.Init(playerStats, inventory, _enemies.Count, boss);
            inventory.OnChanged.AddListener(() => hud.RefreshInventory(inventory));
        }

        void Update()
        {
            // F key — use potion
            if (UnityEngine.InputSystem.Keyboard.current.fKey.wasPressedThisFrame)
            {
                if (inventory.UsePotion(playerStats))
                    hud.Log("<color=#88ff88>Used Health Potion! +50 HP</color>");
                else
                    hud.Log("No potions.");
            }
        }

        // ── Enemy spawn ────────────────────────────────────────────────────────

        void SpawnEnemies()
        {
            // Enemies are pre-placed in the scene as prefabs on their spawn tiles.
            // Find them all in the scene.
            var all = FindObjectsByType<EnemyController>(FindObjectsSortMode.None);
            foreach (var e in all)
            {
                if (e is BossController) continue; // boss handled separately
                _enemies.Add(e);
                e.Init(worldBuilder, playerController, playerStats, _enemies);
                e.OnMeleeHit     += dmg => hud.Log($"<color=#f88>{e.name}</color> hits you for <b>{dmg}</b>!");
                e.OnProjectileHit += dmg => hud.Log($"<color=#f88>{e.name}</color> projectile hits for <b>{dmg}</b>!");
                e.OnDied         += () => { hud.UpdateEnemyCount(AliveCount()); };
            }
        }

        void SetupBoss()
        {
            boss.Init(worldBuilder, playerController, playerStats, _enemies);
            _enemies.Add(boss);
            boss.OnEncounter    += () =>
            {
                hud.ShowBossBar(true);
                hud.Log("<color=#cc44ff><b>☠ THE LICH KING AWAKENS! ☠</b></color>");
            };
            boss.OnMeleeHit     += dmg => hud.Log($"<color=#f88>Lich King strikes you for <b>{dmg}</b>!</color>");
            boss.OnPowerStrike  += dmg => hud.Log($"<color=#ff4400><b>⚡ POWER STRIKE — {dmg} damage!</b></color>");
            boss.OnPhase2       += ()  => hud.Log("<color=#ff5500><b>☠ LICH KING ENRAGED! ☠</b></color>");
            boss.OnProjectileHit += dmg => hud.Log($"<color=#cc44ff><b>☠ Shadow Bolt — {dmg} damage!</b></color>");
            boss.OnDied          += ()  => hud.ShowBossBar(false);
        }

        // ── Player event handlers ─────────────────────────────────────────────

        void HandlePlayerMoved(Vector2Int tile)
        {
            // Check teleporter
            var tp = worldBuilder.Data.TeleporterPos;
            if (tp.HasValue && tile == tp.Value)
                hud.ShowInteractPrompt(currentZone == Zone.Overworld
                    ? "[E] Enter dungeon" : "[E] Return to overworld");
            else
                hud.HideInteractPrompt();
        }

        void HandleBumpAttack(Vector2Int from, Vector2Int to)
        {
            var enemy = _enemies.Find(e => !e._stats().IsDead && e.TilePos == to);
            if (enemy == null) return;

            if (playerStats.stamina < playerController.attackStaminaCost)
            {
                hud.Log("<color=#888>Too exhausted…</color>");
                return;
            }

            int roll = playerStats.atk + Random.Range(0, 6);
            int dmg  = enemy.ReceiveDamage(roll);

            if (enemy.LastHitBlocked)
            {
                hud.Log("<color=#aaa>⚔ Your attack bounces off the shield!</color>");
                return;
            }

            string msg = $"You hit <color=#8ef>{enemy.name}</color> for <b>{dmg}</b>!";
            if (enemy._stats().IsDead)
            {
                int gold    = enemy._stats().GoldDrop;
                bool leveled = playerStats.GainXp(enemy._stats().XpValue);
                playerStats.gold += gold;
                hud.Log($"{msg} ☠ +{gold}g +{enemy._stats().XpValue}xp");
                hud.UpdateEnemyCount(AliveCount());
            }
            else
            {
                hud.Log(msg);
            }
        }

        void HandleAttackSwing()
        {
            // Directional attack (Space) toward facing tile
            var facing = playerController.Facing;
            var target = playerController.TilePos + facing;
            var enemy  = _enemies.Find(e => !e._stats().IsDead && e.TilePos == target);

            if (playerStats.stamina < playerController.attackStaminaCost)
            {
                hud.Log("<color=#888>Too exhausted to attack…</color>");
                return;
            }

            if (enemy == null) { hud.Log("You swing at empty air…"); return; }

            int roll = playerStats.atk + Random.Range(0, 6);
            int dmg  = enemy.ReceiveDamage(roll);

            if (enemy.LastHitBlocked)
            {
                hud.Log("<color=#aaa>⚔ Your attack bounces off the shield!</color>");
                return;
            }

            string msg = $"You hit <color=#8ef>{enemy.name}</color> for <b>{dmg}</b>!";
            if (enemy._stats().IsDead)
            {
                int gold     = enemy._stats().GoldDrop;
                bool leveled = playerStats.GainXp(enemy._stats().XpValue);
                playerStats.gold += gold;
                hud.Log($"{msg} ☠ +{gold}g +{enemy._stats().XpValue}xp");
                hud.UpdateEnemyCount(AliveCount());
            }
            else
            {
                hud.Log(msg);
            }
        }

        void HandleInteract()
        {
            var tile = playerController.TilePos;

            // Teleporter?
            var tp = worldBuilder.Data.TeleporterPos;
            if (tp.HasValue && tile == tp.Value)
            {
                string target = currentZone == Zone.Overworld ? dungeonScene : overworldScene;
                StartCoroutine(TransitionToScene(target));
                return;
            }

            // Resource node? (adjacent tile in facing direction)
            var facingTile = tile + playerController.Facing;
            var node = FindResourceAt(facingTile);
            if (node != null)
            {
                if (node.Depleted) { hud.Log("Nothing left to gather here."); return; }
                int amount = node.Interact(inventory);
                hud.Log($"Gathered {amount} {node.resourceType}.");
                return;
            }

            // Merchant?
            var merchant = FindMerchantAt(facingTile);
            if (merchant != null)
            {
                hud.OpenShop(merchant, playerStats, inventory);
                return;
            }
        }

        // ── Zone transition ────────────────────────────────────────────────────

        IEnumerator TransitionToScene(string sceneName)
        {
            hud.Log($"<color=#4488ff>Entering {sceneName}…</color>");
            yield return new WaitForSeconds(0.4f);
            SceneManager.LoadScene(sceneName);
        }

        // ── End conditions ─────────────────────────────────────────────────────

        void HandlePlayerDeath()
        {
            hud.ShowMessage("YOU DIED", "Press R to restart", true);
        }

        void CheckVictory()
        {
            if (AliveCount() == 0)
                hud.ShowMessage("VICTORY!", $"All enemies slain!  Gold: {playerStats.gold}  Lv.{playerStats.level}");
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        int AliveCount() => _enemies.FindAll(e => !e._stats().IsDead).Count;

        ResourceNode FindResourceAt(Vector2Int tile)
        {
            float ts = worldBuilder.tileSize;
            var   wp = new Vector3(tile.x * ts, 0, tile.y * ts);
            var hits = Physics.OverlapSphere(wp, 0.8f);
            foreach (var h in hits)
            {
                var n = h.GetComponentInParent<ResourceNode>();
                if (n != null) return n;
            }
            return null;
        }

        MerchantController FindMerchantAt(Vector2Int tile)
        {
            float ts = worldBuilder.tileSize;
            var   wp = new Vector3(tile.x * ts, 0, tile.y * ts);
            var hits = Physics.OverlapSphere(wp, 1.0f);
            foreach (var h in hits)
            {
                var m = h.GetComponentInParent<MerchantController>();
                if (m != null) return m;
            }
            return null;
        }
    }
}
