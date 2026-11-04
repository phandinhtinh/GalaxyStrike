# Bảng xếp hạng — Hướng dẫn cấu hình Google Play Games

> **Tài liệu này hướng dẫn cấu hình 1 lần duy nhất.**
> Code đã hoàn chỉnh: nếu chưa cấu hình, bảng xếp hạng tự chạy **chế độ OFFLINE**
> (chỉ hiện thành tích của chính bạn) và **không làm crash game**.

---

## Tổng quan

| Chế độ | Khi nào | Hiển thị |
|---|---|---|
| **OFFLINE** | Chưa cài plugin hoặc chưa điền ID | 1 dòng: thành tích của bạn |
| **ONLINE** | Đã cài plugin + điền ID + đăng nhập Google | Top 20 người chơi toàn cầu |

Code tự chọn chế độ dựa vào symbol `GOOGLE_PLAY_GAMES` và `LeaderboardService.Config.IsConfigured`.

---

## 3 Leaderboard cần tạo

| Tên hiển thị | Loại điểm | Đơn vị | Sắp xếp |
|---|---|---|---|
| ĐIỂM CAO | Điểm cao nhất trong 1 màn | Points (Integer) | Lớn → nhỏ |
| LEVEL | Level cao nhất đã chinh phục | Points (Integer) | Lớn → nhỏ |
| QUÁI HẠ | Tổng số quái đã hạ | Points (Integer) | Lớn → nhỏ |

---

## Bước 1 — Đổi Bundle ID (BẮT BUỘC trước khi tạo Play Games)

Play Games Services gắn chặt với package name. **Phải chốt trước khi tạo app trên Play Console**, nếu đổi sau phải tạo lại toàn bộ.

1. Unity → **Edit → Project Settings → Player → Other Settings**
2. **Package Name**: đặt dạng `com.<tenstudio>.galaxystrike`
   - Ví dụ: `com.abi.galaxystrike`, `com.studioabc.galaxystrike`
3. Ghi nhớ chính xác chuỗi này.

> Hiện tại project đang là `com.DefaultCompany.2DProject` — **Google Play sẽ từ chối** khi phát hành.

---

## Bước 2 — Tạo game trên Play Console

1. https://play.google.com/console
2. **Play Games Services → Setup and management → Configuration**
3. **Create new Play Games Services project**
4. Điền:
   - App name: `Galaxy Strike`
   - Android package name: **đúng chuỗi ở Bước 1**
5. Tab **Credentials**:
   - **Add credential** → **Android**
   - OAuth Client ID: làm theo hướng dẫn Google Cloud (nó sẽ mở tab mới)
   - **SHA-1 fingerprint**: lấy bằng lệnh dưới (xem Bước 4)
6. Tab **Testers**: thêm email Google của bạn (để test trước khi phát hành)

---

## Bước 3 — Tạo 3 Leaderboard

**Play Games Services → Leaderboards → Create leaderboard**

Với mỗi bảng:
- **Name**: theo bảng trên
- **Score format type**: `Numeric`
- **Score Order**: `Larger is better`
- **Icon**: có thể bỏ trống

Sau khi tạo, mỗi bảng có **ID** dạng:

```
CgkI1234567890EAIQAQ
```

**Copy 3 ID này** — sẽ dán vào Unity ở Bước 6.

---

## Bước 4 — Lấy SHA-1 fingerprint

### Nếu chưa có keystore:
```bash
keytool -genkey -v -keystore galaxystrike.keystore -alias galaxystrike ^
  -keyalg RSA -keysize 2048 -validity 10000
```

### Lấy SHA-1:
```bash
keytool -list -v -keystore galaxystrike.keystore -alias galaxystrike
```

Tìm dòng:
```
SHA1: AB:CD:EF:01:23:45:67:89:AB:CD:EF:01:23:45:67:89:AB:CD:EF:01
```

**Quan trọng:** Google Play dùng **Play App Signing** nên sau khi upload, SHA-1 có thể khác. Lấy thêm ở:
**Play Console → Setup → App integrity → App signing → SHA-1**

Thêm **cả 2 SHA-1** vào Credentials của Play Games.

---

## Bước 5 — Lấy Web Client ID

1. https://console.cloud.google.com
2. Chọn project vừa tạo (cùng tên với Play Games)
3. **APIs & Services → Credentials**
4. **Create Credentials → OAuth client ID**
5. Application type: **Web application**
6. Name: `Galaxy Strike Web`
7. **Create**
8. Copy **Client ID**, dạng:

```
123456789012-abcdefghijklmnop.apps.googleusercontent.com
```

---

## Bước 6 — Cài plugin vào Unity

1. Unity → **Window → Package Manager**
2. Nút **+** (góc trên trái) → **Add package from git URL...**
3. Dán:

```
https://github.com/playgameservices/play-games-plugin-for-unity.git#current-build
```

