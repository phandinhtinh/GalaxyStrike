using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GalaxyStrike.Data 
{
    // Định nghĩa loại tiền tệ
    public enum CurrencyType
    {
        Gold,
        Gem,
        RealMoney
    }

    // Định nghĩa phân loại vật phẩm
    public enum ItemType
    {
        Booster,
        Weapon,
        GemPack,
        CoinPack
    }

    [CreateAssetMenu(fileName = "ItemData_", menuName = "GalaxyStrike/Item Data")]
    public class ItemDataSO : ScriptableObject // <--- Vẫn kế thừa ScriptableObject của Unity
    {
        [Header("--- Basic Info ---")]
        public string itemID;
        public string itemName;
        [TextArea(2, 4)]
        public string description;
        public Sprite itemIcon;
        public ItemType itemType;

        [Header("--- Pricing ---")]
        public CurrencyType currencyType;
        public int price;

        [Header("--- Gameplay Stats ---")]
        public float duration;
        public float value;
    }
}