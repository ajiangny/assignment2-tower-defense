using System.Collections.Generic;
using UnityEngine;

namespace TowerDefense
{
    /// <summary>Follows an EnemyPath as a kinematic rigidbody so projectiles collide with it.</summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Enemy : MonoBehaviour
    {
        private const float SplitSpawnGap = 0.25f;

        private static readonly List<Enemy> active = new List<Enemy>();
        public static IReadOnlyList<Enemy> Active => active;

        [SerializeField] private EnemyStats stats;
        [SerializeField] private float heightAbovePath = 0.5f;
        [Tooltip("Degrees per second the enemy turns to face where it is walking.")]
        [SerializeField] private float turnSpeed = 540f;

        private Rigidbody body;
        private EnemyPath path;
        private int nextWaypoint;
        private float hp;
        private float healthMultiplier = 1f;
        private float holdTime;
        private float slow;
        private float slowTimer;

        /// <summary>Raised on every hit that doesn't kill.</summary>
        public event System.Action Damaged;

        /// <summary>Raised once, just before the enemy is destroyed by damage.</summary>
        public event System.Action Died;

        public EnemyStats Stats => stats;
        public float Hp => hp;

        /// <summary>Full HP: the base stat times the wave's HP multiplier.</summary>
        public float MaxHp => stats.maxHp * healthMultiplier;

        /// <summary>Scales this enemy's HP for its wave. Call right after spawning, before it takes damage.</summary>
        public void SetHealthMultiplier(float multiplier)
        {
            healthMultiplier = Mathf.Max(0.1f, multiplier);
            hp = MaxHp;
        }

        /// <summary>Speed after any slow from a frost upgrade.</summary>
        public float CurrentSpeed => stats.speed * (1f - slow);
        public bool IsSlowed => slowTimer > 0f;

        /// <summary>Slows by a fraction for a while. The strongest current slow wins; heavy zombies resist part of it.</summary>
        public void ApplySlow(float amount, float duration)
        {
            amount *= 1f - stats.slowResistance;
            if (amount >= slow || slowTimer <= 0f) slow = amount;
            slowTimer = Mathf.Max(slowTimer, duration);
        }

        /// <summary>Current world-space velocity, for predictive aiming.</summary>
        public Vector3 Velocity { get; private set; }

        /// <summary>Distance walked along the path. Towers use it to target the enemy closest to the exit.</summary>
        public float PathProgress { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => active.Clear();

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            hp = stats.maxHp;
            if (GetComponent<HealthBar>() == null) gameObject.AddComponent<HealthBar>();
        }

        private void OnEnable() => active.Add(this);
        private void OnDisable() => active.Remove(this);

        public void Begin(EnemyPath enemyPath)
        {
            path = enemyPath;
            nextWaypoint = 1;
            GameAudio.Play(stats.spawn, 1f, 0f);
            Place(PathPoint(0));
            Vector3 heading = PathPoint(1) - PathPoint(0);
            heading.y = 0f;
            if (heading.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(heading);
                body.rotation = transform.rotation;
            }
        }

        private void BeginFrom(Enemy parent, float delay)
        {
            path = parent.path;
            SetHealthMultiplier(parent.healthMultiplier);
            nextWaypoint = parent.nextWaypoint;
            PathProgress = parent.PathProgress;
            holdTime = delay;
            Place(parent.body.position);
            transform.rotation = parent.body.rotation;
            body.rotation = parent.body.rotation;
        }

        private void Place(Vector3 position)
        {
            transform.position = position;
            body.position = position;
        }

        private Vector3 PathPoint(int index) => path.GetPoint(index) + Vector3.up * heightAbovePath;

        private void FixedUpdate()
        {
            if (path == null) return;

            if (holdTime > 0f)
            {
                holdTime -= Time.fixedDeltaTime;
                Velocity = Vector3.zero;
                return;
            }

            if (slowTimer > 0f)
            {
                slowTimer -= Time.fixedDeltaTime;
                if (slowTimer <= 0f) slow = 0f;
            }

            float speed = CurrentSpeed;
            float remaining = speed * Time.fixedDeltaTime;
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
                    PathProgress += remaining;
                    remaining = 0f;
                }
                else
                {
                    position = target;
                    PathProgress += distance;
                    remaining -= distance;
                    nextWaypoint++;
                }
            }

            Velocity = direction * speed;
            body.MovePosition(position);

            Vector3 flat = new Vector3(direction.x, 0f, direction.z);
            if (flat.sqrMagnitude > 0.0001f)
            {
                Quaternion facing = Quaternion.LookRotation(flat);
                body.MoveRotation(Quaternion.RotateTowards(body.rotation, facing, turnSpeed * Time.fixedDeltaTime));
            }

            if (nextWaypoint >= path.Count)
            {
                if (GameManager.Instance != null) GameManager.Instance.GameOver(stats.displayName);
                Destroy(gameObject);
            }
        }

        public void TakeDamage(float amount)
        {
            if (hp <= 0f) return;
            hp -= Mathf.Max(amount - stats.armor, 1f);
            if (hp > 0f)
            {
                Damaged?.Invoke();
                GameAudio.PlayAny(stats.hurt, 0.5f);
                return;
            }
            GameAudio.PlayAny(stats.death, 0.7f);

            if (GameManager.Instance != null) GameManager.Instance.RegisterKill();
            if (Economy.Instance != null) Economy.Instance.Add(stats.goldReward);

            for (int i = 0; i < stats.splitCount; i++)
            {
                Enemy child = Instantiate(stats.splitInto, body.position, Quaternion.identity);
                child.BeginFrom(this, i * SplitSpawnGap);
            }

            Died?.Invoke();
            Destroy(gameObject);
        }
    }
}
