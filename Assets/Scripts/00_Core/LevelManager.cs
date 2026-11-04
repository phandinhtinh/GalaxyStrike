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
    public static LevelManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<LevelManager>();
                if (_instance == null)
                {
                    GameObject obj = new GameObject("LevelManager");
                    _instance = obj.AddComponent<LevelManager>();
                }
            }
            return _instance;
        }
    }

    [System.Serializable]
    public class LevelData
    {
        public int levelNumber;
        public string levelTitle;
        public string zoneName;
        public int rewardCoins = 50;
        public List<EnemySpawner.WaveConfig> waves = new List<EnemySpawner.WaveConfig>();

        public int GetTotalEnemies()
        {
            int sum = 0;
            if (waves != null)
                foreach (var w in waves) sum += Mathf.Max(1, w.count);
            return sum;
        }

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

    [Header("Enemy Prefabs (Tự động tải từ Resources nếu để trống)")]
    [SerializeField] private GameObject scoutPrefab;
    [SerializeField] private GameObject fighterPrefab;
    [SerializeField] private GameObject strikerPrefab;
    [SerializeField] private GameObject gunshipPrefab;
    [SerializeField] private GameObject cruiserPrefab;
    [SerializeField] private GameObject destroyerPrefab;
    [SerializeField] private GameObject elitePrefab;
    [SerializeField] private GameObject miniBossPrefab;
    [SerializeField] private GameObject bossPrefab;

    private const string LEVEL_SAVE_KEY = "GalaxyStrike_CurrentLevel";

    /// <summary>
    /// Chế độ chơi được chọn ở HomeScene ("Campaign" hoặc "Challenge").
    /// Challenge dùng chung công thức sinh màn nhưng KHÔNG ghi vào tiến trình chiến dịch.
    /// </summary>
    public bool IsChallengeMode =>
        PlayerPrefs.GetString("SelectedGameMode", "Campaign") == "Challenge";

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

        LoadPrefabsIfNull();

        if (useSavedProgress)
        {
            currentLevel = PlayerPrefs.GetInt(LEVEL_SAVE_KEY, 1);
        }
        currentLevel = Mathf.Clamp(currentLevel, 1, maxLevels);
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

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

    private void LoadPrefabsIfNull()
    {
        if (scoutPrefab == null) scoutPrefab = Resources.Load<GameObject>("Prefabs/Enemy_Scout");
        if (fighterPrefab == null) fighterPrefab = Resources.Load<GameObject>("Prefabs/Enemy_Fighter");
        if (strikerPrefab == null) strikerPrefab = Resources.Load<GameObject>("Prefabs/Enemy_Striker");
        if (gunshipPrefab == null) gunshipPrefab = Resources.Load<GameObject>("Prefabs/Enemy_Gunship");
        if (cruiserPrefab == null) cruiserPrefab = Resources.Load<GameObject>("Prefabs/Enemy_Cruiser");
        if (destroyerPrefab == null) destroyerPrefab = Resources.Load<GameObject>("Prefabs/Enemy_Destroyer");
        if (elitePrefab == null) elitePrefab = Resources.Load<GameObject>("Prefabs/Enemy_Elite");
        if (miniBossPrefab == null) miniBossPrefab = Resources.Load<GameObject>("Prefabs/Enemy_MiniBoss");
        if (bossPrefab == null) bossPrefab = Resources.Load<GameObject>("Prefabs/Enemy_Boss");
    }

    public LevelData GetCurrentLevelData()
    {
        return GenerateLevelData(currentLevel);
    }

    /// <summary>
    /// Thuật toán tạo dữ liệu cho bất kỳ Level nào từ 1 đến 50 với đường cong độ khó lũy tiến.
    /// </summary>
    public LevelData GenerateLevelData(int levelNum)
    {
        LoadPrefabsIfNull();
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
                            cruiserPrefab != null ? cruiserPrefab : GetEnemyByTier(zone, 3),
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
            planetBoss != null ? planetBoss : (bossPrefab != null ? bossPrefab : miniBossPrefab),
            EnemyFirePattern.RadialBloom,
            powerUpDropChance,
            0f));

        ApplyArsenalVariety(waveList, lvl, zone);
        return waveList;
    }

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
            default: return bossPrefab;
        }

        if (zone == 5 && level >= maxLevels)
            return Resources.Load<GameObject>("Prefabs/Enemy_Boss_Final");

        return Resources.Load<GameObject>("Prefabs/" + prefabName);
    }
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
    private void ApplyArsenalVariety(List<EnemySpawner.WaveConfig> waves, int level, int zone)
    {
        var candidates = new List<string>();
        if (level >= 2) candidates.Add("Enemy_Swarmer");
        if (level >= 4) candidates.Add("Enemy_Wasp");
        if (zone >= 2) candidates.Add("Enemy_TwinCannon");
        if (level >= 15) candidates.Add("Enemy_ShieldUnit");
        if (zone >= 3) candidates.Add("Enemy_MissileShip");
        for (int i = 0; i < waves.Count; i++)
        {
            var wave = waves[i];
            if (wave.formation == FormationType.Boss)
                continue;
            if (candidates.Count == 0 || (i + level) % 3 != 0) continue;
            var prefab = Resources.Load<GameObject>("Prefabs/" + candidates[(level + i / 3) % candidates.Count]);
            if (prefab != null) wave.enemyPrefab = prefab;
        }
    }

    private GameObject GetEnemyByTier(int zone, int tier)
    {
        switch (zone)
        {
            case 1: // Earth Orbit
                if (tier == 1) return scoutPrefab;
                if (tier == 2) return fighterPrefab;
                return strikerPrefab;

            case 2: // Asteroid Belt
                if (tier == 1) return fighterPrefab;
                if (tier == 2) return strikerPrefab;
                return gunshipPrefab;

            case 3: // Deep Space
                if (tier == 1) return strikerPrefab;
                if (tier == 2) return gunshipPrefab;
                return cruiserPrefab;

            case 4: // Alien Void
                if (tier == 1) return gunshipPrefab;
                if (tier == 2) return cruiserPrefab;
                return destroyerPrefab;

            case 5: // Mothership Core
            default:
                if (tier == 1) return cruiserPrefab;
                if (tier == 2) return destroyerPrefab;
                return elitePrefab;
        }
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

    public void SetLevel(int level)
    {
        currentLevel = Mathf.Clamp(level, 1, maxLevels);
        SaveCurrentLevel();
    }

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
