using UnityEngine;

namespace MournSpire.Enemy
{
    /// <summary>
    /// Self-moving projectile (orc axe, boss shadow bolt).
    /// Spawned and tracked by EnemyController._projectiles.
    /// </summary>
    public class Projectile : MonoBehaviour
    {
        public int   Damage   { get; private set; }
        public bool  HasHit   { get; set; }
        public bool  IsExpired => _ttl <= 0f;

        private Vector3 _velocity;
        private float   _spin;
        private float   _ttl;

        public static Projectile SpawnAxe(Vector3 start, Vector3 target,
                                          int damage, float speed, float spin, float ttl)
        {
            // Build a tiny axe from primitives
            var root   = new GameObject("Axe");
            var handle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            handle.transform.SetParent(root.transform);
            handle.transform.localScale    = new Vector3(0.07f, 0.35f, 0.07f);
            handle.transform.localPosition = Vector3.zero;
            DestroyImmediate(handle.GetComponent<Collider>());

            var blade = GameObject.CreatePrimitive(PrimitiveType.Cube);
            blade.transform.SetParent(root.transform);
            blade.transform.localScale    = new Vector3(0.30f, 0.20f, 0.07f);
            blade.transform.localPosition = new Vector3(0, 0.21f, 0);
            DestroyImmediate(blade.GetComponent<Collider>());

            return Init(root, start, target, damage, speed, spin, ttl);
        }

        public static Projectile SpawnShadowBolt(Vector3 start, Vector3 target,
                                                  int damage, float speed, float spin, float ttl)
        {
            var root = GameObject.CreatePrimitive(PrimitiveType.Cube);
            root.transform.localScale = Vector3.one * 0.38f;
            DestroyImmediate(root.GetComponent<Collider>());

            var rend = root.GetComponent<Renderer>();
            if (rend)
            {
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mat.SetColor("_BaseColor", new Color(0.4f, 0f, 0.8f));
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", new Color(0.27f, 0f, 0.67f) * 3f);
                rend.material = mat;
            }

            // Add a point light
            var light = root.AddComponent<Light>();
            light.type      = LightType.Point;
            light.color     = new Color(0.6f, 0f, 1f);
            light.intensity = 2.5f;
            light.range     = 5f;

            return Init(root, start, target, damage, speed, spin, ttl);
        }

        private static Projectile Init(GameObject go, Vector3 start, Vector3 target,
                                       int damage, float speed, float spin, float ttl)
        {
            go.transform.position = start + Vector3.up * 0.9f;
            var proj = go.AddComponent<Projectile>();
            proj.Damage = damage;
            proj._spin  = spin;
            proj._ttl   = ttl;

            var dir = (target - start).normalized;
            proj._velocity = new Vector3(dir.x, 0f, dir.z) * speed;
            return proj;
        }

        void Update()
        {
            if (HasHit || IsExpired) return;
            transform.position += _velocity * Time.deltaTime;
            transform.Rotate(Vector3.up, _spin * Time.deltaTime * Mathf.Rad2Deg);
            _ttl -= Time.deltaTime;
        }

        public void Tick(float delta) { } // Update handles itself; kept for API compat
    }
}
