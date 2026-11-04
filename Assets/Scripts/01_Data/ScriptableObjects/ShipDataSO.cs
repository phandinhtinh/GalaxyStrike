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