using System.Collections.Generic;
using UnityEngine;

namespace TowerDefense
{
    /// <summary>Aims at the nearest enemy in range and fires at the rate set in its TowerStats.</summary>
    public class Tower : MonoBehaviour
    {
        private static readonly List<Tower> active = new List<Tower>();
        public static IReadOnlyList<Tower> Active => active;

        [SerializeField] private TowerStats stats;
        [SerializeField] private Projectile projectilePrefab;
        [SerializeField] private Transform column;
        [SerializeField] private Transform head;
        [SerializeField] private Transform muzzle;

        private float cooldown;

        public Collider[] Colliders { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => active.Clear();

        private void Awake()
        {
            // Build the tower to the height in its stats. Unity's cylinder is 2 units tall at scale 1.
            column.localPosition = Vector3.up * (stats.height / 2f);
            Vector3 columnScale = column.localScale;
            column.localScale = new Vector3(columnScale.x, stats.height / 2f, columnScale.z);
            head.localPosition = Vector3.up * stats.height;

            Colliders = GetComponentsInChildren<Collider>();
        }

        private void OnEnable() => active.Add(this);
        private void OnDisable() => active.Remove(this);

        private void Update()
        {
            cooldown -= Time.deltaTime;

            Enemy target = FindTarget();
            if (target == null) return;

            Vector3 aimPoint = target.transform.position;
            head.rotation = Quaternion.LookRotation(aimPoint - head.position);

            if (cooldown <= 0f)
            {
                Fire(aimPoint);
                cooldown = 1f / stats.shotsPerSecond;
            }
        }

        private Enemy FindTarget()
        {
            Enemy nearest = null;
            float bestSqr = stats.range * stats.range;
            foreach (Enemy enemy in Enemy.Active)
            {
                float sqr = (enemy.transform.position - head.position).sqrMagnitude;
                if (sqr <= bestSqr)
                {
                    bestSqr = sqr;
                    nearest = enemy;
                }
            }
            return nearest;
        }

        private void Fire(Vector3 aimPoint)
        {
            Vector3 direction = (aimPoint - muzzle.position).normalized;
            Projectile shot = Instantiate(projectilePrefab, muzzle.position, Quaternion.LookRotation(direction));
            shot.Launch(direction * stats.projectileSpeed, stats.damage);
        }
    }
}
