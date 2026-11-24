using System.Collections;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Âm thanh toàn game (BGM + SFX), sống qua các Scene.
///
/// GameBootstrap tạo AudioManager TRƯỚC khi Scene đầu tiên load, nên bản đặt sẵn trong
/// HomeScene/GameScene (có mixer, AudioSource, clip) luôn là "bản trùng". Trước đây bản trùng
/// bị huỷ và mang theo toàn bộ cấu hình -> game không có tiếng. Giờ bản đang sống
/// nhận lại cấu hình của bản trong Scene (<see cref="AdoptSceneConfiguration"/>) trước khi nó bị huỷ,
/// đồng thời dùng nhạc nền của Scene đó.
/// </summary>
public sealed class AudioManager : SingletonBehaviour<AudioManager>
{
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

    [Header("--- Boss Music (để trống: Resources/Audio/Intercept_Course) ---")]
    [SerializeField] private AudioClip bossMusic;

    [Header("--- Gameplay Cues (để trống: dùng âm sinh bằng code) ---")]
    [SerializeField] private AudioClip hitSFX;
    [SerializeField] private AudioClip playerHurtSFX;
    [SerializeField] private AudioClip powerUpSFX;
    [SerializeField] private AudioClip skillSFX;
    [SerializeField] private AudioClip bossWarningSFX;
    [SerializeField] private AudioClip victorySFX;
    [SerializeField] private AudioClip defeatSFX;
    [SerializeField] private AudioClip rewardSFX;
    [SerializeField] private AudioClip reviveSFX;

    private const string BossMusicResource = "Audio/Intercept_Course";
    private const float MusicFadeSeconds = 0.6f;

    // Tên khóa để lưu trong PlayerPrefs
    private const string BGM_VOL_KEY = "BGM_Volume";
    private const string SFX_VOL_KEY = "SFX_Volume";
    private const string BGM_MUTE_KEY = "BGM_Mute";
    private const string SFX_MUTE_KEY = "SFX_Mute";

    private readonly float[] nextCueTime = new float[System.Enum.GetValues(typeof(SfxCue)).Length];
    private float bgmBaseVolume = 1f;
    private bool sourcesFromScene;
    private Coroutine musicFade;

    /// <summary>
    /// Bản trong Scene vừa Awake trong khi đã có AudioManager sống: chuyển cấu hình sang
    /// bản đang sống trước, rồi base.Awake mới huỷ bản trùng.
    /// </summary>
    /// <summary>Mỗi Scene đều có một AudioManager chỉ để mang cấu hình sang -> huỷ là bình thường.</summary>
    protected override bool WarnOnDuplicate => false;

    protected override void Awake()
    {
        if (HasInstance && Instance != this)
            Instance.AdoptSceneConfiguration(this);

        base.Awake();
    }

    protected override void OnSingletonAwake()
    {
        EnsureSources();
    }

    private void Start()
    {
        LoadAudioSettings(); // Tải cài đặt cũ ngay khi vào game

        if (bgmMusic != null && !bgmSource.isPlaying)
            PlayBGM(bgmMusic);
    }

    // =====================================================================
    // CẤU HÌNH
    // =====================================================================

    /// <summary>Tự tạo AudioSource khi bản này được sinh bằng code (không có trong Scene).</summary>
    private void EnsureSources()
    {
        sourcesFromScene = bgmSource != null && sfxSource != null;

        if (bgmSource == null)
        {
            bgmSource = gameObject.AddComponent<AudioSource>();
            bgmSource.loop = true;
            bgmSource.playOnAwake = false;
        }

        if (sfxSource == null)
        {
            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
        }

        bgmBaseVolume = bgmSource.volume;
    }

    /// <summary>
    /// Lấy mixer, clip và thiết lập AudioSource của AudioManager đặt trong Scene.
    /// Chỉ điền chỗ còn trống, riêng nhạc nền thì theo Scene (Home / Game có thể khác nhau).
    /// </summary>
    private void AdoptSceneConfiguration(AudioManager scene)
    {
        if (scene == null)
            return;

        if (mainMixer == null) mainMixer = scene.mainMixer;
        if (playerShootSFX == null) playerShootSFX = scene.playerShootSFX;
        if (enemyExplodeSFX == null) enemyExplodeSFX = scene.enemyExplodeSFX;
        if (coinCollectSFX == null) coinCollectSFX = scene.coinCollectSFX;
        if (buttonClickSFX == null) buttonClickSFX = scene.buttonClickSFX;
        if (bossMusic == null) bossMusic = scene.bossMusic;
        if (hitSFX == null) hitSFX = scene.hitSFX;
        if (playerHurtSFX == null) playerHurtSFX = scene.playerHurtSFX;
        if (powerUpSFX == null) powerUpSFX = scene.powerUpSFX;
        if (skillSFX == null) skillSFX = scene.skillSFX;
        if (bossWarningSFX == null) bossWarningSFX = scene.bossWarningSFX;
        if (victorySFX == null) victorySFX = scene.victorySFX;
        if (defeatSFX == null) defeatSFX = scene.defeatSFX;
        if (rewardSFX == null) rewardSFX = scene.rewardSFX;
        if (reviveSFX == null) reviveSFX = scene.reviveSFX;

        // AudioSource tạo bằng code chưa có mixer group / âm lượng của designer.
        if (!sourcesFromScene)
        {
            CopySourceSettings(scene.bgmSource, bgmSource);
            CopySourceSettings(scene.sfxSource, sfxSource);
            bgmBaseVolume = bgmSource.volume;
            sourcesFromScene = scene.bgmSource != null && scene.sfxSource != null;
        }

        LoadAudioSettings();

        AudioClip sceneMusic = scene.bgmMusic != null ? scene.bgmMusic : bgmMusic;
        if (bgmMusic == null)
            bgmMusic = sceneMusic;
        if (sceneMusic != null && (bgmSource.clip != sceneMusic || !bgmSource.isPlaying))
            CrossfadeTo(sceneMusic);
    }

