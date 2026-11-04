using System;
using UnityEngine;

namespace GalaxyStrike.Data
{
    [CreateAssetMenu(fileName = "PlayerData", menuName = "GalaxyStrike/Data/PlayerData")]
    [System.Obsolete("Giai đoạn 2: dữ liệu người chơi đã chuyển sang SaveManager (Assets/Scripts/Save). " +
                     "Không đọc/ghi PlayerDataSO nữa để tránh hai nguồn dữ liệu lệch nhau.")]
    public class PlayerDataSO : ScriptableObject
    {
        [Header("--- Player Profile ---")]
        public string playerName = "Ace Pilot";
        public int level = 1;

        [Header("--- Currencies & Energy ---")]
        public int currentEnergy = 30;
        public int maxEnergy = 30;
        public int coin = 1000;
        public int gem = 50;

        // Event phát tín hiệu khi có bất kỳ dữ liệu nào thay đổi
        public event Action OnDataChanged;

        public void NotifyDataChanged()
        {
            OnDataChanged?.Invoke();
        }

        public bool ConsumeEnergy(int amount)
        {
            if (currentEnergy >= amount)
            {
                currentEnergy -= amount;
                NotifyDataChanged();
                return true;
            }
            return false; // Không đủ năng lượng
        }

        public void AddCoin(int amount)
        {
            coin += amount;
            NotifyDataChanged();
        }

        public void AddGem(int amount)
        {
            gem += amount;
            NotifyDataChanged();
        }
    }
}