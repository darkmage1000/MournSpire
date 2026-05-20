using UnityEngine;
using UnityEngine.Events;

namespace MournSpire.Items
{
    /// <summary>
    /// Persistent inventory that survives zone changes.
    /// Attach to the GameManager or a DontDestroyOnLoad object.
    /// </summary>
    public class Inventory : MonoBehaviour
    {
        [Header("Resources")]
        public int wood  = 0;
        public int stone = 0;
        public int iron  = 0;

        [Header("Consumables")]
        public int potions = 0;
        public int tonics  = 0;

        // ── Events ─────────────────────────────────────────────────────────────
        [HideInInspector] public UnityEvent OnChanged;

        // ── Resource API ───────────────────────────────────────────────────────

        public void AddResource(string type, int amount = 1)
        {
            switch (type)
            {
                case "tree": case "wood":  wood  += amount; break;
                case "rock": case "stone": stone += amount; break;
                case "iron":               iron  += amount; break;
            }
            OnChanged?.Invoke();
        }

        public void AddPotion() { potions++; OnChanged?.Invoke(); }
        public void AddTonic()  { tonics++;  OnChanged?.Invoke(); }

        // ── Consumable Use ─────────────────────────────────────────────────────

        public bool UsePotion(Player.PlayerStats stats)
        {
            if (potions <= 0 || stats.hp >= stats.maxHp) return false;
            potions--;
            stats.Heal(50);
            OnChanged?.Invoke();
            return true;
        }

        public bool UseTonic(Player.PlayerStats stats)
        {
            if (tonics <= 0 || stats.stamina >= stats.maxStamina) return false;
            tonics--;
            stats.RestoreStamina(60f);
            OnChanged?.Invoke();
            return true;
        }
    }
}
