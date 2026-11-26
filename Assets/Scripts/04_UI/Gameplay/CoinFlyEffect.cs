using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Hiệu ứng "coin fly" khi thắng màn: các đồng xu văng ra từ ô phần thưởng trên Victory Panel,
/// rồi lần lượt bay về biểu tượng vàng của HUD; mỗi đồng chạm đích thì số Coin tăng dần,
/// icon nảy lên và phát tiếng nhặt coin.
///
/// HUD bị ẩn khi Victory Panel mở, nên hiệu ứng nhân bản khung Coin của HUD
/// (cùng vị trí, cùng giao diện) lên lớp Victory để người chơi thấy đích bay tới.
/// Chạy bằng thời gian unscaled vì lúc thắng game đang dừng (timeScale = 0).
/// </summary>
public sealed class CoinFlyEffect : MonoBehaviour
{
    [Header("Số lượng")]
    [SerializeField, Min(1)] private int minCoins = 6;
    [SerializeField, Min(1)] private int maxCoins = 14;
    [Tooltip("Mỗi bao nhiêu Coin thưởng thì thêm 1 đồng bay (trong khoảng min..max)")]
    [SerializeField, Min(1)] private int coinsPerSprite = 10;

    [Header("Thời gian (giây, unscaled)")]
    [SerializeField] private float startDelay = 0.45f;
    [SerializeField] private float burstDuration = 0.35f;
    [SerializeField] private float holdDuration = 0.12f;
    [SerializeField] private float flyDuration = 0.6f;
    [SerializeField] private float stagger = 0.06f;

    [Header("Hình dạng")]
    [SerializeField] private float coinSize = 76f;
    [SerializeField] private Vector2 burstRadius = new Vector2(170f, 330f);
    [Tooltip("Độ cong của đường bay (tỉ lệ theo khoảng cách)")]
    [SerializeField] private float arcHeight = 0.35f;

    private readonly List<GameObject> spawned = new List<GameObject>();
    private Coroutine routine;
    private RectTransform counterClone;
    private TMP_Text counterText;
    private RectTransform counterIcon;
    private int displayedTotal;

    /// <summary>
    /// Bắt đầu hiệu ứng. <paramref name="hudCoinBar"/> là khung Coin gốc của HUD (có thể đang ẩn),
    /// <paramref name="source"/> là nơi coin văng ra, <paramref name="layer"/> là lớp UI phủ toàn màn
    /// (cùng Canvas với HUD) để đặt coin và khung Coin nhân bản.
    /// </summary>
    public void Play(RectTransform hudCoinBar, RectTransform source, RectTransform layer, int totalAfterReward, int reward)
    {
        Stop();
        if (hudCoinBar == null || source == null || layer == null || reward <= 0)
            return;

        routine = StartCoroutine(PlayRoutine(hudCoinBar, source, layer, totalAfterReward, reward));
    }

