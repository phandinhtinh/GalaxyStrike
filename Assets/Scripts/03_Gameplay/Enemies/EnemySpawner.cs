using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Điều khiển nhịp ra quái của một màn chơi.
///
/// Hệ thống mới chỉ dùng 3 nhóm theo thiết kế Galaxy Shooter:
///  I.  <see cref="SpawnOrigin"/>   — xuất hiện từ đỉnh / sườn / đáy / warp.
///  II. <see cref="FormationType"/> — đội hình: Snake, Wall, V, Pincer Cross, Drop &amp; Anchor, Kamikaze, Boss.
///  III.<see cref="SpecialSpawnType"/> — Carrier/Hive, Splitting, Boss Summon, Falling Hazard.
/// </summary>
public class EnemySpawner : MonoBehaviour
{
    [System.Serializable]
    public class WaveConfig
    {
        public string waveName = "Wave 1";

        [Header("Nhóm I — Xuất hiện")]
        public SpawnOrigin spawnOrigin = SpawnOrigin.TopCascade;

        [Header("Nhóm II — Đội hình")]
        public FormationType formation = FormationType.Wall;

        [Header("Nhóm III — Cơ chế đặc biệt")]
        public SpecialSpawnType specialSpawn = SpecialSpawnType.None;

        [Header("Số lượng")]
        [Min(1)] public int count = 8;

        [Header("Tốc độ & nhịp")]
        [Tooltip("Thời gian giữa hai lần thả quái (giây)")]
        [Min(0.02f)] public float spawnDelay = 0.12f;
        [Tooltip("Tốc độ bay xuống")]
        [Min(0.2f)] public float moveSpeed = 2.2f;
        [Tooltip("Biên độ dao động ngang của quỹ đạo")]
        [Min(0f)] public float movementAmplitude = 1.4f;
        [Tooltip("Tần số dao động")]
        [Min(0.1f)] public float movementFrequency = 1.4f;

        [Header("Nội dung")]
        public GameObject enemyPrefab;
        public EnemyFirePattern firePattern = EnemyFirePattern.Single;

        [Tooltip("Giới hạn thời gian Wave (giây). 0 = chờ dọn sạch.")]
        [Min(0f)] public float timeLimit = 0f;

        [Tooltip("Tỉ lệ rơi item khi quái chết")]
        [Range(0f, 1f)] public float dropChance = 0.2f;
    }

    [Header("Enemy Default Prefab")]
    [SerializeField] private GameObject enemyPrefab;

    [Header("Spawner Settings")]
    [SerializeField] private float delayBetweenWaves = 0.8f;
    [SerializeField] private bool autoSyncTotalEnemies = true;
    [SerializeField, Range(0f, 1f)] private float regularEnemyFireChance = 0.45f;
    [Tooltip("Tàu có máu từ ngưỡng này trở lên giữ lane trên và mở thêm chuỗi đạn.")]
    [SerializeField, Min(1)] private int highThreatHealthThreshold = 5;

    [Header("Spawn Area")]
    [SerializeField] private Vector3 spawnCenter = new Vector3(0, 2.0f, 0);
    [Tooltip("Quái xuất hiện phía trên màn hình để bay vào tự nhiên")]
    [SerializeField] private float spawnYOffset = 4f;
    [Tooltip("Nửa bề rộng vùng thả quái")]
    [SerializeField] private float spawnHalfWidth = 2.4f;

    [Header("Bottom Ambush")]
    [Tooltip("Thời gian cảnh báo đỏ trước khi quái lao lên từ đáy")]
    [SerializeField, Min(0.1f)] private float ambushWarningTime = 1.4f;


    private List<WaveConfig> waves = new List<WaveConfig>();
    private int currentWaveIndex;
    private int spawnedEnemiesInWave;

    public int WaveCount => waves.Count;
    public int CurrentWaveNumber => waves.Count == 0 ? 0 : Mathf.Min(currentWaveIndex + 1, waves.Count);
    public bool IsStageComplete => isVictoryTriggered;
    public float StageWaveProgress
    {
        get
        {
            if (waves.Count == 0) return 0f;
            if (isVictoryTriggered || currentWaveIndex >= waves.Count) return 1f;
            int remaining = Mathf.Max(0, activeEnemiesInWave - hiveMinionsAlive);
            float cleared = Mathf.Clamp01((spawnedEnemiesInWave - remaining) /
                (float)Mathf.Max(1, waves[currentWaveIndex].count));
            // Reserve the endpoint for the spawner's confirmed clear, including overlapping/minion waves.
            return Mathf.Min((currentWaveIndex + cleared) / waves.Count, 0.995f);
        }
    }
    private int activeEnemiesInWave;
    private readonly List<Enemy> aliveEnemies = new List<Enemy>(32);
    private bool isSpawningWave;
    private int totalEnemiesToKill;
    private int currentEnemiesKilled;
    private bool isVictoryTriggered;