4. Bấm **Add**, đợi tải xong
5. Unity sẽ tự định nghĩa symbol `GOOGLE_PLAY_GAMES`
6. Vào **Window → Google Play Games → Setup → Android Setup**
   - Paste **Web Client ID** ở Bước 5
   - Bấm **Setup**

Sau bước này, `LeaderboardService` tự chuyển sang chế độ **ONLINE**.

---

## Bước 7 — Điền ID vào game

`LeaderboardService` là singleton tự sinh (không nằm sẵn trong Scene), nên chỗ điền ID là qua **ScriptableObject** hoặc **PlayerPrefs**.

### Cách khuyến nghị: tạo asset config

1. Project window → chuột phải → **Create → Galaxy Strike → Leaderboard Config**
   (nếu chưa có menu này, gắn tạm `LeaderboardService` lên một GameObject bất kỳ trong HomeScene, điền tay, rồi Remove Component — giá trị sẽ mất.)

### Cách đơn giản nhất: sửa trực tiếp trong code

Mở `Assets/Scripts/Leaderboard/LeaderboardService.cs`, tìm dòng:

```csharp
[SerializeField] private PlayGamesConfig config = new PlayGamesConfig();
```

Đổi thành:

```csharp
[SerializeField] private PlayGamesConfig config = new PlayGamesConfig
{
    webClientId = "123456789012-abc.apps.googleusercontent.com",
    highScoreLeaderboardId = "CgkI..._highscore",
    highestLevelLeaderboardId = "CgkI..._level",
    totalKillsLeaderboardId = "CgkI..._kills",
    autoSignIn = true,
    maxEntries = 20
};
```

> Tốt hơn: để tôi tạo asset config cho bạn khi có đủ 3 ID.

---

## Bước 8 — Test

### Trên Editor:
- Chế độ OFFLINE luôn hoạt động
- Bảng hiện 1 dòng thành tích của bạn
- Nút đăng nhập hiện `OFFLINE` (không bấm được)

### Trên Android thật:
1. Build APK (không phải AAB khi test)
2. Cài, mở game
3. Vào tab Leaderboard → bấm **ĐĂNG NHẬP**
4. Chọn tài khoản Google (phải là email trong **Testers** ở Bước 2)
5. Nếu thành công: bảng hiện top người chơi (có thể trống nếu chưa ai chơi)
6. Chơi 1 màn, quay về → điểm tự gửi

### Kiểm tra điểm đã gửi:
Play Console → **Play Games Services → Leaderboards → chọn bảng → View scores**

---

## Xử lý sự cố

| Lỗi | Nguyên nhân | Cách sửa |
|---|---|---|
| `Đăng nhập thất bại: NotAuthenticated` | SHA-1 chưa thêm vào Credentials | Bước 4 — thêm cả SHA-1 keystore và Play App Signing |
| `Đăng nhập thất bại: NetworkError` | Không có mạng / Google Play Games chưa cài | Kiểm tra điện thoại |
| Bảng trống sau khi đăng nhập | Chưa ai submit / leaderboard mới tạo | Tự chơi 1 màn rồi kiểm tra lại |
| Điểm không tăng | `highScoreLeaderboardId` sai | So lại ID trong Play Console |
| Nút hiện `OFFLINE` | Plugin chưa cài hoặc `webClientId` rỗng | Bước 5 + Bước 7 |
| `Duplicate class` khi build | Xung đột `play-services` | Trong `Assets/Plugins/Android`, xoá bản play-services cũ |

---

## Thông tin cần cung cấp để tôi hoàn tất

Khi có đủ, gửi cho tôi:

```
Bundle ID:                  com.xxx.galaxystrike
Web Client ID (OAuth):      123456-abc.apps.googleusercontent.com
Leaderboard HIGH SCORE:     CgkI...EAIQAQ
Leaderboard LEVEL:          CgkI...EAIQAQ
Leaderboard KILLS:          CgkI...EAIQAQ
SHA-1 (keystore):           AB:CD:...
SHA-1 (Play App Signing):   AB:CD:...
```

Tôi sẽ:
- Tạo asset `LeaderboardConfig.asset`
- Gắn vào `LeaderboardService`
- Đổi bundle ID trong `ProjectSettings`
- Build thử APK và kiểm tra

---

## Cấu trúc code hiện tại

```
Assets/Scripts/Leaderboard/
├── LeaderboardModels.cs      # LeaderboardType, LeaderboardEntry, PlayGamesConfig
├── LeaderboardService.cs     # Singleton: đăng nhập, submit, fetch (có fallback offline)
└── LeaderboardPanelUI.cs     # UI tự dựng trong Leaderboard_Panel

Đã nối vào:
- GameBootstrap          → tự sinh LeaderboardService
- GameManager.GameOver() → submit điểm khi thua
- GameManager.OnLevelCleared() → submit điểm khi thắng
- SaveManager            → lưu totalKills, bestScore (mã hoá AES)
- HomeScene > Leaderboard_Panel → LeaderboardPanelUI đã gắn sẵn
```
