using UnityEngine;

namespace TowerDefense
{
    /// <summary>
    /// A small HP bar that floats above an enemy and faces the camera. Hidden until the first hit (the boss's is
    /// always shown and larger). The fill shades green to red, a pale chip trails behind it to show the damage taken,
    /// and the backing turns frosty blue while the enemy is slowed.
    /// Lives on its own object rather than under the enemy, so the hit squash doesn't stretch it.
    /// </summary>
    [RequireComponent(typeof(Enemy))]
    public class HealthBar : MonoBehaviour
    {
        private const float BossHp = 300f;
        private static Sprite pixel;

        private static readonly Color Back = new Color(0.05f, 0.05f, 0.07f, 0.85f);
        private static readonly Color FrostBack = new Color(0.25f, 0.55f, 0.8f, 0.9f);
        private static readonly Color Chip = new Color(1f, 0.95f, 0.85f, 0.9f);
        private static readonly Color Full = new Color(0.35f, 0.9f, 0.3f);
        private static readonly Color Half = new Color(0.98f, 0.8f, 0.2f);
        private static readonly Color Low = new Color(0.95f, 0.25f, 0.2f);

        private Enemy enemy;
        private Transform bar;
        private SpriteRenderer back, chip, fill;
        private float width, height, offset;
        private float chipFraction = 1f;
        private bool alwaysShow, shown;

        private void Start()
        {
            enemy = GetComponent<Enemy>();
            alwaysShow = enemy.Stats.maxHp >= BossHp;

            // Sit just above the top of the enemy's colliders.
            float top = transform.position.y;
            foreach (Collider c in GetComponentsInChildren<Collider>())
                top = Mathf.Max(top, c.bounds.max.y);
            offset = top - transform.position.y + 0.35f;
            width = alwaysShow ? 2.4f : 1.15f;
            height = alwaysShow ? 0.22f : 0.15f;

            bar = new GameObject($"{name} HP").transform;
            back = Part("Back", Back, 0, new Vector2(width + 0.06f, height + 0.06f), 0.5f);
            chip = Part("Chip", Chip, 1, new Vector2(width, height), 0f);
            fill = Part("Fill", Full, 2, new Vector2(width, height), 0f);
            shown = alwaysShow;
            Apply(1f);
        }

        private SpriteRenderer Part(string partName, Color color, int order, Vector2 size, float pivotX)
        {
            GameObject go = new GameObject(partName);
            go.transform.SetParent(bar, false);
            // Left-anchored parts grow from the bar's left edge.
            go.transform.localPosition = new Vector3(pivotX == 0f ? -width / 2f : 0f, 0f, -0.01f * order);
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = Pixel(pivotX);
            renderer.color = color;
            renderer.sortingOrder = 100 + order;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return renderer;
        }

        private void LateUpdate()
        {
            if (bar == null) return;
            float fraction = Mathf.Clamp01(enemy.Hp / Mathf.Max(1f, enemy.MaxHp));
            if (fraction < 1f) shown = true;
            chipFraction = Mathf.MoveTowards(Mathf.Max(chipFraction, fraction), fraction, Time.deltaTime * 0.8f);
            Apply(fraction);

            Camera cam = Camera.main;
            bar.position = transform.position + Vector3.up * offset;
            if (cam != null) bar.rotation = cam.transform.rotation;
        }

        private void Apply(float fraction)
        {
            back.enabled = chip.enabled = fill.enabled = shown;
            if (!shown) return;

            fill.transform.localScale = new Vector3(width * fraction, height, 1f);
            chip.transform.localScale = new Vector3(width * chipFraction, height, 1f);
            fill.color = fraction > 0.5f ? Color.Lerp(Half, Full, (fraction - 0.5f) * 2f) : Color.Lerp(Low, Half, fraction * 2f);
            back.color = enemy.IsSlowed ? FrostBack : Back;
        }

        private void OnDestroy()
        {
            if (bar != null) Destroy(bar.gameObject);
        }

        /// <summary>A 1x1 white sprite, one unit across, pivoted at the left edge or the centre.</summary>
        private static Sprite Pixel(float pivotX)
        {
            if (pixel == null)
            {
                Texture2D texture = Texture2D.whiteTexture;
                pixel = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0f, 0.5f), texture.width);
                centred = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), texture.width);
            }
            return pivotX == 0f ? pixel : centred;
        }

        private static Sprite centred;
    }
}