    private Enemy vLeader;
    private bool vLeaderBroken;

    private void OnEnable()
    {
        Enemy.OnEnemyDied += HandleEnemyDied;
        Enemy.HiveSpawned += HandleHiveSpawn;
    }

    private void OnDisable()
    {
        Enemy.OnEnemyDied -= HandleEnemyDied;
        Enemy.HiveSpawned -= HandleHiveSpawn;
    }

    private void Start()
    {
        isVictoryTriggered = false;
        currentEnemiesKilled = 0;
        aliveEnemies.Clear();

        if (LevelManager.Instance != null)
        {
            LevelManager.LevelData levelData = LevelManager.Instance.GetCurrentLevelData();
            if (levelData?.waves != null && levelData.waves.Count > 0)
                waves = levelData.waves;
        }

        totalEnemiesToKill = CalculateTotalEnemies();

        if (autoSyncTotalEnemies && GameManager.Instance != null)
        {
            LevelManager.LevelData levelData = LevelManager.Instance?.GetCurrentLevelData();
            int totalHp = levelData != null ? levelData.GetTotalHealth() : totalEnemiesToKill;
            GameManager.Instance.InitializeLevel(totalHp, totalEnemiesToKill);
        }

        StartCoroutine(WaveLoopRoutine());
    }

    private int CalculateTotalEnemies()
    {
        int sum = 0;
        for (int i = 0; i < waves.Count; i++)
            sum += Mathf.Max(1, waves[i].count);
        return sum;
    }

    // =====================================================================
    // VÒNG LẶP WAVE
    // =====================================================================
    private IEnumerator WaveLoopRoutine()
    {
        currentWaveIndex = 0;

        while (!isVictoryTriggered)
        {
            if (GameManager.Instance != null && GameManager.Instance.IsLevelCompleted())
                yield break;
            if (currentWaveIndex >= waves.Count)
                yield break;

            WaveConfig wave = waves[currentWaveIndex];

            yield return StartCoroutine(SpawnWaveRoutine(wave));

            // Clear-screen sớm thì sang Wave mới ngay; hết timeLimit thì Wave sau ập vào.
            if (wave.timeLimit > 0f)
            {
                float elapsedLimit = 0f;
                while (!isVictoryTriggered && !IsWaveCleared())
                {
                    elapsedLimit += Time.deltaTime;
                    if (elapsedLimit >= wave.timeLimit)
                        break;
                    yield return null;
                }
            }
            else
            {
                yield return new WaitUntil(() => IsWaveCleared() || isVictoryTriggered);
            }

            if (isVictoryTriggered)
                yield break;

            ClearRemainingEnemies();
            currentWaveIndex++;
            spawnedEnemiesInWave = 0;
            yield return new WaitForSeconds(delayBetweenWaves);
        }
    }

    private bool IsWaveCleared() => !isSpawningWave && activeEnemiesInWave <= 0;

    // =====================================================================
    // SINH WAVE
    // =====================================================================
    private IEnumerator SpawnWaveRoutine(WaveConfig wave)
    {
        spawnedEnemiesInWave = 0;
        isSpawningWave = true;
        activeEnemiesInWave = 0;
        vLeader = null;
        vLeaderBroken = false;

        GameObject prefab = wave.enemyPrefab != null ? wave.enemyPrefab : enemyPrefab;
        int count = Mathf.Max(1, wave.count);

        // Cảnh báo trước cho đòn phục kích từ đáy.
        bool isHighThreat = IsHighThreatPrefab(prefab, wave.formation == FormationType.Boss);
        if (wave.spawnOrigin == SpawnOrigin.BottomAmbush && !isHighThreat)
        {
            float columnX = spawnCenter.x + Random.Range(-spawnHalfWidth * 0.7f, spawnHalfWidth * 0.7f);
            AmbushWarning.Create(
                new Vector3(columnX, GetBottomSpawnY(), 0f),
                ambushWarningTime,
                spawnHalfWidth * 0.5f);

            yield return new WaitForSeconds(ambushWarningTime);
        }

        // Pincer Cross: hai nửa Wave xuất phát cùng lúc từ 2 góc.
        if (wave.formation == FormationType.PincerCross)
        {
            yield return StartCoroutine(SpawnPincerCrossRoutine(wave, prefab, count));
            isSpawningWave = false;
            yield break;
        }

        for (int i = 0; i < count; i++)
        {
            if (isVictoryTriggered || (GameManager.Instance != null && GameManager.Instance.IsLevelCompleted()))
            {
                isSpawningWave = false;
                yield break;
            }

            SpawnSingle(wave, prefab, i, count, 0f);

            yield return new WaitForSeconds(wave.spawnDelay * Random.Range(0.8f, 1.2f));
        }

        isSpawningWave = false;
    }

