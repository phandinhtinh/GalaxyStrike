using UnityEngine;

namespace GalaxyStrike.Save
{
    /// <summary>
    /// Bảng giá + giá trị của 4 chỉ số nâng cấp vĩnh viễn.
    /// Tập trung một chỗ để Designer đổi cân bằng mà không phải sửa UI hay gameplay.
    /// </summary>
    public static class ShipUpgradeCatalog
    {
        /// <summary>Cấp tối đa cho mỗi chỉ số.</summary>
        public const int MaxLevel = 10;

        /// <summary>Giá Coin của bậc nâng cấp kế tiếp (tăng dần theo cấp hiện tại).</summary>
        public static int GetCost(ShipUpgradeType type, int currentLevel)
        {
            int baseCost = GetBaseCost(type);
            int level = Mathf.Clamp(currentLevel, 0, MaxLevel);
            return Mathf.RoundToInt(baseCost * Mathf.Pow(1.55f, level));
        }

        private static int GetBaseCost(ShipUpgradeType type)
        {
            switch (type)
            {
                case ShipUpgradeType.MaxHealth: return 120;
                case ShipUpgradeType.FireRate: return 200;
                case ShipUpgradeType.MoveSpeed: return 100;
                case ShipUpgradeType.Damage: return 250;
                default: return 150;
            }
        }

        public static string GetDisplayName(ShipUpgradeType type)
        {
            switch (type)
            {
                case ShipUpgradeType.MaxHealth: return "GIÁP TÀU";
                case ShipUpgradeType.FireRate: return "TỐC ĐỘ BẮN";
                case ShipUpgradeType.MoveSpeed: return "TỐC ĐỘ BAY";
                case ShipUpgradeType.Damage: return "SÁT THƯƠNG";
                default: return type.ToString();
            }
        }

        /// <summary>Mô tả giá trị ở một cấp cụ thể để hiển thị "+1 máu (3 → 4)".</summary>
        public static string Describe(ShipUpgradeType type, int level, int shipBaseHealth, float shipBaseFireRate, float shipBaseMoveSpeed)
        {
            switch (type)
            {
                case ShipUpgradeType.MaxHealth:
                    return $"Giáp: {shipBaseHealth + GetHealthBonus(level)} (+{GetHealthBonus(level)})";
                case ShipUpgradeType.FireRate:
                    return $"Nhịp bắn: {ApplyFireRate(shipBaseFireRate, level):0.00}s (-{GetFireRateBonusPercent(level)}%)";
                case ShipUpgradeType.MoveSpeed:
                    return $"Tốc độ: {ApplyMoveSpeed(shipBaseMoveSpeed, level):0.0} (+{GetMoveSpeedBonusPercent(level)}%)";
                case ShipUpgradeType.Damage:
                    return $"Sát thương: x{ApplyDamage(1, level)}";
                default:
                    return string.Empty;
            }
        }

        // ----- Công thức áp dụng. Gameplay gọi duy nhất các hàm Apply* này. -----

        public static int GetHealthBonus(int level)
        {
            return Mathf.Clamp(level, 0, MaxLevel);
        }

        public static int ApplyHealth(int baseHealth, int level)
        {
            return Mathf.Max(1, baseHealth + GetHealthBonus(level));
        }

        public static int GetFireRateBonusPercent(int level)
        {
            // Mỗi bậc giảm 4% thời gian hồi, tối đa 40%.
            return Mathf.Clamp(level, 0, MaxLevel) * 4;
        }

        public static float ApplyFireRate(float baseFireRate, int level)
        {
            float multiplier = 1f - GetFireRateBonusPercent(level) / 100f;
            return Mathf.Max(0.05f, baseFireRate * multiplier);
        }

        public static int GetMoveSpeedBonusPercent(int level)
        {
            // Mỗi bậc +3% tốc độ, tối đa +30%.
            return Mathf.Clamp(level, 0, MaxLevel) * 3;
        }

        public static float ApplyMoveSpeed(float baseMoveSpeed, int level)
        {
            return baseMoveSpeed * (1f + GetMoveSpeedBonusPercent(level) / 100f);
        }

        public static int ApplyDamage(int baseDamage, int level)
        {
            // Sát thương tăng tuyến tính: mỗi bậc +1.
            return Mathf.Max(1, baseDamage + Mathf.Clamp(level, 0, MaxLevel));
        }
    }
}
