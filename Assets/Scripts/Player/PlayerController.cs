using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using MournSpire.World;
using MournSpire.Combat;

namespace MournSpire.Player
{
    /// <summary>
    /// Tile-based player movement, attack, block, and interact.
    /// Requires: PlayerStats, WorldBuilder (found via GameManager).
    /// </summary>
    [RequireComponent(typeof(PlayerStats))]
    public class PlayerController : MonoBehaviour
    {
        // ── Tuning ───────────────────────────────────────────────────────────
        [Header("Movement")]
        public float moveCooldown    = 0.16f;
        public float moveSmoothing   = 13f;

        [Header("Combat")]
        public float attackStaminaCost  = 30f;
        public float blockDrainPerSec   = 12f;
        public float blockHitCost       = 22f;
        public float blockDamageReduce  = 0.20f;  // fraction that gets through
        public float regenFree          = 22f;     // SP/s when idle
        public float regenBlocking      = 4f;      // SP/s while blocking
        public float regenDelay         = 1.0f;
        public float staggerDuration    = 1.2f;
        public float guardLockout       = 2.0f;

        // ── State ────────────────────────────────────────────────────────────
        public Vector2Int TilePos  { get; private set; }
        public Vector2Int Facing   { get; private set; } = Vector2Int.up;

        public bool IsBlocking  { get; private set; }
        public bool IsStaggered { get; private set; }
        public bool IsAttacking { get; private set; }

        // ── Events (consumed by GameManager / CombatSystem) ─────────────────
        public System.Action<Vector2Int>             OnMovedTo;
        public System.Action<Vector2Int, Vector2Int> OnBumpAttack;   // (fromTile, toTile)
        public System.Action                         OnInteract;
        public System.Action                         OnAttackSwing;

        // ── Private ──────────────────────────────────────────────────────────
        private PlayerStats      _stats;
        private WorldBuilder     _world;
        private Vector3          _targetWorldPos;
        private float            _moveCooldownTimer;
        private float            _staggerTimer;
        private float            _guardLockTimer;
        private float            _spRegenDelayTimer;
        private bool             _spEmpty;
        private bool             _eWasDown;
        private bool             _spaceWasDown;
        private bool             _interactLock;

        // ── Lifecycle ────────────────────────────────────────────────────────

        void Awake()
        {
            _stats = GetComponent<PlayerStats>();
        }

        public void Init(WorldBuilder world, Vector2Int startTile)
        {
            _world          = world;
            TilePos         = startTile;
            _targetWorldPos = world.TileToWorld(startTile);
            transform.position = _targetWorldPos;
        }

        void Update()
        {
            if (_stats.IsDead) return;

            TickTimers();
            TickStamina();
            TickMovement();
            TickAttack();
            TickInteract();

            // Smooth lerp to target tile
            transform.position = Vector3.Lerp(
                transform.position, _targetWorldPos, Time.deltaTime * moveSmoothing);
        }

        // ── Ticks ────────────────────────────────────────────────────────────

        void TickTimers()
        {
            _moveCooldownTimer  = Mathf.Max(0f, _moveCooldownTimer  - Time.deltaTime);
            _staggerTimer       = Mathf.Max(0f, _staggerTimer       - Time.deltaTime);
            _guardLockTimer     = Mathf.Max(0f, _guardLockTimer     - Time.deltaTime);
            if (_staggerTimer <= 0f) IsStaggered = false;
        }

        void TickStamina()
        {
            bool wantsBlock = Keyboard.current.qKey.isPressed && _guardLockTimer <= 0f && !IsStaggered;
            IsBlocking = wantsBlock;

            if (_spEmpty)
            {
                _spRegenDelayTimer -= Time.deltaTime;
                if (_spRegenDelayTimer <= 0f) _spEmpty = false;
            }
            else
            {
                float rate = IsBlocking ? regenBlocking : regenFree;
                _stats.RestoreStamina(rate * Time.deltaTime);
            }

            if (IsBlocking)
                DrainSP(blockDrainPerSec * Time.deltaTime);
        }

