using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Quản lý dữ liệu và tiến trình 50 Màn chơi (Level System) cho GalaxyStrike.
/// Cung cấp thuật toán phân bổ độ khó lũy tiến (Difficulty Curve) và Boss Stages.
/// </summary>
public class LevelManager : MonoBehaviour
{
    private static LevelManager _instance;

    /// <summary>Xoá tham chiếu tĩnh khi vào Play Mode (tránh giữ object của phiên trước).</summary>
    public static void ResetStaticInstance()
    {
        _instance = null;
    }

    /// <summary>
    /// Singleton "lazy": ưu tiên object có sẵn trong Scene; chỉ tạo mới khi Scene
    /// chưa có LevelManager. Nhờ vậy object lazy không "thắng" object được author
    /// sẵn với currentLevel/maxLevels đúng.
    /// </summary>
    public static LevelManager Instance
    {
        get
        {
            if (_instance != null)
                return _instance;

            _instance = FindObjectOfType<LevelManager>();
            if (_instance == null)
            {
                GameObject obj = new GameObject("LevelManager");
                _instance = obj.AddComponent<LevelManager>();
            }
            return _instance;
        }
    }

    /// <summary>
    /// Dữ liệu mô tả 1 màn chơi: số màn, tên màn, khu vực, thưởng và danh sách Wave.
    /// Được sinh ra bằng thuật toán trong GenerateLevelData thay vì gõ tay từng màn.
    /// </summary>
    [System.Serializable]
    public class LevelData
    {
        public int levelNumber;
        public string levelTitle;
        public string zoneName;
        public int rewardCoins = 50;
        public List<EnemySpawner.WaveConfig> waves = new List<EnemySpawner.WaveConfig>();

        /// <summary>
        /// Tổng số quái của màn = cộng dồn count của tất cả wave
        /// (mỗi wave tối thiểu tính là 1 con).
        /// </summary>
        public int GetTotalEnemies()
        {
            int sum = 0;
            if (waves != null)
                foreach (var w in waves) sum += Mathf.Max(1, w.count);
            return sum;
        }

        /// <summary>
        /// Tổng HP của màn = với từng wave: số quái × HP của prefab quái đó.
        /// Prefab không có component Enemy thì coi HP = 1. Kết quả 0 thì fallback về tổng số quái.
        /// </summary>
        public int GetTotalHealth()
        {
            int sum = 0;
            if (waves != null)
            {
                foreach (var w in waves)
                {
                    int enemyHp = 1;
                    if (w.enemyPrefab != null)
                    {
                        Enemy e = w.enemyPrefab.GetComponent<Enemy>();
                        if (e != null) enemyHp = Mathf.Max(1, e.Health);
                    }
                    sum += Mathf.Max(1, w.count) * enemyHp;
                }
            }
            return sum > 0 ? sum : GetTotalEnemies();
        }
    }

    [Header("Level Progress & Save")]
    [Tooltip("Màn chơi hiện tại (1 đến 50)")]
    [SerializeField] private int currentLevel = 1;
    [Tooltip("Tự động tải màn chơi đã lưu trong PlayerPrefs")]
    [SerializeField] private bool useSavedProgress = true;
    [SerializeField] private int maxLevels = 50;

    // Prefab quái KHÔNG còn SerializeField: nguồn sự thật duy nhất là
    // EnemyFactory.Registry (nạp từ Resources/Prefabs theo EnemyKind).

    private const string LEVEL_SAVE_KEY = "GalaxyStrike_CurrentLevel";

    public static int SavedLevel => PlayerPrefs.GetInt(LEVEL_SAVE_KEY, 1);

    /// <summary>
    /// Chế độ chơi được chọn ở HomeScene ("Campaign" hoặc "Challenge").
    /// Challenge dùng chung công thức sinh màn nhưng KHÔNG ghi vào tiến trình chiến dịch.
    /// </summary>
    public bool IsChallengeMode =>
        PlayerPrefs.GetString("SelectedGameMode", "Campaign") == "Challenge";

    /// <summary>
    /// Khởi tạo LevelManager: đảm bảo Singleton, gỡ khỏi parent và giữ qua các Scene,
    /// nạp registry quái từ Resources, rồi nạp màn hiện tại từ PlayerPrefs (nếu bật).
    /// </summary>
    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            // Tách object ra khỏi Parent (nếu có) để DontDestroyOnLoad hoạt động đúng
            transform.SetParent(null); 
            
