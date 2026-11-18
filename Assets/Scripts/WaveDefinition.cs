using System;
using UnityEngine;

namespace TowerDefense
{
    [CreateAssetMenu(menuName = "Tower Defense/Wave")]
    public class WaveDefinition : ScriptableObject
    {
        [Serializable]
        public class Group
        {
            public Enemy enemy;
            [Min(1)] public int count = 1;
            [Min(0.05f)] public float interval = 1f;
            [Min(0f)] public float startDelay;
        }

        public Group[] groups;
        [Min(0)] public int completionGold = 50;
        [Tooltip("Multiplies every zombie's HP in this wave, so later waves are tougher. Gold per kill is unchanged.")]
        [Min(0.1f)] public float hpMultiplier = 1f;
        [Tooltip("Enemies with at least this base HP (the boss) ignore the wave's HP multiplier.")]
        [Min(0f)] public float scalingCap = 300f;
    }
}
