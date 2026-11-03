using UnityEngine;

namespace TowerDefense
{
    /// <summary>Physics projectile. Damages an enemy on collision and is destroyed by whatever it hits.</summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Projectile : MonoBehaviour
    {
        [SerializeField] private float lifetime = 5f;

        private float damage;

        public void Launch(Vector3 velocity, float projectileDamage)
        {
            damage = projectileDamage;
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
            Enemy enemy = collision.collider.GetComponentInParent<Enemy>();
            if (enemy != null) enemy.TakeDamage(damage);
            Destroy(gameObject);
        }
    }
}
