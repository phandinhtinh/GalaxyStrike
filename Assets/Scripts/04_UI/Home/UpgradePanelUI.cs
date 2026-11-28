using System.Collections.Generic;
using GalaxyStrike.Data;
using GalaxyStrike.Save;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Refreshes the scene-authored upgrade dashboard and applies purchased upgrades.</summary>
public class UpgradePanelUI : MonoBehaviour
{
    [Header("--- Data ---")]
    [Tooltip("ShipDataSO list used to show base stats for the selected ship")]
    [SerializeField] private ShipDataSO[] allShipsData;

    private static readonly ShipUpgradeType[] UpgradeOrder =
    {
        ShipUpgradeType.Damage,
        ShipUpgradeType.FireRate,
        ShipUpgradeType.MaxHealth,
        ShipUpgradeType.MoveSpeed
    };

    private readonly Dictionary<ShipUpgradeType, UpgradeRow> rows = new Dictionary<ShipUpgradeType, UpgradeRow>();
    private readonly Dictionary<ShipUpgradeType, StatCard> statCards = new Dictionary<ShipUpgradeType, StatCard>();

    private sealed class UpgradeRow
    {
        public TextMeshProUGUI currentLevel;
        public TextMeshProUGUI nextLevel;
        public TextMeshProUGUI currentValue;
        public TextMeshProUGUI nextValue;
        public TextMeshProUGUI cost;
        public Button button;
    }

    private sealed class StatCard
    {
        public TextMeshProUGUI level;
        public TextMeshProUGUI value;
    }

    private void OnEnable()
    {
        BindLayout();
        SaveManager save = SaveManager.Instance;
        if (save != null)
            save.OnDataChanged += Refresh;

        Refresh();
    }

    private void OnDisable()
    {
        SaveManager save = FindFirstObjectByType<SaveManager>();
        if (save != null)
            save.OnDataChanged -= Refresh;
    }

    private void BindLayout()
    {
        rows.Clear();
        statCards.Clear();

        for (int i = 0; i < UpgradeOrder.Length; i++)
        {
            ShipUpgradeType type = UpgradeOrder[i];
            string id = GetId(type);

            Transform rowTransform = transform.Find("Upgrade_Details_Panel/Upgrade_Rows/UpgradeRow_" + type);
            if (rowTransform != null)
            {
                UpgradeRow row = new UpgradeRow
                {
                    currentLevel = FindText(rowTransform, "CurrentLevel"),
                    nextLevel = FindText(rowTransform, "NextLevel"),
                    currentValue = FindText(rowTransform, "CurrentValue"),
                    nextValue = FindText(rowTransform, "NextValue"),
                    cost = FindText(rowTransform, "UpgradeButton/Cost"),
                    button = FindButton(rowTransform, "UpgradeButton")
                };

                if (row.button != null)
                {
                    ShipUpgradeType capturedType = type;
                    row.button.onClick.RemoveAllListeners();
                    row.button.onClick.AddListener(() => OnUpgradeButtonClicked(capturedType));
                }

                rows[type] = row;
            }

            Transform cardTransform = transform.Find("Stat_" + id);
            if (cardTransform != null)
            {
                statCards[type] = new StatCard
                {
                    level = FindText(cardTransform, "Level"),
                    value = FindText(cardTransform, "Value")
                };
            }
        }
    }

    private static TextMeshProUGUI FindText(Transform parent, string path)
    {
        Transform child = parent.Find(path);
        return child != null ? child.GetComponent<TextMeshProUGUI>() : null;
    }

    private static Button FindButton(Transform parent, string path)
    {
        Transform child = parent.Find(path);
        return child != null ? child.GetComponent<Button>() : null;
    }

    /// <summary>Called by the scene buttons after binding (also safe for direct UnityEvent use).</summary>
    public void OnUpgradeButtonClicked(ShipUpgradeType type)
    {
        SaveManager save = SaveManager.Instance;
        if (save == null)
            return;

        if (!save.TryUpgrade(type))
        {
            if (save.IsUpgradeMaxed(type))
                GalaxyStrike.UI.Toast.Show($"{UpgradeTitle(type)} is already at max level.", GalaxyStrike.UI.ToastKind.Info);
            else
                GalaxyStrike.UI.Toast.Show(
                    $"Not enough coins! Need {save.GetUpgradeCost(type):N0}, you have {save.Coins:N0}.",
                    GalaxyStrike.UI.ToastKind.Warning);
            Refresh();
            return;
        }

        if (AudioManager.Instance != null && AudioManager.Instance.buttonClickSFX != null)
            AudioManager.Instance.PlaySFX(AudioManager.Instance.buttonClickSFX);
        AudioManager.Play(SfxCue.Reward);
        GalaxyStrike.UI.Toast.Show($"{UpgradeTitle(type)} upgraded to Lv.{save.GetUpgradeLevel(type)}!",
            GalaxyStrike.UI.ToastKind.Success);

        Refresh();
    }