    private static void CopySourceSettings(AudioSource from, AudioSource to)
    {
        if (from == null || to == null)
            return;

        to.outputAudioMixerGroup = from.outputAudioMixerGroup;
        to.volume = from.volume;
        to.pitch = from.pitch;
        to.priority = from.priority;
        to.spatialBlend = from.spatialBlend;
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

    // =====================================================================
    // NHẠC NỀN
    // =====================================================================

    public void PlayBGM(AudioClip clip)
    {
        if (clip == null || bgmSource == null) return;
        if (musicFade != null)
        {
            StopCoroutine(musicFade);
            musicFade = null;
        }

        bgmSource.clip = clip;
        bgmSource.loop = true;
        bgmSource.volume = bgmBaseVolume;
        bgmSource.Play();
    }

    /// <summary>Chuyển nhạc mượt (tắt dần -> đổi bài -> to dần). Chạy cả khi game đang pause.</summary>
    public void CrossfadeTo(AudioClip clip)
    {
        if (clip == null || bgmSource == null)
            return;

        if (!bgmSource.isPlaying || bgmSource.clip == null || !isActiveAndEnabled)
        {
            PlayBGM(clip);
            return;
        }

        if (bgmSource.clip == clip)
            return;

        if (musicFade != null)
            StopCoroutine(musicFade);
        musicFade = StartCoroutine(CrossfadeRoutine(clip));
    }

    private IEnumerator CrossfadeRoutine(AudioClip clip)
    {
        float start = bgmSource.volume;
        for (float t = 0f; t < MusicFadeSeconds; t += Time.unscaledDeltaTime)
        {
            bgmSource.volume = Mathf.Lerp(start, 0f, t / MusicFadeSeconds);
            yield return null;
        }

        bgmSource.clip = clip;
        bgmSource.loop = true;
        bgmSource.Play();

        for (float t = 0f; t < MusicFadeSeconds; t += Time.unscaledDeltaTime)
        {
            bgmSource.volume = Mathf.Lerp(0f, bgmBaseVolume, t / MusicFadeSeconds);
            yield return null;
        }

        bgmSource.volume = bgmBaseVolume;
        musicFade = null;
    }

    /// <summary>
    /// Boss xuất hiện: còi báo động + đổi sang nhạc Boss. Scene load lại (Replay / về Home)
    /// sẽ tự trả về nhạc nền của Scene đó.
    /// </summary>
    public static void OnBossIncoming()
    {
        if (!HasInstance)
            return;

        AudioManager audio = Instance;
        audio.PlayCue(SfxCue.BossWarning);

        if (audio.bossMusic == null)
            audio.bossMusic = Resources.Load<AudioClip>(BossMusicResource);
        if (audio.bossMusic != null)
            audio.CrossfadeTo(audio.bossMusic);
    }

    // =====================================================================
    // HIỆU ỨNG ÂM THANH
    // =====================================================================

    public void PlaySFX(AudioClip clip, float volumeScale = 1f)
    {
        if (clip == null || sfxSource == null) return;
        sfxSource.PlayOneShot(clip, volumeScale);
    }

    /// <summary>Phát một tín hiệu gameplay. An toàn khi chưa có AudioManager.</summary>
    public static void Play(SfxCue cue)
    {
        if (HasInstance)
            Instance.PlayCue(cue);
    }

    public void PlayCue(SfxCue cue)
    {
        int index = (int)cue;
        float now = Time.unscaledTime;
        if (index < 0 || index >= nextCueTime.Length || now < nextCueTime[index])
            return;

        nextCueTime[index] = now + MinInterval(cue);
        AudioClip clip = ClipFor(cue);
        PlaySFX(clip != null ? clip : ProceduralSfx.Get(cue), CueVolume(cue));
    }

    private AudioClip ClipFor(SfxCue cue)
    {
        switch (cue)
        {
            case SfxCue.Hit: return hitSFX;
            case SfxCue.PlayerHurt: return playerHurtSFX;
            case SfxCue.PowerUp: return powerUpSFX;
            case SfxCue.Skill: return skillSFX;
            case SfxCue.BossWarning: return bossWarningSFX;
            case SfxCue.Victory: return victorySFX;
            case SfxCue.Defeat: return defeatSFX;
            case SfxCue.Reward: return rewardSFX;
            case SfxCue.Revive: return reviveSFX;
            default: return null;
        }
    }

    /// <summary>Tiếng trúng đạn rất dày -> giới hạn để không át các âm khác.</summary>
    private static float MinInterval(SfxCue cue) => cue == SfxCue.Hit ? 0.06f : 0.05f;

    private static float CueVolume(SfxCue cue) => cue == SfxCue.Hit ? 0.45f : 1f;
}
