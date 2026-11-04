using UnityEngine;
using UnityEngine.Audio;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("--- Audio Mixer ---")]
    [SerializeField] private AudioMixer mainMixer;

    [Header("--- Audio Sources ---")]
    [SerializeField] private AudioSource bgmSource;
    [SerializeField] private AudioSource sfxSource;

    [Header("--- Audio Clips ---")]
    public AudioClip bgmMusic;
    public AudioClip playerShootSFX;
    public AudioClip enemyExplodeSFX;
    public AudioClip coinCollectSFX;
    public AudioClip buttonClickSFX;

    // Tên khóa để lưu trong PlayerPrefs
    private const string BGM_VOL_KEY = "BGM_Volume";
    private const string SFX_VOL_KEY = "SFX_Volume";
    private const string BGM_MUTE_KEY = "BGM_Mute";
    private const string SFX_MUTE_KEY = "SFX_Mute";

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        LoadAudioSettings(); // Tải cài đặt cũ ngay khi vào game

        if (bgmMusic != null)
            PlayBGM(bgmMusic);
    }

    // Tải và áp dụng âm lượng từ bộ nhớ
    public void LoadAudioSettings()
    {
        float bgmVol = PlayerPrefs.GetFloat(BGM_VOL_KEY, 0.8f);
        float sfxVol = PlayerPrefs.GetFloat(SFX_VOL_KEY, 0.8f);
        bool bgmOn = PlayerPrefs.GetInt(BGM_MUTE_KEY, 1) == 1;
        bool sfxOn = PlayerPrefs.GetInt(SFX_MUTE_KEY, 1) == 1;

        ApplyVolume("BGMVol", bgmOn ? bgmVol : 0.0001f);
        ApplyVolume("SFXVol", sfxOn ? sfxVol : 0.0001f);
    }

    // --- CÁC HÀM XỬ LÝ SLIDER & TOGGLE ---
    public void SetBGMVolume(float value)
    {
        PlayerPrefs.SetFloat(BGM_VOL_KEY, value); // Lưu giá trị
        bool isBgmOn = PlayerPrefs.GetInt(BGM_MUTE_KEY, 1) == 1;
        if (isBgmOn) ApplyVolume("BGMVol", value);
    }

    public void SetSFXVolume(float value)
    {
        PlayerPrefs.SetFloat(SFX_VOL_KEY, value); // Lưu giá trị
        bool isSfxOn = PlayerPrefs.GetInt(SFX_MUTE_KEY, 1) == 1;
        if (isSfxOn) ApplyVolume("SFXVol", value);
    }

    public void ToggleBGM(bool isOn)
    {
        PlayerPrefs.SetInt(BGM_MUTE_KEY, isOn ? 1 : 0); // 1: Bật, 0: Mút
        float savedVol = PlayerPrefs.GetFloat(BGM_VOL_KEY, 0.8f);
        ApplyVolume("BGMVol", isOn ? savedVol : 0.0001f);
    }

    public void ToggleSFX(bool isOn)
    {
        PlayerPrefs.SetInt(SFX_MUTE_KEY, isOn ? 1 : 0);
        float savedVol = PlayerPrefs.GetFloat(SFX_VOL_KEY, 0.8f);
        ApplyVolume("SFXVol", isOn ? savedVol : 0.0001f);
    }

    private void ApplyVolume(string exposedParam, float value)
    {
        if (mainMixer == null)
            return;

        float db = Mathf.Log10(Mathf.Max(0.0001f, value)) * 20f;
        mainMixer.SetFloat(exposedParam, db);
    }

    // --- PHÁT AM THANH ---
    public void PlayBGM(AudioClip clip)
    {
        if (clip == null || bgmSource == null) return;
        bgmSource.clip = clip;
        bgmSource.loop = true;
        bgmSource.Play();
    }

    public void PlaySFX(AudioClip clip, float volumeScale = 1f)
    {
        if (clip == null || sfxSource == null) return;
        sfxSource.PlayOneShot(clip, volumeScale);
    }
}