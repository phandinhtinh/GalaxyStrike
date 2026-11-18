# Cấu trúc thư mục & Design Patterns — Galaxy Strike

Tài liệu mô tả cách `Assets/Scripts` được tổ chức và pattern nào áp ở đâu.
Cập nhật lần cuối: 2026-11-15 — sau khi dọn prefab trùng, migrate Singleton,
nối LevelManager→Factory, thêm Revive / Booster / Juice.

---

## 1. Cây thư mục

```
Assets/Scripts/
├── 00_Core/                 # Bootstrap + manager toàn cục (Singleton)
│   ├── GameBootstrap.cs     # RuntimeInitializeOnLoad: tạo service theo thứ tự xác định
│   ├── GameManager.cs       # Vòng đời 1 màn chơi + Revive + tự gắn FX/Booster
│   ├── LevelManager.cs      # Sinh 50 level (dùng EnemyFactory, KHÔNG giữ prefab)
│   └── Services/
│       ├── AudioManager.cs        : SingletonBehaviour<AudioManager>
│       ├── SaveManager.cs         : SingletonBehaviour<SaveManager>
│       ├── PanelManager.cs        : SingletonBehaviour<PanelManager>
│       ├── IapPurchaseService.cs  : SingletonBehaviour<IapPurchaseService>
│       └── LeaderboardService.cs  : SingletonBehaviour<LeaderboardService>
│
├── 01_Data/                 # KHÔNG phụ thuộc gameplay/UI
│   ├── ScriptableObjects/   # ShipDataSO, ItemDataSO, PlayerDataSO, IapCatalogAsset
│   ├── Models/              # SaveData, IapCatalog, LeaderboardModels (plain class)
│   ├── Enums/               # SpawnOrigin, FormationType, SpecialSpawnType, EnemyFirePattern
│   └── Catalogs/            # ShipUpgradeCatalog (bảng tra cứng)
│
├── 02_Patterns/             # Hạ tầng pattern, không chứa logic game cụ thể
│   ├── Singleton/           # SingletonBehaviour<T>  (5 service dùng thật)
│   ├── ObjectPool/          # IPoolable, BulletPool (đạn/quái va chạm)
│   ├── State/               # IEnemyState, EnemyStateMachine, EnemyContext (hạ tầng sẵn sàng)
│   ├── Factory/             # EnemyFactory, EnemyRegistry, EnemyKind  (nguồn prefab DUY NHẤT)
│   └── Command/             # ICommand, CommandBase, CommandInvoker, PlayerCommands (hạ tầng)
│
├── 03_Gameplay/             # Actor & hệ thống trong trận
│   ├── Player/              # PlayerController, PlayerEquipment, BoosterController
│   ├── Enemies/             # Enemy, BossController, EnemySpawner
│   ├── Projectiles/         # Bullet, EnemyBullet, HomingProjectile
│   ├── Pickups/             # BulletUpgradeItem, CoinDrop
│   ├── Presentation/        # BackgroundScroll, AmbushWarning
│   └── Fx/                  # CameraShake, VfxSpawner, EngineTrail, Haptics
│
├── 04_UI/                   # View/Controller theo màn hình
│   ├── Common/              # SettingsPanelUI, SafeAreaFitter, FloatingEffect
│   ├── Home/                # HomeSceneManager, UpgradePanelUI, BackpackUIController
│   ├── Gameplay/            # HUD_Panel, VictoryPanelUI, GameOverPanelUI,
│   │                        # RevivePanelUI, BoosterHUD, WaveProgressHUD
│   ├── Shop/                # ShopManager, ShopItemUI, ShopCategoryScroller, IapPackCardUI...
│   ├── Tasks/               # TaskPopupController, TaskPopupRowView
│   ├── Leaderboard/         # LeaderboardPanelUI
│   └── Loading/             # LoadingSceneController
│
└── 90_Editor/               # CHỈ chạy trong Unity Editor (không vào build)
    └── GalaxyStrike*Setup.cs, *Builder.cs
```

