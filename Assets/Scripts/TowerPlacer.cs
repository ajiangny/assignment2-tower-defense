using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace TowerDefense
{
    public enum PlacementResult { Placed, NoTowerSelected, GameFinished, TowerLimitReached, NotEnoughGold, OnPath, TooCloseToTower }

    /// <summary>Choose a tower with 1/2/3 or a HUD button, then left-click open ground to build it.</summary>
    public class TowerPlacer : MonoBehaviour
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly Color ValidColor = new Color(0.25f, 0.9f, 0.35f);
        private static readonly Color InvalidColor = new Color(0.95f, 0.25f, 0.2f);
        private const int RingSegments = 64;

        [SerializeField] private Tower[] towerPrefabs;
        [SerializeField] private Collider ground;
        [SerializeField] private EnemyPath path;
        [SerializeField, Min(1)] private int maxTowers = 10;
        [SerializeField] private float pathClearance = 2f;
        [SerializeField] private float towerSpacing = 2f;
        [SerializeField] private Material previewMaterial;

        private Transform preview;
        private Renderer previewRenderer;
        private LineRenderer rangeRing;
        private MaterialPropertyBlock previewBlock;

        public int MaxTowers => maxTowers;
        public int SelectedIndex { get; private set; } = -1;
        public PlacementResult LastResult { get; private set; } = PlacementResult.NoTowerSelected;
        public int TowerTypeCount => towerPrefabs.Length;
        public TowerStats StatsFor(int index) => towerPrefabs[index].Stats;

        private void Awake() => BuildPreview();

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.digit1Key.wasPressedThisFrame) Select(0);
                if (keyboard.digit2Key.wasPressedThisFrame) Select(1);
                if (keyboard.digit3Key.wasPressedThisFrame) Select(2);
                if (keyboard.escapeKey.wasPressedThisFrame) Select(-1);
            }

            Mouse mouse = Mouse.current;
            if (mouse == null) return;
            if (mouse.rightButton.wasPressedThisFrame) Select(-1);

            bool overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            bool onGround = TryGetGroundPoint(mouse.position.ReadValue(), out Vector3 point);
            UpdatePreview(onGround && !overUi, point);

            if (onGround && !overUi && mouse.leftButton.wasPressedThisFrame)
                TryPlace(point);
        }

        public void Select(int index)
        {
            SelectedIndex = index >= 0 && index < towerPrefabs.Length ? index : -1;
            LastResult = PlacementResult.NoTowerSelected;
            if (SelectedIndex < 0) return;

            TowerStats stats = StatsFor(SelectedIndex);
            preview.localScale = new Vector3(1.2f, stats.height / 2f, 1.2f);
            for (int i = 0; i < RingSegments; i++)
            {
                float angle = i * Mathf.PI * 2f / RingSegments;
                rangeRing.SetPosition(i, new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * stats.range);
            }
        }

        /// <summary>Builds the selected tower at a ground point if every rule allows it.</summary>
        public PlacementResult TryPlace(Vector3 point)
        {
            LastResult = Check(point);
            if (LastResult != PlacementResult.Placed) return LastResult;

            Tower prefab = towerPrefabs[SelectedIndex];
            Economy.Instance.TrySpend(prefab.Stats.cost);
            Instantiate(prefab, point, Quaternion.identity);
            return LastResult;
        }

        private PlacementResult Check(Vector3 point)
        {
            if (SelectedIndex < 0) return PlacementResult.NoTowerSelected;
            if (GameManager.Instance.IsFinished) return PlacementResult.GameFinished;
            if (Tower.Active.Count >= maxTowers) return PlacementResult.TowerLimitReached;
            if (Economy.Instance.Gold < StatsFor(SelectedIndex).cost) return PlacementResult.NotEnoughGold;
            if (path.DistanceTo(point) < pathClearance) return PlacementResult.OnPath;
            foreach (Tower tower in Tower.Active)
            {
                if (Vector3.Distance(tower.transform.position, point) < towerSpacing)
                    return PlacementResult.TooCloseToTower;
            }
            return PlacementResult.Placed;
        }

        private bool TryGetGroundPoint(Vector2 screenPosition, out Vector3 point)
        {
            point = default;
            Ray ray = Camera.main.ScreenPointToRay(screenPosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, 500f) || hit.collider != ground) return false;
            point = hit.point;
            return true;
        }

        private void BuildPreview()
        {
            GameObject column = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            column.name = "Placement Preview";
            Destroy(column.GetComponent<Collider>());
            preview = column.transform;
            previewRenderer = column.GetComponent<Renderer>();
            previewRenderer.sharedMaterial = previewMaterial;
            previewBlock = new MaterialPropertyBlock();

            GameObject ring = new GameObject("Range Ring");
            ring.transform.SetParent(transform, false);
            rangeRing = ring.AddComponent<LineRenderer>();
            rangeRing.sharedMaterial = previewMaterial;
            rangeRing.useWorldSpace = false;
            rangeRing.loop = true;
            rangeRing.positionCount = RingSegments;
            rangeRing.startWidth = rangeRing.endWidth = 0.15f;

            column.SetActive(false);
            ring.SetActive(false);
        }

        private void UpdatePreview(bool hasPoint, Vector3 point)
        {
            bool show = hasPoint && SelectedIndex >= 0 && !GameManager.Instance.IsFinished;
            preview.gameObject.SetActive(show);
            rangeRing.gameObject.SetActive(show);
            if (!show) return;

            float height = StatsFor(SelectedIndex).height;
            preview.position = point + Vector3.up * (height / 2f);
            rangeRing.transform.position = point + Vector3.up * 0.1f;

            Color color = Check(point) == PlacementResult.Placed ? ValidColor : InvalidColor;
            previewBlock.SetColor(BaseColor, color);
            previewRenderer.SetPropertyBlock(previewBlock);
            rangeRing.startColor = rangeRing.endColor = color;
            rangeRing.SetPropertyBlock(previewBlock);
        }

        public void Configure(Tower[] prefabs, Collider groundCollider, EnemyPath enemyPath, Material preview)
        {
            towerPrefabs = prefabs;
            ground = groundCollider;
            path = enemyPath;
            previewMaterial = preview;
        }
    }
}
