using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using GalaxyStrike.Data;
using GalaxyStrike.Save;
using GalaxyStrike.UI.Loading;

namespace GalaxyStrike.UI.Home
{
    public class HomeSceneManager : MonoBehaviour
    {
        [Header("--- Top Bar Display ---")]
        [SerializeField] private TextMeshProUGUI energyText;
        [SerializeField] private TextMeshProUGUI energyRechargeText;
        [SerializeField] private TextMeshProUGUI coinText;
        [SerializeField] private TextMeshProUGUI gemText;
        [SerializeField] private TextMeshProUGUI levelText;
        [SerializeField] private TextMeshProUGUI nameText;

        [Header("--- Bottom Navigation Bar ---")]
        [SerializeField] private Button shopNavBtn;
        [SerializeField] private Button upgradeNavBtn;
        [SerializeField] private Button homeNavBtn;
        [SerializeField] private Button mapNavBtn;
        [SerializeField] private Button leaderNavBtn;

        [Header("--- Bottom Navigation Animation ---")]
        [SerializeField] private float navSelectedScale = 1.12f;
        [SerializeField] private float navAnimationDuration = 0.18f;

        [Header("--- UI Panels ---")]
        [SerializeField] private GameObject centerPanel;
        [SerializeField] private GameObject shopPanel;
        [SerializeField] private GameObject upgradePanel;
        [SerializeField] private GameObject mapPanel;
        [SerializeField] private GameObject leaderboardPanel;

        [Header("--- Collection Popup ---")]
        [SerializeField] private Button collectionButton;
        [SerializeField] private GameObject collectionBoard;
        [SerializeField] private Button closeCollectionButton;

        [Header("--- Backpack Popup ---")]
        [SerializeField] private Button backpackButton;
        private BackpackUIController backpackUI;

        [Header("--- Settings Popup ---")]
        [SerializeField] private Button settingsBtn;
        [SerializeField] private GameObject settingsPanel;
        [SerializeField] private Button closeSettingsBtn;

        [Header("--- Game Mode Selection ---")]
        [SerializeField] private Button playBtn;
        [SerializeField] private Button campaignBtn;
        [SerializeField] private Button challengeBtn;
        [SerializeField] private Button leftArrowBtn;
        [SerializeField] private Button rightArrowBtn;
        [SerializeField] private Sprite lockedPlaySprite;
        [SerializeField] private Sprite lockedCampaignSprite;
        [SerializeField] private Sprite lockedChallengeSprite;

        [Header("--- Ship Selection & Lock System ---")]
        [SerializeField] private GameObject[] shipList; // Kéo Ship_1 -> Ship_8 vào đây
        [SerializeField] private GameObject lockIcon;   // Biểu tượng ổ khóa (tùy chọn)
        private int currentShipIndex = 0;
        private bool gameStarting;

        /// <summary>Tab cần mở ngay khi HomeScene vào (ví dụ "Shop" khi hết năng lượng trong trận).</summary>
        private static string pendingTabOnLoad;
        private static string pendingToastOnLoad;
        private static ToastKind pendingToastKind;

        /// <summary>Cấp tài khoản người chơi đã được báo (để chỉ báo "LEVEL UP" một lần).</summary>
        private const string SeenAccountLevelKey = "GalaxyStrike_SeenAccountLevel";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            pendingTabOnLoad = null;
            pendingToastOnLoad = null;
        }

        /// <summary>
        /// Yêu cầu HomeScene mở sẵn một tab ở lần load kế tiếp (dùng một lần rồi xoá).
        /// </summary>
        public static void RequestTabOnLoad(string tabName)
        {
            pendingTabOnLoad = tabName;
        }

        /// <summary>
        /// Hiện một thông báo khi HomeScene vừa vào (dùng khi đổi Scene từ GameScene,
        /// vì toast hiện ngay sẽ trôi qua trong lúc màn hình Loading).
        /// </summary>
        public static void RequestToastOnLoad(string message, ToastKind kind = ToastKind.Info)
        {
            pendingToastOnLoad = message;
            pendingToastKind = kind;
        }
        private Coroutine navSelectionRoutine;