### Quy ước phụ thuộc (một chiều, không vòng)

```
00_Core ──▶ 01_Data
   │           ▲
   ├──▶ 03_Gameplay ──▶ 02_Patterns
   │           │
   └──▶ 04_UI ─┘
```

- `01_Data` **không** được `using` gameplay/UI.
- `02_Patterns` **không** được biết tới UI hay level cụ thể.
- `90_Editor` được phép `using` mọi thứ, nhưng không file nào được `using UnityEditor` ngoài
  thư mục `90_Editor`.

---

## 2. Pattern nào đang áp ở đâu

| Pattern | Trạng thái | Nơi áp dụng |
|---|---|---|
| **Singleton** | ✅ **Đã migrate** | `02_Patterns/Singleton/SingletonBehaviour.cs`; 5 service kế thừa base; `BulletPool` cố ý giữ riêng (per-scene) |
| **Observer** | ✅ Đang dùng rộng | `Enemy.OnEnemyDied` / `Enemy.HiveSpawned` → `EnemySpawner`; `SaveManager.OnDataChanged` → UI; `IapPurchaseService.OnStoreReady` → Shop |
| **State** | ⏳ Hạ tầng sẵn sàng | `02_Patterns/State/` — chưa migrate `Enemy` (xem mục 4) |
| **Object Pool** | ✅ Đã áp dụng runtime | `Bullet`, `EnemyBullet` recycle qua `BulletPool`; EMP Nuke cũng trả đạn về pool |
| **Factory Method** | ✅ **Đã áp dụng runtime** | `EnemyFactory.Create(...)`; `LevelManager` chọn prefab qua `EnemyKind` + `EnemyRegistry` |
| **Command** | ⏳ Hạ tầng sẵn sàng | `02_Patterns/Command/` — chưa nối vào `PlayerController` (xem mục 5) |

Ngoài danh sách, code còn dùng **Facade** (`HUD_Panel`), **Template** (`CommandBase`,
`SingletonBehaviour`), **Registry/Service Locator** (`EnemyRegistry`).

---

## 3. Object Pool — đạn và hiệu ứng

- `BulletPool` giữ object đã tạo, `prewarmCount` = 24 mỗi prefab.
- `Bullet` và `EnemyBullet` implement `IPoolable`, trả về pool thay vì `Destroy`.
- `PlayerController` và `Enemy` lấy đạn từ pool.
- Va chạm shield của player cũng trả đạn về pool (không `Destroy`).
- **EMP Nuke** (`BoosterController.ApplyEmpNuke`) cũng trả đạn về pool.

### Điều kiện để prefab dùng được pool

1. Prefab đạn phải có component `Bullet` / `EnemyBullet`.
2. **Collider phải là `isTrigger`**.
3. Không giữ tham chiếu tĩnh tới instance đạn sau khi nó despawn.

---

## 4. State Machine — hạ tầng đã có, `Enemy` chưa migrate

`Enemy.cs` hiện dùng `switch (formation)` với 7 nhánh `Fly*`.

Hạ tầng: `IEnemyState` (`Enter/Tick/Exit`), `EnemyContext`, `EnemyStateMachine`
(cho phép `ChangeState` từ trong `Tick`, hoãn áp tới cuối frame).

### Cách migrate (từng bước)

1. Viết `SnakeFlyState` bám đúng `FlySnake` hiện tại.
2. Trong `Enemy`, thêm `EnemyStateMachine`; ở `Initialize` gọi `SetInitialState(...)`.
3. So sánh quỹ đạo trước/sau trên cùng một Wave.
4. Lặp lại cho Wall → VFormation → PincerCross → DropAnchor → Kamikaze → Boss.

---

## 5. Command — hạ tầng đã có, chưa nối vào input

Đã có `PlayerController.MoveTo/MoveBy/ScreenToShipPosition`.
Command tương ứng: `PlayerCommands.MoveToWorldPosition`, `PlayerCommands.MoveByAxis`.

