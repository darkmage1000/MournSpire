using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MournSpire.World;
using MournSpire.Player;

namespace MournSpire.Enemy
{
    /// <summary>
    /// AI brain for standard enemies (Goblin, Skeleton, Orc).
    /// Boss inherits from this and overrides _Act().
    /// </summary>
    [RequireComponent(typeof(EnemyStats))]
    public class EnemyController : MonoBehaviour
    {
        [Header("Tile Position")]
        public Vector2Int TilePos;

        // Callbacks — set by GameManager
        public System.Action<int>  OnMeleeHit;
        public System.Action<int>  OnProjectileHit;
        public System.Action       OnDied;

        // ── Protected refs ────────────────────────────────────────────────────
        protected EnemyStats          _stats;
        protected WorldBuilder        _world;
        protected PlayerController    _player;
        protected PlayerStats         _playerStats;
        protected List<EnemyController> _allEnemies;

        // ── State ─────────────────────────────────────────────────────────────
        protected float   _actionTimer;
        protected bool    _shieldUp;
        protected float   _shieldTimer;
        public    bool    LastHitBlocked { get; protected set; }

        protected readonly List<Projectile> _projectiles = new();

        // ── Lifecycle ─────────────────────────────────────────────────────────

        protected virtual void Awake()
        {
            _stats = GetComponent<EnemyStats>();
            _stats.OnDeath.AddListener(HandleDeath);
        }

        public virtual void Init(WorldBuilder world, PlayerController player,
                                 PlayerStats playerStats, List<EnemyController> all)
        {
            _world       = world;
            _player      = player;
            _playerStats = playerStats;
            _allEnemies  = all;
            _actionTimer = Random.Range(0f, _stats.Speed);
        }

        protected virtual void Update()
        {
            if (_stats.IsDead)
            {
                TickProjectiles();
                return;
            }

            // Smooth world-space lerp
            transform.position = Vector3.Lerp(
                transform.position,
                _world.TileToWorld(TilePos),
                Time.deltaTime * 10f);

            // Face player
            var dir = _player.TilePos - TilePos;
            if (dir != Vector2Int.zero)
                transform.rotation = Quaternion.LookRotation(new Vector3(dir.x, 0, dir.y));

            // Shield timer
            if (_shieldUp)
            {
                _shieldTimer -= Time.deltaTime;
                if (_shieldTimer <= 0f) _shieldUp = false;
            }

            TickProjectiles();

            _actionTimer -= Time.deltaTime;
            if (_actionTimer <= 0f)
            {
                _actionTimer = _stats.Speed;
                Act();
            }
        }

        // ── AI ────────────────────────────────────────────────────────────────

        protected virtual void Act()
        {
            int dist = ManhattanDist(_player.TilePos);
            var type = _stats.def > 0 ? _stats.def : 1; // placeholder

            string etype = _stats.def >= 5 ? "orc"
                         : _stats.MaxHp <= 35 ? "goblin"
                         : "skeleton"; // rough heuristic; real impl uses EnemyType enum

            switch (_stats.def)
            {
                case 1: ActDefault(dist);   break; // goblin
                case 2: ActSkeleton(dist);  break;
                case 5: ActOrc(dist);       break;
                default: ActDefault(dist);  break;
            }
        }

        protected void ActDefault(int dist)
        {
            if (dist == 1) MeleeAttack();
            else if (dist <= 9) StepToward();
            else if (Random.value < 0.25f) Wander();
        }

        protected void ActSkeleton(int dist)
        {
            if (dist == 1)
            {
                _shieldUp = false;
                MeleeAttack();
            }
            else if (dist <= 9)
            {
                StepToward();
                if (!_shieldUp && Random.value < 0.45f)
                    RaiseShield(0.9f + Random.value * 0.6f);
            }
            else if (Random.value < 0.25f)
            {
                Wander();
            }
        }

        protected void ActOrc(int dist)
        {
            if (dist == 1) MeleeAttack();
            else if (dist >= 3 && dist <= 8 && Random.value < 0.42f) ThrowAxe();
            else if (dist <= 9) StepToward();
            else if (Random.value < 0.25f) Wander();
        }

