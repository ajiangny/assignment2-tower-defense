using UnityEngine;

namespace TowerDefense
{
    [CreateAssetMenu(menuName = "Tower Defense/Enemy Stats")]
    public class EnemyStats : ScriptableObject
    {
        public string displayName = "Zombie";
        [Min(0.1f)] public float speed = 3f;
        [Min(0.1f)] public float maxHp = 3f;
        [Tooltip("Flat reduction per hit. A hit always deals at least 1 damage.")]
        [Min(0f)] public float armor;
        [Min(0)] public int goldReward = 5;
        [Tooltip("Fraction of any slow this enemy shrugs off (0 = full slow, 1 = immune).")]
        [Range(0f, 1f)] public float slowResistance;
        [Tooltip("Spawned where this enemy dies, for example Runners from a Bloater.")]
        public Enemy splitInto;
        [Min(0)] public int splitCount;

        [Header("Sound")]
        [Tooltip("Groans played now and then while walking.")]
        public AudioClip[] voice;
        public AudioClip[] hurt;
        public AudioClip[] death;
        [Tooltip("Played once when this enemy appears, e.g. the boss roar.")]
        public AudioClip spawn;
    }
}
