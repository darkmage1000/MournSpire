using UnityEngine;
using MournSpire.World;
using MournSpire.Player;

namespace MournSpire.Enemy
{
    /// <summary>
    /// The Lich King. Extends EnemyController with:
    ///  - Encounter trigger (proximity)
    ///  - Phase 2 at 50% HP (faster, harder hits)
    ///  - Power strike every 3rd melee
    ///  - Shadow bolt ranged attack
    /// </summary>
    public class BossController : EnemyController
    {
        [Header("Boss Settings")]
        public float encounterRange  = 10f;  // Manhattan distance
        public int   phase2Atk       = 28;
        public float phase2Speed     = 0.55f;
        public int   powerStrikeEvery = 3;

        public System.Action OnEncounter;
        public System.Action OnPhase2;
        public System.Action<int> OnPowerStrike;

        public bool Triggered { get; private set; }
        public bool InPhase2  { get; private set; }

        private int _atkCount;

        protected override void Update()
        {
            if (!Triggered && !_stats.IsDead)
            {
                if (ManhattanDist(_player.TilePos) <= encounterRange)
                {
                    Triggered = true;
                    OnEncounter?.Invoke();
                }
            }

            base.Update();

            // Phase 2 check
            if (!InPhase2 && !_stats.IsDead &&
                (float)_stats.Hp / _stats.MaxHp <= 0.5f)
            {
                EnterPhase2();
            }
        }

        protected override void Act()
        {
            if (!Triggered) return;

            int dist = ManhattanDist(_player.TilePos);

            if (dist == 1)
            {
                _atkCount++;
                if (_atkCount % powerStrikeEvery == 0)
                {
                    int raw = Mathf.RoundToInt(_stats.Atk * 2f + Random.value * 8f);
                    var r   = _player.ReceiveDamage(raw);
                    OnPowerStrike?.Invoke(r.Damage);
                }
                else
                {
                    MeleeAttack();
                }
            }
            else if (dist >= 3 && dist <= 9 && Random.value < 0.35f)
            {
                FireShadowBolt();
            }
            else
            {
                StepToward();
            }
        }

        void EnterPhase2()
        {
            InPhase2 = true;
            _stats.OverrideAtk(phase2Atk);
            _stats.OverrideSpeed(phase2Speed);
            _actionTimer = Mathf.Min(_actionTimer, phase2Speed);
            OnPhase2?.Invoke();
        }

        void FireShadowBolt()
        {
            var start  = _world.TileToWorld(TilePos);
            var target = _world.TileToWorld(_player.TilePos);
            int damage = Mathf.RoundToInt(_stats.Atk * 0.85f + Random.value * 8f);

            var proj = Projectile.SpawnShadowBolt(start, target, damage, 5f, 4f, 3.5f);
            _projectiles.Add(proj);
        }
    }
}
