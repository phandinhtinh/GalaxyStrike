using UnityEngine;

/// <summary>
/// Object có thể tái sử dụng qua <see cref="BulletPool"/> (PATTERN: Object Pool).
///
/// Vòng đời: lấy ra -> Spawned() -> dùng -> trả về -> Despawned().
/// Implement phải reset TOÀN BỘ trạng thái trong <see cref="OnSpawned"/> để lần
/// tái sử dụng sau không mang rác từ lượt trước (vị trí, sát thương, xuyên thấu...).
/// </summary>
public interface IPoolable
{
    /// <summary>Được lấy ra khỏi pool. Reset state tại đây.</summary>
    void OnSpawned();

    /// <summary>Được trả về pool (thay cho Destroy). Tắt hiển thị / va chạm tại đây.</summary>
    void OnDespawned();
}
