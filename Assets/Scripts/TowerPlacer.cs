using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace TowerDefense
{
    public enum PlacementResult { Placed, NoTowerSelected, GameFinished, TowerLimitReached, NotEnoughGold, OnPath, TooCloseToTower, Blocked, Upgraded, UpgradeLocked, UpgradeMaxed, Sold, TargetingChanged }

    /// <summary>
    /// Choose a tower with 1/2/3 or a HUD button, then left-click open ground to build it.
    /// Left-click a built tower to select it: Q / E buy its upgrades, Tab changes its targeting, Delete sells it.
    /// </summary>
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
        [SerializeField] private float pathClearance = 2.8f;
        [SerializeField] private float towerSpacing = 3.4f;
        [SerializeField] private Material previewMaterial;
        [Tooltip("How far a tower's centre must stay from houses, walls and other scenery.")]
        [SerializeField] private float sceneryClearance = 1.6f;

        private readonly Collider[] overlap = new Collider[16];

        private Transform preview;
        private Renderer previewRenderer;
        private LineRenderer rangeRing;
        private LineRenderer selectionRing;
        private MaterialPropertyBlock previewBlock;

        public int MaxTowers => maxTowers;
        public int SelectedIndex { get; private set; } = -1;

        /// <summary>The built tower whose upgrade panel is open, or null.</summary>
        public Tower SelectedTower { get; private set; }
        public PlacementResult LastResult { get; private set; } = PlacementResult.NoTowerSelected;

        /// <summary>Counts placement clicks, so the HUD can show a message for each failed attempt.</summary>
        public int Attempts { get; private set; }
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
                if (keyboard.escapeKey.wasPressedThisFrame) { Select(-1); SelectTower(null); }
                if (SelectedTower != null)
                {
                    if (keyboard.qKey.wasPressedThisFrame) Upgrade(0);
                    if (keyboard.eKey.wasPressedThisFrame) Upgrade(1);
                    if (keyboard.tabKey.wasPressedThisFrame) CycleTargeting(keyboard.shiftKey.isPressed ? -1 : 1);
                    if (keyboard.deleteKey.wasPressedThisFrame || keyboard.backspaceKey.wasPressedThisFrame) SellSelected();
                }
            }

            Mouse mouse = Mouse.current;
            if (mouse == null) return;
            if (mouse.rightButton.wasPressedThisFrame) { Select(-1); SelectTower(null); }

            bool overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            Vector2 pointer = mouse.position.ReadValue();
            bool onGround = TryGetGroundPoint(pointer, out Vector3 point);
            UpdatePreview(onGround && !overUi, point);
            UpdateSelectionRing();

            if (overUi || !mouse.leftButton.wasPressedThisFrame) return;
            Tower clicked = TowerUnder(pointer);
            if (clicked != null)
            {
                Select(-1);
                SelectTower(clicked == SelectedTower ? null : clicked);
            }
            else if (SelectedIndex >= 0 && onGround)
            {
                TryPlace(point);
            }
            else
            {
                SelectTower(null);
            }
        }

        public void SelectTower(Tower tower)
        {
            if (tower != null && GameManager.Instance.IsFinished) tower = null;
            SelectedTower = tower;
        }

        /// <summary>Buys the next tier on a path of the selected tower.</summary>
        public PlacementResult Upgrade(int path)
        {
            Tower tower = SelectedTower;
            if (tower == null || GameManager.Instance.IsFinished) return PlacementResult.NoTowerSelected;
            if (tower.NextTier(path) == null) LastResult = PlacementResult.UpgradeMaxed;
            else if (tower.IsLocked(path)) LastResult = PlacementResult.UpgradeLocked;
            else LastResult = tower.TryUpgrade(path) ? PlacementResult.Upgraded : PlacementResult.NotEnoughGold;
            Attempts++;
            return LastResult;
        }

        /// <summary>Steps the selected tower's targeting mode (First, Last, Strong, Close).</summary>
        public void CycleTargeting(int step)
        {
            if (SelectedTower == null || GameManager.Instance.IsFinished) return;
            SelectedTower.CycleTargeting(step);
            LastResult = PlacementResult.TargetingChanged;
            Attempts++;
        }

        public void SellSelected()
        {
            if (SelectedTower == null || GameManager.Instance.IsFinished) return;
            SelectedTower.Sell();
            SelectedTower = null;
            LastResult = PlacementResult.Sold;
            Attempts++;
        }

        private Tower TowerUnder(Vector2 screenPosition)
        {
            Ray ray = Camera.main.ScreenPointToRay(screenPosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, 500f, ~0, QueryTriggerInteraction.Ignore)) return null;
            return hit.collider.GetComponentInParent<Tower>();
        }

        private void UpdateSelectionRing()
        {
            bool show = SelectedTower != null;
            selectionRing.gameObject.SetActive(show);
            if (!show) return;
            selectionRing.transform.position = SelectedTower.transform.position + Vector3.up * 0.12f;
            float range = SelectedTower.Range;
            for (int i = 0; i < RingSegments; i++)
            {
                float angle = i * Mathf.PI * 2f / RingSegments;
                selectionRing.SetPosition(i, new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * range);
            }
        }

        public void Select(int index)
        {
            SelectedIndex = index >= 0 && index < towerPrefabs.Length ? index : -1;
            LastResult = PlacementResult.NoTowerSelected;
            if (SelectedIndex < 0) return;
            SelectedTower = null;

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
            Attempts++;
            if (LastResult != PlacementResult.Placed) return LastResult;

            Tower prefab = towerPrefabs[SelectedIndex];
            Economy.Instance.TrySpend(prefab.Stats.cost);
            Instantiate(prefab, point, Quaternion.identity);
            Vfx.Burst(point + Vector3.up * 0.3f, new Color(0.75f, 0.68f, 0.55f, 0.8f), 24, 3f, 0.9f, 0.9f, -0.1f, false);
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
            int hits = Physics.OverlapSphereNonAlloc(point + Vector3.up, sceneryClearance, overlap, ~0, QueryTriggerInteraction.Collide);
            for (int i = 0; i < hits; i++)
            {
                if (overlap[i].GetComponentInParent<Scenery>() != null) return PlacementResult.Blocked;
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

            GameObject selection = new GameObject("Selection Ring");
            selection.transform.SetParent(transform, false);
            selectionRing = selection.AddComponent<LineRenderer>();
            selectionRing.sharedMaterial = previewMaterial;
            selectionRing.useWorldSpace = false;
            selectionRing.loop = true;
            selectionRing.positionCount = RingSegments;
            selectionRing.startWidth = selectionRing.endWidth = 0.15f;
            selectionRing.startColor = selectionRing.endColor = new Color(1f, 0.85f, 0.4f);
            MaterialPropertyBlock gold = new MaterialPropertyBlock();
            gold.SetColor(BaseColor, new Color(1f, 0.85f, 0.4f));
            selectionRing.SetPropertyBlock(gold);

            column.SetActive(false);
            ring.SetActive(false);
            selection.SetActive(false);
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