Nối trong `HandleMovement()` với `CommandInvoker` riêng và `HistoryLimit = 0`
(không ghi history cho input realtime).

---

## 6. Singleton — đã migrate

Tất cả service toàn cục đã kế thừa `SingletonBehaviour<T>`:

```csharp
public sealed class AudioManager : SingletonBehaviour<AudioManager>
{
    protected override void OnSingletonAwake() { /* nội dung Awake cũ */ }
}
```

- `Instance` được base class cung cấp, call site `X.Instance` **giữ nguyên**.
- `GameBootstrap.ResetStatics()` gọi `SingletonBehaviour<X>.ResetStaticInstance()` cho cả 5 service.
- **Không** migrate `BulletPool` — nó cố ý **không** persist qua scene.

---

## 7. Revive — hồi sinh tại chỗ

- `PlayerController.TakeDamage` không còn `Destroy` khi HP = 0: chuyển sang
  trạng thái "downed" (tắt collider + renderer) rồi gọi `GameManager.OnPlayerDefeated()`.
- `GameManager` mời hồi sinh nếu `CanRevive` (mặc định 1 lần/màn); giá lũy tiến
  `900 + 500 × số_lần_đã_hồi`.
- `RevivePanelUI` tự dựng panel nếu Scene chưa author (`EnsureExists`).
- Hồi sinh: đầy máu + bất tử `reviveShieldSeconds` (mặc định 3s).
- Rewarded Ad: `TryReviveWithAd()` hiện là stub — thay bằng callback SDK khi tích hợp ads.

---

## 8. Booster — 4 booster trong trận

`BoosterController` (gắn tự động bởi `GameManager.Awake`):

| Booster | Hiệu ứng | Mặc định |
|---|---|---|
| Bullet Overdrive | `SetBulletLevel(MaxBulletLevel)` | 1 lượt, 300 coin |
| Energy Shield | bất tử 5s | 1 lượt, 350 coin |
| EMP Nuke | xoá đạn địch + 50 dmg toàn hạm đội | 1 lượt, 400 coin |
| Hyper Magnet | hút Coin/PowerUp 10s | 1 lượt, 450 coin |

`BoosterHUD` dựng thanh 4 nút (tự sinh nếu Scene chưa có) và cập nhật số lượt mỗi frame.

---

## 9. Juice — cảm giác chơi

| Thành phần | File | Kích hoạt |
|---|---|---|
| Camera shake | `03_Gameplay/Fx/CameraShake.cs` | trúng đạn, quái chết, nhặt item |
| Vụ nổ | `03_Gameplay/Fx/VfxSpawner.cs` | `Enemy.Die()` (Boss nổ to hơn) |
| Tia lửa | `VfxSpawner.SpawnHitSpark` | `Bullet` trúng quái, nhặt coin/powerup |
| Vệt động cơ | `03_Gameplay/Fx/EngineTrail.cs` | tự gắn vào Player khi `Awake` |
| Haptic | `03_Gameplay/Fx/Haptics.cs` | trúng đạn / nổ / nút không đủ tiền |

`GameManager.EnsureGameplayFxSystems()` tự gắn `CameraShake` (lên `Camera.main`),
`BoosterController`, `BoosterHUD` nếu Scene thiếu — nên **không cần author tay**.

---

## 10. Kiểm tra sau khi đổi cấu trúc

- [x] Mọi file `.cs` giữ nguyên **GUID** trong `.meta`.
- [x] Prefab quái chỉ còn **một bản duy nhất** tại `Assets/Resources/Prefabs/`.
- [x] `GameScene.unity` không còn tham chiếu tới prefab đã xoá
      (`Enemy.prefab`, 9 field `*Prefab` của LevelManager).
- [ ] `Assembly-CSharp.csproj` cần Unity **regenerate** (focus vào Editor một lần).
