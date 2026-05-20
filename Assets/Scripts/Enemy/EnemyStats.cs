using UnityEngine;
using UnityEngine.Events;

namespace MournSpire.Enemy
{
    public enum EnemyType { Goblin, Skeleton, Orc, Boss }

    [CreateAssetMenu(fileName = "EnemyStats", menuName = "MournSpire/Enemy Stats")]
    public class EnemyStatsDef : ScriptableObject
    {
        public EnemyType type;
        public int   hp;
        public int   atk;
        public int   def;
        public float speed;   // seconds per action
        public int   xpValue;
        public int   goldMin;
        public int   goldMax;
    }

    /// <summary>
    /// Runtime enemy stats component — clones values from a ScriptableObject definition.
    /// </summary>
    public class EnemyStats : MonoBehaviour
    {
        [Header("Definition")]
        public EnemyStatsDef def;

        // Runtime values
        public int   MaxHp  { get; private set; }
        public int   Hp     { get; private set; }
        public int   Atk    { get; private set; }
        public int   Def    { get; private set; }
        public float Speed  { get; private set; }
        public int   XpValue{ get; private set; }
        public bool  IsDead => Hp <= 0;

        public UnityEvent<int> OnHpChanged;
        public UnityEvent      OnDeath;

        public int GoldDrop =>
            def == null ? 0 : Random.Range(def.goldMin, def.goldMax + 1);

        void Awake()
        {
            if (def == null) return;
            MaxHp    = def.hp;
            Hp       = def.hp;
            Atk      = def.atk;
            Def      = def.def;
            Speed    = def.speed;
            XpValue  = def.xpValue;
        }

        public int TakeDamage(int raw)
        {
            if (IsDead) return 0;
            int dmg = Mathf.Max(1, raw - Def);
            Hp = Mathf.Max(0, Hp - dmg);
            OnHpChanged?.Invoke(Hp);
            if (Hp <= 0) OnDeath?.Invoke();
            return dmg;
        }

        /// <summary>Called by Boss on phase-2 to override stats at runtime.</summary>
        public void OverrideAtk(int newAtk) => Atk = newAtk;
        public void OverrideSpeed(float s)  => Speed = s;
    }
}
