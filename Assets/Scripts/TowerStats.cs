using UnityEngine;

namespace TowerDefense
{
    [CreateAssetMenu(menuName = "Tower Defense/Tower Stats")]
    public class TowerStats : ScriptableObject
    {
        public string displayName = "Tower";
        [Min(0)] public int cost = 100;
        [Min(0.01f)] public float shotsPerSecond = 1.5f;
        [Min(0.1f)] public float projectileSpeed = 20f;
        [Min(0.5f)] public float height = 3f;
        [Min(0.1f)] public float range = 14f;
        [Min(0f)] public float damage = 1f;
        [Tooltip("0 means the shot only damages what it hits.")]
        [Min(0f)] public float splashRadius;
    }
}
