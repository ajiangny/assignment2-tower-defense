using System.Collections.Generic;
using UnityEngine;

namespace TowerDefense
{
    /// <summary>Fires at the enemy closest to the exit, leading it based on its velocity.</summary>
    public class Tower : MonoBehaviour
    {
        private static readonly List<Tower> active = new List<Tower>();
        public static IReadOnlyList<Tower> Active => active;

        [SerializeField] private TowerStats stats;
        [SerializeField] private Projectile projectilePrefab;
        [SerializeField] private Transform column;
        [SerializeField] private Transform head;
        [SerializeField] private Transform muzzle;
        [Tooltip("Stretch the column to the stats height. Off for modelled structures, which are built to height.")]
        [SerializeField] private bool sizeColumnFromStats = true;
        [Tooltip("Animator state played on every shot, when the unit has an Animator.")]
        [SerializeField] private string attackState = "Attack";

        private float cooldown;
        private Animator unitAnimator;
        private Transform unit;
        private Vector3 unitRestPosition;
        private float recoil;

        /// <summary>When false, aims at the target's current position. Used to measure predictive aim.</summary>
        public bool PredictiveAim { get; set; } = true;

        public TowerStats Stats => stats;
        public Collider[] Colliders { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => active.Clear();

        private void Awake()
        {
            if (sizeColumnFromStats)
            {
                // Build the tower to the height in its stats. Unity's cylinder is 2 units tall at scale 1.
                column.localPosition = Vector3.up * (stats.height / 2f);
                Vector3 columnScale = column.localScale;
                column.localScale = new Vector3(columnScale.x, stats.height / 2f, columnScale.z);
            }
            head.localPosition = Vector3.up * stats.height;
            unitAnimator = head.GetComponentInChildren<Animator>();
            if (unitAnimator != null)
            {
                unit = unitAnimator.transform;
                unitRestPosition = unit.localPosition;
            }

            Colliders = GetComponentsInChildren<Collider>();
        }

        private void OnEnable() => active.Add(this);
        private void OnDisable() => active.Remove(this);

        private void Update()
        {
            cooldown -= Time.deltaTime;

            // Small kick back on every shot so even towers without an attack clip read as firing.
            if (unit != null)
            {
                recoil = Mathf.MoveTowards(recoil, 0f, Time.deltaTime * 6f);
                unit.localPosition = unitRestPosition + Vector3.back * (0.12f * recoil * recoil);
            }

            Enemy target = FindTarget();
            if (target == null) return;

            Vector3 aimPoint = target.transform.position;
            if (PredictiveAim)
                Ballistics.TryIntercept(muzzle.position, aimPoint, target.Velocity, stats.projectileSpeed, out aimPoint);

            // The unit turns to face its target but stays upright; the shot itself is aimed in 3D.
            Vector3 facing = aimPoint - head.position;
            facing.y = 0f;
            if (facing.sqrMagnitude > 0.0001f) head.rotation = Quaternion.LookRotation(facing);

            if (cooldown <= 0f)
            {
                Fire(aimPoint);
                cooldown = 1f / stats.shotsPerSecond;
            }
        }

        private Enemy FindTarget()
        {
            // One leak ends the game, so always shoot the enemy closest to getting through.
            Enemy best = null;
            float sqrRange = stats.range * stats.range;
            foreach (Enemy enemy in Enemy.Active)
            {
                bool inRange = (enemy.transform.position - head.position).sqrMagnitude <= sqrRange;
                if (inRange && (best == null || enemy.PathProgress > best.PathProgress))
                    best = enemy;
            }
            return best;
        }

        private void Fire(Vector3 aimPoint)
        {
            Vector3 direction = (aimPoint - muzzle.position).normalized;
            if (unitAnimator != null && unitAnimator.HasState(0, Animator.StringToHash(attackState)))
                unitAnimator.Play(attackState, 0, 0f);
            recoil = 1f;
            GameAudio.Play(stats.fireSound, 0.55f);
            Projectile shot = Instantiate(projectilePrefab, muzzle.position, Quaternion.LookRotation(direction));
            shot.Launch(direction * stats.projectileSpeed, stats.damage, stats.splashRadius);
        }
    }
}
