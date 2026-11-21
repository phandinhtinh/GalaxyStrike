using TMPro;
using UnityEngine;

/// <summary>Presentation only: live hull/firepower, boss health, notices and pooled pickup labels.</summary>
public sealed class BattleFeedbackHUD : MonoBehaviour
{
    public static BattleFeedbackHUD Instance { get; private set; }
    private static readonly Color Cyan = new Color(0.12f, 0.88f, 1f);
    private static readonly Color Gold = new Color(1f, 0.8f, 0.26f);
    private static readonly Color Danger = new Color(1f, 0.3f, 0.32f);
    private static readonly Color Muted = new Color(0.35f, 0.52f, 0.65f);
    private static readonly Color Panel = new Color(0.025f, 0.065f, 0.13f, 0.92f);

    [SerializeField] private TextMeshProUGUI hullText, powerText, toastTitle, toastDetail, bossLabel;
    [SerializeField] private UnityEngine.UI.Image hullAccent, toastAccent, bossFill;
    [SerializeField] private UnityEngine.UI.Image[] powerCells;
    [SerializeField] private CanvasGroup toastGroup, edgeGroup;
    [SerializeField] private RectTransform toastRect, bossRoot;
    [SerializeField] private TextMeshProUGUI[] pickupLabels;

    private readonly float[] pickupAges = new float[8];
    private readonly Vector2[] pickupOrigins = new Vector2[8];
    private EnemySpawner spawner;
    private Enemy boss;
    private int lastHealth = -1, lastMaxHealth, lastPower = -1, lastMaxPower, lastWave;
    private int nextLabel, coinTotal, coinLabel = -1, noticePriority;
    private float toastAge = 10f, toastDuration = 2f, damageFlash, bossScanAt;
    private bool critical;

    private void Awake()
    {
        Instance = this;
        if (hullText == null) Build(TMP_Settings.defaultFontAsset);
        toastGroup.alpha = edgeGroup.alpha = 0f;
        bossRoot.gameObject.SetActive(false);
        for (int i = 0; i < pickupLabels.Length; i++) pickupLabels[i].gameObject.SetActive(false);
    }

    private void Start() => spawner = FindFirstObjectByType<EnemySpawner>();

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        PlayerController player = PlayerController.Instance;
        bool playing = player != null && !player.IsDowned &&
            (GameManager.Instance == null || !GameManager.Instance.IsRoundOver);
        if (playing) RefreshPlayer(player);
        else critical = false;

