using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public abstract class SkillButton : MonoBehaviour, ICanvasRaycastFilter
{
    [FormerlySerializedAs("pulseIcon")]
    [SerializeField] private Image skillIcon;
    [SerializeField] private Image chargeRing;
    [SerializeField] private TextMeshProUGUI cooldownText;
    [SerializeField] private TMP_Text skillLabel;
    [SerializeField, Min(0.1f)] private float cooldown = 12f;

    protected Sprite RingSprite => chargeRing.sprite;

    private Button button;
    private float readyAt;
    private int displayedSeconds = -1;

    protected void Awake()
    {
        button = GetComponent<Button>();
    }

    protected void OnEnable()
    {
        button.onClick.AddListener(Activate);
    }

    protected void OnDisable()
    {
        button.onClick.RemoveListener(Activate);
    }

    protected void Update()
    {
        RefreshButton();
    }

    private void RefreshButton()
    {
        float remaining = Mathf.Max(0f, readyAt - Time.time);
        button.interactable = remaining <= 0f && CanActivate(PlayerController.Instance);
        chargeRing.fillAmount = 1f - Mathf.Clamp01(remaining / cooldown);

        int seconds = Mathf.CeilToInt(remaining);
        if (seconds == displayedSeconds)
            return;

        displayedSeconds = seconds;
        if (skillLabel != null)
            skillLabel.enabled = seconds == 0;
        cooldownText.enabled = seconds > 0;
        cooldownText.SetText("{0}", seconds);
    }

    private static bool CanActivate(PlayerController player)
    {
        return Time.timeScale > 0f && player != null && player.isActiveAndEnabled &&
               !player.IsDowned && (GameManager.Instance == null || !GameManager.Instance.IsRoundOver);
    }

    private void Activate()
    {
        PlayerController player = PlayerController.Instance;
        if (Time.time < readyAt || !CanActivate(player))
            return;

        readyAt = Time.time + cooldown;
        RefreshButton();
        ApplySkill(player);
    }

    protected abstract void ApplySkill(PlayerController player);

    public bool IsRaycastLocationValid(Vector2 screenPosition, Camera eventCamera)
    {
        var rectTransform = (RectTransform)transform;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rectTransform, screenPosition, eventCamera, out Vector2 localPoint))
            return false;

        Rect rect = rectTransform.rect;
        float hitRadius = Mathf.Min(rect.width, rect.height) * 0.48f;
        return (localPoint - rect.center).sqrMagnitude <= hitRadius * hitRadius;
    }
}
