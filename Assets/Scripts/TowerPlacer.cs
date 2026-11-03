using UnityEngine;
using UnityEngine.InputSystem;

namespace TowerDefense
{
    /// <summary>Left-click on open ground to place a tower. Blocked on the path and on other towers.</summary>
    public class TowerPlacer : MonoBehaviour
    {
        [SerializeField] private Tower towerPrefab;
        [SerializeField] private Collider ground;
        [SerializeField] private EnemyPath path;
        [SerializeField] private float pathClearance = 2f;
        [SerializeField] private float towerSpacing = 2f;

        private void Update()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;
            if (GameManager.Instance != null && GameManager.Instance.IsGameOver) return;

            Ray ray = Camera.main.ScreenPointToRay(mouse.position.ReadValue());
            if (Physics.Raycast(ray, out RaycastHit hit, 500f) && hit.collider == ground)
                TryPlace(hit.point);
        }

        /// <summary>Places a tower at a ground point if the spot is free. Returns whether it was placed.</summary>
        public bool TryPlace(Vector3 point)
        {
            if (path.DistanceTo(point) < pathClearance) return false;

            foreach (Tower tower in Tower.Active)
            {
                if (Vector3.Distance(tower.transform.position, point) < towerSpacing) return false;
            }

            Instantiate(towerPrefab, point, Quaternion.identity);
            return true;
        }

        public void Configure(Tower prefab, Collider groundCollider, EnemyPath enemyPath)
        {
            towerPrefab = prefab;
            ground = groundCollider;
            path = enemyPath;
        }
    }
}