    private static string UpgradeTitle(ShipUpgradeType type)
    {
        switch (type)
        {
            case ShipUpgradeType.Damage: return "Firepower";
            case ShipUpgradeType.FireRate: return "Fire Rate";
            case ShipUpgradeType.MaxHealth: return "Max HP";
            case ShipUpgradeType.MoveSpeed: return "Move Speed";
            default: return type.ToString();
        }
    }

    /// <summary>Updates values and costs from the existing save data/catalog.</summary>
    public void Refresh()
    {
        SaveManager save = SaveManager.Instance;
        if (save == null)
            return;

        ShipDataSO ship = GetSelectedShip(save);
        int baseHealth = ship != null ? ship.maxHealth : 3;
        float baseFireRate = ship != null ? ship.fireRate : 0.5f;
        float baseMoveSpeed = ship != null ? ship.moveSpeed : 8f;

        for (int i = 0; i < UpgradeOrder.Length; i++)
        {
            ShipUpgradeType type = UpgradeOrder[i];
            int level = save.GetUpgradeLevel(type);
            bool maxed = save.IsUpgradeMaxed(type);

            if (rows.TryGetValue(type, out UpgradeRow row))
            {
                if (row.currentLevel != null)
                    row.currentLevel.text = "Lv." + level;
                if (row.nextLevel != null)
                    row.nextLevel.text = maxed ? "MAX" : "Lv." + (level + 1);
                if (row.currentValue != null)
                    row.currentValue.text = FormatValue(type, level, baseHealth, baseFireRate, baseMoveSpeed);
                if (row.nextValue != null)
                    row.nextValue.text = maxed
                        ? "MAX"
                        : FormatValue(type, level + 1, baseHealth, baseFireRate, baseMoveSpeed);

                int cost = maxed ? 0 : save.GetUpgradeCost(type);
                if (row.cost != null)
                    row.cost.text = maxed ? "MAX" : cost.ToString("N0");
                if (row.button != null)
                    row.button.interactable = !maxed && save.Coins >= cost;
            }

            if (statCards.TryGetValue(type, out StatCard card))
            {
                if (card.level != null)
                    card.level.text = "Lv." + level;
                if (card.value != null)
                    card.value.text = FormatBonus(type, level, baseHealth, baseFireRate, baseMoveSpeed);
            }
        }
    }

    private ShipDataSO GetSelectedShip(SaveManager save)
    {
        if (allShipsData == null || allShipsData.Length == 0)
            return null;

        int index = save.SelectedShipIndex;
        return index >= 0 && index < allShipsData.Length ? allShipsData[index] : null;
    }

    private static string FormatValue(ShipUpgradeType type, int level,
        int baseHealth, float baseFireRate, float baseMoveSpeed)
    {
        switch (type)
        {
            case ShipUpgradeType.Damage:
                return "x" + ShipUpgradeCatalog.ApplyDamage(1, level);
            case ShipUpgradeType.FireRate:
                return ShipUpgradeCatalog.ApplyFireRate(baseFireRate, level).ToString("0.00") + "s";
            case ShipUpgradeType.MaxHealth:
                return ShipUpgradeCatalog.ApplyHealth(baseHealth, level) + " HP";
            default:
                return ShipUpgradeCatalog.ApplyMoveSpeed(baseMoveSpeed, level).ToString("0.0");
        }
    }

    private static string FormatBonus(ShipUpgradeType type, int level,
        int baseHealth, float baseFireRate, float baseMoveSpeed)
    {
        switch (type)
        {
            case ShipUpgradeType.Damage:
                return "+" + level + " Damage";
            case ShipUpgradeType.FireRate:
                return "-" + ShipUpgradeCatalog.GetFireRateBonusPercent(level) + "% delay";
            case ShipUpgradeType.MaxHealth:
                return "+" + ShipUpgradeCatalog.GetHealthBonus(level) + " HP";
            default:
                return "+" + ShipUpgradeCatalog.GetMoveSpeedBonusPercent(level) + "% speed";
        }
    }

    private static string GetId(ShipUpgradeType type)
    {
        switch (type)
        {
            case ShipUpgradeType.Damage: return "Damage";
            case ShipUpgradeType.FireRate: return "FireRate";
            case ShipUpgradeType.MaxHealth: return "MaxHealth";
            default: return "MoveSpeed";
        }
    }
}
