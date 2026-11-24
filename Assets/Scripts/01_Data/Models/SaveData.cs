using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace GalaxyStrike.Save
{
    /// <summary>
    /// Toàn bộ dữ liệu người chơi được lưu xuống máy.
    /// Là class thuần (không kế thừa MonoBehaviour) để JsonUtility serialize được.
    /// </summary>
    [Serializable]
    public sealed class SaveData
    {
        public int version = 1;
        public string playerName = "Ace Pilot";
        public int accountLevel = 1;

        [Tooltip("Tổng XP đã tích luỹ (dùng để tính accountLevel)")]
        public int experience;
        public int battlePassClaimedTiers;

        public int coins = 500;
        public bool welcomeGiftClaimed;
        public int dailyLoginClaimDay;
        public int gems = 20;
        public int energy = 30;
        public int maxEnergy = 30;

        public int highestLevel;
        // Zero identifies a save created before Campaign and Challenge used separate progress.
        public int campaignStage;

        [Tooltip("Tổng số quái đã hạ (dùng cho bảng xếp hạng)")]
        public int totalKills;

        [Tooltip("Điểm cao nhất trong một màn (dùng cho bảng xếp hạng)")]
        public int bestScore;

        public int selectedShipIndex;
        public List<int> unlockedShipIndices = new List<int> { 0 };

        [Tooltip("ID các vật phẩm (ItemDataSO.itemID) đã sở hữu")]
        public List<string> ownedItemIds = new List<string>();

        [Tooltip("Transaction ID các giao dịch IAP đã cộng thưởng (chống cộng 2 lần khi Store gửi lại đơn)")]
        public List<string> processedIapTransactions = new List<string>();

        // --- NHIỆM VỤ (TaskCatalog) ---
        // Mảng thống kê đánh chỉ số theo GalaxyStrike.Data.TaskMetric.

        [Tooltip("Ngày UTC (yyyyMMdd) của bộ đếm nhiệm vụ ngày hiện tại")]
        public int taskDay;
        [Tooltip("Ngày UTC (yyyyMMdd) của thứ Hai mở đầu tuần nhiệm vụ hiện tại")]
        public int taskWeek;
        [Tooltip("Ngày UTC (yyyyMMdd) gần nhất đã tính một lượt đăng nhập")]
        public int lastLoginDay;

        public int[] dailyTaskStats = new int[0];
        public int[] weeklyTaskStats = new int[0];
        public int[] lifetimeTaskStats = new int[0];

        public List<string> claimedDailyTasks = new List<string>();
        public List<string> claimedWeeklyTasks = new List<string>();
        public List<string> claimedAchievements = new List<string>();

        public bool HasItem(string itemId)
        {
            return !string.IsNullOrEmpty(itemId) &&
                   ownedItemIds != null &&
                   ownedItemIds.Contains(itemId);
        }

        public void UnlockItem(string itemId)
        {
            if (string.IsNullOrEmpty(itemId))
                return;

            if (ownedItemIds == null)
                ownedItemIds = new List<string>();

            if (!ownedItemIds.Contains(itemId))
                ownedItemIds.Add(itemId);
        }

        public int maxHealthUpgrade;
        public int fireRateUpgrade;
        public int moveSpeedUpgrade;
        public int damageUpgrade;

        /// <summary>Thời điểm (Unix giây UTC) bắt đầu tính nhịp hồi năng lượng.</summary>
        public long lastEnergyRefillUnixTime;

        public bool HasShip(int index)
        {
            return unlockedShipIndices != null && unlockedShipIndices.Contains(index);
        }

        public void UnlockShip(int index)
        {
            if (unlockedShipIndices == null)
                unlockedShipIndices = new List<int>();

            if (!unlockedShipIndices.Contains(index))
                unlockedShipIndices.Add(index);
        }

        public int GetUpgradeLevel(ShipUpgradeType type)
        {
            switch (type)
            {
                case ShipUpgradeType.MaxHealth: return maxHealthUpgrade;
                case ShipUpgradeType.FireRate: return fireRateUpgrade;
                case ShipUpgradeType.MoveSpeed: return moveSpeedUpgrade;
                case ShipUpgradeType.Damage: return damageUpgrade;
                default: return 0;
            }
        }

        public void SetUpgradeLevel(ShipUpgradeType type, int level)
        {
            switch (type)
            {
                case ShipUpgradeType.MaxHealth: maxHealthUpgrade = level; break;
                case ShipUpgradeType.FireRate: fireRateUpgrade = level; break;
                case ShipUpgradeType.MoveSpeed: moveSpeedUpgrade = level; break;
                case ShipUpgradeType.Damage: damageUpgrade = level; break;
            }
        }
    }

    /// <summary>
    /// Bốn chỉ số phi thuyền có thể nâng cấp vĩnh viễn bằng Coin.
    /// </summary>
    public enum ShipUpgradeType
    {
        MaxHealth = 0,
        FireRate = 1,
        MoveSpeed = 2,
        Damage = 3
    }

    /// <summary>
    /// Mã hóa dữ liệu lưu trữ: AES-256 + chữ ký HMAC-SHA256 để chặn sửa file save.
    /// Khóa được tách theo mảnh để không nằm sẵn thành một chuỗi trong bản build.
    /// </summary>
    internal static class SaveCrypto
    {
        private static readonly string[] KeyParts =
        {
            "Gx7Q", "mT2p", "Vb9L", "nR4s", "Hc6K", "wY1z", "Pf8D", "aJ3u"
        };

        private const string HmacTag = "GS2";

        /// <summary>AES-256-CBC + PBKDF2, trả về chuỗi Base64 kèm IV.</summary>
        public static string Encrypt(string plainText)
        {
            byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
            using (var aes = Aes.Create())
            {
                aes.KeySize = 256;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                aes.GenerateIV();

                using (var derive = new Rfc2898DeriveBytes(BuildSecret(), aes.IV, 1000))
                    aes.Key = derive.GetBytes(32);

                using (ICryptoTransform encryptor = aes.CreateEncryptor())
                {
                    byte[] cipher = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
                    byte[] payload = new byte[aes.IV.Length + cipher.Length];
                    Buffer.BlockCopy(aes.IV, 0, payload, 0, aes.IV.Length);
                    Buffer.BlockCopy(cipher, 0, payload, aes.IV.Length, cipher.Length);

                    string body = Convert.ToBase64String(payload);
                    return $"{HmacTag}.{body}.{Sign(body)}";
                }
            }
        }

        /// <summary>Giải mã và kiểm tra chữ ký. Trả false nếu dữ liệu bị sửa hoặc hỏng.</summary>
        public static bool TryDecrypt(string stored, out string plainText)
        {
            plainText = null;

            if (string.IsNullOrEmpty(stored))
                return false;

            string[] segments = stored.Split('.');
            if (segments.Length != 3 || segments[0] != HmacTag)
                return false;

            string body = segments[1];
            if (!FixedTimeEquals(segments[2], Sign(body)))
                return false;

            try
            {
                byte[] payload = Convert.FromBase64String(body);
                if (payload.Length <= 16)
                    return false;

                byte[] iv = new byte[16];
                Buffer.BlockCopy(payload, 0, iv, 0, 16);

                using (var aes = Aes.Create())
                {
                    aes.KeySize = 256;
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;
                    aes.IV = iv;

                    using (var derive = new Rfc2898DeriveBytes(BuildSecret(), iv, 1000))
                        aes.Key = derive.GetBytes(32);

                    using (ICryptoTransform decryptor = aes.CreateDecryptor())
                    {
                        byte[] cipher = new byte[payload.Length - 16];
                        Buffer.BlockCopy(payload, 16, cipher, 0, cipher.Length);

                        byte[] plain = decryptor.TransformFinalBlock(cipher, 0, cipher.Length);
                        plainText = Encoding.UTF8.GetString(plain);
                        return true;
                    }
                }
            }
            catch
            {
                plainText = null;
                return false;
            }
        }

        private static string BuildSecret()
        {
            var builder = new StringBuilder(48);
            for (int i = 0; i < KeyParts.Length; i++)
                builder.Append(KeyParts[i]);

            builder.Append(Application.identifier);
            return builder.ToString();
        }

        private static string Sign(string body)
        {
            using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(BuildSecret())))
            {
                byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(body));
                return Convert.ToBase64String(hash);
            }
        }

        /// <summary>So sánh không rò rỉ thời gian (chống timing attack).</summary>
        private static bool FixedTimeEquals(string a, string b)
        {
            if (a == null || b == null || a.Length != b.Length)
                return false;

            int diff = 0;
            for (int i = 0; i < a.Length; i++)
                diff |= a[i] ^ b[i];

            return diff == 0;
        }
    }
}