        // Gameplay notices and pooled labels freeze with the game; menu button feedback uses unscaled time.
        float dt = Time.deltaTime;
        damageFlash = Mathf.MoveTowards(damageFlash, 0f, dt * 3f);
        edgeGroup.alpha = playing ? Mathf.Max(damageFlash,
            critical ? 0.28f + 0.16f * Mathf.Sin(Time.time * 5f) : 0f) : 0f;
        if (playing && spawner != null && spawner.CurrentWaveNumber > 0 && lastWave != spawner.CurrentWaveNumber)
        {
            lastWave = spawner.CurrentWaveNumber;
            ShowNotice(lastWave == 1 ? "SYSTEMS ONLINE" : "WAVE " + lastWave.ToString("00"),
                lastWave == 1 ? "AUTO FIRE ACTIVE  /  DRAG TO MOVE" : "NEW HOSTILES INBOUND", Cyan, 1.8f, 1);
        }
        AnimateNotice(dt);
        AnimatePickups(dt);
        RefreshBoss(playing);
    }

    private void RefreshPlayer(PlayerController player)
    {
        int health = Mathf.Max(0, player.CurrentHealth);
        int maximum = Mathf.Max(1, player.MaxHealth);
        critical = health > 0 && (health <= maximum / 3f || (maximum > 1 && health == 1));
        if (health != lastHealth || maximum != lastMaxHealth)
        {
            if (lastHealth >= 0 && health < lastHealth) damageFlash = 0.45f;
            hullText.text = (critical ? "HULL CRITICAL  " : "HULL  ") + health + " / " + maximum;
            hullText.color = hullAccent.color = critical ? Danger : Cyan;
            lastHealth = health;
            lastMaxHealth = maximum;
        }
        if (lastPower == player.CurrentBulletLevel && lastMaxPower == player.MaxBulletLevel) return;
        lastPower = player.CurrentBulletLevel;
        lastMaxPower = player.MaxBulletLevel;
        if (powerCells.Length != Mathf.Clamp(lastMaxPower, 1, 12))
        {
            Transform parent = powerCells[0].transform.parent;
            foreach (var cell in powerCells) Destroy(cell.gameObject);
            BuildPowerCells(parent, lastMaxPower);
        }
        powerText.text = "FIREPOWER  " + lastPower + " / " + lastMaxPower;
        for (int i = 0; i < powerCells.Length; i++)
            powerCells[i].color = i < Mathf.CeilToInt(lastPower * powerCells.Length / (float)Mathf.Max(1, lastMaxPower))
                ? Gold : new Color(0.12f, 0.21f, 0.3f);
    }

    public void ShowNotice(string title, string detail, Color accent, float duration = 2f, int priority = 2)
    {
        if (toastAge < toastDuration && priority < noticePriority) return;
        toastTitle.text = title;
        toastTitle.color = toastAccent.color = accent;
        toastDetail.text = detail;
        toastAge = 0f;
        toastDuration = Mathf.Max(0.5f, duration);
        noticePriority = priority;
    }

    private void AnimateNotice(float dt)
    {
        toastAge += dt;
        float enter = Mathf.Clamp01(toastAge / 0.18f);
        float exit = Mathf.Clamp01((toastDuration - toastAge) / 0.28f);
        toastGroup.alpha = Mathf.Min(enter, exit);
        toastRect.anchoredPosition = new Vector2(0f, -900f + (1f - enter) * 32f + (1f - exit) * 18f);
    }

    public void ShowCoinPickup(Vector3 worldPosition, int amount)
    {
        if (amount <= 0) return;
        if (coinLabel >= 0 && pickupLabels[coinLabel].gameObject.activeSelf && pickupAges[coinLabel] < 0.2f)
        {
            coinTotal += amount;
            pickupLabels[coinLabel].text = "+" + coinTotal + " COINS";
            return;
        }
        coinTotal = amount;
        coinLabel = ShowPickupLabel(worldPosition, "+" + amount + " COINS", Gold);
    }

    public int ShowPickupLabel(Vector3 worldPosition, string message, Color tint)
    {
        Camera camera = Camera.main;
        if (camera == null) return -1;
        int index = nextLabel++ % pickupLabels.Length;
        var root = (RectTransform)transform;
        var canvas = GetComponentInParent<Canvas>();
        Vector2 screen = camera.WorldToScreenPoint(worldPosition);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen,
            canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera, out Vector2 point);
        Rect bounds = root.rect;
        point.x = Mathf.Clamp(point.x, bounds.xMin + 260f, bounds.xMax - 260f);
        float bottom = bounds.yMin + Mathf.Min(450f, bounds.height * 0.2f);
        float top = Mathf.Max(bottom, bounds.yMax - Mathf.Min(1000f, bounds.height * 0.35f));
        point.y = Mathf.Clamp(point.y + 180f, bottom, top);
        pickupOrigins[index] = point;
        pickupAges[index] = 0f;
        var label = pickupLabels[index];
        label.text = message;
        label.color = tint;
        label.rectTransform.anchoredPosition = point;
        label.gameObject.SetActive(true);
        return index;
    }

    private void AnimatePickups(float dt)
    {
        for (int i = 0; i < pickupLabels.Length; i++)
        {
            var label = pickupLabels[i];
            if (!label.gameObject.activeSelf) continue;
            float t = (pickupAges[i] += dt) / 0.85f;
            if (t >= 1f) { label.gameObject.SetActive(false); continue; }
            label.rectTransform.anchoredPosition = pickupOrigins[i] + Vector2.up * (t * 160f);
            label.rectTransform.localScale = Vector3.one * (1f + 0.12f * Mathf.Sin(Mathf.Clamp01(t * 4f) * Mathf.PI));
            label.alpha = Mathf.Clamp01((1f - t) * 3f);
        }
    }

    private void RefreshBoss(bool playing)
    {
        if (Time.time >= bossScanAt)
        {
            bossScanAt = Time.time + 0.25f;
            if (boss == null || boss.IsDead)
            {
                boss = null;
                foreach (Enemy candidate in Enemy.ActiveEnemies)
                    if (candidate != null && !candidate.IsDead && candidate.TryGetComponent<BossController>(out _))
                    {
                        boss = candidate;
                        ShowNotice("BOSS DETECTED", "PRIORITY TARGET  /  STAY ALERT", Danger, 2.4f, 3);
                        break;
                    }
            }
        }
        bool visible = playing && boss != null && !boss.IsDead;
        if (bossRoot.gameObject.activeSelf != visible) bossRoot.gameObject.SetActive(visible);
        if (!visible) return;
        bossFill.rectTransform.anchorMax = new Vector2(boss.HealthRatio, 1f);
        int percent = Mathf.CeilToInt(boss.HealthRatio * 100f);
        bossLabel.SetText("BOSS  /  HULL {0}%", percent);
    }

    /// <summary>Called by the editor setup; references and hierarchy remain editable in the scene.</summary>
    public void Build(TMP_FontAsset font)
    {
        var root = (RectTransform)transform;
        Stretch(root);
        if (GetComponent<GalaxyStrike.UI.SafeAreaFitter>() == null)
            gameObject.AddComponent<GalaxyStrike.UI.SafeAreaFitter>();

        RectTransform status = Rect("ShipReadout", root, new Vector2(660f, 250f), new Vector2(82f, 115f), Vector2.zero);
        Image("Backing", status, status.sizeDelta, Vector2.zero, Panel);
        Image("TopTrim", status, new Vector2(660f, 3f), new Vector2(0f, 124f), new Color(0.12f, 0.5f, 0.65f, 0.7f));
        Image("BottomTrim", status, new Vector2(660f, 3f), new Vector2(0f, -124f), new Color(0.12f, 0.3f, 0.45f, 0.6f));
        hullAccent = Image("Accent", status, new Vector2(5f, 212f), new Vector2(-310f, 0f), Cyan);
        hullText = Text("Hull", status, new Vector2(590f, 65f), new Vector2(8f, 68f), 52f, font);
        hullText.text = "HULL  3 / 3";
        hullText.color = Cyan;
        powerText = Text("Firepower", status, new Vector2(590f, 65f), new Vector2(8f, -5f), 48f, font);
        PlayerController previewPlayer = FindFirstObjectByType<PlayerController>();
        int previewPower = previewPlayer != null ? previewPlayer.MaxBulletLevel : 5;
        powerText.text = "FIREPOWER  1 / " + previewPower;
        BuildPowerCells(status, previewPower);

        toastRect = Rect("CombatNotice", root, new Vector2(1320f, 210f), new Vector2(0f, -900f), new Vector2(0.5f, 1f));
        toastGroup = toastRect.gameObject.AddComponent<CanvasGroup>();
        toastGroup.blocksRaycasts = toastGroup.interactable = false;
        toastGroup.alpha = 0f;
        Image("Backing", toastRect, toastRect.sizeDelta, Vector2.zero, Panel);
        toastAccent = Image("Accent", toastRect, new Vector2(5f, 150f), new Vector2(-632f, 0f), Cyan);
        toastTitle = Text("Title", toastRect, new Vector2(1200f, 82f), new Vector2(16f, 40f), 76f, font);
        toastTitle.alignment = TextAlignmentOptions.Center;
        toastDetail = Text("Detail", toastRect, new Vector2(1200f, 62f), new Vector2(16f, -46f), 44f, font);
        toastDetail.alignment = TextAlignmentOptions.Center;
        toastDetail.color = new Color(0.73f, 0.85f, 0.94f);

        bossRoot = Rect("BossReadout", root, new Vector2(1400f, 125f), new Vector2(0f, -690f), new Vector2(0.5f, 1f));
        Image("Backing", bossRoot, bossRoot.sizeDelta, Vector2.zero, Panel);
        bossLabel = Text("BossHealth", bossRoot, new Vector2(1320f, 60f), new Vector2(0f, 23f), 48f, font);
        bossLabel.color = Danger;
        var track = Image("Track", bossRoot, new Vector2(1300f, 12f), new Vector2(0f, -33f), Muted);
        bossFill = Image("Fill", track.transform, Vector2.zero, Vector2.zero, Danger);
        Stretch(bossFill.rectTransform);
        bossRoot.gameObject.SetActive(false);

        var edges = Rect("DamageEdges", root, Vector2.zero, Vector2.zero, Vector2.one * 0.5f);
        Stretch(edges);
        edgeGroup = edges.gameObject.AddComponent<CanvasGroup>();
        edgeGroup.blocksRaycasts = edgeGroup.interactable = false;
        edgeGroup.alpha = 0f;
        Edge(edges, "Left", Vector2.zero, new Vector2(0f, 1f), new Vector2(18f, 0f));
        Edge(edges, "Right", new Vector2(1f, 0f), Vector2.one, new Vector2(18f, 0f));
        pickupLabels = new TextMeshProUGUI[8];
        for (int i = 0; i < pickupLabels.Length; i++)
        {
            pickupLabels[i] = Text("PickupLabel" + i, root, new Vector2(650f, 85f), Vector2.zero, 62f, font);
            pickupLabels[i].alignment = TextAlignmentOptions.Center;
            pickupLabels[i].outlineWidth = 0.22f;
            pickupLabels[i].outlineColor = new Color32(3, 12, 26, 255);
            pickupLabels[i].gameObject.SetActive(false);
        }
    }

    private static void Edge(Transform parent, string name, Vector2 min, Vector2 max, Vector2 size)
    {
        var image = Image(name, parent, size, Vector2.zero, Danger);
        image.rectTransform.anchorMin = min;
        image.rectTransform.anchorMax = max;
    }

    private void BuildPowerCells(Transform parent, int maximum)
    {
        powerCells = new UnityEngine.UI.Image[Mathf.Clamp(maximum, 1, 12)];
        float step = 560f / powerCells.Length;
        for (int i = 0; i < powerCells.Length; i++)
            powerCells[i] = Image("PowerCell" + i, parent, new Vector2(step - 14f, 14f),
                new Vector2(-280f + step * (i + 0.5f), -76f), i == 0 ? Gold : Muted);
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    private static RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 position, Vector2 anchor)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.gameObject.layer = 5;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        return rect;
    }

    private static UnityEngine.UI.Image Image(string name, Transform parent, Vector2 size, Vector2 position, Color tint)
    {
        var image = Rect(name, parent, size, position, Vector2.one * 0.5f).gameObject.AddComponent<UnityEngine.UI.Image>();
        image.color = tint;
        image.raycastTarget = false;
        return image;
    }

    private static TextMeshProUGUI Text(string name, Transform parent, Vector2 size, Vector2 position, float sizeInPixels, TMP_FontAsset font)
    {
        var text = Rect(name, parent, size, position, Vector2.one * 0.5f).gameObject.AddComponent<TextMeshProUGUI>();
        text.font = font;
        text.fontSize = sizeInPixels;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        text.color = new Color(0.9f, 0.96f, 1f);
        return text;
    }
}
