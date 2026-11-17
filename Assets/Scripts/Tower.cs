using System.Collections.Generic;
using UnityEngine;

namespace TowerDefense
{
    /// <summary>
    /// Fires at the enemy closest to the exit, leading it based on its velocity.
    /// Has two upgrade paths (see <see cref="TowerStats.paths"/>): one can reach tier 3, the other stops at tier 1.
    /// </summary>
    public class Tower : MonoBehaviour
    {
        private static readonly List<Tower> active = new List<Tower>();
        public static IReadOnlyList<Tower> Active => active;

        private const int RingSegments = 48;
        private const float FanDegrees = 9f;

        [SerializeField] private TowerStats stats;
        [SerializeField] private Projectile projectilePrefab;
        [SerializeField] private Transform column;
        [SerializeField] private Transform head;
        [SerializeField] private Transform muzzle;
        [Tooltip("Stretch the column to the stats height. Off for modelled structures, which are built to height.")]
        [SerializeField] private bool sizeColumnFromStats = true;
        [Tooltip("Animator state played on every shot, when the unit has an Animator.")]
        [SerializeField] private string attackState = "Attack";
        [Tooltip("Radius of the coloured ring drawn around the base once the tower is upgraded.")]
        [SerializeField] private float baseRingRadius = 2.3f;

        private readonly int[] tiers = new int[2];
        private readonly List<Enemy> targets = new List<Enemy>();
        private float cooldown;
        private Animator unitAnimator;
        private Transform unit;
        private Vector3 unitRestPosition;
        private Vector3 unitRestScale;
        private float recoil;
        private LineRenderer baseRing;
        private ParticleSystem aura;

        /// <summary>When false, aims at the target's current position. Used to measure predictive aim.</summary>
        public bool PredictiveAim { get; set; } = true;

        public TowerStats Stats => stats;
        public Collider[] Colliders { get; private set; }

        // Effective stats: base stats plus every bought tier.
        public float Damage { get; private set; }
        public float ShotsPerSecond { get; private set; }
        public float Range { get; private set; }
        public float SplashRadius { get; private set; }
        public float ProjectileSpeed { get; private set; }
        public int ExtraShots { get; private set; }
        public int Pierce { get; private set; }
        public float Slow { get; private set; }
        public float SlowDuration { get; private set; }

        /// <summary>Gold spent on this tower, including upgrades.</summary>
        public int Invested { get; private set; }
        public int SellValue => Mathf.RoundToInt(Invested * TowerStats.SellRefund);

        /// <summary>Colour of the path with the most tiers, or clear before any upgrade.</summary>
        public Color Signature { get; private set; } = Color.clear;

        public int PathCount => stats.paths != null ? Mathf.Min(stats.paths.Length, tiers.Length) : 0;
        public int Tier(int path) => tiers[path];

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
                unitRestScale = unit.localScale;
            }

            Colliders = GetComponentsInChildren<Collider>();
            Invested = stats.cost;
            Recalculate();
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
            if (baseRing != null) baseRing.transform.Rotate(0f, 25f * Time.deltaTime, 0f, Space.World);

            FindTargets();
            if (targets.Count == 0) return;

            Vector3 aimPoint = AimPoint(targets[0]);

            // The unit turns to face its target but stays upright; the shot itself is aimed in 3D.
            Vector3 facing = aimPoint - head.position;
            facing.y = 0f;
            if (facing.sqrMagnitude > 0.0001f) head.rotation = Quaternion.LookRotation(facing);