        public enum GameMode { Campaign, Challenge }
        private GameMode currentMode = GameMode.Campaign;

        private void OnEnable()
        {
            if (SaveManager.Instance != null)
                SaveManager.Instance.OnDataChanged += RefreshTopBarUI;
        }

        private void OnDisable()
        {
            // FindFirstObjectByType thay vì .Instance: getter sẽ tự tạo lại object khi scene đang đóng.
            SaveManager save = FindFirstObjectByType<SaveManager>();
            if (save != null)
                save.OnDataChanged -= RefreshTopBarUI;
        }

        private void Start()
        {
            InitUI();
            backpackUI = GetComponent<BackpackUIController>();
            if (backpackUI == null)
                backpackUI = gameObject.AddComponent<BackpackUIController>();

            if (backpackButton == null)
            {
                GameObject backpackObject = GameObject.Find("Backpack");
                if (backpackObject != null)
                    backpackButton = backpackObject.GetComponent<Button>();
            }

            RegisterButtonEvents();
            if (backpackUI != null)
                backpackUI.BindShopAction(() => OnTabSelected("Shop"));

            // Dấu báo đỏ trên các nút Missions / Daily / Gift / Battle Pass theo dữ liệu thật.
            if (GetComponent<HomeNotificationBadges>() == null)
                gameObject.AddComponent<HomeNotificationBadges>();

            ShowPendingNotices();
        }

        /// <summary>Thông báo chuyển từ Scene trước và báo lên cấp tài khoản (nếu có).</summary>
        private void ShowPendingNotices()
        {
            if (!string.IsNullOrEmpty(pendingToastOnLoad))
            {
                Toast.Show(pendingToastOnLoad, pendingToastKind, 3f);
                pendingToastOnLoad = null;
            }

            SaveManager save = SaveManager.Instance;
            int level = save.AccountLevel;
            int seen = PlayerPrefs.GetInt(SeenAccountLevelKey, 0);
            // Lần đầu có tính năng này: chỉ ghi nhận, không báo các cấp cũ.
            if (seen > 0 && level > seen)
            {
                int levels = level - seen;
                Toast.Show($"LEVEL UP!  You reached Lv.{level}\n+{levels * 3} Energy", ToastKind.LevelUp, 3.2f);
                AudioManager.Play(SfxCue.Victory);
            }
            if (level != seen)
            {
                PlayerPrefs.SetInt(SeenAccountLevelKey, level);
                PlayerPrefs.Save();
            }
        }

        private void InitUI()
        {
            // Hồi năng lượng theo thời gian thực đã trôi qua trước khi vẽ UI.
            SaveManager.Instance.ApplyEnergyRefill();

            currentShipIndex = SaveManager.Instance.SelectedShipIndex;

            string savedMode = PlayerPrefs.GetString("SelectedGameMode", GameMode.Campaign.ToString());
            if (System.Enum.TryParse(savedMode, out GameMode parsedMode))
            {
                currentMode = parsedMode;
            }

            RefreshTopBarUI();
            UpdateModeUI();

            // Mặc định hiển thị Home, trừ khi GameScene yêu cầu mở tab khác (vd: hết năng lượng -> Shop).
            string startTab = string.IsNullOrEmpty(pendingTabOnLoad) ? "Home" : pendingTabOnLoad;
            pendingTabOnLoad = null;
            OnTabSelected(startTab);

            if (settingsPanel != null) settingsPanel.SetActive(false);
            if (collectionBoard != null) collectionBoard.SetActive(false);
        }

