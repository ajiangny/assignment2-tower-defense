using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace TowerDefense
{
    /// <summary>Builds the screen UI: instructions, status, tower buttons, wave controls, end screen.</summary>
    public class Hud : MonoBehaviour
    {
        private const string Instructions =
            "<b>HOW TO PLACE TOWERS</b>\n" +
            "1. Choose a tower: press <b>1</b>, <b>2</b> or <b>3</b>, or click a button below.\n" +
            "2. <b>Left-click</b> open ground to build it. Green means you can build there.\n" +
            "3. <b>Right-click</b> or <b>Esc</b> to cancel.\n" +
            "You can't build on the road or too close to another tower.\n" +
            "<b>If one zombie reaches the end of the road, you lose!</b>";

        private static readonly Color PanelColor = new Color(0.08f, 0.08f, 0.1f, 0.8f);
        private static readonly Color ButtonColor = new Color(0.22f, 0.24f, 0.3f, 0.95f);
        private static readonly Color SelectedColor = new Color(0.8f, 0.6f, 0.15f, 0.95f);

        [SerializeField] private TowerPlacer placer;
        [SerializeField] private WaveSpawner spawner;

        private TMP_Text status;
        private TMP_Text placementMessage;
        private TMP_Text waveButtonLabel;
        private TMP_Text speedButtonLabel;
        private TMP_Text endText;
        private Button waveButton;
        private GameObject endPanel;
        private Image[] towerButtons;

        private void Awake()
        {
            if (EventSystem.current == null)
            {
                GameObject events = new GameObject("EventSystem", typeof(EventSystem));
                events.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }

            GameObject canvasObject = new GameObject("HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            RectTransform root = (RectTransform)canvasObject.transform;

            RectTransform help = Panel(root, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -20f), new Vector2(640f, 250f));
            Label(help, Instructions, 22, TextAlignmentOptions.TopLeft, 14f);

            RectTransform statusPanel = Panel(root, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-20f, -20f), new Vector2(330f, 170f));
            status = Label(statusPanel, "", 28, TextAlignmentOptions.TopLeft, 14f);

            RectTransform message = Rect("Message", root, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 190f), new Vector2(900f, 50f));
            placementMessage = Label(message, "", 28, TextAlignmentOptions.Center, 0f);

            towerButtons = new Image[placer.TowerTypeCount];
            for (int i = 0; i < towerButtons.Length; i++)
            {
                int index = i;
                TowerStats stats = placer.StatsFor(i);
                string text = $"<b>{i + 1}. {stats.displayName}</b>  {stats.cost}g\n" +
                              $"<size=20>fires {stats.shotsPerSecond:0.#}/s, range {stats.range:0}, height {stats.height:0.#}</size>";
                Vector2 position = new Vector2(20f + i * 410f, 20f);
                Button button = MakeButton(root, new Vector2(0f, 0f), position, new Vector2(400f, 100f), text, 26, () => placer.Select(index), out _);
                towerButtons[i] = button.GetComponent<Image>();
            }

            waveButton = MakeButton(root, new Vector2(1f, 0f), new Vector2(-20f, 20f), new Vector2(300f, 100f), "", 30, () => spawner.StartNextWave(), out waveButtonLabel);
            MakeButton(root, new Vector2(1f, 0f), new Vector2(-330f, 20f), new Vector2(100f, 100f), "", 30, () => GameManager.Instance.ToggleFastForward(), out speedButtonLabel);

            endPanel = Panel(root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(700f, 400f)).gameObject;
            endText = Label((RectTransform)endPanel.transform, "", 42, TextAlignmentOptions.Top, 30f);
            MakeButton((RectTransform)endPanel.transform, new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(300f, 80f), "Restart (R)", 30, () => GameManager.Instance.Restart(), out _);
            endPanel.SetActive(false);
        }

        private void Update()
        {
            GameManager game = GameManager.Instance;

            int shownWave = Mathf.Max(spawner.CurrentWave, 1);
            status.text = $"Gold: <b>{Economy.Instance.Gold}</b>\n" +
                          $"Towers: <b>{Tower.Active.Count}/{placer.MaxTowers}</b>\n" +
                          $"Wave: <b>{shownWave}/{spawner.TotalWaves}</b>\n" +
                          $"Destroyed: <b>{game.Kills}</b>";

            for (int i = 0; i < towerButtons.Length; i++)
                towerButtons[i].color = i == placer.SelectedIndex ? SelectedColor : ButtonColor;

            placementMessage.text = MessageFor(placer.LastResult);

            waveButton.interactable = !spawner.WaveInProgress && !game.IsFinished;
            waveButtonLabel.text = game.IsFinished ? "Game finished" : spawner.WaveInProgress ? "Wave in progress" : $"Start Wave {spawner.CurrentWave + 1}";
            speedButtonLabel.text = game.FastForward ? "2x" : "1x";

            if (game.IsFinished && !endPanel.activeSelf)
            {
                string title = game.Result == GameResult.Won ? "<color=#FFD54A>THE TOWN IS SAVED!</color>" : "<color=#FF5A4A>THE TOWN HAS FALLEN</color>";
                string detail = game.Result == GameResult.Won ? "You survived every wave." : "A zombie reached the end of the road.";
                endText.text = $"<b>{title}</b>\n<size=30>{detail}\nZombies destroyed: {game.Kills}</size>";
                endPanel.SetActive(true);
            }
        }

        private static string MessageFor(PlacementResult result)
        {
            switch (result)
            {
                case PlacementResult.TowerLimitReached: return "<color=#FF8A7A>Tower limit reached.</color>";
                case PlacementResult.NotEnoughGold: return "<color=#FF8A7A>Not enough gold.</color>";
                case PlacementResult.OnPath: return "<color=#FF8A7A>You can't build on the road.</color>";
                case PlacementResult.TooCloseToTower: return "<color=#FF8A7A>Too close to another tower.</color>";
                default: return "";
            }
        }

        private static RectTransform Rect(string name, RectTransform parent, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private static RectTransform Panel(RectTransform parent, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            RectTransform rect = Rect("Panel", parent, anchor, pivot, position, size);
            rect.gameObject.AddComponent<Image>().color = PanelColor;
            return rect;
        }

        private static TMP_Text Label(RectTransform parent, string text, float fontSize, TextAlignmentOptions alignment, float padding)
        {
            RectTransform rect = Rect("Label", parent, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.one * padding;
            rect.offsetMax = Vector2.one * -padding;
            TextMeshProUGUI label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = fontSize;
            label.alignment = alignment;
            label.color = Color.white;
            label.raycastTarget = false;
            return label;
        }

        private static Button MakeButton(RectTransform parent, Vector2 anchor, Vector2 position, Vector2 size, string text, float fontSize, UnityEngine.Events.UnityAction onClick, out TMP_Text label)
        {
            RectTransform rect = Rect("Button", parent, anchor, anchor, position, size);
            rect.gameObject.AddComponent<Image>().color = ButtonColor;
            Button button = rect.gameObject.AddComponent<Button>();
            button.onClick.AddListener(onClick);
            label = Label(rect, text, fontSize, TextAlignmentOptions.Center, 8f);
            return button;
        }

        public void Configure(TowerPlacer towerPlacer, WaveSpawner waveSpawner)
        {
            placer = towerPlacer;
            spawner = waveSpawner;
        }
    }
}
