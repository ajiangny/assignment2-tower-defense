using UnityEngine;

namespace TowerDefense
{
    [CreateAssetMenu(menuName = "Tower Defense/Tower Stats")]
    public class TowerStats : ScriptableObject
    {
        /// <summary>Highest tier a path can reach, and the cap on the other path once one passes tier 1.</summary>
        public const int MaxTier = 3;
        public const int CrossPathCap = 1;
        public const float SellRefund = 0.7f;

        public string displayName = "Tower";
        [Min(0)] public int cost = 100;
        [Min(0.01f)] public float shotsPerSecond = 1.5f;
        [Min(0.1f)] public float projectileSpeed = 20f;
        [Min(0.5f)] public float height = 3f;
        [Min(0.1f)] public float range = 14f;
        [Min(0f)] public float damage = 1f;
        [Tooltip("0 means the shot only damages what it hits.")]
        [Min(0f)] public float splashRadius;

        [Header("Upgrades")]
        [Tooltip("Two upgrade paths of three tiers. One path can reach tier 3; the other stops at tier 1.")]
        public UpgradePath[] paths = new UpgradePath[0];

        [Header("Sound")]
        public AudioClip fireSound;
    }

    [System.Serializable]
    public class UpgradePath
    {
        public string name = "Path";
        [Tooltip("Signature colour: tints the tower's base ring, its shots and its impact effects.")]
        public Color color = Color.white;
        public UpgradeTier[] tiers = new UpgradeTier[0];
    }

    /// <summary>One purchasable upgrade. Every field adds to (or multiplies) the tower's base stats.</summary>
    [System.Serializable]
    public class UpgradeTier
    {
        public string name = "Upgrade";
        [TextArea] public string description = "";
        [Min(0)] public int cost = 100;
        public float damageBonus;
        [Min(0.1f)] public float rateMultiplier = 1f;
        public float rangeBonus;
        public float splashBonus;
        [Min(0.1f)] public float projectileSpeedMultiplier = 1f;
        [Tooltip("Extra projectiles per volley, aimed at the next zombies in range.")]
        [Min(0)] public int extraShots;
        [Tooltip("How many extra zombies each shot passes through.")]
        [Min(0)] public int pierce;
        [Tooltip("Fraction of speed removed on hit (0.3 = 30% slower). The strongest slow applies.")]
        [Range(0f, 0.9f)] public float slow;
        [Min(0f)] public float slowDuration;
    }
}