        private void RefreshTopBarUI()
        {
            SaveManager save = SaveManager.Instance;
            if (save == null) return;

            if (energyText) energyText.text = save.Energy.ToString();
            RefreshEnergyCountdown(save);
            if (coinText) coinText.text = save.Coins.ToString("N0");
            if (gemText) gemText.text = save.Gems.ToString("N0");
            if (levelText) levelText.text = $"Lv.{save.Data.accountLevel}";
            if (nameText) nameText.text = save.Data.playerName;
        }

        private void RefreshEnergyCountdown(SaveManager save)
        {
            if (energyRechargeText == null)
                return;

            // Đồng hồ nằm cạnh icon trên thanh năng lượng: khi đầy thì báo MAX thay vì để trống.
            if (save.Energy >= save.MaxEnergy)
            {
                energyRechargeText.text = "MAX";
                return;
            }

            int seconds = save.SecondsUntilNextEnergy();
            energyRechargeText.text = $"{seconds / 60:00}:{seconds % 60:00}";
        }

        private float energyUiTimer;

        /// <summary>Cập nhật đồng hồ đếm ngược năng lượng mỗi giây, không cần chờ OnDataChanged.</summary>
        private void Update()
        {
            if (energyRechargeText == null)
                return;

            SaveManager save = SaveManager.Instance;
            if (save == null || save.Energy >= save.MaxEnergy)
                return;

            energyUiTimer += Time.unscaledDeltaTime;
            if (energyUiTimer < 1f)
                return;

            energyUiTimer = 0f;
            RefreshEnergyCountdown(save);
        }

        private void RegisterButtonEvents()
        {
            // 1. Navigation Bar
            if (shopNavBtn)    { shopNavBtn.onClick.RemoveAllListeners(); shopNavBtn.onClick.AddListener(() => OnTabSelected("Shop")); }
            if (upgradeNavBtn) { upgradeNavBtn.onClick.RemoveAllListeners(); upgradeNavBtn.onClick.AddListener(() => OnTabSelected("Upgrade")); }
            if (homeNavBtn)    { homeNavBtn.onClick.RemoveAllListeners(); homeNavBtn.onClick.AddListener(() => OnTabSelected("Home")); }
            if (mapNavBtn)     { mapNavBtn.onClick.RemoveAllListeners(); mapNavBtn.onClick.AddListener(() => OnTabSelected("Map")); }
            if (leaderNavBtn)  { leaderNavBtn.onClick.RemoveAllListeners(); leaderNavBtn.onClick.AddListener(() => OnTabSelected("Leaderboard")); }
            if (collectionButton) { collectionButton.onClick.RemoveAllListeners(); collectionButton.onClick.AddListener(OpenCollectionBoard); }
            if (closeCollectionButton) { closeCollectionButton.onClick.RemoveAllListeners(); closeCollectionButton.onClick.AddListener(CloseCollectionBoard); }
            if (backpackButton && backpackUI)
            {
                backpackButton.onClick.RemoveListener(backpackUI.OpenBackpack);
                backpackButton.onClick.AddListener(backpackUI.OpenBackpack);
            }

            // 2. Mũi tên chuyển tàu
            if (leftArrowBtn)  { leftArrowBtn.onClick.RemoveAllListeners(); leftArrowBtn.onClick.AddListener(SelectPreviousShip); }
            if (rightArrowBtn) { rightArrowBtn.onClick.RemoveAllListeners(); rightArrowBtn.onClick.AddListener(SelectNextShip); }

            // 3. Chọn Mode & Bắt đầu Game
            if (campaignBtn)   { campaignBtn.onClick.RemoveAllListeners(); campaignBtn.onClick.AddListener(() => StartGameWithMode(GameMode.Campaign)); }
            if (challengeBtn)  { challengeBtn.onClick.RemoveAllListeners(); challengeBtn.onClick.AddListener(() => StartGameWithMode(GameMode.Challenge)); }
            if (playBtn)       { playBtn.onClick.RemoveAllListeners(); playBtn.onClick.AddListener(() => StartGameWithMode(currentMode)); }

            // 4. Settings Popup
            if (settingsBtn)      { settingsBtn.onClick.RemoveAllListeners(); settingsBtn.onClick.AddListener(OpenSettingsPopup); }
            if (closeSettingsBtn) { closeSettingsBtn.onClick.RemoveAllListeners(); closeSettingsBtn.onClick.AddListener(CloseSettingsPopup); }
        }

