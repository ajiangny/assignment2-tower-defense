using UnityEngine;

namespace TowerDefense
{
    [CreateAssetMenu(menuName = "Tower Defense/Enemy Stats")]
    public class EnemyStats : ScriptableObject
    {
        [Min(0.1f)] public float speed = 3f;
        [Min(0.1f)] public float maxHp = 3f;
    }
}
