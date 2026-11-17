using System.Collections;
using UnityEngine;

namespace TowerDefense
{
    /// <summary>Runs one wave at a time. A wave ends when everything has spawned and no enemies remain.</summary>
    public class WaveSpawner : MonoBehaviour
    {
        [SerializeField] private WaveDefinition[] waves;
        [SerializeField] private EnemyPath path;
        [Tooltip("Seconds between clearing a wave and auto-start launching the next one.")]
        [SerializeField, Min(0f)] private float autoStartDelay = 2.5f;

        private int groupsSpawning;

        /// <summary>Number of waves started so far.</summary>
        public int CurrentWave { get; private set; }
        public int TotalWaves => waves.Length;
        public bool WaveInProgress { get; private set; }

        /// <summary>When on, the next wave starts by itself shortly after the current one is cleared (BTD6's auto-start).</summary>
        public bool AutoStart { get; private set; }

        /// <summary>Seconds until auto-start launches the next wave; only meaningful while waiting with AutoStart on.</summary>
        public float AutoCountdown { get; private set; }

        public void ToggleAutoStart()
        {
            AutoStart = !AutoStart;
            AutoCountdown = autoStartDelay;
        }

        public bool StartNextWave()
        {
            if (WaveInProgress || CurrentWave >= waves.Length) return false;
            if (GameManager.Instance.IsFinished) return false;

            WaveDefinition wave = waves[CurrentWave];
            CurrentWave++;
            WaveInProgress = true;
            groupsSpawning = wave.groups.Length;
            foreach (WaveDefinition.Group group in wave.groups)
                StartCoroutine(SpawnGroup(group));
            return true;
        }

        private IEnumerator SpawnGroup(WaveDefinition.Group group)
        {
            yield return new WaitForSeconds(group.startDelay);
            for (int i = 0; i < group.count; i++)
            {
                Enemy enemy = Instantiate(group.enemy);
                enemy.Begin(path);
                if (i < group.count - 1) yield return new WaitForSeconds(group.interval);
            }
            groupsSpawning--;
        }

        private void Update()
        {
            if (GameManager.Instance.IsFinished) return;

            if (!WaveInProgress)
            {
                // Waiting between waves: count down and launch when auto-start is on.
                if (!AutoStart || CurrentWave >= waves.Length) return;
                AutoCountdown -= Time.deltaTime;
                if (AutoCountdown <= 0f) StartNextWave();
                return;
            }

            if (groupsSpawning > 0 || Enemy.Active.Count > 0) return;

            WaveInProgress = false;
            AutoCountdown = autoStartDelay;
            Economy.Instance.Add(waves[CurrentWave - 1].completionGold);
            if (CurrentWave >= waves.Length) GameManager.Instance.Victory();
        }

        public void Configure(WaveDefinition[] waveList, EnemyPath enemyPath)
        {
            waves = waveList;
            path = enemyPath;
        }
    }
}
