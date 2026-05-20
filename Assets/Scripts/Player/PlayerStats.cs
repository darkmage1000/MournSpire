using UnityEngine;
using UnityEngine.Events;

namespace MournSpire.Player
{
    /// <summary>
    /// Holds all RPG stats for the player. Pure data — no movement or input here.
    /// </summary>
    public class PlayerStats : MonoBehaviour
    {
        // ── Base stats ───────────────────────────────────────────────────────
        [Header("Base Stats")]
        public int   maxHp      = 100;
        public int   hp         = 100;
        public int   atk        = 10;
        public int   def        = 3;
        public int   level      = 1;
        public int   gold       = 0;
        public int   xp         = 0;
        public int   xpToNext   = 30;

        [Header("Stamina")]
        public float maxStamina = 100f;
        public float stamina    = 100f;

        // ── Events ───────────────────────────────────────────────────────────
        [HideInInspector] public UnityEvent<int>   OnHpChanged;
        [HideInInspector] public UnityEvent<float> OnStaminaChanged;
        [HideInInspector] public UnityEvent        OnLevelUp;
        [HideInInspector] public UnityEvent        OnDeath;

        public bool IsDead => hp <= 0;

        // ── Damage / Heal ────────────────────────────────────────────────────

        /// <summary>Apply damage after defense. Returns actual damage dealt.</summary>
        public int TakeDamage(int raw)
        {
            int dmg = Mathf.Max(1, raw - def);
            hp = Mathf.Max(0, hp - dmg);
            OnHpChanged?.Invoke(hp);
            if (hp <= 0) OnDeath?.Invoke();
            return dmg;
        }

        public void Heal(int amount)
        {
            hp = Mathf.Min(maxHp, hp + amount);
            OnHpChanged?.Invoke(hp);
        }

        // ── Stamina ──────────────────────────────────────────────────────────

        public void DrainStamina(float amount)
        {
            stamina = Mathf.Max(0f, stamina - amount);
            OnStaminaChanged?.Invoke(stamina);
        }

        public void RestoreStamina(float amount)
        {
            stamina = Mathf.Min(maxStamina, stamina + amount);
            OnStaminaChanged?.Invoke(stamina);
        }

        // ── XP / Level ───────────────────────────────────────────────────────

        /// <summary>Add XP; returns true if leveled up.</summary>
        public bool GainXp(int amount)
        {
            xp += amount;
            if (xp < xpToNext) return false;

            xp       -= xpToNext;
            xpToNext  = Mathf.RoundToInt(xpToNext * 1.6f);
            level++;
            maxHp       += 20;
            hp           = maxHp;
            atk         += 3;
            def         += 1;
            maxStamina  += 10f;
            stamina      = maxStamina;
            OnLevelUp?.Invoke();
            return true;
        }
    }
}
