using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace TowerDefense
{
    /// <summary>
    /// Builds the screen UI in code: resource bar, build instructions, tower cards, wave controls,
    /// placement messages, wave banner and the end screen.
    /// </summary>
    public class Hud : MonoBehaviour
    {
        // Palette: dark slate panels, warm parchment text, one gold accent for actions.
        private static readonly Color Panel = new Color(0.07f, 0.08f, 0.11f, 0.88f);
        private static readonly Color PanelRaised = new Color(0.13f, 0.14f, 0.19f, 0.95f);
        private static readonly Color Text = new Color(0.94f, 0.92f, 0.88f);
        private static readonly Color Muted = new Color(0.62f, 0.64f, 0.70f);
        private static readonly Color Gold = new Color(0.96f, 0.76f, 0.30f);
        private static readonly Color Danger = new Color(1f, 0.42f, 0.36f);
        private static readonly Color Dim = new Color(0f, 0f, 0f, 0.62f);
        private static readonly Color Ink = new Color(0.12f, 0.09f, 0.05f);
        private static readonly Color[] TowerAccents =
        {
            new Color(0.33f, 0.52f, 0.95f), // Knight: royal blue
            new Color(0.40f, 0.72f, 0.33f), // Archer: forest green
            new Color(0.66f, 0.45f, 0.90f), // Mage: purple
        };

        private const float BannerTime = 2.4f;
        private const float ToastTime = 2.2f;

        [SerializeField] private TowerPlacer placer;
        [SerializeField] private WaveSpawner spawner;
        [SerializeField] private Sprite[] portraits;

        private static Sprite rounded;

        private TMP_Text goldValue, waveValue, towerValue, killValue;
        private RectTransform helpCard, helpChip;
        private TMP_Text hint;
        private RectTransform hintBox;
        private CanvasGroup toast;
        private TMP_Text toastText;
        private float toastTimer;
        private int seenAttempts;
        private CanvasGroup banner;
        private TMP_Text bannerTitle, bannerSubtitle;
        private float bannerTimer;
        private int seenWave;
        private Card[] cards;
        private Button waveButton;
        private Image waveButtonImage;
        private TMP_Text waveTitle, waveSubtitle, speedLabel;
        private GameObject endScreen;
        private TMP_Text endTitle, endDetail, endWaves, endKills;

        private struct Card
        {
            public RectTransform Rect;
            public Image Outline;
            public CanvasGroup Group;
            public TMP_Text Cost;
            public int Price;
        }

        public void Configure(TowerPlacer towerPlacer, WaveSpawner waveSpawner, Sprite[] towerPortraits)
        {
            placer = towerPlacer;
            spawner = waveSpawner;
            portraits = towerPortraits;
        }

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

            BuildResourceBar(root);
            BuildHelp(root);
            BuildTowerCards(root);
            BuildWaveControls(root);
            BuildMessages(root);
            BuildEndScreen(root);
        }

        // ---------- Layout ----------

        private void BuildResourceBar(RectTransform root)
        {
            RectTransform bar = Box("Resource Bar", root, Anchor.Top, new Vector2(0f, -18f), new Vector2(720f, 72f), Panel);
            HorizontalLayoutGroup row = bar.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.padding = new RectOffset(24, 24, 8, 8);
            row.childForceExpandWidth = true;
            row.childControlWidth = true;
            row.childControlHeight = true;

            goldValue = Stat(bar, "GOLD", Gold, true);
            waveValue = Stat(bar, "WAVE", Text, false);
            towerValue = Stat(bar, "TOWERS", Text, false);
            killValue = Stat(bar, "DESTROYED", Text, false);
        }

        private void BuildHelp(RectTransform root)
        {
            helpCard = Box("Help", root, Anchor.TopLeft, new Vector2(18f, -18f), new Vector2(430f, 232f), Panel);
            Label(helpCard, "HOW TO BUILD", 18, Gold, TextAlignmentOptions.TopLeft, new Vector2(22f, -16f), new Vector2(390f, 26f), true, 4f);
            Step(helpCard, 0, "1", "Pick a tower: press <b>1 2 3</b> or click a card");
            Step(helpCard, 1, "2", "<b>Left-click</b> open ground. Green means OK");
            Step(helpCard, 2, "3", "<b>Right-click</b> or <b>Esc</b> to cancel");
            Label(helpCard, "Not on the road, houses, or next to another tower.\nIf one zombie reaches the town gate, you lose.",
                16, Muted, TextAlignmentOptions.TopLeft, new Vector2(22f, -172f), new Vector2(390f, 50f), false, 0f);
            Label(helpCard, "H  hide", 14, Muted, TextAlignmentOptions.TopRight, new Vector2(-18f, -18f), new Vector2(100f, 20f), false, 0f, Anchor.TopRight);

            helpChip = Box("Help Chip", root, Anchor.TopLeft, new Vector2(18f, -18f), new Vector2(150f, 44f), Panel);
            Label(helpChip, "<color=#F5C24D>H</color>  How to build", 16, Text, TextAlignmentOptions.Center, Vector2.zero, new Vector2(150f, 44f), false, 0f, Anchor.Center);
            helpChip.gameObject.SetActive(false);
        }

        private void Step(RectTransform parent, int row, string number, string text)
        {
            float y = -56f - row * 38f;
            RectTransform badge = Box("Step", parent, Anchor.TopLeft, new Vector2(22f, y), new Vector2(28f, 28f), PanelRaised);
            Label(badge, number, 16, Gold, TextAlignmentOptions.Center, Vector2.zero, new Vector2(28f, 28f), true, 0f, Anchor.Center);
            Label(parent, text, 18, Text, TextAlignmentOptions.MidlineLeft, new Vector2(62f, y), new Vector2(350f, 28f), false, 0f);
        }

        private void BuildTowerCards(RectTransform root)
        {
            const float width = 290f, height = 116f, gap = 14f;
            int count = placer.TowerTypeCount;
            cards = new Card[count];
            float start = -(count * width + (count - 1) * gap) / 2f + width / 2f;

            for (int i = 0; i < count; i++)
            {
                int index = i;
                TowerStats stats = placer.StatsFor(i);
                Color accent = TowerAccents[i % TowerAccents.Length];

                RectTransform outline = Box("Tower Card", root, Anchor.Bottom, new Vector2(start + i * (width + gap), 18f), new Vector2(width, height), accent);
                CanvasGroup group = outline.gameObject.AddComponent<CanvasGroup>();
                RectTransform card = Box("Face", outline, Anchor.Stretch, Vector2.zero, Vector2.zero, PanelRaised);
                card.offsetMin = new Vector2(3f, 3f);
                card.offsetMax = new Vector2(-3f, -3f);
                Button button = outline.gameObject.AddComponent<Button>();
                button.targetGraphic = card.GetComponent<Image>();
                button.onClick.AddListener(() => placer.Select(placer.SelectedIndex == index ? -1 : index));

                // Portrait in a rounded, accent-tinted frame.
                RectTransform frame = Box("Portrait", card, Anchor.Left, new Vector2(12f, 0f), new Vector2(92f, 92f), accent * new Color(0.45f, 0.45f, 0.45f, 1f));
                frame.gameObject.AddComponent<Mask>().showMaskGraphic = true;
                if (portraits != null && i < portraits.Length && portraits[i] != null)
                {
                    RectTransform art = Rect("Art", frame, Anchor.Stretch, Vector2.zero, Vector2.zero);
                    Image image = art.gameObject.AddComponent<Image>();
                    image.sprite = portraits[i];
                    image.preserveAspect = true;
                    image.raycastTarget = false;
                }

                RectTransform key = Box("Key", card, Anchor.TopLeft, new Vector2(6f, -6f), new Vector2(26f, 26f), Panel);
                Label(key, (i + 1).ToString(), 15, Text, TextAlignmentOptions.Center, Vector2.zero, new Vector2(26f, 26f), true, 0f, Anchor.Center);

                Label(card, stats.displayName, 20, Text, TextAlignmentOptions.TopLeft, new Vector2(116f, -12f), new Vector2(160f, 26f), true, 0f);
                RectTransform coin = Box("Coin", card, Anchor.TopLeft, new Vector2(116f, -46f), new Vector2(14f, 14f), Gold, 7f);
                TMP_Text cost = Label(card, stats.cost.ToString(), 18, Gold, TextAlignmentOptions.MidlineLeft, new Vector2(136f, -42f), new Vector2(80f, 22f), true, 0f);
                Label(card, $"{stats.shotsPerSecond:0.#} shots/s  ·  range {stats.range:0}\nheight {stats.height:0.#} m",
                    14, Muted, TextAlignmentOptions.TopLeft, new Vector2(116f, -68f), new Vector2(166f, 40f), false, 0f);

                cards[i] = new Card { Rect = outline, Outline = outline.GetComponent<Image>(), Group = group, Cost = cost, Price = stats.cost };
            }
        }

        private void BuildWaveControls(RectTransform root)
        {
            RectTransform wave = Box("Start Wave", root, Anchor.BottomRight, new Vector2(-18f, 18f), new Vector2(300f, 92f), Gold);
            waveButtonImage = wave.GetComponent<Image>();
            waveButton = wave.gameObject.AddComponent<Button>();
            waveButton.targetGraphic = waveButtonImage;
            waveButton.onClick.AddListener(() => spawner.StartNextWave());
            waveTitle = Label(wave, "", 24, Ink, TextAlignmentOptions.Center, new Vector2(0f, 12f), new Vector2(280f, 34f), true, 2f, Anchor.Center);
            waveSubtitle = Label(wave, "", 15, Ink, TextAlignmentOptions.Center, new Vector2(0f, -18f), new Vector2(280f, 22f), false, 0f, Anchor.Center);

            RectTransform speed = Box("Speed", root, Anchor.BottomRight, new Vector2(-330f, 18f), new Vector2(92f, 92f), Panel);
            Button speedButton = speed.gameObject.AddComponent<Button>();
            speedButton.targetGraphic = speed.GetComponent<Image>();
            speedButton.onClick.AddListener(() => GameManager.Instance.ToggleFastForward());
            speedLabel = Label(speed, "", 26, Text, TextAlignmentOptions.Center, new Vector2(0f, 8f), new Vector2(92f, 34f), true, 0f, Anchor.Center);
            Label(speed, "SPEED  F", 12, Muted, TextAlignmentOptions.Center, new Vector2(0f, -22f), new Vector2(92f, 18f), false, 1f, Anchor.Center);
        }

        private void BuildMessages(RectTransform root)
        {
            hintBox = Box("Hint", root, Anchor.Bottom, new Vector2(0f, 150f), new Vector2(620f, 40f), Panel, 20f);
            hint = Label(hintBox, "", 18, Text, TextAlignmentOptions.Center, Vector2.zero, new Vector2(620f, 40f), false, 0f, Anchor.Center);
            hint.textWrappingMode = TextWrappingModes.NoWrap;

            RectTransform toastBox = Box("Toast", root, Anchor.Bottom, new Vector2(0f, 202f), new Vector2(460f, 48f), Panel, 20f);
            toast = toastBox.gameObject.AddComponent<CanvasGroup>();
            toast.blocksRaycasts = false;
            toast.alpha = 0f;
            toastText = Label(toastBox, "", 20, Danger, TextAlignmentOptions.Center, Vector2.zero, new Vector2(460f, 48f), true, 0f, Anchor.Center);

            RectTransform bannerRect = Rect("Wave Banner", root, Anchor.Top, new Vector2(0f, -150f), new Vector2(900f, 140f));
            banner = bannerRect.gameObject.AddComponent<CanvasGroup>();
            banner.blocksRaycasts = false;
            banner.alpha = 0f;
            bannerTitle = Label(bannerRect, "", 64, Text, TextAlignmentOptions.Center, new Vector2(0f, 20f), new Vector2(900f, 80f), true, 10f, Anchor.Center);
            bannerTitle.outlineWidth = 0.18f;
            bannerTitle.outlineColor = new Color32(0, 0, 0, 200);
            bannerSubtitle = Label(bannerRect, "", 22, Gold, TextAlignmentOptions.Center, new Vector2(0f, -36f), new Vector2(900f, 30f), true, 3f, Anchor.Center);
        }

        private void BuildEndScreen(RectTransform root)
        {
            RectTransform backdrop = Rect("End Screen", root, Anchor.Stretch, Vector2.zero, Vector2.zero);
            Image dim = backdrop.gameObject.AddComponent<Image>();
            dim.color = Dim;
            endScreen = backdrop.gameObject;

            RectTransform card = Box("End Card", backdrop, Anchor.Center, Vector2.zero, new Vector2(660f, 380f), new Color(0.07f, 0.08f, 0.11f, 0.97f));
            endTitle = Label(card, "", 40, Text, TextAlignmentOptions.Center, new Vector2(0f, -40f), new Vector2(620f, 56f), true, 3f, Anchor.Top);
            endTitle.textWrappingMode = TextWrappingModes.NoWrap;
            endDetail = Label(card, "", 20, Muted, TextAlignmentOptions.Center, new Vector2(0f, -104f), new Vector2(600f, 30f), false, 0f, Anchor.Top);

            endWaves = BigStat(card, "WAVES SURVIVED", new Vector2(-130f, 10f));
            endKills = BigStat(card, "ZOMBIES DESTROYED", new Vector2(130f, 10f));

            RectTransform again = Box("Play Again", card, Anchor.Bottom, new Vector2(0f, 30f), new Vector2(280f, 64f), Gold);
            Button button = again.gameObject.AddComponent<Button>();
            button.targetGraphic = again.GetComponent<Image>();
            button.onClick.AddListener(() => GameManager.Instance.Restart());
            Label(again, "PLAY AGAIN  (R)", 22, Ink, TextAlignmentOptions.Center, Vector2.zero, new Vector2(280f, 64f), true, 2f, Anchor.Center);

            endScreen.SetActive(false);
        }

        // ---------- Per frame ----------

        private void Update()
        {
            GameManager game = GameManager.Instance;
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && !game.IsFinished)
            {
                if (keyboard.hKey.wasPressedThisFrame) ToggleHelp();
                if (keyboard.fKey.wasPressedThisFrame) game.ToggleFastForward();
                if (keyboard.spaceKey.wasPressedThisFrame) spawner.StartNextWave();
            }

            int gold = Economy.Instance.Gold;
            bool atCap = Tower.Active.Count >= placer.MaxTowers;
            goldValue.text = gold.ToString();
            waveValue.text = $"{spawner.CurrentWave}<size=70%><color=#9EA3AF> / {spawner.TotalWaves}</color></size>";
            towerValue.text = $"{(atCap ? "<color=#FF6B5C>" : "")}{Tower.Active.Count}{(atCap ? "</color>" : "")}<size=70%><color=#9EA3AF> / {placer.MaxTowers}</color></size>";
            killValue.text = game.Kills.ToString();

            UpdateCards(gold, atCap);
            UpdateWaveControls(game);
            UpdateMessages(game);
            UpdateBanner();

            if (game.IsFinished && !endScreen.activeSelf) ShowEndScreen(game);
        }

        private void UpdateCards(int gold, bool atCap)
        {
            for (int i = 0; i < cards.Length; i++)
            {
                bool selected = i == placer.SelectedIndex;
                bool affordable = gold >= cards[i].Price && !atCap;
                Color accent = TowerAccents[i % TowerAccents.Length];
                cards[i].Outline.color = selected ? accent : new Color(accent.r, accent.g, accent.b, 0.35f);
                cards[i].Group.alpha = affordable || selected ? 1f : 0.55f;
                cards[i].Cost.color = gold >= cards[i].Price ? Gold : Danger;
                Vector2 position = cards[i].Rect.anchoredPosition;
                position.y = Mathf.MoveTowards(position.y, selected ? 30f : 18f, Time.unscaledDeltaTime * 120f);
                cards[i].Rect.anchoredPosition = position;
            }
        }

        private void UpdateWaveControls(GameManager game)
        {
            bool ready = !spawner.WaveInProgress && !game.IsFinished && spawner.CurrentWave < spawner.TotalWaves;
            waveButton.interactable = ready;
            waveButtonImage.color = ready ? Gold : PanelRaised;
            Color ink = ready ? Ink : Muted;
            waveTitle.color = waveSubtitle.color = ink;
            if (game.IsFinished)
            {
                waveTitle.text = "GAME OVER";
                waveSubtitle.text = "";
            }
            else if (spawner.WaveInProgress)
            {
                waveTitle.text = $"WAVE {spawner.CurrentWave}";
                int left = Enemy.Active.Count;
                waveSubtitle.text = $"{left} zombie{(left == 1 ? "" : "s")} on the road";
            }
            else
            {
                waveTitle.text = $"START WAVE {spawner.CurrentWave + 1}";
                waveSubtitle.text = "Space";
            }
            speedLabel.text = game.FastForward ? "2x" : "1x";
            speedLabel.color = game.FastForward ? Gold : Text;
        }

        private void UpdateMessages(GameManager game)
        {
            int selected = placer.SelectedIndex;
            bool building = selected >= 0 && !game.IsFinished;
            hintBox.gameObject.SetActive(building);
            if (building)
                hint.text = $"<b>{placer.StatsFor(selected).displayName}</b>  ·  left-click green ground  <color=#9EA3AF>·  right-click to cancel</color>";

            if (placer.Attempts != seenAttempts)
            {
                seenAttempts = placer.Attempts;
                string message = MessageFor(placer.LastResult);
                if (message != null)
                {
                    toastText.text = message;
                    toastTimer = ToastTime;
                }
            }
            toastTimer -= Time.unscaledDeltaTime;
            toast.alpha = Mathf.Clamp01(toastTimer / 0.4f);
        }

        private void UpdateBanner()
        {
            if (spawner.CurrentWave != seenWave)
            {
                seenWave = spawner.CurrentWave;
                bool boss = seenWave == spawner.TotalWaves;
                bannerTitle.text = $"WAVE {seenWave}";
                bannerSubtitle.text = boss ? "THE ZOMBIE KING RISES" : seenWave == 1 ? "THE DEAD ARE COMING" : "";
                bannerTitle.color = boss ? Danger : Text;
                bannerTimer = BannerTime;
            }
            bannerTimer -= Time.unscaledDeltaTime;
            float fadeIn = Mathf.Clamp01((BannerTime - bannerTimer) / 0.25f);
            float fadeOut = Mathf.Clamp01(bannerTimer / 0.5f);
            banner.alpha = Mathf.Min(fadeIn, fadeOut);
        }

        private void ShowEndScreen(GameManager game)
        {
            bool won = game.Result == GameResult.Won;
            endTitle.text = won ? "THE TOWN IS SAVED" : "THE TOWN HAS FALLEN";
            endTitle.color = won ? Gold : Danger;
            endDetail.text = won ? "Every wave of the dead was held back." : "A zombie reached the town gate.";
            int survived = won ? spawner.TotalWaves : Mathf.Max(0, spawner.CurrentWave - 1);
            endWaves.text = $"{survived}<size=60%><color=#9EA3AF> / {spawner.TotalWaves}</color></size>";
            endKills.text = game.Kills.ToString();
            endScreen.SetActive(true);
            placer.Select(-1);
        }

        private void ToggleHelp()
        {
            bool show = !helpCard.gameObject.activeSelf;
            helpCard.gameObject.SetActive(show);
            helpChip.gameObject.SetActive(!show);
        }

        private static string MessageFor(PlacementResult result)
        {
            switch (result)
            {
                case PlacementResult.TowerLimitReached: return "Tower limit reached";
                case PlacementResult.NotEnoughGold: return "Not enough gold";
                case PlacementResult.OnPath: return "Can't build on the road";
                case PlacementResult.TooCloseToTower: return "Too close to another tower";
                case PlacementResult.Blocked: return "Something is in the way";
                default: return null;
            }
        }

        // ---------- Building blocks ----------

        private enum Anchor { Top, TopLeft, TopRight, Bottom, BottomRight, Left, Center, Stretch }

        private static RectTransform Rect(string name, RectTransform parent, Anchor anchor, Vector2 position, Vector2 size)
        {
            RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            Vector2 a;
            switch (anchor)
            {
                case Anchor.Top: a = new Vector2(0.5f, 1f); break;
                case Anchor.TopLeft: a = new Vector2(0f, 1f); break;
                case Anchor.TopRight: a = new Vector2(1f, 1f); break;
                case Anchor.Bottom: a = new Vector2(0.5f, 0f); break;
                case Anchor.BottomRight: a = new Vector2(1f, 0f); break;
                case Anchor.Left: a = new Vector2(0f, 0.5f); break;
                case Anchor.Center: a = new Vector2(0.5f, 0.5f); break;
                default:
                    rect.anchorMin = Vector2.zero;
                    rect.anchorMax = Vector2.one;
                    rect.offsetMin = rect.offsetMax = Vector2.zero;
                    return rect;
            }
            rect.anchorMin = rect.anchorMax = rect.pivot = a;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private static RectTransform Box(string name, RectTransform parent, Anchor anchor, Vector2 position, Vector2 size, Color color, float radius = 12f)
        {
            RectTransform rect = Rect(name, parent, anchor, position, size);
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = RoundedSprite();
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 16f / Mathf.Max(radius, 1f);
            image.color = color;
            return rect;
        }

        private static TMP_Text Label(RectTransform parent, string text, float size, Color color, TextAlignmentOptions alignment,
            Vector2 position, Vector2 box, bool bold, float spacing, Anchor anchor = Anchor.TopLeft)
        {
            RectTransform rect = Rect("Label", parent, anchor, position, box);
            TextMeshProUGUI label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.alignment = alignment;
            label.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            label.characterSpacing = spacing;
            label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.Normal;
            return label;
        }

        private static TMP_Text Stat(RectTransform bar, string caption, Color valueColor, bool coin)
        {
            RectTransform cell = Rect(caption, bar, Anchor.Center, Vector2.zero, new Vector2(150f, 56f));
            Label(cell, caption, 12, Muted, TextAlignmentOptions.Center, new Vector2(0f, -2f), new Vector2(150f, 16f), true, 3f, Anchor.Top);
            TMP_Text value = Label(cell, "", 28, valueColor, TextAlignmentOptions.Center, new Vector2(coin ? 10f : 0f, 2f), new Vector2(140f, 36f), true, 0f, Anchor.Bottom);
            if (coin) Box("Coin", cell, Anchor.Bottom, new Vector2(-34f, 13f), new Vector2(16f, 16f), Gold, 8f);
            return value;
        }

        private static TMP_Text BigStat(RectTransform parent, string caption, Vector2 position)
        {
            RectTransform cell = Rect(caption, parent, Anchor.Center, position, new Vector2(240f, 90f));
            Label(cell, caption, 13, Muted, TextAlignmentOptions.Center, Vector2.zero, new Vector2(240f, 18f), true, 3f, Anchor.Top);
            return Label(cell, "", 48, Text, TextAlignmentOptions.Center, Vector2.zero, new Vector2(240f, 64f), true, 0f, Anchor.Bottom);
        }

        /// <summary>A white 9-sliced rounded rectangle, generated once.</summary>
        private static Sprite RoundedSprite()
        {
            if (rounded != null) return rounded;
            const int size = 64, radius = 16;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            Color32[] pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Max(radius - x - 0.5f, x + 0.5f - (size - radius), 0f);
                    float dy = Mathf.Max(radius - y - 0.5f, y + 0.5f - (size - radius), 0f);
                    float alpha = Mathf.Clamp01(radius - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            rounded = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
            return rounded;
        }
    }
}