    private IEnumerator SpawnPincerCrossRoutine(WaveConfig wave, GameObject prefab, int count)
    {
        int perSide = Mathf.Max(1, count / 2);

        for (int i = 0; i < perSide; i++)
        {
            if (isVictoryTriggered)
                yield break;

            // Trái -> bay chéo sang phải (+1). Phải -> bay chéo sang trái (-1).
            SpawnSingle(wave, prefab, i, perSide, -1f);
            SpawnSingle(wave, prefab, i, perSide, +1f);

            yield return new WaitForSeconds(wave.spawnDelay * Random.Range(0.8f, 1.2f));
        }
    }

    /// <summary>Sinh một con quái theo cấu hình Wave và vị trí chỉ định.</summary>
    private void SpawnSingle(WaveConfig wave, GameObject prefab, int index, int total, float pincerSide)
    {
        if (prefab == null)
            return;

        bool isBoss = wave.formation == FormationType.Boss;
        bool isHighThreat = IsHighThreatPrefab(prefab, isBoss);
        bool moveSpawnToTop = isHighThreat && wave.spawnOrigin == SpawnOrigin.BottomAmbush;
        Vector3 spawnPos = ResolveSpawnPosition(wave, index, total, pincerSide, moveSpawnToTop);
        Vector3 anchor = ResolveAnchor(wave, index, total, spawnPos);

        // PATTERN: Factory — mọi chỗ tạo quái đi qua 1 cửa duy nhất.
        Enemy enemy = EnemyFactory.Create(prefab, spawnPos);
        if (enemy == null)
            return;

        bool isLeader = wave.formation == FormationType.VFormation && index == 0;

        enemy.Initialize(
            moveSpawnToTop ? SpawnOrigin.TopCascade : wave.spawnOrigin,
            wave.formation,
            wave.specialSpawn,
            spawnPos,
            anchor,
            wave.moveSpeed,
            wave.movementAmplitude,
            wave.movementFrequency,
            index,
            isLeader,
            ResolvePlayerTarget());

        // Chữ V: xác định cánh ngay lúc spawn (trái/phải so với tâm).
        if (wave.formation == FormationType.VFormation)
        {
            enemy.SetVWingSide(anchor.x - spawnCenter.x);
        }

        // Hướng bay ngang cho sườn / pincer.
        enemy.SetFlankDirection(pincerSide != 0f ? pincerSide : (anchor.x < spawnCenter.x ? 1f : -1f));

        if (wave.formation == FormationType.Kamikaze)
            enemy.SetKamikazeOrbitTime(1.2f + index * 0.12f);

        // Préfab quái con cho cơ chế đặc biệt.
        if (wave.specialSpawn == SpecialSpawnType.CarrierHive ||
            wave.specialSpawn == SpecialSpawnType.Splitting)
        {
            enemy.SetMinionPrefab(enemyPrefab, 3);
        }

        enemy.SetStaysInUpperLane(isHighThreat);
        enemy.ConfigureFormationShooting(isHighThreat, regularEnemyFireChance);
        enemy.SetFirePattern(wave.firePattern);
        enemy.SetDropChance(wave.dropChance);

        if (isLeader)
            vLeader = enemy;

        activeEnemiesInWave++;
        spawnedEnemiesInWave++;
        aliveEnemies.Add(enemy);
    }

    // =====================================================================
    // VỊ TRÍ XUẤT HIỆN & ĐIỂM NEO
    // =====================================================================
    private bool IsHighThreatPrefab(GameObject prefab, bool isBoss)
    {
        if (isBoss)
            return true;

        Enemy enemyStats = prefab != null ? prefab.GetComponent<Enemy>() : null;
        return enemyStats != null && enemyStats.BaseHealth >= highThreatHealthThreshold;
    }