        #region Public API Methods
        public void OpenSettingsPopup()
        {
            if (settingsPanel != null) settingsPanel.SetActive(true);
        }

        public void CloseSettingsPopup()
        {
            if (settingsPanel != null) settingsPanel.SetActive(false);
        }

        public void OpenCollectionBoard()
        {
            if (collectionBoard != null) collectionBoard.SetActive(true);
        }

        public void CloseCollectionBoard()
        {
            if (collectionBoard != null) collectionBoard.SetActive(false);
        }

        // Hàm gọi từ Shop để Mua / Mở khóa tàu mới
        public void UnlockShip(int shipIndex)
        {
            SaveManager.Instance.UnlockShip(shipIndex);
            UpdateShipDisplay();
            Debug.Log($"[HomeSceneManager] Unlocked Ship Index: {shipIndex}");
        }

        // Kiểm tra xem tàu index này đã mở khóa chưa
        public bool IsShipUnlocked(int shipIndex)
        {
            // Ship_1 (Index 0) mặc định luôn được sở hữu từ đầu
            return SaveManager.Instance.IsShipUnlocked(shipIndex);
        }
        #endregion

        #region Internal Logic
        private void SelectNextShip()
        {
            if (shipList == null || shipList.Length == 0) return;

            currentShipIndex = (currentShipIndex + 1) % shipList.Length;
            UpdateShipDisplay();
        }

        private void SelectPreviousShip()
        {
            if (shipList == null || shipList.Length == 0) return;

            currentShipIndex--;
            if (currentShipIndex < 0) currentShipIndex = shipList.Length - 1;
            UpdateShipDisplay();
        }

        private void UpdateShipDisplay()
        {
            if (shipList == null) return;

            bool isUnlocked = IsShipUnlocked(currentShipIndex);

            for (int i = 0; i < shipList.Length; i++)
            {
                if (shipList[i] != null)
                {
                    bool isActive = (i == currentShipIndex);
                    shipList[i].SetActive(isActive);

                    // Xử lý đồ họa cho Tàu đang hiển thị
                    if (isActive)
                    {
                        Image shipImage = shipList[i].GetComponent<Image>();
                        if (shipImage != null)
                        {
                            // Đã sở hữu: Giữ màu sáng chuẩn | Chưa sở hữu: Làm tối xám (25% sáng)
                            shipImage.color = isUnlocked ? Color.white : new Color(0.25f, 0.25f, 0.25f, 1f);
                        }
                    }
                }
            }

            // Hiển thị / Ẩn icon Ổ khóa
            if (lockIcon != null)
            {
                lockIcon.SetActive(!isUnlocked);
            }

            UpdateShipAction(playBtn, lockedPlaySprite, isUnlocked);
            UpdateShipAction(campaignBtn, lockedCampaignSprite, isUnlocked);
            UpdateShipAction(challengeBtn, lockedChallengeSprite, isUnlocked);
        }

        private static void UpdateShipAction(Button button, Sprite lockedSprite, bool isUnlocked)
        {
            if (!button)
                return;

            button.interactable = isUnlocked;
            if (button.image)
                button.image.overrideSprite = isUnlocked ? null : lockedSprite;
        }

        public void PlayCampaignFromMap()
        {
            StartGameWithMode(GameMode.Campaign);
        }

        public void PlayCampaignStageFromMap(int stage)
        {
            if (stage < 1 || stage > LevelManager.CampaignLength)
                return;
            StartGameWithMode(GameMode.Campaign, stage);
        }

