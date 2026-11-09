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
    }
}