    private Vector3 ResolveSpawnPosition(WaveConfig wave, int index, int total, float pincerSide, bool forceTop = false)
    {
        float camHalfWidth = GetCameraHalfWidth();
        float halfWidth = Mathf.Min(spawnHalfWidth, camHalfWidth - 0.5f);

        if (forceTop)
        {
            float t = total > 1 ? index / (float)(total - 1) : 0.5f;
            float x = spawnCenter.x + Mathf.Lerp(-halfWidth, halfWidth, t);
            return new Vector3(x, spawnCenter.y + spawnYOffset, 0f);
        }

        switch (wave.spawnOrigin)
        {
            case SpawnOrigin.SideFlank:
                {
                    bool fromLeft = pincerSide <= 0f;
                    float x = spawnCenter.x + (fromLeft ? -(camHalfWidth + 0.7f) : (camHalfWidth + 0.7f));
                    float t = total > 1 ? index / (float)(total - 1) : 0f;
                    float y = spawnCenter.y + Mathf.Lerp(2.8f, 0.2f, t);
                    return new Vector3(x, y, 0f);
                }

            case SpawnOrigin.BottomAmbush:
                {
                    float t = total > 1 ? index / (float)(total - 1) : 0.5f;
                    float x = spawnCenter.x + Mathf.Lerp(-halfWidth * 0.7f, halfWidth * 0.7f, t);
                    return new Vector3(x, GetBottomSpawnY(), 0f);
                }

            case SpawnOrigin.WarpIn:
                {
                    float t = total > 1 ? index / (float)(total - 1) : 0.5f;
                    float x = spawnCenter.x + Mathf.Lerp(-halfWidth * 0.7f, halfWidth * 0.7f, t);
                    float y = spawnCenter.y + Mathf.Lerp(2.6f, -0.6f, t);
                    return new Vector3(x, y, 0f);
                }

            case SpawnOrigin.TopCascade:
            default:
                {
                    float t = total > 1 ? index / (float)(total - 1) : 0.5f;
                    float x = spawnCenter.x + Mathf.Lerp(-halfWidth, halfWidth, t);
                    float y = spawnCenter.y + spawnYOffset;
                    return new Vector3(x, y, 0f);
                }
        }
    }

    /// <summary>Điểm neo (dùng cho đội hình và trụ pháo).</summary>
    private Vector3 ResolveAnchor(WaveConfig wave, int index, int total, Vector3 spawnPos)
    {
        float camHalfWidth = GetCameraHalfWidth();
        float halfWidth = Mathf.Min(spawnHalfWidth, camHalfWidth - 0.5f);

        switch (wave.formation)
        {
            case FormationType.Snake:
                // Neo đầu rắn. Đuôi (index lớn) nằm cao hơn -> bay vào sau, đúng hình rắn bò.
                return new Vector3(spawnCenter.x, spawnCenter.y + 2.2f, 0f);

            case FormationType.Wall:
                {
                    float t = total > 1 ? index / (float)(total - 1) : 0.5f;
                    float x = spawnCenter.x + Mathf.Lerp(-halfWidth, halfWidth, t);
                    return new Vector3(x, spawnCenter.y + 2.4f, 0f);
                }

            case FormationType.VFormation:
                {
                    int wing = index / 2;
                    int side = index % 2 == 1 ? 1 : -1;
                    float x = spawnCenter.x + side * wing * 0.95f;
                    float y = spawnCenter.y + 2.4f - wing * 0.75f;
                    return new Vector3(x, y, 0f);
                }

            case FormationType.DropAnchor:
                {
                    float t = total > 1 ? index / (float)(total - 1) : 0.5f;
                    float x = spawnCenter.x + Mathf.Lerp(-halfWidth * 0.6f, halfWidth * 0.6f, t);
                    return new Vector3(x, spawnCenter.y + 1f, 0f);
                }

            case FormationType.Kamikaze:
                {
                    float t = total > 1 ? index / (float)(total - 1) : 0.5f;
                    float x = spawnCenter.x + Mathf.Lerp(-halfWidth * 0.8f, halfWidth * 0.8f, t);
                    return new Vector3(x, spawnCenter.y + 2.6f, 0f);
                }

            case FormationType.Boss:
                return new Vector3(spawnCenter.x, spawnCenter.y + 2.6f, 0f);

            default:
                return new Vector3(spawnPos.x, spawnCenter.y + 2.4f, 0f);
        }
    }

    private float GetCameraHalfWidth()
    {
        Camera cam = Camera.main;
        if (cam == null || !cam.orthographic)
            return 2.8f;

        float aspect = cam.aspect > 0.01f ? cam.aspect : (9f / 16f);
        return cam.orthographicSize * aspect;
    }

