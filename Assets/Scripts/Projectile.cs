using UnityEngine;

namespace TowerDefense
{
    /// <summary>Everything a tower's upgrades change about one shot.</summary>
    public struct ShotInfo
    {
        public float Damage;
        public float Splash;
        public int Pierce;
        public float Slow;
        public float SlowDuration;
        /// <summary>Upgrade path colour for the trail and impact; clear keeps the projectile's own colour.</summary>
        public Color Tint;
        /// <summary>Highest upgrade tier on the tower (0-3). Each tier makes the shot bigger and flashier.</summary>
        public int Tier;
    }

    public enum ImpactStyle { Splinters, Sparks, Arcane }

    /// <summary>Physics projectile. Damages on collision and is destroyed by whatever it hits, unless it pierces.</summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Projectile : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        [SerializeField] private float lifetime = 5f;
        [SerializeField] private AudioClip impactSound;

        [Header("Look")]
        [Tooltip("Child spun around its local axis in flight (the axe tumbling end over end).")]
        [SerializeField] private Transform spinner;
        [SerializeField] private Vector3 spinAxis = Vector3.right;
        [SerializeField] private float spinSpeed;
        [SerializeField] private TrailRenderer trail;
        [SerializeField] private Light glow;
        [Tooltip("Renderers recoloured by an upgrade tint (the orb's core).")]
        [SerializeField] private Renderer[] tintRenderers = new Renderer[0];
        [SerializeField] private ImpactStyle impactStyle = ImpactStyle.Sparks;
        [ColorUsage(false, true)]
        [SerializeField] private Color impactColor = Color.white;

        private Rigidbody body;
        private Collider own;
        private ShotInfo shot;
        private Vector3 launchVelocity;
        private int piercesLeft;
        private bool spent;

        public void Launch(Vector3 velocity, ShotInfo info)
        {
            body = GetComponent<Rigidbody>();
            own = GetComponent<Collider>();
            shot = info;
            piercesLeft = info.Pierce;
            launchVelocity = velocity;
            body.linearVelocity = velocity;

            // Shots pass through towers so a neighbouring tower can't block them.
            foreach (Tower tower in Tower.Active)
            {
                foreach (Collider towerCollider in tower.Colliders)
                    Physics.IgnoreCollision(own, towerCollider);
            }

            if (info.Tint.a > 0f) ApplyTint(info.Tint);
            if (info.Tier > 0) ApplyTierLook(info.Tier, info.Tint.a > 0f ? info.Tint : impactColor);
            Destroy(gameObject, lifetime);
        }

        private void ApplyTint(Color tint)
        {
            impactColor = tint;
            if (trail != null)
            {
                Color start = tint;
                Color end = tint;
                start.a = trail.startColor.a;
                end.a = 0f;
                trail.startColor = start;
                trail.endColor = end;
            }
            if (glow != null) glow.color = tint;
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            foreach (Renderer r in tintRenderers)
            {
                r.GetPropertyBlock(block);
                block.SetColor(BaseColorId, tint);
                block.SetColor(EmissionColorId, tint * 3f);
                r.SetPropertyBlock(block);
            }
        }

        /// <summary>
        /// Upgraded shots read at a glance: per tier the model grows and the trail widens and lengthens;
        /// every upgraded shot wears a glowing halo in the path colour; tier 2+ sheds sparks and casts light.
        /// </summary>
        private void ApplyTierLook(int tier, Color color)
        {
            Transform model = spinner != null ? spinner : transform.Find("Visual");
            if (model == null) model = transform.Find("Core");
            if (model != null) model.localScale *= 1f + 0.18f * tier;

            if (trail != null)
            {
                trail.widthMultiplier *= 1f + 0.45f * tier;
                trail.time *= 1f + 0.3f * tier;
            }

            float size = (own is SphereCollider sphere ? sphere.radius * 2f : 0.4f) + 0.35f + 0.25f * tier;
            Vfx.ProjectileGlow(transform, color, size, tier >= 2 ? 25f * tier : 0f);

            if (tier >= 2 && glow == null)
            {
                Light light = new GameObject("Upgrade Light").AddComponent<Light>();
                light.transform.SetParent(transform, false);
                light.type = LightType.Point;
                light.color = color;
                light.range = 2.5f + tier;
                light.intensity = 1.5f * tier;
                light.shadows = LightShadows.None;
            }
            else if (glow != null)
            {
                glow.intensity *= 1f + 0.3f * tier;
                glow.range *= 1f + 0.2f * tier;
            }
        }

