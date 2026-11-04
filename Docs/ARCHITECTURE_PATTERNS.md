# Cấu trúc thư mục & Design Patterns — Galaxy Strike

Tài liệu mô tả cách `Assets/Scripts` được tổ chức và pattern nào áp ở đâu.

---

## 1. Cây thư mục

```
Assets/Scripts/
├── 00_Core/                 # Bootstrap + manager toàn cục (Singleton)
│   ├── GameBootstrap.cs     # RuntimeInitializeOnLoad: tạo service theo thứ tự xác định
│   ├── GameManager.cs       # Vòng đời 1 màn chơi (score, coin, win/lose)
│   ├── LevelManager.cs      # Sinh 50 level, tiến trình, Campaign/Challenge
│   └── Services/
│       ├── AudioManager.cs
│       ├── SaveManager.cs
│       ├── PanelManager.cs
│       ├── IapPurchaseService.cs
│       └── LeaderboardService.cs
│
├── 01_Data/                 # KHÔNG phụ thuộc gameplay/UI
│   ├── ScriptableObjects/   # ShipDataSO, ItemDataSO, PlayerDataSO, IapCatalogAsset
│   ├── Models/              # SaveData, IapCatalog, LeaderboardModels (plain class)
│   ├── Enums/               # SpawnOrigin, FormationType, SpecialSpawnType, EnemyFirePattern
│   └── Catalogs/            # ShipUpgradeCatalog (bảng tra cứng)
│
├── 02_Patterns/             # Hạ tầng pattern, không chứa logic game cụ thể
│   ├── Singleton/           # SingletonBehaviour<T>
│   ├── ObjectPool/          # IPoolable, BulletPool
│   ├── State/               # IEnemyState, EnemyStateMachine, EnemyContext
│   ├── Factory/             # EnemyFactory, EnemyRegistry, EnemyKind
│   └── Command/             # ICommand, CommandBase, CommandInvoker, PlayerCommands
│
├── 03_Gameplay/             # Actor & hệ thống trong trận
│   ├── Player/              # PlayerController, PlayerEquipment
│   ├── Enemies/             # Enemy, BossController, EnemySpawner
│   ├── Projectiles/         # Bullet, EnemyBullet, HomingProjectile
│   ├── Pickups/             # BulletUpgradeItem
│   └── Presentation/        # BackgroundScroll, AmbushWarning (thuần hiển thị)
│
├── 04_UI/                   # View/Controller theo màn hình
│   ├── Common/              # SettingsPanelUI, SafeAreaFitter, FloatingEffect
│   ├── Home/                # HomeSceneManager, UpgradePanelUI, BackpackUIController
│   ├── Gameplay/            # HUD_Panel, VictoryPanelUI, GameOverPanelUI
│   ├── Shop/                # ShopManager, ShopItemUI, ShopCategoryScroller, IapPackCardUI
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
  thư mục `90_Editor` (đã kiểm tra: 0 vi phạm).

---

## 2. Pattern nào đang áp ở đâu

| Pattern | Trạng thái | Nơi áp dụng |
|---|---|---|
| **Singleton** | ✅ Có base class, **dùng thật** cho `BulletPool` | `02_Patterns/Singleton/SingletonBehaviour.cs`; `BulletPool` (per-scene) |
| **Observer** | ✅ Đang dùng rộng | `Enemy.OnEnemyDied` / `Enemy.HiveSpawned` (static event) → `EnemySpawner`; `SaveManager.OnDataChanged` → UI; `IapPurchaseService.OnStoreReady` → Shop |
| **State** | ✅ Hạ tầng sẵn sàng | `02_Patterns/State/` — xem mục 4 (chưa migrate `Enemy`) |
| **Object Pool** | ✅ **Đã áp dụng runtime** | `Bullet`, `EnemyBullet` recycle qua `BulletPool`; bỏ `Instantiate`/`Destroy` mỗi phát bắn |
| **Factory Method** | ✅ **Đã áp dụng runtime** | `EnemyFactory.Create(...)`; `EnemySpawner` + `Enemy.Die()` (splitting) đều đi qua đây |
| **Command** | ✅ Hạ tầng sẵn sàng | `02_Patterns/Command/` — xem mục 5 (chưa nối vào `PlayerController`) |

Ngoài danh sách, code còn dùng **Facade** (`HUD_Panel`), **Template** (`CommandBase`,
`SingletonBehaviour`), **Registry/Service Locator** (`EnemyRegistry`).

---

## 3. Object Pool — thay đổi hành vi (QUAN TRỌNG)

Trước: mỗi viên đạn người chơi **và** đạn địch đều `Instantiate` rồi `Destroy`.
Trên Android, màn khó có thể sinh 30–60 object/giây → GC spike, tụt khung.

Sau:

- `BulletPool` giữ object đã tạo, `prewarmCount` = 24 mỗi prefab.
- `Bullet` và `EnemyBullet` implement `IPoolable`, trả về pool thay vì `Destroy`.
- `PlayerController` và `Enemy` lấy đạn từ pool.
- Va chạm shield của player cũng trả đạn về pool (không `Destroy`).

### Điều kiện để prefab dùng được pool

1. Prefab đạn phải có component `Bullet` / `EnemyBullet` (đã có).
2. **Collider phải là `isTrigger`** (đã dùng `OnTriggerEnter2D` nên chắc chắn đúng).
3. Không giữ tham chiếu tĩnh tới instance đạn sau khi nó despawn.

### Tinh chỉnh nếu cần

Gắn `BulletPool` vào một GameObject trong `GameScene` để chỉnh trong Inspector.
Nếu không gắn, pool tự sinh và dùng giá trị mặc định.

---

## 4. State Machine — hạ tầng đã có, `Enemy` chưa migrate

`Enemy.cs` hiện dùng `switch (formation)` với 7 nhánh `Fly*` (~200 dòng).

Hạ tầng mới:

- `IEnemyState` — `Enter / Tick / Exit`.
- `EnemyContext` — dữ liệu chung, tách khỏi `MonoBehaviour` (dễ unit test).
- `EnemyStateMachine` — chuyển state an toàn, cho phép `ChangeState` **từ trong** `Tick`
  (hoãn áp dụng tới cuối frame, tránh phá vòng lặp).

### Cách migrate (làm từng bước, không gãy gameplay)

1. Viết `SnakeFlyState` bám đúng `FlySnake` hiện tại (copy thân hàm, thay
   `transform` → `ctx.body`).
2. Trong `Enemy`, thêm `EnemyStateMachine`; ở `Initialize` gọi
   `SetInitialState(new SnakeFlyState(), ctx)` thay cho `formation = FormationType.Snake`.
3. So sánh quỹ đạo trước/sau trên cùng một Wave.
4. Lặp lại cho Wall → VFormation → PincerCross → DropAnchor → Kamikaze → Boss.

Riêng `Kamikaze` là ứng viên rõ nhất cho State Machine vì nó **đã có 2 pha**
(`orbit` → `locked dive`), hiện cài bằng cờ `isKamikazeLocked`/`hasDivedPast`.

---

## 5. Command — hạ tầng đã có, chưa nối vào input

Đã thêm vào `PlayerController`:

```csharp
public void MoveTo(Vector3 worldPosition);          // touch/drag
public void MoveBy(Vector2 axis, float dt);          // bàn phím
public Vector3 ScreenToShipPosition(Vector3 screen); // toạ độ ngón tay -> tàu
```

Command tương ứng: `PlayerCommands.MoveToWorldPosition`, `PlayerCommands.MoveByAxis`.

### Cách nối (bước tiếp theo)

Trong `HandleMovement()`:

```csharp
if (pointerDown)
    _commands.Execute(new PlayerCommands.MoveToWorldPosition(this, ScreenToShipPosition(Input.mousePosition)));