    private float GetBottomSpawnY()
    {
        Camera cam = Camera.main;
        if (cam == null)
            return -6.2f;

        return cam.ViewportToWorldPoint(new Vector3(0.5f, -0.06f, Mathf.Abs(cam.transform.position.z))).y;
    }

    private Transform ResolvePlayerTarget()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        return playerObj != null ? playerObj.transform : null;
    }

    // =====================================================================
    // SỰ KIỆN
    // =====================================================================
    private void HandleEnemyDied(Enemy enemy)
    {
        if (enemy == null)
            return;

        if (!aliveEnemies.Remove(enemy))
            return;

        // Quái con (Hive/Carrier) chỉ giảm biến đếm riêng, KHÔNG cộng vào tiến trình màn.
        if (hiveMinions.Remove(enemy))
        {
            hiveMinionsAlive = Mathf.Max(0, hiveMinionsAlive - 1);
            activeEnemiesInWave = Mathf.Max(0, activeEnemiesInWave - 1);
            return;
        }

        activeEnemiesInWave = Mathf.Max(0, activeEnemiesInWave - 1);
        currentEnemiesKilled++;

        // V-Formation: leader chết -> hai cánh tỏa ra mép màn hình.
        if (enemy.IsVLeader && !vLeaderBroken)
        {
            vLeaderBroken = true;
            for (int i = 0; i < aliveEnemies.Count; i++)
            {
                if (aliveEnemies[i] != null && aliveEnemies[i].Formation == FormationType.VFormation)
                    aliveEnemies[i].BreakVFormation();
            }
        }

        if (!isVictoryTriggered && currentEnemiesKilled >= totalEnemiesToKill)
        {
            isVictoryTriggered = true;
            StartCoroutine(TriggerVictoryRoutine());
        }
    }

    /// <summary>Carrier/Hive phóng tàu con: tạo ngay tại vị trí tàu mẹ (không tính vào Wave).</summary>
    private void HandleHiveSpawn(Enemy carrier, GameObject minion)
    {
        if (carrier == null || minion == null || isVictoryTriggered)
            return;

        for (int i = 0; i < 2; i++)
        {
            Vector3 offset = new Vector3((i == 0 ? -0.55f : 0.55f), -0.15f, 0f);
            Vector3 spawnPos = carrier.transform.position + offset;

            // PATTERN: Factory — quái con cũng đi qua EnemyFactory.
            Enemy e = EnemyFactory.Create(minion, spawnPos);
            if (e == null)
                continue;

            e.Initialize(
                SpawnOrigin.TopCascade,
                FormationType.Kamikaze,
                SpecialSpawnType.None,
                spawnPos,
                spawnPos,
                2.4f, 0.6f, 2.2f, i, false, ResolvePlayerTarget());

            e.ConfigureFormationShooting(false, 0.3f);
            e.SetFirePattern(EnemyFirePattern.Single);
            e.SetMinionPrefab(null);

            // Quái con KHÔNG tính vào tiến trình màn (đã có tàu mẹ trong tổng số).
            // Chỉ đếm riêng để Wave biết còn quái trên màn khi kết thúc.
            hiveMinionsAlive++;
            hiveMinions.Add(e);
            activeEnemiesInWave++;
            aliveEnemies.Add(e);
        }
    }

    private int hiveMinionsAlive;
    private readonly List<Enemy> hiveMinions = new List<Enemy>(16);

    private void ClearRemainingEnemies()
    {
        // Chỉ quái CHÍNH còn sống mới tính vào tiến trình màn.
        // Quái con (Hive) không nằm trong tổng số nên phải trừ ra.
        int mainEnemiesRemaining = Mathf.Max(0, activeEnemiesInWave - hiveMinionsAlive);
        currentEnemiesKilled += mainEnemiesRemaining;

        for (int i = 0; i < aliveEnemies.Count; i++)
        {
            if (aliveEnemies[i] != null)
                Destroy(aliveEnemies[i].gameObject);
        }

        aliveEnemies.Clear();
        hiveMinions.Clear();
        hiveMinionsAlive = 0;
        activeEnemiesInWave = 0;

        if (!isVictoryTriggered && currentEnemiesKilled >= totalEnemiesToKill)
        {
            isVictoryTriggered = true;
            StartCoroutine(TriggerVictoryRoutine());
        }
    }

    private IEnumerator TriggerVictoryRoutine()
    {
        yield return new WaitForSeconds(1.5f);
        if (GameManager.Instance != null)
            GameManager.Instance.OnLevelCleared();
    }
}
