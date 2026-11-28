using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Số sát thương bay lên tại chỗ đạn trúng quái.
///  - Dùng pool TextMeshPro (3D) cố định, không tạo/huỷ object khi đang bắn.
///  - Nhiều viên trúng cùng một quái trong thời gian ngắn được cộng dồn vào một số
///    (tránh cả màn hình toàn số "1" khi bắn nhanh), số nảy lên mỗi lần cộng thêm.
///  - Đòn kết liễu hiện màu vàng cam, to hơn.
/// Chạy theo Time.deltaTime nên đứng yên khi game tạm dừng.
/// </summary>
public sealed class DamageNumbers : MonoBehaviour
{
    private const int PoolSize = 28;
    private const float Lifetime = 0.75f;
    private const float MergeWindow = 0.14f;   // gộp các phát trúng cùng quái trong khoảng này
    private const float RiseDistance = 0.85f;  // đơn vị world (camera ortho size 5)
    private const float BaseFontSize = 2.1f;
    private const int SortingOrder = 60;       // trên VFX trúng đạn (30) và cảnh báo (50)

    private static readonly Color NormalColor = new Color(1f, 1f, 1f);
    private static readonly Color HeavyColor = new Color(1f, 0.93f, 0.45f);
    private static readonly Color KillColor = new Color(1f, 0.62f, 0.18f);

    private sealed class Label
    {
        public TextMeshPro text;
        public Transform transform;
        public Object owner;
        public Vector3 origin;
        public float age = Lifetime;
        public float lastHit;
        public float pop;
        public int value;
        public bool kill;
    }

    private static DamageNumbers instance;
    private readonly List<Label> labels = new List<Label>(PoolSize);
    private Material sharedMaterial;
    private int nextIndex;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => instance = null;

    /// <summary>Hiện (hoặc cộng dồn) số sát thương của <paramref name="owner"/> tại điểm trúng.</summary>
    public static void Show(Object owner, int damage, Vector3 position, bool killingBlow)
    {
        if (damage <= 0 || !Application.isPlaying)
            return;

        if (instance == null)
        {
            var go = new GameObject("[DamageNumbers]");
            instance = go.AddComponent<DamageNumbers>();
            instance.Build();
        }
        instance.Spawn(owner, damage, position, killingBlow);
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
        if (sharedMaterial != null)
            Destroy(sharedMaterial);
    }

    private void Build()
    {
        TMP_FontAsset font = TMP_Settings.defaultFontAsset;
        if (font != null && font.material != null)
        {
            // Một material dùng chung có viền đen để số đọc được trên mọi nền/hiệu ứng nổ.
            sharedMaterial = new Material(font.material) { name = "DamageNumbers (Outline)" };
            sharedMaterial.EnableKeyword("OUTLINE_ON");
            sharedMaterial.SetFloat("_OutlineWidth", 0.28f);
            sharedMaterial.SetColor("_OutlineColor", new Color(0.05f, 0.02f, 0.1f, 1f));
            sharedMaterial.SetFloat("_FaceDilate", 0.12f);
        }

        for (int i = 0; i < PoolSize; i++)
        {
            var go = new GameObject("DamageNumber", typeof(TextMeshPro));
            go.transform.SetParent(transform, false);
            var text = go.GetComponent<TextMeshPro>();
            if (font != null) text.font = font;
            if (sharedMaterial != null) text.fontSharedMaterial = sharedMaterial;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.rectTransform.sizeDelta = new Vector2(4f, 1.2f);
            text.sortingOrder = SortingOrder;
            text.raycastTarget = false;
            go.SetActive(false);
            labels.Add(new Label { text = text, transform = go.transform });
        }
    }

    private void Spawn(Object owner, int damage, Vector3 position, bool kill)
    {
        float now = Time.time;

        // Cộng dồn vào số đang hiện của cùng quái nếu vừa trúng xong.
        if (owner != null)
        {
            foreach (Label label in labels)
            {
                if (label.owner == owner && label.age < Lifetime && now - label.lastHit <= MergeWindow)
                {
                    label.value += damage;
                    label.kill |= kill;
                    label.lastHit = now;
                    label.age = Mathf.Min(label.age, 0.12f); // kéo dài thời gian hiện
                    label.pop = 1f;
                    Apply(label);
                    return;
                }
            }
        }

        Label slot = labels[nextIndex];
        nextIndex = (nextIndex + 1) % labels.Count;

        slot.owner = owner;
        slot.value = damage;
        slot.kill = kill;
        slot.age = 0f;
        slot.lastHit = now;
        slot.pop = 1f;
        slot.origin = position + new Vector3(Random.Range(-0.18f, 0.18f), 0.15f, 0f);
        slot.origin.z = -1f;
        slot.transform.position = slot.origin;
        slot.text.gameObject.SetActive(true);
        Apply(slot);
    }

    private static void Apply(Label label)
    {
        label.text.text = label.value.ToString();
        label.text.color = label.kill ? KillColor : label.value >= 5 ? HeavyColor : NormalColor;
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f)
            return;

        foreach (Label label in labels)
        {
            if (label.age >= Lifetime)
                continue;

            label.age += dt;
            if (label.age >= Lifetime)
            {
                label.owner = null;
                label.text.gameObject.SetActive(false);
                continue;
            }

            float t = label.age / Lifetime;
            label.pop = Mathf.MoveTowards(label.pop, 0f, dt * 7f);

            // Bay lên nhanh rồi chậm dần; mờ dần ở 40% cuối.
            float rise = 1f - (1f - t) * (1f - t);
            label.transform.position = label.origin + new Vector3(0f, rise * RiseDistance, 0f);

            float size = BaseFontSize * (label.kill ? 1.3f : 1f) * (1f + 0.35f * label.pop);
            label.text.fontSize = size;

            Color c = label.text.color;
            c.a = t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f;
            label.text.color = c;
        }
    }
}