        // ── Combat helpers ────────────────────────────────────────────────────

        protected void MeleeAttack()
        {
            var result = _player.ReceiveDamage(_stats.Atk);
            OnMeleeHit?.Invoke(result.Damage);
        }

        protected void RaiseShield(float dur)
        {
            _shieldUp    = true;
            _shieldTimer = dur;
        }

        public virtual int ReceiveDamage(int raw)
        {
            LastHitBlocked = false;
            if (_shieldUp && _stats.MaxHp <= 35) // skeleton-tier shield
            {
                LastHitBlocked = true;
                return 0;
            }
            return _stats.TakeDamage(raw);
        }

        // ── Projectile: Orc axe ───────────────────────────────────────────────

        protected void ThrowAxe()
        {
            var start  = _world.TileToWorld(TilePos);
            var target = _world.TileToWorld(_player.TilePos);
            int damage = Mathf.Max(4, Mathf.RoundToInt(_stats.Atk * 0.70f + Random.value * 4f));

            var proj = Projectile.SpawnAxe(start, target, damage, 7.5f, 10f, 2.5f);
            _projectiles.Add(proj);
        }

        protected void TickProjectiles()
        {
            for (int i = _projectiles.Count - 1; i >= 0; i--)
            {
                var p = _projectiles[i];
                if (p == null) { _projectiles.RemoveAt(i); continue; }

                p.Tick(Time.deltaTime);

                if (!_playerStats.IsDead)
                {
                    float dx = p.transform.position.x - _player.transform.position.x;
                    float dz = p.transform.position.z - _player.transform.position.z;
                    if (dx * dx + dz * dz < 1.3f * 1.3f && !p.HasHit)
                    {
                        p.HasHit = true;
                        var result = _player.ReceiveDamage(p.Damage);
                        OnProjectileHit?.Invoke(result.Damage);
                    }
                }

                if (p.HasHit || p.IsExpired)
                {
                    Destroy(p.gameObject);
                    _projectiles.RemoveAt(i);
                }
            }
        }

        // ── Movement ──────────────────────────────────────────────────────────

        protected void StepToward()
        {
            var step = BFS(_player.TilePos);
            if (step.HasValue)
                TilePos = step.Value;
        }

        protected void Wander()
        {
            var dirs = new[] {
                Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right
            };
            var d   = dirs[Random.Range(0, 4)];
            var n   = TilePos + d;
            if (_world.IsWalkable(n.x, n.y) && !TileOccupied(n))
                TilePos = n;
        }

        // ── BFS pathfinding ───────────────────────────────────────────────────

        protected Vector2Int? BFS(Vector2Int goal, int maxDepth = 12)
        {
            var dirs  = new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
            var seen  = new HashSet<Vector2Int> { TilePos };
            var queue = new Queue<(Vector2Int pos, Vector2Int? first)>();
            queue.Enqueue((TilePos, null));

            for (int depth = 0; depth < maxDepth && queue.Count > 0; depth++)
            {
                int count = queue.Count;
                for (int i = 0; i < count; i++)
                {
                    var (cur, first) = queue.Dequeue();
                    foreach (var d in dirs)
                    {
                        var next = cur + d;
                        if (seen.Contains(next)) continue;
                        seen.Add(next);

                        var step = first ?? next;
                        if (next == goal) return step;
                        if (!_world.IsWalkable(next.x, next.y)) continue;
                        if (TileOccupied(next)) continue;

                        queue.Enqueue((next, step));
                    }
                }
            }
            return null;
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        protected int ManhattanDist(Vector2Int to)
            => Mathf.Abs(to.x - TilePos.x) + Mathf.Abs(to.y - TilePos.y);

        protected bool TileOccupied(Vector2Int t)
        {
            foreach (var e in _allEnemies)
                if (e != this && !e._stats.IsDead && e.TilePos == t) return true;
            return false;
        }

        void HandleDeath()
        {
            _shieldUp = false;
            OnDied?.Invoke();
        }
    }
}