        private void StartGameWithMode(GameMode mode, int? stage = null)
        {
            if (gameStarting)
                return;

            // Chặn vào trận nếu tàu chưa mở khóa
            if (!IsShipUnlocked(currentShipIndex))
            {
                Toast.Show("This ship is locked.\nUnlock it in the Store.", ToastKind.Warning);
                OnTabSelected("Shop");
                return;
            }

            currentMode = mode;
            UpdateModeUI();

            SaveManager save = SaveManager.Instance;

            gameStarting = true;
            // Challenge: không tiêu năng lượng (không ảnh hưởng tiến trình chiến dịch).
            if (currentMode == GameMode.Challenge || save.TryConsumeEnergy())
            {
                Debug.Log($"[HomeSceneManager] Starting Game - Mode: {currentMode} | Ship Index: {currentShipIndex}");

                save.SelectShip(currentShipIndex);

                PlayerPrefs.SetString("SelectedGameMode", currentMode.ToString());
                // Chiến dịch: màn chọn từ bản đồ, hoặc màn xa nhất đã mở khi bấm Play.
                if (currentMode == GameMode.Campaign)
                    LevelManager.Instance.SetLevel(stage ?? LevelManager.FrontierLevel);
                PlayerPrefs.Save();

                LoadingSceneController.LoadScene("GameScene");
            }
            else
            {
                gameStarting = false;
                int wait = save.SecondsUntilNextEnergy();
                Toast.Show($"Not enough energy! Need {save.EnergyCostPerRun}, you have {save.Energy}.\n" +
                           $"+1 in {wait / 60:00}:{wait % 60:00}", ToastKind.Warning, 3f);
                OnTabSelected("Shop");
            }
        }

        private void UpdateModeUI()
        {
            if (campaignBtn) campaignBtn.transform.localScale = (currentMode == GameMode.Campaign) ? new Vector3(1.1f, 1.1f, 1f) : Vector3.one;
            if (challengeBtn) challengeBtn.transform.localScale = (currentMode == GameMode.Challenge) ? new Vector3(1.1f, 1.1f, 1f) : Vector3.one;
        }

        public void OnTabSelected(string tabName)
        {
            // Home actions are a sibling of the panels; transparent shop UI must hide them explicitly.
            if (playBtn != null && playBtn.transform.parent != null)
                playBtn.transform.parent.gameObject.SetActive(tabName == "Home");
            // Nút Settings nằm chung lớp UI_Panels nên đè lên tiêu đề Store/Upgrade/Leaderboard;
            // chỉ hiện ở tab Home.
            if (settingsBtn != null)
                settingsBtn.gameObject.SetActive(tabName == "Home");
            if (centerPanel) centerPanel.SetActive(false);
            if (shopPanel) shopPanel.SetActive(false);
            if (upgradePanel) upgradePanel.SetActive(false);
            if (mapPanel) mapPanel.SetActive(false);
            if (leaderboardPanel) leaderboardPanel.SetActive(false);

            switch (tabName)
            {
                case "Home":
                    if (centerPanel) centerPanel.SetActive(true);
                    UpdateShipDisplay();
                    break;
                case "Shop":
                    if (shopPanel) shopPanel.SetActive(true);
                    break;
                case "Upgrade":
                    if (upgradePanel) upgradePanel.SetActive(true);
                    break;
                case "Map":
                    if (mapPanel) mapPanel.SetActive(true);
                    break;
                case "Leaderboard":
                    if (leaderboardPanel) leaderboardPanel.SetActive(true);
                    break;
            }

            AnimateNavSelection(tabName);
        }

        private void AnimateNavSelection(string selectedTab)
        {
            if (navSelectionRoutine != null)
                StopCoroutine(navSelectionRoutine);

            navSelectionRoutine = StartCoroutine(AnimateNavSelectionRoutine(selectedTab));
        }