if (axis != Vector2.zero)
    _commands.Execute(new PlayerCommands.MoveByAxis(this, axis, Time.deltaTime));
```

Kèm một `CommandInvoker` field (ví dụ `private readonly CommandInvoker _commands = new CommandInvoker();`).

**Lưu ý:** không ghi `MoveTo` vào history mỗi frame (60 command/giây × 64 slot = vô nghĩa).
Chỉ nên ghi command khi làm replay; với input realtime dùng `Execute` với `HistoryLimit = 0`
hoặc tạo invoker riêng cho replay.

---

## 6. Singleton — hướng dẫn migrate

`SingletonBehaviour<T>` đã sẵn nhưng **các manager hiện tại vẫn tự viết singleton**.
Migrate khi có thời gian, theo mẫu:

```csharp
// Trước
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }
    private void Awake() { ... Instance = this; DontDestroyOnLoad(gameObject); ... }
}

// Sau
public sealed class AudioManager : SingletonBehaviour<AudioManager>
{
    protected override void OnSingletonAwake() { /* nội dung Awake cũ */ }
}
```

`AudioManager.Instance` ở mọi nơi **giữ nguyên**, không phải sửa call site.

**Đừng** migrate `BulletPool` — nó cố ý **không** persist qua scene vì object trong pool
gắn với scene hiện tại.

---

## 7. Kiểm tra sau khi đổi cấu trúc

- [x] Mọi file `.cs` giữ nguyên **GUID** trong `.meta` → scene/prefab không mất tham chiếu.
- [x] Không còn thư mục cũ (`Core`, `Data`, `Gameplay`, `IAP`, `Leaderboard`, `Save`, `UI`, `Editor`).
- [x] Không file runtime nào `using UnityEditor`.
- [x] Không có tham chiếu chuỗi hard-code tới đường dẫn `Assets/Scripts/...`.
- [x] `Assembly-CSharp.csproj` cần Unity **regenerate** (focus vào Editor một lần).