        void TickMovement()
        {
            if (IsStaggered || _moveCooldownTimer > 0f) return;

            var kb = Keyboard.current;
            int dx = 0, dz = 0;
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed)    dz = -1;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed)  dz =  1;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed)  dx = -1;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) dx =  1;

            if (dx == 0 && dz == 0) return;

            var dir = new Vector2Int(dx, dz);
            Facing = dir;
            transform.rotation = Quaternion.LookRotation(new Vector3(dx, 0, dz));

            var next = TilePos + dir;
            // Bump combat check is handled by GameManager via OnBumpAttack
            if (!_world.IsWalkable(next.x, next.y))
            {
                _moveCooldownTimer = moveCooldown;
                return;
            }

            // Signal bump-attack if something is on next tile — GameManager handles lookup
            OnBumpAttack?.Invoke(TilePos, next);

            TilePos         = next;
            _targetWorldPos = _world.TileToWorld(next);
            _moveCooldownTimer = moveCooldown;
            OnMovedTo?.Invoke(TilePos);
        }

        void TickAttack()
        {
            if (IsStaggered || IsBlocking) return;
            bool spaceNow = Keyboard.current.spaceKey.isPressed;
            if (spaceNow && !_spaceWasDown)
            {
                if (_stats.stamina < attackStaminaCost)
                {
                    OnAttackSwing?.Invoke(); // fires "too exhausted" log upstream
                }
                else
                {
                    DrainSP(attackStaminaCost);
                    OnAttackSwing?.Invoke();
                }
            }
            _spaceWasDown = spaceNow;
        }

        void TickInteract()
        {
            bool eNow = Keyboard.current.eKey.isPressed;
            if (eNow && !_eWasDown) OnInteract?.Invoke();
            _eWasDown = eNow;
        }

        // ── Public: take damage (called by CombatSystem) ─────────────────────

        public struct DamageResult
        {
            public int  Damage;
            public bool Guarded;
            public bool GuardBroken;
        }

        public DamageResult ReceiveDamage(int rawDamage)
        {
            if (IsStaggered)
            {
                int dmg = _stats.TakeDamage(rawDamage);
                return new DamageResult { Damage = dmg };
            }

            if (IsBlocking && _guardLockTimer <= 0f)
            {
                if (_stats.stamina > 0f)
                {
                    int dmg = _stats.TakeDamage(Mathf.RoundToInt(rawDamage * blockDamageReduce));
                    DrainSP(blockHitCost);
                    bool broke = _stats.stamina <= 0f;
                    if (broke) TriggerGuardBreak();
                    return new DamageResult { Damage = dmg, Guarded = true, GuardBroken = broke };
                }
                else
                {
                    int dmg = _stats.TakeDamage(rawDamage);
                    TriggerGuardBreak();
                    return new DamageResult { Damage = dmg, GuardBroken = true };
                }
            }

            int d = _stats.TakeDamage(rawDamage);
            if (_stats.stamina <= 0f) TriggerStagger(staggerDuration * 0.8f);
            return new DamageResult { Damage = d };
        }

        // ── Private helpers ───────────────────────────────────────────────────

        void DrainSP(float amount)
        {
            _stats.DrainStamina(amount);
            if (_stats.stamina <= 0f)
            {
                _spEmpty           = true;
                _spRegenDelayTimer = regenDelay;
            }
        }

        void TriggerGuardBreak()
        {
            IsBlocking      = false;
            IsStaggered     = true;
            _staggerTimer   = staggerDuration;
            _guardLockTimer = guardLockout;
            _spEmpty        = true;
            _spRegenDelayTimer = regenDelay * 2f;
        }

        void TriggerStagger(float dur)
        {
            IsStaggered   = true;
            _staggerTimer = dur;
        }
    }
}