        private IEnumerator AnimateNavSelectionRoutine(string selectedTab)
        {
            Button[] buttons = { shopNavBtn, upgradeNavBtn, homeNavBtn, mapNavBtn, leaderNavBtn };
            string[] tabNames = { "Shop", "Upgrade", "Home", "Map", "Leaderboard" };
            Vector3[] startIconScales = new Vector3[buttons.Length];
            Color[] startBackgroundColors = new Color[buttons.Length];
            Color[] startLabelColors = new Color[buttons.Length];
            Color[] startIconColors = new Color[buttons.Length];
            bool[] selectedStates = new bool[buttons.Length];

            Canvas.ForceUpdateCanvases();

            for (int i = 0; i < buttons.Length; i++)
            {
                Button button = buttons[i];
                if (button == null)
                    continue;

                selectedStates[i] = tabNames[i] == selectedTab;

                Transform tabBackgroundTransform = button.transform.Find("Tab_Background");
                Image background = tabBackgroundTransform != null ? tabBackgroundTransform.GetComponent<Image>() : null;
                startBackgroundColors[i] = background != null ? background.color : Color.clear;

                TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>(true);
                startLabelColors[i] = label != null ? label.color : Color.white;

                Transform iconTransform = button.transform.Find("Icon");
                Image icon = iconTransform != null ? iconTransform.GetComponent<Image>() : null;
                startIconScales[i] = icon != null ? icon.rectTransform.localScale : Vector3.one;
                startIconColors[i] = icon != null ? icon.color : Color.white;

            }

            float duration = Mathf.Max(0.01f, navAnimationDuration);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                for (int i = 0; i < buttons.Length; i++)
                {
                    Button button = buttons[i];
                    if (button == null)
                        continue;

                    bool selected = selectedStates[i];
                    Transform tabBackgroundTransform = button.transform.Find("Tab_Background");
                    Image background = tabBackgroundTransform != null ? tabBackgroundTransform.GetComponent<Image>() : null;
                    if (background != null)
                    {
                        Color targetColor = selected ? Color.white : new Color(1f, 1f, 1f, 0f);
                        background.color = Color.Lerp(startBackgroundColors[i], targetColor, t);
                    }

                    TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>(true);
                    if (label != null)
                    {
                        Color targetColor = selected ? Color.white : new Color(0.72f, 0.82f, 0.94f, 0f);
                        label.color = Color.Lerp(startLabelColors[i], targetColor, t);
                    }

                    Transform iconTransform = button.transform.Find("Icon");
                    Image icon = iconTransform != null ? iconTransform.GetComponent<Image>() : null;
                    if (icon != null)
                    {
                        float targetScale = selected ? navSelectedScale : 1f;
                        icon.rectTransform.localScale = Vector3.Lerp(startIconScales[i], Vector3.one * targetScale, t);
                        Color targetColor = selected ? Color.white : new Color(0.82f, 0.90f, 1f, 0.88f);
                        icon.color = Color.Lerp(startIconColors[i], targetColor, t);
                    }
                }

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            for (int i = 0; i < buttons.Length; i++)
            {
                Button button = buttons[i];
                if (button == null)
                    continue;

                bool selected = selectedStates[i];
                Transform tabBackgroundTransform = button.transform.Find("Tab_Background");
                Image background = tabBackgroundTransform != null ? tabBackgroundTransform.GetComponent<Image>() : null;
                if (background != null)
                    background.color = selected
                        ? Color.white
                        : new Color(1f, 1f, 1f, 0f);

                TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>(true);
                if (label != null)
                    label.color = selected ? Color.white : new Color(0.72f, 0.82f, 0.94f, 0f);

                Transform iconTransform = button.transform.Find("Icon");
                Image icon = iconTransform != null ? iconTransform.GetComponent<Image>() : null;
                if (icon != null)
                {
                    icon.rectTransform.localScale = Vector3.one * (selected ? navSelectedScale : 1f);
                    icon.color = selected ? Color.white : new Color(0.82f, 0.90f, 1f, 0.88f);
                }
            }

            navSelectionRoutine = null;
        }
        #endregion
    }
}
