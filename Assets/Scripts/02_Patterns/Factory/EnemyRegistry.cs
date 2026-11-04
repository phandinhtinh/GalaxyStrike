using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Sổ đăng ký loại quái theo khoá logic (PATTERN: Factory — phần tra cứu).
///
/// Thay vì spawner/level manager giữ 9 tham chiếu prefab rời rạc
/// (scoutPrefab, fighterPrefab, ...), mọi prefab được đăng ký theo <see cref="EnemyKind"/>
/// và tra bằng 1 khoá.
///
/// Không phải MonoBehaviour: có thể dựng trong code, đọc từ Resources, hoặc
/// (giai đoạn sau) nạp từ Addressables.
/// </summary>
public sealed class EnemyRegistry
{
    private readonly Dictionary<EnemyKind, GameObject> byKind = new Dictionary<EnemyKind, GameObject>(16);
    private readonly Dictionary<GameObject, EnemyKind> reverse = new Dictionary<GameObject, EnemyKind>(16);

    /// <summary>Số loại đã đăng ký được (có prefab khác null).</summary>
    public int Count => byKind.Count;

    public void Register(EnemyKind kind, GameObject prefab)
    {
        if (prefab == null)
        {
            byKind.Remove(kind);
            return;
        }

        byKind[kind] = prefab;

        if (!reverse.ContainsKey(prefab))
            reverse[prefab] = kind;
    }

    /// <summary>Lấy prefab theo loại. Trả null nếu chưa đăng ký.</summary>
    public GameObject Get(EnemyKind kind)
    {
        return byKind.TryGetValue(kind, out GameObject prefab) ? prefab : null;
    }

    public bool TryGet(EnemyKind kind, out GameObject prefab)
    {
        return byKind.TryGetValue(kind, out prefab) && prefab != null;
    }

    /// <summary>Hỏi ngược: prefab này thuộc loại quái nào.</summary>
    public bool TryGetKind(GameObject prefab, out EnemyKind kind)
    {
        kind = default;
        return prefab != null && reverse.TryGetValue(prefab, out kind);
    }

    /// <summary>
    /// Nạp mặc định từ Resources/Prefabs — khớp với cách LevelManager đang load.
    /// Gọi 1 lần khi khởi tạo game; trả về số loại nạp được.
    /// </summary>
    public int LoadDefaultsFromResources()
    {
        int loaded = 0;
        foreach (EnemyKind kind in Enum.GetValues(typeof(EnemyKind)))
        {
            if (kind == EnemyKind.None)
                continue;

            GameObject prefab = Resources.Load<GameObject>("Prefabs/" + kind);
            if (prefab == null)
                continue;

            Register(kind, prefab);
            loaded++;
        }

        return loaded;
    }

    public void Clear()
    {
        byKind.Clear();
        reverse.Clear();
    }
}

/// <summary>Danh sách chủng quái. Tên khớp tên prefab trong Resources/Prefabs.</summary>
public enum EnemyKind
{
    None = 0,
    Enemy_Scout = 1,
    Enemy_Fighter = 2,
    Enemy_Striker = 3,
    Enemy_Gunship = 4,
    Enemy_Cruiser = 5,
    Enemy_Destroyer = 6,
    Enemy_Elite = 7,
    Enemy_MiniBoss = 8,
    Enemy_Boss = 9,
    Enemy_Swarmer = 10,
    Enemy_ShieldUnit = 11,
    Enemy_TwinCannon = 12,
    Enemy_MissileShip = 13,
    Enemy_Wasp = 14,
    Enemy_AlienBoss = 15
}