            // Giữ lại LevelManager khi Load lại Scene (Khắc phục lỗi kẹt Level)
            DontDestroyOnLoad(gameObject);
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
            return;
        }

        EnsureEnemyRegistry();

        if (useSavedProgress)
        {
            currentLevel = SavedLevel;
        }
        currentLevel = Mathf.Clamp(currentLevel, 1, maxLevels);
    }

    /// <summary>
    /// Đăng ký sự kiện SceneManager.sceneLoaded để bắt lúc chuyển Scene.
    /// </summary>
    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    /// <summary>
    /// Hủy đăng ký sự kiện sceneLoaded khi object bị disable/hủy
    /// (tránh gọi vào object đã chết và tránh đăng ký trùng).
    /// </summary>
    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    /// <summary>
    /// Challenge: mỗi lượt vào GameScene là một màn ngẫu nhiên.
    /// Phải làm ở đây (không phải Awake) vì LevelManager sống qua các Scene.
    /// </summary>
    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!IsChallengeMode)
            return;

        // Bỏ qua LoadingScene: chỉ roll khi thực sự vào trận.
        if (scene.name != "GameScene")
            return;

        currentLevel = RollChallengeLevel();
        PlayerPrefs.SetInt(LEVEL_SAVE_KEY, currentLevel);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Đảm bảo registry của <see cref="EnemyFactory"/> đã nạp prefab từ Resources/Prefabs.
    /// Trước đây LevelManager tự giữ 9 field prefab; nay mọi chủng quái đều là
    /// <see cref="EnemyKind"/> và được tra cứu qua Factory.
    /// </summary>
    private static void EnsureEnemyRegistry()
    {
        EnemyFactory.EnsureRegistry();
    }

    /// <summary>Prefab của một chủng quái theo Factory (null nếu chưa có trong Resources).</summary>
    private static GameObject GetEnemyPrefab(EnemyKind kind)
    {
        return EnemyFactory.Registry.Get(kind);
    }

    /// <summary>
    /// Trả về dữ liệu của màn đang chơi (gọi GenerateLevelData cho currentLevel).
    /// </summary>
    public LevelData GetCurrentLevelData()
    {
        return GenerateLevelData(currentLevel);
    }

    /// <summary>
    /// Thuật toán tạo dữ liệu cho bất kỳ Level nào từ 1 đến 50 với đường cong độ khó lũy tiến.
    /// </summary>
    public LevelData GenerateLevelData(int levelNum)
    {
        EnsureEnemyRegistry();
        levelNum = Mathf.Clamp(levelNum, 1, maxLevels);

        LevelData data = new LevelData();
        data.levelNumber = levelNum;

        // Phân loại Khu Vực (Zones)
        int zoneIndex = (levelNum - 1) / 10 + 1; // Zone 1 -> 5
        switch (zoneIndex)
        {
            case 1:
                data.zoneName = "Zone 1: Earth Orbit";
                break;
            case 2:
                data.zoneName = "Zone 2: Asteroid Belt";
                break;
            case 3:
                data.zoneName = "Zone 3: Deep Space";
                break;
            case 4:
                data.zoneName = "Zone 4: Alien Void";
                break;
            case 5:
                data.zoneName = "Zone 5: Mothership Core";
                break;
        }

        bool isMajorBossLevel = (levelNum % 10 == 0);
        bool isMiniBossLevel = (levelNum % 5 == 0 && !isMajorBossLevel);

        if (isMajorBossLevel)
        {
            data.levelTitle = $"LEVEL {levelNum}: BOSS SHOWDOWN";
            data.rewardCoins = 150 + levelNum * 10;
        }
        else if (isMiniBossLevel)
        {
            data.levelTitle = $"LEVEL {levelNum}: ELITE COMMANDER";
            data.rewardCoins = 80 + levelNum * 5;
        }
        else
        {
            data.levelTitle = $"LEVEL {levelNum}";
            data.rewardCoins = 30 + levelNum * 2;
        }

        // Tạo danh sách các Wave dựa theo tiến độ của Level
        data.waves = BuildWavesForLevel(levelNum, zoneIndex);

        return data;
    }

    /// <summary>
    /// Dá»±ng danh sÃ¡ch Wave theo 3 nhÃ³m cá»§a Galaxy Shooter.
    ///
    /// Nhá»‹p Ä‘á»™: Warm-up (Top Cascade + Snake) â†’ Giá»¯a vÃ¡n (Side-Flank, Pincer Cross, Drop &amp; Anchor)
    ///          â†’ Cao trÃ o (Bottom Ambush, Kamikaze, Warp-in) â†’ Boss.
    /// </summary>
    private List<EnemySpawner.WaveConfig> BuildWavesForLevel(int lvl, int zone)
    {
        var waveList = new List<EnemySpawner.WaveConfig>();
        float spawnDelay = Mathf.Max(0.30f - lvl * 0.0015f, 0.16f);
        float speed = Mathf.Min(1.15f + lvl * 0.012f, 1.65f);
        float amplitude = 1.2f + zone * 0.18f;
        float frequency = 1.3f + lvl * 0.02f;

        float levelProgress = (lvl - 1f) / Mathf.Max(1f, maxLevels - 1f);
        int totalWaves = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(10f, 15f, levelProgress)), 10, 15);
        float powerUpDropChance = Mathf.Lerp(0.09f, 0.04f, levelProgress);
        int combatWaves = totalWaves - 1;

        for (int w = 1; w <= combatWaves; w++)
        {
            float phase = w / (float)combatWaves;
            bool isWarmUp = phase <= 0.25f;
            bool isMid = phase > 0.25f && phase <= 0.65f;
            EnemySpawner.WaveConfig wave;

            if (isWarmUp)
            {
                bool useSnake = w == 1 || w % 2 == 0;
                wave = MakeWave(
                    useSnake ? $"Wave {w} - Snake" : $"Wave {w} - Cascade",
                    SpawnOrigin.TopCascade,
                    useSnake ? FormationType.Snake : FormationType.Wall,
                    useSnake ? 8 : 5,
                    spawnDelay, speed, amplitude, frequency,
                    GetEnemyByTier(zone, 1),
                    EnemyFirePattern.Single,
                    useSnake ? 0.6f : 0.35f,
                    26f);
            }
            else if (isMid)
            {
                switch (w % 4)
                {
                    case 0:
                        wave = MakeWave($"Wave {w} - Side Flank", SpawnOrigin.SideFlank, FormationType.Wall,
                            5, spawnDelay, speed, 0f, 1f, GetEnemyByTier(zone, 2),
                            EnemyFirePattern.Aimed, 0.25f, 24f);
                        break;
                    case 1:
                        wave = MakeWave($"Wave {w} - Pincer Cross", SpawnOrigin.SideFlank, FormationType.PincerCross,
                            8, spawnDelay, speed, amplitude, frequency, GetEnemyByTier(zone, 2),
                            EnemyFirePattern.Spread, 0.28f, 26f);
                        break;
                    case 2:
                        wave = MakeWave($"Wave {w} - Drop & Anchor", SpawnOrigin.TopCascade, FormationType.DropAnchor,
                            2, 0.45f, 0.95f, 0f, 1f,
                            GetEnemyPrefab(EnemyKind.Enemy_Cruiser),
                            EnemyFirePattern.RadialBloom, 0.9f, 26f);
                        break;
                    default:
                        wave = MakeWave($"Wave {w} - V-Formation", SpawnOrigin.TopCascade, FormationType.VFormation,
                            9, spawnDelay, speed, amplitude, frequency, GetEnemyByTier(zone, 2),
                            EnemyFirePattern.Spread, 0.3f, 26f);
                        break;
                }
            }
            else
            {
                switch (w % 3)
                {
                    case 0:
                        wave = MakeWave($"Wave {w} - Bottom Ambush", SpawnOrigin.BottomAmbush, FormationType.Wall,
                            5, spawnDelay, speed * 0.95f, 0f, 1f, GetEnemyByTier(zone, 2),
                            EnemyFirePattern.Aimed, 0.28f, 22f);
                        break;
                    case 1:
                        wave = MakeWave($"Wave {w} - Kamikaze", SpawnOrigin.TopCascade, FormationType.Kamikaze,
                            6, spawnDelay * 1.4f, speed, 1.2f, 2.0f, GetEnemyByTier(zone, 1),
                            EnemyFirePattern.Single, 0.2f, 24f);
                        break;
                    default:
                        wave = MakeWave($"Wave {w} - Warp In", SpawnOrigin.WarpIn, FormationType.Wall,
                            5, spawnDelay * 1.6f, speed * 0.75f, 0f, 1f, GetEnemyByTier(zone, 3),
                            EnemyFirePattern.Spiral, 0.35f, 25f);
                        break;
                }
            }

            if (w == combatWaves)
            {
                wave.formation = FormationType.PincerCross;
                wave.spawnOrigin = SpawnOrigin.SideFlank;
                wave.waveName = $"Wave {w} - Final Cross";
                wave.count = 10;
                wave.firePattern = EnemyFirePattern.Spread;
                wave.timeLimit = 0f;
            }

            wave.dropChance = powerUpDropChance;
            waveList.Add(wave);
        }

        GameObject planetBoss = GetPlanetBossPrefab(zone, lvl);
        string bossWaveName = lvl >= maxLevels
            ? $"Wave {totalWaves} - FINAL BOSS"
            : $"Wave {totalWaves} - Planet Boss";
        waveList.Add(MakeWave(
            bossWaveName,
            SpawnOrigin.TopCascade,
            FormationType.Boss,
            1,
            0.3f,
            1.0f,
            1.4f,
            0.8f,
            planetBoss != null ? planetBoss : (GetEnemyPrefab(EnemyKind.Enemy_Boss) ?? GetEnemyPrefab(EnemyKind.Enemy_MiniBoss)),
            EnemyFirePattern.RadialBloom,
            powerUpDropChance,
            0f));

        ApplyArsenalVariety(waveList, lvl, zone);
        return waveList;
    }

    /// <summary>
    /// Trả về prefab Boss "hành tinh" riêng của từng Zone (Enemy_Boss_Zone01..05).
    /// Ở màn cuối của Zone 5 dùng Boss cuối (Enemy_Boss_Final).
    /// Không tìm thấy thì trả về Boss mặc định qua Factory.
    /// </summary>
    private GameObject GetPlanetBossPrefab(int zone, int level)
    {
        string prefabName;
        switch (zone)
        {
            case 1: prefabName = "Enemy_Boss_Zone01"; break;
            case 2: prefabName = "Enemy_Boss_Zone02"; break;
            case 3: prefabName = "Enemy_Boss_Zone03"; break;
            case 4: prefabName = "Enemy_Boss_Zone04"; break;
            case 5: prefabName = "Enemy_Boss_Zone05"; break;
            default: return GetEnemyPrefab(EnemyKind.Enemy_Boss);
        }

        if (zone == 5 && level >= maxLevels)
            return Resources.Load<GameObject>("Prefabs/Enemy_Boss_Final");

        // Boss theo Zone nằm ngoài enum EnemyKind (biến thể riêng của từng chiến khu),
        // nên vẫn nạp trực tiếp từ Resources; lỗi chỉ là thiếu prefab chứ không ảnh hưởng Factory.
        return Resources.Load<GameObject>("Prefabs/" + prefabName);
    }

    /// <summary>
    /// Hàm tiện ích tạo một WaveConfig từ tham số truyền vào,
    /// giúp BuildWavesForLevel viết ngắn gọn và không lặp code khởi tạo.
    /// </summary>
    private EnemySpawner.WaveConfig MakeWave(
        string name,
        SpawnOrigin origin,
        FormationType formation,
        int count,
        float spawnDelay,
        float speed,
        float amplitude,
        float frequency,
        GameObject prefab,
        EnemyFirePattern fire,
        float dropChance,
        float timeLimit,
        SpecialSpawnType special = SpecialSpawnType.None)
    {
        return new EnemySpawner.WaveConfig
        {
            waveName = name,
            spawnOrigin = origin,
            formation = formation,
            specialSpawn = special,
            count = count,
            spawnDelay = spawnDelay,
            moveSpeed = speed,
            movementAmplitude = amplitude,
            movementFrequency = frequency,
            enemyPrefab = prefab,
            firePattern = fire,
            dropChance = dropChance,
            timeLimit = timeLimit
        };
    }

    // Deterministic variety preserves the authored wave counts and formations.

    /// <summary>
    /// Trộn thêm loại quái đặc biệt (Swarmer, Wasp, TwinCannon, Shield, MissileShip)
    /// vào một số wave để đa dạng hoá kẻ địch. Việc chọn dựa trên (chỉ số wave + level)
    /// nên MANG TÍNH XÁC ĐỊNH: chơi lại cùng màn sẽ gặp đúng thứ tự quái như trước.
    /// Không đụng vào wave Boss.
    /// </summary>
    private void ApplyArsenalVariety(List<EnemySpawner.WaveConfig> waves, int level, int zone)
    {
        var candidates = new List<EnemyKind>();
        if (level >= 2) candidates.Add(EnemyKind.Enemy_Swarmer);
        if (level >= 4) candidates.Add(EnemyKind.Enemy_Wasp);
        if (zone >= 2) candidates.Add(EnemyKind.Enemy_TwinCannon);
        if (level >= 15) candidates.Add(EnemyKind.Enemy_ShieldUnit);
        if (zone >= 3) candidates.Add(EnemyKind.Enemy_MissileShip);
        for (int i = 0; i < waves.Count; i++)
        {
            var wave = waves[i];
            if (wave.formation == FormationType.Boss)
                continue;
            if (candidates.Count == 0 || (i + level) % 3 != 0) continue;
            var prefab = GetEnemyPrefab(candidates[(level + i / 3) % candidates.Count]);
            if (prefab != null) wave.enemyPrefab = prefab;
        }
    }

    /// <summary>
    /// Chọn prefab quái theo Zone và "tier" (mức mạnh 1..3):
    /// Zone càng cao thì cùng một tier sẽ ra quái mạnh hơn (scout → fighter → striker...).
    /// </summary>
    private GameObject GetEnemyByTier(int zone, int tier)
    {
        EnemyKind kind;
        switch (zone)
        {
            case 1: // Earth Orbit
                kind = tier == 1 ? EnemyKind.Enemy_Scout
                     : tier == 2 ? EnemyKind.Enemy_Fighter
                                 : EnemyKind.Enemy_Striker;
                break;

            case 2: // Asteroid Belt
                kind = tier == 1 ? EnemyKind.Enemy_Fighter
                     : tier == 2 ? EnemyKind.Enemy_Striker
                                 : EnemyKind.Enemy_Gunship;
                break;

            case 3: // Deep Space
                kind = tier == 1 ? EnemyKind.Enemy_Striker
                     : tier == 2 ? EnemyKind.Enemy_Gunship
                                 : EnemyKind.Enemy_Cruiser;
                break;

            case 4: // Alien Void
                kind = tier == 1 ? EnemyKind.Enemy_Gunship
                     : tier == 2 ? EnemyKind.Enemy_Cruiser
                                 : EnemyKind.Enemy_Destroyer;
                break;

            case 5: // Mothership Core
            default:
                kind = tier == 1 ? EnemyKind.Enemy_Cruiser
                     : tier == 2 ? EnemyKind.Enemy_Destroyer
                                 : EnemyKind.Enemy_Elite;
                break;
        }

        return GetEnemyPrefab(kind);
    }


    /// <summary>Lên màn kế tiếp. Đã ở màn cuối thì giữ nguyên màn cuối.</summary>
    public void AdvanceToNextLevel()
    {
        // Challenge: mỗi lượt là một màn ngẫu nhiên, không đụng tiến trình chiến dịch.
        if (IsChallengeMode)
        {
            currentLevel = RollChallengeLevel();
            PlayerPrefs.SetInt(LEVEL_SAVE_KEY, currentLevel);
            PlayerPrefs.Save();
            return;
        }

        if (IsCampaignComplete())
            return;

        currentLevel = Mathf.Min(currentLevel + 1, maxLevels);
        SaveCurrentLevel();
    }

    /// <summary>Màn ngẫu nhiên 1..50 cho một lượt Challenge.</summary>
    private int RollChallengeLevel()
    {
        int rolled = Random.Range(1, maxLevels + 1);
        // Tránh lặp lại đúng màn vừa chơi khi có thể.
        if (rolled == currentLevel && maxLevels > 1)
            rolled = rolled % maxLevels + 1;
        return rolled;
    }

    /// <summary>Đã chinh phục màn cuối của chiến dịch (50) hay chưa.</summary>
    public bool IsCampaignComplete()
    {
        return currentLevel >= maxLevels;
    }

    /// <summary>
    /// Gán màn chơi trực tiếp (ví dụ chọn màn từ bản đồ) rồi lưu lại tiến trình.
    /// </summary>
    public void SetLevel(int level)
    {
        currentLevel = Mathf.Clamp(level, 1, maxLevels);
        SaveCurrentLevel();
    }

    /// <summary>
    /// Ghi currentLevel xuống PlayerPrefs, flush, và báo HUD cập nhật số màn đang chơi.
    /// </summary>
    private void SaveCurrentLevel()
    {
        PlayerPrefs.SetInt(LEVEL_SAVE_KEY, currentLevel);
        PlayerPrefs.Save();
        if (HUD_Panel.Instance != null)
        {
            HUD_Panel.Instance.UpdateLevelDisplay();
        }
    }

    public int CurrentLevel => currentLevel;
    public int MaxLevels => maxLevels;
}
