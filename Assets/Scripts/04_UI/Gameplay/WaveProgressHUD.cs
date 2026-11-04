using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Live wave milestones. END is reached only when the spawner clears the stage.</summary>
public class WaveProgressHUD : MonoBehaviour
{
    [SerializeField] private Image progressFill;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private RectTransform milestoneRoot;
    [SerializeField] private List<Image> milestones = new List<Image>();
    [SerializeField] private int displayedWaveCount;
    private EnemySpawner spawner;
    private readonly Color cyan = new Color(0.05f, 0.9f, 1f);
    private readonly Color gold = new Color(1f, 0.8f, 0.2f);

    private void Start()
    {
        spawner = FindObjectOfType<EnemySpawner>();
    }

    private void Update()
    {
        if (spawner == null || spawner.WaveCount == 0) return;
        if (displayedWaveCount != spawner.WaveCount) BuildMilestones(spawner.WaveCount);
        ApplyProgress(spawner.CurrentWaveNumber, spawner.StageWaveProgress, spawner.IsStageComplete);
    }

    public void Build(Sprite frameSprite, int previewWaveCount)
    {
        RectTransform root = GetComponent<RectTransform>();
        root.anchorMin = root.anchorMax = new Vector2(0.5f, 1f);
        root.pivot = new Vector2(0.5f, 0.5f);
        root.anchoredPosition = new Vector2(0f, -570f);
        root.sizeDelta = new Vector2(1560f, 230f);

        Image frame = MakeImage("GeneratedFrame", root, new Vector2(1560f, 130f), Vector2.zero, Color.white);
        frame.sprite = frameSprite;
        frame.preserveAspect = false;

        progressFill = MakeImage("ProgressFill", root, new Vector2(1400f, 38f), Vector2.zero, cyan);
        // The generated sprite's recessed center remains visible around the live fill.
        progressFill.rectTransform.pivot = new Vector2(0f, 0.5f);
        progressFill.rectTransform.anchoredPosition = new Vector2(-700f, 0f);
        statusText = MakeText("WaveStatus", root, new Vector2(1400f, 65f), new Vector2(0f, 100f), 44f);
        milestoneRoot = MakeRect("WaveMilestones", root, new Vector2(1400f, 1f), Vector2.zero);
        BuildMilestones(Mathf.Max(1, previewWaveCount));
        ApplyProgress(1, 0f, false);
    }

    private void BuildMilestones(int count)
    {
        for (int i = milestoneRoot.childCount - 1; i >= 0; i--)
        {
            GameObject child = milestoneRoot.GetChild(i).gameObject;
            if (Application.isPlaying) Destroy(child); else DestroyImmediate(child);
        }
        milestones.Clear();
        displayedWaveCount = count;
        for (int i = 0; i <= count; i++)
        {
            float x = Mathf.Lerp(-700f, 700f, i / (float)count);
            Image marker = MakeImage(i == count ? "FinishMarker" : "Wave_" + (i + 1),
                milestoneRoot, new Vector2(24f, 24f), new Vector2(x, 0f), cyan);
            marker.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            milestones.Add(marker);
            TextMeshProUGUI label = MakeText("Label_" + i, milestoneRoot,
                new Vector2(130f, 60f), new Vector2(x, -87f), 42f);
            label.text = i == count ? "END" : (i + 1).ToString();
            label.color = i == count ? gold : Color.white;
        }
    }

    private void ApplyProgress(int wave, float progress, bool complete)
    {
        if (progressFill != null)
            progressFill.rectTransform.sizeDelta = new Vector2(1400f * Mathf.Clamp01(progress), 38f);
        if (statusText != null) statusText.text = complete ? "STAGE CLEAR" : "WAVE " + wave + " / " + displayedWaveCount;
        for (int i = 0; i < milestones.Count; i++)
        {
            if (milestones[i] == null) continue;
            bool reached = complete || i < wave - 1;
            bool current = !complete && i == wave - 1;
            milestones[i].color = current ? gold : reached ? cyan : new Color(0.18f, 0.35f, 0.5f);
        }
    }

    private static RectTransform MakeRect(string objectName, Transform parent, Vector2 size, Vector2 position)
    {
        GameObject obj = new GameObject(objectName, typeof(RectTransform));
        obj.layer = 5;
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        return rect;
    }

    private static Image MakeImage(string objectName, Transform parent, Vector2 size, Vector2 position, Color color)
    {
        RectTransform rect = MakeRect(objectName, parent, size, position);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static TextMeshProUGUI MakeText(string objectName, Transform parent, Vector2 size, Vector2 position, float fontSize)
    {
        RectTransform rect = MakeRect(objectName, parent, size, position);
        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = TMP_Settings.defaultFontAsset;
        text.fontSize = fontSize;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.enableWordWrapping = false;
        text.raycastTarget = false;
        text.color = Color.white;
        return text;
    }
}