            if (cooldown <= 0f)
            {
                Fire(aimPoint);
                cooldown = 1f / ShotsPerSecond;
            }
        }

        private Vector3 AimPoint(Enemy target)
        {
            Vector3 aimPoint = target.transform.position;
            if (PredictiveAim)
                Ballistics.TryIntercept(muzzle.position, aimPoint, target.Velocity, ProjectileSpeed, out aimPoint);
            return aimPoint;
        }

        /// <summary>Every enemy in range, closest to the exit first. One leak ends the game.</summary>
        private void FindTargets()
        {
            targets.Clear();
            float sqrRange = Range * Range;
            foreach (Enemy enemy in Enemy.Active)
            {
                if ((enemy.transform.position - head.position).sqrMagnitude <= sqrRange)
                    targets.Add(enemy);
            }
            if (targets.Count > 1) targets.Sort((a, b) => b.PathProgress.CompareTo(a.PathProgress));
        }

        private void Fire(Vector3 aimPoint)
        {
            if (unitAnimator != null && unitAnimator.HasState(0, Animator.StringToHash(attackState)))
                unitAnimator.Play(attackState, 0, 0f);
            recoil = 1f;
            GameAudio.Play(stats.fireSound, 0.55f);

            Launch((aimPoint - muzzle.position).normalized);

            // Extra shots go to the next zombies in line; with nobody else in range they fan out around the first.
            for (int i = 1; i <= ExtraShots; i++)
            {
                Vector3 direction;
                if (i < targets.Count)
                {
                    direction = (AimPoint(targets[i]) - muzzle.position).normalized;
                }
                else
                {
                    float angle = FanDegrees * ((i + 1) / 2) * (i % 2 == 1 ? 1f : -1f);
                    direction = Quaternion.AngleAxis(angle, Vector3.up) * (aimPoint - muzzle.position).normalized;
                }
                Launch(direction);
            }
        }

        private void Launch(Vector3 direction)
        {
            Projectile shot = Instantiate(projectilePrefab, muzzle.position, Quaternion.LookRotation(direction));
            shot.Launch(direction * ProjectileSpeed, new ShotInfo
            {
                Damage = Damage,
                Splash = SplashRadius,
                Pierce = Pierce,
                Slow = Slow,
                SlowDuration = SlowDuration,
                Tint = Signature,
            });
        }

        // ---------- Upgrades ----------

        public UpgradeTier NextTier(int path)
        {
            if (path < 0 || path >= PathCount) return null;
            UpgradeTier[] pathTiers = stats.paths[path].tiers;
            return tiers[path] < pathTiers.Length ? pathTiers[tiers[path]] : null;
        }

        /// <summary>Highest tier this path may reach, given the other path (the 3/1 cross-path rule).</summary>
        public int TierCap(int path)
        {
            int other = tiers[1 - path];
            int length = stats.paths[path].tiers.Length;
            return Mathf.Min(length, other > TowerStats.CrossPathCap ? TowerStats.CrossPathCap : TowerStats.MaxTier);
        }

        /// <summary>The tier is locked by the other path, not just unaffordable.</summary>
        public bool IsLocked(int path) => tiers[path] >= TierCap(path) && tiers[path] < stats.paths[path].tiers.Length;

        public bool CanUpgrade(int path, int gold)
        {
            UpgradeTier next = NextTier(path);
            return next != null && tiers[path] < TierCap(path) && gold >= next.cost;
        }

        public bool TryUpgrade(int path)
        {
            if (Economy.Instance == null || !CanUpgrade(path, Economy.Instance.Gold)) return false;
            UpgradeTier next = NextTier(path);
            if (!Economy.Instance.TrySpend(next.cost)) return false;

            tiers[path]++;
            Invested += next.cost;
            Recalculate();
            UpdateUpgradeVisuals();
            Vfx.Burst(transform.position + Vector3.up * (stats.height + 0.8f), stats.paths[path].color, 40, 5f, 0.35f, 0.8f, -1.5f);
            Vfx.Ring(transform.position + Vector3.up * 0.15f, stats.paths[path].color, baseRingRadius + 0.6f, 0.5f);
            return true;
        }

        public void Sell()
        {
            if (Economy.Instance != null) Economy.Instance.Add(SellValue);
            Vfx.Burst(transform.position + Vector3.up * 1f, new Color(1f, 0.8f, 0.4f), 30, 4f, 0.4f, 0.7f, 1f);
            Destroy(gameObject);
        }

        private void Recalculate()
        {
            Damage = stats.damage;
            ShotsPerSecond = stats.shotsPerSecond;
            Range = stats.range;
            SplashRadius = stats.splashRadius;
            ProjectileSpeed = stats.projectileSpeed;
            ExtraShots = 0;
            Pierce = 0;
            Slow = 0f;
            SlowDuration = 0f;

            int best = 0;
            Signature = Color.clear;
            for (int p = 0; p < PathCount; p++)
            {
                for (int t = 0; t < tiers[p]; t++)
                {
                    UpgradeTier tier = stats.paths[p].tiers[t];
                    Damage += tier.damageBonus;
                    ShotsPerSecond *= tier.rateMultiplier;
                    Range += tier.rangeBonus;
                    SplashRadius += tier.splashBonus;
                    ProjectileSpeed *= tier.projectileSpeedMultiplier;
                    ExtraShots += tier.extraShots;
                    Pierce += tier.pierce;
                    Slow = Mathf.Max(Slow, tier.slow);
                    SlowDuration = Mathf.Max(SlowDuration, tier.slowDuration);
                }
                if (tiers[p] > best)
                {
                    best = tiers[p];
                    Signature = stats.paths[p].color;
                }
            }
        }

        /// <summary>Each tier grows the defender a little and thickens a ring in the leading path's colour; tier 3 adds an aura.</summary>
        private void UpdateUpgradeVisuals()
        {
            int total = tiers[0] + tiers[1];
            int top = Mathf.Max(tiers[0], tiers[1]);
            if (unit != null) unit.localScale = unitRestScale * (1f + 0.07f * total);

            if (baseRing == null)
            {
                baseRing = Vfx.Circle(transform, baseRingRadius, RingSegments);
                baseRing.transform.localPosition = Vector3.up * 0.08f;
            }
            // Vertex colours clamp at 1; the additive material's HDR tint makes the ring bloom.
            Color glow = Signature;
            glow.a = 0.55f + 0.15f * top;
            baseRing.startColor = baseRing.endColor = glow;
            baseRing.startWidth = baseRing.endWidth = 0.12f + 0.08f * top;

            if (top >= TowerStats.MaxTier && aura == null)
                aura = Vfx.Aura(head, Signature, 0.9f);
        }
    }
}
