using UnityEngine;

/// <summary>
/// Hệ thống 4 Booster trong trận (gắn trong GameScene):
///
///  1. Bullet Overdrive : nhảy hỏa lực lên cấp tối đa (5 tia) cho cả màn.
///  2. Energy Shield    : bất tử 5 giây.
///  3. EMP Nuke         : xoá toàn bộ đạn địch + gây 50 sát thương toàn hạm đội.
///  4. Hyper Magnet     : hút mọi Coin/PowerUp trong 10 giây.
///
/// Mỗi Booster có lượt dùng giới hạn (đếm trong màn) và giá Coin tuỳ chọn.
/// HUD gọi <see cref="Activate"/> theo <see cref="BoosterType"/>.
///
/// Không có UI/HUD gắn vào thì vẫn dùng được từ code (ví dụ test trong Editor).
/// </summary>
public sealed class BoosterController : MonoBehaviour
{
    public static BoosterController Instance { get; private set; }

    public enum BoosterType
    {
        Overdrive = 0,
        Shield = 1,
        EmpNuke = 2,
        HyperMagnet = 3
    }

    [System.Serializable]
    public class BoosterConfig
    {
        public BoosterType type;
        public string displayName = "Booster";
        [Tooltip("Số lượt dùng mỗi màn")]
        [Min(0)] public int chargesPerLevel = 1;
        [Tooltip("Giá Coin mỗi lần dùng (0 = miễn phí)")]
        [Min(0)] public int coinCost;
        [Tooltip("Thời gian hiệu lực (giây); 0 = tức thời / cả màn")]
        public float duration;
    }

    private static readonly BoosterType[] AllTypes =
    {
        BoosterType.Overdrive, BoosterType.Shield, BoosterType.EmpNuke, BoosterType.HyperMagnet
    };

    [Header("--- Booster Config ---")]
    [SerializeField] private BoosterConfig[] boosters =
    {
        new BoosterConfig { type = BoosterType.Overdrive,   displayName = "Bullet Overdrive", chargesPerLevel = 1, coinCost = 300, duration = 0f },
        new BoosterConfig { type = BoosterType.Shield,      displayName = "Energy Shield",    chargesPerLevel = 1, coinCost = 350, duration = 5f },
        new BoosterConfig { type = BoosterType.EmpNuke,     displayName = "EMP Nuke",         chargesPerLevel = 1, coinCost = 400, duration = 0f },
        new BoosterConfig { type = BoosterType.HyperMagnet, displayName = "Hyper Magnet",     chargesPerLevel = 1, coinCost = 450, duration = 10f }
    };

    [Header("--- EMP Nuke ---")]
    [Tooltip("Sát thương diện rộng lên toàn bộ quái trong màn")]
    [SerializeField, Min(1)] private int empDamage = 50;

    private readonly int[] remainingCharges = new int[4];

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        ResetCharges();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>Nạp lại lượt dùng cho toàn bộ Booster (đầu mỗi màn).</summary>
    public void ResetCharges()
    {
        for (int t = 0; t < AllTypes.Length; t++)
        {
            BoosterConfig cfg = FindConfig((BoosterType)t);
            remainingCharges[t] = cfg != null ? Mathf.Max(0, cfg.chargesPerLevel) : 0;
        }
    }

    /// <summary>Số lượt còn lại của một Booster.</summary>
    public int GetRemaining(BoosterType type)
    {
        BoosterConfig cfg = FindConfig(type);
        if (cfg == null) return 0;

        int idx = (int)type;
        if (idx < 0 || idx >= remainingCharges.Length) return 0;
        return remainingCharges[idx];
    }

    /// <summary>Còn lượt và (nếu tốn Coin) đang đủ tiền hay không — dùng để bật/tắt nút trên HUD.</summary>
    public bool CanUse(BoosterType type)
    {
        if (GetRemaining(type) <= 0) return false;

        // Khoá nút khi trận đã kết thúc hoặc đang pause (khớp điều kiện của Activate).
        if (Time.timeScale <= 0f) return false;

        if (GameManager.Instance != null && GameManager.Instance.IsOverForBooster)
            return false;

        BoosterConfig cfg = FindConfig(type);
        int cost = cfg != null ? Mathf.Max(0, cfg.coinCost) : 0;
        if (cost == 0) return true;

        return GalaxyStrike.Save.SaveManager.Instance != null &&
               GalaxyStrike.Save.SaveManager.Instance.Coins >= cost;
    }

