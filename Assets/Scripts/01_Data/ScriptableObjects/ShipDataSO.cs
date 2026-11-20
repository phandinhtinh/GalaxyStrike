using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GalaxyStrike.Data
{
    [CreateAssetMenu(fileName = "ShipData_", menuName = "GalaxyStrike/Ship Data")]
    public class ShipDataSO : ScriptableObject
    {
        [Header("--- Visual & Info ---")]
        public string shipID;          // ID định danh (vd: ship_vanguard)
        public string shipName = "Ship";
        public Sprite shipSprite;

        [System.Serializable]
        public class ExhaustNozzle
        {
            [Tooltip("Vị trí trên sprite: (0,0) góc dưới trái, (1,1) góc trên phải.")]
            public Vector2 position = new Vector2(0.5f, 0.08f);
            [Range(0.25f, 1.5f)] public float strength = 1f;
        }

        [Header("--- Engine Exhaust ---")]
        public Color exhaustColor = new Color(0.2f, 0.8f, 1f);
        public Color exhaustTailColor = new Color(0.08f, 0.3f, 1f);
        [Range(0.04f, 0.3f)] public float exhaustWidth = 0.16f;
        [Range(0.1f, 0.5f)] public float exhaustLifetime = 0.24f;
        [Range(1f, 5f)] public float exhaustSpeed = 3.2f;
        [Range(0f, 12f)] public float exhaustSpread = 2f;
        [Tooltip("Tổng số hạt vệt sáng mỗi giây, chia giữa các cửa phụt.")]
        [Range(0f, 40f)] public float exhaustWakeRate = 16f;
        public ExhaustNozzle[] exhaustNozzles = { new ExhaustNozzle() };

        [Header("--- Pricing ---")]
        public CurrencyType currencyType = CurrencyType.Gold; // Loại tiền
        public int price = 1000;       // Giá mua tàu

        [Header("--- Stats ---")]
        public int maxHealth = 3;
        public float moveSpeed = 8f;
        public float fireRate = 0.5f;

        [Header("--- Combat ---")]
        public GameObject bulletPrefab;
    }
}