        private void Update()
        {
            if (spinner != null && spinSpeed != 0f) spinner.Rotate(spinAxis, spinSpeed * Time.deltaTime, Space.Self);
        }

        private void OnCollisionEnter(Collision collision)
        {
            // Several contacts can arrive in one physics step; only the first one counts.
            if (spent) return;

            Enemy direct = collision.collider.GetComponentInParent<Enemy>();
            if (direct != null)
            {
                Hit(direct);
                if (piercesLeft > 0)
                {
                    // Pass through: ignore this zombie from now on and keep flying at the launch velocity.
                    piercesLeft--;
                    foreach (Collider c in direct.GetComponentsInChildren<Collider>()) Physics.IgnoreCollision(own, c);
                    body.linearVelocity = launchVelocity;
                    PlayImpact(transform.position, 0.5f);
                    GameAudio.Play(impactSound, 0.4f);
                    return;
                }
            }

            spent = true;
            if (direct != null || shot.Splash > 0f) GameAudio.Play(impactSound, 0.6f);

            if (shot.Splash > 0f)
            {
                Vector3 centre = transform.position;
                float sqrRadius = shot.Splash * shot.Splash;
                // Copied because a kill can spawn split children into Enemy.Active.
                Enemy[] nearby = new Enemy[Enemy.Active.Count];
                for (int i = 0; i < nearby.Length; i++) nearby[i] = Enemy.Active[i];
                foreach (Enemy enemy in nearby)
                {
                    if (enemy != null && enemy != direct && (enemy.transform.position - centre).sqrMagnitude <= sqrRadius)
                        Hit(enemy);
                }
            }

            PlayImpact(transform.position, direct != null || shot.Splash > 0f ? 1f : 0.45f);
            DetachTrail();
            Destroy(gameObject);
        }

        private void Hit(Enemy enemy)
        {
            if (shot.Slow > 0f) enemy.ApplySlow(shot.Slow, shot.SlowDuration);
            enemy.TakeDamage(shot.Damage);
        }

        private void PlayImpact(Vector3 point, float strength)
        {
            // Upgraded shots hit harder on screen too: more particles, and a coloured flash and shockwave at tier 3.
            strength *= 1f + 0.4f * shot.Tier;
            if (shot.Tier >= 1 && impactStyle != ImpactStyle.Arcane)
                Vfx.Flash(point, impactColor, 0.6f + 0.35f * shot.Tier);
            if (shot.Tier >= TowerStats.MaxTier && strength >= 1f && impactStyle != ImpactStyle.Arcane)
                Vfx.Ring(point, impactColor, 1.6f, 0.3f);

            switch (impactStyle)
            {
                case ImpactStyle.Splinters:
                    Vfx.Burst(point, impactColor, Mathf.RoundToInt(14 * strength), 4.5f, 0.18f, 0.45f, 1.2f);
                    Vfx.Burst(point, new Color(0.35f, 0.3f, 0.25f, 0.7f), Mathf.RoundToInt(6 * strength), 1.5f, 0.6f, 0.6f, -0.2f, false);
                    break;
                case ImpactStyle.Sparks:
                    Vfx.Burst(point, impactColor, Mathf.RoundToInt(10 * strength), 3.5f, 0.14f, 0.35f, 0.6f);
                    break;
                case ImpactStyle.Arcane:
                    Vfx.Flash(point, impactColor, 1.2f + shot.Splash * 0.9f);
                    Vfx.Burst(point, impactColor, Mathf.RoundToInt(26 * strength), 5f, 0.3f, 0.6f, -0.6f);
                    if (shot.Splash > 0f && strength >= 1f) Vfx.Ring(point, impactColor, shot.Splash, 0.35f);
                    break;
            }
        }

        /// <summary>Lets the trail finish fading instead of vanishing with the projectile.</summary>
        private void DetachTrail()
        {
            if (trail == null) return;
            trail.transform.SetParent(null, true);
            trail.emitting = false;
            trail.autodestruct = true;
            Destroy(trail.gameObject, trail.time + 0.1f);
        }
    }
}
