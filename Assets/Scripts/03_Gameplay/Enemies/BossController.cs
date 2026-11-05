using UnityEngine;

/// <summary>
/// Gắn script này lên prefab Enemy_Boss (và Enemy_MiniBoss nếu muốn).
/// EnemySpawner phải đặt Wave của Boss thành BossPattern.
/// </summary>
public class BossController : MonoBehaviour
{
    [Header("Horizontal movement")]
    [SerializeField] private float horizontalAmplitude = 1.5f;
    [SerializeField] private float horizontalFrequency = 0.8f;
    [SerializeField] private float verticalAmplitude = 0.25f;

    [Header("Phase thresholds (HP ratio)")]
    [Range(0.1f, 0.9f)] [SerializeField] private float phase2Threshold = 0.6f;
    [Range(0.05f, 0.8f)] [SerializeField] private float phase3Threshold = 0.25f;
    [SerializeField] private float phase2Multiplier = 1.35f;
    [SerializeField] private float phase3Multiplier = 1.8f;

    private Enemy enemy;
    private SpriteRenderer spriteRenderer;
    private Vector3 baseLocalPosition;
    private bool movementInitialized;
    private int currentPhase;
    private Color originalColor = Color.white;

    [Header("Entry")]
    [Tooltip("Boss chui xuống từ trên trong khoảng thời gian này trước khi bắt đầu dao động")]
    [SerializeField] private float entryDuration = 1.6f;
    [SerializeField] private float entryTargetY = 2.2f;

    private float entryTimer;
    private float entryStartY;
    private float currentEntryTargetY;
    private bool hasEntered;

    private void Awake()
    {
        // Cache Enemy và SpriteRenderer để Update không phải GetComponent mỗi frame.
        enemy = GetComponent<Enemy>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
            originalColor = spriteRenderer.color;

        // Đặt trạng thái ban đầu: chưa vào màn, chưa bay (movement chưa init).
        entryTimer = 0f;
        entryStartY = transform.position.y;
        currentEntryTargetY = entryTargetY;
        hasEntered = false;
        movementInitialized = false;
    }

    /// <summary>
    /// Bắt đầu pha "chui xuống": ghi vị trí xuất phát (spawnPosition.y), tính đích
    /// an toàn trong khung hình và reset bộ đếm/phase để Boss vào màn từ đầu.
    /// Được Enemy.Initialize gọi khi tạo Boss.
    /// </summary>
    public void BeginEntry(Vector3 spawnPosition)
    {
        entryStartY = spawnPosition.y;
        currentEntryTargetY = GetSafeEntryTargetY();
        entryTimer = 0f;
        hasEntered = false;
        movementInitialized = false;
        currentPhase = 0;
    }

    /// <summary>
    /// Vòng lặp Boss chia 2 pha:
    /// 1) ENTRY: trượt từ trên xuống độ cao chiến đấu (dùng easing smoothstep).
    /// 2) FIGHT: dao động ngang/dọc quanh vị trí gốc, càng mất máu càng nhanh/to hơn
    ///    (theo phase), đồng thời đổi màu sprite + tốc độ bắn qua ApplyPhase.
    /// </summary>
    private void Update()
    {
        if (enemy == null || enemy.IsDead || Time.timeScale <= 0f)
            return;

        // Giai đoạn 1: trượt xuống từ phía trên màn hình cho tới độ cao chiến đấu.
        if (!hasEntered)
        {
            entryTimer += Time.deltaTime;
            float progress = Mathf.Clamp01(entryTimer / Mathf.Max(0.01f, entryDuration));
            progress = progress * progress * (3f - 2f * progress);
            Vector3 pos = transform.position;
            pos.y = Mathf.Lerp(entryStartY, currentEntryTargetY, progress);
            transform.position = pos;

            if (progress >= 1f)
            {
                pos.y = currentEntryTargetY;
                transform.position = pos;
                hasEntered = true;
                movementInitialized = false;
            }

            return;
        }

        if (!movementInitialized)
        {
            baseLocalPosition = transform.localPosition;
            movementInitialized = true;
        }

        int phase = GetPhase(enemy.HealthRatio);
        if (phase != currentPhase)
        {
            currentPhase = phase;
            ApplyPhase(phase);
        }

        float speedMultiplier = phase == 3
            ? phase3Multiplier
            : phase == 2 ? phase2Multiplier : 1f;

        float t = Time.time * horizontalFrequency * speedMultiplier;
        Vector3 offset = new Vector3(
            Mathf.Sin(t) * horizontalAmplitude * (phase == 3 ? 1.15f : 1f),
            Mathf.Sin(t * 0.55f) * verticalAmplitude,
            0f
        );

        transform.localPosition = baseLocalPosition + offset;
    }

    /// <summary>
    /// Xác định phase (1..3) dựa trên tỉ lệ máu còn lại:
    /// càng ít máu thì phase càng cao (Boss càng hung hăng).
    /// </summary>
    private int GetPhase(float healthRatio)
    {
        if (healthRatio <= phase3Threshold)
            return 3;
        if (healthRatio <= phase2Threshold)
            return 2;
        return 1;
    }

    /// <summary>
    /// Tính độ cao đích khi vào màn, đảm bảo Boss không trôi quá thấp:
    /// nếu là camera orthographic thì lấy cao hơn mức y của "làn trên" màn hình.
    /// </summary>
    private float GetSafeEntryTargetY()
    {
        float targetY = entryTargetY;
        Camera cam = Camera.main;
        if (cam != null && cam.orthographic)
        {
            float upperLaneY = cam.ViewportToWorldPoint(
                new Vector3(0.5f, 0.62f, Mathf.Abs(cam.transform.position.z))).y;
            targetY = Mathf.Max(targetY, upperLaneY);
        }
        return targetY;
    }

    /// <summary>
    /// Áp hiệu ứng khi đổi phase: tăng tốc độ bắn (SetFireRateMultiplier),
    /// báo Enemy dùng combo đạn của phase đó (SetBossPhase) và đổi màu sprite
    /// (tím hồng ở phase 2, đỏ ở phase 3) để người chơi dễ nhận biết.
    /// </summary>
    private void ApplyPhase(int phase)
    {
        if (enemy != null)
        {
            enemy.SetFireRateMultiplier(phase == 3 ? 1.8f : phase == 2 ? 1.3f : 1f);
            enemy.SetBossPhase(phase);
        }

        if (spriteRenderer == null)
            return;

        switch (phase)
        {
            case 1:
                spriteRenderer.color = originalColor;
                break;
            case 2:
                spriteRenderer.color = new Color(1f, 0.72f, 0.9f, 1f);
                break;
            case 3:
                spriteRenderer.color = new Color(1f, 0.4f, 0.45f, 1f);
                break;
        }
    }
}