    /// <summary>Dừng và dọn sạch các object tạm (gọi khi panel đóng / đổi Scene).</summary>
    public void Stop()
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }

        foreach (GameObject go in spawned)
        {
            if (go != null)
                Destroy(go);
        }
        spawned.Clear();
        counterClone = null;
        counterText = null;
        counterIcon = null;
    }

    private void OnDisable()
    {
        Stop();
    }

    private IEnumerator PlayRoutine(RectTransform hudCoinBar, RectTransform source, RectTransform layer, int total, int reward)
    {
        displayedTotal = Mathf.Max(0, total - reward);
        CreateCounterClone(hudCoinBar, layer);
        if (counterIcon == null)
        {
            routine = null;
            yield break;
        }

        Sprite coinSprite = counterIcon.GetComponent<Image>()?.sprite;
        int count = Mathf.Clamp(reward / coinsPerSprite, minCoins, maxCoins);
        float scale = layer.lossyScale.x;

        yield return WaitUnscaled(startDelay);

        // ---- Tạo coin tại ô phần thưởng ----
        var coins = new RectTransform[count];
        var burstTargets = new Vector3[count];
        Vector3 origin = source.position;
        for (int i = 0; i < count; i++)
        {
            coins[i] = CreateCoin(coinSprite, layer, origin);
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float radius = Random.Range(burstRadius.x, burstRadius.y) * scale;
            burstTargets[i] = origin + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius;
        }

        // ---- Pha 1: văng ra (ease-out + nảy kích thước) ----
        for (float t = 0f; t < burstDuration; t += Time.unscaledDeltaTime)
        {
            float k = EaseOutBack(Mathf.Clamp01(t / burstDuration));
            for (int i = 0; i < count; i++)
            {
                coins[i].position = Vector3.LerpUnclamped(origin, burstTargets[i], k);
                coins[i].localScale = Vector3.one * Mathf.Lerp(0.2f, 1f, Mathf.Clamp01(k));
                coins[i].localRotation = Quaternion.Euler(0f, 0f, k * 180f * (i % 2 == 0 ? 1f : -1f));
            }
            yield return null;
        }

        yield return WaitUnscaled(holdDuration);

        // ---- Pha 2: lần lượt bay về icon HUD theo đường cong ----
        int share = reward / count;
        int remainder = reward - share * count;
        float totalTime = flyDuration + stagger * (count - 1);
        var arrived = new bool[count];
        for (float t = 0f; ; t += Time.unscaledDeltaTime)
        {
            Vector3 target = counterIcon.position;
            bool allArrived = true;
            for (int i = 0; i < count; i++)
            {
                if (arrived[i])
                    continue;

                float local = Mathf.Clamp01((t - i * stagger) / flyDuration);
                if (local >= 1f)
                {
                    arrived[i] = true;
                    coins[i].gameObject.SetActive(false);
                    OnCoinArrived(share + (i == count - 1 ? remainder : 0));
                    continue;
                }

                allArrived = false;
                float k = EaseInCubic(local);
                Vector3 start = burstTargets[i];
                Vector3 control = (start + target) * 0.5f +
                                  Vector3.up * Vector3.Distance(start, target) * arcHeight;
                coins[i].position = Bezier(start, control, target, k);
                coins[i].localScale = Vector3.one * Mathf.Lerp(1f, 0.55f, k);
            }

            if (allArrived || t > totalTime + 1f)
                break;
            yield return null;
        }

        // Chốt số cuối cho chắc chắn khớp ví thật.
        displayedTotal = total;
        SetCounter(displayedTotal);
        routine = null;
    }

    private void OnCoinArrived(int amount)
    {
        displayedTotal += amount;
        SetCounter(displayedTotal);
        StartCoroutine(Punch(counterIcon));

        if (AudioManager.HasInstance && AudioManager.Instance.coinCollectSFX != null)
            AudioManager.Instance.PlaySFX(AudioManager.Instance.coinCollectSFX, 0.45f);
        Haptics.Light();
    }

    private void CreateCounterClone(RectTransform hudCoinBar, RectTransform layer)
    {
        GameObject clone = Instantiate(hudCoinBar.gameObject, layer, false);
        clone.name = "CoinFly_Counter";
        clone.SetActive(true);
        spawned.Add(clone);

        counterClone = (RectTransform)clone.transform;
        counterClone.SetAsLastSibling();

        foreach (Graphic g in clone.GetComponentsInChildren<Graphic>(true))
            g.raycastTarget = false;

        // Ưu tiên con tên "CoinText" / "Coin" như layout HUD; nếu không có thì lấy theo kiểu.
        counterText = FindChild<TMP_Text>(counterClone, "CoinText") ?? clone.GetComponentInChildren<TMP_Text>(true);
        Image icon = FindChild<Image>(counterClone, "Coin");
        if (icon == null)
        {
            foreach (Image img in clone.GetComponentsInChildren<Image>(true))
            {
                if (img.transform != counterClone && img.sprite != null)
                {
                    icon = img;
                    break;
                }
            }
        }
        counterIcon = icon != null ? icon.rectTransform : counterClone;
        SetCounter(displayedTotal);
    }

    private RectTransform CreateCoin(Sprite sprite, RectTransform layer, Vector3 worldPosition)
    {
        var go = new GameObject("CoinFly_Coin", typeof(RectTransform), typeof(Image));
        spawned.Add(go);
        var rt = (RectTransform)go.transform;
        rt.SetParent(layer, false);
        rt.SetAsLastSibling();
        rt.sizeDelta = new Vector2(coinSize, coinSize);
        rt.position = worldPosition;
        rt.localScale = Vector3.one * 0.2f;

        var image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
        return rt;
    }

    private void SetCounter(int value)
    {
        if (counterText != null)
            counterText.text = value.ToString("N0", CultureInfo.InvariantCulture);
    }

    private static IEnumerator Punch(RectTransform target)
    {
        if (target == null)
            yield break;

        const float duration = 0.16f;
        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            if (target == null)
                yield break;
            float s = 1f + 0.28f * Mathf.Sin(Mathf.Clamp01(t / duration) * Mathf.PI);
            target.localScale = Vector3.one * s;
            yield return null;
        }

        if (target != null)
            target.localScale = Vector3.one;
    }

    private static T FindChild<T>(Transform root, string name) where T : Component
    {
        foreach (T c in root.GetComponentsInChildren<T>(true))
        {
            if (c.name == name)
                return c;
        }
        return null;
    }

    private static IEnumerator WaitUnscaled(float seconds)
    {
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            yield return null;
    }

    private static Vector3 Bezier(Vector3 a, Vector3 b, Vector3 c, float t)
    {
        float u = 1f - t;
        return u * u * a + 2f * u * t * b + t * t * c;
    }

    private static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }

    private static float EaseInCubic(float t) => t * t * t;
}
