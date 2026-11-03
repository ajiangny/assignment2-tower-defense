using UnityEngine;

namespace TowerDefense
{
    /// <summary>Spawns enemies at the start of the path at a fixed interval.</summary>
    public class WaveSpawner : MonoBehaviour
    {
        [SerializeField] private Enemy enemyPrefab;
        [SerializeField] private EnemyPath path;
        [SerializeField, Min(0.1f)] private float spawnInterval = 3f;
        [SerializeField, Min(0f)] private float firstSpawnDelay = 5f;

        private float timer;

        private void Start() => timer = firstSpawnDelay;

        private void Update()
        {
            timer -= Time.deltaTime;
            if (timer > 0f) return;

            timer = spawnInterval;
            Enemy enemy = Instantiate(enemyPrefab);
            enemy.Begin(path);
        }

        public void Configure(Enemy prefab, EnemyPath enemyPath)
        {
            enemyPrefab = prefab;
            path = enemyPath;
        }
    }
}
