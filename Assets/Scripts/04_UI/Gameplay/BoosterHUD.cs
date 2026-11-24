using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Thanh 4 nút Booster trong màn chơi (tự sinh nếu Scene chưa có).
///
/// Đặt cạnh mép dưới màn hình, mỗi nút hiển thị tên viết tắt + số lượt còn lại.
/// Bấm để gọi <see cref="BoosterController.Activate"/>;
/// hết lượt hoặc không đủ Coin thì nút mờ đi.
///
/// Chỉ cần gắn script này lên một GameObject bất kỳ trong GameScene.
/// </summary>
public sealed class BoosterHUD : MonoBehaviour
{
    private const float ButtonSize = 96f;
    private const float Spacing = 12f;

    private readonly Button[] buttons = new Button[4];
    private readonly TextMeshProUGUI[] labels = new TextMeshProUGUI[4];
    private readonly Image[] backgrounds = new Image[4];

    // Cache trạng thái đã hiển thị để KHÔNG ghi lại text/màu mỗi frame (tránh GC + rebuild mesh).
    private readonly int[] lastRemaining = { int.MinValue, int.MinValue, int.MinValue, int.MinValue };
    private readonly bool[] lastCanUse = new bool[4];

    private static readonly BoosterController.BoosterType[] Types =
    {
        BoosterController.BoosterType.Overdrive,
        BoosterController.BoosterType.Shield,
        BoosterController.BoosterType.EmpNuke,
        BoosterController.BoosterType.HyperMagnet
    };

    private static readonly string[] ShortNames = { "OVR", "SHD", "EMP", "MAG" };

    private static readonly Color ReadyColor = new Color(0.10f, 0.55f, 0.95f, 0.90f);
    private static readonly Color DisabledColor = new Color(0.25f, 0.28f, 0.32f, 0.55f);

    private void Start()
    {
        BuildIfMissing();
    }

    private void Update()
    {
        var controller = BoosterController.Instance;
        if (controller == null) return;

        for (int i = 0; i < Types.Length; i++)
        {
            int remaining = controller.GetRemaining(Types[i]);
            bool canUse = controller.CanUse(Types[i]);

            // Chỉ cập nhật khi giá trị thực sự đổi (đa số frame không đổi).
            if (lastCanUse[i] != canUse)
            {
                lastCanUse[i] = canUse;

                if (buttons[i] != null)
                    buttons[i].interactable = canUse;

                if (backgrounds[i] != null)
                    backgrounds[i].color = canUse ? ReadyColor : DisabledColor;
            }

            if (lastRemaining[i] != remaining)
            {
                lastRemaining[i] = remaining;

                // Chỉ cấp phát chuỗi khi giá trị đổi (không phải mỗi frame).
                if (labels[i] != null)
                    labels[i].SetText(ShortNames[i] + "\nx" + remaining.ToString(CultureInfo.InvariantCulture));
            }
        }
    }

    /// <summary>Tự dựng cụm nút nếu Scene chưa đặt sẵn (không cần author tay).</summary>
    private void BuildIfMissing()
    {
        if (buttons[0] != null) return;

        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null) return;

        RectTransform root = new GameObject("BoosterBar", typeof(RectTransform)).GetComponent<RectTransform>();
        root.SetParent(canvas.transform, false);
        root.anchorMin = new Vector2(0.5f, 0f);
        root.anchorMax = new Vector2(0.5f, 0f);
        root.pivot = new Vector2(0.5f, 0f);
        root.anchoredPosition = new Vector2(0f, 140f);

        float totalWidth = Types.Length * ButtonSize + (Types.Length - 1) * Spacing;
        root.sizeDelta = new Vector2(totalWidth, ButtonSize);

        for (int i = 0; i < Types.Length; i++)
            BuildButton(root, i, totalWidth);
    }

    private void BuildButton(RectTransform parent, int index, float totalWidth)
    {
        var go = new GameObject($"Booster_{ShortNames[index]}", typeof(RectTransform));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.sizeDelta = new Vector2(ButtonSize, ButtonSize);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);

        float leftEdge = -totalWidth * 0.5f + ButtonSize * 0.5f;
        rect.anchoredPosition = new Vector2(leftEdge + index * (ButtonSize + Spacing), 0f);

        Image bg = go.AddComponent<Image>();
        bg.color = ReadyColor;
        backgrounds[index] = bg;

        Button button = go.AddComponent<Button>();
        buttons[index] = button;

        var textGo = new GameObject("Label", typeof(RectTransform));
        RectTransform textRect = textGo.GetComponent<RectTransform>();
        textRect.SetParent(rect, false);
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        TextMeshProUGUI label = textGo.AddComponent<TextMeshProUGUI>();
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = 22f;
        label.color = Color.white;
        label.raycastTarget = false;
        label.text = ShortNames[index];
        labels[index] = label;

        BoosterController.BoosterType captured = Types[index];
        button.onClick.AddListener(() => BoosterController.Instance?.Activate(captured));

        // Tooltip/tên đầy đủ lấy từ BoosterConfig.displayName (một nguồn sự thật duy nhất).
        if (BoosterController.Instance != null)
            go.name = "Booster_" + BoosterController.Instance.GetDisplayName(captured);
    }
}
