using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Màn mở đầu trận: "STAGE xx" + "READY" rồi "GO!" ở giữa màn hình.
/// <see cref="EnemySpawner"/> gọi <see cref="Play"/> và chờ đúng thời gian trả về trước khi
/// thả wave đầu tiên, để quái không xuất hiện khi chữ còn che màn hình.
/// Chạy theo Time.deltaTime nên dừng lại khi game tạm dừng.
/// </summary>
public sealed class BattleIntroFx : MonoBehaviour
{
    private const float ReadyIn = 0.35f;
    private const float ReadyHold = 0.5f;
    private const float ReadyOut = 0.15f;
    private const float GoIn = 0.3f;
    private const float GoOut = 0.4f;

    /// <summary>Thời điểm (tính từ lúc bắt đầu) đủ an toàn để thả quái: ngay khi chữ GO! bật lên.</summary>
    public const float SpawnDelay = ReadyIn + ReadyHold + ReadyOut + 0.2f;

    private static readonly Color ReadyColor = new Color(0.55f, 0.92f, 1f);
    private static readonly Color GoColor = new Color(1f, 0.82f, 0.25f);

    private static BattleIntroFx instance;

    /// <summary>Đang chạy READY / GO! (HUD hoãn thông báo wave đầu tới khi xong).</summary>
    public static bool IsPlaying => instance != null && instance.routine != null;

    private RectTransform root;
    private CanvasGroup group;
    private TextMeshProUGUI word;
    private TextMeshProUGUI subtitle;
    private Coroutine routine;

    /// <summary>Chạy hiệu ứng mở đầu; trả về số giây spawner nên chờ (0 nếu không có HUD).</summary>
    public static float Play(string subtitleText)
    {
        if (instance == null || !instance.isActiveAndEnabled)
            return 0f;

        if (instance.routine != null)
            instance.StopCoroutine(instance.routine);
        instance.routine = instance.StartCoroutine(instance.Run(subtitleText));
        return SpawnDelay;
    }

    private void Awake()
    {
        instance = this;
        Build();
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    private void Build()
    {
        TMP_FontAsset font = BattleFeedbackHUD.Instance != null ? BattleFeedbackHUD.Instance.Font : TMP_Settings.defaultFontAsset;

        root = new GameObject("BattleIntro", typeof(RectTransform)).GetComponent<RectTransform>();
        root.gameObject.layer = 5;
        root.SetParent(transform, false);
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = root.offsetMax = Vector2.zero;
        group = root.gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = group.interactable = false;
        group.alpha = 0f;

        word = CreateText("Word", font, 260f, new Vector2(0f, 120f), new Vector2(1600f, 320f));
        word.characterSpacing = 8f;
        subtitle = CreateText("Subtitle", font, 70f, new Vector2(0f, 330f), new Vector2(1400f, 110f));
        subtitle.characterSpacing = 14f;
        subtitle.color = new Color(0.75f, 0.88f, 1f);
    }

    private TextMeshProUGUI CreateText(string name, TMP_FontAsset font, float size, Vector2 position, Vector2 box)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.gameObject.layer = 5;
        rect.SetParent(root, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = box;
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = font;
        text.fontSize = size;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        text.outlineWidth = 0.18f;
        text.outlineColor = new Color32(3, 12, 26, 255);
        return text;
    }

    private IEnumerator Run(string subtitleText)
    {
        subtitle.text = subtitleText ?? string.Empty;
        group.alpha = 1f;

        // READY: phóng to rồi thu về đúng cỡ.
        word.text = "READY";
        word.color = ReadyColor;
        AudioManager.Play(SfxCue.PowerUp);
        for (float t = 0f; t < ReadyIn; t += Time.deltaTime)
        {
            float k = EaseOut(t / ReadyIn);
            SetWord(Mathf.Lerp(1.8f, 1f, k), k);
            subtitle.alpha = k;
            yield return null;
        }
        SetWord(1f, 1f);

        for (float t = 0f; t < ReadyHold; t += Time.deltaTime)
        {
            SetWord(1f + 0.03f * Mathf.Sin(t * 18f), 1f);
            yield return null;
        }

        for (float t = 0f; t < ReadyOut; t += Time.deltaTime)
        {
            float k = t / ReadyOut;
            SetWord(Mathf.Lerp(1f, 0.6f, k), 1f - k);
            yield return null;
        }

        // GO!: bật mạnh, vượt cỡ rồi ổn định.
        word.text = "GO!";
        word.color = GoColor;
        AudioManager.Play(SfxCue.Skill);
        Haptics.Light();
        for (float t = 0f; t < GoIn; t += Time.deltaTime)
        {
            float k = t / GoIn;
            float scale = k < 0.6f ? Mathf.Lerp(0.5f, 1.25f, EaseOut(k / 0.6f)) : Mathf.Lerp(1.25f, 1f, (k - 0.6f) / 0.4f);
            SetWord(scale, Mathf.Clamp01(k * 3f));
            yield return null;
        }

        for (float t = 0f; t < GoOut; t += Time.deltaTime)
        {
            float k = t / GoOut;
            SetWord(Mathf.Lerp(1f, 1.45f, k), 1f - k);
            subtitle.alpha = 1f - k;
            yield return null;
        }

        group.alpha = 0f;
        routine = null;
    }

    private void SetWord(float scale, float alpha)
    {
        word.rectTransform.localScale = new Vector3(scale, scale, 1f);
        word.alpha = alpha;
    }

    private static float EaseOut(float t) => 1f - (1f - t) * (1f - t);
}
