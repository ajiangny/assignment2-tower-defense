using UnityEngine;

namespace TowerDefense
{
    /// <summary>Physics projectile. Damages on collision and is destroyed by whatever it hits.</summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Projectile : MonoBehaviour
    {
        [SerializeField] private float lifetime = 5f;

        private float damage;
        private float splashRadius;
        private bool spent;

        public void Launch(Vector3 velocity, float projectileDamage, float splash = 0f)
        {
            damage = projectileDamage;
            splashRadius = splash;
            GetComponent<Rigidbody>().linearVelocity = velocity;

            // Shots pass through towers so a neighbouring tower can't block them.
            Collider own = GetComponent<Collider>();
            foreach (Tower tower in Tower.Active)
            {
                foreach (Collider towerCollider in tower.Colliders)
                    Physics.IgnoreCollision(own, towerCollider);
            }

            Destroy(gameObject, lifetime);
        }

        private void OnCollisionEnter(Collision collision)
        {
            // Several contacts can arrive in one physics step; only the first one counts.
            if (spent) return;
            spent = true;

            Enemy direct = collision.collider.GetComponentInParent<Enemy>();
            if (direct != null) direct.TakeDamage(damage);

            if (splashRadius > 0f)
            {
                Vector3 centre = transform.position;
                float sqrRadius = splashRadius * splashRadius;
                // Copied because a kill can spawn split children into Enemy.Active.
                Enemy[] nearby = new Enemy[Enemy.Active.Count];
                for (int i = 0; i < nearby.Length; i++) nearby[i] = Enemy.Active[i];
                foreach (Enemy enemy in nearby)
                {
                    if (enemy != direct && (enemy.transform.position - centre).sqrMagnitude <= sqrRadius)
                        enemy.TakeDamage(damage);
                }
            }

            Destroy(gameObject);
        }
    }
}
