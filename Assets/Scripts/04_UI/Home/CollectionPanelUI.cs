using System.Collections.Generic;
using System.Linq;
using GalaxyStrike.Data;
using GalaxyStrike.Save;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GalaxyStrike.UI.Home
{
    /// <summary>
    /// Bảng Collection: hai tab ENEMIES / MY SHIPS và màn chi tiết khi bấm vào một ô.
    ///  - ENEMIES: lưới quái có sẵn trong scene; chỉ số (giáp, điểm) lấy từ prefab quái thật
    ///    trong Resources/Prefabs, khớp theo sprite.
    ///  - MY SHIPS: dựng từ danh sách tàu của Shop, hiện trạng thái EQUIPPED / OWNED / LOCKED.
    /// Gắn lúc chạy vào Collection_Board (HomeSceneManager), mọi phần thêm mới dựng bằng code.
    /// </summary>
    public sealed class CollectionPanelUI : MonoBehaviour
    {
        private const string ActiveTabSprite = "UI/Toast/Panel_Info";
        private const string CardSprite = "UI/Toast/Panel_Info";

        private static readonly Color TitleColor = new Color(0.855f, 0.953f, 1f);
        private static readonly Color MutedColor = new Color(0.6f, 0.72f, 0.85f);
        private static readonly Color GoldColor = new Color(1f, 0.82f, 0.3f);
        private static readonly Color GreenColor = new Color(0.4f, 1f, 0.6f);

        private static readonly Dictionary<string, string> EnemyNotes = new Dictionary<string, string>
        {
            { "DRONE FIGHTER", "Light scout drone. Fragile and fast, it usually arrives in large groups." },
            { "LASER TURRET", "Armed gunship that keeps up steady fire from range." },
            { "MINE", "Small fighter that drifts into your path. Clear it before it closes in." },
            { "BOMBER", "Strike craft built for quick attack runs." },
            { "SNIPER", "Armoured cruiser with long-range cannons. Keep moving." },
            { "SWARMER", "Weak alone, dangerous in numbers. Sweep them with spread fire." },
            { "SHIELD UNIT", "Heavily plated support ship that soaks up damage." },
            { "HEAVY CRUISER", "Tough destroyer-class hull. Focus fire to bring it down." },
            { "TWIN CANNON", "Fires paired shots. Watch the gap between its bullets." },
            { "MISSILE SHIP", "Launches missiles that punish standing still." },
            { "STINGER", "Elite interceptor with a thick hull and aggressive patterns." },
            { "WASP", "Agile attacker that weaves through the formation." },
            { "DREADNOUGHT", "Mini-boss flagship. Expect heavy fire and a long fight." },
            { "ALIEN BOSS", "Alien commander guarding the deep zones." },
            { "FINAL BOSS", "The mothership core. The last obstacle of the campaign." },
        };

        private sealed class EnemySlot
        {
            public GameObject root;
            public Sprite sprite;
            public string name;
        }

        private readonly List<EnemySlot> enemySlots = new List<EnemySlot>();
        private readonly List<GameObject> shipSlots = new List<GameObject>();
        private Dictionary<string, List<Enemy>> enemiesBySprite;

        private RectTransform content;
        private RectTransform grid;
        private ScrollRect scroll;
        private TMP_FontAsset font;
        private Image enemiesTab, shipsTab;
        private TextMeshProUGUI enemiesTabLabel, shipsTabLabel;
        private bool showingShips;

        private GameObject detail;
        private TextMeshProUGUI detailTitle, detailStats, detailNote;
        private Image detailImage;

        private void Awake()
        {
            content = transform.Find("Collection_Content") as RectTransform;
            grid = content != null ? content.Find("Collection_ScrollView/Content") as RectTransform : null;
            scroll = content != null ? content.GetComponentInChildren<ScrollRect>(true) : null;
            if (content == null || grid == null)
            {
                enabled = false;
                return;
            }

            Transform section = content.Find("Section_Label");
            TMP_Text sectionText = section != null ? section.GetComponent<TMP_Text>() : null;
            font = sectionText != null ? sectionText.font : TMP_Settings.defaultFontAsset;
            if (section != null)
                section.gameObject.SetActive(false);

            CollectEnemySlots();
            BuildTabs(section != null ? ((RectTransform)section).anchoredPosition.y : 567f);
            BuildShipSlots();
            BuildDetail();
            ShowTab(false);
        }

        private void OnEnable()
        {
            if (detail != null)
                detail.SetActive(false);
            if (enabled && shipSlots.Count > 0)
                RefreshShipSlots();
        }

        // =====================================================================
        // TABS
        // =====================================================================

        private void BuildTabs(float y)
        {
            Sprite tab = Resources.Load<Sprite>(ActiveTabSprite);
            enemiesTab = CreateTab("Tab_Enemies", "ENEMIES", new Vector2(-215f, y), tab, out enemiesTabLabel);
            shipsTab = CreateTab("Tab_MyShips", "MY SHIPS", new Vector2(215f, y), tab, out shipsTabLabel);
            enemiesTab.GetComponent<Button>().onClick.AddListener(() => ShowTab(false));
            shipsTab.GetComponent<Button>().onClick.AddListener(() => ShowTab(true));
        }

        private Image CreateTab(string name, string text, Vector2 position, Sprite sprite, out TextMeshProUGUI label)
        {
            var rect = NewRect(name, content, position, new Vector2(400f, 96f));
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.type = sprite != null && sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
            rect.gameObject.AddComponent<Button>().targetGraphic = image;
            label = NewText("Label", rect, Vector2.zero, new Vector2(380f, 80f), text, 44f, TextAlignmentOptions.Center);
            label.characterSpacing = 4f;
            return image;
        }

        private void ShowTab(bool ships)
        {
            showingShips = ships;
            foreach (EnemySlot slot in enemySlots)
                slot.root.SetActive(!ships);
            foreach (GameObject slot in shipSlots)
                slot.SetActive(ships);
            if (ships)
                RefreshShipSlots();

            SetTabState(enemiesTab, enemiesTabLabel, !ships);
            SetTabState(shipsTab, shipsTabLabel, ships);
            if (scroll != null)
                scroll.verticalNormalizedPosition = 1f;
        }

        private static void SetTabState(Image tab, TextMeshProUGUI label, bool active)
        {
            tab.color = active ? Color.white : new Color(0.38f, 0.45f, 0.58f, 0.9f);
            label.color = active ? TitleColor : MutedColor;
        }

        // =====================================================================
        // ENEMIES
        // =====================================================================

        private void CollectEnemySlots()
        {
            foreach (Transform child in grid)
            {
                var slot = new EnemySlot { root = child.gameObject };
                Transform image = child.Find("Unit_Image");
                Image unitImage = image != null ? image.GetComponent<Image>() : null;
                slot.sprite = unitImage != null ? unitImage.sprite : null;
                Transform name = child.Find("Name_Label");
                TMP_Text nameText = name != null ? name.GetComponent<TMP_Text>() : null;
                slot.name = nameText != null ? nameText.text : child.name;
                enemySlots.Add(slot);

                Button button = child.GetComponent<Button>();
                if (button != null)
                    button.onClick.AddListener(() => ShowEnemyDetail(slot));
            }
        }

        private void ShowEnemyDetail(EnemySlot slot)
        {
            Enemy enemy = FindEnemyPrefab(slot);
            if (slot.sprite == null || enemy == null)
            {
                ShowDetail("UNKNOWN", null,
                    "THREAT  <color=#FFD24D>???</color>",
                    "This enemy has not been identified yet.\nKeep advancing through the campaign to discover it.");
                return;
            }

            int hull = enemy.BaseHealth;
            bool boss = slot.name.Contains("BOSS") || enemy.name.Contains("Boss") || hull > 12;
            string threat = boss ? "<color=#FF6B6B>BOSS</color>"
                : hull > 5 ? "<color=#FF9F43>HIGH</color>"
                : hull > 2 ? "<color=#FFD24D>MEDIUM</color>"
                : "<color=#6BFF9E>LOW</color>";
            string stats = $"HULL  <b>{hull}</b>\nSCORE  <b>{enemy.ScoreValue:N0}</b>\nTHREAT  {threat}";
            EnemyNotes.TryGetValue(slot.name, out string note);
            ShowDetail(slot.name, slot.sprite, stats, note ?? "Hostile unit of the invading fleet.");
        }

        /// <summary>
        /// Tìm prefab quái có cùng sprite với ô. Nhiều prefab dùng chung sprite (quái thường và
        /// boss khu vực) nên: ô tên có BOSS -> chọn bản giáp dày nhất; còn lại -> ưu tiên bản không
        /// phải boss, giáp mỏng nhất.
        /// </summary>
        private Enemy FindEnemyPrefab(EnemySlot slot)
        {
            if (slot.sprite == null)
                return null;

            if (enemiesBySprite == null)
            {
                enemiesBySprite = new Dictionary<string, List<Enemy>>();
                foreach (Enemy prefab in Resources.LoadAll<Enemy>("Prefabs"))
                {
                    SpriteRenderer renderer = prefab.GetComponentInChildren<SpriteRenderer>(true);
                    if (renderer == null || renderer.sprite == null)
                        continue;
                    if (!enemiesBySprite.TryGetValue(renderer.sprite.name, out List<Enemy> list))
                        enemiesBySprite[renderer.sprite.name] = list = new List<Enemy>();
                    list.Add(prefab);
                }
            }

            if (!enemiesBySprite.TryGetValue(slot.sprite.name, out List<Enemy> candidates) || candidates.Count == 0)
                return null;

            if (slot.name.Contains("BOSS"))
                return candidates.OrderByDescending(e => e.BaseHealth).First();

            return candidates
                .OrderBy(e => e.name.Contains("Boss_") ? 1 : 0)
                .ThenBy(e => e.BaseHealth)
                .First();
        }

        // =====================================================================
        // MY SHIPS
        // =====================================================================

        private IReadOnlyList<ShipDataSO> Ships()
        {
            ShopManager shop = FindAnyObjectByType<ShopManager>(FindObjectsInactive.Include);
            return shop != null && shop.Ships != null ? shop.Ships : new ShipDataSO[0];
        }

        private void BuildShipSlots()
        {
            if (enemySlots.Count == 0)
                return;

            GameObject template = enemySlots[0].root;
            IReadOnlyList<ShipDataSO> ships = Ships();
            for (int i = 0; i < ships.Count; i++)
            {
                if (ships[i] == null)
                    continue;

                GameObject slot = Instantiate(template, grid);
                slot.name = "Ship_Slot_" + (i + 1);
                Button button = slot.GetComponent<Button>();
                if (button != null)
                {
                    button.onClick.RemoveAllListeners();
                    int index = i;
                    button.onClick.AddListener(() => ShowShipDetail(index));
                }

                Transform image = slot.transform.Find("Unit_Image");
                if (image != null && image.GetComponent<Image>() is Image unitImage)
                {
                    unitImage.sprite = ships[i].shipSprite;
                    unitImage.preserveAspect = true;
                }

                Transform name = slot.transform.Find("Name_Label");
                if (name != null && name.GetComponent<TMP_Text>() is TMP_Text nameText)
                    nameText.text = ships[i].shipName.ToUpperInvariant();

                var status = NewText("Status_Label", (RectTransform)slot.transform, new Vector2(0f, -106f),
                    new Vector2(200f, 30f), string.Empty, 22f, TextAlignmentOptions.Center);
                status.characterSpacing = 3f;
                shipSlots.Add(slot);
            }
        }

        private void RefreshShipSlots()
        {
            SaveManager save = SaveManager.Instance;
            IReadOnlyList<ShipDataSO> ships = Ships();
            int slotIndex = 0;
            for (int i = 0; i < ships.Count && slotIndex < shipSlots.Count; i++)
            {
                if (ships[i] == null)
                    continue;

                GameObject slot = shipSlots[slotIndex++];
                bool owned = save != null && save.IsShipUnlocked(i);
                bool equipped = owned && save.SelectedShipIndex == i;

                Transform image = slot.transform.Find("Unit_Image");
                if (image != null && image.GetComponent<Image>() is Image unitImage)
                    unitImage.color = owned ? Color.white : new Color(0.28f, 0.32f, 0.42f, 1f);

                Transform status = slot.transform.Find("Status_Label");
                if (status != null && status.GetComponent<TMP_Text>() is TMP_Text statusText)
                {
                    statusText.text = equipped ? "EQUIPPED" : owned ? "OWNED" : "LOCKED";
                    statusText.color = equipped ? GreenColor : owned ? TitleColor : MutedColor;
                }
            }
        }

        private void ShowShipDetail(int index)
        {
            IReadOnlyList<ShipDataSO> ships = Ships();
            if (index < 0 || index >= ships.Count || ships[index] == null)
                return;

            ShipDataSO ship = ships[index];
            SaveManager save = SaveManager.Instance;
            bool owned = save != null && save.IsShipUnlocked(index);
            bool equipped = owned && save.SelectedShipIndex == index;
            string currency = ship.currencyType == CurrencyType.Gem ? "GEMS" : "COINS";
            string status = equipped ? "<color=#66FF99>EQUIPPED</color>"
                : owned ? "<color=#DAF3FF>OWNED</color>"
                : $"<color=#FFD24D>LOCKED  ·  {ship.price:N0} {currency}</color>";
            string stats = $"HULL  <b>{ship.maxHealth}</b>\n" +
                           $"FIRE/s  <b>{1f / Mathf.Max(0.01f, ship.fireRate):0.00}</b>\n" +
                           $"SPEED  <b>{ship.moveSpeed:0.0}</b>\n" +
                           status;
            string note = owned
                ? "Select it on the Home screen to fly it. Upgrades apply to every ship."
                : "Unlock it in the Store to add it to your fleet.";
            ShowDetail(ship.shipName.ToUpperInvariant(), ship.shipSprite, stats, note);
        }

        // =====================================================================
        // DETAIL VIEW
        // =====================================================================

        private void BuildDetail()
        {
            var overlay = NewRect("Collection_Detail", content, Vector2.zero, Vector2.zero);
            overlay.anchorMin = Vector2.zero;
            overlay.anchorMax = Vector2.one;
            overlay.offsetMin = overlay.offsetMax = Vector2.zero;
            var dim = overlay.gameObject.AddComponent<Image>();
            dim.color = new Color(0.01f, 0.03f, 0.08f, 0.82f);
            overlay.gameObject.AddComponent<Button>().onClick.AddListener(HideDetail);

            Sprite cardSprite = Resources.Load<Sprite>(CardSprite);
            var card = NewRect("Detail_Card", overlay, new Vector2(0f, 20f), new Vector2(880f, 1100f));
            var cardImage = card.gameObject.AddComponent<Image>();
            cardImage.sprite = cardSprite;
            cardImage.type = cardSprite != null && cardSprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
            cardImage.raycastTarget = true; // bấm vào thẻ không đóng

            detailTitle = NewText("Title", card, new Vector2(0f, 430f), new Vector2(780f, 90f), string.Empty, 64f, TextAlignmentOptions.Center);
            detailTitle.characterSpacing = 4f;

            var imageRect = NewRect("Image", card, new Vector2(0f, 170f), new Vector2(420f, 360f));
            detailImage = imageRect.gameObject.AddComponent<Image>();
            detailImage.preserveAspect = true;
            detailImage.raycastTarget = false;

            detailStats = NewText("Stats", card, new Vector2(0f, -150f), new Vector2(760f, 230f), string.Empty, 44f, TextAlignmentOptions.Center);
            detailStats.lineSpacing = 8f;
            detailNote = NewText("Note", card, new Vector2(0f, -360f), new Vector2(760f, 150f), string.Empty, 32f, TextAlignmentOptions.Center);
            detailNote.textWrappingMode = TextWrappingModes.Normal;
            detailNote.color = MutedColor;
            detailNote.fontStyle = FontStyles.Normal;

            var hint = NewText("Hint", overlay, new Vector2(0f, -620f), new Vector2(760f, 50f), "TAP ANYWHERE TO CLOSE", 28f, TextAlignmentOptions.Center);
            hint.color = MutedColor;
            hint.characterSpacing = 6f;

            detail = overlay.gameObject;
            detail.SetActive(false);
            PopupTransition.Attach(detail, card);
        }

        private void ShowDetail(string title, Sprite sprite, string stats, string note)
        {
            detailTitle.text = title;
            detailImage.sprite = sprite;
            detailImage.enabled = sprite != null;
            detailStats.text = stats;
            detailNote.text = note;
            detail.transform.SetAsLastSibling();
            detail.SetActive(true);
            AudioManager.Play(SfxCue.PowerUp);
        }

        private void HideDetail() => PopupTransition.Close(detail);

        // =====================================================================
        // HELPERS
        // =====================================================================

        private static RectTransform NewRect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.gameObject.layer = 5;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private TextMeshProUGUI NewText(string name, RectTransform parent, Vector2 position, Vector2 size, string text,
            float fontSize, TextAlignmentOptions alignment)
        {
            var rect = NewRect(name, parent, position, size);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.fontSize = fontSize;
            label.fontStyle = FontStyles.Bold;
            label.alignment = alignment;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.raycastTarget = false;
            label.color = TitleColor;
            label.text = text;
            return label;
        }
    }
}
