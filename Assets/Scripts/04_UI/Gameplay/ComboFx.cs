using TMPro;
using UnityEngine;

/// <summary>
/// Bộ đếm combo: hạ quái liên tiếp (mỗi lần cách nhau không quá <see cref="Window"/> giây)
/// thì tăng combo. Từ x3 trở lên hiện "COMBO xN" bên phải màn hình, nảy mỗi khi tăng,
/// đổi màu theo bậc; hết thời gian chờ thì mờ dần. Mốc x10, x20... có thông báo riêng.
/// Chỉ là hiệu ứng hiển thị, không thay đổi điểm hay phần thưởng.
/// </summary>
public sealed class ComboFx : MonoBehaviour
{
    public const float Window = 1.6f;
    private const int ShowFrom = 3;
    private const float FadeTime = 0.35f;

    private RectTransform root;
    private CanvasGroup group;
    private TextMeshProUGUI label;
    private TextMeshProUGUI count;
    private int combo;
    private float lastKill = -10f;
    private float pop;

    public int Combo => combo;

    private void Awake()
    {
        TMP_FontAsset font = BattleFeedbackHUD.Instance != null ? BattleFeedbackHUD.Instance.Font : TMP_Settings.defaultFontAsset;

        root = new GameObject("Combo", typeof(RectTransform)).GetComponent<RectTransform>();
        root.gameObject.layer = 5;
        root.SetParent(transform, false);
        root.anchorMin = root.anchorMax = root.pivot = new Vector2(1f, 0.62f);
        root.anchoredPosition = new Vector2(-130f, 0f);
        root.sizeDelta = new Vector2(700f, 300f);
        group = root.gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = group.interactable = false;
        group.alpha = 0f;

        label = CreateText("Label", font, 76f, new Vector2(0f, 90f), new Vector2(700f, 90f));
        label.text = "COMBO";
        label.characterSpacing = 10f;
        count = CreateText("Count", font, 220f, new Vector2(0f, -45f), new Vector2(700f, 230f));
    }

    private TextMeshProUGUI CreateText(string name, TMP_FontAsset font, float size, Vector2 position, Vector2 box)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.gameObject.layer = 5;
        rect.SetParent(root, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = box;
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = font;
        text.fontSize = size;
        text.fontStyle = FontStyles.Bold | FontStyles.Italic;
        text.alignment = TextAlignmentOptions.MidlineRight;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        text.outlineWidth = 0.2f;
        text.outlineColor = new Color32(3, 12, 26, 255);
        return text;
    }

    private void OnEnable() => Enemy.OnEnemyDied += HandleEnemyDied;

    private void OnDisable()
    {
        Enemy.OnEnemyDied -= HandleEnemyDied;
        combo = 0;
        if (group != null) group.alpha = 0f;
    }

    private void HandleEnemyDied(Enemy enemy)
    {
        // OnEnemyDied cũng bắn khi quái bay khỏi màn -> chỉ tính quái bị hạ thật (hết máu).
        if (enemy == null || enemy.Health > 0)
            return;

        float now = Time.time;
        combo = now - lastKill <= Window ? combo + 1 : 1;
        lastKill = now;

        if (combo < ShowFrom)
            return;

        count.text = "x" + combo;
        Color tint = ComboColor(combo);
        count.color = tint;
        label.color = Color.Lerp(Color.white, tint, 0.4f);
        pop = 1f;

        if (combo % 10 == 0)
        {
            BattleFeedbackHUD.Instance?.ShowNotice($"{combo} KILL COMBO!", "KEEP THE CHAIN ALIVE", tint, 1.4f, 1);
            Haptics.Light();
        }
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f)
            return;

        pop = Mathf.MoveTowards(pop, 0f, dt * 5f);
        float scale = 1f + 0.35f * pop * pop;
        count.rectTransform.localScale = new Vector3(scale, scale, 1f);

        float sinceKill = Time.time - lastKill;
        if (combo >= ShowFrom && sinceKill <= Window)
            group.alpha = Mathf.MoveTowards(group.alpha, 1f, dt * 10f);
        else
        {
            group.alpha = Mathf.MoveTowards(group.alpha, 0f, dt / FadeTime);
            if (sinceKill > Window)
                combo = 0;
        }
    }

    public static Color ComboColor(int combo)
    {
        if (combo >= 20) return new Color(1f, 0.4f, 0.95f);
        if (combo >= 10) return new Color(1f, 0.55f, 0.2f);
        if (combo >= 5) return new Color(1f, 0.85f, 0.3f);
        return new Color(0.45f, 0.92f, 1f);
    }
}
