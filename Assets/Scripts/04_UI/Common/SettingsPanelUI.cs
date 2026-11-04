using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using GalaxyStrike.UI.Loading;

namespace GalaxyStrike.UI
{
    public class SettingsPanelUI : MonoBehaviour
    {
        [Header("--- BGM Controls ---")]
        [SerializeField] private Slider bgmSlider;
        [SerializeField] private Toggle bgmToggle;

        [Header("--- SFX Controls ---")]
        [SerializeField] private Slider sfxSlider;
        [SerializeField] private Toggle sfxToggle;

        [Header("--- Navigation Buttons ---")]
        [SerializeField] private Button resumeBtn;
        [SerializeField] private Button mainMenuBtn;
        [SerializeField] private Button closeBtn;

        private void Awake()
        {
            RegisterEvents();
        }

        private void OnEnable()
        {
            SyncUIWithSavedSettings();
            ConfigureButtonsByScene();
        }

        private void RegisterEvents()
        {
            // Bắt sự kiện kéo thanh âm lượng
            if (bgmSlider != null) bgmSlider.onValueChanged.AddListener(OnBGMVolumeChanged);
            if (sfxSlider != null) sfxSlider.onValueChanged.AddListener(OnSFXVolumeChanged);

            // Bắt sự kiện Bật/Tắt Toggle
            if (bgmToggle != null) bgmToggle.onValueChanged.AddListener(OnBGMToggled);
            if (sfxToggle != null) sfxToggle.onValueChanged.AddListener(OnSFXToggled);

            // Bắt sự kiện nút bấm
            if (closeBtn != null) closeBtn.onClick.AddListener(ClosePanel);
            if (resumeBtn != null) resumeBtn.onClick.AddListener(OnResumeClicked);
            if (mainMenuBtn != null) mainMenuBtn.onClick.AddListener(OnMainMenuClicked);
        }

        public void SyncUIWithSavedSettings()
        {
            float bgmVol = PlayerPrefs.GetFloat("BGM_Volume", 0.8f);
            float sfxVol = PlayerPrefs.GetFloat("SFX_Volume", 0.8f);
            bool bgmOn = PlayerPrefs.GetInt("BGM_Mute", 1) == 1;
            bool sfxOn = PlayerPrefs.GetInt("SFX_Mute", 1) == 1;

            if (bgmSlider != null) bgmSlider.value = bgmVol;
            if (sfxSlider != null) sfxSlider.value = sfxVol;
            if (bgmToggle != null) bgmToggle.isOn = bgmOn;
            if (sfxToggle != null) sfxToggle.isOn = sfxOn;
        }

        // Tự động ẩn/hiện nút tùy theo đang ở HomeScene hay GameScene
        private void ConfigureButtonsByScene()
        {
            bool isInGameplay = SceneManager.GetActiveScene().name != "HomeScene";

            if (resumeBtn != null) resumeBtn.gameObject.SetActive(isInGameplay);
            if (mainMenuBtn != null) mainMenuBtn.gameObject.SetActive(isInGameplay);
        }

        #region UI Event Callbacks
        private void OnBGMVolumeChanged(float value)
        {
            PlayerPrefs.SetFloat("BGM_Volume", value);
            if (AudioManager.Instance != null) AudioManager.Instance.SetBGMVolume(value);
        }

        private void OnSFXVolumeChanged(float value)
        {
            PlayerPrefs.SetFloat("SFX_Volume", value);
            if (AudioManager.Instance != null) AudioManager.Instance.SetSFXVolume(value);
        }

        private void OnBGMToggled(bool isOn)
        {
            PlayerPrefs.SetInt("BGM_Mute", isOn ? 1 : 0);
            if (AudioManager.Instance != null) AudioManager.Instance.ToggleBGM(isOn);
        }

        private void OnSFXToggled(bool isOn)
        {
            PlayerPrefs.SetInt("SFX_Mute", isOn ? 1 : 0);
            if (AudioManager.Instance != null) AudioManager.Instance.ToggleSFX(isOn);
        }

        private void ClosePanel()
        {
            if (PanelManager.Instance != null)
            {
                PanelManager.Instance.CloseSettingsPanel();
            }
            else
            {
                gameObject.SetActive(false);
            }
        }

        private void OnResumeClicked()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.ResumeGame();
            }
            else
            {
                Time.timeScale = 1f;
                gameObject.SetActive(false);
            }
        }

        private void OnMainMenuClicked()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.LoadMainMenu();
            }
            else
            {
                Time.timeScale = 1f;
                LoadingSceneController.LoadScene("HomeScene");
            }
        }
        #endregion
    }
}