    /// <summary>
    /// Kích hoạt Booster: kiểm tra lượt + điều kiện + Coin, áp hiệu ứng rồi mới trừ tiền.
    /// Trả về true nếu dùng thành công.
    /// </summary>
    public bool Activate(BoosterType type)
    {
        int idx = (int)type;
        if (idx < 0 || idx >= remainingCharges.Length)
            return false;

        if (GetRemaining(type) <= 0)
            return false;

        // Không cho dùng khi game đang pause / đã thắng / đã thua / tàu đang gục:
        // nếu không người chơi bị trừ Coin mà hiệu ứng không áp lên trận đang chạy.
        if (Time.timeScale <= 0f)
            return false;

        if (GameManager.Instance != null && GameManager.Instance.IsOverForBooster)
            return false;

        PlayerController player = PlayerController.Instance;
        if (player == null || player.IsDowned)
            return false;

        BoosterConfig cfg = FindConfig(type);
        int cost = cfg != null ? Mathf.Max(0, cfg.coinCost) : 0;

        if (cost > 0)
        {
            var save = GalaxyStrike.Save.SaveManager.Instance;
            if (save == null || !save.TrySpendCurrency(GalaxyStrike.Data.CurrencyType.Gold, cost))
                return false;

            // HUD coin đang giữ bản sao nên đồng bộ lại số dư thật.
            if (GameManager.Instance != null)
                GameManager.Instance.SyncCoinsFromSave();
        }

        remainingCharges[idx]--;

        switch (type)
        {
            case BoosterType.Overdrive:   ApplyOverdrive();   break;
            case BoosterType.Shield:      ApplyShield(cfg);   break;
            case BoosterType.EmpNuke:     ApplyEmpNuke();     break;
            case BoosterType.HyperMagnet: ApplyHyperMagnet(cfg); break;
        }

        GalaxyStrike.Save.SaveManager.Instance?.RecordTaskMetric(GalaxyStrike.Data.TaskMetric.PowerUpsUsed);
        AudioManager.Play(SfxCue.Skill);

        return true;
    }

    // =====================================================================
    // HIỆU ỨNG TỪNG BOOSTER
    // =====================================================================

    /// <summary>Overdrive: đẩy hỏa lực lên cấp tối đa ngay lập tức (giữ tới khi nhặt power-up khác).</summary>
    private void ApplyOverdrive()
    {
        PlayerController player = PlayerController.Instance;
        if (player != null)
            player.SetBulletLevel(player.MaxBulletLevel);
    }

    /// <summary>Shield: bất tử theo thời gian cấu hình.</summary>
    private void ApplyShield(BoosterConfig cfg)
    {
        float seconds = cfg != null ? Mathf.Max(1f, cfg.duration) : 5f;

        PlayerController player = PlayerController.Instance;
        if (player != null)
            player.GrantInvulnerability(seconds);

        PlayerEquipment equipment = player != null ? player.GetComponent<PlayerEquipment>() : null;
        if (equipment != null)
            equipment.ActivateShield(seconds);
    }

    /// <summary>EMP Nuke: xoá toàn bộ đạn địch đang bay và gây sát thương diện rộng.</summary>
    private void ApplyEmpNuke()
    {
        // Xoá đạn địch: trả về pool thay vì Destroy để không sinh rác GC.
        EnemyBullet[] bullets = FindObjectsByType<EnemyBullet>(FindObjectsSortMode.None);
        for (int i = 0; i < bullets.Length; i++)
        {
            if (bullets[i] == null) continue;

            if (BulletPool.HasInstanceAvailable)
                BulletPool.Instance.Despawn(bullets[i].gameObject);
            else
                Destroy(bullets[i].gameObject);
        }

        // Sát thương diện rộng. Duyệt bản sao mảng để TakeDamage -> Die() không làm hỏng vòng lặp.
        Enemy[] enemies = FindObjectsByType<Enemy>(FindObjectsSortMode.None);
        for (int i = 0; i < enemies.Length; i++)
        {
            if (enemies[i] != null)
                enemies[i].TakeDamage(empDamage);
        }
    }

    /// <summary>Hyper Magnet: cưỡng bức hút Coin/PowerUp trong thời gian cấu hình.</summary>
    private void ApplyHyperMagnet(BoosterConfig cfg)
    {
        // Thiếu cấu hình booster -> 0 = dùng hyperMagnetDuration của PlayerEquipment (mặc định 10s).
        float seconds = cfg != null ? Mathf.Max(1f, cfg.duration) : 0f;

        PlayerController player = PlayerController.Instance;
        PlayerEquipment equipment = player != null ? player.GetComponent<PlayerEquipment>() : null;

        if (equipment != null)
            equipment.ActivateHyperMagnet(seconds);
    }

    /// <summary>Tên hiển thị của một Booster (dùng cho HUD/tooltip).</summary>
    public string GetDisplayName(BoosterType type)
    {
        BoosterConfig cfg = FindConfig(type);
        return cfg != null ? cfg.displayName : type.ToString();
    }

    private BoosterConfig FindConfig(BoosterType type)
    {
        if (boosters == null) return null;

        for (int i = 0; i < boosters.Length; i++)
        {
            if (boosters[i] != null && boosters[i].type == type)
                return boosters[i];
        }
        return null;
    }
}
