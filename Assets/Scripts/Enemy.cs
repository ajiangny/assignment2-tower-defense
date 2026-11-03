using System.Collections.Generic;
using UnityEngine;

namespace TowerDefense
{
    /// <summary>Follows an EnemyPath as a kinematic rigidbody so projectiles collide with it.</summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Enemy : MonoBehaviour
    {
        private static readonly List<Enemy> active = new List<Enemy>();
        public static IReadOnlyList<Enemy> Active => active;

        [SerializeField] private EnemyStats stats;
        [SerializeField] private float heightAbovePath = 0.5f;

        private Rigidbody body;
        private EnemyPath path;
        private int nextWaypoint;
        private float hp;

        /// <summary>Current world-space velocity, for predictive aiming.</summary>
        public Vector3 Velocity { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => active.Clear();

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            hp = stats.maxHp;
        }

        private void OnEnable() => active.Add(this);
        private void OnDisable() => active.Remove(this);

        public void Begin(EnemyPath enemyPath)
        {
            path = enemyPath;
            nextWaypoint = 1;
            Vector3 start = PathPoint(0);
            transform.position = start;
            body.position = start;
        }

        private Vector3 PathPoint(int index) => path.GetPoint(index) + Vector3.up * heightAbovePath;

        private void FixedUpdate()
        {
            if (path == null) return;

            float remaining = stats.speed * Time.fixedDeltaTime;
            Vector3 position = body.position;
            Vector3 direction = Vector3.zero;

            // Carry leftover movement past a corner so speed stays constant through turns.
            while (remaining > 0f && nextWaypoint < path.Count)
            {
                Vector3 target = PathPoint(nextWaypoint);
                Vector3 toTarget = target - position;
                float distance = toTarget.magnitude;
                if (distance > remaining)
                {
                    direction = toTarget / distance;
                    position += direction * remaining;
                    remaining = 0f;
                }
                else
                {
                    position = target;
                    remaining -= distance;
                    nextWaypoint++;
                }
            }

            Velocity = direction * stats.speed;
            body.MovePosition(position);

            if (nextWaypoint >= path.Count)
            {
                if (GameManager.Instance != null) GameManager.Instance.GameOver();
                Destroy(gameObject);
            }
        }

        public void TakeDamage(float amount)
        {
            if (hp <= 0f) return;
            hp -= amount;
            if (hp <= 0f)
            {
                if (GameManager.Instance != null) GameManager.Instance.RegisterKill();
                Destroy(gameObject);
            }
        }
    }
}
